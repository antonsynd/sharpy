namespace Sharpy.Compiler.Parser.Ast;

/// <summary>
/// Function definition statement
/// </summary>
public record FunctionDef : Statement
{
    public string Name { get; init; } = string.Empty;
    public List<Parameter> Parameters { get; init; } = new();
    public TypeAnnotation? ReturnType { get; init; }
    public List<Statement> Body { get; init; } = new();
    public string? AccessModifier { get; init; }
    public string? Docstring { get; init; }
}

/// <summary>
/// Class definition statement
/// </summary>
public record ClassDef : Statement
{
    public string Name { get; init; } = string.Empty;
    public List<string> TypeParameters { get; init; } = new();
    public List<Expression> BaseClasses { get; init; } = new();
    public List<Statement> Body { get; init; } = new();
    public string? AccessModifier { get; init; }
    public string? Docstring { get; init; }
}

/// <summary>
/// Return statement
/// </summary>
public record Return : Statement
{
    public Expression? Value { get; init; }
}

/// <summary>
/// Assignment statement
/// </summary>
public record Assign : Statement
{
    public Expression Target { get; init; } = null!;
    public Expression Value { get; init; } = null!;
}

/// <summary>
/// If statement
/// </summary>
public record If : Statement
{
    public Expression Test { get; init; } = null!;
    public List<Statement> Body { get; init; } = new();
    public List<Statement> OrElse { get; init; } = new();
}

/// <summary>
/// While loop statement
/// </summary>
public record While : Statement
{
    public Expression Test { get; init; } = null!;
    public List<Statement> Body { get; init; } = new();
}

/// <summary>
/// For loop statement
/// </summary>
public record For : Statement
{
    public Expression Target { get; init; } = null!;
    public Expression Iterator { get; init; } = null!;
    public List<Statement> Body { get; init; } = new();
}

/// <summary>
/// Pass statement (no-op)
/// </summary>
public record Pass : Statement;

/// <summary>
/// Expression statement (expression used as statement)
/// </summary>
public record ExpressionStatement : Statement
{
    public Expression Expression { get; init; } = null!;
}

/// <summary>
/// Function parameter
/// </summary>
public record Parameter
{
    public string Name { get; init; } = string.Empty;
    public TypeAnnotation? Type { get; init; }
    public Expression? DefaultValue { get; init; }
}
