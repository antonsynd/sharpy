using System.Globalization;

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
}
