using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// The store seam's REACH: every position that decides a value's admission must consult the one
/// decision AND apply the accepted verdict's side effects (plan-14853b Decision 1).
///
/// <para>Distinct from <c>StoreConversionMatrixTests</c>, which is the position × shape product.
/// This class covers the routes that were found bypassing the seam after that matrix landed — the
/// ones that called <c>ClassifyStore</c> with a null value node, re-implemented its arms, or
/// classified without applying. Every cell EXECUTES: an accepted cell prints a value the refused
/// program could not print, and a refused cell asserts code, message and count.</para>
///
/// <para>Cells that print a value are the falsifiable half. A cell that only compiled would pass
/// with the fact recorded and unapplied — which is the exact defect (the checker says float32, the
/// emitter prints an unsuffixed double) — so the assertion is always on stdout.</para>
/// </summary>
[Collection("HeavyCompilation")]
public class StorePositionReachTests : IntegrationTestBase
{
    public StorePositionReachTests(ITestOutputHelper output) : base(output) { }

    /// <summary>
    /// Item 1: collection-literal elements. Every arm passed types only, so the constant,
    /// float32/decimal and literal-derived arms could never fire for an element.
    /// </summary>
    public static IEnumerable<object[]> CollectionElementCells => new[]
    {
        new object[] { "list-int8", "xs: list[int8] = [1, 2]\n    print(xs[0])", "1\n" },
        new object[] { "set-int8", "s: set[int8] = {1, 2}\n    print(len(s))", "2\n" },
        new object[] { "dict-value-int8", "d: dict[str, int8] = {\"a\": 1}\n    print(d[\"a\"])", "1\n" },
        new object[] { "dict-key-int8", "d: dict[int8, str] = {1: \"a\"}\n    print(d[1])", "a\n" },
        new object[] { "tuple-int8", "t: tuple[int8, int8] = (1, 2)\n    print(t[0])", "1\n" },
        new object[] { "tuple-literalstring", "t: tuple[LiteralString, int8] = (\"a\", 1)\n    print(t[0])", "a\n" },
        new object[] { "list-float32", "xs: list[float32] = [0.5]\n    print(xs[0])", "0.5\n" },
        new object[] { "set-float32", "s: set[float32] = {0.5}\n    print(len(s))", "1\n" },
        new object[] { "list-decimal", "xs: list[decimal] = [1.5]\n    print(xs[0])", "1.5\n" },
        new object[] { "list-literalstring", "xs: list[LiteralString] = [\"a\"]\n    print(xs[0])", "a\n" },
        new object[] { "nested-list-int8", "xs: list[list[int8]] = [[1]]\n    print(xs[0][0])", "1\n" },
        new object[] { "comprehension-int8", "xs: list[int8] = [7 for _ in range(1)]\n    print(xs[0])", "7\n" },
    };

    [Theory]
    [MemberData(nameof(CollectionElementCells))]
    public void CollectionElement_AdmitsTheValueShapeAndAppliesIt(string cell, string body, string expected)
        => AssertPrints(cell, body, expected);

    /// <summary>
    /// Item 2: conditional-of-constants. Admitted iff BOTH branches are, and an integer branch
    /// carries a per-branch cast fact because C# gives `c ? 7 : 8` the natural type int.
    /// </summary>
    public static IEnumerable<object[]> ConditionalCells => new[]
    {
        new object[] { "int8", "c: bool = True\n    x: int8 = 0\n    x = 7 if c else 8\n    print(x)", "7\n" },
        new object[] { "float32", "c: bool = True\n    x: float32 = 0.5 if c else 0.25\n    print(x)", "0.5\n" },
        new object[] { "decimal", "c: bool = True\n    d: decimal = 1.5 if c else 2.5\n    print(d)", "1.5\n" },
        new object[] { "literalstring", "c: bool = True\n    s: LiteralString = \"a\" if c else \"b\"\n    print(s)", "a\n" },
        new object[] { "variable-and-constant", "c: bool = True\n    x: int8 = 3\n    y: int8 = x if c else 7\n    print(y)", "3\n" },
        new object[] { "nested", "c: bool = True\n    d: bool = False\n    x: int8 = 1 if c else (2 if d else 3)\n    print(x)", "1\n" },
    };

    [Theory]
    [MemberData(nameof(ConditionalCells))]
    public void ConditionalOfConstants_AdmitsBothBranches(string cell, string body, string expected)
        => AssertPrints(cell, body, expected);

    /// <summary>
    /// Item 3: an unannotated lambda under a typed target takes the expected return type as its
    /// body's slot, so the declaration's function-type comparison agrees.
    /// </summary>
    [Theory]
    [InlineData("int8", "f: () -> int8 = lambda: 7\n    print(f())", "7\n")]
    [InlineData("float32", "f: () -> float32 = lambda: 0.5\n    print(f())", "0.5\n")]
    [InlineData("literalstring", "f: () -> LiteralString = lambda: \"a\"\n    print(f())", "a\n")]
    public void LambdaBody_TakesTheExpectedReturnTypeAsItsSlot(string cell, string body, string expected)
        => AssertPrints(cell, body, expected);

