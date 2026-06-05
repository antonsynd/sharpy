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
/// properties (Type, BaseType, CodeGenInfo) that are set after creation during semantic analysis.
/// Using value-based equality (the record default) would make hash codes unstable, causing
/// dictionary/set lookups to fail silently after mutation. Reference equality makes symbols
/// safe to use as dictionary keys in any collection without requiring ReferenceEqualityComparer.
/// </remarks>
public abstract record Symbol
{
    public string Name { get; init; } = string.Empty;
    public SymbolKind Kind { get; init; }
    public bool IsNameBacktickEscaped { get; init; }
    public AccessLevel AccessLevel { get; internal set; } = AccessLevel.Public;
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

    public int? EffectiveNameLine => NameDeclarationLine ?? DeclarationLine;
    public int? EffectiveNameColumn => NameDeclarationColumn ?? DeclarationColumn;

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
    public AccessLevel? ExplicitAccessLevel { get; init; }

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
    /// Uses 'internal set' because documentation is populated after symbol creation
    /// (same pattern as CodeGenInfo). For Sharpy source, populated from AST DocString
    /// during name resolution. For .NET assemblies, populated from XML doc files
    /// during assembly discovery.
    /// </remarks>
    public string? Documentation { get; internal set; }

    /// <summary>
    /// Code generation information computed during semantic analysis.
    /// Null until CodeGenInfo computation pass runs.
    /// </summary>
    /// <remarks>
    /// Uses 'internal set' to allow setting CodeGenInfo after initial symbol creation
    /// (symbols are created during NameResolver, CodeGenInfo is computed during/after TypeChecker).
    /// Readers should prefer SemanticBinding.GetCodeGenInfo when available.
    /// </remarks>
    public CodeGenInfo? CodeGenInfo { get; internal set; }

    /// <summary>
    /// Use reference equality for symbols. Record default (value equality) is unsafe
    /// because mutable properties would make hash codes unstable.
    /// </summary>
    public virtual bool Equals(Symbol? other) => ReferenceEquals(this, other);

    public override int GetHashCode() => RuntimeHelpers.GetHashCode(this);

    public override string ToString() => $"{Kind} '{Name}'";
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
    public SemanticType Type { get; internal set; } = SemanticType.Unknown;
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
    public Parser.Ast.ParameterModifier ParameterModifier { get; init; } = Parser.Ast.ParameterModifier.None;

    public virtual bool Equals(VariableSymbol? other) => ReferenceEquals(this, other);
    public override int GetHashCode() => RuntimeHelpers.GetHashCode(this);
}

/// <summary>
/// Function or method symbol
/// </summary>
public record FunctionSymbol : Symbol
{
    public List<ParameterSymbol> Parameters { get; init; } = new();
    public SemanticType ReturnType { get; internal set; } = SemanticType.Unknown;
    public bool IsStatic { get; init; }
    public bool IsAbstract { get; init; }
    public bool IsVirtual { get; init; }
    public bool IsOverride { get; init; }
    /// <summary>
    /// Set by TypeChecker.Definitions.cs in sync with SemanticInfo.MarkAsGenerator().
    /// CodeGen uses SemanticInfo.IsGenerator(FunctionDef) when it has the AST node;
    /// SynthesisAnalyzer uses this property when it has only the FunctionSymbol.
    /// </summary>
    public bool IsGenerator { get; internal set; }

    /// <summary>
    /// Set by TypeChecker.Definitions.cs when the function is declared with 'async def'.
    /// CodeGen uses this to wrap return types in Task&lt;T&gt; and enable await expressions.
    /// Follows the same pattern as IsGenerator.
    /// </summary>
    public bool IsAsync { get; internal set; }

    // Generic type parameters (for generic functions like def identity[T](value: T) -> T)
    public List<TypeParameterDef> TypeParameters { get; init; } = new();
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
    public bool IsCached { get; internal set; }

    /// <summary>
    /// Maxsize for the <c>@lru_cache</c> wrapper, or <c>null</c> for an unbounded cache
    /// (matching <c>@cache</c> and <c>@lru_cache(maxsize=None)</c>). Only meaningful
    /// when <see cref="IsCached"/> is <c>true</c>.
    /// </summary>
    public int? CacheMaxSize { get; internal set; }

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
    /// Whether this type extends SourceGenerator (detected during inheritance resolution).
    /// </summary>
    public bool IsSourceGenerator { get; internal set; }

    /// <summary>
    /// Whether this type is a @dataclass.
    /// </summary>
    public bool IsDataclass { get; internal set; }

    /// <summary>
    /// Dataclass configuration options (only set when IsDataclass is true).
    /// </summary>
    public DataclassOptions? DataclassInfo { get; internal set; }

