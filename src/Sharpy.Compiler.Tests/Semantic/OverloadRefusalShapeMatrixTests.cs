using System.Text.RegularExpressions;
using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// #1775: an overload set that rejects a call on TYPES names the argument, exactly as the
/// single-signature twin does. When every arity-surviving candidate rejects the SAME argument for a
/// type reason, the refusal is the store seam's SPY0220 at that argument — with its steer — instead
/// of SPY0354 "No matching overload", which names nothing at all.
///
/// <para><b>Contract.</b> The refusal shape is a property of the ARGUMENT, not of how many
/// signatures the member happens to have: <c>xs.index(n)</c> and <c>xs.count(n)</c> report the same
/// thing for the same mistyped needle. SPY0354 survives exactly where there is something to choose
/// between — an arity mismatch, or candidates that fail at DIFFERENT indices.</para>
///
/// <para><b>Axes.</b> member (14) x needle shape (5) x route (2) = 140 cells. Every cell is either
/// live (compiled, refused, and the diagnostic names the argument's own type) or declared
/// <see cref="NotApplicable"/> with a reason. The route axis is what makes the theory a class
/// statement rather than a member list: a Sharpy Core member and its CLR twin answer the same
/// mistyped needle the same way, or the CLR cell cites the issue that says why not.</para>
///
/// <para><b>Mutation.</b> Restore the <c>arityCandidates.Count &lt; 2</c> guard in
/// <c>TryReportSameArgumentRefusal</c>'s callers (or make the helper return false) -> every live
/// Sharpy-core cell goes RED. <c>DivergentCandidates_KeepsSPY0354</c> is the discriminating positive
/// control: it stays green under that mutation, so the guard cannot pass by refusing everything.</para>
/// </summary>
[Collection("HeavyCompilation")]
public class OverloadRefusalShapeMatrixTests : IntegrationTestBase
{
    public OverloadRefusalShapeMatrixTests(ITestOutputHelper output) : base(output) { }

    private const string SharpyCore = "core";
    private const string ClrMember = "clr";

    /// <summary>The routes a member call can take to the argument seam.</summary>
    private static readonly string[] Routes = { SharpyCore, ClrMember };

    /// <summary>
    /// A member's call site on one route: the statements that build the receiver, and the call with
    /// <c>{n}</c> standing for the needle. <see cref="NotApplicable"/> is the reason this route has
    /// no cell for the member.
    ///
    /// <para><c>ElementSlot</c> marks a position whose expected type is the RECEIVER'S ELEMENT type,
    /// so a collection argument is admitted member by member (R-W arm 1, #1783) and a mistyped one
    /// is refused naming the member that does not fit rather than the whole argument. Measured, not
    /// assumed, and a property of the SITE rather than of the <see cref="SlotFamily"/>:
    /// <c>dict.update</c>'s parameter is a whole <c>dict</c>, and every CLR twin binds
    /// <c>IEnumerable[T]</c> through the CLR route, which arm 1 never reaches — all of them
    /// Iterable-family sites that name the whole tuple.</para>
    /// </summary>
    private sealed record Site(
        string Prelude, string Call, string Import = "", string? NotApplicable = null,
        bool ElementSlot = false)
    {
        public static Site Na(string reason) => new("", "", NotApplicable: reason);
    }

    /// <summary>
    /// The slot family a member's needle lands in. Only a NUMERIC slot has a range, so the
    /// out-of-range-constant shape is N/A everywhere else.
    /// </summary>
    private enum SlotFamily { Numeric, Str, Iterable }

    private sealed record MemberRow(string Name, SlotFamily Family, Site Core, Site Clr);

    private const string Na1798 =
        "#1798: the CLR member's parameter is the receiver's open type parameter (or a `char`), "
        + "which the argument seam declines to adjudicate — the call reaches Roslyn as CS1503";

