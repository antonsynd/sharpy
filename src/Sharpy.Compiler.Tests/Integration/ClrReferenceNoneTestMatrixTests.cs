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
/// <c>T | None</c>, narrowed by assignment, narrowed by <c>is not None</c> — and runs the type's
/// <c>operator ==</c> (the native <c>x == null</c>), so a destroyed "fake null" object answers
/// <c>== None</c> → True. <c>is None</c> stays the reference check that never runs user code (#2224,
/// ruling R-EJ): the same destroyed object answers <c>is None</c> → False. Sharpy classes, value types and
/// the Optional family are controls whose verdicts the lift does not touch.
/// </summary>
/// <remarks>
/// <para>Axes: kind {CLR reference with a fake-null <c>==</c> (the destroyed-object <c>Handle</c> of
/// <see cref="IsNoneReferenceCheckMatrixTests"/>), CLR reference with a plain overloaded <c>==</c>
/// (<c>System.Version</c>), CLR reference without one (<c>StringBuilder</c>), Sharpy class with a
/// well-behaved <c>__eq__(object)</c>, with a permissive (always-True) one — the Sharpy analogue of the fake
/// null —, without <c>__eq__</c>, value type <c>int</c>, Optional <c>Handle?</c>}
/// × form {declared <c>T</c>, <c>T | None</c> un-narrowed, narrowed by assignment, narrowed by
/// <c>is not None</c>} × spelling {<c>x == None</c>, <c>x != None</c>, <c>None == x</c>, <c>None != x</c>, and
/// the <c>is</c> twins <c>x is None</c>, <c>x is not None</c>, <c>None is x</c>, <c>None is not x</c>} ×
/// position {<c>return</c>, <c>if</c>, <c>assert</c>, <c>assert</c> under the <c>@test</c> host}.</para>
/// <para>The verdict table (<see cref="IsRefused"/>) and the truth table (<see cref="Truth"/>) are spelled
/// by hand. Every admitted cell is EXECUTED for every subject state its form can hold (a destroyed and a
/// live <c>Handle</c>; absent only for the un-narrowed form); the truth is python3's for the Sharpy
/// controls and the operator's answer for the CLR kinds. An assert cell observes both directions (holds →
/// True, violated → the assertion error, never another throw). Every refused cell must report SPY0222 with
/// the <c>is None</c> steer at its own line.</para>
/// </remarks>
[Collection("HeavyCompilation")]
public class ClrReferenceNoneTestMatrixTests : IntegrationTestBase
{
    public ClrReferenceNoneTestMatrixTests(ITestOutputHelper output) : base(output)
    {
    }

    protected override IEnumerable<string> GetAdditionalReferenceAssemblyPaths()
        => base.GetAdditionalReferenceAssemblyPaths()
            .Append(typeof(Xunit.Assert).Assembly.Location)
            .Append(IsNoneReferenceCheckMatrixTests.FakeNullAssembly.Value);

    public enum Kind
    {
        ClrFakeNull, ClrOverloadedEq, ClrPlainReference, SharpyEq, SharpyPermissiveEq, SharpyNoEq, ValueType, Optional,
    }

    public enum Form { Declared, Unnarrowed, NarrowedByAssignment, NarrowedByIsNotNone }

    public enum Spelling { EqNone, NotEqNone, NoneEq, NoneNotEq, IsNone, IsNotNone, NoneIs, NoneIsNot }

    public enum Position { Return, If, Assert, TestHostAssert }

    private const string Prelude = """
        from system import Version
        from system.text import StringBuilder
        from spyfakenull import Handle
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

        def launder_handle(v: Handle | None) -> Handle | None:
            return v

        def launder_version(v: Version | None) -> Version | None:
            return v

        def launder_sb(v: StringBuilder | None) -> StringBuilder | None:
            return v

        def launder_w(v: W | None) -> W | None:
            return v

        def launder_p(v: P | None) -> P | None:
            return v

        def launder_n(v: N | None) -> N | None:
            return v

        def launder_int(v: int | None) -> int | None:
            return v

        def launder_opt(v: Handle?) -> Handle?:
            return v


        """;

    /// <summary>One subject state: the value expression, whether it is absent, and whether the type's
    /// <c>==</c> calls it equal to None while it is present (the destroyed <c>Handle</c>, and <c>P</c> whose
    /// <c>__eq__</c> says True to everything — python3: <c>P(2) == None</c> and <c>None == P(2)</c> are True).</summary>
    private sealed record State(string Name, string Value, bool Absent, bool FakeNull);

    /// <summary>The declared type, the <c>T | None</c> spelling (the Optional family has none — it stays
    /// <c>T?</c>), the absent value, the launder function, and the present states.</summary>
    private sealed record KindSpec(string Type, string NullableType, string AbsentValue, string Launder, State[] Present);

