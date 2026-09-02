using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Sharpy.Compiler.CodeGen;

public enum OperandSlot
{
    Left,
    Right,
    Condition,
    Receiver,
    CastOperand,
    PrefixOperand,
    PostfixOperand,
    AwaitOperand,
    IsPatternSubject,
    SwitchGoverning,
    AssignmentLeft,
    InterpolationHole,
    NullCoalescingRight,
    ConditionalBranch,
    LambdaBody,
    ArgumentOrInitializer,
}

/// <summary>
/// The emitter's one precedence seam (#1727, #1712). The tree the emitter builds and the text it
/// prints must be the same program: the default compile path reparses the printed C#, so an
/// operand whose C# precedence is lower than its parent operator's must be a
/// <see cref="ParenthesizedExpressionSyntax"/> or the reparse re-associates it
/// (<c>cond ? a : b.Length</c> for a tree that means <c>(cond ? a : b).Length</c>).
/// <list type="bullet">
/// <item><description><see cref="Operand"/> decides, from C# <see cref="SyntaxKind"/> facts only
/// (ECMA-334 §12.4.2 plus Roslyn's cast disambiguation), whether a child needs parentheses in a
/// given slot of a given parent, and wraps it when it does.</description></item>
/// <item><description>The factory wrappers (<see cref="Binary"/>, <see cref="Member"/>,
/// <see cref="Cast"/>, <see cref="Prefix"/>, <see cref="Postfix"/>, <see cref="Conditional"/>,
/// <see cref="IsPattern"/>, <see cref="Await"/>, <see cref="Element"/>,
/// <see cref="ConditionalAccess"/>) build the same node the raw <c>SyntaxFactory</c> call would
/// and route every operand slot through <see cref="Operand"/>. Emitter code that wraps a
/// <em>generated</em> expression builds it through them; a literal, identifier or invocation
/// operand is primary and passes through unchanged, so the wrappers change nothing for the
/// shapes the corpus already emitted.</description></item>
/// <item><description><see cref="Violations"/> is the structural invariant over a built tree:
/// every parent–child edge where parentheses are required and absent. It runs on every
/// CodeGen unit-test emission (<c>EmitterTestPipeline</c>), over both corpus arms of
/// <c>ReparseEquivalenceConformanceTests</c>, and in production as SPY0524
/// (<c>CompilerInvariants.AssertEmittedTreePrecedence</c>), so a site that skips the seam is
/// named by the compiler rather than by the C# compiler behind SPY0908.</description></item>
/// </list>
/// Rule 2 (CLAUDE.md): nothing here reads a Sharpy type or <c>SemanticInfo</c>; the decision is a
/// property of the C# tree alone.
/// </summary>
public static class EmittedTreePrecedence
{
    /// <summary>ECMA-334 §12.4.2 precedence level of <paramref name="expr"/>: 1 = assignment/lambda/throw … 17 = primary.</summary>
    public static int PrecedenceOf(ExpressionSyntax expr) => PrecedenceOfKind(expr.Kind());

