using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Differential;

/// <summary>
/// The class guard for format-spec parity (#1943, #1944, #1945): a differential sweep of the PEP 3101
/// mini-language against python3. It generates <c>(operand kind, spec)</c> cells from the grammar
/// <c>[[fill]align][sign][z][#][0][width][grouping][.precision][type]</c> (a single fixed-seed
/// <c>System.Random</c> draws every slot, so the corpus is byte-identical run-to-run), assembles each
/// into a <c>try: print(format(v, "spec")) except ValueError / TypeError</c>
/// program, runs it under Sharpy (the production compile+execute path) and python3 3.12 (through
/// <c>build_tools/differential_exec/run_programs.py</c>), and compares stdout byte-for-byte. Every
/// cell is compared in TWO columns (<see cref="Column"/>): the ENGINE column hides the spec in a
/// variable so every cell exercises Core's runtime engine and its <c>ValueError</c>/<c>TypeError</c> refusals; the
/// STATIC column passes the literal, so a refused spec may meet the compile-time twin (SPY0609,
/// #1956), which agrees only with CPython's refusal text verbatim — pinning the static twin
/// (<c>FormatSpecGrammar</c>, a projection over Core's one validator since #1984) to CPython without
/// giving up runtime coverage.
///
/// <para>Sweep discipline mirrors <see cref="DifferentialExecutionTests"/>: any non-allowlisted
/// divergence fails the run; an allowlisted cell that no longer diverges fails until its line is
/// deleted (drain-on-fix). Every allowlist row cites an issue. The values are fixed and exactly
/// representable (int 42, float 3.5, whole-valued float 3.0, bool True, str "ab"), so the sweep
/// exercises the SPEC grammar, not float-repr pathology. A separately seeded kind-axis stratum and
/// three explicit strata widen the operand axis (#1988 #1989 #1978): negative zero (-0.0) and a
/// negative value that renders as zero (-1e-9) under 'z'; the no-<c>__format__</c> kinds (list,
/// dict, set, tuple — CPython's TypeError); a class that owns its spec (Sharpy's
/// <c>System.IFormattable</c> ↔ python's <c>__format__</c>); and '#' with 'c'. The whole-valued float is its own kind
/// because the '#' alternate form diverges most visibly there (<c>format(3.0, "#g")</c> is
/// <c>3.00000</c>; the point and the trailing zeros are exactly what '#' keeps, #1958). No cell is
/// excluded at generation: every drawn spec, '#' included, is compared. Skips (no-op) when a
/// suitable python3 is absent; CI pins 3.12.</para>
///
/// <para>Where the three consumers (format(), str.format, f-strings) are tied together by
/// <c>FormatEngineConsumerParityTests</c>, this generator only needs the format() route.</para>
/// </summary>
[Trait("Category", "GapDiscovery")]
public class FormatSpecDifferentialTests : IntegrationTestBase
{
    public FormatSpecDifferentialTests(ITestOutputHelper output) : base(output) { }

    // A fixed integer seed drives a System.Random so the generated corpus is byte-for-byte identical
    // run-to-run and machine-to-machine on a pinned runtime — a sweep whose inputs drift cannot be
    // ratcheted. (CsCheck's Sample was observed to NOT reproduce its cell set across runs here, which
    // would let the sweep flap between pre-existing engine bugs; a seeded Random is unambiguous.)
    private const int GeneratedSeed = 0x5F0A_7C31;

    // The contract wants at least this many distinct cells per run.
    private const int TargetCells = 240;

    // The '='-after-radix-prefix stratum (#1959): a separately seeded draw so the uniform corpus
    // above is unaffected by its size.
    private const int EqualsPrefixStratumSeed = 0x1959_3D23;
    private const int EqualsPrefixStratumCells = 40;

    // The kind-axis stratum (#1988 #1989): the same grammar draw over the operand kinds the uniform
    // corpus does not carry, separately seeded so the uniform corpus is unchanged.
    private const int KindAxisStratumSeed = 0x1988_4B1D;
    private const int KindAxisStratumCells = 84;

    private const int SharpyExecTimeoutMs = 12_000;

    // The static column's positive control: at least this many cells must agree by a COMPILE-time
    // SPY0609 carrying CPython's ValueError/TypeError wording. Measured 219 @ 195992e40 + the widened corpus, on
    // top of the #1988 static kinds (435 cells; was 130 of 280 before the kind-axis and explicit
    // strata). The fixed seeds and explicit strata make the count deterministic, so a drop means the
    // static twin (FormatSpecGrammar through CheckStaticFormatSpecArguments, #1956) stopped firing on
    // literal specs — or stopped projecting an operand kind (#1988) — not that the corpus moved.
    private const int StaticTwinAgreementFloor = 219;

    private sealed record Cell(string Kind, string Spec)
    {
        // A key safe for the allowlist file: the spec is hex-encoded so a '#', '*', or whitespace in
        // it can never be read as a comment, a glob, or trimmed away. The human-readable spec travels
        // in the divergence detail and in the row's trailing comment. The column is part of the key:
        // an engine-column row and a static-column row for the same spec are different facts.
        public string Key(Column column) => "formatspec::" + ColumnName(column) + "::" + Kind + "::"
            + string.Concat(Encoding.UTF8.GetBytes(Spec).Select(b => b.ToString("x2", CultureInfo.InvariantCulture)));
    }

