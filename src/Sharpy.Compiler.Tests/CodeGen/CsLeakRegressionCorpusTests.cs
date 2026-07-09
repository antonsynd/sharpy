using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// Permanent regression corpus pinning the "no CS leaks" invariant (#1035): every
/// program below reconstructs a historical bug where the compiler emitted invalid C#
/// that surfaced to users as a raw <c>CSxxxx</c> Roslyn diagnostic. Each case is
/// compiled and then emitted all the way to IL through Roslyn; the invariant is that the
/// user never sees a CS code — the program either emits clean or is rejected with a
/// SPY-coded diagnostic — and, because every case here is already fixed, no SPY0908
/// (generated-C#-failed-to-compile) internal-error diagnostic fires either.
///
/// CONVENTION: this corpus grows by exactly one case per future CS-leak bug, added in
/// the same PR that fixes it. Prefer stdlib-free reconstructions so the corpus needs
/// only the shared (Sharpy.Core) reference set; where the original repro used a stdlib
/// module, the case comment notes the faithful user-code reconstruction.
/// </summary>
[Trait("Category", "Regression")]
[Collection("HeavyCompilation")]
public class CsLeakRegressionCorpusTests
{
    // The internal-error net for escaped Roslyn CS errors (registered by the SPY0908
    // handler). Referenced as a literal so this corpus stays independent of that code:
    // Compiler.Compile stops at codegen and never runs the assembly stage, so SPY0908
    // can only appear here as a defensive guard, but pinning it documents the invariant.
    private const string GeneratedCodeCompilationError = "SPY0908";

    private readonly ITestOutputHelper _output;

    public CsLeakRegressionCorpusTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public static IEnumerable<object[]> Corpus()
    {
        // #980 — empty tuple () emitted `var t = ();` (CS1525).
        yield return Case("#980-empty-tuple", """
            def main() -> None:
                t = ()
                print(t)
            """);

        // #873 — bare `await` expression statement emitted invalid `void` usage.
        yield return Case("#873-bare-await", """
            async def make_value() -> int:
                return 42

            async def runner() -> None:
                await make_value()

            def main() -> None:
                print("ok")
            """);

        // #949 — bytes([1,2,3]) emitted Sharpy.List<int> instead of Bytes (CS0029).
        yield return Case("#949-bytes-from-list", """
            def main() -> None:
                data: bytes = bytes([104, 105, 33])
                print(len(data))
            """);

        // #921 — the L suffix was dropped, so `object o = 42L` boxed an int (CS-clean but
        // wrong runtime type; here we pin that the boxed-long emit stays valid C#).
        yield return Case("#921-long-literal-suffix", """
            def main() -> None:
                o: object = 42L
                x: long = 100L
                print(x)
            """);

        // #902 — a constant tuple index inside an f-string interpolation was left as a
        // raw indexer (CS0021) instead of lowering to .ItemN.
        yield return Case("#902-tuple-index-in-fstring", """
            def go(attrs: list[tuple[str, str]]) -> None:
                for attr in attrs:
                    print(f"({attr[0]}, {attr[1]})")

            def main() -> None:
                a: list[tuple[str, str]] = []
                a.append(("k", "v"))
                go(a)
            """);

        // #900 — a non-constant tuple index must be rejected with a SPY diagnostic, not
        // leak CS0021 from the generated C#. Accept either a clean emit or a SPY error.
        yield return Case("#900-nonconstant-tuple-index", """
            def main() -> None:
                t: tuple[int, str] = (1, "a")
                i: int = 0
                print(t[i])
            """);

        // #866 — `case None` emitted `case default:` (CS8505).
        yield return Case("#866-match-case-none", """
            def serialize(value: object) -> str:
                match value:
                    case None:
                        return "null"
                    case _:
                        return "other"

            def main() -> None:
                print(serialize(None))
            """);

        // #867 — `case list()` / `case dict()` emitted non-generic Sharpy.List/Dict
        // patterns (CS0305) and the bound variable lost its type args (CS1061/CS8130).
        yield return Case("#867-match-case-list-dict", """
            def serialize(value: object) -> str:
                match value:
                    case list() as items:
                        parts: list[str] = []
                        for item in items:
                            parts.append(str(item))
                        return ", ".join(parts)
                    case dict() as d:
                        parts2: list[str] = []
                        for k, v in d.items():
                            parts2.append(str(k) + ": " + str(v))
                        return ", ".join(parts2)
                    case _:
                        return "other"

            def main() -> None:
                items: list[int] = [1, 2, 3]
                print(serialize(items))
            """);

        // #846 — foreach tuple deconstruction emitted `var(a, b)in` with missing spaces.
        yield return Case("#846-foreach-tuple-deconstruction", """
            def main() -> None:
                pairs: list[tuple[int, int]] = [(1, 2), (3, 4)]
                for a, b in pairs:
                    print(a + b)
            """);

        // #960 — user-defined generic method `def wrap[T](...)` dropped the <T> type
        // parameter on the C# method, so body references to T were undefined (CS0246).
        yield return Case("#960-generic-class-method", """
            class Box:
                def __init__(self) -> None:
                    pass

                def wrap[T](self, y: T) -> str:
                    return "wrapped"

            def main() -> None:
                b: Box = Box()
                print(b.wrap(5))
            """);

        // #961 — generic interface/protocol method `def wrap[T](self, ...)` failed at the
        // semantic stage (SPY0202) and omitted <T>; pin it now compiles or SPY-rejects.
        yield return Case("#961-generic-interface-method", """
            interface W:
                def wrap[T](self, y: T) -> str

            class Wrapper(W):
                def wrap[T](self, y: T) -> str:
                    return "wrapped"

            def main() -> None:
                w: W = Wrapper()
                print(w.wrap(5))
            """);

        // #917 — an inline collection literal passed to a generic call was mis-typed from
        // the outer result annotation (soundness hole: clean diagnostics, invalid C#).
        // Stdlib-free reconstruction of the itertools.compress case: `flags` must infer
        // list[bool], not the outer list[int] annotation.
        yield return Case("#917-inline-literal-generic-arg", """
            def pick[T](values: list[T], flags: list[bool]) -> list[T]:
                return values

            def main() -> None:
                result: list[int] = pick([1, 2, 3], [True, False, True])
                print(len(result))
            """);

        // #912 — indexing a generic-collection-typed field access produced an internal
        // error (SPY0907) instead of working. Stdlib-free reconstruction of the
        // json.loads[Record].scores[0] case using a plain class field.
        yield return Case("#912-index-generic-collection-field", """
            class RecordWithList:
                scores: list[int] = []

            def main() -> None:
                rec: RecordWithList = RecordWithList()
                rec.scores.append(1)
                print(rec.scores[0])
            """);
    }

