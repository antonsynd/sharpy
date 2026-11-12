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
/// <c>True</c> for <c>not in</c> — except where CPython RAISES for a <c>None</c> needle (a <c>str</c> or a
/// <c>bytes</c> container: <c>TypeError</c>), where Sharpy refuses the needle (SPY0222) or raises
/// <c>TypeError</c> too, and never prints a value or throws a CLR exception.
/// <para><b>Generated, never listed.</b> The axes:</para>
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
/// <para><b>Expected map.</b> CPython 3.15 (the <c>.python-version</c> pin) answers each value; one program per
/// payload is compiled under Sharpy, cells refused at compile time are attributed by line, removed and the rest
/// recompiled until it runs (<see cref="Expect"/>). A plain-<c>T</c> or <c>T | None</c> cell runs and prints
/// CPython's values, except where the spec's <c>membership-needle-type-mismatch</c> deviation refuses the width
/// (SPY0222: an integer the <c>int</c> slot of <c>range</c>/<c>bytes</c> cannot hold, decided by
/// <see cref="PrimitiveCatalog.CanImplicitlyConvert"/>); where CPython raises for <c>None</c>, a SPY0222 refusal
/// is also correct. The literal <c>None</c> is refused by SPY0222 (the same deviation: <c>None</c> is never a
/// member of a <c>T</c> container). A chain-link cell takes its expression twin's checker verdict, and SPY0519
/// where that twin runs (codegen refuses every <c>in</c> link — out of this contract; the checker's chain arm
/// must still decide the needle as the binary arm does).</para>
/// <para><b>Known red.</b> <c>optional-membership-differential-allowlist.txt</c> (Conformance), one row per
/// <c>container/form</c> (the axis the cure varies by: each container's lowering); a row whose cells all
/// pass fails the test until it is deleted.</para>
/// <para>Skips when no python3 ≥ 3.15 answers, except under <c>SHARPY_REQUIRE_PYTHON_ORACLE=1</c> (CI).</para>
/// </summary>
public class OptionalMembershipDifferentialTests : IntegrationTestBase
{
    public OptionalMembershipDifferentialTests(ITestOutputHelper output) : base(output) => MuteCompilePath = true;

    private const string AllowlistFileName = "optional-membership-differential-allowlist.txt";

    /// <summary>The primitives the payload axis leaves out, each with why its python twin cannot be spelled.</summary>
    private static readonly Dictionary<string, string> ExcludedPrimitives = new(StringComparer.Ordinal)
    {
        ["decimal"] = "python's Decimal is a stdlib class with its own literal spelling, not a builtin",
        ["char"] = "python has no character type",
        ["object"] = "an `object` element slot admits every needle: no needle-type decision to make",
        ["void"] = "None itself, the needle form axis",
    };

    private enum PayloadKind { Integer, Float, Bool, Str, Tuple, Class }

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
        public string RowKey => $"{Container}/{Form}";
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

    private static bool Applies(Payload payload, Container container) => container switch
    {
        Container.Str => payload.Kind == PayloadKind.Str,
        Container.Bytes or Container.Range => payload.Kind == PayloadKind.Integer,
        _ => true,
    };

