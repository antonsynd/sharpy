using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Represents a symbol in the symbol table.
/// </summary>
/// <remarks>
/// Symbol uses reference-based equality (not value-based) because symbols have mutable
/// properties (Type, BaseType) that are set after creation during semantic analysis.
/// Using value-based equality (the record default) would make hash codes unstable, causing
/// dictionary/set lookups to fail silently after mutation. Reference equality makes symbols
/// safe to use as dictionary keys in any collection without requiring ReferenceEqualityComparer.
/// </remarks>
public abstract record Symbol
{
    public string Name { get; init; } = string.Empty;
    public SymbolKind Kind { get; init; }
    public bool IsNameBacktickEscaped { get; init; }
    public AccessLevel AccessLevel { get; internal set { ThrowIfFrozen(); field = value; } } = AccessLevel.Public;
    public int? DeclarationLine { get; init; }
    public int? DeclarationColumn { get; init; }

    /// <summary>
    /// Line/column of the symbol's name token within its declaration.
    /// For <c>async def foo</c>, DeclarationLine points to <c>async</c> while
    /// NameDeclarationLine points to <c>foo</c>. Falls back to DeclarationLine/Column
    /// when not explicitly set (e.g. variables where the name IS the declaration start).
    /// </summary>
    public int? NameDeclarationLine { get; init; }
    public int? NameDeclarationColumn { get; init; }

    /// <summary>
    /// Exclusive end column of the symbol's name token, copied from the AST node's
    /// <c>NameColumnEnd</c> (which the parser records as <c>nameToken.Column + nameToken.Length</c>,
    /// escape-aware by construction per #1281). Null for symbols with no parsed node — CLR imports,
    /// synthesized members — which fall back to the derivation in
    /// <see cref="EffectiveNameColumnEnd"/>.
    ///
    /// <para>
    /// There is deliberately no <c>NameDeclarationLineEnd</c>: an identifier cannot span lines in
    /// this lexer (a backticked name is a single-line token), so the name's end line is always
    /// <see cref="EffectiveNameLine"/>.
    /// </para>
    /// </summary>
    public int? NameDeclarationColumnEnd { get; init; }

    public int? EffectiveNameLine => NameDeclarationLine ?? DeclarationLine;
    public int? EffectiveNameColumn => NameDeclarationColumn ?? DeclarationColumn;

    /// <summary>
    /// Exclusive end column of the name, preferring the recorded extent (#1454) and falling back to
    /// the spelling-derived length for symbols that never had a parsed node.
    ///
    /// <para>
    /// This fallback is the LAST place in the codebase that reconstructs a name extent from
    /// <c>Name.Length</c> plus a backtick allowance. Handlers must read this accessor rather than
    /// re-deriving the arithmetic themselves (plan-80eee2 Design Decision 7).
    /// </para>
    /// </summary>
    public int? EffectiveNameColumnEnd =>
        NameDeclarationColumnEnd
        ?? (EffectiveNameColumn is int start
            ? start + Name.Length + (IsNameBacktickEscaped ? 2 : 0)
            : null);

    /// <summary>
    /// The source span of this symbol's declaration (for LSP go-to-definition).
    /// </summary>
    public Text.TextSpan? DeclarationSpan { get; init; }

    /// <summary>
    /// The file path where this symbol is declared.
    /// Populated during name resolution for all symbol types.
    /// </summary>
    public string? DeclaringFilePath { get; init; }

    /// <summary>
    /// The builtin this symbol dispatches as, when it is bound under a DIFFERENT spelling — set
    /// only by <c>from builtins import len as blen</c> and friends.
    ///
    /// <para>
    /// Every builtin-vs-user decision is made by reference identity against the registry
    /// (<c>BuiltinRegistry.IsBuiltinSymbol</c>, the #1241 rule), and the registry is keyed by the
    /// builtin's own name. An alias needs both halves at once: it must BIND as <c>blen</c>, so the
    /// name resolves, while still BEING <c>len</c>, so dispatch, return-type inference and the
    /// emitter's builtin arm all recognize it. Cloning the registry symbol under the new name gave
    /// the first half and destroyed the second — the clone looked like a user function shadowing
    /// the builtin, so the call was ranked against the raw discovered overload set where
    /// <c>Len(ICollection)</c>, <c>Len(ISized)</c> and <c>Len(object)</c> all match a
    /// <c>list[int]</c> equally well, and the program got SPY0353 (#1383).
    /// </para>
    ///
    /// <para>
    /// This is the same identity-vs-spelling split the backtick escape uses (#1281/#1326): the
    /// spelling lives in <see cref="Name"/>, the identity lives here.
    /// </para>
    /// </summary>
    public Symbol? BuiltinAliasOf { get; init; }

    /// <summary>
    /// The symbol this one was cloned from at an import or alias boundary; identity lives here,
    /// spelling in <see cref="Name"/>. Used for shadow dispatch: a bound symbol that IS one of
    /// the overloads (or a clone of one) is not shadowed (#1525).
    /// </summary>
    public Symbol? OriginSymbol { get; init; }

    /// <summary>
    /// Indicates if this symbol is re-exported from another module (e.g., via "from .submodule import func")
    /// </summary>
    public bool IsReExport { get; init; }

    /// <summary>
    /// For re-exported symbols, the original module name where the symbol was defined
    /// </summary>
    public string? OriginalModule { get; init; }

    /// <summary>
    /// Indicates if this is an error recovery symbol created when imports fail.
    /// Error recovery symbols suppress cascading errors - when TypeChecker encounters
    /// an error recovery symbol, it skips reporting "undefined identifier" errors
    /// since the root cause (failed import) was already reported.
    /// </summary>
    public bool IsErrorRecovery { get; init; }

