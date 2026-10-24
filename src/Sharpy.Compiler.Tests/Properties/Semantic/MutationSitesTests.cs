using CsCheck;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Tests.Properties.Generators.Typed;
using Xunit;
using Xunit.Abstractions;
using Module = Sharpy.Compiler.Parser.Ast.Module;

namespace Sharpy.Compiler.Tests.Properties.Semantic;

/// <summary>
/// The mutation generator's site enumerator (#2324, P63b Phase 1 Task 2): every (position, family) cell
/// of the plan's matrix has sites in the raw corpus of the base the lanes break, and every site is a node
/// the rewriter can replace. The table (position × family → sites per sample, slot types the generator's
/// facts could not name) is printed per base.
/// </summary>
[Trait("Category", "Property")]
public class MutationSitesTests
{
    private const ulong Seed = 2324;
    private const int Samples = 40;

    private readonly ITestOutputHelper _output;

    public MutationSitesTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>The matrix: each base and the (position, family) cells its programs must carry — the
    /// positions the plan's Defect Class names, over the generators that emit them. Literal, never derived
    /// from the enumerator under test.</summary>
    private static readonly (string Base, (Position Position, string Family)[] Cells)[] Matrix =
    {
        ("TypedStatements", new[]
        {
            (Position.Operand, "Arithmetic"), (Position.Operand, "Comparison"), (Position.Operand, "Equality"),
            (Position.Operand, "Logical"), (Position.Operand, "TruthTest"), (Position.Operand, "ConditionalBranch"),
            (Position.Argument, "OverloadedBuiltin"), (Position.Call, "StrMethod"), (Position.MemberName, "StrMethod"),
            (Position.Name, "Plain"), (Position.Annotation, "Declaration"), (Position.Store, "AnnotatedDeclaration"),
            (Position.Store, "Reassignment"), (Position.ReturnValue, "ImplicitNoneDef"),
        }),
        ("TypedCollections", new[] { (Position.Store, "ListInitializer"), (Position.Annotation, "TypeArgument") }),
        ("TypedOptionals", new[] { (Position.Operand, "Identity") }),
        ("ListSubscripts", new[] { (Position.Operand, "Index") }),
        ("Functions", new[]
        {
            (Position.Annotation, "Parameter"), (Position.Annotation, "Return"), (Position.ReturnValue, "Return"),
            (Position.Argument, "UserFunction"), (Position.Call, "UserFunction"),
        }),
        ("Classes", new[]
        {
            (Position.Argument, "Constructor"), (Position.Argument, "SuperInit"), (Position.Call, "Constructor"),
            (Position.Call, "SuperInit"), (Position.Call, "Method"), (Position.MemberName, "Method"),
            (Position.MemberName, "FieldStore"), (Position.Annotation, "BaseClass"), (Position.Annotation, "Field"),
            (Position.ReturnValue, "MethodReturn"),
        }),
        ("BinaryOperatorProgram", new[] { (Position.Operand, "Arithmetic"), (Position.MemberName, "OperatorDunder"), (Position.Argument, "Constructor") }),
        ("UnaryOperatorProgram", new[] { (Position.Operand, "Unary"), (Position.MemberName, "OperatorDunder") }),
        ("ComparisonOperatorProgram", new[] { (Position.MemberName, "OperatorDunder"), (Position.ReturnValue, "DunderReturn") }),
        ("FStringWithArithmetic", new[] { (Position.Name, "FStringHole"), (Position.Operand, "FStringHole") }),
        ("FStringWithFormatSpecs", new[] { (Position.Operand, "FStringNumericSpec") }),
        ("FStringWithMethodCalls", new[] { (Position.MemberName, "FStringMethod") }),
        ("FStringWithNestedCalls", new[] { (Position.Argument, "OverloadedBuiltin"), (Position.Call, "StrBuiltin") }),
        ("ContextManagerWithAsBinding", new[] { (Position.Name, "WithTarget"), (Position.MemberName, "Method") }),
        ("MatchWithGuards", new[] { (Position.Operand, "Guard"), (Position.Pattern, "WildcardLast") }),
        ("MatchWithOrPatterns", new[] { (Position.Pattern, "Or"), (Position.Pattern, "Literal"), (Position.Pattern, "WildcardLast") }),
        ("MatchWithListPatterns", new[] { (Position.Pattern, "List") }),
        ("MatchWithAndPatterns", new[] { (Position.Pattern, "And") }),
        ("MatchWithTypePatterns", new[] { (Position.Annotation, "PatternHead"), (Position.Pattern, "Class") }),
        ("MatchWithNestedPatterns", new[] { (Position.Pattern, "Tuple") }),
        ("CovariantInterfaceProgram", new[] { (Position.Annotation, "BaseClass"), (Position.Annotation, "TypeArgument") }),
        ("GenericFunctionProgram", new[] { (Position.Argument, "ExplicitGeneric"), (Position.Annotation, "Parameter") }),
        ("GenericWithInheritanceProgram", new[] { (Position.Annotation, "BaseClass"), (Position.Annotation, "TypeArgument") }),
        ("ModuleWithInterfaceHierarchy", new[] { (Position.Annotation, "BaseInterface") }),
        ("ModuleWithGenerator", new[] { (Position.ReturnValue, "Yield"), (Position.Annotation, "Return") }),
        ("ModuleWithYieldFrom", new[] { (Position.ReturnValue, "YieldFrom") }),
        ("ModuleWithTryExcept", new[] { (Position.Annotation, "Handler") }),
        ("ModuleWithDataclass", new[] { (Position.Argument, "Dataclass"), (Position.Call, "Dataclass") }),
    };

