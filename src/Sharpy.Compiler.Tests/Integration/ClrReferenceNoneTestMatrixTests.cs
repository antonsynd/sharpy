using System.Text;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// One rule for <c>None</c> tests on a .NET reference type (#2238, ruling R-ES, lifting #2221's R-DX part 1
/// for CLR types): <c>== None</c> / <c>!= None</c> is admitted on EVERY operand — declared <c>T</c>,
/// <c>T | None</c>, narrowed by assignment, narrowed by <c>is not None</c>, a <c>T?</c> narrowed by
/// <c>is not None</c> — and runs the type's SAME-TYPE <c>operator ==</c> / <c>!=</c>: the None operand is
/// emitted as a typed null, <c>x == (T?)null</c> (cure A), so no operator-overload shape makes it ambiguous
/// (CS9342) and a destroyed "fake null" object answers <c>== None</c> → True. <c>is None</c> stays the
/// reference check that never runs user code (#2224, ruling R-EJ): the same destroyed object answers
/// <c>is None</c> → False. Sharpy classes, value types and the un-narrowed Optional are controls whose
/// verdicts the lift does not touch.
/// </summary>
/// <remarks>
/// <para>Axes: kind {CLR reference with a fake-null same-type <c>==</c> (the destroyed-object <c>Handle</c>
/// of <see cref="IsNoneReferenceCheckMatrixTests"/>), with a plain same-type <c>==</c>
/// (<c>System.Version</c>), with none (<c>StringBuilder</c>), with TWO nullable overloads
/// (<c>Amb</c>: same-type + <c>Other</c>), with a same-type + <c>object</c> overload (<c>WithObject</c>), with an
/// <c>==</c> only against ANOTHER type (<c>OnlyOther</c> — the typed null binds reference equality); Sharpy
/// class with a well-behaved <c>__eq__(object)</c>, with a permissive (always-True) one, without
/// <c>__eq__</c>; value type <c>int</c>} × form {declared <c>T</c>, <c>T | None</c> un-narrowed, narrowed by
/// assignment, narrowed by <c>is not None</c>, <c>T?</c> un-narrowed, <c>T?</c> narrowed by <c>is not None</c>}
/// × spelling {<c>x == None</c>, <c>x != None</c>, <c>None == x</c>, <c>None != x</c>, and the <c>is</c> twins
/// <c>x is None</c>, <c>x is not None</c>, <c>None is x</c>, <c>None is not x</c>} × position {<c>return</c>,
/// <c>if</c>, <c>assert</c>, <c>assert</c> under the <c>@test</c> host}.</para>
/// <para>The verdict table (<see cref="IsRefused"/>) and the truth table (<see cref="Truth"/>) are spelled
/// by hand. Every admitted cell is EXECUTED for every subject state its form can hold (a destroyed and a
/// live specimen; absent only for the un-narrowed forms). The specimens' operators make WHICH overload ran
/// visible: each same-type <c>==</c> treats a destroyed object as null, and each cross-type overload
/// (against <c>Other</c> or <c>object</c>) answers True to BOTH <c>==</c> and <c>!=</c> — no consistent
/// operator does — so a cell that bound it, or that fell to reference equality on a type with a same-type
/// operator, prints a value no expected row holds. An assert cell observes both directions (holds → True,
/// violated → the assertion error, never another throw). Every refused cell must report SPY0222 with the
/// <c>is None</c> steer at its own line.</para>
/// </remarks>
[Collection("HeavyCompilation")]
public class ClrReferenceNoneTestMatrixTests : IntegrationTestBase
{
    public ClrReferenceNoneTestMatrixTests(ITestOutputHelper output) : base(output)
    {
    }

