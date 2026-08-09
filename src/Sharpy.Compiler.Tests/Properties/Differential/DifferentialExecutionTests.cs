using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CsCheck;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Tests.Properties.Generators;
using Sharpy.Compiler.Tests.Properties.Generators.Typed;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Differential;

/// <summary>
/// Differential EXECUTION oracle vs CPython (the runtime-semantics counterpart to
/// <c>CPythonDifferentialParseTests</c>, which is a parse-only oracle).
///
/// <para>Contract: for any program in the shared Sharpy∩Python <em>executable</em> subset, a
/// Sharpy-compiled run produces byte-identical stdout to <c>python3</c> executing the same source
/// (modulo a documented float-formatting tolerance), or the divergence is a filed issue / documented
/// designed deviation captured in the reviewed allowlist — never silent. This closes the gap that let
/// the #1063/#1066/#1070/#1072/#1073/#1085/#1098 class (stable sort, split/replace edges, negative
/// indexing, popitem order, floor division, float repr, …) reach mainline until someone hand-ported a
/// CPython test.</para>
///
/// <para>Entry-point bridge: Sharpy requires a <c>def main()</c> and auto-invokes it, and rejects
/// module-level executable statements (SPY0340/SPY0403 — the <c>module-level-executable-statement</c> /
/// <c>missing-main-function</c> designed deviations). CPython does the opposite: it runs top-level code
/// and does not auto-call <c>main</c>. The two arms therefore share <b>byte-identical program text</b>
/// (every def, annotation, and statement) and differ only in the invocation: the Python arm appends a
/// single <c>main()</c> call so CPython drives the same entry point Sharpy auto-invokes. Nothing in the
/// body is transformed.</para>
///
/// <para>Corpus (three arms): (1) hand-picked programs targeting the historical divergence surface,
/// each verified against <c>python3</c> by hand; (2) <b>every</b> eligible single-file
/// <c>.spy</c>+<c>.expected</c> fixture from <b>both</b> declared roots — the language corpus and
/// the stdlib corpus (#1338) — whose top level is functions-only-with-<c>main</c> (module-level
/// imports allowed) and which passes the shared-subset AST filter; (3) generated
/// well-typed programs reusing <see cref="GenTyped"/>. The Sharpy arm runs each through the
/// production <see cref="IntegrationTestBase.CompileAndExecute"/> path. The Python arm batches the
/// whole corpus through ONE <c>python3</c> that drives
/// <c>build_tools/differential_exec/run_programs.py</c>.</para>
///
/// <para>Coverage is the contract, not a sample (#1202): the fixture arm exhausts its eligible pool
/// on every run, every fixture kept out of that pool is counted under an attributable shape id, and
/// the report states pool size / cells run / coverage % so a run that is narrower than the contract
/// says so in its own output. <c>DIFF_EXEC_LIMIT=&lt;n&gt;</c> requests a labelled subsample for fast
/// local iteration.</para>
///
/// <para>Sweep discipline (Design Decision #10, mirrors <c>InteropConformanceTests</c>): one aggregated
/// JSON report under <c>.claude/tmp/</c>, ratcheted against a reviewed allowlist. Drain-on-fix is
/// enforced on full-pool runs (mirroring the metamorphic sweep): an allowlisted cell that no longer
/// diverges fails the run until its entry is deleted. The test skips (no-op) when a suitable
/// <c>python3</c> is absent; CI pins 3.12.</para>
///
/// <para>Classification is deliberately stderr-blind for Sharpy-ok/CPython-fail cases beyond the
/// missing-name heuristic: every attributable Sharpy-only form is excluded up front at the AST level
/// (the narrow stderr attribution #1202 sketched turned out unnecessary — the AST rules caught every
/// attributable cell), so an unattributed CPython runtime failure is always a red divergence.</para>
/// </summary>
[Trait("Category", "GapDiscovery")]
public class DifferentialExecutionTests : IntegrationTestBase
{
    /// <summary>
    /// Default program cap: none. The fixture arm sweeps the ENTIRE eligible pool every run.
    ///
    /// <para>Until #1202 the default was a 60-program budget whose fixture share was 22 of ~550
    /// eligible. The stable-hash selection guaranteed a deterministic <em>spread</em> but never
    /// <em>coverage</em>: a fixture could stay unsampled indefinitely while its category looked
    /// covered, and the allowlist — the thing that makes a divergence "reviewed" rather than
    /// "silent" — was calibrated to 4% of the corpus without saying so anywhere. Exhausting the
    /// pool exposed 32 non-allowlisted divergences that had been invisible in CI. Sample rotation
    /// was considered and rejected for the same reason: the allowlist should mean what it says.</para>
    ///
    /// <para>Measured cost of the honest default on the reference machine (post-#1202 exclusions:
    /// 527 fixtures + 26 hand-picked + 12 generated = 565 cells): <b>~46 s</b> wall — well inside the
    /// ~5 min the coverage decision budgeted for, because the AST exclusions removed two cells that
    /// each burned a 5 s <c>python3</c> pdb timeout and because the fuzz arm no longer inherits the
    /// fixture arm's unfilled budget. Set <c>DIFF_EXEC_LIMIT=&lt;n&gt;</c> for a fast local subsample —
    /// the report labels such a run and states its coverage.</para>
    /// </summary>
    private const int FullPool = 0;

    /// <summary>
    /// Generated-arm size when the fixture arm is running the full pool. Deliberately the same 12
    /// programs the old 60-program budget produced, so #1202's flip changes exactly one variable
    /// (fixture reach); how large the fuzz arm should be is an independent question.
    /// </summary>
    private const int FullPoolGeneratedTarget = 12;

    // Fixed CsCheck seed so the generated arm (and hence the whole default corpus) is deterministic
    // run-to-run — a sweep whose inputs drift cannot be ratcheted.
    private const string GeneratedSeed = "0000DifferentialExec";

    // Sharpy execution is capped tighter than the 30 s base default: a functions-only program that
    // needs longer is a perf outlier we would rather skip than let dominate the wall.
    private const int SharpyExecTimeoutMs = 12_000;

    public DifferentialExecutionTests(ITestOutputHelper output) : base(output) { }

    /// <summary>
    /// Set immediately before each <see cref="IntegrationTestBase.CompileAndExecute"/> call; read
    /// back by the override below. The Sharpy arm is a sequential loop over one test instance, so
    /// a field is the whole mechanism.
    /// </summary>
    private bool _currentProgramNeedsStdlib;

    protected override IEnumerable<string> GetAdditionalReferenceAssemblyPaths()
    {
        if (_currentProgramNeedsStdlib)
            yield return SharpyStdlibReference.Location;
    }

