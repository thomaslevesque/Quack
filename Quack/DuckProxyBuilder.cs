using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace Quack
{
    static class DuckProxyBuilder
    {
        private static readonly ModuleBuilder _moduleBuilder;

        private static readonly ConcurrentDictionary<Tuple<Type, Type>, Type> _proxyTypes =
            new ConcurrentDictionary<Tuple<Type, Type>, Type>();

        static DuckProxyBuilder()
        {
            var aName = new AssemblyName("Quack.DuckProxies");
            AssemblyBuilder assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(aName, AssemblyBuilderAccess.Run);
            _moduleBuilder = assemblyBuilder.DefineDynamicModule(aName.Name);
        }

        public static T GetDuckProxy<T>(object target)
        {
            var interfaceType = typeof(T);
            var targetType = target.GetType();
            var key = Tuple.Create(interfaceType, targetType);
            var type = _proxyTypes.GetOrAdd(key, k => BuildProxyType(interfaceType, targetType));
            var ctor = type.GetConstructors().Single();
            return (T)ctor.Invoke(new[] { target });
        }

        private static Type BuildProxyType(Type interfaceType, Type targetType)
        {
            var matches = TryMatch(interfaceType, targetType);
            var missingMembers = matches.Where(m => !m.Success).Select(m => m.InterfaceMember).ToList();
            if (missingMembers.Any())
            {
                var missingMembersText = string.Join(Environment.NewLine, missingMembers);
                throw new ArgumentException(
                    "The target object doesn't implement the following members: " + Environment.NewLine + missingMembersText);
            }

            string proxyTypeName = string.Format("Quack.DuckProxies.{0}DuckProxy_{1}_{2}", interfaceType.Name, interfaceType.MetadataToken, targetType.MetadataToken);
            var proxyType = _moduleBuilder.DefineType(proxyTypeName);
            proxyType.AddInterfaceImplementation(interfaceType);

            var field = proxyType.DefineField("_target", targetType, FieldAttributes.InitOnly);

            BuildConstructor(targetType, proxyType, field);

            // members
            foreach (var match in matches)
            {
                switch (match.InterfaceMember.MemberType)
                {
                    case MemberTypes.Event:
                        BuildDelegatingEvent(proxyType, match, field);
                        break;
                    case MemberTypes.Method:
                        BuildDelegatingMethod(proxyType, match, field);
                        break;
                    case MemberTypes.Property:
                        BuildDelegatingProperty(proxyType, match, field);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            return proxyType.CreateType();
        }

        private static ConstructorBuilder BuildConstructor(Type targetType, TypeBuilder proxyType, FieldBuilder field)
        {
            var ctor = proxyType.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, new[] { targetType });
            var il = ctor.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0); // this
            // ReSharper disable once AssignNullToNotNullAttribute
            il.Emit(OpCodes.Call, typeof(object).GetConstructor(Type.EmptyTypes)); // : base()
            il.Emit(OpCodes.Ldarg_0); // this
            il.Emit(OpCodes.Ldarg_1); // target
            il.Emit(OpCodes.Stfld, field); // this._target = target;
            il.Emit(OpCodes.Ret);
            return ctor;
        }

        private static EventBuilder BuildDelegatingEvent(TypeBuilder proxyType, MemberMatch match, FieldInfo field)
        {
            throw new NotImplementedException();
        }

        private static PropertyBuilder BuildDelegatingProperty(TypeBuilder proxyType, MemberMatch match, FieldInfo field)
        {
            var interfaceProp = (PropertyInfo)match.InterfaceMember;
            var targetProp = (PropertyInfo)match.TargetMember;

            var parameters = interfaceProp.GetIndexParameters();
            var paramTypes = parameters.Select(p => p.ParameterType).ToArray();

            Type[] returnRequiredCustomModifiers = interfaceProp.GetRequiredCustomModifiers();
            Type[] returnOptionalCustomModifiers = interfaceProp.GetOptionalCustomModifiers();

            var paramRequiredCustomModifiers = parameters.Select(p => p.GetRequiredCustomModifiers()).ToArray();
            var paramOptionalCustomModifiers = parameters.Select(p => p.GetOptionalCustomModifiers()).ToArray();

            var prop = proxyType.DefineProperty(
                interfaceProp.Name,
                PropertyAttributes.None,
                CallingConventions.Standard,
                interfaceProp.PropertyType,
                returnRequiredCustomModifiers,
                returnOptionalCustomModifiers,
                paramTypes,
                paramRequiredCustomModifiers,
                paramOptionalCustomModifiers);

            if (interfaceProp.GetMethod != null)
            {
                var getter = BuildDelegatingMethod(proxyType, new MemberMatch(interfaceProp.GetMethod, targetProp.GetMethod), field);
                prop.SetGetMethod(getter);
            }
            if (interfaceProp.SetMethod != null)
            {
                var setter = BuildDelegatingMethod(proxyType, new MemberMatch(interfaceProp.SetMethod, targetProp.SetMethod), field);
                prop.SetSetMethod(setter);
            }
            return prop;
        }

        private static MethodBuilder BuildDelegatingMethod(TypeBuilder proxyType, MemberMatch match, FieldInfo field)
        {
            var interfaceMethod = (MethodInfo)match.InterfaceMember;
            var parameters = interfaceMethod.GetParameters();
            var paramTypes = parameters.Select(p => p.ParameterType).ToArray();
            Type[] returnRequiredCustomModifiers = null;
            Type[] returnOptionalCustomModifiers = null;
            if (interfaceMethod.ReturnParameter != null)
            {
                returnRequiredCustomModifiers = interfaceMethod.ReturnParameter.GetRequiredCustomModifiers();
                returnOptionalCustomModifiers = interfaceMethod.ReturnParameter.GetOptionalCustomModifiers();
            }
            var paramRequiredCustomModifiers = parameters.Select(p => p.GetRequiredCustomModifiers()).ToArray();
            var paramOptionalCustomModifiers = parameters.Select(p => p.GetOptionalCustomModifiers()).ToArray();
            var method = proxyType.DefineMethod(
                match.InterfaceMember.Name,
                MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final,
                CallingConventions.HasThis,
                interfaceMethod.ReturnType,
                returnRequiredCustomModifiers,
                returnOptionalCustomModifiers,
                paramTypes,
                paramRequiredCustomModifiers,
                paramOptionalCustomModifiers);

            proxyType.DefineMethodOverride(method, interfaceMethod);

            var il = method.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, field);
            for (int i = 0; i < parameters.Length; i++)
            {
                il.Emit(OpCodes.Ldarg, i + 1);
            }
            il.Emit(OpCodes.Call, (MethodInfo)match.TargetMember);
            il.Emit(OpCodes.Ret);

            return method;
        }

        static IList<MemberMatch> TryMatch(Type interfaceType, Type targetType)
        {
            var flags = BindingFlags.Public | BindingFlags.Instance;
            var memberTypeMask = MemberTypes.Method | MemberTypes.Property | MemberTypes.Event;
            Func<MemberInfo, bool> filter = m => (m.MemberType | memberTypeMask) != 0;
            var interfaceMembers = interfaceType.GetMembers(flags).Where(filter);
            var targetMembers = targetType.GetMembers(flags).Where(filter);
            return
                interfaceMembers
                    .GroupJoin(targetMembers, im => im, tm => tm, (im, tmp) => new { im, tmp }, new MemberCompatibilityComparer())
                    .SelectMany(_ => _.tmp.DefaultIfEmpty(), (_, tm) => new MemberMatch(_.im, tm))
                    .ToList();
        }

        class MemberMatch
        {
            private readonly MemberInfo _interfaceMember;
            private readonly MemberInfo _targetMember;

            public MemberMatch(MemberInfo interfaceMember, MemberInfo targetMember)
            {
                _interfaceMember = interfaceMember;
                _targetMember = targetMember;
            }

            public MemberInfo InterfaceMember
            {
                get { return _interfaceMember; }
            }

            public MemberInfo TargetMember
            {
                get { return _targetMember; }
            }

            public bool Success
            {
                get { return _targetMember != null; }
            }
        }

        class MemberCompatibilityComparer : IEqualityComparer<MemberInfo>
        {
            public bool Equals(MemberInfo x, MemberInfo y)
            {
                if (x.Name != y.Name)
                    return false;
                if (x.MemberType != y.MemberType)
                    return false;
                switch (x.MemberType)
                {
                    case MemberTypes.Event:
                        {
                            var evtX = (EventInfo)x;
                            var evtY = (EventInfo)y;
                            if (evtX.EventHandlerType != evtY.EventHandlerType)
                                return false;
                            break;
                        }
                    case MemberTypes.Method:
                        {
                            var mx = (MethodInfo)x;
                            var my = (MethodInfo)y;
                            if (mx.ReturnType != my.ReturnType)
                                return false;
                            if (!ParametersEqual(mx, my))
                                return false;
                            break;
                        }
                    case MemberTypes.Property:
                        {
                            var propX = (PropertyInfo)x;
                            var propY = (PropertyInfo)y;
                            if (propX.PropertyType != propY.PropertyType)
                                return false;
                            if (propX.GetMethod != null && propY.GetMethod == null)
                                return false;
                            if (propX.SetMethod != null && propY.SetMethod == null)
                                return false;
                            break;
                        }
                    default:
                        throw new ArgumentOutOfRangeException();
                }
                return true;
            }

            private static bool ParametersEqual(MethodBase x, MethodBase y)
            {
                var paramsX = x.GetParameters();
                var paramsY = y.GetParameters();
                if (paramsX.Length != paramsY.Length)
                    return false;
                return paramsX.Zip(paramsY, (px, py) => px.ParameterType == py.ParameterType && px.IsOut == py.IsOut)
                              .All(eq => eq);
            }

            public int GetHashCode(MemberInfo obj)
            {
                int hashCode = (obj.Name.GetHashCode() * 397) ^ obj.MemberType.GetHashCode();
                return hashCode;
            }
        }
    }
}