    /// <summary>
    /// Item 4: the argument routes. Acceptance was a parallel predicate and the accepted verdict
    /// was applied at none of the five binding routes.
    /// </summary>
    [Fact]
    public void KeywordArgument_AppliesTheAcceptedVerdict()
    {
        AssertPrints("kw-float32",
            "print(takes_small(x=0.5))", "0.5\n",
            prelude: "def takes_small(x: float32) -> float32:\n    return x\n\n");
        AssertPrints("kw-decimal",
            "print(takes_money(x=1.5))", "1.5\n",
            prelude: "def takes_money(x: decimal) -> decimal:\n    return x\n\n");
        AssertPrints("kw-literalstring",
            "print(takes_literal(s=\"a\"))", "a\n",
            prelude: "def takes_literal(s: LiteralString) -> str:\n    return s\n\n");
        AssertPrints("kw-int8",
            "print(takes_narrow(n=7))", "7\n",
            prelude: "def takes_narrow(n: int8) -> int8:\n    return n\n\n");
    }

    [Fact]
    public void ClrArgumentRoutes_ApplyTheAcceptedVerdict()
    {
        AssertPrints("clr-method-float32",
            "xs: list[float32] = []\n    xs.append(0.0)\n    print(xs[0])", "0.0\n");
        AssertPrints("clr-constructor-float32",
            "v: System.Numerics.Vector2 = System.Numerics.Vector2(1.0, 2.0)\n    print(v.X)", "1.0\n",
            prelude: "import System.Numerics\n\n");
    }

    /// <summary>
    /// R-G (#1720): the strict-Optional refusal reaches the ARGUMENT positions too, with the same
    /// code and the same steers it has at a declaration.
    /// </summary>
    [Fact]
    public void StrictOptionalRefusal_ReachesArgumentPositions()
    {
        AssertRefused("bare-value-positional",
            "print(takes(7))",
            prelude: "def takes(o: int8?) -> str:\n    return \"x\"\n\n",
            code: "SPY0604",
            message: "'int32' is not an Optional[int8]; construct it with Some(...)");

        AssertRefused("bare-none-keyword",
            "print(takes(o=None))",
            prelude: "def takes(o: int?) -> str:\n    return \"x\"\n\n",
            code: "SPY0604",
            message: "bare None is not an Optional[int32]; use None(), or declare the slot 'int32 | None'");
    }

    /// <summary>
    /// Item 5: a tuple-unpacking element is a store into its target's DECLARED slot, and item 6:
    /// a `T | None` target measures the value shape against its underlying type.
    /// </summary>
    [Fact]
    public void TupleUnpackingElement_IsAStoreIntoTheDeclaredSlot()
    {
        AssertPrints("tuple-unpack-int8-float32",
            "a: int8 = 0\n    b: float32 = 0.0\n    a, b = 1, 2.5\n    print(a)\n    print(b)", "1\n2.5\n");

        AssertRefused("tuple-unpack-mistyped",
            "a: int = 0\n    b: int = 0\n    a, b = \"x\", 3\n    print(a)",
            code: "SPY0220",
            message: "Cannot assign type 'str' to 'int32' in tuple unpacking");
    }

    [Fact]
    public void NullableTarget_MeasuresTheShapeAgainstTheUnderlyingType()
    {
        AssertPrints("nullable-int8-constant", "x: int8 | None = 7\n    print(x)", "7\n");
        AssertPrints("nullable-float32-literal", "x: float32 | None = 0.5\n    print(x)", "0.5\n");

        AssertRefused("nullable-into-optional-steers-to-maybe",
            "y: int | None = 1\n    x: int? = y\n    print(x)",
            code: "SPY0220",
            message: "cross with 'maybe'");
    }

    /// <summary>
    /// Items 7 and 8: the augmented position is the same decision. The PEP-675 cell and the
    /// regression cell (`xs *= n` with a narrow count) are the falsifiable pair.
    /// </summary>
    [Theory]
    [InlineData("literalstring-concat", "s: LiteralString = \"a\"\n    s += \"b\"\n    print(s)", "ab\n")]
    [InlineData("list-repeat-narrow-count", "xs: list[int] = [1]\n    n: int8 = 2\n    xs *= n\n    print(len(xs))", "2\n")]
    [InlineData("float32-literal", "f: float32 = 1.0\n    f += 1.0\n    print(f)", "2.0\n")]
    [InlineData("decimal-literal", "d: decimal = 1.5\n    d += 1.5\n    print(d)", "3.0\n")]
    [InlineData("uint32-constant", "a: uint32 = 5\n    a += 1\n    print(a)", "6\n")]
    [InlineData("uint64-constant", "a: uint64 = 5\n    a += 1\n    print(a)", "6\n")]
    public void Augmented_IsTheSameStoreDecision(string cell, string body, string expected)
        => AssertPrints(cell, body, expected);

    /// <summary>
    /// Positive controls. Each is a value shape the seam must NOT admit — without them every
    /// acceptance above could be explained by "the seam admits everything".
    /// </summary>
    [Theory]
    [InlineData("out-of-range-element", "xs: list[int8] = [300]\n    print(xs[0])",
        "Cannot assign type 'int32' to 'int8'")]
    [InlineData("out-of-range-float32-element", "xs: list[float32] = [1e40]\n    print(xs[0])",
        "Cannot assign type 'float64' to 'float32'")]
    [InlineData("str-variable-into-literalstring", "v: str = \"a\"\n    xs: list[LiteralString] = [v]\n    print(xs[0])",
        "Cannot assign type 'str' to 'LiteralString'")]
    [InlineData("out-of-range-conditional", "c: bool = True\n    x: int8 = 7 if c else 300\n    print(x)",
        "Cannot assign type 'int32' to variable of type 'int8'")]
    [InlineData("set-augmented-list-rhs", "s: set[int] = {1}\n    s |= [2]\n    print(len(s))",
        "use s.update(xs) to update from any iterable")]
    [InlineData("out-of-range-augmented", "x: int8 = 1\n    x += 300\n    print(x)",
        "Result type 'int32' of augmented assignment is not assignable to target type 'int8'")]
    [InlineData("literalstring-augmented-str-variable", "s: LiteralString = \"a\"\n    v: str = \"b\"\n    s += v\n    print(s)",
        "Result type 'str' of augmented assignment is not assignable to target type 'LiteralString'")]
    public void Controls_StayRefused(string cell, string body, string message)
        => AssertRefused(cell, body, code: null, message: message);

