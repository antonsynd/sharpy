using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>
/// Class-A (#1002/#1003/#1004/#1133/#1136/#1138/#1141/#1142) — generic-reference conformance sweep.
/// The umbrella contract it enforces is the generic-reference-unification contract (#1143); it is
/// also the completeness invariant (#1145) for the per-callee-kind resolution arms, and it holds the
/// SPY0908 "no un-lowerable accepted program" policy (#1146) for this surface.
///
/// <para>
/// One defect class threads through those issues: an explicit-type-argument reference
/// <c>callee[T, …]</c> (and its uncalled form <c>g = callee[T, …]</c>) resolves, arity-checks,
/// and lowers <b>differently depending on what <c>callee</c> is</b> — a builtin, a user function,
/// a user-module function, a stdlib .NET-module function, a user instance method, a BCL instance
/// method, or a user function that shadows a same-named builtin. The "type args vs subscript"
/// decision lives in per-callee-kind arms in <c>TypeChecker</c> (the <c>CheckIndexAccessCore</c>
/// area), and every new callee shape found a missing arm. There is no single "generic reference"
/// resolution notion, so the matrix (callee-kind × usage-form × arity) has been filled one cell at
/// a time by accident during feature work and <c>/verify-implementation</c>.
/// </para>
///
/// <para><b>Contract this sweep enforces.</b> For every expressible cell of the matrix the compiler
/// must EITHER (a) compile cleanly (and, for the curated runnable subset, execute with the correct
/// output), OR (b) emit a <b>deliberate</b> diagnostic. It must NEVER:
/// <list type="bullet">
///   <item>emit an internal-error diagnostic (<c>SPY0908</c>/<c>SPY09xx</c>) — an ICE;</item>
///   <item>fire <c>SPY0320</c> "does not support indexing" at a generic callable — the subscript
///         misfire signature (every cell here references a generic callable, so <c>[...]</c> is
///         always type-args, never a subscript);</item>
///   <item>leak a raw <c>CSxxxx</c> Roslyn code, or silently mis-emit uncompilable C#;</item>
///   <item>print the wrong result for a runnable cell.</item>
/// </list>
/// </para>
///
/// <para>
/// This is a <b>sweep</b> (Design Decision #10), not hundreds of xUnit cases: every cell aggregates
/// into ONE <c>Category=GapDiscovery</c> JSON report under <c>.claude/tmp/</c>, and the test is
/// traited out of the fast suite. A reviewed allowlist
/// (<c>Conformance/generic-reference-allowlist.txt</c>) makes it a <b>ratchet</b>: any cell whose
/// outcome is <c>ice</c>/<c>subscriptMisfire</c>/<c>csLeak</c>/<c>wrongOutput</c>/<c>crash</c> and
/// whose key is not allowlisted fails the build. The allowlist was seeded only with cells a real run
/// confirmed as known-open issues (#1141 BCL typo'd member; #1142 user-module generic fn → SPY0320;
/// #1147 parenthesized method-group callee → C# cast; #1148 builtin value-arg validation after
/// type-arg selection). This is the systematic analog of <see cref="InteropConformanceTests"/> that
/// the class-A analysis called out as the single clearest missing mechanism.
/// </para>
///
/// <para><b>Acceptance criterion (#1143) — MET.</b> Those issues are fixed and every allowlist line
/// is drained: <c>generic-reference-allowlist.txt</c> is EMPTY (every cell resolves cleanly or emits
/// a deliberate diagnostic). The file itself stays in place — its presence is what arms the ratchet —
/// so any regression that reintroduces a gap now fails the suite loudly instead of being absorbed.
/// </para>
/// </summary>
[Trait("Category", "GapDiscovery")]
[Collection("HeavyCompilation")]
public class GenericReferenceConformanceTests : IntegrationTestBase
{
    public GenericReferenceConformanceTests(ITestOutputHelper output) : base(output) { }

    // ---- outcome buckets ----
    private const string OutcomeOk = "ok";
    private const string OutcomeDeliberate = "deliberateDiagnostic";
    private const string OutcomeIce = "ice";
    private const string OutcomeSubscriptMisfire = "subscriptMisfire";
    private const string OutcomeCsLeak = "csLeak";
    private const string OutcomeWrongOutput = "wrongOutput";
    private const string OutcomeCrash = "crash";
    private const string OutcomeNotAttempted = "notAttempted";

