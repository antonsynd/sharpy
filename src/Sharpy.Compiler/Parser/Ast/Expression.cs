using System.Collections.Immutable;

namespace Sharpy.Compiler.Parser.Ast;

/// <summary>
/// Base class for all expression nodes
/// </summary>
public abstract record Expression : Node;

#region Literals

/// <summary>
/// Integer literal (42, 1_000_000, 42L, etc.)
/// </summary>
public record IntegerLiteral : Expression
{
    public string Value { get; init; } = "";
    public string? Suffix { get; init; }  // L, U, UL, etc.
}

/// <summary>
/// Float literal (3.14, 3.14f, 3.14m, etc.)
/// </summary>
public record FloatLiteral : Expression
{
    public string Value { get; init; } = "";
    public string? Suffix { get; init; }  // f, F, d, D, m, M
}

/// <summary>
/// String literal ("hello", 'world', r"C:\path", """multi-line""")
/// </summary>
public record StringLiteral : Expression
{
    public string Value { get; init; } = "";
    public bool IsRaw { get; init; }
}

/// <summary>
/// F-string literal (f"Hello {name}")
/// </summary>
public record FStringLiteral : Expression
{
    public ImmutableArray<FStringPart> Parts { get; init; } = ImmutableArray<FStringPart>.Empty;
}

public record FStringPart
{
    public string? Text { get; init; }
    public Expression? Expression { get; init; }
    public string? FormatSpec { get; init; }  // Format specification (e.g., ".2f", ">10")
}

/// <summary>
/// Boolean literal (True or False)
/// </summary>
public record BooleanLiteral : Expression
{
    public bool Value { get; init; }
}

/// <summary>
/// None literal
/// </summary>
public record NoneLiteral : Expression;

/// <summary>
/// Ellipsis literal (...)
/// </summary>
public record EllipsisLiteral : Expression;

#endregion

#region Collections

/// <summary>
/// List literal [1, 2, 3]
/// </summary>
public record ListLiteral : Expression
{
    public ImmutableArray<Expression> Elements { get; init; } = ImmutableArray<Expression>.Empty;
}

/// <summary>
/// Dictionary literal {"a": 1, "b": 2}
/// </summary>
public record DictLiteral : Expression
{
    public ImmutableArray<DictEntry> Entries { get; init; } = ImmutableArray<DictEntry>.Empty;
}

public record DictEntry
{
    public Expression Key { get; init; } = null!;
    public Expression Value { get; init; } = null!;
}

/// <summary>
/// Set literal {1, 2, 3}
/// </summary>
public record SetLiteral : Expression
{
    public ImmutableArray<Expression> Elements { get; init; } = ImmutableArray<Expression>.Empty;
}

/// <summary>
/// Tuple literal (1, 2, 3) or (1,)
/// </summary>
public record TupleLiteral : Expression
{
    public ImmutableArray<Expression> Elements { get; init; } = ImmutableArray<Expression>.Empty;
}

#endregion

#region Comprehensions

/// <summary>
/// List comprehension [expr for x in iterable if condition]
/// </summary>
public record ListComprehension : Expression
{
    public Expression Element { get; init; } = null!;
    public ImmutableArray<ComprehensionClause> Clauses { get; init; } = ImmutableArray<ComprehensionClause>.Empty;
}

/// <summary>
/// Set comprehension {expr for x in iterable if condition}
/// </summary>
public record SetComprehension : Expression
{
    public Expression Element { get; init; } = null!;
    public ImmutableArray<ComprehensionClause> Clauses { get; init; } = ImmutableArray<ComprehensionClause>.Empty;
}

/// <summary>
/// Dictionary comprehension {key: value for x in iterable if condition}
/// </summary>
public record DictComprehension : Expression
{
    public Expression Key { get; init; } = null!;
    public Expression Value { get; init; } = null!;
    public ImmutableArray<ComprehensionClause> Clauses { get; init; } = ImmutableArray<ComprehensionClause>.Empty;
}

/// <summary>
/// Base class for comprehension clauses (for/if)
/// </summary>
public abstract record ComprehensionClause : Node;

/// <summary>
/// For clause in comprehension (for x in iterable)
/// </summary>
public record ForClause : ComprehensionClause
{
    public Expression Target { get; init; } = null!;  // Loop variable (single identifier only for now)
    public Expression Iterator { get; init; } = null!;
}

/// <summary>
/// If clause in comprehension (if condition)
/// </summary>
public record IfClause : ComprehensionClause
{
    public Expression Condition { get; init; } = null!;
}

#endregion

#region Primary Expressions

/// <summary>
/// Identifier/name reference
/// </summary>
public record Identifier : Expression
{
    public string Name { get; init; } = "";
}

/// <summary>
/// Member access (obj.member or obj?.member)
/// </summary>
public record MemberAccess : Expression
{
    public Expression Object { get; init; } = null!;
    public string Member { get; init; } = "";
    public bool IsNullConditional { get; init; }  // obj?.member
}

/// <summary>
/// Index access (obj[index])
/// </summary>
public record IndexAccess : Expression
{
    public Expression Object { get; init; } = null!;
    public Expression Index { get; init; } = null!;
}

/// <summary>
/// Slice access (obj[start:stop:step])
/// </summary>
public record SliceAccess : Expression
{
    public Expression Object { get; init; } = null!;
    public Expression? Start { get; init; }
    public Expression? Stop { get; init; }
    public Expression? Step { get; init; }
}