    /// <summary>
    /// The two columns every cell is compared in. <see cref="Column.Engine"/> passes the spec through
    /// a <c>str</c> variable, so the checker cannot see a literal and EVERY cell reaches Core's runtime
    /// engine (<c>PyFormat</c>) — its output and its refusal text are compared with python3.
    /// <see cref="Column.Static"/> passes the spec as a string literal, so a spec CPython refuses must be
    /// refused at compile time by the static twin (SPY0609, #1956); that agrees only when the
    /// diagnostic's message is CPython's refusal text verbatim, and a spec python accepts
    /// that Sharpy refuses statically is an over-refusal divergence. A literal spec CPython refuses
    /// that COMPILES and is refused only by Core at runtime is a divergence too, even though the
    /// runtime text matches: the refusal came at the wrong stage (the twin is missing). Specs CPython
    /// accepts run and are compared like the engine column.
    /// </summary>
    private enum Column
    {
        Engine,
        Static,
    }

    private static readonly Column[] Columns = { Column.Engine, Column.Static };

    private static string ColumnName(Column column) => column == Column.Engine ? "engine" : "static";

    [Fact]
    public void FormatSpec_DifferentialSweep_MatchesCPython()
    {
        var oracle = PyOracle.TryLocate();
        if (oracle is null)
        {
            Output.WriteLine("SKIP: python3 >= 3.12 and run_programs.py not both available.");
            return;
        }

        var sw = Stopwatch.StartNew();
        var cells = GenerateCells(TargetCells);
        Assert.True(cells.Count >= 200 + EqualsPrefixStratumCells,
            $"the grammar generator produced only {cells.Count} distinct cells: the spec space or iter is too small.");
        // Anchor: the '='-after-radix-prefix stratum (#1959) is really in the corpus.
        int eqPrefixCells = cells.Count(IsEqualsPrefixCell);
        Assert.True(eqPrefixCells >= EqualsPrefixStratumCells,
            $"only {eqPrefixCells} '='+'#'+radix cells with room to pad were drawn (< {EqualsPrefixStratumCells}).");
        // Anchors (#1988 #1989 #1978): every operand kind of the literal roster is in the corpus, and
        // the explicit strata are really drawn — a kind or stratum dropped from generation is a failure,
        // not a quietly smaller sweep.
        var missingKinds = AllKinds.Where(k => !cells.Any(c => c.Kind == k)).ToList();
        Assert.True(missingKinds.Count == 0, "operand kinds with no cell: " + string.Join(", ", missingKinds));
        foreach (var (kind, spec) in AltFormCStratum().Concat(NegativeZeroStratum()).Concat(OperandKindStratum()))
            Assert.Contains(new Cell(kind, spec), cells);

        // --- Sharpy arm: production compile + execute, sequential, one program per cell x column. ---
        var sharpy = new Dictionary<string, ArmOutcome>(StringComparer.Ordinal);
        foreach (var cell in cells)
        {
            foreach (var column in Columns)
            {
                var r = CompileAndExecute(ProgramFor(cell, column, forPython: false), "format_spec_diff.spy", executionTimeoutMs: SharpyExecTimeoutMs);
                var staticRefusals = r.RawDiagnostics
                    .Where(d => d.Code == DiagnosticCodes.SemanticOverflow.InvalidFormatSpecification)
                    .Select(d => d.Message)
                    .ToList();
                sharpy[cell.Key(column)] = new ArmOutcome(r.Success && !r.TimedOut, r.StandardOutput, r.TimedOut, staticRefusals);
            }
        }

        // --- Python arm: one batch process, main() appended so CPython drives Sharpy's auto-entry.
        //     Both columns run under python too (they are the same program to CPython), so each
        //     column is compared with its own oracle run. ---
        var requests = new List<(int Id, string Source)>(cells.Count * Columns.Length);
        for (int i = 0; i < cells.Count; i++)
        {
            for (int c = 0; c < Columns.Length; c++)
                requests.Add((i * Columns.Length + c, ProgramFor(cells[i], Columns[c], forPython: true) + "\nmain()\n"));
        }
        var pythonById = oracle.RunBatch(requests);

        var allowlist = Allowlist.Load();
        var divergences = new List<(Cell Cell, Column Column, string Detail, bool Allowlisted)>();
        int staticAgreements = 0;
        for (int i = 0; i < cells.Count; i++)
        {
            var cell = cells[i];
            for (int c = 0; c < Columns.Length; c++)
            {
                var column = Columns[c];
                var key = cell.Key(column);
                var s = sharpy[key];
                pythonById.TryGetValue(i * Columns.Length + c, out var py);

                // The Python side must have run the cell (no syntax error, no timeout) to be an oracle.
                if (py is null || py.SyntaxError || py.TimedOut || s.TimedOut)
                    continue;

                string sharpyOut = Normalize(s.Stdout);
                string pythonOut = Normalize(py.Stdout);
                if (!s.Ok && column == Column.Static && IsStaticTwinOf(s, pythonOut))
                {
                    // A literal spec CPython refuses at runtime is refused by Sharpy at COMPILE time
                    // (SPY0609, the static twin — #1956) with CPython's exact wording: agreement.
                    staticAgreements++;
                }
                else if (!s.Ok)
                {
                    string refusals = s.StaticRefusals.Count > 0 ? " SPY0609='" + string.Join("' / '", s.StaticRefusals) + "'" : "";
                    divergences.Add((cell, column,
                        $"Sharpy failed to compile/run (python ok): spec='{cell.Spec}' pyout='{pythonOut}'{refusals}",
                        allowlist.Matches(key)));
                }
                else if (column == Column.Static && IsCPythonRefusal(pythonOut))
                {
                    // The literal spec COMPILED and ran although CPython refuses it. Even when Core's
                    // runtime ValueError repeats CPython's text (so stdout matches), the refusal came at
                    // the wrong stage: a literal spec is visible to the checker and must meet the static
                    // twin. Without this arm, removing the twin leaves the column green (#1956).
                    divergences.Add((cell, column,
                        $"{RuntimeOnlyReason}: spec='{cell.Spec}' sharpy='{sharpyOut}' python='{pythonOut}'",
                        allowlist.Matches(key)));
                }
                else if (sharpyOut != pythonOut)
                {
                    divergences.Add((cell, column, $"spec='{cell.Spec}': sharpy='{sharpyOut}' python='{pythonOut}'",
                        allowlist.Matches(key)));
                }
            }
        }

        var offenders = divergences.Where(d => !d.Allowlisted).ToList();
        var stale = allowlist.ExactKeys
            .Except(divergences.Select(d => d.Cell.Key(d.Column)), StringComparer.Ordinal)
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToList();
        int engineDivergent = divergences.Count(d => d.Column == Column.Engine);
        int staticDivergent = divergences.Count(d => d.Column == Column.Static);

        int staticRuntimeOnly = divergences.Count(d => d.Column == Column.Static && d.Detail.StartsWith(RuntimeOnlyReason, StringComparison.Ordinal));

        sw.Stop();
        Output.WriteLine(
            $"Format-spec differential: {cells.Count} cells x {Columns.Length} columns, {divergences.Count} divergent "
            + $"({offenders.Count} non-allowlisted, {stale.Count} stale-allowlisted); "
            + $"engine column {engineDivergent} divergent, static column {staticDivergent} divergent "
            + $"({staticAgreements} agreeing by a compile-time SPY0609 with CPython's ValueError/TypeError wording, "
            + $"{staticRuntimeOnly} refused only at runtime); "
            + $"{eqPrefixCells} '='+'#'+radix cells. Wall={sw.Elapsed.TotalSeconds:F1}s.");
        foreach (var o in offenders.Take(40))
            Output.WriteLine($"  DIVERGENCE {o.Cell.Key(o.Column)}  {o.Detail}");
        foreach (var k in stale)
            Output.WriteLine($"  STALE {k}");

        Assert.True(offenders.Count == 0,
            $"Format-spec differential: {offenders.Count} non-allowlisted divergence(s) between Sharpy and python3. "
            + "Fix the engine, or add an allowlist entry citing an issue:\n"
            + string.Join("\n", offenders.Take(40).Select(o => "  " + o.Cell.Key(o.Column) + "  " + o.Detail)));
        Assert.True(stale.Count == 0,
            $"Format-spec differential: {stale.Count} allowlist entr(ies) no longer diverge — delete them:\n"
            + string.Join("\n", stale.Select(k => "  " + k)));
        Assert.True(staticAgreements >= StaticTwinAgreementFloor,
            $"Format-spec differential: only {staticAgreements} static-column cell(s) agree by a compile-time SPY0609 "
            + $"(< {StaticTwinAgreementFloor}): the static twin no longer refuses the literal specs CPython refuses.");
    }