    [Fact]
    public void DifferentialExecution_SharedSubset_MatchesCPython()
    {
        var oracle = PythonExecOracle.TryLocate();
        if (oracle is null)
        {
            Output.WriteLine("SKIP: python3 >= 3.12 and run_programs.py not both available.");
            return;
        }

        var sw = Stopwatch.StartNew();
        int limit = ReadIntEnv("DIFF_EXEC_LIMIT", FullPool);
        var corpus = BuildCorpus(limit);
        var programs = corpus.Programs;
        var pool = corpus.FixturePool;
        Assert.True(programs.Count > 0, "Differential-exec corpus is empty — corpus construction is broken.");

        // --- Sharpy arm: production compile + execute, sequential (each spawns a dotnet subprocess). ---
        var sharpy = new Dictionary<string, ArmOutcome>(StringComparer.Ordinal);
        foreach (var p in programs)
        {
            // Referenced (and therefore deployed) per program rather than for the whole arm: the
            // stdlib closure is a dozen assemblies including MathNet and the SQLite provider, and
            // copying it into ~550 temp execution directories that never import anything would
            // dominate the sweep's wall clock.
            _currentProgramNeedsStdlib = p.NeedsStdlib;
            var r = CompileAndExecute(p.Source, "diff_exec.spy", executionTimeoutMs: SharpyExecTimeoutMs);
            sharpy[p.Key] = new ArmOutcome(
                Ok: r.Success && !r.TimedOut,
                Stdout: r.StandardOutput,
                TimedOut: r.TimedOut);
        }

        // --- Python arm: one batch process. Append main() so CPython invokes Sharpy's auto-entry. ---
        var requests = new List<PyRequest>(programs.Count);
        for (int i = 0; i < programs.Count; i++)
            requests.Add(new PyRequest(i, programs[i].Key, programs[i].Source + "\nmain()\n"));
        var pythonById = oracle.RunBatch(requests);

        // --- Compare + classify. ---
        var allowlist = Allowlist.Load();
        var results = new List<CaseResult>();
        for (int i = 0; i < programs.Count; i++)
        {
            var p = programs[i];
            var s = sharpy[p.Key];
            pythonById.TryGetValue(i, out var py);

            var c = Classify(s, py);
            results.Add(new CaseResult(p, s, py, c.Verdict, c.Detail, c.SkipClass, allowlist.Matches(p.Key)));
        }

        var divergences = results.Where(r => r.Verdict is Verdict.Divergent
                                          or Verdict.SharpyOkPythonError
                                          or Verdict.PythonOkSharpyError).ToList();
        var offenders = divergences.Where(r => !r.Allowlisted).ToList();
        var matches = results.Count(r => r.Verdict == Verdict.Match || r.Verdict == Verdict.BothError);
        var skips = results.Where(r => r.Verdict == Verdict.Skip).ToList();

        // Drain-on-fix enforcement (mirrors the metamorphic sweep): an allowlisted cell that no
        // longer diverges is a line the fixing change forgot to delete. Surfacing it as a failure
        // keeps the list trending to empty. Only meaningful over a full-pool run — a subsample has
        // not attempted every allowlisted cell — and only for exact keys (globs cover open sets).
        var stale = limit <= FullPool
            ? allowlist.ExactKeys
                .Except(divergences.Select(d => d.Program.Key), StringComparer.Ordinal)
                .OrderBy(k => k, StringComparer.Ordinal)
                .ToList()
            : new List<string>();

        sw.Stop();
        WriteReport(results, divergences, offenders, matches, skips, stale, allowlist, pool, limit, sw.Elapsed);

        var coverage = FixtureCoveragePercent(pool);
        Output.WriteLine(
            $"Differential-exec [{(limit <= FullPool ? "full-pool" : $"SUBSAMPLE DIFF_EXEC_LIMIT={limit}")}]: "
            + $"{results.Count} programs "
            + $"({programs.Count(x => x.Key.StartsWith("handpicked", StringComparison.Ordinal))} handpicked, "
            + $"{programs.Count(x => x.Key.StartsWith("fixture", StringComparison.Ordinal))} fixture, "
            + $"{programs.Count(x => x.Key.StartsWith("generated", StringComparison.Ordinal))} generated). "
            + $"Match={matches} Skip={skips.Count} Divergent={divergences.Count} "
            + $"Non-allowlisted={offenders.Count} Stale-allowlisted={stale.Count}. "
            + $"Wall={sw.Elapsed.TotalSeconds:F1}s.");
        Output.WriteLine(
            $"Fixture coverage: {pool.Selected.Count}/{pool.Eligible} eligible run ({coverage:F1}%) "
            + $"of {pool.Discovered} discovered fixtures."
            + (coverage < 100.0
                ? "  *** PARTIAL RUN — the allowlist's guarantee is scoped to these cells, not the corpus. ***"
                : ""));
        foreach (var kv in pool.Exclusions.Where(kv => kv.Key.StartsWith("sharpy-only-call:", StringComparison.Ordinal)))
            Output.WriteLine($"  excluded (CPython cannot execute) {kv.Key}: {kv.Value}");
        foreach (var g in skips.GroupBy(r => r.SkipClass.Length == 0 ? "unclassified" : r.SkipClass,
                                        StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal))
            Output.WriteLine($"  skipped at classify time ({g.Key}): {g.Count()}");
        foreach (var o in offenders.Take(40))
            Output.WriteLine($"  DIVERGENCE [{o.Verdict}] {o.Program.Key}: {o.Detail}");

        // Enumeration sanity — a corpus that shrank to nothing is itself a regression.
        Assert.True(results.Count > 0, "No programs were compared.");

        // Vacuity guard for the #1338 widening. The stdlib corpus reaches the oracle only if BOTH
        // halves hold: the Stdlib root is discovered, and ExecSubsetFilter admits its imports.
        // Either one regressing puts the count back to zero while every other number stays healthy.
        if (limit <= FullPool)
        {
            Assert.True(
                pool.Selected.Any(prog =>
                    prog.Key.StartsWith("fixture::stdlib-tests/", StringComparison.Ordinal)),
                "No Stdlib.Tests fixture reached the CPython oracle. Either fixture discovery "
                + "narrowed back to one root or ExecSubsetFilter stopped admitting the "
                + "CPython-available modules — both make the widening vacuous (#1338).");
        }

        // Ratchet: the allowlist file's presence engages it. Any non-allowlisted divergence fails.
        if (Allowlist.FileExists())
        {
            Assert.True(offenders.Count == 0,
                $"Differential-execution oracle: {offenders.Count} non-allowlisted divergence(s) between "
                + "Sharpy and CPython on shared-subset programs. Each is either a real runtime-semantics bug "
                + "(fix the compiler / stdlib), a designed deviation (add an allowlist entry citing "
                + "docs/deviations.yaml), or a harness/subset gap (tighten the filter). See "
                + ".claude/tmp/differential-exec-report.json.\n"
                + string.Join("\n", offenders.Take(30).Select(o => $"  {o.Program.Key} [{o.Verdict}] {o.Detail}")));

            Assert.True(stale.Count == 0,
                $"Differential-execution oracle: {stale.Count} allowlist entr(ies) no longer diverge. "
                + "Whatever fixed them must also delete the entries (drain-on-fix) so the list trends "
                + "to empty instead of accumulating lines nobody revisits:\n"
                + string.Join("\n", stale.Take(40).Select(k => $"  {k}")));
        }
    }

    // ----------------------------------------------------------------------------------------- //
    // Corpus construction
    // ----------------------------------------------------------------------------------------- //

    private Corpus BuildCorpus(int limit)
    {
        var handpicked = HandPicked();

        // Default: the fixture arm takes every eligible fixture, the fuzz arm its fixed budget.
        if (limit <= FullPool)
        {
            var full = Fixtures(int.MaxValue);
            return Compose(handpicked, full, Generated(FullPoolGeneratedTarget));
        }

        // DIFF_EXEC_LIMIT=<n>: a subsample for fast local iteration. Split the remaining budget
        // ~2/3 fixtures (real, realistic code) : ~1/3 generated (fuzz).
        var remaining = Math.Max(0, limit - handpicked.Count);
        var fixtureTarget = (remaining * 2) / 3;
        var generatedTarget = remaining - fixtureTarget;

        var pool = Fixtures(fixtureTarget);
        // Any fixture budget the pool could not fill rolls over to the generated arm.
        generatedTarget += Math.Max(0, fixtureTarget - pool.Selected.Count);
        return Compose(handpicked, pool, Generated(generatedTarget));
    }

    private static Corpus Compose(List<Program> handpicked, FixturePool pool, List<Program> generated)
    {
        var programs = new List<Program>(handpicked.Count + pool.Selected.Count + generated.Count);
        programs.AddRange(handpicked);
        programs.AddRange(pool.Selected);
        programs.AddRange(generated);
        return new Corpus(programs, pool);
    }

