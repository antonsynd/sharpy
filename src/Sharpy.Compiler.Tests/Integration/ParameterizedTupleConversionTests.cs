using Xunit;
using Xunit.Abstractions;

using Sharpy.TestInfrastructure.Integration;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// Integration tests for parameterized tuple conversion, <c>tuple[int, str](t)</c> (#1200).
///
/// <para>The happy paths and the deliberate rejections are pinned by the
/// <c>basics/tuple_parameterized_construction</c> fixtures. What lives here instead is the one case
/// whose output DIVERGES from CPython by design, which a python3-derived fixture must not contain:
/// widening the element type. CPython's <c>tuple[float, str]</c> is a runtime no-op annotation, so
/// <c>tuple[float, str]((1, "a"))</c> keeps an <c>int</c> element there; Sharpy makes the value match
/// the type it gives the expression, exactly as an annotated <c>x: float = 1</c> emits a real
/// <c>double</c> (Axiom 1/3 over Axiom 2).</para>
/// </summary>
[Collection("HeavyCompilation")]
public class ParameterizedTupleConversionTests : IntegrationTestBase
{
    public ParameterizedTupleConversionTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void ParameterizedTupleConversion_WideningElementType_ProducesTheWrittenType()
    {
        var source = @"
def main() -> None:
    widened = tuple[float, str]((1, ""a""))
    print(widened)
    print(widened[0])
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.Equal("(1.0, 'a')\n1.0\n", result.StandardOutput);
    }

    [Fact]
    public void ParameterizedTupleConversion_MatchingElementTypes_IsIdentity()
    {
        var source = @"
def main() -> None:
    src: tuple[int, str] = (5, ""z"")
    print(tuple[int, str](src))
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.Equal("(5, 'z')\n", result.StandardOutput);
    }
}
