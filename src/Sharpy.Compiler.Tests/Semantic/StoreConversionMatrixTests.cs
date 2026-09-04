using System.Reflection;
using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// The store-conversion matrix — every store POSITION × every value SHAPE (#1706, #1698, #1688,
/// #1731, #1720, #1564; plan-14853b Phase 2 Task 3, rewritten by the 2026-09-04 remediation).
///
/// <para><b>Contract.</b> A value entering a typed slot is admitted or refused by ONE seam
/// (<c>TypeChecker.ClassifyStore</c>) consulted at every store position, against the DECLARED slot.
/// The matrix is the product of that contract's two axes: the 17 members of the seam's
/// <c>StorePosition</c> enum and the 24 value shapes the seam distinguishes.</para>
///
/// <para><b>Every cell executes.</b> An accepted cell compiles, runs, and prints the stored value —
/// a cell that only compiled would pass with the fact recorded and unapplied, which is the exact
/// defect this seam exists to close (the checker says float32, the emitter prints an unsuffixed
/// double). A refused cell asserts the diagnostic's CODE, the position's own message phrasing, the
/// LINE, and that exactly one diagnostic carries that code.</para>
///
/// <para><b>No cell is exempt.</b> <see cref="NotApplicableCells"/> is empty: every one of the 17
/// positions can host every one of the 24 shapes, so there is nothing the matrix declines to
/// measure. 17 cells are KNOWN RED against filed issues (#1759–#1763); their contract assertion is
/// skipped and drains when the issue closes, and the nine of them whose contract is a refusal are
/// still asserted live to be refused, so a regression to silent acceptance fails here.</para>
///
/// <para><b>Sibling harnesses.</b> <c>StorePositionReachTests</c> covers the routes that were found
/// bypassing the seam; <c>StoreSeamConformanceTests</c> is the Roslyn source scan that keeps the
/// decision in one place; <c>StoreTargetMatrixTests</c> covers declared-type binding shape.</para>
/// </summary>
[Collection("HeavyCompilation")]
public class StoreConversionMatrixTests : IntegrationTestBase
{
    public StoreConversionMatrixTests(ITestOutputHelper output) : base(output) { }

    // ── Axis sizes, anchored to literals ─────────────────────────────────────────────────────
    // A count derived from the same enum the roster is built from is vacuous — it can only agree
    // with itself. These are written down, and Positions_AreExactlyTheStorePositionEnum compares
    // the roster to the enum, so ADDING a StorePosition member fails here until its row is added.

    private const int PositionCount = 17;
    private const int ShapeCount = 24;
    private const int AcceptedCellCount = 225;
    private const int RefusedCellCount = 166;
    private const int KnownRedCellCount = 17;
    private const int NotApplicableCellCount = 0;

    // ── Axis 1: value shapes ─────────────────────────────────────────────────────────────────

    /// <param name="Slot">The declared slot type, as Sharpy source.</param>
    /// <param name="Seed">An initializer the slot accepts, for positions that store twice.</param>
    /// <param name="Value">The value expression under test.</param>
    /// <param name="Prelude">Module-level source the value needs (empty for most shapes).</param>
    /// <param name="ValueType">The value type as a diagnostic spells it.</param>
    /// <param name="SlotType">The slot type as a diagnostic spells it.</param>
    /// <param name="AcceptedOutput">stdout when the shape is admitted; null when it is refused.</param>
    /// <param name="RefusedCode">The code when the refusal is the same at every position.</param>
    /// <param name="RefusedMessage">That refusal's whole message.</param>
    /// <param name="SteerTail">A fragment the refusal's steer must carry, beyond the head.</param>
    private sealed record Shape(
        string Name,
        string Slot,
        string Seed,
        string Value,
        string Prelude,
        string ValueType,
        string SlotType,
        string? AcceptedOutput,
        string? RefusedCode = null,
        string? RefusedMessage = null,
        string SteerTail = "");