    /// <summary>
    /// Hand-authored programs probing the historical runtime-divergence surface. Each is a valid
    /// Sharpy <c>def main()</c> program whose CPython behaviour (top-level body + trailing
    /// <c>main()</c>) was verified against <c>python3 3.12</c> before inclusion. Some are DESIGNED
    /// deviations (UTF-16 string length/indexing — <c>string-indexing-utf16</c>) and are
    /// expected to diverge; the seeded allowlist keeps the ratchet green while the report records them.
    /// </summary>
    private static List<Program> HandPicked()
    {
        var items = new (string Name, string Body)[]
        {
            ("sort_stability", """
                data = [(1, "b"), (1, "a"), (0, "c"), (1, "d")]
                data.sort(key=lambda p: p[0])
                for x in data:
                    print(x[0], x[1])
                """),
            ("sorted_stability_by_len", """
                words: list[str] = ["bb", "aa", "cc", "a", "b"]
                for w in sorted(words, key=len):
                    print(w)
                """),
            ("split_default_whitespace", """
                s: str = "  a  b   c  "
                print(s.split())
                print(len(s.split()))
                """),
            ("split_empty_sep_edges", """
                print("a,b,,c,".split(","))
                print("".split(","))
                print(",".split(","))
                """),
            ("replace_count_and_empty", """
                print("aaaa".replace("a", "b", 2))
                print("abc".replace("", "-"))
                """),
            ("negative_index_and_slice", """
                xs: list[int] = [10, 20, 30, 40]
                print(xs[-1], xs[-2])
                print(xs[-3:])
                print(xs[:-1])
                """),
            ("int_floordiv_modulo_negatives", """
                print(-7 // 2)
                print(7 // -2)
                print(-7 % 3)
                print(7 % -3)
                """),
            // Float % on negatives follows the same floored rule (sign of divisor). #1153.
            ("float_modulo_negatives", """
                print(-7.5 % 2.0)
                print(7.5 % -2.0)
                print(-7.5 % -2.0)
                print(-1.0 % 3.0)
                """),
            // The divmod identity a == (a // b) * b + (a % b) holds once % floors. #1153.
            ("divmod_modulo_identity", """
                print((-7 // 3) * 3 + (-7 % 3))
                print((7 // -3) * -3 + (7 % -3))
                print(divmod(-7, 3))
                print(-7 // 3, -7 % 3)
                """),
            ("float_repr_halves", """
                print(0.1 + 0.2)
                print(1.0)
                print(3 / 2)
                print(1 / 3)
                print(2.5)
                """),
            ("string_multiplication", """
                print("ab" * 3)
                print("-" * 10)
                print(3 * "xy")
                """),
            ("chained_comparison", """
                x: int = 5
                print(1 < x < 10)
                print(10 < x < 20)
                print(1 < x <= 5)
                """),
            ("dict_popitem_order", """
                d: dict[str, int] = {"a": 1, "b": 2, "c": 3}
                print(d.popitem())
                print(d.popitem())
                print(d)
                """),
            ("bool_none_printing", """
                print(True, False, None)
                print([True, None, False])
                print(str(None))
                """),
            ("list_slice_step", """
                xs: list[int] = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9]
                print(xs[::2])
                print(xs[1::2])
                print(xs[::-1])
                print(xs[8:2:-2])
                """),
            ("enumerate_and_zip", """
                for i, c in enumerate(["a", "b", "c"]):
                    print(i, c)
                print(list(zip([1, 2, 3], [4, 5])))
                """),
            ("comprehension_and_reductions", """
                xs: list[int] = [1, 2, 3, 4, 5]
                print([x * x for x in xs if x % 2 == 1])
                print(sum(xs), min(xs), max(xs))
                """),
            ("str_join_and_membership", """
                parts: list[str] = ["a", "b", "c"]
                print(",".join(parts))
                print("b" in parts)
                print("ell" in "hello")
                """),
            ("true_division_types", """
                print(7 / 2)
                print(6 / 3)
                print(1 / 4)
                """),
            ("sorted_reverse_and_items", """
                xs: list[int] = [3, 1, 2]
                print(sorted(xs, reverse=True))
                d: dict[str, int] = {"b": 2, "a": 1, "c": 3}
                print(sorted(d.items()))
                """),
            // Regression guard (#1154, fixed): sorted(dict) iterates the dict's keys like Python.
            ("sorted_dict_keys", """
                d: dict[str, int] = {"b": 2, "a": 1, "c": 3}
                print(sorted(d))
                """),
            // Regression guard (#1154, fixed): the rest of the dict-iteration ring. list(d)/max(d)/
            // min(d) needed execution cells — max/min compiled but crashed at runtime (KeyValuePair
            // is not comparable), a shape invisible to compile-only sweeps. enumerate(d)/zip(d, …)
            // are the silent-wrong cases: they compiled AND ran, iterating KeyValuePair pairs instead
            // of keys, so only a stdout oracle catches a regression.
            ("dict_iteration_keys_ring", """
                d: dict[str, int] = {"b": 2, "a": 1, "c": 3}
                print(list(d))
                print(max(d))
                print(min(d))
                print(list(enumerate(d)))
                print(list(zip(d, [10, 20, 30])))
                """),
            ("list_mutation_methods", """
                xs: list[int] = [1, 2, 3]
                xs.append(4)
                xs.insert(0, 0)
                xs.reverse()
                print(xs)
                print(xs.index(3), xs.count(1))
                """),
            ("str_query_methods", """
                s: str = "Hello, World"
                print(s.find("o"), s.rfind("o"))
                print(s.startswith("Hello"), s.endswith("!"))
                print(s.upper(), s.lower())
                print(s.count("l"))
                """),
            ("range_variants", """
                print(list(range(5)))
                print(list(range(2, 8)))
                print(list(range(0, 10, 3)))
                print(list(range(10, 0, -2)))
                """),
            ("power_abs_divmod", """
                print(abs(-5), abs(5))
                print(2 ** 10)
                print(divmod(17, 5))
                """),
        };

        var list = new List<Program>(items.Length);
        foreach (var (name, body) in items)
            list.Add(new Program($"handpicked::{name}", WrapInMain(body)));
        return list;
    }

    /// <summary>
    /// Real single-file fixtures whose top level is functions-only with a <c>main</c> and whose whole
    /// tree is in the shared executable subset. Every fixture the walk rejects is counted under an
    /// attributable shape id (#1202) so the report can state the size of the eligible pool and why
    /// the rest is out, instead of silently shrinking the denominator. When <paramref name="target"/>
    /// is below the pool size the selection is a deterministic subsample (stable FNV hash of the
    /// name) that touches a spread of categories rather than an alphabetical prefix.
    /// </summary>
    private static FixturePool Fixtures(int target)
    {
        var eligible = new List<Program>();
        var exclusions = new SortedDictionary<string, int>(StringComparer.Ordinal);
        // The attributable Sharpy-only CALL forms are listed by name, not just counted: a reviewer
        // must be able to check that no rule fired on a program CPython could legitimately have run
        // (the failure mode a blanket "CPython errored ⇒ skip" would have institutionalised).
        var callFormExclusions = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
        int discovered = 0;

        void Exclude(string shape, string? name = null)
        {
            exclusions[shape] = exclusions.TryGetValue(shape, out var n) ? n + 1 : 1;
            if (name is not null && shape.StartsWith("sharpy-only-call:", StringComparison.Ordinal))
            {
                if (!callFormExclusions.TryGetValue(shape, out var names))
                    callFormExclusions[shape] = names = new List<string>();
                names.Add(name);
            }
        }

        foreach (var fx in FixtureDiscoveryHelper.DiscoverFixturesFrom(
                     FixtureRoots.CompilerTests, FixtureRoots.StdlibTests))
        {
            discovered++;
            if (fx.IsMultiFile || fx.ExpectedFile is null || fx.ErrorFile is not null
                || fx.RuntimeErrorFile is not null || fx.Features.Count > 0)
            {
                Exclude("not-a-single-file-stdout-fixture");
                continue;
            }

            string source;
            try
            { source = File.ReadAllText(fx.SpyFilePath); }
            catch
            {
                Exclude("unreadable");
                continue;
            }

            Module module;
            try
            {
                var parser = new Sharpy.Compiler.Parser.Parser(
                    new Sharpy.Compiler.Lexer.Lexer(source).TokenizeAll());
                module = parser.ParseModule();
                if (parser.Diagnostics.HasErrors)
                {
                    Exclude("parse-errors");
                    continue;
                }
            }
            catch
            {
                Exclude("parse-errors");
                continue;
            }

            if (!IsFunctionsOnlyWithMain(module))
            {
                Exclude("not-functions-only-with-main");
                continue;
            }

            var shape = ExecSubsetFilter.RejectionShape(module);
            if (shape is not null)
            {
                Exclude(shape, fx.TestName);
                continue;
            }

            eligible.Add(new Program(
                $"fixture::{fx.TestName}", source, NeedsStdlib: ImportsAStdlibModule(module)));
        }

        var selected = target <= 0
            ? new List<Program>()
            : eligible
                .OrderBy(p => StableHash(p.Key))
                .ThenBy(p => p.Key, StringComparer.Ordinal)
                .Take(target)
                .OrderBy(p => p.Key, StringComparer.Ordinal)
                .ToList();

        return new FixturePool(selected, eligible.Count, discovered, exclusions, callFormExclusions);
    }

