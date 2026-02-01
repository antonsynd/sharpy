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
public class ModuleInfo
{
    public string Path { get; init; } = string.Empty;
    public Module Module { get; init; } = null!;
    public Dictionary<string, Symbol> ExportedSymbols { get; init; } = new();
    public bool IsNetModule { get; init; } = false;

    /// <summary>
    /// The canonical module name (e.g., "mypackage.submodule") derived from the file path.
    /// Used for DefiningModule tracking in re-exported symbols.
    /// </summary>
    public string? CanonicalModuleName { get; init; }
}
