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
    /// <para>
    /// SAME-FILE-ONLY, deliberately not serialized: CodeGenInfo that drives a symbol's own
    /// declaration emission is always freshly computed by <c>CodeGenInfoComputer</c> for the file
    /// being emitted — cached/deserialized CodeGenInfo serves only cross-file reference
    /// resolution, which never emits the declaration. Same invariant as
    /// <c>OverridesClrBaseMember</c>/<c>ForwardingConstructors</c>; classified same-file-only in
    /// <c>SymbolFactMirrorConformanceTests</c>, which fails if the property is left unclassified.
    /// </para>
    /// </summary>
    public bool IsCompileTimeConstant { get; init; }

    /// <summary>
    /// An int-enum member's python name when its emitted C# identifier differs from it
    /// (<c>red</c> → <c>Red</c>), else null (#2007, #2069). The emitter stamps it as
    /// <c>[global::Sharpy.SharpyFieldName("red")]</c> on the enum field — attribute-when-different
    /// (#1607) — and Core's <c>Builtins.EnumName</c> reads it for <c>str</c>, <c>repr</c> and
    /// <c>.name</c>. A string-enum member carries its name as a constructor argument and never
    /// sets it. SAME-FILE-ONLY (it drives the member's own declaration emission; same invariant as
    /// <see cref="IsCompileTimeConstant"/>).
    /// </summary>
    public string? EnumMemberPythonName { get; init; }

    /// <summary>
    /// If true, this variable should not become a module-level field due to execution order issues.
    /// Example: Variables that depend on runtime values in their initializers.
    /// </summary>
    public bool HasExecutionOrderIssues { get; init; }

    /// <summary>
    /// If true, this bare local's assignment can be skipped by a suppression-capable <c>with</c>
    /// block (#1839, R-AI): the emitter declares a companion <c>bool</c> assigned-flag, sets it at
    /// every store, and checks it at every runtime-checked read (<c>Builtins.CheckedLocal</c>), so an
    /// unset read raises <c>UnboundLocalError</c> at runtime instead of being refused.
    /// </summary>
    public bool HasRuntimeAssignedFlag { get; init; }

    /// <summary>
    /// For enum types, indicates if this is a string enum (has string values).
    /// String enums are generated as classes with static readonly fields instead of C# enums.
    /// </summary>
    public bool IsStringEnum { get; init; }

    /// <summary>
    /// True for a <c>@dataclass</c>/<c>struct</c> field whose default is the mutable-collection
    /// family (#1684, R-A) — a list/dict/set literal, a <c>list()</c>/<c>dict()</c>/<c>set()</c>
    /// call, or a list/dict/set comprehension. Set at <c>CodeGenInfoComputer.ProcessField</c> from
    /// <c>Validation.ConstantDefaultClassifier.Classify</c> — the ONE classification authority
    /// (also read by <c>ConstantPositionValidator</c>'s <c>AdmissionTable.PerInstanceFieldDefault</c>
    /// admission) — so code generation never re-derives the shape from the AST (CLAUDE.md Rule 2).
    /// When true, the synthesized dataclass/struct constructor must lower this field's default to a
    /// per-instance initializer — a sentinel <c>T? name = null</c> parameter plus
    /// <c>if (name is null) { &lt;default expression&gt; } else { this.Field = name; }</c> — rather
    /// than a C# default-parameter value, which Roslyn refuses for a non-constant expression
    /// (CS1736). The default is evaluated ONLY on the absent-argument branch and the member's own
    /// initializer is dropped, because a field/property initializer runs on every construction and
    /// the family this fact admits is classified by AST SHAPE with no purity check — an impure
    /// default (<c>[side(1)]</c>, a comprehension over a call) was otherwise evaluated and discarded
    /// on the explicit-argument path (#1901). False for a field with no
    /// default, a <c>const</c> field, a <c>@static</c> field, or a default the validator refuses
    /// outright (nullable-typed, tuple, call, etc.) — those keep the unchanged
    /// <c>GenerateParameterDefault</c> path (or never reach code generation at all).
    /// </summary>
    public bool RequiresPerInstanceDefault { get; init; }

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
    /// True when this method's body contains a METHOD-lowered <c>super()</c> call (#1740) — a call
    /// to a non-operator dunder or a regular method via <c>super()</c>, which needs <c>base.Method()</c>
    /// to bypass virtual dispatch and reach the PARENT's own implementation (a cast-based call like
    /// <c>((Base)this).Method()</c> would still virtual-dispatch to THIS class's override). <c>base</c>
    /// is legal only inside an instance method, so when this method is itself an operator dunder
    /// (emitted as a static C# <c>operator</c>) it needs the instance <c>_Impl</c> split so the
    /// super call has an instance-method body to live in. An OPERATOR-dunder super call
    /// (<c>super().__add__(x)</c>) never sets this: it is tagged
    /// <see cref="OperatorLoweringKind.SuperOperatorApplication"/> on its own call node instead and
    /// lowers to a cast-based operator application that works inline, in any host. Detected in
    /// semantic analysis (<see cref="TypeChecker"/>) and frozen here at <c>MaterializeCodeGenInfo</c>;
    /// code generation reads this instead of re-deriving it with a kind-enumerating AST walk.
    /// </summary>
    public bool RequiresInstanceImpl { get; init; }

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
    /// For equality/comparison dunders (<c>__eq__</c>, <c>__ne__</c>, <c>__lt__</c>, etc.):
    /// the shape of the non-self parameter that determines how the C# operator body is
    /// generated. <see cref="EqualityParameterShape.Reference"/> means <c>right is null</c>
    /// is valid; <see cref="EqualityParameterShape.ValueOrOptional"/> means it is not (#1719).
    /// Null for non-dunder methods and for the <c>__eq__(self, other: object)</c> override.
    /// </summary>
    public EqualityParameterShape? OperatorParameterShape { get; init; }

    /// <summary>
    /// For an imported <see cref="ModuleSymbol"/> (<c>import thing</c>): the namespace its members
    /// class and sibling types live in, relative to the root namespace — <c>pkg/thing.spy</c> →
    /// [Pkg, Thing] (<see cref="Shared.ModuleIdentifiers.LayoutNamespaceSegments"/>); for a .NET
    /// (discovered) module, its reflected namespace. Recorded once at <c>CodeGenInfoComputer</c> so
    /// every emitter family reads the one layout (#2039, Decision 28 (e)). Null for any other symbol.
    /// </summary>
    public IReadOnlyList<string>? NamespaceSegments { get; init; }

    /// <summary>
    /// For an imported <see cref="ModuleSymbol"/>: <c>&lt;X&gt;</c>, the class its functions, variables
    /// and constants live in — <c>ThingModule</c> for <c>thing.spy</c>
    /// (<see cref="Shared.ModuleIdentifiers.LayoutMembersClassName"/>); for a .NET module, its
    /// reflected module class. Null for any other symbol (#2039).
    /// </summary>
    public string? MembersClassName { get; init; }

    /// <summary>
    /// For a type symbol: true when the type is a top-level declaration of a Sharpy module, which the
    /// module-as-namespace layout emits BESIDE the module's members class in the module namespace
    /// rather than nested inside it (#2039, Decision 28 (a)). False for a nested type and for a type
    /// discovered from a .NET assembly. Set for the declaring file's own top-level types and for a type
    /// from-imported out of a Sharpy module.
    /// </summary>
    public bool IsNamespaceSibling { get; init; }

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
/// The shape of the non-self parameter on a comparison/equality dunder, determining how
/// the C# operator body handles null (#1719, #1806).
/// </summary>
public enum EqualityParameterShape
{
    /// <summary>The parameter is a reference type — <c>right is null</c> is valid C#.</summary>
    Reference,
    /// <summary>The parameter is a value type or Optional&lt;T&gt; — no null pattern.</summary>
    ValueOrOptional,
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