    /// <summary>
    /// The grammar-totality stratum (#2017): cells the seeded generator cannot reach (its alphabet is
    /// ASCII, it draws one separator and always a precision digit, width at most 12). The four named
    /// grammar rules (missing precision, both separators, the <c>'\xNN'</c> type-code spelling, too many
    /// digits) and every Unicode-<c>Nd</c> digit family — Arabic-Indic, fullwidth and the non-BMP
    /// mathematical digits (a surrogate pair) beside an ASCII control — at every site the one digit
    /// reader (<c>PyFormatSpec.TryReadDecimal</c>) serves: width and precision on the four spec routes
    /// (<c>format()</c>, <c>str.format</c>, f-string, t-string), and the field index (plain, with a
    /// spec the static twin must pair with the right operand, nested in a spec) and item key on
    /// <c>str.format</c>, the only route with a field name. Every cell runs in both columns against
    /// python3. Python 3.12 has no t-strings: a rendered <c>Template</c> is Sharpy's f-string
    /// rendering (template_strings.md), so its python program is the f-string's. No allowlist row may
    /// name a <c>grammar::</c> key: the stratum is total.
    /// </summary>
    [Fact]
    public void FormatSpec_GrammarTotalityStratum_MatchesCPython()
    {
        var oracle = PyOracle.TryLocate();
        if (oracle is null)
        {
            Output.WriteLine("SKIP: python3 >= 3.12 and run_programs.py not both available.");
            return;
        }

        var cells = GrammarTotalityStratum().ToList();
        // Anchor to literals: 22 spec cells x 4 routes + 17 field cells, each in two columns.
        Assert.Equal(22 * 4 + 17, cells.Count);
        Assert.Equal(cells.Count, cells.Select(c => c.Label).Distinct(StringComparer.Ordinal).Count());

        var requests = new List<(int Id, string Source)>();
        var sharpy = new Dictionary<int, ArmOutcome>();
        for (int i = 0; i < cells.Count; i++)
        {
            for (int c = 0; c < Columns.Length; c++)
            {
                int id = i * Columns.Length + c;
                var (sharpySource, pythonSource) = cells[i].Programs(Columns[c]);
                var r = CompileAndExecute(sharpySource, "format_grammar_stratum.spy", executionTimeoutMs: SharpyExecTimeoutMs);
                var staticRefusals = r.RawDiagnostics
                    .Where(d => d.Code == DiagnosticCodes.SemanticOverflow.InvalidFormatSpecification)
                    .Select(d => d.Message)
                    .ToList();
                sharpy[id] = new ArmOutcome(r.Success && !r.TimedOut, r.StandardOutput, r.TimedOut, staticRefusals);
                requests.Add((id, pythonSource + "\nmain()\n"));
            }
        }
        var pythonById = oracle.RunBatch(requests);

        var divergences = new List<string>();
        int staticAgreements = 0;
        int ndAgreements = 0;
        for (int i = 0; i < cells.Count; i++)
        {
            for (int c = 0; c < Columns.Length; c++)
            {
                int id = i * Columns.Length + c;
                string key = "grammar::" + ColumnName(Columns[c]) + "::" + cells[i].Label;
                var s = sharpy[id];
                if (!pythonById.TryGetValue(id, out var py) || py.SyntaxError || py.TimedOut || s.TimedOut)
                {
                    divergences.Add($"{key}: no oracle verdict (python syntax error / timeout)");
                    continue;
                }

                string sharpyOut = Normalize(s.Stdout);
                string pythonOut = Normalize(py.Stdout);
                bool agrees;
                if (!s.Ok)
                    agrees = Columns[c] == Column.Static && IsStaticTwinOf(s, pythonOut);
                else
                    agrees = sharpyOut == pythonOut && !(Columns[c] == Column.Static && cells[i].StaticSpec && IsCPythonRefusal(pythonOut));
                if (!agrees)
                {
                    string refusals = s.StaticRefusals.Count > 0 ? " SPY0609='" + string.Join("' / '", s.StaticRefusals) + "'" : "";
                    divergences.Add($"{key}: sharpy{(s.Ok ? "" : " (failed)")}='{sharpyOut}'{refusals} python='{pythonOut}'");
                    continue;
                }
                if (!s.Ok)
                    staticAgreements++;
                if (cells[i].Label.Contains("::nd-", StringComparison.Ordinal))
                    ndAgreements++;
            }
        }

        Output.WriteLine($"Grammar-totality stratum: {cells.Count} cells x {Columns.Length} columns, {divergences.Count} divergent, "
            + $"{staticAgreements} agreeing by a compile-time SPY0609, {ndAgreements} non-ASCII-digit agreements.");
        foreach (var d in divergences)
            Output.WriteLine("  DIVERGENCE " + d);

        var allowlisted = Allowlist.Load().ExactKeys.Where(k => k.StartsWith("grammar::", StringComparison.Ordinal)).ToList();
        Assert.True(allowlisted.Count == 0, "the grammar-totality stratum takes no allowlist rows: " + string.Join(", ", allowlisted));
        Assert.True(divergences.Count == 0,
            $"Grammar-totality stratum: {divergences.Count} divergence(s) from python3:\n  " + string.Join("\n  ", divergences));
        // Positive controls: the refusal rows really met the static twin, and the Nd rows really ran.
        Assert.True(staticAgreements >= GrammarStaticAgreementFloor,
            $"only {staticAgreements} stratum cell(s) agreed by a compile-time SPY0609 (< {GrammarStaticAgreementFloor}).");
        Assert.True(ndAgreements == NdCellColumns,
            $"{ndAgreements} of {NdCellColumns} non-ASCII-digit cell-columns agreed with python3.");
    }

