using System.Collections.Immutable;
using CsCheck;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Tests.Properties.Generators;

internal static class GenStatements
{
    public static Gen<Statement> Statement(GenContext ctx) =>
        ctx.HasFuel
            ? Gen.Frequency(
                (5, SimpleStatement(ctx)),
                (2, CompoundStatement(ctx.Burn())))
            : SimpleStatement(ctx);

    private static Gen<Statement> SimpleStatement(GenContext ctx) =>
        Gen.OneOf<Statement>(
            ExprStmt(ctx),
            VarDecl(ctx),
            Assign(ctx),
            ReturnStmt(ctx),
            PassStmt(),
            AssertStmt(ctx));

    private static Gen<Statement> CompoundStatement(GenContext ctx) =>
        Gen.OneOf<Statement>(
            IfStmt(ctx),
            WhileStmt(ctx),
            ForStmt(ctx),
            TryStmt(ctx),
            WithStmt(ctx),
            MatchStmt(ctx));

    public static Gen<ExpressionStatement> ExprStmt(GenContext ctx) =>
        GenExpressions.Expression(ctx)
            .Where(e => e is not StringLiteral and not NoneLiteral)
            .Select(e => new ExpressionStatement { Expression = e });

    public static Gen<VariableDeclaration> VarDecl(GenContext ctx) =>
        Gen.Select(
            GenIdentifier.Name,
            GenTypes.SimpleType,
            GenExpressions.Expression(ctx),
            (name, type, val) => new VariableDeclaration
            {
                Name = name,
                Type = type,
                InitialValue = val
            });

    public static Gen<Assignment> Assign(GenContext ctx) =>
        Gen.Select(
            GenExpressions.IdentifierExpr(ctx).Select(x => (Expression)x),
            GenExpressions.Expression(ctx),
            Gen.OneOfConst(
                AssignmentOperator.Assign,
                AssignmentOperator.PlusAssign,
                AssignmentOperator.MinusAssign,
                AssignmentOperator.StarAssign),
            (target, val, op) => new Assignment
            {
                Target = target,
                Value = val,
                Operator = op
            });

    public static Gen<ReturnStatement> ReturnStmt(GenContext ctx) =>
        Gen.Null(GenExpressions.Expression(ctx)).Select(val =>
            new ReturnStatement { Value = val });

    public static Gen<PassStatement> PassStmt() =>
        Gen.Const(new PassStatement());

    public static Gen<AssertStatement> AssertStmt(GenContext ctx) =>
        Gen.Select(
            GenExpressions.Expression(ctx),
            Gen.Null(GenLiterals.SimpleString.Select(x => (Expression)x)),
            (cond, msg) => new AssertStatement
            {
                Test = cond,
                Message = msg
            });

    public static Gen<IfStatement> IfStmt(GenContext ctx) =>
        Gen.Select(
            GenExpressions.Expression(ctx),
            Body(ctx),
            Body(ctx),
            (test, then, els) => new IfStatement
            {
                Test = test,
                ThenBody = then,
                ElseBody = els
            });

    public static Gen<WhileStatement> WhileStmt(GenContext ctx) =>
        Gen.Select(
            GenExpressions.Expression(ctx),
            Body(ctx.Burn() with { InLoop = true }),
            (test, body) => new WhileStatement
            {
                Test = test,
                Body = body
            });

    public static Gen<ForStatement> ForStmt(GenContext ctx) =>
        Gen.Select(
            GenIdentifier.Name,
            GenExpressions.Expression(ctx),
            Body(ctx.Burn() with { InLoop = true }),
            (target, iter, body) => new ForStatement
            {
                Target = new Identifier { Name = target },
                Iterator = iter,
                Body = body
            });

    public static Gen<TryStatement> TryStmt(GenContext ctx) =>
        Gen.Select(
            Body(ctx),
            ExceptHandler(ctx),
            (body, handler) => new TryStatement
            {
                Body = body,
                Handlers = ImmutableArray.Create(handler)
            });

    private static Gen<ExceptHandler> ExceptHandler(GenContext ctx) =>
        Gen.Select(
            Gen.Null(GenTypes.SimpleType),
            Gen.Null(GenIdentifier.Name),
            Body(ctx),
            (type, name, body) => new ExceptHandler
            {
                ExceptionType = type,
                Name = name,
                Body = body
            });

    public static Gen<WithStatement> WithStmt(GenContext ctx) =>
        Gen.Select(
            GenExpressions.Expression(ctx),
            Gen.Null(GenIdentifier.Name),
            Body(ctx),
            (expr, name, body) => new WithStatement
            {
                Items = ImmutableArray.Create(new WithItem
                {
                    ContextExpression = expr,
                    Name = name
                }),
                Body = body
            });

    public static Gen<MatchStatement> MatchStmt(GenContext ctx) =>
        Gen.Select(
            GenExpressions.Expression(ctx),
            MatchCaseGen(ctx).Array[1, 3],
            (scrutinee, cases) => new MatchStatement
            {
                Scrutinee = scrutinee,
                Cases = cases.ToImmutableArray()
            });

    private static Gen<MatchCase> MatchCaseGen(GenContext ctx) =>
        Gen.Select(
            GenPatterns.Pattern(ctx),
            Body(ctx),
            (pat, body) => new MatchCase
            {
                Pattern = pat,
                Body = body
            });

    public static Gen<FunctionDef> FunctionDefStmt(GenContext ctx) =>
        Gen.Select(
            GenIdentifier.FunctionName,
            GenIdentifier.Name.Array[0, Sizing.MaxParameters(ctx.Fuel)],
            Gen.Null(GenTypes.SimpleType),
            Body(ctx.Burn() with { InFunction = true }),
            (name, paramNames, retType, body) => new FunctionDef
            {
                Name = name,
                Parameters = paramNames.Select(n => new Parameter { Name = n }).ToImmutableArray(),
                ReturnType = retType,
                Body = body
            });

    public static Gen<ClassDef> ClassDefStmt(GenContext ctx) =>
        Gen.Select(
            GenIdentifier.ClassName,
            FunctionDefStmt(ctx.Burn() with { InClass = true }).Array[0, 2],
            (name, methods) =>
            {
                var body = methods.Length > 0
                    ? methods.Cast<Statement>().ToImmutableArray()
                    : ImmutableArray.Create<Statement>(new PassStatement());
                return new ClassDef
                {
                    Name = name,
                    Body = body
                };
            });

    public static Gen<ImmutableArray<Statement>> Body(GenContext ctx)
    {
        int maxLen = Sizing.MaxBodyLength(ctx.Fuel);
        return Statement(ctx.Burn()).Array[1, Math.Max(1, maxLen)]
            .Select(stmts => stmts.ToImmutableArray());
    }
}
