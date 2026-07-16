using System.Linq;
using FluentAssertions;
using Sharpy.Compiler.Shared;
using Xunit;

namespace Sharpy.Compiler.Tests.Lowering;

/// <summary>
/// Tests for the E3 stack-collections pass (<c>opt_stack_collections</c>, #1057). v1 lowers a list
/// literal whose only use is as a <c>for</c>-statement iterator to a raw array (<c>new T[] { ... }</c>)
/// instead of the <c>Sharpy.List</c> wrapper. The escape rule is purely syntactic: an assigned,
/// returned, or otherwise-escaping literal is left as a wrapper, and the pass is inert when disabled.
/// </summary>
public class StackCollectionsPassTests
{
    private readonly CompilerApi _api = new();

    private string CompileCSharp(string source, bool stack)
    {
        var options = new CompilerOptions
        {
            OutputType = "library",
            Features = stack ? FeatureFlags.None.Enable("opt_stack_collections") : FeatureFlags.None,
        };
        var result = _api.Compile(source, options);
        result.Success.Should().BeTrue(
            string.Join("\n", result.Diagnostics.Select(d => $"[{d.Code}] {d.Message}")));
        return (result.GeneratedCSharp ?? "").Replace(" ", "");
    }

    // `new int[]` (the array creation header) space-strips to `newint[]`; the initializer `{` sits on
    // the next line, so the marker must not cross the newline that Replace(" ", "") leaves intact. The
    // wrapper form is `new Sharpy.List<int>()`, which never contains `int[]`.
    private const string StackArray = "newint[]";
    private const string Wrapper = "Sharpy.List<int>";

    // The literal's only use is as the for-iterator — it cannot escape.
    private const string ForOverLiteral =
        "def f() -> int:\n"
        + "    total: int = 0\n"
        + "    for x in [1, 2, 3]:\n"
        + "        total = total + x\n"
        + "    return total\n";

    [Fact]
    public void Enabled_ForOverLiteral_LowersToArray()
    {
        var csharp = CompileCSharp(ForOverLiteral, stack: true);
        csharp.Should().Contain(StackArray);
        csharp.Should().NotContain(Wrapper);
    }

    [Fact]
    public void Disabled_ForOverLiteral_UsesWrapper()
    {
        var csharp = CompileCSharp(ForOverLiteral, stack: false);
        csharp.Should().Contain(Wrapper);
        csharp.Should().NotContain(StackArray);
    }

    [Fact]
    public void Enabled_AssignedLiteral_NotLowered()
    {
        // The literal is bound to `xs` and then iterated through the variable — it escapes the loop,
        // so it stays a wrapper even with the flag on.
        var src =
            "def f() -> int:\n"
            + "    xs: list[int] = [1, 2, 3]\n"
            + "    total: int = 0\n"
            + "    for x in xs:\n"
            + "        total = total + x\n"
            + "    return total\n";
        var csharp = CompileCSharp(src, stack: true);
        csharp.Should().Contain(Wrapper);
        csharp.Should().NotContain(StackArray);
    }

    [Fact]
    public void Enabled_ReturnedLiteral_NotLowered()
    {
        // A returned literal escapes the function, so it is not a for-iterator and is left a wrapper.
        var src = "def f() -> list[int]:\n    return [1, 2, 3]\n";
        var csharp = CompileCSharp(src, stack: true);
        csharp.Should().Contain(Wrapper);
        csharp.Should().NotContain(StackArray);
    }
}
