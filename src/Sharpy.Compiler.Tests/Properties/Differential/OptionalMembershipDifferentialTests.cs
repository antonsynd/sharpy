using System.Globalization;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.TestInfrastructure.Integration;
using Sharpy.TestInfrastructure.Oracles;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Differential;

/// <summary>
/// The class guard for membership with an optional left operand (#2635): a needle of type <c>T | None</c> is
/// tested against every container kind exactly as the <c>T</c> needle is, and its <c>None</c> answers what
/// CPython answers — <c>False</c> for <c>in</c> (an equality scan finds no element equal to <c>None</c>),
/// <c>True</c> for <c>not in</c>, and <c>TypeError</c> at run time where CPython raises (a <c>str</c> or
/// <c>bytes</c> container) — owner rulings 2026-11-12. Never SPY0222, SPY0908 or a CLR exception. The literal
/// <c>None</c> into a non-optional slot is refused (SPY0222) by owner ruling A, 2026-11-12; into an optional element
/// type it is accepted (the plan's named control, not generated here).
/// <para><b>Generated, never listed.</b> The full product of five axes, with no exclusion; the count is asserted
/// from the axis sizes, not the generator:</para>
/// <list type="bullet">
/// <item><b>payload</b> — the image of <see cref="PrimitiveCatalog.GetAllPrimitives"/> (one canonical name per
/// CLR type) minus <see cref="ExcludedPrimitives"/>, each excluded with its reason, plus two composite kinds a
/// primitive census cannot reach: a value-typed <c>tuple[int, int]</c> and a reference-typed user class;</item>
/// <item><b>container</b> — the closed set of the spec's "Container kinds" table
/// (<c>membership_operators.md</c>), read from the spec and mapped row by row (<see cref="SpecRowMap"/>);</item>
/// <item><b>needle form</b> — the plain <c>T</c> (the control twin), <c>T | None</c>, and the literal
/// <c>None</c>;</item>
/// <item><b>operator</b> — the <see cref="BinaryOperator"/> members that are membership tests;</item>
/// <item><b>position</b> — the checker's two membership sites (a binary operator, a comparison-chain link) and
/// the two statement-level contexts that lower a binary operator differently (an <c>if</c> condition, a
/// comprehension filter).</item>
/// </list>
/// <para><b>Expected map</b> (<see cref="Expect"/>). COMPARED cells print CPython 3.15's values (the
/// <c>.python-version</c> pin). HAND-EXPECTED cells — a typed needle the slot cannot hold (<see cref="SlotHolds"/>),
/// refused by SPY0222 under the deviation <c>membership-needle-type-mismatch</c> (owner ruling B for bool, float and
/// decimal into an <c>int</c> slot), the literal <c>None</c> (SPY0222, ruling A), and chain links (SPY0519, #2648) —
/// are counted apart in the summary line. One program per payload is compiled;
/// cells refused at compile time are attributed by line, removed and the rest recompiled until it runs.</para>
/// <para><b>Known red.</b> <c>optional-membership-differential-allowlist.txt</c> (Conformance): one row per
/// <c>container/form</c>, each pinning its exact divergent cells and their observed Sharpy verdicts (<c>cells=</c>,
/// <c>pin=</c> and one <c>#= cell -&gt; verdict</c> line per cell). A cell that starts or stops diverging inside a
/// row, or whose wrong answer changes, fails the test; the problems and a regenerated allowlist are written under
/// <c>.claude/tmp/</c>, and every divergence always is.</para>
/// <para>Skips when no python3 ≥ 3.15 answers, except under <c>SHARPY_REQUIRE_PYTHON_ORACLE=1</c> (CI).</para>
/// </summary>
public class OptionalMembershipDifferentialTests : IntegrationTestBase
{
    public OptionalMembershipDifferentialTests(ITestOutputHelper output) : base(output) => MuteCompilePath = true;

    private const string AllowlistFileName = "optional-membership-differential-allowlist.txt";

    /// <summary>The primitives the payload axis leaves out, each with why its python twin cannot be spelled.</summary>
    private static readonly Dictionary<string, string> ExcludedPrimitives = new(StringComparer.Ordinal)
    {
        ["char"] = "python has no character type",
        ["object"] = "an `object` element slot admits every needle: no needle-type decision to make",
        ["void"] = "None itself, the needle form axis",
    };

    private enum PayloadKind { Integer, Float, Decimal, Bool, Str, Tuple, Class }

    private sealed record Payload(string Name, PayloadKind Kind, string Member, string Other, string NonMember);

    private enum Container { Tuple, List, Set, FrozenSet, Dict, DictKeys, DictValues, ContainsDunder, Str, Bytes, Range }

