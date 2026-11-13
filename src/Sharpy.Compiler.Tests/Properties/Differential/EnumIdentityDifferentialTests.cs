using System.Globalization;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.TestInfrastructure.Integration;
using Sharpy.TestInfrastructure.Oracles;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Differential;

/// <summary>
/// The class guard for identity and equality on enums (#2669, #2670). Identity (owner ruling 2026-11-12): <c>is</c> / <c>is not</c> where an
/// operand's static type is an enum answers what CPython 3.15 answers. Enum members are singletons in Python, so
/// identity and equality coincide; an int-backed enum is a C# value type, and the old lowering
/// (<c>object.ReferenceEquals</c>) boxed both operands and always answered False.
/// <para><b>Generated, never listed.</b> The full product of four axes; the count is asserted from the axis sizes:</para>
/// <list type="bullet">
/// <item><b>left operand</b> × <b>right operand</b> — every <see cref="Operand"/>: an enum variable, a variable
/// holding another member of the same enum, an enum member written literally (the spelling the parser reads as a
/// type name, SPY0349 before the fix), <c>E | None</c> holding a member and holding None, a variable and a member
/// of ANOTHER int-backed enum, an <c>object</c> holding a member, the string-backed variants (variable, member,
/// another string enum with an equal value), two effectful calls that print when they are evaluated (so the
/// answer and the number of evaluations of each operand are both compared), and the CLR enums — a
/// <c>System.DayOfWeek</c> variable, member, <c>DateTime.day_of_week</c> property (typed by the CLR bridge, not
/// by the imported name) and <c>DayOfWeek | None</c>, and a variable of another CLR enum
/// (<c>DateTimeKind</c>); their CPython twins are plain <c>Enum</c> classes of the same members;</item>
/// <item><b>operator</b> — <see cref="Operators"/>;</item>
/// <item><b>position</b> — <see cref="Position"/>: the binary operator as a printed value, an <c>if</c>
/// condition, a comparison-chain link, a comprehension filter.</item>
/// </list>
/// <para><b>Equality</b> (#2670, owner ruling 2026-11-12) rides the same matrix: <c>==</c> / <c>!=</c> between two
/// DIFFERENT enum types answers False / True as CPython does, both operands evaluated once — it was CS0019 behind
/// SPY0908 for two int-backed enums, an int-backed against a string-backed one and <c>E | None</c> against another
/// enum, and a silent True for two string-backed enums with equal values. The same-enum pairs are the control.</para>
/// <para><b>Expected map</b> (<see cref="Expect"/>). COMPARED cells print CPython's lines. HAND-EXPECTED cells,
/// counted apart: an <c>is</c> link of a comparison chain is not lowered at all (SPY0519, the class #2648 tracks; a
/// chain headed by <c>x is Member</c> keeps SPY0349); <c>is</c> between two operands that are BOTH typed
/// <c>object</c> stays boxed reference identity — no operand's static type is an enum, so the contract does not
/// reach it and the answer is pinned so that a change is seen; and <c>==</c> / <c>!=</c> with an <c>object</c>
/// operand is the checker's general rule, not an enum-pair decision (SPY0222 against an enum, SPY0402 for two
/// objects, and a pinned reference comparison where a chain link skips that refusal).</para>
/// <para><b>Emitted shape</b> (<see cref="ShapeViolation"/>): every cell that runs and compares two operands of ONE
/// int-backed enum must emit the enum's native operator, and every pair of two different enums
/// <c>object.Equals</c> (the probe's positive control). The answers alone cannot see a same-enum pair that was
/// boxed: <c>object.Equals</c> answers it correctly.</para>
/// <para>No cell may draw SPY0465 ("always False due to boxing"): an enum's <c>is</c> is value equality. The
/// absence is read from the same diagnostics a positive control (<c>is</c> on two ints) is shown to hit.</para>
/// <para>Skips when no python3 ≥ 3.15 answers, except under <c>SHARPY_REQUIRE_PYTHON_ORACLE=1</c> (CI).</para>
/// </summary>
public class EnumIdentityDifferentialTests : IntegrationTestBase
{
    public EnumIdentityDifferentialTests(ITestOutputHelper output) : base(output) => MuteCompilePath = true;

