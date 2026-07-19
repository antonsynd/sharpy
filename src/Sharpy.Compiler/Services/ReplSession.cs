extern alias SharpyRT;

using System.Collections.Immutable;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Sharpy.Compiler.CodeGen;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Project;
using Sharpy.Compiler.Semantic.Registry;

namespace Sharpy.Compiler.Services;

/// <summary>
/// Result of evaluating a single REPL input.
/// </summary>
/// <param name="Success">True if the input compiled and executed without errors.</param>
/// <param name="Output">Standard output produced by executing the new input
/// (excludes output produced by previous accumulated history).</param>
/// <param name="Diagnostics">All diagnostics (errors, warnings, info) emitted by the
/// compiler for this evaluation. May be non-empty even when <see cref="Success"/> is true
/// (e.g., warnings).</param>
public record ReplResult(
    bool Success,
    string Output,
    IReadOnlyList<CompilerDiagnostic> Diagnostics);

/// <summary>
/// Programmatic REPL service that compiles and executes individual Sharpy
/// statements or expressions, preserving prior definitions across evaluations.
/// </summary>
/// <remarks>
/// <para>
/// Each call to <see cref="EvaluateAsync(string, CancellationToken)"/> recompiles the
/// cumulative history (all previously accepted inputs concatenated) plus the new input
/// as a single in-memory compilation unit. Bare expressions are automatically wrapped
/// in <c>print(...)</c> for display, mirroring Python's interactive prompt.
/// </para>
/// <para>
/// Output capture: the cumulative history is re-executed on every call, but only the
/// output produced beyond the previous run's output is returned. Side effects in prior
/// snippets are therefore re-run on each evaluation; users should avoid REPL inputs
/// with non-idempotent side effects (file writes, network calls). For typical
/// definitional and expression evaluation this is invisible to callers.
/// </para>
/// <para>
/// Thread safety: a single <see cref="ReplSession"/> is intended for use by one logical
/// REPL loop. Concurrent <c>EvaluateAsync</c> calls on the same instance are not
/// supported.
/// </para>
/// </remarks>
public class ReplSession
{
    private readonly ICompilerLogger _logger;
    private readonly ICodeEmitterFactory _emitterFactory;

    // Snippets that go at module level (function defs, classes, imports, typed
    // variable declarations, etc.). These accumulate across evaluations so prior
    // definitions remain visible.
    private readonly List<string> _moduleLevelSnippets = new();

    // Snippets of executable statements (assignments, expressions, control flow)
    // emitted inside a synthesized main() function. The cumulative main() body
    // is re-executed on every evaluation; output diffing isolates new output.
    private readonly List<string> _statementSnippets = new();

    private string _lastFullOutput = string.Empty;
    private int _evaluationCounter;

    // Session-wide experimental feature flags (from --enable-feature). Unioned with any
    // per-input `from __future__ import` features before gate checking, mirroring the
    // batch pipelines.
    private readonly Shared.FeatureFlags _features;

    /// <summary>
    /// Creates a new REPL session.
    /// </summary>
    /// <param name="logger">Optional compiler logger; defaults to <see cref="NullLogger.Instance"/>.</param>
    /// <param name="features">Optional experimental feature flags enabled for the whole session.</param>
    public ReplSession(ICompilerLogger? logger = null, Shared.FeatureFlags? features = null)
        : this(logger, emitterFactory: null, features)
    {
    }

    internal ReplSession(
        ICompilerLogger? logger,
        ICodeEmitterFactory? emitterFactory,
        Shared.FeatureFlags? features = null)
    {
        _logger = logger ?? NullLogger.Instance;
        _emitterFactory = emitterFactory ?? new RoslynEmitterFactory();
        _features = features ?? Shared.FeatureFlags.None;
    }

    /// <summary>
    /// The number of inputs that have been successfully evaluated so far.
    /// </summary>
    public int EvaluationCount => _evaluationCounter;

    /// <summary>
    /// The accumulated source code of all successfully evaluated inputs, in the
    /// shape that the compiler actually sees: definitions at module level
    /// followed by a synthesized <c>main()</c> containing executable statements.
    /// </summary>
    public string GetAccumulatedSource() => BuildFullSource(extraModuleLevel: null, extraStatement: null);

