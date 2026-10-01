using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Sharpy.Compiler.Formatting;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Shared;
using Sharpy.TestInfrastructure;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>
/// O7 of P22b (#2062 #2068): <b><c>format ∘ compile ≡ compile</c></b> — for every executing
/// single-file fixture P (an <c>.expected</c> sidecar, no <c>.error</c>), the Roslyn-normalised C#
/// of <c>Format(P)</c> equals P's with every <c>#line</c> directive line removed. The parse-level
/// twin sweep (<see cref="FormatterMeaningPreservationSweepTests"/>) compares ASTs through a
/// comparer that can miss a field; this sweep observes the program the compiler actually emits.
/// <c>Format(P)</c> here is the RAW output (<c>FormatterService.FormatUnchecked</c>, the SPY0912 net
/// bypassed): a refusal returns P itself, whose C# is P's by construction.
///
/// <para><b>Why whole directives are forgotten.</b> The formatter moves lines (blank lines are
/// normalised, a bracket is re-laid out), and the emitted C# maps every statement back to its
/// source line with a <c>#line</c> directive — every fixture whose layout changes would differ for
/// a reason that is not an emission decision. <c>LetEmissionInvarianceTests</c> forgets only the
/// directive COLUMNS because <c>let</c> insertion keeps every line; here the line numbers move too,
/// so the whole directive line goes, and nothing else does
/// (<see cref="Comparison_ForgetsWholeLineDirectivesOnly"/>).</para>
///
/// <para><b>Ratchet.</b> Rows <c>stem identity emitChanged # #issue reason</c> in their own section
/// of <c>Conformance/formatter-meaning-preservation-allowlist.txt</c>; an unlisted differing
/// fixture fails, and a listed fixture that now emits identical C# fails too (drain on fix). One
/// aggregated report is written to <c>.claude/tmp/formatter-emit-invariance-report.json</c>.</para>
///
/// <para>Not in scope: multi-file fixtures (the comparison drives the single-file production path)
/// and <c>.skip</c> fixtures (they do not execute). Both are counted in the census line.</para>
/// </summary>
[Trait("Category", "GapDiscovery")]
[Collection("HeavyCompilation")]
public class FormatterEmitInvarianceSweepTests : FileBasedIntegrationTestsBase
{
    private const string AllowlistFileName = "formatter-meaning-preservation-allowlist.txt";
    private const string EmitChanged = FormatterMeaningPreservationSweepTests.EmitChanged;
    private const string FormatChangesControl = "basics/lambda_variable_invocation";

    private static readonly string FixturesPathValue = FixtureRoots.CompilerTests.Path;

    protected override string FixturesPath => FixturesPathValue;

    public FormatterEmitInvarianceSweepTests(ITestOutputHelper output) : base(output)
    {
    }

    private static IEnumerable<TestFixtureInfo> AllFixtures()
        => FixtureDiscoveryHelper.DiscoverFixtures(FixturesPathValue);

    private static IEnumerable<TestFixtureInfo> ExecutingFixtures()
        => AllFixtures().Where(f => !f.IsMultiFile && f.ExpectedFile != null && f.ErrorFile == null);

    private sealed record Cell(string Stem, string Outcome, string Detail);