    private enum Operand
    {
        EnumVar, EnumOtherMemberVar, EnumMember, OptionalHoldingMember, OptionalHoldingNone,
        OtherEnumVar, OtherEnumMember, ObjectHoldingMember,
        StrEnumVar, StrEnumMember, OtherStrEnumVar,
        EnumCall, OtherEnumCall,
        ClrEnumVar, ClrEnumMember, ClrEnumProperty, OptionalClrEnum, OtherClrEnumVar,
    }

    /// <summary>
    /// Which enum declaration an operand's STATIC type names (through <c>E | None</c>), or null for an
    /// <c>object</c>. Two operands of one family are a same-enum pair: the emitted-shape assertion requires
    /// their comparison to be the enum's native operator. Stated per operand, never derived from the answers.
    /// </summary>
    private static string? Family(Operand operand) => operand switch
    {
        Operand.EnumVar or Operand.EnumOtherMemberVar or Operand.EnumMember or Operand.OptionalHoldingMember
            or Operand.OptionalHoldingNone or Operand.EnumCall => "Color",
        Operand.OtherEnumVar or Operand.OtherEnumMember or Operand.OtherEnumCall => "Size",
        Operand.StrEnumVar or Operand.StrEnumMember => "Tone",
        Operand.OtherStrEnumVar => "Mood",
        Operand.ClrEnumVar or Operand.ClrEnumMember or Operand.ClrEnumProperty or Operand.OptionalClrEnum => "DayOfWeek",
        Operand.OtherClrEnumVar => "DateTimeKind",
        Operand.ObjectHoldingMember => null,
        _ => throw new ArgumentOutOfRangeException(nameof(operand)),
    };

    /// <summary>The string-backed families: reference singletons, whose <c>is</c> is reference identity by design.</summary>
    private static bool IsStringBacked(string family) => family is "Tone" or "Mood";

    /// <summary>Where the test is written: a returned expression, an <c>if</c> condition, a chain link, a filter.</summary>
    private enum Position { Print, IfCondition, ChainLink, ComprehensionFilter }

    /// <summary>The operator axis: the identity tests (#2669) and the equality tests (#2670).</summary>
    private static readonly BinaryOperator[] Operators =
    {
        BinaryOperator.Is, BinaryOperator.IsNot, BinaryOperator.Equal, BinaryOperator.NotEqual,
    };

    private static bool IsIdentity(BinaryOperator op) => op is BinaryOperator.Is or BinaryOperator.IsNot;

    private static string Spell(BinaryOperator op) => op switch
    {
        BinaryOperator.Is => "is",
        BinaryOperator.IsNot => "is not",
        BinaryOperator.Equal => "==",
        BinaryOperator.NotEqual => "!=",
        _ => throw new ArgumentOutOfRangeException(nameof(op)),
    };

    /// <summary>The declaration of an operand's variable (annotated, so it is one line of both languages), or null.</summary>
    private static string? Declaration(Operand operand, string side) => operand switch
    {
        Operand.EnumVar => $"{side}_enum: Color = Color.RED",
        Operand.EnumOtherMemberVar => $"{side}_green: Color = Color.GREEN",
        Operand.OptionalHoldingMember => $"{side}_opt: Color | None = Color.RED",
        Operand.OptionalHoldingNone => $"{side}_none: Color | None = None",
        Operand.OtherEnumVar => $"{side}_size: Size = Size.S",
        Operand.ObjectHoldingMember => $"{side}_obj: object = Color.RED",
        Operand.StrEnumVar => $"{side}_tone: Tone = Tone.A",
        Operand.OtherStrEnumVar => $"{side}_mood: Mood = Mood.A",
        Operand.ClrEnumVar => $"{side}_day: DayOfWeek = DayOfWeek.Monday",
        // 2024-01-01 is a Monday: the property is typed by the CLR bridge, not by an imported name.
        Operand.ClrEnumProperty => $"{side}_dt: DateTime = DateTime(2024, 1, 1)",
        Operand.OptionalClrEnum => $"{side}_oday: DayOfWeek | None = DayOfWeek.Monday",
        Operand.OtherClrEnumVar => $"{side}_kind: DateTimeKind = DateTimeKind.Utc",
        _ => null,
    };

