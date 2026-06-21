using System.Collections.Immutable;
using System.Diagnostics;
using Sharpy.Compiler.Text;

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
/// Byte string literal (b"hello", b'\xff')
/// </summary>
public record BytesLiteralExpression : Expression
{
    public string Value { get; init; } = "";
}

/// <summary>
/// F-string literal (f"Hello {name}")
/// </summary>
public record FStringLiteral : Expression
{
    public ImmutableArray<FStringPart> Parts { get; init; } = ImmutableArray<FStringPart>.Empty;

    /// <inheritdoc/>
    public override void ValidateInvariants()
    {
        base.ValidateInvariants();
        Debug.Assert(Parts != null, "FStringLiteral.Parts cannot be null");
    }

    /// <inheritdoc/>
    public override IEnumerable<Node> GetChildNodes()
    {
        foreach (var part in Parts)
        {
            if (part.Expression != null)
                yield return part.Expression;
        }
    }
}

public record FStringPart
{
    public string? Text { get; init; }
    public Expression? Expression { get; init; }
    public string? FormatSpec { get; init; }  // Format specification (e.g., ".2f", ">10")
}

/// <summary>
/// Template string literal (t"..." / t'...' / t"""..."""), PEP 750.
/// Same structure as FStringLiteral but produces a Template object, not a string.
/// </summary>
public record TStringLiteral : Expression
{
    public ImmutableArray<FStringPart> Parts { get; init; } = ImmutableArray<FStringPart>.Empty;

    /// <inheritdoc/>
    public override void ValidateInvariants()
    {
        base.ValidateInvariants();
        Debug.Assert(Parts != null, "TStringLiteral.Parts cannot be null");
    }

    /// <inheritdoc/>
    public override IEnumerable<Node> GetChildNodes()
    {
        foreach (var part in Parts)
        {
            if (part.Expression != null)
                yield return part.Expression;
        }
    }
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

    /// <inheritdoc/>
    public override void ValidateInvariants()
    {
        base.ValidateInvariants();
        Debug.Assert(Elements != null, "ListLiteral.Elements cannot be null");
    }

    /// <inheritdoc/>
    public override IEnumerable<Node> GetChildNodes() => Elements;
}

/// <summary>
/// Dictionary literal {"a": 1, "b": 2}
/// </summary>
public record DictLiteral : Expression
{
    public ImmutableArray<DictEntry> Entries { get; init; } = ImmutableArray<DictEntry>.Empty;

    /// <inheritdoc/>
    public override void ValidateInvariants()
    {
        base.ValidateInvariants();
        Debug.Assert(Entries != null, "DictLiteral.Entries cannot be null");
    }

    /// <inheritdoc/>
    public override IEnumerable<Node> GetChildNodes()
    {
        foreach (var entry in Entries)
        {
            if (entry.Key != null)
                yield return entry.Key;
            yield return entry.Value;
        }
    }
}

public record DictEntry
{
    /// <summary>
    /// null when this entry is a dict spread (**expr), in which case Value is the spread target
    /// </summary>
    public Expression? Key { get; init; }
    public Expression Value { get; init; } = null!;
}

/// <summary>
/// Set literal {1, 2, 3}
/// </summary>
public record SetLiteral : Expression
{
    public ImmutableArray<Expression> Elements { get; init; } = ImmutableArray<Expression>.Empty;

    /// <inheritdoc/>
    public override void ValidateInvariants()
    {
        base.ValidateInvariants();
        Debug.Assert(Elements != null, "SetLiteral.Elements cannot be null");
    }

    /// <inheritdoc/>
    public override IEnumerable<Node> GetChildNodes() => Elements;
}

/// <summary>
/// Tuple literal (1, 2, 3) or (1,) or named tuple (x=1.0, y=2.0)
/// </summary>
public record TupleLiteral : Expression
{
    public ImmutableArray<Expression> Elements { get; init; } = ImmutableArray<Expression>.Empty;

    /// <summary>
    /// Element names for named tuple literals. Empty for unnamed tuples.
    /// When present, must have the same count as Elements.
    /// </summary>
    public ImmutableArray<string?> ElementNames { get; init; } = ImmutableArray<string?>.Empty;

