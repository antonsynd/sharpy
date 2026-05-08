using System.Collections.Immutable;
using CsCheck;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Tests.Properties.Generators;

internal static class GenPatterns
{
    public static Gen<Pattern> Pattern(GenContext ctx) =>
        ctx.HasFuel
            ? Gen.Frequency(
                (3, Leaf(ctx)),
                (2, Composite(ctx.Burn())))
            : Leaf(ctx);

    private static Gen<Pattern> Leaf(GenContext ctx) =>
        Gen.OneOf<Pattern>(
            Wildcard(),
            Binding(ctx),
            LiteralPat(),
            MemberAccessPat());

    private static Gen<Pattern> Composite(GenContext ctx) =>
        Gen.OneOf<Pattern>(
            TuplePat(ctx),
            ListPat(ctx),
            OrPat(ctx),
            TypePat(),
            PositionalPat(ctx),
            AndPat(ctx),
            GuardPat(ctx),
            RelationalPat(),
            PropertyPat(ctx),
            UnionCasePat(ctx));

    public static Gen<WildcardPattern> Wildcard() =>
        Gen.Const(new WildcardPattern());

    public static Gen<BindingPattern> Binding(GenContext ctx) =>
        GenIdentifier.Name.Select(n => new BindingPattern
        {
            Name = new Identifier { Name = n }
        });

    public static Gen<LiteralPattern> LiteralPat() =>
        GenLiterals.AnyLiteral.Where(e => e is IntegerLiteral or StringLiteral or BooleanLiteral or NoneLiteral)
            .Select(e => new LiteralPattern { Literal = e });

    public static Gen<TypePattern> TypePat() =>
        GenIdentifier.ClassName.Select(name => new TypePattern
        {
            Type = new TypeAnnotation { Name = name }
        });

    public static Gen<TuplePattern> TuplePat(GenContext ctx) =>
        Pattern(ctx.Burn()).Array[2, 4].Select(elems =>
            new TuplePattern { Elements = elems.ToImmutableArray() });

    public static Gen<ListPattern> ListPat(GenContext ctx) =>
        Pattern(ctx.Burn()).Array[0, 3].Select(elems =>
            new ListPattern { Elements = elems.ToImmutableArray() });

    public static Gen<OrPattern> OrPat(GenContext ctx) =>
        Pattern(ctx.Burn()).Array[2, 4].Select(alts =>
            new OrPattern { Alternatives = alts.ToImmutableArray() });

    public static Gen<MemberAccessPattern> MemberAccessPat() =>
        Gen.Select(GenIdentifier.ClassName, GenIdentifier.Name,
            (cls, member) => new MemberAccessPattern
            {
                Parts = ImmutableArray.Create(cls, member)
            });

    public static Gen<PositionalPattern> PositionalPat(GenContext ctx) =>
        Gen.Select(GenIdentifier.ClassName, Pattern(ctx.Burn()).Array[1, 3],
            (name, elems) => new PositionalPattern
            {
                Type = new TypeAnnotation { Name = name },
                Elements = elems.ToImmutableArray()
            });

    public static Gen<AndPattern> AndPat(GenContext ctx) =>
        Gen.Select(
            Pattern(ctx.Burn()),
            Pattern(ctx.Burn()),
            (left, right) => new AndPattern { Left = left, Right = right });

    public static Gen<GuardPattern> GuardPat(GenContext ctx) =>
        Gen.Select(
            Pattern(ctx.Burn()),
            GenExpressions.Expression(ctx.Burn()),
            (inner, guard) => new GuardPattern { Inner = inner, Guard = guard });

    public static Gen<RelationalPattern> RelationalPat() =>
        Gen.Select(
            Gen.OneOfConst(RelationalOperator.GreaterThan, RelationalOperator.GreaterThanOrEqual,
                RelationalOperator.LessThan, RelationalOperator.LessThanOrEqual),
            GenLiterals.Integer.Select(x => (Expression)x),
            (op, val) => new RelationalPattern { Operator = op, Value = val });

    public static Gen<PropertyPattern> PropertyPat(GenContext ctx) =>
        Gen.Select(
            Gen.Null(GenIdentifier.ClassName.Select(n => new TypeAnnotation { Name = n })),
            PropertyPatternFieldGen(ctx).Array[1, 3],
            (type, fields) => new PropertyPattern
            {
                Type = type,
                Fields = fields.ToImmutableArray()
            });

    private static Gen<PropertyPatternField> PropertyPatternFieldGen(GenContext ctx) =>
        Gen.Select(
            GenIdentifier.Name,
            Pattern(ctx.Burn()),
            (name, pat) => new PropertyPatternField { Name = name, Pattern = pat });

    public static Gen<UnionCasePattern> UnionCasePat(GenContext ctx) =>
        Gen.Select(
            GenIdentifier.ClassName,
            Pattern(ctx.Burn()).Array[0, 2],
            (caseName, fields) => new UnionCasePattern
            {
                CaseName = caseName,
                FieldPatterns = fields.ToImmutableArray()
            });
}
