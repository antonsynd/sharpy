using System.Collections.Generic;
using System.Linq;
using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>
/// The import-name fidelity matrix #1687 promised: a CLR type reaches Sharpy under the name it was
/// IMPORTED with, and every emitted CLR name is read from the reflected <c>System.Type</c> rather
/// than re-derived from the Sharpy spelling. The axes are import SPELLING (bare, aliased, module,
/// module-qualified, builtin alias, nested type chain) × NAME FORM (reverse-mangled Sharpy or
/// verbatim CLR) × OUTCOME (resolves, or is refused by name).
///
/// <para>
/// Every spelling that denotes the same type must reach the same symbol — that is the whole claim.
/// A matrix over one spelling proves nothing about the others, which is how the ALIAS row
/// (<c>from system import Guid as G</c> emitting <c>System.GUID</c>, #1863) survived the 18
/// spellings measured for #1687: none of them used <c>as</c>.
/// </para>
///
/// <para>
/// Cells expecting a refusal name the CODE, and the ICE code SPY0908 is never an accepted outcome:
/// a cell that reports it has leaked to Roslyn, which is the defect this family exists to close.
/// Two cells are KNOWN RED against open issues and are declared as such rather than omitted.
/// </para>
/// </summary>
public class ClrImportNameFidelityTests : IntegrationTestBase
{
    private readonly ITestOutputHelper _output;

    // CompileAndExecute, not CompilerApi.Compile: an outcome that only shows up at the C# STAGE
    // (SPY0908) is invisible to a harness that stops after semantic analysis, and both KnownSpy0908
    // cells below are exactly that shape. #1837 is the same vacuity one harness over.
    public ClrImportNameFidelityTests(ITestOutputHelper output) : base(output) => _output = output;

    private enum Expect
    {
        /// <summary>No error: the spelling resolves.</summary>
        Resolves,

        /// <summary>Refused by name with the code in <see cref="Cell.Code"/>.</summary>
        Refused,

        /// <summary>
        /// Leaks to the C# stage today, against the open issue in <see cref="Cell.Issue"/>. Asserted
        /// as a KNOWN RED so the cell fails loudly when the issue is fixed ("drain on fix") instead
        /// of being silently omitted.
        /// </summary>
        KnownSpy0908
    }

    private sealed record Cell(
        string Label, string Spelling, string Source, Expect Expect,
        string? Code = null, string? Issue = null);

    private static IEnumerable<Cell> Cells()
    {
        // ── bare `from X import T`, both name forms ──
        yield return new Cell("bare.sharpy-names", "bare import",
            "from system import Guid\n\ndef main() -> None:\n"
            + "    print(len(Guid.new_guid().to_string()) > 0)\n",
            Expect.Resolves);

        yield return new Cell("bare.verbatim-clr-names", "bare import",
            "from system import Guid\n\ndef main() -> None:\n"
            + "    print(len(Guid.NewGuid().ToString()) > 0)\n",
            Expect.Resolves);

        // ── aliased `from X import T as A` (#1863) ──
        yield return new Cell("alias.sharpy-names", "aliased import",
            "from system import Guid as G\n\ndef main() -> None:\n"
            + "    print(len(G.new_guid().to_string()) > 0)\n",
            Expect.KnownSpy0908, Issue: "#1863");

        // ── `import X` + fully qualified ──
        yield return new Cell("module.qualified-type", "module import",
            "import system\n\ndef main() -> None:\n"
            + "    print(len(system.Guid.new_guid().to_string()) > 0)\n",
            Expect.Resolves);

        yield return new Cell("module.qualified-nested-namespace", "module import",
            "import system.io\n\ndef main() -> None:\n"
            + "    print(system.io.Path.combine(\"a\", \"b\"))\n",
            Expect.Resolves);

        yield return new Cell("nested-namespace.bare-import", "bare import",
            "from system.io import Path\n\ndef main() -> None:\n"
            + "    print(Path.combine(\"a\", \"b\"))\n",
            Expect.Resolves);

        // ── builtin alias in receiver position (#1686) ──
        yield return new Cell("builtin-alias.static-field", "builtin alias",
            "def main() -> None:\n    print(int.max_value)\n",
            Expect.Resolves);

        yield return new Cell("builtin-alias.static-method", "builtin alias",
            "def main() -> None:\n    print(str.is_null_or_empty(\"\"))\n",
            Expect.Resolves);

        // ── nested static TYPE chain (#1864): the fourth receiver spelling ──
        yield return new Cell("nested-type-chain.argument-position", "nested type chain",
            "from system import Environment\n\ndef main() -> None:\n"
            + "    print(Environment.get_folder_path(Environment.SpecialFolder.Desktop) != \"\")\n",
            Expect.Resolves);

        yield return new Cell("nested-type-chain.value-position", "nested type chain",
            "from system import Environment\n\ndef main() -> None:\n"
            + "    f: bool = Environment.SpecialFolder.Desktop\n    print(f)\n",
            Expect.KnownSpy0908, Issue: "#1864");

        // ── the REFUSAL half: an absent member and an absent import, by name ──
        yield return new Cell("absent-member.refused", "bare import",
            "from system import Guid\n\ndef main() -> None:\n    print(Guid.no_such_member_xyz)\n",
            Expect.Refused, Code: DiagnosticCodes.Semantic.UndefinedMember);

        yield return new Cell("absent-import.refused", "bare import",
            "from system import NoSuchTypeXyz\n\ndef main() -> None:\n    print(1)\n",
            Expect.Refused, Code: DiagnosticCodes.Semantic.ImportError);
    }