    // The spec routes: label, Sharpy expression for a literal spec, for a spec in the variable `spec`, and
    // the python spelling of each (the t-string route renders as the f-string does).
    private static readonly (string Route, Func<string, string> Static, string Engine, Func<string, string> PyStatic, string PyEngine)[] SpecRoutes =
    {
        ("format", spec => $"format(v, \"{spec}\")", "format(v, spec)", spec => $"format(v, \"{spec}\")", "format(v, spec)"),
        ("strformat", spec => $"\"{{:{spec}}}\".format(v)", "\"{:{}}\".format(v, spec)", spec => $"\"{{:{spec}}}\".format(v)", "\"{:{}}\".format(v, spec)"),
        ("fstring", spec => $"f\"{{v:{spec}}}\"", "f\"{v:{spec}}\"", spec => $"f\"{{v:{spec}}}\"", "f\"{v:{spec}}\""),
        ("tstring", spec => $"str(t\"{{v:{spec}}}\")", "str(t\"{v:{spec}}\")", spec => $"f\"{{v:{spec}}}\"", "f\"{v:{spec}}\""),
    };

    // Digit families: label, then the digits 5, 2 and 1. The mathematical bold digits are non-BMP
    // (U+1D7D3, U+1D7D0, U+1D7CF) — each a surrogate pair in a CLR string.
    private static readonly (string Family, string Five, string Two, string One)[] DigitFamilies =
    {
        ("ascii", "5", "2", "1"),
        ("nd-arabic", "\u0665", "\u0662", "\u0661"),
        ("nd-fullwidth", "\uFF15", "\uFF12", "\uFF11"),
        ("nd-mathbold", "\U0001D7D3", "\U0001D7D0", "\U0001D7CF"),
    };

    // Static-column agreements the stratum must produce: the 5 named-rule spec cells + the Nd too-many
    // cell on 4 routes (24), plus the 4 field-with-spec cells (28). Measured 28 at the #2017 commit.
    private const int GrammarStaticAgreementFloor = 28;