    /// <summary>
    /// Some(v)'s argument is a store into the Optional's UNDERLYING slot, so the seam decides it —
    /// an in-range constant into an `int8?` was SPY0220 while the same constant into an `int8`
    /// declaration ran (#1698, R-G #1720).
    /// </summary>
    [Fact]
    public void SomeArgument_IsAStoreIntoTheUnderlyingSlot()
    {
        AssertPrints("some-declaration", "z: int8? = Some(7)\n    print(z)", "7\n");
        AssertPrints("some-argument", "print(takes(Some(7)))", "1\n",
            prelude: "def takes(o: int8?) -> int:\n    return 1\n\n");
    }

    /// <summary>
    /// The literal-narrowing predicates answer FORM, RANGE and UNDERFLOW without throwing. Asking
    /// only "is it a finite double" admitted `1e40` into a decimal slot, and the emitter's
    /// `decimal.Parse` then threw — SPY0909, a compiler CRASH where SPY0220 is the honest answer.
    /// </summary>
    [Theory]
    [InlineData("decimal-out-of-range", "d: decimal = 1e40\n    print(d)",
        "Cannot assign type 'float64' to variable of type 'decimal'")]
    [InlineData("decimal-underflow", "d: decimal = 1e-30\n    print(d)",
        "Cannot assign type 'float64' to variable of type 'decimal'")]
    [InlineData("float32-out-of-range-exponent", "x: float32 = 1e40\n    print(x)",
        "Cannot assign type 'float64' to variable of type 'float32'")]
    [InlineData("decimal-suffixed-literal", "d: decimal = 1.5f\n    print(d)",
        "Cannot assign type 'float32' to variable of type 'decimal'")]
    [InlineData("float32-suffixed-literal", "x: float32 = 1.5d\n    print(x)",
        "Cannot assign type 'float64' to variable of type 'float32'")]
    public void LiteralNarrowing_RefusesWhatItCannotRepresent(string cell, string body, string message)
        => AssertRefused(cell, body, code: null, message: message);

    [Theory]
    [InlineData("float32-exponent", "x: float32 = 1.5e2\n    print(x)", "150.0\n")]
    [InlineData("decimal-exponent", "d: decimal = 1.5e2\n    print(d)", "150\n")]
    [InlineData("decimal-negative-exponent", "d: decimal = 2.5e-2\n    print(d)", "0.025\n")]
    public void LiteralNarrowing_AdmitsAnInRangeExponentLiteral(string cell, string body, string expected)
        => AssertPrints(cell, body, expected);

    /// <summary>
    /// #1756: an INDEX store writes the container's declared element type. The local and attribute
    /// targets already obeyed the declared-slot rule (#1706); the subscript target did not, so the
    /// identical program was SPY0229 through `d["k"]` and ran through `b.v`.
    /// </summary>
    [Theory]
    [InlineData("dict-if-narrowed",
        "d: dict[str, str | None] = {\"k\": \"v\"}\n    if d[\"k\"] is not None:\n        d[\"k\"] = None\n    print(d[\"k\"])")]
    [InlineData("dict-assert-narrowed",
        "d: dict[str, str | None] = {\"k\": \"v\"}\n    assert d[\"k\"] is not None\n    d[\"k\"] = None\n    print(d[\"k\"])")]
    [InlineData("list-if-narrowed",
        "xs: list[str | None] = [\"v\"]\n    if xs[0] is not None:\n        xs[0] = None\n    print(xs[0])")]
    [InlineData("dict-control-no-narrowing",
        "d: dict[str, str | None] = {\"k\": \"v\"}\n    d[\"k\"] = None\n    print(d[\"k\"])")]
    [InlineData("local-control-same-narrowing",
        "x: str | None = \"v\"\n    if x is not None:\n        x = None\n    print(x)")]
    public void IndexStore_WritesTheDeclaredElementType(string cell, string body)
        => AssertPrints(cell, body, "None\n");