    private static readonly Shape[] Shapes =
    {
        // ── integer constant conversion (#1698) — the value is checked, not the spelling ──
        new("InRangeIntConstant", "int8", "0", "7", "", "int32", "int8", "7\n"),
        new("OutOfRangeIntConstant", "int8", "0", "300", "", "int32", "int8", null),
        new("ConstReference", "int8", "0", "K", "const K: int = 7\n\n", "int32", "int8", "7\n"),
        new("FoldedConstant", "int8", "0", "(1 << 6)", "", "int32", "int8", "64\n"),
        new("NegativeConstant", "int8", "0", "-1", "", "int32", "int8", "-1\n"),
        new("NegativeOutOfRangeConstant", "int8", "0", "-129", "", "int32", "int8", null),

        // ── float32 / decimal literal narrowing (#1688, Decision 6 ruled A) ──
        new("FloatLiteralIntoFloat32", "float32", "0.0", "0.5", "", "float64", "float32", "0.5\n"),
        new("FloatLiteralIntoDecimal", "decimal", "0.0", "1.5", "", "float64", "decimal", "1.5\n"),
        new("OutOfRangeFloatLiteral", "float32", "0.0", "1e40", "", "float64", "float32", null),

        // ── literal-derived strings (#1731, R-P) — three FORMS, each its own shape ──
        new("StringLiteralIntoLiteralString", "LiteralString", "\"\"", "\"a\"", "", "str", "LiteralString", "a\n"),
        new("ParenthesizedLiteralIntoLiteralString", "LiteralString", "\"\"", "(\"a\")", "", "str", "LiteralString", "a\n"),
        new("ConcatLiteralIntoLiteralString", "LiteralString", "\"\"", "\"a\" + \"b\"", "", "str", "LiteralString", "ab\n"),
        new("StrValueIntoLiteralString", "LiteralString", "\"\"", "sv()",
            "def sv() -> str:\n    return \"a\"\n\n", "str", "LiteralString", null),

        // ── strict Optional construction (#1720, R-G) ──
        new("BareValueIntoOptional", "int?", "None()", "42", "", "int32", "int32?", null,
            DiagnosticCodes.SemanticOverflow.StrictOptionalConstruction,
            "'int32' is not an Optional[int32]; construct it with Some(...)"),
        new("BareNoneIntoOptional", "int?", "None()", "None", "", "None", "int32?", null,
            DiagnosticCodes.SemanticOverflow.StrictOptionalConstruction,
            "bare None is not an Optional[int32]; use None(), or declare the slot 'int32 | None'"),
        new("SomeIntoOptional", "int?", "None()", "Some(42)", "", "int32?", "int32?", "42\n"),
        new("NoneCallIntoOptional", "int?", "Some(1)", "None()", "", "int32?", "int32?", "None\n"),
        new("NoneIntoNullable", "int | None", "1", "None", "", "None", "int32 | None", "None\n"),
        new("NoneIntoNonNullable", "int", "0", "None", "", "None", "int32", null,
            DiagnosticCodes.Semantic.NullabilityViolation,
            "Cannot assign 'None' to non-nullable type 'int32'"),
        new("NullableIntoOptional", "int?", "None()", "nv()",
            "def nv() -> int | None:\n    return None\n\n", "int32 | None", "int32?", null,
            null, null, "(C# nullability) and the slot is Optional[int32]; cross with 'maybe'"),
        new("OptionalIntoNullable", "int | None", "1", "ov()",
            "def ov() -> int?:\n    return Some(1)\n\n", "int32?", "int32 | None", null,
            null, null, "is Optional[int32]; narrow it ('if x is not None:') or unwrap it first"),
        new("OptionalIntoNonOptional", "int", "0", "ov()",
            "def ov() -> int?:\n    return Some(1)\n\n", "int32?", "int32", null,
            null, null, "is Optional[int32]; narrow it ('if x is not None:') or unwrap it first"),

        // ── the two shapes that cross the axes: a narrow slot UNDER an Optional / a nullable ──
        new("SomeConstantIntoNarrowOptional", "int8?", "None()", "Some(7)", "", "int8?", "int8?", "7\n"),
        new("ConstantIntoNarrowNullable", "int8 | None", "None", "7", "", "int32", "int8 | None", "7\n"),
    };

