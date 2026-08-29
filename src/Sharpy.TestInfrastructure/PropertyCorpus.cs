using Xunit;
using Xunit.Abstractions;

namespace Sharpy.TestInfrastructure;

/// <summary>
/// Shared helpers for property-test corpora that replace proportion asserts
/// (passed > total / 3) with per-sample assertions that name each failure.
/// </summary>
public static class PropertyCorpus
{
    /// <summary>
    /// A single sample from a property-test corpus with its compilation result.
    /// </summary>
    public record SampleResult(string Source, bool Passed, IReadOnlyList<string> ErrorCodes);

    /// <summary>
    /// Asserts that every sample either passes or carries an error code that appears
    /// in the <paramref name="allowedCodes"/> set. Failures are reported individually
    /// with their unparsed source and error codes.
    /// </summary>
    public static void AssertAllPassOrAllowed(
        IReadOnlyList<SampleResult> samples,
        HashSet<string>? allowedCodes,
        ITestOutputHelper output,
        string testLabel)
    {
        Assert.True(samples.Count > 0, $"{testLabel}: no samples were generated");

        var failures = new List<(int Index, string Source, IReadOnlyList<string> Codes)>();
        int passed = 0;
        int allowed = 0;

        for (int i = 0; i < samples.Count; i++)
        {
            var s = samples[i];
            if (s.Passed)
            {
                passed++;
                continue;
            }

            if (allowedCodes != null && s.ErrorCodes.Count > 0
                && s.ErrorCodes.All(c => allowedCodes.Contains(c)))
            {
                allowed++;
                continue;
            }

            failures.Add((i, s.Source, s.ErrorCodes));
        }

        output.WriteLine($"{testLabel}: {passed} passed, {allowed} allowed, " +
                         $"{failures.Count} failed / {samples.Count} total");

        if (failures.Count > 0)
        {
            var report = string.Join("\n\n", failures.Select(f =>
                $"--- Sample {f.Index} [{string.Join(", ", f.Codes)}] ---\n" +
                TruncateSource(f.Source, 500)));
            Assert.Fail($"{testLabel}: {failures.Count} sample(s) failed unexpectedly:\n\n{report}");
        }
    }

    /// <summary>
    /// Compiles a source string and returns a <see cref="SampleResult"/>.
    /// </summary>
    public static SampleResult CompileSample(string source)
    {
        try
        {
            var compiler = new Sharpy.Compiler.Compiler();
            var result = compiler.Analyze(source, "property_test.spy");
            var codes = result.Diagnostics.GetErrors()
                .Select(d => d.Code ?? "UNKNOWN")
                .Distinct()
                .ToList();
            return new SampleResult(source, result.Success, codes);
        }
        catch (Exception ex)
        {
            return new SampleResult(source, false, new[] { $"EXCEPTION:{ex.GetType().Name}" });
        }
    }

    private static string TruncateSource(string source, int maxLen)
    {
        if (source.Length <= maxLen) return source;
        return source[..maxLen] + "\n... (truncated)";
    }
}
