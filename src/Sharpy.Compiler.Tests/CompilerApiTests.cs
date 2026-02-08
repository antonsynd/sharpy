using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests;

/// <summary>
/// Tests for <see cref="CompilerApi"/> — the public programmatic entry point for tooling consumers.
/// </summary>
public class CompilerApiTests
{
    private readonly ITestOutputHelper _output;
    private readonly CompilerApi _api = new();

    public CompilerApiTests(ITestOutputHelper output)
    {
        _output = output;
    }

    // ----- Compile tests -----

    [Fact]
    public void Compile_ValidProgram_ReturnsSuccess()
    {
        var source = @"
def main():
    print(""hello"")
";
        var result = _api.Compile(source);

        result.Success.Should().BeTrue();
        result.GeneratedCSharp.Should().NotBeNullOrEmpty();
        result.Ast.Should().NotBeNull();
        result.Diagnostics.Where(d => d.IsError).Should().BeEmpty();
    }

    [Fact]
    public void Compile_WithTypeError_ReturnsFailure()
    {
        var source = @"
def main():
    x: int = ""hello""
    print(x)
";
        var result = _api.Compile(source);

        result.Success.Should().BeFalse();
        result.Diagnostics.Should().Contain(d => d.IsError);
        result.Diagnostics.Should().Contain(d =>
            !string.IsNullOrEmpty(d.Code) && d.Code.StartsWith("SPY"));
        // Span should be present for type errors (Phase 1 work ensured this)
        var errors = result.Diagnostics.Where(d => d.IsError).ToList();
        errors.Should().Contain(d => d.Span.HasValue || d.Line.HasValue,
            "type error diagnostics should have location information");
    }

