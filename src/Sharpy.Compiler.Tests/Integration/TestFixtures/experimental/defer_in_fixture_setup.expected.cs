#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Xunit;
using static DeferInFixtureSetup;

public static partial class DeferInFixtureSetup
{
    public static void Main()
    {
#line (16, 5) - (16, 16) 8 "defer_in_fixture_setup.spy"
        global::Sharpy.Builtins.Print("ok");
#line hidden
    }
}

public class ResourceFixture : global::System.IDisposable
{
    public string Value { get; private set; } = default!;

    private global::System.Action? _teardown;
    public ResourceFixture()
    {
#line (3, 5) - (3, 42) 8 "defer_in_fixture_setup.spy"
        try
#line hidden
        {
#line (4, 5) - (4, 43) 12 "defer_in_fixture_setup.spy"
            try
#line hidden
            {
#line (5, 5) - (5, 19) 16 "defer_in_fixture_setup.spy"
                global::Sharpy.Builtins.Print("setup");
#line hidden
            }
            finally
            {
#line (4, 11) - (4, 42) 16 "defer_in_fixture_setup.spy"
                global::Sharpy.Builtins.Print("cleanup b (runs first)");
#line hidden
            }
        }
        finally
        {
#line (3, 11) - (3, 41) 12 "defer_in_fixture_setup.spy"
            global::Sharpy.Builtins.Print("cleanup a (runs last)");
#line hidden
        }

        Value = "the-resource";
        _teardown = () =>
        {
#line (7, 5) - (7, 22) 12 "defer_in_fixture_setup.spy"
            global::Sharpy.Builtins.Print("teardown");
#line hidden
        };
    }

    public void Dispose()
    {
        _teardown?.Invoke();
    }
}

public partial class DeferInFixtureSetupTests : Xunit.IClassFixture<ResourceFixture>
{
    private readonly ResourceFixture _resourceFixture;
    public DeferInFixtureSetupTests(ResourceFixture resourceFixture)
    {
        _resourceFixture = resourceFixture;
    }

    [Xunit.FactAttribute]
    public void TestUsesResource()
    {
        string resource = _resourceFixture.Value;
#line (12, 5) - (12, 39) 8 "defer_in_fixture_setup.spy"
        Xunit.Assert.Equal("the-resource", resource);
#line hidden
    }
}
#line default
