extern alias SharpyRT;

using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Lowering;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Tests.Helpers;
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
/// builtin of the same name, not an alias (<c>IOError is OSError</c>), that constructs from the one
/// argument Core's constructor takes (<c>T("x")</c>, or <c>T(2)</c> for <c>SystemExit</c>).
/// Each pair is crossed with clause order {base first, derived first} × raised instance {derived,
/// base} × spelling of the second clause {<c>except T:</c>, <c>except T as e:</c>,
/// <c>except (T, U):</c> with a <c>U</c> unrelated to the derived type}: 12 cells per pair, one <c>try</c>
/// each, in ONE program compiled and run once under Sharpy and once under python3 (one
/// <see cref="PythonBatchOracle"/> batch, after one probe batch that settles the builtin set).</para>
/// <para><b>One generated refusal.</b> Sharpy has no <c>BaseException</c>: a type Core marks
/// <c>[PythonBaseException]</c> (<c>SystemExit</c>) is caught by <c>except Exception:</c>, so a later
/// handler naming it, which python runs, cannot run in Sharpy and is refused (SPY0709;
/// <c>docs/deviations.yaml</c> except-exception-catches-systemexit). Those base-first cells are
/// compiled on their own and must each draw SPY0709; their derived-first twins (the handler moved
/// first) are in the run matrix and print what python prints.</para>
/// <para>Generic exception types have no python spelling (<c>except Box[int]:</c> is a TypeError
/// there), so <see cref="ExceptHandlerOrder_GenericHierarchy_RunsFirstMatchWins"/> crosses them with
/// their own expected verdict: the first clause whose type the raised instance is an instance of.</para>
/// <para>Both facts also assert the validator's warned handlers are exactly the handlers the lowering
/// leaves out (<see cref="AssertWarnedEqualsPruned"/>): the warning and the omission are one verdict.</para>
/// <para>Skips when no python3 ≥ 3.15 (the .python-version pin) answers, except under <c>SHARPY_REQUIRE_PYTHON_ORACLE=1</c> (CI).</para>
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
            name, arg = name
            cls = getattr(builtins, name, None)
            if not (isinstance(cls, type) and issubclass(cls, BaseException) and cls.__name__ == name):
                continue
            try:
                cls(eval(arg))
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
    /// <param name="DerivedIsPythonBaseException">Core marks the derived type <c>[PythonBaseException]</c>
    /// (python roots it at <c>BaseException</c>).</param>
    private sealed record Pair(string Base, string Derived, string Unrelated, bool UnrelatedCaughtByBase,
        string BaseArgument, string DerivedArgument, bool DerivedIsPythonBaseException);

    private sealed record Cell(int Id, Pair Pair, Order Order, Raised Raised, Spelling Spelling)
    {
        public string First => Order == Order.BaseFirst ? Pair.Base : Pair.Derived;
        public string Second => Order == Order.BaseFirst ? Pair.Derived : Pair.Base;

        /// <summary>The second clause is dead in Sharpy: the first clause's type covers every type it names.</summary>
        public bool SecondIsDead => Order == Order.BaseFirst && (Spelling != Spelling.Tuple || Pair.UnrelatedCaughtByBase);

        /// <summary>The generated refusal: a python-<c>BaseException</c> handler after <c>except Exception:</c>.</summary>
        public bool IsRefused => Order == Order.BaseFirst && Pair.DerivedIsPythonBaseException;
    }

    /// <summary>Every public, concrete, non-generic Core exception type with a one-argument constructor
    /// (<see cref="ArgumentFor"/>), plus <c>System.Exception</c>.</summary>
    private static IReadOnlyList<ClrType> CandidateTypes() =>
        typeof(SharpyRT::Sharpy.KeyError).Assembly.GetExportedTypes()
            .Where(t => typeof(Exception).IsAssignableFrom(t) && !t.IsAbstract && !t.IsGenericTypeDefinition
                && ArgumentFor(t) != null)
            .Append(typeof(Exception))
            .OrderBy(t => t.FullName, StringComparer.Ordinal)
            .ToArray();

    /// <summary>The source literal both languages construct the type from: a <c>str</c> when a
    /// one-argument constructor takes one, else an <c>int</c> (<c>SystemExit(code)</c>); null when neither.</summary>
    private static string? ArgumentFor(ClrType type)
    {
        var single = type.GetConstructors().Where(c => c.GetParameters().Length == 1)
            .Select(c => c.GetParameters()[0].ParameterType).ToList();
        if (single.Any(p => p.IsAssignableFrom(typeof(string))))
            return "\"x\"";
        if (single.Any(p => p == typeof(int)))
            return "2";
        return null;
    }

    /// <summary>The compiler's rule: the type or a base carries the (non-inherited) marker.</summary>
    private static bool IsPythonBaseException(ClrType type)
    {
        for (var t = type; t != null; t = t.BaseType)
        {
            if (t.IsDefined(typeof(SharpyRT::Sharpy.PythonBaseExceptionAttribute), inherit: false))
                return true;
        }
        return false;
    }

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
                        baseType.IsAssignableFrom(byName[unrelated]), ArgumentFor(baseType)!, ArgumentFor(derived)!,
                        IsPythonBaseException(derived)));
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
                var raised = c.Raised == Raised.Derived
                    ? c.Pair.Derived + "(" + c.Pair.DerivedArgument + ")"
                    : c.Pair.Base + "(" + c.Pair.BaseArgument + ")";
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
                sb.Append("            raise ").Append(raised).Append('\n');
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

    private static HashSet<string> ProbePythonBuiltins(PythonBatchOracle oracle, IEnumerable<(string Name, string Argument)> names)
    {
        var pairs = names.Select(n => new[] { n.Name, n.Argument }).ToArray();
        var script = ProbeScript.Replace("__NAMES__", JsonSerializer.Serialize(pairs), StringComparison.Ordinal);
        var verdicts = oracle.RunBatch(new[] { new PyRequest(0, script) });
        Assert.True(verdicts.TryGetValue(0, out var probe) && probe.Ok,
            "python3 did not run the builtin-exception probe: " + (probe?.Stderr ?? "no verdict"));
        return JsonSerializer.Deserialize<string[]>(probe!.Stdout.Trim())!.ToHashSet(StringComparer.Ordinal);
    }

    [SkippableFact]
    public void ExceptHandlerOrder_EveryHierarchyPair_MatchesCPython()
    {
        // The oracle minor is the repository's .python-version pin (3.15).
        var oracle = PythonBatchOracle.Over(PythonInterpreter.RequireOrSkip(minMinor: 15));

        var types = CandidateTypes();
        var builtins = ProbePythonBuiltins(oracle, types.Select(t => (SharpyName(t), ArgumentFor(t)!)));
        var pairs = GeneratePairs(types, builtins);
        var cells = GenerateCells(pairs);
        Output.WriteLine($"{types.Count} candidate types, {builtins.Count} python builtins, {pairs.Count} pairs, {cells.Count} cells");

        // Positive controls: the generator reaches the #2363 cells, so the matrix cannot pass vacuously.
        Assert.True(pairs.Count >= 20, $"only {pairs.Count} (base, derived) pairs were generated");
        Assert.Contains(pairs, p => p.Base == "LookupError" && p.Derived == "KeyError");
        Assert.Contains(pairs, p => p.Base == "OSError" && p.Derived == "FileNotFoundError");
        Assert.Contains(pairs, p => p.Base == "Exception" && p.Derived == "ValueError");
        Assert.Contains(pairs, p => p.Base == "Exception" && p.DerivedIsPythonBaseException);

        var source = ProgramFor(cells);
        var refusedCells = cells.Where(c => c.IsRefused).ToList();
        var runCells = cells.Where(c => !c.IsRefused).ToList();
        var runSource = ProgramFor(runCells);

        var py = oracle.RunBatch(new[] { new PyRequest(0, source + "\nmain()\n") });
        Assert.True(py.TryGetValue(0, out var pyResult) && pyResult.Ok,
            "python3 did not run the handler-order matrix: " + (pyResult?.Stderr ?? "no verdict"));
        var pythonById = ParseLines(pyResult!.Stdout);
        Assert.Equal(cells.Count, pythonById.Count);

        var run = CompileAndExecute(runSource, "except_order_matrix.spy", executionTimeoutMs: 120_000);
        Assert.True(RanToCompletion(run), JudgeRun(run, runSource, "Sharpy handler-order matrix") ?? "timed out");
        var sharpyById = ParseLines(run.StandardOutput);

        var divergences = new List<string>();
        foreach (var cell in runCells)
        {
            var python = pythonById[cell.Id];
            var expected = python;
            var actual = sharpyById.TryGetValue(cell.Id, out var s) ? s : "<no line>";
            if (!string.Equals(expected, actual, StringComparison.Ordinal))
            {
                divergences.Add($"  {cell.Pair.Base}/{cell.Pair.Derived} {cell.Order} raise {cell.Raised} "
                    + $"second={cell.Spelling}: python {python}, expected {expected}, sharpy {actual}");
            }
        }
        Assert.True(divergences.Count == 0,
            $"{divergences.Count} of {runCells.Count} handler-order cells diverge from CPython:\n" + string.Join("\n", divergences));

        // The python column reaches both answers: a dead second clause, and a live one that runs.
        Assert.Contains(cells, c => c.SecondIsDead && pythonById[c.Id] == "first");
        Assert.Contains(cells, c => pythonById[c.Id] == "second");
        Assert.DoesNotContain(cells, c => pythonById[c.Id] == "escaped");
        // The refusal is real in python (it runs the SystemExit handler) and generated, not listed.
        Assert.Contains(refusedCells, c => c.Raised == Raised.Derived && pythonById[c.Id] == "second");

        // Exactly one SPY0488 per dead second clause, and none for a live or a partly covered one.
        var deadClauses = runCells.Count(c => c.SecondIsDead);
        var warned = run.RawDiagnostics.Where(d => d.Code == DiagnosticCodes.Validation.UnreachableArm).ToList();
        Assert.Equal(deadClauses, warned.Count);
        Assert.DoesNotContain(run.RawDiagnostics, d => d.Code == DiagnosticCodes.ValidationOverflow.BaseExceptionHandlerAfterException);

        // Every refused cell draws SPY0709, and nothing reaches Roslyn.
        var refusedSource = ProgramFor(refusedCells);
        var refusedRun = CompileAndExecute(refusedSource, "except_order_refused.spy");
        Assert.False(refusedRun.Success, "a SystemExit handler after `except Exception:` is refused");
        Assert.True(refusedCells.Count > 0, "no SystemExit-after-Exception clause was generated");
        Assert.Equal(refusedCells.Count,
            refusedRun.RawDiagnostics.Count(d => d.Code == DiagnosticCodes.ValidationOverflow.BaseExceptionHandlerAfterException));
        Assert.DoesNotContain(refusedRun.RawDiagnostics, d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError);

        AssertWarnedEqualsPruned(runSource);
    }

    // ---- generic exception types: no python spelling, their own expected verdict -------------------

    /// <summary>The declared generic hierarchy: each type's Sharpy spelling and its supertypes (itself included).</summary>
    private static readonly (string Name, string[] IsA)[] GenericTypes =
    {
        ("AppError", new[] { "AppError" }),
        ("Box[int]", new[] { "Box[int]", "AppError" }),
        ("Box[str]", new[] { "Box[str]", "AppError" }),
        ("SubBox[int]", new[] { "SubBox[int]", "Box[int]", "AppError" }),
    };

    private const string GenericPrelude =
        "class AppError(Exception):\n    pass\n\n"
        + "class Box[T](AppError):\n    pass\n\n"
        + "class SubBox[T](Box[T]):\n    pass\n\n";

    /// <summary>
    /// Every ordered pair of distinct types of a generic exception hierarchy (a generic class under a
    /// non-generic base, two closed instantiations of one definition, a generic subclass of a generic
    /// base) × raised {first type, second type}: 24 cells. The expected line is the first clause whose
    /// type the raised instance is an instance of; a second clause whose type is an instance of the
    /// first's is warned (SPY0488) and left out, never SPY0908 CS0160.
    /// </summary>
    [Fact]
    public void ExceptHandlerOrder_GenericHierarchy_RunsFirstMatchWins()
    {
        var cells = new List<(int Id, int First, int Second, int Raised)>();
        for (var first = 0; first < GenericTypes.Length; first++)
            for (var second = 0; second < GenericTypes.Length; second++)
                if (first != second)
                    foreach (var raised in new[] { first, second })
                        cells.Add((cells.Count, first, second, raised));

        var sb = new StringBuilder(GenericPrelude).Append("def main() -> None:\n");
        foreach (var (id, first, second, raised) in cells)
        {
            sb.Append("    try:\n        raise ").Append(GenericTypes[raised].Name).Append("(\"x\")\n");
            sb.Append("    except ").Append(GenericTypes[first].Name).Append(":\n        print(").Append(id).Append(", \"first\")\n");
            sb.Append("    except ").Append(GenericTypes[second].Name).Append(":\n        print(").Append(id).Append(", \"second\")\n");
        }
        var source = sb.ToString();

        bool IsA(int instance, int type) => GenericTypes[instance].IsA.Contains(GenericTypes[type].Name);

        var run = CompileAndExecute(source, "except_order_generic.spy");
        Assert.True(RanToCompletion(run), JudgeRun(run, source, "Sharpy generic handler-order matrix") ?? "timed out");
        var byId = ParseLines(run.StandardOutput);

        var wrong = new List<string>();
        foreach (var c in cells)
        {
            var expected = IsA(c.Raised, c.First) ? "first" : "second";
            var actual = byId.TryGetValue(c.Id, out var a) ? a : "<no line>";
            if (actual != expected)
            {
                wrong.Add($"  except {GenericTypes[c.First].Name} / except {GenericTypes[c.Second].Name}, raise "
                    + $"{GenericTypes[c.Raised].Name}: expected {expected}, sharpy {actual}");
            }
        }
        Assert.True(wrong.Count == 0, $"{wrong.Count} of {cells.Count} generic cells wrong:\n" + string.Join("\n", wrong));

        // Second clauses an instance of the first: (AppError, Box[int]), (AppError, Box[str]),
        // (AppError, SubBox[int]), (Box[int], SubBox[int]), each raised two ways.
        var dead = cells.Count(c => IsA(c.Second, c.First));
        Assert.Equal(8, dead);
        Assert.Equal(dead, run.RawDiagnostics.Count(d => d.Code == DiagnosticCodes.Validation.UnreachableArm));

        AssertWarnedEqualsPruned(source);
    }

    /// <summary>
    /// The validator's SPY0488 handlers and the lowering's left-out handlers are the same set, located
    /// by the handler's exception-type position (or the <c>except</c> keyword for a bare handler).
    /// </summary>
    private void AssertWarnedEqualsPruned(string source)
    {
        using var helper = new ProjectCompilationHelper(Output);
        helper.WithRootNamespace("Test").AddSourceFile("main.spy", source).CreateProjectFile();
        var result = helper.Compile();
        Assert.True(result.Success, "the matrix compiles as a project:\n"
            + string.Join("\n", result.Diagnostics.GetErrors().Select(d => $"[{d.Code}] {d.Message}")));

        var warned = result.Diagnostics.GetAll()
            .Where(d => d.Code == DiagnosticCodes.Validation.UnreachableArm)
            .Select(d => (d.Line ?? 0, d.Column ?? 0))
            .OrderBy(p => p).ToList();

        var ir = result.ProjectModel!.IrCompilation!;
        var pruned = new List<(int, int)>();
        foreach (var (node, irNode) in ir.Index)
        {
            if (node is not TryStatement tryStmt || irNode is not IrPrunedTry prunedTry)
                continue;
            for (var i = 0; i < tryStmt.Handlers.Length; i++)
            {
                if (prunedTry.Handlers[i].Emitted)
                    continue;
                var handler = tryStmt.Handlers[i];
                pruned.Add((handler.ExceptionType?.LineStart ?? handler.LineStart,
                    handler.ExceptionType?.ColumnStart ?? handler.ColumnStart));
            }
        }
        pruned.Sort();

        Assert.NotEmpty(warned);
        Assert.Equal(warned, pruned);
    }

}
