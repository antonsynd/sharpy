using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests;

/// <summary>
/// Tests for <see cref="Compiler.Analyze(string, string)"/> -- runs Lexer/Parser/Semantic
/// without code generation, returning a <see cref="CompilationResult"/> with no generated C#.
/// </summary>
public class CompilerAnalyzeTests
{
    private readonly ITestOutputHelper _output;

    public CompilerAnalyzeTests(ITestOutputHelper output)
    {
        _output = output;
    }

    // ----- Valid code: verifies AST, SemanticInfo, SymbolTable are populated -----

    [Fact]
    public void Analyze_ValidFunction_ReturnsSuccessWithSemanticArtifacts()
    {
        var source = @"
def add(a: int, b: int) -> int:
    return a + b
";
        var compiler = new Compiler(new CompilerOptions { OutputType = "library" });
        var result = compiler.Analyze(source, "test.spy");

        result.Success.Should().BeTrue(because: FormatDiagnostics(result));
        result.Module.Should().NotBeNull("AST should be populated");
        result.Module!.Body.Should().NotBeEmpty("module body should have statements");
        result.SemanticInfo.Should().NotBeNull("SemanticInfo should be populated after analysis");
        result.SymbolTable.Should().NotBeNull("SymbolTable should be populated after analysis");
    }

    [Fact]
    public void Analyze_ValidFunction_SymbolTableContainsDeclaredFunction()
    {
        var source = @"
def greet(name: str) -> str:
    return ""hello "" + name
";
        var compiler = new Compiler(new CompilerOptions { OutputType = "library" });
        var result = compiler.Analyze(source, "test.spy");

        result.Success.Should().BeTrue(because: FormatDiagnostics(result));
        result.SymbolTable.Should().NotBeNull();

        var symbol = result.SymbolTable!.Lookup("greet");
        symbol.Should().NotBeNull("the declared function 'greet' should appear in the symbol table");
    }

    [Fact]
    public void Analyze_ValidCode_SemanticInfoHasExpressionTypes()
    {
        var source = @"
def compute(x: int) -> int:
    return x + 1
";
        var compiler = new Compiler(new CompilerOptions { OutputType = "library" });
        var result = compiler.Analyze(source, "test.spy");

        result.Success.Should().BeTrue(because: FormatDiagnostics(result));
        result.SemanticInfo.Should().NotBeNull();
        result.SemanticInfo!.ExpressionTypeCount.Should().BeGreaterThan(0,
            "type checker should record expression types for non-empty modules");
    }

    [Fact]
    public void Analyze_ValidMainFunction_ReturnsSuccess()
    {
        var source = @"
def main():
    x: int = 42
    print(x)
";
        var compiler = new Compiler();
        var result = compiler.Analyze(source, "test.spy");

        result.Success.Should().BeTrue(because: FormatDiagnostics(result));
        result.Module.Should().NotBeNull();
        result.SemanticInfo.Should().NotBeNull();
        result.SymbolTable.Should().NotBeNull();
    }

    [Fact]
    public void Analyze_ClassDefinition_SymbolTableContainsType()
    {
        var source = @"
class Point:
    x: int
    y: int

    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y
";
        var compiler = new Compiler(new CompilerOptions { OutputType = "library" });
        var result = compiler.Analyze(source, "test.spy");

        result.Success.Should().BeTrue(because: FormatDiagnostics(result));
        var symbol = result.SymbolTable!.Lookup("Point");
        symbol.Should().NotBeNull("class 'Point' should be in the symbol table");
    }

    // ----- No generated C# code -----

    [Fact]
    public void Analyze_ValidCode_GeneratedCSharpCodeIsNull()
    {
        var source = @"
def main():
    print(42)
";
        var compiler = new Compiler();
        var result = compiler.Analyze(source, "test.spy");

        result.Success.Should().BeTrue(because: FormatDiagnostics(result));
        result.GeneratedCSharpCode.Should().BeNull(
            "Analyze should skip code generation entirely");
    }

    [Fact]
    public void Analyze_ValidCode_GeneratedCSharpFilesIsEmpty()
    {
        var source = @"
def main():
    print(42)
";
        var compiler = new Compiler();
        var result = compiler.Analyze(source, "test.spy");

        result.Success.Should().BeTrue(because: FormatDiagnostics(result));
        result.GeneratedCSharpFiles.Should().BeEmpty(
            "Analyze should not produce any generated C# files");
    }