    // Buckets that fail the ratchet (a contract violation), vs. ok/deliberate which are passes.
    private static readonly HashSet<string> FailingOutcomes = new(StringComparer.Ordinal)
    {
        OutcomeIce, OutcomeSubscriptMisfire, OutcomeCsLeak, OutcomeWrongOutput, OutcomeCrash,
    };

    // ---- usage forms ----
    private const string FormCalled = "called";                       // f[T](args)
    private const string FormUncalledAssigned = "uncalledAssigned";   // g = f[T]
    private const string FormUncalledThenCalled = "uncalledThenCalled"; // g = f[T]; g(args)
    private const string FormPassedAsArg = "passedAsArg";             // h(f[T])
    private const string FormParenCalled = "parenCalled";            // (f[T])(args)
    private const string FormTernaryCalled = "ternaryCalled";        // (f[T] if c else f[T])(args)

    // ---- arity variants ----
    private const string ArityExact = "exact";
    private const string ArityExcess = "excess";       // one extra type arg
    private const string ArityDeficient = "deficient"; // one missing (multi-param only)
    private const string ArityNone = "none";           // no brackets — control cell

    [Fact]
    public void GenericReferenceSweep_AllCalleeKinds_ResolveOrDeliberateDiagnostic()
    {
        var (corePath, stdlibPath) = ResolveStdlibAssemblyPaths();
        Assert.True(File.Exists(corePath), $"Sharpy.Core.dll not found at {corePath}");
        Assert.True(File.Exists(stdlibPath), $"Sharpy.Stdlib.dll not found at {stdlibPath}");

        var api = new CompilerApi(NullLogger.Instance, new[] { corePath, stdlibPath });
        var csharpBase = BuildCSharpBaseCompilation();

        var specimens = BuildSpecimens();
        var cells = EnumerateCells(specimens).ToList();
        // #1147 generality probes: two NON-generic parenthesized-callee cells, to prove the
        // parenthesized method-group cast mis-emit is not generic-specific.
        cells.AddRange(BuildNonGenericProbeCells());

        // Compile every cell in-process. Cells are independent (each CompilerApi.Compile builds its
        // own Compiler/ModuleRegistry; multi-file cells write to a unique temp dir), so the sweep
        // parallelizes to keep the CI-only run tractable — same idiom as the interop sweep.
        var dop = Math.Max(1, ReadIntEnv("GENERIC_SWEEP_DOP", Math.Min(4, Environment.ProcessorCount)));
        var evaluated = new ConcurrentBag<CellResult>();
        Parallel.ForEach(cells, new ParallelOptions { MaxDegreeOfParallelism = dop }, cell =>
        {
            evaluated.Add(EvaluateCompile(api, csharpBase, cell));
        });

        // Curated runnable subset: one clean-compiling cell per Core-only callee kind, exact arity,
        // called form — executed end-to-end so a silent mis-emit that still produces valid C#
        // (#1136's original failure mode) is caught by a wrong printed result. Executed sequentially
        // (each spawns a dotnet subprocess) and kept small (<= 15).
        var executedResults = RunExecutedSubset(specimens);
        foreach (var r in executedResults)
            evaluated.Add(r);

        var results = evaluated.ToList();
        var byOutcome = results.GroupBy(r => r.Outcome)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        var byCalleeKind = results.GroupBy(r => r.Cell.CalleeKind)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => g.GroupBy(r => r.Outcome).ToDictionary(x => x.Key, x => x.Count(), StringComparer.Ordinal),
                StringComparer.Ordinal);

        var allowlist = LoadAllowlist();
        var failures = results.Where(r => FailingOutcomes.Contains(r.Outcome)).ToList();
        var offenders = failures.Where(f => !allowlist.Contains(f.Cell.Key)).ToList();