    /// <summary>
    /// Well-typed generated programs (fuzz arm), reusing <see cref="GenTyped.ExpressionOfType"/> over a
    /// fixed, optional-free environment so the outputs stay inside the shared subset: two
    /// <c>print(expr)</c> statements over int / str / bool / list results with fuel 1 (shallow enough
    /// that int arithmetic cannot overflow Sharpy's 32-bit <c>int</c> and masquerade as a divergence).
    /// Each candidate must compile in Sharpy and pass the subset filter.
    /// </summary>
    private static List<Program> Generated(int target)
    {
        var results = new List<Program>();
        if (target <= 0)
            return results;

        var env = TypeEnv.WithCollections;
        var types = new[] { "int", "str", "bool", "list[int]", "list[str]" };
        var intElem = GenLiterals.Integer.Select(x => (Expression)x);
        var strElem = GenLiterals.SimpleString.Select(x => (Expression)x);

        var gen =
            from xv in GenLiterals.Integer
            from yv in GenLiterals.Integer
            from sv in GenLiterals.SimpleString
            from flagv in GenLiterals.Boolean
            from nums in intElem.Array[0, 3]
            from words in strElem.Array[0, 3]
            from t1 in Gen.OneOfConst(types)
            from e1 in GenTyped.ExpressionOfType(env, t1, 1)
            from t2 in Gen.OneOfConst(types)
            from e2 in GenTyped.ExpressionOfType(env, t2, 1)
            select BuildGeneratedMain(xv, yv, sv, flagv,
                nums.ToImmutableArray(), words.ToImmutableArray(), e1, e2);

        int idx = 0;
        var seenSources = new HashSet<string>(StringComparer.Ordinal);
        gen.Sample(module =>
        {
            if (results.Count >= target)
                return;

            string source;
            try
            { source = Sharpy.Compiler.Pretty.Unparser.Unparse(module); }
            catch { return; }
            if (!seenSources.Add(source))
                return;

            // Gate 1: Sharpy accepts it (analysis-clean).
            try
            {
                var analysis = new Sharpy.Compiler.Compiler().Analyze(source, "gen_diff.spy");
                if (!analysis.Success)
                    return;
            }
            catch { return; }

            // Gate 2: whole tree is in the shared executable subset (reparse then filter).
            try
            {
                var parser = new Sharpy.Compiler.Parser.Parser(
                    new Sharpy.Compiler.Lexer.Lexer(source).TokenizeAll());
                var reparsed = parser.ParseModule();
                if (parser.Diagnostics.HasErrors || !ExecSubsetFilter.IsInSharedSubset(reparsed))
                    return;
            }
            catch { return; }

            results.Add(new Program($"generated::g{idx++:D4}", source));
            // threads:1 keeps this collection loop single-threaded (results/seenSources are not
            // concurrent); seed pins determinism.
        }, seed: GeneratedSeed, iter: Math.Max(target * 12L, 96L), threads: 1);

        return results;
    }

    // ----------------------------------------------------------------------------------------- //
    // Program builders
    // ----------------------------------------------------------------------------------------- //

    /// <summary>Wraps a top-level-body snippet as <c>def main() -> None:</c> with 4-space indentation.</summary>
    private static string WrapInMain(string body)
    {
        var sb = new StringBuilder("def main() -> None:\n");
        foreach (var raw in body.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
            sb.Append("    ").Append(raw).Append('\n');
        return sb.ToString();
    }

    private static Module BuildGeneratedMain(
        IntegerLiteral xv, IntegerLiteral yv, StringLiteral sv, BooleanLiteral flagv,
        ImmutableArray<Expression> nums, ImmutableArray<Expression> words,
        Expression print1, Expression print2)
    {
        var body = ImmutableArray.CreateBuilder<Statement>();
        body.Add(Decl("x", Ann("int"), xv));
        body.Add(Decl("y", Ann("int"), yv));
        body.Add(Decl("s", Ann("str"), sv));
        body.Add(Decl("flag", Ann("bool"), flagv));
        body.Add(Decl("nums", ListAnn("int"), new ListLiteral { Elements = nums }));
        body.Add(Decl("words", ListAnn("str"), new ListLiteral { Elements = words }));
        body.Add(Print(print1));
        body.Add(Print(print2));

        return new Module
        {
            Body = ImmutableArray.Create<Statement>(
                new FunctionDef { Name = "main", Body = body.ToImmutable() })
        };
    }

    private static VariableDeclaration Decl(string name, TypeAnnotation type, Expression value) =>
        new() { Name = name, Type = type, InitialValue = value };

    private static ExpressionStatement Print(Expression arg) =>
        new()
        {
            Expression = new FunctionCall
            {
                Function = new Identifier { Name = "print" },
                Arguments = ImmutableArray.Create(arg)
            }
        };

    private static TypeAnnotation Ann(string name) => new() { Name = name };

    private static TypeAnnotation ListAnn(string element) =>
        new() { Name = "list", TypeArguments = ImmutableArray.Create(new TypeAnnotation { Name = element }) };

    // ----------------------------------------------------------------------------------------- //
    // Classification + output comparison
    // ----------------------------------------------------------------------------------------- //

    private static Classification Classify(ArmOutcome sharpy, PyResult? python)
    {
        if (python is null)
            return new(Verdict.Divergent, "no python verdict returned for this program");

        // Not parseable as Python => a Sharpy-only annotation slipped past the node-level filter.
        // Parse-level differences are the parse oracle's job, so treat as out-of-subset skip.
        if (python.SyntaxError)
            return new(Verdict.Skip, "python could not parse the source (Sharpy-only syntax); out of subset",
                SkipClass: "out-of-subset-syntax");

        if (sharpy.Ok && python.Ok)
        {
            return OutputComparer.Equivalent(sharpy.Stdout, python.Stdout)
                ? new(Verdict.Match, "")
                : new(Verdict.Divergent,
                    $"stdout mismatch — sharpy={Quote(sharpy.Stdout)} python={Quote(python.Stdout)}");
        }

        if (!sharpy.Ok && !python.Ok)
            return new(Verdict.BothError, "both arms failed (accepted as agreement)");

        if (sharpy.Ok && !python.Ok)
        {
            // A name CPython cannot resolve, while Sharpy ran, means the program used a Sharpy-only
            // name (an interop/builtin type like `array`, or `Some`/`Ok`) — out of the shared subset,
            // not a runtime divergence. Sharpy would have errored at compile time on a truly undefined
            // name, so a successful Sharpy run guarantees the name is Sharpy-provided.
            if (ReferencesNameMissingInPython(python.Stderr))
                return new(Verdict.Skip, $"python name not available (Sharpy-only): {FirstLine(python.Stderr)}",
                    SkipClass: "name-missing-in-python");

            return new(Verdict.SharpyOkPythonError,
                $"sharpy ran (stdout={Quote(sharpy.Stdout)}) but python failed"
                + (python.TimedOut ? " (timeout)" : $": {FirstLine(python.Stderr)}"));
        }

        return new(Verdict.PythonOkSharpyError,
            $"python ran (stdout={Quote(python.Stdout)}) but sharpy failed"
            + (sharpy.TimedOut ? " (timeout)" : ""));
    }

    /// <summary>
    /// Line-wise stdout comparison with a float-tolerant fallback, ported from the dogfood
    /// <c>_outputs_equivalent</c> comparator: exact match per line, else both-pure-number within
    /// relative tolerance (decimal-point presence must agree so <c>22</c> ≠ <c>22.0</c>), else
    /// embedded-number match where the non-numeric text is identical and every numeric token is close.
    /// </summary>
    private static class OutputComparer
    {
        private const double RelTol = 1e-9;
        private static readonly Regex Number = new(@"-?\d+\.?\d*", RegexOptions.Compiled);
        private static readonly Regex PureNumber = new(@"^-?\d+\.?\d*$", RegexOptions.Compiled);

        public static bool Equivalent(string a, string b)
        {
            var aLines = Normalize(a);
            var bLines = Normalize(b);
            if (aLines.Length != bLines.Length)
                return false;

            for (int i = 0; i < aLines.Length; i++)
            {
                var x = aLines[i].Trim();
                var y = bLines[i].Trim();
                if (x == y)
                    continue;

                if (PureNumber.IsMatch(x) && PureNumber.IsMatch(y))
                {
                    if (NumbersClose(x, y))
                        continue;
                    return false;
                }

                var xNums = Number.Matches(x);
                var yNums = Number.Matches(y);
                if (xNums.Count > 0 && xNums.Count == yNums.Count)
                {
                    var xText = Number.Replace(x, "\0");
                    var yText = Number.Replace(y, "\0");
                    if (xText == yText)
                    {
                        bool allClose = true;
                        for (int k = 0; k < xNums.Count; k++)
                        {
                            if (!NumbersClose(xNums[k].Value, yNums[k].Value))
                            {
                                allClose = false;
                                break;
                            }
                        }
                        if (allClose)
                            continue;
                    }
                }
                return false;
            }
            return true;
        }

        private static string[] Normalize(string output) =>
        output.Replace("\r\n", "\n", StringComparison.Ordinal)
              .TrimEnd('\n')
              .Split('\n');

        private static bool NumbersClose(string a, string b)
        {
            bool aDot = a.Contains('.', StringComparison.Ordinal);
            bool bDot = b.Contains('.', StringComparison.Ordinal);
            if (aDot != bDot)
                return false;
            if (!double.TryParse(a, NumberStyles.Float, CultureInfo.InvariantCulture, out var av)
                || !double.TryParse(b, NumberStyles.Float, CultureInfo.InvariantCulture, out var bv))
                return false;
            if (av == 0)
                return Math.Abs(bv) <= RelTol;
            return Math.Abs((av - bv) / av) <= RelTol;
        }
    }

    // ----------------------------------------------------------------------------------------- //
    // Fixture eligibility + subset filter
    // ----------------------------------------------------------------------------------------- //

    /// <summary>
    /// True when every top-level statement is a plain function definition, one is named <c>main</c>,
    /// and no name is defined twice. This excludes module-level statics (the <c>global</c>-mutation
    /// divergence class), classes (object-repr divergences), imports (stdlib semantics), decorated
    /// defs, and function overloading (Sharpy resolves by parameter type; CPython's second <c>def</c>
    /// simply shadows the first — a designed deviation, not a runtime bug) — leaving a program the
    /// Python arm can drive by appending a single <c>main()</c> call.
    /// </summary>
    /// <summary>
    /// Whether the program imports anything. By the time a fixture is eligible every surviving
    /// import names a module on <c>ExecSubsetFilter</c>'s CPython-available whitelist, and each of
    /// those is a Sharpy stdlib module — so "has an import" and "needs Sharpy.Stdlib" coincide.
    /// </summary>
    private static bool ImportsAStdlibModule(Module module)
        => module.Body.Any(s => s is ImportStatement or FromImportStatement);

    private static bool IsFunctionsOnlyWithMain(Module module)
    {
        if (module.Body.Length == 0)
            return false;
        bool hasMain = false;
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var stmt in module.Body)
        {
            // Module-level imports are top-level statements in both arms and mean the same thing:
            // Sharpy hoists them, CPython executes them before the appended main() call. Which
            // modules may appear is ExecSubsetFilter's decision, not this one's.
            if (stmt is ImportStatement or FromImportStatement)
                continue;
            if (stmt is not FunctionDef fn)
                return false;
            if (!names.Add(fn.Name))
                return false; // overloaded / redefined name — CPython would shadow, not overload.
            if (fn.Name == "main")
                hasMain = true;
        }
        return hasMain;
    }

