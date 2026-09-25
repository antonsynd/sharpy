using System.Collections.Immutable;
using System.Reflection;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Pretty;
using Xunit;
using SLexer = Sharpy.Compiler.Lexer.Lexer;
using SParser = Sharpy.Compiler.Parser.Parser;

namespace Sharpy.Compiler.Tests.Properties.Unparser;

/// <summary>
/// The structural comparer is total over <see cref="FStringPart"/> (#2024): every public property is
/// either COMPARED — two parts differing only in it are unequal — or named EXEMPT with a reason. The
/// round-trip harnesses (<c>UnparserFixtureRoundTripTests</c>) were blind to <c>ExpressionText</c>, so
/// the formatter re-spelling a t-string hole passed them; a property added later without a comparer
/// arm fails here, not silently in a round trip.
/// </summary>
[Trait("Category", "Property")]
public class FStringPartPropertyCoverageTests
{
    private static readonly StructuralEqualityComparer Comparer = StructuralEqualityComparer.Instance;

    /// <summary>Property → two values that must compare unequal (texts are compared when both sides carry one).</summary>
    private static readonly Dictionary<string, (object? A, object? B)> Compared = new()
    {
        [nameof(FStringPart.Text)] = ("a", "b"),
        [nameof(FStringPart.Expression)] = (new Identifier { Name = "x" }, new Identifier { Name = "y" }),
        [nameof(FStringPart.Spec)] = (
            (ImmutableArray<FStringPart>?)ImmutableArray.Create(new FStringPart { Text = ">4" }),
            (ImmutableArray<FStringPart>?)ImmutableArray.Create(new FStringPart { Text = ">5" })),
        [nameof(FStringPart.Conversion)] = ((char?)'r', (char?)'s'),
        [nameof(FStringPart.SourceText)] = ("x=", "x ="),
        [nameof(FStringPart.IsSelfDocumenting)] = (true, false),
        [nameof(FStringPart.ExpressionText)] = ("x", " x"),
        [nameof(FStringPart.RawText)] = ("x", " x "),
    };

    /// <summary>Properties deliberately not compared, with the reason. Empty: every property is meaning.</summary>
    private static readonly Dictionary<string, string> Exempt = new();

    private static IEnumerable<PropertyInfo> PublicProperties() =>
        typeof(FStringPart).GetProperties(BindingFlags.Public | BindingFlags.Instance);

    [Fact]
    public void EveryProperty_IsComparedOrNamedExempt()
    {
        var unclassified = PublicProperties()
            .Select(p => p.Name)
            .Where(n => !Compared.ContainsKey(n) && !Exempt.ContainsKey(n))
            .ToList();
        Assert.True(unclassified.Count == 0,
            "FStringPart properties the structural comparer is not known to cover: " + string.Join(", ", unclassified));

        var stale = Compared.Keys.Concat(Exempt.Keys)
            .Where(n => PublicProperties().All(p => p.Name != n))
            .ToList();
        Assert.True(stale.Count == 0, "classified names that are not FStringPart properties: " + string.Join(", ", stale));
    }

    public static TheoryData<string> ComparedProperties()
    {
        var data = new TheoryData<string>();
        foreach (var name in Compared.Keys)
            data.Add(name);
        return data;
    }

    [Theory]
    [MemberData(nameof(ComparedProperties))]
    public void PartsDifferingOnlyInTheProperty_AreUnequal(string name)
    {
        var property = typeof(FStringPart).GetProperty(name)!;
        var (a, b) = Compared[name];

        var left = BasePart();
        var right = BasePart();
        property.SetValue(left, a);
        property.SetValue(right, b);

        Assert.True(Comparer.Equals(Wrap(left), Wrap(left with { })), $"{name}: a part must equal its copy");
        Assert.False(Comparer.Equals(Wrap(left), Wrap(right)), $"{name}: parts differing only in {name} compared equal");
    }

    [Fact]
    public void SourceTexts_AreComparedOnlyWhenBothSidesCarryThem()
    {
        // An AST built without source (generator, fallback unparse) has no captured texts; it must
        // still equal the re-parse of its own unparse.
        var withSource = BasePart();
        var sourceless = withSource with { ExpressionText = null, RawText = null };
        Assert.True(Comparer.Equals(Wrap(withSource), Wrap(sourceless)));
    }

    [Theory]
    [InlineData("t\"{ x}\"", "t\"{x}\"")]     // ExpressionText ' x' vs 'x' (and RawText)
    [InlineData("t\"{x }\"", "t\"{x}\"")]     // ExpressionText equal, RawText 'x ' vs 'x'
    [InlineData("f\"{ x }\"", "f\"{x}\"")]
    public void ParsedHoles_DifferingOnlyInSpelling_AreUnequal(string left, string right)
    {
        Assert.False(Comparer.Equals(ParseValue(left), ParseValue(right)));
        Assert.True(Comparer.Equals(ParseValue(left), ParseValue(left)));
    }

    private static FStringPart BasePart() => new()
    {
        Expression = new Identifier { Name = "x" },
        ExpressionText = "x",
        RawText = "x",
    };

    private static FStringLiteral Wrap(FStringPart part) => new() { Parts = ImmutableArray.Create(part) };

    private static Expression ParseValue(string literal)
    {
        var lexer = new SLexer("v = " + literal + "\n");
        var module = new SParser(lexer.TokenizeAll()).ParseModule();
        return ((Assignment)module.Body[0]).Value;
    }
}
