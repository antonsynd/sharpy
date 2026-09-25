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
/// <c>dict[str, None]</c>, a function type's parameter <c>(None) -&gt; int</c>, <c>None?</c>, an alias
/// whose body is <c>None</c> used as a value} × SPY0614; controls {function return, method return,
/// a function type's return <c>() -&gt; None</c>, an alias of <c>None</c> used as a return, a
/// delegate's return, <c>int | None</c>} × run.
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
        ["optional_none"] = ("def f(x: None?) -> int:\n    return 1\n\ndef main() -> None:\n    print(1)\n", 1, 10),
        ["alias_as_value"] = ("type Unit = None\n\ndef main() -> None:\n    x: Unit = None\n    print(1)\n", 4, 8),
    };

    private static readonly Dictionary<string, (string Source, string Output)> ControlCells = new()
    {
        ["function_return"] = ("def f() -> None:\n    print(1)\n\ndef main() -> None:\n    f()\n", "1"),
        ["method_return"] = ("class C:\n    def m(self) -> None:\n        print(2)\n\ndef main() -> None:\n    C().m()\n", "2"),
        ["function_type_return"] = ("def g() -> None:\n    print(3)\n\ndef main() -> None:\n    f: () -> None = g\n    f()\n", "3"),
        ["alias_as_return"] = ("type Unit = None\n\ndef f() -> Unit:\n    print(4)\n\ndef main() -> None:\n    f()\n", "4"),
        ["delegate_return"] = ("delegate D() -> None\n\ndef main() -> None:\n    print(5)\n", "5"),
        ["union_with_none"] = ("def main() -> None:\n    x: int | None = None\n    print(x is None)\n", "True"),
    };

    public static IEnumerable<object[]> Refused() => RefusedCells.Keys.Select(k => new object[] { k });
    public static IEnumerable<object[]> Controls() => ControlCells.Keys.Select(k => new object[] { k });

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
        hit[0].Message.Should().Be("'None' is not a type; annotate a local, parameter, field or type argument as `T | None`, or use `object`");
        (hit[0].Line, hit[0].Column).Should().Be((line, column), $"[{cell}] the None annotation's position");
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
        RefusedCells.Should().HaveCount(9);
        ControlCells.Should().HaveCount(6);
    }
}
