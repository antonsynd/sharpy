namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Information computed during semantic analysis for use during code generation.
/// Attached to symbols after type checking to avoid recomputing names during emission.
///
/// This is a TWO-WAY DOOR decision: CodeGenInfo is purely additive and can be
/// removed without affecting other functionality.
/// </summary>
public sealed record CodeGenInfo
{
    /// <summary>
    /// The C# name to use for this symbol (with proper casing applied).
    /// For variables: camelCase (local) or PascalCase (module-level)
    /// For constants: CONSTANT_CASE
    /// For types: PascalCase
    /// For methods: PascalCase
    /// </summary>
    public required string CSharpName { get; init; }

    /// <summary>
    /// The original Sharpy name (preserved for diagnostics and debugging).
    /// </summary>
    public required string OriginalName { get; init; }

    /// <summary>
    /// For redeclared variables, the version number (0 for first declaration, 1 for first redeclaration, etc.).
    /// This maps to variable names like: x, x_1, x_2, etc.
    /// </summary>
    public int Version { get; init; } = 0;

    /// <summary>
    /// If true, this is a module-level variable/constant (becomes a static field in C#).
    /// </summary>
    public bool IsModuleLevel { get; init; }

    /// <summary>
    /// If true, use CONSTANT_CASE and emit as `const` in C#.
    /// </summary>
    public bool IsConstant { get; init; }

    /// <summary>
    /// If true, this variable should not become a module-level field due to execution order issues.
    /// Example: Variables that depend on runtime values in their initializers.
    /// </summary>
    public bool HasExecutionOrderIssues { get; init; }

    /// <summary>
    /// For enum types, indicates if this is a string enum (has string values).
    /// String enums are generated as classes with static readonly fields instead of C# enums.
    /// </summary>
    public bool IsStringEnum { get; init; }

    /// <summary>
    /// For imported symbols, indicates how the symbol was imported.
    /// </summary>
    public ImportKind ImportKind { get; init; } = ImportKind.None;

    /// <summary>
    /// For aliased imports, the original name (e.g., "from config import MAX_VALUE as MAX" → "MAX_VALUE").
    /// </summary>
    public string? OriginalImportName { get; init; }

    /// <summary>
    /// For discovery-loaded (CLR) methods, the original CLR method name (e.g., "IsOSPlatform").
    /// Code generation emits this verbatim instead of round-tripping through name mangling,
    /// which would corrupt acronym casing (IsOSPlatform → is_os_platform → IsOsPlatform).
    /// </summary>
    public string? ClrMethodName { get; init; }

    /// <summary>
    /// True when this method (in a class whose base chain contains a CLR-backed type)
    /// overrides an abstract/virtual member of that CLR base (#1122). Detected in semantic
    /// analysis (<see cref="TypeChecker"/>) and frozen here at <c>MaterializeCodeGenInfo</c>;
    /// code generation emits the <c>override</c> modifier from this frozen fact without any
    /// reflection or re-derivation. Pure-Sharpy hierarchies keep decorator-driven override.
    /// </summary>
    public bool OverridesClrBaseMember { get; init; }

    /// <summary>
    /// For a class that declares no <c>__init__</c> and inherits constructors from an ancestor, the
    /// forwarders code generation must synthesize — with the base clause's written type arguments
    /// already substituted into every parameter (#1408). Null when the class declares its own
    /// constructors, when no ancestor declares any, or when the base chain's arguments cannot be
    /// read (such a base stays UNKNOWN rather than being guessed from arity — #1287 Design
    /// Decision 2); code generation then keeps its own nearest-ancestor walk.
    /// </summary>
    /// <remarks>
    /// The one non-scalar fact on this record, and it has to be keyed on the DERIVED symbol:
    /// <see cref="TypeSymbol.Constructors"/> is built once from the OPEN definition and shared by
    /// every instantiation, so <c>List[T].List(IEnumerable[T])</c> is the same
    /// <see cref="FunctionSymbol"/> for <c>IntList</c> and for <c>StrList</c>. Substituting it onto
    /// the ancestor would corrupt the other derived classes; substituting it in the emitter would be
    /// the emitter making a type decision (CLAUDE.md Rule 2). Code generation reads this verbatim.
    /// </remarks>
    public IReadOnlyList<FunctionSymbol>? ForwardingConstructors { get; init; }

    // ============================================================
    // FUTURE EXTENSIBILITY (for v0.2.x+)
    // These fields are reserved for future features. They are
    // nullable/optional and won't affect current functionality.
    // ============================================================

    /// <summary>
    /// Reserved for tagged unions (v0.2.x): The discriminator value for union cases.
    /// </summary>
    public int? UnionDiscriminatorValue { get; init; }

    /// <summary>
    /// Reserved for async/await (v0.2.x): The state ID in async state machine.
    /// </summary>
    public int? AsyncStateId { get; init; }

    /// <summary>
    /// Reserved for properties: The accessor method name for property getters/setters.
    /// </summary>
    public string? PropertyAccessorName { get; init; }

    /// <summary>
    /// Get the versioned C# name (includes version suffix for redeclared variables).
    /// </summary>
    public string GetVersionedCSharpName()
    {
        if (Version == 0)
            return CSharpName;
        return $"{CSharpName}_{Version}";
    }
}

/// <summary>
/// How a symbol was imported into the current module.
/// </summary>
public enum ImportKind
{
    /// <summary>Not imported (defined locally).</summary>
    None,

    /// <summary>Imported via "import module" - accessed as module.member.</summary>
    ModuleImport,

    /// <summary>Imported via "from module import symbol" - accessed directly.</summary>
    FromImport,

    /// <summary>Imported via "from module import symbol as alias" - accessed via alias.</summary>
    FromImportWithAlias
}
