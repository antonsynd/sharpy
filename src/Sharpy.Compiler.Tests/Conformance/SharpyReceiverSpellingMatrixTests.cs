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
/// accepted), backtick (the escape → binds the wrapper member verbatim), and reverse-mangled
/// (s.length / s.to_upper, the accepted column pinned by bcl_member_on_builtin_receiver_typed).</item>
/// <item><b>position</b> — value, callee, argument, typed store: a PascalCase spelling is refused in
/// every one of them.</item>
/// </list>
///
/// <para>
/// The BACKTICK column is a full cross-product (5 receivers × {property value, property typed store,
/// method group value/argument/typed store, method group callee, extension method group}): the escape
/// asks for the wrapper's CLR member, so the verdict follows what that member IS, and it is the same
/// verdict for every receiver. A method in value position is SPY0336 — it printed a
/// <c>System.Func</c> before #1851 — while the callee position still calls, and a property is TYPED
/// (its typed store names int32 rather than failing as CS0029 behind SPY0908).
/// </para>
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

        /// <summary>
        /// SPY0336: a wrapper METHOD named in VALUE position through the escape. The escape binds the
        /// CLR member verbatim, and a method there is a group nothing can spell — <c>print(xs.`Count`)</c>
        /// printed <c>System.Func`2[System.Int32,System.Int32]</c> and <c>b: bool = xs.`Count`</c> reached
        /// Roslyn as CS0428 behind SPY0908 (#1851). Never SPY0203: the escape is not a misspelling.
        /// </summary>
        RefusedMethodGroup,

        /// <summary>
        /// SPY0220: the escape names a wrapper PROPERTY, which IS bindable, and the store rejects its
        /// type. The discriminating half of the escape rule — proof the member is TYPED, not merely
        /// left Unknown (an Unknown reaches the store silently and fails as CS0029 behind SPY0908).
        /// </summary>
        RefusedTypeMismatch,
    }

    /// <param name="Steer">A substring the refusal must contain — the Sharpy spelling steered to.</param>
    private sealed record Cell(string Label, string Source, Expect Expect, string? Steer = null);

    [Fact]
    [Trait("Category", "Conformance")]
    public void SharpyReceiverSpellingMatrix_AllCellsPass()
    {
        var cells = GenerateCells().ToList();
        Assert.Equal(cells.Count, cells.Select(c => c.Label).Distinct().Count());

        // Anti-vacuity, anchored to LITERALS rather than to the generator that produces the cells:
        // the backtick cross-product is 5 receivers × 7 cells, and each of the four expectation kinds
        // must be represented. A column that silently stopped generating would otherwise leave this
        // test green with nothing to say.
        Assert.Equal(5 * 7, cells.Count(c => c.Label.StartsWith("backtick.", StringComparison.Ordinal)));
        foreach (var expectation in Enum.GetValues<Expect>())
            Assert.Contains(cells, c => c.Expect == expectation);

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

                case Expect.RefusedMethodGroup
                    when !errors.Any(d => d.Code == DiagnosticCodes.Semantic.AmbiguousCallableReference):
                    failures.Add($"{cell.Label}: expected SPY0336, got {Describe(errors)}");
                    break;

                case Expect.RefusedTypeMismatch
                    when !errors.Any(d => d.Code == DiagnosticCodes.Semantic.TypeMismatch):
                    failures.Add($"{cell.Label}: expected SPY0220 (the escape is TYPED), got {Describe(errors)}");
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

        // ---- backtick escape × member kind × position, on every wrapper receiver. ----
        // The escape binds the wrapper's CLR member VERBATIM, so the verdict follows what that member
        // IS: a property is typed and reads; a method is a group nothing can spell in value position
        // (SPY0336) but calls fine in callee position; an extension method is the same group (#1851,
        // #1858). Before this rule every method cell PRINTED a System.Func — silent wrong output — and
        // a typed store on either kind reached Roslyn as CS0428/CS0029 behind SPY0908.
        var backtick = new (string Recv, string Decl, string Property, string Method, string CallArgs)[]
        {
            ("xs", "xs: list[int] = [1, 2, 3]", "Length", "Count", "1"),
            ("d", "d: dict[str, int] = {\"a\": 1}", "Count", "Keys", ""),
            ("st", "st: set[int] = {1, 2}", "Count", "Add", "3"),
            ("s", "s: str = \"abc\"", "Length", "ToUpper", ""),
            ("b", "b: bytes = b\"abc\"", "Length", "Decode", ""),
        };

        foreach (var row in backtick)
        {
            // property: reads as a value, and is TYPED — the store names int32, it is not Unknown.
            yield return new Cell($"backtick.property.value.{row.Recv}.{row.Property}",
                Body(row.Decl, $"n: int = {row.Recv}.`{row.Property}`"), Expect.Compiles);
            yield return new Cell($"backtick.property.typed-store.{row.Recv}.{row.Property}",
                Body(row.Decl, $"f: bool = {row.Recv}.`{row.Property}`"), Expect.RefusedTypeMismatch);

            // method group in VALUE position — refused in all three value-ish positions.
            yield return new Cell($"backtick.method-group.value.{row.Recv}.{row.Method}",
                Body(row.Decl, $"x = {row.Recv}.`{row.Method}`"), Expect.RefusedMethodGroup);
            yield return new Cell($"backtick.method-group.argument.{row.Recv}.{row.Method}",
                Body(row.Decl, $"print({row.Recv}.`{row.Method}`)"), Expect.RefusedMethodGroup);
            yield return new Cell($"backtick.method-group.typed-store.{row.Recv}.{row.Method}",
                Body(row.Decl, $"n: int = {row.Recv}.`{row.Method}`"), Expect.RefusedMethodGroup);

            // method group in CALLEE position — the escape still CALLS the wrapper member.
            yield return new Cell($"backtick.method-group.callee.{row.Recv}.{row.Method}",
                Body(row.Decl, $"{row.Recv}.`{row.Method}`({row.CallArgs})"), Expect.Compiles);

            // extension method group through the escape: the same unspellable group (#1858).
            yield return new Cell($"backtick.extension.value.{row.Recv}.Select",
                Body(row.Decl, $"f = {row.Recv}.`Select`"), Expect.RefusedMethodGroup);
        }

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
