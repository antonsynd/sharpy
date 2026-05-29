using System.Collections.Immutable;
using CsCheck;
using Sharpy.Compiler.Lexer;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Tests.Properties.Generators;

internal static class GenStatements
{
    /// <summary>
    /// Randomly attaches BlankLines leading trivia to a statement (~20% chance).
    /// BlankLineCount is randomly chosen between 1 and 3, with empty Text.
    /// </summary>
    internal static Gen<T> WithOptionalBlankLineTrivia<T>(Gen<T> gen) where T : Statement =>
        Gen.Select(gen, Gen.Int[1, 100], Gen.Int[1, 3], (stmt, roll, count) =>
        {
            if (roll > 20)
                return stmt;
            return AddBlankLineTrivia(stmt, count);
        });

    /// <summary>
    /// Deterministically attaches BlankLines leading trivia to a statement.
    /// </summary>
    internal static T AddBlankLineTrivia<T>(T stmt, int blankLineCount) where T : Statement
    {
        var trivia = new Trivia
        {
            Kind = TriviaKind.BlankLines,
            BlankLineCount = blankLineCount,
            Text = ""
        };
        return (T)(stmt with
        {
            LeadingTrivia = new[] { trivia }
        });
    }

    /// <summary>
    /// Deterministically attaches BlankLines trivia to a statement based on
    /// a hash of its position, giving roughly 20% coverage with counts 1-3.
    /// Suitable for use inside Select transforms.
    /// </summary>
    internal static Statement AddOptionalBlankLineTrivia(Statement stmt)
    {
        // Use a hash of the statement's hashcode to pseudo-randomly decide
        var hash = Math.Abs(stmt.GetHashCode());
        if (hash % 5 != 0)
            return stmt;
        int count = (hash % 3) + 1;
        return AddBlankLineTrivia(stmt, count);
    }

    public static Gen<Statement> Statement(GenContext ctx) =>
        ctx.HasFuel
            ? Gen.Frequency(
                (5, SimpleStatement(ctx)),
                (2, CompoundStatement(ctx.Burn())))
            : SimpleStatement(ctx);

    private static Gen<Statement> SimpleStatement(GenContext ctx)
    {
        var gens = new List<Gen<Statement>>
        {
            ExprStmt(ctx).Select(x => (Statement)x),
            VarDecl(ctx).Select(x => (Statement)x),
            Assign(ctx).Select(x => (Statement)x),
            ReturnStmt(ctx).Select(x => (Statement)x),
            PassStmt().Select(x => (Statement)x),
            AssertStmt(ctx).Select(x => (Statement)x),
            RaiseStmt(ctx).Select(x => (Statement)x),
            ImportStmt().Select(x => (Statement)x),
            FromImportStmt().Select(x => (Statement)x),
            TypeAliasStmt().Select(x => (Statement)x),
            PropertyDefStmt().Select(x => (Statement)x)
        };
        if (ctx.InLoop)
        {
            gens.Add(BreakStmt().Select(x => (Statement)x));
            gens.Add(ContinueStmt().Select(x => (Statement)x));
        }
        if (ctx.AllowYield)
            gens.Add(YieldStmt(ctx).Select(x => (Statement)x));
        return Gen.OneOf(gens.ToArray());
    }

    private static Gen<Statement> CompoundStatement(GenContext ctx) =>
        Gen.OneOf(
            IfStmt(ctx).Select(x => (Statement)x),
            WhileStmt(ctx).Select(x => (Statement)x),
            ForStmt(ctx).Select(x => (Statement)x),
            TryStmt(ctx).Select(x => (Statement)x),
            WithStmt(ctx).Select(x => (Statement)x),
            MatchStmt(ctx).Select(x => (Statement)x),
            EnumDefStmt().Select(x => (Statement)x),
            StructDefStmt(ctx).Select(x => (Statement)x),
            InterfaceDefStmt(ctx).Select(x => (Statement)x));

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

    public static Gen<BreakStatement> BreakStmt() =>
        Gen.Const(new BreakStatement());

    public static Gen<ContinueStatement> ContinueStmt() =>
        Gen.Const(new ContinueStatement());

