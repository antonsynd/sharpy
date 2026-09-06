using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic.Validation;

internal enum EmittableConstantKind
{
    Literal,
    NegatedLiteral,
    FoldedOfAdmitted,
    ConstReference,
    EnumMember,
    NoneLiteral,
    NoneCall,
    TypeOf,
    ConditionalOfAdmitted,
    TupleLiteral,
    CaseConstructor,
    Collection,
    Comprehension,
    Call,
    Lambda,
    Other,
}

internal enum AdmissionTable
{
    ParameterDefault,
    LambdaParameterDefault,
    DecoratorArgument,

    /// <summary>
    /// A <c>const</c> declaration's initializer, at EVERY host — module body, function body, class
    /// field, struct field, nested-type field. One table, because the host does not change what C#
    /// admits in a <c>const</c> initializer (#1791).
    /// </summary>
    ConstInitializer,
}

/// <summary>
/// ONE emittable-constant classifier for every constant position — def/lambda/__init__/dataclass
/// parameter defaults, decorator arguments, match-case constants and <c>const</c> initializers at
/// every host — with a per-site <see cref="AdmissionTable"/>. The classifier reads the SHAPE of the
/// expression; the two facts a shape cannot carry are supplied by the caller:
/// <list type="bullet">
/// <item><c>constResolver</c> — whether an <see cref="Identifier"/> names a constant the site may
/// read as a C# constant. Every same-file caller answers from the ONE fact
/// <see cref="ConstEligibility"/> computed ("a <c>const</c> whose own initializer is compile-time"),
/// so a chain of consts folds and a forward reference to a literal const folds too (C# resolves
/// const dependency order itself).</item>
/// <item><c>operatorLowersToConstant</c> — whether an operator node (<see cref="BinaryOp"/>, a
/// non-literal <see cref="UnaryOp"/>, <see cref="ConditionalExpression"/>) LOWERS to a C# constant
/// operator. <c>//</c>, <c>%</c>, float <c>**</c>, str <c>*</c> and ordinal string compares lower to
/// calls (<c>FloorDiv</c>, <c>FloorMod</c>, <c>Math.Pow</c>, <c>Repeat</c>, <c>CompareOrdinal</c>),
/// which C# refuses in a <c>const</c> initializer (CS0133) and in a parameter default (CS1736). A
/// caller holding the checker's recorded lowerings passes
/// <see cref="ConstEligibility.LowersToConstantExpression(Expression, SemanticInfo?)"/>; the
/// cross-module route, which has no recorded lowerings, passes the operand-type-independent
/// roster instead.</item>
/// </list>
/// </summary>
internal static class ConstantDefaultClassifier
{
    public static EmittableConstantKind Classify(
        Expression expr,
        Func<Identifier, bool>? constResolver = null,
        Func<Expression, bool>? operatorLowersToConstant = null,
        Func<MemberAccess, bool?>? memberConstResolver = null)
    {
        switch (expr)
        {
            case IntegerLiteral:
            case FloatLiteral:
            case StringLiteral:
            case BooleanLiteral:
                return EmittableConstantKind.Literal;

            case NoneLiteral:
                return EmittableConstantKind.NoneLiteral;

            case UnaryOp { Operator: UnaryOperator.Minus or UnaryOperator.Plus, Operand: IntegerLiteral or FloatLiteral }:
                return EmittableConstantKind.NegatedLiteral;

            case UnaryOp unary:
                {
                    if (operatorLowersToConstant != null && !operatorLowersToConstant(unary))
                        return EmittableConstantKind.Other;
                    var operandKind = Classify(unary.Operand, constResolver, operatorLowersToConstant, memberConstResolver);
                    return IsAdmittedForFolding(operandKind)
                        ? EmittableConstantKind.FoldedOfAdmitted
                        : EmittableConstantKind.Other;
                }

            case BinaryOp binary:
                {
                    if (operatorLowersToConstant != null && !operatorLowersToConstant(binary))
                        return EmittableConstantKind.Other;
                    var leftKind = Classify(binary.Left, constResolver, operatorLowersToConstant, memberConstResolver);
                    var rightKind = Classify(binary.Right, constResolver, operatorLowersToConstant, memberConstResolver);
                    return IsAdmittedForFolding(leftKind) && IsAdmittedForFolding(rightKind)
                        ? EmittableConstantKind.FoldedOfAdmitted
                        : EmittableConstantKind.Other;
                }

            case Parenthesized paren:
                return Classify(paren.Expression, constResolver, operatorLowersToConstant, memberConstResolver);

            case ConditionalExpression cond:
                {
                    if (operatorLowersToConstant != null && !operatorLowersToConstant(cond))
                        return EmittableConstantKind.Other;
                    var testKind = Classify(cond.Test, constResolver, operatorLowersToConstant, memberConstResolver);
                    var thenKind = Classify(cond.ThenValue, constResolver, operatorLowersToConstant, memberConstResolver);
                    var elseKind = Classify(cond.ElseValue, constResolver, operatorLowersToConstant, memberConstResolver);
                    return IsAdmittedForFolding(testKind)
                        && IsAdmittedForFolding(thenKind)
                        && IsAdmittedForFolding(elseKind)
                        ? EmittableConstantKind.ConditionalOfAdmitted
                        : EmittableConstantKind.Other;
                }

            case Identifier id:
                {
                    if (constResolver != null && constResolver(id))
                        return EmittableConstantKind.ConstReference;
                    return EmittableConstantKind.Other;
                }

            // A qualified constant: an enum member (`Color.RED`), a class const (`C.K`) or a
            // nested-type const (`Outer.Holder.K`). The chain must bottom out in an identifier —
            // C# resolves exactly that spelling in a constant position (#1791) — and, when it names
            // a `const`, that const's own fact decides. Without the second half a class const with a
            // call initializer was admitted by SHAPE and refused by Roslyn (CS1736 behind SPY0908).
            case MemberAccess member when RootsInIdentifier(member):
                return memberConstResolver?.Invoke(member) == false
                    ? EmittableConstantKind.Other
                    : EmittableConstantKind.EnumMember;

            case FunctionCall call:
                {
                    var callee = AstHelper.UnwrapParenthesized(call.Function);
                    if (callee is NoneLiteral && call.Arguments.Length == 0 && call.KeywordArguments.Length == 0)
                        return EmittableConstantKind.NoneCall;

                    if (callee is Identifier { Name: "type" } && call.Arguments.Length == 1 && call.KeywordArguments.Length == 0)
                        return EmittableConstantKind.TypeOf;

                    if (callee is Identifier fid && fid.Name is "Some" or "Ok" or "Err" && call.Arguments.Length == 1)
                        return EmittableConstantKind.CaseConstructor;

                    return EmittableConstantKind.Call;
                }

            case TupleLiteral:
                return EmittableConstantKind.TupleLiteral;

            case ListLiteral:
            case DictLiteral:
            case SetLiteral:
                return EmittableConstantKind.Collection;

            case ListComprehension:
            case SetComprehension:
            case DictComprehension:
            case DictSpreadComprehension:
                return EmittableConstantKind.Comprehension;

            case LambdaExpression:
                return EmittableConstantKind.Lambda;

            default:
                return EmittableConstantKind.Other;
        }
    }