    // Non-ASCII-digit cells: (3 Nd families x 4 spec cells + the too-many and mixed-width cells) x 4
    // routes + 3 Nd families x 4 field cells, each in two columns.
    private const int NdCellColumns = ((3 * 4 + 2) * 4 + 3 * 4) * 2;

    private sealed record GrammarCell(string Label, bool StaticSpec, Func<Column, (string Sharpy, string Python)> Programs);

    private static IEnumerable<GrammarCell> GrammarTotalityStratum()
    {
        var specCells = new List<(string Label, string Decl, string Spec)>
        {
            // The four named rules (#2017, landed with #1984): CPython 3.12's text on every route.
            ("rule-missing-precision", "v: float = 1.5", ".f"),
            ("rule-both-separators", "v: int = 1", ",_d"),
            ("rule-xNN-code", "v: int = 1", "\u00e9"),
            ("rule-too-many-width", "v: int = 1", "99999999999999999999d"),
            ("rule-too-many-precision", "v: float = 1.5", ".99999999999999999999f"),
            ("nd-arabic-too-many", "v: int = 1", string.Concat(Enumerable.Repeat("\u0669", 20)) + "d"),
            ("nd-arabic-mixed-width", "v: int = 65", "1\u0665d"),
        };
        foreach (var (family, five, _, _) in DigitFamilies)
        {
            specCells.Add((family + "-width-int", "v: int = 65", five + "d"));
            specCells.Add((family + "-width-str", "v: str = \"ab\"", five));
        }
        foreach (var (family, five, two, _) in DigitFamilies)
        {
            if (family == "ascii")
                continue;
            specCells.Add((family + "-precision-float", "v: float = 1.23456", "." + two + "f"));
            specCells.Add((family + "-width-precision", "v: float = 1.23456", five + "." + two + "f"));
        }
        specCells.Add(("ascii-precision-float", "v: float = 1.23456", ".2f"));

        foreach (var (label, decl, spec) in specCells)
        {
            foreach (var route in SpecRoutes)
            {
                yield return new GrammarCell($"spec::{route.Route}::{label}", StaticSpec: true, column =>
                    column == Column.Static
                        ? (GrammarProgram(decl, route.Static(spec)), GrammarProgram(decl, route.PyStatic(spec)))
                        : (GrammarProgram(decl + $"\n    spec: str = \"{spec}\"", route.Engine),
                           GrammarProgram(decl + $"\n    spec: str = \"{spec}\"", route.PyEngine)));
            }
        }

        var fieldCells = new List<(string Label, string Template, string Args, bool StaticSpec)>
        {
            ("field-too-many", "{99999999999999999999}", "\"a\"", false),
        };
        foreach (var (family, _, _, one) in DigitFamilies)
        {
            fieldCells.Add(($"{family}-field-index", "{" + one + "}", "\"a\", \"b\"", false));
            // The static twin must pair field `1` with the str operand: CPython refuses 'd' for 'str'.
            fieldCells.Add(($"{family}-field-index-spec", "{" + one + ":d}", "1.5, \"s\"", true));
            fieldCells.Add(($"{family}-field-index-nested", "{0:{" + one + "}}", "65, \"5\"", false));
            fieldCells.Add(($"{family}-item-key", "{0[" + one + "]}", "xs", false));
        }

        foreach (var (label, template, args, staticSpec) in fieldCells)
        {
            const string decl = "xs: list[str] = [\"x\", \"y\"]";
            yield return new GrammarCell($"field::strformat::{label}", staticSpec, column =>
            {
                string program = column == Column.Static
                    ? GrammarProgram(decl, $"\"{template}\".format({args})")
                    : GrammarProgram(decl + $"\n    tpl: str = \"{template}\"", $"tpl.format({args})");
                return (program, program);
            });
        }
    }

    private static string GrammarProgram(string decls, string expr) =>
        "def main() -> None:\n"
        + $"    {decls}\n"
        + "    try:\n"
        + $"        print(\"[\" + {expr} + \"]\")\n"
        + "    except ValueError as e:\n"
        + "        print(\"ValueError:\", e)\n"
        + "    except TypeError as e:\n"
        + "        print(\"TypeError:\", e)\n"
        + "    except IndexError as e:\n"
        + "        print(\"IndexError:\", e)\n"
        + "    except KeyError as e:\n"
        + "        print(\"KeyError:\", e)\n";

    private static string Normalize(string s) => s.Replace("\r\n", "\n").TrimEnd('\n');

    // CPython's two refusals of a spec: ValueError (the operand's __format__ rejects it) and TypeError
    // (the operand has no __format__, #1988). The static twin carries either text verbatim.
    private static readonly string[] PythonRefusalPrefixes = { "ValueError: ", "TypeError: " };

    private const string RuntimeOnlyReason = "literal spec refused only at runtime: static twin missing";

    /// <summary>
    /// Whether a Sharpy compile failure is the static twin of CPython's runtime refusal: python3
    /// printed exactly one <c>ValueError: msg</c> or <c>TypeError: msg</c> line and Sharpy reported
    /// SPY0609 with that same <c>msg</c>. Any other compile failure — a different code, a different
    /// wording, or a refusal of a spec CPython accepts — stays a divergence.
    /// </summary>
    private static bool IsStaticTwinOf(ArmOutcome sharpy, string pythonOut)
    {
        if (!IsCPythonRefusal(pythonOut))
            return false;
        var message = pythonOut.Substring(pythonOut.IndexOf(": ", StringComparison.Ordinal) + 2);
        return sharpy.StaticRefusals.Contains(message, StringComparer.Ordinal);
    }

