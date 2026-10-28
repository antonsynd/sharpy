using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// #2004 (Decision 16, rulings 4 and 14): <c>None</c> is not a type. Only a return slot spells "no
/// value" (void); in every value slot the annotation is SPY0614 with the <c>T | None</c> steer.
/// Before the fix every value-slot cell became C# <c>void</c> and died as SPY0599 (a syntax error in
/// the generated C#) or SPY0908 (CS0670) — no working program is refused.
/// </summary>
/// <remarks>
/// Cells: value slots {local, parameter, field, module variable, <c>list[None]</c>,
/// <c>dict[str, None]</c>, a function type's parameter <c>(None) -&gt; int</c> — written inline and as
/// an alias body <c>type Cb = (None) -&gt; int</c> (the only source of a parser <c>FunctionType</c>) —
/// <c>None?</c>, an alias whose body is <c>None</c> used as a value, a property getter's type, an
/// auto-property, a union case field} × exactly one SPY0614; a modifier over <c>None</c> in a RETURN
/// slot {<c>-&gt; None?</c>, <c>-&gt; None | None</c>, <c>-&gt; None !str</c>, a method's
/// <c>-&gt; None | None</c>, a function type's <c>() -&gt; None?</c>, a delegate's <c>-&gt; None?</c>, an
/// alias body <c>None?</c>, an alias of <c>None</c> used as <c>Unit?</c>} × exactly one SPY0614 (they
/// were SPY0599: <c>Optional&lt;void&gt;</c>, <c>void?</c>); controls {function return, method return, a
/// setter's <c>-&gt; None</c>, a function type's return <c>() -&gt; None</c>, an alias of <c>None</c>
/// used as a return, a delegate's return, <c>int | None</c> as a value and as a return} × run.
/// </remarks>
public class NoneAnnotationPositionMatrixTests : IntegrationTestBase
{
    public NoneAnnotationPositionMatrixTests(ITestOutputHelper output) : base(output) { }

    private static readonly Dictionary<string, (string Source, int Line, int Column)> RefusedCells = new()
    {
        ["local"] = ("def main() -> None:\n    x: None = None\n    print(1)\n", 2, 8),
        ["parameter"] = ("def f(x: None) -> int:\n    return 1\n\ndef main() -> None:\n    print(f(None))\n", 1, 10),
        ["field"] = ("class C:\n    v: None\n\n    def __init__(self) -> None:\n        self.v = None\n\ndef main() -> None:\n    print(1)\n", 2, 8),
        ["module_variable"] = ("X: None = None\n\ndef main() -> None:\n    print(1)\n", 1, 4),
        ["list_type_argument"] = ("def main() -> None:\n    xs: list[None] = []\n    print(len(xs))\n", 2, 14),
        ["dict_type_argument"] = ("def main() -> None:\n    d: dict[str, None] = {}\n    print(len(d))\n", 2, 18),
        ["function_type_parameter"] = ("def main() -> None:\n    f: (None) -> int = lambda x: 1\n    print(1)\n", 2, 9),
        // An alias body is the only place the parser builds a FunctionType; its parameter slots are
        // resolved by ResolveFunctionType, not the inline function-type arm above.
        ["alias_function_type_parameter"] = ("type Cb = (None) -> int\n\ndef main() -> None:\n    f: Cb = lambda x: 1\n    print(1)\n", 1, 12),
        ["optional_none"] = ("def f(x: None?) -> int:\n    return 1\n\ndef main() -> None:\n    print(1)\n", 1, 10),
        ["alias_as_value"] = ("type Unit = None\n\ndef main() -> None:\n    x: Unit = None\n    print(1)\n", 4, 8),
        // A getter's return annotation IS the property's type — a value position (#2004 follow-up;
        // 5a0135370 resolved it as a return, and the cell stayed SPY0908 CS0547).
        ["property_getter"] = ("class C:\n    property get p(self) -> None:\n        return None\n\ndef main() -> None:\n    print(1)\n", 2, 29),
        ["auto_property"] = ("class C:\n    property p: None = None\n\ndef main() -> None:\n    print(1)\n", 2, 17),
        ["union_case_field"] = ("union U:\n    case A(x: None)\n    case B()\n\ndef main() -> None:\n    print(1)\n", 2, 15),
    };

