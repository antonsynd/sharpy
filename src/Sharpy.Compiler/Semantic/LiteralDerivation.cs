using System.Collections.Immutable;
using System.Linq;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// PEP 675's literal-derived form set (#1741, #1731, R-P — owner ruling 2026-09-03), computed by ONE
/// rule instead of a per-site check duplicated at each producing seam. A literal-derived expression's
/// TYPE stays <c>str</c> — this is a compile-time PROVENANCE fact, consumed only by the
/// <c>LiteralString</c> store seam (<c>TypeChecker.StoreConversion.ClassifyStore</c>, case 5), never by
/// inference, overload resolution, or display.
///
/// <para>Two BASE cases are recorded at their own sites, not here, because each needs context this
/// helper does not have: a string literal (the literal node itself,
/// <c>TypeChecker.Expressions.Literals.CheckStringLiteral</c>) and a <c>LiteralString</c>-typed
/// identifier read (the resolved symbol's declared type, <c>TypeChecker.Expressions.CheckIdentifier</c>,
/// #1766). Every other form composes from those two through the rules below, which this file
/// centralizes:</para>
/// <list type="bullet">
/// <item>an f-string whose every hole — recursively through its format spec — is derived
/// (<see cref="IsFStringDerived"/>); a zero-hole f-string is derived vacuously;</item>
/// <item><c>derived-str + derived-str</c> (concatenation, folds the #1731 case into this one rule) and
/// <c>derived-str * int</c> / <c>int * derived-str</c> (repetition) — <see cref="IsConcatOrRepeatDerived"/>;</item>
/// <item>a call to a PEP 675 STR-RETURNING method (<see cref="PreservingMethods"/>) on a derived
/// receiver, with every str-typed argument also derived — <see cref="IsPreservingMethodCallDerived"/>.</item>
/// </list>
/// </summary>
internal static class LiteralDerivation
{
    /// <summary>
    /// PEP 675's <c>str</c> methods that preserve <c>LiteralString</c> when the receiver and every
    /// str-typed argument are <c>LiteralString</c> — as a LITERAL LIST, not derived from an enum or a
    /// reflected method table, so a totality check over this set has an independent anchor (a totality
    /// count from the same enum it audits is vacuous). Restricted to the methods whose Sharpy
    /// <c>str</c> return type is itself <c>str</c>: <c>split</c>, <c>rsplit</c>, <c>splitlines</c>,
    /// <c>partition</c>, <c>rpartition</c> return <c>list[str]</c>/tuples, and the fact this file
    /// computes is tracked per str-typed EXPRESSION (R-P: "the expression's type stays str"), so it
    /// structurally cannot attach to a container element — those five are N/A, recorded as such in
    /// <c>literal_string.md</c>'s form table rather than silently dropped.
    /// </summary>
    public static readonly IReadOnlySet<string> PreservingMethods = new HashSet<string>
    {
        "capitalize", "casefold", "center", "expandtabs", "format", "format_map",
        "join", "ljust", "lower", "lstrip", "replace", "rjust", "rstrip",
        "strip", "swapcase", "title", "upper", "zfill",
    };

    /// <summary>
    /// True when every part of an f-string — each hole's expression and, recursively, any expression
    /// inside its format spec (<c>{x:{w}}</c>) — is literal-derived. A plain text part (no expression)
    /// is vacuously derived, so a hole-free f-string is derived too (PEP 675 §f-strings).
    /// </summary>
    public static bool IsFStringDerived(SemanticInfo semanticInfo, ImmutableArray<FStringPart> parts)
    {
        foreach (var part in parts)
        {
            if (!IsPartDerived(semanticInfo, part))
                return false;
        }
        return true;
    }

    private static bool IsPartDerived(SemanticInfo semanticInfo, FStringPart part)
    {
        if (part.Expression != null
            && !semanticInfo.IsLiteralDerived(AstHelper.UnwrapParenthesized(part.Expression)))
        {
            return false;
        }

        if (part.Spec != null)
        {
            foreach (var specPart in part.Spec)
            {
                if (!IsPartDerived(semanticInfo, specPart))
                    return false;
            }
        }

        return true;
    }

