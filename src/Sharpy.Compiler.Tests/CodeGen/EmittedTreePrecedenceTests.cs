using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.CodeGen;
using Xunit;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Sharpy.Compiler.Tests.CodeGen;

public class EmittedTreePrecedenceTests
{
    private static IdentifierNameSyntax Name(string n) => IdentifierName(n);

    // --- Flagged shapes (Violations non-empty) ---

    [Fact]
    [Trait("Category", "Infrastructure")]
    public void ConditionalUnderMemberAccess_Flagged()
    {
        // (a ? b : c).Length — without the parens
        var conditional = ConditionalExpression(Name("a"), Name("b"), Name("c"));
        var tree = MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            conditional,
            Name("Length"));

        var root = WrapInCompilationUnit(tree);
        var violations = EmittedTreePrecedence.Violations(root);

        violations.Should().ContainSingle(v =>
            v.Slot == OperandSlot.Receiver &&
            v.ChildKind == SyntaxKind.ConditionalExpression);
    }

    [Fact]
    [Trait("Category", "Infrastructure")]
    public void ConditionalUnderMemberAccessGreaterThan_Flagged()
    {
        // (a ? b : c).Length > 0 — the #1727 shape
        var conditional = ConditionalExpression(Name("a"), Name("b"), Name("c"));
        var memberAccess = MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            conditional,
            Name("Length"));
        var tree = BinaryExpression(
            SyntaxKind.GreaterThanExpression,
            memberAccess,
            LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(0)));

        var root = WrapInCompilationUnit(tree);
        var violations = EmittedTreePrecedence.Violations(root);

        violations.Should().Contain(v =>
            v.Slot == OperandSlot.Receiver &&
            v.ChildKind == SyntaxKind.ConditionalExpression);
    }

    [Fact]
    [Trait("Category", "Infrastructure")]
    public void BinaryUnderMemberAccess_Flagged()
    {
        // (a + b).Length — without parens
        var binary = BinaryExpression(SyntaxKind.AddExpression, Name("a"), Name("b"));
        var tree = MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            binary,
            Name("Length"));

        var root = WrapInCompilationUnit(tree);
        var violations = EmittedTreePrecedence.Violations(root);

        violations.Should().ContainSingle(v =>
            v.Slot == OperandSlot.Receiver &&
            v.ChildKind == SyntaxKind.AddExpression);
    }

    [Fact]
    [Trait("Category", "Infrastructure")]
    public void ConditionalUnderNotEquals_Flagged()
    {
        // (a ? b : c) != null — without parens
        var conditional = ConditionalExpression(Name("a"), Name("b"), Name("c"));
        var tree = BinaryExpression(
            SyntaxKind.NotEqualsExpression,
            conditional,
            LiteralExpression(SyntaxKind.NullLiteralExpression));

        var root = WrapInCompilationUnit(tree);
        var violations = EmittedTreePrecedence.Violations(root);

        violations.Should().ContainSingle(v =>
            v.Slot == OperandSlot.Left &&
            v.ChildKind == SyntaxKind.ConditionalExpression);
    }

    [Fact]
    [Trait("Category", "Infrastructure")]
    public void CoalesceUnderMemberAccess_Flagged()
    {
        // (a ?? b).Length — without parens
        var coalesce = BinaryExpression(SyntaxKind.CoalesceExpression, Name("a"), Name("b"));
        var tree = MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            coalesce,
            Name("Length"));

        var root = WrapInCompilationUnit(tree);
        var violations = EmittedTreePrecedence.Violations(root);

        violations.Should().ContainSingle(v =>
            v.Slot == OperandSlot.Receiver &&
            v.ChildKind == SyntaxKind.CoalesceExpression);
    }

    [Fact]
    [Trait("Category", "Infrastructure")]
    public void AssignmentUnderLogicalAnd_Flagged()
    {
        // x = y under &&
        var assignment = AssignmentExpression(
            SyntaxKind.SimpleAssignmentExpression, Name("x"), Name("y"));
        var tree = BinaryExpression(SyntaxKind.LogicalAndExpression, assignment, Name("z"));

        var root = WrapInCompilationUnit(tree);
        var violations = EmittedTreePrecedence.Violations(root);

        violations.Should().Contain(v =>
            v.Slot == OperandSlot.Left &&
            v.ChildKind == SyntaxKind.SimpleAssignmentExpression);
    }

    [Fact]
    [Trait("Category", "Infrastructure")]
    public void LambdaUnderMemberAccess_Flagged()
    {
        // (() => x).Invoke() — lambda as receiver
        var lambda = ParenthesizedLambdaExpression().WithExpressionBody(Name("x"));
        var tree = MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            lambda,
            Name("Invoke"));

        var root = WrapInCompilationUnit(tree);
        var violations = EmittedTreePrecedence.Violations(root);

        violations.Should().ContainSingle(v =>
            v.Slot == OperandSlot.Receiver &&
            v.ChildKind == SyntaxKind.ParenthesizedLambdaExpression);
    }

    [Fact]
    [Trait("Category", "Infrastructure")]
    public void UnaryMinusUnderNonKeywordCast_Flagged()
    {
        // (T)(-x) — §12.9.7 disambiguation rule
        var unaryMinus = PrefixUnaryExpression(SyntaxKind.UnaryMinusExpression, Name("x"));
        var tree = CastExpression(IdentifierName("T"), unaryMinus);

        var root = WrapInCompilationUnit(tree);
        var violations = EmittedTreePrecedence.Violations(root);

        violations.Should().ContainSingle(v =>
            v.Slot == OperandSlot.CastOperand &&
            v.ChildKind == SyntaxKind.UnaryMinusExpression);
    }

    [Fact]
    [Trait("Category", "Infrastructure")]
    public void RightSubtractUnderSubtract_Flagged()
    {
        // a - b - c is (a - b) - c, but building a - (b - c) raw needs parens on right
        var innerSub = BinaryExpression(SyntaxKind.SubtractExpression, Name("b"), Name("c"));
        var tree = BinaryExpression(SyntaxKind.SubtractExpression, Name("a"), innerSub);

        var root = WrapInCompilationUnit(tree);
        var violations = EmittedTreePrecedence.Violations(root);

        violations.Should().ContainSingle(v =>
            v.Slot == OperandSlot.Right &&
            v.ChildKind == SyntaxKind.SubtractExpression);
    }

    [Fact]
    [Trait("Category", "Infrastructure")]
    public void ConditionalUnderCast_Flagged()
    {
        // (int)(a ? b : c) — conditional is prec 2, needs parens for cast (prec 16 required)
        var conditional = ConditionalExpression(Name("a"), Name("b"), Name("c"));
        var tree = CastExpression(PredefinedType(Token(SyntaxKind.IntKeyword)), conditional);

        var root = WrapInCompilationUnit(tree);
        var violations = EmittedTreePrecedence.Violations(root);

        violations.Should().ContainSingle(v =>
            v.Slot == OperandSlot.CastOperand &&
            v.ChildKind == SyntaxKind.ConditionalExpression);
    }

    // --- Sanctioned shapes (Violations empty) ---

    [Fact]
    [Trait("Category", "Infrastructure")]
    public void LeftAssociativeSubtract_Sanctioned()
    {
        // a - b - c (left-assoc, natural grouping)
        var leftSub = BinaryExpression(SyntaxKind.SubtractExpression, Name("a"), Name("b"));
        var tree = BinaryExpression(SyntaxKind.SubtractExpression, leftSub, Name("c"));

        var root = WrapInCompilationUnit(tree);
        EmittedTreePrecedence.Violations(root).Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Infrastructure")]
    public void RightAssociativeCoalesce_Sanctioned()
    {
        // a ?? b ?? c (right-assoc, b ?? c on right is fine)
        var rightCoalesce = BinaryExpression(SyntaxKind.CoalesceExpression, Name("b"), Name("c"));
        var tree = BinaryExpression(SyntaxKind.CoalesceExpression, Name("a"), rightCoalesce);

        var root = WrapInCompilationUnit(tree);
        EmittedTreePrecedence.Violations(root).Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Infrastructure")]
    public void RightAssociativeConditional_Sanctioned()
    {
        // a ? b : c ? d : e (nested in else branch — full expression)
        var innerCond = ConditionalExpression(Name("c"), Name("d"), Name("e"));
        var tree = ConditionalExpression(Name("a"), Name("b"), innerCond);

        var root = WrapInCompilationUnit(tree);
        EmittedTreePrecedence.Violations(root).Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Infrastructure")]
    public void ConditionalInArgument_Sanctioned()
    {
        // f(a ? b : c) — argument is a full expression context
        var conditional = ConditionalExpression(Name("a"), Name("b"), Name("c"));
        var tree = InvocationExpression(
            Name("f"),
            ArgumentList(SingletonSeparatedList(Argument(conditional))));

        var root = WrapInCompilationUnit(tree);
        EmittedTreePrecedence.Violations(root).Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Infrastructure")]
    public void MemberAccessUnderCast_Sanctioned()
    {
        // (T)x.y — member access is primary (17), higher than cast requirement (16)
        var memberAccess = MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            Name("x"),
            Name("y"));
        var tree = CastExpression(IdentifierName("T"), memberAccess);

        var root = WrapInCompilationUnit(tree);
        EmittedTreePrecedence.Violations(root).Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Infrastructure")]
    public void KeywordCastWithUnaryMinus_Sanctioned()
    {
        // (int)-x — keyword cast, §12.9.7 does not apply
        var unaryMinus = PrefixUnaryExpression(SyntaxKind.UnaryMinusExpression, Name("x"));
        var tree = CastExpression(PredefinedType(Token(SyntaxKind.IntKeyword)), unaryMinus);

        var root = WrapInCompilationUnit(tree);
        EmittedTreePrecedence.Violations(root).Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Infrastructure")]
    public void PrimaryChain_Sanctioned()
    {
        // x.y.z() — all primary, no parens
        var xy = MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            Name("x"),
            Name("y"));
        var xyz = MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            xy,
            Name("z"));
        var tree = InvocationExpression(xyz);

        var root = WrapInCompilationUnit(tree);
        EmittedTreePrecedence.Violations(root).Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Infrastructure")]
    public void NegatedComparison_ParenthesizedSanctioned()
    {
        // !(a < b) — already parenthesized
        var comparison = BinaryExpression(SyntaxKind.LessThanExpression, Name("a"), Name("b"));
        var parenthesized = ParenthesizedExpression(comparison);
        var tree = PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, parenthesized);

        var root = WrapInCompilationUnit(tree);
        EmittedTreePrecedence.Violations(root).Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Infrastructure")]
    public void InterpolationWithParenthesizedConditional_Sanctioned()
    {
        // $"{(a ? b : c)}" — conditional already parenthesized in the hole
        var conditional = ConditionalExpression(Name("a"), Name("b"), Name("c"));
        var parenthesized = ParenthesizedExpression(conditional);
        var interpolation = Interpolation(parenthesized);
        var tree = InterpolatedStringExpression(
            Token(SyntaxKind.InterpolatedStringStartToken),
            SingletonList<InterpolatedStringContentSyntax>(interpolation),
            Token(SyntaxKind.InterpolatedStringEndToken));

        var root = WrapInCompilationUnit(tree);
        EmittedTreePrecedence.Violations(root).Should().BeEmpty();
    }

    // --- Operand() helper ---

    [Fact]
    [Trait("Category", "Infrastructure")]
    public void Operand_WrapsWhenNeeded()
    {
        var conditional = ConditionalExpression(Name("a"), Name("b"), Name("c"));
        var result = EmittedTreePrecedence.Operand(
            conditional,
            SyntaxKind.SimpleMemberAccessExpression,
            OperandSlot.Receiver);

        result.Should().BeOfType<ParenthesizedExpressionSyntax>();
    }

    [Fact]
    [Trait("Category", "Infrastructure")]
    public void Operand_PassesThroughWhenSafe()
    {
        var name = Name("x");
        var result = EmittedTreePrecedence.Operand(
            name,
            SyntaxKind.SimpleMemberAccessExpression,
            OperandSlot.Receiver);

        result.Should().BeSameAs(name);
    }

    // --- Factories: each routes its operand slots through Operand() (#1727 sibling seams) ---

    [Fact]
    [Trait("Category", "Infrastructure")]
    public void Binary_ParenthesizesLowerPrecedenceLeftOperand()
    {
        // Python groups `a & b == c` as (a & b) == c; C# ranks & below ==, so the tree must print it.
        var c = Name("c");
        var tree = EmittedTreePrecedence.Binary(SyntaxKind.EqualsExpression,
            BinaryExpression(SyntaxKind.BitwiseAndExpression, Name("a"), Name("b")), c);

        tree.Left.Should().BeOfType<ParenthesizedExpressionSyntax>();
        tree.Right.IsIncrementallyIdenticalTo(c).Should().BeTrue("a primary operand passes through untouched");
        tree.NormalizeWhitespace().ToFullString().Should().Be("(a & b) == c");
    }

    [Fact]
    [Trait("Category", "Infrastructure")]
    public void Member_ParenthesizesCompositeReceiver()
    {
        var sum = BinaryExpression(SyntaxKind.AddExpression, Name("xs"), Name("ys"));
        EmittedTreePrecedence.Member(sum, "Contains").Expression.Should().BeOfType<ParenthesizedExpressionSyntax>();
        var xs = Name("xs");
        EmittedTreePrecedence.Member(xs, "Contains").Expression.IsIncrementallyIdenticalTo(xs).Should().BeTrue("an identifier receiver passes through untouched");
        EmittedTreePrecedence.Member(InvocationExpression(Name("f")), "Count").Expression.Should().BeOfType<InvocationExpressionSyntax>();
    }

    [Fact]
    [Trait("Category", "Infrastructure")]
    public void Cast_ParenthesizesBinaryOperand_AndUnaryMinusUnderAmbiguousTypeOnly()
    {
        var sum = BinaryExpression(SyntaxKind.AddExpression, Name("n"), Name("m"));
        EmittedTreePrecedence.Cast(PredefinedType(Token(SyntaxKind.LongKeyword)), sum)
            .Expression.Should().BeOfType<ParenthesizedExpressionSyntax>();

        var negative = PrefixUnaryExpression(SyntaxKind.UnaryMinusExpression, Name("x"));
        EmittedTreePrecedence.Cast(IdentifierName("Foo"), negative)
            .Expression.Should().BeOfType<ParenthesizedExpressionSyntax>("(Foo)-x reads as subtraction");
        EmittedTreePrecedence.Cast(PredefinedType(Token(SyntaxKind.IntKeyword)), negative)
            .Expression.IsIncrementallyIdenticalTo(negative).Should().BeTrue("(int)-x is a cast");
        EmittedTreePrecedence.Cast(NullableType(PredefinedType(Token(SyntaxKind.IntKeyword))), negative)
            .Expression.IsIncrementallyIdenticalTo(negative).Should().BeTrue("(int?)-x is a cast");
        EmittedTreePrecedence.Cast(QualifiedName(AliasQualifiedName(IdentifierName(Token(SyntaxKind.GlobalKeyword)), IdentifierName("Sharpy")), IdentifierName("Foo")), negative)
            .Expression.Should().BeOfType<ParenthesizedExpressionSyntax>("(global::Sharpy.Foo)-x reads as subtraction (Roslyn ScanCast: names are ambiguous, only predefined/nullable/array/pointer/tuple are not)");
    }

    [Fact]
    [Trait("Category", "Infrastructure")]
    public void PrefixAndPostfix_ParenthesizeCompositeOperands()
    {
        var conditional = ConditionalExpression(Name("a"), Name("b"), Name("c"));
        var comparison = BinaryExpression(SyntaxKind.EqualsExpression, Name("a"), Name("b"));

        EmittedTreePrecedence.Prefix(SyntaxKind.LogicalNotExpression, comparison)
            .Operand.Should().BeOfType<ParenthesizedExpressionSyntax>();
        EmittedTreePrecedence.Prefix(SyntaxKind.LogicalNotExpression, InvocationExpression(Name("f")))
            .Operand.Should().BeOfType<InvocationExpressionSyntax>();
        EmittedTreePrecedence.Postfix(SyntaxKind.SuppressNullableWarningExpression, conditional)
            .Operand.Should().BeOfType<ParenthesizedExpressionSyntax>();
        EmittedTreePrecedence.Postfix(SyntaxKind.SuppressNullableWarningExpression, Name("x"))
            .Operand.Should().BeOfType<IdentifierNameSyntax>();
    }

    [Fact]
    [Trait("Category", "Infrastructure")]
    public void ConditionalIsPatternAwaitElementAndConditionalAccess_ParenthesizeCompositeOperands()
    {
        var conditional = ConditionalExpression(Name("a"), Name("b"), Name("c"));
        var sum = BinaryExpression(SyntaxKind.AddExpression, Name("a"), Name("b"));

        EmittedTreePrecedence.Conditional(conditional, Name("x"), Name("y"))
            .Condition.Should().BeOfType<ParenthesizedExpressionSyntax>();
        EmittedTreePrecedence.Conditional(sum, Name("x"), Name("y"))
            .Condition.IsIncrementallyIdenticalTo(sum).Should().BeTrue("a binary test binds tighter than ?:");
        EmittedTreePrecedence.IsPattern(conditional, ConstantPattern(LiteralExpression(SyntaxKind.NullLiteralExpression)))
            .Expression.Should().BeOfType<ParenthesizedExpressionSyntax>();
        EmittedTreePrecedence.IsPattern(sum, ConstantPattern(LiteralExpression(SyntaxKind.NullLiteralExpression)))
            .Expression.IsIncrementallyIdenticalTo(sum).Should().BeTrue("additive binds tighter than `is`");
        EmittedTreePrecedence.Await(conditional).Expression.Should().BeOfType<ParenthesizedExpressionSyntax>();
        EmittedTreePrecedence.Element(sum).Expression.Should().BeOfType<ParenthesizedExpressionSyntax>();
        EmittedTreePrecedence.ConditionalAccess(conditional, MemberBindingExpression(IdentifierName("m")))
            .Expression.Should().BeOfType<ParenthesizedExpressionSyntax>();
    }

    // --- Violations walkers added for the postfix and switch-governing slots ---

    [Fact]
    [Trait("Category", "Infrastructure")]
    public void ConditionalUnderPostfixNullForgiving_Flagged()
    {
        var tree = PostfixUnaryExpression(SyntaxKind.SuppressNullableWarningExpression,
            ConditionalExpression(Name("a"), Name("b"), Name("c")));

        var violations = EmittedTreePrecedence.Violations(WrapInCompilationUnit(tree));

        violations.Should().ContainSingle()
            .Which.Slot.Should().Be(OperandSlot.PostfixOperand);
    }

    [Fact]
    [Trait("Category", "Infrastructure")]
    public void ConditionalUnderSwitchGoverning_Flagged()
    {
        var tree = SwitchExpression(ConditionalExpression(Name("a"), Name("b"), Name("c")))
            .WithArms(SingletonSeparatedList(SwitchExpressionArm(DiscardPattern(), Name("d"))));

        var violations = EmittedTreePrecedence.Violations(WrapInCompilationUnit(tree));

        violations.Should().ContainSingle()
            .Which.Slot.Should().Be(OperandSlot.SwitchGoverning);
    }

    // --- PrecedenceOf ---

    [Theory]
    [Trait("Category", "Infrastructure")]
    [InlineData(SyntaxKind.SimpleAssignmentExpression, 1)]
    [InlineData(SyntaxKind.ConditionalExpression, 2)]
    [InlineData(SyntaxKind.CoalesceExpression, 3)]
    [InlineData(SyntaxKind.LogicalOrExpression, 4)]
    [InlineData(SyntaxKind.LogicalAndExpression, 5)]
    [InlineData(SyntaxKind.EqualsExpression, 9)]
    [InlineData(SyntaxKind.AddExpression, 12)]
    [InlineData(SyntaxKind.MultiplyExpression, 13)]
    public void PrecedenceOf_MatchesExpectedLevels(SyntaxKind kind, int expected)
    {
        var expr = kind switch
        {
            SyntaxKind.SimpleAssignmentExpression =>
                (ExpressionSyntax)AssignmentExpression(kind, Name("x"), Name("y")),
            SyntaxKind.ConditionalExpression =>
                ConditionalExpression(Name("a"), Name("b"), Name("c")),
            SyntaxKind.CoalesceExpression or
            SyntaxKind.LogicalOrExpression or
            SyntaxKind.LogicalAndExpression or
            SyntaxKind.EqualsExpression or
            SyntaxKind.AddExpression or
            SyntaxKind.MultiplyExpression =>
                BinaryExpression(kind, Name("a"), Name("b")),
            _ => throw new ArgumentException($"Unhandled kind: {kind}")
        };

        EmittedTreePrecedence.PrecedenceOf(expr).Should().Be(expected);
    }

    [Fact]
    [Trait("Category", "Infrastructure")]
    public void PrecedenceOf_Primary_ForIdentifier()
    {
        EmittedTreePrecedence.PrecedenceOf(Name("x")).Should().Be(17);
    }

    [Fact]
    [Trait("Category", "Infrastructure")]
    public void PrecedenceOf_Primary_ForInvocation()
    {
        var invocation = InvocationExpression(Name("f"));
        EmittedTreePrecedence.PrecedenceOf(invocation).Should().Be(17);
    }

    // --- Instrument cross-check ---

    [Theory]
    [Trait("Category", "Infrastructure")]
    [MemberData(nameof(CrossCheckCases))]
    public void Violations_EquivalentToRoundTripKindShape(string label, ExpressionSyntax tree, bool expectViolations)
    {
        var root = WrapInCompilationUnit(tree);
        var violations = EmittedTreePrecedence.Violations(root);
        bool hasViolations = violations.Count > 0;

        hasViolations.Should().Be(expectViolations, $"case '{label}' expected violations={expectViolations}");

        var text = tree.NormalizeWhitespace().ToFullString();
        var reparsed = SyntaxFactory.ParseExpression(text);

        bool sameShape = PrecedenceCrossCheck.KindShapeMatches(tree, reparsed);

        if (!hasViolations)
        {
            sameShape.Should().BeTrue($"case '{label}' has no violations but round-trip changed the kind-shape");
        }
        else
        {
            sameShape.Should().BeFalse($"case '{label}' has violations but round-trip preserved the kind-shape");
        }
    }

    public static IEnumerable<object[]> CrossCheckCases()
    {
        // Flagged (violations expected, round-trip changes shape)
        yield return new object[]
        {
            "conditional-under-member-access",
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                ConditionalExpression(IdentifierName("a"), IdentifierName("b"), IdentifierName("c")),
                IdentifierName("Length")),
            true
        };

        yield return new object[]
        {
            "binary-add-under-member-access",
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                BinaryExpression(SyntaxKind.AddExpression, IdentifierName("a"), IdentifierName("b")),
                IdentifierName("Length")),
            true
        };

        yield return new object[]
        {
            "conditional-under-not-equals",
            BinaryExpression(
                SyntaxKind.NotEqualsExpression,
                ConditionalExpression(IdentifierName("a"), IdentifierName("b"), IdentifierName("c")),
                LiteralExpression(SyntaxKind.NullLiteralExpression)),
            true
        };

        yield return new object[]
        {
            "right-subtract-under-subtract",
            BinaryExpression(
                SyntaxKind.SubtractExpression,
                IdentifierName("a"),
                BinaryExpression(SyntaxKind.SubtractExpression, IdentifierName("b"), IdentifierName("c"))),
            true
        };

        // Sanctioned (no violations, round-trip preserves shape)
        yield return new object[]
        {
            "left-assoc-subtract",
            BinaryExpression(
                SyntaxKind.SubtractExpression,
                BinaryExpression(SyntaxKind.SubtractExpression, IdentifierName("a"), IdentifierName("b")),
                IdentifierName("c")),
            false
        };

        yield return new object[]
        {
            "right-assoc-coalesce",
            BinaryExpression(
                SyntaxKind.CoalesceExpression,
                IdentifierName("a"),
                BinaryExpression(SyntaxKind.CoalesceExpression, IdentifierName("b"), IdentifierName("c"))),
            false
        };

        yield return new object[]
        {
            "primary-chain",
            InvocationExpression(
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        IdentifierName("x"),
                        IdentifierName("y")),
                    IdentifierName("z"))),
            false
        };

        yield return new object[]
        {
            "parenthesized-negation",
            PrefixUnaryExpression(
                SyntaxKind.LogicalNotExpression,
                ParenthesizedExpression(
                    BinaryExpression(SyntaxKind.LessThanExpression, IdentifierName("a"), IdentifierName("b")))),
            false
        };

        // Bitwise-vs-equality: Python groups `a & b == c` as (a & b) == c, C# as a & (b == c).
        yield return new object[]
        {
            "bitwise-and-under-equals",
            BinaryExpression(
                SyntaxKind.EqualsExpression,
                BinaryExpression(SyntaxKind.BitwiseAndExpression, IdentifierName("a"), IdentifierName("b")),
                IdentifierName("c")),
            true
        };

        // Cast disambiguation (§12.9.7 / Roslyn ScanCast): which cast types are "must be a type".
        yield return new object[]
        {
            "unary-minus-under-plain-name-cast",
            CastExpression(IdentifierName("Foo"), PrefixUnaryExpression(SyntaxKind.UnaryMinusExpression, IdentifierName("x"))),
            true
        };
        yield return new object[]
        {
            "unary-minus-under-nullable-int-cast",
            CastExpression(NullableType(PredefinedType(Token(SyntaxKind.IntKeyword))),
                PrefixUnaryExpression(SyntaxKind.UnaryMinusExpression, LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(1)))),
            false
        };
        yield return new object[]
        {
            "unary-minus-under-nullable-name-cast",
            CastExpression(NullableType(IdentifierName("Foo")),
                PrefixUnaryExpression(SyntaxKind.UnaryMinusExpression, IdentifierName("x"))),
            false
        };
        yield return new object[]
        {
            "unary-minus-under-array-cast",
            CastExpression(ArrayType(PredefinedType(Token(SyntaxKind.IntKeyword)))
                    .WithRankSpecifiers(SingletonList(ArrayRankSpecifier(SingletonSeparatedList<ExpressionSyntax>(OmittedArraySizeExpression())))),
                PrefixUnaryExpression(SyntaxKind.UnaryMinusExpression, IdentifierName("x"))),
            false
        };
        yield return new object[]
        {
            // Roslyn refuted the first draft's assumption that an alias-qualified name is "must be a
            // type": `(global::Sharpy.Foo)-x` reparses as a subtraction, like every other name form.
            "unary-minus-under-alias-qualified-cast",
            CastExpression(QualifiedName(AliasQualifiedName(IdentifierName(Token(SyntaxKind.GlobalKeyword)), IdentifierName("Sharpy")), IdentifierName("Foo")),
                PrefixUnaryExpression(SyntaxKind.UnaryMinusExpression, IdentifierName("x"))),
            true
        };
        yield return new object[]
        {
            "unary-minus-under-qualified-name-cast",
            CastExpression(QualifiedName(IdentifierName("A"), IdentifierName("B")),
                PrefixUnaryExpression(SyntaxKind.UnaryMinusExpression, IdentifierName("x"))),
            true
        };

        // Postfix and switch-governing slots.
        yield return new object[]
        {
            "conditional-under-null-forgiving",
            PostfixUnaryExpression(SyntaxKind.SuppressNullableWarningExpression,
                ConditionalExpression(IdentifierName("a"), IdentifierName("b"), IdentifierName("c"))),
            true
        };
        yield return new object[]
        {
            "conditional-under-switch-governing",
            SwitchExpression(ConditionalExpression(IdentifierName("a"), IdentifierName("b"), IdentifierName("c")))
                .WithArms(SingletonSeparatedList(SwitchExpressionArm(DiscardPattern(), IdentifierName("d")))),
            true
        };
    }

    private static SyntaxNode WrapInCompilationUnit(ExpressionSyntax expr)
    {
        return CompilationUnit()
            .WithMembers(SingletonList<MemberDeclarationSyntax>(
                GlobalStatement(ExpressionStatement(expr))));
    }
}