/// <summary>
/// Function call (func(arg1, arg2, key=value))
/// </summary>
public record FunctionCall : Expression
{
    public Expression Function { get; init; } = null!;
    public ImmutableArray<Expression> Arguments { get; init; } = ImmutableArray<Expression>.Empty;
    public ImmutableArray<KeywordArgument> KeywordArguments { get; init; } = ImmutableArray<KeywordArgument>.Empty;
}

public record KeywordArgument
{
    public string Name { get; init; } = "";
    public Expression Value { get; init; } = null!;

    // Source location
    public int LineStart { get; init; }
    public int ColumnStart { get; init; }
    public int LineEnd { get; init; }
    public int ColumnEnd { get; init; }
}

#endregion

#region Operators

/// <summary>
/// Unary operation (+x, -x, not x, ~x)
/// </summary>
public record UnaryOp : Expression
{
    public UnaryOperator Operator { get; init; }
    public Expression Operand { get; init; } = null!;
}

public enum UnaryOperator
{
    Plus,      // +x
    Minus,     // -x
    Not,       // not x
    BitwiseNot // ~x
}

/// <summary>
/// Binary operation (a + b, a * b, a and b, etc.)
/// </summary>
public record BinaryOp : Expression
{
    public BinaryOperator Operator { get; init; }
    public Expression Left { get; init; } = null!;
    public Expression Right { get; init; } = null!;
}

public enum BinaryOperator
{
    // Arithmetic
    Add,
    Subtract,
    Multiply,
    Divide,
    FloorDivide,
    Modulo,
    Power,

    // Comparison
    Equal,
    NotEqual,
    LessThan,
    LessThanOrEqual,
    GreaterThan,
    GreaterThanOrEqual,

    // Logical
    And,
    Or,

    // Bitwise
    BitwiseAnd,
    BitwiseOr,
    BitwiseXor,
    LeftShift,
    RightShift,

    // Membership and Identity
    In,
    NotIn,
    Is,
    IsNot,

    // Null coalescing
    NullCoalesce,

    // Pipe forward
    PipeForward
}

/// <summary>
/// Comparison chain (a < b < c)
/// </summary>
public record ComparisonChain : Expression
{
    public ImmutableArray<Expression> Operands { get; init; } = ImmutableArray<Expression>.Empty;
    public ImmutableArray<ComparisonOperator> Operators { get; init; } = ImmutableArray<ComparisonOperator>.Empty;
}

public enum ComparisonOperator
{
    Equal,
    NotEqual,
    LessThan,
    LessThanOrEqual,
    GreaterThan,
    GreaterThanOrEqual,
    In,
    NotIn,
    Is,
    IsNot
}

#endregion

#region Advanced Expressions

/// <summary>
/// Conditional/ternary expression (value if test else other)
/// </summary>
public record ConditionalExpression : Expression
{
    public Expression Test { get; init; } = null!;
    public Expression ThenValue { get; init; } = null!;
    public Expression ElseValue { get; init; } = null!;
}

/// <summary>
/// Lambda expression (lambda x, y: x + y)
/// </summary>
public record LambdaExpression : Expression
{
    public ImmutableArray<Parameter> Parameters { get; init; } = ImmutableArray<Parameter>.Empty;
    public Expression Body { get; init; } = null!;
}

/// <summary>
/// Type cast (value as Type)
/// </summary>
public record TypeCast : Expression
{
    public Expression Value { get; init; } = null!;
    public TypeAnnotation TargetType { get; init; } = null!;
}

/// <summary>
/// Type coercion (value to Type or value to Type?)
/// Unlike TypeCast (as), this throws InvalidCastException on failure for non-nullable types
/// or returns None for nullable types (T?).
/// </summary>
public record TypeCoercion : Expression
{
    public Expression Value { get; init; } = null!;
    public TypeAnnotation TargetType { get; init; } = null!;
}

/// <summary>
/// Type check (value is Type)
/// </summary>
public record TypeCheck : Expression
{
    public Expression Value { get; init; } = null!;
    public TypeAnnotation CheckType { get; init; } = null!;
}

/// <summary>
/// Parenthesized expression ((expression))
/// </summary>
public record Parenthesized : Expression
{
    public Expression Expression { get; init; } = null!;
}

/// <summary>
/// Super expression (super())
/// Provides access to the parent class. Can only be used in specific contexts:
/// - __init__ methods to call super().__init__(...)
/// - Dunder methods to call super().__any_dunder__(...)
/// - @override methods to call super().method(...)
/// </summary>
public record SuperExpression : Expression;

/// <summary>
/// Walrus/assignment expression (name := value)
/// Assigns value to name and returns the value.
/// </summary>
public record WalrusExpression : Expression
{
    public string Target { get; init; } = "";
    public Expression Value { get; init; } = null!;
}

/// <summary>
/// Try expression (try expr or try[ExceptionType] expr)
/// Wraps an expression in Result[T, E] where E is the exception type.
/// If the expression raises an exception of type E, the result holds its Err case.
/// </summary>
public record TryExpression : Expression
{
    public Expression Operand { get; init; } = null!;
    public TypeAnnotation? ExceptionType { get; init; }  // Optional: try[ValueError] expr
}

/// <summary>
/// Maybe expression (maybe expr)
/// Wraps a nullable expression in Optional[T].
/// If the expression is None, the result holds the empty Optional case.
/// </summary>
public record MaybeExpression : Expression
{
    public Expression Operand { get; init; } = null!;
}

#endregion
