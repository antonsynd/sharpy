#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Xunit;

namespace TestFixtureBasic
{
    public static partial class TestFixtureBasicModule
    {
        public static void Main()
        {
#line (14, 5) - (14, 16) 12 "test_fixture_basic.spy"
            global::Sharpy.Builtins.Print("ok");
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

    public partial class TestFixtureBasicModuleTests : Xunit.IClassFixture<GreetingFixture>
    {
        private readonly GreetingFixture _greetingFixture;
        public TestFixtureBasicModuleTests(GreetingFixture greetingFixture)
        {
            _greetingFixture = greetingFixture;
        }

        [Xunit.FactAttribute]
        public void TestUsesGreeting()
        {
            string greeting = _greetingFixture.Value;
#line (7, 5) - (7, 32) 12 "test_fixture_basic.spy"
            Xunit.Assert.Equal("hello", greeting);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestGreetingUpper()
        {
            string greeting = _greetingFixture.Value;
#line (11, 5) - (11, 40) 12 "test_fixture_basic.spy"
            Xunit.Assert.Equal("HELLO", global::Sharpy.StringExtensions.Upper(greeting));
#line hidden
        }
    }
}
#line default