    /// <summary>
    /// Closed-whitelist predicate: rejects any node whose runtime meaning is Sharpy-only or diverges
    /// from CPython by design at a level the byte-identical-text comparison cannot reconcile — Sharpy-
    /// only expression forms, type/struct/enum/interface/class definitions, imports, decorators, and
    /// the divergence-prone compound statements (match/with/try/defer/yield). Annotation-level Sharpy-
    /// only syntax (<c>T?</c>, <c>!E</c>) is not caught here; CPython's parser rejects it and the
    /// harness records that as a skip. Adapted from the parse oracle's SubsetFilter.
    ///
    /// <para>Rejections carry a short shape id so the report can attribute every excluded fixture
    /// (#1202). The <c>sharpy-only-call:*</c> family covers forms CPython rejects at RUNTIME rather
    /// than at parse time — those slip past both the SyntaxError skip and the NameError heuristic in
    /// <see cref="Classify"/>, so before #1202 they surfaced as bogus semantic divergences. Each such
    /// rule must be attributable to a specific construct CPython cannot execute; a blanket
    /// "CPython failed ⇒ ignore" is expressly not allowed, because that would hide the real case
    /// where Sharpy accepts a program CPython legitimately rejects.</para>
    /// </summary>
    private sealed class ExecSubsetFilter : AstVisitor
    {
        /// <summary>
        /// Non-<c>def</c> callees that a Sharpy program may subscript with explicit type arguments
        /// and CPython provably cannot. <c>map</c>/<c>zip</c>/<c>filter</c> exist in CPython but
        /// implement no <c>__class_getitem__</c> (<c>TypeError: type 'map' is not subscriptable</c>);
        /// <c>array</c>/<c>frozendict</c> are Sharpy-only names CPython resolves to nothing at all.
        /// The list is deliberately evidence-backed rather than "any subscripted callee": PEP-585
        /// generic aliases (<c>list[int]()</c>, <c>dict[str, int]()</c>) really do construct, and
        /// indexing a container of callables (<c>funcs[i](5)</c>, <c>pair[0](9)</c>) is ordinary
        /// Python — excluding either would drop programs CPython can legitimately run.
        ///
        /// <para>Known scope limit: these rules (and <see cref="_exceptAliases"/>, which is
        /// module-wide rather than per-function) key off BARE NAMES, not resolved bindings. A future
        /// fixture that binds a local variable named <c>map</c>/<c>array</c>/… to a subscriptable
        /// value and then does <c>map[0](5)</c> would be silently excluded from the pool — a quiet
        /// loss of coverage (visible in the exclusion counts), never a hidden divergence. Acceptable
        /// while the fixture corpus is curated; make the rules binding-aware if such a fixture ever
        /// lands.</para>
        /// </summary>
        private static readonly HashSet<string> NonSubscriptableInCPython =
            new(StringComparer.Ordinal) { "map", "zip", "filter", "array", "frozendict" };

        /// <summary>
        /// Modules where <c>import X</c> denotes the same library in both arms: present in a stock
        /// CPython 3.12 <em>and</em> implemented in Sharpy's stdlib under the same name, so the
        /// shared program text means one thing.
        ///
        /// <para>This is the list the #1338 widening turns on. Without it, adding the Stdlib.Tests
        /// root would have been vacuous — the filter rejected <em>every</em> import outright, so a
        /// corpus of stdlib fixtures would have been excluded to the last cell while the report
        /// showed a larger discovered count. A Sharpy-only module (or one CPython does not ship,
        /// like numpy) is excluded by name here, visibly, rather than allowlisted after diverging.</para>
        ///
        /// <para>Grows one module at a time: each addition puts a new corpus in front of the
        /// oracle, and the divergences it finds are the deliverable.</para>
        /// </summary>
        private static readonly HashSet<string> CPythonAvailableModules = new(StringComparer.Ordinal)
        {
            "json", "math", "pprint", "re", "string",
        };

        /// <summary>
        /// Member spellings Sharpy's stdlib exposes under a different name than CPython's: naming
        /// convention, not runtime semantics. Comparing a program that reads one tells us only
        /// that the two libraries spell an API differently, so they leave the shared subset by
        /// name — attributably, rather than arriving as a divergence whose triage ends in
        /// "different spelling" (#1338).
        /// </summary>
        private static readonly HashSet<string> SharpyOnlyMemberSpellings = new(StringComparer.Ordinal)
        {
            "Error",        // re.Error — CPython spells the module's exception re.error
            "pattern_str",  // re.Pattern.pattern_str — CPython spells it .pattern
        };