    /// <summary>
    /// The access level explicitly set via decorator (@public, @protected, @private, @internal).
    /// When non-null, this overrides the name-based access level convention.
    /// </summary>
    public AccessLevel? ExplicitAccessLevel { get; internal set { ThrowIfFrozen(); field = value; } }

    /// <summary>
    /// If non-null, this symbol is deprecated and this is the deprecation message.
    /// Populated from @deprecated("msg") decorator during name resolution.
    /// </summary>
    public string? DeprecationMessage { get; init; }

    /// <summary>
    /// Documentation string for this symbol (from source docstrings or XML docs).
    /// Null until documentation is populated during name resolution or assembly discovery.
    /// </summary>
    /// <remarks>
    /// Uses 'internal set' because documentation is populated after symbol creation.
    /// For Sharpy source, populated from AST DocString
    /// during name resolution. For .NET assemblies, populated from XML doc files
    /// during assembly discovery.
    /// </remarks>
    public string? Documentation { get; internal set { ThrowIfFrozen(); field = value; } }

    /// <summary>
    /// Use reference equality for symbols. Record default (value equality) is unsafe
    /// because mutable properties would make hash codes unstable.
    /// </summary>
    public virtual bool Equals(Symbol? other) => ReferenceEquals(this, other);

    public override int GetHashCode() => RuntimeHelpers.GetHashCode(this);

    public override string ToString() => $"{Kind} '{Name}'";

    /// <summary>
    /// The symbol this one was frozen as. A symbol is frozen when this is the symbol itself, so a
    /// <c>with</c> copy — which copies the field but is a different object — is born unfrozen: the
    /// copy is a new declaration its maker owns, the original stays the master's.
    /// </summary>
    private Symbol? _frozenAs;

    /// <summary>
    /// True for a symbol owned by a master <see cref="Registry.BuiltinRegistry"/>, frozen when its
    /// construction completes. Every analysis on a <c>CompilerApi</c> reads the same master (the
    /// per-compilation clone was retired at #1633), so a write to one is a write every later
    /// document reads — a user <c>def len</c> inside a <c>try:</c> body retyped the builtin
    /// <c>len</c> for every document analysed after it (#2250).
    /// </summary>
    internal bool IsFrozen => ReferenceEquals(_frozenAs, this);

    /// <summary>
    /// Freezes this symbol: every settable property, and every member list or overload table it owns
    /// (<see cref="SymbolMemberList{T}"/>, <see cref="SymbolOverloadTable"/>), throws on a write from
    /// now on (#2250).
    /// </summary>
    internal void Freeze()
    {
        _frozenAs = this;
        FreezeMembers();
    }

    /// <summary>Freezes the member collections this symbol owns — every one of them (#2250).</summary>
    private protected virtual void FreezeMembers()
    {
    }

    /// <summary>
    /// True when this symbol was declared FOR <paramref name="definition"/>: the declaring site stamps the
    /// definition's span (its start position for a span-less, synthesized definition). A same-named symbol found by a
    /// name lookup — a builtin, an import, an enclosing definition — carries another span or none. The one rule every
    /// pass that WRITES the symbol of a definition it is walking applies before writing (#2250).
    /// </summary>
    internal bool IsDeclaredAt(Parser.Ast.Node definition)
        => definition.Span is { } span
            ? DeclarationSpan == span
            : DeclarationLine == definition.LineStart && DeclarationColumn == definition.ColumnStart;

    /// <summary>
    /// Refuses a write to a frozen symbol — always on, not DEBUG-only, like
    /// <see cref="SemanticBinding"/>'s phase freezes: a write that reached a master symbol is a
    /// silent retype of every later document, and an internal error is the better failure.
    /// </summary>
    private protected void ThrowIfFrozen([CallerMemberName] string property = "")
    {
        if (IsFrozen)
        {
            throw new Diagnostics.PhaseViolationException(
                $"write {GetType().Name}.{property}", "construction of the master BuiltinRegistry", Name);
        }
    }
}

/// <summary>
/// Variable or field symbol
/// </summary>
public record VariableSymbol : Symbol
{
    /// <summary>
    /// The resolved type of this variable.
    /// </summary>
    /// <remarks>
    /// Uses 'internal set' because type resolution happens after symbol creation.
    /// Readers should prefer SemanticBinding.GetVariableType when available.
    /// </remarks>
    public SemanticType Type { get; internal set { ThrowIfFrozen(); field = value; } } = SemanticType.Unknown;
    public bool IsParameter { get; init; }
    public bool IsConstant { get; init; }
    public bool IsStatic { get; init; }

    /// <summary>
    /// Indicates the field was declared with the <c>@final</c> decorator.
    /// Final fields can only be assigned within constructors of the declaring type
    /// and emit a C# <c>readonly</c> modifier.
    /// </summary>
    public bool IsFinal { get; init; }
    public bool HasDefaultValue { get; init; }

    /// <summary>
    /// The original CLR field name for discovery-loaded module fields.
    /// Used by code generation to emit the exact CLR name (e.g. "Pi")
    /// while the Sharpy-side key uses the canonical spelling ("pi").
    /// </summary>
    public string? ClrFieldName { get; init; }

