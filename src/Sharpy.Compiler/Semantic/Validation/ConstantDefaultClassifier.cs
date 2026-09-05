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

internal static class ConstantDefaultClassifier
{
    public static EmittableConstantKind Classify(Expression expr, Func<string, bool>? constResolver = null)
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
                var operandKind = Classify(unary.Operand, constResolver);
                return IsAdmittedForFolding(operandKind)
                    ? EmittableConstantKind.FoldedOfAdmitted
                    : EmittableConstantKind.Other;
            }

            case BinaryOp binary:
            {
                var leftKind = Classify(binary.Left, constResolver);
                var rightKind = Classify(binary.Right, constResolver);
                return IsAdmittedForFolding(leftKind) && IsAdmittedForFolding(rightKind)
                    ? EmittableConstantKind.FoldedOfAdmitted
                    : EmittableConstantKind.Other;
            }

            case Parenthesized paren:
                return Classify(paren.Expression, constResolver);

            case ConditionalExpression cond:
            {
                var testKind = Classify(cond.Test, constResolver);
                var thenKind = Classify(cond.ThenValue, constResolver);
                var elseKind = Classify(cond.ElseValue, constResolver);
                return IsAdmittedForFolding(testKind)
                    && IsAdmittedForFolding(thenKind)
                    && IsAdmittedForFolding(elseKind)
                    ? EmittableConstantKind.ConditionalOfAdmitted
                    : EmittableConstantKind.Other;
            }

            case Identifier id:
            {
                if (constResolver != null && constResolver(id.Name))
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
