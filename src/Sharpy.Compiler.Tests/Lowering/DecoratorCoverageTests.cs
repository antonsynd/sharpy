using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Sharpy.Compiler.Lowering;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Tests.Infrastructure;
using Xunit;

namespace Sharpy.Compiler.Tests.Lowering;

/// <summary>
/// Totality of <c>LoweringPass.DecoratorsOf</c> (#2443): decorators are not child nodes, so the
/// lowering walk reaches their arguments only through this switch, and an AST statement with a
/// <c>Decorators</c> property the switch misses would leave its bracket-attribute arguments
/// unlowered — and the emitter's required IR read throws SPY0909 on a valid program. The set of
/// decorated statement types is taken from the AST by reflection, never spelled here.
/// </summary>
public class DecoratorCoverageTests
{
    private const string SiteFile = "LoweringPass.cs";
    private const string SiteMethod = "DecoratorsOf";

    private static IReadOnlyList<Type> DecoratedStatementTypes() => typeof(Statement).Assembly.GetTypes()
        .Where(t => typeof(Statement).IsAssignableFrom(t) && !t.IsAbstract
                    && t.GetProperty("Decorators")?.PropertyType == typeof(ImmutableArray<Decorator>))
        .OrderBy(t => t.Name, StringComparer.Ordinal)
        .ToList();

    [Fact]
    public void EveryDecoratedStatementType_HasItsDecoratorsRead()
    {
        var types = DecoratedStatementTypes();
        Assert.True(types.Count >= 10, $"reflection found only {types.Count} decorated statement types");

        var decorator = new Decorator
        {
            QualifiedParts = ImmutableArray.Create("obsolete"),
            Arguments = ImmutableArray.Create<Expression>(new StringLiteral { Value = "m" }),
            KeywordArguments = ImmutableArray.Create(new KeywordArgument { Name = "k", Value = new IntegerLiteral() }),
            IsBracketAttribute = true,
        };

        var missed = new List<string>();
        foreach (var type in types)
        {
            var statement = (Statement)Activator.CreateInstance(type)!;
            type.GetProperty("Decorators")!.SetValue(statement, ImmutableArray.Create(decorator));
            if (LoweringPass.DecoratorArgumentsOf(statement).Count != 2)
                missed.Add(type.Name);
        }

        Assert.True(missed.Count == 0,
            $"{SiteMethod} misses decorated statement type(s) {string.Join(", ", missed)}: their decorator "
            + "arguments would not be lowered");
    }

    /// <summary>
    /// Scan-vs-roster over the source: the switch's arms in <c>LoweringPass.cs</c> name exactly the
    /// reflected decorated statement types.
    /// </summary>
    [Fact]
    public void DecoratorsOfSwitch_NamesExactlyTheDecoratedTypes()
    {
        var path = Path.Combine(DispatchSiteScan.FindRepoRoot(), "src", "Sharpy.Compiler", "Lowering", SiteFile);
        var source = File.ReadAllText(path);
        var body = Regex.Match(source, SiteMethod + @"\(Statement statement\) => statement switch\s*\{(?<arms>.*?)\};",
            RegexOptions.Singleline);
        Assert.True(body.Success, $"{SiteFile} :: \"{SiteMethod}\" switch not found");

        var arms = Regex.Matches(body.Groups["arms"].Value, @"^\s*(\w+) s =>", RegexOptions.Multiline)
            .Select(m => m.Groups[1].Value)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(DecoratedStatementTypes().Select(t => t.Name).ToList(), arms);
    }
}
