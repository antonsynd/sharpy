using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>
/// Replacement-field grammar matrix (#1814, #1815, #1862): every f-string / t-string hole is
/// rendered by ONE Python-format engine, and holes and nested spec fields evaluate in SOURCE order.
/// A plain hole is <c>Builtins.Str(v)</c> (so <c>f"{None}"</c> → <c>None</c>); a conversion is
/// <c>Repr</c>/<c>Str</c>/<c>Ascii</c>; a spec'd hole is <c>Sharpy.PyFormat.Apply(v, spec)</c>
/// (python-3.12-accurate). A STATIC invalid spec is refused at compile time as SPY0609 with CPython's
/// <c>ValueError</c>/<c>TypeError</c> wording; a DYNAMIC (nested-field) invalid spec raises a runtime
/// <c>ValueError</c>/<c>TypeError</c>.
///
/// <para>Axes (representative combinations, not the full cross-product): spec component {none, bare
/// width, fill+align×4, sign×3, <c>#</c>, <c>0</c>, grouping×2, precision, <c>.N</c> no type,
/// type ∈ {d,f,e,g,x,X,o,b,c,n,%,s}, <c>z</c>, combined} × value kind {int, float, str, bool,
/// <c>int | None</c> (set/None), <c>object</c> (None), <c>None</c> literal, <c>int?</c>
/// (Some/None())} × conversion {none, <c>!r</c>, <c>!s</c>, <c>!a</c>, <c>=</c>, <c>=:spec</c>} ×
/// nesting {static, <c>{w}</c> width, <c>.{p}</c> precision, <c>{fill}{align}{w}</c>} × ordering
/// (the #1862 core) × static/dynamic refusals.</para>
///
/// <para>Every ExpectedOutput is pinned from python3 (3.12). The full green set was executed through
/// the built <c>sharpyc</c> and diffed against the identical python3 program on 2026-09-15 — the diff
/// was empty (PARITY: IDENTICAL). The pinning oracle:
/// <code>
/// python3 -c 'x=5;y=3.14159;s="ab";b=True; print(f"[{5:5}]", f"[{y:.2f}]", f"[{None}]", f"[{s:&gt;4}]")'
/// </code>
/// Every hole is wrapped in <c>[...]</c> so leading/trailing padding is load-bearing and
/// exact-matchable (the stdout is compared byte-for-byte after CRLF normalization — no trimming, no
/// whitespace collapse, unlike a trace-style matrix).</para>
///
/// <para>Depth-2 spec nesting (<c>f"{x:{y:{z}}}"</c>) is the <c>nest.depth2</c> cell: python accepts it (→ <c>  5</c>)
/// and Sharpy matches it after the #1884 lexer fix — a self-introduced Phase 2 regression this
/// matrix surfaced and the lead repaired before landing. Both depths are covered.</para>
///
/// <para><see cref="KnownRed"/> is EMPTY at landing: every cell is green. A cell that regresses is a
/// real defect, not an allowlist entry.</para>
/// </summary>
[Collection("HeavyCompilation")]
public class FStringReplacementFieldMatrixTests : IntegrationTestBase
{
    public FStringReplacementFieldMatrixTests(ITestOutputHelper output) : base(output) { }

    /// <summary>
    /// Cells expected to fail at landing. EMPTY: every cell below is python-pinned and green. This
    /// roster exists so a future regression is parked with an issue rather than silently deleted —
    /// but it must trend to empty (drain on fix).
    /// </summary>
    private static readonly HashSet<string> KnownRed = new();

    /// <summary>An output cell (green): renders <paramref name="output"/> exactly.</summary>
    private sealed record Cell(
        string Label,
        string Source,
        string? ExpectedOutput = null,
        string? StaticRefuseMessage = null,
        string? DynamicRefuseSubstring = null);

