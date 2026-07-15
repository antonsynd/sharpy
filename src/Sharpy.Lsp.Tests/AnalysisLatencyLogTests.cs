using System.Globalization;
using FluentAssertions;
using Sharpy.Lsp;
using Xunit;

namespace Sharpy.Lsp.Tests;

/// <summary>
/// Pins the shape of the LSP analysis-latency log line (Wave 4 Phase 14). Both change→publish
/// call sites (single-file debounce and project reanalysis) log <see cref="AnalysisLatencyLog.Format"/>,
/// so asserting the formatter fixes the on-disk baseline's greppable shape without intercepting
/// the logger.
/// </summary>
public class AnalysisLatencyLogTests
{
    [Fact]
    public void Format_single_file_path_has_stable_shape()
    {
        var line = AnalysisLatencyLog.Format(AnalysisLatencyLog.SingleFilePath, affectedFiles: 1, elapsedMs: 12.34);

        line.Should().Be("LSP analysis latency: path=single-file affectedFiles=1 elapsedMs=12.3");
    }

    [Fact]
    public void Format_project_path_has_stable_shape()
    {
        var line = AnalysisLatencyLog.Format(AnalysisLatencyLog.ProjectPath, affectedFiles: 4, elapsedMs: 987.62);

        line.Should().Be("LSP analysis latency: path=project affectedFiles=4 elapsedMs=987.6");
    }

    [Fact]
    public void Format_starts_with_the_greppable_marker()
    {
        var line = AnalysisLatencyLog.Format(AnalysisLatencyLog.SingleFilePath, 1, 0.0);

        line.Should().StartWith(AnalysisLatencyLog.Marker);
        line.Should().Contain("path=");
        line.Should().Contain("affectedFiles=");
        line.Should().Contain("elapsedMs=");
    }

    [Fact]
    public void Format_uses_invariant_decimal_separator()
    {
        // Even under a comma-decimal culture the elapsed value must use a '.' separator so the
        // recorded baseline and any log-scraping stay locale-independent.
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            var line = AnalysisLatencyLog.Format(AnalysisLatencyLog.ProjectPath, 2, 3.5);
            line.Should().Contain("elapsedMs=3.5");
            line.Should().NotContain("3,5");
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