    /// <summary>
    /// Ordered list of dataclass fields (collected during semantic analysis).
    /// Includes inherited fields from parent @dataclass types (parent fields first).
    /// Only populated when IsDataclass is true.
    /// </summary>
    public IReadOnlyList<VariableSymbol>? DataclassFields { get; internal set; }

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
    public List<TypeParameterDef> TypeParameters { get; init; } = new();
    public bool IsGeneric => TypeParameters.Count > 0;

    // Members
    public List<VariableSymbol> Fields { get; init; } = new();
    public List<FunctionSymbol> Methods { get; init; } = new();
    public List<PropertySymbol> Properties { get; init; } = new();
    public List<EventSymbol> Events { get; init; } = new();

    // Operator methods (dunder methods for operators)
    // Maps operator dunder names (e.g., "__add__", "__eq__") to lists of overloads
    public Dictionary<string, List<FunctionSymbol>> OperatorMethods { get; init; } = new();

    // Protocol methods (non-operator dunders like __len__, __str__, __iter__)
    // Maps protocol dunder names to lists of overloads (usually just one, but allows flexibility)
    public Dictionary<string, List<FunctionSymbol>> ProtocolMethods { get; init; } = new();

    // Regular method overloads — maps method name to list of overloads
    // Only populated when a class has multiple methods with the same name
    public Dictionary<string, List<FunctionSymbol>> MethodOverloads { get; init; } = new();

    // Constructors - tracks all __init__ overloads
    // Unlike Python (which allows only one __init__ that gets replaced), Sharpy supports
    // multiple __init__ methods that map to C# constructor overloads
    public List<FunctionSymbol> Constructors { get; init; } = new();

    // Union cases (only populated for TypeKind.Union)
    public List<TypeSymbol> UnionCases { get; init; } = new();

    // Nested types
    public List<TypeSymbol> NestedTypes { get; init; } = new();

    /// <summary>
    /// The enclosing type for nested types. Null for top-level types.
    /// </summary>
    public TypeSymbol? DeclaringType { get; internal set; }

    // Inheritance
    /// <summary>
    /// The base type (parent class) of this type symbol.
    /// </summary>
    /// <remarks>
    /// Uses 'internal set' because inheritance resolution happens after symbol creation.
    /// Readers should prefer SemanticBinding.GetBaseType when available.
    /// </remarks>
    public TypeSymbol? BaseType { get; internal set; }
    public List<InterfaceReference> Interfaces { get; init; } = new();

    /// <summary>
    /// Unresolved base class name from AST, used for deferred inheritance resolution
    /// when types are imported from other modules. The actual BaseType is resolved
    /// after all imports are registered in the symbol table.
    /// </summary>
    public string? UnresolvedBaseName { get; init; }

    /// <summary>
    /// Unresolved interface names from AST, used for deferred inheritance resolution.
    /// </summary>
    public List<string> UnresolvedInterfaceNames { get; init; } = new();

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

    public virtual bool Equals(TypeSymbol? other) => ReferenceEquals(this, other);
    public override int GetHashCode() => RuntimeHelpers.GetHashCode(this);
}

/// <summary>
/// Property symbol for class/struct/interface properties
/// </summary>
public record PropertySymbol
{
    public string Name { get; init; } = string.Empty;
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
}

/// <summary>
/// Event symbol for class/struct/interface events.
/// Modeled on PropertySymbol with add/remove accessors instead of get/set.
/// </summary>
public record EventSymbol
{
    public string Name { get; init; } = string.Empty;
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
    /// for generic types). Preserves the CLR identity lost by <see cref="Discovery.ClrTypeMapper"/>
    /// (e.g., IEnumerable&lt;T&gt; mapped to list[T]). Used for overload specificity comparison.
    /// </summary>
    public string? ClrTypeName { get; init; }
}

/// <summary>
/// Module symbol - represents an imported module namespace
/// </summary>
public record ModuleSymbol : Symbol
{
    public string FilePath { get; init; } = string.Empty;
    public Dictionary<string, Symbol> Exports { get; init; } = new();
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
    public static TypeAliasSymbol CreateFrom(TypeAlias typeAlias) => new()
    {
        Name = typeAlias.Name,
        Kind = SymbolKind.TypeAlias,
        AccessLevel = AccessLevel.Public,
        TypeAnnotation = typeAlias.Type,
        FunctionType = typeAlias.FunctionType,
        TypeParameters = typeAlias.TypeParameters.IsEmpty
            ? Array.Empty<TypeParameterDef>()
            : typeAlias.TypeParameters.ToArray(),
        DeclarationLine = typeAlias.LineStart,
        DeclarationColumn = typeAlias.ColumnStart,
        NameDeclarationLine = typeAlias.NameLineStart,
        NameDeclarationColumn = typeAlias.NameColumnStart
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