    /// <summary>How an operand is written in a cell: its variable, the member, or a call that prints the cell id.</summary>
    private static string Spelling(Operand operand, string side, int cell) => operand switch
    {
        Operand.EnumVar => $"{side}_enum",
        Operand.EnumOtherMemberVar => $"{side}_green",
        Operand.EnumMember => "Color.RED",
        Operand.OptionalHoldingMember => $"{side}_opt",
        Operand.OptionalHoldingNone => $"{side}_none",
        Operand.OtherEnumVar => $"{side}_size",
        Operand.OtherEnumMember => "Size.S",
        Operand.ObjectHoldingMember => $"{side}_obj",
        Operand.StrEnumVar => $"{side}_tone",
        Operand.StrEnumMember => "Tone.A",
        Operand.OtherStrEnumVar => $"{side}_mood",
        Operand.EnumCall => $"ev_color({cell.ToString(CultureInfo.InvariantCulture)})",
        Operand.OtherEnumCall => $"ev_size({cell.ToString(CultureInfo.InvariantCulture)})",
        Operand.ClrEnumVar => $"{side}_day",
        Operand.ClrEnumMember => "DayOfWeek.Monday",
        Operand.ClrEnumProperty => $"{side}_dt.day_of_week",
        Operand.OptionalClrEnum => $"{side}_oday",
        Operand.OtherClrEnumVar => $"{side}_kind",
        _ => throw new ArgumentOutOfRangeException(nameof(operand)),
    };

    /// <summary>An operand the parser reads as a type name after <c>is</c>: an uppercase dotted member.</summary>
    private static bool IsMemberLiteral(Operand operand)
        => operand is Operand.EnumMember or Operand.OtherEnumMember or Operand.StrEnumMember or Operand.ClrEnumMember;

    private sealed record Cell(int Id, Operand Left, Operand Right, BinaryOperator Op, Position Position)
    {
        public string Key => $"{Left} {Spell(Op)} {Right} @{Position}";

        public string Expression
        {
            get
            {
                var left = Spelling(Left, "l", Id);
                var right = Spelling(Right, "r", Id);
                var link = $"{left} {Spell(Op)} {right}";
                // A chain's last operand is the right operand again: `l op r op r`.
                return Position == Position.ChainLink ? $"{link} {Spell(Op)} {right}" : link;
            }
        }
    }

    private static List<Cell> GenerateCells()
    {
        var cells = new List<Cell>();
        foreach (var op in Operators)
            foreach (var position in Enum.GetValues<Position>())
                foreach (var left in Enum.GetValues<Operand>())
                    foreach (var right in Enum.GetValues<Operand>())
                        cells.Add(new Cell(cells.Count + 1, left, right, op, position));
        return cells;
    }

    private const string SharpyHeader = """
        from system import DayOfWeek, DateTime, DateTimeKind

        enum Color:
            RED = 1
            GREEN = 2

        enum Size:
            S = 1
            M = 2

        enum Tone:
            A = "a"
            B = "b"

        enum Mood:
            A = "a"


        """;

    private const string PythonHeader = """
        from enum import Enum

        # The CLR enums' twins: System.DayOfWeek, System.DateTimeKind, and a DateTime whose day_of_week is the
        # member the CLR property returns for 2024-01-01.
        class DayOfWeek(Enum):
            Sunday = 0
            Monday = 1

        class DateTimeKind(Enum):
            Unspecified = 0
            Utc = 1

        class DateTime:
            def __init__(self, year, month, day):
                self.day_of_week = DayOfWeek.Monday

        class Color(Enum):
            RED = 1
            GREEN = 2

        class Size(Enum):
            S = 1
            M = 2

        class Tone(Enum):
            A = "a"
            B = "b"

        class Mood(Enum):
            A = "a"


        """;

    private sealed record Program(string Body, Dictionary<int, int> CellByLine);

