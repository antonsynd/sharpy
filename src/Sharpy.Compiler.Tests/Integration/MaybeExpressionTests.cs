using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// Integration tests for the 'maybe' expression, which converts C# nullable (T | None) to Optional[T].
/// At the C# level, both NullableType and OptionalType map to T?, so 'maybe' is a semantic
/// pass-through — the type checker enforces the distinction while generated code is unchanged.
/// </summary>
[Collection("HeavyCompilation")]
public class MaybeExpressionTests : IntegrationTestBase
{
    public MaybeExpressionTests(ITestOutputHelper output) : base(output)
    {
    }

    #region Type Checking - Error Cases

    [Fact]
    public void Maybe_WithNonNullableType_ReportsError()
    {
        var source = @"
def main():
    x: int = 42
    y = maybe x
";

        var result = CompileAndExecute(source);

        Assert.False(result.Success, "Expected compilation to fail for non-nullable operand");
        Assert.NotEmpty(result.CompilationErrors);
        var errorText = string.Join(" ", result.CompilationErrors).ToLower();
        Assert.Contains("nullable", errorText);
    }

    [Fact]
    public void Maybe_WithStringType_ReportsError()
    {
        var source = @"
def main():
    x: str = ""hello""
    y = maybe x
";

        var result = CompileAndExecute(source);

        Assert.False(result.Success, "Expected compilation to fail for non-nullable string operand");
        Assert.NotEmpty(result.CompilationErrors);
        var errorText = string.Join(" ", result.CompilationErrors).ToLower();
        Assert.Contains("nullable", errorText);
    }

    [Fact]
    public void Maybe_WithOptionalType_ReportsError()
    {
        var source = @"
def main():
    x: int? = Some(42)
    y = maybe x
";

        var result = CompileAndExecute(source);

        Assert.False(result.Success, "Expected compilation to fail for Optional operand");
        Assert.NotEmpty(result.CompilationErrors);
        var errorText = string.Join(" ", result.CompilationErrors).ToLower();
        Assert.Contains("nullable", errorText);
    }

    #endregion

    #region Code Generation - Success Cases

    [Fact]
    public void Maybe_WithNullableStringParam_NonNull_Compiles()
    {
        var source = @"
def convert(raw: str | None) -> str?:
    return maybe raw

def main():
    result: str? = convert(""hello"")
    value: str = result ?? ""fallback""
    print(value)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Contains("hello", result.StandardOutput);
    }

    [Fact]
    public void Maybe_WithNullableStringParam_Null_UsesFallback()
    {
        var source = @"
def convert(raw: str | None) -> str?:
    return maybe raw

def main():
    result: str? = convert(None)
    value: str = result ?? ""fallback""
    print(value)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Contains("fallback", result.StandardOutput);
    }

    [Fact]
    public void Maybe_WithNullableIntParam_NonNull_Works()
    {
        var source = @"
def convert(raw: int | None) -> int?:
    return maybe raw

def main():
    result: int? = convert(42)
    value: int = result ?? 0
    print(value)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Contains("42", result.StandardOutput);
    }

    [Fact]
    public void Maybe_WithNullableIntParam_Null_UsesFallback()
    {
        var source = @"
def convert(raw: int | None) -> int?:
    return maybe raw

def main():
    result: int? = convert(None)
    value: int = result ?? 99
    print(value)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Contains("99", result.StandardOutput);
    }

    [Fact]
    public void Maybe_NullCoalesceChain_Works()
    {
        var source = @"
def safe_get(raw: str | None) -> str?:
    return maybe raw

def main():
    a: str? = safe_get(None)
    b: str? = safe_get(""found"")
    value: str = a ?? b ?? ""default""
    print(value)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Contains("found", result.StandardOutput);
    }

    [Fact]
    public void Maybe_WithNullCoalesce_NonNullCase()
    {
        var source = @"
def try_get(raw: str | None) -> str?:
    return maybe raw

def main():
    result: str? = try_get(""hello"")
    print(result ?? ""no value"")
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Contains("hello", result.StandardOutput);
    }

    [Fact]
    public void Maybe_WithNullCoalesce_NullCase()
    {
        var source = @"
def try_get(raw: str | None) -> str?:
    return maybe raw

def main():
    result: str? = try_get(None)
    print(result ?? ""no value"")
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Contains("no value", result.StandardOutput);
    }

    #endregion

    #region Generated C# Verification

    [Fact]
    public void Maybe_GeneratedCSharp_EmitsOptionalFrom()
    {
        var source = @"
def convert(raw: str | None) -> str?:
    return maybe raw

def main():
    result: str? = convert(""test"")
    print(result ?? ""none"")
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.NotNull(result.GeneratedCSharp);
        // maybe converts NullableType to OptionalType via Optional.From()
        Assert.Contains("global::Sharpy.Optional.From(raw)", result.GeneratedCSharp!);
    }

    #endregion
}
