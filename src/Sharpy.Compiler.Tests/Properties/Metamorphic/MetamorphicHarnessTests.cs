using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Tests.Properties.Metamorphic.Transforms;
using Xunit;

namespace Sharpy.Compiler.Tests.Properties.Metamorphic;

/// <summary>
/// The metamorphic harness testing itself (#1157). A sweep is only as trustworthy as its transforms:
/// a transform that quietly changes a program's meaning manufactures false violations at corpus
/// scale, and one that silently rewrites nothing manufactures false confidence. These cases pin the
/// no-op contract, the allowed-delta table, and the specific semantic guards each transform needs.
/// </summary>
public class MetamorphicHarnessTests
{
    // ---- no-op contract ----

    /// <summary>
    /// The sweep's perf lever and its <c>notApplicable</c> bucket both rest on this: a transform with
    /// nothing to rewrite returns the source BYTE-IDENTICAL (the skip is a string-identity check).
    /// A transform that returned a re-joined or re-indented copy would compile ~1,500 needless cells
    /// and report them as real coverage.
    /// </summary>
    [Fact]
    public void EveryTransform_ReturnsSourceUnchanged_WhenNothingApplies()
    {
        const string nothingToRewrite = "# just a comment\n# and another\n";
        foreach (var transform in MetamorphicTransforms.All)
        {
            Assert.Equal(nothingToRewrite, transform.Apply(nothingToRewrite));
        }
    }

    [Fact]
    public void EveryTransform_HasAnAllowedDeltaEntry()
    {
        foreach (var transform in MetamorphicTransforms.All)
        {
            // Throws when a transform was added without stating its diagnostic contract.
            _ = MetamorphicTransforms.AllowedNewDiagnostics(transform);
        }
    }

    [Fact]
    public void TransformNames_AreUniqueAndStable()
    {
        var names = MetamorphicTransforms.All.Select(t => t.Name).ToList();
        Assert.Equal(names.Count, names.Distinct(StringComparer.Ordinal).Count());
    }

    // ---- allowed-delta table: one case per transform ----

    [Fact]
    public void DeadCodeAfterReturn_AllowsExactlyUnreachableAndUnusedWarnings()
    {
        var allowed = MetamorphicTransforms.AllowedNewDiagnostics(new DeadCodeAfterReturnTransform());
        Assert.Equal(
            new[] { DiagnosticCodes.Validation.UnreachableCodeWarning, DiagnosticCodes.Validation.UnusedVariable }
                .OrderBy(x => x, StringComparer.Ordinal),
            allowed.OrderBy(x => x, StringComparer.Ordinal));
    }

    [Theory]
    [InlineData(typeof(CommentInsertionTransform))]
    [InlineData(typeof(PassInsertionTransform))]
    [InlineData(typeof(ParensWrapTransform))]
    [InlineData(typeof(ParensWrapCalleeTransform))]
    [InlineData(typeof(IfTrueWrapTransform))]
    [InlineData(typeof(SwapCommutativeTransform))]
    [InlineData(typeof(AlphaRenameTransform))]
    [InlineData(typeof(AddRedundantTypeAnnotationTransform))]
    public void DiagnosticNeutralTransforms_AllowNoNewCodes(Type transformType)
    {
        var transform = MetamorphicTransforms.All.Single(t => t.GetType() == transformType);
        Assert.Empty(MetamorphicTransforms.AllowedNewDiagnostics(transform));
    }

    /// <summary>
    /// The delta entry must be real, not defensive: dead code after a return genuinely raises both
    /// codes. If this stops holding, the entry is silently widening the contract for everyone.
    /// </summary>
    [Fact]
    public void DeadCodeAfterReturn_ActuallyRaisesTheCodesItDeclares()
    {
        const string source =
            "def f() -> int:\n" +
            "    return 1\n" +
            "\n" +
            "def main() -> None:\n" +
            "    print(f())\n";

        var transformed = new DeadCodeAfterReturnTransform().Apply(source);
        Assert.NotEqual(source, transformed);

        var codes = new Compiler().Analyze(transformed, "dead_code.spy")
            .Diagnostics.GetAll()
            .Select(d => d.Code)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains(DiagnosticCodes.Validation.UnreachableCodeWarning, codes);
        Assert.Contains(DiagnosticCodes.Validation.UnusedVariable, codes);
    }

    // ---- per-transform semantic guards ----

    [Fact]
    public void ParensWrap_WrapsASinglePositionalArgument()
    {
        var result = new ParensWrapTransform().Apply("def main():\n    print(1 + 2)\n");
        Assert.Contains("print((1 + 2))", result, StringComparison.Ordinal);
    }

    /// <summary>
    /// <c>print(a, b)</c> prints two space-separated values; <c>print((a, b))</c> prints a tuple.
    /// Wrapping here would change the program's OUTPUT, so the transform must decline.
    /// </summary>
    [Theory]
    [InlineData("def main():\n    print(1, 2)\n")]
    [InlineData("def main():\n    print(1, end=\"\")\n")]
    [InlineData("def main():\n    xs: list[int] = [1]\n    print(*xs)\n")]
    public void ParensWrap_DeclinesAnythingButOnePositionalArgument(string source)
    {
        Assert.Equal(source, new ParensWrapTransform().Apply(source));
    }