    /// <summary>
    /// #1757: a walrus writes its target from inside an EXPRESSION, so the flow analysis never
    /// killed the target's narrowing facts and later reads used a value the variable no longer
    /// held. The contract is that the walrus form behaves exactly like its STATEMENT twin, so each
    /// cell asserts the pair rather than a value — a cell pinned to one expected string would pass
    /// if BOTH forms regressed together.
    /// </summary>
    [Theory]
    [InlineData("isinstance-rebind",
        "x: object = 1\n    if isinstance(x, int):\n        y: object = (x := \"s\")\n        print(x)",
        "x: object = 1\n    if isinstance(x, int):\n        x = \"s\"\n        print(x)")]
    [InlineData("none-rebind",
        "x: str | None = \"a\"\n    if x is not None:\n        y: str = (x := None)\n        print(x)",
        "x: str | None = \"a\"\n    if x is not None:\n        x = None\n        print(x)")]
    [InlineData("none-rebind-then-read",
        "x: str | None = \"a\"\n    if x is not None:\n        z: str | None = (x := None)\n        print(len(x))",
        "x: str | None = \"a\"\n    if x is not None:\n        x = None\n        print(len(x))")]
    public void WalrusStore_BehavesLikeItsStatementTwin(string cell, string walrusBody, string statementBody)
    {
        var walrus = CompileAndExecute("def main():\n    " + walrusBody + "\n");
        var twin = CompileAndExecute("def main():\n    " + statementBody + "\n");

        walrus.Success.Should().Be(twin.Success,
            $"cell '{cell}': the walrus form and its statement twin must agree. Walrus: "
            + string.Join(" | ", walrus.CompilationErrors));
        walrus.StandardOutput.Should().Be(twin.StandardOutput,
            $"cell '{cell}': the walrus form prints what its statement twin prints");
        RuntimeFailureOf(walrus).Should().Be(RuntimeFailureOf(twin),
            $"cell '{cell}': a stale narrowing shows up as a .NET cast or null failure where the "
            + "twin has none, or has Sharpy's own typed error");
    }

    /// <summary>
    /// The .NET exception a run failed with, or the empty string. Named exactly, because the two
    /// #1757 symptoms ARE exception identities: InvalidCastException from `((int)x!)` emitted
    /// against a stale isinstance narrowing, and NullReferenceException from an unwrapped access
    /// where the statement twin raises Sharpy's own TypeError.
    /// </summary>
    private static string RuntimeFailureOf(ExecutionResult result)
    {
        foreach (var marker in new[]
        {
            "System.InvalidCastException", "System.NullReferenceException", "Sharpy.TypeError",
        })
        {
            if (result.StandardError.Contains(marker, StringComparison.Ordinal))
                return marker;
        }

        return string.Empty;
    }

    [Fact]
    public void NoneIntoNonNullable_IsStillSPY0229()
        => AssertRefused("none-into-int", "x: int = None\n    print(x)",
            code: "SPY0229", message: "Cannot assign 'None' to non-nullable type 'int32'");

    [Fact]
    public void FloatVariableIntoFloat32Argument_IsStillRefused()
        => AssertRefused("float-variable-argument",
            "y: float = 1.5\n    print(takes(y))",
            prelude: "def takes(x: float32) -> float32:\n    return x\n\n",
            code: "SPY0220",
            message: "Cannot pass argument of type 'float64' to parameter of type 'float32'");

    // ═══════════════════════════════════════════════════════════════════════════════════════════
    // `??=` is a store into the LEFT slot — left × right × target (plan-757fbb Decision 6, #1767)
    //
    // Contract: the RHS of `x ??= v` is decided by ONE seam call at StorePosition.CoalesceAssign
    // for EVERY target kind — local, `self.v`, `d["k"]`, narrowed local — and receives the SAME value
    // lowerings a plain store's RHS gets (float32 literal re-typing, conditional branch casts,
    // Optional wrap). A non-nullable / non-Optional left is refused SPY0222 BEFORE the seam (spec:
    // "y is not nullable or optional"); a bare `None` RHS keeps its SPY0222; `None()` is typed by
    // the slot (SPY0244 at `T | None`, a running no-op at `T?`); cross-family cells refuse SPY0220
    // with the `maybe` / unwrap steers. SPY0908 in no cell.
    //
    // What this table caught at 852bf488b (measured, `sharpyc run`): `x: int = 1; x ??= 42` ICEd
    // CS0019; `x: float32 | None = None; x ??= 0.5` ICEd CS0266 (it RAN at dff55b2cd); `x: float32?
    // ??= 0.5` and `x: int8? ??= 7 if c else 8` ICEd CS1503 (classified, never applied); a narrowed
    // `x ??= 5` ICEd CS1061 (`x.Unwrap().IsSome`); `d["k"] ??= 7` and `c.v ??= 0.5` were refused
    // SPY0222 on the operator path; `x: int = 1; x ??= Some(1)` reported SPY0230.
    // ═══════════════════════════════════════════════════════════════════════════════════════════

    // ── Axis sizes, anchored to literals ─────────────────────────────────────────────────────

    private const int CoalesceLeftCount = 7;
    private const int CoalesceRightCount = 11;
    private const int CoalesceTargetCount = 4;
    private const int CoalesceAcceptedCellCount = 128;
    private const int CoalesceRefusedCellCount = 168;
    private const int CoalesceNotApplicableCellCount = 12;

    private enum SlotFamily { Plain, Optional, Nullable }

    private enum PayloadKind { Int, Int8, Float32 }

    /// <param name="Slot">The declared type, as Sharpy source.</param>
    /// <param name="Display">The slot as a diagnostic spells it.</param>
    /// <param name="Empty">An ABSENT initializer (a Plain slot has no absence; it is seeded present).</param>
    /// <param name="Present">A PRESENT initializer, for the narrowed target.</param>
    /// <param name="PresentPrint">What the present seed prints — a `??=` on it is a no-op.</param>
    private sealed record CoalesceLeft(
        string Name,
        string Slot,
        string Display,
        SlotFamily Family,
        PayloadKind Payload,
        string Empty,
        string Present,
        string PresentPrint);