    [Fact]
    public void FormattedFixtures_EmitTheSameCSharp_ModuloLineDirectives()
    {
        var stopwatch = Stopwatch.StartNew();
        var all = AllFixtures().ToList();
        var fixtures = ExecutingFixtures().ToList();
        fixtures.Should().NotBeEmpty();

        var cells = new ConcurrentBag<Cell>();
        var dop = Math.Max(1, Math.Min(8, Environment.ProcessorCount));
        Parallel.ForEach(fixtures, new ParallelOptions { MaxDegreeOfParallelism = dop }, fixture =>
        {
            var source = File.ReadAllText(fixture.SpyFilePath);
            // The RAW output (the SPY0912 net bypassed): a refusal returns P, whose C# is P's by
            // construction, and would hide every cell the net catches.
            var formatted = FormatterService.FormatUnchecked(source).FormattedText;
            if (formatted == source)
            {
                cells.Add(new Cell(fixture.TestName, "unchanged", ""));
                return;
            }

            var fileName = Path.GetFileName(fixture.SpyFilePath);
            var features = fixture.Features.Count == 0 ? FeatureFlags.None : FeatureFlags.None.Enable(fixture.Features);
            var original = Compile(source, fileName, features);
            if (!original.Success || original.GeneratedCSharp == null)
            {
                cells.Add(new Cell(fixture.TestName, "baselineFails", Errors(original)));
                return;
            }

            var twin = Compile(formatted, fileName, features);
            if (!twin.Success || twin.GeneratedCSharp == null)
            {
                cells.Add(new Cell(fixture.TestName, EmitChanged, "Format(P) does not compile: " + Errors(twin)));
                return;
            }

            var a = Comparable(original.GeneratedCSharp);
            var b = Comparable(twin.GeneratedCSharp);
            cells.Add(a == b
                ? new Cell(fixture.TestName, "identical", "")
                : new Cell(fixture.TestName, EmitChanged, FirstDifference(a, b)));
        });

        var results = cells.OrderBy(c => c.Stem, StringComparer.Ordinal).ToList();
        var allowlist = LoadAllowlist();
        var unlisted = results.Where(c => c.Outcome == EmitChanged && !allowlist.ContainsKey(c.Stem)).ToList();
        var stale = allowlist.Keys
            .Where(stem => !results.Any(c => c.Stem == stem && c.Outcome == EmitChanged))
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();
        var baseline = results.Where(c => c.Outcome == "baselineFails").ToList();

        var census = $"FMTEMIT-CENSUS discovered={all.Count} ran={results.Count} "
            + string.Join(" ", results.GroupBy(c => c.Outcome).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => $"{g.Key}={g.Count()}"))
            + $" excluded-multi-file={all.Count(f => f.IsMultiFile)} "
            + $"excluded-error-sidecar={all.Count(f => !f.IsMultiFile && f.ErrorFile != null)} "
            + $"excluded-no-expected={all.Count(f => !f.IsMultiFile && f.ErrorFile == null && f.ExpectedFile == null)} "
            + $"allowlisted={allowlist.Count} elapsed={stopwatch.Elapsed.TotalSeconds:F0}s";
        Output.WriteLine(census);
        foreach (var cell in results.Where(c => c.Outcome is EmitChanged or "baselineFails"))
            Output.WriteLine($"FMTEMIT {cell.Stem} {cell.Outcome} {cell.Detail.Split('\n')[0]}");
        WriteReport(new
        {
            census,
            cells = results.Where(c => c.Outcome != "unchanged" && c.Outcome != "identical"),
            unlisted = unlisted.Select(c => c.Stem),
            stale,
        });

