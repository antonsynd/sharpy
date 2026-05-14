using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Entry in the import chain for error reporting
/// </summary>
internal record ImportChainEntry(
    string ModulePath,
    int? LineStart,
    int? ColumnStart,
    string? ImportingModule
);

/// <summary>
/// Information about a loaded module
/// </summary>
/// <remarks>
/// When <see cref="IsNetModule"/> is true, the <see cref="Module"/> property will be null
/// because .NET assemblies don't have an AST representation. Always check <see cref="IsNetModule"/>
/// before accessing <see cref="Module"/> to avoid null reference errors.
/// </remarks>
internal class ModuleInfo
{
    public string Path { get; init; } = string.Empty;
    public Module Module { get; init; } = null!;
    public Dictionary<string, Symbol> ExportedSymbols { get; init; } = new();
    public Dictionary<string, List<FunctionSymbol>> FunctionOverloads { get; init; } = new();
    public bool IsNetModule { get; init; } = false;

    /// <summary>
    /// The .NET namespace name for CLR namespace modules (e.g., "System" for "system").
    /// Null for Sharpy stdlib modules loaded via [SharpyModule] and non-.NET modules.
    /// </summary>
    public string? NetNamespaceName { get; init; }

    /// <summary>
    /// The canonical module name (e.g., "mypackage.submodule") derived from the file path.
    /// Used for DefiningModule tracking in re-exported symbols.
    /// </summary>
    public string? CanonicalModuleName { get; init; }

    /// <summary>
    /// Indicates if this is an error recovery module created when the actual module couldn't be found.
    /// Error recovery modules contain placeholder symbols that suppress cascading errors in TypeChecker.
    /// </summary>
    public bool IsErrorRecovery { get; init; } = false;

    /// <summary>
    /// Indicates this is a stub module created during circular import deferral.
    /// Stub modules contain only type declarations (class/struct/interface/enum names)
    /// — enough for type annotation resolution but not for runtime usage.
    /// </summary>
    public bool IsStub { get; init; } = false;

    /// <summary>
    /// The file path that triggered the circular import cycle that created this stub.
    /// Only set when <see cref="IsStub"/> is true.
    /// </summary>
    public string? StubSourcePath { get; init; }
}
