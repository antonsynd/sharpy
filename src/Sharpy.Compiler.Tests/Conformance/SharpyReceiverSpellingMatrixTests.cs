using Sharpy.Compiler;
using Sharpy.Compiler.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>
/// Sharpy-receiver spelling matrix (R-AP, #1851): a Sharpy BUILTIN receiver exposes its Sharpy names
/// only. The axes are receiver × member kind × spelling × position.
///
/// <list type="bullet">
/// <item><b>receiver</b> — list, dict, set, frozenset, frozendict, str, bytes (the wrappers whose
/// .NET surface must not leak) and <c>tuple</c> as a CONTROL (its item spellings stay, #1783).</item>
/// <item><b>member kind</b> — property (Count/Length), method (Add/ToUpper), method group / view
/// (Keys/Values), and the tuple element (Item1).</item>
/// <item><b>spelling</b> — PascalCase (the refused .NET spelling), snake_case (the Sharpy name,
/// accepted), backtick (the escape, accepted → binds the wrapper member), and reverse-mangled
/// (s.length / s.to_upper, the accepted column pinned by bcl_member_on_builtin_receiver_typed).</item>
/// <item><b>position</b> — value, callee, argument, typed store: a PascalCase spelling is refused in
/// every one of them.</item>
/// </list>
///
/// <para>
/// The expectation is a CODE (SPY0203) and, for the refusals, the Sharpy-spelling STEER substring —
/// the steer text is the class contract for Count/Length/Keys/Values/Add. <c>_api.Compile</c> with a
/// library output runs the semantic checker only, so a <c>Compiles</c> cell means "no semantic
/// diagnostic"; the actual runtime behaviour (a backtick escape prints the wrapper member, a tuple
/// element prints its value) is pinned by the executing fixtures sharpy_receiver_backtick_escape_1851
/// and the tuple fixtures — an accepted cell that emitted wrong C# is caught there, not here.
/// </para>
/// </summary>
public class SharpyReceiverSpellingMatrixTests
{
    private readonly ITestOutputHelper _output;
    private readonly CompilerApi _api = new();

    public SharpyReceiverSpellingMatrixTests(ITestOutputHelper output) => _output = output;

    private enum Expect
    {
        /// <summary>SPY0203, from the checker — never SPY0908 and never a silent accept.</summary>
        Refused,

        /// <summary>No semantic diagnostic: the Sharpy/backtick/reverse-mangled spelling is accepted.</summary>
        Compiles,
    }

    /// <param name="Steer">A substring the refusal must contain — the Sharpy spelling steered to.</param>
    private sealed record Cell(string Label, string Source, Expect Expect, string? Steer = null);

    [Fact]
    [Trait("Category", "Conformance")]
    public void SharpyReceiverSpellingMatrix_AllCellsPass()
    {
        var cells = GenerateCells().ToList();
        Assert.Equal(cells.Count, cells.Select(c => c.Label).Distinct().Count());

        var failures = new List<string>();
        foreach (var cell in cells)
        {
            CompileResult result;
            try
            {
                result = _api.Compile(cell.Source, new CompilerOptions { OutputType = "library" });
            }
            catch (Exception ex)
            {
                failures.Add($"{cell.Label}: crashed — {ex.GetType().Name}: {ex.Message}");
                continue;
            }

            var errors = result.Diagnostics
                .Where(d => d.Severity == CompilerDiagnosticSeverity.Error)
                .ToList();

            if (errors.Any(d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError))
            {
                failures.Add($"{cell.Label}: SPY0908 — the member resolved to Unknown instead of typed/refused");
                continue;
            }

            switch (cell.Expect)
            {
                case Expect.Refused when !errors.Any(d => d.Code == DiagnosticCodes.Semantic.UndefinedMember):
                    failures.Add($"{cell.Label}: expected SPY0203, got {Describe(errors)}");
                    break;

                case Expect.Compiles when errors.Count > 0:
                    failures.Add($"{cell.Label}: expected to compile but drew {errors[0].Code}: {errors[0].Message}");
                    break;
            }

            if (cell.Steer != null
                && !errors.Any(d => d.Message.Contains(cell.Steer, StringComparison.Ordinal)))
            {
                failures.Add($"{cell.Label}: refusal does not steer to '{cell.Steer}' — got {Describe(errors)}");
            }
        }

        _output.WriteLine($"Sharpy-receiver spelling cells: {cells.Count}  Failures: {failures.Count}");
        foreach (var f in failures)
            _output.WriteLine($"  {f}");

        Assert.True(failures.Count == 0,
            $"Sharpy-receiver spelling (R-AP, #1851): {failures.Count} of {cells.Count} cells failed.\n"
            + string.Join("\n", failures.Select(f => "  " + f)));
    }

