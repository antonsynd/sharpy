extern alias SharpyRT;

using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Sharpy.TestInfrastructure.Oracles;
using Xunit;
using Xunit.Abstractions;
using ClrType = System.Type;

namespace Sharpy.Compiler.Tests.Properties.Differential;

/// <summary>
/// The class guard for except-handler order (#2363): a <c>try</c> whose handlers python accepts in any
/// order compiles and runs first-match-wins, as CPython does, and a handler an earlier one covers is
/// warned (SPY0488) and left out instead of reaching Roslyn as CS0160 behind SPY0908.
/// <para><b>Generated, never listed.</b> The pairs are every (base, derived) of Core's exception
/// hierarchy, enumerated by reflection over the <c>Sharpy.Core</c> assembly (the row set of
/// <c>ExceptionHierarchyPythonMroTests</c>) plus <c>Exception</c> itself, kept where python has a
/// builtin of the same name, not an alias (<c>IOError is OSError</c>), that <c>T("x")</c> constructs.
/// Each pair is crossed with clause order {base first, derived first} × raised instance {derived,
/// base} × spelling of the second clause {<c>except T:</c>, <c>except T as e:</c>,
/// <c>except (T, U):</c> with a <c>U</c> unrelated to the derived type}: 12 cells per pair, one <c>try</c>
/// each, in ONE program compiled and run once under Sharpy and once under python3 (one
/// <see cref="PythonBatchOracle"/> batch, after one probe batch that settles the builtin set).</para>
/// <para>Skips when no python3 ≥ 3.12 answers, except under <c>SHARPY_REQUIRE_PYTHON_ORACLE=1</c> (CI).</para>
/// </summary>
public class ExceptHandlerOrderDifferentialTests : IntegrationTestBase
{
    public ExceptHandlerOrderDifferentialTests(ITestOutputHelper output) : base(output) => MuteCompilePath = true;

    /// <summary>
    /// The pair cap. Over it, pairs are taken at an even stride through the ordinal-sorted list
    /// (deterministic; the first and last pair are always kept), so the program stays one compile.
    /// </summary>
    private const int MaxPairs = 150;

    /// <summary>Unrelated-type candidates for the tuple spelling; the first unrelated to both ends of the pair is used.</summary>
    private static readonly string[] UnrelatedCandidates = { "ZeroDivisionError", "KeyError", "TypeError" };

    private const string ProbeScript = """
        import builtins, json
        NAMES = __NAMES__
        ok = []
        for name in NAMES:
            cls = getattr(builtins, name, None)
            if not (isinstance(cls, type) and issubclass(cls, BaseException) and cls.__name__ == name):
                continue
            try:
                cls("x")
            except Exception:
                continue
            ok.append(name)
        print(json.dumps(ok))
        """;

    private enum Order { BaseFirst, DerivedFirst }

    private enum Raised { Derived, Base }

    private enum Spelling { Plain, Bound, Tuple }

    /// <param name="UnrelatedCaughtByBase">The tuple's extra type is a subclass of <paramref name="Base"/> (only
    /// when the base is <c>Exception</c>, which every other type derives from).</param>
    private sealed record Pair(string Base, string Derived, string Unrelated, bool UnrelatedCaughtByBase);

    private sealed record Cell(int Id, Pair Pair, Order Order, Raised Raised, Spelling Spelling)
    {
        public string First => Order == Order.BaseFirst ? Pair.Base : Pair.Derived;
        public string Second => Order == Order.BaseFirst ? Pair.Derived : Pair.Base;

        /// <summary>The second clause is dead in python: the first clause's type covers every type it names.</summary>
        public bool SecondIsDead => Order == Order.BaseFirst && (Spelling != Spelling.Tuple || Pair.UnrelatedCaughtByBase);
    }

    /// <summary>Every public, concrete, non-generic Core exception type with a one-argument constructor a <c>str</c> fits, plus <c>System.Exception</c>.</summary>
    private static IReadOnlyList<ClrType> CandidateTypes() =>
        typeof(SharpyRT::Sharpy.KeyError).Assembly.GetExportedTypes()
            .Where(t => typeof(Exception).IsAssignableFrom(t) && !t.IsAbstract && !t.IsGenericTypeDefinition
                && t.GetConstructors().Any(c => c.GetParameters() is [var only] && only.ParameterType.IsAssignableFrom(typeof(string))))
            .Append(typeof(Exception))
            .OrderBy(t => t.FullName, StringComparer.Ordinal)
            .ToArray();