    /// <summary>
    /// The program body shared by both languages (the enum declarations differ, <see cref="SharpyHeader"/> /
    /// <see cref="PythonHeader"/>): one function per cell, declaring the cell's own operands and returning the
    /// test's value from its position, and a <c>main</c> that prints each cell's id and value. An effectful operand
    /// prints <c>id eval-…</c> when it is evaluated, so those lines precede the value and are compared with it.
    /// <c>CellByLine</c> is keyed by the 1-based line of the SHARPY program (header included); every line of a cell's
    /// function is the cell's, so a refusal raised anywhere in it — a statement hoisted out of a comprehension
    /// included — is attributed to that cell.
    /// </summary>
    private static Program ProgramFor(IReadOnlyList<Cell> cells)
    {
        var lines = new List<string>
        {
            "def ev_color(cell: int) -> Color:",
            "    print(cell, \"eval-color\")",
            "    return Color.RED",
            "",
            "def ev_size(cell: int) -> Size:",
            "    print(cell, \"eval-size\")",
            "    return Size.S",
            "",
        };

        var headerLines = SharpyHeader.Split('\n').Length - 1;
        var cellByLine = new Dictionary<int, int>();
        void Add(Cell cell, string line)
        {
            lines.Add(line);
            cellByLine[headerLines + lines.Count] = cell.Id;
        }

        foreach (var cell in cells)
        {
            var id = cell.Id.ToString(CultureInfo.InvariantCulture);
            Add(cell, $"def cell_{id}() -> bool:");
            // A statement of the cell's own before anything else: a C# error in a statement hoisted out of a
            // comprehension is reported at the line of the statement BEFORE it, which without this would be the
            // previous cell's (or no cell's) when the operands need no declaration.
            Add(cell, $"    marker: int = {id}");
            if (Declaration(cell.Left, "l") is { } leftDeclaration)
                Add(cell, "    " + leftDeclaration);
            if (Declaration(cell.Right, "r") is { } rightDeclaration)
                Add(cell, "    " + rightDeclaration);
            switch (cell.Position)
            {
                case Position.Print:
                case Position.ChainLink:
                    Add(cell, $"    return {cell.Expression}");
                    break;
                case Position.IfCondition:
                    Add(cell, $"    if {cell.Expression}:");
                    Add(cell, "        return True");
                    Add(cell, "    return False");
                    break;
                case Position.ComprehensionFilter:
                    Add(cell, $"    return len([k for k in [0] if {cell.Expression}]) == 1");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(cells));
            }
            lines.Add("");
        }

        lines.Add("def main():");
        foreach (var cell in cells)
        {
            var id = cell.Id.ToString(CultureInfo.InvariantCulture);
            Add(cell, $"    print({id}, cell_{id}())");
        }
        lines.Add("    print(\"done\")");
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

    /// <summary>What a cell must do: print CPython's lines, print pinned lines, or be refused by one code.</summary>
    private sealed record Expectation(bool Compared, IReadOnlyList<string>? Pinned, string? RefusedBy, string Why);

    private static readonly Expectation ComparedToCPython = new(true, null, null, "CPython 3.15");

    private static Expectation Expect(Cell cell)
    {
        if (!IsIdentity(cell.Op))
        {
            // Equality with an `object` operand is not an enum-pair decision: the checker's general rules own it.
            var objectOperands = (cell.Left == Operand.ObjectHoldingMember ? 1 : 0)
                + (cell.Right == Operand.ObjectHoldingMember ? 1 : 0);
            if (objectOperands == 2)
            {
                return cell.Position == Position.ChainLink
                    ? new(false, new[] { "False" }, null,
                        "`object == object` as a chain link is not refused as the binary form is, and compares the two "
                        + "boxes by reference (pinned; outside #2670's contract, cell on #2648)")
                    : new(false, null, DiagnosticCodes.Validation.UnsupportedOperator,
                        "`object == object` is refused (SPY0402): no enum operand type, outside #2670's contract");
            }
            if (objectOperands == 1)
                return new(false, null, DiagnosticCodes.Semantic.InvalidBinaryOperation,
                    "`object ==` an enum is refused by the checker (SPY0222): not a pair of enum types");
            return ComparedToCPython;
        }

        if (cell.Position == Position.ChainLink)
        {
            // `x is Member is y`: the type-test spelling heads a chain, which has no single-comparison reading.
            if (cell.Op == BinaryOperator.Is && IsMemberLiteral(cell.Right))
                return new(false, null, DiagnosticCodes.Semantic.IsTypeTestRetired,
                    "a chain headed by `x is Member` keeps the type-test refusal it had before #2669");
            return new(false, null, DiagnosticCodes.CodeGen.UnsupportedOperator,
                "an `is` link of a comparison chain is not lowered (SPY0519, the class #2648 tracks)");
        }

        if (cell.Left == Operand.ObjectHoldingMember && cell.Right == Operand.ObjectHoldingMember)
        {
            // Two `object`s: neither static type is an enum, so this is ordinary reference identity on two boxes.
            return new(false, new[] { cell.Op == BinaryOperator.Is ? "False" : "True" }, null,
                "both operands are typed `object`: outside #2669's contract, reference identity of two boxes (pinned; deviations.yaml enum-identity-is)");
        }

        return ComparedToCPython;
    }