    private static string Describe(IReadOnlyList<CompilerDiagnostic> errors)
        => errors.Count == 0
            ? "no error at all (the member is still Unknown / a wrong-typed accept)"
            : $"{errors[0].Code}: {errors[0].Message}";

    private static IEnumerable<Cell> GenerateCells()
    {
        // ---- PascalCase spelling × position: refused SPY0203 with the steer, on every receiver. ----
        // Each row: receiver decl, receiver expr, the PascalCase member, the steer the refusal must
        // carry. The four positions are generated per row.
        var pascal = new (string Decl, string Recv, string Member, string Steer)[]
        {
            ("xs: list[int] = [1, 2, 3]", "xs", "Count", "len(xs)"),
            ("xs: list[int] = [1, 2, 3]", "xs", "Length", "len(xs)"),
            ("d: dict[str, int] = {\"a\": 1}", "d", "Count", "len(d)"),
            ("d: dict[str, int] = {\"a\": 1}", "d", "Keys", "d.keys()"),
            ("d: dict[str, int] = {\"a\": 1}", "d", "Values", "d.values()"),
            ("st: set[int] = {1, 2}", "st", "Count", "len(st)"),
            ("fs: frozenset[int] = frozenset([1])", "fs", "Count", "len(fs)"),
            ("s: str = \"abc\"", "s", "Length", "len(s)"),
            ("b: bytes = b\"abc\"", "b", "Length", "len(b)"),
        };

        foreach (var row in pascal)
        {
            // value, argument, callee, typed store — all four refuse.
            yield return new Cell($"pascal.value.{row.Recv}.{row.Member}",
                Body(row.Decl, $"x = {row.Recv}.{row.Member}"), Expect.Refused, row.Steer);
            yield return new Cell($"pascal.argument.{row.Recv}.{row.Member}",
                Body(row.Decl, $"print({row.Recv}.{row.Member})"), Expect.Refused, row.Steer);
            yield return new Cell($"pascal.callee.{row.Recv}.{row.Member}",
                Body(row.Decl, $"y = {row.Recv}.{row.Member}()"), Expect.Refused, row.Steer);
            yield return new Cell($"pascal.typed-store.{row.Recv}.{row.Member}",
                Body(row.Decl, $"n: int = {row.Recv}.{row.Member}"), Expect.Refused, row.Steer);
        }

        // xs.Add(4): a PascalCase METHOD (mutating collection verb) refused with the append steer.
        yield return new Cell("pascal.method.list.Add",
            Body("xs: list[int] = [1, 2, 3]", "xs.Add(4)"), Expect.Refused, "xs.append(...)");

        // ---- snake_case Sharpy spelling: accepted (resolves against the registry surface). ----
        yield return new Cell("snake.list.count", Body("xs: list[int] = [1, 2, 3]", "y = xs.count(1)"), Expect.Compiles);
        yield return new Cell("snake.dict.keys", Body("d: dict[str, int] = {\"a\": 1}", "y = d.keys()"), Expect.Compiles);
        yield return new Cell("snake.list.append", Body("xs: list[int] = [1, 2, 3]", "xs.append(4)"), Expect.Compiles);

        // ---- backtick escape: accepted (binds the wrapper CLR member verbatim). ----
        yield return new Cell("backtick.list.Length", Body("xs: list[int] = [1, 2, 3]", "n: int = xs.`Length`"), Expect.Compiles);
        yield return new Cell("backtick.str.Length", Body("s: str = \"abc\"", "n: int = s.`Length`"), Expect.Compiles);

        // ---- reverse-mangled CLR spelling on str: the accepted column (#1291, R-AP letters PascalCase). ----
        yield return new Cell("reverse.str.length", Body("s: str = \"abc\"", "n: int = s.length"), Expect.Compiles);
        yield return new Cell("reverse.str.to_upper", Body("s: str = \"abc\"", "u: str = s.to_upper()"), Expect.Compiles);

        // ---- tuple CONTROL (#1783): Item1 / item1 are typed from the element types, never refused. ----
        yield return new Cell("control.tuple.Item1", Body("t = (1, 2)", "n: int = t.Item1"), Expect.Compiles);
        yield return new Cell("control.tuple.item1", Body("t = (1, 2)", "n: int = t.item1"), Expect.Compiles);

        // A bogus member on a Sharpy receiver is SPY0203 regardless of R-AP (positive control that the
        // refusal channel is UndefinedMember and did not move).
        yield return new Cell("control.list.Bogus", Body("xs: list[int] = [1, 2, 3]", "x = xs.Bogus"), Expect.Refused);
    }

    private static string Body(string decl, string stmt)
        => $"def _use() -> None:\n    {decl}\n    {stmt}\n";
}
