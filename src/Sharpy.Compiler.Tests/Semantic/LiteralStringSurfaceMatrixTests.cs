using System.Linq;
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

    #region Wrapper axis — LiteralString?, LiteralString | None, Template?, Template | None (#1781)

    /// <summary>
    /// Execution evidence for #1781: <c>x: LiteralString? = Some("a"); print(x)</c> prints <c>a</c>.
    /// Before the fix, the LiteralString arm in <see cref="Sharpy.Compiler.Semantic.TypeResolver"/>
    /// returned early, skipping the modifier tail that wraps with <see cref="Sharpy.Compiler.Semantic.OptionalType"/>.
    /// </summary>
    [Fact]
    public void LiteralStringOptional_SomeValue_Prints()
    {
        var result = CompileAndExecuteWithGC(
            "def main() -> None:\n" +
            "    x: LiteralString? = Some(\"a\")\n" +
            "    print(x)\n");
        result.Success.Should().BeTrue(
            "LiteralString? = Some(\"a\") must compile. Errors:\n"
            + string.Join("\n", result.CompilationErrors));
        result.StandardOutput.Should().Be("a\n");
    }

    /// <summary>
    /// Narrowed read on <c>LiteralString?</c>: after <c>if x is not None:</c>, the narrowed type
    /// is <c>LiteralStringType</c> (subtype of str), so <c>x.upper()</c> must resolve and print <c>A</c>.
    /// </summary>
    [Fact]
    public void LiteralStringOptional_NarrowedRead_PrintsUppercase()
    {
        var result = CompileAndExecuteWithGC(
            "def main() -> None:\n" +
            "    x: LiteralString? = Some(\"a\")\n" +
            "    if x is not None:\n" +
            "        print(x.upper())\n");
        result.Success.Should().BeTrue(
            "narrowed LiteralString? read must compile. Errors:\n"
            + string.Join("\n", result.CompilationErrors));
        result.StandardOutput.Should().Be("A\n");
    }

    /// <summary>
    /// <c>LiteralString | None</c> with a literal value prints the value.
    /// </summary>
    [Fact]
    public void LiteralStringNullable_Value_Prints()
    {
        var result = CompileAndExecuteWithGC(
            "def main() -> None:\n" +
            "    x: LiteralString | None = \"a\"\n" +
            "    print(x)\n");
        result.Success.Should().BeTrue(
            "LiteralString | None = \"a\" must compile. Errors:\n"
            + string.Join("\n", result.CompilationErrors));
        result.StandardOutput.Should().Be("a\n");
    }

    /// <summary>
    /// Narrowed read on <c>LiteralString | None</c>: <c>if x is not None: print(x.upper())</c>.
    /// </summary>
    [Fact]
    public void LiteralStringNullable_NarrowedRead_PrintsUppercase()
    {
        var result = CompileAndExecuteWithGC(
            "def main() -> None:\n" +
            "    x: LiteralString | None = \"a\"\n" +
            "    if x is not None:\n" +
            "        print(x.upper())\n");
        result.Success.Should().BeTrue(
            "narrowed LiteralString | None read must compile. Errors:\n"
            + string.Join("\n", result.CompilationErrors));
        result.StandardOutput.Should().Be("A\n");
    }

    /// <summary>
    /// <c>Template? = None()</c> compiles and runs (the Template arm now goes through the modifier tail).
    /// </summary>
    [Fact]
    public void TemplateOptional_None_Runs()
    {
        var result = CompileAndExecuteWithGC(
            "def main() -> None:\n" +
            "    x: Template? = None()\n" +
            "    print(x)\n");
        result.Success.Should().BeTrue(
            "Template? = None() must compile. Errors:\n"
            + string.Join("\n", result.CompilationErrors));
    }

    /// <summary>
    /// <c>Template | None = None</c> compiles and runs.
    /// </summary>
    [Fact]
    public void TemplateNullable_None_Runs()
    {
        var result = CompileAndExecuteWithGC(
            "def main() -> None:\n" +
            "    x: Template | None = None\n" +
            "    print(x)\n");
        result.Success.Should().BeTrue(
            "Template | None = None must compile. Errors:\n"
            + string.Join("\n", result.CompilationErrors));
    }

    #endregion

    #region Form axis (#1741, P7.T) — LiteralDerivation's PEP 675 form set, measured against ddc5f5aac

    /// <summary>
    /// Form × input-kind × position cells (probe names l01-l20 from the plan-d35e69 ledger).
    /// <see cref="ExpectsAcceptance"/> cells type as literal-derived and PRINT a python3-matching
    /// value; refusing cells assert the MEASURED diagnostic code — a store position's code is not
    /// uniformly SPY0220 (a `return` refusal is SPY0260; an argument refusal names the parameter),
    /// so each cell records what was actually observed against the built compiler, not an assumed
    /// code.
    /// </summary>
    public record FormCell(
        string Name, string Source, bool ExpectsAcceptance,
        string? ExpectedOutput, string? ExpectedCode, string? ExpectedErrorSubstring);

    private static IReadOnlyList<FormCell> BuildFormCells()
    {
        var cells = new List<FormCell>();

        void Accept(string name, string source, string expectedOutput) =>
            cells.Add(new FormCell(name, source, true, expectedOutput, null, null));

        void Refuse(string name, string source, string code, string errorSubstring) =>
            cells.Add(new FormCell(name, source, false, null, code, errorSubstring));

        // --- Accepting: PEP 675 forms over a derived receiver/operands ---

        // l01: f-string, one hole, the hole is a LiteralString-typed name — position: declaration.
        Accept("l01_fstring_derived_hole_declaration", @"
def main() -> None:
    q: LiteralString = ""SELECT""
    z: LiteralString = f""{q} 1""
    print(z)
", "SELECT 1\n");

        // l04: .format with a LiteralString-typed-name arg and a literal arg — position: argument
        // (the call result is passed into a LiteralString-typed parameter).
        Accept("l04_format_argument", @"
def use(v: LiteralString) -> None:
    print(v)

def main() -> None:
    lit: LiteralString = ""x""
    use(""{} and {}"".format(lit, ""z""))
", "x and z\n");

        // l05: .join over a literal list DISPLAY (not a variable, see l12's refusal twin) —
        // position: field (a class-level field initializer).
        Accept("l05_join_literal_display_field", @"
class Config:
    label: LiteralString = "", "".join([""a"", ""b""])

def main() -> None:
    c: Config = Config()
    print(c.label)
", "a, b\n");

        // l06: literal * int (repetition on a literal, not a name) — position: return.
        Accept("l06_literal_repeat_return", @"
def make() -> LiteralString:
    return ""ab"" * 3

def main() -> None:
    print(make())
", "ababab\n");

        // l07: PEP 675 method call, receiver a LiteralString-typed name — position: declaration.
        Accept("l07_upper_declaration", @"
def main() -> None:
    lit: LiteralString = ""select""
    z: LiteralString = lit.upper()
    print(z)
", "SELECT\n");

        // l08: PEP 675 method call with two literal arguments — position: declaration. python3:
        // 'select'.replace('e', '3') replaces BOTH 'e's (no count given).
        Accept("l08_replace_declaration", @"
def main() -> None:
    lit: LiteralString = ""select""
    z: LiteralString = lit.replace(""e"", ""3"")
    print(z)
", "s3l3ct\n");

        // l10: `+` concatenation, left operand a LiteralString-typed name read from MODULE scope
        // (not a local, unlike IdentifierRead_IsLiteralDerived_ConcatIntoLiteralStringSlot above) —
        // position: field. Locks that a class-body field initializer can read an outer LiteralString
        // name and that CheckIdentifier's derived-fact (#1766) survives into that scope.
        Accept("l10_concat_identifier_field", @"
lit: LiteralString = ""he""

class Config:
    label: LiteralString = lit + ""llo""

def main() -> None:
    c: Config = Config()
    print(c.label)
", "hello\n");

        // l11: f-string with NO holes — derived vacuously (empty product) — position: declaration.
        Accept("l11_fstring_no_holes_declaration", @"
def main() -> None:
    z: LiteralString = f""literal""
    print(z)
", "literal\n");

        // l13: PEP 675 method call, receiver a LOCAL LiteralString-typed name — position: return.
        Accept("l13_strip_return", @"
def clean() -> LiteralString:
    lit: LiteralString = ""  hi  ""
    return lit.strip()

def main() -> None:
    print(clean())
", "hi\n");

        // l17: nested composition — f-string whose hole is itself a derived method call, then
        // concatenated with a literal — position: declaration.
        Accept("l17_nested_fstring_then_concat_declaration", @"
def main() -> None:
    lit: LiteralString = ""ab""
    z: LiteralString = f""{lit.upper()}"" + ""z""
    print(z)
", "ABz\n");

        // l18: repetition, operand a LiteralString-typed NAME (not a literal, unlike l06) —
        // position: argument.
        Accept("l18_repeat_identifier_argument", @"
def use(v: LiteralString) -> None:
    print(v)

def main() -> None:
    lit: LiteralString = ""ab""
    use(lit * 2)
", "abab\n");

        // l19: augmented assignment whose RHS is a derived f-string of the LHS itself —
        // position: augmented.
        Accept("l19_augmented_fstring", @"
def main() -> None:
    lit: LiteralString = ""ab""
    lit += f""{lit}""
    print(lit)
", "abab\n");

        // --- Must-refuse positive controls: a str-typed input stays SPY0220/SPY0260 even under an
        // otherwise-accepted form — the fact is bottom-up, never inferred from the form alone. These
        // anchor the guard: an implementation that accepted ANY str-typed input would turn every one
        // of these green, which is exactly what mutation 3 (l12) demonstrates.

        // l09: f-string hole is str-typed (not derived) — position: declaration.
        Refuse("l09_fstring_str_hole_declaration_refused", @"
def main() -> None:
    s: str = ""x""
    z: LiteralString = f""{s}""
    print(z)
", "SPY0220", "Cannot assign type 'str' to variable of type 'LiteralString'");

        // l12: .join over a list[str] VARIABLE (not a literal display, contrast l05) — even though
        // its elements happen to be literals, the variable's contents cannot be proven — position:
        // declaration. THE guard cell for mutation 3.
        Refuse("l12_join_list_variable_refused", @"
def main() -> None:
    items: list[str] = [""a"", ""b""]
    z: LiteralString = "", "".join(items)
    print(z)
", "SPY0220", "Cannot assign type 'str' to variable of type 'LiteralString'");

        // l14: PEP 675 method call, receiver derived but ONE ARGUMENT is str-typed (not derived) —
        // position: argument. Note the argument-position message names the PARAMETER, not "assign".
        Refuse("l14_replace_str_argument_refused", @"
def use(v: LiteralString) -> None:
    print(v)

def main() -> None:
    lit: LiteralString = ""ab""
    s: str = ""z""
    use(lit.replace(""a"", s))
", "SPY0220", "Cannot pass argument of type 'str' to parameter of type 'LiteralString'");

        // l15: PEP 675 method call, RECEIVER is str-typed (not derived) — position: return. The
        // return-position code is SPY0260, NOT SPY0220 (measured — a store-position code is not
        // uniform across positions).
        Refuse("l15_upper_str_receiver_return_refused", @"
def make() -> LiteralString:
    s: str = ""select""
    return s.upper()

def main() -> None:
    print(make())
", "SPY0260", "Cannot return type 'str' from function expecting 'LiteralString'");

        // l16: repetition, operand str-typed (not derived) — position: augmented.
        Refuse("l16_repeat_str_operand_augmented_refused", @"
def main() -> None:
    lit: LiteralString = ""ab""
    s: str = ""cd""
    lit += s * 2
    print(lit)
", "SPY0220", "Result type 'str' of augmented assignment is not assignable to target type 'LiteralString'");

        // --- Parse refusals: implicit concatenation never parses, in ANY position (spec-deliberate,
        // string_literals.md "No Implicit Concatenation") ---

        // l02: position — declaration.
        Refuse("l02_implicit_concat_declaration_parse_refused", @"
def main() -> None:
    z: LiteralString = ""a"" ""b""
    print(z)
", "SPY0103", "Expected end of statement");

        // l20: position — return (confirms the refusal is a PARSER fact, not context-dependent).
        Refuse("l20_implicit_concat_return_parse_refused", @"
def make() -> LiteralString:
    return ""a"" ""b""

def main() -> None:
    print(make())
", "SPY0103", "Expected end of statement");

        // --- `%` is not a Sharpy string operator (never a LiteralString form) ---
        Refuse("percent_operator_refused", @"
def main() -> None:
    lit: LiteralString = ""x""
    z: LiteralString = ""fmt=%s"" % lit
    print(z)
", "SPY0222", "does not support operator '%'");

        return cells;
    }

    public static IEnumerable<object[]> FormCells()
    {
        foreach (var cell in BuildFormCells())
            yield return new object[] { cell.Name, cell };
    }

    [Theory]
    [MemberData(nameof(FormCells))]
    public void FormAxis_ProducesMeasuredOutcome(string name, FormCell cell)
    {
        var result = CompileAndExecuteWithGC(cell.Source);

        if (cell.ExpectsAcceptance)
        {
            result.Success.Should().BeTrue(
                $"'{name}' must be literal-derived and compile. Errors:\n"
                + string.Join("\n", result.CompilationErrors));
            result.StandardOutput.Should().Be(cell.ExpectedOutput,
                $"'{name}' must print the python3-matching value");
        }
        else
        {
            result.Success.Should().BeFalse($"'{name}' must be refused (a str-typed input is not literal-derived)");
            result.RawDiagnostics.Should().Contain(
                d => d.Code == cell.ExpectedCode,
                $"'{name}' expected {cell.ExpectedCode}, got: {string.Join("; ", result.RawDiagnostics.Select(d => d.Code))}");
            string.Join("\n", result.CompilationErrors).Should().Contain(cell.ExpectedErrorSubstring!,
                $"'{name}' error message must name the str/LiteralString mismatch");
        }
    }

    /// <summary>The form axis is anchored to a literal so a dropped cell is not silent (same
    /// discipline as <see cref="TwinCellCount"/>): 12 accepting + 5 must-refuse + 2 parse-refusal +
    /// 1 `%`-refusal = 20.</summary>
    private const int FormAxisCellCount = 20;

    [Fact]
    public void FormAxisHasTheDeclaredCellCount()
    {
        BuildFormCells().Should().HaveCount(FormAxisCellCount,
            "every form cell added to BuildFormCells must raise this literal (§ totality anchored to literals)");
    }

    #endregion
}