    public static Gen<YieldStatement> YieldStmt(GenContext ctx) =>
        Gen.Select(
            GenExpressions.Expression(ctx),
            Gen.Bool,
            (val, isFrom) => new YieldStatement
            {
                Value = val,
                IsFrom = isFrom
            });

    public static Gen<RaiseStatement> RaiseStmt(GenContext ctx) =>
        Gen.Null(GenExpressions.Expression(ctx)).Select(exc =>
            new RaiseStatement { Exception = exc });

    public static Gen<ImportStatement> ImportStmt() =>
        GenIdentifier.Name.Array[1, 2].Select(names =>
            new ImportStatement
            {
                Names = names.Select(n => new ImportAlias { Name = n }).ToImmutableArray()
            });

    public static Gen<FromImportStatement> FromImportStmt() =>
        Gen.Select(
            GenIdentifier.Name,
            GenIdentifier.Name.Array[1, 3],
            (module, names) => new FromImportStatement
            {
                Module = module,
                Names = names.Select(n => new ImportAlias { Name = n }).ToImmutableArray(),
                ImportAll = false
            });

    public static Gen<TypeAlias> TypeAliasStmt() =>
        Gen.Select(
            GenIdentifier.ClassName,
            GenTypes.SimpleType,
            (name, type) => new TypeAlias
            {
                Name = name,
                Type = type
            });

    private static readonly string[] EnumMemberNames = { "RED", "GREEN", "BLUE", "ALPHA" };

    public static Gen<EnumDef> EnumDefStmt() =>
        Gen.Select(
            GenIdentifier.ClassName,
            Gen.Int[2, 4],
            (name, count) => new EnumDef
            {
                Name = name,
                Members = EnumMemberNames.Take(count)
                    .Select(n => new EnumMember { Name = n })
                    .ToImmutableArray()
            });

    public static Gen<StructDef> StructDefStmt(GenContext ctx) =>
        Gen.Select(
            GenIdentifier.ClassName,
            FunctionDefStmt(ctx.Burn() with { InClass = true }).Array[0, 2],
            (name, methods) =>
            {
                var body = methods.Length > 0
                    ? methods.Cast<Statement>().ToImmutableArray()
                    : ImmutableArray.Create<Statement>(new PassStatement());
                return new StructDef
                {
                    Name = name,
                    Body = body
                };
            });

    public static Gen<InterfaceDef> InterfaceDefStmt(GenContext ctx) =>
        Gen.Select(
            GenIdentifier.ClassName,
            FunctionDefStmt(ctx.Burn() with { InClass = true }).Array[0, 2],
            (name, methods) =>
            {
                var body = methods.Length > 0
                    ? methods.Cast<Statement>().ToImmutableArray()
                    : ImmutableArray.Create<Statement>(new PassStatement());
                return new InterfaceDef
                {
                    Name = name,
                    Body = body
                };
            });

    public static Gen<PropertyDef> PropertyDefStmt() =>
        Gen.Select(
            GenIdentifier.Name,
            GenTypes.SimpleType,
            (name, type) => new PropertyDef
            {
                Name = name,
                Type = type,
                Accessor = PropertyAccessor.None,
                IsFunctionStyle = false
            });

    public static Gen<ImmutableArray<Statement>> Body(GenContext ctx)
    {
        int maxLen = Sizing.MaxBodyLength(ctx.Fuel);
        return Statement(ctx.Burn()).Array[1, Math.Max(1, maxLen)]
            .Select(stmts => stmts.ToImmutableArray());
    }

    /// <summary>
    /// Like <see cref="Statement"/> but with ~20% chance of BlankLines trivia on each statement.
    /// </summary>
    public static Gen<Statement> StatementWithTrivia(GenContext ctx) =>
        WithOptionalBlankLineTrivia(Statement(ctx));

    /// <summary>
    /// Like <see cref="Body"/> but statements may carry BlankLines trivia.
    /// </summary>
    public static Gen<ImmutableArray<Statement>> BodyWithTrivia(GenContext ctx)
    {
        int maxLen = Sizing.MaxBodyLength(ctx.Fuel);
        return StatementWithTrivia(ctx.Burn()).Array[1, Math.Max(1, maxLen)]
            .Select(stmts => stmts.ToImmutableArray());
    }
}
