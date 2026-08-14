using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.CodeGen;

/// <summary>
/// Maintains state during code generation
/// </summary>
internal class CodeGenContext
{
    private readonly DiagnosticBag _diagnostics = new();

    public SymbolTable SymbolTable { get; }
    public BuiltinRegistry Builtins { get; }
    public string? SourceFilePath { get; set; }

    /// <summary>
    /// Whether the emitted C# will be compiled into a TEST HOST — an xUnit project that supplies the
    /// framework reference and runs the result through a test runner (#1495).
    ///
    /// <para>Default FALSE, which is the answer for every ordinary compilation: <c>sharpyc run</c>,
    /// <c>compile</c>, <c>emit csharp</c>, the REPL and LSP. A <c>@test</c> function's asserts lower
    /// framework-free there — an ordinary runtime check raising <c>Sharpy.AssertionError</c>, the
    /// same lowering an assert outside <c>@test</c> has always had, and the direction #1413 started
    /// with <c>assert_raises</c>. Before this the rewrites emitted <c>Xunit.Assert.*</c>
    /// unconditionally, so any program containing a <c>@test</c> function was uncompilable under
    /// <c>run</c>: CS0246 behind SPY0908, an internal-compiler-error report for an ordinary program.
    /// </para>
    ///
    /// <para>TRUE only where the framework is actually there and buys runner integration: a
    /// <c>.spyproj</c> declaring <c>&lt;TestHost&gt;true&lt;/TestHost&gt;</c> (the spy-test corpora),
    /// and the integration-test harness, which supplies an Xunit reference of its own. Those paths
    /// keep the Xunit lowering byte-for-byte, which is the owner's condition on this change.</para>
    ///
    /// <para>TODO(#1532): two surfaces still ignore this flag and emit Xunit unconditionally — the
    /// test-CLASS emission (so a module-level caller cannot reach a <c>@test</c> function under
    /// <c>run</c>, CS0103) and the TUPLE form of <c>assert isinstance(x, (A, B))</c>, which has no
    /// framework-free lowering (CS1503). #1495 closes when those follow.</para>
    /// </summary>
    public bool TargetsTestHost { get; set; }

    /// <summary>
    /// When true, emits #line directives in generated C# for source mapping.
    /// This enables .spy file names and line numbers in runtime stack traces.
    /// Defaults to true. Set to false when inspecting raw generated C#.
    /// </summary>
    public bool EmitLineDirectives { get; set; } = true;

    /// <summary>
    /// Structured diagnostics from code generation.
    /// </summary>
    public DiagnosticBag Diagnostics => _diagnostics;

    /// <summary>
    /// Returns true if there are any errors
    /// </summary>
    public bool HasErrors => _diagnostics.HasErrors;

    /// <summary>
    /// Add an error during code generation
    /// </summary>
    public void AddError(string message, string? code = null, int? line = null, int? column = null)
    {
        _diagnostics.AddError(message, line, column, SourceFilePath, code, CompilerPhase.CodeGeneration);
    }

    /// <summary>
    /// Add a warning during code generation
    /// </summary>
    public void AddWarning(string message, string? code = null, int? line = null, int? column = null)
    {
        _diagnostics.AddWarning(message, line, column, SourceFilePath, code, CompilerPhase.CodeGeneration);
    }

    /// <summary>
    /// Add an informational diagnostic during code generation
    /// </summary>
    public void AddInfo(string message, string? code = null, int? line = null, int? column = null)
    {
        _diagnostics.AddInfo(message, line, column, SourceFilePath, code, CompilerPhase.CodeGeneration);
    }

    /// <summary>
    /// Project root namespace (for multi-file compilation)
    /// </summary>
    public string? ProjectNamespace { get; set; }

    /// <summary>
    /// Project root directory path (for computing relative namespaces)
    /// </summary>
    public string? ProjectRootPath { get; set; }

    /// <summary>
    /// If true, this file should generate a Main entry point.
    /// Defaults to false; explicitly set to true for the entry point file.
    /// </summary>
    public bool IsEntryPoint { get; set; } = false;

    /// <summary>
    /// If true, this file is a package __init__.spy file.
    /// Package init files re-export from-imported symbols as delegating members.
    /// Regular library modules do not re-export to avoid CS0229 ambiguity.
    /// </summary>
    public bool IsPackageInit { get; set; } = false;

    /// <summary>
    /// Logger for code generation warnings and messages.
    /// </summary>
    public ICompilerLogger Logger { get; set; } = NullLogger.Instance;

    /// <summary>
    /// Semantic binding storing semantic data separate from AST nodes.
    /// Used to retrieve import resolution data without reading from mutable AST properties.
    /// </summary>
    public SemanticBinding SemanticBinding { get; set; } = new();

    /// <summary>
    /// Semantic info providing expression type information from type checking.
    /// Used for tagged union constructor detection (Some/None()/Ok/Err).
    /// </summary>
    public SemanticInfo? SemanticInfo { get; set; }

    /// <summary>
    /// The project's middle-end lowering IR, built once per project immediately before code
    /// generation (E2, #1056). Handed to the emitter's context so migrated constructs can read
    /// their lowering facts from the IR instead of <see cref="SemanticInfo"/> side-tables. In E2
    /// Phase 1 nothing in the emitter reads it yet; the first migrated construct will.
    /// </summary>
    public Lowering.IrCompilation? Ir { get; set; }

    /// <summary>
    /// The effective experimental features enabled for this file during code generation:
    /// compilation-wide <see cref="CompilerOptions.Features"/> unioned with this file's
    /// per-file <c>from __future__ import</c> features. Consumers gate
    /// <see cref="FeatureScope.CodeGen"/>-scoped emission behavior on this set. Defaults to
    /// <see cref="FeatureFlags.None"/>. Nothing in CodeGen reads it yet; the first gated
    /// pilot feature will.
    /// </summary>
    public FeatureFlags Features { get; set; } = FeatureFlags.None;

    public CodeGenContext(SymbolTable symbolTable, BuiltinRegistry builtins)
    {
        SymbolTable = symbolTable;
        Builtins = builtins;
    }

    public Symbol? LookupSymbol(string name)
    {
        return SymbolTable.Lookup(name);
    }

    public bool IsBuiltinFunction(string name)
    {
        return Builtins.GetFunction(name) != null;
    }

    public bool IsBuiltinType(string name)
    {
        return Builtins.GetType(name) != null;
    }
}
