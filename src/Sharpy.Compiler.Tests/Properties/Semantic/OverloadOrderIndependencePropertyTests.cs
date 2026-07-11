using CsCheck;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Semantic;

/// <summary>
/// Property tests for #1043/#975: overload resolution must be independent of declaration
/// (registration) order across every engine — user functions, methods, and operator dunders.
///
/// <para>
/// Strategy: generate an overload set + a fixed call site, then compile every permutation of the
/// overloads' declaration order and assert the observable result is invariant. Resolution is
/// surfaced into diagnostics by consuming the call result at an explicit <c>str</c> type: the
/// overload whose parameter the argument selects has a known return type, so a different winner
/// changes whether the <c>str</c> annotation type-checks. We compare the (success, sorted
/// diagnostic-code multiset) signature rather than the full generated C#, because permuting the
/// source legitimately reorders the emitted method declarations (a non-resolution difference), and
/// because per-overload line shifts would perturb diagnostic positions.
/// </para>
/// </summary>
[Trait("Category", "Property")]
[Trait("Category", "RandomProperty")]
[Trait("Speed", "Slow")]
[Collection("HeavyCompilation")]
public class OverloadOrderIndependencePropertyTests
{
    private readonly ITestOutputHelper _output;

    public OverloadOrderIndependencePropertyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private enum Engine { Function, Method, Operator }

    // Parameter/argument types with a Sharpy literal for each. bool is non-numeric (no implicit
    // conversion); int→float widening exercises conversion-ranked resolution.
    private static readonly (string Type, string Literal)[] TypePool =
    {
        ("int", "1"),
        ("str", "\"a\""),
        ("bool", "True"),
        ("float", "1.0"),
    };

    // Distinct 2- and 3-element parameter-type sets (order within a set is irrelevant — permutation
    // is applied by the test). The first element's overload returns str; the rest return int.
    private static readonly string[][] ParamSets =
    {
        new[] { "int", "str" },
        new[] { "int", "bool" },
        new[] { "int", "float" },
        new[] { "str", "bool" },
        new[] { "str", "float" },
        new[] { "bool", "float" },
        new[] { "int", "str", "bool" },
        new[] { "int", "str", "float" },
        new[] { "int", "bool", "float" },
        new[] { "str", "bool", "float" },
    };

    private sealed record Scenario(Engine Engine, string[] ParamSet, string ArgLiteral);

    private static Gen<Scenario> GenScenario =>
        from engine in Gen.OneOfConst(Engine.Function, Engine.Method, Engine.Operator)
        from paramSet in Gen.OneOfConst(ParamSets)
        from arg in Gen.OneOfConst(TypePool)
        select new Scenario(engine, paramSet, arg.Literal);

    [Fact]
    public void OverloadResolution_IsDeclarationOrderIndependent()
    {
        GenScenario.Sample(scenario =>
        {
            // Each overload is a fixed (param, return, returnLiteral). The first parameter's overload
            // returns str; the others return int — so the winner's return type is observable.
            var overloads = scenario.ParamSet
                .Select((p, i) => i == 0
                    ? (Param: p, Ret: "str", Lit: "\"r\"")
                    : (Param: p, Ret: "int", Lit: "1"))
                .ToList();

            var permutations = Permutations(overloads).ToList();

            string? baselineSig = null;
            string? baselineSource = null;
            foreach (var order in permutations)
            {
                var source = BuildSource(scenario.Engine, order, scenario.ArgLiteral);
                var sig = ResolutionSignature(source);

                if (baselineSig == null)
                {
                    baselineSig = sig;
                    baselineSource = source;
                    // Determinism: the same source must resolve identically on a repeated compile.
                    var repeat = ResolutionSignature(source);
                    if (repeat != sig)
                        throw new Exception(
                            $"Non-deterministic resolution across repeated compiles:\n{sig}\nvs\n{repeat}\n--- source ---\n{source}");
                }
                else if (sig != baselineSig)
                {
                    throw new Exception(
                        "Overload resolution depends on declaration order.\n" +
                        $"--- order A ---\n{baselineSource}\n=> {baselineSig}\n" +
                        $"--- order B ---\n{source}\n=> {sig}");
                }
            }
        }, iter: 200, print: s => $"{s.Engine} [{string.Join(",", s.ParamSet)}] arg={s.ArgLiteral}");
    }

    /// <summary>
    /// A resolution "signature": compile success plus the sorted multiset of diagnostic codes.
    /// Positions and messages are excluded so that reordering the overload declarations (which
    /// shifts line numbers of any per-overload diagnostic) is not itself a difference; a change in
    /// which overload wins still shows up as a change in the code multiset (the str-typed consumer
    /// errors or not).
    /// </summary>
    private static string ResolutionSignature(string source)
    {
        try
        {
            var compiler = new Sharpy.Compiler.Compiler();
            var result = compiler.Compile(source, "test.spy");
            var codes = (result.Diagnostics?.GetAll() ?? Enumerable.Empty<Sharpy.Compiler.Diagnostics.CompilerDiagnostic>())
                .Select(d => d.Code)
                .OrderBy(c => c, StringComparer.Ordinal);
            return $"success={result.Success};codes=[{string.Join(",", codes)}]";
        }
        catch (Exception ex)
        {
            return $"THREW:{ex.GetType().Name}";
        }
    }

    private static string BuildSource(Engine engine, List<(string Param, string Ret, string Lit)> overloads, string argLiteral)
    {
        var sb = new System.Text.StringBuilder();
        switch (engine)
        {
            case Engine.Function:
                foreach (var o in overloads)
                {
                    sb.AppendLine($"def f(x: {o.Param}) -> {o.Ret}:");
                    sb.AppendLine($"    return {o.Lit}");
                }
                sb.AppendLine("def main() -> None:");
                sb.AppendLine($"    v: str = f({argLiteral})");
                sb.AppendLine("    print(v)");
                break;

            case Engine.Method:
                sb.AppendLine("class C:");
                foreach (var o in overloads)
                {
                    sb.AppendLine($"    def m(self, x: {o.Param}) -> {o.Ret}:");
                    sb.AppendLine($"        return {o.Lit}");
                }
                sb.AppendLine("def main() -> None:");
                sb.AppendLine("    c: C = C()");
                sb.AppendLine($"    v: str = c.m({argLiteral})");
                sb.AppendLine("    print(v)");
                break;

            case Engine.Operator:
                sb.AppendLine("class C:");
                foreach (var o in overloads)
                {
                    sb.AppendLine($"    def __add__(self, x: {o.Param}) -> {o.Ret}:");
                    sb.AppendLine($"        return {o.Lit}");
                }
                sb.AppendLine("def main() -> None:");
                sb.AppendLine("    c: C = C()");
                sb.AppendLine($"    v: str = c + {argLiteral}");
                sb.AppendLine("    print(v)");
                break;
        }
        return sb.ToString();
    }

    /// <summary>All permutations of a small list (overload counts are 2–3, so at most 6).</summary>
    private static IEnumerable<List<T>> Permutations<T>(List<T> items)
    {
        if (items.Count <= 1)
        {
            yield return new List<T>(items);
            yield break;
        }
        for (int i = 0; i < items.Count; i++)
        {
            var rest = new List<T>(items);
            rest.RemoveAt(i);
            foreach (var perm in Permutations(rest))
            {
                perm.Insert(0, items[i]);
                yield return perm;
            }
        }
    }
}