    [Fact]
    public void EveryMatrixCell_HasSites_InTheRawCorpusOfItsBase()
    {
        var bases = MutationLanes.Census
            .GroupBy(r => r.Base, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().Generator, StringComparer.Ordinal);
        var missing = new List<string>();
        int cellsTested = 0;

        foreach (var (baseLabel, cells) in Matrix)
        {
            Assert.True(bases.ContainsKey(baseLabel), $"the matrix names base '{baseLabel}', which no census row samples");
            var counts = new Dictionary<(Position, string), int>();
            var unknown = new Dictionary<Position, int>();
            foreach (var module in Corpus(bases[baseLabel]()))
            {
                foreach (var site in MutationSites.Enumerate(module))
                {
                    counts[(site.Position, site.Family)] = counts.GetValueOrDefault((site.Position, site.Family)) + 1;
                    if (site.SlotType is null)
                        unknown[site.Position] = unknown.GetValueOrDefault(site.Position) + 1;
                }
            }

            _output.WriteLine($"{baseLabel} ({Samples} raw samples, seed {Seed}):");
            foreach (var ((position, family), count) in counts.OrderBy(c => c.Key.Item1).ThenBy(c => c.Key.Item2, StringComparer.Ordinal))
                _output.WriteLine($"  {position,-12} {family,-22} {count / (double)Samples,6:F2} sites/sample");
            foreach (var (position, count) in unknown.OrderBy(u => u.Key))
                _output.WriteLine($"  unknown-slot {position,-12} {count / (double)Samples,6:F2}/sample");

            foreach (var cell in cells)
            {
                cellsTested++;
                if (counts.GetValueOrDefault(cell) == 0)
                    missing.Add($"{baseLabel}: no {cell.Position}/{cell.Family} site in {Samples} raw samples");
            }
        }

        Assert.True(cellsTested > 0, "the matrix named no cell");
        Assert.True(missing.Count == 0, $"{missing.Count} matrix cell(s) have no site:\n  " + string.Join("\n  ", missing));
    }

    /// <summary>Every enumerated site is replaceable: its target is a node <see cref="AstRewriter.Replace"/>
    /// re-parents (a site the rewriter cannot reach would make its lane throw, not mutate).</summary>
    [Fact]
    public void EverySite_IsANodeTheRewriterCanReplace()
    {
        var failures = new List<string>();
        int sitesTested = 0;
        foreach (var (baseLabel, generator) in MutationLanes.Census.GroupBy(r => r.Base).Select(g => (g.Key, g.First().Generator)))
        {
            foreach (var module in Corpus(generator()).Take(10))
            {
                foreach (var site in MutationSites.Enumerate(module))
                {
                    sitesTested++;
                    if (Record.Exception(() => AstRewriter.Replace(module, site.Target, site.Target with { })) is { } unreachable)
                        failures.Add($"{baseLabel} {site.Position}/{site.Family} ({site.Describe}): {unreachable.Message}");
                }
            }
        }
        _output.WriteLine($"{sitesTested} sites replaced");
        Assert.True(sitesTested > 0, "no site was enumerated");
        Assert.True(failures.Count == 0, $"{failures.Count} unreplaceable site(s):\n  " + string.Join("\n  ", failures.Take(10)));
    }

    /// <summary><see cref="Samples"/> raw samples of <paramref name="gen"/> at the fixed seed, normalized.</summary>
    private static List<Module> Corpus(Gen<Module> gen)
    {
        var pcg = new PCG(1u, Seed);
        var corpus = new List<Module>(Samples);
        for (int i = 0; i < Samples; i++)
            corpus.Add(AstRewriter.Normalize(gen.Generate(pcg, null, out _)));
        return corpus;
    }
}