    /// <summary>
    /// The folded integer constant value for this variable, when it is a <c>const</c> whose
    /// initializer folds to a <see cref="System.Numerics.BigInteger"/> via
    /// <see cref="IntegerConstantEvaluator"/> and whose declared type is an integer primitive.
    /// Null for non-const variables, non-integer consts, and consts whose initializers cannot
    /// be folded. Used by <c>IsImplicitConstantConversion</c> to treat a const reference as a
    /// constant expression at every §10.2.11 site (#1460).
    /// </summary>
    public System.Numerics.BigInteger? ConstantValue { get; internal set { ThrowIfFrozen(); field = value; } }

    /// <summary>
    /// The compile-time-constant fact for a <c>const</c> symbol that arrived from ANOTHER module —
    /// an in-project <c>.spy</c> import (<c>ModuleLoader</c> computes it from the exporting
    /// declaration) or a warm incremental build (<c>SymbolSerializer</c> round-trips it). For a
    /// const declared in the file under compilation the fact lives on
    /// <c>SemanticBinding.SetCompileTimeConstant</c> instead, because it is computed after the
    /// symbol is created; <c>ConstEligibility.IsCompileTimeConstant</c> is the ONE reader that
    /// consults both (#1791, #1782).
    /// </summary>
    public bool IsCompileTimeConstant { get; internal set { ThrowIfFrozen(); field = value; } }

    public Parser.Ast.ParameterModifier ParameterModifier { get; init; } = Parser.Ast.ParameterModifier.None;

    /// <summary>
    /// True when this symbol represents a module-level property declaration
    /// (a <c>PropertyDef</c> at module scope, see #844). Module-level properties
    /// are implicitly static and are emitted as static C# properties on the
    /// module class. Reads type-check like variable reads; assignment is only
    /// valid when <see cref="HasPropertySetter"/> is true.
    /// </summary>
    public bool IsModuleProperty { get; init; }

    /// <summary>
    /// True when a module-level property has a getter accessor
    /// (<c>property get name() -> T:</c> or auto-property style).
    /// Only meaningful when <see cref="IsModuleProperty"/> is true.
    /// </summary>
    /// <remarks>
    /// Uses 'internal set' because separate <c>property get</c>/<c>property set</c>
    /// declarations for the same name are merged onto one symbol during name resolution.
    /// </remarks>
    public bool HasPropertyGetter { get; internal set { ThrowIfFrozen(); field = value; } }

    /// <summary>
    /// True when a module-level property has a setter accessor
    /// (<c>property set name(value: T):</c>).
    /// Only meaningful when <see cref="IsModuleProperty"/> is true.
    /// </summary>
    /// <remarks>
    /// Uses 'internal set' because separate <c>property get</c>/<c>property set</c>
    /// declarations for the same name are merged onto one symbol during name resolution.
    /// </remarks>
    public bool HasPropertySetter { get; internal set { ThrowIfFrozen(); field = value; } }

    public virtual bool Equals(VariableSymbol? other) => ReferenceEquals(this, other);
    public override int GetHashCode() => RuntimeHelpers.GetHashCode(this);
}

/// <summary>
/// Function or method symbol
/// </summary>
public record FunctionSymbol : Symbol
{
    public SymbolMemberList<ParameterSymbol> Parameters { get; init; } = new();

    /// <summary>
    /// Replaces one parameter — the only write a checker makes to <see cref="Parameters"/> once the
    /// symbol is declared, routed here so a frozen (master) symbol refuses it (#2250).
    /// </summary>
    internal void ReplaceParameter(int index, ParameterSymbol parameter)
    {
        ThrowIfFrozen(nameof(Parameters));
        Parameters[index] = parameter;
    }

    private protected override void FreezeMembers()
    {
        Parameters.Freeze(Name);
        TypeParameters.Freeze(Name);
    }

    public SemanticType ReturnType { get; internal set { ThrowIfFrozen(); field = value; } } = SemanticType.Unknown;
    public bool IsStatic { get; init; }
    public bool IsAbstract { get; init; }
    public bool IsVirtual { get; init; }
    public bool IsOverride { get; init; }
    /// <summary>
    /// Set by TypeChecker.Definitions.cs in sync with SemanticInfo.MarkAsGenerator().
    /// CodeGen reads the routing fact from the lowering IR (IrCompilation.IsGenerator, E2 #1056);
    /// the pre-lowering validators read SemanticInfo.IsGenerator(FunctionDef); SynthesisAnalyzer
    /// uses this property when it has only the FunctionSymbol.
    /// </summary>
    public bool IsGenerator { get; internal set { ThrowIfFrozen(); field = value; } }

    /// <summary>
    /// Set by NameResolver when the function carries the '@must_use' decorator (#1022). The
    /// MustUseValidator warns (SPY0480) when a call to this function is discarded as a bare
    /// expression statement, even when the return type is not itself a Result/Optional carrier.
    /// </summary>
    public bool IsMustUse { get; internal set { ThrowIfFrozen(); field = value; } }

    /// <summary>
    /// Set by TypeChecker.Definitions.cs when the function is declared with 'async def'.
    /// CodeGen uses this to wrap return types in Task&lt;T&gt; and enable await expressions.
    /// Follows the same pattern as IsGenerator.
    /// </summary>
    public bool IsAsync { get; internal set { ThrowIfFrozen(); field = value; } }

    // Generic type parameters (for generic functions like def identity[T](value: T) -> T)
    public SymbolMemberList<TypeParameterDef> TypeParameters { get; init; } = new();
    public bool IsGeneric => TypeParameters.Count > 0;

    // For .NET interop
    public System.Reflection.MethodInfo? ClrMethod { get; init; }