    /// <summary>Whether python3 refused the cell: it printed exactly one <c>ValueError: msg</c> or <c>TypeError: msg</c> line.</summary>
    private static bool IsCPythonRefusal(string pythonOut) =>
        PythonRefusalPrefixes.Any(p => pythonOut.StartsWith(p, StringComparison.Ordinal)) && !pythonOut.Contains('\n');

    /// <summary>
    /// The cell's program. The two languages share it except for the <c>formattable</c> kind's class:
    /// Sharpy spells <c>__format__</c> as <c>System.IFormattable</c> (<c>to_string(fmt, provider)</c>,
    /// #1988), python3 as <c>__format__</c> — both return <c>F&lt;spec&gt;</c>, so the cell is a real
    /// value-parity cell, not a Sharpy-only one.
    /// </summary>
    private static string ProgramFor(Cell cell, Column column, bool forPython)
    {
        var (ctype, lit) = cell.Kind switch
        {
            "int" => ("int", "42"),
            "int_neg" => ("int", "-255"),
            "float" => ("float", "3.5"),
            "float_whole" => ("float", "3.0"),
            "float_negzero" => ("float", "-0.0"),
            "float_tiny" => ("float", "-1e-9"),
            "bool" => ("bool", "True"),
            "str" => ("str", "\"ab\""),
            "list" => ("list[int]", "[1, 2]"),
            "dict" => ("dict[str, int]", "{\"a\": 1}"),
            "set" => ("set[int]", "{1}"),
            "tuple" => ("tuple[int, int]", "(1, 2)"),
            "formattable" => ("F", "F()"),
            "dunder_format" => ("D", "D()"),
            _ => throw new ArgumentOutOfRangeException(nameof(cell), cell.Kind, "no declaration for this operand kind"),
        };
        // dunder_format (#2009, R-CB): Sharpy's own __format__ — the SAME program text in both
        // languages, the synthesized System.IFormattable carrying the spec.
        string prelude = cell.Kind == "dunder_format"
            ? "class D:\n    def __format__(self, s: str) -> str:\n        return \"D<\" + s + \">\"\n\n\n"
            : cell.Kind != "formattable" ? ""
            : forPython
                ? "class F:\n    def __format__(self, s):\n        return \"F<\" + s + \">\"\n\n\n"
                : "from System import IFormattable, IFormatProvider\n\n\n"
                    + "class F(IFormattable):\n"
                    + "    def to_string(self, fmt: str, provider: IFormatProvider) -> str:\n"
                    + "        return \"F<\" + fmt + \">\"\n\n\n";
        // The spec grammar never generates '"', '\\', '{' or '}', so it embeds directly in a literal.
        // Engine column: the spec travels through a str variable, so no static check can see it.
        string specDecl = column == Column.Engine ? $"    spec: str = \"{cell.Spec}\"\n" : "";
        string specArg = column == Column.Engine ? "spec" : $"\"{cell.Spec}\"";
        return prelude
            + "def main() -> None:\n"
            + $"    v: {ctype} = {lit}\n"
            + specDecl
            + "    try:\n"
            + $"        print(format(v, {specArg}))\n"
            + "    except ValueError as e:\n"
            + "        print(\"ValueError:\", e)\n"
            + "    except TypeError as e:\n"
            + "        print(\"TypeError:\", e)\n";
    }

    // The literal roster of operand kinds (the anchor): the uniform draw's five, the '='-prefix
    // stratum's int_neg, and the kind-axis stratum's seven (#1988 #1989).
    private static readonly string[] UniformKinds = { "int", "float", "float_whole", "bool", "str" };
    private static readonly string[] KindAxisKinds =
        { "float_negzero", "float_tiny", "list", "dict", "set", "tuple", "formattable" };
    // dunder_format rides only in the explicit OperandKindStratum, so the seeded kind-axis draw is
    // unchanged.
    private static readonly string[] AllKinds = UniformKinds.Append("int_neg").Concat(KindAxisKinds)
        .Append("dunder_format").ToArray();

    /// <summary>
    /// #1978: '#' with the 'c' presentation type, alone and beside the other int rules it competes
    /// with (sign, z, '=' fill). The uniform draw reaches 'c' but rarely with '#' and a legal width,
    /// so the stratum is explicit.
    /// </summary>
    private static IEnumerable<(string Kind, string Spec)> AltFormCStratum() =>
        from kind in new[] { "int", "int_neg", "bool" }
        from spec in new[] { "#c", "*=#5c", "+#c", "#zc", "#10c", "<#c" }
        select (kind, spec);

    /// <summary>
    /// #1989: every rendering of a negative zero or a negative value that rounds to zero, under 'z',
    /// across the presentations whose digits can all be zero (e E % g G n f, the type-less float, with
    /// and without a sign). CPython coerces on the RENDERED text, so -1e-9 under 'z.1%' is '0.0%'.
    /// </summary>
    private static IEnumerable<(string Kind, string Spec)> NegativeZeroStratum() =>
        from kind in new[] { "float_negzero", "float_tiny" }
        from spec in new[] { "z", "z.0e", "z.1e", "z.2E", "z.0%", "z.1%", "z.1g", "z.3G", "z.1n", "z.0f", "z.3", "+z.1e", " z.2%", "z010.1e" }
        select (kind, spec);