    private static string SharpyName(ClrType type) => type.Name;

    private static List<Pair> GeneratePairs(IReadOnlyList<ClrType> types, ISet<string> pythonBuiltins)
    {
        var usable = types.Where(t => pythonBuiltins.Contains(SharpyName(t))).ToList();
        var byName = usable.ToDictionary(SharpyName, StringComparer.Ordinal);

        bool Related(ClrType a, ClrType b) => a.IsAssignableFrom(b) || b.IsAssignableFrom(a);

        var pairs = new List<Pair>();
        foreach (var derived in usable)
        {
            foreach (var baseType in usable)
            {
                if (baseType == derived || !baseType.IsAssignableFrom(derived))
                    continue;
                // Unrelated to the derived type and not the base itself; under `Exception` every
                // candidate is a subclass of the base, which makes the base-first tuple wholly dead.
                var unrelated = UnrelatedCandidates.FirstOrDefault(name =>
                    byName.TryGetValue(name, out var u) && u != baseType && !Related(u, derived)
                    && (!Related(u, baseType) || baseType.IsAssignableFrom(u)));
                if (unrelated != null)
                {
                    pairs.Add(new Pair(SharpyName(baseType), SharpyName(derived), unrelated,
                        baseType.IsAssignableFrom(byName[unrelated])));
                }
            }
        }

        pairs.Sort((a, b) => string.CompareOrdinal(a.Base + "/" + a.Derived, b.Base + "/" + b.Derived));
        if (pairs.Count <= MaxPairs)
            return pairs;

        var sampled = new List<Pair>(MaxPairs);
        for (var i = 0; i < MaxPairs; i++)
            sampled.Add(pairs[(int)((long)i * (pairs.Count - 1) / (MaxPairs - 1))]);
        return sampled;
    }

    private static List<Cell> GenerateCells(IReadOnlyList<Pair> pairs)
    {
        var cells = new List<Cell>();
        foreach (var pair in pairs)
            foreach (var order in Enum.GetValues<Order>())
                foreach (var raised in Enum.GetValues<Raised>())
                    foreach (var spelling in Enum.GetValues<Spelling>())
                        cells.Add(new Cell(cells.Count, pair, order, raised, spelling));
        return cells;
    }

    private static string ProgramFor(IReadOnlyList<Cell> cells)
    {
        var sb = new StringBuilder();
        var functions = new List<string>();
        foreach (var group in cells.GroupBy(c => c.Pair))
        {
            var name = "pair_" + functions.Count.ToString(CultureInfo.InvariantCulture);
            functions.Add(name);
            sb.Append("def ").Append(name).Append("() -> None:\n");
            foreach (var c in group)
            {
                var id = c.Id.ToString(CultureInfo.InvariantCulture);
                var raised = c.Raised == Raised.Derived ? c.Pair.Derived : c.Pair.Base;
                var second = c.Spelling switch
                {
                    Spelling.Plain => c.Second,
                    Spelling.Bound => c.Second + " as e",
                    _ => "(" + c.Second + ", " + c.Pair.Unrelated + ")",
                };
                // The outer handler keeps one cell's escaped exception (a reachable clause wrongly
                // left out) a divergent LINE rather than the end of the run.
                sb.Append("    try:\n");
                sb.Append("        try:\n");
                sb.Append("            raise ").Append(raised).Append("(\"x\")\n");
                sb.Append("        except ").Append(c.First).Append(":\n");
                sb.Append("            print(").Append(id).Append(", \"first\")\n");
                sb.Append("        except ").Append(second).Append(":\n");
                sb.Append("            print(").Append(id).Append(", \"second\")\n");
                sb.Append("    except Exception:\n");
                sb.Append("        print(").Append(id).Append(", \"escaped\")\n");
            }
            sb.Append('\n');
        }

        sb.Append("def main() -> None:\n");
        foreach (var f in functions)
            sb.Append("    ").Append(f).Append("()\n");
        return sb.ToString();
    }