    private sealed record SharpyVerdict(IReadOnlyList<string>? Values, IReadOnlySet<string>? RefusedBy);

    /// <summary>Compile rounds per program: checker refusals, then codegen refusals, then Roslyn's (SPY0908), which
    /// can surface one cell per round. A green tree needs three; the ceiling only bounds a red one.</summary>
    private const int MaxRounds = 200;

    /// <summary>Compiles the program, removing each cell a refusal is attributed to, until it runs.</summary>
    private Dictionary<int, SharpyVerdict> RunSharpy(
        string name, IReadOnlyList<Cell> cells, List<string> harnessFailures, List<string> boxingWarnings,
        Dictionary<int, string> emittedByCell)
    {
        var verdicts = new Dictionary<int, SharpyVerdict>();
        var active = cells.ToList();
        for (var round = 0; round < MaxRounds && active.Count > 0; round++)
        {
            var program = ProgramFor(active);
            var run = CompileAndExecute(SharpyHeader + program.Body, "enum_identity.spy", executionTimeoutMs: 120_000);
            if (RanToCompletion(run))
            {
                var byId = ParseLines(run.StandardOutput);
                foreach (var cell in active)
                    verdicts[cell.Id] = new SharpyVerdict(byId.TryGetValue(cell.Id, out var v) ? v : new List<string>(), null);
                if (!run.StandardOutput.Contains("done", StringComparison.Ordinal))
                    harnessFailures.Add($"{name}: the program did not reach its last line");
                foreach (var cell in active)
                    if (EmittedCellBody(run.GeneratedCSharp ?? "", cell.Id) is { } body)
                        emittedByCell[cell.Id] = body;
                boxingWarnings.AddRange(run.RawDiagnostics
                    .Where(d => d.Code == DiagnosticCodes.Validation.IsWithValueTypes)
                    .Select(d => $"{name} line {d.Line}: {d.Message}"));
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
                }
                else
                {
                    unattributed.Add($"line {d.Line}: [{d.Code}] {d.Message}");
                }
            }
            if (unattributed.Count > 0 || refused.Count == 0)
            {
                harnessFailures.Add($"{name}: round {round} failed with no cell to remove: "
                    + (unattributed.Count > 0 ? string.Join("; ", unattributed.Take(5)) : run.FailureSummary()));
                return verdicts;
            }
            foreach (var (id, codes) in refused)
                verdicts[id] = new SharpyVerdict(null, codes);
            active = active.Where(c => !refused.ContainsKey(c.Id)).ToList();
            Output.WriteLine($"{name}: round {round} removed {refused.Count} cell(s) ("
                + string.Join(",", refused.Values.SelectMany(c => c).Distinct().OrderBy(c => c, StringComparer.Ordinal)) + ")");
        }
        if (active.Count > 0)
            harnessFailures.Add($"{name}: still refusing after {MaxRounds} rounds");
        return verdicts;
    }

    /// <summary>The emitted C# of one cell's function (<c>cell_7</c> is <c>Cell7()</c>), up to the next member.</summary>
    private static string? EmittedCellBody(string generated, int id)
    {
        var start = generated.IndexOf($" Cell{id.ToString(CultureInfo.InvariantCulture)}()", StringComparison.Ordinal);
        if (start < 0)
            return null;
        var end = generated.IndexOf("public static", start, StringComparison.Ordinal);
        return end < 0 ? generated[start..] : generated[start..end];
    }

    /// <summary>
    /// The emitted-shape rule (#2670 follow-up): a comparison of two operands of ONE int-backed enum is the enum's
    /// native operator — never a boxing <c>object.Equals</c> / <c>object.ReferenceEquals</c> — whichever symbols the
    /// two operands' types reached the checker through (an imported CLR enum name against a CLR property's type
    /// were two symbols of one enum, and every such stdlib comparison was boxed). The positive control is the
    /// cross-family pair of the same run, which must be <c>object.Equals</c>: the probe is shown to see the call.
    /// Returns null when the cell meets the rule or the rule does not speak of it.
    /// </summary>
    private static string? ShapeViolation(Cell cell, string body)
    {
        if (Family(cell.Left) is not { } left || Family(cell.Right) is not { } right)
            return null;
        if (IsStringBacked(left) || IsStringBacked(right))
            return null;

        var boxes = body.Contains("object.Equals(", StringComparison.Ordinal)
            || body.Contains("object.ReferenceEquals(", StringComparison.Ordinal);
        if (left == right)
            return boxes ? "a same-enum pair must lower to the native operator, but the cell boxes" : null;
        return body.Contains("object.Equals(", StringComparison.Ordinal)
            ? null
            : "a pair of two different enums must lower to object.Equals (the shape probe's positive control)";
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

    [SkippableFact]
    public void EnumOperand_EveryOperandPairOperatorPosition_MatchesCPython()
    {
        // The oracle minor is the repository's .python-version pin (3.15).
        var oracle = PythonBatchOracle.Over(PythonInterpreter.RequireOrSkip(minMinor: 15));

        var cells = GenerateCells();

        // Totality: the count is the product of the axis sizes, never read back from the generator.
        var operandCount = Enum.GetValues<Operand>().Length;
        var positionCount = Enum.GetValues<Position>().Length;
        Assert.Equal(18, operandCount);
        Assert.Equal(4, positionCount);
        Assert.Equal(operandCount * operandCount * Operators.Length * positionCount, cells.Count);

        // One program per operator × position, in both languages: a refusal costs a recompile of its own group only.
        var groups = cells.GroupBy(c => (c.Op, c.Position))
            .Select(g => (Name: $"{Spell(g.Key.Op)}/{g.Key.Position}", Cells: g.ToList())).ToList();

        var py = oracle.RunBatch(groups.Select((g, i) =>
            new PyRequest(i, PythonHeader + ProgramFor(g.Cells).Body + "main()\n")).ToList());
        var pythonById = new Dictionary<int, List<string>>();
        for (var i = 0; i < groups.Count; i++)
        {
            Assert.True(py.TryGetValue(i, out var result) && result.Ok,
                $"python3 did not run the {groups[i].Name} program: " + (result?.Stderr ?? "no verdict"));
            foreach (var (id, values) in ParseLines(result!.Stdout))
                pythonById[id] = values;
        }
        Assert.Equal(cells.Count, pythonById.Count);

        var harnessFailures = new List<string>();
        var boxingWarnings = new List<string>();
        var sharpyById = new Dictionary<int, SharpyVerdict>();
        var emittedByCell = new Dictionary<int, string>();
        foreach (var group in groups)
            foreach (var (id, verdict) in RunSharpy(group.Name, group.Cells, harnessFailures, boxingWarnings, emittedByCell))
                sharpyById[id] = verdict;
        Assert.True(harnessFailures.Count == 0, "harness: " + string.Join("\n", harnessFailures));
        Assert.Equal(cells.Count, sharpyById.Count);

        var divergences = new List<string>();
        int compared = 0, pinned = 0, refusals = 0;
        foreach (var cell in cells)
        {
            var expected = Expect(cell);
            var sharpy = sharpyById[cell.Id];
            var python = pythonById[cell.Id];
            bool meets;
            if (expected.RefusedBy is { } code)
            {
                refusals++;
                meets = sharpy.RefusedBy is { Count: 1 } by && by.Single() == code;
            }
            else if (expected.Pinned is { } lines)
            {
                pinned++;
                meets = sharpy.Values is { } values && values.SequenceEqual(lines);
            }
            else
            {
                compared++;
                meets = sharpy.Values is { } values && values.SequenceEqual(python);
            }
            if (meets)
                continue;

            var observed = sharpy.RefusedBy is { } refusedBy
                ? "refused " + string.Join(",", refusedBy.OrderBy(c => c, StringComparer.Ordinal))
                : "[" + string.Join(" | ", sharpy.Values!) + "]";
            var wanted = expected.RefusedBy is { } wantCode ? "refused " + wantCode
                : expected.Pinned is { } wantLines ? "[" + string.Join(" | ", wantLines) + "] (pinned)"
                : "[" + string.Join(" | ", python) + "] (CPython)";
            divergences.Add($"{cell.Key}: `{cell.Expression}` sharpy {observed}, expected {wanted} — {expected.Why}");
        }

        // Every divergence is written out; the assertion message carries the first of them.
        var divergencePath = Path.Combine(ReportDirectory(), "enum-identity-differential-divergences.txt");
        var summary = $"cells: {cells.Count} (compared to CPython: {compared}, pinned values: {pinned}, "
            + $"hand-expected refusals: {refusals}); divergences: {divergences.Count}";
        File.WriteAllLines(divergencePath, divergences.Prepend("# " + summary));
        Output.WriteLine(summary);
        Assert.True(divergences.Count == 0,
            $"{divergences.Count} of {cells.Count} cell(s) diverge (all in {divergencePath}):\n"
            + string.Join("\n", divergences.Take(40)));

        // Emitted shape, over every cell that ran: same-enum pairs are native, cross-enum pairs are object.Equals.
        var shapeViolations = new List<string>();
        int sameEnumShapes = 0, crossEnumShapes = 0;
        foreach (var cell in cells)
        {
            if (sharpyById[cell.Id].Values == null)
                continue;
            if (!emittedByCell.TryGetValue(cell.Id, out var body))
            {
                shapeViolations.Add($"{cell.Key}: the cell ran but its emitted function was not found");
                continue;
            }
            if (Family(cell.Left) is { } lf && Family(cell.Right) is { } rf && !IsStringBacked(lf) && !IsStringBacked(rf))
            {
                if (lf == rf)
                    sameEnumShapes++;
                else
                    crossEnumShapes++;
            }
            if (ShapeViolation(cell, body) is { } violation)
                shapeViolations.Add($"{cell.Key}: `{cell.Expression}` {violation}: {body.Trim().ReplaceLineEndings(" ")}");
        }
        File.WriteAllLines(Path.Combine(ReportDirectory(), "enum-identity-differential-shapes.txt"),
            shapeViolations.Prepend($"# shape cells: same-enum {sameEnumShapes}, cross-enum {crossEnumShapes}; "
                + $"violations: {shapeViolations.Count}"));
        Assert.True(sameEnumShapes > 0 && crossEnumShapes > 0, "the shape assertion saw no cells");
        Assert.True(shapeViolations.Count == 0,
            $"{shapeViolations.Count} cell(s) have the wrong emitted shape (same-enum {sameEnumShapes}, cross-enum "
            + $"{crossEnumShapes}):\n" + string.Join("\n", shapeViolations.Take(30)));

        // Absence with its positive control: no cell warns "always False due to boxing", read from the diagnostics
        // the same probe is shown to hit for two ints.
        Assert.True(boxingWarnings.Count == 0, "SPY0465 on an enum operand:\n" + string.Join("\n", boxingWarnings.Take(20)));
        var control = CompileAndExecute("def main():\n    x: int = 1\n    y: int = 1\n    print(x is y)\n", "control.spy");
        Assert.True(RanToCompletion(control), control.FailureSummary());
        Assert.Contains(control.RawDiagnostics, d => d.Code == DiagnosticCodes.Validation.IsWithValueTypes);
    }
}
