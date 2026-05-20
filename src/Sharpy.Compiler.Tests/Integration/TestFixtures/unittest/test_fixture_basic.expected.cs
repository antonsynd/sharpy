#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Xunit;

public static partial class TestFixtureBasic
{
    public static void Main()
    {
#line (14, 5) - (14, 16) 1 "test_fixture_basic.spy"
        global::Sharpy.Builtins.Print("ok");
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

public partial class TestFixtureBasicTests : Xunit.IClassFixture<GreetingFixture>
{
    private readonly GreetingFixture _greetingFixture;
    public TestFixtureBasicTests(GreetingFixture greetingFixture)
    {
        _greetingFixture = greetingFixture;
    }

    [Xunit.FactAttribute]
    public void TestUsesGreeting()
    {
        string greeting = _greetingFixture.Value;
#line (7, 5) - (7, 32) 1 "test_fixture_basic.spy"
        Xunit.Assert.Equal("hello", greeting);
    }

    [Xunit.FactAttribute]
    public void TestGreetingUpper()
    {
        string greeting = _greetingFixture.Value;
#line (11, 5) - (11, 40) 1 "test_fixture_basic.spy"
        Xunit.Assert.Equal("HELLO", greeting.Upper());
    }
}
