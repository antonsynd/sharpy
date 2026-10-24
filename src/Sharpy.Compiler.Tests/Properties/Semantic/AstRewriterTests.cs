using System.Reflection;
using System.Runtime.CompilerServices;
using CsCheck;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Pretty;
using Sharpy.Compiler.Tests.Properties.Generators.Typed;
using Xunit;
using Xunit.Abstractions;
using Module = Sharpy.Compiler.Parser.Ast.Module;

namespace Sharpy.Compiler.Tests.Properties.Semantic;

/// <summary>
/// The mutation generator's rewriter (#2324, P63b Phase 1 Task 1): <see cref="AstRewriter.Replace"/>
/// re-parents every node kind the typed generators emit, a replacement by an equal node changes nothing,
/// a replacement by a different leaf changes the source, and a target outside the tree throws.
/// </summary>
[Trait("Category", "Property")]
public class AstRewriterTests
{
    private const string GeneratorsNamespace = "Sharpy.Compiler.Tests.Properties.Generators.Typed";
    private const ulong Seed = 2324;
    private const int CorpusSize = 200;

    private readonly ITestOutputHelper _output;

    public AstRewriterTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Every node kind in the raw corpus of EVERY public typed generator member (reflection, arguments
    /// synthesized by parameter name and type, 200 samples at a fixed seed, re-parsed) is re-parented by
    /// <see cref="AstRewriter.Replace"/>: the first node of each kind per member is replaced by a copy, and
    /// the rebuilt module holds the copy, no longer holds the original, and is structurally equal to the
    /// input. A kind the rewriter cannot reach throws and is red with the member and source.
    /// </summary>
    [Fact]
    public void Replace_ReparentsEveryNodeKind_InEveryTypedGeneratorMembersRawCorpus()
    {
        var failures = new List<string>();
        var kindsTested = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var unparsed = new List<string>();
        int membersTested = 0;

        foreach (var (label, generator) in Members(failures))
        {
            membersTested++;
            var tested = new HashSet<Type>();
            foreach (var source in RawSources(generator))
            {
                Module parsed;
                try
                {
                    parsed = AstRewriter.Parse(source);
                }
                catch (InvalidOperationException e)
                {
                    unparsed.Add($"{label}: {e.Message}");
                    continue;
                }

                foreach (var node in AstRewriter.Descendants(parsed).Skip(1))
                {
                    if (!tested.Add(node.GetType()))
                        continue;
                    kindsTested[node.GetType().Name] = kindsTested.GetValueOrDefault(node.GetType().Name) + 1;
                    if (Reparent(parsed, node) is { } failure)
                        failures.Add($"{label}: {failure}\n{source}");
                }
            }
        }

        _output.WriteLine($"{membersTested} typed generator members x {CorpusSize} samples (seed {Seed}); {unparsed.Count} sample(s) "
            + $"did not parse (an invalid-program member's own refusal, not the rewriter's); {kindsTested.Count} node kinds re-parented:");
        foreach (var (kind, count) in kindsTested)
            _output.WriteLine($"  {kind,-24} in {count} member corpus(es)");
        foreach (var reason in unparsed.Distinct().Take(5))
            _output.WriteLine($"  did not parse: {reason}");
        Assert.True(membersTested > 0, "no typed generator member was sampled");
        Assert.True(kindsTested.Count > 0, "no node kind was re-parented");
        Assert.True(failures.Count == 0, $"{failures.Count} re-parenting failure(s):\n" + string.Join("\n", failures.Take(5)));
    }

    /// <summary>The input with <paramref name="node"/> replaced by an equal copy: the copy is in the result,
    /// the original is not, and nothing else moved — null, or why not.</summary>
    private static string? Reparent(Module root, Node node)
    {
        var copy = node with { };
        Module rebuilt;
        try
        {
            rebuilt = AstRewriter.Replace(root, node, copy);
        }
        catch (AstRewriter.UnreachableNodeException e)
        {
            return $"{node.GetType().Name}: {e.Message}";
        }
        var reachable = AstRewriter.Descendants(rebuilt).ToList();
        if (!reachable.Any(n => ReferenceEquals(n, copy)))
            return $"{node.GetType().Name}: the copy is not in the rebuilt module";
        if (reachable.Any(n => ReferenceEquals(n, node)))
            return $"{node.GetType().Name}: the original is still in the rebuilt module";
        return StructuralEqualityComparer.Instance.Equals(root, rebuilt)
            ? null
            : $"{node.GetType().Name}: replacing it by an equal copy changed the module";
    }

    /// <summary>
    /// Over <see cref="MutationBases.TypedStatements"/>: every literal leaf replaced by an equal copy leaves
    /// the unparsed source unchanged, and replaced by a literal of another value changes it — so a rewriter
    /// that rebuilt nothing (returned its input) is red here.
    /// </summary>
    [Fact]
    public void Replace_ALeafByAnotherLiteral_ChangesTheSource_AndByAnEqualOne_DoesNot()
    {
        int leavesTested = 0;
        var failures = new List<string>();
        foreach (var source in RawSources(MutationBases.TypedStatements(), count: 50))
        {
            var parsed = AstRewriter.Parse(source);
            var before = Sharpy.Compiler.Pretty.Unparser.Unparse(parsed);
            foreach (var leaf in AstRewriter.Descendants(parsed).OfType<IntegerLiteral>())
            {
                leavesTested++;
                if (Sharpy.Compiler.Pretty.Unparser.Unparse(AstRewriter.Replace(parsed, leaf, leaf with { })) != before)
                    failures.Add($"an equal copy of {leaf.Value} changed the source:\n{before}");
                var changed = AstRewriter.Replace(parsed, leaf, new IntegerLiteral { Value = leaf.Value + "7" });
                if (Sharpy.Compiler.Pretty.Unparser.Unparse(changed) == before)
                    failures.Add($"replacing {leaf.Value} by {leaf.Value}7 left the source unchanged:\n{before}");
            }
        }
        _output.WriteLine($"{leavesTested} integer leaves replaced");
        Assert.True(leavesTested > 0, "no integer leaf was drawn");
        Assert.True(failures.Count == 0, $"{failures.Count} failure(s):\n" + string.Join("\n", failures.Take(3)));
    }

    /// <summary>
    /// A node inside an f-string hole replaced: the source changes. The Unparser writes a parsed hole from
    /// its captured raw text, so a rewriter that rebuilt the hole's part without dropping the capture left
    /// the source unchanged and every f-string lane judged the VALID program (measured before the fix).
    /// </summary>
    [Fact]
    public void Replace_InsideAnFStringHole_ChangesTheSource()
    {
        int holesTested = 0;
        var failures = new List<string>();
        foreach (var source in RawSources(GenFStrings.FStringWithArithmetic(), count: 20)
                     .Concat(RawSources(GenFStrings.FStringWithMethodCalls(), count: 20)))
        {
            var parsed = AstRewriter.Parse(source);
            var before = Sharpy.Compiler.Pretty.Unparser.Unparse(parsed);
            foreach (var hole in AstRewriter.Descendants(parsed).OfType<FStringLiteral>().SelectMany(f => f.Parts).Select(p => p.Expression).OfType<Expression>())
            {
                holesTested++;
                var name = AstRewriter.Descendants(hole).OfType<Identifier>().First();
                var after = Sharpy.Compiler.Pretty.Unparser.Unparse(AstRewriter.Replace(parsed, name, new Identifier { Name = name.Name + "_changed" }));
                if (!after.Contains(name.Name + "_changed", StringComparison.Ordinal))
                    failures.Add($"renaming {name.Name} in a hole left the source unchanged:\n{after}");
            }
        }
        _output.WriteLine($"{holesTested} f-string holes rewritten");
        Assert.True(holesTested > 0, "no f-string hole was drawn");
        Assert.True(failures.Count == 0, $"{failures.Count} failure(s):\n" + string.Join("\n", failures.Take(3)));
    }

    /// <summary>A target that is not in the tree throws — the rewriter never returns a silently unchanged module.</summary>
    [Fact]
    public void Replace_ATargetOutsideTheTree_Throws()
    {
        var module = AstRewriter.Parse("def main():\n    print(1)\n");
        var stranger = new IntegerLiteral { Value = "1" };
        Assert.Throws<AstRewriter.UnreachableNodeException>(() => AstRewriter.Replace(module, stranger, new IntegerLiteral { Value = "2" }));

        // Positive control: the tree's own literal is reachable.
        var own = AstRewriter.Descendants(module).OfType<IntegerLiteral>().Single();
        Assert.Contains("print(2)", Sharpy.Compiler.Pretty.Unparser.Unparse(AstRewriter.Replace(module, own, new IntegerLiteral { Value = "2" })));
    }

    // ---- the typed generator members --------------------------------------------------------------

    /// <summary>Every public static member of a <c>Gen*</c> type in the typed namespace whose result is a
    /// <c>Gen</c> of a module, a source text or a tuple carrying them, with arguments synthesized by
    /// parameter type and name. A parameter the synthesizer cannot fill is a failure, so a new member
    /// shape is never skipped silently.</summary>
    private static IEnumerable<(string Label, object Generator)> Members(List<string> failures)
    {
        var types = typeof(GenTyped).Assembly.GetTypes()
            .Where(t => t.Namespace == GeneratorsNamespace && !t.IsNested && t.Name.StartsWith("Gen", StringComparison.Ordinal))
            .OrderBy(t => t.Name, StringComparer.Ordinal);
        foreach (var type in types)
        {
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static).OrderBy(m => m.ToString(), StringComparer.Ordinal))
            {
                if (GenElement(method.ReturnType) is not { } element || !CarriesSource(element))
                    continue;
                var arguments = new List<object?>();
                foreach (var parameter in method.GetParameters())
                {
                    if (Argument(method, parameter) is { } argument)
                        arguments.Add(argument);
                    else
                        failures.Add($"{type.Name}.{method.Name}: no synthesized argument for {parameter.ParameterType.Name} {parameter.Name}");
                }
                if (arguments.Count != method.GetParameters().Length)
                    continue;
                yield return ($"{type.Name}.{method.Name}({string.Join(", ", arguments)})", method.Invoke(null, arguments.ToArray())!);
            }
        }
    }

    private static object? Argument(MethodInfo method, ParameterInfo parameter)
    {
        var generic = method.Name.Contains("Generic", StringComparison.Ordinal) || method.Name.Contains("Constraint", StringComparison.Ordinal)
            || method.Name.Contains("TypeArg", StringComparison.Ordinal) || method.Name.Contains("MultiTypeParam", StringComparison.Ordinal);
        return parameter.ParameterType switch
        {
            var t when t == typeof(TypeEnv) => generic ? TypeEnv.WithGenerics : TypeEnv.Default,
            var t when t == typeof(int) => parameter.Name == "fuel" ? 2 : 1,
            var t when t == typeof(bool) => true,
            var t when t == typeof(string) => parameter.Name == "dunderName" ? "__len__" : "int",
            _ => null,
        };
    }

    private static Type? GenElement(Type type)
    {
        for (var t = type; t != null; t = t.BaseType)
        {
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Gen<>))
                return t.GetGenericArguments()[0];
        }
        return null;
    }

    private static bool CarriesSource(Type element) =>
        element == typeof(Module) || element == typeof(string)
        || typeof(ITuple).IsAssignableFrom(element) && element.GetGenericArguments().Any(a => a == typeof(string));

    /// <summary>The unparsed sources of <paramref name="count"/> raw samples at the fixed seed.</summary>
    private static List<string> RawSources(object generator, int count = CorpusSize)
    {
        var draw = typeof(AstRewriterTests).GetMethod(nameof(Draw), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(GenElement(generator.GetType())!);
        return (List<string>)draw.Invoke(null, new[] { generator, count })!;
    }

    private static List<string> Draw<T>(Gen<T> gen, int count)
    {
        var pcg = new PCG(1u, Seed);
        var sources = new List<string>();
        for (int i = 0; i < count; i++)
        {
            object? sample = gen.Generate(pcg, null, out _);
            switch (sample)
            {
                case Module m:
                    sources.Add(Sharpy.Compiler.Pretty.Unparser.Unparse(m));
                    break;
                case string s:
                    sources.Add(s);
                    break;
                case ITuple tuple:
                    for (int j = 0; j < tuple.Length; j++)
                    {
                        if (tuple[j] is string text)
                            sources.Add(text);
                    }
                    break;
            }
        }
        return sources;
    }
}