    // ── Axis 2: store positions ──────────────────────────────────────────────────────────────

    /// <param name="Compose">The whole program for a shape at this position.</param>
    /// <param name="StoreLine">The store's line within the program, past the shape's prelude.</param>
    /// <param name="MismatchCode">The code this position reports a plain type mismatch under.</param>
    /// <param name="MismatchMessage">How this position phrases it, given (valueType, slotType).</param>
    /// <param name="ReportsAtContainerLevel">
    /// True where the refusal is re-raised by the enclosing container's own assignability check, so
    /// the message names the container pair and the seam's steer is dropped — #1759. The message
    /// template below is the container form; the cells where the container also changes the CODE
    /// are KNOWN RED, not modelled here.
    /// </param>
    private sealed record Position(
        string Name,
        Func<Shape, string> Compose,
        int StoreLine,
        string MismatchCode,
        Func<string, string, string> MismatchMessage,
        bool ReportsAtContainerLevel = false);

    private static readonly Position[] Positions =
    {
        new("Declaration",
            s => $"def main():\n    x: {s.Slot} = {s.Value}\n    print(x)\n",
            2, DiagnosticCodes.Semantic.TypeMismatch,
            (v, t) => $"Cannot assign type '{v}' to variable of type '{t}'"),

        new("PlainStore",
            s => $"def main():\n    x: {s.Slot} = {s.Seed}\n    x = {s.Value}\n    print(x)\n",
            3, DiagnosticCodes.Semantic.TypeMismatch,
            (v, t) => $"Cannot assign type '{v}' to variable of type '{t}'"),

        new("MemberStore",
            s => $"class C:\n    v: {s.Slot} = {s.Seed}\n\ndef main():\n    c: C = C()\n    c.v = {s.Value}\n    print(c.v)\n",
            6, DiagnosticCodes.Semantic.TypeMismatch,
            (v, t) => $"Cannot assign type '{v}' to '{t}'"),

        new("IndexStore",
            s => $"def main():\n    xs: list[{s.Slot}] = [{s.Seed}]\n    xs[0] = {s.Value}\n    print(xs[0])\n",
            3, DiagnosticCodes.Semantic.TypeMismatch,
            (v, t) => $"Cannot assign type '{v}' to '{t}'"),

        new("DictStore",
            s => $"def main():\n    d: dict[str, {s.Slot}] = {{\"k\": {s.Seed}}}\n    d[\"k\"] = {s.Value}\n    print(d[\"k\"])\n",
            3, DiagnosticCodes.Semantic.TypeMismatch,
            (v, t) => $"Cannot assign type '{v}' to '{t}'"),

        new("Return",
            s => $"def f() -> {s.Slot}:\n    return {s.Value}\n\ndef main():\n    print(f())\n",
            2, DiagnosticCodes.Semantic.MissingReturnValue,
            (v, t) => $"Cannot return type '{v}' from function expecting '{t}'"),

        new("Yield",
            s => $"def g() -> {s.Slot}:\n    yield {s.Value}\n\ndef main():\n    for v in g():\n        print(v)\n",
            2, DiagnosticCodes.Semantic.TypeMismatch,
            (v, t) => $"Yielded type '{v}' is not assignable to declared return type '{t}'"),

        new("ParameterDefault",
            s => $"def f(x: {s.Slot} = {s.Value}) -> None:\n    print(x)\n\ndef main():\n    f()\n",
            1, DiagnosticCodes.Semantic.TypeMismatch,
            (v, t) => $"Default value type '{v}' is not assignable to parameter type '{t}'"),

        new("LambdaParameterDefault",
            s => $"def main():\n    f = lambda x: {s.Slot} = {s.Value}: x\n    print(f())\n",
            2, DiagnosticCodes.Semantic.TypeMismatch,
            (v, t) => $"Default value of type '{v}' is not assignable to parameter type '{t}'"),

        new("PropertyDefault",
            s => $"class C:\n    v: {s.Slot} = {s.Value}\n\ndef main():\n    print(C().v)\n",
            2, DiagnosticCodes.Semantic.TypeMismatch,
            (v, t) => $"Cannot assign type '{v}' to variable of type '{t}'"),

        new("ArgumentPositional",
            s => $"def f(x: {s.Slot}) -> None:\n    print(x)\n\ndef main():\n    f({s.Value})\n",
            5, DiagnosticCodes.Semantic.TypeMismatch,
            (v, t) => $"Cannot pass argument of type '{v}' to parameter of type '{t}'"),

        new("ArgumentKeyword",
            s => $"def f(x: {s.Slot}) -> None:\n    print(x)\n\ndef main():\n    f(x={s.Value})\n",
            5, DiagnosticCodes.Semantic.TypeMismatch,
            (v, t) => $"Cannot pass argument of type '{v}' to parameter 'x' of type '{t}'"),

        new("TupleElement",
            s => $"def main():\n    t: tuple[{s.Slot}, int] = ({s.Value}, 1)\n    print(t[0])\n",
            2, DiagnosticCodes.Semantic.TypeMismatch,
            (v, t) => $"Cannot assign type 'tuple[{v}, int32]' to variable of type 'tuple[{t}, int32]'",
            ReportsAtContainerLevel: true),

        new("Walrus",
            s => $"def main():\n    x: {s.Slot} = {s.Seed}\n    (x := {s.Value})\n    print(x)\n",
            3, DiagnosticCodes.Semantic.TypeMismatch,
            (v, t) => $"Cannot assign type '{v}' to variable of type '{t}'"),

        new("CollectionElement",
            s => $"def main():\n    xs: list[{s.Slot}] = [{s.Value}]\n    print(xs[0])\n",
            2, DiagnosticCodes.Semantic.TypeMismatch,
            (v, t) => $"Cannot assign type 'list[{v}]' to variable of type 'list[{t}]'",
            ReportsAtContainerLevel: true),

        new("LambdaBody",
            s => $"def main():\n    f: () -> {s.Slot} = lambda: {s.Value}\n    print(f())\n",
            2, DiagnosticCodes.Semantic.TypeMismatch,
            (v, t) => $"Cannot assign type '() -> {v}' to variable of type '() -> {t}'",
            ReportsAtContainerLevel: true),

        new("Augmented",
            s => $"def main():\n    x: {s.Slot} = {s.Seed}\n    x += {s.Value}\n    print(x)\n",
            3, DiagnosticCodes.Semantic.TypeMismatch,
            (v, t) => $"Result type '{v}' of augmented assignment is not assignable to target type '{t}'"),
    };