    /// <summary>
    /// A modifier over <c>None</c> wraps nothing, so it is refused in a return slot too — the slot
    /// where bare <c>None</c> is legal. Each was SPY0599 (<c>Optional&lt;void&gt;</c> / <c>void?</c> /
    /// <c>Result&lt;void, string&gt;</c>).
    /// </summary>
    private static readonly Dictionary<string, (string Source, int Line, int Column)> WrappedReturnCells = new()
    {
        ["return_optional"] = ("def f() -> None?:\n    return None()\n\ndef main() -> None:\n    print(1)\n", 1, 12),
        ["return_nullable"] = ("def f() -> None | None:\n    return None\n\ndef main() -> None:\n    print(1)\n", 1, 12),
        ["return_result"] = ("def f() -> None !str:\n    return Ok(None)\n\ndef main() -> None:\n    print(1)\n", 1, 12),
        ["method_return_nullable"] = ("class C:\n    def m(self) -> None | None:\n        return None\n\ndef main() -> None:\n    print(1)\n", 2, 20),
        ["function_type_return_optional"] = ("def main() -> None:\n    g: () -> None? = lambda: None()\n    print(1)\n", 2, 14),
        ["delegate_return_optional"] = ("delegate D() -> None?\n\ndef main() -> None:\n    print(1)\n", 1, 17),
        ["alias_body_optional"] = ("type U = None?\n\ndef f() -> U:\n    return None()\n\ndef main() -> None:\n    print(1)\n", 1, 10),
        ["alias_use_optional"] = ("type Unit = None\n\ndef f() -> Unit?:\n    return None()\n\ndef main() -> None:\n    print(1)\n", 3, 12),
    };

    private static readonly Dictionary<string, (string Source, string Output)> ControlCells = new()
    {
        ["function_return"] = ("def f() -> None:\n    print(1)\n\ndef main() -> None:\n    f()\n", "1"),
        ["method_return"] = ("class C:\n    def m(self) -> None:\n        print(2)\n\ndef main() -> None:\n    C().m()\n", "2"),
        ["function_type_return"] = ("def g() -> None:\n    print(3)\n\ndef main() -> None:\n    f: () -> None = g\n    f()\n", "3"),
        ["alias_as_return"] = ("type Unit = None\n\ndef f() -> Unit:\n    print(4)\n\ndef main() -> None:\n    f()\n", "4"),
        ["delegate_return"] = ("delegate D() -> None\n\ndef main() -> None:\n    print(5)\n", "5"),
        ["union_with_none"] = ("def main() -> None:\n    x: int | None = None\n    print(x is None)\n", "True"),
        ["return_union_with_none"] = ("def f() -> int | None:\n    return None\n\ndef main() -> None:\n    print(f() is None)\n", "True"),
        // A setter's `-> None` is a true return (#2004 follow-up).
        ["setter_return"] = ("class C:\n    _v: int = 0\n\n    property get v(self) -> int:\n        return self._v\n\n"
            + "    property set v(self, value: int) -> None:\n        self._v = value\n\ndef main() -> None:\n"
            + "    c = C()\n    c.v = 6\n    print(c.v)\n", "6"),
    };

    // ---- #2370 (R-JD): a setter's return annotation is the accessor's return, never the property's type ----
    // Closed axes: accessor shape {set-only, get-then-set, set-then-get} × host {class, struct,
    // interface, module} × the setter's return annotation {none, `-> None`, `-> int`} = 36 cells. Every
    // cell types the property by the setter's VALUE parameter (`v: int`): `-> None` and none run, and
    // `-> int` is SPY0496 at each annotated setter (the interface declares and implements one, so two).
    // Before the fix a set-only `-> None` was SPY0220 "Cannot assign type 'int32' to 'None'" (class,
    // struct, interface) or SPY0908 CS0547 (module; any host with no store), a set-then-get pair with
    // `-> None` was SPY0260 + SPY0220, and `-> int` was silently ignored and ran.
    public static readonly string[] SetterShapes = { "set_only", "get_then_set", "set_then_get" };
    public static readonly string[] SetterHosts = { "class", "struct", "interface", "module" };
    public static readonly string[] SetterAnnotations = { "none", "none_annotated", "int_annotated" };

    private static string SetterAnnotationText(string annotation) => annotation switch
    {
        "none" => "",
        "none_annotated" => " -> None",
        "int_annotated" => " -> int",
        _ => throw new ArgumentOutOfRangeException(nameof(annotation), annotation, null),
    };

