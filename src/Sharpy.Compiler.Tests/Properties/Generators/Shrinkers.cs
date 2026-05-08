using CsCheck;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Tests.Properties.Generators;

internal static class Shrinkers
{
    public static Gen<Expression> ShrinkableExpression(GenContext ctx) =>
        GenExpressions.Expression(ctx).SelectMany(expr => expr switch
        {
            BinaryOp bin => Gen.OneOf(
                Gen.Const(expr),
                Gen.Const(bin.Left),
                Gen.Const(bin.Right)),
            UnaryOp un => Gen.OneOf(
                Gen.Const(expr),
                Gen.Const(un.Operand)),
            Parenthesized p => Gen.OneOf(
                Gen.Const(expr),
                Gen.Const(p.Expression)),
            ConditionalExpression c => Gen.OneOf(
                Gen.Const(expr),
                Gen.Const(c.ThenValue),
                Gen.Const(c.ElseValue)),
            _ => Gen.Const(expr)
        });

    public static Gen<Statement> ShrinkableStatement(GenContext ctx) =>
        GenStatements.Statement(ctx).SelectMany(stmt => stmt switch
        {
            IfStatement ifs when ifs.ThenBody.Length > 1 =>
                Gen.OneOf(
                    Gen.Const(stmt),
                    Gen.Const<Statement>(new IfStatement
                    {
                        Test = ifs.Test,
                        ThenBody = ifs.ThenBody.RemoveAt(ifs.ThenBody.Length - 1),
                        ElseBody = ifs.ElseBody
                    })),
            _ => Gen.Const(stmt)
        });
}