    [Fact]
    public void ParensWrapCallee_WrapsPlainAndMemberCallees()
    {
        var result = new ParensWrapCalleeTransform().Apply(
            "def main():\n    foo(1)\n    obj.method(2)\n");
        Assert.Contains("(foo)(1)", result, StringComparison.Ordinal);
        Assert.Contains("(obj.method)(2)", result, StringComparison.Ordinal);
    }

    /// <summary>
    /// Declaration syntax, pattern syntax and a literal receiver only LOOK like call sites. Wrapping
    /// one produces a parse error the sweep would report as a compiler regression. Other genuine call
    /// sites in the same snippet may still be wrapped — the contract is about the guarded construct.
    /// </summary>
    [Theory]
    [InlineData("class C(B):\n    def m(self, x: int) -> None:\n        super().m(x)\n", "(super)")]
    [InlineData("def main():\n    match r:\n        case Ok(v):\n            print(v)\n", "(Ok)")]
    [InlineData("class C:\n    property health: int = 1\n        before_set(new_value):\n            print(new_value)\n", "(before_set)")]
    [InlineData("def main():\n    print(\" \".join(xs))\n", "(.join)")]
    public void ParensWrapCallee_DeclinesNonCallSites(string source, string forbidden)
    {
        Assert.DoesNotContain(forbidden, new ParensWrapCalleeTransform().Apply(source), StringComparison.Ordinal);
    }

    /// <summary>
    /// <c>Ok</c>/<c>Some</c>/<c>super</c> are call syntax, not values (<c>k = Ok</c> is SPY0230), so
    /// there is no value to parenthesize.
    /// </summary>
    [Fact]
    public void ParensWrapCallee_DeclinesConstructorSyntaxThatIsNotAValue()
    {
        const string source = "def f() -> int !str:\n    return Ok(1)\n";
        Assert.Equal(source, new ParensWrapCalleeTransform().Apply(source));
    }

    [Fact]
    public void SwapCommutative_SwapsIntegerLiteralAddition()
    {
        var result = new SwapCommutativeTransform().Apply("def main():\n    print(1 + 2)\n");
        Assert.Contains("2 + 1", result, StringComparison.Ordinal);
    }

    /// <summary>
    /// Rewriting inside a string changes printed output, and the float case is worse: without the
    /// boundary guards <c>1.5 + 2</c> matches at <c>5 + 2</c> and yields the garbage <c>1.2 + 5</c>.
    /// </summary>
    [Theory]
    [InlineData("def main():\n    print(\"1 + 2\")\n")]
    [InlineData("def main():\n    print(1.5 + 2.5)\n")]
    [InlineData("def main():\n    x: float = 1.5 + 2\n    print(x)\n")]
    [InlineData("def main():\n    print(3)  # 1 + 2\n")]
    public void SwapCommutative_DeclinesLiteralsCommentsAndFloats(string source)
    {
        Assert.Equal(source, new SwapCommutativeTransform().Apply(source));
    }

    [Fact]
    public void CommentInsertion_InsertsIntoBlockBodiesOnly()
    {
        var result = new CommentInsertionTransform().Apply("def main():\n    print(1)\n");
        Assert.Contains("def main():\n    # generated comment", result, StringComparison.Ordinal);
    }

    [Fact]
    public void CommentInsertion_DeclinesAOneLineBody()
    {
        // No block is opened, so there is no body to comment into.
        const string source = "def f() -> int: return 1\n";
        Assert.Equal(source, new CommentInsertionTransform().Apply(source));
    }

    [Fact]
    public void CommentInsertion_LeavesADocstringsContentsIntact()
    {
        const string source = "def main():\n    print(\"\"\"\ndef inner():\n\"\"\")\n";
        var result = new CommentInsertionTransform().Apply(source);

        // The real `def main():` header gets its comment; the `def inner():` INSIDE the string must
        // not, or the transform would change what the program prints.
        Assert.Contains("def main():\n    # generated comment", result, StringComparison.Ordinal);
        Assert.Contains("print(\"\"\"\ndef inner():\n\"\"\")", result, StringComparison.Ordinal);
    }

