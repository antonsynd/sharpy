#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Xunit;

public static partial class AsyncFixtureReturn
{
    public static void Main()
    {
#line (10, 5) - (10, 16) 1 "async_fixture_return.spy"
        global::Sharpy.Builtins.Print("ok");
    }
}

public class ResourceFixture : Xunit.IAsyncLifetime
{
    public int Value { get; private set; } = default!;

    public async System.Threading.Tasks.Task InitializeAsync()
    {
        Value = 42;
        await global::System.Threading.Tasks.Task.CompletedTask;
    }

    public System.Threading.Tasks.Task DisposeAsync()
    {
        return global::System.Threading.Tasks.Task.CompletedTask;
    }
}

public partial class AsyncFixtureReturnTests : Xunit.IClassFixture<ResourceFixture>
{
    private readonly ResourceFixture _resourceFixture;
    public AsyncFixtureReturnTests(ResourceFixture resourceFixture)
    {
        _resourceFixture = resourceFixture;
    }

    [Xunit.FactAttribute]
    public async System.Threading.Tasks.Task TestUsesResource()
    {
        int resource = _resourceFixture.Value;
#line (7, 5) - (7, 27) 1 "async_fixture_return.spy"
        Xunit.Assert.Equal(42, resource);
    }
}
