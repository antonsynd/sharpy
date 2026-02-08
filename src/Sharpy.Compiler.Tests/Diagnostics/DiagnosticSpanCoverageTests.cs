using Sharpy.Compiler.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Diagnostics;

/// <summary>
/// Verifies that diagnostics from every compiler phase carry non-null TextSpan
/// with valid Start >= 0 and Length > 0. This catches regressions where error
/// messages lose their source location, making them useless for the user.
/// </summary>
public class DiagnosticSpanCoverageTests
{
    private readonly ITestOutputHelper _output;

    public DiagnosticSpanCoverageTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private CompilerApi CreateApi() => new();

    private void AssertDiagnosticHasValidSpan(CompilerDiagnostic diagnostic)
    {
        _output.WriteLine($"Diagnostic: [{diagnostic.Code}] {diagnostic.Message}");
        _output.WriteLine($"  Span: {diagnostic.Span}, Line: {diagnostic.Line}, Column: {diagnostic.Column}");

        Assert.True(diagnostic.Span.HasValue,
            $"Diagnostic '{diagnostic.Code}: {diagnostic.Message}' should have a Span");
        Assert.True(diagnostic.Span!.Value.Start >= 0,
            $"Diagnostic '{diagnostic.Code}' Span.Start should be >= 0, got {diagnostic.Span.Value.Start}");
        Assert.True(diagnostic.Span!.Value.Length > 0,
            $"Diagnostic '{diagnostic.Code}' Span.Length should be > 0, got {diagnostic.Span.Value.Length}");
    }

    [Fact]
    public void Lexer_UnterminatedString_HasValidSpan()
    {
        var api = CreateApi();
        var result = api.Compile("def main():\n    x: str = \"unterminated\n");

        Assert.False(result.Success);

        var error = result.Diagnostics.FirstOrDefault(d =>
            d.IsError && d.Code == DiagnosticCodes.Lexer.UnterminatedString);

        Assert.NotNull(error);
        AssertDiagnosticHasValidSpan(error);
    }

    [Fact]
    public void Lexer_InvalidEscapeSequence_HasValidSpan()
    {
        var api = CreateApi();
        var result = api.Compile("def main():\n    x: str = \"hello\\q\"\n");

        Assert.False(result.Success);

        var error = result.Diagnostics.FirstOrDefault(d =>
            d.IsError && d.Code == DiagnosticCodes.Lexer.InvalidEscapeSequence);

        Assert.NotNull(error);
        AssertDiagnosticHasValidSpan(error);
    }

    [Fact]
    public void Lexer_UnexpectedCharacter_HasValidSpan()
    {
        var api = CreateApi();
        var result = api.Compile("def main():\n    x: int = $\n");

        Assert.False(result.Success);

        var error = result.Diagnostics.FirstOrDefault(d =>
            d.IsError && d.Code == DiagnosticCodes.Lexer.UnexpectedCharacter);

        Assert.NotNull(error);
        AssertDiagnosticHasValidSpan(error);
    }

    [Fact]
    public void Parser_ExpectedColon_HasValidSpan()
    {
        var api = CreateApi();
        var result = api.Parse("def main()\n    pass\n");

        Assert.False(result.Success);

        var error = result.Diagnostics.FirstOrDefault(d =>
            d.IsError && d.Code == DiagnosticCodes.Parser.ExpectedToken);

        Assert.NotNull(error);
        AssertDiagnosticHasValidSpan(error);
    }

    [Fact]
    public void Parser_UnexpectedToken_HasValidSpan()
    {
        var api = CreateApi();
        var result = api.Parse("def main():\n    ]\n");

        Assert.False(result.Success);

        var error = result.Diagnostics.FirstOrDefault(d =>
            d.IsError && (d.Code == DiagnosticCodes.Parser.UnexpectedToken ||
                         d.Code == DiagnosticCodes.Parser.ExpectedEndOfStatement));

        Assert.NotNull(error);
        AssertDiagnosticHasValidSpan(error);
    }

    [Fact]
    public void NameResolution_DuplicateDefinition_HasValidSpan()
    {
        var api = CreateApi();
        var result = api.Compile("def foo() -> int:\n    return 1\n\ndef foo() -> int:\n    return 2\n\ndef main():\n    pass\n");

        Assert.False(result.Success);

        var error = result.Diagnostics.FirstOrDefault(d =>
            d.IsError && d.Code == DiagnosticCodes.Semantic.DuplicateDefinition);

        Assert.NotNull(error);
        AssertDiagnosticHasValidSpan(error);
    }

    [Fact]
    public void TypeChecking_UndefinedVariable_HasValidSpan()
    {
        var api = CreateApi();
        var result = api.Compile("def main():\n    print(xyz)\n");

        Assert.False(result.Success);

        var error = result.Diagnostics.FirstOrDefault(d =>
            d.IsError && d.Code == DiagnosticCodes.Semantic.UndefinedVariable);

        Assert.NotNull(error);
        AssertDiagnosticHasValidSpan(error);
    }

    [Fact]
    public void TypeChecking_TypeMismatch_HasValidSpan()
    {
        var api = CreateApi();
        var result = api.Compile("def main():\n    x: int = \"hello\"\n");

        Assert.False(result.Success);

        var error = result.Diagnostics.FirstOrDefault(d =>
            d.IsError && d.Code == DiagnosticCodes.Semantic.TypeMismatch);

        Assert.NotNull(error);
        AssertDiagnosticHasValidSpan(error);
    }

    [Fact]
    public void TypeChecking_WrongArgCount_HasValidSpan()
    {
        var api = CreateApi();
        var result = api.Compile(
            "def add(a: int, b: int) -> int:\n    return a + b\n\ndef main():\n    add(1, 2, 3)\n");

        Assert.False(result.Success);

        var error = result.Diagnostics.FirstOrDefault(d =>
            d.IsError && d.Code == DiagnosticCodes.Semantic.WrongArgumentCount);

        Assert.NotNull(error);
        AssertDiagnosticHasValidSpan(error);
    }

    [Fact]
    public void Validation_NotAllPathsReturn_HasValidSpan()
    {
        var api = CreateApi();
        var result = api.Compile(
            "def foo(x: int) -> int:\n    if x > 0:\n        return 1\n\ndef main():\n    pass\n");

        Assert.False(result.Success);

        var error = result.Diagnostics.FirstOrDefault(d =>
            d.IsError && d.Code == DiagnosticCodes.Semantic.NotAllPathsReturn);

        Assert.NotNull(error);
        AssertDiagnosticHasValidSpan(error);
    }

    [Fact]
    public void Validation_ModuleLevelStatement_HasValidSpan()
    {
        var api = CreateApi();
        var result = api.Compile("print(\"hello\")\n\ndef main():\n    pass\n");

        Assert.False(result.Success);

        var error = result.Diagnostics.FirstOrDefault(d =>
            d.IsError && d.Code == DiagnosticCodes.Semantic.ModuleLevelExecutableStatement);

        Assert.NotNull(error);
        AssertDiagnosticHasValidSpan(error);
    }
}