    /// <summary>
    /// #1988: the operand kinds with no __format__ (list, dict, set, tuple) and one that owns its spec
    /// (formattable; and dunder_format, the same protocol spelled <c>def __format__</c>, #2009) under the empty spec (str(value), never refused — the positive control), a
    /// padding spec, a numeric code and a spec no builtin kind parses.
    /// </summary>
    private static IEnumerable<(string Kind, string Spec)> OperandKindStratum() =>
        from kind in new[] { "list", "dict", "set", "tuple", "formattable", "dunder_format" }
        from spec in new[] { "", ">10", "d", "abc", "*^9" }
        select (kind, spec);

    private static List<Cell> GenerateCells(int target)
    {
        // Grammar slots. Fill is restricted to a literal-safe alphabet (no '"', '\\', '{', '}').
        char[] fillChars = { '*', '0', ' ', '~', '@' };
        char[] alignChars = { '<', '>', '^', '=' };
        char[] signChars = { '+', '-', ' ' };
        char[] groupChars = { ',', '_' };
        char[] typeChars = { 'd', 'n', 'f', 'F', 'e', 'E', 'g', 'G', 'x', 'X', 'o', 'b', 'c', '%', 's' };
        string[] kinds = UniformKinds;

        var rng = new Random(GeneratedSeed);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var cells = new List<Cell>();

        // Deterministic sampling: draw grammar slots from the seeded RNG until the target count of
        // distinct, in-scope cells is reached, with a hard iteration ceiling so a shrinking spec
        // space can never loop forever.
        long ceiling = Math.Max(target * 60L, 4000L);
        for (long iter = 0; iter < ceiling && cells.Count < target; iter++)
        {
            int fillAlign = rng.Next(0, 3); // 0 none, 1 lone align, 2 fill+align
            char fill = fillChars[rng.Next(fillChars.Length)];
            char align = alignChars[rng.Next(alignChars.Length)];
            bool hasSign = rng.Next(2) == 1;
            char sign = signChars[rng.Next(signChars.Length)];
            bool hasZ = rng.Next(2) == 1;
            bool hasHash = rng.Next(2) == 1;
            bool hasZero = rng.Next(2) == 1;
            int width = rng.Next(0, 13);
            bool hasGroup = rng.Next(2) == 1;
            char groupSep = groupChars[rng.Next(groupChars.Length)];
            bool hasPrec = rng.Next(2) == 1;
            int prec = rng.Next(0, 7);
            bool hasType = rng.Next(2) == 1;
            char type = typeChars[rng.Next(typeChars.Length)];
            string kind = kinds[rng.Next(kinds.Length)];

            string spec = BuildSpec(fillAlign, fill, align, hasSign, sign, hasZ, hasHash, hasZero,
                width, hasGroup, groupSep, hasPrec, prec, hasType, type);
            var cell = new Cell(kind, spec);
            if (seen.Add(cell.Key(Column.Engine)))
                cells.Add(cell);
        }

        // The '='-after-radix-prefix stratum (#1959). The uniform draw above almost never combines an
        // '=' alignment, the '#' radix prefix, a radix type and a width wide enough to pad (the fixed
        // seed drew none), so the explicit-fill / lone-'=' path of the alignment switch went unswept.
        // A second, separately seeded draw restricted to that sub-grammar adds a fixed number of such
        // cells: [fill]'=' [sign] '#' ['0'] width(10..14) ['_'] {x X o b}, on 42, -255 and True.
        char[] radixTypes = { 'x', 'X', 'o', 'b' };
        string[] stratumKinds = { "int", "int_neg", "bool" };
        var stratumRng = new Random(EqualsPrefixStratumSeed);
        int added = 0;
        for (int iter = 0; iter < EqualsPrefixStratumCells * 40 && added < EqualsPrefixStratumCells; iter++)
        {
            int fillAlign = stratumRng.Next(1, 3); // 1 lone '=', 2 fill + '='
            char fill = fillChars[stratumRng.Next(fillChars.Length)];
            bool hasSign = stratumRng.Next(2) == 1;
            char sign = signChars[stratumRng.Next(signChars.Length)];
            bool hasZero = stratumRng.Next(4) == 0;
            int width = stratumRng.Next(10, 15);
            bool hasGroup = stratumRng.Next(2) == 1;
            char type = radixTypes[stratumRng.Next(radixTypes.Length)];
            string kind = stratumKinds[stratumRng.Next(stratumKinds.Length)];

            string spec = BuildSpec(fillAlign, fill, '=', hasSign, sign, hasZ: false, hasHash: true, hasZero,
                width, hasGroup, '_', hasPrec: false, prec: 0, hasType: true, type);
            var cell = new Cell(kind, spec);
            if (seen.Add(cell.Key(Column.Engine)))
            {
                cells.Add(cell);
                added++;
            }
        }

        // The kind-axis stratum (#1988 #1989): the uniform grammar draw over the operand kinds the
        // uniform corpus above does not carry — a separately seeded draw, so the uniform corpus (and
        // its measured floor) is unaffected by the kind roster's size.
        var kindRng = new Random(KindAxisStratumSeed);
        int kindAdded = 0;
        for (int iter = 0; iter < KindAxisStratumCells * 40 && kindAdded < KindAxisStratumCells; iter++)
        {
            int fillAlign = kindRng.Next(0, 3);
            char fill = fillChars[kindRng.Next(fillChars.Length)];
            char align = alignChars[kindRng.Next(alignChars.Length)];
            bool hasSign = kindRng.Next(2) == 1;
            char sign = signChars[kindRng.Next(signChars.Length)];
            bool hasZ = kindRng.Next(2) == 1;
            bool hasHash = kindRng.Next(2) == 1;
            bool hasZero = kindRng.Next(2) == 1;
            int width = kindRng.Next(0, 13);
            bool hasGroup = kindRng.Next(2) == 1;
            char groupSep = groupChars[kindRng.Next(groupChars.Length)];
            bool hasPrec = kindRng.Next(2) == 1;
            int prec = kindRng.Next(0, 7);
            bool hasType = kindRng.Next(2) == 1;
            char type = typeChars[kindRng.Next(typeChars.Length)];
            string kind = KindAxisKinds[kindRng.Next(KindAxisKinds.Length)];

            string spec = BuildSpec(fillAlign, fill, align, hasSign, sign, hasZ, hasHash, hasZero,
                width, hasGroup, groupSep, hasPrec, prec, hasType, type);
            var cell = new Cell(kind, spec);
            if (seen.Add(cell.Key(Column.Engine)))
            {
                cells.Add(cell);
                kindAdded++;
            }
        }

        // The explicit strata: every listed (kind, spec) is drawn, whatever the seeds produced.
        foreach (var (kind, spec) in AltFormCStratum().Concat(NegativeZeroStratum()).Concat(OperandKindStratum()))
        {
            var cell = new Cell(kind, spec);
            if (seen.Add(cell.Key(Column.Engine)))
                cells.Add(cell);
        }

        return cells;
    }

