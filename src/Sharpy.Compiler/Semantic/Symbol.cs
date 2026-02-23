using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Sharpy.Compiler.Parser.Ast;

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
    public AccessLevel AccessLevel { get; init; } = AccessLevel.Public;
    public int? DeclarationLine { get; init; }
    public int? DeclarationColumn { get; init; }

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
    public bool HasDefaultValue { get; init; }

    public virtual bool Equals(VariableSymbol? other) => ReferenceEquals(this, other);
    public override int GetHashCode() => RuntimeHelpers.GetHashCode(this);
}

/// <summary>
/// Function or method symbol
/// </summary>
public record FunctionSymbol : Symbol
{
    public List<ParameterSymbol> Parameters { get; init; } = new();
    public SemanticType ReturnType { get; init; } = SemanticType.Unknown;
    public bool IsStatic { get; init; }
    public bool IsAbstract { get; init; }
    public bool IsVirtual { get; init; }
    public bool IsOverride { get; init; }
    public bool IsGenerator { get; internal set; }

    // Generic type parameters (for generic functions like def identity[T](value: T) -> T)
    public List<TypeParameterDef> TypeParameters { get; init; } = new();
    public bool IsGeneric => TypeParameters.Count > 0;

    // For .NET interop
    public System.Reflection.MethodInfo? ClrMethod { get; init; }

    /// <summary>
    /// Signature key for overload deduplication (set during name resolution).
    /// Format: "paramType1,paramType2,..." based on AST type annotation names.
    /// </summary>
    public string? SignatureKey { get; init; }

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
/// Parameter symbol for functions/methods
/// </summary>
public record ParameterSymbol
{
    public string Name { get; init; } = string.Empty;
    public SemanticType Type { get; init; } = SemanticType.Unknown;
    public bool HasDefault { get; init; }
    public Expression? DefaultValue { get; init; }
    /// <summary>
    /// If true, this parameter accepts a variable number of arguments (params array).
    /// The Type property contains the element type of the params array.
    /// </summary>
    public bool IsVariadic { get; init; }
}

/// <summary>
/// Module symbol - represents an imported module namespace
/// </summary>
public record ModuleSymbol : Symbol
{
    public string FilePath { get; init; } = string.Empty;
    public Dictionary<string, Symbol> Exports { get; init; } = new();

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
    TypeAlias,
    TypeParameter
}

public enum TypeKind
{
    Class,
    Struct,
    Interface,
    Enum
}

public enum AccessLevel
{
    Public,
    Internal,
    Protected,
    Private
}
