namespace Sharpy.Compiler.Parser.Ast;

/// <summary>
/// Binary operation expression
/// </summary>
public record BinaryOp : Expression
{
    public Expression Left { get; init; } = null!;
    public BinaryOperator Operator { get; init; }
    public Expression Right { get; init; } = null!;
}

/// <summary>
/// Unary operation expression
/// </summary>
public record UnaryOp : Expression
{
    public UnaryOperator Operator { get; init; }
    public Expression Operand { get; init; } = null!;
}

/// <summary>
/// Comparison expression
/// </summary>
public record Compare : Expression
{
    public Expression Left { get; init; } = null!;
    public List<CompareOperator> Operators { get; init; } = new();
    public List<Expression> Comparators { get; init; } = new();
}

/// <summary>
/// Function call expression
/// </summary>
public record Call : Expression
{
    public Expression Function { get; init; } = null!;
    public List<Expression> Arguments { get; init; } = new();
    public List<Keyword> Keywords { get; init; } = new();
}

/// <summary>
/// Attribute access expression (e.g., obj.attr)
/// </summary>
public record Attribute : Expression
{
    public Expression Value { get; init; } = null!;
    public string AttributeName { get; init; } = string.Empty;
}

/// <summary>
/// Subscript expression (e.g., obj[key])
/// </summary>
public record Subscript : Expression
{
    public Expression Value { get; init; } = null!;
    public Expression Index { get; init; } = null!;
}

/// <summary>
/// Name/identifier expression
/// </summary>
public record Name : Expression
{
    public string Id { get; init; } = string.Empty;
}

/// <summary>
/// Constant literal expression
/// </summary>
public record Constant : Expression
{
    public object? Value { get; init; }

    public ConstantKind Kind => Value switch
    {
        null => ConstantKind.None,
        int => ConstantKind.Integer,
        long => ConstantKind.Integer,
        double => ConstantKind.Float,
        string => ConstantKind.String,
        bool => ConstantKind.Boolean,
        _ => ConstantKind.Other
    };
}

/// <summary>
/// List literal expression
/// </summary>
public record ListExpr : Expression
{
    public List<Expression> Elements { get; init; } = new();
}

/// <summary>
/// Dictionary literal expression
/// </summary>
public record DictExpr : Expression
{
    public List<(Expression Key, Expression Value)> Items { get; init; } = new();
}

/// <summary>
/// Keyword argument in function call
/// </summary>
public record Keyword
{
    public string Name { get; init; } = string.Empty;
    public Expression Value { get; init; } = null!;
}

public enum ConstantKind
{
    None,
    Integer,
    Float,
    String,
    Boolean,
    Other
}

public enum BinaryOperator
{
    Add, Sub, Mult, Div, FloorDiv, Mod, Pow,
    BitAnd, BitOr, BitXor, LeftShift, RightShift,
    And, Or
}

public enum UnaryOperator
{
    Not, Invert, Plus, Minus
}

public enum CompareOperator
{
    Eq, NotEq, Lt, LtE, Gt, GtE, Is, IsNot, In, NotIn
}