    /// <summary>
    /// The spec's "Container kinds" rows (first cell, verbatim) → the containers generated for that row, or the
    /// reason none is. Read against the spec by <see cref="AssertContainerAxisIsTheSpecTable"/>.
    /// </summary>
    private static readonly (string Row, Container[] Generated, string? Excluded)[] SpecRowMap =
    {
        ("`list[T]`, `set[T]`, `frozenset[T]`, `array[T]`", new[] { Container.List, Container.Set, Container.FrozenSet },
            "array[T]: python's array.array is a typecode-keyed stdlib class with no T | None spelling"),
        ("`dict[K, V]`", new[] { Container.Dict, Container.DictKeys, Container.DictValues }, null),
        ("`str`", new[] { Container.Str }, null),
        ("`bytes`", new[] { Container.Bytes }, null),
        ("`tuple[...]`", new[] { Container.Tuple }, null),
        ("`range(...)`", new[] { Container.Range }, null),
        ("a class with `__contains__` (declared or inherited)", new[] { Container.ContainsDunder }, null),
        ("a CLR type with a public `Contains`, or any `IEnumerable[T]`", Array.Empty<Container>(),
            "no python twin; the CLR Contains arm is the same element-slot decision"),
    };

    private enum Form { Plain, Optional, NoneLiteral }

    private enum Position { Expression, IfCondition, ComprehensionFilter, ChainLink }

    private sealed record Cell(int Id, Payload Payload, Container Container, Form Form, BinaryOperator Operator, Position Position)
    {
        public string Key => $"{Payload.Name}/{Container}/{Form}/{OperatorSpelling(Operator)}/{Position}";
        /// <summary>The allowlist row: the container × whether the needle can be None (<c>T | None</c> and the literal
        /// <c>None</c> share one row, as they share the rulings). Rows pin their exact cells, so the key's coarseness
        /// hides nothing.</summary>
        public string RowKey => $"{Container}/{(Form == Form.Plain ? "Plain" : "NoneNeedle")}";
    }

    private static string OperatorSpelling(BinaryOperator op) => op == BinaryOperator.In ? "in" : "not in";

    private static readonly BinaryOperator[] Operators =
        Enum.GetValues<BinaryOperator>().Where(o => o is BinaryOperator.In or BinaryOperator.NotIn).ToArray();

    /// <summary>One canonical Sharpy name per CLR primitive (aliases dropped), ordinal-sorted.</summary>
    private static IReadOnlyList<string> CanonicalPrimitives() =>
        PrimitiveCatalog.GetAllPrimitives()
            .Where(p => PrimitiveCatalog.GetByClrType(p.Info.ClrType)?.SharpyName == p.Name)
            .Select(p => p.Name)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

    private static List<Payload> GeneratePayloads()
    {
        var payloads = new List<Payload>();
        foreach (var name in CanonicalPrimitives())
        {
            if (ExcludedPrimitives.ContainsKey(name))
                continue;
            var info = PrimitiveCatalog.GetByName(name)!;
            var payload = info.Kind switch
            {
                PrimitiveCatalog.NumericKind.SignedInteger or PrimitiveCatalog.NumericKind.UnsignedInteger
                    => new Payload(name, PayloadKind.Integer, "1", "2", "5"),
                PrimitiveCatalog.NumericKind.FloatingPoint => new Payload(name, PayloadKind.Float, "1.5", "2.5", "5.5"),
                // The python twin spells it through the prelude's `from decimal import Decimal as decimal`.
                PrimitiveCatalog.NumericKind.Decimal
                    => new Payload(name, PayloadKind.Decimal, "decimal(1)", "decimal(2)", "decimal(5)"),
                _ when name == "bool" => new Payload(name, PayloadKind.Bool, "True", "True", "False"),
                _ when name == "str" => new Payload(name, PayloadKind.Str, "\"a\"", "\"b\"", "\"z\""),
                // A primitive the generator has no literals for: the totality assertion names it.
                _ => null,
            };
            if (payload != null)
                payloads.Add(payload);
        }
        payloads.Add(new Payload("tuple[int, int]", PayloadKind.Tuple, "(1, 2)", "(3, 4)", "(5, 6)"));
        payloads.Add(new Payload("K", PayloadKind.Class, "K(1)", "K(2)", "K(5)"));
        return payloads;
    }

    /// <summary>The full product of the axes, with no exclusion: a needle the container's slot cannot hold is a CELL
    /// (its expectation is the spec's refusal), never a skip.</summary>
    private static List<Cell> GenerateCells(IReadOnlyList<Payload> payloads)
    {
        var cells = new List<Cell>();
        foreach (var payload in payloads)
            foreach (var container in Enum.GetValues<Container>())
            {
                foreach (var form in Enum.GetValues<Form>())
                    foreach (var op in Operators)
                        foreach (var position in Enum.GetValues<Position>())
                            cells.Add(new Cell(cells.Count, payload, container, form, op, position));
            }
        return cells;
    }

