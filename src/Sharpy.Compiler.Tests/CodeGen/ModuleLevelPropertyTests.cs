using FluentAssertions;
using Xunit;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// Tests for module-level property emission (#844). Module-level PropertyDef
/// nodes are emitted as static C# properties on the module class; split
/// get/set declarations for the same name merge into a single property.
/// Assertions are on the raw generated C# text.
/// </summary>
public class ModuleLevelPropertyTests
{
    private readonly CompilerApi _api = new();

    [Fact]
    public void FunctionStyleGetter_EmitsStaticReadOnlyProperty()
    {
        var source = @"
property get answer() -> int:
    return 42

def main():
    pass
";
        var result = _api.Compile(source);

        result.Success.Should().BeTrue();
        result.GeneratedCSharp.Should().Contain("public static int Answer");
        result.GeneratedCSharp.Should().NotContain("set");
    }

    [Fact]
    public void SplitGetterSetter_MergeIntoSingleStaticProperty()
    {
        var source = @"
_debug_mode: bool = False

property get debug_mode() -> bool:
    return _debug_mode

property set debug_mode(value: bool):
    _debug_mode = value

def main():
    pass
";
        var result = _api.Compile(source);

        result.Success.Should().BeTrue();
        var generated = result.GeneratedCSharp!;
        generated.Should().Contain("public static bool DebugMode");
        // Merged: exactly one property declaration for the name
        generated.IndexOf("bool DebugMode", System.StringComparison.Ordinal)
            .Should().Be(generated.LastIndexOf("bool DebugMode", System.StringComparison.Ordinal));
        // #1683: a same-module backing field is now emitted global::-qualified through its module class
        // (#2039: namespace Source, members class SourceModule).
        generated.Should().Contain("return global::Source.SourceModule._DebugMode");
        // plan-775c50 D4: `_debug_mode` is a Guarded module variable, so the setter's plain store is its `__set`.
        generated.Should().Contain("_DebugMode__set(value)");
    }

    [Fact]
    public void AutoProperty_EmitsStaticPropertyWithGuardedDefault()
    {
        var source = @"
property default_timeout: float = 30.0

def main():
    pass
";
        var result = _api.Compile(source);

        result.Success.Should().BeTrue();
        // plan-775c50 D4: a property default is Guarded — no initializer on the storage; the default is
        // assigned by its init method, run at its position in the module's sweep, and the accessors
        // keep get / set through the readiness prelude and store.
        var generated = result.GeneratedCSharp!;
        generated.Should().Contain("public static double DefaultTimeout");
        generated.Should().NotContain("} = 30.0d;");
        generated.Should().Contain("DefaultTimeout__field = 30.0d;");
        generated.Should().Contain("global::Sharpy.StaticInit.Run(ref DefaultTimeout__state, \"default_timeout\", DefaultTimeout__init);");
        generated.Should().Contain("global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref DefaultTimeout__state, ref DefaultTimeout__field, value);");
    }

    [Fact]
    public void PrivateDecorator_EmitsPrivateStaticProperty()
    {
        var source = @"
@private
property get secret() -> str:
    return ""hidden""

def main():
    pass
";
        var result = _api.Compile(source);

        result.Success.Should().BeTrue();
        result.GeneratedCSharp.Should().Contain("private static string Secret");
    }
}