    /// <summary>
    /// The original CLR method name (e.g., "IsOSPlatform") for discovery-loaded methods.
    /// Used by code generation to emit the exact CLR name rather than round-tripping
    /// through name mangling, which would corrupt acronym casing.
    /// </summary>
    public string? ClrMethodName { get; init; }

    /// <summary>
    /// Signature key for overload deduplication (set during name resolution).
    /// Format: "paramType1,paramType2,..." based on AST type annotation names.
    /// </summary>
    public string? SignatureKey { get; init; }

    /// <summary>
    /// Set by TypeChecker.Definitions.cs when the function is decorated with
    /// <c>@functools.lru_cache</c> or <c>@functools.cache</c>. CodeGen reads this
    /// to emit a memoization wrapper backed by <see cref="global::Sharpy.LruCache{TKey,TResult}"/>.
    /// </summary>
    public bool IsCached { get; internal set { ThrowIfFrozen(); field = value; } }

    /// <summary>
    /// Maxsize for the <c>@lru_cache</c> wrapper, or <c>null</c> for an unbounded cache
    /// (matching <c>@cache</c> and <c>@lru_cache(maxsize=None)</c>). Only meaningful
    /// when <see cref="IsCached"/> is <c>true</c>.
    /// </summary>
    public int? CacheMaxSize { get; internal set { ThrowIfFrozen(); field = value; } }

    /// <summary>
    /// True when this method's receiver is a PEP 3101 format template whose replacement fields are
    /// filled from the positional arguments — Core's <c>FormatTemplateAttribute</c> on the
    /// <c>this string</c> parameter of <c>StringExtensions.Format</c> (<c>str.format</c>), read at
    /// registration by <c>BuiltinRegistry.DiscoverStringExtensionMethods</c>. Read by
    /// <c>TypeChecker.CheckStaticFormatTemplateArguments</c> (#1956).
    /// </summary>
    public bool IsFormatTemplateReceiver { get; init; }

    /// <summary>
    /// The sentence appended to SPY0234 when a call to this method passes a keyword it cannot
    /// accept — Core's <c>SharpyKeywordSteerAttribute</c> (R-BE, #1955); null for every method that
    /// declares none.
    /// </summary>
    public string? KeywordSteer { get; init; }

    public virtual bool Equals(FunctionSymbol? other) => ReferenceEquals(this, other);
    public override int GetHashCode() => RuntimeHelpers.GetHashCode(this);
}

/// <summary>
/// Type symbol (class, struct, interface, enum)
/// </summary>
public record TypeSymbol : Symbol
{
    public TypeKind TypeKind { get; init; }
    public Type? ClrType { get; init; }
    public bool IsAbstract { get; init; }

    /// <summary>
    /// For CLR name groups — types differing only in generic arity under one stripped name
    /// (<c>EventHandler</c>/<c>EventHandler&lt;T&gt;</c>, <c>Action</c>/<c>Action&lt;T1..&gt;</c>,
    /// <c>Func&lt;..&gt;</c>) — maps each available arity to its group member (#1613). Populated
    /// only on the representative the namespace-import channel exports under the stripped name;
    /// null for ordinary types. Annotation-site resolution selects the member matching the
    /// written arity, mirroring C# type-group semantics. Carried through re-export
    /// <c>with</c>-copies like every other init-only fact.
    /// </summary>
    public IReadOnlyDictionary<int, TypeSymbol>? ClrArityGroup { get; init; }

    /// <summary>
    /// Whether this type extends SourceGenerator (detected during inheritance resolution).
    /// </summary>
    public bool IsSourceGenerator { get; internal set { ThrowIfFrozen(); field = value; } }

    /// <summary>
    /// Whether this type carries the '@must_use' decorator (#1022). The MustUseValidator warns
    /// (SPY0480) when an expression of this type is discarded as a bare expression statement.
    /// </summary>
    public bool IsMustUse { get; internal set { ThrowIfFrozen(); field = value; } }

    /// <summary>
    /// Whether this enum's members carry string values (#1284). A string-backed enum is emitted as
    /// a sealed class of singleton instances with an implicit conversion to <c>string</c>, not as a
    /// C# enum, so the checker's own rules — <c>.value</c> typing, str assignability, iteration —
    /// have to branch on it. Known from Pass 1, unlike <c>CodeGenInfo.IsStringEnum</c> which is
    /// materialized only after type checking. Always false for non-enums.
    /// </summary>
    public bool IsStringEnum { get; init; }

    /// <summary>
    /// Whether this type is a @dataclass.
    /// </summary>
    public bool IsDataclass { get; internal set { ThrowIfFrozen(); field = value; } }

    /// <summary>
    /// Dataclass configuration options (only set when IsDataclass is true).
    /// </summary>
    public DataclassOptions? DataclassInfo { get; internal set { ThrowIfFrozen(); field = value; } }

    /// <summary>
    /// Ordered list of dataclass fields (collected during semantic analysis).
    /// Includes inherited fields from parent @dataclass types (parent fields first).
    /// Only populated when IsDataclass is true.
    /// </summary>
    public IReadOnlyList<VariableSymbol>? DataclassFields { get; internal set { ThrowIfFrozen(); field = value; } }

    /// <summary>
    /// The module path that defines this type (e.g., "animal" for a type imported from animal.spy).
    /// Null for types defined in the current module.
    /// </summary>
    public string? DefiningModule { get; init; }

    /// <summary>
    /// The file path where this type is defined (e.g., "/path/to/animal.spy").
    /// Used for cross-file type references within the same project.
    /// </summary>
    public string? DefiningFilePath { get; init; }

