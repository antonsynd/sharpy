namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Represents a symbol in the symbol table
/// </summary>
public abstract record Symbol
{
    public string Name { get; init; } = string.Empty;
    public SymbolKind Kind { get; init; }
    public AccessLevel AccessLevel { get; init; } = AccessLevel.Public;
}

/// <summary>
/// Variable symbol
/// </summary>
public record VariableSymbol : Symbol
{
    public SemanticType Type { get; init; } = SemanticType.Unknown;
    public bool IsParameter { get; init; }
}

/// <summary>
/// Function symbol
/// </summary>
public record FunctionSymbol : Symbol
{
    public List<ParameterSymbol> Parameters { get; init; } = new();
    public SemanticType ReturnType { get; init; } = SemanticType.Unknown;
    public System.Reflection.MethodInfo? ClrMethod { get; init; }
}

/// <summary>
/// Type symbol
/// </summary>
public record TypeSymbol : Symbol
{
    public Type? ClrType { get; init; }
    public List<MethodSymbol> Methods { get; init; } = new();
    public bool IsGeneric { get; init; }
    public List<string> TypeParameters { get; init; } = new();
}

/// <summary>
/// Method symbol
/// </summary>
public record MethodSymbol
{
    public string Name { get; init; } = string.Empty;
    public List<ParameterSymbol> Parameters { get; init; } = new();
    public SemanticType ReturnType { get; init; } = SemanticType.Unknown;
    public System.Reflection.MethodInfo? ClrMethod { get; init; }
    public bool IsStatic { get; init; }
}

/// <summary>
/// Parameter symbol
/// </summary>
public record ParameterSymbol
{
    public string Name { get; init; } = string.Empty;
    public SemanticType Type { get; init; } = SemanticType.Unknown;
    public bool HasDefault { get; init; }
}

public enum SymbolKind
{
    Variable,
    Parameter,
    Function,
    Type,
    Module
}

public enum AccessLevel
{
    Public,
    Private,
    Protected
}

/// <summary>
/// Represents a type in the semantic analysis
/// </summary>
public abstract record SemanticType
{
    public static readonly SemanticType Unknown = new UnknownType();
    public static readonly SemanticType Int = new BuiltinType("int");
    public static readonly SemanticType Float = new BuiltinType("float");
    public static readonly SemanticType Str = new BuiltinType("str");
    public static readonly SemanticType Bool = new BuiltinType("bool");
    public static readonly SemanticType None = new BuiltinType("None");
}

public record UnknownType : SemanticType;

public record BuiltinType : SemanticType
{
    public string Name { get; init; }

    public BuiltinType(string name)
    {
        Name = name;
    }
}

public record GenericType : SemanticType
{
    public string Name { get; init; } = string.Empty;
    public List<SemanticType> TypeArguments { get; init; } = new();
}

public record UserDefinedType : SemanticType
{
    public string Name { get; init; } = string.Empty;
    public TypeSymbol? Symbol { get; init; }
}