    /// <summary>The 14 members whose overload sets #1775 names, with their CLR twins.</summary>
    private static readonly MemberRow[] Members =
    {
        new("list.index", SlotFamily.Numeric,
            new("xs: list[uint8] = [1]", "print(xs.index({n}))"),
            Site.Na(Na1798)),
        new("list.insert", SlotFamily.Numeric,
            new("xs: list[uint8] = [1]", "xs.insert(0, {n})"),
            new("xs = List[uint8]()\n    xs.add(1)", "xs.insert(0, {n})",
                Import: "from system.collections.generic import List")),
        new("str.find", SlotFamily.Str,
            new("s: str = \"abc\"", "print(s.find({n}))"), Site.Na(Na1798)),
        new("str.rfind", SlotFamily.Str,
            new("s: str = \"abc\"", "print(s.rfind({n}))"), Site.Na(Na1798)),
        new("str.index", SlotFamily.Str,
            new("s: str = \"abc\"", "print(s.index({n}))"), Site.Na(Na1798)),
        new("str.count", SlotFamily.Str,
            new("s: str = \"abc\"", "print(s.count({n}))"), Site.Na(Na1798)),
        new("str.startswith", SlotFamily.Str,
            new("s: str = \"abc\"", "print(s.startswith({n}))"), Site.Na(Na1798)),
        new("dict.get", SlotFamily.Numeric,
            new("d: dict[uint8, str] = {1: \"a\"}", "print(d.get({n}))"),
            new("d = Dictionary[uint8, str]()", "print(d.contains_key({n}))",
                Import: "from system.collections.generic import Dictionary")),
        new("dict.pop", SlotFamily.Numeric,
            new("d: dict[uint8, str] = {1: \"a\"}", "print(d.pop({n}))"),
            Site.Na(Na1798)),
        new("set.update", SlotFamily.Iterable,
            new("st: set[uint8] = {1}", "st.update({n})", ElementSlot: true),
            new("st = HashSet[uint8]()", "st.union_with({n})",
                Import: "from system.collections.generic import HashSet")),
        new("set.intersection_update", SlotFamily.Iterable,
            new("st: set[uint8] = {1}", "st.intersection_update({n})", ElementSlot: true),
            new("st = HashSet[uint8]()", "st.intersect_with({n})",
                Import: "from system.collections.generic import HashSet")),
        new("set.difference_update", SlotFamily.Iterable,
            new("st: set[uint8] = {1}", "st.difference_update({n})", ElementSlot: true),
            new("st = HashSet[uint8]()", "st.except_with({n})",
                Import: "from system.collections.generic import HashSet")),
        new("set.symmetric_difference_update", SlotFamily.Iterable,
            new("st: set[uint8] = {1}", "st.symmetric_difference_update({n})", ElementSlot: true),
            new("st = HashSet[uint8]()", "st.symmetric_except_with({n})",
                Import: "from system.collections.generic import HashSet")),
        new("dict.update", SlotFamily.Iterable,
            new("d: dict[uint8, str] = {1: \"a\"}", "d.update({n})"),
            Site.Na("no CLR member merges a whole dictionary in one call — the shape has no twin")),
    };

    /// <summary>
    /// The five needle shapes. <see cref="ArgumentTypeDisplay"/> is what the refusal must name: the
    /// argument's OWN type, which SPY0354 never printed.
    /// </summary>
    private sealed record Shape(string Name, string Extra, string Needle, bool NumericSlotOnly = false)
    {
        public string ArgumentTypeDisplay(SlotFamily family, bool elementSlot) => Name switch
        {
            "mistyped-var" => family == SlotFamily.Numeric ? "'str'" : "'bool'",
            "out-of-range-const" => "'int32'",
            "optional" => "'uint8?'",
            "nullable" => "'uint8 | None'",
            // mistyped-tuple, split by SITE because the compiler's answer is. Measured at
            // 9e41f12c3 over all 20 live cells:
            //
            //   element slot  st.update / intersection_update / difference_update /
            //                 symmetric_difference_update, all core:
            //                 "Cannot pass argument of type 'str' to parameter of type 'uint8'"
            //   everywhere    list.index/insert, dict.get/pop, the five str members, dict.update,
            //                 and every CLR twin:
            //                 "... of type 'tuple[str, str]' to parameter of type 'uint8'|'str'|…"
            //
            // The receiver's element type is the slot each tuple member is admitted into at the
            // four element-slot sites (R-W arm 1, #1783), so the refusal lands on the member that
            // does not fit; a scalar slot has no members to admit and names the whole argument, as
            // it did before that work. Refusal to refusal at every cell — nothing was widened into
            // acceptance, and nothing that ran is refused.
            _ => elementSlot ? "'str'" : "'tuple[str, str]'",
        };

