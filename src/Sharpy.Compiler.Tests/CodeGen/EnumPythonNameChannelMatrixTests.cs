using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// An int enum member's python spelling (#2007, #2069): <c>str</c> is <c>Color.red</c>, <c>repr</c>
/// <c>&lt;Color.red: 2&gt;</c>, <c>.name</c> <c>red</c> — every System.Enum, through ONE Core channel
/// (<c>Builtins.EnumName</c> reading the field's <c>[SharpyFieldName]</c>, which the emitter stamps
/// only when the emitted C# member name differs from the declared one).
///
/// <para><b>Cells.</b> member spelling {SCREAMING <c>RED</c>, lower <c>red</c>, snake
/// <c>dark_blue</c>, camel <c>myGreen</c>, escaped <c>`plain`</c>, negative-valued <c>NEG</c>} ×
/// route {<c>.name</c>, <c>str</c>, <c>repr</c>, f-string pad, list element, <c>.value</c>}; plus a
/// nested enum, dict/tuple elements, a CLR interop enum (Sharpy-only by the owner ruling "all"), the
/// string-enum controls (<c>str</c>/<c>.name</c>/<c>.value</c> unchanged — StrEnum), and the
/// attribute-when-different stamp read off the emitted C#. Every literal below is python3's
/// (3.12, the same enum as <c>class Color(Enum)</c>).</para>
///
/// <para><b>Direction (prior commit's binary).</b> <c>str</c>/<c>print</c>/f-string printed the CLR
/// member name (<c>RED</c>, <c>Red</c>, <c>DarkBlue</c>, <c>Mygreen</c>); <c>repr</c> and a list
/// element the same; <c>.name</c> the CLR name for every unescaped non-SCREAMING member (#2069).</para>
/// </summary>
[Collection("HeavyCompilation")]
public class EnumPythonNameChannelMatrixTests : IntegrationTestBase
{
    public EnumPythonNameChannelMatrixTests(ITestOutputHelper output) : base(output) { }

    private const string ColorEnum =
        "enum Color:\n    RED = 1\n    red = 2\n    dark_blue = 3\n    myGreen = 4\n    `plain` = 5\n    NEG = -7\n\n";

    /// <summary>(member spelling in source, python3's name | str | repr | f"[{m:&gt;14}]" | [m] | value).</summary>
    public static IEnumerable<object[]> Members() => new[]
    {
        new object[] { "RED", "RED", "Color.RED", "<Color.RED: 1>", "[     Color.RED]", "[<Color.RED: 1>]", "1" },
        new object[] { "red", "red", "Color.red", "<Color.red: 2>", "[     Color.red]", "[<Color.red: 2>]", "2" },
        new object[] { "dark_blue", "dark_blue", "Color.dark_blue", "<Color.dark_blue: 3>", "[Color.dark_blue]", "[<Color.dark_blue: 3>]", "3" },
        new object[] { "myGreen", "myGreen", "Color.myGreen", "<Color.myGreen: 4>", "[ Color.myGreen]", "[<Color.myGreen: 4>]", "4" },
        new object[] { "plain", "plain", "Color.plain", "<Color.plain: 5>", "[   Color.plain]", "[<Color.plain: 5>]", "5" },
        new object[] { "NEG", "NEG", "Color.NEG", "<Color.NEG: -7>", "[     Color.NEG]", "[<Color.NEG: -7>]", "-7" },
    };

    [Theory]
    [MemberData(nameof(Members))]
    public void IntEnumMember_EveryRoute_IsPythons(
        string member, string name, string str, string repr, string padded, string listed, string value)
    {
        var source = ColorEnum + "def main() -> None:\n"
            + $"    m: Color = Color.{member}\n"
            + "    print(m.name)\n    print(str(m))\n    print(repr(m))\n    print(f\"[{m:>14}]\")\n"
            + "    print([m])\n    print(m.value)\n    print(m)\n";
        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue($"[{member}] {string.Join(" | ", result.CompilationErrors)}\n{source}");
        result.StandardOutput.Should().Be(string.Join("\n", name, str, repr, padded, listed, value, str) + "\n",
            $"[{member}]\n{source}");
    }

    [Fact]
    public void NestedEnum_DictAndTupleElements_ArePythons()
    {
        // python3: str(Outer.Inner.Blue) -> 'Inner.Blue' (the class name, not the qualname),
        // repr -> '<Inner.Blue: 1>', .name -> 'Blue'; {Color.RED: 1} -> '{<Color.RED: 1>: 1}';
        // (Color.RED, Color.red) -> '(<Color.RED: 1>, <Color.red: 2>)'. The `.name` read goes
        // through an annotated local: an UNannotated nested-member chain is untyped (#2082).
        var source = ColorEnum
            + "class Outer:\n    enum Inner:\n        Blue = 1\n\n"
            + "def main() -> None:\n"
            + "    print(Outer.Inner.Blue)\n    print(repr(Outer.Inner.Blue))\n"
            + "    inner: Outer.Inner = Outer.Inner.Blue\n    print(inner.name)\n"
            + "    d: dict[Color, int] = {Color.RED: 1}\n    print(d)\n"
            + "    t: tuple[Color, Color] = (Color.RED, Color.red)\n    print(t)\n";
        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue(string.Join(" | ", result.CompilationErrors));
        result.StandardOutput.Should().Be(
            "Inner.Blue\n<Inner.Blue: 1>\nBlue\n{<Color.RED: 1>: 1}\n(<Color.RED: 1>, <Color.red: 2>)\n");
    }