    /// <inheritdoc/>
    public override void ValidateInvariants()
    {
        base.ValidateInvariants();
        Debug.Assert(Elements != null, "TupleLiteral.Elements cannot be null");
    }

    /// <inheritdoc/>
    public override IEnumerable<Node> GetChildNodes() => Elements;
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

    /// <inheritdoc/>
    public override void ValidateInvariants()
    {
        base.ValidateInvariants();
        Debug.Assert(Element != null, "ListComprehension.Element cannot be null");
        Debug.Assert(Clauses != null, "ListComprehension.Clauses cannot be null");
        Debug.Assert(Clauses.Length > 0, "ListComprehension.Clauses must have at least one clause");
    }

    /// <inheritdoc/>
    public override IEnumerable<Node> GetChildNodes()
    {
        yield return Element;
        foreach (var clause in Clauses)
            yield return clause;
    }
}

/// <summary>
/// Set comprehension {expr for x in iterable if condition}
/// </summary>
public record SetComprehension : Expression
{
    public Expression Element { get; init; } = null!;
    public ImmutableArray<ComprehensionClause> Clauses { get; init; } = ImmutableArray<ComprehensionClause>.Empty;

    /// <inheritdoc/>
    public override void ValidateInvariants()
    {
        base.ValidateInvariants();
        Debug.Assert(Element != null, "SetComprehension.Element cannot be null");
        Debug.Assert(Clauses != null, "SetComprehension.Clauses cannot be null");
        Debug.Assert(Clauses.Length > 0, "SetComprehension.Clauses must have at least one clause");
    }

    /// <inheritdoc/>
    public override IEnumerable<Node> GetChildNodes()
    {
        yield return Element;
        foreach (var clause in Clauses)
            yield return clause;
    }
}

/// <summary>
/// Dictionary comprehension {key: value for x in iterable if condition}
/// </summary>
public record DictComprehension : Expression
{
    public Expression Key { get; init; } = null!;
    public Expression Value { get; init; } = null!;
    public ImmutableArray<ComprehensionClause> Clauses { get; init; } = ImmutableArray<ComprehensionClause>.Empty;

    /// <inheritdoc/>
    public override void ValidateInvariants()
    {
        base.ValidateInvariants();
        Debug.Assert(Key != null, "DictComprehension.Key cannot be null");
        Debug.Assert(Value != null, "DictComprehension.Value cannot be null");
        Debug.Assert(Clauses != null, "DictComprehension.Clauses cannot be null");
        Debug.Assert(Clauses.Length > 0, "DictComprehension.Clauses must have at least one clause");
    }

    /// <inheritdoc/>
    public override IEnumerable<Node> GetChildNodes()
    {
        yield return Key;
        yield return Value;
        foreach (var clause in Clauses)
            yield return clause;
    }
}

/// <summary>
/// Dict spread comprehension {**d for d in dicts}
/// </summary>
public record DictSpreadComprehension : Expression
{
    public Expression Spread { get; init; } = null!;
    public ImmutableArray<ComprehensionClause> Clauses { get; init; } = ImmutableArray<ComprehensionClause>.Empty;

    /// <inheritdoc/>
    public override void ValidateInvariants()
    {
        base.ValidateInvariants();
        Debug.Assert(Spread != null, "DictSpreadComprehension.Spread cannot be null");
        Debug.Assert(Clauses != null, "DictSpreadComprehension.Clauses cannot be null");
        Debug.Assert(Clauses.Length > 0, "DictSpreadComprehension.Clauses must have at least one clause");
    }

    /// <inheritdoc/>
    public override IEnumerable<Node> GetChildNodes()
    {
        yield return Spread;
        foreach (var clause in Clauses)
            yield return clause;
    }
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

    /// <inheritdoc/>
    public override void ValidateInvariants()
    {
        base.ValidateInvariants();
        Debug.Assert(Target != null, "ForClause.Target cannot be null");
        Debug.Assert(Iterator != null, "ForClause.Iterator cannot be null");
    }

    /// <inheritdoc/>
    public override IEnumerable<Node> GetChildNodes()
    {
        yield return Target;
        yield return Iterator;
    }
}

/// <summary>
/// If clause in comprehension (if condition)
/// </summary>
public record IfClause : ComprehensionClause
{
    public Expression Condition { get; init; } = null!;

