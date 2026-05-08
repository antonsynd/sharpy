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
            IdentifierExpr(ctx));

    private static Gen<Expression> SimpleComposite(GenContext ctx) =>
        Gen.OneOf<Expression>(
            UnaryOpExpr(ctx),
            BinaryOpExpr(ctx),
            ParenthesizedExpr(ctx),
            MemberAccessExpr(ctx),
            FunctionCallExpr(ctx),
            ConditionalExpr(ctx));

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
            WalrusExpr(ctx));

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
}
