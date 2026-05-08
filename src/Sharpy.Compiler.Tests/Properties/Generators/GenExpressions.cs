using System.Collections.Immutable;
using CsCheck;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Tests.Properties.Generators;

internal static class GenExpressions
{
    public static Gen<Expression> Expression(GenContext ctx) =>
        ctx.HasFuel
            ? Gen.Frequency(
                (5, Leaf(ctx)),
                (3, SimpleComposite(ctx.Burn())),
                (1, ComplexComposite(ctx.Burn(2))))
            : Leaf(ctx);

    private static Gen<Expression> Leaf(GenContext ctx) =>
        Gen.OneOf<Expression>(
            GenLiterals.AnyLiteral,
            IdentifierExpr(ctx),
            BytesLiteralExpr(),
            SuperExpressionExpr());

    private static Gen<Expression> SimpleComposite(GenContext ctx)
    {
        var gens = new List<Gen<Expression>>
        {
            UnaryOpExpr(ctx).Select(x => (Expression)x),
            BinaryOpExpr(ctx).Select(x => (Expression)x),
            ParenthesizedExpr(ctx).Select(x => (Expression)x),
            MemberAccessExpr(ctx).Select(x => (Expression)x),
            FunctionCallExpr(ctx).Select(x => (Expression)x),
            ConditionalExpr(ctx).Select(x => (Expression)x),
            TryExpressionExpr(ctx).Select(x => (Expression)x),
            MaybeExpressionExpr(ctx).Select(x => (Expression)x),
            StarExpressionExpr(ctx).Select(x => (Expression)x),
            SpreadElementExpr(ctx).Select(x => (Expression)x),
            ModifiedArgumentExpr(ctx).Select(x => (Expression)x),
        };
        if (ctx.AllowAsync)
            gens.Add(AwaitExpressionExpr(ctx).Select(x => (Expression)x));
        return Gen.OneOf(gens.ToArray());
    }

    private static Gen<Expression> ComplexComposite(GenContext ctx) =>
        Gen.OneOf<Expression>(
            ListLiteralExpr(ctx),
            DictLiteralExpr(ctx),
            SetLiteralExpr(ctx),
            TupleLiteralExpr(ctx),
            LambdaExpr(ctx),
            ListComprehensionExpr(ctx),
            IndexAccessExpr(ctx),
            SliceAccessExpr(ctx),
            WalrusExpr(ctx),
            FStringLiteralExpr(ctx),
            TStringLiteralExpr(ctx),
            SetComprehensionExpr(ctx),
            DictComprehensionExpr(ctx),
            MatchExpressionExpr(ctx));

    public static Gen<Identifier> IdentifierExpr(GenContext ctx) =>
        GenIdentifier.NameFromContext(ctx).Select(n =>
            new Identifier { Name = n });

    public static Gen<UnaryOp> UnaryOpExpr(GenContext ctx) =>
        Gen.Select(
            Gen.OneOfConst(UnaryOperator.Plus, UnaryOperator.Minus, UnaryOperator.Not, UnaryOperator.BitwiseNot),
            Expression(ctx),
            (op, operand) => new UnaryOp { Operator = op, Operand = operand });

    private static readonly BinaryOperator[] SafeBinaryOps =
    {
        BinaryOperator.Add, BinaryOperator.Subtract, BinaryOperator.Multiply,
        BinaryOperator.Divide, BinaryOperator.FloorDivide, BinaryOperator.Modulo,
        BinaryOperator.Power, BinaryOperator.And, BinaryOperator.Or,
        BinaryOperator.BitwiseAnd, BinaryOperator.BitwiseOr, BinaryOperator.BitwiseXor,
        BinaryOperator.LeftShift, BinaryOperator.RightShift, BinaryOperator.NullCoalesce
    };

    public static Gen<BinaryOp> BinaryOpExpr(GenContext ctx) =>
        Gen.Select(
            Gen.OneOfConst(SafeBinaryOps),
            Expression(ctx),
            Expression(ctx),
            (op, left, right) => new BinaryOp { Operator = op, Left = left, Right = right });

    public static Gen<Parenthesized> ParenthesizedExpr(GenContext ctx) =>
        Expression(ctx).Select(e => new Parenthesized { Expression = e });

    public static Gen<MemberAccess> MemberAccessExpr(GenContext ctx) =>
        Gen.Select(
            Expression(ctx),
            GenIdentifier.Name,
            (obj, member) => new MemberAccess { Object = obj, Member = member });

    public static Gen<FunctionCall> FunctionCallExpr(GenContext ctx) =>
        Gen.Select(
            IdentifierExpr(ctx).Select(x => (Expression)x),
            Expression(ctx).Array[0, Sizing.MaxParameters(ctx.Fuel)],
            (func, args) => new FunctionCall
            {
                Function = func,
                Arguments = args.ToImmutableArray()
            });