    /// <inheritdoc/>
    public override void ValidateInvariants()
    {
        base.ValidateInvariants();
        Debug.Assert(Condition != null, "IfClause.Condition cannot be null");
    }

    /// <inheritdoc/>
    public override IEnumerable<Node> GetChildNodes()
    {
        yield return Condition;
    }
}

#endregion

#region Primary Expressions

/// <summary>
/// Identifier/name reference
/// </summary>
public record Identifier : Expression
{
    public string Name { get; init; } = "";
    public bool IsNameBacktickEscaped { get; init; }

    /// <inheritdoc/>
    public override void ValidateInvariants()
    {
        base.ValidateInvariants();
        Debug.Assert(!string.IsNullOrEmpty(Name), "Identifier.Name cannot be null or empty");
    }
}

/// <summary>
/// Member access (obj.member or obj?.member)
/// </summary>
public record MemberAccess : Expression
{
    public Expression Object { get; init; } = null!;
    public string Member { get; init; } = "";
    public bool IsNullConditional { get; init; }  // obj?.member
    public bool IsMemberBacktickEscaped { get; init; }

    /// <inheritdoc/>
    public override void ValidateInvariants()
    {
        base.ValidateInvariants();
        Debug.Assert(Object != null, "MemberAccess.Object cannot be null");
        Debug.Assert(!string.IsNullOrEmpty(Member), "MemberAccess.Member cannot be null or empty");
    }

    /// <inheritdoc/>
    public override IEnumerable<Node> GetChildNodes()
    {
        yield return Object;
    }
}

/// <summary>
/// Index access (obj[index])
/// </summary>
public record IndexAccess : Expression
{
    public Expression Object { get; init; } = null!;
    public Expression Index { get; init; } = null!;

    /// <inheritdoc/>
    public override void ValidateInvariants()
    {
        base.ValidateInvariants();
        Debug.Assert(Object != null, "IndexAccess.Object cannot be null");
        Debug.Assert(Index != null, "IndexAccess.Index cannot be null");
    }

    /// <inheritdoc/>
    public override IEnumerable<Node> GetChildNodes()
    {
        yield return Object;
        yield return Index;
    }
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

    /// <inheritdoc/>
    public override void ValidateInvariants()
    {
        base.ValidateInvariants();
        Debug.Assert(Object != null, "SliceAccess.Object cannot be null");
    }

    /// <inheritdoc/>
    public override IEnumerable<Node> GetChildNodes()
    {
        yield return Object;
        if (Start != null)
            yield return Start;
        if (Stop != null)
            yield return Stop;
        if (Step != null)
            yield return Step;
    }
}

/// <summary>
/// A single dimension in a multi-axis subscript — either an index expression
/// or a slice (start:stop:step).
/// </summary>
public record SubscriptDimension : Node
{
    public bool IsSlice { get; init; }
    public Expression? Index { get; init; }
    public Expression? Start { get; init; }
    public Expression? Stop { get; init; }
    public Expression? Step { get; init; }

    /// <inheritdoc/>
    public override IEnumerable<Node> GetChildNodes()
    {
        if (Index != null)
            yield return Index;
        if (Start != null)
            yield return Start;
        if (Stop != null)
            yield return Stop;
        if (Step != null)
            yield return Step;
    }
}

/// <summary>
/// Multi-axis subscript (obj[dim0, dim1, ...]) where dimensions can be
/// indices or slices. Used for NdArray multi-axis indexing and slicing.
/// </summary>
public record MultiAxisAccess : Expression
{
    public Expression Object { get; init; } = null!;
    public ImmutableArray<SubscriptDimension> Dimensions { get; init; }

    /// <inheritdoc/>
    public override void ValidateInvariants()
    {
        base.ValidateInvariants();
        Debug.Assert(Object != null, "MultiAxisAccess.Object cannot be null");
        Debug.Assert(Dimensions.Length >= 2, "MultiAxisAccess must have at least 2 dimensions");
    }

    /// <inheritdoc/>
    public override IEnumerable<Node> GetChildNodes()
    {
        yield return Object;
        foreach (var dim in Dimensions)
            yield return dim;
    }
}