    // ── Cells whose refusal is decided BEFORE any store ───────────────────────────────────────
    // `+=` on an Optional or nullable slot has no operator to resolve, so the value never reaches
    // the seam: SPY0222 is the honest answer and the store's own verdict is unreachable here.
    // Listed explicitly rather than derived, so an operator added later shows up as a red cell.

    private static readonly HashSet<string> OperatorRefusalCells = new()
    {
        "Augmented×BareValueIntoOptional", "Augmented×BareNoneIntoOptional",
        "Augmented×SomeIntoOptional", "Augmented×NoneCallIntoOptional",
        "Augmented×NoneIntoNullable", "Augmented×NoneIntoNonNullable",
        "Augmented×NullableIntoOptional", "Augmented×OptionalIntoNullable",
        "Augmented×OptionalIntoNonOptional", "Augmented×SomeConstantIntoNarrowOptional",
        "Augmented×ConstantIntoNarrowNullable",
    };

    // ── N/A cells ────────────────────────────────────────────────────────────────────────────
    // Empty, and that is the measured result: every one of the 17 positions can host every one of
    // the 24 shapes as a running program. A reason such as "same conversion as Declaration" or
    // "no declared target" would assume the property under test, so no such row exists.

    private static readonly Dictionary<string, string> NotApplicableCells = new();