    public static Gen<ConditionalExpression> ConditionalExpr(GenContext ctx) =>
        Gen.Select(
            Expression(ctx),
            Expression(ctx),
            Expression(ctx),
            (test, then, els) => new ConditionalExpression
            {
                Test = test,
                ThenValue = then,
                ElseValue = els
            });

    public static Gen<ListLiteral> ListLiteralExpr(GenContext ctx) =>
        Expression(ctx).Array[0, Sizing.MaxListLength(ctx.Fuel)].Select(elems =>
            new ListLiteral { Elements = elems.ToImmutableArray() });

    public static Gen<DictLiteral> DictLiteralExpr(GenContext ctx) =>
        GenLiterals.Dict(Expression(ctx), Expression(ctx), Sizing.MaxListLength(ctx.Fuel));

    public static Gen<SetLiteral> SetLiteralExpr(GenContext ctx) =>
        Expression(ctx).Array[1, Math.Max(1, Sizing.MaxListLength(ctx.Fuel))].Select(elems =>
            new SetLiteral { Elements = elems.ToImmutableArray() });

    public static Gen<TupleLiteral> TupleLiteralExpr(GenContext ctx) =>
        Expression(ctx).Array[1, Math.Max(1, Sizing.MaxListLength(ctx.Fuel))].Select(elems =>
            new TupleLiteral
            {
                Elements = elems.ToImmutableArray(),
                ElementNames = ImmutableArray<string?>.Empty
            });

    public static Gen<IndexAccess> IndexAccessExpr(GenContext ctx) =>
        Gen.Select(
            Expression(ctx),
            Expression(ctx),
            (obj, index) => new IndexAccess { Object = obj, Index = index });

    public static Gen<SliceAccess> SliceAccessExpr(GenContext ctx) =>
        Gen.Select(
            Expression(ctx),
            Gen.Null(Expression(ctx)),
            Gen.Null(Expression(ctx)),
            (obj, start, stop) => new SliceAccess
            {
                Object = obj,
                Start = start,
                Stop = stop
            });

    public static Gen<ComparisonChain> ComparisonChainExpr(GenContext ctx) =>
        Gen.Select(
            Expression(ctx).Array[2, 4],
            Gen.OneOfConst(
                ComparisonOperator.Equal, ComparisonOperator.NotEqual,
                ComparisonOperator.LessThan, ComparisonOperator.LessThanOrEqual,
                ComparisonOperator.GreaterThan, ComparisonOperator.GreaterThanOrEqual
            ).Array[1, 3],
            (operands, ops) =>
            {
                var opCount = Math.Min(operands.Length - 1, ops.Length);
                return new ComparisonChain
                {
                    Operands = operands[..(opCount + 1)].ToImmutableArray(),
                    Operators = ops[..opCount].ToImmutableArray()
                };
            });

    public static Gen<LambdaExpression> LambdaExpr(GenContext ctx) =>
        Gen.Select(
            GenIdentifier.Name.Array[0, 3],
            Expression(ctx),
            (paramNames, body) => new LambdaExpression
            {
                Parameters = paramNames.Select(n => new Parameter { Name = n }).ToImmutableArray(),
                Body = body
            });

    public static Gen<ListComprehension> ListComprehensionExpr(GenContext ctx) =>
        Gen.Select(
            GenIdentifier.Name,
            Expression(ctx),
            Expression(ctx),
            (varName, elem, iter) => new ListComprehension
            {
                Element = elem,
                Clauses = ImmutableArray.Create<ComprehensionClause>(
                    new ForClause
                    {
                        Target = new Identifier { Name = varName },
                        Iterator = iter
                    })
            });

    public static Gen<TypeCoercion> TypeCoercionExpr(GenContext ctx) =>
        Gen.Select(
            Expression(ctx),
            GenTypes.SimpleType,
            (expr, type) => new TypeCoercion { Value = expr, TargetType = type });

    public static Gen<TypeCheck> TypeCheckExpr(GenContext ctx) =>
        Gen.Select(
            Expression(ctx),
            GenTypes.SimpleType,
            (expr, type) => new TypeCheck
            {
                Value = expr,
                CheckType = type
            });

    public static Gen<WalrusExpression> WalrusExpr(GenContext ctx) =>
        Gen.Select(
            GenIdentifier.Name,
            Expression(ctx),
            (name, val) => new WalrusExpression
            {
                Target = name,
                Value = val
            });

    private static readonly string[] FStringTextParts =
    {
        "hello ", "world", " is ", "value: ", "result = ", " - ", ", ", "! ", " "
    };