    private static KindSpec Spec(Kind kind) => kind switch
    {
        Kind.ClrFakeNull => new("Handle", "Handle | None", "None", "launder_handle", new[]
        {
            new State("destroyed", "Handle(True)", false, true), new State("live", "Handle(False)", false, false),
        }),
        Kind.ClrOverloadedEq => new("Version", "Version | None", "None", "launder_version",
            new[] { new State("value", "Version(1, 2)", false, false) }),
        Kind.ClrPlainReference => new("StringBuilder", "StringBuilder | None", "None", "launder_sb",
            new[] { new State("value", "StringBuilder()", false, false) }),
        Kind.SharpyEq => new("W", "W | None", "None", "launder_w", new[] { new State("value", "W(2)", false, false) }),
        Kind.SharpyPermissiveEq => new("P", "P | None", "None", "launder_p",
            new[] { new State("value", "P(2)", false, true) }),
        Kind.SharpyNoEq => new("N", "N | None", "None", "launder_n", new[] { new State("value", "N(2)", false, false) }),
        Kind.ValueType => new("int", "int | None", "None", "launder_int", new[] { new State("value", "0", false, false) }),
        Kind.Optional => new("Handle?", "Handle?", "None()", "launder_opt",
            new[] { new State("destroyed", "Some(Handle(True))", false, true) }),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>Subject states per form: only the un-narrowed form can hold None.</summary>
    private static State[] States(Kind kind, Form form)
    {
        var spec = Spec(kind);
        return form == Form.Unnarrowed
            ? spec.Present.Append(new State("absent", spec.AbsentValue, true, false)).ToArray()
            : spec.Present;
    }

    private static bool IsEquality(Spelling spelling) => spelling <= Spelling.NoneNotEq;

    /// <summary>
    /// Hand-spelled verdicts. Every CLR-reference and Sharpy-class cell is admitted (R-ES). The equality
    /// spellings stay refused on a value type (statically always-False, SPY0222 — `int | None` included)
    /// and on the Optional family, which must be narrowed first: an Optional narrowed by `is not None` IS
    /// the bare CLR reference, so it takes the R-ES rule; narrowing by assignment does not narrow `T?`.
    /// </summary>
    private static bool IsRefused(Kind kind, Form form, Spelling spelling)
        => IsEquality(spelling)
            && (kind == Kind.ValueType || (kind == Kind.Optional && form != Form.NarrowedByIsNotNone));

    /// <summary>The truth value: <c>is</c> asks absence; <c>==</c> asks the type's operator, which for the
    /// fake-null <c>Handle</c> also says a destroyed object equals None (python3 for the Sharpy classes).</summary>
    private static bool Truth(Spelling spelling, State state)
    {
        var equalsNone = IsEquality(spelling) ? state.Absent || state.FakeNull : state.Absent;
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

    /// <summary>The lines binding <c>x</c> for a form and state.</summary>
    private static string[] Binding(KindSpec spec, Form form, State state) => form switch
    {
        Form.Declared => new[] { $"x: {spec.Type} = {state.Value}" },
        Form.Unnarrowed or Form.NarrowedByIsNotNone => new[] { $"x: {spec.NullableType} = {spec.Launder}({state.Value})" },
        Form.NarrowedByAssignment => new[] { $"x: {spec.NullableType} = {spec.AbsentValue}", $"x = {state.Value}" },
        _ => throw new ArgumentOutOfRangeException(nameof(form)),
    };

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
                        if (form == Form.NarrowedByIsNotNone)
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
        var program = new StringBuilder(Prelude);
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
        // Anchored to literals: 8 kinds × 4 forms × 8 spellings × 4 positions = 1024 cells, of which
        // value type 4 forms × 4 equality spellings × 4 positions = 64 and Optional 3 forms × 4 × 4 = 48
        // are refusals (112). Executed rows: states per form are 2/3/2/2 for Handle and 1/2/1/1 otherwise
        // (44 kind×form×state rows) × 8 spellings × 4 positions = 1408, minus the refused rows
        // (value type 5 × 4 × 4 = 80, Optional 4 × 4 × 4 = 64) = 1264.
        CellCount(refused: false).Should().Be(912);
        CellCount(refused: true).Should().Be(112);
        Enum.GetValues<Kind>().Sum(k => BuildProgram(k, refused: false).Cells.Count).Should().Be(1264);
        Enum.GetValues<Kind>().Sum(k => BuildProgram(k, refused: true).Cells.Count).Should().Be(112);
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
    /// agrees with the binary form.</summary>
    [Fact]
    public void DestroyedObject_Narrowed_EqualsNone_ButIsNotNone()
    {
        var result = CompileAndExecute(Prelude + """
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
            """);

        result.Success.Should().BeTrue(
            $"{string.Join("; ", result.CompilationErrors)} stderr: {result.StandardError}");
        result.StandardOutput.Replace("\r\n", "\n").Should().Be("True False False True\nTrue False\nFalse True\n");
    }
}
