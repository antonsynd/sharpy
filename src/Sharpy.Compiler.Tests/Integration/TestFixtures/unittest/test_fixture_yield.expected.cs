#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Xunit;

public static partial class TestFixtureYield
{
    public static void Main()
    {
#line (13, 5) - (13, 16) 1 "test_fixture_yield.spy"
        global::Sharpy.Builtins.Print("ok");
    }
}

public class CounterFixture : System.IDisposable
{
    public Sharpy.List<int> Value { get; private set; } = default!;

    private System.Action? _teardown;
    public CounterFixture()
    {
#line (3, 5) - (3, 27) 1 "test_fixture_yield.spy"
        Sharpy.List<int> data = new Sharpy.List<int>()
        {
            0
        };
        Value = data;
        _teardown = () =>
        {
#line (5, 5) - (5, 17) 1 "test_fixture_yield.spy"
            data.Clear();
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
#line (9, 5) - (9, 22) 1 "test_fixture_yield.spy"
        counter.Append(1);
#line (10, 5) - (10, 30) 1 "test_fixture_yield.spy"
        Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(counter));
    }
}
