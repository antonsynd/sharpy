#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Xunit;

namespace TestFixtureClass
{
    public static partial class TestFixtureClassModule
    {
        public static void Main()
        {
#line (26, 5) - (26, 16) 12 "test_fixture_class.spy"
            global::Sharpy.Builtins.Print("ok");
#line hidden
        }
    }

    [global::Sharpy.SharpyModuleType("__main__", "TestGreetingCase")]
    public class TestGreetingCase
    {
        public TestGreetingCase()
        {
            Setup();
        }

        public int Value;
        private void Setup()
#line 10 "test_fixture_class.spy"
        {
#line (11, 9) - (11, 24) 12 "test_fixture_class.spy"
            this.Value = 99;
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestValue()
#line 14 "test_fixture_class.spy"
        {
#line (15, 9) - (15, 33) 12 "test_fixture_class.spy"
            Xunit.Assert.Equal(99, this.Value);
#line hidden
        }
    }

    public class GreetingFixture
    {
        public string Value { get; private set; } = default!;

        public GreetingFixture()
        {
            Value = "hello";
        }
    }

    public partial class TestFixtureClassModuleTests : Xunit.IClassFixture<GreetingFixture>
    {
        private readonly GreetingFixture _greetingFixture;
        public TestFixtureClassModuleTests(GreetingFixture greetingFixture)
        {
            _greetingFixture = greetingFixture;
        }

        [Xunit.FactAttribute]
        public void TestUsesGreeting()
        {
            string greeting = _greetingFixture.Value;
#line (19, 5) - (19, 32) 12 "test_fixture_class.spy"
            Xunit.Assert.Equal("hello", greeting);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestGreetingLength()
        {
            string greeting = _greetingFixture.Value;
#line (23, 5) - (23, 31) 12 "test_fixture_class.spy"
            Xunit.Assert.Equal(5, greeting.Length);
#line hidden
        }
    }
}
#line default
