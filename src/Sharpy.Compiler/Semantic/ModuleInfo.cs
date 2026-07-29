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
    /// <summary>
    /// The module's exports as loaded, before they become a <see cref="ModuleSymbol"/>. Same
    /// <see cref="ModuleExports"/> unit the symbol layer uses, so the staging layer cannot desync
    /// its two views either and the hand-off is a single copy of both (#1145).
    /// </summary>
    public ModuleExports ExportedSymbols { get; init; } = new();

    /// <summary>
    /// Exported types keyed by name — a read-only view of <see cref="ExportedSymbols"/>, kept
    /// separate from the value-position lookup so an export (e.g. a static field) that shares a
    /// name with a type does not shadow the type in annotation position. CPython's
    /// <c>sqlite3.Row</c> is both a type and the <c>row_factory</c> value; Sharpy splits it into
    /// the <c>Row</c> type and the <c>Row</c> factory field, which collide in the value view (the
    /// field overwrites the type). <c>ResolveQualifiedType</c> consults this lookup first so both
    /// uses coexist (#1092).
    /// </summary>
    public IReadOnlyDictionary<string, TypeSymbol> ExportedTypes => ExportedSymbols.Types;
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
    /// The C# namespace of the [SharpyModule] class (e.g., "Sharpy" for stdlib, "MyLib" for third-party).
    /// Used by the emitter to generate fully-qualified using directives for cross-namespace imports.
    /// Null for .spy source modules and CLR namespace modules.
    /// </summary>
    public string? CSharpNamespace { get; init; }

    /// <summary>
    /// The simple C# class name of the [SharpyModule] class backing this module
    /// (e.g., "EmailModule" for "email"). Used by the emitter to alias the module
    /// to its real class instead of guessing a PascalCase name. Null for .spy source
    /// modules and CLR namespace modules.
    /// </summary>
    public string? CSharpClassName { get; init; }

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