    private static readonly CoalesceLeft[] CoalesceLefts =
    {
        new("int", "int", "int32", SlotFamily.Plain, PayloadKind.Int, "1", "1", "1\n"),
        new("int?", "int?", "int32?", SlotFamily.Optional, PayloadKind.Int, "None()", "Some(1)", "1\n"),
        new("int|None", "int | None", "int32 | None", SlotFamily.Nullable, PayloadKind.Int, "None", "1", "1\n"),
        new("int8?", "int8?", "int8?", SlotFamily.Optional, PayloadKind.Int8, "None()", "Some(1)", "1\n"),
        new("int8|None", "int8 | None", "int8 | None", SlotFamily.Nullable, PayloadKind.Int8, "None", "1", "1\n"),
        new("float32?", "float32?", "float32?", SlotFamily.Optional, PayloadKind.Float32, "None()", "Some(1.0)", "1.0\n"),
        new("float32|None", "float32 | None", "float32 | None", SlotFamily.Nullable, PayloadKind.Float32, "None", "1.0", "1.0\n"),
    };

    private static string PayloadType(PayloadKind k)
        => k switch { PayloadKind.Int => "int", PayloadKind.Int8 => "int8", _ => "float32" };

    private static string PayloadDisplay(PayloadKind k)
        => k switch { PayloadKind.Int => "int32", PayloadKind.Int8 => "int8", _ => "float32" };

    /// <summary>A literal of the payload's own kind that is NOT a constant conversion (`5` into int/int8 via a variable).</summary>
    private static string PayloadLiteral(PayloadKind k) => k == PayloadKind.Float32 ? "0.5" : "5";

    private static string PayloadLiteralPrint(PayloadKind k) => k == PayloadKind.Float32 ? "0.5\n" : "5\n";

    /// <summary>An in-range constant of the payload's kind: `7` exercises the integer constant arm at int8.</summary>
    private static string PayloadConstant(PayloadKind k) => k == PayloadKind.Float32 ? "0.5" : "7";

    private static string PayloadConstantPrint(PayloadKind k) => k == PayloadKind.Float32 ? "0.5\n" : "7\n";

    private static string PayloadConditional(PayloadKind k)
        => k == PayloadKind.Float32 ? "0.5 if c else 0.25" : "7 if c else 8";

    /// <param name="Setup">Statements the RHS needs (unindented; the target indents them).</param>
    /// <param name="Expr">The RHS expression.</param>
    /// <param name="Print">What an EMPTY slot prints after an accepted store.</param>
    private sealed record CoalesceRight(
        string Name,
        Func<PayloadKind, string[]> Setup,
        Func<PayloadKind, string> Expr,
        Func<PayloadKind, string> Print);

    private static readonly CoalesceRight[] CoalesceRights =
    {
        new("SameT", k => new[] { $"v: {PayloadType(k)} = {PayloadLiteral(k)}" }, _ => "v", PayloadLiteralPrint),
        new("OptionalT", k => new[] { $"o: {PayloadType(k)}? = Some({PayloadLiteral(k)})" }, _ => "o", PayloadLiteralPrint),
        new("NullableT", k => new[] { $"n: {PayloadType(k)} | None = {PayloadLiteral(k)}" }, _ => "n", PayloadLiteralPrint),
        new("SomeLiteral", _ => Array.Empty<string>(), k => $"Some({PayloadLiteral(k)})", PayloadLiteralPrint),
        new("None", _ => Array.Empty<string>(), _ => "None", _ => "None\n"),
        new("NoneCall", _ => Array.Empty<string>(), _ => "None()", _ => "None\n"),
        new("Constant", _ => Array.Empty<string>(), PayloadConstant, PayloadConstantPrint),
        new("Float32Literal", _ => Array.Empty<string>(), _ => "0.5", _ => "0.5\n"),
        new("Conditional", _ => new[] { "c: bool = True" }, PayloadConditional, PayloadConstantPrint),
        new("Mistyped", _ => Array.Empty<string>(), _ => "\"s\"", _ => "s\n"),
        // A second mistyped payload: `Some(b"ab")` is Optional[bytes], still not Optional[int32],
        // so the refusal must name both types and must NOT steer to Some(...) (852bf488b did, SPY0604).
        new("MistypedBytes", _ => Array.Empty<string>(), _ => "b\"ab\"", _ => "b'ab'\n"),
    };

    /// <param name="Compose">(left, setup lines, RHS) → the whole program.</param>
    /// <param name="Narrowed">The slot is seeded PRESENT under `is not None`, so an accepted store is a no-op that prints the seed.</param>
    private sealed record CoalesceTarget(
        string Name,
        Func<CoalesceLeft, string[], string, string> Compose,
        bool Narrowed);

    private static string IndentLines(string[] lines, int spaces)
        => string.Concat(lines.Select(l => new string(' ', spaces) + l + "\n"));

