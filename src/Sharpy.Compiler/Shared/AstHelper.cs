using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Shared;

/// <summary>
/// Shared AST utility methods used by the parser, semantic analysis, and code generation.
/// </summary>
internal static class AstHelper
{
    /// <summary>
    /// Tries to extract a constant integer value from an expression.
    /// Handles IntegerLiteral and UnaryOp(Minus, IntegerLiteral) for negative indices.
    /// </summary>
    public static bool TryGetConstantIntIndex(Expression expr, out int value)
    {
        if (expr is IntegerLiteral intLit && int.TryParse(intLit.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        if (expr is UnaryOp { Operator: UnaryOperator.Minus, Operand: IntegerLiteral negIntLit }
            && int.TryParse(negIntLit.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var posValue))
        {
            value = -posValue;
            return true;
        }

        value = 0;
        return false;
    }

    /// <summary>
    /// Extracts a key to use for type narrowing from an expression.
    /// For simple identifiers, returns the name. For subscript expressions like arr[i], returns "arr[i]".
    /// For member access like self.value, returns "self.value".
    /// Returns null if the expression contains unsupported node types.
    /// </summary>
    public static string? ExtractNarrowingKey(Expression expr)
    {
        return expr switch
        {
            Identifier id => id.Name,
            IndexAccess indexAccess => BuildIndexAccessNarrowingKey(indexAccess),
            MemberAccess ma => ExtractMemberAccessNarrowingKey(ma),
            _ => null
        };
    }

    /// <summary>
    /// Builds a narrowing key for a subscript expression. The index must be distinguishable so
    /// sibling accesses like <c>t[0]</c> and <c>t[1]</c> get distinct keys — otherwise narrowing
    /// one tuple element would incorrectly shadow the others (and, via the index-access early
    /// return in CheckIndexAccess, suppress .ItemN lowering of the siblings). Returns null when
    /// the object or index cannot be keyed, so unsupported indices never collide on a shared key.
    /// </summary>
    private static string? BuildIndexAccessNarrowingKey(IndexAccess indexAccess)
    {
        var objectKey = ExtractNarrowingKey(indexAccess.Object);
        if (objectKey == null)
            return null;

        var indexKey = ExtractIndexComponentKey(indexAccess.Index);
        if (indexKey == null)
            return null;

        return $"{objectKey}[{indexKey}]";
    }

    /// <summary>
    /// Extracts a stable key component for a subscript index. Handles integer literals (including
    /// negated ones) and string literals as constants, and identifiers / nested subscripts /
    /// member accesses via <see cref="ExtractNarrowingKey"/>. Returns null for anything else.
    /// </summary>
    private static string? ExtractIndexComponentKey(Expression index)
    {
        return index switch
        {
            IntegerLiteral intLit => intLit.Value,
            UnaryOp { Operator: UnaryOperator.Minus, Operand: IntegerLiteral negLit } => "-" + negLit.Value,
            StringLiteral strLit => $"\"{strLit.Value}\"",
            _ => ExtractNarrowingKey(index)
        };
    }

    /// <summary>
    /// Checks whether an expression tree contains a walrus (assignment) expression.
    /// </summary>
    public static bool ContainsWalrusExpression(Expression expr)
    {
        return expr switch
        {
            WalrusExpression => true,
            BinaryOp binOp => ContainsWalrusExpression(binOp.Left) || ContainsWalrusExpression(binOp.Right),
            UnaryOp unaryOp => ContainsWalrusExpression(unaryOp.Operand),
            FunctionCall call => ContainsWalrusExpression(call.Function) || call.Arguments.Any(ContainsWalrusExpression),
            ComparisonChain cmp => cmp.Operands.Any(ContainsWalrusExpression),
            Parenthesized paren => ContainsWalrusExpression(paren.Expression),
            _ => false
        };
    }

    /// <summary>
    /// Strips any <see cref="Parenthesized"/> wrappers, returning the inner expression. This is the
    /// canonical normalization seam for callee-shape dispatch (#1170): redundant parentheses never
    /// change what an expression denotes, so <c>(isinstance)(x, T)</c>, <c>(Shape.Circle)(r)</c>,
    /// <c>(dict)()</c> and <c>(Token)("a")</c> must resolve — and narrow, and lower — exactly like
    /// their unparenthesized forms.
    ///
    /// <para>Every site that decides <em>what a call means</em> from the callee's surface syntax must
    /// dispatch on the result of this helper rather than on the raw <c>FunctionCall.Function</c>: the
    /// call-typing arms in <c>TypeChecker.CheckFunctionCall</c>, special-form detection, the
    /// callee-shape validators, and the emitter's <c>GenerateCall</c> (whose top-level unwrap #1147
    /// established the contract on the codegen side). Normalization strips parentheses only — it never
    /// looks through a call, an index, or any other expression, so <c>(get_fn())(x)</c> stays an
    /// ordinary call through a callable value.</para>
    ///
    /// <para>Note this is about <em>callees</em>, not reads: narrowing deliberately keeps type-test
    /// operands on the raw node (see the type-test-operand contract in <c>TypeChecker</c>).</para>
    /// </summary>
    public static Expression UnwrapParenthesized(Expression expr)
    {
        while (expr is Parenthesized paren)
            expr = paren.Expression;
        return expr;
    }

    /// <summary>
    /// The single stub-detection authority (#1214): recognizes a statement that is an ellipsis stub
    /// body (<c>...</c>) and hands back the underlying <see cref="EllipsisLiteral"/>.
    ///
    /// <para>Grouping is transparent, so the ellipsis is looked up through any
    /// <see cref="Parenthesized"/> wrappers via <see cref="UnwrapParenthesized"/> — <c>(...)</c> and
    /// <c>((...))</c> are the same stub as <c>...</c>. Before #1214 the class-member and
    /// declaration seams pattern-matched the raw expression, so a parenthesized ellipsis was neither
    /// a stub to the checker (wrong SPY0266 on an interface method) nor to the emitter (SPY0510 +
    /// SPY0908 in a class body), while the statement seam had already been unwrapped by #1197.</para>
    ///
    /// <para>This returns the <em>node</em> rather than a bare <c>bool</c> on purpose: span-sensitive
    /// callers keep their distinctions without re-implementing the unwrap.
    /// <c>BodylessSyntaxValidator</c> tells body-less declaration syntax (a parser-synthesized
    /// ellipsis, <c>Span is null</c>) apart from a user-written <c>...</c> or <c>(...)</c>
    /// (<c>Span</c> non-null) by testing the returned node's <see cref="Node.Span"/>. A boolean-only
    /// helper would force that validator to keep a private duplicate of the unwrap — exactly the
    /// drift this authority removes.</para>
    /// </summary>
    public static bool TryGetEllipsisStub(Statement stmt, [MaybeNullWhen(false)] out EllipsisLiteral ellipsis)
    {
        if (stmt is ExpressionStatement exprStmt
            && UnwrapParenthesized(exprStmt.Expression) is EllipsisLiteral literal)
        {
            ellipsis = literal;
            return true;
        }

        ellipsis = null;
        return false;
    }

    /// <summary>
    /// Whole-body convenience over <see cref="TryGetEllipsisStub"/>: true when <paramref name="body"/>
    /// is a single ellipsis-stub statement. Ellipsis-only — for the seams that accept <c>pass</c> as
    /// an equally valid stub body, use <see cref="IsAbstractStubBody"/>.
    /// </summary>
    public static bool IsEllipsisStubBody(ImmutableArray<Statement> body)
    {
        return body.Length == 1 && TryGetEllipsisStub(body[0], out _);
    }

    /// <summary>
    /// True when <paramref name="body"/> is a single stub statement — an ellipsis (<c>...</c>,
    /// including parenthesized forms) <em>or</em> a <see cref="PassStatement"/>.
    ///
    /// <para>This is the shape the abstract-member seams actually test (abstract/interface methods,
    /// function-style property and event stubs): a stub written <c>pass</c> is as abstract as one
    /// written <c>...</c>, so routing those seams through the ellipsis-only wrapper would silently
    /// stop recognizing them. Note <c>(pass)</c> is not a thing — <c>pass</c> is a statement, not an
    /// expression, so there is nothing to unwrap on that side.</para>
    ///
    /// <para>An empty body is deliberately <em>not</em> a stub here; the one site that also accepts
    /// <c>Body.Length == 0</c> keeps that disjunct at its own call site.</para>
    /// </summary>
    public static bool IsAbstractStubBody(ImmutableArray<Statement> body)
    {
        return body.Length == 1
            && (body[0] is PassStatement || TryGetEllipsisStub(body[0], out _));
    }

    private static string? ExtractMemberAccessNarrowingKey(MemberAccess ma)
    {
        var objectKey = ExtractNarrowingKey(ma.Object);
        if (objectKey == null)
            return null;
        return $"{objectKey}.{ma.Member}";
    }
}