    /// <summary>
    /// Operator-overload shapes. A cross-type overload answers True to both <c>==</c> and <c>!=</c>, so a
    /// test bound to it is distinguishable from the same-type operator (fake null: a destroyed object
    /// equals null) and from reference equality.
    /// </summary>
    private const string OverloadSource = """
        namespace Spyoverloads
        {
            public class Other { }

            public class Amb
            {
                public Amb(bool destroyed) { Destroyed = destroyed; }
                public bool Destroyed { get; }

                public static bool operator ==(Amb? a, Amb? b)
                {
                    var aNull = a is null || a.Destroyed;
                    var bNull = b is null || b.Destroyed;
                    return aNull || bNull ? aNull && bNull : ReferenceEquals(a, b);
                }

                public static bool operator !=(Amb? a, Amb? b) => !(a == b);
                public static bool operator ==(Amb? a, Other? b) => true;
                public static bool operator !=(Amb? a, Other? b) => true;
                public override bool Equals(object? obj) => ReferenceEquals(this, obj);
                public override int GetHashCode() => 0;
            }

            public class WithObject
            {
                public WithObject(bool destroyed) { Destroyed = destroyed; }
                public bool Destroyed { get; }

                public static bool operator ==(WithObject? a, WithObject? b)
                {
                    var aNull = a is null || a.Destroyed;
                    var bNull = b is null || b.Destroyed;
                    return aNull || bNull ? aNull && bNull : ReferenceEquals(a, b);
                }

                public static bool operator !=(WithObject? a, WithObject? b) => !(a == b);
                public static bool operator ==(WithObject? a, object? b) => true;
                public static bool operator !=(WithObject? a, object? b) => true;
                public override bool Equals(object? obj) => ReferenceEquals(this, obj);
                public override int GetHashCode() => 0;
            }

            public class OnlyOther
            {
                public OnlyOther(bool destroyed) { Destroyed = destroyed; }
                public bool Destroyed { get; }

                public static bool operator ==(OnlyOther? a, Other? b) => true;
                public static bool operator !=(OnlyOther? a, Other? b) => true;
                public override bool Equals(object? obj) => ReferenceEquals(this, obj);
                public override int GetHashCode() => 0;
            }
        }
        """;

    private static readonly Lazy<string> OverloadAssembly =
        new(() => IsNoneReferenceCheckMatrixTests.BuildSpecimenAssembly("Spyoverloads", OverloadSource));

    protected override IEnumerable<string> GetAdditionalReferenceAssemblyPaths()
        => base.GetAdditionalReferenceAssemblyPaths()
            .Append(typeof(Xunit.Assert).Assembly.Location)
            .Append(IsNoneReferenceCheckMatrixTests.FakeNullAssembly.Value)
            .Append(OverloadAssembly.Value);

    public enum Kind
    {
        ClrFakeNull, ClrOverloadedEq, ClrPlainReference, ClrTwoNullableOverloads, ClrObjectOverload,
        ClrOnlyOtherTypeOverload, SharpyEq, SharpyPermissiveEq, SharpyNoEq, ValueType,
    }

    public enum Form { Declared, Unnarrowed, NarrowedByAssignment, NarrowedByIsNotNone, Optional, NarrowedOptional }

    public enum Spelling { EqNone, NotEqNone, NoneEq, NoneNotEq, IsNone, IsNotNone, NoneIs, NoneIsNot }

    public enum Position { Return, If, Assert, TestHostAssert }

    private const string Prelude = """
        from system import Version
        from system.text import StringBuilder
        from spyfakenull import Handle
        from spyoverloads import Amb, WithObject, OnlyOther
        from xunit.sdk import XunitException

        class W:
            v: int
            def __init__(self, v: int):
                self.v = v
            def __eq__(self, other: object) -> bool:
                return isinstance(other, W) and self.v == other.v
            def __hash__(self) -> int:
                return self.v

        class P:
            v: int
            def __init__(self, v: int):
                self.v = v
            def __eq__(self, other: object) -> bool:
                return True
            def __hash__(self) -> int:
                return 1

        class N:
            v: int
            def __init__(self, v: int):
                self.v = v


        """;