    [Fact]
    [Trait("Category", "Conformance")]
    public void FStringReplacementFieldMatrix_AllCellsPass()
    {
        Assert.Empty(KnownRed);

        var failures = new List<string>();
        var cells = GenerateCells().ToList();

        Assert.Equal(cells.Count, cells.Select(c => c.Label).Distinct().Count());

        foreach (var cell in cells)
        {
            var result = CompileAndExecute(cell.Source, executionTimeoutMs: 15_000);

            if (cell.StaticRefuseMessage != null)
            {
                // Static invalid spec: SPY0609 at compile time, message = CPython's wording.
                if (result.Success)
                {
                    failures.Add($"{cell.Label}: expected SPY0609 refusal but it compiled/ran (output '{result.StandardOutput.TrimEnd()}')");
                    continue;
                }
                var spy0609 = result.RawDiagnostics
                    .Where(d => d.Code == DiagnosticCodes.SemanticOverflow.InvalidFormatSpecification)
                    .ToList();
                if (spy0609.Count == 0)
                {
                    failures.Add($"{cell.Label}: expected SPY0609 but got: {string.Join("; ", result.RawDiagnostics.Select(d => $"{d.Code}: {d.Message}"))}");
                }
                else if (!spy0609.Any(d => d.Message.Contains(cell.StaticRefuseMessage, StringComparison.Ordinal)))
                {
                    failures.Add($"{cell.Label}: SPY0609 message mismatch — expected substring '{cell.StaticRefuseMessage}', got: {string.Join("; ", spy0609.Select(d => d.Message))}");
                }
                continue;
            }

            if (cell.DynamicRefuseSubstring != null)
            {
                // Dynamic (nested-field) invalid spec: the run fails with a runtime ValueError/TypeError.
                if (result.Success)
                {
                    failures.Add($"{cell.Label}: expected a runtime refusal but the program succeeded (output '{result.StandardOutput.TrimEnd()}')");
                    continue;
                }
                var haystack = result.StandardError + "\n" + string.Join("\n", result.CompilationErrors);
                if (!haystack.Contains(cell.DynamicRefuseSubstring, StringComparison.Ordinal))
                {
                    failures.Add($"{cell.Label}: runtime refusal message mismatch — expected substring '{cell.DynamicRefuseSubstring}', got stderr: {result.StandardError.Trim()}");
                }
                continue;
            }

            // Output cell: exact stdout (padding is load-bearing — no trim, no whitespace collapse).
            if (!result.Success)
            {
                failures.Add($"{cell.Label}: failed to compile/run: {string.Join("; ", result.CompilationErrors)}");
                continue;
            }
            var actual = result.StandardOutput.Replace("\r\n", "\n");
            if (actual != cell.ExpectedOutput)
                failures.Add($"{cell.Label}: output mismatch — expected {Escape(cell.ExpectedOutput!)}, got {Escape(actual)}");
        }

        // Value-kind totality anchor: every value kind the engine distinguishes has at least one cell.
        // A value kind added to the matrix must arrive with its cell.
        var valueKinds = new[]
        {
            "int.none", "float.none", "str.none", "bool.none", "none.literal",
            "union.none", "object.none", "optional.some",
        };
        // Conversion totality anchor.
        var conversions = new[]
        {
            "conv.repr_str", "conv.str_int", "conv.ascii", "conv.selfdoc", "conv.selfdoc_spec",
        };
        // Nesting totality anchor (depth-1 and depth-2).
        var nesting = new[] { "nest.width", "nest.prec", "nest.fill_align_w", "nest.fillalignw_vars", "nest.depth2" };
        // Refusal totality anchor: static SPY0609 and a dynamic runtime error.
        var refusals = new[] { "refuse.str_d", "refuse.none_spec", "refuse.dynamic_int_q" };

        var labels = cells.Select(c => c.Label).ToHashSet();
        var missing = valueKinds.Concat(conversions).Concat(nesting).Concat(refusals)
            .Where(l => !labels.Contains(l)).ToList();

        Output.WriteLine($"F-string replacement-field cells: {cells.Count}  Failures: {failures.Count}");
        foreach (var f in failures)
            Output.WriteLine($"  {f}");

        Assert.True(missing.Count == 0,
            "anchor labels absent (an axis is not represented): " + string.Join(", ", missing));
        Assert.True(failures.Count == 0,
            $"Replacement-field grammar matrix (#1814, #1815, #1862): {failures.Count} of {cells.Count} cells failed.\n"
            + string.Join("\n", failures.Select(f => "  " + f)));
    }

