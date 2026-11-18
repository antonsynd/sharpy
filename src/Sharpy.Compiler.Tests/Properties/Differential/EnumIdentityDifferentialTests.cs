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
/// another string enum with an equal value and with a different value), two effectful calls that print when
/// they are evaluated (so the answer and the number of evaluations of each operand are both compared), and the
/// CLR enums — a <c>System.DayOfWeek</c> variable, member, <c>DateTime.day_of_week</c> property (typed by the CLR
/// bridge, not by the imported name) and <c>DayOfWeek | None</c>, and a variable of another CLR enum
/// (<c>DateTimeKind</c>); their CPython twins are plain <c>Enum</c> classes of the same members. The
/// string-backed enums' twins are <c>StrEnum</c> classes: the spec says a string-backed enum "behaves like
/// CPython's <c>StrEnum</c>";</item>
/// <item><b>operator</b> — <see cref="Operators"/>;</item>
/// <item><b>position</b> — <see cref="Position"/>: the binary operator as a printed value, an <c>if</c>
/// condition, a comparison-chain link, a comprehension filter.</item>
/// </list>
/// <para><b>Equality</b> (#2670) rides the same matrix: <c>==</c> / <c>!=</c> on enum operands answers as CPython
/// answers for the enum kind the spec maps each to, both operands evaluated once. Two DIFFERENT enum types that
/// are not both string-backed answer False / True, as plain <c>Enum</c> members do (owner ruling 2026-11-12) —
/// it was CS0019 behind SPY0908 for two int-backed enums, an int-backed against a string-backed one and
/// <c>E | None</c> against another enum. Two string-backed enums compare as their strings, as <c>StrEnum</c>
/// members do (owner ruling 2026-11-17): <c>Tone.A == Mood.A</c> is True. The same-enum pairs are the
/// control.</para>
/// <para><b>Expected map</b> (<see cref="Expect"/>). COMPARED cells print CPython's lines — an <c>is</c> link of a
/// comparison chain included: a link is its binary twin (P93 D3, #2648), and a chain headed by <c>x is Member</c>
/// gives its first link the type test's rule (P93 D4). HAND-EXPECTED cells,
/// counted apart: <c>is</c> between two operands that are BOTH typed
/// <c>object</c> stays boxed reference identity — no operand's static type is an enum, so the contract does not
/// reach it and the answer is pinned so that a change is seen; and <c>==</c> / <c>!=</c> with an <c>object</c>
/// operand is the checker's general rule, not an enum-pair decision (SPY0222 against an enum, SPY0402 for two
/// objects, in a chain link as in the binary form).</para>
/// <para><b>Emitted shape</b> (<see cref="ShapeViolation"/>): every cell that runs and compares two operands of ONE
/// int-backed enum must emit the enum's native operator, and every pair of two different non-string enums
/// <c>EnumHelpers.Identity</c> for <c>is</c> (#2680) and <c>object.Equals</c> for <c>==</c> (the probe's positive
/// control). The answers alone cannot see a same-enum pair that was boxed: either call answers it correctly. An <c>==</c> / <c>!=</c> between two different string-backed
/// enums must NOT be <c>object.Equals</c> (it would answer False): it is the native string comparison.</para>
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
        StrEnumVar, StrEnumMember, OtherStrEnumVar, OtherStrEnumDifferentValueVar,
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
        Operand.OtherStrEnumVar or Operand.OtherStrEnumDifferentValueVar => "Mood",
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
        Operand.OtherStrEnumDifferentValueVar => $"{side}_moodb: Mood = Mood.B",
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
        Operand.OtherStrEnumDifferentValueVar => $"{side}_moodb",
        Operand.EnumCall => $"ev_color({cell.ToString(CultureInfo.InvariantCulture)})",
        Operand.OtherEnumCall => $"ev_size({cell.ToString(CultureInfo.InvariantCulture)})",
        Operand.ClrEnumVar => $"{side}_day",
        Operand.ClrEnumMember => "DayOfWeek.Monday",
        Operand.ClrEnumProperty => $"{side}_dt.day_of_week",
        Operand.OptionalClrEnum => $"{side}_oday",
        Operand.OtherClrEnumVar => $"{side}_kind",
        _ => throw new ArgumentOutOfRangeException(nameof(operand)),
    };

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
            B = "b"


        """;

    private const string PythonHeader = """
        from enum import Enum, StrEnum

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

        # A string-backed enum "behaves like CPython's StrEnum" (enums.md): its members compare as their strings.
        class Tone(StrEnum):
            A = "a"
            B = "b"

        class Mood(StrEnum):
            A = "a"
            B = "b"


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

    /// <summary>What a cell must do: print CPython's lines, print pinned lines, or be refused by exactly the codes
    /// <see cref="RefusedBy"/> lists (sorted, comma-joined).</summary>
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
                // A chain link is refused as its binary twin is (P93 D3; owner ruling, #2648 comment 6865267508).
                return new(false, null, DiagnosticCodes.Validation.UnsupportedOperator,
                    "`object == object` is refused (SPY0402), as a chain link too: no enum operand type, outside #2670's contract");
            }
            if (objectOperands == 1)
            {
                // A chain `l == r == r` is its two links (P93 D3): with the `object` on the right, the second link
                // is `object == object`, refused as its twin is (SPY0402) beside the first link's SPY0222.
                return cell.Position == Position.ChainLink && cell.Right == Operand.ObjectHoldingMember
                    ? new(false, null,
                        DiagnosticCodes.Semantic.InvalidBinaryOperation + "," + DiagnosticCodes.Validation.UnsupportedOperator,
                        "`object ==` an enum is refused by the checker (SPY0222), and the chain's `object == object` link "
                        + "as its twin (SPY0402)")
                    : new(false, null, DiagnosticCodes.Semantic.InvalidBinaryOperation,
                        "`object ==` an enum is refused by the checker (SPY0222): not a pair of enum types");
            }
            return ComparedToCPython;
        }

        // A chain link is its binary twin (P93 D3, #2648), and a chain headed by `x is Member` takes the type
        // test's rule for its first link (P93 D4): an enum member is an identity link. So an `is` chain cell
        // `l op r op r` answers what its two links answer.
        if (cell.Left == Operand.ObjectHoldingMember && cell.Right == Operand.ObjectHoldingMember)
        {
            // Two `object`s: neither static type is an enum, so this is ordinary reference identity on two boxes.
            // As a chain, the second link compares `r` with itself: one box, so `is` holds and `is not` fails
            // — `l is r` (False) short-circuits; `l is not r` (True) then `r is not r` (False).
            return new(false, new[] { cell.Position == Position.ChainLink || cell.Op == BinaryOperator.Is ? "False" : "True" }, null,
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

    /// <summary>The Core helper an <c>is</c> between an enum and a non-same-enum operand lowers to (#2680).</summary>
    private const string EnumIdentityHelper = "global::Sharpy.EnumHelpers.Identity(";

    /// <summary>
    /// The emitted-shape rule (#2670 follow-up): a comparison of two operands of ONE int-backed enum is the enum's
    /// native operator — never a boxing <c>object.Equals</c> / <c>object.ReferenceEquals</c> /
    /// <c>EnumHelpers.Identity</c> — whichever symbols the two operands' types reached the checker through (an
    /// imported CLR enum name against a CLR property's type were two symbols of one enum, and every such stdlib
    /// comparison was boxed). The positive control is the cross-family pair of the same run, which must be
    /// <c>EnumHelpers.Identity</c> for <c>is</c> and <c>object.Equals</c> for <c>==</c>: the probe is shown to see the call.
    /// An <c>==</c> / <c>!=</c> between two DIFFERENT string-backed enums must not be <c>object.Equals</c>, which
    /// answers False where <c>StrEnum</c> compares the strings: it is the native string comparison.
    /// Returns null when the cell meets the rule or the rule does not speak of it.
    /// </summary>
    private static string? ShapeViolation(Cell cell, string body)
    {
        if (Family(cell.Left) is not { } left || Family(cell.Right) is not { } right)
            return null;
        if (IsStringBacked(left) && IsStringBacked(right) && left != right && !IsIdentity(cell.Op))
        {
            return body.Contains("object.Equals(", StringComparison.Ordinal)
                ? "an `==` / `!=` between two different string-backed enums must compare the strings, but the cell "
                    + "lowers to object.Equals"
                : null;
        }
        if (IsStringBacked(left) || IsStringBacked(right))
            return null;

        var boxes = body.Contains("object.Equals(", StringComparison.Ordinal)
            || body.Contains("object.ReferenceEquals(", StringComparison.Ordinal)
            || body.Contains(EnumIdentityHelper, StringComparison.Ordinal);
        if (left == right)
            return boxes ? "a same-enum pair must lower to the native operator, but the cell boxes" : null;
        // `is` asks EnumHelpers.Identity, which never calls an operand's Equals (#2680); `==` is object.Equals.
        var crossEnumCall = IsIdentity(cell.Op) ? EnumIdentityHelper : "object.Equals(";
        return body.Contains(crossEnumCall, StringComparison.Ordinal)
            ? null
            : $"a pair of two different enums must lower to {crossEnumCall}…) (the shape probe's positive control)";
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
        Assert.Equal(19, operandCount);
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
                // The refusing codes, sorted and comma-joined: a chain is refused by each of its links' codes.
                meets = sharpy.RefusedBy is { Count: > 0 } by
                    && string.Join(",", by.OrderBy(c => c, StringComparer.Ordinal)) == code;
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
        int sameEnumShapes = 0, crossEnumShapes = 0, crossStrEnumShapes = 0;
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
            else if (Family(cell.Left) is { } sl && Family(cell.Right) is { } sr && IsStringBacked(sl) && IsStringBacked(sr)
                && sl != sr && !IsIdentity(cell.Op))
            {
                crossStrEnumShapes++;
            }
            if (ShapeViolation(cell, body) is { } violation)
                shapeViolations.Add($"{cell.Key}: `{cell.Expression}` {violation}: {body.Trim().ReplaceLineEndings(" ")}");
        }
        File.WriteAllLines(Path.Combine(ReportDirectory(), "enum-identity-differential-shapes.txt"),
            shapeViolations.Prepend($"# shape cells: same-enum {sameEnumShapes}, cross-enum {crossEnumShapes}, "
                + $"cross-string-enum equality {crossStrEnumShapes}; violations: {shapeViolations.Count}"));
        Assert.True(sameEnumShapes > 0 && crossEnumShapes > 0 && crossStrEnumShapes > 0, "the shape assertion saw no cells");
        Assert.True(shapeViolations.Count == 0,
            $"{shapeViolations.Count} cell(s) have the wrong emitted shape (same-enum {sameEnumShapes}, cross-enum "
            + $"{crossEnumShapes}, cross-string-enum equality {crossStrEnumShapes}):\n"
            + string.Join("\n", shapeViolations.Take(30)));

        // Absence with its positive control: no cell warns "always False due to boxing", read from the diagnostics
        // the same probe is shown to hit for two ints.
        Assert.True(boxingWarnings.Count == 0, "SPY0465 on an enum operand:\n" + string.Join("\n", boxingWarnings.Take(20)));
        var control = CompileAndExecute("def main():\n    x: int = 1\n    y: int = 1\n    print(x is y)\n", "control.spy");
        Assert.True(RanToCompletion(control), control.FailureSummary());
        Assert.Contains(control.RawDiagnostics, d => d.Code == DiagnosticCodes.Validation.IsWithValueTypes);
    }

    // ---- `is` never runs user code (#2680) ------------------------------------------------------------------

    /// <summary>The enum the cell's enum operand belongs to: int-backed (<c>Color</c>) or string-backed (<c>Tone</c>).</summary>
    private enum Backing { Int, Str }

    /// <summary>How the enum operand is written: a variable, the member itself (the spelling the parser reads as a
    /// type name, checked by the type-test rule), or an <c>E | None</c> variable holding the member.</summary>
    private enum EnumSpelling { Variable, Member, OptionalVariable }

    /// <summary>Which side of <c>is</c> the enum operand is on; the other operand takes the other side.</summary>
    private enum EnumSide { Left, Right }

    /// <summary>
    /// The operand the enum is tested against: a class whose <c>__eq__</c> prints the cell id and answers True, one
    /// whose <c>__eq__</c> prints and answers False, an <c>object</c> holding the same member and another member,
    /// another int-backed enum with an equal value (<c>Size.S</c>, value 1), another string-backed enum
    /// (<c>Mood.A</c> against <c>Tone</c>, <c>Tone.A</c> against <c>Color</c>), and the <c>None</c> literal.
    /// </summary>
    private enum OtherOperand { EqTrue, EqFalse, ObjectSameMember, ObjectOtherMember, IntEnumEqualValue, StrEnum, None }

    private static readonly BinaryOperator[] IdentityOperators = { BinaryOperator.Is, BinaryOperator.IsNot };

    private sealed record UserCodeCell(
        int Id, Backing Backing, EnumSpelling Spelling, EnumSide Side, OtherOperand Other, BinaryOperator Op,
        Position Position)
    {
        public string Key => $"{Backing}/{Spelling} {Side} {Spell(Op)} {Other} @{Position}";

        private string EnumOperand => Spelling switch
        {
            EnumSpelling.Member => Backing == Backing.Int ? "Color.RED" : "Tone.A",
            _ => "e",
        };

        private string OtherOperandText => Other == OtherOperand.None ? "None" : "o";

        public string? EnumDeclaration => Spelling switch
        {
            EnumSpelling.Variable => Backing == Backing.Int ? "e: Color = Color.RED" : "e: Tone = Tone.A",
            EnumSpelling.OptionalVariable => Backing == Backing.Int ? "e: Color | None = Color.RED" : "e: Tone | None = Tone.A",
            _ => null,
        };

        public string? OtherDeclaration
        {
            get
            {
                var id = Id.ToString(CultureInfo.InvariantCulture);
                return Other switch
                {
                    OtherOperand.EqTrue => $"o: EqTrue = EqTrue({id})",
                    OtherOperand.EqFalse => $"o: EqFalse = EqFalse({id})",
                    OtherOperand.ObjectSameMember => Backing == Backing.Int ? "o: object = Color.RED" : "o: object = Tone.A",
                    OtherOperand.ObjectOtherMember => Backing == Backing.Int ? "o: object = Color.GREEN" : "o: object = Tone.B",
                    OtherOperand.IntEnumEqualValue => "o: Size = Size.S",
                    OtherOperand.StrEnum => Backing == Backing.Int ? "o: Tone = Tone.A" : "o: Mood = Mood.A",
                    OtherOperand.None => null,
                    _ => throw new ArgumentOutOfRangeException(nameof(Other)),
                };
            }
        }

        public string Expression
        {
            get
            {
                var (left, right) = Side == EnumSide.Left ? (EnumOperand, OtherOperandText) : (OtherOperandText, EnumOperand);
                var link = $"{left} {Spell(Op)} {right}";
                // A chain's last operand is the right operand again: `l op r op r`.
                return Position == Position.ChainLink ? $"{link} {Spell(Op)} {right}" : link;
            }
        }
    }

    private static List<UserCodeCell> GenerateUserCodeCells()
    {
        var cells = new List<UserCodeCell>();
        foreach (var op in IdentityOperators)
            foreach (var position in Enum.GetValues<Position>())
                foreach (var backing in Enum.GetValues<Backing>())
                    foreach (var spelling in Enum.GetValues<EnumSpelling>())
                        foreach (var side in Enum.GetValues<EnumSide>())
                            foreach (var other in Enum.GetValues<OtherOperand>())
                                cells.Add(new UserCodeCell(cells.Count + 1, backing, spelling, side, other, op, position));
        return cells;
    }

    /// <summary>The two user classes, the same source in both languages: each <c>__eq__</c> prints its cell's id, so
    /// a call is a line of that cell's output and is compared with CPython's (which has none for <c>is</c>).</summary>
    private const string UserEqClasses = """
        class EqTrue:
            cell: int

            def __init__(self, cell: int):
                self.cell = cell

            def __eq__(self, other: object) -> bool:
                print(self.cell, "eq-true called")
                return True

            def __hash__(self) -> int:
                return 0

        class EqFalse:
            cell: int

            def __init__(self, cell: int):
                self.cell = cell

            def __eq__(self, other: object) -> bool:
                print(self.cell, "eq-false called")
                return False

            def __hash__(self) -> int:
                return 0


        """;

    private static string UserCodeProgram(IEnumerable<UserCodeCell> cells)
    {
        var lines = new List<string>();
        foreach (var cell in cells)
        {
            var id = cell.Id.ToString(CultureInfo.InvariantCulture);
            lines.Add($"def cell_{id}() -> bool:");
            if (cell.EnumDeclaration is { } enumDeclaration)
                lines.Add("    " + enumDeclaration);
            if (cell.OtherDeclaration is { } otherDeclaration)
                lines.Add("    " + otherDeclaration);
            switch (cell.Position)
            {
                case Position.Print:
                case Position.ChainLink:
                    lines.Add($"    return {cell.Expression}");
                    break;
                case Position.IfCondition:
                    lines.Add($"    if {cell.Expression}:");
                    lines.Add("        return True");
                    lines.Add("    return False");
                    break;
                case Position.ComprehensionFilter:
                    lines.Add($"    return len([k for k in [0] if {cell.Expression}]) == 1");
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
            lines.Add($"    print({id}, cell_{id}())");
        }
        lines.Add("    print(\"done\")");
        return string.Join("\n", lines) + "\n";
    }

    /// <summary>
    /// The class guard for #2680 (owner ruling 2026-11-17, "1) your rec"): <c>is</c> / <c>is not</c> with an enum
    /// operand on either side never runs user code and answers what CPython 3.15 answers — the same member → True,
    /// anything else → False, an <c>object</c> holding the member compares by member. Generated, never listed: the
    /// product of enum backing {int, string} × enum spelling {variable, member, <c>E | None</c>} × the enum's side
    /// {left, right} × the other operand (<see cref="OtherOperand"/>) × {<c>is</c>, <c>is not</c>} × position (a
    /// returned value, an <c>if</c> condition, a chain link, a comprehension filter). Every cell is compared to
    /// CPython's lines, which include any <c>__eq__</c> call (each prints its cell id): CPython prints none for
    /// <c>is</c>. The positive control shows the probe sees an <c>__eq__</c> call: <c>==</c> on the same classes
    /// prints its line in both languages.
    /// </summary>
    [SkippableFact]
    public void EnumIdentity_NeverRunsUserEq_MatchesCPython()
    {
        var oracle = PythonBatchOracle.Over(PythonInterpreter.RequireOrSkip(minMinor: 15));

        var cells = GenerateUserCodeCells();
        // Totality: the count is the product of the axis sizes, never read back from the generator.
        Assert.Equal(2, Enum.GetValues<Backing>().Length);
        Assert.Equal(3, Enum.GetValues<EnumSpelling>().Length);
        Assert.Equal(2, Enum.GetValues<EnumSide>().Length);
        Assert.Equal(7, Enum.GetValues<OtherOperand>().Length);
        Assert.Equal(2 * 3 * 2 * 7 * IdentityOperators.Length * Enum.GetValues<Position>().Length, cells.Count);

        var groups = cells.GroupBy(c => (c.Op, c.Position))
            .Select(g => (Name: $"{Spell(g.Key.Op)}/{g.Key.Position}", Cells: g.ToList())).ToList();
        // The positive control: `==` runs __eq__ in both languages, and the probe reads its line.
        const string controlBody = "def main():\n    print(1, EqTrue(1) == EqFalse(2))\n    print(\"done\")\n";

        var requests = groups.Select((g, i) =>
            new PyRequest(i, PythonHeader + UserEqClasses + UserCodeProgram(g.Cells) + "main()\n")).ToList();
        requests.Add(new PyRequest(groups.Count, PythonHeader + UserEqClasses + controlBody + "main()\n"));
        var py = oracle.RunBatch(requests);
        var pythonById = new Dictionary<int, List<string>>();
        for (var i = 0; i < groups.Count; i++)
        {
            Assert.True(py.TryGetValue(i, out var result) && result.Ok,
                $"python3 did not run the {groups[i].Name} program: " + (result?.Stderr ?? "no verdict"));
            foreach (var (id, values) in ParseLines(result!.Stdout))
                pythonById[id] = values;
        }
        Assert.Equal(cells.Count, pythonById.Count);

        var divergences = new List<string>();
        var eqCallsInCPython = 0;
        foreach (var group in groups)
        {
            var run = CompileAndExecute(SharpyHeader + UserEqClasses + UserCodeProgram(group.Cells),
                "enum_identity_user_eq.spy", executionTimeoutMs: 120_000);
            Assert.True(RanToCompletion(run) && run.StandardOutput.Contains("done", StringComparison.Ordinal),
                $"{group.Name}: the program did not run to completion: " + run.FailureSummary());
            var sharpyById = ParseLines(run.StandardOutput);
            foreach (var cell in group.Cells)
            {
                var python = pythonById[cell.Id];
                eqCallsInCPython += python.Count(l => l.EndsWith(" called", StringComparison.Ordinal));
                var sharpy = sharpyById.TryGetValue(cell.Id, out var v) ? v : new List<string>();
                if (!sharpy.SequenceEqual(python))
                {
                    divergences.Add($"{cell.Key}: `{cell.Expression}` sharpy [{string.Join(" | ", sharpy)}], "
                        + $"CPython [{string.Join(" | ", python)}]");
                }
            }
        }

        var summary = $"cells: {cells.Count}; divergences: {divergences.Count}";
        File.WriteAllLines(Path.Combine(ReportDirectory(), "enum-identity-user-eq-divergences.txt"),
            divergences.Prepend("# " + summary));
        Output.WriteLine(summary);
        Assert.True(divergences.Count == 0,
            $"{divergences.Count} of {cells.Count} cell(s) diverge:\n" + string.Join("\n", divergences.Take(40)));
        // CPython's `is` never calls __eq__: every compared line set is free of one.
        Assert.Equal(0, eqCallsInCPython);

        // Positive control: the same classes' `==` prints the __eq__ line in both languages, and the probe reads it.
        Assert.True(py.TryGetValue(groups.Count, out var controlPy) && controlPy.Ok, controlPy?.Stderr ?? "no verdict");
        var expectedControl = new[] { "eq-true called", "True" };
        Assert.Equal(expectedControl, ParseLines(controlPy!.Stdout)[1]);
        var controlRun = CompileAndExecute(SharpyHeader + UserEqClasses + controlBody, "enum_identity_user_eq_control.spy");
        Assert.True(RanToCompletion(controlRun), controlRun.FailureSummary());
        Assert.Equal(expectedControl, ParseLines(controlRun.StandardOutput)[1]);
    }
}