    /// <summary>
    /// A cell of the #1959 class: '=' alignment + the '#' prefix on a radix type, with a width of at
    /// least 10 (every radix rendering of 42, -255 and True is narrower than that before '_' grouping,
    /// so the fill lands between the prefix and the digits).
    /// </summary>
    private static bool IsEqualsPrefixCell(Cell cell)
    {
        var m = Regex.Match(cell.Spec, @"^.?=[+\- ]?#0?(\d+)_?[xXob]$");
        return m.Success && (cell.Kind == "int" || cell.Kind == "int_neg" || cell.Kind == "bool")
            && int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) >= 10;
    }

    private static string BuildSpec(
        int fillAlign, char fill, char align, bool hasSign, char sign, bool hasZ, bool hasHash,
        bool hasZero, int width, bool hasGroup, char groupSep, bool hasPrec, int prec, bool hasType, char type)
    {
        var sb = new StringBuilder();
        if (fillAlign == 2)
        {
            sb.Append(fill);
            sb.Append(align);
        }
        else if (fillAlign == 1)
        {
            sb.Append(align);
        }
        if (hasSign)
            sb.Append(sign);
        if (hasZ)
            sb.Append('z');
        if (hasHash)
            sb.Append('#');
        if (hasZero)
            sb.Append('0');
        if (width > 0)
            sb.Append(width.ToString(CultureInfo.InvariantCulture));
        if (hasGroup)
            sb.Append(groupSep);
        if (hasPrec)
        {
            sb.Append('.');
            sb.Append(prec.ToString(CultureInfo.InvariantCulture));
        }
        if (hasType)
            sb.Append(type);
        return sb.ToString();
    }

    private sealed record ArmOutcome(bool Ok, string Stdout, bool TimedOut, IReadOnlyList<string> StaticRefusals);

    private sealed record PyResult(bool Ok, string Stdout, string Stderr, bool TimedOut, bool SyntaxError);

    // ------------------------------------------------------------------------------------------- //
    // python3 bridge (a focused replica of DifferentialExecutionTests.PythonExecOracle)
    // ------------------------------------------------------------------------------------------- //

    private sealed class PyOracle
    {
        private readonly string _pythonExe;
        private readonly string _scriptPath;

        private PyOracle(string pythonExe, string scriptPath)
        {
            _pythonExe = pythonExe;
            _scriptPath = scriptPath;
        }

        public static PyOracle? TryLocate()
        {
            string? script = FindScript();
            if (script is null)
                return null;
            foreach (var candidate in new[] { "python3", "python" })
            {
                if (HasSupportedVersion(candidate))
                    return new PyOracle(candidate, script);
            }
            return null;
        }

        private static string? FindScript()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "sharpy.sln")))
                {
                    var candidate = Path.Combine(dir.FullName, "build_tools", "differential_exec", "run_programs.py");
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

        public IReadOnlyDictionary<int, PyResult> RunBatch(IReadOnlyList<(int Id, string Source)> requests)
        {
            string batchPath = Path.Combine(Path.GetTempPath(), $"sharpy-fmtspec-{Guid.NewGuid():N}.jsonl");
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

    // ------------------------------------------------------------------------------------------- //
    // Allowlist (key ratchet, mirroring the interop/differential allowlists)
    // ------------------------------------------------------------------------------------------- //

    private sealed class Allowlist
    {
        private readonly HashSet<string> _exact;

        private Allowlist(HashSet<string> exact) => _exact = exact;

        public bool Matches(string key) => _exact.Contains(key);

        public IReadOnlyCollection<string> ExactKeys => _exact;

        public static Allowlist Load()
        {
            var exact = new HashSet<string>(StringComparer.Ordinal);
            var path = FindPath();
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

        private static string? FindPath()
        {
            var current = AppContext.BaseDirectory;
            while (current != null)
            {
                var dir = Path.Combine(current, "src", "Sharpy.Compiler.Tests", "Conformance");
                if (Directory.Exists(dir))
                    return Path.Combine(dir, "format-spec-differential-allowlist.txt");
                current = Directory.GetParent(current)?.FullName;
            }
            return null;
        }
    }
}