/// <summary>
/// Function call (func(arg1, arg2, key=value))
/// </summary>
public record FunctionCall : Expression
{
    public Expression Function { get; init; } = null!;
    public ImmutableArray<Expression> Arguments { get; init; } = ImmutableArray<Expression>.Empty;
    public ImmutableArray<KeywordArgument> KeywordArguments { get; init; } = ImmutableArray<KeywordArgument>.Empty;

    /// <inheritdoc/>
    public override void ValidateInvariants()
    {
        base.ValidateInvariants();
        Debug.Assert(Function != null, "FunctionCall.Function cannot be null");
        Debug.Assert(Arguments != null, "FunctionCall.Arguments cannot be null");
        Debug.Assert(KeywordArguments != null, "FunctionCall.KeywordArguments cannot be null");
    }

    /// <inheritdoc/>
    public override IEnumerable<Node> GetChildNodes()
    {
        yield return Function;
        foreach (var arg in Arguments)
            yield return arg;
        foreach (var kwArg in KeywordArguments)
            yield return kwArg.Value;
    }
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

    // Span covering the keyword name through the value (set by the parser).
    public TextSpan? Span { get; init; }
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

    /// <inheritdoc/>
    public override void ValidateInvariants()
    {
        base.ValidateInvariants();
        Debug.Assert(Operand != null, "UnaryOp.Operand cannot be null");
    }

    /// <inheritdoc/>
    public override IEnumerable<Node> GetChildNodes()
    {
        yield return Operand;
    }
}

public enum UnaryOperator
{
    Plus,      // +x
    Minus,     // -x
    Not,       // not x
    BitwiseNot // ~x
}

/// <summary>
/// Postfix ? operator for early-return on Result/Optional (expr?)
/// </summary>
public record QuestionMarkExpression : Expression
{
    public Expression Operand { get; init; } = null!;

    /// <inheritdoc/>
    public override void ValidateInvariants()
    {
        base.ValidateInvariants();
        Debug.Assert(Operand != null, "QuestionMarkExpression.Operand cannot be null");
    }

    /// <inheritdoc/>
    public override IEnumerable<Node> GetChildNodes()
    {
        yield return Operand;
    }
}

/// <summary>
/// Binary operation (a + b, a * b, a and b, etc.)
/// </summary>
public record BinaryOp : Expression
{
    public BinaryOperator Operator { get; init; }
    public Expression Left { get; init; } = null!;
    public Expression Right { get; init; } = null!;

    /// <summary>
    /// Line of the operator token (1-based). 0 means not set (backward compatibility).
    /// </summary>
    public int OperatorLine { get; init; }

    /// <summary>
    /// Column of the operator token (1-based). 0 means not set (backward compatibility).
    /// </summary>
    public int OperatorColumn { get; init; }

    /// <inheritdoc/>
    public override void ValidateInvariants()
    {
        base.ValidateInvariants();
        Debug.Assert(Left != null, "BinaryOp.Left cannot be null");
        Debug.Assert(Right != null, "BinaryOp.Right cannot be null");
    }

    /// <inheritdoc/>
    public override IEnumerable<Node> GetChildNodes()
    {
        yield return Left;
        yield return Right;
    }
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

    /// <summary>
    /// Positions of each operator token, parallel to Operators.
    /// Empty means not set (backward compatibility).
    /// </summary>
    public ImmutableArray<(int Line, int Column)> OperatorPositions { get; init; } = ImmutableArray<(int Line, int Column)>.Empty;

    /// <inheritdoc/>
    public override void ValidateInvariants()
    {
        base.ValidateInvariants();
        Debug.Assert(Operands != null, "ComparisonChain.Operands cannot be null");
        Debug.Assert(Operators != null, "ComparisonChain.Operators cannot be null");
        Debug.Assert(Operands.Length >= 2, "ComparisonChain.Operands must have at least 2 elements");
        Debug.Assert(Operators.Length == Operands.Length - 1,
            "ComparisonChain.Operators.Length must equal Operands.Length - 1");
        Debug.Assert(OperatorPositions.IsEmpty || OperatorPositions.Length == Operators.Length,
            "ComparisonChain.OperatorPositions.Length must equal Operators.Length when non-empty");
    }

    /// <inheritdoc/>
    public override IEnumerable<Node> GetChildNodes() => Operands;
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