    [Theory]
    [MemberData(nameof(Corpus))]
    public void HistoricalLeak_EmitsCleanOrSpyRejects(string issue, string source)
    {
        var compiler = new Sharpy.Compiler.Compiler();
        var result = compiler.Compile(source, "corpus.spy");

        // These bugs are all fixed: the internal-error net (SPY0908) must never fire, and
        // the front end never surfaces a raw CS code.
        foreach (var diag in result.Diagnostics.GetAll())
        {
            Assert.False(
                diag.Code == GeneratedCodeCompilationError,
                $"{issue}: SPY0908 internal error should not fire for a fixed bug:\n{diag}");
            Assert.False(
                diag.Code is not null && diag.Code.StartsWith("CS", StringComparison.Ordinal),
                $"{issue}: a raw CS code leaked from the front end:\n{diag}");
        }

        if (!result.Success || string.IsNullOrEmpty(result.GeneratedCSharpCode))
        {
            // Rejected by a SPY-coded diagnostic before codegen — acceptable: the user
            // sees a Sharpy diagnostic, never a CS leak. (This is the #900 outcome when
            // the semantic layer rejects non-constant tuple indexing.)
            Assert.Contains(result.Diagnostics.GetErrors(), d => d.Code is not null);
            _output.WriteLine($"{issue}: SPY-rejected before codegen (no CS leak).");
            return;
        }

        var syntaxTree = CSharpSyntaxTree.ParseText(result.GeneratedCSharpCode);
        var compilation = CSharpCompilation.Create(
            "CsLeakCorpus",
            new[] { syntaxTree },
            IntegrationTestBase.GetSharedReferences(),
            new CSharpCompilationOptions(OutputKind.ConsoleApplication));

        using var ms = new MemoryStream();
        var emitResult = compilation.Emit(ms);

        var csErrors = emitResult.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        if (csErrors.Count > 0)
        {
            var detail = string.Join("\n  ", csErrors.Take(5).Select(d => d.ToString()));
            Assert.Fail(
                $"{issue}: generated C# leaked CS error(s) — historical CS-leak invariant broken (#1035):\n" +
                $"=== Sharpy source ===\n{source}\n" +
                $"=== Generated C# ===\n{result.GeneratedCSharpCode}\n" +
                $"=== CS diagnostics ===\n  {detail}");
        }

        _output.WriteLine($"{issue}: emitted clean to IL.");
    }

    private static object[] Case(string issue, string source) => new object[] { issue, source };
}