    [Fact]
    [Trait("Category", "Conformance")]
    public void ImportNameFidelity_EverySpellingReachesTheSameType()
    {
        var cells = Cells().ToList();
        Assert.Equal(cells.Count, cells.Select(c => c.Label).Distinct().Count());

        // Both halves must be present, or the matrix cannot tell a resolver that accepts everything
        // from one that refuses everything.
        Assert.Contains(cells, c => c.Expect == Expect.Resolves);
        Assert.Contains(cells, c => c.Expect == Expect.Refused);

        foreach (var known in cells.Where(c => c.Expect == Expect.KnownSpy0908))
        {
            Assert.False(string.IsNullOrWhiteSpace(known.Issue),
                $"{known.Label}: a KnownSpy0908 cell must cite the issue that deletes it");
            Assert.StartsWith("#", known.Issue, System.StringComparison.Ordinal);
        }

        var failures = new List<string>();

        foreach (var cell in cells)
        {
            var result = CompileAndExecute(cell.Source);
            var errors = result.RawDiagnostics
                .Where(d => d.Severity == CompilerDiagnosticSeverity.Error)
                .ToList();
            var ice = errors.Any(d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError);
            _output.WriteLine($"{cell.Label}: success={result.Success} errors={errors.Count}");

            switch (cell.Expect)
            {
                case Expect.Resolves when !result.Success:
                    failures.Add($"{cell.Label} ({cell.Spelling}): expected to resolve and run, drew "
                        + $"{Describe(errors)} / {string.Join(" | ", result.CompilationErrors.Take(2))}");
                    break;

                case Expect.Refused when !errors.Any(d => d.Code == cell.Code):
                    failures.Add($"{cell.Label} ({cell.Spelling}): expected {cell.Code}, got {Describe(errors)}");
                    break;

                case Expect.Refused when ice:
                    failures.Add($"{cell.Label} ({cell.Spelling}): refused, but ALSO leaked SPY0908");
                    break;

                case Expect.KnownSpy0908 when !ice:
                    failures.Add($"{cell.Label} ({cell.Spelling}): {cell.Issue} appears FIXED — "
                        + $"no SPY0908 any more (got {Describe(errors)}). Re-measure the expected "
                        + "outcome and delete this KnownSpy0908 entry.");
                    break;
            }
        }

        _output.WriteLine($"Import-name fidelity cells: {cells.Count}  Failures: {failures.Count}");
        foreach (var f in failures)
            _output.WriteLine("  " + f);

        Assert.True(failures.Count == 0,
            $"CLR import-name fidelity (#1687): {failures.Count} of {cells.Count} cells failed.\n"
            + string.Join("\n", failures.Select(f => "  " + f)));
    }

    private static string Describe(IReadOnlyList<CompilerDiagnostic> errors)
        => errors.Count == 0
            ? "no error at all"
            : string.Join("; ", errors.Take(2).Select(e => $"{e.Code}: {e.Message}"));
}