        private string? _rejection;
        private readonly HashSet<string> _exceptAliases = new(StringComparer.Ordinal);
        private HashSet<string> _declaredFunctions = new(StringComparer.Ordinal);

        /// <summary>
        /// <c>null</c> when the whole tree is in the shared executable subset; otherwise the shape id
        /// of the first construct that took it out.
        /// </summary>
        public static string? RejectionShape(Node node)
        {
            var f = new ExecSubsetFilter { _declaredFunctions = FunctionNameCollector.Collect(node) };
            f.Visit(node);
            return f._rejection;
        }

        public static bool IsInSharedSubset(Node node) => RejectionShape(node) is null;

        private void Reject(string shape) => _rejection ??= shape;

        private void Reject(Node node) => Reject("sharpy-only-node:" + node.GetType().Name);

        public override void Visit(Node node)
        {
            if (_rejection is not null)
                return;
            base.Visit(node);
        }

        // Sharpy-only expression forms (no CPython analog / divergent runtime meaning).
        public override void VisitMaybeExpression(MaybeExpression node) => Reject(node);
        public override void VisitTryExpression(TryExpression node) => Reject(node);
        public override void VisitQuestionMarkExpression(QuestionMarkExpression node) => Reject(node);
        public override void VisitTypeCoercion(TypeCoercion node) => Reject(node);
        public override void VisitTypeCheck(TypeCheck node) => Reject(node);
        public override void VisitMatchExpression(MatchExpression node) => Reject(node);
        public override void VisitAwaitExpression(AwaitExpression node) => Reject(node);
        public override void VisitModifiedArgument(ModifiedArgument node) => Reject(node);
        public override void VisitTStringLiteral(TStringLiteral node) => Reject(node);
        public override void VisitStarExpression(StarExpression node) => Reject(node);
        public override void VisitSpreadElement(SpreadElement node) => Reject(node);
        public override void VisitDictSpreadComprehension(DictSpreadComprehension node) => Reject(node);
        public override void VisitWalrusExpression(WalrusExpression node) => Reject(node);
        // F-strings: Sharpy/CPython disagree on delimiter/escape micro-syntax; exclude wholesale.
        public override void VisitFStringLiteral(FStringLiteral node) => Reject(node);

        // Definitions whose Python behaviour is Sharpy-only or diverges (object repr, static state).
        public override void VisitClassDef(ClassDef node) => Reject(node);
        public override void VisitStructDef(StructDef node) => Reject(node);
        public override void VisitInterfaceDef(InterfaceDef node) => Reject(node);
        public override void VisitEnumDef(EnumDef node) => Reject(node);
        public override void VisitUnionDef(UnionDef node) => Reject(node);
        public override void VisitDelegateDef(DelegateDef node) => Reject(node);
        public override void VisitEventDef(EventDef node) => Reject(node);
        public override void VisitPropertyDef(PropertyDef node) => Reject(node);
        public override void VisitTypeAlias(TypeAlias node) => Reject(node);
        public override void VisitDecoratedStatement(DecoratedStatement node) => Reject(node);

        // Imports: a module whose name resolves to the SAME library in both arms keeps the program
        // in the shared subset — that is the whole point of comparing stdlib fixtures against
        // CPython (#1338). Anything else leaves it, attributably by module name.
        public override void VisitImportStatement(ImportStatement node)
        {
            foreach (var alias in node.Names)
                RejectUnlessCPythonAvailable(alias.Name);
        }

        public override void VisitFromImportStatement(FromImportStatement node)
            => RejectUnlessCPythonAvailable(node.Module);

        private void RejectUnlessCPythonAvailable(string moduleName)
        {
            var root = moduleName.Split('.')[0];
            if (!CPythonAvailableModules.Contains(root))
                Reject("sharpy-only-import:" + root);
        }

        // Divergence-prone / Sharpy-only compound statements.
        public override void VisitMatchStatement(MatchStatement node) => Reject(node);
        public override void VisitWithStatement(WithStatement node) => Reject(node);
        public override void VisitDeferStatement(DeferStatement node) => Reject(node);
        public override void VisitYieldStatement(YieldStatement node) => Reject(node);

        // Shared nodes carrying a Sharpy-only flag.
        public override void VisitIdentifier(Identifier node)
        {
            if (node.IsNameBacktickEscaped)
                Reject("sharpy-only-syntax:backtick-escaped-name");
        }

        public override void VisitMemberAccess(MemberAccess node)
        {
            if (node.IsNullConditional)
            {
                Reject("sharpy-only-syntax:null-conditional-member");
                return;
            }

            // `except E as e: print(e.message)`. Sharpy surfaces .NET's Exception.Message as a
            // property (Axiom 1); CPython 3 removed BaseException.message, so the program dies with
            // an AttributeError instead of producing comparable stdout.
            if (node.Member == "message"
                && Unwrap(node.Object) is Identifier target
                && _exceptAliases.Contains(target.Name))
            {
                Reject("sharpy-only-call:exception-message-member");
                return;
            }

            if (SharpyOnlyMemberSpellings.Contains(node.Member))
            {
                Reject("sharpy-only-member:" + node.Member);
                return;
            }

            DefaultVisit(node);
        }

        public override void VisitTryStatement(TryStatement node)
        {
            foreach (var handler in node.Handlers)
            {
                if (!string.IsNullOrEmpty(handler.Name))
                    _exceptAliases.Add(handler.Name);

                // An except clause's exception type is a TypeAnnotation, not an expression, so a
                // Sharpy-only spelling there never reaches VisitMemberAccess. `except re.Error:`
                // is CPython's `re.error`, and the mismatch surfaces as an AttributeError at the
                // handler rather than as anything about runtime semantics.
                var lastSegment = handler.ExceptionType?.Name.Split('.')[^1];
                if (lastSegment is not null && SharpyOnlyMemberSpellings.Contains(lastSegment))
                {
                    Reject("sharpy-only-member:" + lastSegment);
                    return;
                }
            }
            DefaultVisit(node);
        }

        public override void VisitFunctionCall(FunctionCall node)
        {
            var callee = Unwrap(node.Function);

            // `identity[int](42)`, `map[int, int](f, xs)`, `(identity[int])(6)` — explicit type
            // arguments on a callee CPython cannot subscript: a name this module declares with
            // `def` (CPython functions never implement __class_getitem__, so the call dies with
            // "'function' object is not subscriptable"), or one of the names above.
            if (callee is IndexAccess indexed
                && Unwrap(indexed.Object) is Identifier subscripted
                && (_declaredFunctions.Contains(subscripted.Name)
                    || NonSubscriptableInCPython.Contains(subscripted.Name)))
            {
                Reject("sharpy-only-call:explicit-type-arguments");
                return;
            }

            // The same form on a QUALIFIED callee: `json.loads[dict[str, int]](text)`. A CPython
            // module-level function is a plain function object and never implements
            // __class_getitem__, so subscripting one is a TypeError there whatever the module is.
            // The unqualified rule above could not see these, which is how a typed-loads fixture
            // reached the oracle as a divergence once the stdlib corpus entered scope (#1338).
            if (callee is IndexAccess qualified && Unwrap(qualified.Object) is MemberAccess)
            {
                Reject("sharpy-only-call:explicit-type-arguments-qualified");
                return;
            }

            if (callee is Identifier name)
            {
                // CPython's breakpoint() enters pdb and reads stdin; there is no batch analog.
                if (name.Name == "breakpoint")
                {
                    Reject("sharpy-only-call:breakpoint");
                    return;
                }

                // Sharpy's map() accepts strict=; CPython's map() takes no keyword arguments at all
                // (zip(..., strict=True) IS valid CPython >= 3.10 and stays eligible).
                if (name.Name == "map" && node.KeywordArguments.Any(k => k.Name == "strict"))
                {
                    Reject("sharpy-only-call:map-strict-keyword");
                    return;
                }
            }

            // Sharpy accepts the sort key positionally; CPython's list.sort() is keyword-only.
            if (callee is MemberAccess method && method.Member == "sort" && node.Arguments.Length > 0)
            {
                Reject("sharpy-only-call:sort-positional-key");
                return;
            }

            DefaultVisit(node);
        }