    // ── Known-red cells ──────────────────────────────────────────────────────────────────────

    private enum RedContract
    {
        /// <summary>The cell must produce this code and message.</summary>
        Code,

        /// <summary>The cell must compile and print the stored value.</summary>
        Accepted,

        /// <summary>Accept or refuse is an open ruling; SPY0908 is not an answer either way.</summary>
        NotAnIce,
    }

    private sealed record KnownRed(
        string Issue,
        string Observed,
        RedContract Contract,
        string? Code = null,
        string? Message = null,
        string? Output = null);

    private static readonly Dictionary<string, KnownRed> KnownRedCells = new()
    {
        // #1759 — the element's refusal is re-raised by the container, which also changes the CODE.
        ["TupleElement×BareValueIntoOptional"] = new("#1759", "SPY0220 naming the tuple pair",
            RedContract.Code, DiagnosticCodes.SemanticOverflow.StrictOptionalConstruction,
            "'int32' is not an Optional[int32]; construct it with Some(...)"),
        ["TupleElement×BareNoneIntoOptional"] = new("#1759", "SPY0220 naming the tuple pair",
            RedContract.Code, DiagnosticCodes.SemanticOverflow.StrictOptionalConstruction,
            "bare None is not an Optional[int32]; use None(), or declare the slot 'int32 | None'"),
        ["TupleElement×NoneIntoNonNullable"] = new("#1759", "SPY0220 naming the tuple pair",
            RedContract.Code, DiagnosticCodes.Semantic.NullabilityViolation,
            "Cannot assign 'None' to non-nullable type 'int32'"),
        ["CollectionElement×BareValueIntoOptional"] = new("#1759", "SPY0220 naming the list pair",
            RedContract.Code, DiagnosticCodes.SemanticOverflow.StrictOptionalConstruction,
            "'int32' is not an Optional[int32]; construct it with Some(...)"),
        ["LambdaBody×BareValueIntoOptional"] = new("#1759", "SPY0220 naming the function-type pair",
            RedContract.Code, DiagnosticCodes.SemanticOverflow.StrictOptionalConstruction,
            "'int32' is not an Optional[int32]; construct it with Some(...)"),

        // #1760 — an all-`None` list literal infers its element type without the declared slot.
        ["CollectionElement×BareNoneIntoOptional"] = new("#1760",
            "SPY0227 'Cannot infer element type from a list of only None'",
            RedContract.Code, DiagnosticCodes.SemanticOverflow.StrictOptionalConstruction,
            "bare None is not an Optional[int32]; use None(), or declare the slot 'int32 | None'"),
        ["CollectionElement×NoneIntoNonNullable"] = new("#1760",
            "SPY0227 'Cannot infer element type from a list of only None'",
            RedContract.Code, DiagnosticCodes.Semantic.NullabilityViolation,
            "Cannot assign 'None' to non-nullable type 'int32'"),

        // #1761 — `lambda: None` emits a C# lambda with no return on any path.
        ["LambdaBody×BareNoneIntoOptional"] = new("#1761", "SPY0908 / CS1643 in Func<Optional<int>>",
            RedContract.Code, DiagnosticCodes.SemanticOverflow.StrictOptionalConstruction,
            "bare None is not an Optional[int32]; use None(), or declare the slot 'int32 | None'"),
        ["LambdaBody×NoneIntoNonNullable"] = new("#1761", "SPY0908 / CS1643 in Func<int>",
            RedContract.Code, DiagnosticCodes.Semantic.NullabilityViolation,
            "Cannot assign 'None' to non-nullable type 'int32'"),

        // #1762 — an Optional constructor as a parameter default reaches C# as a non-constant.
        // Refuse-or-lower is the open ruling, so the contract asserted here is only "not an ICE".
        ["ParameterDefault×SomeIntoOptional"] = new("#1762", "SPY0908 / CS1736", RedContract.NotAnIce),
        ["ParameterDefault×SomeConstantIntoNarrowOptional"] = new("#1762", "SPY0908 / CS1736", RedContract.NotAnIce),
        ["LambdaParameterDefault×SomeIntoOptional"] = new("#1762", "SPY0908 / CS1736", RedContract.NotAnIce),
        ["LambdaParameterDefault×SomeConstantIntoNarrowOptional"] = new("#1762", "SPY0908 / CS1736", RedContract.NotAnIce),
        ["LambdaParameterDefault×NoneCallIntoOptional"] = new("#1762", "SPY0908 / CS1736", RedContract.NotAnIce),

        // #1763 — the walrus does not push its target's declared slot as the expected type, so the
        // Optional constructors infer against whatever the enclosing expression expects.
        ["Walrus×SomeIntoOptional"] = new("#1763", "SPY0230 'Some must be called as a function'",
            RedContract.Accepted, Output: "42\n"),
        ["Walrus×NoneCallIntoOptional"] = new("#1763", "SPY0244 'None() can only construct Optional types'",
            RedContract.Accepted, Output: "None\n"),
        ["Walrus×SomeConstantIntoNarrowOptional"] = new("#1763", "SPY0230 'Some must be called as a function'",
            RedContract.Accepted, Output: "7\n"),
    };