    // Generic type parameters
    public SymbolMemberList<TypeParameterDef> TypeParameters { get; init; } = new();
    public bool IsGeneric => TypeParameters.Count > 0;

    // Members
    public SymbolMemberList<VariableSymbol> Fields { get; init; } = new();
    public SymbolMemberList<FunctionSymbol> Methods { get; init; } = new();
    public SymbolMemberList<PropertySymbol> Properties { get; init; } = new();
    public SymbolMemberList<EventSymbol> Events { get; init; } = new();

    // Operator methods (dunder methods for operators)
    // Maps operator dunder names (e.g., "__add__", "__eq__") to lists of overloads
    public SymbolOverloadTable OperatorMethods { get; init; } = new();

    // Protocol methods (non-operator dunders like __len__, __str__, __iter__)
    // Maps protocol dunder names to lists of overloads (usually just one, but allows flexibility)
    public SymbolOverloadTable ProtocolMethods { get; init; } = new();

    // Regular method overloads — maps method name to list of overloads
    // Only populated when a class has multiple methods with the same name
    public SymbolOverloadTable MethodOverloads { get; init; } = new();

    // Constructors - tracks all __init__ overloads
    // Unlike Python (which allows only one __init__ that gets replaced), Sharpy supports
    // multiple __init__ methods that map to C# constructor overloads
    public SymbolMemberList<FunctionSymbol> Constructors { get; init; } = new();

    /// <summary>
    /// The property and event declarations of this type's body that the member seam REFUSED — a
    /// second meaning of a name (SPY0204), a duplicate accessor half, an interface abstract half
    /// beside a bodied one (SPY0617) — by reference identity (AST nodes are records). Later
    /// consumers that group a body's declarations by name read only the admitted ones, so a refused
    /// half draws its one refusal and no cascade (P21h D7, R-JO). Written only by the resolver.
    /// </summary>
    public IReadOnlySet<Statement> RefusedDeclarations => _refusedDeclarations;
    private readonly HashSet<Statement> _refusedDeclarations = new(ReferenceEqualityComparer.Instance);

    internal void RefuseDeclaration(Statement declaration)
    {
        ThrowIfFrozen();
        _refusedDeclarations.Add(declaration);
    }

    // Union cases (only populated for TypeKind.Union)
    public SymbolMemberList<TypeSymbol> UnionCases { get; init; } = new();

    // Nested types
    public SymbolMemberList<TypeSymbol> NestedTypes { get; init; } = new();

    /// <summary>
    /// Nested type aliases (<c>type Id = int</c> declared inside a class/struct/interface body).
    /// A <see cref="TypeAliasSymbol"/> is not a <see cref="TypeSymbol"/>, so it cannot live in
    /// <see cref="NestedTypes"/>; this is the list <c>TypeResolver.LookupNestedTypeAlias</c> walks to
    /// answer a qualified <c>Outer.Id</c> (#1729, R-I). The bare-inside form resolves through the
    /// host scope's symbol table instead — this list carries the qualified spelling only.
    /// </summary>
    public SymbolMemberList<TypeAliasSymbol> NestedTypeAliases { get; init; } = new();

    /// <summary>
    /// The enclosing type for nested types. Null for top-level types.
    /// </summary>
    public TypeSymbol? DeclaringType { get; internal set { ThrowIfFrozen(); field = value; } }

    // Inheritance
    /// <summary>
    /// The base type (parent class) of this type symbol.
    /// </summary>
    /// <remarks>
    /// Uses 'internal set' because inheritance resolution happens after symbol creation.
    /// Readers should prefer SemanticBinding.GetBaseType when available.
    /// </remarks>
    public TypeSymbol? BaseType { get; internal set { ThrowIfFrozen(); field = value; } }
    public BaseTypeReference? BaseTypeRef { get; internal set { ThrowIfFrozen(); field = value; } }
    public SymbolMemberList<InterfaceReference> Interfaces { get; init; } = new();

    /// <summary>
    /// Unresolved base class name from AST, used for deferred inheritance resolution
    /// when types are imported from other modules. The actual BaseType is resolved
    /// after all imports are registered in the symbol table.
    /// </summary>
    public string? UnresolvedBaseName { get; init; }

    /// <summary>
    /// Unresolved base class type arguments from AST, carried alongside
    /// <see cref="UnresolvedBaseName"/> for deferred inheritance resolution (#1287).
    /// </summary>
    public ImmutableArray<TypeAnnotation> UnresolvedBaseTypeArgs { get; init; }
        = ImmutableArray<TypeAnnotation>.Empty;

    /// <summary>
    /// Unresolved interface references from AST, used for deferred inheritance resolution.
    /// </summary>
    /// <remarks>
    /// The written <see cref="TypeAnnotation"/>, not just its name (#1403). An interface base is a
    /// reference like any other — <c>class Repo(Comparable[int])</c> means <c>Comparable</c> AT
    /// <c>int</c> — and storing only "Comparable" made every deferred resolution produce an
    /// argument-less <see cref="InterfaceReference"/>, so member lookup through the interface saw
    /// open type parameters where the source wrote concrete types. Mirrors
    /// <see cref="UnresolvedBaseTypeArgs"/>, which carries the same information for the base class.
    /// Names are a projection (<c>.Select(a =&gt; a.Name)</c>) and are deliberately NOT stored
    /// alongside: a name list and an annotation list are two records of one fact, and the second
    /// one silently goes stale.
    /// </remarks>
    public SymbolMemberList<TypeAnnotation> UnresolvedInterfaces { get; init; } = new();

