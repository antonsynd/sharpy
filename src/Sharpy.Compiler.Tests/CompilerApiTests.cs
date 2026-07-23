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

    // ----- AnalyzeProject option-parity tests (#1109) -----

    // `defer` is an experimental, parser-scoped feature: the statement is always parsed, but its
    // use is rejected with SPY0331 unless the feature is enabled. That makes it a clean probe for
    // whether AnalyzeProject threads a project's <Features> into the feature gate.
    private const string DeferProgram = @"
def run() -> None:
    defer print(""cleanup"")
    print(""body"")


def main() -> None:
    run()
";

    [Fact]
    public void AnalyzeProject_ProjectEnablesFeature_NoFeatureGateError()
    {
        // #1109: the .spyproj analyze path must gate identically to the compile path. A project
        // whose <Features> enables `defer` analyzes a defer-using file with no SPY0331 — the merge
        // now carries config-side features into the ProjectCompiler AnalyzeProject constructs.
        using var helper = new Helpers.ProjectCompilationHelper(_output)
            .WithRootNamespace("DeferAnalyze")
            .WithOutputType("exe")
            .WithEntryPoint("main.spy");
        helper.Options.Features.Add("defer");
        helper.AddSourceFile("main.spy", DeferProgram);
        helper.CreateProjectFile();

        var config = ProjectFileParser.Load(
            Path.Combine(helper.ProjectDirectory, "DeferAnalyze.spyproj"));

        var result = _api.AnalyzeProject(config);

        result.Diagnostics.GetAll().Should().NotContain(
            d => d.Code == DiagnosticCodes.Semantic.FeatureNotEnabled,
            "the project's <Features>defer</Features> must reach AnalyzeProject's feature gate");
        result.Success.Should().BeTrue(
            "a defer program analyzes cleanly once the feature is enabled");
    }

    [Fact]
    public void AnalyzeProject_ProjectOmitsFeature_ReportsFeatureGateError()
    {
        // The same file in a project WITHOUT <Features> must still report SPY0331 — parity with
        // the compile path, and a regression guard on the option-less construction #1109 fixed.
        using var helper = new Helpers.ProjectCompilationHelper(_output)
            .WithRootNamespace("DeferAnalyzeUngated")
            .WithOutputType("exe")
            .WithEntryPoint("main.spy");
        helper.AddSourceFile("main.spy", DeferProgram);
        helper.CreateProjectFile();

        var config = ProjectFileParser.Load(
            Path.Combine(helper.ProjectDirectory, "DeferAnalyzeUngated.spyproj"));

        var result = _api.AnalyzeProject(config);

        result.Diagnostics.GetAll().Should().Contain(
            d => d.Code == DiagnosticCodes.Semantic.FeatureNotEnabled,
            "an ungated defer use must still report SPY0331 through AnalyzeProject");
    }

    [Fact]
    public void AnalyzeProject_ExplicitOptionsEnableFeature_NoFeatureGateError()
    {
        // The optional CompilerOptions parameter carries CLI-side features too: a project without
        // <Features> analyzes cleanly when the caller supplies the feature via options (parity
        // with --enable-feature on the compile path).
        using var helper = new Helpers.ProjectCompilationHelper(_output)
            .WithRootNamespace("DeferAnalyzeCliOpt")
            .WithOutputType("exe")
            .WithEntryPoint("main.spy");
        helper.AddSourceFile("main.spy", DeferProgram);
        helper.CreateProjectFile();

        var config = ProjectFileParser.Load(
            Path.Combine(helper.ProjectDirectory, "DeferAnalyzeCliOpt.spyproj"));

        var options = new CompilerOptions
        {
            Features = Sharpy.Compiler.Shared.FeatureFlags.None.Enable("defer")
        };
        var result = _api.AnalyzeProject(config, options);

        result.Diagnostics.GetAll().Should().NotContain(
            d => d.Code == DiagnosticCodes.Semantic.FeatureNotEnabled,
            "options-supplied features must reach AnalyzeProject's feature gate");
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