    // ── Cell resolution ──────────────────────────────────────────────────────────────────────

    private static string Key(Position p, Shape s) => $"{p.Name}×{s.Name}";

    private static Position Pos(string name) => Positions.Single(p => p.Name == name);

    private static Shape Shp(string name) => Shapes.Single(s => s.Name == name);

    private static string Source(Position p, Shape s) => s.Prelude + p.Compose(s);

    private static int ExpectedLine(Position p, Shape s)
        => s.Prelude.Count(c => c == '\n') + p.StoreLine;

    private enum Verdict { Accepted, Refused, KnownRed, NotApplicable }

    private static Verdict Classify(Position p, Shape s)
    {
        var key = Key(p, s);
        if (NotApplicableCells.ContainsKey(key)) return Verdict.NotApplicable;
        if (KnownRedCells.ContainsKey(key)) return Verdict.KnownRed;
        if (OperatorRefusalCells.Contains(key)) return Verdict.Refused;
        return s.AcceptedOutput != null ? Verdict.Accepted : Verdict.Refused;
    }

    /// <summary>The code and message a refused cell must carry — read off the two axis tables.</summary>
    private static (string Code, string Head, string Tail) RefusalOf(Position p, Shape s)
    {
        if (OperatorRefusalCells.Contains(Key(p, s)))
            return (DiagnosticCodes.Semantic.InvalidBinaryOperation,
                $"Type '{s.SlotType}' does not support operator '+=' with operand of type '{s.ValueType}'",
                "");

        if (s.RefusedCode != null)
            return (s.RefusedCode, s.RefusedMessage!, "");

        return (p.MismatchCode,
            p.MismatchMessage(s.ValueType, s.SlotType),
            p.ReportsAtContainerLevel ? "" : s.SteerTail);
    }

    private static IEnumerable<object[]> CellsWhere(Verdict verdict)
        => from p in Positions
           from s in Shapes
           where Classify(p, s) == verdict
           select new object[] { p.Name, s.Name };

    public static IEnumerable<object[]> AcceptedCells => CellsWhere(Verdict.Accepted);

    public static IEnumerable<object[]> RefusedCells => CellsWhere(Verdict.Refused);

    public static IEnumerable<object[]> KnownRedCellData => CellsWhere(Verdict.KnownRed);

    public static IEnumerable<object[]> KnownRedRefusalCells
        => CellsWhere(Verdict.KnownRed)
            .Where(row => KnownRedCells[$"{row[0]}×{row[1]}"].Contract == RedContract.Code);

