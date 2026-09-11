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
    ///
    /// <para><b>Canonical-input contract:</b> callers pass unwrapped expressions — parenthesized
    /// wrappers are stripped by upstream unwrap points before the expression reaches this method:
    /// <c>NarrowingConditionInterpreter.Recognize</c> (NarrowingFlowAnalysis.cs) and
    /// <c>CollectAssignedKeys</c> (NarrowingFlowAnalysis.cs). This method does not handle
    /// <see cref="Parenthesized"/> — adding such an arm would mask a missing upstream unwrap.</para>
    ///
    /// <para><b>Consumers:</b> <c>ControlFlowGraphBuilder</c>, <c>NarrowingFlowAnalysis</c>
    /// (Recognize path, two call sites; CollectAssignedKeys default arm), <c>TypeChecker</c>
    /// (via private wrapper), <c>RoslynEmitter</c> (delegate).</para>
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
    ///
    /// <para>Shares the canonical-input contract of <see cref="ExtractNarrowingKey"/>: callers
    /// pass unwrapped expressions; this method does not handle <see cref="Parenthesized"/>.</para>
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
    /// True when <paramref name="root"/> or any descendant satisfies <paramref name="predicate"/>.
    /// A structural descendant walk over <see cref="Node.GetChildNodes"/> — there is no kind list
    /// to go stale, which is how the member-call, index, keyword-argument, star-argument,
    /// container-element and conditional hosts are all covered by one walk.
    /// </summary>
    /// <param name="root">The node to search, inclusive of itself.</param>
    /// <param name="predicate">What the caller is looking for.</param>
    /// <param name="stopAt">
    /// An optional boundary. A caller looking for a construct whose meaning is scoped — a walrus,
    /// say, whose target binds the enclosing Python scope — stops at
    /// <see cref="LambdaExpression"/>, since a lambda body is its own scope. Comprehensions are
    /// deliberately NOT a boundary for that use: Sharpy's comprehension walrus binds the enclosing
    /// scope (<c>walrus_operator.md</c>).
    /// </param>
    internal static bool ContainsDescendant(Node root, Func<Node, bool> predicate, Func<Node, bool>? stopAt = null)
    {
        if (predicate(root))
            return true;
        if (stopAt?.Invoke(root) == true)
            return false;
        foreach (var child in root.GetChildNodes())
        {
            if (ContainsDescendant(child, predicate, stopAt))
                return true;
        }
        return false;
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
    /// Canonicalizes a <em>store target</em> — the left side of an assignment or augmented
    /// assignment, an annotated declaration's name, a <c>for</c> / comprehension loop target, or a
    /// <c>with … as</c> target. Redundant parentheses never change what a target binds (#1170), so
    /// <c>(a) = 1</c>, <c>for (x) in xs</c>, <c>((a), b) = t</c>, <c>with cm as (f)</c> and
    /// <c>*(rest), last = xs</c> bind exactly like their unparenthesized spellings (python3 accepts
    /// every one of them).
    ///
    /// <para>The parser applies this ONCE, at every site that constructs a target
    /// (<c>ParseStoreTarget</c> and the assignment / annotated-declaration constructors in
    /// <c>ParseSimpleStatement</c>), so no downstream consumer — the checker's target authority
    /// <c>IsValidAssignmentTarget</c>, definite assignment, narrowing-key collection, the naming and
    /// collision validators, the emitter's stores, the LSP binding walkers — can ever see a
    /// <see cref="Parenthesized"/> in a target position. A <c>Parenthesized</c> arm in any of those
    /// switches is dead code; <c>AssignmentTargetDispatchTotalityTests</c> pins their arm sets
    /// without it and <c>StoreTargetCanonicalizationTests</c> pins the parser seam.</para>
    ///
    /// <para><b>The starred element is one node in every spelling (#1841).</b> A <c>*</c> inside a
    /// group reaches this helper as a <see cref="SpreadElement"/> when the group was parsed by the
    /// <em>expression</em> parser (an assignment's left side, a list display anywhere) and as a
    /// <see cref="StarExpression"/> when it was parsed by the store-target element parser (an
    /// unparenthesized <c>for</c>/assignment target list). Everything downstream — the target
    /// authority <c>IsValidAssignmentTarget</c>, the one unpacking arity rule, the emitter's store
    /// path — keys on <c>StarExpression</c>, so the expression-parsed spellings silently carried an
    /// element no store path recognized: <c>(a, *rest) = t</c>, <c>[a, *rest] = t</c>,
    /// <c>[a, [b, *c]] = t</c> and <c>(a, *rest), b = t</c> were all SPY0225 while <c>a, *rest = t</c>
    /// and <c>for (a, *rest) in …</c> worked (#1841, #1692). Canonicalizing the spread into the star
    /// here makes the two parse routes agree at the one seam every store target already passes
    /// through, in all four unpacking positions at once.</para>
    ///
    /// <para>Refusals that must survive: <c>(*a), b = xs</c> and <c>(*a) = xs</c> are Python
    /// SyntaxErrors ("cannot use starred expression here"), while the comma-suffixed
    /// <c>(*a,) = xs</c> is legal — and the parser gives all three the same one-element
    /// <see cref="TupleLiteral"/>, so no rule here can separate them. A group whose <em>only</em>
    /// element is the star therefore keeps its <c>SpreadElement</c> and stays refused by the target
    /// authority (SPY0225); <c>(*a,) = xs</c> and <c>[*a] = xs</c> are the legal spellings that stay
    /// refused with it (#1845). Parentheses around a walrus target (<c>((a) := 1)</c>) and around an
    /// <c>except … as</c> name are Python syntax errors too; those two parse sites deliberately do
    /// not canonicalize.</para>
    /// </summary>
    public static Expression CanonicalizeStoreTarget(Expression target)
    {
        return target switch
        {
            Parenthesized paren => CanonicalizeStoreTarget(paren.Expression),
            TupleLiteral tuple => tuple with
            {
                Elements = CanonicalizeStoreTargetElements(tuple.Elements)
            },
            ListLiteral list => new TupleLiteral
            {
                Elements = CanonicalizeStoreTargetElements(list.Elements),
                IsListDisplay = true,
                LineStart = list.LineStart,
                ColumnStart = list.ColumnStart,
                LineEnd = list.LineEnd,
                ColumnEnd = list.ColumnEnd,
            },
            StarExpression star => star with { Operand = CanonicalizeStoreTarget(star.Operand) },
            _ => target,
        };
    }

    /// <summary>
    /// Canonicalizes the elements of a tuple/list-display store target, turning an
    /// expression-parsed <see cref="SpreadElement"/> into the <see cref="StarExpression"/> every
    /// store path keys on (#1841). A group of one element is left alone: see the
    /// <c>(*a)</c> / <c>(*a,)</c> note on <see cref="CanonicalizeStoreTarget"/>.
    /// </summary>
    private static ImmutableArray<Expression> CanonicalizeStoreTargetElements(
        ImmutableArray<Expression> elements)
    {
        var soleElement = elements.Length <= 1;
        return elements
            .Select(e => e is SpreadElement spread && !soleElement
                ? new StarExpression
                {
                    Operand = CanonicalizeStoreTarget(spread.Value),
                    LineStart = spread.LineStart,
                    ColumnStart = spread.ColumnStart,
                    LineEnd = spread.LineEnd,
                    ColumnEnd = spread.ColumnEnd,
                    Span = spread.Span,
                }
                : CanonicalizeStoreTarget(e))
            .ToImmutableArray();
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
    /// The single docstring-classification authority (#1247): decides whether a suite's already-parsed
    /// leading statement is a docstring, and hands back its text.
    ///
    /// <para>The rule is CPython's, measured on python3 3.12.13: a docstring is <em>an expression
    /// statement whose expression is a string constant</em>. It is a property of the parsed statement,
    /// not of the leading token — which is exactly what the token peeks this replaced got wrong. They
    /// consumed a leading <c>String</c> token before any statement was parsed, so <c>"world" - False</c>
    /// became a docstring plus a dangling <c>-False</c> (a silently wrong AST that parsed fine), and
    /// <c>"a".upper()</c> became a parse error at the orphaned <c>.</c>. Both now parse as the single
    /// expression statement CPython produces.</para>
    ///
    /// <para>Grouping is transparent, per <see cref="UnwrapParenthesized"/> and the #1197 rule:
    /// <c>("doc")</c> is the same docstring as <c>"doc"</c>, matching CPython (whose AST has no paren
    /// node). Raw strings count — <c>r"doc"</c> is a <c>str</c> constant to CPython and is a
    /// <see cref="StringLiteral"/> here. F-strings, t-strings and byte strings do not: CPython gives
    /// <c>JoinedStr</c>/<c>Constant(bytes)</c> rather than a string constant, and each is its own AST
    /// record here, so they fall through without a special case.</para>
    ///
    /// <para>Implicit concatenation (<c>"a" "b"</c>, which CPython joins to <c>'ab'</c> before the
    /// docstring rule ever sees it) is not a Sharpy expression at all — adjacent string literals are a
    /// parse error in every position. This helper therefore never sees a concatenation to join; the
    /// leading-token peeks used to accept the form in this one position only, yielding docstring
    /// <c>'a'</c> plus a stray <c>"b"</c> statement.</para>
    /// </summary>
    public static bool TryGetDocString(Statement stmt, [MaybeNullWhen(false)] out string docString)
    {
        if (stmt is ExpressionStatement exprStmt
            && UnwrapParenthesized(exprStmt.Expression) is StringLiteral literal)
        {
            docString = literal.Value;
            return true;
        }

        docString = null;
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

    public static readonly object NoneValue = new();

    public static object? TryGetLiteralValue(Expression expr)
    {
        return expr switch
        {
            StringLiteral s => s.Value,
            IntegerLiteral i => i.Value,
            FloatLiteral f when double.TryParse(
                f.Value.Replace("_", "", StringComparison.Ordinal),
                NumberStyles.Float, CultureInfo.InvariantCulture, out var d) => d,
            BooleanLiteral b => b.Value,
            NoneLiteral => NoneValue,
            UnaryOp { Operator: UnaryOperator.Minus, Operand: IntegerLiteral negInt }
                => "-" + negInt.Value,
            UnaryOp { Operator: UnaryOperator.Minus, Operand: FloatLiteral negFloat }
                when double.TryParse(
                    negFloat.Value.Replace("_", "", StringComparison.Ordinal),
                    NumberStyles.Float, CultureInfo.InvariantCulture, out var nd) => -nd,
            _ => null,
        };
    }

    private static string? ExtractMemberAccessNarrowingKey(MemberAccess ma)
    {
        var objectKey = ExtractNarrowingKey(ma.Object);
        if (objectKey == null)
            return null;
        return $"{objectKey}.{ma.Member}";
    }
}
