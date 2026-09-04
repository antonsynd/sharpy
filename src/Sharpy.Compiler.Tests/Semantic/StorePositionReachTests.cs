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
        "Cannot assign type 'list[int32]' to variable of type 'list[int8]'")]
    [InlineData("out-of-range-float32-element", "xs: list[float32] = [1e40]\n    print(xs[0])",
        "Cannot assign type 'list[float64]' to variable of type 'list[float32]'")]
    [InlineData("str-variable-into-literalstring", "v: str = \"a\"\n    xs: list[LiteralString] = [v]\n    print(xs[0])",
        "Cannot assign type 'list[str]' to variable of type 'list[LiteralString]'")]
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