    private static string ContainerSource(Cell cell) => cell.Container switch
    {
        Container.Tuple => "(a, c)",
        Container.List => "[a, c]",
        Container.Set => "{a, c}",
        Container.FrozenSet => "frozenset([a, c])",
        Container.Dict => "{a: 0, c: 1}",
        Container.DictKeys => "{a: 0, c: 1}.keys()",
        Container.DictValues => "{0: a, 1: c}.values()",
        Container.ContainsDunder => "Bag(a, c)",
        Container.Str => "\"ab\"",
        Container.Bytes => "bytes([1, 2])",
        Container.Range => "range(1, 3)",
        _ => throw new InvalidOperationException(cell.Container.ToString()),
    };

    private sealed record Program(string Source, Dictionary<int, int> CellByLine);

    /// <summary>One program per payload over the cells still active; every line of a cell's function and of its
    /// driver loop is attributed to it, so a refusal anywhere in either removes that cell alone.</summary>
    private static Program ProgramFor(Payload payload, IReadOnlyList<Cell> cells)
    {
        var lines = new List<string>();
        var cellByLine = new Dictionary<int, int>();
        void Add(string line, int? cellId = null)
        {
            lines.Add(line);
            if (cellId is { } id)
                cellByLine[lines.Count] = id;
        }

        var p = payload.Name;
        Add("class K:");
        Add("    v: int");
        Add("    def __init__(self, v: int):");
        Add("        self.v = v");
        Add("");
        Add("class Bag:");
        Add($"    items: list[{p}]");
        Add($"    def __init__(self, a: {p}, c: {p}):");
        Add("        self.items = [a, c]");
        Add($"    def __contains__(self, item: {p}) -> bool:");
        Add("        return item in self.items");
        Add("");

        foreach (var cell in cells)
        {
            var op = OperatorSpelling(cell.Operator);
            var needle = cell.Form == Form.NoneLiteral ? "None" : "x";
            var parameters = cell.Form switch
            {
                Form.Plain => $"x: {p}, a: {p}, c: {p}",
                Form.Optional => $"x: {p} | None, a: {p}, c: {p}",
                _ => $"a: {p}, c: {p}",
            };
            var test = $"{needle} {op} {ContainerSource(cell)}";
            Add($"def c{cell.Id}({parameters}) -> bool:", cell.Id);
            // A located first statement: code the emitter hoists above the test (a comprehension) carries no
            // #line of its own, and a Roslyn error there maps to the last located line — this cell's, not the
            // previous function's.
            Add($"    cell: int = {cell.Id}", cell.Id);
            switch (cell.Position)
            {
                case Position.Expression:
                    Add($"    return {test}", cell.Id);
                    break;
                case Position.IfCondition:
                    Add($"    if {test}:", cell.Id);
                    Add("        return True", cell.Id);
                    Add("    return False", cell.Id);
                    break;
                case Position.ComprehensionFilter:
                    Add($"    return len([0 for _ in [0] if {test}]) == 1", cell.Id);
                    break;
                case Position.ChainLink:
                    // The first link reads the needle twice; the literal None's spelling is `None is None` (Sharpy has
                    // no `None == None`), so the cell tests the chain arm's needle decision, not `==`.
                    Add(cell.Form == Form.NoneLiteral
                        ? $"    return None is None {op} {ContainerSource(cell)}"
                        : $"    return x == x {op} {ContainerSource(cell)}", cell.Id);
                    break;
            }
            Add("");
        }

        Add("def main() -> None:");
        Add($"    a: {p} = {payload.Member}");
        Add($"    c: {p} = {payload.Other}");
        Add($"    n: {p} = {payload.NonMember}");
        Add($"    plain: list[{p}] = [a, n]");
        Add($"    optional: list[{p} | None] = [a, n, None]");
        foreach (var cell in cells)
        {
            var id = cell.Id.ToString(CultureInfo.InvariantCulture);
            var (loop, call) = cell.Form switch
            {
                Form.Plain => ("    for v in plain:", $"c{id}(v, a, c)"),
                Form.Optional => ("    for v in optional:", $"c{id}(v, a, c)"),
                _ => ("    for v in [0]:", $"c{id}(a, c)"),
            };
            Add(loop, cell.Id);
            Add("        try:", cell.Id);
            Add($"            print({id}, {call})", cell.Id);
            Add("        except TypeError:", cell.Id);
            Add($"            print({id}, \"TypeError\")", cell.Id);
            Add("        except Exception:", cell.Id);
            Add($"            print({id}, \"other\")", cell.Id);
        }
        Add("    print(\"done\")");
        return new Program(string.Join("\n", lines) + "\n", cellByLine);
    }