    [Fact]
    public void ClrInteropEnum_TakesThePythonSpelling()
    {
        // Sharpy-only (no python twin), by the owner ruling "all": every System.Enum. (`.name`/`.value`
        // on a CLR enum are refused by the checker today — #2083, not this channel.)
        var source = "from System import DayOfWeek\n\n"
            + "def main() -> None:\n    d: DayOfWeek = DayOfWeek.Monday\n"
            + "    print(d)\n    print(repr(d))\n    print([d])\n";
        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue(string.Join(" | ", result.CompilationErrors));
        result.StandardOutput.Should().Be("DayOfWeek.Monday\n<DayOfWeek.Monday: 1>\n[<DayOfWeek.Monday: 1>]\n");
    }

    [Fact]
    public void StringEnum_StrNameValue_AreUnchanged()
    {
        // Control (enums.md: a string enum behaves like CPython's StrEnum): str is the value.
        // python3 (class Mood(StrEnum)): str(Mood.happy) -> 'h', .name -> 'happy', .value -> 'h'.
        var source = "enum Mood:\n    happy = \"h\"\n    SAD = \"s\"\n\n"
            + "def main() -> None:\n    print(Mood.happy)\n    print(str(Mood.SAD))\n"
            + "    print(Mood.happy.name)\n    print(Mood.SAD.value)\n";
        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue(string.Join(" | ", result.CompilationErrors));
        result.StandardOutput.Should().Be("h\ns\nhappy\ns\n");
    }

    // ── Known residuals, pinned to their CURRENT refusal so each fix is a visible direction change ──
    // Neither pin asserts a value: each asserts the refusal that stands today and goes RED when its
    // issue is fixed — then delete the pin and move the cell into the matrices above (drain on fix).

    [Fact]
    public void KnownResidual_2082_UnannotatedNestedEnumMemberChain_IsUntyped()
    {
        // #2082: `Outer.Inner.Blue` read through its chain is Unknown in the checker, so no
        // `.name` lowering is recorded and the emitter spells a C# member `Name` (CS1061 behind
        // SPY0908). python3 prints 'Blue'. Prior commit: the same CS1061.
        var source = "class Outer:\n    enum Inner:\n        Blue = 1\n\n"
            + "def main() -> None:\n    print(Outer.Inner.Blue.name)\n";
        var result = CompileAndExecute(source);

        result.Success.Should().BeFalse("#2082 still open? delete this pin and add the cell to the matrix");
        result.RawDiagnostics.Should().Contain(d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError
            && d.Message.Contains("CS1061"), string.Join(" | ", result.CompilationErrors));
    }

    [Fact]
    public void KnownResidual_2083_NameOnAClrEnum_IsRefused()
    {
        // #2083: `.name`/`.value` on a CLR interop enum are SPY0203 (the checker's enum arm keys on a
        // source enum). Prior commit: the same SPY0203.
        var source = "from System import DayOfWeek\n\n"
            + "def main() -> None:\n    d: DayOfWeek = DayOfWeek.Monday\n    print(d.name)\n";
        var result = CompileAndExecute(source);

        result.Success.Should().BeFalse("#2083 still open? delete this pin and add the cell to ClrInteropEnum_…");
        result.RawDiagnostics.Should().Contain(d => d.Code == DiagnosticCodes.Semantic.UndefinedMember
            && d.Message == "Type 'DayOfWeek' has no member 'name'", string.Join(" | ", result.CompilationErrors));
    }

    [Fact]
    public void SharpyFieldName_IsStampedOnlyWhereTheEmittedNameDiffers()
    {
        // Attribute-when-different (#1607): RED, `plain` and NEG emit their declared spelling and carry
        // no attribute; red → Red, dark_blue → DarkBlue, myGreen → Mygreen carry it.
        var result = CompileAndExecute(ColorEnum + "def main() -> None:\n    print(Color.RED)\n");

        result.Success.Should().BeTrue(string.Join(" | ", result.CompilationErrors));
        var members = CSharpSyntaxTree.ParseText(result.GeneratedCSharp!).GetRoot()
            .DescendantNodes().OfType<EnumDeclarationSyntax>().Single(e => e.Identifier.Text == "Color")
            .Members;
        var stamped = members
            .Where(m => m.AttributeLists.SelectMany(l => l.Attributes).Any(a => a.Name.ToString().EndsWith("SharpyFieldName", StringComparison.Ordinal)))
            .ToDictionary(
                m => m.Identifier.ValueText,
                m => m.AttributeLists.SelectMany(l => l.Attributes).Single().ArgumentList!.Arguments.Single().ToString());
        stamped.Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["Red"] = "\"red\"",
            ["DarkBlue"] = "\"dark_blue\"",
            ["Mygreen"] = "\"myGreen\"",
        });
        members.Should().HaveCount(6, "the positive control: all six members were emitted and read");
    }
}