    /// <summary>One subject state: the value expression (absent: null), and what the type's selected
    /// <c>==</c> answers for <c>x == None</c> while it is present — True for a destroyed specimen whose
    /// same-type <c>==</c> treats it as null, and for <c>P</c> whose <c>__eq__</c> says True to everything
    /// (python3: <c>P(2) == None</c> and <c>None == P(2)</c> are True).</summary>
    private sealed record State(string Name, string? Value, bool EqualsNone)
    {
        public bool Absent => Value is null;
    }

    /// <summary>A launder-function key, the declared type, and the present states.</summary>
    private sealed record KindSpec(string Key, string Type, State[] Present);

    private static State[] DestroyedAndLive(string type, bool sameTypeOperator) => new[]
    {
        new State("destroyed", $"{type}(True)", sameTypeOperator), new State("live", $"{type}(False)", false),
    };

    private static KindSpec Spec(Kind kind) => kind switch
    {
        Kind.ClrFakeNull => new("handle", "Handle", DestroyedAndLive("Handle", true)),
        Kind.ClrOverloadedEq => new("version", "Version", new[] { new State("value", "Version(1, 2)", false) }),
        Kind.ClrPlainReference => new("sb", "StringBuilder", new[] { new State("value", "StringBuilder()", false) }),
        Kind.ClrTwoNullableOverloads => new("amb", "Amb", DestroyedAndLive("Amb", true)),
        Kind.ClrObjectOverload => new("withobj", "WithObject", DestroyedAndLive("WithObject", true)),
        // No same-type operator: the typed null binds reference equality, so a destroyed object is not None.
        Kind.ClrOnlyOtherTypeOverload => new("onlyother", "OnlyOther", DestroyedAndLive("OnlyOther", false)),
        Kind.SharpyEq => new("w", "W", new[] { new State("value", "W(2)", false) }),
        Kind.SharpyPermissiveEq => new("p", "P", new[] { new State("value", "P(2)", true) }),
        Kind.SharpyNoEq => new("n", "N", new[] { new State("value", "N(2)", false) }),
        Kind.ValueType => new("int", "int", new[] { new State("value", "0", false) }),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>Subject states per form: only the un-narrowed forms can hold None.</summary>
    private static State[] States(Kind kind, Form form)
    {
        var spec = Spec(kind);
        return form is Form.Unnarrowed or Form.Optional
            ? spec.Present.Append(new State("absent", null, true)).ToArray()
            : spec.Present;
    }

    private static bool IsEquality(Spelling spelling) => spelling <= Spelling.NoneNotEq;

    /// <summary>
    /// Hand-spelled verdicts. Every CLR-reference and Sharpy-class cell is admitted (R-ES). The equality
    /// spellings stay refused on a value type (statically always-False, SPY0222 — `int | None` and a
    /// narrowed `int?` included) and on an un-narrowed Optional `T?`, which must be narrowed first: a `T?`
    /// narrowed by `is not None` IS the bare `T`, so it takes the rule of `T`.
    /// </summary>
    private static bool IsRefused(Kind kind, Form form, Spelling spelling)
        => IsEquality(spelling) && (kind == Kind.ValueType || form == Form.Optional);

    /// <summary>The truth value: <c>is</c> asks absence; <c>==</c> asks the type's selected operator.</summary>
    private static bool Truth(Spelling spelling, State state)
    {
        var equalsNone = IsEquality(spelling) ? state.Absent || state.EqualsNone : state.Absent;
        var negated = spelling is Spelling.NotEqNone or Spelling.NoneNotEq or Spelling.IsNotNone or Spelling.NoneIsNot;
        return equalsNone != negated;
    }

    private static string Test(Spelling spelling) => spelling switch
    {
        Spelling.EqNone => "x == None",
        Spelling.NotEqNone => "x != None",
        Spelling.NoneEq => "None == x",
        Spelling.NoneNotEq => "None != x",
        Spelling.IsNone => "x is None",
        Spelling.IsNotNone => "x is not None",
        Spelling.NoneIs => "None is x",
        Spelling.NoneIsNot => "None is not x",
        _ => throw new ArgumentOutOfRangeException(nameof(spelling)),
    };

    private static string Steer(Spelling spelling)
        => spelling is Spelling.EqNone or Spelling.NoneEq ? "Did you mean 'is None'?" : "Did you mean 'is not None'?";

    /// <summary>The lines binding <c>x</c> for a form and state; the un-narrowed forms pass the value
    /// through a launder function so nothing narrows it.</summary>
    private static string[] Binding(KindSpec spec, Form form, State state) => form switch
    {
        Form.Declared => new[] { $"x: {spec.Type} = {state.Value}" },
        Form.Unnarrowed or Form.NarrowedByIsNotNone =>
            new[] { $"x: {spec.Type} | None = launder_{spec.Key}({state.Value ?? "None"})" },
        Form.NarrowedByAssignment => new[] { $"x: {spec.Type} | None = None", $"x = {state.Value}" },
        Form.Optional or Form.NarrowedOptional => new[]
        {
            $"x: {spec.Type}? = launder_opt_{spec.Key}({(state.Value is null ? "None()" : $"Some({state.Value})")})",
        },
        _ => throw new ArgumentOutOfRangeException(nameof(form)),
    };

    private static string Launders(KindSpec spec)
        => $"def launder_{spec.Key}(v: {spec.Type} | None) -> {spec.Type} | None:\n    return v\n\n"
            + $"def launder_opt_{spec.Key}(v: {spec.Type}?) -> {spec.Type}?:\n    return v\n\n";

    /// <summary>The statements testing <paramref name="test"/> in a position; the index of the line that
    /// carries the test is returned for the refusal's location check.</summary>
    private static (string[] Lines, int TestLine) Body(Position position, string test) => position switch
    {
        Position.Return => (new[] { $"return {test}" }, 0),
        Position.If => (new[] { $"if {test}:", "    return True", "return False" }, 0),
        Position.Assert => (new[]
        {
            "try:", $"    assert {test}", "    return True", "except AssertionError:", "    return False",
        }, 1),
        Position.TestHostAssert => (new[] { $"assert {test}" }, 0),
        _ => throw new ArgumentOutOfRangeException(nameof(position)),
    };

    /// <summary>One generated cell: the label printed with its outcome, its expected outcome, and the
    /// 1-based source line of its test.</summary>
    private sealed record Cell(string Label, string Expected, int Line);

    /// <summary>
    /// Builds ONE program for a kind holding every admitted (or every refused) cell as its own function —
    /// a <c>@test</c> method on <c>Cells</c> for the test-host position — each call isolated so a throw
    /// names its cell. A test-host cell prints True when its assertion holds and False when xUnit's
    /// assertion exception stops it; any other throw prints THROW.
    /// </summary>
    private static (string Program, List<Cell> Cells) BuildProgram(Kind kind, bool refused)
    {
        var spec = Spec(kind);
        var functions = new List<string>();
        var methods = new List<string>();
        var calls = new StringBuilder();
        var cells = new List<Cell>();
        var lineOffsets = new List<(int Index, bool Method, int Offset)>();
        var i = 0;

        foreach (var form in Enum.GetValues<Form>())
            foreach (var spelling in Enum.GetValues<Spelling>())
                foreach (var position in Enum.GetValues<Position>())
                {
                    if (IsRefused(kind, form, spelling) != refused)
                        continue;
                    var states = refused ? new[] { spec.Present[0] } : States(kind, form);
                    foreach (var state in states)
                    {
                        var name = $"cell_{i++}";
                        var label = $"{form}|{state.Name}|{Test(spelling)}|{position}";
                        var hosted = position == Position.TestHostAssert;
                        var (body, testLine) = Body(position, Test(spelling));
                        var lines = new List<string>();
                        lines.AddRange(Binding(spec, form, state));
                        if (form is Form.NarrowedByIsNotNone or Form.NarrowedOptional)
                        {
                            lines.Add("if x is not None:");
                            testLine += lines.Count;
                            lines.AddRange(body.Select(l => "    " + l));
                            lines.Add("else:");
                            lines.Add("    raise ValueError(\"unreached: the narrowing test failed\")");
                        }
                        else
                        {
                            testLine += lines.Count;
                            lines.AddRange(body);
                        }

                        var indent = hosted ? "        " : "    ";
                        var header = hosted
                            ? $"    @test\n    def {name}(self) -> None:\n"
                            : $"def {name}() -> bool:\n";
                        var text = header + string.Join("", lines.Select(l => indent + l + "\n")) + "\n";
                        // The header occupies 1 line (2 with the decorator) before the body.
                        var offset = (hosted ? 2 : 1) + testLine;
                        (hosted ? methods : functions).Add(text);
                        lineOffsets.Add((cells.Count, hosted, offset));

                        calls.Append(hosted
                            ? $"    try:\n        Cells().{name}()\n        print(\"{label}\", True)\n"
                                + $"    except XunitException:\n        print(\"{label}\", False)\n"
                                + $"    except Exception:\n        print(\"{label}\", \"THROW\")\n"
                            : $"    run(\"{label}\", {name})\n");
                        cells.Add(new Cell(label, refused ? "" : (Truth(spelling, state) ? "True" : "False"), 0));
                    }
                }

        // Lay the program out and resolve each cell's absolute test line.
        var program = new StringBuilder(Prelude).Append(Launders(spec));
        var lineOf = new Dictionary<int, int>();
        int Lines(StringBuilder sb) => sb.ToString().Count(c => c == '\n');
        var fnIndex = 0;
        var methodIndex = 0;
        var fnStarts = new List<int>();
        foreach (var fn in functions)
        {
            fnStarts.Add(Lines(program) + 1);
            program.Append(fn);
        }
        program.Append("class Cells:\n");
        if (methods.Count == 0)
            program.Append("    pass\n");
        var methodStarts = new List<int>();
        foreach (var method in methods)
        {
            methodStarts.Add(Lines(program) + 1);
            program.Append(method);
        }
        program.Append("\n");
        foreach (var (index, method, offset) in lineOffsets)
            lineOf[index] = method ? methodStarts[methodIndex++] + offset : fnStarts[fnIndex++] + offset;

        program.Append("def run(label: str, f: () -> bool):\n    try:\n        print(label, f())\n"
            + "    except Exception:\n        print(label, \"THROW\")\n\n");
        program.Append("def main():\n").Append(calls.Length == 0 ? "    pass\n" : calls.ToString());

        return (program.ToString(), cells.Select((c, k) => c with { Line = lineOf[k] }).ToList());
    }

    public static TheoryData<Kind> Kinds()
    {
        var data = new TheoryData<Kind>();
        foreach (var kind in Enum.GetValues<Kind>())
            data.Add(kind);
        return data;
    }

    public static TheoryData<Kind> KindsWithRefusals()
    {
        var data = new TheoryData<Kind>();
        foreach (var kind in Enum.GetValues<Kind>())
            if (BuildProgram(kind, refused: true).Cells.Count > 0)
                data.Add(kind);
        return data;
    }

    private static int CellCount(bool refused) => Enum.GetValues<Kind>().Sum(k =>
        Enum.GetValues<Form>().Sum(f => Enum.GetValues<Spelling>().Count(s => IsRefused(k, f, s) == refused))
        * Enum.GetValues<Position>().Length);

    [Fact]
    public void Matrix_CoversEveryCell()
    {
        // Anchored to literals: 10 kinds × 6 forms × 8 spellings × 4 positions = 1920 cells, of which
        // value type 6 forms × 4 equality spellings × 4 positions = 96 and the un-narrowed `T?` form of the
        // other 9 kinds 9 × 4 × 4 = 144 are refusals (240). Executed rows: states per form are
        // 2/3/2/2/3/2 for the 4 destroyed+live kinds and 1/2/1/1/2/1 for the other 6 (104 kind×form×state
        // rows) × 8 spellings × 4 positions = 3328, minus the refused rows (value type 8 × 4 × 4 = 128,
        // un-narrowed `T?` (4 × 3 + 5 × 2) × 4 × 4 = 352) = 2848.
        CellCount(refused: false).Should().Be(1680);
        CellCount(refused: true).Should().Be(240);
        Enum.GetValues<Kind>().Sum(k => BuildProgram(k, refused: false).Cells.Count).Should().Be(2848);
        Enum.GetValues<Kind>().Sum(k => BuildProgram(k, refused: true).Cells.Count).Should().Be(240);
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public void AdmittedCells_ObserveTheTruthValue_InEveryPosition(Kind kind)
    {
        var (program, cells) = BuildProgram(kind, refused: false);
        var result = CompileAndExecute(program);

        result.Success.Should().BeTrue(
            $"{string.Join("; ", result.CompilationErrors)} stderr: {result.StandardError}");
        // Every diverging cell is named, not just the first.
        var actual = result.StandardOutput.Replace("\r\n", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var expected = cells.Select(c => $"{c.Label} {c.Expected}").ToArray();
        actual.Length.Should().Be(expected.Length, "every cell prints one line");
        expected.Zip(actual).Where(p => p.First != p.Second).Select(p => $"expected '{p.First}', got '{p.Second}'")
            .Should().BeEmpty();

        // Positive control: every test-host cell went through the test-host assert rewrite.
        var hosted = cells.Count(c => c.Label.EndsWith($"|{Position.TestHostAssert}", StringComparison.Ordinal));
        hosted.Should().BeGreaterThan(0);
        System.Text.RegularExpressions.Regex.Matches(result.GeneratedCSharp, @"Xunit\.Assert\.")
            .Count.Should().BeGreaterThanOrEqualTo(hosted);
    }

    [Theory]
    [MemberData(nameof(KindsWithRefusals))]
    public void RefusedCells_AreSpy0222WithTheIsNoneSteer_AtTheirOwnLine(Kind kind)
    {
        var (program, cells) = BuildProgram(kind, refused: true);
        var result = CompileAndExecute(program);

        result.Success.Should().BeFalse();
        var errors = result.RawDiagnostics.Where(d => d.IsError).ToList();
        foreach (var cell in cells)
        {
            var steer = Steer(Enum.GetValues<Spelling>().Single(s => cell.Label.Contains($"|{Test(s)}|")));
            errors.Should().Contain(d => d.Line == cell.Line
                && d.Code == DiagnosticCodes.Semantic.InvalidBinaryOperation && d.Message.Contains(steer),
                $"cell {cell.Label} at line {cell.Line}");
        }
        errors.Should().OnlyContain(d => d.Code == DiagnosticCodes.Semantic.InvalidBinaryOperation,
            "a refusal is SPY0222 and nothing else");
    }

    /// <summary>The acceptance cell (#2238): a destroyed object, narrowed, answers <c>== None</c> → True
    /// (its operator runs) and <c>is None</c> → False (the reference check), and a comparison-chain link
    /// agrees with the binary form — including a None shared by two links over a two-overload type.</summary>
    [Fact]
    public void DestroyedObject_Narrowed_EqualsNone_ButIsNotNone()
    {
        var result = CompileAndExecute(Prelude + Launders(Spec(Kind.ClrFakeNull)) + """
            def main() -> None:
                x: Handle | None = None
                x = Handle(True)
                print(x == None, x is None, None != x, x is not None)
                y = launder_handle(Handle(True))
                if y is not None:
                    print(y == None, y is None)
                v: Version = Version(1, 2)
                s: StringBuilder = StringBuilder()
                print(v == None == s, v != None != s)
                a: Amb = Amb(True)
                b: Amb = Amb(True)
                c: Amb = Amb(False)
                print(a == None == b, a == None == c, None != c != None)
            """);

        result.Success.Should().BeTrue(
            $"{string.Join("; ", result.CompilationErrors)} stderr: {result.StandardError}");
        result.StandardOutput.Replace("\r\n", "\n").Should().Be(
            "True False False True\nTrue False\nFalse True\nTrue False True\n");
    }
}