    /// <summary>
    /// True for <c>derived-str + derived-str</c> (concatenation — folds the #1731 case into this one
    /// rule) or <c>derived-str * int</c> / <c>int * derived-str</c> (repetition, either operand order;
    /// PEP 675 <c>__mul__</c>/<c>__rmul__</c> — the count need not be a compile-time constant).
    /// <paramref name="leftType"/>/<paramref name="rightType"/> must already be the OperandView-normalized
    /// types (a <c>LiteralString</c> operand views as <c>str</c>, #1766).
    /// </summary>
    public static bool IsConcatOrRepeatDerived(
        SemanticInfo semanticInfo, BinaryOperator op,
        SemanticType leftType, SemanticType rightType,
        Expression left, Expression right)
    {
        var l = AstHelper.UnwrapParenthesized(left);
        var r = AstHelper.UnwrapParenthesized(right);

        if (op == BinaryOperator.Add)
        {
            return leftType == SemanticType.Str && rightType == SemanticType.Str
                && semanticInfo.IsLiteralDerived(l) && semanticInfo.IsLiteralDerived(r);
        }

        if (op == BinaryOperator.Multiply)
        {
            return (leftType == SemanticType.Str && semanticInfo.IsLiteralDerived(l))
                || (rightType == SemanticType.Str && semanticInfo.IsLiteralDerived(r));
        }

        return false;
    }

    /// <summary>
    /// True when <c>receiver.methodName(args)</c> is literal-derived: <paramref name="methodName"/> is
    /// in <see cref="PreservingMethods"/>, <paramref name="receiver"/> is itself derived, and every
    /// argument that carries string content is derived too. <c>join</c>'s sole argument must be a
    /// literal list DISPLAY of derived elements (a <c>list[str]</c>-typed variable's contents cannot be
    /// proven, even when it happens to hold only literals at this call site); every other method's
    /// str-typed arguments must be derived, and a non-str argument (a width, a count, ...) does not
    /// gate the result's provenance.
    /// </summary>
    public static bool IsPreservingMethodCallDerived(
        SemanticInfo semanticInfo, string methodName, Expression receiver,
        ImmutableArray<Expression> positionalArgs, ImmutableArray<KeywordArgument> keywordArgs)
    {
        if (!PreservingMethods.Contains(methodName))
            return false;

        if (!semanticInfo.IsLiteralDerived(AstHelper.UnwrapParenthesized(receiver)))
            return false;

        if (methodName == "join")
        {
            Expression? joinArg = positionalArgs.Length > 0
                ? positionalArgs[0]
                : keywordArgs.FirstOrDefault(k => k.Name == "iterable")?.Value;

            return joinArg != null
                && AstHelper.UnwrapParenthesized(joinArg) is ListLiteral listLiteral
                && listLiteral.Elements.All(
                    e => semanticInfo.IsLiteralDerived(AstHelper.UnwrapParenthesized(e)));
        }

        foreach (var arg in positionalArgs)
        {
            if (!IsArgumentDerivedOrIrrelevant(semanticInfo, arg))
                return false;
        }
        foreach (var kwarg in keywordArgs)
        {
            if (!IsArgumentDerivedOrIrrelevant(semanticInfo, kwarg.Value))
                return false;
        }
        return true;
    }

    /// <summary>
    /// A non-str-typed argument (a width, a count, ...) does not gate the call result's provenance —
    /// only a str/LiteralString-typed argument must itself be derived.
    /// </summary>
    private static bool IsArgumentDerivedOrIrrelevant(SemanticInfo semanticInfo, Expression arg)
    {
        var unwrapped = AstHelper.UnwrapParenthesized(arg);
        var argType = semanticInfo.GetExpressionType(unwrapped);
        bool isStrLike = argType == SemanticType.Str || argType is LiteralStringType;
        return !isStrLike || semanticInfo.IsLiteralDerived(unwrapped);
    }
}