    // ── The cells ────────────────────────────────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(AcceptedCells))]
    public void AcceptedCell_RunsAndPrintsTheStoredValue(string position, string shape)
    {
        var p = Pos(position);
        var s = Shp(shape);
        var source = Source(p, s);

        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue(
            $"[{position} × {shape}] must compile — the seam admits this shape at every position. "
            + $"Diagnostics: {string.Join(" | ", result.CompilationErrors)}\n{source}");
        result.StandardOutput.Should().Be(s.AcceptedOutput,
            $"[{position} × {shape}] prints the value that was stored, so a fact recorded but never "
            + $"applied fails here\n{source}");
    }

    [Theory]
    [MemberData(nameof(RefusedCells))]
    public void RefusedCell_CarriesTheCodeMessageAndLine(string position, string shape)
    {
        var p = Pos(position);
        var s = Shp(shape);
        var source = Source(p, s);
        var (code, head, tail) = RefusalOf(p, s);

        var result = CompileAndExecute(source);

        result.Success.Should().BeFalse(
            $"[{position} × {shape}] must be refused; it printed "
            + $"'{result.StandardOutput}'\n{source}");

        var matching = result.RawDiagnostics.Where(d => d.Code == code).ToList();
        matching.Should().HaveCount(1,
            $"[{position} × {shape}] must report {code} exactly once. Got: "
            + $"{string.Join(" | ", result.RawDiagnostics.Select(d => $"{d.Code}@{d.Line}: {d.Message}"))}\n{source}");

        matching[0].Message.Should().Contain(head,
            $"[{position} × {shape}] must phrase the refusal the way this position does\n{source}");

        if (tail.Length > 0)
        {
            matching[0].Message.Should().Contain(tail,
                $"[{position} × {shape}] must carry the shape's steer\n{source}");
        }

        matching[0].Line.Should().Be(ExpectedLine(p, s),
            $"[{position} × {shape}] must be reported at the store, not at the enclosing "
            + $"statement\n{source}");
    }

    /// <summary>
    /// The half of the known-red set whose contract is a REFUSAL stays executing: the code they
    /// carry today is the wrong one (#1759, #1760, #1761), but a regression that ADMITS the value
    /// would be a soundness hole, and this cell catches it while the issue is open.
    /// </summary>
    [Theory]
    [MemberData(nameof(KnownRedRefusalCells))]
    public void KnownRedRefusal_IsStillRefused(string position, string shape)
    {
        var p = Pos(position);
        var s = Shp(shape);
        var source = Source(p, s);
        var red = KnownRedCells[Key(p, s)];

        var result = CompileAndExecute(source);

        result.Success.Should().BeFalse(
            $"[{position} × {shape}] must not be admitted. It is known red under {red.Issue} "
            + $"(observed: {red.Observed}) because the CODE is wrong, not because the value is "
            + $"legal\n{source}");
    }

    /// <summary>
    /// The contract for the known-red cells, in full. Skipped while #1759–#1763 are open; deleting
    /// the Skip is how each issue is verified closed, and a row whose issue is fixed but whose
    /// entry survives fails <see cref="Matrix_IsTotalOverItsAxes"/>'s stale-key check only after
    /// the entry is removed — so the drain is: fix, unskip, delete the row.
    /// </summary>
    [Theory(Skip = "Known red: #1759 (container re-reports the element's refusal), #1760 (all-None "
        + "list literal ignores the declared element type), #1761 (lambda: None ICEs CS1643), "
        + "#1762 (Optional constructor as a parameter default ICEs CS1736), #1763 (walrus does not "
        + "push its declared slot as the expected type)")]
    [MemberData(nameof(KnownRedCellData))]
    public void KnownRedCell_MeetsTheSeamContract(string position, string shape)
    {
        var p = Pos(position);
        var s = Shp(shape);
        var source = Source(p, s);
        var red = KnownRedCells[Key(p, s)];

        var result = CompileAndExecute(source);

        switch (red.Contract)
        {
            case RedContract.Accepted:
                result.Success.Should().BeTrue(
                    $"[{position} × {shape}] ({red.Issue}) must compile. Diagnostics: "
                    + $"{string.Join(" | ", result.CompilationErrors)}\n{source}");
                result.StandardOutput.Should().Be(red.Output, $"[{position} × {shape}]\n{source}");
                break;

            case RedContract.Code:
                result.Success.Should().BeFalse($"[{position} × {shape}] ({red.Issue})\n{source}");
                result.RawDiagnostics.Should().ContainSingle(d => d.Code == red.Code,
                    $"[{position} × {shape}] ({red.Issue}) must report {red.Code}; observed "
                    + $"{red.Observed}\n{source}");
                result.RawDiagnostics.Single(d => d.Code == red.Code).Message.Should().Contain(red.Message!);
                break;

            case RedContract.NotAnIce:
                result.RawDiagnostics.Should().NotContain(
                    d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError,
                    $"[{position} × {shape}] ({red.Issue}) must be answered at semantic time — "
                    + $"accept or refuse is an open ruling, an ICE is neither. Observed "
                    + $"{red.Observed}\n{source}");
                break;
        }
    }