    /// <summary>
    /// Whether <paramref name="child"/> must be parenthesized in <paramref name="slot"/> of a
    /// <paramref name="parentKind"/> node. For <see cref="OperandSlot.CastOperand"/>, pass the cast's
    /// <paramref name="castType"/> when the child is not yet attached to its cast (the factory path);
    /// when it is attached (the <see cref="Violations"/> walk) the type is read off the parent.
    /// </summary>
    public static bool NeedsParentheses(ExpressionSyntax child, SyntaxKind parentKind, OperandSlot slot, TypeSyntax? castType = null)
    {
        int childPrec = PrecedenceOf(child);

        if (slot == OperandSlot.ArgumentOrInitializer)
            return false;

        if (slot == OperandSlot.ConditionalBranch)
            return false;

        if (slot == OperandSlot.LambdaBody)
            return false;

        if (slot == OperandSlot.InterpolationHole)
            return child is ConditionalExpressionSyntax;

        // Lambda/assignment/throw in most operand slots needs parens
        if (childPrec == 1 &&
            slot != OperandSlot.NullCoalescingRight &&
            slot != OperandSlot.AssignmentLeft)
        {
            return true;
        }

        return slot switch
        {
            OperandSlot.Left => NeedsParensLeftOperand(childPrec, parentKind),
            OperandSlot.Right => NeedsParensRightOperand(childPrec, parentKind),
            OperandSlot.Condition => childPrec <= 2,
            OperandSlot.Receiver => childPrec < 17,
            OperandSlot.CastOperand => NeedsParensCastOperand(child, childPrec, parentKind, castType),
            OperandSlot.PrefixOperand => childPrec < 16,
            OperandSlot.PostfixOperand => childPrec < 17,
            OperandSlot.AwaitOperand => childPrec < 16,
            OperandSlot.IsPatternSubject => childPrec < 10,
            OperandSlot.SwitchGoverning => childPrec < 15,
            OperandSlot.AssignmentLeft => childPrec < 16,
            OperandSlot.NullCoalescingRight => NeedsParensRightOperand(childPrec, parentKind),
            _ => false,
        };
    }

    private static bool NeedsParensLeftOperand(int childPrec, SyntaxKind parentKind)
    {
        int parentPrec = PrecedenceOfKind(parentKind);
        return IsRightAssociative(parentKind)
            ? childPrec <= parentPrec
            : childPrec < parentPrec;
    }

    private static bool NeedsParensRightOperand(int childPrec, SyntaxKind parentKind)
    {
        int parentPrec = PrecedenceOfKind(parentKind);
        return IsRightAssociative(parentKind)
            ? childPrec < parentPrec
            : childPrec <= parentPrec;
    }