    private static string Escape(string s) => "'" + s.Replace("\n", "\\n") + "'";

    // A program that declares locals (decls) and prints one f-string.
    private static string Prog(string decls, string fstringBody) =>
        "def main() -> None:\n" + decls + "    print(f\"" + fstringBody + "\")\n";

    private static Cell Out(string label, string decls, string fstringBody, string expected) =>
        new(label, Prog(decls, fstringBody), ExpectedOutput: expected);

    private static Cell Refuse(string label, string decls, string fstringBody, string message) =>
        new(label, Prog(decls, fstringBody), StaticRefuseMessage: message);

    private const string Int = "";
    private const string Flt = "    y: float = 3.14159\n";
    private const string Str = "    s: str = \"ab\"\n";
    private const string Bool = "    b: bool = True\n";

    private static IEnumerable<Cell> GenerateCells()
    {
        // ---- spec component × int (literal operands: statically Integral) --------------------------
        // python3 -c 'print(f"[{5}]", f"[{5:5}]", f"[{5:<5}]", f"[{5:>5}]", f"[{5:^5}]", f"[{5:=5}]", f"[{5:*^7}]")'
        yield return Out("int.none", Int, "[{5}]", "[5]\n");
        yield return Out("int.width5", Int, "[{5:5}]", "[    5]\n");
        yield return Out("int.fill_left", Int, "[{5:<5}]", "[5    ]\n");
        yield return Out("int.fill_right", Int, "[{5:>5}]", "[    5]\n");
        yield return Out("int.fill_center", Int, "[{5:^5}]", "[  5  ]\n");
        yield return Out("int.fill_eq", Int, "[{5:=5}]", "[    5]\n");
        yield return Out("int.fillchar", Int, "[{5:*^7}]", "[***5***]\n");
        // python3 -c 'print(f"[{5:+}]", f"[{5:-}]", f"[{5: }]", f"[{5:#x}]", f"[{5:#b}]", f"[{5:05}]")'
        yield return Out("int.sign_plus", Int, "[{5:+}]", "[+5]\n");
        yield return Out("int.sign_minus", Int, "[{5:-}]", "[5]\n");
        yield return Out("int.sign_space", Int, "[{5: }]", "[ 5]\n");
        yield return Out("int.hash_x", Int, "[{5:#x}]", "[0x5]\n");
        yield return Out("int.hash_b", Int, "[{5:#b}]", "[0b101]\n");
        yield return Out("int.zero", Int, "[{5:05}]", "[00005]\n");
        // python3 -c 'print(f"[{1234567:,}]", f"[{1234567:_}]", f"[{5:d}]", f"[{5:x}]", f"[{255:X}]", f"[{5:o}]", f"[{5:b}]", f"[{65:c}]", f"[{5:n}]")'
        yield return Out("int.group_comma", Int, "[{1234567:,}]", "[1,234,567]\n");
        yield return Out("int.group_under", Int, "[{1234567:_}]", "[1_234_567]\n");
        yield return Out("int.type_d", Int, "[{5:d}]", "[5]\n");
        yield return Out("int.type_x", Int, "[{5:x}]", "[5]\n");
        yield return Out("int.type_X", Int, "[{255:X}]", "[FF]\n");
        yield return Out("int.type_o", Int, "[{5:o}]", "[5]\n");
        yield return Out("int.type_b", Int, "[{5:b}]", "[101]\n");
        yield return Out("int.type_c", Int, "[{65:c}]", "[A]\n");
        yield return Out("int.type_n", Int, "[{5:n}]", "[5]\n");
        // python3 -c 'print(f"[{5:e}]", f"[{5:f}]", f"[{5:g}]", f"[{5:%}]", f"[{1234567:12,}]", f"[{5:=+5}]", f"[{5:.2f}]")'
        yield return Out("int.type_e", Int, "[{5:e}]", "[5.000000e+00]\n");
        yield return Out("int.type_f", Int, "[{5:f}]", "[5.000000]\n");
        yield return Out("int.type_g", Int, "[{5:g}]", "[5]\n");
        yield return Out("int.type_pct", Int, "[{5:%}]", "[500.000000%]\n");
        yield return Out("int.combined_12comma", Int, "[{1234567:12,}]", "[   1,234,567]\n");
        yield return Out("int.combined_eqplus5", Int, "[{5:=+5}]", "[+   5]\n");
        yield return Out("int.prec2f", Int, "[{5:.2f}]", "[5.00]\n");

        // ---- spec component × float ---------------------------------------------------------------
        // python3 -c 'y=3.14159; print(f"[{y}]", f"[{y:6}]", f"[{y:.2f}]", f"[{y:.3}]", f"[{y:e}]", f"[{y:g}]", f"[{0.1234:.1%}]")'
        yield return Out("float.none", Flt, "[{y}]", "[3.14159]\n");
        yield return Out("float.width6", Flt, "[{y:6}]", "[3.14159]\n");
        yield return Out("float.prec2f", Flt, "[{y:.2f}]", "[3.14]\n");
        yield return Out("float.prec3_notype", Flt, "[{y:.3}]", "[3.14]\n");
        yield return Out("float.type_e", Flt, "[{y:e}]", "[3.141590e+00]\n");
        yield return Out("float.type_g", Flt, "[{y:g}]", "[3.14159]\n");
        yield return Out("float.type_pct", Int, "[{0.1234:.1%}]", "[12.3%]\n");
        // python3 -c 'y=3.14159; nz=-0.0; print(f"[{y:8.2f}]", f"[{y:07.2f}]", f"[{nz:z.1f}]")'
        yield return Out("float.combined_8_2f", Flt, "[{y:8.2f}]", "[    3.14]\n");
        yield return Out("float.combined_07_2f", Flt, "[{y:07.2f}]", "[0003.14]\n");
        yield return Out("float.z_negzero", "    nz: float = -0.0\n", "[{nz:z.1f}]", "[0.0]\n");

        // ---- spec component × str -----------------------------------------------------------------
        // python3 -c 's="ab"; print(f"[{s}]", f"[{s:4}]", f"[{s:>4}]", f"[{s:*^6}]", f"[{s:.1}]", f"[{s:s}]")'
        yield return Out("str.none", Str, "[{s}]", "[ab]\n");
        yield return Out("str.width4", Str, "[{s:4}]", "[ab  ]\n");
        yield return Out("str.fill_right", Str, "[{s:>4}]", "[  ab]\n");
        yield return Out("str.fill_center", Str, "[{s:*^6}]", "[**ab**]\n");
        yield return Out("str.prec1", Str, "[{s:.1}]", "[a]\n");
        yield return Out("str.type_s", Str, "[{s:s}]", "[ab]\n");

        // ---- spec component × bool (bool formats as int under a non-empty spec) --------------------
        // python3 -c 'b=True; print(f"[{b}]", f"[{b:6}]", f"[{b:d}]", f"[{b:>6}]", f"[{b:f}]")'
        yield return Out("bool.none", Bool, "[{b}]", "[True]\n");
        yield return Out("bool.width6", Bool, "[{b:6}]", "[     1]\n");
        yield return Out("bool.type_d", Bool, "[{b:d}]", "[1]\n");
        yield return Out("bool.fill_right6", Bool, "[{b:>6}]", "[     1]\n");
        yield return Out("bool.type_f", Bool, "[{b:f}]", "[1.000000]\n");

        // ---- value kind: None / union / optional / object (all render None as "None") -------------
        // python3 -c 'print(f"[{None}]", f"[{None!r}]", f"[{None!s}]", f"[{None=}]")'
        yield return Out("none.literal", Int, "[{None}]", "[None]\n");
        yield return Out("none.repr", Int, "[{None!r}]", "[None]\n");
        yield return Out("none.str", Int, "[{None!s}]", "[None]\n");
        yield return Out("none.selfdoc", Int, "[{None=}]", "[None=None]\n");
        // python3 -c 'n=None; nn=7; o=None; print(f"[{n}]", f"[{nn}]", f"[{o}]")'  (int|None, object)
        yield return Out("union.none", "    n: int | None = None\n", "[{n}]", "[None]\n");
        yield return Out("union.set", "    nn: int | None = 7\n", "[{nn}]", "[7]\n");
        yield return Out("object.none", "    o: object = None\n", "[{o}]", "[None]\n");
        // int? (Sharpy Optional): Some(5) -> "5", None() -> "None"
        yield return Out("optional.some", "    p: int? = Some(5)\n", "[{p}]", "[5]\n");
        yield return Out("optional.none", "    q: int? = None()\n", "[{q}]", "[None]\n");

        // ---- conversions --------------------------------------------------------------------------
        // python3 -c 's="ab"; y=3.14159; x=5; print(f"[{s!r}]", f"[{5!s}]", f"[{x=}]", f"[{y=:.2f}]", f"[{s!r:>6}]", f"[{s=}]")'
        yield return Out("conv.repr_str", Str, "[{s!r}]", "['ab']\n");
        yield return Out("conv.str_int", Int, "[{5!s}]", "[5]\n");
        yield return Out("conv.ascii", "    s: str = \"café\"\n", "[{s!a}]", "['caf\\xe9']\n");
        yield return Out("conv.selfdoc", "    x: int = 5\n", "[{x=}]", "[x=5]\n");
        yield return Out("conv.selfdoc_spec", Flt, "[{y=:.2f}]", "[y=3.14]\n");
        yield return Out("conv.repr_spec", Str, "[{s!r:>6}]", "[  'ab']\n");
        yield return Out("conv.selfdoc_str", Str, "[{s=}]", "[s='ab']\n");

        // ---- nesting (depth-1; nested spec fields evaluate as expressions) -------------------------
        // python3 -c 'w=3; prec=3; fill="*"; align=">"; print(f"[{5:{w}}]", f"[{3.14159:.{prec}f}]", f"[{5:*>{w}}]", f"[{5:{fill}{align}{w}}]")'
        yield return Out("nest.width", "    w: int = 3\n", "[{5:{w}}]", "[  5]\n");
        yield return Out("nest.prec", "    y: float = 3.14159\n    prec: int = 3\n", "[{y:.{prec}f}]", "[3.142]\n");
        yield return Out("nest.fill_align_w", "    w: int = 3\n", "[{5:*>{w}}]", "[**5]\n");
        yield return Out("nest.fillalignw_vars",
            "    w: int = 3\n    fill: str = \"*\"\n    align: str = \">\"\n",
            "[{5:{fill}{align}{w}}]", "[**5]\n");
        // Depth-2: the inner field {z} formats y, whose result is the width spec for the hole (#1884).
        // python3 -c 'y=3; z="d"; print(f"[{5:{y:{z}}}]")'  -> "[  5]"  (format(3,"d")="3" -> width 3)
        yield return Out("nest.depth2", "    y: int = 3\n    z: str = \"d\"\n", "[{5:{y:{z}}}]", "[  5]\n");

        // ---- ordering (#1862): holes and nested fields evaluate left-to-right ----------------------
        // The list has 3 elements so the correct output (pop first) differs from the buggy output
        // (len first): correct "1 2", buggy "1 3".
        // python3 -c 'xs=[1,2,3]; print(f"{xs.pop(0)} {len([v for v in xs])}")'  -> 1 2
        yield return new Cell("order.plain",
            "def main() -> None:\n    xs: list[int] = [1, 2, 3]\n    print(f\"{xs.pop(0)} {len([v for v in xs])}\")\n",
            ExpectedOutput: "1 2\n");
        // python3 -c 'w=["left","a","b"]; print(f"{w.pop(0)} {len([v for v in w])}")'  -> left 2
        yield return new Cell("order.fillalign",
            "def main() -> None:\n    words: list[str] = [\"left\", \"a\", \"b\"]\n    print(f\"{words.pop(0)} {len([v for v in words])}\")\n",
            ExpectedOutput: "left 2\n");
        // python3 -c 'xs=[1,2,3]; print(f"{xs.pop(0):<3}{len([v for v in xs])}")'  -> "1  2"
        yield return new Cell("order.spec",
            "def main() -> None:\n    xs: list[int] = [1, 2, 3]\n    print(f\"{xs.pop(0):<3}{len([v for v in xs])}\")\n",
            ExpectedOutput: "1  2\n");
        // The later hole supplies the earlier hole's spec width; pop-first leaves width 2 -> " 1".
        // python3 -c 'xs=[1,2,3]; print(f"{xs.pop(0):{len([v for v in xs])}}")'  -> " 1"
        yield return new Cell("order.nested",
            "def main() -> None:\n    xs: list[int] = [1, 2, 3]\n    print(f\"{xs.pop(0):{len([v for v in xs])}}\")\n",
            ExpectedOutput: " 1\n");
        // t-string twin: rendering the Template joins interpolations in source order.
        // python3 analog: xs=[1,2,3]; print(xs.pop(0), len([v for v in xs]))  -> 1 2
        yield return new Cell("order.tstring_render",
            "def main() -> None:\n    xs: list[int] = [1, 2, 3]\n    tmpl: Template = t\"{xs.pop(0)} {len([v for v in xs])}\"\n    print(tmpl)\n",
            ExpectedOutput: "1 2\n");
        // t-string twin: Interpolations are constructed in source order (values[0]=1, values[1]=2).
        yield return new Cell("order.tstring_values",
            "def main() -> None:\n    xs: list[int] = [1, 2, 3]\n    tmpl: Template = t\"{xs.pop(0)} {len([v for v in xs])}\"\n    print(tmpl.values[0], tmpl.values[1])\n",
            ExpectedOutput: "1 2\n");

        // ---- static refusals (SPY0609, message = CPython's ValueError/TypeError text) --------------
        // python3 -c 's="ab"; f"{s:d}"'  -> ValueError: Unknown format code 'd' for object of type 'str'
        yield return Refuse("refuse.str_d", Str, "[{s:d}]", "Unknown format code 'd' for object of type 'str'");
        yield return Refuse("refuse.str_f", Str, "[{s:5.2f}]", "Unknown format code 'f' for object of type 'str'");
        yield return Refuse("refuse.int_s", Int, "[{5:s}]", "Unknown format code 's' for object of type 'int'");
        yield return Refuse("refuse.float_d", Flt, "[{y:d}]", "Unknown format code 'd' for object of type 'float'");
        // python3 -c 'f"{None:>6}"'  -> TypeError: unsupported format string passed to NoneType.__format__
        yield return Refuse("refuse.none_spec", Int, "[{None:>6}]", "unsupported format string passed to NoneType.__format__");
        // python3 -c 'f"{5:5.2}"'  -> ValueError: Precision not allowed in integer format specifier
        yield return Refuse("refuse.int_prec", Int, "[{5:5.2}]", "Precision not allowed in integer format specifier");
        yield return Refuse("refuse.int_q", Int, "[{5:q}]", "Unknown format code 'q' for object of type 'int'");

        // ---- dynamic refusal (nested-field spec, unknown until runtime -> runtime ValueError) ------
        // python3 -c 'x=5; bad="q"; f"{x:{bad}}"'  -> ValueError: Unknown format code 'q' for object of type 'int'
        yield return new Cell("refuse.dynamic_int_q",
            "def main() -> None:\n    x: int = 5\n    bad: str = \"q\"\n    print(f\"[{x:{bad}}]\")\n",
            DynamicRefuseSubstring: "Unknown format code 'q' for object of type 'int'");
    }
}