        public override void VisitBinaryOp(BinaryOp node)
        {
            if (node.Operator is BinaryOperator.NullCoalesce or BinaryOperator.PipeForward)
            {
                Reject("sharpy-only-syntax:" + node.Operator);
                return;
            }
            DefaultVisit(node);
        }

        public override void VisitForClause(ForClause node)
        {
            if (node.IsAsync)
            {
                Reject("sharpy-only-syntax:async-for-clause");
                return;
            }
            DefaultVisit(node);
        }

        public override void VisitLambdaExpression(LambdaExpression node)
        {
            if (node.IsArrowSyntax)
            {
                Reject("sharpy-only-syntax:arrow-lambda");
                return;
            }
            DefaultVisit(node);
        }

        public override void VisitIntegerLiteral(IntegerLiteral node)
        {
            if (node.Suffix is not null)
                Reject("sharpy-only-syntax:integer-literal-suffix");
        }

        public override void VisitFloatLiteral(FloatLiteral node)
        {
            if (node.Suffix is not null)
                Reject("sharpy-only-syntax:float-literal-suffix");
        }

        private static Expression Unwrap(Expression expression)
        {
            while (expression is Parenthesized parenthesized)
                expression = parenthesized.Expression;
            return expression;
        }

        /// <summary>Every <c>def</c> name in the tree, so a subscripted callee can be told apart
        /// from an indexed container of callables.</summary>
        private sealed class FunctionNameCollector : AstVisitor
        {
            private readonly HashSet<string> _names = new(StringComparer.Ordinal);

            public static HashSet<string> Collect(Node node)
            {
                var c = new FunctionNameCollector();
                c.Visit(node);
                return c._names;
            }

            public override void VisitFunctionDef(FunctionDef node)
            {
                _names.Add(node.Name);
                DefaultVisit(node);
            }
        }
    }

    // ----------------------------------------------------------------------------------------- //
    // Python oracle process wrapper
    // ----------------------------------------------------------------------------------------- //

    private sealed class PythonExecOracle
    {
        private readonly string _pythonExe;
        private readonly string _scriptPath;

        private PythonExecOracle(string pythonExe, string scriptPath)
        {
            _pythonExe = pythonExe;
            _scriptPath = scriptPath;
        }

        public static PythonExecOracle? TryLocate()
        {
            string? script = FindRunnerScript();
            if (script is null)
                return null;
            foreach (var candidate in new[] { "python3", "python" })
            {
                if (HasSupportedVersion(candidate))
                    return new PythonExecOracle(candidate, script);
            }
            return null;
        }

