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
            PositionalPat(ctx));

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
}