    [Fact]
    public void Analyze_LibraryCode_GeneratedCSharpCodeIsNull()
    {
        var source = @"
def add(a: int, b: int) -> int:
    return a + b
";
        var compiler = new Compiler(new CompilerOptions { OutputType = "library" });
        var result = compiler.Analyze(source, "test.spy");

        result.Success.Should().BeTrue(because: FormatDiagnostics(result));
        result.GeneratedCSharpCode.Should().BeNull(
            "Analyze should skip code generation even for library code");
    }

    // ----- Error reporting -----

    [Fact]
    public void Analyze_TypeMismatch_ReturnsFailureWithErrors()
    {
        var source = @"
def main():
    x: int = ""hello""
    print(x)
";
        var compiler = new Compiler();
        var result = compiler.Analyze(source, "test.spy");

        result.Success.Should().BeFalse("type mismatch should cause analysis failure");
        result.Diagnostics.HasErrors.Should().BeTrue();
        result.Diagnostics.GetErrors().Should().Contain(d =>
            d.Code != null && d.Code.StartsWith("SPY"),
            "errors should have SPY diagnostic codes");
    }

    [Fact]
    public void Analyze_UndefinedVariable_ReturnsFailureWithErrors()
    {
        var source = @"
def main():
    print(undefined_variable)
";
        var compiler = new Compiler();
        var result = compiler.Analyze(source, "test.spy");

        result.Success.Should().BeFalse("undefined variable should cause analysis failure");
        result.Diagnostics.HasErrors.Should().BeTrue();
    }