        private static string? FindRunnerScript()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "sharpy.sln")))
                {
                    var candidate = Path.Combine(
                        dir.FullName, "build_tools", "differential_exec", "run_programs.py");
                    return File.Exists(candidate) ? candidate : null;
                }
                dir = dir.Parent;
            }
            return null;
        }

        private static bool HasSupportedVersion(string exe)
        {
            try
            {
                using var proc = Process.Start(new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = "--version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                });
                if (proc is null)
                    return false;
                string outText = proc.StandardOutput.ReadToEnd() + proc.StandardError.ReadToEnd();
                if (!proc.WaitForExit(10_000))
                {
                    try
                    { proc.Kill(entireProcessTree: true); }
                    catch { }
                    return false;
                }
                var match = Regex.Match(outText, @"Python (\d+)\.(\d+)");
                if (!match.Success)
                    return false;
                int major = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                int minor = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
                return major > 3 || (major == 3 && minor >= 12);
            }
            catch
            {
                return false;
            }
        }

        public IReadOnlyDictionary<int, PyResult> RunBatch(IReadOnlyList<PyRequest> requests)
        {
            string batchPath = Path.Combine(Path.GetTempPath(), $"sharpy-diffexec-{Guid.NewGuid():N}.jsonl");
            try
            {
                using (var writer = new StreamWriter(batchPath, append: false, new UTF8Encoding(false)))
                {
                    foreach (var r in requests)
                        writer.WriteLine(JsonSerializer.Serialize(new { id = r.Id, source = r.Source }));
                }
                return ParseVerdicts(RunRunner(batchPath));
            }
            finally
            {
                try
                { File.Delete(batchPath); }
                catch { }
            }
        }

        private string RunRunner(string batchPath)
        {
            using var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _pythonExe,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                },
            };
            proc.StartInfo.ArgumentList.Add(_scriptPath);
            proc.StartInfo.ArgumentList.Add("--batch");
            proc.StartInfo.ArgumentList.Add(batchPath);

            var stdout = new StringBuilder();
            var stderr = new StringBuilder();
            proc.OutputDataReceived += (_, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
            proc.ErrorDataReceived += (_, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };

            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            // Generous ceiling: each program has its own per-program timeout inside the runner.
            if (!proc.WaitForExit(300_000))
            {
                try
                { proc.Kill(entireProcessTree: true); }
                catch { }
                throw new InvalidOperationException("run_programs.py timed out.");
            }
            proc.WaitForExit();

            if (proc.ExitCode != 0)
                throw new InvalidOperationException($"run_programs.py exited {proc.ExitCode}: {stderr}");

            return stdout.ToString();
        }

        private static IReadOnlyDictionary<int, PyResult> ParseVerdicts(string stdout)
        {
            var result = new Dictionary<int, PyResult>();
            foreach (var line in stdout.Split('\n'))
            {
                var trimmed = line.Trim();
                if (trimmed.Length == 0)
                    continue;
                using var doc = JsonDocument.Parse(trimmed);
                var root = doc.RootElement;
                int id = root.GetProperty("id").GetInt32();
                result[id] = new PyResult(
                    Ok: root.GetProperty("ok").GetBoolean(),
                    Stdout: root.TryGetProperty("stdout", out var so) ? so.GetString() ?? "" : "",
                    Stderr: root.TryGetProperty("stderr", out var se) ? se.GetString() ?? "" : "",
                    TimedOut: root.TryGetProperty("timed_out", out var to) && to.GetBoolean(),
                    SyntaxError: root.TryGetProperty("syntax_error", out var sy) && sy.GetBoolean());
            }
            return result;
        }
    }

    // ----------------------------------------------------------------------------------------- //
    // Allowlist (module::key ratchet, mirroring the interop conformance allowlist)
    // ----------------------------------------------------------------------------------------- //

    private sealed class Allowlist
    {
        private readonly HashSet<string> _exact;
        private readonly List<string> _wildcards;

        private Allowlist(HashSet<string> exact, List<string> wildcards)
        {
            _exact = exact;
            _wildcards = wildcards;
        }

        public bool Matches(string key)
            => _exact.Contains(key) || _wildcards.Any(w => GlobMatch(key, w));

        public int Count => _exact.Count + _wildcards.Count;

        /// <summary>The exact (non-glob) keys, for stale-entry detection.</summary>
        public IReadOnlyCollection<string> ExactKeys => _exact;

        private static readonly Lazy<string?> PathLazy = new(FindPath);

        public static bool FileExists() => PathLazy.Value != null && File.Exists(PathLazy.Value);

        public static Allowlist Load()
        {
            var exact = new HashSet<string>(StringComparer.Ordinal);
            var wildcards = new List<string>();
            var path = PathLazy.Value;
            if (path == null || !File.Exists(path))
                return new Allowlist(exact, wildcards);

            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw;
                var hash = line.IndexOf('#', StringComparison.Ordinal);
                if (hash >= 0)
                    line = line.Substring(0, hash);
                line = line.Trim();
                if (line.Length == 0)
                    continue;
                if (line.Contains('*', StringComparison.Ordinal))
                    wildcards.Add(line);
                else
                    exact.Add(line);
            }
            return new Allowlist(exact, wildcards);
        }

        private static string? FindPath()
        {
            var current = AppContext.BaseDirectory;
            while (current != null)
            {
                var dir = Path.Combine(current, "src", "Sharpy.Compiler.Tests", "Conformance");
                if (Directory.Exists(dir))
                    return Path.Combine(dir, "differential-exec-allowlist.txt");
                current = Directory.GetParent(current)?.FullName;
            }
            return null;
        }

        private static bool GlobMatch(string text, string pattern)
        {
            var parts = pattern.Split('*');
            var pos = 0;
            for (var i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                if (part.Length == 0)
                    continue;
                if (i == 0)
                {
                    if (!text.StartsWith(part, StringComparison.Ordinal))
                        return false;
                    pos = part.Length;
                }
                else
                {
                    var idx = text.IndexOf(part, pos, StringComparison.Ordinal);
                    if (idx < 0)
                        return false;
                    pos = idx + part.Length;
                }
            }
            return pattern.EndsWith("*", StringComparison.Ordinal) || pos == text.Length;
        }
    }

    // ----------------------------------------------------------------------------------------- //
    // Report I/O
    // ----------------------------------------------------------------------------------------- //

    private void WriteReport(
        List<CaseResult> results, List<CaseResult> divergences, List<CaseResult> offenders,
        int matches, List<CaseResult> skips, List<string> stale, Allowlist allowlist,
        FixturePool pool, int limit, TimeSpan elapsed)
    {
        var reportDir = ReportDir();
        Directory.CreateDirectory(reportDir);
        var report = new
        {
            summary = new
            {
                programs = results.Count,
                handpicked = results.Count(r => r.Program.Key.StartsWith("handpicked", StringComparison.Ordinal)),
                fixture = results.Count(r => r.Program.Key.StartsWith("fixture", StringComparison.Ordinal)),
                generated = results.Count(r => r.Program.Key.StartsWith("generated", StringComparison.Ordinal)),
                matches,
                skips = skips.Count,
                divergences = divergences.Count,
                nonAllowlistedDivergences = offenders.Count,
                allowlistSize = allowlist.Count,
                staleAllowlistEntries = stale.Count,
                wallSeconds = Math.Round(elapsed.TotalSeconds, 1),
            },
            staleAllowlistEntries = stale,
            // The run states its own scope (#1202). Before this, a report could show zero
            // divergences while silently covering 4% of the fixture corpus, so the allowlist's
            // guarantee was narrower than the contract it is supposed to enforce.
            coverage = new
            {
                mode = limit <= FullPool ? "full-pool" : $"subsample (DIFF_EXEC_LIMIT={limit})",
                fixturesDiscovered = pool.Discovered,
                fixturesEligible = pool.Eligible,
                fixturesRun = pool.Selected.Count,
                fixtureCoveragePercent = FixtureCoveragePercent(pool),
                // Per-root, because the #1338 widening is only real if the stdlib root actually
                // contributes cells: a pool that is 100%-covered and 0%-stdlib is the vacuous
                // widening the ExecSubsetFilter's blanket import rejection would have produced.
                fixturesRunByRoot = pool.Selected
                    .GroupBy(prog => prog.Key.StartsWith("fixture::stdlib-tests/", StringComparison.Ordinal)
                        ? "stdlib-tests"
                        : "compiler-tests", StringComparer.Ordinal)
                    .OrderBy(g => g.Key, StringComparer.Ordinal)
                    .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal),
                fixtureExclusionsByShape = pool.Exclusions,
                skipsByClass = skips
                    .GroupBy(r => r.SkipClass.Length == 0 ? "unclassified" : r.SkipClass, StringComparer.Ordinal)
                    .OrderBy(g => g.Key, StringComparer.Ordinal)
                    .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal),
            },
            ratchetMode = Allowlist.FileExists(),
            // Which fixtures CPython cannot execute, named so the attribution is auditable (#1202).
            sharpyOnlyCallFormExclusions = pool.CallFormExclusions,
            divergences = divergences.Select(r => new
            {
                key = r.Program.Key,
                verdict = r.Verdict.ToString(),
                allowlisted = r.Allowlisted,
                detail = r.Detail,
                sharpyOk = r.Sharpy.Ok,
                sharpyStdout = r.Sharpy.Stdout,
                pythonOk = r.Python?.Ok,
                pythonStdout = r.Python?.Stdout,
                pythonStderr = FirstLine(r.Python?.Stderr ?? ""),
                source = r.Program.Source,
            }),
            skips = skips.Take(60).Select(r => new { key = r.Program.Key, detail = r.Detail }),
        };
        var path = Path.Combine(reportDir, "differential-exec-report.json");
        File.WriteAllText(path, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        Output.WriteLine($"Report written to: {path}");
    }

    private static double FixtureCoveragePercent(FixturePool pool)
        => pool.Eligible == 0 ? 100.0 : Math.Round(100.0 * pool.Selected.Count / pool.Eligible, 1);

    private static string ReportDir()
        => Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(typeof(DifferentialExecutionTests).Assembly.Location)!,
            "..", "..", "..", "..", "..", ".claude", "tmp"));

    // ----------------------------------------------------------------------------------------- //
    // Small helpers + records
    // ----------------------------------------------------------------------------------------- //

    private static int ReadIntEnv(string name, int fallback)
        => int.TryParse(Environment.GetEnvironmentVariable(name), out var v) && v > 0 ? v : fallback;

    /// <summary>
    /// True when CPython failed because it could not resolve a name/module — i.e. the program leans
    /// on a Sharpy-only builtin/interop name (e.g. <c>array[int](...)</c>) with no CPython analog.
    /// </summary>
    private static bool ReferencesNameMissingInPython(string stderr)
        => stderr.Contains("NameError", StringComparison.Ordinal)
           || stderr.Contains("ModuleNotFoundError", StringComparison.Ordinal)
           || stderr.Contains("ImportError", StringComparison.Ordinal);

    /// <summary>Deterministic (process-independent) 32-bit FNV-1a hash for stable subsampling.</summary>
    private static uint StableHash(string s)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (var c in s)
            {
                hash ^= c;
                hash *= 16777619;
            }
            return hash;
        }
    }

    private static string Quote(string s)
    {
        var trimmed = s.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n');
        if (trimmed.Length > 200)
            trimmed = trimmed.Substring(0, 200) + "…";
        return "`" + trimmed.Replace("\n", "\\n", StringComparison.Ordinal) + "`";
    }

    private static string FirstLine(string s)
    {
        if (string.IsNullOrEmpty(s))
            return "";
        var nl = s.IndexOf('\n', StringComparison.Ordinal);
        var line = nl >= 0 ? s.Substring(0, nl) : s;
        return line.Length > 200 ? line.Substring(0, 200) + "…" : line;
    }

    private enum Verdict
    {
        Match,
        BothError,
        Skip,
        Divergent,
        SharpyOkPythonError,
        PythonOkSharpyError,
    }

    /// <summary>
    /// One corpus program. <paramref name="NeedsStdlib"/> is set for the fixtures that import a
    /// stdlib module: their compile needs Sharpy.Stdlib referenced and their run needs it deployed.
    /// </summary>
    private sealed record Program(string Key, string Source, bool NeedsStdlib = false);

    /// <summary>
    /// The fixture arm's own coverage statement: how many fixtures the discovery walk saw, how many
    /// survived every eligibility gate, how many of those this run actually executed, and — per
    /// attributable shape id — why the rest were excluded (#1202).
    /// </summary>
    private sealed record FixturePool(
        List<Program> Selected, int Eligible, int Discovered,
        IReadOnlyDictionary<string, int> Exclusions,
        IReadOnlyDictionary<string, List<string>> CallFormExclusions);

    private sealed record Corpus(List<Program> Programs, FixturePool FixturePool);

    private sealed record ArmOutcome(bool Ok, string Stdout, bool TimedOut);

    private sealed record PyRequest(int Id, string Key, string Source);

    private sealed record PyResult(bool Ok, string Stdout, string Stderr, bool TimedOut, bool SyntaxError);

    private sealed record Classification(Verdict Verdict, string Detail, string SkipClass = "");

    private sealed record CaseResult(
        Program Program, ArmOutcome Sharpy, PyResult? Python, Verdict Verdict, string Detail,
        string SkipClass, bool Allowlisted);
}
