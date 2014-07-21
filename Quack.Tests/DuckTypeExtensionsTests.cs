using FluentAssertions;
using NUnit.Framework;

namespace Quack.Tests
{
    [TestFixture]
    public class DuckTypeExtensionsTests
    {
        public interface IFooWithMethods
        {
            // Void, no params
            void Test1();
            // Void, 1 value-type param
            void Test1(int x);
            // Void, 1 ref-type param
            void Test1(string x);
            // Void, 2 params
            void Test1(int x, string y);

            // Value-type return value
            int Test2();
            // Ref-type return value
            string Test3();

            // byref value-type param
            void Test4(ref int x);
            // byref ref-type param
            void Test4(ref string x);

            // out value-type param
            void Test5(out int x);
            // out ref-type param
            void Test5(out string x);
        }

        public class FooWithMethods
        {
            public void Test1()
            {
            }

            public void Test1(int x)
            {
            }

            public void Test1(string x)
            {
            }

            public void Test1(int x, string y)
            {
            }

            public int Test2()
            {
                return 42;
            }

            public string Test3()
            {
                return "Hello world";
            }

            public void Test4(ref int x)
            {
                x += 42;
            }

            public void Test4(ref string x)
            {
                x += "Hello world";
            }

            public void Test5(out int x)
            {
                x = 42;
            }

            public void Test5(out string x)
            {
                x = "Hello world";
            }
        }

        [Test]
        public void DuckTypeAs_Returns_Working_Proxy_With_Methods()
        {
            var target = new FooWithMethods();
            var foo = target.DuckTypeAs<IFooWithMethods>();
            foo.Test1();
            foo.Test1(42);
            foo.Test1("hello");
            foo.Test1(42, "hello");
            foo.Test2().Should().Be(42);
            foo.Test3().Should().Be("Hello world");
            
            int x = 0;
            foo.Test4(ref x);
            x.Should().Be(42);

            string s = "";
            foo.Test4(ref s);
            s.Should().Be("Hello world");

            x = 0;
            foo.Test5(out x);
            x.Should().Be(42);

            s = "";
            foo.Test5(out s);
            s.Should().Be("Hello world");
        }
    }
}
