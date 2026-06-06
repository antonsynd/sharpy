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
        generated.Should().Contain("return _DebugMode");
        generated.Should().Contain("_DebugMode = value");
    }

    [Fact]
    public void AutoProperty_EmitsStaticAutoPropertyWithInitializer()
    {
        var source = @"
property default_timeout: float = 30.0

def main():
    pass
";
        var result = _api.Compile(source);

        result.Success.Should().BeTrue();
        result.GeneratedCSharp.Should().Contain("public static double DefaultTimeout { get; set; } = 30.0d;");
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