    public static Gen<FStringLiteral> FStringLiteralExpr(GenContext ctx) =>
        Gen.Int[1, 3].SelectMany(count =>
            Enumerable.Range(0, count)
                .Select(_ => Gen.Frequency(
                    (1, Gen.OneOfConst(FStringTextParts).Select(t =>
                        new FStringPart { Text = t })),
                    (1, Expression(ctx.Burn()).Select(e =>
                        new FStringPart { Expression = e }))))
                .Aggregate(Gen.Const(ImmutableArray<FStringPart>.Empty),
                    (acc, partGen) => Gen.Select(acc, partGen, (parts, part) => parts.Add(part)))
                .Select(parts => new FStringLiteral { Parts = parts }));

    public static Gen<TStringLiteral> TStringLiteralExpr(GenContext ctx) =>
        Gen.Int[1, 3].SelectMany(count =>
            Enumerable.Range(0, count)
                .Select(_ => Gen.Frequency(
                    (1, Gen.OneOfConst(FStringTextParts).Select(t =>
                        new FStringPart { Text = t })),
                    (1, Expression(ctx.Burn()).Select(e =>
                        new FStringPart { Expression = e }))))
                .Aggregate(Gen.Const(ImmutableArray<FStringPart>.Empty),
                    (acc, partGen) => Gen.Select(acc, partGen, (parts, part) => parts.Add(part)))
                .Select(parts => new TStringLiteral { Parts = parts }));

    public static Gen<BytesLiteralExpression> BytesLiteralExpr() =>
        Gen.OneOfConst("hello", "test", "data", "bytes", "", "abc")
            .Select(v => new BytesLiteralExpression { Value = v });

    public static Gen<SetComprehension> SetComprehensionExpr(GenContext ctx) =>
        Gen.Select(
            GenIdentifier.Name,
            Expression(ctx),
            Expression(ctx),
            (varName, elem, iter) => new SetComprehension
            {
                Element = elem,
                Clauses = ImmutableArray.Create<ComprehensionClause>(
                    new ForClause
                    {
                        Target = new Identifier { Name = varName },
                        Iterator = iter
                    })
            });

    public static Gen<DictComprehension> DictComprehensionExpr(GenContext ctx) =>
        Gen.Select(
            GenIdentifier.Name,
            Expression(ctx),
            Expression(ctx),
            Expression(ctx),
            (varName, key, value, iter) => new DictComprehension
            {
                Key = key,
                Value = value,
                Clauses = ImmutableArray.Create<ComprehensionClause>(
                    new ForClause
                    {
                        Target = new Identifier { Name = varName },
                        Iterator = iter
                    })
            });

    public static Gen<MatchExpression> MatchExpressionExpr(GenContext ctx) =>
        Gen.Select(
            Expression(ctx),
            GenPatterns.Pattern(ctx.Burn()).Array[1, 3],
            Expression(ctx.Burn()).Array[1, 3],
            Gen.Null(Expression(ctx.Burn())).Array[1, 3],
            (scrutinee, patterns, results, guards) =>
            {
                var armCount = Math.Min(patterns.Length, results.Length);
                armCount = Math.Min(armCount, guards.Length);
                var arms = new MatchArm[armCount];
                for (int i = 0; i < armCount; i++)
                {
                    arms[i] = new MatchArm
                    {
                        Pattern = patterns[i],
                        Guard = guards[i],
                        Result = results[i]
                    };
                }
                return new MatchExpression
                {
                    Scrutinee = scrutinee,
                    Arms = arms.ToImmutableArray()
                };
            });

    public static Gen<TryExpression> TryExpressionExpr(GenContext ctx) =>
        Gen.Select(
            Expression(ctx),
            Gen.Null(GenTypes.SimpleType),
            (operand, exType) => new TryExpression
            {
                Operand = operand,
                ExceptionType = exType
            });

    public static Gen<MaybeExpression> MaybeExpressionExpr(GenContext ctx) =>
        Expression(ctx).Select(operand => new MaybeExpression { Operand = operand });

    public static Gen<StarExpression> StarExpressionExpr(GenContext ctx) =>
        IdentifierExpr(ctx).Select(id => new StarExpression { Operand = id });

    public static Gen<SpreadElement> SpreadElementExpr(GenContext ctx) =>
        Expression(ctx).Select(value => new SpreadElement { Value = value });

    public static Gen<ModifiedArgument> ModifiedArgumentExpr(GenContext ctx) =>
        Gen.Select(
            Gen.OneOfConst(ParameterModifier.Ref, ParameterModifier.Out, ParameterModifier.In),
            IdentifierExpr(ctx),
            (mod, id) => new ModifiedArgument
            {
                Modifier = mod,
                Argument = id
            });

    public static Gen<AwaitExpression> AwaitExpressionExpr(GenContext ctx) =>
        Expression(ctx).Select(operand => new AwaitExpression { Operand = operand });

    public static Gen<SuperExpression> SuperExpressionExpr() =>
        Gen.Const(new SuperExpression());
}
