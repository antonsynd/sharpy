using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Initializer host-context totality (#1685): a comprehension, generator, dict/set comprehension
/// or walrus that appears inside a field/module/const initializer, or inside an
/// <c>__init__</c> self-assignment, runs exactly as it would inside an ordinary function body —
/// or is refused BY NAME (never SPY0908/SPY0909) when the host cannot legally host it.
///
/// <para><b>Contract.</b> Before #1685, a class field, struct field, <c>@static</c> field or
/// module-level variable/const initializer was checked directly in its declaring
/// <c>TypeBody</c>/<c>Module</c> scope — neither of which owns a <see cref="!:LocalBindingLedger" />
/// — so a comprehension/generator/lambda/walrus inside the initializer expression had no scope to
/// name its locals in, and the emitter crashed with SPY0909 ("No CodeGenInfo for local"). The fix
/// is two halves: (1) <c>SymbolTable.ClassifyScope</c> gives every such initializer its own
/// <c>ScopeKind.InitializerHost</c> scope (<c>initializer:&lt;Owner&gt;.&lt;name&gt;</c>), which owns
/// a ledger without being an R-Y function-like crossing; (2) the emitter runs the initializer
/// expression under a scope sink and, when anything hoisted, wraps it as a cast-based
/// immediately-invoked lambda (an initializer is an expression position, not a statement list) — or,
/// for the <c>__init__ self.field = value</c> arm specifically, flushes the hoisted statements as a
/// prologue ahead of the assignment (that arm already produces a statement list).</para>
///
/// <para><b>Axis.</b> One axis, host × its Defect-Class-assigned representative construct (line 52 of
/// <c>.claude/plans/plan-d35e69.md</c>): 15 admitted hosts (each executes and prints the python3-matching
/// value) and 2 refused-by-name hosts (a def parameter default, and an async comprehension outside an
/// <c>async def</c>). <c>InitLocal</c>, <c>PropertyBody</c> and <c>LambdaBody</c> are POSITIVE CONTROLS:
/// their owning scope already had a ledger before #1685 (a real method/lambda/accessor body), so they
/// were never broken — keeping them in the matrix is what makes the totality claim falsifiable rather
/// than "every new cell happens to pass".</para>
///
/// <para><b>Finding (plan-roster-vs-actual, reported to the lead):</b> the Defect Class list (plan
/// line 52) labels "module-level const" a REFUSAL ("not constant"). Measured @ HEAD (b8bc7825a): a
/// module <c>const</c> whose initializer is a comprehension COMPILES AND RUNS
/// (<see cref="ModuleConst_ComprehensionInitializer_RunsAndPrints" />) — <c>const</c> only refuses a
/// non-foldable initializer when something downstream needs the value to be a C# compile-time
/// constant (a parameter default, a match-case pattern — already covered by
/// <c>ParameterDefaultConstantMatrixTests</c>'s const-host matrix, "FoldedCallLowered" arm); a bare
/// declaration-and-read is not such a position, and #1685 gave the initializer itself a home
/// regardless of the declaration's <c>const</c>-ness. The cell is asserted ADMITTED here, matching
/// measured behavior, not the stale "(refused)" label.</para>
/// </summary>
[Collection("HeavyCompilation")]
public class InitializerHostContextMatrixTests : IntegrationTestBase
{
    public InitializerHostContextMatrixTests(ITestOutputHelper output) : base(output) { }

    private const int CellCount = 17;
    private const int AdmittedCellCount = 15;
    private const int RefusedCellCount = 2;

    private sealed record Cell(
        string Name,
        string Source,
        string? ExpectedOutput,
        string? RefusedCode = null,
        string? RefusedFragment = null);

    private static readonly Cell[] Cells =
    {
        new("ClassField",
            "class Holder:\n    values: list[int] = [x * x for x in range(4)]\n\n"
            + "def main() -> None:\n    print(Holder().values)\n",
            "[0, 1, 4, 9]\n"),

        new("StructField",
            "struct Squares:\n    values: list[int] = [x * x for x in range(3)]\n\n"
            + "def main() -> None:\n    print(Squares().values)\n",
            "[0, 1, 4]\n"),

        new("StaticField",
            "class Holder:\n    @static\n    values: list[int] = [i * i for i in range(3)]\n\n"
            + "def main() -> None:\n    print(Holder.values)\n",
            "[0, 1, 4]\n"),

        new("ModuleVariable",
            "xs: list[int] = [i * i for i in range(3)]\n\ndef main() -> None:\n    print(xs)\n",
            "[0, 1, 4]\n"),

        // See the class remarks: the Defect Class list's "(refused)" premise is stale — measured.
        new("ModuleConst",
            "const XS: list[int] = [i * i for i in range(3)]\n\ndef main() -> None:\n    print(XS)\n",
            "[0, 1, 4]\n"),

        new("InitSelfAssignment",
            "class Squares:\n    values: list[int]\n\n    def __init__(self):\n"
            + "        self.values = [x * x for x in range(4)]\n\n"
            + "def main() -> None:\n    print(Squares().values)\n",
            "[0, 1, 4, 9]\n"),

        // Positive control: a plain local's initializer already had an owning ledger (the enclosing
        // method) before #1685 — this cell must stay green under mutation (a) to prove the mutation
        // targets the initializer-HOST scope specifically, not comprehensions in general.
        new("InitLocal",
            "class Holder:\n    def __init__(self) -> None:\n        vals = [i for i in range(3)]\n"
            + "        print(vals)\n\ndef main() -> None:\n    Holder()\n",
            "[0, 1, 2]\n"),

        new("DataclassField",
            "@dataclass\nclass Squares:\n    values: list[int] = [x * x for x in range(3)]\n\n"
            + "def main() -> None:\n    print(Squares().values)\n",
            "[0, 1, 4]\n"),

        // Positive control: a property accessor body is a real method (function-like scope).
        new("PropertyBody",
            "class Holder:\n    property get values(self) -> list[int]:\n"
            + "        return [i for i in range(3)]\n\ndef main() -> None:\n    print(Holder().values)\n",
            "[0, 1, 2]\n"),

        // Positive control: a lambda already owns its own function-like scope.
        new("LambdaBody",
            "def main() -> None:\n    f = lambda: [i for i in range(3)]\n    print(f())\n",
            "[0, 1, 2]\n"),

        // Refused control: SPY0400, not SPY0908/SPY0909 — the mutable-collection family is refused
        // BY NAME at a def-parameter host (R-R/R-A: SPY0400 stays reserved for function parameters).
        new("DefDefault",
            "def f(xs: list[int] = [i for i in range(3)]) -> None:\n    print(xs)\n\n"
            + "def main() -> None:\n    f()\n",
            null,
            DiagnosticCodes.Validation.MutableDefault,
            "Mutable default value is not allowed for parameter 'xs'"),

        new("ClassLevelExpression",
            "class Holder:\n    total: int = sum([i for i in range(4)])\n\n"
            + "def main() -> None:\n    print(Holder().total)\n",
            "6\n"),

        new("NestedComprehension",
            "class Holder:\n    values: list[int] = [i * j for i in range(2) for j in range(2)]\n\n"
            + "def main() -> None:\n    print(Holder().values)\n",
            "[0, 0, 0, 1]\n"),

        new("DictSetComprehension",
            "class Holder:\n    d: dict[int, int] = {i: i * i for i in range(3)}\n"
            + "    s: set[int] = {i * i for i in range(3)}\n\n"
            + "def main() -> None:\n    print(Holder().d)\n    print(sorted(Holder().s))\n",
            "{0: 0, 1: 1, 2: 4}\n[0, 1, 4]\n"),

        new("GeneratorExpression",
            "class Holder:\n    total: int = sum(i for i in range(4))\n\n"
            + "def main() -> None:\n    print(Holder().total)\n",
            "6\n"),

        new("Walrus",
            "class Holder:\n    values: list[int] = [y for x in range(3) if (y := x * x) > 0]\n\n"
            + "def main() -> None:\n    print(Holder().values)\n",
            "[1, 4]\n"),

        // Refused control: SPY0273 — an initializer is not an async context, so `async for` there
        // is refused exactly as it is anywhere outside `async def` (the existing
        // errors/async_for_outside_async_error fixture is the positive control that the CODE itself
        // fires for a genuine violation; this cell is the initializer-HOST instance of the same rule).
        new("AsyncComprehension",
            "async def gen() -> int:\n    yield 1\n    yield 2\n    yield 3\n\n"
            + "class Holder:\n    values: list[int] = [x async for x in gen()]\n\n"
            + "async def main():\n    print(Holder().values)\n",
            null,
            DiagnosticCodes.Semantic.AwaitOutsideAsync,
            "'async for' can only be used inside 'async def' functions"),
    };

    private static Cell C(string name) => Cells.Single(c => c.Name == name);

    public static IEnumerable<object[]> AdmittedCells =>
        Cells.Where(c => c.ExpectedOutput != null).Select(c => new object[] { c.Name });

    public static IEnumerable<object[]> RefusedCells =>
        Cells.Where(c => c.RefusedCode != null).Select(c => new object[] { c.Name });

    [Theory]
    [MemberData(nameof(AdmittedCells))]
    public void AdmittedCell_RunsAndPrintsThePythonMatchingValue(string name)
    {
        var cell = C(name);

        var result = CompileAndExecute(cell.Source);

        result.RawDiagnostics.Should().NotContain(
            d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError
                || d.Code == DiagnosticCodes.Infrastructure.InternalCompilerError,
            $"[{name}] the initializer host must never produce SPY0908/SPY0909 — that is the exact "
            + $"defect class #1685 fixes. Diagnostics: {string.Join(" | ", result.CompilationErrors)}\n{cell.Source}");
        result.Success.Should().BeTrue(
            $"[{name}] must compile and run. Diagnostics: {string.Join(" | ", result.CompilationErrors)}\n{cell.Source}");
        result.StandardOutput.Should().Be(cell.ExpectedOutput,
            $"[{name}] must print the python3-matching value\n{cell.Source}");
    }

    [Theory]
    [MemberData(nameof(RefusedCells))]
    public void RefusedCell_ReportsItsNamedDiagnostic(string name)
    {
        var cell = C(name);

        var result = CompileAndExecute(cell.Source);

        result.Success.Should().BeFalse($"[{name}] must be refused\n{cell.Source}");
        result.RawDiagnostics.Should().Contain(
            d => d.Code == cell.RefusedCode,
            $"[{name}] must report {cell.RefusedCode}. Got: "
            + $"{string.Join(" | ", result.RawDiagnostics.Select(d => $"{d.Code}: {d.Message}"))}\n{cell.Source}");
        result.RawDiagnostics.First(d => d.Code == cell.RefusedCode).Message.Should().Contain(
            cell.RefusedFragment!, $"[{name}] must carry the expected diagnostic text\n{cell.Source}");
        result.RawDiagnostics.Should().NotContain(
            d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError
                || d.Code == DiagnosticCodes.Infrastructure.InternalCompilerError,
            $"[{name}] the refusal must be ours, never SPY0908/SPY0909\n{cell.Source}");
    }

    /// <summary>Names the finding in the class remarks with its own executable assertion.</summary>
    [Fact]
    public void ModuleConst_ComprehensionInitializer_RunsAndPrints()
    {
        var cell = C("ModuleConst");

        var result = CompileAndExecute(cell.Source);

        result.Success.Should().BeTrue(
            "a module const's comprehension initializer is not a refusal — #1685 gives it a ledger "
            + $"like any other initializer host. Diagnostics: {string.Join(" | ", result.CompilationErrors)}");
        result.StandardOutput.Should().Be("[0, 1, 4]\n");
    }

    [Fact]
    public void Matrix_IsTotalOverItsAxis()
    {
        Cells.Length.Should().Be(CellCount);
        Cells.Select(c => c.Name).Should().OnlyHaveUniqueItems();

        var admitted = Cells.Count(c => c.ExpectedOutput != null);
        var refused = Cells.Count(c => c.RefusedCode != null);

        admitted.Should().Be(AdmittedCellCount);
        refused.Should().Be(RefusedCellCount);
        (admitted + refused).Should().Be(CellCount,
            "every cell is either an executing admission or a named refusal — there is no known-red bucket");
    }
}
