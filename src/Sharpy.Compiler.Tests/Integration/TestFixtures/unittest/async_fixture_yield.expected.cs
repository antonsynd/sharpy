#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Xunit;
using static AsyncFixtureYield;

public static partial class AsyncFixtureYield
{
    public static void Main()
    {
#line (12, 5) - (12, 16) 1 "async_fixture_yield.spy"
        global::Sharpy.Builtins.Print("ok");
    }
}

public class ResourceFixture : Xunit.IAsyncLifetime
{
    public int Value { get; private set; } = default!;

    private global::System.Func<System.Threading.Tasks.Task>? _teardown;
    public async System.Threading.Tasks.Task InitializeAsync()
    {
#line (3, 5) - (3, 21) 1 "async_fixture_yield.spy"
        int value = 42;
        Value = value;
        _teardown = async () =>
        {
#line (5, 5) - (5, 14) 1 "async_fixture_yield.spy"
            value = 0;
            await global::System.Threading.Tasks.Task.CompletedTask;
        };
        await global::System.Threading.Tasks.Task.CompletedTask;
    }

    public async System.Threading.Tasks.Task DisposeAsync()
    {
        if (_teardown != null)
        {
            await _teardown();
        }
    }
}

public partial class AsyncFixtureYieldTests : Xunit.IClassFixture<ResourceFixture>
{
    private readonly ResourceFixture _resourceFixture;
    public AsyncFixtureYieldTests(ResourceFixture resourceFixture)
    {
        _resourceFixture = resourceFixture;
    }

    [Xunit.FactAttribute]
    public async System.Threading.Tasks.Task TestUsesResource()
    {
        int resource = _resourceFixture.Value;
#line (9, 5) - (9, 27) 1 "async_fixture_yield.spy"
        Xunit.Assert.Equal(42, resource);
    }
}