    /// <summary>
    /// Dunder-synthesized interface rows restored from the symbol cache and not yet resolved to
    /// a definition (#1746). The cold hoist resolves each row's definition to a CLR-backed
    /// symbol, which has no registry id, so the cache carries the row by CLR interface name;
    /// <see cref="InheritanceResolver"/> re-resolves it through the same rule on a warm build,
    /// alongside <see cref="UnresolvedInterfaces"/>. Empty on the cold path.
    /// </summary>
    public SymbolMemberList<UnresolvedSynthesizedInterface> UnresolvedSynthesizedInterfaces { get; init; } = new();

    /// <summary>
    /// Builds a MethodOverloads dictionary from a list of methods, excluding dunder methods.
    /// Used by ModuleLoader and BuiltinRegistry to batch-populate overloads.
    /// </summary>
    public static Dictionary<string, List<FunctionSymbol>> BuildMethodOverloads(IReadOnlyList<FunctionSymbol> methods)
    {
        var overloads = new Dictionary<string, List<FunctionSymbol>>();
        foreach (var method in methods)
        {
            if (DunderDetector.IsDunderMethod(method.Name))
                continue;

            if (!overloads.TryGetValue(method.Name, out var list))
            {
                list = new List<FunctionSymbol>();
                overloads[method.Name] = list;
            }
            list.Add(method);
        }
        return overloads;
    }

    /// <summary>
    /// Files <paramref name="method"/> under the dunder table its name selects —
    /// <see cref="OperatorMethods"/> for an operator dunder, <see cref="ProtocolMethods"/> for a
    /// protocol dunder — and answers which, or null for neither. The one rule both the declaring
    /// build (<c>NameResolver</c>) and a cache restore (<see cref="DeriveMethodTables"/>) apply.
    /// </summary>
    internal string? RegisterDunderMethod(FunctionSymbol method)
    {
        var table = Registry.OperatorRegistry.IsOperatorDunder(method.Name) ? OperatorMethods
            : Registry.ProtocolRegistry.IsProtocolDunder(method.Name) ? ProtocolMethods
            : null;
        if (table == null)
            return null;

        if (!table.TryGetValue(method.Name, out var overloads))
        {
            overloads = new List<FunctionSymbol>();
            table[method.Name] = overloads;
        }
        overloads.Add(method);
        return ReferenceEquals(table, OperatorMethods) ? "operator" : "protocol";
    }

    /// <summary>
    /// Rebuilds the tables derived from <see cref="Methods"/> — <see cref="MethodOverloads"/> and the
    /// two dunder tables — for a type restored from the incremental cache, which carries only the
    /// method list (#2027). Derived rather than put on the wire, so a warm build indexes its methods
    /// by the same rule a cold build does: without the dunder tables a cached class's
    /// <c>__add__</c> did not exist warm (SPY0222 on <c>P(1) + P(2)</c>, cold builds).
    /// </summary>
    internal void DeriveMethodTables()
    {
        foreach (var (name, overloads) in BuildMethodOverloads(Methods))
            MethodOverloads.TryAdd(name, overloads);
        foreach (var method in Methods)
            RegisterDunderMethod(method);
    }

    private protected override void FreezeMembers()
    {
        TypeParameters.Freeze(Name);
        Fields.Freeze(Name);
        Methods.Freeze(Name);
        Properties.Freeze(Name);
        Events.Freeze(Name);
        OperatorMethods.Freeze(Name);
        ProtocolMethods.Freeze(Name);
        MethodOverloads.Freeze(Name);
        Constructors.Freeze(Name);
        UnionCases.Freeze(Name);
        NestedTypes.Freeze(Name);
        NestedTypeAliases.Freeze(Name);
        Interfaces.Freeze(Name);
        UnresolvedInterfaces.Freeze(Name);
        UnresolvedSynthesizedInterfaces.Freeze(Name);
    }

    public virtual bool Equals(TypeSymbol? other) => ReferenceEquals(this, other);
    public override int GetHashCode() => RuntimeHelpers.GetHashCode(this);
}

/// <summary>
/// Property symbol for class/struct/interface properties
/// </summary>
public record PropertySymbol
{
    public string Name { get; init; } = string.Empty;
    /// <summary>
    /// True when the declared name was written backtick-escaped (<c>`Name`</c>), so the emitter must
    /// preserve its verbatim spelling rather than PascalCase-mangling it (#1455). Standalone records
    /// are the escape flag's blind spot (#1385) — carried here so every symbol-derived emission
    /// honors it.
    /// </summary>
    public bool IsNameBacktickEscaped { get; init; }
    public SemanticType Type { get; init; } = SemanticType.Unknown;
    /// <summary>
    /// Documentation for this property (from XML doc or source).
    /// </summary>
    public string? Documentation { get; init; }
    public bool HasGetter { get; init; }
    public bool HasSetter { get; init; }
    public bool HasInit { get; init; }
    public bool IsStatic { get; init; }
    public bool IsVirtual { get; init; }
    public bool IsAbstract { get; init; }
    public bool IsOverride { get; init; }
    public bool IsFinal { get; init; }
    public AccessLevel GetterAccess { get; init; } = AccessLevel.Public;
    public AccessLevel SetterAccess { get; init; } = AccessLevel.Public;
    public string? ExplicitInterface { get; init; }