    /// <summary>The cell's program: a setter printing its value and, for the pair shapes, a getter
    /// returning 7; <c>main</c> stores 5 and, for the pairs, prints the property.</summary>
    private static string SetterCellSource(string shape, string host, string annotation)
    {
        bool module = host == "module";
        string self = module ? "" : "self, ";
        string selfOnly = module ? "" : "self";
        string ann = SetterAnnotationText(annotation);
        string setter = $"property set fresh({self}v: int){ann}:\n    print(v)\n";
        string getter = $"property get fresh({selfOnly}) -> int:\n    return 7\n";
        string members = shape switch
        {
            "set_only" => setter,
            "get_then_set" => getter + "\n" + setter,
            "set_then_get" => setter + "\n" + getter,
            _ => throw new ArgumentOutOfRangeException(nameof(shape), shape, null),
        };
        bool pair = shape != "set_only";
        static string Indent(string s) => string.Join("\n", s.Split('\n').Select(l => l.Length == 0 ? l : "    " + l));
        string read = pair ? (module ? "    print(fresh)\n" : "    print(h.fresh)\n") : "";
        switch (host)
        {
            case "module":
                return members + "\ndef main() -> None:\n    fresh = 5\n" + read;
            case "interface":
                // The interface declares the same accessors body-less; the class implements them.
                string declared = shape switch
                {
                    "set_only" => $"property set fresh(self, v: int){ann}\n",
                    "get_then_set" => $"property get fresh(self) -> int\nproperty set fresh(self, v: int){ann}\n",
                    _ => $"property set fresh(self, v: int){ann}\nproperty get fresh(self) -> int\n",
                };
                return "interface IHost:\n" + Indent(declared) + "\nclass Host(IHost):\n" + Indent(members)
                    + "\ndef main() -> None:\n    h: IHost = Host()\n    h.fresh = 5\n" + read;
            default:
                return $"{host} Host:\n    tag: int = 0\n\n" + Indent(members)
                    + "\ndef main() -> None:\n    h = Host()\n    h.fresh = 5\n" + read;
        }
    }

    public static IEnumerable<object[]> SetterCells() =>
        from shape in SetterShapes
        from host in SetterHosts
        from annotation in SetterAnnotations
        select new object[] { shape, host, annotation };

    [Theory]
    [MemberData(nameof(SetterCells))]
    public void SetterReturnAnnotation_IsTheAccessorsReturn_NeverThePropertysType(string shape, string host, string annotation)
    {
        var cell = $"{shape} × {host} × {annotation}";
        var source = SetterCellSource(shape, host, annotation);
        var result = CompileAndExecute(source);
        var all = string.Join(" | ", result.RawDiagnostics.Select(d => $"{d.Code} {d.Line}:{d.Column} {d.Message}"));
        var errors = result.RawDiagnostics.Where(d => d.Severity == CompilerDiagnosticSeverity.Error).ToList();

        errors.Select(d => d.Code).Should().NotContain(DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError, $"[{cell}] never SPY0908");

        if (annotation == "int_annotated")
        {
            result.Success.Should().BeFalse($"[{cell}] a setter's `-> int` is refused, never ignored");
            // Exactly one SPY0496 per annotated setter, at its annotation, and nothing else (#2075).
            var expectedPositions = source.Split('\n')
                .Select((line, index) => (line, index))
                .Where(x => x.line.TrimStart().StartsWith("property set ", StringComparison.Ordinal) && x.line.Contains("-> int"))
                .Select(x => (Line: x.index + 1, Column: x.line.IndexOf("-> int", StringComparison.Ordinal) + 4))
                .ToList();
            expectedPositions.Should().HaveCount(host == "interface" ? 2 : 1, $"[{cell}] the cell's own setter count");
            errors.Should().OnlyContain(d => d.Code == DiagnosticCodes.Validation.AccessorParameterNotExpressible, $"[{cell}] {all}");
            errors.Select(d => (d.Line, d.Column)).Should().BeEquivalentTo(expectedPositions, $"[{cell}] {all}");
        }
        else
        {
            result.Success.Should().BeTrue($"[{cell}] {all}");
            result.StandardOutput.Trim().Should().Be(shape == "set_only" ? "5" : "5\n7", $"[{cell}] the setter ran with the stored value");
        }
    }

    /// <summary>
    /// The accessors' written order never decides the property's type (#2370, R-JD): with a getter
    /// typed <c>int</c> and a setter typed <c>str</c>, both orders type the property <c>int</c> (the
    /// getter's annotation first) and refuse the store of a <c>str</c>. Taking the first-written
    /// accessor typed the set-then-get order <c>str</c>.
    /// </summary>
    [Theory]
    [InlineData("get_then_set")]
    [InlineData("set_then_get")]
    public void AccessorWrittenOrder_DoesNotDecideThePropertyType(string order)
    {
        const string getter = "    property get v(self) -> int:\n        return 7\n";
        const string setter = "    property set v(self, x: str):\n        print(x)\n";
        var source = "class C:\n" + (order == "get_then_set" ? getter + "\n" + setter : setter + "\n" + getter)
            + "\ndef main() -> None:\n    c = C()\n    c.v = \"s\"\n    print(c.v)\n";
        var result = CompileAndExecute(source);

        var errors = result.RawDiagnostics.Where(d => d.Severity == CompilerDiagnosticSeverity.Error).ToList();
        errors.Should().ContainSingle($"[{order}] {string.Join(" | ", result.RawDiagnostics.Select(d => d.Code + " " + d.Message))}");
        errors[0].Code.Should().Be(DiagnosticCodes.Semantic.TypeMismatch);
        errors[0].Message.Should().Be("Cannot assign type 'str' to 'int32'");
        errors[0].Line.Should().Be(source.Split('\n').ToList().FindIndex(l => l.Contains("c.v = \"s\"")) + 1, $"[{order}] at the store");
    }

