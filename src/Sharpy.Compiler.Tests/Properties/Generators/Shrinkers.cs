using System.Collections.Immutable;
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

    public static Gen<Expression> ShrinkableListLiteral(GenContext ctx) =>
        GenExpressions.ListLiteralExpr(ctx).SelectMany(list => list.Elements.Length > 0
            ? Gen.OneOf(
                Gen.Const((Expression)list),
                Gen.Const((Expression)(list with { Elements = list.Elements.RemoveAt(list.Elements.Length - 1) })),
                Gen.Const((Expression)(list with { Elements = ImmutableArray<Expression>.Empty })))
            : Gen.Const((Expression)list));

    public static Gen<Statement> ShrinkableFunctionDef(GenContext ctx) =>
        GenStatements.FunctionDefStmt(ctx).SelectMany(fn => fn.Body.Length > 1
            ? Gen.OneOf(
                Gen.Const((Statement)fn),
                Gen.Const((Statement)(fn with { Body = ImmutableArray.Create(fn.Body[0]) })))
            : Gen.Const((Statement)fn));

    public static Gen<Module> ShrinkableModule(GenContext ctx) =>
        GenModule.Module(ctx).SelectMany(module =>
        {
            if (module.Body.Length <= 1)
                return Gen.Const(module);

            var first = module.Body[0];
            var shrunk = module with { Body = ImmutableArray.Create(first) };

            if (first is ClassDef cls && cls.Body.Length > 1)
            {
                var shrunkClass = cls with
                {
                    Body = ImmutableArray.Create(cls.Body[0])
                };
                return Gen.OneOf(
                    Gen.Const(module),
                    Gen.Const(shrunk),
                    Gen.Const(module with
                    {
                        Body = ImmutableArray.Create<Statement>(shrunkClass)
                    }));
            }

            return Gen.OneOf(
                Gen.Const(module),
                Gen.Const(shrunk));
        });

    public static Gen<Expression> ShrinkableConditional(GenContext ctx) =>
        GenExpressions.ConditionalExpr(ctx).SelectMany(cond =>
            Gen.OneOf(
                Gen.Const((Expression)cond),
                Gen.Const(cond.ThenValue)));
}