    private static List<Cell> GenerateCells(IReadOnlyList<Payload> payloads)
    {
        var cells = new List<Cell>();
        foreach (var payload in payloads)
            foreach (var container in Enum.GetValues<Container>())
            {
                if (!Applies(payload, container))
                    continue;
                foreach (var form in Enum.GetValues<Form>())
                    foreach (var op in Operators)
                        foreach (var position in Enum.GetValues<Position>())
                        {
                            // The chain link reads the needle twice (`x == x in c`); a literal None has no `==`.
                            if (form == Form.NoneLiteral && position == Position.ChainLink)
                                continue;
                            cells.Add(new Cell(cells.Count, payload, container, form, op, position));
                        }
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
        Container.Str => "a + c",
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
                    Add($"    return x == x {op} {ContainerSource(cell)}", cell.Id);
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

    /// <summary>What a cell may do: print CPython's values, and/or be refused by exactly one of these codes.</summary>
    private sealed record Expectation(bool Values, IReadOnlySet<string> RefusedBy, string Why)
    {
        public string Describe() => (Values ? "python's values" : "")
            + (Values && RefusedBy.Count > 0 ? " or " : "")
            + (RefusedBy.Count > 0 ? "refused by " + string.Join("|", RefusedBy.OrderBy(c => c, StringComparer.Ordinal)) : "")
            + " (" + Why + ")";
    }

    private static readonly IReadOnlySet<string> NoCodes = new HashSet<string>();
    private static readonly IReadOnlySet<string> Spy0222 = new HashSet<string> { DiagnosticCodes.Semantic.InvalidBinaryOperation };

    /// <summary>
    /// The spec's <c>membership-needle-type-mismatch</c> deviation, as the catalog decides it: a container whose
    /// element slot is <c>int</c> (<c>range</c>, <c>bytes</c>) refuses an integer needle that does not implicitly
    /// convert to <c>int32</c> (SPY0222 with a cast steer). Every other generated container's slot IS the payload.
    /// </summary>
    private static bool NeedleCannotConvertToInt(Cell cell) =>
        cell.Container is Container.Range or Container.Bytes
        && !PrimitiveCatalog.CanImplicitlyConvert(PrimitiveCatalog.GetByName(cell.Payload.Name)!, PrimitiveCatalog.GetByName("int32")!)
        && cell.Payload.Name != "int32";

    /// <summary>
    /// The case → expected map: CPython's values, except where the spec refuses (SPY0222): the literal <c>None</c>,
    /// and a needle width the <c>int</c> slot cannot hold (<see cref="NeedleCannotConvertToInt"/>). Where CPython
    /// raises for <c>None</c>, a SPY0222 refusal is also correct (the direction is the same: no value).
    /// </summary>
    private static Expectation Expect(Cell cell, IReadOnlyDictionary<string, Cell> byKey,
        IReadOnlyDictionary<int, List<string>> python)
    {
        if (cell.Form == Form.NoneLiteral)
            return new Expectation(false, Spy0222, "membership-needle-type-mismatch: None is never a member of a T container");

        if (cell.Position == Position.ChainLink)
        {
            // The checker's chain arm decides the needle as the binary arm does; codegen then refuses every `in`
            // link (SPY0519, out of this contract).
            var atExpression = Expect(byKey[cell.Key.Replace("/ChainLink", "/Expression", StringComparison.Ordinal)],
                byKey, python);
            var codes = new HashSet<string>(atExpression.RefusedBy);
            if (atExpression.Values)
                codes.Add(DiagnosticCodes.CodeGen.UnsupportedOperator);
            return new Expectation(false, codes, "the expression twin's verdict at the checker; SPY0519 at codegen");
        }

        if (NeedleCannotConvertToInt(cell))
            return new Expectation(false, Spy0222, "membership-needle-type-mismatch: the int slot cannot hold this width");

        if (cell.Form == Form.Optional && python[cell.Id].Contains("TypeError"))
            return new Expectation(true, Spy0222, "python raises TypeError for None");

        return new Expectation(true, NoCodes, cell.Form == Form.Plain ? "plain needle" : "None is an equality scan's non-member");
    }

    private const string PythonPrelude =
        "int8 = int16 = int32 = int64 = uint8 = uint16 = uint32 = uint64 = int\nfloat32 = float64 = float\n";

    [SkippableFact]
    public void OptionalNeedle_EveryContainerOperatorPosition_MatchesCPython()
    {
        // The oracle minor is the repository's .python-version pin (3.15).
        var oracle = PythonBatchOracle.Over(PythonInterpreter.RequireOrSkip(minMinor: 15));

        AssertPayloadAxisIsTheCatalog();
        AssertContainerAxisIsTheSpecTable();

        var payloads = GeneratePayloads();
        var cells = GenerateCells(payloads);

        // Totality: the cell count is the product of the axes, computed apart from the generator.
        var expectedCount = payloads.Sum(p => Enum.GetValues<Container>().Count(c => Applies(p, c))
            * Operators.Length * (2 * Enum.GetValues<Position>().Length + (Enum.GetValues<Position>().Length - 1)));
        Assert.Equal(expectedCount, cells.Count);
        Assert.Equal(2, Operators.Length);

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
        var expectations = cells.ToDictionary(c => c.Id, c => Expect(c, byKey, pythonById));

        var divergences = new List<(Cell Cell, string Detail)>();
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
            var shown = sharpy.RefusedBy is { } by ? "refused " + string.Join(",", by.OrderBy(c => c, StringComparer.Ordinal))
                : string.Join(" ", sharpy.Values!);
            divergences.Add((cell, $"python [{string.Join(" ", python)}] sharpy [{shown}]: expected {expected.Describe()}"));
        }

        var allowlist = LoadAllowlist();
        var divergentRows = divergences.Select(d => d.Cell.RowKey).ToHashSet(StringComparer.Ordinal);
        var offenders = divergences.Where(d => !allowlist.Contains(d.Cell.RowKey)).ToList();
        var stale = allowlist.Where(r => !divergentRows.Contains(r)).OrderBy(r => r, StringComparer.Ordinal).ToList();

        Output.WriteLine($"{payloads.Count} payloads, {cells.Count} cells; {divergences.Count} divergent in "
            + $"{divergentRows.Count} rows; allowlist {allowlist.Count}, non-allowlisted {offenders.Count}, stale {stale.Count}");
        Output.WriteLine($"cells expected refused by SPY0222: {expectations.Values.Count(e => !e.Values && e.RefusedBy.SetEquals(Spy0222))}; "
            + $"by CPython's TypeError or SPY0222: {expectations.Values.Count(e => e.Values && e.RefusedBy.Count > 0)}");
        foreach (var row in divergentRows.OrderBy(r => r, StringComparer.Ordinal))
            Output.WriteLine($"  ROW {row}  ({divergences.Count(d => d.Cell.RowKey == row)} cells)");
        foreach (var d in divergences.Take(60))
            Output.WriteLine($"  DIVERGENCE {d.Cell.Key}  {d.Detail}");

        // Positive controls: the oracle column reaches every answer the expected map distinguishes.
        Assert.Contains(cells, c => pythonById[c.Id].Contains("True"));
        Assert.Contains(cells, c => pythonById[c.Id].Contains("False"));
        Assert.Contains(cells, c => c.Form == Form.Optional && pythonById[c.Id].Contains("TypeError"));
        Assert.Contains(cells, c => c.Form == Form.Plain && c.Position != Position.ChainLink && sharpyById[c.Id].Values != null);
        // The optional column can pass: a reference payload's T | None needle already runs and prints False for None.
        Assert.Contains(cells, c => c.Form == Form.Optional && sharpyById[c.Id].Values is { } v
            && v.Contains("False") && v.SequenceEqual(pythonById[c.Id]) && divergences.All(d => d.Cell != c));

        Assert.True(offenders.Count == 0,
            $"{offenders.Count} non-allowlisted optional-membership divergence(s) from CPython "
            + $"(fix the compiler, or add a '{{container}}/{{form}}' row to {AllowlistFileName} citing an open issue):\n"
            + string.Join("\n", offenders.Take(40).Select(o => $"  {o.Cell.Key}  {o.Detail}")));
        Assert.True(stale.Count == 0,
            $"{stale.Count} {AllowlistFileName} row(s) no longer diverge — delete them:\n"
            + string.Join("\n", stale.Select(r => "  " + r)));
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

    private static HashSet<string> LoadAllowlist()
    {
        var rows = new HashSet<string>(StringComparer.Ordinal);
        var path = PythonInterpreter.FindRepoFile("src", "Sharpy.Compiler.Tests", "Conformance", AllowlistFileName);
        if (path == null || !File.Exists(path))
            return rows;
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw;
            var hash = line.IndexOf('#', StringComparison.Ordinal);
            if (hash >= 0)
                line = line[..hash];
            line = line.Trim();
            if (line.Length > 0)
                rows.Add(line);
        }
        return rows;
    }
}