    /// <summary>
    /// True for a defaulted instance auto-property of a struct with no explicit <c>__init__</c>
    /// whose default is a constant parameter default: the property is a member of the synthesized
    /// struct constructor's roster, exactly like a defaulted field (#1938) — an optional parameter
    /// carrying the default, assigned in both synthesized constructors. Set at
    /// <c>CodeGenInfoComputer.ProcessTypeMembers</c> (the materialization pass, so it is a fact of the
    /// compilation that EMITS the struct; the import extractor leaves it false). A defaulted property
    /// whose default is not an admitted parameter default keeps its initializer and is not a
    /// parameter — graceful, never a new refusal.
    /// </summary>
    public bool IsConstructorParameter { get; init; }

    /// <summary>
    /// Store-time observer clauses (<c>before_set</c>/<c>after_set</c>) declared on this
    /// auto-property. Empty for properties without observers. Populated during name resolution
    /// from <see cref="PropertyDef.Observers"/>; consumed by hover/completion and the codegen
    /// expanded setter. Gated behind the experimental <c>property_observers</c> feature (#416).
    /// </summary>
    public ImmutableArray<PropertyObserver> Observers { get; init; } = ImmutableArray<PropertyObserver>.Empty;
}

/// <summary>
/// Event symbol for class/struct/interface events.
/// Modeled on PropertySymbol with add/remove accessors instead of get/set.
/// </summary>
public record EventSymbol
{
    public string Name { get; init; } = string.Empty;
    /// <summary>
    /// True when the declared name was written backtick-escaped (<c>`Name`</c>), so the emitter must
    /// preserve its verbatim spelling rather than PascalCase-mangling it (#1455). Standalone records
    /// are the escape flag's blind spot (#1385) — carried here so every symbol-derived emission
    /// honors it.
    /// </summary>
    public bool IsNameBacktickEscaped { get; init; }
    public SemanticType Type { get; init; } = SemanticType.Unknown;
    /// <summary>
    /// Documentation for this event (from XML doc or source).
    /// </summary>
    public string? Documentation { get; init; }
    public bool HasAdd { get; init; }
    public bool HasRemove { get; init; }
    public bool IsStatic { get; init; }
    public bool IsVirtual { get; init; }
    public bool IsAbstract { get; init; }
    public bool IsOverride { get; init; }
    public bool IsFinal { get; init; }
    public AccessLevel AccessLevel { get; init; } = AccessLevel.Public;
    public AccessLevel AddAccessLevel { get; init; } = AccessLevel.Public;
    public AccessLevel RemoveAccessLevel { get; init; } = AccessLevel.Public;
}

/// <summary>
/// Parameter symbol for functions/methods
/// </summary>
public record ParameterSymbol
{
    public string Name { get; init; } = string.Empty;
    /// <summary>
    /// True when the declared name was written backtick-escaped (<c>`Name`</c>), so the emitter must
    /// preserve its verbatim spelling rather than PascalCase-mangling it — including across
    /// forwarders (#1455). Standalone records are the escape flag's blind spot (#1385).
    /// </summary>
    public bool IsNameBacktickEscaped { get; init; }
    public SemanticType Type { get; init; } = SemanticType.Unknown;
    /// <summary>
    /// Documentation for this parameter (from XML doc &lt;param&gt; tags or source).
    /// </summary>
    public string? Documentation { get; init; }
    public bool HasDefault { get; init; }
    public Expression? DefaultValue { get; init; }
    /// <summary>
    /// If true, this parameter accepts a variable number of arguments (params array).
    /// The Type property contains the element type of the params array.
    /// </summary>
    public bool IsVariadic { get; init; }
    public bool IsPositionalOnly { get; init; }
    public bool IsKeywordOnly { get; init; }
    public bool IsLateBound { get; init; }
    public Parser.Ast.ParameterModifier Modifier { get; init; } = Parser.Ast.ParameterModifier.None;
    /// <summary>
    /// The original CLR type name (assembly-qualified for non-generic, generic definition AQN
    /// for generic types). Preserves the CLR identity lost by <see cref="Discovery.ClrTypeBridge"/>
    /// (e.g., IEnumerable&lt;T&gt; mapped to list[T]). Used for overload specificity comparison.
    /// </summary>
    public string? ClrTypeName { get; init; }
    /// <summary>
    /// For a discovered CLR format-spec parameter, the name of the parameter whose value the spec
    /// formats (from <c>Sharpy.FormatSpecAttribute</c>, via
    /// <see cref="Discovery.Caching.ParameterSignature.FormatSpecOf"/>); null otherwise. Read by
    /// <c>TypeChecker.CheckStaticFormatSpecArguments</c> (#1956).
    /// </summary>
    public string? FormatSpecOf { get; init; }
}

/// <summary>
/// Module symbol - represents an imported module namespace
/// </summary>
public record ModuleSymbol : Symbol
{
    public string FilePath { get; init; } = string.Empty;

    /// <summary>
    /// The module's exports: the value-position lookup and the types-only lookup as one unit.
    /// Reads go through this object (it is itself the value-position lookup); writes go through
    /// <see cref="ModuleExports.Add"/> or <see cref="ModuleExports.MergeFrom"/>, which maintain
    /// the types view automatically (#1145).
    /// </summary>
    public ModuleExports Exports { get; init; } = new();

    /// <summary>
    /// Exported types keyed by name — a read-only view of <see cref="Exports"/>, kept separate
    /// from the value-position lookup so an export that shares a name with a type does not shadow
    /// the type in annotation position (e.g. <c>sqlite3.Row</c> — both the <c>Row</c> type and the
    /// <c>row_factory</c> value). <c>ResolveQualifiedType</c> consults this first; value lookups
    /// keep using <see cref="Exports"/> (#1092).
    /// </summary>
    public IReadOnlyDictionary<string, TypeSymbol> ExportedTypes => Exports.Types;
    public Dictionary<string, List<FunctionSymbol>> FunctionOverloads { get; init; } = new();
    public bool IsNetModule { get; init; } = false;