        WriteReport(new
        {
            summaryStats = new
            {
                calleeKinds = specimens.Select(s => s.CalleeKind).Distinct().Count(),
                specimens = specimens.Count,
                cellsEnumerated = results.Count,
                ok = byOutcome.GetValueOrDefault(OutcomeOk),
                deliberateDiagnostic = byOutcome.GetValueOrDefault(OutcomeDeliberate),
                notAttempted = byOutcome.GetValueOrDefault(OutcomeNotAttempted),
                ice = byOutcome.GetValueOrDefault(OutcomeIce),
                subscriptMisfire = byOutcome.GetValueOrDefault(OutcomeSubscriptMisfire),
                csLeak = byOutcome.GetValueOrDefault(OutcomeCsLeak),
                wrongOutput = byOutcome.GetValueOrDefault(OutcomeWrongOutput),
                crash = byOutcome.GetValueOrDefault(OutcomeCrash),
                allowlistSize = allowlist.Count,
                failures = failures.Count,
                nonAllowlistedFailures = offenders.Count,
            },
            ratchetMode = AllowlistFileExists(),
            scopeNotes = new[]
            {
                "Callee kinds: builtin generic fn (map), user top-level generic fn, user-module generic fn (multi-file), stdlib .NET-module generic fn (json.loads), user instance generic method, BCL instance generic method (List.ConvertAll), user fn shadowing a same-named builtin (map).",
                "Usage forms: called f[T](args); uncalledAssigned g=f[T]; uncalledThenCalled g=f[T] then g(x); passedAsArg print(f[T]); parenCalled (f[T])(x); ternaryCalled (f[T] if c else f[T])(x).",
                "Arity variants: exact; excess (one extra type arg); deficient (one missing, multi-param only); none (no brackets — control call that relies on inference).",
                "Outcomes: ok (clean compile), deliberateDiagnostic (any SPY error except SPY09xx and except SPY0320), ice (SPY0908/SPY09xx), subscriptMisfire (SPY0320 at a generic callee), csLeak (raw CSxxxx or mis-emitted C# that fails Roslyn bind), wrongOutput (runnable cell printed the wrong result), notAttempted.",
                "Every cell references a generic callable, so [...] is always explicit type args — any SPY0320 is a misfire by construction.",
            },
            byOutcome,
            byCalleeKind,
            cells = results
                .OrderBy(r => r.Cell.Key, StringComparer.Ordinal)
                .Select(r => r.ToReport(allowlist)),
        });
        WriteFailureKeys(failures.Select(f => f.Cell.Key).Distinct().OrderBy(k => k, StringComparer.Ordinal));

        Output.WriteLine($"Cells enumerated: {results.Count}");
        Output.WriteLine($"ok={byOutcome.GetValueOrDefault(OutcomeOk)} deliberate={byOutcome.GetValueOrDefault(OutcomeDeliberate)} notAttempted={byOutcome.GetValueOrDefault(OutcomeNotAttempted)}");
        Output.WriteLine($"ice={byOutcome.GetValueOrDefault(OutcomeIce)} subscriptMisfire={byOutcome.GetValueOrDefault(OutcomeSubscriptMisfire)} csLeak={byOutcome.GetValueOrDefault(OutcomeCsLeak)} wrongOutput={byOutcome.GetValueOrDefault(OutcomeWrongOutput)} crash={byOutcome.GetValueOrDefault(OutcomeCrash)}");
        Output.WriteLine($"Allowlist size: {allowlist.Count}  Non-allowlisted failures: {offenders.Count}");
        foreach (var o in offenders.Take(50))
            Output.WriteLine($"  OFFENDER {o.Cell.Key} [{o.Outcome}] {o.Diagnostics.FirstOrDefault()}");

        // Enumeration sanity always holds.
        Assert.True(results.Count > 0, "Generic-reference sweep enumerated zero cells.");