    /// <summary>
    /// Compiles and executes a single REPL input, returning its output and diagnostics.
    /// On failure the input is not appended to the cumulative history; on success the
    /// snippet (possibly rewritten to wrap a bare expression in <c>print(...)</c>) is
    /// appended so future evaluations can see its definitions.
    /// </summary>
    public Task<ReplResult> EvaluateAsync(string input, CancellationToken cancellationToken = default)
    {
        if (input == null)
            throw new ArgumentNullException(nameof(input));

        // Run the synchronous compilation/execution pipeline on a worker thread to
        // satisfy the async contract without blocking the caller's thread. The
        // compiler itself has no async surface today, so wrapping in Task.Run is
        // the most honest signal of "this is potentially long-running CPU work".
        return Task.Run(() => EvaluateCore(input, cancellationToken), cancellationToken);
    }

    private ReplResult EvaluateCore(string input, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Step 1: Pre-parse the new input on its own to (a) report syntax errors
        // before they get tangled with cumulative history, and (b) categorize
        // statements as module-level vs. executable. Sharpy disallows bare
        // executable statements at module scope, so executable inputs must be
        // emitted inside a synthesized main() function.
        var classification = ClassifyInput(input, out var preParseDiagnostics);
        if (preParseDiagnostics.Count > 0)
        {
            return new ReplResult(false, string.Empty, preParseDiagnostics);
        }

        // Step 2: Build the cumulative source (including the new snippet) and
        // compile/run it as a single in-memory module.
        string? newModuleSnippet = classification.Kind == InputKind.ModuleLevel ? classification.Source : null;
        string? newStatementSnippet = classification.Kind == InputKind.Statement ? classification.Source : null;

        var fullSource = BuildFullSource(newModuleSnippet, newStatementSnippet);

        var pipelineResult = CompileAndExecute(fullSource, cancellationToken);

        if (!pipelineResult.Success)
        {
            return new ReplResult(false, string.Empty, pipelineResult.Diagnostics);
        }

        // Step 3: Diff the captured stdout against the previous run so callers only
        // see new output. New output is whatever extends the previous full output as
        // a prefix; if the prefix relationship breaks (e.g., due to changed control
        // flow), we conservatively return the entire output to avoid hiding data.
        string newOutput;
        if (pipelineResult.Output.StartsWith(_lastFullOutput, StringComparison.Ordinal))
        {
            newOutput = pipelineResult.Output.Substring(_lastFullOutput.Length);
        }
        else
        {
            newOutput = pipelineResult.Output;
        }

        // Commit the snippet to history and update the output baseline.
        if (newModuleSnippet != null)
            _moduleLevelSnippets.Add(newModuleSnippet);
        if (newStatementSnippet != null)
            _statementSnippets.Add(newStatementSnippet);

        _lastFullOutput = pipelineResult.Output;
        _evaluationCounter++;

        return new ReplResult(true, newOutput, pipelineResult.Diagnostics);
    }

    /// <summary>
    /// Lex + parse the input alone to (a) catch syntax errors early and (b) decide
    /// whether the input is a module-level definition or an executable statement.
    /// Inputs containing only one bare expression are rewritten as <c>print(expr)</c>.
    /// </summary>
    private InputClassification ClassifyInput(string input, out IReadOnlyList<CompilerDiagnostic> diagnostics)
    {
        var lexer = new Sharpy.Compiler.Lexer.Lexer(input, _logger);
        var tokens = lexer.TokenizeAll();
        if (lexer.Diagnostics.HasErrors)
        {
            diagnostics = lexer.Diagnostics.GetAll().ToList();
            return InputClassification.Empty;
        }

        var parser = new Sharpy.Compiler.Parser.Parser(tokens, _logger);
        var statements = parser.ParseStatements();
        if (parser.Diagnostics.HasErrors)
        {
            diagnostics = parser.Diagnostics.GetAll().ToList();
            return InputClassification.Empty;
        }

        diagnostics = Array.Empty<CompilerDiagnostic>();

        if (statements.Count == 0)
        {
            // Whitespace-only input — treat as a no-op statement snippet so that
            // history bookkeeping stays consistent.
            return new InputClassification(InputKind.Statement, "pass");
        }

        // If every statement is a valid module-level construct, classify the entire
        // input as module-level. Otherwise treat it as executable and (when it is
        // a single bare expression) rewrite into a print() call.
        bool allModuleLevel = statements.All(IsModuleLevelStatement);
        if (allModuleLevel)
        {
            return new InputClassification(InputKind.ModuleLevel, input.TrimEnd());
        }

        if (statements.Count == 1
            && statements[0] is ExpressionStatement exprStmt
            && !IsPrintCall(exprStmt.Expression))
        {
            return new InputClassification(InputKind.Statement, $"print({input.TrimEnd()})");
        }

        return new InputClassification(InputKind.Statement, input.TrimEnd());
    }

