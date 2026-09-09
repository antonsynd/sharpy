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
/// The matrix is the product of that contract's two axes: the 19 members of the seam's
/// <c>StorePosition</c> enum and the 24 value shapes the seam distinguishes.</para>
///
/// <para><b>Every cell executes.</b> An accepted cell compiles, runs, and prints the stored value —
/// a cell that only compiled would pass with the fact recorded and unapplied, which is the exact
/// defect this seam exists to close (the checker says float32, the emitter prints an unsuffixed
/// double). A refused cell asserts the diagnostic's CODE, the position's own message phrasing, the
/// LINE, and that exactly one diagnostic carries that code.</para>
///
/// <para><b>N/A cells.</b> Four cells at the ParameterDefault and LambdaParameterDefault positions
/// are refused by <c>DefaultParameterValidator</c> (SPY0401) before the value reaches the store
/// seam — they are tested in <c>ParameterDefaultConstantMatrixTests</c> instead. All known-red
/// cells have been drained (#1762 closed).</para>
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

    private const int PositionCount = 21;
    private const int ShapeCount = 24;
    private const int AcceptedCellCount = 248;
    private const int RefusedCellCount = 202;
    private const int KnownRedCellCount = 0;
    private const int NotApplicableCellCount = 54;

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
            (v, t) => $"Cannot assign type '{v}' to '{t}'"),

        new("Walrus",
            s => $"def main():\n    x: {s.Slot} = {s.Seed}\n    (x := {s.Value})\n    print(x)\n",
            3, DiagnosticCodes.Semantic.TypeMismatch,
            (v, t) => $"Cannot assign type '{v}' to variable of type '{t}'"),

        new("CollectionElement",
            s => $"def main():\n    xs: list[{s.Slot}] = [{s.Value}]\n    print(xs[0])\n",
            2, DiagnosticCodes.Semantic.TypeMismatch,
            (v, t) => $"Cannot assign type '{v}' to '{t}'"),

        new("LambdaBody",
            s => $"def main():\n    f: () -> {s.Slot} = lambda: {s.Value}\n    print(f())\n",
            2, DiagnosticCodes.Semantic.TypeMismatch,
            (v, t) => $"Arrow lambda body type '{v}' is not assignable to expected return type '{t}'"),

        new("Augmented",
            s => $"def main():\n    x: {s.Slot} = {s.Seed}\n    x += {s.Value}\n    print(x)\n",
            3, DiagnosticCodes.Semantic.TypeMismatch,
            (v, t) => $"Result type '{v}' of augmented assignment is not assignable to target type '{t}'"),

        // `??=` is a store into the LEFT slot (plan-757fbb Decision 6, #1767). The refusal names
        // the whole slot; a bare payload into an Optional slot is ACCEPTED here (the override below)
        // because that is the spec's own form (`x ??= 42` wraps as Some(42)).
        new("CoalesceAssign",
            s => $"def main():\n    x: {s.Slot} = {s.Seed}\n    x ??= {s.Value}\n    print(x)\n",
            3, DiagnosticCodes.Semantic.TypeMismatch,
            (v, t) => $"Cannot assign type '{v}' to '??=' target of type '{t}'"),

        // The RIGHT operand of a user dunder is a store into the selected overload's slot (#1719,
        // plan-499995 Design Decision 6). The refusal keeps the operator's code (SPY0222) and names
        // the dunder's slot; a bare-None operand never reaches the seam (the two N/A cells). No
        // `__hash__`: a typed `__eq__` is not the .NET `Equals(object)` override, and declaring the
        // hash alone is refused (SPY0456) before any operand is stored.
        new("OperatorOperand",
            s => $"class D:\n    def __eq__(self, other: {s.Slot}) -> bool:\n        print(other)\n        return True\n\ndef main():\n    d = D()\n    r: bool = d == {s.Value}\n",
            8, DiagnosticCodes.Semantic.InvalidBinaryOperation,
            (v, t) => $"Type 'D' does not support operator '==' with operand of type '{v}'; '__eq__' takes '{t}'"),

        // Fixed-slot positions: the store slot is fixed (not the shape's), so the shape axis creates
        // no meaningful variation — all cells are N/A. Tested in dedicated integration tests.
        new("FStringHole",
            s => $"def main():\n    print(f\"{{str({s.Value})}}\")\n",
            2, DiagnosticCodes.Semantic.TypeMismatch,
            (v, t) => $"Cannot assign type '{v}' to '{t}'"),

        new("TruthinessTest",
            s => $"def main():\n    if {s.Value}:\n        print(True)\n",
            2, DiagnosticCodes.Semantic.TypeMismatch,
            (v, t) => $"Cannot assign type '{v}' to '{t}'"),
    };

    // ── Fixed-slot positions: slot is fixed (object / Unknown), not the shape's ──────────────
    // All cells N/A: the shape axis creates no variation. Tested in f-string / truthiness
    // integration tests (#1743).
    private static readonly HashSet<string> FixedSlotPositions = new() { "FStringHole", "TruthinessTest" };

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

    // ── `??=` cells decided BEFORE the seam, and the two cells the seam decides differently ───
    // A left that cannot hold absence is refused SPY0222 before the RHS is checked (spec: "y is not
    // nullable or optional") — every shape whose slot is a plain `int8`/`float32`/`decimal`/
    // `LiteralString`/`int`. A bare `None` RHS keeps its operator refusal (SPY0222, "operand of type
    // 'None'"). Listed explicitly, so a shape added with a plain slot shows up as a red cell.

    private static readonly HashSet<string> CoalesceLeftRefusalCells = new()
    {
        "CoalesceAssign×InRangeIntConstant", "CoalesceAssign×OutOfRangeIntConstant",
        "CoalesceAssign×ConstReference", "CoalesceAssign×FoldedConstant",
        "CoalesceAssign×NegativeConstant", "CoalesceAssign×NegativeOutOfRangeConstant",
        "CoalesceAssign×FloatLiteralIntoFloat32", "CoalesceAssign×FloatLiteralIntoDecimal",
        "CoalesceAssign×OutOfRangeFloatLiteral",
        "CoalesceAssign×StringLiteralIntoLiteralString", "CoalesceAssign×ParenthesizedLiteralIntoLiteralString",
        "CoalesceAssign×ConcatLiteralIntoLiteralString", "CoalesceAssign×StrValueIntoLiteralString",
        "CoalesceAssign×NoneIntoNonNullable", "CoalesceAssign×OptionalIntoNonOptional",
    };

    private static readonly HashSet<string> CoalesceNoneRefusalCells = new()
    {
        "CoalesceAssign×BareNoneIntoOptional", "CoalesceAssign×NoneIntoNullable",
    };

    // At `??=` a bare payload into an Optional slot is the accepted form, and `None()` into a
    // PRESENT Optional is a no-op that keeps the seed — the two cells whose verdict or output differs
    // from the shape's plain-store row.
    private static readonly Dictionary<string, string> CoalesceAcceptedOverrides = new()
    {
        ["CoalesceAssign×BareValueIntoOptional"] = "42\n",
        ["CoalesceAssign×NoneCallIntoOptional"] = "1\n",
    };

    // ── N/A cells ────────────────────────────────────────────────────────────────────────────
    // These cells are refused by DefaultParameterValidator (SPY0401) BEFORE the value reaches the
    // store seam — the store conversion is never consulted, so the matrix declines to measure it.
    // Tested in ParameterDefaultConstantMatrixTests instead.

    private static readonly Dictionary<string, string> NotApplicableCells = new()
    {
        ["ParameterDefault×SomeIntoOptional"] = "refused by DefaultParameterValidator (SPY0401), not the store seam — tested in ParameterDefaultConstantMatrixTests",
        ["ParameterDefault×SomeConstantIntoNarrowOptional"] = "refused by DefaultParameterValidator (SPY0401), not the store seam — tested in ParameterDefaultConstantMatrixTests",
        ["LambdaParameterDefault×SomeIntoOptional"] = "refused by DefaultParameterValidator (SPY0401), not the store seam — tested in ParameterDefaultConstantMatrixTests",
        ["LambdaParameterDefault×SomeConstantIntoNarrowOptional"] = "refused by DefaultParameterValidator (SPY0401), not the store seam — tested in ParameterDefaultConstantMatrixTests",
        ["OperatorOperand×BareNoneIntoOptional"] = "a bare None operand is the #1079 null check (NoneCheck) on a class receiver, never a store — `d == None` prints False; the None-admitting dispatch is tested in DunderEqualitySynthesisMatrixTests",
        ["OperatorOperand×NoneIntoNonNullable"] = "a bare None operand is the #1079 null check (NoneCheck) on a class receiver, never a store — `d == None` prints False; the None-admitting dispatch is tested in DunderEqualitySynthesisMatrixTests",
    };

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
        if (FixedSlotPositions.Contains(p.Name))
            return Verdict.NotApplicable;
        var key = Key(p, s);
        if (NotApplicableCells.ContainsKey(key))
            return Verdict.NotApplicable;
        if (KnownRedCells.ContainsKey(key))
            return Verdict.KnownRed;
        if (OperatorRefusalCells.Contains(key)
            || CoalesceLeftRefusalCells.Contains(key)
            || CoalesceNoneRefusalCells.Contains(key))
            return Verdict.Refused;
        if (CoalesceAcceptedOverrides.ContainsKey(key))
            return Verdict.Accepted;
        return s.AcceptedOutput != null ? Verdict.Accepted : Verdict.Refused;
    }

    private static string AcceptedOutputOf(Position p, Shape s)
        => CoalesceAcceptedOverrides.TryGetValue(Key(p, s), out var overridden) ? overridden : s.AcceptedOutput!;

    /// <summary>The code and message a refused cell must carry — read off the two axis tables.</summary>
    private static (string Code, string Head, string Tail) RefusalOf(Position p, Shape s)
    {
        if (OperatorRefusalCells.Contains(Key(p, s)))
            return (DiagnosticCodes.Semantic.InvalidBinaryOperation,
                $"Type '{s.SlotType}' does not support operator '+=' with operand of type '{s.ValueType}'",
                "");

        if (CoalesceLeftRefusalCells.Contains(Key(p, s)))
            return (DiagnosticCodes.Semantic.InvalidBinaryOperation,
                $"Type '{s.SlotType}' does not support operator '??=': the target must be nullable "
                + $"('{s.SlotType} | None') or Optional ('{s.SlotType}?')",
                "");

        if (CoalesceNoneRefusalCells.Contains(Key(p, s)))
            return (DiagnosticCodes.Semantic.InvalidBinaryOperation,
                $"Type '{s.SlotType}' does not support operator '??=' with operand of type 'None'",
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
        result.StandardOutput.Should().Be(AcceptedOutputOf(p, s),
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
    [Theory(Skip = "All known-red cells drained (#1762 closed)")]
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
    /// The contract for the known-red cells, in full. Skipped while issues are open; deleting
    /// the Skip is how each issue is verified closed, and a row whose issue is fixed but whose
    /// entry survives fails <see cref="Matrix_IsTotalOverItsAxes"/>'s stale-key check only after
    /// the entry is removed — so the drain is: fix, unskip, delete the row.
    /// </summary>
    [Theory(Skip = "All known-red cells drained (#1762 closed)")]
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

    // ── Comprehension element hosts (#1776) ────────────────────────────────────────────────
    // Comprehension elements route through AdmitCollectionElements so the ELEMENT reports
    // the refusal at its own span — matching the literal twin ([v]).
    // The matrix is 4 hosts × 5 values = 20 cells (12 accepted, 8 refused).
    //
    // Mutation: drop the AdmitCollectionElements call in CheckListComprehension → the None
    // cell regresses to the container's SPY0220 ("Cannot assign type 'list[None]' to variable
    // of type 'list[int32]'" at the declaration), because ContextualElementType returns the
    // produced type on refusal.

    private sealed record CompHost(string Name, Func<string, string, string> Compose);

    private sealed record CompValue(
        string Name, string Slot, string Value, bool Accepted,
        string? RefusedCode = null, string? MessageFragment = null);

    private static readonly CompHost[] CompHosts =
    {
        new("ListComp",
            (slot, value) => $"def main():\n    xs: list[{slot}] = [{value} for _ in range(1)]\n    print(len(xs))\n"),
        new("SetComp",
            (slot, value) => $"def main():\n    xs: set[{slot}] = {{{value} for _ in range(1)}}\n    print(len(xs))\n"),
        new("DictKeyComp",
            (slot, value) => $"def main():\n    d: dict[{slot}, str] = {{{value}: \"v\" for _ in range(1)}}\n    print(len(d))\n"),
        new("DictValueComp",
            (slot, value) => $"def main():\n    d: dict[str, {slot}] = {{\"k\": {value} for _ in range(1)}}\n    print(len(d))\n"),
    };

    private static readonly CompValue[] CompValues =
    {
        new("None", "int", "None", false,
            DiagnosticCodes.Semantic.NullabilityViolation,
            "Cannot assign 'None' to non-nullable type 'int32'"),
        new("NoneCall", "int?", "None()", true),
        new("Some", "int?", "Some(1)", true),
        new("Mistyped", "int", "\"x\"", false,
            DiagnosticCodes.Semantic.TypeMismatch,
            "Cannot assign type 'str' to 'int32'"),
        new("PayloadConstant", "int8", "7", true),
    };

    public static IEnumerable<object[]> CompAcceptedCells
        => from h in CompHosts
           from v in CompValues
           where v.Accepted
           select new object[] { h.Name, v.Name };

    public static IEnumerable<object[]> CompRefusedCells
        => from h in CompHosts
           from v in CompValues
           where !v.Accepted
           select new object[] { h.Name, v.Name };

    [Theory]
    [MemberData(nameof(CompAcceptedCells))]
    public void ComprehensionElement_Accepted_CompilesAndRuns(string host, string value)
    {
        var h = CompHosts.Single(x => x.Name == host);
        var v = CompValues.Single(x => x.Name == value);
        var source = h.Compose(v.Slot, v.Value);

        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue(
            $"[{host} × {value}] must compile — the seam admits this shape. "
            + $"Diagnostics: {string.Join(" | ", result.CompilationErrors)}\n{source}");
    }

    [Theory]
    [MemberData(nameof(CompRefusedCells))]
    public void ComprehensionElement_Refused_ReportsAtElement(string host, string value)
    {
        var h = CompHosts.Single(x => x.Name == host);
        var v = CompValues.Single(x => x.Name == value);
        var source = h.Compose(v.Slot, v.Value);

        var result = CompileAndExecute(source);

        result.Success.Should().BeFalse(
            $"[{host} × {value}] must be refused\n{source}");

        var matching = result.RawDiagnostics.Where(d => d.Code == v.RefusedCode).ToList();
        matching.Should().HaveCount(1,
            $"[{host} × {value}] must report {v.RefusedCode} exactly once. Got: "
            + $"{string.Join(" | ", result.RawDiagnostics.Select(d => $"{d.Code}@{d.Line}: {d.Message}"))}\n{source}");

        matching[0].Line.Should().Be(2,
            $"[{host} × {value}] must report at the element (line 2), not at the container\n{source}");

        matching[0].Message.Should().Contain(v.MessageFragment!,
            $"[{host} × {value}] must phrase the refusal at the element level\n{source}");
    }

    [Fact]
    public void ComprehensionElementMatrix_IsTotalOverItsAxes()
    {
        CompHosts.Length.Should().Be(4, "4 comprehension hosts");
        CompValues.Length.Should().Be(5, "5 value shapes");
        CompHosts.Select(h => h.Name).Should().OnlyHaveUniqueItems();
        CompValues.Select(v => v.Name).Should().OnlyHaveUniqueItems();

        var accepted = CompAcceptedCells.Count();
        var refused = CompRefusedCells.Count();
        accepted.Should().Be(12, "3 accepted values × 4 hosts");
        refused.Should().Be(8, "2 refused values × 4 hosts");
        (accepted + refused).Should().Be(4 * 5, "the whole product");
    }

    // ── Callee-kind axis for the two argument positions (#1797, plan-499995 Phase 3) ────────
    // The argument seam must receive the CLOSED slot on every route that closes a generic
    // binding — not only at a non-generic callee (the ArgumentPositional / ArgumentKeyword rows).
    // Each kind composes the same 24 shapes against a `T` closed to the shape's slot by a
    // different binding source: the written type arguments of a def or a constructor, the
    // inference a sibling argument drives, a keyword into a written def, and the enclosing class's
    // base reference for `super().__init__`. The verdict, code, message head and steer are the
    // argument row's own, read off the same tables, so a route that admits what the row refuses
    // (`1` into an inferred `T?`) or refuses what it admits (`7` into a `T` closed to int8) is a
    // red cell. A refusal the INFERENCE makes (SPY0237: the value's own type conflicts with the
    // binding a sibling pinned) is a refusal by name too and is admitted for the inferred kinds;
    // SPY0908 never is.

    private const int CalleeKindCount = 6;
    private const int CalleeKindAcceptedCellCount = 78;
    private const int CalleeKindRefusedCellCount = 66;
    private const int InferenceRefusedCellCount = 6;

    /// <summary>
    /// The shape's slot split into the type ARGUMENT that closes <c>T</c> and the parameter
    /// spelling that wraps it: <c>int?</c> is <c>T?</c> closed at <c>int</c>, <c>int8 | None</c> is
    /// <c>T | None</c> closed at <c>int8</c>, a plain slot is <c>T</c> closed at itself. The
    /// modifier is the PARAMETER's, never the type argument's — that is what Phase 3 closes (#1797).
    /// </summary>
    private static (string TypeArg, string Formal) SplitSlot(Shape s)
        => s.Slot.EndsWith("?", StringComparison.Ordinal) ? (s.Slot[..^1], "T?")
         : s.Slot.EndsWith(" | None", StringComparison.Ordinal) ? (s.Slot[..^" | None".Length], "T | None")
         : (s.Slot, "T");

    /// <summary>A value of the closing type, to seed the sibling that drives inference.</summary>
    private static string SeedOf(string typeArg) => typeArg switch
    {
        "float32" or "decimal" => "0.0",
        "LiteralString" => "\"\"",
        _ => "0",
    };

    /// <summary>
    /// The inferred-kind cells that refuse through INFERENCE rather than the seam, written down:
    /// the value's own natural type (an int32 literal, a float64 literal) conflicts with the
    /// binding its sibling pinned (<c>int8 | None</c>, <c>decimal</c>), and unification refuses
    /// before any slot closes — SPY0237, by name; C# refuses the same programs with CS0411. Every
    /// other inferred cell must match its argument row exactly, so an inference that started
    /// refusing more (or admitting one of these) is a red cell, not a wider allowance.
    /// </summary>
    private static readonly HashSet<string> InferenceRefusedCells = new()
    {
        "InferredGenericDef×FloatLiteralIntoDecimal",
        "InferredGenericDef×OptionalIntoNullable",
        "InferredGenericDef×OptionalIntoNonOptional",
        "InferredGenericCtor×FloatLiteralIntoDecimal",
        "InferredGenericCtor×OptionalIntoNullable",
        "InferredGenericCtor×OptionalIntoNonOptional",
    };

    /// <summary>
    /// <c>T | None</c> closed at a VALUE type is the value type itself — C#'s rule for an
    /// unconstrained <c>T?</c> (there is no <c>Nullable&lt;T&gt;</c> to make), which Sharpy's
    /// substitution mirrors (the collapse arm in <c>TypeSubstitution</c>). So a shape that stores
    /// into an <c>int | None</c> slot behaves, through a <c>T | None</c> formal closed at int,
    /// exactly as its non-nullable twin: <c>None</c> is refused (SPY0229), an Optional is refused
    /// with the unwrap steer, a constant narrows. The program is still composed from the shape;
    /// only the expectation is the twin's.
    /// </summary>
    private static Shape EffectiveShape(Shape s)
    {
        var (typeArg, formal) = SplitSlot(s);
        if (formal != "T | None" || typeArg is not ("int" or "int8"))
            return s;
        return s.Name switch
        {
            "NoneIntoNullable" => Shp("NoneIntoNonNullable"),
            "OptionalIntoNullable" => Shp("OptionalIntoNonOptional"),
            "ConstantIntoNarrowNullable" => Shp("InRangeIntConstant"),
            _ => throw new InvalidOperationException(s.Name),
        };
    }

    /// <param name="Row">The argument position whose verdict, code and message this kind must match.</param>
    /// <param name="StoreLine">The line of the store within the composed program (past the shape's prelude).</param>
    private sealed record CalleeKind(string Name, string Row, Func<Shape, string> Compose, int StoreLine);

    private static readonly CalleeKind[] CalleeKinds =
    {
        new("ExplicitGenericDef", "ArgumentPositional",
            s => { var (a, f) = SplitSlot(s); return $"def f[T](x: {f}) -> None:\n    print(x)\n\ndef main():\n    f[{a}]({s.Value})\n"; }, 5),
        new("InferredGenericDef", "ArgumentPositional",
            s => { var (a, f) = SplitSlot(s); return $"def f[T](xs: list[T], x: {f}) -> None:\n    print(x)\n\ndef main():\n    xs: list[{a}] = [{SeedOf(a)}]\n    f(xs, {s.Value})\n"; }, 6),
        new("GenericCtor", "ArgumentPositional",
            s => { var (a, f) = SplitSlot(s); return $"class Box[T]:\n    def __init__(self, x: {f}) -> None:\n        print(x)\n\ndef main():\n    Box[{a}]({s.Value})\n"; }, 6),
        new("InferredGenericCtor", "ArgumentPositional",
            s => { var (a, f) = SplitSlot(s); return $"class Box[T]:\n    def __init__(self, xs: list[T], x: {f}) -> None:\n        print(x)\n\ndef main():\n    xs: list[{a}] = [{SeedOf(a)}]\n    Box(xs, {s.Value})\n"; }, 7),
        new("KeywordOnGenericDef", "ArgumentKeyword",
            s => { var (a, f) = SplitSlot(s); return $"def f[T](x: {f}) -> None:\n    print(x)\n\ndef main():\n    f[{a}](x={s.Value})\n"; }, 5),
        new("SuperInitIntoGenericBase", "ArgumentPositional",
            s => { var (a, f) = SplitSlot(s); return $"class Base[T]:\n    def __init__(self, x: {f}) -> None:\n        print(x)\n\nclass D(Base[{a}]):\n    def __init__(self) -> None:\n        super().__init__({s.Value})\n\ndef main():\n    D()\n"; }, 7),
    };

    public static IEnumerable<object[]> CalleeKindCells
        => from k in CalleeKinds
           from s in Shapes
           select new object[] { k.Name, s.Name };

    [Theory]
    [MemberData(nameof(CalleeKindCells))]
    public void CalleeKindCell_MatchesTheArgumentRow(string kind, string shape)
    {
        var k = CalleeKinds.Single(x => x.Name == kind);
        var s = Shp(shape);
        var row = Pos(k.Row);
        var source = s.Prelude + k.Compose(s);
        var label = $"[{kind} × {shape}]";
        // The verdict, output, code and message come from the shape the closed formal makes of it.
        var eff = EffectiveShape(s);

        var result = CompileAndExecute(source);
        var diagnostics = string.Join(" | ", result.RawDiagnostics.Select(d => $"{d.Code}@{d.Line}: {d.Message}"));

        result.RawDiagnostics.Should().NotContain(d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError,
            $"{label} must never reach Roslyn — the closed slot decides here: {diagnostics}\n{source}");

        if (InferenceRefusedCells.Contains($"{kind}×{shape}"))
        {
            result.Success.Should().BeFalse($"{label} is refused by inference (see InferenceRefusedCells); it printed '{result.StandardOutput}'\n{source}");
            result.RawDiagnostics.Should().Contain(d => d.Code == DiagnosticCodes.Semantic.CannotInferGenericType,
                $"{label} must refuse by name through inference: {diagnostics}\n{source}");
            return;
        }

        if (Classify(row, eff) == Verdict.Accepted)
        {
            result.Success.Should().BeTrue($"{label} must compile — the {k.Row} row admits this shape: {diagnostics}\n{source}");
            result.StandardOutput.Should().Be(AcceptedOutputOf(row, eff), $"{label} must print the stored value\n{source}");
            return;
        }

        result.Success.Should().BeFalse($"{label} must be refused — the {k.Row} row refuses this shape; it printed '{result.StandardOutput}'\n{source}");

        var (code, head, tail) = RefusalOf(row, eff);
        var seam = result.RawDiagnostics.Where(d => d.Code == code).ToList();
        seam.Should().HaveCount(1, $"{label} must report {code} exactly once, as the {k.Row} row does: {diagnostics}\n{source}");
        seam[0].Message.Should().Contain(head, $"{label} must phrase the refusal the way the {k.Row} row does\n{source}");
        if (tail.Length > 0)
            seam[0].Message.Should().Contain(tail, $"{label} must carry the shape's steer\n{source}");
        seam[0].Line.Should().Be(s.Prelude.Count(c => c == '\n') + k.StoreLine,
            $"{label} must be reported at the store, not at the enclosing statement\n{source}");
    }

    [Fact]
    public void CalleeKindMatrix_IsTotalOverItsAxes()
    {
        CalleeKinds.Length.Should().Be(CalleeKindCount);
        CalleeKinds.Select(k => k.Name).Should().OnlyHaveUniqueItems();
        CalleeKinds.Select(k => k.Row).Should().OnlyContain(r => r == "ArgumentPositional" || r == "ArgumentKeyword",
            "every kind is a route into one of the two argument positions");

        var accepted = CalleeKinds.Sum(k => Shapes.Count(s => Classify(Pos(k.Row), EffectiveShape(s)) == Verdict.Accepted));
        accepted.Should().Be(CalleeKindAcceptedCellCount, "13 admitted shapes × 6 kinds — NoneIntoNullable collapses to its non-nullable twin's refusal");
        (CalleeKinds.Length * Shapes.Length - accepted).Should().Be(CalleeKindRefusedCellCount, "11 refused shapes × 6 kinds");

        InferenceRefusedCells.Count.Should().Be(InferenceRefusedCellCount, "the inference-refused roster is a literal");
        foreach (var key in InferenceRefusedCells)
        {
            var parts = key.Split('×');
            CalleeKinds.Should().Contain(k => k.Name == parts[0] && k.Name.StartsWith("Inferred"), $"{key} names an inferred kind");
            Shapes.Should().Contain(sh => sh.Name == parts[1], $"{key} names a shape");
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

        foreach (var key in CoalesceLeftRefusalCells.Concat(CoalesceNoneRefusalCells).Concat(CoalesceAcceptedOverrides.Keys))
            product.Should().Contain(key, $"??= row '{key}' names no cell");

        foreach (var key in NotApplicableCells.Keys)
            product.Should().Contain(key, $"N/A row '{key}' names no cell");

        var accepted = AcceptedCells.Count();
        var refused = RefusedCells.Count();
        var red = KnownRedCellData.Count();
        var fixedSlotNA = FixedSlotPositions.Count * ShapeCount;
        var na = NotApplicableCells.Count + fixedSlotNA;

        accepted.Should().Be(AcceptedCellCount, "the accepted half is written down");
        refused.Should().Be(RefusedCellCount, "the refused half is written down");
        red.Should().Be(KnownRedCellCount, "known-red cells are drained (#1762 closed)");
        na.Should().Be(NotApplicableCellCount,
            "N/A cells are refused before the store seam or have a fixed slot that does not vary with the shape axis");

        (accepted + refused + red + na).Should().Be(PositionCount * ShapeCount,
            $"live ({accepted + refused}) + known-red ({red}) + N/A ({na}) must be the whole "
            + $"product ({PositionCount} × {ShapeCount})");
    }
}
