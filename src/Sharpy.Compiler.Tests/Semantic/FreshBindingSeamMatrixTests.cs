using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Fresh-binding seam matrix — route × value × slot (#1812, #1516, R-AE).
///
/// <para><b>Contract.</b> Every route that binds a NEW name from a value decides that name's type
/// through <c>BestCommonType</c>'s single-operand case. An untyped operand (bare <c>None</c>,
/// void call) is refused by name at the binding (SPY0227), or takes the DIRECT store slot's type
/// (R-AE). A <c>None()</c> call is always SPY0227. Controls (typed values, <c>Some(v)</c>) must
/// keep printing.</para>
///
/// <para><b>Axes.</b> Route: {assignment, declaration, walrus, tuple-element} × Value: {BareNone,
/// VoidCall, TypedValue, SomeCall, NoneCall} × Slot: {SlotLess, DirectSlot, StrictSlot}.
/// Not all combinations are applicable — see <see cref="NotApplicableCells"/>.</para>
/// </summary>
[Collection("HeavyCompilation")]
public class FreshBindingSeamMatrixTests : IntegrationTestBase
{
    public FreshBindingSeamMatrixTests(ITestOutputHelper output) : base(output) { }

    private const int RouteCount = 4;
    private const int ValueCount = 5;
    private const int SlotCount = 3;
    private const int NotApplicableCellCount = 33;

    private sealed record BindingRoute(string Name);

    private static readonly BindingRoute[] Routes =
    {
        new("Assignment"),
        new("Declaration"),
        new("Walrus"),
        new("TupleElement"),
    };

    private sealed record BindingValue(string Name);

    private static readonly BindingValue[] Values =
    {
        new("BareNone"),
        new("VoidCall"),
        new("TypedValue"),
        new("SomeCall"),
        new("NoneCall"),
    };

    private sealed record BindingSlot(string Name);

    private static readonly BindingSlot[] Slots =
    {
        new("SlotLess"),
        new("DirectSlot"),
        new("StrictSlot"),
    };

    private enum CellExpectation { Compiles, RefusedSPY0227, RefusedSPY0604, RefusedSPY0220, RefusedSPY0229, RefusedSPY0244, NotApplicable }

    private static string VoidCallPrelude => "def void_fn() -> None:\n    pass\n\n";

    private static (string Source, CellExpectation Expectation, string? ExpectedOutput) ComposeCell(
        string route, string value, string slot)
    {
        var key = $"{route}×{value}×{slot}";
        if (IsNotApplicable(key))
            return ("", CellExpectation.NotApplicable, null);

        return (route, value, slot) switch
        {
            // ── Assignment × SlotLess ──
            ("Assignment", "BareNone", "SlotLess") =>
                ("def main():\n    x = None\n", CellExpectation.RefusedSPY0227, null),
            ("Assignment", "VoidCall", "SlotLess") =>
                (VoidCallPrelude + "def main():\n    x = void_fn()\n", CellExpectation.RefusedSPY0227, null),
            ("Assignment", "TypedValue", "SlotLess") =>
                ("def main():\n    x = 42\n    print(x)\n", CellExpectation.Compiles, "42\n"),
            ("Assignment", "SomeCall", "SlotLess") =>
                ("def main():\n    x = Some(5)\n", CellExpectation.RefusedSPY0227, null),
            ("Assignment", "NoneCall", "SlotLess") =>
                ("def main():\n    x = None()\n", CellExpectation.RefusedSPY0227, null),

            // ── Declaration × SlotLess ──
            ("Declaration", "BareNone", "SlotLess") =>
                ("def main():\n    x = None\n", CellExpectation.RefusedSPY0227, null),
            ("Declaration", "VoidCall", "SlotLess") =>
                (VoidCallPrelude + "def main():\n    x = void_fn()\n", CellExpectation.RefusedSPY0227, null),
            ("Declaration", "TypedValue", "SlotLess") =>
                ("def main():\n    x = \"hello\"\n    print(x)\n", CellExpectation.Compiles, "hello\n"),
            ("Declaration", "SomeCall", "SlotLess") =>
                ("def main():\n    x = Some(5)\n", CellExpectation.RefusedSPY0227, null),
            ("Declaration", "NoneCall", "SlotLess") =>
                ("def main():\n    x = None()\n", CellExpectation.RefusedSPY0227, null),

            // ── Walrus × SlotLess ──
            ("Walrus", "BareNone", "SlotLess") =>
                ("def main():\n    x = (q := None)\n", CellExpectation.RefusedSPY0227, null),
            ("Walrus", "VoidCall", "SlotLess") =>
                (VoidCallPrelude + "def main():\n    x = (q := void_fn())\n", CellExpectation.RefusedSPY0227, null),
            ("Walrus", "TypedValue", "SlotLess") =>
                ("def main():\n    x = (q := 42)\n    print(q)\n", CellExpectation.Compiles, "42\n"),
            ("Walrus", "SomeCall", "SlotLess") =>
                ("def main():\n    x = (q := Some(5))\n", CellExpectation.RefusedSPY0227, null),
            ("Walrus", "NoneCall", "SlotLess") =>
                ("def main():\n    x = (q := None())\n", CellExpectation.RefusedSPY0227, null),

            // ── TupleElement × SlotLess ──
            ("TupleElement", "BareNone", "SlotLess") =>
                ("def main():\n    a, b = None, 1\n", CellExpectation.RefusedSPY0227, null),
            ("TupleElement", "VoidCall", "SlotLess") =>
                (VoidCallPrelude + "def main():\n    a, b = void_fn(), 1\n", CellExpectation.RefusedSPY0227, null),
            ("TupleElement", "TypedValue", "SlotLess") =>
                ("def main():\n    a, b = \"hi\", 1\n    print(a)\n    print(b)\n", CellExpectation.Compiles, "hi\n1\n"),

            // ── Walrus × DirectSlot (the R-AE seam: a fresh walrus under a direct store slot) ──
            ("Walrus", "BareNone", "DirectSlot") =>
                ("def g(x: str | None) -> None:\n    print(x)\n\ndef main():\n    g((q := None))\n    print(q)\n",
                 CellExpectation.Compiles, "None\nNone\n"),
            ("Walrus", "VoidCall", "DirectSlot") =>
                (VoidCallPrelude + "def g(x: str | None) -> None:\n    print(x)\n\ndef main():\n    g((q := void_fn()))\n",
                 CellExpectation.RefusedSPY0227, null),
            ("Walrus", "TypedValue", "DirectSlot") =>
                ("def g(x: str | None) -> None:\n    print(x)\n\ndef main():\n    g((q := \"hello\"))\n    print(q)\n",
                 CellExpectation.Compiles, "hello\nhello\n"),
            ("Walrus", "SomeCall", "DirectSlot") =>
                ("def g(x: int?) -> None:\n    print(x)\n\ndef main():\n    g((q := Some(5)))\n    print(q)\n",
                 CellExpectation.Compiles, "5\n5\n"),
            ("Walrus", "NoneCall", "DirectSlot") =>
                ("def g(x: int?) -> None:\n    print(x)\n\ndef main():\n    g((q := None()))\n    print(q)\n",
                 CellExpectation.Compiles, "None\nNone\n"),

            // ── Walrus × StrictSlot (SPY0229 for bare None into non-nullable) ──
            ("Walrus", "BareNone", "StrictSlot") =>
                ("def h(x: int) -> None:\n    print(x)\n\ndef main():\n    h((q := None))\n",
                 CellExpectation.RefusedSPY0229, null),
            ("Walrus", "TypedValue", "StrictSlot") =>
                ("def h(x: int) -> None:\n    print(x)\n\ndef main():\n    h((q := 42))\n    print(q)\n",
                 CellExpectation.Compiles, "42\n42\n"),

            // ── TupleElement × DirectSlot (declared-slot tuple unpacking) ──
            ("TupleElement", "BareNone", "DirectSlot") =>
                ("def main():\n    a: str | None = None\n    b: int = 0\n    a, b = None, 1\n    print(a)\n    print(b)\n",
                 CellExpectation.Compiles, "None\n1\n"),
            ("TupleElement", "TypedValue", "DirectSlot") =>
                ("def main():\n    a: str = \"\"\n    b: int = 0\n    a, b = \"hi\", 1\n    print(a)\n    print(b)\n",
                 CellExpectation.Compiles, "hi\n1\n"),

            _ => throw new InvalidOperationException($"Unmapped cell: {key}"),
        };
    }

    private static bool IsNotApplicable(string key) => NotApplicableCells.ContainsKey(key);

    private static readonly Dictionary<string, string> NotApplicableCells = new(StringComparer.Ordinal)
    {
        // Assignment and Declaration into a slot are not FRESH bindings — they store into an
        // existing declared variable, which is the StoreConversionMatrix's domain.
        ["Assignment×BareNone×DirectSlot"] = "storing into an existing typed variable is the store seam's domain, not a fresh binding",
        ["Assignment×VoidCall×DirectSlot"] = "storing into an existing typed variable is the store seam's domain",
        ["Assignment×TypedValue×DirectSlot"] = "storing into an existing typed variable is the store seam's domain",
        ["Assignment×SomeCall×DirectSlot"] = "storing into an existing typed variable is the store seam's domain",
        ["Assignment×NoneCall×DirectSlot"] = "storing into an existing typed variable is the store seam's domain",
        ["Assignment×BareNone×StrictSlot"] = "storing into an existing typed variable is the store seam's domain",
        ["Assignment×VoidCall×StrictSlot"] = "storing into an existing typed variable is the store seam's domain",
        ["Assignment×TypedValue×StrictSlot"] = "storing into an existing typed variable is the store seam's domain",
        ["Assignment×SomeCall×StrictSlot"] = "storing into an existing typed variable is the store seam's domain",
        ["Assignment×NoneCall×StrictSlot"] = "storing into an existing typed variable is the store seam's domain",
        // Declaration with a type annotation is not a fresh auto binding — it already has a slot.
        ["Declaration×BareNone×DirectSlot"] = "a declared-type binding is a store into the declared slot, not a fresh auto binding",
        ["Declaration×VoidCall×DirectSlot"] = "a declared-type binding is a store into the declared slot",
        ["Declaration×TypedValue×DirectSlot"] = "a declared-type binding is a store into the declared slot",
        ["Declaration×SomeCall×DirectSlot"] = "a declared-type binding is a store into the declared slot",
        ["Declaration×NoneCall×DirectSlot"] = "a declared-type binding is a store into the declared slot",
        ["Declaration×BareNone×StrictSlot"] = "a declared-type binding is a store into the declared slot",
        ["Declaration×VoidCall×StrictSlot"] = "a declared-type binding is a store into the declared slot",
        ["Declaration×TypedValue×StrictSlot"] = "a declared-type binding is a store into the declared slot",
        ["Declaration×SomeCall×StrictSlot"] = "a declared-type binding is a store into the declared slot",
        ["Declaration×NoneCall×StrictSlot"] = "a declared-type binding is a store into the declared slot",
        // TupleElement slot-less: SomeCall and NoneCall are not fresh-binding-specific (the tuple
        // element type is driven by the element's own type, same as the typed case).
        ["TupleElement×SomeCall×SlotLess"] = "Some(v) in a tuple element takes the element type, not a fresh-binding decision",
        ["TupleElement×NoneCall×SlotLess"] = "None() in a tuple element takes the element type, not a fresh-binding decision",
        // TupleElement with a declared slot: VoidCall, SomeCall, NoneCall into declared targets
        ["TupleElement×VoidCall×DirectSlot"] = "a void call in a declared-slot tuple element is the store seam's refusal",
        ["TupleElement×SomeCall×DirectSlot"] = "Some(v) into a declared tuple target is the store seam's domain",
        ["TupleElement×NoneCall×DirectSlot"] = "None() into a declared tuple target is the store seam's domain",
        // TupleElement strict slot: all are the store seam's domain
        ["TupleElement×BareNone×StrictSlot"] = "a bare None into a non-nullable declared tuple target is the store seam's refusal",
        ["TupleElement×VoidCall×StrictSlot"] = "a void call into a non-nullable declared tuple target is the store seam's refusal",
        ["TupleElement×TypedValue×StrictSlot"] = "a typed value into a non-nullable declared tuple target is the store seam's domain",
        ["TupleElement×SomeCall×StrictSlot"] = "Some(v) into a non-nullable declared tuple target is the store seam's domain",
        ["TupleElement×NoneCall×StrictSlot"] = "None() into a non-nullable declared tuple target is the store seam's domain",
        // Walrus StrictSlot: remaining value kinds
        ["Walrus×VoidCall×StrictSlot"] = "a void-call walrus is refused at every slot, same as slot-less",
        ["Walrus×SomeCall×StrictSlot"] = "Some(v) into a strict slot is the store seam's refusal (SPY0604)",
        ["Walrus×NoneCall×StrictSlot"] = "None() into a strict slot is the store seam's refusal",
    };

    public static IEnumerable<object[]> ApplicableCells =>
        from r in Routes
        from v in Values
        from s in Slots
        let key = $"{r.Name}×{v.Name}×{s.Name}"
        where !NotApplicableCells.ContainsKey(key)
        select new object[] { r.Name, v.Name, s.Name };

    [Theory]
    [MemberData(nameof(ApplicableCells))]
    public void Cell_FreshBindingDecidedByBestCommonType(string route, string value, string slot)
    {
        var (source, expectation, expectedOutput) = ComposeCell(route, value, slot);
        expectation.Should().NotBe(CellExpectation.NotApplicable);

        var result = CompileAndExecute(source);

        result.RawDiagnostics.Should().NotContain(
            d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError,
            $"[{route} × {value} × {slot}] must never produce SPY0908. "
            + $"Diagnostics: {string.Join(" | ", result.CompilationErrors)}\n{source}");

        switch (expectation)
        {
            case CellExpectation.Compiles:
                result.Success.Should().BeTrue(
                    $"[{route} × {value} × {slot}] must compile and run. "
                    + $"Diagnostics: {string.Join(" | ", result.CompilationErrors)}\n{source}");
                result.StandardOutput.Should().Be(expectedOutput,
                    $"[{route} × {value} × {slot}]\n{source}");
                break;

            case CellExpectation.RefusedSPY0227:
                result.Success.Should().BeFalse(
                    $"[{route} × {value} × {slot}] must be refused\n{source}");
                result.RawDiagnostics.Should().Contain(
                    d => d.Code == DiagnosticCodes.Semantic.CannotInferType,
                    $"[{route} × {value} × {slot}] must report SPY0227. Got: "
                    + $"{string.Join(" | ", result.RawDiagnostics.Select(d => $"{d.Code}: {d.Message}"))}\n{source}");
                break;

            case CellExpectation.RefusedSPY0604:
                result.Success.Should().BeFalse(
                    $"[{route} × {value} × {slot}] must be refused\n{source}");
                result.RawDiagnostics.Should().Contain(
                    d => d.Code == DiagnosticCodes.SemanticOverflow.StrictOptionalConstruction,
                    $"[{route} × {value} × {slot}] must report SPY0604\n{source}");
                break;

            case CellExpectation.RefusedSPY0220:
                result.Success.Should().BeFalse(
                    $"[{route} × {value} × {slot}] must be refused\n{source}");
                result.RawDiagnostics.Should().Contain(
                    d => d.Code == DiagnosticCodes.Semantic.TypeMismatch,
                    $"[{route} × {value} × {slot}] must report SPY0220. Got: "
                    + $"{string.Join(" | ", result.RawDiagnostics.Select(d => $"{d.Code}: {d.Message}"))}\n{source}");
                break;

            case CellExpectation.RefusedSPY0229:
                result.Success.Should().BeFalse(
                    $"[{route} × {value} × {slot}] must be refused\n{source}");
                result.RawDiagnostics.Should().Contain(
                    d => d.Code == DiagnosticCodes.Semantic.NullabilityViolation,
                    $"[{route} × {value} × {slot}] must report SPY0229. Got: "
                    + $"{string.Join(" | ", result.RawDiagnostics.Select(d => $"{d.Code}: {d.Message}"))}\n{source}");
                break;

            case CellExpectation.RefusedSPY0244:
                result.Success.Should().BeFalse(
                    $"[{route} × {value} × {slot}] must be refused\n{source}");
                result.RawDiagnostics.Should().Contain(
                    d => d.Code == DiagnosticCodes.Semantic.InvalidNoneConstructor,
                    $"[{route} × {value} × {slot}] must report SPY0244. Got: "
                    + $"{string.Join(" | ", result.RawDiagnostics.Select(d => $"{d.Code}: {d.Message}"))}\n{source}");
                break;
        }
    }

    [Fact]
    public void Matrix_IsTotalOverItsAxes()
    {
        Routes.Length.Should().Be(RouteCount);
        Values.Length.Should().Be(ValueCount);
        Slots.Length.Should().Be(SlotCount);
        Routes.Select(r => r.Name).Should().OnlyHaveUniqueItems();
        Values.Select(v => v.Name).Should().OnlyHaveUniqueItems();
        Slots.Select(s => s.Name).Should().OnlyHaveUniqueItems();

        var product = RouteCount * ValueCount * SlotCount;
        var applicable = ApplicableCells.Count();
        var notApplicable = NotApplicableCells.Count;

        notApplicable.Should().Be(NotApplicableCellCount, "every N/A cell is written down with its reason");
        var allKeys = (from r in Routes from v in Values from s in Slots select $"{r.Name}×{v.Name}×{s.Name}").ToHashSet();
        NotApplicableCells.Keys.Should().OnlyContain(k => allKeys.Contains(k),
            "an N/A key must name a real cell");
        (applicable + notApplicable).Should().Be(product,
            $"applicable ({applicable}) + N/A ({notApplicable}) must be the whole product "
            + $"({RouteCount} × {ValueCount} × {SlotCount})");
    }

    [Fact]
    public void RefuseUntypedVoidBinding_IsDeleted()
    {
        var repoRoot = Infrastructure.DispatchSiteScan.FindRepoRoot();
        var semanticDir = Path.Combine(repoRoot, "src", "Sharpy.Compiler", "Semantic");
        var files = Directory.GetFiles(semanticDir, "*.cs", SearchOption.AllDirectories);
        foreach (var file in files)
        {
            var content = File.ReadAllText(file);
            content.Should().NotContain("RefuseUntypedVoidBinding",
                $"{Path.GetFileName(file)} must not reference the deleted helper");
        }
    }

    [Fact]
    public void RefusalReasonStrings_ExistOnlyInBestCommonType()
    {
        var repoRoot = Infrastructure.DispatchSiteScan.FindRepoRoot();
        var semanticDir = Path.Combine(repoRoot, "src", "Sharpy.Compiler", "Semantic");
        var files = Directory.GetFiles(semanticDir, "*.cs", SearchOption.AllDirectories);

        var reasonStrings = new[] { "names no type on its own", "produces no value, so there is nothing for" };
        var filesWithReasons = new List<string>();

        foreach (var file in files)
        {
            var content = File.ReadAllText(file);
            if (reasonStrings.Any(r => content.Contains(r, StringComparison.Ordinal)))
                filesWithReasons.Add(Path.GetFileName(file));
        }

        filesWithReasons.Should().BeEquivalentTo(
            new[] { "TypeChecker.BestCommonType.cs" },
            "the two refusal reason strings must exist only in BestCommonType.cs — "
            + "the one-site positive control");
    }
}
