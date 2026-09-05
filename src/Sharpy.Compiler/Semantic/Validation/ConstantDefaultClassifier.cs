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
    ModuleConst,
}

/// <summary>
/// ONE emittable-constant classifier for every constant position — def/lambda/__init__/dataclass
/// parameter defaults, decorator arguments and module-level <c>const</c> initializers — with a
/// per-site <see cref="AdmissionTable"/>. The classifier reads the SHAPE of the expression; the two
/// facts a shape cannot carry are supplied by the caller:
/// <list type="bullet">
/// <item><c>constResolver</c> — whether an <see cref="Identifier"/> names a constant the site may
/// read as a C# constant. The validator answers "any <c>const</c> symbol"; the module-const computer
/// answers "a <c>const</c> whose own initializer is compile-time", so a chain of consts folds and a
/// forward reference to a literal const folds too (C# resolves const dependency order itself).</item>
/// <item><c>operatorLowersToConstant</c> — whether an operator node (<see cref="BinaryOp"/>, a
/// non-literal <see cref="UnaryOp"/>, <see cref="ConditionalExpression"/>) LOWERS to a C# constant
/// operator. <c>//</c>, <c>%</c>, <c>**</c>, str <c>*</c> and ordinal string compares lower to calls
/// (<c>FloorDiv</c>, <c>FloorMod</c>, <c>Math.Pow</c>, <c>Repeat</c>, <c>CompareOrdinal</c>), which
/// C# refuses in a <c>const</c> initializer (CS0133) and in a parameter default (CS1736). A caller
/// holding the checker's recorded lowerings passes the fact; a caller without them (the validator
/// runs before type checking) passes null and the shape rule stands alone.</item>
/// </list>
/// </summary>
internal static class ConstantDefaultClassifier
{
    public static EmittableConstantKind Classify(
        Expression expr,
        Func<Identifier, bool>? constResolver = null,
        Func<Expression, bool>? operatorLowersToConstant = null)
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
                    var operandKind = Classify(unary.Operand, constResolver, operatorLowersToConstant);
                    return IsAdmittedForFolding(operandKind)
                        ? EmittableConstantKind.FoldedOfAdmitted
                        : EmittableConstantKind.Other;
                }

            case BinaryOp binary:
                {
                    if (operatorLowersToConstant != null && !operatorLowersToConstant(binary))
                        return EmittableConstantKind.Other;
                    var leftKind = Classify(binary.Left, constResolver, operatorLowersToConstant);
                    var rightKind = Classify(binary.Right, constResolver, operatorLowersToConstant);
                    return IsAdmittedForFolding(leftKind) && IsAdmittedForFolding(rightKind)
                        ? EmittableConstantKind.FoldedOfAdmitted
                        : EmittableConstantKind.Other;
                }

            case Parenthesized paren:
                return Classify(paren.Expression, constResolver, operatorLowersToConstant);

            case ConditionalExpression cond:
                {
                    if (operatorLowersToConstant != null && !operatorLowersToConstant(cond))
                        return EmittableConstantKind.Other;
                    var testKind = Classify(cond.Test, constResolver, operatorLowersToConstant);
                    var thenKind = Classify(cond.ThenValue, constResolver, operatorLowersToConstant);
                    var elseKind = Classify(cond.ElseValue, constResolver, operatorLowersToConstant);
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

            case MemberAccess { Object: Identifier }:
                return EmittableConstantKind.EnumMember;

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

            AdmissionTable.DecoratorArgument => kind is
                EmittableConstantKind.Literal or
                EmittableConstantKind.NegatedLiteral or
                EmittableConstantKind.NoneLiteral or
                EmittableConstantKind.EnumMember or
                EmittableConstantKind.TypeOf,

            AdmissionTable.ModuleConst => kind is
                EmittableConstantKind.Literal or
                EmittableConstantKind.NegatedLiteral or
                EmittableConstantKind.FoldedOfAdmitted or
                EmittableConstantKind.ConstReference or
                EmittableConstantKind.ConditionalOfAdmitted,

            _ => false,
        };
    }

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
