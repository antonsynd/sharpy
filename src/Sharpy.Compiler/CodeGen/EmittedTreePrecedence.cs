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

public static class EmittedTreePrecedence
{
    public static int PrecedenceOf(ExpressionSyntax expr) => expr.Kind() switch
    {
        // 1: Assignment, lambda, throw (right-assoc)
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

        // 2: Conditional (right-assoc)
        SyntaxKind.ConditionalExpression => 2,

        // 3: Null-coalescing (right-assoc)
        SyntaxKind.CoalesceExpression => 3,

        // 4: Conditional OR
        SyntaxKind.LogicalOrExpression => 4,

        // 5: Conditional AND
        SyntaxKind.LogicalAndExpression => 5,

        // 6: Logical OR
        SyntaxKind.BitwiseOrExpression => 6,

        // 7: Logical XOR
        SyntaxKind.ExclusiveOrExpression => 7,

        // 8: Logical AND
        SyntaxKind.BitwiseAndExpression => 8,

        // 9: Equality
        SyntaxKind.EqualsExpression or
        SyntaxKind.NotEqualsExpression
            => 9,

        // 10: Relational / type-testing
        SyntaxKind.LessThanExpression or
        SyntaxKind.GreaterThanExpression or
        SyntaxKind.LessThanOrEqualExpression or
        SyntaxKind.GreaterThanOrEqualExpression or
        SyntaxKind.IsExpression or
        SyntaxKind.IsPatternExpression or
        SyntaxKind.AsExpression
            => 10,

        // 11: Shift
        SyntaxKind.LeftShiftExpression or
        SyntaxKind.RightShiftExpression or
        SyntaxKind.UnsignedRightShiftExpression
            => 11,

        // 12: Additive
        SyntaxKind.AddExpression or
        SyntaxKind.SubtractExpression
            => 12,

        // 13: Multiplicative
        SyntaxKind.MultiplyExpression or
        SyntaxKind.DivideExpression or
        SyntaxKind.ModuloExpression
            => 13,

        // 14: Switch / with
        SyntaxKind.SwitchExpression or
        SyntaxKind.WithExpression
            => 14,

        // 15: Range
        SyntaxKind.RangeExpression => 15,

        // 16: Unary
        SyntaxKind.UnaryPlusExpression or
        SyntaxKind.UnaryMinusExpression or
        SyntaxKind.LogicalNotExpression or
        SyntaxKind.BitwiseNotExpression or
        SyntaxKind.PreIncrementExpression or
        SyntaxKind.PreDecrementExpression or
        SyntaxKind.CastExpression or
        SyntaxKind.AwaitExpression or
        SyntaxKind.IndexExpression or
        SyntaxKind.PointerIndirectionExpression or
        SyntaxKind.AddressOfExpression
            => 16,

        // 17: Primary (everything else)
        _ => 17,
    };

    public static bool NeedsParentheses(ExpressionSyntax child, SyntaxKind parentKind, OperandSlot slot)
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
            OperandSlot.CastOperand => NeedsParensCastOperand(child, childPrec, parentKind),
            OperandSlot.PrefixOperand => childPrec < 16,
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

    private static bool NeedsParensCastOperand(ExpressionSyntax child, int childPrec, SyntaxKind parentKind)
    {
        if (childPrec < 16)
            return true;

        // ECMA-334 §12.9.7: non-keyword cast + operand starting with -, +, *, &, ++, --
        if (parentKind == SyntaxKind.CastExpression && !IsKeywordCast(child))
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

    private static bool IsKeywordCast(ExpressionSyntax castOperand)
    {
        if (castOperand.Parent is CastExpressionSyntax cast)
            return cast.Type is PredefinedTypeSyntax;
        return false;
    }

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

    public static ExpressionSyntax Operand(ExpressionSyntax child, SyntaxKind parentKind, OperandSlot slot)
    {
        return NeedsParentheses(child, parentKind, slot)
            ? ParenthesizedExpression(child)
            : child;
    }

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
            CheckAwaitOperand(node, violations);
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