    /// <inheritdoc/>
    public override void ValidateInvariants()
    {
        base.ValidateInvariants();
        Debug.Assert(Test != null, "ConditionalExpression.Test cannot be null");
        Debug.Assert(ThenValue != null, "ConditionalExpression.ThenValue cannot be null");
        Debug.Assert(ElseValue != null, "ConditionalExpression.ElseValue cannot be null");
    }

    /// <inheritdoc/>
    public override IEnumerable<Node> GetChildNodes()
    {
        yield return Test;
        yield return ThenValue;
        yield return ElseValue;
    }
}

/// <summary>
/// Lambda expression: traditional (lambda x, y: x + y) or arrow ((x: int) -> x + 1)
/// </summary>
public record LambdaExpression : Expression
{
    public ImmutableArray<Parameter> Parameters { get; init; } = ImmutableArray<Parameter>.Empty;
    public Expression Body { get; init; } = null!;
    public TypeAnnotation? ReturnType { get; init; }
    public bool IsArrowSyntax { get; init; }

    /// <inheritdoc/>
    public override void ValidateInvariants()
    {
        base.ValidateInvariants();
        Debug.Assert(Parameters != null, "LambdaExpression.Parameters cannot be null");
        Debug.Assert(Body != null, "LambdaExpression.Body cannot be null");
    }

    /// <inheritdoc/>
    public override IEnumerable<Node> GetChildNodes()
    {
        // Note: param.Type is TypeAnnotation which doesn't inherit from Node
        foreach (var param in Parameters)
        {
            if (param.DefaultValue != null)
                yield return param.DefaultValue;
        }
        yield return Body;
    }
}

/// <summary>
/// Call-site argument with ref/out/in modifier: swap(ref x, ref y), parse("42", out result)
/// When InlineName is set, represents an inline out declaration: parse("42", out value: int)
/// </summary>
public record ModifiedArgument : Expression
{
    public ParameterModifier Modifier { get; init; }
    public Expression Argument { get; init; } = null!;

    /// <summary>
    /// When set, this argument is an inline out variable declaration (e.g., out value: int).
    /// The Argument property is set to a synthetic Identifier with this name for downstream compatibility.
    /// </summary>
    public string? InlineName { get; init; }

    /// <summary>
    /// The type annotation for an inline out declaration (e.g., int in out value: int).
    /// Must be non-null when InlineName is set.
    /// </summary>
    public TypeAnnotation? InlineType { get; init; }

    /// <inheritdoc/>
    public override void ValidateInvariants()
    {
        base.ValidateInvariants();
        Debug.Assert(Argument != null, "ModifiedArgument.Argument cannot be null");
        Debug.Assert(Modifier != ParameterModifier.None, "ModifiedArgument.Modifier must not be None");

        if (InlineName != null)
        {
            Debug.Assert(Modifier == ParameterModifier.Out,
                "ModifiedArgument: inline declarations are only valid with Out modifier");
            Debug.Assert(InlineType != null,
                "ModifiedArgument: InlineType must be set when InlineName is set");
        }
    }

    /// <inheritdoc/>
    public override IEnumerable<Node> GetChildNodes()
    {
        yield return Argument;
    }
}

/// <summary>
/// Type coercion (value to Type or value to Type?)
/// Throws InvalidCastException on failure for non-nullable types
/// or returns None for nullable types (T?).
/// </summary>
public record TypeCoercion : Expression
{
    public Expression Value { get; init; } = null!;
    public TypeAnnotation TargetType { get; init; } = null!;

    /// <inheritdoc/>
    public override void ValidateInvariants()
    {
        base.ValidateInvariants();
        Debug.Assert(Value != null, "TypeCoercion.Value cannot be null");
        Debug.Assert(TargetType != null, "TypeCoercion.TargetType cannot be null");
    }

    /// <inheritdoc/>
    public override IEnumerable<Node> GetChildNodes()
    {
        // Note: TargetType is TypeAnnotation which doesn't inherit from Node
        yield return Value;
    }
}

/// <summary>
/// Type check (value is Type)
/// </summary>
public record TypeCheck : Expression
{
    public Expression Value { get; init; } = null!;
    public TypeAnnotation CheckType { get; init; } = null!;