    private static readonly CoalesceTarget[] CoalesceTargets =
    {
        new("Local",
            (l, setup, rhs) =>
                $"def main():\n    x: {l.Slot} = {l.Empty}\n{IndentLines(setup, 4)}    x ??= {rhs}\n    print(x)\n",
            Narrowed: false),

        new("Member",
            (l, setup, rhs) =>
                $"class C:\n    v: {l.Slot} = {l.Empty}\n\n    def fill(self) -> None:\n{IndentLines(setup, 8)}        self.v ??= {rhs}\n\n\n"
                + "def main():\n    c: C = C()\n    c.fill()\n    print(c.v)\n",
            Narrowed: false),

        new("Index",
            (l, setup, rhs) =>
                $"def main():\n    d: dict[str, {l.Slot}] = {{\"k\": {l.Empty}}}\n{IndentLines(setup, 4)}    d[\"k\"] ??= {rhs}\n    print(d[\"k\"])\n",
            Narrowed: false),

        new("Narrowed",
            (l, setup, rhs) =>
                $"def main():\n    x: {l.Slot} = {l.Present}\n    if x is not None:\n{IndentLines(setup, 8)}        x ??= {rhs}\n        print(x)\n",
            Narrowed: true),
    };

    // ── The contract, cell by cell ───────────────────────────────────────────────────────────

    private enum CoalesceVerdict { Accepted, Refused, NotApplicable }

    private sealed record CoalesceExpectation(
        CoalesceVerdict Verdict,
        string? Output = null,
        string? Code = null,
        string? Message = null,
        string? Reason = null);

    private static CoalesceExpectation CoalesceRefusal(string code, string message)
        => new(CoalesceVerdict.Refused, Code: code, Message: message);

    private static CoalesceExpectation ExpectationOf(CoalesceLeft l, CoalesceRight r, CoalesceTarget t)
    {
        // (a) A left that cannot hold absence is refused before the RHS is looked at — every
        //     right, every target. The refusal names the LEFT, not an operand type.
        if (l.Family == SlotFamily.Plain)
        {
            return CoalesceRefusal(DiagnosticCodes.Semantic.InvalidBinaryOperation,
                $"Type '{l.Display}' does not support operator '??=': the target must be nullable "
                + $"('{l.Display} | None') or Optional ('{l.Display}?')");
        }

        var payload = PayloadDisplay(l.Payload);
        var accepted = new CoalesceExpectation(CoalesceVerdict.Accepted,
            Output: t.Narrowed ? l.PresentPrint : r.Print(l.Payload));

        switch (r.Name)
        {
            // (b) bare None keeps today's operator refusal; None() is typed by the slot.
            case "None":
                return CoalesceRefusal(DiagnosticCodes.Semantic.InvalidBinaryOperation,
                    $"Type '{l.Display}' does not support operator '??=' with operand of type 'None'");

            case "NoneCall":
                return l.Family == SlotFamily.Nullable
                    ? CoalesceRefusal(DiagnosticCodes.Semantic.InvalidNoneConstructor,
                        $"'None()' can only construct Optional types, not '{l.Display}'")
                    : accepted;

            // (c) the seam: whole slot first, payload second, cross-family with the steers.
            case "OptionalT":
                return l.Family == SlotFamily.Nullable
                    ? CoalesceRefusal(DiagnosticCodes.Semantic.TypeMismatch,
                        $"Cannot assign type '{payload}?' to '??=' target of type '{l.Display}' — the value is "
                        + $"Optional[{payload}]; narrow it ('if x is not None:') or unwrap it first")
                    : accepted;

            case "NullableT":
                return l.Family == SlotFamily.Optional
                    ? CoalesceRefusal(DiagnosticCodes.Semantic.TypeMismatch,
                        $"Cannot assign type '{payload} | None' to '??=' target of type '{l.Display}' — the value is "
                        + $"'{payload} | None' (C# nullability) and the slot is Optional[{payload}]; cross with 'maybe'")
                    : accepted;

            case "SomeLiteral":
                return l.Family == SlotFamily.Nullable
                    ? new CoalesceExpectation(CoalesceVerdict.NotApplicable,
                        Reason: "`Some(v)` under a `T | None` expectation falls through to a call of the bare "
                        + "identifier and reports SPY0230 — at a declaration `y: int | None = Some(5)` too, "
                        + "so it is the Some route's own defect (#1784), not this seam's cell")
                    : accepted;

            case "Float32Literal":
                return l.Payload == PayloadKind.Float32
                    ? accepted
                    : CoalesceRefusal(DiagnosticCodes.Semantic.TypeMismatch,
                        $"Cannot assign type 'float64' to '??=' target of type '{l.Display}'");

            case "Mistyped":
                return CoalesceRefusal(DiagnosticCodes.Semantic.TypeMismatch,
                    $"Cannot assign type 'str' to '??=' target of type '{l.Display}'");

            case "MistypedBytes":
                return CoalesceRefusal(DiagnosticCodes.Semantic.TypeMismatch,
                    $"Cannot assign type 'bytes' to '??=' target of type '{l.Display}'");

            default: // SameT, Constant, Conditional
                return accepted;
        }
    }

    private static CoalesceLeft CL(string name) => CoalesceLefts.Single(l => l.Name == name);

    private static CoalesceRight CR(string name) => CoalesceRights.Single(r => r.Name == name);

    private static CoalesceTarget CT(string name) => CoalesceTargets.Single(t => t.Name == name);

    private static string CoalesceSource(CoalesceLeft l, CoalesceRight r, CoalesceTarget t)
        => t.Compose(l, r.Setup(l.Payload), r.Expr(l.Payload));

    private static IEnumerable<object[]> CoalesceCellsWhere(CoalesceVerdict verdict)
        => from l in CoalesceLefts
           from r in CoalesceRights
           from t in CoalesceTargets
           where ExpectationOf(l, r, t).Verdict == verdict
           select new object[] { l.Name, r.Name, t.Name };

