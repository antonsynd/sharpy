using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Represents a symbol in the symbol table
/// </summary>
public abstract record Symbol
{
    public string Name { get; init; } = string.Empty;
    public SymbolKind Kind { get; init; }
    public AccessLevel AccessLevel { get; init; } = AccessLevel.Public;
    public int? DeclarationLine { get; init; }
    public int? DeclarationColumn { get; init; }

    /// <summary>
    /// Indicates if this symbol is re-exported from another module (e.g., via "from .submodule import func")
    /// </summary>
    public bool IsReExport { get; init; }

    /// <summary>
    /// For re-exported symbols, the original module name where the symbol was defined
    /// </summary>
    public string? OriginalModule { get; init; }
}

/// <summary>
/// Variable or field symbol
/// </summary>
public record VariableSymbol : Symbol
{
    public SemanticType Type { get; init; } = SemanticType.Unknown;
    public bool IsParameter { get; init; }
    public bool IsConstant { get; init; }
    public bool HasDefaultValue { get; init; }
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

    // For .NET interop
    public System.Reflection.MethodInfo? ClrMethod { get; init; }
}

/// <summary>
/// Type symbol (class, struct, interface, enum)
/// </summary>
public record TypeSymbol : Symbol
{
    public TypeKind TypeKind { get; init; }
    public Type? ClrType { get; init; }
    public bool IsAbstract { get; init; }

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

    // Constructors - tracks all __init__ overloads
    // Unlike Python (which allows only one __init__ that gets replaced), Sharpy supports
    // multiple __init__ methods that map to C# constructor overloads
    public List<FunctionSymbol> Constructors { get; init; } = new();

    // Inheritance
    public TypeSymbol? BaseType { get; set; }
    public List<TypeSymbol> Interfaces { get; init; } = new();
}

/// <summary>
/// Property symbol (for future use)
/// </summary>
public record PropertySymbol
{
    public string Name { get; init; } = string.Empty;
    public SemanticType Type { get; init; } = SemanticType.Unknown;
    public bool HasGetter { get; init; }
    public bool HasSetter { get; init; }
    public AccessLevel GetterAccess { get; init; } = AccessLevel.Public;
    public AccessLevel SetterAccess { get; init; } = AccessLevel.Public;
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
}

/// <summary>
/// Module symbol - represents an imported module namespace
/// </summary>
public record ModuleSymbol : Symbol
{
    public string FilePath { get; init; } = string.Empty;
    public Dictionary<string, Symbol> Exports { get; init; } = new();
}

/// <summary>
/// Type alias symbol (compile-time only, no C# output)
/// </summary>
public record TypeAliasSymbol : Symbol
{
    public TypeAnnotation? TypeAnnotation { get; init; }
    public Parser.Ast.FunctionType? FunctionType { get; init; }
}

public enum SymbolKind
{
    Variable,
    Parameter,
    Function,
    Type,
    Module,
    Property,
    TypeAlias
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
    Protected,
    Private
}
