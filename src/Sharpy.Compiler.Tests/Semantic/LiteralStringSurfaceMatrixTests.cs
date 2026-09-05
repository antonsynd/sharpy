using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Twin-surface matrix: receiver {str, LiteralString} × operation.
/// The two columns must print IDENTICAL stdout — a LiteralString is a str at every value-use
/// route through <c>OperandView</c> (#1766). Each cell compiles and runs a program; when the
/// LiteralString column prints differently, <c>OperandView</c> missed a route.
///
/// <para>Discriminating cell: <c>x.split(",")</c> prints <c>['a', 'b']</c> vs <c>a b</c> —
/// that distinction is the proof that static-extension dispatch resolved <c>System.String.Split</c>
/// rather than falling back to a bare <c>__str__</c> path (#1741 binary cell).</para>
///
/// <para>Refusal twin: <c>x - "a"</c> must refuse identically (SPY0222) in both columns.
/// R-P control: <c>x += s</c> (s: str) stays SPY0220 on the LiteralString column only.</para>
/// </summary>
[Collection("HeavyCompilation")]
public class LiteralStringSurfaceMatrixTests : IntegrationTestBase
{
    public LiteralStringSurfaceMatrixTests(ITestOutputHelper output) : base(output) { }

    public record Cell(string Name, string StrSource, string LiteralStringSource,
        bool ExpectsRefusal, string? ExpectedOutput, string? ExpectedError);

    private static string WrapStr(string body) =>
        $"def main() -> None:\n    x: str = \"a,b\"\n    y: str = \"c\"\n{body}";

    private static string WrapLitStr(string body) =>
        $"def main() -> None:\n    x: LiteralString = \"a,b\"\n    y: LiteralString = \"c\"\n{body}";

    private static IReadOnlyList<Cell> BuildCells()
    {
        var cells = new List<Cell>();

        void AddTwin(string name, string bodyLines, string expected)
        {
            cells.Add(new Cell(name, WrapStr(bodyLines), WrapLitStr(bodyLines),
                false, expected, null));
        }

        void AddRefusalTwin(string name, string bodyLines, string errorSubstring)
        {
            cells.Add(new Cell(name, WrapStr(bodyLines), WrapLitStr(bodyLines),
                true, null, errorSubstring));
        }

        // --- Comparison operators ---
        AddTwin("eq", "    print(x == \"a,b\")", "True\n");
        AddTwin("neq", "    print(x != \"\")", "True\n");
        AddTwin("lt", "    print(x < \"b\")", "True\n");
        AddTwin("chained_cmp", "    print(\"a\" < x < \"d\")", "True\n");

        // --- Arithmetic operators ---
        AddTwin("concat", "    print(x + \"!\")", "a,b!\n");
        AddTwin("repeat", "    print(y * 2)", "cc\n");

        // --- Builtins ---
        AddTwin("len", "    print(len(x))", "3\n");
        AddTwin("index", "    print(x[0])", "a\n");
        AddTwin("slice", "    print(x[0:1])", "a\n");

        // --- Iteration ---
        AddTwin("for_in",
            "    result: str = \"\"\n    for c in x:\n        result = result + c\n    print(result)",
            "a,b\n");

        // --- Membership ---
        AddTwin("in", "    print(\"a\" in x)", "True\n");

        // --- Truthiness ---
        AddTwin("if_x",
            "    if x:\n        print(\"truthy\")\n    else:\n        print(\"falsy\")",
            "truthy\n");
        AddTwin("not_x", "    print(not x)", "False\n");

        // --- Conversions ---
        AddTwin("fstring", "    print(f\"{x}\")", "a,b\n");
        AddTwin("str_call", "    print(str(x))", "a,b\n");

        // --- String methods (discriminating: split prints list repr vs space-separated) ---
        AddTwin("upper", "    print(x.upper())", "A,B\n");
        AddTwin("split", "    print(x.split(\",\"))", "['a', 'b']\n");
        AddTwin("replace", "    print(x.replace(\"a\", \"z\", 1))", "z,b\n");

        // --- Dict key ---
        AddTwin("dict_key",
            "    d: dict[str, int] = {x: 1}\n    print(d[\"a,b\"])",
            "1\n");

        // --- list membership ---
        AddTwin("in_list",
            "    xs: list[str] = [\"a,b\", \"c\"]\n    print(x in xs)",
            "True\n");

        // --- isinstance ---
        AddTwin("isinstance", "    print(isinstance(x, str))", "True\n");

        // --- sorted ---
        AddTwin("sorted",
            "    print(sorted([y, x]))",
            "['a,b', 'c']\n");

        // --- match ---
        AddTwin("match",
            "    match x:\n        case \"a,b\":\n            print(\"matched\")\n        case _:\n            print(\"no\")",
            "matched\n");

        // --- Both LiteralString ---
        AddTwin("both_eq", "    print(x == y)", "False\n");

        // --- Refusal twin: subtraction refuses identically ---
        AddRefusalTwin("sub_refused", "    print(x - \"a\")",
            "does not support operator '-'");

        return cells;
    }