    private static bool NeedsParensCastOperand(ExpressionSyntax child, int childPrec, SyntaxKind parentKind, TypeSyntax? castType)
    {
        if (childPrec < 16)
            return true;

        // ECMA-334 §12.9.7: an ambiguous cast type + operand starting with -, +, *, &, ++, --
        bool unambiguous = castType != null ? IsUnambiguousCastType(castType) : IsUnambiguousCast(child);
        if (parentKind == SyntaxKind.CastExpression && !unambiguous)
        {
            var childKind = child.Kind();
            if (childKind is SyntaxKind.UnaryMinusExpression or
                SyntaxKind.UnaryPlusExpression or
                SyntaxKind.PointerIndirectionExpression or
                SyntaxKind.AddressOfExpression or
                SyntaxKind.PreIncrementExpression or
                SyntaxKind.PreDecrementExpression)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsUnambiguousCast(ExpressionSyntax castOperand)
    {
        if (castOperand.Parent is CastExpressionSyntax cast)
            return IsUnambiguousCastType(cast.Type);
        return false;
    }

    /// <summary>
    /// §12.9.7's disambiguation applies only where the parenthesized tokens could also be an
    /// expression. Roslyn's <c>ScanCast</c> treats a predefined type and a nullable, array, pointer
    /// or tuple type as "must be a type", so <c>(int?)-1</c> and <c>(T[])-x</c> are casts whatever
    /// follows; every name form — plain, generic, qualified and alias-qualified (<c>(Foo)-x</c>,
    /// <c>(A.B)-x</c>, <c>(global::A.B)-x</c>) — is read as a parenthesized expression when a
    /// sign-like token follows. Each of these is verified against <c>ParseExpression</c> by the
    /// cross-check theory in <c>EmittedTreePrecedenceTests</c>; the alias-qualified row is there
    /// because the first draft of this table assumed the opposite and the theory refuted it.
    /// </summary>
    private static bool IsUnambiguousCastType(TypeSyntax type) => type switch
    {
        PredefinedTypeSyntax => true,
        NullableTypeSyntax => true,
        ArrayTypeSyntax => true,
        PointerTypeSyntax => true,
        TupleTypeSyntax => true,
        _ => false,
    };

    private static bool IsRightAssociative(SyntaxKind kind) => kind is
        SyntaxKind.CoalesceExpression or
        SyntaxKind.CoalesceAssignmentExpression or
        SyntaxKind.SimpleAssignmentExpression or
        SyntaxKind.AddAssignmentExpression or
        SyntaxKind.SubtractAssignmentExpression or
        SyntaxKind.MultiplyAssignmentExpression or
        SyntaxKind.DivideAssignmentExpression or
        SyntaxKind.ModuloAssignmentExpression or
        SyntaxKind.AndAssignmentExpression or
        SyntaxKind.OrAssignmentExpression or
        SyntaxKind.ExclusiveOrAssignmentExpression or
        SyntaxKind.LeftShiftAssignmentExpression or
        SyntaxKind.RightShiftAssignmentExpression or
        SyntaxKind.UnsignedRightShiftAssignmentExpression or
        SyntaxKind.ConditionalExpression;

    /// <summary>
    /// The one precedence table (ECMA-334 §12.4.2). <see cref="PrecedenceOf"/> reads the child's level and
    /// the slot rules read the parent's level from this same switch, so an edit cannot make the rule
    /// asymmetric. 1: assignment, lambda, throw (right-assoc) · 2: conditional (right-assoc) ·
    /// 3: null-coalescing (right-assoc) · 4: || · 5: &amp;&amp; · 6: | · 7: ^ · 8: &amp; · 9: equality ·
    /// 10: relational / type-testing · 11: shift · 12: additive · 13: multiplicative · 14: switch / with ·
    /// 15: range · 16: unary (incl. cast, await) · 17: primary (everything else).
    /// </summary>
    private static int PrecedenceOfKind(SyntaxKind kind) => kind switch
    {
        SyntaxKind.SimpleAssignmentExpression or
        SyntaxKind.AddAssignmentExpression or
        SyntaxKind.SubtractAssignmentExpression or
        SyntaxKind.MultiplyAssignmentExpression or
        SyntaxKind.DivideAssignmentExpression or
        SyntaxKind.ModuloAssignmentExpression or
        SyntaxKind.AndAssignmentExpression or
        SyntaxKind.OrAssignmentExpression or
        SyntaxKind.ExclusiveOrAssignmentExpression or
        SyntaxKind.LeftShiftAssignmentExpression or
        SyntaxKind.RightShiftAssignmentExpression or
        SyntaxKind.UnsignedRightShiftAssignmentExpression or
        SyntaxKind.CoalesceAssignmentExpression or
        SyntaxKind.SimpleLambdaExpression or
        SyntaxKind.ParenthesizedLambdaExpression or
        SyntaxKind.ThrowExpression
            => 1,
        SyntaxKind.ConditionalExpression => 2,
        SyntaxKind.CoalesceExpression => 3,
        SyntaxKind.LogicalOrExpression => 4,
        SyntaxKind.LogicalAndExpression => 5,
        SyntaxKind.BitwiseOrExpression => 6,
        SyntaxKind.ExclusiveOrExpression => 7,
        SyntaxKind.BitwiseAndExpression => 8,
        SyntaxKind.EqualsExpression or SyntaxKind.NotEqualsExpression => 9,
        SyntaxKind.LessThanExpression or SyntaxKind.GreaterThanExpression or
        SyntaxKind.LessThanOrEqualExpression or SyntaxKind.GreaterThanOrEqualExpression or
        SyntaxKind.IsExpression or SyntaxKind.IsPatternExpression or SyntaxKind.AsExpression
            => 10,
        SyntaxKind.LeftShiftExpression or SyntaxKind.RightShiftExpression or
        SyntaxKind.UnsignedRightShiftExpression
            => 11,
        SyntaxKind.AddExpression or SyntaxKind.SubtractExpression => 12,
        SyntaxKind.MultiplyExpression or SyntaxKind.DivideExpression or
        SyntaxKind.ModuloExpression => 13,
        SyntaxKind.SwitchExpression or SyntaxKind.WithExpression => 14,
        SyntaxKind.RangeExpression => 15,
        SyntaxKind.CastExpression or SyntaxKind.AwaitExpression or
        SyntaxKind.UnaryPlusExpression or SyntaxKind.UnaryMinusExpression or
        SyntaxKind.LogicalNotExpression or SyntaxKind.BitwiseNotExpression or
        SyntaxKind.PreIncrementExpression or SyntaxKind.PreDecrementExpression or
        SyntaxKind.IndexExpression or SyntaxKind.PointerIndirectionExpression or
        SyntaxKind.AddressOfExpression
            => 16,
        _ => 17,
    };

    /// <summary>
    /// <paramref name="child"/> as it must appear in <paramref name="slot"/> of a
    /// <paramref name="parentKind"/> node: itself when its precedence allows, otherwise wrapped in a
    /// <see cref="ParenthesizedExpressionSyntax"/>. The single seam every generated operand passes
    /// through (#1727, #1712).
    /// </summary>
    public static ExpressionSyntax Operand(ExpressionSyntax child, SyntaxKind parentKind, OperandSlot slot, TypeSyntax? castType = null)
    {
        return NeedsParentheses(child, parentKind, slot, castType)
            ? ParenthesizedExpression(child)
            : child;
    }

    // ---------------------------------------------------------------------------------------------
    // Precedence-aware factories. Each builds exactly the node its SyntaxFactory namesake would and
    // routes every operand slot through Operand(); primary operands pass through untouched.
    // ---------------------------------------------------------------------------------------------

    /// <summary><c>left op right</c> with both operands placed per the operator's precedence and associativity.</summary>
    public static BinaryExpressionSyntax Binary(SyntaxKind kind, ExpressionSyntax left, ExpressionSyntax right)
        => BinaryExpression(kind,
            Operand(left, kind, OperandSlot.Left),
            Operand(right, kind, kind == SyntaxKind.CoalesceExpression ? OperandSlot.NullCoalescingRight : OperandSlot.Right));

    /// <summary><c>receiver.name</c> — the receiver must be primary.</summary>
    public static MemberAccessExpressionSyntax Member(ExpressionSyntax receiver, SimpleNameSyntax name)
        => MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
            Operand(receiver, SyntaxKind.SimpleMemberAccessExpression, OperandSlot.Receiver), name);

    /// <summary><c>receiver.name</c> — the receiver must be primary.</summary>
    public static MemberAccessExpressionSyntax Member(ExpressionSyntax receiver, string name)
        => Member(receiver, IdentifierName(name));

    /// <summary><c>(type)operand</c> — the operand must be unary or tighter, and a non-keyword cast must not be followed by a sign-like token (§12.9.7).</summary>
    public static CastExpressionSyntax Cast(TypeSyntax type, ExpressionSyntax operand)
        => CastExpression(type, Operand(operand, SyntaxKind.CastExpression, OperandSlot.CastOperand, type));

    /// <summary><c>op operand</c> for a prefix operator — the operand must be unary or tighter.</summary>
    public static PrefixUnaryExpressionSyntax Prefix(SyntaxKind kind, ExpressionSyntax operand)
        => PrefixUnaryExpression(kind, Operand(operand, kind, OperandSlot.PrefixOperand));

    /// <summary><c>operand op</c> for a postfix operator (<c>!</c>, <c>++</c>, <c>--</c>) — the operand must be primary.</summary>
    public static PostfixUnaryExpressionSyntax Postfix(SyntaxKind kind, ExpressionSyntax operand)
        => PostfixUnaryExpression(kind, Operand(operand, kind, OperandSlot.PostfixOperand));

    /// <summary><c>condition ? whenTrue : whenFalse</c> — the condition must bind tighter than a conditional; the branches are full expressions.</summary>
    public static ConditionalExpressionSyntax Conditional(ExpressionSyntax condition, ExpressionSyntax whenTrue, ExpressionSyntax whenFalse)
        => ConditionalExpression(Operand(condition, SyntaxKind.ConditionalExpression, OperandSlot.Condition), whenTrue, whenFalse);

    /// <summary><c>subject is pattern</c> — the subject must be relational or tighter.</summary>
    public static IsPatternExpressionSyntax IsPattern(ExpressionSyntax subject, PatternSyntax pattern)
        => IsPatternExpression(Operand(subject, SyntaxKind.IsPatternExpression, OperandSlot.IsPatternSubject), pattern);

    /// <summary><c>await operand</c> — the operand must be unary or tighter.</summary>
    public static AwaitExpressionSyntax Await(ExpressionSyntax operand)
        => AwaitExpression(Operand(operand, SyntaxKind.AwaitExpression, OperandSlot.AwaitOperand));

    /// <summary><c>receiver[...]</c> — the receiver must be primary; the argument list is added by the caller.</summary>
    public static ElementAccessExpressionSyntax Element(ExpressionSyntax receiver)
        => ElementAccessExpression(Operand(receiver, SyntaxKind.ElementAccessExpression, OperandSlot.Receiver));

    /// <summary><c>receiver?.whenNotNull</c> — the receiver must be primary.</summary>
    public static ConditionalAccessExpressionSyntax ConditionalAccess(ExpressionSyntax receiver, ExpressionSyntax whenNotNull)
        => ConditionalAccessExpression(Operand(receiver, SyntaxKind.ConditionalAccessExpression, OperandSlot.Receiver), whenNotNull);

    public record Violation(SyntaxKind ParentKind, SyntaxKind ChildKind, OperandSlot Slot, string Text, int Line);

    public static IReadOnlyList<Violation> Violations(SyntaxNode root)
    {
        var violations = new List<Violation>();

        foreach (var node in root.DescendantNodesAndSelf().OfType<ExpressionSyntax>())
        {
            CheckBinaryExpression(node, violations);
            CheckConditionalExpression(node, violations);
            CheckMemberAccessReceiver(node, violations);
            CheckInvocationReceiver(node, violations);
            CheckElementAccessReceiver(node, violations);
            CheckConditionalAccessReceiver(node, violations);
            CheckCastOperand(node, violations);
            CheckPrefixUnaryOperand(node, violations);
            CheckPostfixUnaryOperand(node, violations);
            CheckAwaitOperand(node, violations);
            CheckSwitchGoverning(node, violations);
            CheckIsPatternSubject(node, violations);
            CheckAssignmentLeft(node, violations);
            CheckInterpolationHole(node, violations);
        }

        return violations;
    }

    private static void CheckBinaryExpression(ExpressionSyntax node, List<Violation> violations)
    {
        if (node is not BinaryExpressionSyntax binary)
            return;

        var parentKind = binary.Kind();
        CheckChild(binary.Left, parentKind, OperandSlot.Left, violations);

        var rightSlot = parentKind == SyntaxKind.CoalesceExpression
            ? OperandSlot.NullCoalescingRight
            : OperandSlot.Right;
        CheckChild(binary.Right, parentKind, rightSlot, violations);
    }

    private static void CheckConditionalExpression(ExpressionSyntax node, List<Violation> violations)
    {
        if (node is not ConditionalExpressionSyntax conditional)
            return;

        CheckChild(conditional.Condition, SyntaxKind.ConditionalExpression, OperandSlot.Condition, violations);
        // Then/else are full expression slots — never violations.
    }

    private static void CheckMemberAccessReceiver(ExpressionSyntax node, List<Violation> violations)
    {
        if (node is not MemberAccessExpressionSyntax memberAccess)
            return;
        if (memberAccess.Kind() != SyntaxKind.SimpleMemberAccessExpression)
            return;

        CheckChild(memberAccess.Expression, SyntaxKind.SimpleMemberAccessExpression, OperandSlot.Receiver, violations);
    }

    private static void CheckInvocationReceiver(ExpressionSyntax node, List<Violation> violations)
    {
        if (node is not InvocationExpressionSyntax invocation)
            return;

        if (invocation.Expression is MemberAccessExpressionSyntax)
            return; // the member access itself handles its receiver

        CheckChild(invocation.Expression, SyntaxKind.InvocationExpression, OperandSlot.Receiver, violations);
    }

    private static void CheckElementAccessReceiver(ExpressionSyntax node, List<Violation> violations)
    {
        if (node is not ElementAccessExpressionSyntax elementAccess)
            return;

        CheckChild(elementAccess.Expression, SyntaxKind.ElementAccessExpression, OperandSlot.Receiver, violations);
    }

    private static void CheckConditionalAccessReceiver(ExpressionSyntax node, List<Violation> violations)
    {
        if (node is not ConditionalAccessExpressionSyntax conditionalAccess)
            return;

        CheckChild(conditionalAccess.Expression, SyntaxKind.ConditionalAccessExpression, OperandSlot.Receiver, violations);
    }

    private static void CheckCastOperand(ExpressionSyntax node, List<Violation> violations)
    {
        if (node is not CastExpressionSyntax cast)
            return;

        CheckChild(cast.Expression, SyntaxKind.CastExpression, OperandSlot.CastOperand, violations);
    }

    private static void CheckPrefixUnaryOperand(ExpressionSyntax node, List<Violation> violations)
    {
        if (node is not PrefixUnaryExpressionSyntax prefix)
            return;

        CheckChild(prefix.Operand, prefix.Kind(), OperandSlot.PrefixOperand, violations);
    }

    private static void CheckPostfixUnaryOperand(ExpressionSyntax node, List<Violation> violations)
    {
        if (node is not PostfixUnaryExpressionSyntax postfix)
            return;

        CheckChild(postfix.Operand, postfix.Kind(), OperandSlot.PostfixOperand, violations);
    }

    private static void CheckSwitchGoverning(ExpressionSyntax node, List<Violation> violations)
    {
        if (node is not SwitchExpressionSyntax switchExpr)
            return;

        CheckChild(switchExpr.GoverningExpression, SyntaxKind.SwitchExpression, OperandSlot.SwitchGoverning, violations);
    }

    private static void CheckAwaitOperand(ExpressionSyntax node, List<Violation> violations)
    {
        if (node is not AwaitExpressionSyntax awaitExpr)
            return;

        CheckChild(awaitExpr.Expression, SyntaxKind.AwaitExpression, OperandSlot.AwaitOperand, violations);
    }

    private static void CheckIsPatternSubject(ExpressionSyntax node, List<Violation> violations)
    {
        if (node is not IsPatternExpressionSyntax isPattern)
            return;

        CheckChild(isPattern.Expression, SyntaxKind.IsPatternExpression, OperandSlot.IsPatternSubject, violations);
    }

    private static void CheckAssignmentLeft(ExpressionSyntax node, List<Violation> violations)
    {
        if (node is not AssignmentExpressionSyntax assignment)
            return;

        CheckChild(assignment.Left, assignment.Kind(), OperandSlot.AssignmentLeft, violations);
        // Assignment RHS is a full expression — never a violation.
    }

    private static void CheckInterpolationHole(ExpressionSyntax node, List<Violation> violations)
    {
        if (node.Parent is not InterpolationSyntax)
            return;

        // Only flag if the node is directly in an interpolation hole (not already parenthesized)
        if (NeedsParentheses(node, SyntaxKind.InterpolatedStringExpression, OperandSlot.InterpolationHole) &&
            node is not ParenthesizedExpressionSyntax)
        {
            var location = node.GetLocation();
            int line = location.GetLineSpan().StartLinePosition.Line + 1;
            violations.Add(new Violation(
                SyntaxKind.InterpolatedStringExpression,
                node.Kind(),
                OperandSlot.InterpolationHole,
                node.ToFullString().Trim(),
                line));
        }
    }

    private static void CheckChild(ExpressionSyntax child, SyntaxKind parentKind, OperandSlot slot, List<Violation> violations)
    {
        if (child is ParenthesizedExpressionSyntax)
            return;

        if (!NeedsParentheses(child, parentKind, slot))
            return;

        var location = child.GetLocation();
        int line = location.GetLineSpan().StartLinePosition.Line + 1;
        violations.Add(new Violation(
            parentKind,
            child.Kind(),
            slot,
            child.ToFullString().Trim(),
            line));
    }
}
