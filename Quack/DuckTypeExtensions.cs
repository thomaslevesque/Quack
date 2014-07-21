using System;

namespace Quack
{
    public static class DuckTypeExtensions
    {
        public static T DuckTypeAs<T>(this object target)
        {
            if (target == null)
                throw new ArgumentNullException("target");
            if (!typeof(T).IsInterface)
                throw new NotSupportedException("T must be an interface type");

            return DuckProxyBuilder.GetDuckProxy<T>(target);
        }
    }
}