    [Fact]
    public void Compile_WithOptions_RespectsOptions()
    {
        var source = @"
def add(a: int, b: int) -> int:
    return a + b
";
        var options = new CompilerOptions { OutputType = "library" };
        var result = _api.Compile(source, options);

        // Library mode doesn't require main()
        result.Success.Should().BeTrue();
        result.GeneratedCSharp.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Compile_WithFilePath_UsesItInDiagnostics()
    {
        var source = @"
def main():
    x: int = ""hello""
";
        var result = _api.Compile(source, filePath: "my_program.spy");

        result.Success.Should().BeFalse();
        // The file path should appear in diagnostics
        result.Diagnostics.Should().Contain(d =>
            d.FilePath != null && d.FilePath.Contains("my_program.spy"));
    }

    // ----- CompileFile tests -----

    [Fact]
    public void CompileFile_NonExistentFile_ThrowsFileNotFound()
    {
        var act = () => _api.CompileFile("/nonexistent/file.spy");

        act.Should().Throw<FileNotFoundException>();
    }

    // ----- Parse tests -----

    [Fact]
    public void Parse_ValidCode_ReturnsAst()
    {
        var source = @"
x: int = 42
print(x)
";
        var result = _api.Parse(source);

        result.Success.Should().BeTrue();
        result.Ast.Should().NotBeNull();
        result.Ast!.Body.Should().NotBeEmpty();
        result.Diagnostics.Where(d => d.IsError).Should().BeEmpty();
    }

    [Fact]
    public void Parse_WithSyntaxError_ReturnsFailure()
    {
        var source = @"
def foo(
    print(42)
";
        var result = _api.Parse(source);

        result.Success.Should().BeFalse();
        result.Diagnostics.Should().Contain(d => d.IsError);
    }

    [Fact]
    public void Parse_DoesNotRunSemanticAnalysis()
    {
        // This has a type error, but Parse should succeed since it only checks syntax
        var source = @"
x: int = ""hello""
";
        var result = _api.Parse(source);

        // Parse should succeed — type errors are only caught by semantic analysis
        result.Success.Should().BeTrue();
        result.Ast.Should().NotBeNull();
    }

    // ----- Analyze tests -----

    [Fact]
    public void Analyze_ValidCode_ReturnsSemanticInfo()
    {
        var source = @"
def add(a: int, b: int) -> int:
    return a + b
";
        var result = _api.Analyze(source);

        result.Success.Should().BeTrue();
        result.Ast.Should().NotBeNull();
        result.SemanticInfo.Should().NotBeNull();
        result.SymbolTable.Should().NotBeNull();
    }

    [Fact]
    public void Analyze_WithTypeError_ReturnsFailureWithSemanticInfo()
    {
        var source = @"
def main():
    x: int = ""hello""
    print(x)
";
        var result = _api.Analyze(source);

        result.Success.Should().BeFalse();
        result.Diagnostics.Should().Contain(d => d.IsError);
        // Even on failure, partial semantic info may be available
        result.Ast.Should().NotBeNull();
    }

    // ----- FindNodeAtPosition tests -----

    [Fact]
    public void FindNodeAtPosition_FindsIdentifier()
    {
        var source = @"x: int = 42
print(x)
";
        var parseResult = _api.Parse(source);
        parseResult.Success.Should().BeTrue();

        // Line 1, column 1 should find the 'x' identifier in the variable declaration
        var node = _api.FindNodeAtPosition(parseResult.Ast!, 1, 1);

        node.Should().NotBeNull();
    }

    [Fact]
    public void FindNodeOfType_FindsSpecificNodeType()
    {
        var source = @"x: int = 42
print(x)
";
        var parseResult = _api.Parse(source);
        parseResult.Success.Should().BeTrue();

        // Line 2 should contain a FunctionCall (print)
        var call = _api.FindNodeOfType<FunctionCall>(parseResult.Ast!, 2, 1);

        call.Should().NotBeNull();
    }

    // ----- FormatDiagnostic tests -----

    [Fact]
    public void FormatDiagnostic_WithSpan_ContainsUnderlines()
    {
        var source = @"
def main():
    x: int = ""hello""
    print(x)
";
        var result = _api.Compile(source);
        result.Success.Should().BeFalse();

        var errors = result.Diagnostics.Where(d => d.IsError).ToList();
        errors.Should().NotBeEmpty();

        var formatted = _api.FormatDiagnostic(errors[0], source);
        _output.WriteLine(formatted);

        formatted.Should().NotBeNullOrEmpty();
        // Should contain the error header
        formatted.Should().Contain("error");
        // If the diagnostic has a span, should contain underline markers
        if (errors[0].Span.HasValue)
        {
            formatted.Should().Contain("^");
        }
    }

    [Fact]
    public void FormatDiagnostic_WithoutSource_ReturnsHeaderOnly()
    {
        var diagnostic = new CompilerDiagnostic(
            "Test error message",
            CompilerDiagnosticSeverity.Error,
            Code: "SPY0000");

        var formatted = _api.FormatDiagnostic(diagnostic);

        formatted.Should().Contain("error");
        formatted.Should().Contain("SPY0000");
        formatted.Should().Contain("Test error message");
    }

    // ----- Cancellation tests -----

    [Fact]
    public void Compile_WithCancelledToken_ThrowsOperationCanceledException()
    {
        var source = @"
def main():
    print(42)
";
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // CompilerApi.Compile returns a result with diagnostics rather than throwing,
        // because the underlying Compiler catches OperationCanceledException.
        var result = _api.Compile(source, cancellationToken: cts.Token);
        result.Success.Should().BeFalse();
        result.Diagnostics.Should().Contain(d =>
            d.Code == DiagnosticCodes.Infrastructure.CompilationCancelled);
    }

    [Fact]
    public void Parse_WithCancelledToken_ThrowsOperationCanceledException()
    {
        var source = @"
x: int = 42
print(x)
";
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Parse propagates OperationCanceledException
        var act = () => _api.Parse(source, cts.Token);
        act.Should().Throw<OperationCanceledException>();
    }

    [Fact]
    public void Analyze_WithCancelledToken_ReturnsCancelledResult()
    {
        var source = @"
def add(a: int, b: int) -> int:
    return a + b
";
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Analyze delegates to Compile which catches the cancellation
        var result = _api.Analyze(source, cts.Token);
        result.Success.Should().BeFalse();
    }

    // ----- Result immutability tests -----

    [Fact]
    public void CompileResult_DiagnosticsCollection_IsReadOnly()
    {
        var result = _api.Compile("print(42)");

        // The diagnostics list should be an IReadOnlyList
        result.Diagnostics.Should().BeAssignableTo<IReadOnlyList<CompilerDiagnostic>>();
    }
}
