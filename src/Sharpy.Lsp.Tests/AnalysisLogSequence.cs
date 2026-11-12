using SCG = System.Collections.Generic;

namespace Sharpy.Lsp.Tests;

/// <summary>
/// Reads captured log lines as the ordered sequence of single-file analysis lines — latency and
/// stage attribution — so a test can wait on, and assert about, the order the server writes them in
/// rather than on two independent counts (#2589).
/// </summary>
/// <remarks>
/// <para>
/// <c>SharpyWorkspace.FireAndForgetAnalysis</c> writes an analysis's latency line and then, on the
/// same thread with no await between them, its stage line (only when the compiler ran). Waiting for
/// the latency count and then reading the stage count therefore races twice over: a real analysis's
/// stage line can still be pending (a flake: "found 0"), and a stage line a fast-path edit should
/// never have produced can still be pending (a false pass of the absence check).
/// </para>
/// <para>
/// Both lines of one analysis carry the same <c>elapsedMs=</c> value, read from one stopped
/// stopwatch, so a stage line can be paired with the latency line it belongs to instead of merely
/// counted.
/// </para>
/// <para>
/// An absence ("no stage line for this analysis") is settled by a later, real analysis requested
/// only after the latency line in question was seen: it cannot log before the debounce delay plus a
/// full analysis have elapsed. That is a bound, not a happens-before — the workspace exposes no
/// "finished logging" signal — but it is far wider than the two adjacent statements it covers.
/// </para>
/// </remarks>
internal static class AnalysisLogSequence
{
    public enum Kind
    {
        Latency,
        Stages,
    }

    public readonly record struct Line(Kind Kind, string ElapsedMs);

    /// <summary>The latency and stage lines among <paramref name="messages"/>, in logged order.</summary>
    public static SCG.List<Line> Of(SCG.IEnumerable<string> messages)
    {
        var lines = new SCG.List<Line>();
        foreach (var message in messages)
        {
            if (message.Contains(AnalysisLatencyLog.Marker, StringComparison.Ordinal))
                lines.Add(new Line(Kind.Latency, ElapsedOf(message)));
            else if (message.Contains(AnalysisLatencyLog.StagesMarker, StringComparison.Ordinal))
                lines.Add(new Line(Kind.Stages, ElapsedOf(message)));
        }

        return lines;
    }

    /// <summary>
    /// True once the <paramref name="n"/>th latency line (1-based) has been followed by a stage
    /// line — i.e. the <paramref name="n"/>th analysis, a compiler-backed one, has finished
    /// logging. Whether that stage line is the right one (paired by <c>elapsedMs</c>) is left to
    /// the caller's assertion, so a wrong one fails loudly rather than ending the wait as a hang.
    /// </summary>
    public static bool HasStagesAfterLatency(SCG.IReadOnlyList<Line> lines, int n)
    {
        var seen = 0;
        for (var i = 0; i < lines.Count; i++)
        {
            if (lines[i].Kind == Kind.Latency)
            {
                seen++;
                continue;
            }

            if (seen >= n)
                return true;
        }

        return false;
    }

    private static string ElapsedOf(string message)
    {
        const string key = "elapsedMs=";
        var start = message.IndexOf(key, StringComparison.Ordinal);
        if (start < 0)
            return "";
        start += key.Length;
        var end = message.IndexOf(' ', start);
        return end < 0 ? message.Substring(start) : message.Substring(start, end - start);
    }
}
