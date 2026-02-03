using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Semantic;

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