    [Fact]
    public void DeadCodeAfterReturn_UsesAUniqueNamePerInsertionSite()
    {
        var result = new DeadCodeAfterReturnTransform().Apply(
            "def f(x: int) -> int:\n" +
            "    if x > 0:\n" +
            "        return 1\n" +
            "    return 2\n");
        var names = result.Split('\n')
            .Where(l => l.Contains("dead_after_return_", StringComparison.Ordinal))
            .Select(l => l.Trim())
            .ToList();
        Assert.Equal(2, names.Count);
        Assert.Equal(2, names.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>A <c>return match n:</c> opens a block — its statement is not over yet.</summary>
    [Fact]
    public void DeadCodeAfterReturn_DeclinesABlockOpeningReturn()
    {
        const string source =
            "def categorize(n: int) -> str:\n" +
            "    return match n:\n" +
            "        case 1: \"one\"\n" +
            "        case _: \"other\"\n";
        Assert.Equal(source, new DeadCodeAfterReturnTransform().Apply(source));
    }

    [Fact]
    public void DeadCodeAfterReturn_DeclinesOutsideFunctionBodies()
    {
        // A class body would take the inserted line as a FIELD declaration, not dead code.
        const string source = "class C:\n    value: int = 1\n";
        Assert.Equal(source, new DeadCodeAfterReturnTransform().Apply(source));
    }

    [Fact]
    public void IfTrueWrap_PreservesTheStatementsIndentation()
    {
        var result = new IfTrueWrapTransform().Apply(
            "def main():\n    if x:\n        print(1)\n");
        Assert.Contains("        if True:\n            print(1)", result, StringComparison.Ordinal);
    }

    [Fact]
    public void PassInsertion_UsesTheOriginalIndentationString()
    {
        var result = new PassInsertionTransform().Apply("def main():\n\tprint(1)\n");
        Assert.Contains("\tpass\n\tprint(1)", result, StringComparison.Ordinal);
    }

    [Fact]
    public void AlphaRename_RenamesEveryOccurrenceOfALocal()
    {
        var result = new AlphaRenameTransform().Apply(
            "def main():\n    count: int = 1\n    print(count + count)\n");
        Assert.Contains("count_r: int = 1", result, StringComparison.Ordinal);
        Assert.Contains("print(count_r + count_r)", result, StringComparison.Ordinal);
    }

    /// <summary>
    /// Renaming must not touch a member of the same name, a keyword argument, or text inside a
    /// literal — each would either break compilation or change output.
    /// </summary>
    [Theory]
    [InlineData("def main():\n    count: int = 1\n    print(obj.count)\n")]
    [InlineData("def main():\n    count: int = 1\n    f(count=2)\n")]
    [InlineData("def main():\n    count: int = 1\n    print(\"count\")\n")]
    public void AlphaRename_DeclinesWhenTheNameMeansSomethingElseToo(string source)
    {
        Assert.Equal(source, new AlphaRenameTransform().Apply(source));
    }

    [Fact]
    public void AddRedundantTypeAnnotation_AnnotatesASoleIntBinding()
    {
        var result = new AddRedundantTypeAnnotationTransform().Apply(
            "def main():\n    count = 42\n    print(count)\n");
        Assert.Contains("count: int = 42", result, StringComparison.Ordinal);
    }

    /// <summary>
    /// Annotating a REassignment declares a new variable rather than restating an inferred type, and
    /// a literal that does not fit <c>int</c> infers <c>long</c> — both would change the program.
    /// </summary>
    [Theory]
    [InlineData("def main():\n    count = 1\n    count = 2\n    print(count)\n")]
    [InlineData("def main():\n    count = 99999999999\n    print(count)\n")]
    [InlineData("def main():\n    for count in range(3):\n        pass\n    count = 1\n    print(count)\n")]
    [InlineData("class C:\n    count = 1\n")]
    public void AddRedundantTypeAnnotation_DeclinesWhenIntWouldBeWrong(string source)
    {
        Assert.Equal(source, new AddRedundantTypeAnnotationTransform().Apply(source));
    }

    // ---- masking ----

    [Fact]
    public void MaskedSource_BlanksLiteralsAndComments_PreservingIndices()
    {
        var masked = MaskedSource.Of("x = \"a + b\"  # 1 + 2\ny = 3\n");
        Assert.Equal(masked.Source.Length, masked.Masked.Length);
        Assert.DoesNotContain("a + b", masked.Masked, StringComparison.Ordinal);
        Assert.DoesNotContain("1 + 2", masked.Masked, StringComparison.Ordinal);
        Assert.Contains("y = 3", masked.Masked, StringComparison.Ordinal);
    }

    [Fact]
    public void MaskedSource_TreatsTripleQuotedBodiesAsNonCode()
    {
        var masked = MaskedSource.Of("def f():\n    \"\"\"\n    def inner():\n    \"\"\"\n    return 1\n");
        Assert.False(masked.StartsStatement(2), "a line inside a docstring does not start a statement");
        Assert.True(masked.StartsStatement(4));
    }

    [Fact]
    public void MaskedSource_TracksContinuationLines()
    {
        var masked = MaskedSource.Of("def main():\n    foo(\n        1,\n        2)\n    print(1)\n");
        Assert.False(masked.StartsStatement(2), "a line inside an unclosed call is a continuation");
        Assert.True(masked.StartsStatement(4));
    }

    [Fact]
    public void MaskedSource_ClassifiesTheEnclosingBlock()
    {
        var masked = MaskedSource.Of(
            "class C:\n" +
            "    field: int = 1\n" +
            "\n" +
            "    def m(self) -> None:\n" +
            "        if True:\n" +
            "            local: int = 2\n");
        Assert.Equal("class", masked.EnclosingBlockKind(1));
        Assert.Equal("def", masked.EnclosingBlockKind(5));
    }
}