    public static IEnumerable<object[]> TwinCells()
    {
        foreach (var cell in BuildCells())
            yield return new object[] { cell.Name, cell };
    }

    [Theory]
    [MemberData(nameof(TwinCells))]
    public void TwinColumns_ProduceIdenticalOutput(string name, Cell cell)
    {
        var strResult = CompileAndExecuteWithGC(cell.StrSource);
        var litResult = CompileAndExecuteWithGC(cell.LiteralStringSource);

        if (cell.ExpectsRefusal)
        {
            strResult.Success.Should().BeFalse($"str column of '{name}' must refuse");
            litResult.Success.Should().BeFalse($"LiteralString column of '{name}' must refuse");

            string strErrors = string.Join("\n", strResult.CompilationErrors);
            string litErrors = string.Join("\n", litResult.CompilationErrors);
            strErrors.Should().Contain(cell.ExpectedError!,
                $"str column of '{name}' must contain expected error");
            litErrors.Should().Contain(cell.ExpectedError!,
                $"LiteralString column of '{name}' must contain expected error");
        }
        else
        {
            strResult.Success.Should().BeTrue(
                $"str column of '{name}' must compile. Errors:\n"
                + string.Join("\n", strResult.CompilationErrors));
            litResult.Success.Should().BeTrue(
                $"LiteralString column of '{name}' must compile. Errors:\n"
                + string.Join("\n", litResult.CompilationErrors));

            litResult.StandardOutput.Should().Be(strResult.StandardOutput,
                $"LiteralString column of '{name}' must match str column's stdout");

            if (cell.ExpectedOutput != null)
            {
                strResult.StandardOutput.Should().Be(cell.ExpectedOutput,
                    $"str column of '{name}' must match expected output");
            }
        }
    }

    /// <summary>
    /// R-P control: <c>x += s</c> (s: str) is SPY0220 on the LiteralString column ONLY — a
    /// LiteralString variable cannot be augmented-assigned from a str because the store seam
    /// checks the DECLARED type, not the value-use view.
    /// </summary>
    /// <summary>The twin axis is anchored to a literal so a dropped cell is not silent.</summary>
    private const int TwinCellCount = 25;

    [Fact]
    public void MatrixHasTheDeclaredCellCount()
    {
        BuildCells().Should().HaveCount(TwinCellCount,
            "every twin cell added to BuildCells must raise this literal (§ totality anchored to literals)");
    }

    /// <summary>
    /// A <c>LiteralString</c>-typed identifier READ is literal-derived (<c>SetLiteralDerived</c> in
    /// <c>CheckIdentifier</c>, plan-757fbb Decision 11), so <c>x + "b"</c> is still admissible into a
    /// <c>LiteralString</c> slot — #1741's binary cell, SPY0222 at dff55b2cd. The <c>str</c> column has
    /// no twin (a <c>str</c> is never literal-derived), so this is a single-column executing cell with
    /// the R-P control beside it: a non-literal operand makes the result a plain <c>str</c>.
    /// </summary>
    [Fact]
    public void IdentifierRead_IsLiteralDerived_ConcatIntoLiteralStringSlot()
    {
        var accepted = CompileAndExecuteWithGC(
            "def main() -> None:\n    x: LiteralString = \"a\"\n    z: LiteralString = x + \"b\"\n    w: LiteralString = (x + \"b\") + x\n    print(z, w)\n");
        accepted.Success.Should().BeTrue(
            $"x + \"b\" on a LiteralString read is literal-derived; got: {string.Join(" | ", accepted.CompilationErrors)}");
        accepted.StandardOutput.Should().Be("ab aba\n");

        var refused = CompileAndExecuteWithGC(
            "def main() -> None:\n    x: LiteralString = \"a\"\n    s: str = \"b\"\n    z: LiteralString = x + s\n    print(z)\n");
        refused.Success.Should().BeFalse("x + s (s: str) is not literal-derived — R-P");
        refused.RawDiagnostics.Should().Contain(d => d.Code == DiagnosticCodes.Semantic.TypeMismatch);
    }

    [Fact]
    public void AugmentedAssign_StrIntoLiteralString_IsRefusedOnlyInLiteralStringColumn()
    {
        var strSource = @"def main() -> None:
    x: str = ""hello""
    s: str = "" world""
    x += s
    print(x)
";
        var litSource = @"def main() -> None:
    x: LiteralString = ""hello""
    s: str = "" world""
    x += s
    print(x)
";
        var strResult = CompileAndExecuteWithGC(strSource);
        strResult.Success.Should().BeTrue(
            "str += str must succeed. Errors:\n" + string.Join("\n", strResult.CompilationErrors));
        strResult.StandardOutput.Should().Be("hello world\n");

        var litResult = CompileAndExecuteWithGC(litSource);
        litResult.Success.Should().BeFalse(
            "LiteralString += str must be REFUSED (store seam, R-P control)");
        litResult.RawDiagnostics.Should().Contain(
            d => d.Code == "SPY0220",
            "the refusal must be SPY0220 (type mismatch at the store seam)");
    }
}