    public static bool IsAdmitted(EmittableConstantKind kind, AdmissionTable table)
    {
        return table switch
        {
            AdmissionTable.ParameterDefault or AdmissionTable.LambdaParameterDefault => kind is
                EmittableConstantKind.Literal or
                EmittableConstantKind.NegatedLiteral or
                EmittableConstantKind.FoldedOfAdmitted or
                EmittableConstantKind.ConstReference or
                EmittableConstantKind.EnumMember or
                EmittableConstantKind.NoneLiteral or
                EmittableConstantKind.NoneCall or
                EmittableConstantKind.TypeOf or
                EmittableConstantKind.ConditionalOfAdmitted,

            // A C# attribute argument IS a constant expression, so this table admits exactly what a
            // const initializer does: a ConstReference whose own fact is true, a fold over admitted
            // operands, and a conditional over them (#1782). The emitter's
            // GenerateAttributeArgumentExpression prints an identifier through the ordinary read
            // route and a composition through ordinary expression emission (#1801); the two land
            // together, because admitting a shape the emitter cannot print turns a clean SPY0425
            // into an SPY0909.
            AdmissionTable.DecoratorArgument => kind is
                EmittableConstantKind.Literal or
                EmittableConstantKind.NegatedLiteral or
                EmittableConstantKind.NoneLiteral or
                EmittableConstantKind.EnumMember or
                EmittableConstantKind.TypeOf or
                EmittableConstantKind.ConstReference or
                EmittableConstantKind.FoldedOfAdmitted or
                EmittableConstantKind.ConditionalOfAdmitted,

            AdmissionTable.ConstInitializer => kind is
                EmittableConstantKind.Literal or
                EmittableConstantKind.NegatedLiteral or
                EmittableConstantKind.FoldedOfAdmitted or
                EmittableConstantKind.ConstReference or
                EmittableConstantKind.EnumMember or
                EmittableConstantKind.ConditionalOfAdmitted,

            _ => false,
        };
    }

    /// <summary>
    /// Whether a member-access chain bottoms out in a plain identifier, so its C# spelling is a
    /// qualified name rather than an expression with a runtime receiver.
    /// </summary>
    private static bool RootsInIdentifier(Expression expr) => expr switch
    {
        Identifier => true,
        MemberAccess member => RootsInIdentifier(member.Object),
        _ => false,
    };

    private static bool IsAdmittedForFolding(EmittableConstantKind kind) =>
        kind is EmittableConstantKind.Literal
            or EmittableConstantKind.NegatedLiteral
            or EmittableConstantKind.FoldedOfAdmitted
            or EmittableConstantKind.ConstReference
            or EmittableConstantKind.EnumMember
            or EmittableConstantKind.NoneLiteral
            or EmittableConstantKind.NoneCall
            or EmittableConstantKind.TypeOf
            or EmittableConstantKind.ConditionalOfAdmitted;
}