        /// <summary>The wrong-typed variable is chosen against the slot: a `str` is wrong for a
        /// numeric slot and right for a `str` one.</summary>
        public string ExtraFor(SlotFamily family) => Name == "mistyped-var"
            ? (family == SlotFamily.Numeric ? "bad: str = \"z\"" : "bad: bool = True")
            : Extra;
    }

    private static readonly Shape[] Shapes =
    {
        new("mistyped-var", "", "bad"),
        new("out-of-range-const", "", "300", NumericSlotOnly: true),
        new("optional", "o: uint8? = Some(1)", "o"),
        new("nullable", "nb: uint8 | None = 1", "nb"),
        new("mistyped-tuple", "", "(\"a\", \"b\")"),
    };

    public static IEnumerable<object[]> Cells()
    {
        foreach (var member in Members)
            foreach (var shape in Shapes)
                foreach (var route in Routes)
                    yield return new object[] { member.Name, shape.Name, route };
    }

    private static MemberRow Row(string member) => Members.Single(m => m.Name == member);

    private static Shape ShapeOf(string shape) => Shapes.Single(s => s.Name == shape);

    /// <summary>The reason a cell is N/A, or null when it is live.</summary>
    private static string? NotApplicableReason(MemberRow member, Shape shape, string route)
    {
        if (shape.NumericSlotOnly && member.Family != SlotFamily.Numeric)
            return "out of range is a question only a numeric slot has";
        return (route == SharpyCore ? member.Core : member.Clr).NotApplicable;
    }

    /// <summary>
    /// Every live cell: the call is refused at SEMANTIC time with SPY0220, and the message names the
    /// argument's own type. A cell that regressed to SPY0354 names nothing; one that regressed to
    /// SPY0908 reached Roslyn.
    /// </summary>
    [Theory]
    [MemberData(nameof(Cells))]
    public void MistypedNeedle_ReportsTheArgumentsOwnTypeMismatch(string member, string shape, string route)
    {
        var row = Row(member);
        var needleShape = ShapeOf(shape);
        if (NotApplicableReason(row, needleShape, route) != null)
            return;

        var site = route == SharpyCore ? row.Core : row.Clr;
        var extra = needleShape.ExtraFor(row.Family);
        var source = (site.Import.Length > 0 ? site.Import + "\n\n" : "")
            + "def main() -> None:\n    " + site.Prelude + "\n"
            + (extra.Length > 0 ? "    " + extra + "\n" : "")
            + "    " + site.Call.Replace("{n}", needleShape.Needle) + "\n";

        var result = CompileAndExecute(source);
        var cell = $"{member} x {shape} x {route}";

        result.Success.Should().BeFalse($"cell '{cell}' must be refused");
        result.RawDiagnostics.Should().Contain(
            d => d.Code == DiagnosticCodes.Semantic.TypeMismatch,
            $"cell '{cell}' reports the argument's own SPY0220, not SPY0354. Diagnostics: "
            + string.Join(" | ", result.RawDiagnostics.Select(d => $"{d.Code}: {d.Message}")));

        // Assert against the ARGUMENT half of the sentence only. Scanning the whole message lets a
        // cell pass on the PARAMETER's type instead: with a `'str'` expectation the five str.*
        // members matched `to parameter of type 'str'` while their argument half said
        // `'tuple[str, str]'` — green for the opposite of what this test exists to check. The two
        // message shapes are the core route's "Cannot pass argument of type 'X' to parameter of
        // type 'Y'" and the CLR route's "Argument N of 'M' expects 'Y' but got 'X'".
        var arguments = result.RawDiagnostics
            .Where(d => d.Code == DiagnosticCodes.Semantic.TypeMismatch)
            .Select(d => ArgumentTypeIn(d.Message))
            .Where(t => t != null)
            .ToList();

        arguments.Should().NotBeEmpty(
            $"cell '{cell}' must report a SPY0220 whose argument type this test can read; got "
            + string.Join(" | ", result.RawDiagnostics.Select(d => $"{d.Code}: {d.Message}")));

        // Substring WITHIN the argument half, not equality: the four other shapes were green under
        // a whole-message scan and must stay green, and only the half the match is allowed to look
        // at needed narrowing.
        var expected = needleShape.ArgumentTypeDisplay(row.Family, site.ElementSlot);
        arguments.Should().Contain(t => t!.Contains(expected, System.StringComparison.Ordinal),
            $"cell '{cell}' names the ARGUMENT's own type ({expected}); argument types reported "
            + "were " + string.Join(" | ", arguments));
    }

