using Xunit;
using Xunit.Abstractions;

using Sharpy.TestInfrastructure.Integration;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// Tests that a generic function whose type parameter can only be inferred from an unannotated
/// lambda's parameters reports SPY0237 (CannotInferGenericType) at the call site, instead of
/// binding the parameter to Unknown and leaking a C# CS0411 error (#904).
/// </summary>
[Collection("HeavyCompilation")]
public class UninferrableGenericLambdaTests : StdlibAwareIntegrationTestBase
{
    public UninferrableGenericLambdaTests(ITestOutputHelper output) : base(output) { }

    private static bool HasCode(IntegrationTestBase.ExecutionResult result, string code)
        => result.RawDiagnostics.Exists(d => d.Code == code);

    [Fact]
    public void CmpToKey_UnannotatedLambda_EmitsSpy0237()
    {
        var result = CompileAndExecute(@"
import functools

def main() -> None:
    key = functools.cmp_to_key(lambda a, b: a - b)
    print(key)
");

        Assert.True(HasCode(result, "SPY0237"),
            $"Expected SPY0237, got: {string.Join(", ", result.CompilationErrors)}");
        // The leak we are preventing must not surface.
        Assert.DoesNotContain(result.CompilationErrors, e => e.Contains("CS0411"));
    }

    [Fact]
    public void CmpToKey_AnnotatedLambda_NoSpy0237()
    {
        var result = CompileAndExecute(@"
import functools

def main() -> None:
    key = functools.cmp_to_key(lambda a: int, b: int: a - b)
    print(key)
");

        Assert.False(HasCode(result, "SPY0237"),
            $"Annotated lambda must not trigger SPY0237. Errors: {string.Join(", ", result.CompilationErrors)}");
    }

    [Fact]
    public void CmpToKey_UnannotatedLambdaAsKwarg_EmitsSpy0237()
    {
        var result = CompileAndExecute(@"
import functools

def main() -> None:
    key = functools.cmp_to_key(cmp=lambda a, b: a - b)
    print(key)
");

        Assert.True(HasCode(result, "SPY0237"),
            $"Expected SPY0237, got: {string.Join(", ", result.CompilationErrors)}");
        // The leak we are preventing must not surface.
        Assert.DoesNotContain(result.CompilationErrors, e => e.Contains("CS0411"));
    }

    [Fact]
    public void CmpToKey_AnnotatedLambdaAsKwarg_NoSpy0237()
    {
        var result = CompileAndExecute(@"
import functools

def main() -> None:
    key = functools.cmp_to_key(cmp=lambda a: int, b: int: a - b)
    print(key)
");

        Assert.False(HasCode(result, "SPY0237"),
            $"Annotated lambda must not trigger SPY0237. Errors: {string.Join(", ", result.CompilationErrors)}");
    }

    [Fact]
    public void SortWithKeyLambda_NoSpy0237()
    {
        // Regression guard: key-selector lambdas (TKey inferred from the body, not the
        // parameter annotation) must not be flagged as uninferrable.
        var result = CompileAndExecute(@"
def main() -> None:
    items: list[tuple[int, str]] = [(2, ""b""), (1, ""a"")]
    items.sort(key=lambda t: t[0])
    print(items)
");

        Assert.False(HasCode(result, "SPY0237"),
            $"sort(key=...) must not trigger SPY0237. Errors: {string.Join(", ", result.CompilationErrors)}");
        Assert.True(result.Success, $"Expected success, got: {string.Join(", ", result.CompilationErrors)}");
    }
}