    private static Dictionary<int, string> ParseLines(string stdout)
    {
        var byId = new Dictionary<int, string>();
        foreach (var raw in stdout.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            var space = line.IndexOf(' ');
            if (space > 0 && int.TryParse(line[..space], NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
                byId[id] = line[(space + 1)..];
        }
        return byId;
    }

    private static HashSet<string> ProbePythonBuiltins(PythonBatchOracle oracle, IEnumerable<string> names)
    {
        var script = ProbeScript.Replace("__NAMES__", JsonSerializer.Serialize(names.ToArray()), StringComparison.Ordinal);
        var verdicts = oracle.RunBatch(new[] { new PyRequest(0, script) });
        Assert.True(verdicts.TryGetValue(0, out var probe) && probe.Ok,
            "python3 did not run the builtin-exception probe: " + (probe?.Stderr ?? "no verdict"));
        return JsonSerializer.Deserialize<string[]>(probe!.Stdout.Trim())!.ToHashSet(StringComparer.Ordinal);
    }

    [SkippableFact]
    public void ExceptHandlerOrder_EveryHierarchyPair_MatchesCPython()
    {
        var oracle = PythonBatchOracle.Over(PythonInterpreter.RequireOrSkip(minMinor: 12));

        var types = CandidateTypes();
        var builtins = ProbePythonBuiltins(oracle, types.Select(SharpyName).Concat(UnrelatedCandidates).Distinct());
        var pairs = GeneratePairs(types, builtins);
        var cells = GenerateCells(pairs);
        Output.WriteLine($"{types.Count} candidate types, {builtins.Count} python builtins, {pairs.Count} pairs, {cells.Count} cells");

        // Positive controls: the generator reaches the #2363 cells, so the matrix cannot pass vacuously.
        Assert.True(pairs.Count >= 20, $"only {pairs.Count} (base, derived) pairs were generated");
        Assert.Contains(pairs, p => p.Base == "LookupError" && p.Derived == "KeyError");
        Assert.Contains(pairs, p => p.Base == "OSError" && p.Derived == "FileNotFoundError");
        Assert.Contains(pairs, p => p.Base == "Exception" && p.Derived == "ValueError");

        var source = ProgramFor(cells);

        var py = oracle.RunBatch(new[] { new PyRequest(0, source + "\nmain()\n") });
        Assert.True(py.TryGetValue(0, out var pyResult) && pyResult.Ok,
            "python3 did not run the handler-order matrix: " + (pyResult?.Stderr ?? "no verdict"));
        var pythonById = ParseLines(pyResult!.Stdout);
        Assert.Equal(cells.Count, pythonById.Count);

        var run = CompileAndExecute(source, "except_order_matrix.spy", executionTimeoutMs: 120_000);
        Assert.True(RanToCompletion(run), JudgeRun(run, source, "Sharpy handler-order matrix") ?? "timed out");
        var sharpyById = ParseLines(run.StandardOutput);

        var divergences = new List<string>();
        foreach (var cell in cells)
        {
            var python = pythonById[cell.Id];
            var actual = sharpyById.TryGetValue(cell.Id, out var s) ? s : "<no line>";
            if (!string.Equals(python, actual, StringComparison.Ordinal))
            {
                divergences.Add($"  {cell.Pair.Base}/{cell.Pair.Derived} {cell.Order} raise {cell.Raised} "
                    + $"second={cell.Spelling}: python {python}, sharpy {actual}");
            }
        }
        Assert.True(divergences.Count == 0,
            $"{divergences.Count} of {cells.Count} handler-order cells diverge from CPython:\n" + string.Join("\n", divergences));

        // The python column reaches both answers: a dead second clause, and a live one that runs.
        Assert.Contains(cells, c => c.SecondIsDead && pythonById[c.Id] == "first");
        Assert.Contains(cells, c => pythonById[c.Id] == "second");
        Assert.DoesNotContain(cells, c => pythonById[c.Id] == "escaped");

        // Exactly one SPY0488 per dead second clause, and none for a live or a partly covered one.
        var deadClauses = cells.Count(c => c.SecondIsDead);
        var warned = run.RawDiagnostics.Count(d => d.Code == DiagnosticCodes.Validation.UnreachableArm);
        Assert.Equal(deadClauses, warned);
    }
}