    private static bool IsModuleLevelStatement(Statement stmt) => stmt switch
    {
        FunctionDef => true,
        ClassDef => true,
        StructDef => true,
        InterfaceDef => true,
        EnumDef => true,
        UnionDef => true,
        DelegateDef => true,
        PropertyDef => true,
        TypeAlias => true,
        ImportStatement => true,
        FromImportStatement => true,
        // Module-level VariableDeclarations are allowed only when typed (or const).
        // The TypeChecker enforces this; we conservatively classify as module-level
        // and let the validator surface a precise error if untyped.
        VariableDeclaration => true,
        _ => false,
    };

    private static bool IsPrintCall(Expression expression)
    {
        return expression is FunctionCall call
            && call.Function is Identifier id
            && id.Name == "print";
    }

    /// <summary>
    /// Compose the cumulative source: module-level definitions, optionally a new
    /// definition, then a synthesized <c>main()</c> containing all accumulated
    /// statements (and optionally a new statement) indented as its body.
    /// </summary>
    private string BuildFullSource(string? extraModuleLevel, string? extraStatement)
    {
        var sb = new StringBuilder();

        foreach (var snippet in _moduleLevelSnippets)
        {
            sb.AppendLine(snippet);
            sb.AppendLine();
        }
        if (extraModuleLevel != null)
        {
            sb.AppendLine(extraModuleLevel);
            sb.AppendLine();
        }

        sb.AppendLine("def main() -> None:");

        bool wroteAnyStatement = false;
        foreach (var snippet in _statementSnippets)
        {
            AppendIndented(sb, snippet, "    ");
            wroteAnyStatement = true;
        }
        if (extraStatement != null)
        {
            AppendIndented(sb, extraStatement, "    ");
            wroteAnyStatement = true;
        }

        if (!wroteAnyStatement)
        {
            sb.AppendLine("    pass");
        }

        return sb.ToString();
    }

    private static void AppendIndented(StringBuilder sb, string snippet, string indent)
    {
        // Re-indent each non-empty line so that the snippet sits inside the
        // synthesized main() function. Blank lines are preserved verbatim to
        // keep diagnostic line numbers reasonable.
        foreach (var line in snippet.Split('\n'))
        {
            var trimmed = line.TrimEnd('\r');
            if (trimmed.Length == 0)
            {
                sb.AppendLine();
            }
            else
            {
                sb.Append(indent);
                sb.AppendLine(trimmed);
            }
        }
    }

