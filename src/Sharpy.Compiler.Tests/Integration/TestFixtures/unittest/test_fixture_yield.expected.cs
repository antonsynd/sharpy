#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Xunit;
using static TestFixtureYield;

public static partial class TestFixtureYield
{
    public static void Main()
    {
#line (13, 5) - (13, 16) 8 "test_fixture_yield.spy"
        global::Sharpy.Builtins.Print("ok");
#line hidden
    }
}

public class CounterFixture : global::System.IDisposable
{
    public Sharpy.List<int> Value { get; private set; } = default!;

    private global::System.Action? _teardown;
    public CounterFixture()
    {
#line (3, 5) - (3, 27) 8 "test_fixture_yield.spy"
        Sharpy.List<int> data = new Sharpy.List<int>()
#line hidden
        {
            0
        };
        Value = data;
        _teardown = () =>
        {
#line (5, 5) - (5, 17) 12 "test_fixture_yield.spy"
            data.Clear();
#line hidden
        };
    }

    public void Dispose()
    {
        _teardown?.Invoke();
    }
}

public partial class TestFixtureYieldTests : Xunit.IClassFixture<CounterFixture>
{
    private readonly CounterFixture _counterFixture;
    public TestFixtureYieldTests(CounterFixture counterFixture)
    {
        _counterFixture = counterFixture;
    }

    [Xunit.FactAttribute]
    public void TestCounterAppend()
    {
        Sharpy.List<int> counter = _counterFixture.Value;
#line (9, 5) - (9, 22) 8 "test_fixture_yield.spy"
        counter.Append(1);
#line (10, 5) - (10, 30) 8 "test_fixture_yield.spy"
        Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(counter));
#line hidden
    }
}
#line default