        // Ratchet: only engages once a reviewed allowlist exists (the baseline commit).
        if (AllowlistFileExists())
        {
            Assert.True(offenders.Count == 0,
                "Generic-reference conformance ratchet: the following cells violate the resolve-or-deliberate-diagnostic " +
                "contract and are not on the reviewed allowlist. Either fix the generic-reference resolution / file an " +
                "issue, or add a justified allowlist entry.\n" +
                string.Join("\n", offenders.Take(50).Select(o => $"  {o.Cell.Key} [{o.Outcome}] {o.Diagnostics.FirstOrDefault()}")) +
                "\nFull report: .claude/tmp/generic-reference-conformance-report.json");
        }
    }

    // ---- specimen model ----

    /// <summary>
    /// A concrete generic callable plus everything needed to reference it. One specimen yields many
    /// cells (form × arity). Multi-file specimens carry a <see cref="SiblingModule"/> written next to
    /// the entry file so the local import resolves through the on-disk synthetic-project path.
    /// </summary>
    private sealed record Specimen(
        string Id,
        string CalleeKind,
        int TypeParamCount,
        string Imports,
        string Prelude,
        string? SiblingModuleName,
        string? SiblingModuleContent,
        string EnclosingParams,
        string Receiver,
        string Member,
        string[] ExactTypeArgs,
        string CallArgs,
        string? Runnable = null,
        string? ExpectedOutput = null);

    private sealed record Cell(Specimen Specimen, string Form, string Arity, string Source)
    {
        public string CalleeKind => Specimen.CalleeKind;
        public bool MultiFile => Specimen.SiblingModuleName != null;
        public string Key => $"{Specimen.Id}::{Form}::{Arity}";
    }

    private sealed record CellResult(Cell Cell, string Outcome, IReadOnlyList<string> Diagnostics, string? Detail = null)
    {
        public object ToReport(Allowlist allowlist) => new
        {
            key = Cell.Key,
            calleeKind = Cell.CalleeKind,
            specimen = Cell.Specimen.Id,
            form = Cell.Form,
            arity = Cell.Arity,
            outcome = Outcome,
            allowlisted = allowlist.Contains(Cell.Key),
            diagnostics = Diagnostics,
            detail = Detail,
            snippet = Cell.Source,
        };
    }

    private static List<Specimen> BuildSpecimens()
    {
        const string dblPrelude = "def _dbl(x: int) -> int:\n    return x * 2\n\n";
        const string boxPrelude =
            "class Box:\n" +
            "    def __init__(self):\n" +
            "        pass\n\n" +
            "    def convert[T](self, value: T) -> str:\n" +
            "        return f\"c{value}\"\n\n" +
            "    def pick[T, U](self, a: T, b: U) -> str:\n" +
            "        return \"p\"\n\n";
        const string identityDecl = "def identity[T](x: T) -> T:\n    return x\n\n";
        const string pairDecl = "def pair[T, U](a: T, b: U) -> T:\n    return a\n\n";
        const string shadowMapDecl = "def map[T](x: T) -> T:\n    return x\n\n";

        return new List<Specimen>
        {
            // (1) builtin generic function — map[TIn, TOut] (two type params for the 1-iterable form).
            new("builtin_map", "builtin", 2,
                Imports: "", Prelude: dblPrelude, SiblingModuleName: null, SiblingModuleContent: null,
                EnclosingParams: "xs: list[int]", Receiver: "", Member: "map",
                ExactTypeArgs: new[] { "int", "int" }, CallArgs: "_dbl, xs",
                Runnable:
                    "def _dbl(x: int) -> int:\n" +
                    "    return x * 2\n\n" +
                    "def main() -> None:\n" +
                    "    xs: list[int] = [1, 2, 3]\n" +
                    "    print(list(map[int, int](_dbl, xs)))\n",
                ExpectedOutput: "[2, 4, 6]"),

            // (2) user top-level generic function — single and multi type-parameter specimens.
            new("user_identity", "user-fn", 1,
                Imports: "", Prelude: identityDecl, SiblingModuleName: null, SiblingModuleContent: null,
                EnclosingParams: "", Receiver: "", Member: "identity",
                ExactTypeArgs: new[] { "int" }, CallArgs: "5",
                Runnable: identityDecl + "def main() -> None:\n    print(identity[int](42))\n",
                ExpectedOutput: "42"),
            new("user_pair", "user-fn", 2,
                Imports: "", Prelude: pairDecl, SiblingModuleName: null, SiblingModuleContent: null,
                EnclosingParams: "", Receiver: "", Member: "pair",
                ExactTypeArgs: new[] { "int", "str" }, CallArgs: "1, \"a\"",
                Runnable: pairDecl + "def main() -> None:\n    print(pair[int, str](1, \"a\"))\n",
                ExpectedOutput: "1"),

            // (3) user-module generic function (multi-file). Known broken: #1142.
            new("usermod_identity", "user-module", 1,
                Imports: "import genlib\n", Prelude: "", SiblingModuleName: "genlib",
                SiblingModuleContent: "def identity[T](x: T) -> T:\n    return x\n",
                EnclosingParams: "", Receiver: "genlib.", Member: "identity",
                ExactTypeArgs: new[] { "int" }, CallArgs: "5"),
            new("usermod_pair", "user-module", 2,
                Imports: "import genlib\n", Prelude: "", SiblingModuleName: "genlib",
                SiblingModuleContent: "def identity[T](x: T) -> T:\n    return x\n\ndef pair[T, U](a: T, b: U) -> T:\n    return a\n",
                EnclosingParams: "", Receiver: "genlib.", Member: "pair",
                ExactTypeArgs: new[] { "int", "str" }, CallArgs: "1, \"a\""),

            // (4) stdlib .NET-module generic function — json.loads[T] ("Arm B", known-good).
            new("stdlib_json_loads", "stdlib-module", 1,
                Imports: "import json\n", Prelude: "", SiblingModuleName: null, SiblingModuleContent: null,
                EnclosingParams: "", Receiver: "json.", Member: "loads",
                ExactTypeArgs: new[] { "int" }, CallArgs: "\"5\""),

            // (5) user instance generic method — single and multi type-parameter.
            new("instance_convert", "instance-method", 1,
                Imports: "", Prelude: boxPrelude, SiblingModuleName: null, SiblingModuleContent: null,
                EnclosingParams: "recv: Box", Receiver: "recv.", Member: "convert",
                ExactTypeArgs: new[] { "int" }, CallArgs: "5",
                Runnable:
                    boxPrelude +
                    "def main() -> None:\n" +
                    "    b: Box = Box()\n" +
                    "    print(b.convert[int](5))\n",
                ExpectedOutput: "c5"),
            new("instance_pick", "instance-method", 2,
                Imports: "", Prelude: boxPrelude, SiblingModuleName: null, SiblingModuleContent: null,
                EnclosingParams: "recv: Box", Receiver: "recv.", Member: "pick",
                ExactTypeArgs: new[] { "int", "str" }, CallArgs: "1, \"a\""),

            // (6) BCL instance generic method — List[int].ConvertAll<TOutput>. Receiver is a parameter
            // (not constructed) to avoid the separate raw-BCL-construction ambiguity (#1139).
            new("bcl_convertall", "bcl-method", 1,
                Imports: "from system.collections.generic import List\n", Prelude: "",
                SiblingModuleName: null, SiblingModuleContent: null,
                EnclosingParams: "lst: List[int]", Receiver: "lst.", Member: "convert_all",
                ExactTypeArgs: new[] { "str" }, CallArgs: "lambda x: str(x)"),

            // (7) user function shadowing a same-named builtin — def map[T] shadows builtin map (#1002/#1003).
            new("shadow_map", "shadow-builtin", 1,
                Imports: "", Prelude: shadowMapDecl, SiblingModuleName: null, SiblingModuleContent: null,
                EnclosingParams: "", Receiver: "", Member: "map",
                ExactTypeArgs: new[] { "int" }, CallArgs: "5",
                Runnable: shadowMapDecl + "def main() -> None:\n    print(map[int](5))\n",
                ExpectedOutput: "5"),
        };
    }

    private static IEnumerable<Cell> EnumerateCells(IEnumerable<Specimen> specimens)
    {
        foreach (var s in specimens)
        {
            // exact arity across every usage form.
            foreach (var form in new[] { FormCalled, FormUncalledAssigned, FormUncalledThenCalled, FormPassedAsArg, FormParenCalled, FormTernaryCalled })
                yield return MakeCell(s, form, ArityExact);

            // excess arity: the count-check surface, on a called and an uncalled form.
            yield return MakeCell(s, FormCalled, ArityExcess);
            yield return MakeCell(s, FormUncalledAssigned, ArityExcess);

            // deficient arity: only expressible for multi-param generics (one missing type arg).
            if (s.TypeParamCount >= 2)
            {
                yield return MakeCell(s, FormCalled, ArityDeficient);
                yield return MakeCell(s, FormUncalledAssigned, ArityDeficient);
            }

            // none: control cell — a plain call with no brackets, relying on inference.
            yield return MakeCell(s, FormCalled, ArityNone);
            yield return MakeCell(s, FormParenCalled, ArityNone);

            // typo'd member: a NONEXISTENT generic member on a real receiver, called with exact-arity
            // type args (`lst.no_such_generic[str](...)`). Only meaningful for receiver-bearing kinds
            // (a member access, not a bare identifier). The contract requires member-not-found, not an
            // ICE — this is the axis that pins #1141 (typo'd BCL member + type args → SPY0908).
            if (s.Receiver.Length > 0)
                yield return MakeTypoMemberCell(s);
        }
    }

    /// <summary>
    /// #1147 generality probes: parenthesized callees over PLAIN (non-generic) callables. If these
    /// csLeak the same way the generic <c>parenCalled::none</c> cells do, the parenthesized
    /// method-group cast mis-emit is a general codegen defect the generics sweep merely surfaced —
    /// not a generics-specific bug. Kept outside the specimen matrix (arity/type-args don't apply).
    /// </summary>
    private static IEnumerable<Cell> BuildNonGenericProbeCells()
    {
        var userFn = new Specimen("nongeneric_userfn", "nongeneric-parenprobe", 0,
            Imports: "", Prelude: "def foo(x: int) -> int:\n    return x\n\n",
            SiblingModuleName: null, SiblingModuleContent: null,
            EnclosingParams: "", Receiver: "", Member: "foo",
            ExactTypeArgs: Array.Empty<string>(), CallArgs: "5");
        yield return new Cell(userFn, FormParenCalled, ArityNone,
            $"{userFn.Prelude}def _use() -> None:\n    _x = (foo)(5)\n");

        var instance = new Specimen("nongeneric_instance", "nongeneric-parenprobe", 0,
            Imports: "",
            Prelude: "class Plain:\n    def __init__(self):\n        pass\n\n    def method(self, x: int) -> int:\n        return x\n\n",
            SiblingModuleName: null, SiblingModuleContent: null,
            EnclosingParams: "recv: Plain", Receiver: "recv.", Member: "method",
            ExactTypeArgs: Array.Empty<string>(), CallArgs: "5");
        yield return new Cell(instance, FormParenCalled, ArityNone,
            $"{instance.Prelude}def _use(recv: Plain) -> None:\n    _x = (recv.method)(5)\n");
    }

    private static Cell MakeTypoMemberCell(Specimen s)
    {
        var typoRef = $"{s.Receiver}no_such_generic[{string.Join(", ", s.ExactTypeArgs)}]";
        var stmt = $"_x = {typoRef}({s.CallArgs})";
        var source = $"{s.Imports}{s.Prelude}def _use({s.EnclosingParams}) -> None:\n    {stmt}\n";
        return new Cell(s, "typoMember", ArityExact, source);
    }

    private static Cell MakeCell(Specimen s, string form, string arity)
    {
        var typeArgs = arity switch
        {
            ArityExact => s.ExactTypeArgs,
            ArityExcess => s.ExactTypeArgs.Append("int").ToArray(),
            ArityDeficient => s.ExactTypeArgs.Take(s.ExactTypeArgs.Length - 1).ToArray(),
            ArityNone => null,
            _ => s.ExactTypeArgs,
        };

        var refExpr = typeArgs == null
            ? $"{s.Receiver}{s.Member}"
            : $"{s.Receiver}{s.Member}[{string.Join(", ", typeArgs)}]";

        var stmt = form switch
        {
            FormCalled => $"_x = {refExpr}({s.CallArgs})",
            FormUncalledAssigned => $"_x = {refExpr}",
            FormUncalledThenCalled => $"_g = {refExpr}\n    _y = _g({s.CallArgs})",
            FormPassedAsArg => $"print({refExpr})",
            FormParenCalled => $"_x = ({refExpr})({s.CallArgs})",
            FormTernaryCalled => $"_x = ({refExpr} if True else {refExpr})({s.CallArgs})",
            _ => $"_x = {refExpr}",
        };

        var paramList = s.EnclosingParams;
        var source = $"{s.Imports}{s.Prelude}def _use({paramList}) -> None:\n    {stmt}\n";
        return new Cell(s, form, arity, source);
    }

    // ---- compile-phase evaluation ----

    private CellResult EvaluateCompile(CompilerApi api, CSharpCompilation csharpBase, Cell cell)
    {
        CompileResult result;
        try
        {
            result = cell.MultiFile
                ? CompileMultiFile(api, cell)
                : api.Compile(cell.Source, new CompilerOptions { OutputType = "library" });
        }
        catch (Exception ex)
        {
            return new CellResult(cell, OutcomeCrash, new[] { $"{ex.GetType().Name}: {ex.Message}" });
        }

        var errors = result.Diagnostics
            .Where(d => d.Severity == CompilerDiagnosticSeverity.Error)
            .Select(d => new { d.Code, d.Message })
            .ToList();
        var diagStrings = errors.Select(e => $"{e.Code}: {e.Message}").Distinct().Take(6).ToList();

        // Classify Sharpy-phase diagnostics. Priority: an internal error (SPY09xx) is the most
        // severe; then the subscript misfire (SPY0320 at a generic callee — always a misfire here);
        // then a raw CS-code leak in a diagnostic (never wrapped by the SPY0908 net); then any other
        // SPY error is a deliberate diagnostic.
        if (errors.Any(e => IsInfrastructureCode(e.Code)))
            return new CellResult(cell, OutcomeIce, diagStrings);
        if (errors.Any(e => e.Code == DiagnosticCodes.Semantic.ProtocolMissingMethod))
            return new CellResult(cell, OutcomeSubscriptMisfire, diagStrings);
        if (errors.Any(e => IsRawCsCode(e.Code)))
            return new CellResult(cell, OutcomeCsLeak, diagStrings);
        if (errors.Count > 0)
            return new CellResult(cell, OutcomeDeliberate, diagStrings);

        // No Sharpy errors: bind the generated C# through Roslyn. A silent mis-emit produces valid
        // Sharpy analysis but uncompilable C# (#1136's original failure mode before the SPY0908 net).
        var generated = CollectGeneratedCSharpSources(result);
        var csErrors = BindGeneratedCSharp(csharpBase, generated);
        if (csErrors.Count > 0)
            return new CellResult(cell, OutcomeCsLeak, csErrors);

        return new CellResult(cell, OutcomeOk, Array.Empty<string>());
    }

    /// <summary>
    /// Compiles a multi-file cell by writing the entry file plus its sibling module to a unique temp
    /// directory and driving the on-disk synthetic-project path (<see cref="CompilerApi.CompileFile"/>),
    /// which walks the local-import closure — the exact path a real <c>import genlib</c> takes.
    /// </summary>
    private CompileResult CompileMultiFile(CompilerApi api, Cell cell)
    {
        var dir = Path.Combine(Path.GetTempPath(), $"sharpy_genref_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var siblingPath = Path.Combine(dir, cell.Specimen.SiblingModuleName + ".spy");
            File.WriteAllText(siblingPath, cell.Specimen.SiblingModuleContent);
            var mainPath = Path.Combine(dir, "main.spy");
            File.WriteAllText(mainPath, cell.Source);
            return api.CompileFile(mainPath, new CompilerOptions { OutputType = "library" });
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { /* best effort */ }
        }
    }

    /// <summary>
    /// Collects the generated C# as one source string PER FILE. Multi-file cells (usermod imports)
    /// produce a compilation unit per .spy file, each a complete unit with its own using directives;
    /// they must bind as separate syntax trees. Concatenating them into one string put a file's
    /// usings after the prior file's namespace → a spurious CS1529 that masqueraded as a csLeak once
    /// the usermod inference cells started compiling (#1142/#1143).
    /// </summary>
    private static IReadOnlyList<string> CollectGeneratedCSharpSources(CompileResult result)
    {
        if (result.GeneratedCSharpFiles.Count > 0)
            return result.GeneratedCSharpFiles
                .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv => kv.Value)
                .ToList();
        return result.GeneratedCSharp is { Length: > 0 } single
            ? new[] { single }
            : Array.Empty<string>();
    }

    // ---- execute-phase subset ----

    private List<CellResult> RunExecutedSubset(IReadOnlyList<Specimen> specimens)
    {
        var results = new List<CellResult>();
        foreach (var s in specimens.Where(s => s.Runnable != null))
        {
            // A dedicated executed cell key (never collides with a compile cell), exact/called.
            var cell = new Cell(s, "executed", ArityExact, s.Runnable!);
            try
            {
                var exec = CompileAndExecute(s.Runnable!, fileName: $"genref_{s.Id}.spy");
                if (!exec.Success)
                {
                    var diags = exec.CompilationErrors.Concat(exec.RawDiagnostics.Select(d => $"{d.Code}: {d.Message}"))
                        .Distinct().Take(6).ToList();
                    // A runnable exact/called cell that fails to compile/run is at least a wrong-output
                    // class failure (it should produce ExpectedOutput). Bucket internal errors as ice.
                    var outcome = exec.RawDiagnostics.Any(d => IsInfrastructureCode(d.Code)) ? OutcomeIce
                        : exec.RawDiagnostics.Any(d => d.Code == DiagnosticCodes.Semantic.ProtocolMissingMethod) ? OutcomeSubscriptMisfire
                        : OutcomeWrongOutput;
                    results.Add(new CellResult(cell, outcome, diags, "runnable cell did not compile/run"));
                    continue;
                }

                var actual = exec.StandardOutput.Replace("\r\n", "\n").Trim();
                var expected = s.ExpectedOutput!.Trim();
                if (actual == expected)
                    results.Add(new CellResult(cell, OutcomeOk, Array.Empty<string>(), $"output={actual}"));
                else
                    results.Add(new CellResult(cell, OutcomeWrongOutput,
                        new[] { $"expected '{expected}' but got '{actual}'" }, "silent mis-emit / wrong runtime result"));
            }
            catch (Exception ex)
            {
                results.Add(new CellResult(cell, OutcomeCrash, new[] { $"{ex.GetType().Name}: {ex.Message}" }));
            }
        }
        return results;
    }

    // ---- classification helpers ----

    private static bool IsInfrastructureCode(string? code)
        => code != null && code.StartsWith("SPY09", StringComparison.Ordinal);

    private static readonly Regex RawCsCode = new(@"^CS\d{3,5}$", RegexOptions.Compiled);
    private static bool IsRawCsCode(string? code)
        => code != null && RawCsCode.IsMatch(code);

    // ---- Roslyn C# bind ----

    private static CSharpCompilation BuildCSharpBaseCompilation()
    {
        var refs = new List<MetadataReference>(IntegrationTestBase.GetSharedReferences());
        var seen = refs.OfType<PortableExecutableReference>()
            .Select(r => Path.GetFileName(r.FilePath))
            .Where(n => n != null)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Add every DLL next to the test assembly (Sharpy.Stdlib + its NuGet deps) so generated C#
        // that touches a stdlib dependency binds without a missing reference masquerading as a leak.
        var binDir = Path.GetDirectoryName(typeof(GenericReferenceConformanceTests).Assembly.Location)!;
        foreach (var dll in Directory.GetFiles(binDir, "*.dll"))
        {
            var fileName = Path.GetFileName(dll);
            if (!seen.Add(fileName))
                continue;
            try { refs.Add(MetadataReference.CreateFromFile(dll)); }
            catch { /* not a managed assembly */ }
        }

        return CSharpCompilation.Create(
            "GenericReferenceSweepBase",
            Array.Empty<SyntaxTree>(),
            refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static List<string> BindGeneratedCSharp(CSharpCompilation baseCompilation, IReadOnlyList<string> generatedSources)
    {
        var trees = generatedSources
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => CSharpSyntaxTree.ParseText(s))
            .ToArray();
        if (trees.Length == 0)
            return new List<string>();
        var compilation = baseCompilation.AddSyntaxTrees(trees);
        return compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => $"{d.Id}: {d.GetMessage()}")
            .Distinct()
            .Take(6)
            .ToList();
    }

    // ---- allowlist + report I/O ----

    private static readonly Lazy<string?> AllowlistPath = new(FindAllowlistPath);

    private static bool AllowlistFileExists() => AllowlistPath.Value != null && File.Exists(AllowlistPath.Value);

    private static Allowlist LoadAllowlist()
    {
        var exact = new HashSet<string>(StringComparer.Ordinal);
        var path = AllowlistPath.Value;
        if (path == null || !File.Exists(path))
            return new Allowlist(exact);

        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw;
            var hash = line.IndexOf('#', StringComparison.Ordinal);
            if (hash >= 0)
                line = line.Substring(0, hash);
            line = line.Trim();
            if (line.Length == 0)
                continue;
            exact.Add(line);
        }
        return new Allowlist(exact);
    }

    private static string? FindAllowlistPath()
    {
        var current = AppContext.BaseDirectory;
        while (current != null)
        {
            var dir = Path.Combine(current, "src", "Sharpy.Compiler.Tests", "Conformance");
            if (Directory.Exists(dir))
                return Path.Combine(dir, "generic-reference-allowlist.txt");
            current = Directory.GetParent(current)?.FullName;
        }
        return null;
    }

    private void WriteReport(object report)
    {
        var reportDir = ReportDir();
        Directory.CreateDirectory(reportDir);
        var reportPath = Path.Combine(reportDir, "generic-reference-conformance-report.json");
        File.WriteAllText(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        Output.WriteLine($"Report written to: {reportPath}");
    }

    private static void WriteFailureKeys(IEnumerable<string> keys)
    {
        var reportDir = ReportDir();
        Directory.CreateDirectory(reportDir);
        File.WriteAllLines(Path.Combine(reportDir, "generic-reference-conformance-failures.txt"), keys);
    }

    private static string ReportDir()
        => Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(typeof(GenericReferenceConformanceTests).Assembly.Location)!,
            "..", "..", "..", "..", "..", ".claude", "tmp"));

    private static int ReadIntEnv(string name, int fallback)
        => int.TryParse(Environment.GetEnvironmentVariable(name), out var v) ? v : fallback;

    private static (string CorePath, string StdlibPath) ResolveStdlibAssemblyPaths()
    {
        var baseDir = Path.GetDirectoryName(typeof(GenericReferenceConformanceTests).Assembly.Location)!;
        return (Path.Combine(baseDir, "Sharpy.Core.dll"), Path.Combine(baseDir, "Sharpy.Stdlib.dll"));
    }

    /// <summary>The reviewed conformance allowlist: exact <c>specimen::form::arity</c> cell keys.</summary>
    private sealed class Allowlist
    {
        private readonly HashSet<string> _exact;
        public Allowlist(HashSet<string> exact) => _exact = exact;
        public int Count => _exact.Count;
        public bool Contains(string key) => _exact.Contains(key);
    }
}