    // ── Totality ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The roster is the seam's own enum. A new <c>StorePosition</c> member fails here until its
    /// row is written, which is the only thing that stops a position from being silently forgotten
    /// — the count alone cannot, because a count taken from the same enum agrees with itself.
    /// </summary>
    [Fact]
    public void Positions_AreExactlyTheStorePositionEnum()
    {
        var enumType = typeof(Sharpy.Compiler.Semantic.TypeChecker)
            .GetNestedType("StorePosition", BindingFlags.NonPublic | BindingFlags.Public);

        enumType.Should().NotBeNull(
            "the matrix's position axis is the seam's StorePosition enum; if it moved, this test "
            + "is measuring nothing");

        var members = Enum.GetNames(enumType!);

        Positions.Select(p => p.Name).Should().BeEquivalentTo(members,
            "every store position the seam knows must have a matrix row, and the matrix must not "
            + "invent positions the seam does not have");
        members.Length.Should().Be(PositionCount,
            "the axis size is written down, not derived — a new StorePosition must fail here");
    }

    [Fact]
    public void Matrix_IsTotalOverItsAxes()
    {
        Positions.Length.Should().Be(PositionCount);
        Shapes.Length.Should().Be(ShapeCount);
        Positions.Select(p => p.Name).Should().OnlyHaveUniqueItems();
        Shapes.Select(s => s.Name).Should().OnlyHaveUniqueItems();

        var product = (from p in Positions from s in Shapes select Key(p, s)).ToHashSet();
        product.Count.Should().Be(PositionCount * ShapeCount);

        foreach (var key in KnownRedCells.Keys)
        {
            product.Should().Contain(key,
                $"known-red row '{key}' names no cell — a stale entry left behind by a renamed "
                + "axis member hides a cell that is no longer measured");
        }

        foreach (var key in OperatorRefusalCells)
            product.Should().Contain(key, $"operator-refusal row '{key}' names no cell");

        foreach (var key in NotApplicableCells.Keys)
            product.Should().Contain(key, $"N/A row '{key}' names no cell");

        var accepted = AcceptedCells.Count();
        var refused = RefusedCells.Count();
        var red = KnownRedCellData.Count();
        var na = NotApplicableCells.Count;

        accepted.Should().Be(AcceptedCellCount, "the accepted half is written down");
        refused.Should().Be(RefusedCellCount, "the refused half is written down");
        red.Should().Be(KnownRedCellCount, "known-red cells drain as #1759–#1763 close");
        na.Should().Be(NotApplicableCellCount,
            "every position can host every shape; an exemption here would be a claim that needs a "
            + "reason which is not the property under test");

        (accepted + refused + red + na).Should().Be(PositionCount * ShapeCount,
            $"live ({accepted + refused}) + known-red ({red}) + N/A ({na}) must be the whole "
            + $"product ({PositionCount} × {ShapeCount})");
    }
}