    [Fact]
    public void Analyze_SyntaxError_ReturnsFailureBeforeSemanticPhase()
    {
        var source = @"
def foo(
    print(42)
";
        var compiler = new Compiler();
        var result = compiler.Analyze(source, "test.spy");

        result.Success.Should().BeFalse("syntax error should cause analysis failure");
        result.Diagnostics.HasErrors.Should().BeTrue();
        // SemanticInfo should be null since semantic analysis was not reached
        result.SemanticInfo.Should().BeNull(
            "semantic analysis should not run when parsing fails");
    }

    [Fact]
    public void Analyze_InvalidCode_GeneratedCSharpCodeIsStillNull()
    {
        var source = @"
def main():
    x: int = ""hello""
";
        var compiler = new Compiler();
        var result = compiler.Analyze(source, "test.spy");

        result.Success.Should().BeFalse();
        result.GeneratedCSharpCode.Should().BeNull(
            "Analyze should never produce generated C# code, even on error paths");
    }

    [Fact]
    public void Analyze_ErrorDiagnostics_HaveLocationInfo()
    {
        var source = @"
def main():
    x: int = ""hello""
    print(x)
";
        var compiler = new Compiler();
        var result = compiler.Analyze(source, "test.spy");

        result.Success.Should().BeFalse();
        var errors = result.Diagnostics.GetErrors().ToList();
        errors.Should().NotBeEmpty();
        errors.Should().Contain(d => d.Span.HasValue || d.Line.HasValue,
            "error diagnostics should have location information");
    }

    // ----- Cancellation -----

    [Fact]
    public void Analyze_WithCancelledToken_ReturnsFailure()
    {
        var source = @"
def main():
    print(42)
";
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var compiler = new Compiler();
        var result = compiler.Analyze(source, "test.spy", cts.Token);

        result.Success.Should().BeFalse();
        result.Diagnostics.GetAll().Should().Contain(d =>
            d.Code == DiagnosticCodes.Infrastructure.CompilationCancelled);
    }

    // ----- Compile vs Analyze comparison -----

    [Fact]
    public void Compile_SameCode_ProducesGeneratedCSharp_UnlikeAnalyze()
    {
        var source = @"
def main():
    print(42)
";
        var compiler = new Compiler();

        var analyzeResult = compiler.Analyze(source, "test.spy");
        var compileResult = compiler.Compile(source, "test.spy");

        analyzeResult.GeneratedCSharpCode.Should().BeNull(
            "Analyze should not produce generated C#");
        compileResult.GeneratedCSharpCode.Should().NotBeNullOrEmpty(
            "Compile should produce generated C#");
    }

    [Fact]
    public void Analyze_SameCodeAsCompile_ProducesConsistentDiagnostics()
    {
        var source = @"
def main():
    x: int = ""hello""
    print(x)
";
        var compiler = new Compiler();
        var analyzeResult = compiler.Analyze(source, "test.spy");
        var compileResult = compiler.Compile(source, "test.spy");

        analyzeResult.Success.Should().Be(compileResult.Success,
            "Analyze and Compile should agree on success/failure");

        var analyzeErrors = analyzeResult.Diagnostics.GetErrors()
            .Select(d => d.Code).OrderBy(c => c).ToList();
        var compileErrors = compileResult.Diagnostics.GetErrors()
            .Select(d => d.Code).OrderBy(c => c).ToList();

        analyzeErrors.Should().BeEquivalentTo(compileErrors,
            "Analyze and Compile should report the same error codes for semantic errors");
    }

    // ----- Metrics -----

    [Fact]
    public void Analyze_ValidCode_PopulatesMetrics()
    {
        var source = @"
def main():
    x: int = 42
    print(x)
";
        var compiler = new Compiler();
        var result = compiler.Analyze(source, "test.spy");

        result.Success.Should().BeTrue(because: FormatDiagnostics(result));
        result.Metrics.Should().NotBeNull("metrics should be available after analysis");
        result.Metrics!.TokenCount.Should().BeGreaterThan(0, "tokens should be counted");
        result.Metrics.AstNodeCount.Should().BeGreaterThan(0, "AST nodes should be counted");
    }

    // ----- Tokens and SourceText -----

    [Fact]
    public void Analyze_ValidCode_PopulatesTokensAndSourceText()
    {
        var source = @"
def main():
    print(42)
";
        var compiler = new Compiler();
        var result = compiler.Analyze(source, "test.spy");

        result.Success.Should().BeTrue(because: FormatDiagnostics(result));
        result.Tokens.Should().NotBeNull("tokens should be available after analysis");
        result.Tokens!.Count.Should().BeGreaterThan(0);
        result.SourceText.Should().NotBeNull("source text should be available after analysis");
    }

    // ----- Overload without CancellationToken -----

    [Fact]
    public void Analyze_OverloadWithoutCancellationToken_Works()
    {
        var source = @"
def add(a: int, b: int) -> int:
    return a + b
";
        var compiler = new Compiler(new CompilerOptions { OutputType = "library" });

        // This calls the two-parameter overload (no CancellationToken)
        var result = compiler.Analyze(source, "test.spy");

        result.Success.Should().BeTrue(because: FormatDiagnostics(result));
    }

    // ----- Helper -----

    private static string FormatDiagnostics(CompilationResult result)
    {
        var errors = result.Diagnostics.GetErrors().ToList();
        if (errors.Count == 0)
            return "no errors";
        return string.Join("; ", errors.Select(d => $"[{d.Code}] {d.Message}"));
    }
}

/// <summary>
/// Tests that <see cref="CompilerApi.Analyze"/> delegates to <see cref="Compiler.Analyze"/>
/// and does not produce generated C# code.
/// </summary>
public class CompilerApiAnalyzeTests
{
    [Fact]
    public void Analyze_ValidCode_ReturnsSemanticInfoWithoutGeneratedCode()
    {
        var api = new CompilerApi();
        var result = api.Analyze(@"
def add(a: int, b: int) -> int:
    return a + b
");

        result.Success.Should().BeTrue(
            because: string.Join("; ", result.Diagnostics.Where(d => d.IsError).Select(d => d.Message)));
        // SemanticResult does not have a GeneratedCSharp property by design,
        // confirming Analyze never exposes generated code
        result.Ast.Should().NotBeNull();
        result.SemanticInfo.Should().NotBeNull();
        result.SymbolTable.Should().NotBeNull();
    }

    [Fact]
    public void Analyze_TypeError_ReportsFailure()
    {
        var api = new CompilerApi();
        var result = api.Analyze(@"
def main():
    x: int = ""hello""
    print(x)
");

        result.Success.Should().BeFalse();
        result.Diagnostics.Should().Contain(d => d.IsError);
    }

    [Fact]
    public void Analyze_WithCancellation_ReturnsCancelledResult()
    {
        var api = new CompilerApi();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = api.Analyze(@"
def add(a: int, b: int) -> int:
    return a + b
", cts.Token);

        result.Success.Should().BeFalse();
    }

    /// <summary>
    /// LSP regression guard (#1087): the no-options overload analyzes as a library, so a
    /// buffer without <c>main()</c> must not gain a missing-entry-point diagnostic when
    /// single-file analyze rides the project pipeline.
    /// </summary>
    [Fact]
    public void Analyze_NoOptionsOverload_MainlessSource_HasNoMissingMainDiagnostic()
    {
        var api = new CompilerApi();
        var result = api.Analyze("def helper() -> int:\n    return 1\n");

        result.Success.Should().BeTrue(
            because: string.Join("; ", result.Diagnostics.Where(d => d.IsError).Select(d => d.Message)));
        result.Diagnostics.Should().NotContain(
            d => d.Code == DiagnosticCodes.Validation.MissingMainFunction,
            "library-mode analyze must not require an entry point");
    }

    /// <summary>
    /// Analyze-path parity (#1087): the analyze facade preserves trivia, so comments in the
    /// buffer surface as <see cref="SemanticResult.CommentSpans"/> (consumed by LSP hover).
    /// </summary>
    [Fact]
    public void Analyze_SourceWithComments_SurfacesCommentSpans()
    {
        var api = new CompilerApi();
        var result = api.Analyze("# leading comment\ndef helper() -> int:\n    return 1  # trailing\n");

        result.Success.Should().BeTrue(
            because: string.Join("; ", result.Diagnostics.Where(d => d.IsError).Select(d => d.Message)));
        result.CommentSpans.Should().NotBeEmpty("preserveTrivia is always on for the analyze path");
        result.CommentSpans.Should().Contain(s => s.Line == 1, "the leading comment is on line 1");
        result.CommentSpans.Should().Contain(s => s.Line == 3, "the trailing comment is on line 3");
    }
}

/// <summary>
/// Covers single-file analyze of an entry file with a local import closure (#1087): the
/// re-pointed <see cref="Compiler.Analyze(string, string)"/> discovers and analyzes imported
/// local <c>.spy</c> files through the synthetic project (where the deleted single-file driver
/// used lazy <c>ImportResolver</c> loading), and the entry file's symbols carry no path
/// identity while imported files keep theirs (the <c>NullifyEntryFilePath</c> contract LSP
/// handlers rely on).
/// </summary>
public class CompilerAnalyzeImportClosureTests : IDisposable
{
    private readonly string _tempDir;

    public CompilerAnalyzeImportClosureTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"sharpy_analyze_closure_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void Analyze_FileWithLocalImport_ResolvesImportedSymbols()
    {
        File.WriteAllText(Path.Combine(_tempDir, "helpers.spy"),
            "def helper_value() -> int:\n    return 41\n");
        var mainPath = Path.Combine(_tempDir, "main.spy");
        const string mainSource = "import helpers\n\ndef local_fn() -> int:\n    return helpers.helper_value() + 1\n";
        File.WriteAllText(mainPath, mainSource);

        var compiler = new Compiler(new CompilerOptions { OutputType = "library" });
        var result = compiler.Analyze(mainSource, mainPath);

        // Type-checking helpers.helper_value() succeeds only if the imported module was
        // discovered, parsed, and semantically analyzed as part of the closure.
        result.Success.Should().BeTrue(because: string.Join("; ",
            result.Diagnostics.GetErrors().Select(d => $"[{d.Code}] {d.Message}")));
        result.Module.Should().NotBeNull();
        result.SymbolTable.Should().NotBeNull();
        result.SymbolTable!.Lookup("local_fn").Should().NotBeNull(
            "entry-file module-level symbols resolve via bare Lookup");
    }

    [Fact]
    public void Analyze_EntryFileSymbolsPathless_ImportedFileSymbolsKeepPath()
    {
        var helpersPath = Path.Combine(_tempDir, "helpers.spy");
        File.WriteAllText(helpersPath, "def helper_value() -> int:\n    return 41\n");
        var mainPath = Path.Combine(_tempDir, "main.spy");
        const string mainSource = "import helpers\n\ndef local_fn() -> int:\n    return helpers.helper_value() + 1\n";
        File.WriteAllText(mainPath, mainSource);

        var compiler = new Compiler(new CompilerOptions { OutputType = "library" });
        var result = compiler.Analyze(mainSource, mainPath);

        result.Success.Should().BeTrue(because: string.Join("; ",
            result.Diagnostics.GetErrors().Select(d => $"[{d.Code}] {d.Message}")));

        var table = result.SymbolTable!;
        var localFn = table.Lookup("local_fn");
        localFn.Should().NotBeNull();
        localFn!.DeclaringFilePath.Should().BeNull(
            "the analyze entry file has no path identity — LSP handlers substitute the request document URI");

        var helpersScope = table.GetModuleScope("helpers");
        helpersScope.Should().NotBeNull("the imported module has its own module scope");
        var helperFn = helpersScope!.GetAllSymbols().FirstOrDefault(s => s.Name == "helper_value");
        helperFn.Should().NotBeNull();
        helperFn!.DeclaringFilePath.Should().NotBeNull(
            "imported closure files keep their real path identity");
        Path.GetFileName(helperFn.DeclaringFilePath).Should().Be("helpers.spy");
    }
}
