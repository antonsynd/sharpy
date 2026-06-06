using FluentAssertions;
using Xunit;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// Regression tests for issue #846: Roslyn's NormalizeWhitespace renders tuple
/// deconstructions without spaces ("var(a, b)in items", "var(a, b) = ...").
/// The emitter applies DeconstructionSpacingRewriter after normalization to
/// restore the spacing. These tests assert on the raw generated C# text —
/// file-based snapshot tests cannot catch this because NormalizeCSharp
/// re-formats with Formatter.Format, which masks the bug.
/// </summary>
public class DeconstructionSpacingTests
{
    private readonly CompilerApi _api = new();

    [Fact]
    public void ForLoopTupleUnpacking_EmitsSpacedForeachDeconstruction()
    {
        var source = @"
def main():
    pairs: list[tuple[int, str]] = [(1, ""a""), (2, ""b"")]
    for num, label in pairs:
        print(num)
        print(label)
";
        var result = _api.Compile(source);

        result.Success.Should().BeTrue();
        result.GeneratedCSharp.Should().Contain("foreach (var (num, label) in pairs)");
        result.GeneratedCSharp.Should().NotContain("var(");
    }

    [Fact]
    public void TupleAssignment_EmitsSpacedDeconstruction()
    {
        var source = @"
def get_pair() -> tuple[int, str]:
    return (1, ""a"")

def main():
    a, b = get_pair()
    print(a)
    print(b)
";
        var result = _api.Compile(source);

        result.Success.Should().BeTrue();
        result.GeneratedCSharp.Should().Contain("var (a, b) = GetPair()");
        result.GeneratedCSharp.Should().NotContain("var(");
    }
}
