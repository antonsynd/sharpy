using Xunit;
using Xunit.Abstractions;

using Sharpy.TestInfrastructure.Integration;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Semantic tests for the postfix ? operator (early return on Result/Optional).
/// </summary>
[Collection("HeavyCompilation")]
public class QuestionMarkTypeCheckTests : IntegrationTestBase
{
    public QuestionMarkTypeCheckTests(ITestOutputHelper output) : base(output)
    {
    }

    #region Result - Success Cases

    [Fact]
    public void QuestionMark_OnResult_InResultFunction_Compiles()
    {
        // ? on Result<int, str> in function returning Result<bool, str> should succeed
        // and the unwrapped type should be int
        var source = @"
def get_value() -> int !str:
    return Ok(42)

def process() -> bool !str:
    val: int = get_value()?
    return Ok(val > 0)

def main() -> None:
    pass
";

        var result = CompileAndExecute(source);

        // Type checking should pass — codegen may fail since ? emission is not implemented yet
        // For now we just verify no semantic errors are produced
        var semanticErrors = result.CompilationErrors
            .Where(e => e.Contains("SPY02") || e.Contains("SPY04"))
            .ToList();
        Assert.Empty(semanticErrors);
    }

    #endregion

    #region Optional - Success Cases

    [Fact]
    public void QuestionMark_OnOptional_InOptionalFunction_Compiles()
    {
        // ? on Optional<int> in function returning Optional<str> should succeed
        // and the unwrapped type should be int
        var source = @"
def get_value() -> int?:
    return Some(42)

def process() -> str?:
    val: int = get_value()?
    return Some(str(val))

def main() -> None:
    pass
";

        var result = CompileAndExecute(source);

        var semanticErrors = result.CompilationErrors
            .Where(e => e.Contains("SPY02") || e.Contains("SPY04"))
            .ToList();
        Assert.Empty(semanticErrors);
    }

    #endregion

    #region Error Cases

    [Fact]
    public void QuestionMark_OnNonResultOrOptional_ReportsError()
    {
        var source = @"
def process(x: int) -> int !str:
    val: int = x?
    return Ok(val)

def main() -> None:
    pass
";

        var result = CompileAndExecute(source);

        Assert.False(result.Success, "Expected compilation to fail");
        Assert.NotEmpty(result.CompilationErrors);
        var errorText = string.Join(" ", result.CompilationErrors);
        Assert.Contains("'?' operator requires Result or Optional type", errorText);
    }

    [Fact]
    public void QuestionMark_AtModuleLevel_ReportsError()
    {
        var source = @"
x: int = 42
val = x?
";

        var result = CompileAndExecute(source);

        Assert.False(result.Success, "Expected compilation to fail");
        Assert.NotEmpty(result.CompilationErrors);
        var errorText = string.Join(" ", result.CompilationErrors);
        Assert.Contains("'?' operator can only be used inside a function", errorText);
    }

    [Fact]
    public void QuestionMark_ResultInOptionalFunction_ReportsError()
    {
        var source = @"
def get_value() -> int !str:
    return Ok(42)

def process() -> int?:
    val: int = get_value()?
    return Some(val)

def main() -> None:
    pass
";

        var result = CompileAndExecute(source);

        Assert.False(result.Success, "Expected compilation to fail");
        Assert.NotEmpty(result.CompilationErrors);
        var errorText = string.Join(" ", result.CompilationErrors);
        Assert.Contains("'?' on Result requires function to return Result", errorText);
    }

    [Fact]
    public void QuestionMark_OptionalInResultFunction_ReportsError()
    {
        var source = @"
def get_value() -> int?:
    return Some(42)

def process() -> int !str:
    val: int = get_value()?
    return Ok(val)

def main() -> None:
    pass
";

        var result = CompileAndExecute(source);

        Assert.False(result.Success, "Expected compilation to fail");
        Assert.NotEmpty(result.CompilationErrors);
        var errorText = string.Join(" ", result.CompilationErrors);
        Assert.Contains("'?' on Optional requires function to return Optional", errorText);
    }

    [Fact]
    public void QuestionMark_InFinallyBlock_ReportsError()
    {
        var source = @"
def get_value() -> int !str:
    return Ok(42)

def process() -> int !str:
    try:
        pass
    finally:
        val: int = get_value()?
    return Ok(0)

def main() -> None:
    pass
";

        var result = CompileAndExecute(source);

        Assert.False(result.Success, "Expected compilation to fail");
        Assert.NotEmpty(result.CompilationErrors);
        var errorText = string.Join(" ", result.CompilationErrors);
        Assert.Contains("'?' operator cannot be used inside a 'finally' block", errorText);
    }

    #endregion

    #region Error Subtype Assignability

    [Fact]
    public void QuestionMark_ErrorSubtype_Allowed()
    {
        // ? on Result<int, DerivedError> in function returning Result<bool, BaseError>
        // should be allowed since DerivedError is assignable to BaseError
        var source = @"
class BaseError(Exception):
    pass

class DerivedError(BaseError):
    pass

def get_value() -> int !DerivedError:
    return Ok(42)

def process() -> bool !BaseError:
    val: int = get_value()?
    return Ok(val > 0)

def main() -> None:
    pass
";

        var result = CompileAndExecute(source);

        // Should not have semantic errors about incompatible return types
        var semanticErrors = result.CompilationErrors
            .Where(e => e.Contains("SPY0461") || e.Contains("not assignable"))
            .ToList();
        Assert.Empty(semanticErrors);
    }

    #endregion
}