    private static Dictionary<int, List<string>> ParseLines(string stdout)
    {
        var byId = new Dictionary<int, List<string>>();
        foreach (var raw in stdout.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            var space = line.IndexOf(' ');
            if (space > 0 && int.TryParse(line[..space], NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
            {
                if (!byId.TryGetValue(id, out var values))
                    byId[id] = values = new List<string>();
                values.Add(line[(space + 1)..]);
            }
        }
        return byId;
    }

    /// <summary>Compile rounds per payload: checker refusals, then codegen refusals, then Roslyn's (SPY0908).</summary>
    private const int MaxRounds = 40;

    private sealed record SharpyVerdict(IReadOnlyList<string>? Values, IReadOnlySet<string>? RefusedBy);

    /// <summary>Compiles the payload's program, removing each cell a refusal is attributed to, until it runs.</summary>
    private Dictionary<int, SharpyVerdict> RunSharpy(Payload payload, IReadOnlyList<Cell> cells, List<string> harnessFailures)
    {
        var verdicts = new Dictionary<int, SharpyVerdict>();
        var active = cells.ToList();
        for (var round = 0; round < MaxRounds; round++)
        {
            var program = ProgramFor(payload, active);
            var run = CompileAndExecute(program.Source, "optional_membership.spy", executionTimeoutMs: 120_000);
            if (RanToCompletion(run))
            {
                var byId = ParseLines(run.StandardOutput);
                foreach (var cell in active)
                    verdicts[cell.Id] = new SharpyVerdict(byId.TryGetValue(cell.Id, out var v) ? v : new List<string>(), null);
                if (!run.StandardOutput.Contains("done", StringComparison.Ordinal))
                    harnessFailures.Add($"{payload.Name}: the program did not reach its last line");
                return verdicts;
            }

            var refused = new Dictionary<int, HashSet<string>>();
            var unattributed = new List<string>();
            foreach (var d in run.RawDiagnostics.Where(d => d.IsError))
            {
                if (d.Line is { } line && program.CellByLine.TryGetValue(line, out var id))
                {
                    if (!refused.TryGetValue(id, out var codes))
                        refused[id] = codes = new HashSet<string>(StringComparer.Ordinal);
                    codes.Add(d.Code ?? "?");
                    if (d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError)
                        Output.WriteLine($"  {payload.Name} c{id} line {line}: {d.Message}");
                }
                else
                {
                    unattributed.Add($"line {d.Line}: [{d.Code}] {d.Message}");
                }
            }
            if (unattributed.Count > 0 || refused.Count == 0)
            {
                harnessFailures.Add($"{payload.Name}: round {round} failed with no cell to remove: "
                    + (unattributed.Count > 0 ? string.Join("; ", unattributed.Take(5)) : run.FailureSummary()));
                return verdicts;
            }
            foreach (var (id, codes) in refused)
                verdicts[id] = new SharpyVerdict(null, codes);
            active = active.Where(c => !refused.ContainsKey(c.Id)).ToList();
            Output.WriteLine($"{payload.Name}: round {round} removed {refused.Count} cell(s) ("
                + string.Join(",", refused.Values.SelectMany(c => c).Distinct().OrderBy(c => c, StringComparer.Ordinal)) + ")");
        }
        harnessFailures.Add($"{payload.Name}: still refusing after {MaxRounds} rounds");
        return verdicts;
    }

    /// <summary>What a cell may do: print CPython's values (<see cref="Values"/>: the cell is COMPARED against the
    /// oracle), or be refused by exactly one of <see cref="RefusedBy"/> (HAND-EXPECTED: the basis is in
    /// <see cref="Why"/>, never the oracle).</summary>
    private sealed record Expectation(bool Values, IReadOnlySet<string> RefusedBy, string Why)
    {
        public bool Compared => Values;

        public string Describe() => (Values ? "python's values" : "")
            + (RefusedBy.Count > 0 ? "refused by " + string.Join("|", RefusedBy.OrderBy(c => c, StringComparer.Ordinal)) : "")
            + " (" + Why + ")";
    }

    private static readonly IReadOnlySet<string> NoCodes = new HashSet<string>();
    private static readonly IReadOnlySet<string> Spy0222 = new HashSet<string> { DiagnosticCodes.Semantic.InvalidBinaryOperation };

    /// <summary>The spec's needle-slot rule (membership_operators.md › Container kinds): every generated
    /// equality-scan container's slot IS the payload; <c>range</c> and <c>bytes</c> hold an <c>int</c>, so an integer
    /// payload fits when the catalog converts it to <c>int32</c> implicitly and nothing else fits; <c>str</c> holds a
    /// <c>str</c> (substring test).</summary>
    private static bool SlotHolds(Cell cell)
    {
        var payload = cell.Payload;
        return cell.Container switch
        {
            Container.Range or Container.Bytes => payload.Kind == PayloadKind.Integer
                && (payload.Name == "int32" || PrimitiveCatalog.CanImplicitlyConvert(
                    PrimitiveCatalog.GetByName(payload.Name)!, PrimitiveCatalog.GetByName("int32")!)),
            Container.Str => payload.Kind == PayloadKind.Str,
            _ => true,
        };
    }

    /// <summary>
    /// The case → expected map. COMPARED: a needle the slot holds, plain or <c>T | None</c>, prints CPython's values —
    /// <c>False</c> for a None needle in an equality scan (owner ruling 1, #2635) and <c>TypeError</c> for a None
    /// needle in <c>str</c>/<c>bytes</c> (owner ruling 2: accepted, TypeError at run time); so does the literal
    /// HAND-EXPECTED, by owner ruling A (2026-11-12): the literal <c>None</c> into a non-optional slot (every generated
    /// container) is refused by SPY0222. HAND-EXPECTED, by
    /// the deviation <c>membership-needle-type-mismatch</c> in <c>docs/deviations.yaml</c> ("a value the element type
    /// cannot hold can never be a member, so the test is a type error rather than a constant False"): a typed needle
    /// the slot cannot hold (<see cref="SlotHolds"/>) is refused by SPY0222. HAND-EXPECTED, out of this contract: a
    /// chain link (#2648, below).
    /// </summary>
    private static Expectation Expect(Cell cell, IReadOnlyDictionary<string, Cell> byKey)
    {
        if (cell.Form == Form.NoneLiteral && cell.Position != Position.ChainLink)
        {
            // Owner ruling A, 2026-11-12 (superseding the day's earlier literal-None rulings): "switch to the table,
            // refuse literal None for non-optional containers, makes the most sense". Every generated container's
            // element type is non-optional (the literal into an optional element is the plan's named control), so the
            // literal is refused at compile time, like any needle that can never match. The chain link inherits it.
            return new Expectation(false, Spy0222, "literal None into a non-optional slot: owner ruling A (2026-11-12)");
        }

        if (cell.Position == Position.ChainLink)
        {
            // The checker's chain arm decides the needle as the binary arm does; codegen then refuses every `in` link
            // with SPY0519 (#2648, out of this contract), so a link whose expression twin runs expects SPY0519.
            var atExpression = Expect(byKey[cell.Key.Replace("/ChainLink", "/Expression", StringComparison.Ordinal)], byKey);
            var codes = atExpression.Values
                ? new HashSet<string> { DiagnosticCodes.CodeGen.UnsupportedOperator }
                : new HashSet<string>(atExpression.RefusedBy);
            return new Expectation(false, codes, "chain link: the expression twin's checker verdict, then SPY0519 (#2648)");
        }

        if (!SlotHolds(cell))
            return new Expectation(false, Spy0222, "by deviation membership-needle-type-mismatch, not by oracle: the slot cannot hold this type");

        return new Expectation(true, NoCodes, cell.Form == Form.Plain
            ? "plain needle"
            : "T | None needle: owner rulings 1-2 (None is no member of an equality scan; TypeError for str/bytes)");
    }

    private const string PythonPrelude =
        "from decimal import Decimal as decimal\n"
        + "int8 = int16 = int32 = int64 = uint8 = uint16 = uint32 = uint64 = int\nfloat32 = float64 = float\n";

    [SkippableFact]
    public void OptionalNeedle_EveryContainerOperatorPosition_MatchesCPython()
    {
        // The oracle minor is the repository's .python-version pin (3.15).
        var oracle = PythonBatchOracle.Over(PythonInterpreter.RequireOrSkip(minMinor: 15));

        AssertPayloadAxisIsTheCatalog();
        AssertContainerAxisIsTheSpecTable();

        var payloads = GeneratePayloads();
        var cells = GenerateCells(payloads);

        // Totality: the cell count is the product of the axis sizes, each from its own source (the catalog minus its
        // named exclusions plus the two composites; the spec table's containers; the enums). Never from the generator,
        // so a cell the generator drops (or invents) is red.
        var payloadCount = CanonicalPrimitives().Count - ExcludedPrimitives.Count + 2;
        var containerCount = SpecRowMap.Sum(m => m.Generated.Length);
        var formCount = Enum.GetValues<Form>().Length;
        var positionCount = Enum.GetValues<Position>().Length;
        var expectedCount = payloadCount * containerCount * Operators.Length * formCount * positionCount;
        Assert.Equal(2, Operators.Length);
        Assert.Equal(payloadCount, payloads.Count);
        Assert.True(expectedCount == cells.Count,
            $"the generator made {cells.Count} cells; the axes give {expectedCount} ({payloadCount} payloads x "
            + $"{containerCount} containers x {Operators.Length} operators x {formCount} forms x {positionCount} positions): "
            + "a cell was dropped or invented");

        // CPython: one program per payload, one batch.
        var programs = payloads.Select(p => ProgramFor(p, cells.Where(c => c.Payload == p).ToList())).ToList();
        var py = oracle.RunBatch(programs.Select((prog, i) => new PyRequest(i, PythonPrelude + prog.Source + "main()\n")).ToList());
        var pythonById = new Dictionary<int, List<string>>();
        for (var i = 0; i < payloads.Count; i++)
        {
            Assert.True(py.TryGetValue(i, out var result) && result.Ok,
                $"python3 did not run the {payloads[i].Name} program: " + (result?.Stderr ?? "no verdict"));
            foreach (var (id, values) in ParseLines(result!.Stdout))
                pythonById[id] = values;
        }
        Assert.Equal(cells.Count, pythonById.Count);

        // Sharpy.
        var harnessFailures = new List<string>();
        var sharpyById = new Dictionary<int, SharpyVerdict>();
        foreach (var payload in payloads)
            foreach (var (id, verdict) in RunSharpy(payload, cells.Where(c => c.Payload == payload).ToList(), harnessFailures))
                sharpyById[id] = verdict;
        Assert.True(harnessFailures.Count == 0, "harness: " + string.Join("\n", harnessFailures));
        Assert.Equal(cells.Count, sharpyById.Count);

        var byKey = cells.ToDictionary(c => c.Key, StringComparer.Ordinal);
        var expectations = cells.ToDictionary(c => c.Id, c => Expect(c, byKey));

        var divergences = new List<(Cell Cell, string Observed, string Detail)>();
        foreach (var cell in cells)
        {
            var python = pythonById[cell.Id];
            var sharpy = sharpyById[cell.Id];
            var expected = expectations[cell.Id];
            var meets = sharpy.RefusedBy is { } refusedBy
                ? refusedBy.Count == 1 && expected.RefusedBy.Contains(refusedBy.Single())
                : expected.Values && python.SequenceEqual(sharpy.Values!);
            if (meets)
                continue;
            var observed = sharpy.RefusedBy is { } by
                ? "refused " + string.Join(",", by.OrderBy(c => c, StringComparer.Ordinal))
                : "values " + string.Join(" ", sharpy.Values!);
            // The pinned verdict names both sides, so a changed oracle answer or expectation for a red cell is a change
            // to its pin, exactly as a changed Sharpy answer is.
            var expectedShort = expected.Values
                ? "python " + string.Join(" ", python)
                : "refused " + string.Join("|", expected.RefusedBy.OrderBy(c => c, StringComparer.Ordinal));
            var pinned = $"sharpy {observed} | expected {expectedShort}";
            divergences.Add((cell, pinned, $"python [{string.Join(" ", python)}] sharpy [{observed}]: expected {expected.Describe()}"));
        }

        var compared = expectations.Values.Count(e => e.Compared);
        var handExpected = cells.Count - compared;
        var reportDir = ReportDirectory();
        var divergencePath = Path.Combine(reportDir, "optional-membership-differential-divergences.txt");
        File.WriteAllLines(divergencePath, divergences.OrderBy(d => d.Cell.Key, StringComparer.Ordinal)
            .Select(d => $"{d.Cell.Key}  {d.Detail}"));

        var allowlist = LoadAllowlist();
        var actualByRow = divergences.GroupBy(d => d.Cell.RowKey, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToDictionary(d => d.Cell.Key, d => d.Observed, StringComparer.Ordinal),
                StringComparer.Ordinal);
        var problems = new List<string>();
        foreach (var row in actualByRow.Keys.Union(allowlist.Keys).OrderBy(r => r, StringComparer.Ordinal))
        {
            var actual = actualByRow.GetValueOrDefault(row) ?? new Dictionary<string, string>(StringComparer.Ordinal);
            if (!allowlist.TryGetValue(row, out var pinned))
            {
                problems.Add($"ROW {row}: not in the allowlist ({actual.Count} divergent cells)");
                problems.AddRange(actual.OrderBy(a => a.Key, StringComparer.Ordinal).Select(a => $"  added {a.Key} -> {a.Value}"));
                continue;
            }
            if (pinned.Count != pinned.Cells.Count || pinned.Pin != Digest(pinned.Cells))
                problems.Add($"ROW {row}: its header (cells={pinned.Count} pin={pinned.Pin}) does not match its "
                    + $"{pinned.Cells.Count} pin lines (pin={Digest(pinned.Cells)})");
            if (actual.Count == 0)
                problems.Add($"ROW {row}: no cell diverges any more — delete the row");
            foreach (var (key, verdict) in actual.OrderBy(a => a.Key, StringComparer.Ordinal))
            {
                if (!pinned.Cells.TryGetValue(key, out var was))
                    problems.Add($"  {row}: added {key} -> {verdict} (a cell outside the pin now diverges)");
                else if (was != verdict)
                    problems.Add($"  {row}: changed {key}: {was} -> {verdict}");
            }
            foreach (var key in pinned.Cells.Keys.Where(k => !actual.ContainsKey(k)).OrderBy(k => k, StringComparer.Ordinal))
                problems.Add($"  {row}: removed {key} (pinned {pinned.Cells[key]}; it now meets its expectation)");
        }

        Output.WriteLine($"{payloads.Count} payloads, {cells.Count} cases: {compared} compared with CPython, {handExpected} "
            + $"hand-expected (slot mismatches by deviation membership-needle-type-mismatch; literal None by owner ruling A; "
            + "chain links by #2648); "
            + $"{divergences.Count} divergent in {actualByRow.Count} rows; allowlist {allowlist.Count} rows; "
            + $"{problems.Count} pin problem line(s). Every divergence: {divergencePath}");

        // Positive controls: the oracle column reaches every answer the expected map distinguishes.
        Assert.Contains(cells, c => pythonById[c.Id].Contains("True"));
        Assert.Contains(cells, c => pythonById[c.Id].Contains("False"));
        Assert.Contains(cells, c => expectations[c.Id].Compared && c.Form == Form.Optional && pythonById[c.Id].Contains("TypeError"));
        Assert.Contains(cells, c => c.Form == Form.Plain && c.Position != Position.ChainLink && sharpyById[c.Id].Values != null);
        // The optional column can pass: a reference payload's T | None needle already runs and prints False for None.
        Assert.Contains(cells, c => c.Form == Form.Optional && sharpyById[c.Id].Values is { } v
            && v.Contains("False") && v.SequenceEqual(pythonById[c.Id]) && divergences.All(d => d.Cell != c));

        // Written on every run, header included, so copying it over the allowlist is a byte-identical round trip
        // when nothing moved and a reviewable diff when something did.
        var proposedPath = Path.Combine(reportDir, "optional-membership-differential-proposed-allowlist.txt");
        File.WriteAllText(proposedPath, string.Join("\n", ProposedAllowlist(actualByRow, allowlist)) + "\n");

        if (problems.Count > 0)
        {
            var problemPath = Path.Combine(reportDir, "optional-membership-differential-pin-problems.txt");
            File.WriteAllLines(problemPath, problems);
            Assert.Fail($"{problems.Count} pin problem line(s) against {AllowlistFileName}: every line is in {problemPath}; "
                + $"a regenerated allowlist (rows' citations kept) is in {proposedPath}. First lines:\n"
                + string.Join("\n", problems.Take(30)));
        }
    }

    /// <summary>The first 16 hex digits of SHA-256 over the row's sorted <c>cell -&gt; verdict</c> lines.</summary>
    private static string Digest(IReadOnlyDictionary<string, string> cells)
    {
        var text = string.Concat(cells.OrderBy(c => c.Key, StringComparer.Ordinal).Select(c => $"{c.Key} -> {c.Value}\n"));
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(hash, 0, 8).ToLowerInvariant();
    }

    /// <summary>The allowlist's header, emitted verbatim by <see cref="ProposedAllowlist"/>.</summary>
    private static readonly string[] AllowlistHeader =
    {
        "# Optional-needle membership differential allowlist (OptionalMembershipDifferentialTests).",
        "#",
        "# A row is '<container>/<needle>  cells=N  pin=HEX  # citation', needle = Plain | NoneNeedle (a T | None needle and",
        "# the literal None share a row). The '#= <cell> -> <observed Sharpy verdict>' lines under a row pin its EXACT",
        "# divergent cells: a pinned cell that starts passing, a cell of the row that starts diverging, or a pinned verdict",
        "# that changes fails the test, and the row's cells= and pin= (a SHA-256 prefix over its sorted pin lines) must match",
        "# them. On a failure the test writes every problem and a regenerated allowlist under .claude/tmp/; every",
        "# divergence (cell, CPython's answer, Sharpy's, the expectation) is written there on every run. Delete a row when",
        "# its last cell passes. Pin lines are comments, so scorecard_metrics.py counts one row per container/needle.",
    };

    private static IEnumerable<string> ProposedAllowlist(
        IReadOnlyDictionary<string, Dictionary<string, string>> actualByRow, IReadOnlyDictionary<string, PinnedRow> allowlist)
    {
        foreach (var line in AllowlistHeader)
            yield return line;
        foreach (var (row, cellsInRow) in actualByRow.OrderBy(r => r.Key, StringComparer.Ordinal))
        {
            var cite = allowlist.TryGetValue(row, out var old) ? old.Citation : "# see #<open issue>";
            yield return "";
            yield return $"{row}  cells={cellsInRow.Count}  pin={Digest(cellsInRow)}  {cite}";
            foreach (var (key, verdict) in cellsInRow.OrderBy(c => c.Key, StringComparer.Ordinal))
                yield return $"#= {key} -> {verdict}";
        }
    }

    private static string ReportDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "sharpy.sln")))
            dir = dir.Parent;
        var tmp = Path.Combine(dir?.FullName ?? Path.GetTempPath(), ".claude", "tmp");
        Directory.CreateDirectory(tmp);
        return tmp;
    }

    /// <summary>Every canonical primitive is a payload or an exclusion with its reason — never neither, never both.</summary>
    private static void AssertPayloadAxisIsTheCatalog()
    {
        var canonical = CanonicalPrimitives();
        var generated = GeneratePayloads().Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        var unaccounted = canonical.Where(n => !generated.Contains(n) && !ExcludedPrimitives.ContainsKey(n)).ToList();
        Assert.True(unaccounted.Count == 0,
            "primitives neither generated nor excluded (give them literals or an exclusion reason): " + string.Join(", ", unaccounted));
        var phantom = ExcludedPrimitives.Keys.Where(n => !canonical.Contains(n)).ToList();
        Assert.True(phantom.Count == 0, "excluded names that are not canonical primitives: " + string.Join(", ", phantom));
        Assert.DoesNotContain(generated, ExcludedPrimitives.ContainsKey);
    }

    /// <summary>The container axis is the spec's "Container kinds" table: every row is mapped, every mapped row
    /// exists, and every <see cref="Container"/> comes from exactly one row.</summary>
    private static void AssertContainerAxisIsTheSpecTable()
    {
        var spec = PythonInterpreter.FindRepoFile("docs", "language_specification", "membership_operators.md")
            ?? throw new FileNotFoundException("docs/language_specification/membership_operators.md");
        var rows = new List<string>();
        var inTable = false;
        foreach (var line in File.ReadAllLines(spec))
        {
            if (line.StartsWith("### Container kinds", StringComparison.Ordinal))
            {
                inTable = true;
                continue;
            }
            if (!inTable)
                continue;
            if (line.StartsWith("#", StringComparison.Ordinal))
                break;
            if (!line.StartsWith("|", StringComparison.Ordinal) || line.StartsWith("|---", StringComparison.Ordinal))
                continue;
            var first = line.Split('|')[1].Trim();
            if (first != "container")
                rows.Add(first);
        }
        Assert.True(rows.Count >= 5, $"the spec's Container kinds table was not found ({rows.Count} rows)");
        var mapped = SpecRowMap.Select(m => m.Row).ToList();
        Assert.Equal(rows.OrderBy(r => r, StringComparer.Ordinal), mapped.OrderBy(r => r, StringComparer.Ordinal));
        var fromRows = SpecRowMap.SelectMany(m => m.Generated).ToList();
        Assert.Equal(fromRows.Count, fromRows.Distinct().Count());
        Assert.Equal(Enum.GetValues<Container>().OrderBy(c => c), fromRows.OrderBy(c => c));
    }

    /// <summary>A row: its key, the count and digest its header pins, its citation comment, and the pinned cells
    /// (<c>#= cell -&gt; verdict</c> comment lines under it, which <c>scorecard_metrics.py</c> does not count).</summary>
    private sealed record PinnedRow(int Count, string Pin, string Citation, Dictionary<string, string> Cells);

    private static Dictionary<string, PinnedRow> LoadAllowlist()
    {
        var rows = new Dictionary<string, PinnedRow>(StringComparer.Ordinal);
        var path = PythonInterpreter.FindRepoFile("src", "Sharpy.Compiler.Tests", "Conformance", AllowlistFileName);
        if (path == null || !File.Exists(path))
            return rows;
        PinnedRow? current = null;
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (line.StartsWith("#=", StringComparison.Ordinal))
            {
                var arrow = line.IndexOf(" -> ", StringComparison.Ordinal);
                Assert.True(current != null && arrow > 2, $"{AllowlistFileName}: a pin line outside a row or without ' -> ': {raw}");
                current!.Cells[line[2..arrow].Trim()] = line[(arrow + 4)..].Trim();
                continue;
            }
            var hash = line.IndexOf('#', StringComparison.Ordinal);
            var citation = hash >= 0 ? line[hash..] : "";
            if (hash >= 0)
                line = line[..hash].Trim();
            if (line.Length == 0)
                continue;
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var count = parts.FirstOrDefault(t => t.StartsWith("cells=", StringComparison.Ordinal));
            var pin = parts.FirstOrDefault(t => t.StartsWith("pin=", StringComparison.Ordinal));
            Assert.True(count != null && pin != null
                && System.Text.RegularExpressions.Regex.IsMatch(citation, @"#[0-9]+"),
                $"{AllowlistFileName}: a row needs 'cells=N pin=HEX # see #NNN' (an issue number): {raw}");
            current = new PinnedRow(int.Parse(count!["cells=".Length..], CultureInfo.InvariantCulture), pin!["pin=".Length..],
                citation, new Dictionary<string, string>(StringComparer.Ordinal));
            rows.Add(parts[0], current);
        }
        return rows;
    }
}
