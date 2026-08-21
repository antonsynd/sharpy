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
    /// True when a const variable's initializer can be emitted as a C# <c>const</c> field rather
    /// than <c>static readonly</c>. Set at <c>MaterializeCodeGenInfo</c> from
    /// <see cref="VariableSymbol.ConstantValue"/> and const-eligible type (#1460). The emitter reads
    /// this in the module-level path instead of inspecting the AST via <c>IsCompileTimeLiteral</c>,
    /// so expressions like <c>100 + 100</c> emit as <c>const</c> — C# folds them.
    /// </summary>
    public bool IsCompileTimeConstant { get; init; }

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
    /// True when the <c>override</c> modifier on this method targets an interface method rather
    /// than a base-class method, meaning C# requires the keyword be stripped (#1519).
    /// Computed in semantic analysis from the type hierarchy; code generation reads this fact
    /// without re-walking the hierarchy.
    /// </summary>
    public bool StripsOverrideKeyword { get; init; }

    /// <summary>
    /// True when this method implements an interface method, meaning the emitter should add
    /// <c>virtual</c> so subclasses can override it (#1519). Computed in semantic analysis;
    /// code generation reads this fact directly.
    /// </summary>
    public bool ImplementsInterfaceMethod { get; init; }

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

    /// <summary>
    /// The explicit-interface bridges a class must emit so its <c>Self</c>-annotated interface
    /// members bind (#1342) — one per (implemented interface × Self-mentioning member), with the
    /// interface's composed base-clause arguments and the resolved implementing member already
    /// baked in. Null when the class implements no interface with a bridged <c>Self</c> member.
    /// </summary>
    /// <remarks>
    /// Symbol-keyed on the class (Rule 2a), like <see cref="ForwardingConstructors"/>: the composed
    /// interface instantiation and the implementing member (which may be inherited from a base
    /// class, shape 3) are semantic facts the emitter must not re-derive. Code generation reads
    /// these verbatim in <c>GenerateSelfInterfaceBridges</c>.
    /// </remarks>
    public IReadOnlyList<SelfInterfaceBridgeSpec>? SelfInterfaceBridges { get; init; }

    /// <summary>
    /// Protocol interfaces synthesized by <see cref="SynthesisAnalyzer"/> for this type declaration,
    /// pre-filtered against the explicit base list. Computed in semantic analysis and frozen here
    /// so the emitter reads the list without re-running the analyzer (#1521).
    /// </summary>
    public IReadOnlyList<SynthesizedInterfaceInfo>? SynthesizedInterfaces { get; init; }

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