    public static IEnumerable<object[]> CoalesceAcceptedCells => CoalesceCellsWhere(CoalesceVerdict.Accepted);

    public static IEnumerable<object[]> CoalesceRefusedCells => CoalesceCellsWhere(CoalesceVerdict.Refused);

    public static IEnumerable<object[]> CoalesceNotApplicableCells => CoalesceCellsWhere(CoalesceVerdict.NotApplicable);

    [Theory]
    [MemberData(nameof(CoalesceAcceptedCells))]
    public void CoalesceAssign_AcceptedCell_RunsAndPrintsTheSlot(string left, string right, string target)
    {
        var (l, r, t) = (CL(left), CR(right), CT(target));
        var source = CoalesceSource(l, r, t);
        var expected = ExpectationOf(l, r, t);

        var result = CompileAndExecute(source);

        result.RawDiagnostics.Should().NotContain(
            d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError,
            $"[{left} ??= {right} @ {target}] must never ICE — a recorded-but-unapplied lowering shows up here\n{source}");
        result.Success.Should().BeTrue(
            $"[{left} ??= {right} @ {target}] must compile and run. Diagnostics: "
            + $"{string.Join(" | ", result.CompilationErrors)}\n{source}");
        result.StandardOutput.Should().Be(expected.Output,
            $"[{left} ??= {right} @ {target}] prints the slot after the store\n{source}");
    }

    [Theory]
    [MemberData(nameof(CoalesceRefusedCells))]
    public void CoalesceAssign_RefusedCell_CarriesTheCodeAndSteer(string left, string right, string target)
    {
        var (l, r, t) = (CL(left), CR(right), CT(target));
        var source = CoalesceSource(l, r, t);
        var expected = ExpectationOf(l, r, t);

        var result = CompileAndExecute(source);

        result.RawDiagnostics.Should().NotContain(
            d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError,
            $"[{left} ??= {right} @ {target}] must be refused at semantic time, never by Roslyn\n{source}");
        result.Success.Should().BeFalse(
            $"[{left} ??= {right} @ {target}] must be refused; it printed '{result.StandardOutput}'\n{source}");

        var matching = result.RawDiagnostics.Where(d => d.Code == expected.Code).ToList();
        matching.Should().HaveCount(1,
            $"[{left} ??= {right} @ {target}] must report {expected.Code} exactly once. Got: "
            + $"{string.Join(" | ", result.RawDiagnostics.Select(d => $"{d.Code}: {d.Message}"))}\n{source}");
        matching[0].Message.Should().Contain(expected.Message,
            $"[{left} ??= {right} @ {target}] must carry the seam's phrasing and steer\n{source}");
        matching[0].Message.Should().NotContain("Some(",
            $"[{left} ??= {right} @ {target}] a steer must be truthful: a bare payload is already the "
            + $"accepted form at `??=`, and `Some(v)` of a mistyped `v` is refused too\n{source}");
    }

    /// <summary>
    /// Every N/A cell names a real cell and carries a reason; the count is written down so a cell
    /// cannot slip into N/A unnoticed.
    /// </summary>
    [Theory]
    [MemberData(nameof(CoalesceNotApplicableCells))]
    public void CoalesceAssign_NotApplicableCell_HasAReason(string left, string right, string target)
    {
        var expected = ExpectationOf(CL(left), CR(right), CT(target));
        expected.Reason.Should().NotBeNullOrWhiteSpace($"[{left} ??= {right} @ {target}]");
    }

    [Fact]
    public void CoalesceAssign_MatrixIsTotalOverItsAxes()
    {
        CoalesceLefts.Length.Should().Be(CoalesceLeftCount);
        CoalesceRights.Length.Should().Be(CoalesceRightCount);
        CoalesceTargets.Length.Should().Be(CoalesceTargetCount);
        CoalesceLefts.Select(l => l.Name).Should().OnlyHaveUniqueItems();
        CoalesceRights.Select(r => r.Name).Should().OnlyHaveUniqueItems();
        CoalesceTargets.Select(t => t.Name).Should().OnlyHaveUniqueItems();

        var accepted = CoalesceAcceptedCells.Count();
        var refused = CoalesceRefusedCells.Count();
        var na = CoalesceNotApplicableCells.Count();

        accepted.Should().Be(CoalesceAcceptedCellCount, "the accepted half is written down");
        refused.Should().Be(CoalesceRefusedCellCount, "the refused half is written down");
        na.Should().Be(CoalesceNotApplicableCellCount, "the N/A cells are written down, each with a reason");
        (accepted + refused + na).Should().Be(CoalesceLeftCount * CoalesceRightCount * CoalesceTargetCount,
            $"accepted ({accepted}) + refused ({refused}) + N/A ({na}) must be the whole product");
    }