    /// <summary>
    /// The argument's type as a quoted display, pulled out of either refusal shape, or <c>null</c>
    /// when the message is neither.
    /// </summary>
    private static string? ArgumentTypeIn(string message)
    {
        var core = Regex.Match(message, @"Cannot pass argument of type ('[^']*')");
        if (core.Success)
            return core.Groups[1].Value;

        var clr = Regex.Match(message, @"but got ('[^']*')");
        return clr.Success ? clr.Groups[1].Value : null;
    }

    /// <summary>
    /// Totality. The axis sizes are literals, not counts derived from the same arrays that build the
    /// cells, so deleting a row changes a number the test states rather than one it recomputes.
    /// </summary>
    [Fact]
    public void TotalityAnchors_AxisSizesAndCellCounts()
    {
        Members.Length.Should().Be(14, "the #1775 member roster");
        Shapes.Length.Should().Be(5, "needle shapes");
        Routes.Length.Should().Be(2, "route: Sharpy Core member, CLR member");
        Cells().Count().Should().Be(140, "14 x 5 x 2");

        var notApplicable = 0;
        foreach (var member in Members)
            foreach (var shape in Shapes)
                foreach (var route in Routes)
                    if (NotApplicableReason(member, shape, route) != null)
                        notApplicable++;

        notApplicable.Should().Be(54,
            "10 non-numeric-slot members x out-of-range x 2 routes = 20; #1798 CLR cells "
            + "(list.index 5, str.* 20, dict.pop 5) = 30; dict.update's twinless CLR route = 4");
        (140 - notApplicable).Should().Be(86, "live cells");
    }

    // ── #1775's own repro, and its single-signature twin ─────────────────────────────────────

    /// <summary>
    /// The issue's headline cell. <c>list.index</c> has three overloads and only one survives arity,
    /// which is exactly the shape the retired <c>arityCandidates.Count &lt; 2</c> guard exempted.
    /// </summary>
    [Fact]
    public void ListIndex_WrongType_ReportsSPY0220()
    {
        var source = "def main() -> None:\n    xs: list[uint64] = [1, 2, 3]\n    n: int32 = 2\n    print(xs.index(n))\n";
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse();
        result.RawDiagnostics.Should().Contain(
            d => d.Code == DiagnosticCodes.Semantic.TypeMismatch,
            "a mistyped needle at an overloaded member reports the argument, not SPY0354");
        var errors = string.Join(" ", result.CompilationErrors);
        errors.Should().Contain("'int32'").And.Contain("'uint64'",
            "the message names the needle's type and the slot it must be converted to");
    }

    /// <summary>
    /// The single-signature twin. #1775 IS the asymmetry between these two, so they are asserted
    /// against the same expected substring.
    /// </summary>
    [Fact]
    public void ListCount_WrongType_StaysSPY0220()
    {
        var source = "def main() -> None:\n    xs: list[uint64] = [1, 2, 3]\n    n: int32 = 2\n    print(xs.count(n))\n";
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse();
        var errors = string.Join(" ", result.CompilationErrors);
        errors.Should().Contain("'int32'").And.Contain("'uint64'");
    }

    // ── SPY0354 survives where there is something to choose between ──────────────────────────

    [Fact]
    public void ArityMismatch_KeepsSPY0354()
    {
        // Calling with the wrong NUMBER of args: no arity candidate survives.
        var source = "class C:\n    def __init__(self):\n        pass\n    def f(self, x: int, y: int) -> int:\n        return x + y\n    def f(self, x: int, y: int, z: int) -> int:\n        return x + y + z\n\ndef main() -> None:\n    c: C = C()\n    print(c.f(1))\n";
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse();
        result.RawDiagnostics.Should().Contain(
            d => d.Code == DiagnosticCodes.Semantic.NoMatchingOverload,
            "arity mismatch keeps SPY0354");
    }

