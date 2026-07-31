using System.Globalization;
using System.Text;
using Sharpy.Compiler.Diagnostics;

namespace Sharpy.Lsp;

/// <summary>
/// Formats a single, stable "analysis latency" log line for the two LSP change→publish paths:
/// single-file debounced analysis (<see cref="SharpyWorkspace"/>) and full project reanalysis
/// (<see cref="LanguageService.OnDocumentChangedAsync"/>). Centralizing the shape keeps both call
/// sites identical and gives the recorded LSP latency baseline a greppable marker.
/// </summary>
/// <remarks>
/// This is the D1 "measure first" principle applied to the LSP: the incremental-frontend work
/// (#1099) starts from recorded numbers rather than intuition. The formatter is pure so the
/// log shape can be asserted without intercepting the logger.
/// </remarks>
internal static class AnalysisLatencyLog
{
    /// <summary>Stable prefix every latency line starts with (a greppable marker).</summary>
    public const string Marker = "LSP analysis latency";

    /// <summary>Path label for the single-file debounced analysis path.</summary>
    public const string SingleFilePath = "single-file";

    /// <summary>Path label for the full project reanalysis path.</summary>
    public const string ProjectPath = "project";

    /// <summary>
    /// Builds the latency line. <paramref name="affectedFiles"/> is the number of files whose
    /// diagnostics were (re)published: 1 for the single-file path, the affected-set size for the
    /// project path. <paramref name="elapsedMs"/> is the wall time from change handling to
    /// results being ready to publish. Invariant culture keeps the decimal separator stable
    /// across locales.
    /// </summary>
    public static string Format(string path, int affectedFiles, double elapsedMs)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{Marker}: path={path} affectedFiles={affectedFiles} elapsedMs={elapsedMs:F1}");
    }

    /// <summary>Prefix for the per-stage attribution line (a second greppable marker).</summary>
    public const string StagesMarker = "LSP analysis stages";

    /// <summary>
    /// Builds the per-stage attribution line that breaks a single <see cref="Format"/> latency
    /// number apart (#1140): one <c>name=ms</c> entry per pipeline stage, plus the stage total so a
    /// reader can see at a glance how much of <paramref name="elapsedMs"/> the stages account for.
    /// The remainder is real work outside the compiler call — debounce bookkeeping, the analysis
    /// task hop, and diagnostic publication — not measurement error.
    /// </summary>
    /// <param name="path">The path label, as in <see cref="Format"/>.</param>
    /// <param name="stages">The stages recorded for the call, in execution order.</param>
    /// <param name="elapsedMs">The same wall time reported on the latency line.</param>
    public static string FormatStages(string path, IReadOnlyList<PhaseMetric> stages, double elapsedMs)
    {
        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"{StagesMarker}: path={path} ");
        sb.Append(CultureInfo.InvariantCulture, $"elapsedMs={elapsedMs:F1} stages=[");

        double total = 0;
        for (var i = 0; i < stages.Count; i++)
        {
            var stage = stages[i];
            total += stage.Duration.TotalMilliseconds;
            if (i > 0)
                sb.Append(", ");
            sb.Append(CultureInfo.InvariantCulture, $"{stage.Name}={stage.Duration.TotalMilliseconds:F2}");
        }

        sb.Append(CultureInfo.InvariantCulture, $"] stagesTotalMs={total:F1}");
        return sb.ToString();
    }
}
