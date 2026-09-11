using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Fresh-binding seam matrix — route × value × slot (#1812, #1516, #1796, R-AE).
///
/// <para><b>Contract.</b> EVERY route that binds a NEW name from a value decides that name's type
/// through <c>BestCommonType</c>'s single-operand case. An untyped operand (bare <c>None</c>, a
/// void call) is refused BY NAME at the binding (SPY0227) or takes the DIRECT store slot's type
/// (R-AE); it is never recorded as <c>void</c> and handed to the emitter. Controls (typed values,
/// <c>Some(v)</c>, <c>None()</c>) keep their own answers.</para>
///
/// <para><b>Axes.</b> Route (11) × Value (5) × Slot (3). The route axis is the tripwire the plan's
/// own adversarial review named: a refusal added to one route leaves the others emitting
/// <c>void</c>. Every route compiles a DISTINCT source — <c>x = None</c> (assignment) and
/// <c>x: auto = None</c> (declaration) are two spellings the checker reaches through two methods,
/// and a matrix that compiled the same text for both measured one of them twice.</para>
///
/// <para><b>N/A.</b> Every inapplicable cell is named in <see cref="NotApplicableCells"/> with its
/// reason. There is deliberately no "this whole position is N/A" predicate: the one this file used
/// to carry made 33 cells disappear without a line of evidence.</para>
/// </summary>
[Collection("HeavyCompilation")]
public class FreshBindingSeamMatrixTests : IntegrationTestBase
{
    public FreshBindingSeamMatrixTests(ITestOutputHelper output) : base(output) { }

    private const int RouteCount = 11;
    private const int ValueCount = 5;
    private const int SlotCount = 3;
    private const int ApplicableCellCount = 56;
    private const int NotApplicableCellCount = 109;

    private static readonly string[] Routes =
    {
        "Assignment",
        "Declaration",
        "Walrus",
        "TupleElementFlat",
        "TupleElementNested",
        "StarredUnpacking",
        "ComprehensionElement",
        "ForTarget",
        "MatchCapture",
        "ModuleLevel",
        "ClassBody",
    };

    private static readonly string[] Values =
    {
        "BareNone",
        "VoidCall",
        "TypedValue",
        "SomeCall",
        "NoneCall",
    };

    private static readonly string[] Slots =
    {
        "SlotLess",
        "DirectSlot",
        "StrictSlot",
    };

    private enum CellExpectation { Compiles, RefusedSPY0227, RefusedSPY0229, RefusedSPY0275 }

    private const string VoidCallPrelude = "def void_fn() -> None:\n    pass\n\n";

    /// <summary>
    /// The source, expectation and (for an accepted cell) the exact stdout of one cell. Every
    /// accepted cell PRINTS: a type decided but never applied is the "recorded ≠ applied" shape,
    /// and only a value comparison catches it.
    /// </summary>
    private static (string Source, CellExpectation Expectation, string? Output) ComposeCell(
        string route, string value, string slot)
        => (route, value, slot) switch
        {
            // ── Assignment (`x = v`) — the auto-binding form ────────────────────────────────
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

            // ── Declaration (`x: auto = v`) — a DIFFERENT checker method, a different source ──
            ("Declaration", "BareNone", "SlotLess") =>
                ("def main():\n    x: auto = None\n", CellExpectation.RefusedSPY0227, null),
            ("Declaration", "VoidCall", "SlotLess") =>
                (VoidCallPrelude + "def main():\n    x: auto = void_fn()\n", CellExpectation.RefusedSPY0227, null),
            ("Declaration", "TypedValue", "SlotLess") =>
                ("def main():\n    x: auto = \"hello\"\n    print(x)\n", CellExpectation.Compiles, "hello\n"),
            ("Declaration", "SomeCall", "SlotLess") =>
                ("def main():\n    x: auto = Some(5)\n", CellExpectation.RefusedSPY0227, null),
            ("Declaration", "NoneCall", "SlotLess") =>
                ("def main():\n    x: auto = None()\n", CellExpectation.RefusedSPY0227, null),

            // ── Walrus ──────────────────────────────────────────────────────────────────────
            ("Walrus", "BareNone", "SlotLess") =>
                ("def main():\n    x = (q := None)\n", CellExpectation.RefusedSPY0227, null),
            ("Walrus", "VoidCall", "SlotLess") =>
                (VoidCallPrelude + "def main():\n    x = (q := void_fn())\n", CellExpectation.RefusedSPY0227, null),
            ("Walrus", "TypedValue", "SlotLess") =>
                ("def main():\n    x = (q := 42)\n    print(x)\n    print(q)\n", CellExpectation.Compiles, "42\n42\n"),
            ("Walrus", "SomeCall", "SlotLess") =>
                ("def main():\n    x = (q := Some(5))\n", CellExpectation.RefusedSPY0227, null),
            ("Walrus", "NoneCall", "SlotLess") =>
                ("def main():\n    x = (q := None())\n", CellExpectation.RefusedSPY0227, null),

            // R-AE: a fresh walrus that IS the store's value node takes the slot's type.
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

            ("Walrus", "BareNone", "StrictSlot") =>
                ("def h(x: int) -> None:\n    print(x)\n\ndef main():\n    h((q := None))\n",
                 CellExpectation.RefusedSPY0229, null),
            ("Walrus", "TypedValue", "StrictSlot") =>
                ("def h(x: int) -> None:\n    print(x)\n\ndef main():\n    h((q := 42))\n    print(q)\n",
                 CellExpectation.Compiles, "42\n42\n"),

            // ── Tuple unpacking, FLAT ───────────────────────────────────────────────────────
            ("TupleElementFlat", "BareNone", "SlotLess") =>
                ("def main():\n    a, b = None, 1\n", CellExpectation.RefusedSPY0227, null),
            ("TupleElementFlat", "VoidCall", "SlotLess") =>
                (VoidCallPrelude + "def main():\n    a, b = void_fn(), 1\n", CellExpectation.RefusedSPY0227, null),
            ("TupleElementFlat", "TypedValue", "SlotLess") =>
                ("def main():\n    a, b = \"hi\", 1\n    print(a)\n    print(b)\n", CellExpectation.Compiles, "hi\n1\n"),
            ("TupleElementFlat", "BareNone", "DirectSlot") =>
                ("def main():\n    a: str | None = \"s\"\n    b: int = 0\n    a, b = None, 1\n    print(a)\n    print(b)\n",
                 CellExpectation.Compiles, "None\n1\n"),
            ("TupleElementFlat", "TypedValue", "DirectSlot") =>
                ("def main():\n    a: str = \"\"\n    b: int = 0\n    a, b = \"hi\", 1\n    print(a)\n    print(b)\n",
                 CellExpectation.Compiles, "hi\n1\n"),

            // ── Tuple unpacking, NESTED — the inner literal is a row of its own seams ────────
            ("TupleElementNested", "BareNone", "SlotLess") =>
                ("def main():\n    (a, b), c = (None, 1), 2\n", CellExpectation.RefusedSPY0227, null),
            ("TupleElementNested", "VoidCall", "SlotLess") =>
                (VoidCallPrelude + "def main():\n    (a, b), c = (void_fn(), 1), 2\n",
                 CellExpectation.RefusedSPY0227, null),
            ("TupleElementNested", "TypedValue", "SlotLess") =>
                ("def main():\n    (a, b), c = (\"hi\", 1), 2\n    print(a)\n    print(b)\n    print(c)\n",
                 CellExpectation.Compiles, "hi\n1\n2\n"),
            ("TupleElementNested", "BareNone", "DirectSlot") =>
                ("def main():\n    a: str | None = \"s\"\n    b: int = 0\n    c: int = 0\n"
                 + "    (a, b), c = (None, 1), 2\n    print(a)\n    print(b)\n    print(c)\n",
                 CellExpectation.Compiles, "None\n1\n2\n"),
            ("TupleElementNested", "TypedValue", "DirectSlot") =>
                ("def main():\n    a: str = \"\"\n    b: int = 0\n    c: int = 0\n"
                 + "    (a, b), c = (\"hi\", 1), 2\n    print(a)\n    print(b)\n    print(c)\n",
                 CellExpectation.Compiles, "hi\n1\n2\n"),

            // ── Starred unpacking ───────────────────────────────────────────────────────────
            ("StarredUnpacking", "BareNone", "SlotLess") =>
                ("def main():\n    a, *rest = None, 1, 2\n", CellExpectation.RefusedSPY0227, null),
            ("StarredUnpacking", "VoidCall", "SlotLess") =>
                (VoidCallPrelude + "def main():\n    a, *rest = void_fn(), 1, 2\n",
                 CellExpectation.RefusedSPY0227, null),
            ("StarredUnpacking", "TypedValue", "SlotLess") =>
                ("def main():\n    a, *rest = \"hi\", 1, 2\n    print(a)\n    print(len(rest))\n",
                 CellExpectation.Compiles, "hi\n2\n"),
            ("StarredUnpacking", "BareNone", "DirectSlot") =>
                ("def main():\n    a: str | None = \"s\"\n    a, *rest = None, 1, 2\n    print(a)\n    print(len(rest))\n",
                 CellExpectation.Compiles, "None\n2\n"),
            ("StarredUnpacking", "TypedValue", "DirectSlot") =>
                ("def main():\n    a: str = \"\"\n    a, *rest = \"hi\", 1, 2\n    print(a)\n    print(len(rest))\n",
                 CellExpectation.Compiles, "hi\n2\n"),

            // ── Comprehension element ───────────────────────────────────────────────────────
            ("ComprehensionElement", "BareNone", "SlotLess") =>
                ("def main():\n    xs = [None for i in range(2)]\n", CellExpectation.RefusedSPY0227, null),
            ("ComprehensionElement", "VoidCall", "SlotLess") =>
                (VoidCallPrelude + "def main():\n    xs = [void_fn() for i in range(2)]\n",
                 CellExpectation.RefusedSPY0227, null),
            ("ComprehensionElement", "TypedValue", "SlotLess") =>
                ("def main():\n    xs = [i for i in range(2)]\n    print(len(xs))\n",
                 CellExpectation.Compiles, "2\n"),
            ("ComprehensionElement", "BareNone", "DirectSlot") =>
                ("def main():\n    xs: list[str | None] = [None for i in range(2)]\n    print(len(xs))\n",
                 CellExpectation.Compiles, "2\n"),
            ("ComprehensionElement", "TypedValue", "DirectSlot") =>
                ("def main():\n    xs: list[int] = [i for i in range(2)]\n    print(len(xs))\n",
                 CellExpectation.Compiles, "2\n"),

            // ── for target — the element type comes from the iterable, so the refusal is the
            //    iterable literal's own (the target never sees an untyped operand) ───────────
            ("ForTarget", "BareNone", "SlotLess") =>
                ("def main():\n    for v in [None]:\n        print(v)\n", CellExpectation.RefusedSPY0227, null),
            ("ForTarget", "VoidCall", "SlotLess") =>
                (VoidCallPrelude + "def main():\n    for v in [void_fn()]:\n        print(v)\n",
                 CellExpectation.RefusedSPY0227, null),
            ("ForTarget", "TypedValue", "SlotLess") =>
                ("def main():\n    for v in [1, 2]:\n        print(v)\n", CellExpectation.Compiles, "1\n2\n"),
            ("ForTarget", "BareNone", "DirectSlot") =>
                ("def main():\n    xs: list[str | None] = [None]\n    for v in xs:\n        print(v)\n",
                 CellExpectation.Compiles, "None\n"),
            ("ForTarget", "TypedValue", "DirectSlot") =>
                ("def main():\n    xs: list[int] = [1]\n    for v in xs:\n        print(v)\n",
                 CellExpectation.Compiles, "1\n"),

            // ── match capture ───────────────────────────────────────────────────────────────
            // A bare `None` scrutinee is a legal match subject whose capture prints `None`
            // (python3 agrees); a VOID CALL has no value to match on at all (SPY0275).
            ("MatchCapture", "BareNone", "SlotLess") =>
                ("def main():\n    match None:\n        case y:\n            print(y)\n",
                 CellExpectation.Compiles, "None\n"),
            ("MatchCapture", "VoidCall", "SlotLess") =>
                (VoidCallPrelude + "def main():\n    match void_fn():\n        case y:\n            print(y)\n",
                 CellExpectation.RefusedSPY0275, null),
            ("MatchCapture", "TypedValue", "SlotLess") =>
                ("def main():\n    match 42:\n        case y:\n            print(y)\n",
                 CellExpectation.Compiles, "42\n"),

            // ── module level ────────────────────────────────────────────────────────────────
            ("ModuleLevel", "BareNone", "SlotLess") =>
                ("x = None\n\ndef main():\n    print(x)\n", CellExpectation.RefusedSPY0227, null),
            ("ModuleLevel", "VoidCall", "SlotLess") =>
                (VoidCallPrelude + "x = void_fn()\n\ndef main():\n    print(x)\n",
                 CellExpectation.RefusedSPY0227, null),
            ("ModuleLevel", "TypedValue", "SlotLess") =>
                ("x: int = 42\n\ndef main():\n    print(x)\n", CellExpectation.Compiles, "42\n"),

            // ── class body ──────────────────────────────────────────────────────────────────
            ("ClassBody", "BareNone", "SlotLess") =>
                ("class C:\n    x = None\n\ndef main():\n    print(1)\n", CellExpectation.RefusedSPY0227, null),
            ("ClassBody", "VoidCall", "SlotLess") =>
                (VoidCallPrelude + "class C:\n    x = void_fn()\n\ndef main():\n    print(1)\n",
                 CellExpectation.RefusedSPY0227, null),
            ("ClassBody", "TypedValue", "SlotLess") =>
                ("class C:\n    x: int = 5\n\ndef main():\n    c = C()\n    print(c.x)\n",
                 CellExpectation.Compiles, "5\n"),

            _ => throw new InvalidOperationException(
                $"Unmapped cell: {route}×{value}×{slot} — every product member is either composed "
                + "here or listed in NotApplicableCells"),
        };

    // ── N/A ──────────────────────────────────────────────────────────────────────────────────
    // Written down BY CELL. The reasons are shared through these constants, but no cell is N/A by
    // virtue of a predicate over its route: a route added without rows fails the totality test.

    private const string DeclaredSlotIsTheStoreSeam =
        "storing into an ALREADY DECLARED slot is the store seam's domain (StoreConversionMatrixTests), "
        + "not a fresh binding — this route has no slot of its own to take";

    private const string ConstructedOptionalIsNotFresh =
        "Some(v)/None() carry their own Optional typing rule (R-G, #1720): at this route they are "
        + "decided by the enclosing slot or refused for want of an annotation, which is "
        + "StoreConversionMatrixTests' axis, not the fresh-binding decision";

    private const string RouteHasNoStrictSlot =
        "this route never presents a NON-NULLABLE slot to a fresh binding — the strict-slot cell "
        + "exists only where a fresh binding can be the direct operand of one (the walrus)";

    private const string ScrutineeIsNotAStore =
        "a match scrutinee is not a store: Some(v)/None() there are matched, not bound, and the "
        + "capture takes the scrutinee's own type (match_statement.md)";

    private static readonly Dictionary<string, string> NotApplicableCells = BuildNotApplicableCells();

    private static Dictionary<string, string> BuildNotApplicableCells()
    {
        var na = new Dictionary<string, string>(StringComparer.Ordinal);

        void Mark(string route, string slot, string reason, params string[] values)
        {
            foreach (var v in values)
                na[$"{route}×{v}×{slot}"] = reason;
        }

        var all = Values;
        var constructed = new[] { "SomeCall", "NoneCall" };

        // Assignment and Declaration: an annotated target is a STORE, not a fresh binding.
        foreach (var route in new[] { "Assignment", "Declaration" })
        {
            Mark(route, "DirectSlot", DeclaredSlotIsTheStoreSeam, all);
            Mark(route, "StrictSlot", DeclaredSlotIsTheStoreSeam, all);
        }

        // Walrus: the strict slot is real, but only for the two values that reach it as a fresh
        // binding — a void-call walrus is refused before any slot is consulted, and the two
        // constructed Optionals are the store seam's SPY0604 row.
        Mark("Walrus", "StrictSlot",
            "a void-call walrus is refused at EVERY slot, before the slot is consulted (R-AE)",
            "VoidCall");
        Mark("Walrus", "StrictSlot", ConstructedOptionalIsNotFresh, constructed);

        // The unpacking family: an element's slot is its TARGET's declared type, so DirectSlot is
        // real; there is no non-nullable fresh-binding cell, and the constructed Optionals are the
        // store seam's.
        foreach (var route in new[] { "TupleElementFlat", "TupleElementNested", "StarredUnpacking" })
        {
            Mark(route, "SlotLess", ConstructedOptionalIsNotFresh, constructed);
            Mark(route, "DirectSlot", DeclaredSlotIsTheStoreSeam, "VoidCall");
            Mark(route, "DirectSlot", ConstructedOptionalIsNotFresh, constructed);
            Mark(route, "StrictSlot", RouteHasNoStrictSlot, all);
        }

        // Comprehension element and for target: the element expectation is the slot.
        foreach (var route in new[] { "ComprehensionElement", "ForTarget" })
        {
            Mark(route, "SlotLess", ConstructedOptionalIsNotFresh, constructed);
            Mark(route, "DirectSlot", DeclaredSlotIsTheStoreSeam, "VoidCall");
            Mark(route, "DirectSlot", ConstructedOptionalIsNotFresh, constructed);
            Mark(route, "StrictSlot", RouteHasNoStrictSlot, all);
        }

        // Match capture: the capture's type is the scrutinee's.
        Mark("MatchCapture", "SlotLess", ScrutineeIsNotAStore, constructed);
        Mark("MatchCapture", "DirectSlot", ScrutineeIsNotAStore, all);
        Mark("MatchCapture", "StrictSlot", ScrutineeIsNotAStore, all);

        // Module level and class body: an annotated module field or class field is a store.
        foreach (var route in new[] { "ModuleLevel", "ClassBody" })
        {
            Mark(route, "SlotLess", ConstructedOptionalIsNotFresh, constructed);
            Mark(route, "DirectSlot", DeclaredSlotIsTheStoreSeam, all);
            Mark(route, "StrictSlot", DeclaredSlotIsTheStoreSeam, all);
        }

        return na;
    }

    public static IEnumerable<object[]> ApplicableCells =>
        from r in Routes
        from v in Values
        from s in Slots
        where !NotApplicableCells.ContainsKey($"{r}×{v}×{s}")
        select new object[] { r, v, s };

    [Theory]
    [MemberData(nameof(ApplicableCells))]
    public void Cell_FreshBindingDecidedByBestCommonType(string route, string value, string slot)
    {
        var (source, expectation, expectedOutput) = ComposeCell(route, value, slot);
        var label = $"{route} × {value} × {slot}";

        var result = CompileAndExecute(source);

        result.RawDiagnostics.Should().NotContain(
            d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError,
            $"[{label}] must never produce SPY0908 — an untyped binding that reaches Roslyn IS the "
            + $"defect. Diagnostics: {string.Join(" | ", result.CompilationErrors)}\n{source}");

        switch (expectation)
        {
            case CellExpectation.Compiles:
                result.Success.Should().BeTrue(
                    $"[{label}] must compile and run. "
                    + $"Diagnostics: {string.Join(" | ", result.CompilationErrors)}\n{source}");
                result.StandardOutput.Should().Be(expectedOutput,
                    $"[{label}] prints the value that was bound — a type decided but never applied "
                    + $"fails here, not at the diagnostic count\n{source}");
                break;

            case CellExpectation.RefusedSPY0227:
                AssertRefused(result, DiagnosticCodes.Semantic.CannotInferType, label, source);
                break;

            case CellExpectation.RefusedSPY0229:
                AssertRefused(result, DiagnosticCodes.Semantic.NullabilityViolation, label, source);
                break;

            case CellExpectation.RefusedSPY0275:
                AssertRefused(result, DiagnosticCodes.Semantic.VoidMatchScrutinee, label, source);
                break;
        }
    }

    private static void AssertRefused(
        ExecutionResult result, string code, string label, string source)
    {
        result.Success.Should().BeFalse($"[{label}] must be refused\n{source}");
        result.RawDiagnostics.Should().Contain(
            d => d.Code == code,
            $"[{label}] must report {code}. Got: "
            + $"{string.Join(" | ", result.RawDiagnostics.Select(d => $"{d.Code}: {d.Message}"))}\n{source}");
    }

    /// <summary>
    /// The bare-<c>None</c> refusal names BOTH spellings of absence, in real source. It cannot
    /// steer to <c>x: T? = None</c>, which R-G (#1720) refuses.
    /// </summary>
    [Theory]
    [InlineData("Assignment", "def main():\n    x = None\n", "x")]
    [InlineData("Declaration", "def main():\n    x: auto = None\n", "x")]
    [InlineData("Walrus", "def main():\n    y = (q := None)\n", "q")]
    [InlineData("TupleElementFlat", "def main():\n    a, b = None, 1\n", "a")]
    [InlineData("StarredUnpacking", "def main():\n    a, *rest = None, 1, 2\n", "a")]
    public void BareNoneRefusal_SteersWithRealSource(string label, string source, string name)
    {
        var result = CompileAndExecute(source);

        var diagnostic = result.RawDiagnostics
            .FirstOrDefault(d => d.Code == DiagnosticCodes.Semantic.CannotInferType);
        diagnostic.Should().NotBeNull($"[{label}] must report SPY0227\n{source}");

        diagnostic!.Message.Should().Contain($"'{name}: T | None = None'",
            $"[{label}] must offer the .NET-nullable spelling as source the user can paste\n{source}");
        diagnostic.Message.Should().Contain($"'{name}: T? = None()'",
            $"[{label}] must offer the Sharpy-optional spelling, built with None()\n{source}");
        diagnostic.Message.Should().NotContain("T? = None'",
            $"[{label}] must never steer to 'T? = None', which R-G refuses\n{source}");
    }

    [Fact]
    public void Matrix_IsTotalOverItsAxes()
    {
        Routes.Length.Should().Be(RouteCount);
        Values.Length.Should().Be(ValueCount);
        Slots.Length.Should().Be(SlotCount);
        Routes.Should().OnlyHaveUniqueItems();
        Values.Should().OnlyHaveUniqueItems();
        Slots.Should().OnlyHaveUniqueItems();

        var product = (from r in Routes from v in Values from s in Slots select $"{r}×{v}×{s}")
            .ToHashSet();
        product.Count.Should().Be(RouteCount * ValueCount * SlotCount);

        NotApplicableCells.Keys.Should().OnlyContain(k => product.Contains(k),
            "an N/A key must name a real cell — a stale key hides a cell nobody measures");

        var applicable = ApplicableCells.Count();
        applicable.Should().Be(ApplicableCellCount, "the applicable half is written down");
        NotApplicableCells.Count.Should().Be(NotApplicableCellCount,
            "every N/A cell is written down with its reason");
        (applicable + NotApplicableCells.Count).Should().Be(product.Count,
            $"applicable ({applicable}) + N/A ({NotApplicableCells.Count}) must be the whole "
            + $"product ({RouteCount} × {ValueCount} × {SlotCount})");
    }

    /// <summary>
    /// Every route compiles a source no other route compiles. A matrix whose "two routes" run the
    /// same text measures one route twice and reports a coverage it does not have.
    /// </summary>
    [Fact]
    public void EveryRoute_CompilesADistinctSource()
    {
        var byRoute = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var route in Routes)
        {
            var sources = ApplicableCells
                .Where(c => (string)c[0] == route)
                .Select(c => ComposeCell(route, (string)c[1], (string)c[2]).Source)
                .ToList();

            sources.Should().NotBeEmpty($"route '{route}' must have at least one applicable cell");
            byRoute[route] = string.Join("\u0000", sources);
        }

        foreach (var (route, joined) in byRoute)
        {
            var twin = byRoute.FirstOrDefault(kv => kv.Key != route && kv.Value == joined);
            twin.Key.Should().BeNull(
                $"routes '{route}' and '{twin.Key}' compile identical sources, so one of them is "
                + "not measured at all");
        }
    }

    [Fact]
    public void RefuseUntypedVoidBinding_IsDeleted()
    {
        var repoRoot = Infrastructure.DispatchSiteScan.FindRepoRoot();
        var semanticDir = Path.Combine(repoRoot, "src", "Sharpy.Compiler", "Semantic");
        var files = Directory.GetFiles(semanticDir, "*.cs", SearchOption.AllDirectories);

        var hits = files.Where(f => File.ReadAllText(f).Contains("RefuseUntypedVoidBinding")).ToList();
        hits.Should().BeEmpty("the second refusal beside the seam is deleted");

        // Positive control: the same scan, for a name the seam DOES define, must find it — an
        // absence assertion over a directory that failed to enumerate would otherwise pass.
        var control = files.Where(f => File.ReadAllText(f).Contains("BestCommonType(")).ToList();
        control.Should().NotBeEmpty(
            "positive control: the scan reads real files, so the absence above is a measurement");
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