    /// <summary>
    /// The canonical (fully-qualified) module name (e.g., "package_a.helper").
    /// Derived from ModuleInfo.CanonicalModuleName during import resolution.
    /// </summary>
    public string? CanonicalModuleName { get; init; }

    /// <summary>
    /// The .NET namespace name for CLR namespace modules (e.g., "System" for "system").
    /// Null for Sharpy stdlib modules and non-.NET modules.
    /// </summary>
    public string? NetNamespaceName { get; init; }

    /// <summary>
    /// The C# namespace of the [SharpyModule] class (e.g., "Sharpy" for stdlib, "MyLib" for third-party).
    /// Used by the emitter to generate fully-qualified using directives for cross-namespace imports.
    /// Null for .spy source modules and CLR namespace modules.
    /// </summary>
    public string? CSharpNamespace { get; init; }

    /// <summary>
    /// The simple C# class name for this module (e.g., "NumpyRandom" for "numpy.random").
    /// Set during lazy sub-module resolution in the type checker.
    /// </summary>
    public string? CSharpClassName { get; init; }

    public virtual bool Equals(ModuleSymbol? other) => ReferenceEquals(this, other);
    public override int GetHashCode() => RuntimeHelpers.GetHashCode(this);
}

/// <summary>
/// Type alias symbol (compile-time only, no C# output)
/// </summary>
public record TypeAliasSymbol : Symbol
{
    public TypeAnnotation? TypeAnnotation { get; init; }
    public Parser.Ast.FunctionType? FunctionType { get; init; }
    public IReadOnlyList<TypeParameterDef> TypeParameters { get; init; } = Array.Empty<TypeParameterDef>();

    /// <summary>
    /// Creates a TypeAliasSymbol from a TypeAlias AST node.
    /// Used by both NameResolver (module/class scope) and TypeChecker (function scope)
    /// to avoid duplicating construction logic.
    /// </summary>
    /// <param name="typeAlias">The declaration.</param>
    /// <param name="declaringFilePath">The file that declares the alias — required, so every
    /// construction site states it. Without it (and <see cref="Symbol.DeclarationSpan"/>) an alias was
    /// the one declaration kind go-to-definition could not land on, in the same file or another
    /// (#2242).</param>
    public static TypeAliasSymbol CreateFrom(TypeAlias typeAlias, string? declaringFilePath) => new()
    {
        DeclarationSpan = typeAlias.Span,
        DeclaringFilePath = declaringFilePath,
        Name = typeAlias.Name,
        Kind = SymbolKind.TypeAlias,
        IsNameBacktickEscaped = typeAlias.IsNameBacktickEscaped,
        AccessLevel = AccessLevel.Public,
        TypeAnnotation = typeAlias.Type,
        FunctionType = typeAlias.FunctionType,
        TypeParameters = typeAlias.TypeParameters.IsEmpty
            ? Array.Empty<TypeParameterDef>()
            : typeAlias.TypeParameters.ToArray(),
        DeclarationLine = typeAlias.LineStart,
        DeclarationColumn = typeAlias.ColumnStart,
        NameDeclarationLine = typeAlias.NameLineStart,
        NameDeclarationColumn = typeAlias.NameColumnStart,
        NameDeclarationColumnEnd = typeAlias.NameColumnEnd
    };

    public virtual bool Equals(TypeAliasSymbol? other) => ReferenceEquals(this, other);
    public override int GetHashCode() => RuntimeHelpers.GetHashCode(this);
}

/// <summary>
/// Type parameter symbol (e.g., T in class Box[T])
/// </summary>
public record TypeParameterSymbol : Symbol
{
    /// <summary>
    /// The type symbol that declares this type parameter
    /// </summary>
    public TypeSymbol? DeclaringType { get; init; }

    /// <summary>
    /// Constraint clauses for this type parameter (e.g., T: IComparable)
    /// </summary>
    public ImmutableArray<ConstraintClause> Constraints { get; init; }
        = ImmutableArray<ConstraintClause>.Empty;

    /// <summary>
    /// Variance annotation (None, Covariant, Contravariant) for delegate/interface type parameters.
    /// </summary>
    public TypeParameterVariance Variance { get; init; } = TypeParameterVariance.None;

    /// <summary>
    /// Default type for this type parameter (PEP 696).
    /// When provided, callers can omit this type argument and the default is used.
    /// </summary>
    public SemanticType? DefaultType { get; init; }

    public virtual bool Equals(TypeParameterSymbol? other) => ReferenceEquals(this, other);
    public override int GetHashCode() => RuntimeHelpers.GetHashCode(this);
}

public enum SymbolKind
{
    Variable,
    Parameter,
    Function,
    Type,
    Module,
    Property,
    Event,
    TypeAlias,
    TypeParameter
}

public enum TypeKind
{
    Class,
    Struct,
    Interface,
    Enum,
    Union,
    Delegate
}

public enum AccessLevel
{
    Public,
    Internal,
    Protected,
    Private
}

/// <summary>
/// Configuration options for @dataclass decorator.
/// </summary>
/// <param name="Frozen">If true, fields are init-only (immutable after construction).</param>
/// <param name="Eq">If true, synthesize __eq__ and __hash__ methods.</param>
/// <param name="Repr">If true, synthesize __repr__ method.</param>
public record DataclassOptions(bool Frozen = false, bool Eq = true, bool Repr = true);