    public static IEnumerable<object[]> Refused() => RefusedCells.Keys.Select(k => new object[] { k });
    public static IEnumerable<object[]> Controls() => ControlCells.Keys.Select(k => new object[] { k });
    public static IEnumerable<object[]> WrappedReturns() => WrappedReturnCells.Keys.Select(k => new object[] { k });

    [Theory]
    [MemberData(nameof(Refused))]
    public void ValueSlot_None_IsSpy0614(string cell)
    {
        var (source, line, column) = RefusedCells[cell];
        var result = CompileAndExecute(source);

        result.Success.Should().BeFalse($"[{cell}] must be refused");
        var codes = result.RawDiagnostics.Where(d => d.Severity == CompilerDiagnosticSeverity.Error).Select(d => d.Code).ToList();
        codes.Should().NotContain(DiagnosticCodes.CodeGen.InternalGeneratedCSharpParseError, $"[{cell}] never SPY0599");
        codes.Should().NotContain(DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError, $"[{cell}] never SPY0908");
        var hit = result.RawDiagnostics.Where(d => d.Code == DiagnosticCodes.SemanticOverflow.NoneAnnotationInValuePosition).ToList();
        hit.Should().ContainSingle($"[{cell}] {string.Join(" | ", result.RawDiagnostics.Select(d => d.Code + " " + d.Message))}");
        // One mistake, one diagnostic (#2075): no cascade such as SPY0227 beside the SPY0614.
        result.RawDiagnostics.Where(d => d.Severity == CompilerDiagnosticSeverity.Error).Should().ContainSingle(
            $"[{cell}] {string.Join(" | ", result.RawDiagnostics.Select(d => d.Code + " " + d.Message))}");
        hit[0].Message.Should().Be("'None' is not a type; annotate a local, parameter, field or type argument as `T | None`, or use `object`");
        (hit[0].Line, hit[0].Column).Should().Be((line, column), $"[{cell}] the None annotation's position");
    }

    [Theory]
    [MemberData(nameof(WrappedReturns))]
    public void ReturnSlot_ModifierOverNone_IsSpy0614(string cell)
    {
        var (source, line, column) = WrappedReturnCells[cell];
        var result = CompileAndExecute(source);

        result.Success.Should().BeFalse($"[{cell}] must be refused");
        var codes = result.RawDiagnostics.Where(d => d.Severity == CompilerDiagnosticSeverity.Error).Select(d => d.Code).ToList();
        codes.Should().NotContain(DiagnosticCodes.CodeGen.InternalGeneratedCSharpParseError, $"[{cell}] never SPY0599");
        codes.Should().NotContain(DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError, $"[{cell}] never SPY0908");
        // One mistake, one diagnostic (#2075): the returned value is not "uninferable" (SPY0227) and
        // a lambda body is not measured against the whole function type (SPY0244).
        var errors = result.RawDiagnostics.Where(d => d.Severity == CompilerDiagnosticSeverity.Error).ToList();
        errors.Should().ContainSingle($"[{cell}] {string.Join(" | ", result.RawDiagnostics.Select(d => d.Code + " " + d.Message))}");
        errors[0].Code.Should().Be(DiagnosticCodes.SemanticOverflow.NoneAnnotationInValuePosition);
        errors[0].Message.Should().Be("'None' is not a type, so `None?`, `None | None` and `None !E` wrap nothing; a return type spells no value as bare `None`");
        (errors[0].Line, errors[0].Column).Should().Be((line, column), $"[{cell}] the None annotation's position");
    }

    [Theory]
    [MemberData(nameof(Controls))]
    public void ReturnSlot_None_IsVoid_AndNullableSlotsAreUntouched(string cell)
    {
        var (source, output) = ControlCells[cell];
        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue($"[{cell}] {string.Join(" | ", result.RawDiagnostics.Select(d => d.Code + " " + d.Message))}");
        result.StandardOutput.Trim().Should().Be(output);
    }

    [Fact]
    public void Matrix_IsTotal()
    {
        RefusedCells.Should().HaveCount(13);
        WrappedReturnCells.Should().HaveCount(8);
        ControlCells.Should().HaveCount(8);
        SetterCells().Should().HaveCount(36, "3 shapes × 4 hosts × 3 setter annotations (#2370)");
    }
}
