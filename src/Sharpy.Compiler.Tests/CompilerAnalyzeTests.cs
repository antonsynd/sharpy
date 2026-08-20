using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
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

    [Fact]
    public void Analyze_ClassWithLen_ReportsSPY1001WithoutCodeGeneration()
    {
        // #1521's user-visible fix: protocol-interface synthesis runs in SEMANTIC analysis, so
        // an analysis-only run (no code generation) sees SPY1001. Before the move the analyzer
        // ran on the emission path and `emit diagnostics` / LSP-style analysis never saw it.
        var source = @"
class Box:
    _n: int = 3

    def __len__(self) -> int:
        return self._n

def main():
    b: Box = Box()
    print(len(b))
";
        var compiler = new Compiler();
        var result = compiler.Analyze(source, "test.spy");

        result.Success.Should().BeTrue(because: FormatDiagnostics(result));
        result.Diagnostics.GetAll().Should().Contain(
            d => d.Code == DiagnosticCodes.Info.ImplicitInterfaceSynthesis
                 && d.Message.Contains("ISized"),
            "analysis-only runs must see the synthesis decision (#1521)");
    }

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

/// <summary>
/// #1262: in single-file analyze the entry buffer has no path identity, and EVERY symbol the
/// entry file produces has to agree on that. Name resolution nulled the entry path, but type
/// checking kept receiving the synthetic entry path (<c>"&lt;source&gt;"</c> for
/// <see cref="CompilerApi.Analyze(string, CancellationToken)"/>), so symbols the TypeChecker
/// creates — function locals, assignment variables — carried a path their module-level
/// neighbours did not, so anything comparing the two paths read two symbols from one buffer as
/// living in different files.
/// </summary>
public class CompilerAnalyzeSymbolPathIdentityTests
{
    [Fact]
    public void Analyze_FunctionLocalSymbol_AgreesWithModuleLevelSymbolsOnNullPath()
    {
        const string source = """
def helper() -> int:
    local_value: int = 41
    return local_value + 1
""";
        var api = new CompilerApi();
        var result = api.Analyze(source);

        result.Success.Should().BeTrue(because: FormatDiagnostics(result));

        var helper = result.Ast!.Body.OfType<FunctionDef>().Single(f => f.Name == "helper");
        var declaration = helper.Body.OfType<VariableDeclaration>().Single(v => v.Name == "local_value");

        var localSymbol = result.SemanticInfo!.GetDeclarationSymbol(declaration);
        localSymbol.Should().NotBeNull("the TypeChecker records a symbol for a local declaration");
        localSymbol!.DeclaringFilePath.Should().BeNull(
            "the analyze entry file has no path identity — a local must not carry the synthetic "
            + "'<source>' path its enclosing function does not have (#1262)");

        var moduleLevelSymbol = result.SymbolTable!.Lookup("helper");
        moduleLevelSymbol.Should().NotBeNull();
        moduleLevelSymbol!.DeclaringFilePath.Should().Be(localSymbol.DeclaringFilePath,
            "name resolution and type checking must agree about the entry file's path identity");
    }

    /// <summary>
    /// Overloads declared in a pathless analyze buffer resolve, and the winner is recorded as the
    /// call's target.
    /// <para>
    /// Scope note, because #1262 also made the same-file overload guard null-tolerant
    /// (<c>_currentFilePath != overloads[0].DeclaringFilePath</c>, both-null = same file): this
    /// test does NOT discriminate that hunk, and nothing can. Mutation-verified by reverting the
    /// guard, and by reverting the entire pre-#1262 state (guard plus the synthetic
    /// <c>"&lt;source&gt;"</c> type-check path) — this test passes either way, because
    /// <c>ResolveImportedFunctionOverload</c> runs immediately after and resolves the same
    /// overload set identically: its shadow check is inert exactly when the overloads' declaring
    /// path is null, which is the only case the guard change affects. So the guard hunk is a
    /// correctness tidy-up with no observable behaviour, and this test pins the end-to-end
    /// contract instead: whichever resolver runs, an analyzed buffer's own overloads must resolve.
    /// </para>
    /// </summary>
    [Fact]
    public void Analyze_SameFileOverloads_ResolveAgainstThePathlessEntryFile()
    {
        const string source = """
def pick(x: int) -> str:
    return "from int"

def pick(x: str) -> int:
    return 42

def use() -> int:
    return pick("hello")
""";
        var api = new CompilerApi();
        var result = api.Analyze(source);

        result.Success.Should().BeTrue(because: FormatDiagnostics(result));

        var use = result.Ast!.Body.OfType<FunctionDef>().Single(f => f.Name == "use");
        var call = use.Body.OfType<ReturnStatement>().Single().Value as FunctionCall;
        call.Should().NotBeNull("the return value is a call to pick");

        var target = result.SemanticInfo!.GetCallTarget(call!);
        target.Should().NotBeNull(
            "the same-file overload path records the resolved target; a null-path bail-out "
            + "leaves it unrecorded (#1262)");
        target!.Parameters.Should().ContainSingle();
        target.Parameters[0].Type.GetDisplayName().Should().Be("str",
            "pick(\"hello\") selects the str overload, not the first-declared int one");
        target.ReturnType.GetDisplayName().Should().Be("int");
    }

    private static string FormatDiagnostics(SemanticResult result)
    {
        var errors = result.Diagnostics.Where(d => d.IsError).ToList();
        return errors.Count == 0
            ? "no errors"
            : string.Join("; ", errors.Select(d => $"[{d.Code}] {d.Message}"));
    }
}