    /// <inheritdoc/>
    public override void ValidateInvariants()
    {
        base.ValidateInvariants();
        Debug.Assert(Value != null, "TypeCheck.Value cannot be null");
        Debug.Assert(CheckType != null, "TypeCheck.CheckType cannot be null");
    }

    /// <inheritdoc/>
    public override IEnumerable<Node> GetChildNodes()
    {
        // Note: CheckType is TypeAnnotation which doesn't inherit from Node
        yield return Value;
    }
}

/// <summary>
/// Parenthesized expression ((expression))
/// </summary>
public record Parenthesized : Expression
{
    public Expression Expression { get; init; } = null!;

    /// <inheritdoc/>
    public override void ValidateInvariants()
    {
        base.ValidateInvariants();
        Debug.Assert(Expression != null, "Parenthesized.Expression cannot be null");
    }

    /// <inheritdoc/>
    public override IEnumerable<Node> GetChildNodes()
    {
        yield return Expression;
    }
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

    /// <inheritdoc/>
    public override void ValidateInvariants()
    {
        base.ValidateInvariants();
        Debug.Assert(!string.IsNullOrEmpty(Target), "WalrusExpression.Target cannot be null or empty");
        Debug.Assert(Value != null, "WalrusExpression.Value cannot be null");
    }

    /// <inheritdoc/>
    public override IEnumerable<Node> GetChildNodes()
    {
        yield return Value;
    }
}

/// <summary>
/// Try expression (try expr or try[ExceptionType] expr or try[A | B | C] expr)
/// Wraps an expression in Result[T, E] where E is the (common) exception type.
/// If the expression raises an exception matching one of the listed types, the
/// result holds its Err case; other exceptions propagate normally.
/// An empty <see cref="ExceptionTypes"/> array represents the untyped form `try expr`,
/// a single element represents `try[E] expr`, and multiple elements represent
/// `try[A | B | C] expr` union exception types.
/// </summary>
public record TryExpression : Expression
{
    public Expression Operand { get; init; } = null!;
    public ImmutableArray<TypeAnnotation> ExceptionTypes { get; init; } = ImmutableArray<TypeAnnotation>.Empty;

    /// <inheritdoc/>
    public override void ValidateInvariants()
    {
        base.ValidateInvariants();
        Debug.Assert(Operand != null, "TryExpression.Operand cannot be null");
        Debug.Assert(!ExceptionTypes.IsDefault, "TryExpression.ExceptionTypes must be initialized (use ImmutableArray.Empty for none)");
    }

    /// <inheritdoc/>
    public override IEnumerable<Node> GetChildNodes()
    {
        // Note: ExceptionTypes are TypeAnnotations which don't inherit from Node
        yield return Operand;
    }
}

/// <summary>
/// Maybe expression (maybe expr)
/// Wraps a nullable expression in Optional[T].
/// If the expression is None, the result holds the empty Optional case.
/// </summary>
public record MaybeExpression : Expression
{
    public Expression Operand { get; init; } = null!;

    /// <inheritdoc/>
    public override void ValidateInvariants()
    {
        base.ValidateInvariants();
        Debug.Assert(Operand != null, "MaybeExpression.Operand cannot be null");
    }

    /// <inheritdoc/>
    public override IEnumerable<Node> GetChildNodes()
    {
        yield return Operand;
    }
}

#endregion

#region Unpacking

/// <summary>
/// Star expression (*rest) used in unpacking targets: first, *rest = items
/// </summary>
public record StarExpression : Expression
{
    public Expression Operand { get; init; } = null!;

    public override void ValidateInvariants()
    {
        base.ValidateInvariants();
        Debug.Assert(Operand != null, "StarExpression.Operand cannot be null");
    }

    /// <inheritdoc/>
    public override IEnumerable<Node> GetChildNodes()
    {
        yield return Operand;
    }
}

#endregion

#region Spread

/// <summary>
/// Spread element (*expr) used in collection literals: [*a, 1, *b], {*a, *b}
/// </summary>
public record SpreadElement : Expression
{
    public Expression Value { get; init; } = null!;

    public override void ValidateInvariants()
    {
        base.ValidateInvariants();
        Debug.Assert(Value != null, "SpreadElement.Value cannot be null");
    }

    /// <inheritdoc/>
    public override IEnumerable<Node> GetChildNodes()
    {
        yield return Value;
    }
}

#endregion