    private CompilationOutcome CompileAndExecute(string source, CancellationToken cancellationToken)
    {
        try
        {
            // The REPL's replayed source becomes a synthetic project-of-one-file driven through
            // the unified ProjectCompiler (#1087) — no hand-rolled phase pipeline. The REPL keeps
            // only its in-memory Roslyn emit + execute. EmitLineDirectives is off so the emitted
            // C# carries no source-line mapping (it is compiled and run in memory). Feature gating
            // (SPY0331), entry-point rules, and all analysis diagnostics come from ProjectCompiler.
            var moduleRegistry = new ModuleRegistry(_logger);
            moduleRegistry.LoadReference(SharpyCoreAssembly.Location);
            var stdlibPath = Path.Combine(Path.GetDirectoryName(SharpyCoreAssembly.Location)!, "Sharpy.Stdlib.dll");
            if (File.Exists(stdlibPath))
                moduleRegistry.LoadReference(stdlibPath);
            else
                _logger.LogWarning("Sharpy.Stdlib.dll not found — stdlib modules will not be available.", 0, 0);

            var config = new ProjectConfig
            {
                ProjectFilePath = ReplFileName,
                ProjectDirectory = Directory.GetCurrentDirectory(),
                // Empty root namespace keeps generated code in the global namespace, matching the
                // single-file compile path (and what the REPL's own codegen produced before).
                RootNamespace = string.Empty,
                OutputType = "exe",
                EntryPoint = ReplFileName,
                SourceFiles = new List<string> { ReplFileName },
                InMemorySources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [ReplFileName] = source
                },
                EmitLineDirectives = false
            };

            var projectCompiler = new ProjectCompiler(
                _logger, moduleRegistry,
                warningsAsErrors: false, suppressedWarnings: null, maxErrors: 0,
                incremental: false, _emitterFactory, _features);

            var result = projectCompiler.Compile(config, cancellationToken, emitAssembly: false);
            cancellationToken.ThrowIfCancellationRequested();

            var diagnostics = result.Diagnostics.GetAll();

            // Pull the entry file's generated C# out of the project model. Absent on any
            // analysis/codegen failure, in which case the diagnostics carry the reason.
            string? generatedCSharp = null;
            if (result.ProjectModel != null)
            {
                foreach (var unit in result.ProjectModel.Units.Values)
                {
                    if (SyntheticProject.PathsEqual(unit.FilePath, ReplFileName))
                    {
                        generatedCSharp = unit.GeneratedCSharp;
                        break;
                    }
                }
            }

            if (!result.Success || generatedCSharp == null)
                return CompilationOutcome.Failure(diagnostics);

            // Carry warnings/info forward so a successful eval still surfaces them.
            var replDiagnostics = diagnostics.ToList();

            cancellationToken.ThrowIfCancellationRequested();

            // Compile generated C# to an in-memory assembly, then load and run it in-process.
            var (assemblyBytes, csharpDiagnostics) = CompileCSharp(generatedCSharp, cancellationToken);
            if (assemblyBytes == null)
            {
                replDiagnostics.AddRange(csharpDiagnostics);
                return CompilationOutcome.Failure(replDiagnostics);
            }

            cancellationToken.ThrowIfCancellationRequested();

            var output = ExecuteAssembly(assemblyBytes, cancellationToken);
            return CompilationOutcome.SuccessfulRun(output, replDiagnostics);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Surface unexpected failures as a synthetic diagnostic so callers see them.
            var diag = new CompilerDiagnostic(
                Message: $"REPL evaluation failed: {ex.Message}",
                Severity: CompilerDiagnosticSeverity.Error,
                Code: DiagnosticCodes.Infrastructure.CompilationFailed,
                Phase: CompilerPhase.Unknown);
            return CompilationOutcome.Failure(new[] { diag });
        }
    }

    // internal (not private) so the SPY0908 no-CS-leak net can be exercised directly with
    // deliberately invalid generated C# (#1059). InternalsVisibleTo covers Sharpy.Compiler.Tests.
    internal static (byte[]? Assembly, IReadOnlyList<CompilerDiagnostic> Diagnostics) CompileCSharp(
        string generatedCSharp,
        CancellationToken cancellationToken)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(generatedCSharp, cancellationToken: cancellationToken);
        var references = BuildMetadataReferences();

        var compilation = CSharpCompilation.Create(
            $"SharpyRepl_{Guid.NewGuid():N}",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.ConsoleApplication));

        using var ms = new MemoryStream();
        var emitResult = compilation.Emit(ms, cancellationToken: cancellationToken);

        if (!emitResult.Success)
        {
            // Share the assembly path's SPY0908 net so a compiler bug that emits invalid
            // C# surfaces as an internal-error diagnostic, never a bare CSxxxx code (#1059).
            // EmitLineDirectives is off in the REPL, so any mapped location is generated-C#
            // coords — that's the documented scope; SPY0908 + preserved CS text is the point.
            var diagnostics = AssemblyCompiler.MapGeneratedCodeDiagnostics(emitResult.Diagnostics);
            return (null, diagnostics);
        }

        return (ms.ToArray(), Array.Empty<CompilerDiagnostic>());
    }

    private static List<MetadataReference> BuildMetadataReferences()
    {
        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Collections").Location),
            MetadataReference.CreateFromFile(SharpyCoreAssembly.Location),
        };

        var stdlibPath = Path.Combine(Path.GetDirectoryName(SharpyCoreAssembly.Location)!, "Sharpy.Stdlib.dll");
        if (File.Exists(stdlibPath))
            references.Add(MetadataReference.CreateFromFile(stdlibPath));

        // netstandard.dll is required because Sharpy.Core targets netstandard2.1.
        // Note: missing Stdlib warning for metadata references is handled by the
        // instance method CompileAndEvaluate() via _logger (above), not here.
        try
        {
            var netstandardAssembly = Assembly.Load("netstandard");
            references.Add(MetadataReference.CreateFromFile(netstandardAssembly.Location));
        }
        catch
        {
            var runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location);
            if (!string.IsNullOrEmpty(runtimeDir))
            {
                var netstandardPath = Path.Combine(runtimeDir, "netstandard.dll");
                if (File.Exists(netstandardPath))
                    references.Add(MetadataReference.CreateFromFile(netstandardPath));
            }
        }

        return references;
    }

    /// <summary>
    /// Loads the compiled assembly into the current AppDomain and invokes its
    /// entry point with stdout redirected to a string buffer.
    /// </summary>
    private static string ExecuteAssembly(byte[] assemblyBytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var assembly = Assembly.Load(assemblyBytes);
        var entryPoint = assembly.EntryPoint
            ?? throw new InvalidOperationException("Generated assembly has no entry point.");

        var capturedOut = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(capturedOut);
        try
        {
            var parameters = entryPoint.GetParameters().Length == 0
                ? null
                : new object?[] { Array.Empty<string>() };
            entryPoint.Invoke(null, parameters);
        }
        catch (TargetInvocationException tie) when (tie.InnerException != null)
        {
            // Surface user-program exceptions as part of captured output so the
            // REPL displays them rather than crashing the host process.
            capturedOut.WriteLine($"Unhandled exception: {tie.InnerException.GetType().Name}: {tie.InnerException.Message}");
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        return capturedOut.ToString();
    }

    private const string ReplFileName = "<repl>.spy";

    /// <summary>
    /// Local helper that resolves the Sharpy.Core assembly via the externally aliased
    /// <c>SharpyRT</c> reference. Centralized so all reference-building paths agree.
    /// </summary>
    private static class SharpyCoreAssembly
    {
        public static Assembly Assembly => typeof(SharpyRT::Sharpy.Builtins).Assembly;
        public static string Location => Assembly.Location;
    }

    /// <summary>
    /// Internal carrier for compile-and-execute pipeline results. Distinct from
    /// the public <see cref="ReplResult"/> because callers don't need to see the
    /// full cumulative output; only the new output since the previous evaluation.
    /// </summary>
    private readonly record struct CompilationOutcome(
        bool Success,
        string Output,
        IReadOnlyList<CompilerDiagnostic> Diagnostics)
    {
        public static CompilationOutcome SuccessfulRun(string output, IReadOnlyList<CompilerDiagnostic> diagnostics)
            => new(true, output, diagnostics);

        public static CompilationOutcome Failure(IEnumerable<CompilerDiagnostic> diagnostics)
            => new(false, string.Empty, diagnostics.ToList());
    }

    /// <summary>
    /// Whether an input snippet should be emitted at module level or inside the
    /// synthesized <c>main()</c> function.
    /// </summary>
    private enum InputKind
    {
        ModuleLevel,
        Statement,
    }

    private readonly record struct InputClassification(InputKind Kind, string Source)
    {
        public static InputClassification Empty => new(InputKind.Statement, string.Empty);
    }
}