    /// <summary>
    /// Message quality at the cells the cell prober found routing through the wrong arm at
    /// 852bf488b (measured): a non-nullable left reported the RHS's failure (SPY0230 for a
    /// <c>Some(1)</c> RHS, SPY0220 "Result type … of augmented assignment" for <c>"s"</c>) instead of
    /// the left's; an out-of-range constant into <c>uint8?</c> was steered to <c>Some(...)</c>
    /// (SPY0604), which is refused too — it must get the seam's own out-of-range refusal, the same
    /// code and head a declaration gives the payload.
    /// </summary>
    [Fact]
    public void CoalesceAssign_NonNullableLeft_IsRefusedBeforeTheValueIsChecked()
    {
        foreach (var rhs in new[] { "Some(1)", "\"s\"", "42", "None()", "None" })
        {
            var result = CompileAndExecute($"def main():\n    x: int = 1\n    x ??= {rhs}\n    print(x)\n");

            result.Success.Should().BeFalse($"`x: int = 1; x ??= {rhs}` must be refused");
            result.RawDiagnostics.Should().ContainSingle(
                d => d.Code == DiagnosticCodes.Semantic.InvalidBinaryOperation,
                $"`x ??= {rhs}` on an int: the refusal is about the LEFT. Got: "
                + string.Join(" | ", result.RawDiagnostics.Select(d => $"{d.Code}: {d.Message}")));
            result.RawDiagnostics.Single(d => d.Code == DiagnosticCodes.Semantic.InvalidBinaryOperation)
                .Message.Should().Contain("does not support operator '??='");
            result.RawDiagnostics.Should().NotContain(d => d.Code == DiagnosticCodes.Semantic.NotCallable,
                $"`x ??= {rhs}`: the RHS is not checked against a slot that cannot hold it (no SPY0230 cascade)");
            result.RawDiagnostics.Should().NotContain(d => d.Code == DiagnosticCodes.Semantic.TypeMismatch,
                $"`x ??= {rhs}`: no 'augmented assignment' result mismatch — `??=` is not an operator");
        }
    }

    [Fact]
    public void CoalesceAssign_OutOfRangeConstant_GetsTheSeamsRefusalNotTheSomeSteer()
    {
        var coalesce = CompileAndExecute("def main():\n    x: uint8? = None()\n    x ??= 300\n    print(x)\n");
        var declaration = CompileAndExecute("def main():\n    y: uint8 = 300\n    print(y)\n");

        declaration.Success.Should().BeFalse("the twin: an out-of-range constant into a uint8 declaration");
        coalesce.Success.Should().BeFalse("`x: uint8? = None(); x ??= 300` must be refused");

        var declarationRefusal = declaration.RawDiagnostics.Single(d => d.Code == DiagnosticCodes.Semantic.TypeMismatch);
        var coalesceRefusal = coalesce.RawDiagnostics.Should().ContainSingle(
                d => d.Code == DiagnosticCodes.Semantic.TypeMismatch,
                "the `??=` cell carries the SAME code as the declaration twin. Got: "
                + string.Join(" | ", coalesce.RawDiagnostics.Select(d => $"{d.Code}: {d.Message}")))
            .Which;

        declarationRefusal.Message.Should().StartWith("Cannot assign type 'int32' to");
        coalesceRefusal.Message.Should().StartWith("Cannot assign type 'int32' to",
            "the same head as the declaration — the seam's out-of-range refusal");
        coalesceRefusal.Message.Should().Contain("'??=' target of type 'uint8?'");
        coalesce.RawDiagnostics.Should().NotContain(
            d => d.Code == DiagnosticCodes.SemanticOverflow.StrictOptionalConstruction,
            "`Some(300)` into `uint8?` is refused too, so 'construct it with Some(...)' is wrong advice");
    }

    /// <summary>
    /// The narrowed no-op (Decision 4): on a name known not to be None, `??=` stores nothing and the
    /// RemoveNone fact survives, for a payload RHS and an Optional RHS alike. Both ICEd or misreported
    /// at 852bf488b (CS1061 on `x.Unwrap().IsSome`; SPY0230 for `Some(9)` under the narrowed `int`).
    /// </summary>
    [Theory]
    [InlineData("payload", "x: int8? = Some(1)\n    if x is not None:\n        x ??= 5\n        print(x)\n    print(x)", "1\n1\n")]
    [InlineData("some", "x: int? = Some(1)\n    if x is not None:\n        x ??= Some(9)\n        print(x)\n    print(x)", "1\n1\n")]
    [InlineData("nullable", "x: int | None = 1\n    if x is not None:\n        x ??= 9\n        print(x)\n    print(x)", "1\n1\n")]
    [InlineData("survival", "d: int? = Some(10)\n    if d is not None:\n        d ??= 7\n        e: int = d\n        print(e)", "10\n")]
    public void CoalesceAssign_OnANarrowedName_IsANoOpThatKeepsTheNarrowing(string cell, string body, string expected)
        => AssertPrints(cell, body, expected);

    private void AssertPrints(string cell, string body, string expected, string prelude = "")
    {
        var source = prelude + "def main():\n    " + body + "\n";
        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue(
            $"cell '{cell}' must compile and run. Diagnostics: "
            + string.Join(" | ", result.CompilationErrors));
        result.StandardOutput.Should().Be(expected, $"cell '{cell}' prints the stored value");
    }

    private void AssertRefused(string cell, string body, string? code, string message, string prelude = "")
    {
        var source = prelude + "def main():\n    " + body + "\n";
        var result = CompileAndExecute(source);

        result.Success.Should().BeFalse($"cell '{cell}' must be refused");

        var matches = result.RawDiagnostics
            .Where(d => d.Message.Contains(message, StringComparison.Ordinal)
                && (code == null || d.Code == code))
            .ToList();

        matches.Should().HaveCount(1,
            $"cell '{cell}' reports exactly one refusal naming \"{message}\". Diagnostics: "
            + string.Join(" | ", result.RawDiagnostics.Select(d => $"{d.Code}: {d.Message}")));
    }
}