        var problems = new List<string>();
        problems.AddRange(baseline.Select(c => $"{c.Stem}: the fixture has an .expected sidecar but does not compile here — an instrument fault, not a skipped cell: {c.Detail}"));
        problems.AddRange(unlisted.Select(c => $"{c.Stem}: Format(P) emits different C#. {c.Detail}"));
        problems.AddRange(stale.Select(s => $"{s}: Format(P) now emits identical C# — {allowlist[s]} is fixed for it: delete the row `{s} identity {EmitChanged}` from Conformance/{AllowlistFileName}"));
        Assert.True(problems.Count == 0,
            $"{problems.Count} formatter emit-invariance violation(s):\n  " + string.Join("\n  ", problems)
            + "\nFull report: .claude/tmp/formatter-emit-invariance-report.json");
    }

    // ================================================================
    // Controls
    // ================================================================

    /// <summary>
    /// The comparison's one exemption is narrow: it forgets every <c>#line</c> directive line —
    /// moved, added or removed — and nothing else; a changed literal, a changed identifier or a
    /// removed code line still differs.
    /// </summary>
    [Fact]
    public void Comparison_ForgetsWholeLineDirectivesOnly()
    {
        static string Program(string directive, string code)
            => "class C\n{\n    void M()\n    {\n" + directive + "        " + code + "\n#line default\n    }\n}\n";

        var original = Program("#line (3, 5) - (3, 19) 12 \"a.spy\"\n", "int x = 1;");
        Comparable(Program("#line (7, 1) - (9, 2) 12 \"a.spy\"\n", "int x = 1;")).Should().Be(Comparable(original), "a moved directive");
        Comparable(Program("", "int x = 1;")).Should().Be(Comparable(original), "a removed directive");
        Comparable(Program("#line 4 \"a.spy\"\n#line hidden\n", "int x = 1;")).Should().Be(Comparable(original), "added directives");
        Comparable(Program("#line (3, 5) - (3, 19) 12 \"a.spy\"\n", "int x = 2;")).Should().NotBe(Comparable(original), "a literal");
        Comparable(Program("#line (3, 5) - (3, 19) 12 \"a.spy\"\n", "int y = 1;")).Should().NotBe(Comparable(original), "an identifier");
        Comparable(Program("#line (3, 5) - (3, 19) 12 \"a.spy\"\n", "")).Should().NotBe(Comparable(original), "a removed code line");
    }

    /// <summary>
    /// Compiled end to end: <c>print(1)</c> and <c>print(2)</c> differ after the comparison (it can
    /// see a literal change), and a fixture whose layout <c>Format</c> changes — so its <c>#line</c>
    /// directives move — compares identical (it forgets the moved lines).
    /// </summary>
    [Fact]
    public void PositiveControls_ALiteralChangeDiffers_AMovedLayoutDoesNot()
    {
        var one = Compile("def main():\n    print(1)\n", "control.spy", FeatureFlags.None);
        var two = Compile("def main():\n    print(2)\n", "control.spy", FeatureFlags.None);
        one.Success.Should().BeTrue(Errors(one));
        two.Success.Should().BeTrue(Errors(two));
        Comparable(two.GeneratedCSharp!).Should().NotBe(Comparable(one.GeneratedCSharp!));

        var fixture = ExecutingFixtures().Single(f => f.TestName == FormatChangesControl);
        var source = File.ReadAllText(fixture.SpyFilePath);
        var formatted = FormatterService.Format(source).FormattedText;
        formatted.Should().NotBe(source, $"{FormatChangesControl} must be a fixture whose layout Format changes");
        var original = Compile(source, Path.GetFileName(fixture.SpyFilePath), FeatureFlags.None);
        var twin = Compile(formatted, Path.GetFileName(fixture.SpyFilePath), FeatureFlags.None);
        original.Success.Should().BeTrue(Errors(original));
        twin.Success.Should().BeTrue(Errors(twin));
        NormalizeCSharp(twin.GeneratedCSharp!).Should().NotBe(NormalizeCSharp(original.GeneratedCSharp!),
            "the #line directives move with the layout, so the raw normalised C# differs");
        Comparable(twin.GeneratedCSharp!).Should().Be(Comparable(original.GeneratedCSharp!));
    }

    // ================================================================
    // Compilation, comparison, allowlist, report
    // ================================================================

    private static readonly Regex LineDirective = new(@"^[ \t]*#line\b[^\n]*\n", RegexOptions.Multiline | RegexOptions.CultureInvariant);

    /// <summary>The Roslyn-normalised C# with every <c>#line</c> directive line removed; every other line byte-for-byte.</summary>
    private static string Comparable(string csharp) => LineDirective.Replace(NormalizeCSharp(csharp), "");

    /// <summary>The production single-file path, stopping after code generation (as <c>LetEmissionInvarianceTests</c>).</summary>
    private static CompileResult Compile(string source, string fileName, FeatureFlags features)
    {
        var dir = Path.Combine(Path.GetTempPath(), $"sharpy_fmtemit_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, fileName);
            File.WriteAllText(path, source);
            var api = new CompilerApi(NullLogger.Instance, new[] { SharpyCoreReference.Location });
            var options = new CompilerOptions
            {
                OutputType = "exe",
                TargetsTestHost = true,
                Features = features,
            };
            return api.Compile(source, options, path);
        }
        finally
        {
            try
            {
                Directory.Delete(dir, recursive: true);
            }
            catch
            {
                // Best-effort cleanup.
            }
        }
    }

    private static string Errors(CompileResult result)
        => string.Join("\n", result.Diagnostics
            .Where(d => d.Severity == global::Sharpy.Compiler.Diagnostics.CompilerDiagnosticSeverity.Error)
            .Select(d => $"{d.Code} L{d.Line}: {d.Message}"));

    private static string FirstDifference(string expected, string actual)
    {
        var a = expected.Split('\n');
        var b = actual.Split('\n');
        for (var i = 0; i < Math.Max(a.Length, b.Length); i++)
        {
            var x = i < a.Length ? a[i] : "<end>";
            var y = i < b.Length ? b[i] : "<end>";
            if (x != y)
                return $"First differing C# line {i + 1}: original `{x.Trim()}` vs formatted `{y.Trim()}`";
        }

        return "The comparable C# differs only in length.";
    }

    /// <summary>Stem → cite, from the <c>emitChanged</c> rows of the shared allowlist.</summary>
    private static Dictionary<string, string> LoadAllowlist()
    {
        var path = FindAllowlistPath()
            ?? throw new InvalidOperationException($"Conformance/{AllowlistFileName} is missing. Its presence is what arms the ratchet.");
        var entries = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;
            var split = line.IndexOf(" #", StringComparison.Ordinal);
            var fields = (split < 0 ? line : line.Substring(0, split)).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length != 3 || fields[2] != EmitChanged)
                continue;
            if (fields[1] != FormatterMeaningPreservationSweepTests.Identity)
                throw new InvalidOperationException($"Conformance/{AllowlistFileName}: '{line}' — an {EmitChanged} row names the identity twin.");
            entries[fields[0]] = split < 0 ? "" : line.Substring(split + 2).Trim();
        }

        return entries;
    }

    private static string? FindAllowlistPath()
    {
        var current = AppContext.BaseDirectory;
        while (current != null)
        {
            var dir = Path.Combine(current, "src", "Sharpy.Compiler.Tests", "Conformance");
            if (Directory.Exists(dir))
            {
                var path = Path.Combine(dir, AllowlistFileName);
                return File.Exists(path) ? path : null;
            }

            current = Directory.GetParent(current)?.FullName;
        }

        return null;
    }

    private void WriteReport(object report)
    {
        var reportDir = Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(typeof(FormatterEmitInvarianceSweepTests).Assembly.Location)!,
            "..", "..", "..", "..", "..", ".claude", "tmp"));
        Directory.CreateDirectory(reportDir);
        var reportPath = Path.Combine(reportDir, "formatter-emit-invariance-report.json");
        File.WriteAllText(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        Output.WriteLine($"Report written to: {reportPath}");
    }
}