    /// <summary>
    /// The discriminating positive control: candidates that fail at DIFFERENT indices. This is what
    /// stops the same-argument rule from being "always report the first argument" — and it stays
    /// green under the mutation that reddens every other cell in this class.
    /// </summary>
    [Fact]
    public void DivergentCandidates_KeepsSPY0354()
    {
        // f(int, str) fails at index 0; f(str, int) passes index 0 and fails at index 1.
        var source = "class C:\n    def __init__(self):\n        pass\n    def f(self, x: int, y: str) -> str:\n        return str(x) + y\n    def f(self, x: str, y: int) -> str:\n        return x + str(y)\n\ndef main() -> None:\n    c: C = C()\n    print(c.f(\"a\", \"b\"))\n";
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse();
        result.RawDiagnostics.Should().Contain(
            d => d.Code == DiagnosticCodes.Semantic.NoMatchingOverload,
            "divergent candidates keep SPY0354");
    }

    // ── One index, several accepted types ────────────────────────────────────────────────────

    /// <summary>
    /// Every candidate rejects argument 0, but they accept different types there. Naming only the
    /// first would present one acceptable type as if it were the only one.
    /// </summary>
    [Fact]
    public void SameIndexDifferentTypes_NamesEveryAcceptedType()
    {
        var source = "class C:\n    def __init__(self):\n        pass\n    def f(self, x: int) -> int:\n        return x\n    def f(self, x: float) -> int:\n        return 1\n\ndef main() -> None:\n    c: C = C()\n    print(c.f(\"a\"))\n";
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse();
        var errors = string.Join(" ", result.CompilationErrors);
        errors.Should().Contain("'int32' or 'float64'",
            "both candidates' expected types at the failing index are named");
    }

    /// <summary>
    /// The six set/dict mutators name the SUBSTITUTED element type, never the declaration's type
    /// PARAMETER: <c>uint8</c>, never <c>T0</c>.
    /// </summary>
    /// <remarks>
    /// The assertion names BOTH sides of the mismatch rather than just <c>uint8</c>. Measured before
    /// (@ 311252e33) the message was "Cannot pass argument of type 'tuple[str, str]' to parameter of
    /// type 'list[uint8]'", so <c>Contain("set[uint8]")</c> was already false and
    /// <c>Contain("uint8")</c> alone could be satisfied by the parameter's display with nothing said
    /// about the argument. Measured now it is "Cannot pass argument of type 'str' to parameter of
    /// type 'uint8'": the receiver's element type is the SLOT each tuple element is admitted into
    /// (R-W arm 1, #1783), so the refusal names the element that does not fit. Requiring both
    /// <c>'str'</c> and <c>'uint8'</c> keeps the substitution under guard — an unsubstituted slot
    /// would print <c>T0</c> or <c>T</c> — and pins the offending-element granularity that replaced
    /// the whole-collection message.
    /// </remarks>
    [Fact]
    public void SetUpdate_NamesTheSubstitutedElementType()
    {
        var source = "def main() -> None:\n    st: set[uint8] = {1}\n    st.update((\"a\", \"b\"))\n";
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse();
        var errors = string.Join(" ", result.CompilationErrors);
        errors.Should().Contain("'uint8'")
            .And.Contain("'str'")
            .And.NotContain("T0")
            .And.NotContain("'T'",
                "the receiver's type argument is substituted before the parameter is displayed, and "
                + "the refusal names the element that does not fit it");
    }

    // ── The CLR routes ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A CLR member with SEVERAL same-arity overloads used to stay silent — "which overload the call
    /// means is CLR overload resolution's answer" (#1243) — and the call reached Roslyn as CS1503
    /// behind SPY0908. When every candidate rejects the same argument there is nothing to choose
    /// between, so the same helper reports it.
    /// </summary>
    [Fact]
    public void ClrMultiOverload_SameArgument_ReportsSPY0220()
    {
        var source = "from system.text import StringBuilder\n\ndef main() -> None:\n    sb = StringBuilder()\n    sb.insert(\"a\", \"b\")\n    print(sb.to_string())\n";
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse();
        result.RawDiagnostics.Should().Contain(
            d => d.Code == DiagnosticCodes.Semantic.TypeMismatch,
            "every Insert overload takes an int index: the argument is reported, not CS1503");
        result.RawDiagnostics.Should().NotContain(
            d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError,
            "the refusal happens at semantic time");
    }
}
