using CsCheck;
using Sharpy.Compiler.Shared;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Metamorphic;

/// <summary>
/// The execution half of the metamorphic corpus property (#1157): a semantics-preserving transform
/// must not change what a program PRINTS.
///
/// <para><see cref="MetamorphicCorpusSweepTests"/> proves the whole corpus still compiles clean after
/// each transform, but a mis-emit can survive that gate — produce valid C#, bind fine, and print the
/// wrong answer (the #1136 failure mode). Only running the program catches it.</para>
///
/// <para><b>Oracle.</b> The fixture's own <c>.expected</c> file, not a re-run of the untransformed
/// program. It is the same string by construction — <c>FileBasedIntegrationTests</c> asserts exact
/// equality against it on every run — and using it halves the subprocess count while making a
/// divergence attributable to the transform rather than to a flaky pair of runs. It also settles
/// nondeterminism structurally instead of by category blocklist: a fixture whose stdout varied could
/// not hold a stable <c>.expected</c>, so it would already be failing (or skipped) in the fixture
/// suite and never reaches this corpus.</para>
///
/// <para><b>Bounded and seed-widened.</b> Each run executes <see cref="DefaultPairs"/> distinct
/// (fixture, transform) pairs drawn by CsCheck from the applicable, non-allowlisted set; every
/// invocation draws a different sample, so <c>/property-stress N MetamorphicCorpus</c> widens
/// coverage across rounds. Executing the full ~8,700-pair matrix would take hours — that is what the
/// compile-only sweep is for.</para>
/// </summary>
[Trait("Category", "Property")]
[Trait("Category", "RandomProperty")]
[Trait("Speed", "Slow")]
[Collection("HeavyCompilation")]
public class MetamorphicCorpusInvarianceTests : IntegrationTestBase
{
    /// <summary>
    /// Pairs executed per run. Each pair is a compile plus a <c>dotnet</c> subprocess (~2s), so this
    /// is a wall-clock budget, not a coverage target — stress rounds supply the coverage.
    /// Override with <c>METAMORPHIC_INVARIANCE_PAIRS</c>.
    /// </summary>
    private const int DefaultPairs = 25;

    private const int ExecutionTimeoutMs = 20_000;

    public MetamorphicCorpusInvarianceTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void TransformedFixtures_PrintTheirFixturesExpectedOutput()
    {
        var pairs = BuildApplicablePairs(out var skippedAllowlisted, out var notApplicable);
        Assert.True(pairs.Count > 0, "Metamorphic invariance sample found no applicable (fixture, transform) pairs.");

        var budget = ReadIntEnv("METAMORPHIC_INVARIANCE_PAIRS", DefaultPairs);
        Output.WriteLine(
            $"Applicable pairs: {pairs.Count} (allowlisted-and-skipped: {skippedAllowlisted}, " +
            $"transform-not-applicable: {notApplicable}). Executing {budget} of them.");

        var attempted = new HashSet<string>(StringComparer.Ordinal);
        var executed = new List<string>();

        Gen.OneOfConst(pairs.ToArray()).Sample(pair =>
        {
            if (executed.Count >= budget || !attempted.Add(pair.Key))
                return;

            var result = CompileAndExecuteWithGC(
                pair.TransformedSource, pair.FileName, ExecutionTimeoutMs, pair.Features);

            if (!result.Success)
                throw new Exception(
                    $"{pair.Key}: the transformed program failed to compile or run" +
                    (result.TimedOut ? " (timed out)" : "") + ".\n" +
                    $"Errors: {string.Join("; ", result.CompilationErrors.Take(3))}\n" +
                    $"Transformed source:\n{pair.TransformedSource}");

            if (result.StandardOutput != pair.ExpectedOutput)
                throw new Exception(
                    $"{pair.Key}: output changed under a semantics-preserving transform.\n" +
                    $"Expected (fixture .expected): {Quote(pair.ExpectedOutput)}\n" +
                    $"Actual   (transformed):       {Quote(result.StandardOutput)}\n" +
                    $"Transformed source:\n{pair.TransformedSource}");

            executed.Add(pair.Key);
            // threads:1 — `executed`/`attempted` are not concurrent, and each iteration spawns a
            // subprocess, so parallel iterations would fight the compile-clean sweep for the machine.
        }, iter: budget * 4L, threads: 1);

        foreach (var key in executed)
            Output.WriteLine($"  ok {key}");

        Assert.True(executed.Count > 0,
            "Metamorphic invariance sample executed nothing — the generator or the budget is broken.");
    }

    /// <summary>
    /// Every (fixture, transform) pair where the transform actually rewrites something and the cell
    /// is not a known-failing allowlisted key. Pure string work over the corpus (no compilation), so
    /// enumerating the whole matrix up front costs about a second.
    /// </summary>
    private static List<Pair> BuildApplicablePairs(out int skippedAllowlisted, out int notApplicable)
    {
        var allowlist = MetamorphicCorpusSweepTests.Allowlist.Load();
        var pairs = new List<Pair>();
        skippedAllowlisted = 0;
        notApplicable = 0;

        foreach (var fixture in MetamorphicCorpusSweepTests.EligibleFixtures())
        {
            string source;
            string expected;
            try
            {
                source = File.ReadAllText(fixture.SpyFilePath);
                expected = File.ReadAllText(fixture.ExpectedFile!);
            }
            catch
            {
                continue;
            }

            var features = fixture.Features.Count == 0
                ? FeatureFlags.None
                : FeatureFlags.None.Enable(fixture.Features);

            foreach (var transform in MetamorphicTransforms.All)
            {
                var key = $"{transform.Name}::{fixture.TestName}";
                if (allowlist.Contains(key))
                {
                    skippedAllowlisted++;
                    continue;
                }

                var transformed = transform.Apply(source);
                if (string.Equals(transformed, source, StringComparison.Ordinal))
                {
                    notApplicable++;
                    continue;
                }

                pairs.Add(new Pair(key, Path.GetFileName(fixture.SpyFilePath), transformed, expected, features));
            }
        }

        return pairs;
    }

    private static string Quote(string s)
    {
        var text = s.Replace("\r\n", "\n", StringComparison.Ordinal);
        if (text.Length > 400)
            text = text[..400] + "…";
        return "`" + text.Replace("\n", "\\n", StringComparison.Ordinal) + "`";
    }

    private static int ReadIntEnv(string name, int fallback)
        => int.TryParse(Environment.GetEnvironmentVariable(name), out var v) && v > 0 ? v : fallback;

    private sealed record Pair(
        string Key, string FileName, string TransformedSource, string ExpectedOutput, FeatureFlags Features);
}
