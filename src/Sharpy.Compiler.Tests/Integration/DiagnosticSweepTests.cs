using System.Text.Json;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Project;
using Xunit;
using Xunit.Abstractions;

using Sharpy.TestInfrastructure.Integration;

namespace Sharpy.Compiler.Tests.Integration;

[Trait("Category", "GapDiscovery")]
public class DiagnosticSweepTests
{
    private readonly ITestOutputHelper _output;
    private readonly CompilerApi _api = new();

    public DiagnosticSweepTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void DiagnosticSweep_AllFixtures_NoCrashes()
    {
        var fixtures = FixtureDiscoveryHelper.DiscoverFixtures().ToList();
        Assert.True(fixtures.Count > 0, "No fixtures discovered");

        var crashes = new List<object>();
        var unexpectedDiagnostics = new List<object>();
        var unexpectedWarnings = new List<object>();
        var advisoryWarnings = new List<object>();
        var missingErrors = new List<object>();
        var totalAnalyzed = 0;
        var passCount = 0;
        var failCount = 0;

        foreach (var fixture in fixtures)
        {
            totalAnalyzed++;
            var isNegative = fixture.ErrorFile != null;
            var hasWarningFile = fixture.WarningFile != null;

            IReadOnlyList<CompilerDiagnostic> diagnostics;
            try
            {
                diagnostics = AnalyzeFixture(fixture);
            }
            catch (Exception ex)
            {
                crashes.Add(new
                {
                    fixture = fixture.TestName,
                    exception = ex.GetType().Name,
                    message = ex.Message
                });
                failCount++;
                continue;
            }

            var errors = diagnostics
                .Where(d => d.Severity == CompilerDiagnosticSeverity.Error)
                .ToList();
            var warnings = diagnostics
                .Where(d => d.Severity == CompilerDiagnosticSeverity.Warning)
                .ToList();

            if (!isNegative && errors.Count > 0)
            {
                // Positive fixture produced errors
                unexpectedDiagnostics.Add(new
                {
                    fixture = fixture.TestName,
                    errors = errors.Select(e => new { code = e.Code, message = e.Message }).ToList()
                });
                failCount++;
                continue;
            }

            if (isNegative && errors.Count == 0)
            {
                // Negative fixture produced no errors
                missingErrors.Add(new
                {
                    fixture = fixture.TestName,
                    expectedErrorFile = Path.GetFileName(fixture.ErrorFile!)
                });
                failCount++;
                continue;
            }

            if (!isNegative && warnings.Count > 0 && hasWarningFile)
            {
                // Has warning file - check if warnings are expected
                var expectedWarnings = File.ReadAllLines(fixture.WarningFile!)
                    .Where(l => !string.IsNullOrWhiteSpace(l))
                    .ToList();

                if (expectedWarnings.Count == 0)
                {
                    // Empty warning file means expect no warnings
                    unexpectedWarnings.Add(new
                    {
                        fixture = fixture.TestName,
                        warnings = warnings.Select(w => new { code = w.Code, message = w.Message }).ToList()
                    });
                    failCount++;
                    continue;
                }

                // Non-empty warning file: verify each expected substring appears in at least one warning
                var warningMessages = warnings.Select(w => w.Message).ToList();
                var missingExpected = expectedWarnings
                    .Where(expected => !warningMessages.Any(msg => msg.Contains(expected, StringComparison.Ordinal)))
                    .ToList();

                if (missingExpected.Count > 0)
                {
                    unexpectedWarnings.Add(new
                    {
                        fixture = fixture.TestName,
                        warnings = missingExpected.Select(m => new { code = "MISSING", message = $"Expected warning substring not found: {m}" }).ToList()
                    });
                    failCount++;
                    continue;
                }
            }
            else if (!isNegative && warnings.Count > 0 && !hasWarningFile)
            {
                // No .warning file and has warnings — advisory only (not a failure).
                // These are logged separately from unexpectedWarnings (which are failures).
                advisoryWarnings.Add(new
                {
                    fixture = fixture.TestName,
                    warnings = warnings.Select(w => new { code = w.Code, message = w.Message }).ToList()
                });
            }

            passCount++;
        }

        // Build report
        var report = new
        {
            summaryStats = new
            {
                totalAnalyzed,
                passCount,
                failCount,
                crashCount = crashes.Count
            },
            crashes,
            unexpectedDiagnostics = unexpectedDiagnostics.Take(50).ToList(),
            unexpectedWarnings = unexpectedWarnings.Take(50).ToList(),
            advisoryWarnings = advisoryWarnings.Take(50).ToList(),
            missingErrors = missingErrors.Take(50).ToList()
        };

        // Write report
        var reportDir = Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(typeof(DiagnosticSweepTests).Assembly.Location)!,
            "..", "..", "..", "..", "..", ".claude", "tmp"));
        Directory.CreateDirectory(reportDir);
        var reportPath = Path.Combine(reportDir, "diagnostic-sweep-report.json");
        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(reportPath, json);

        // Output summary
        _output.WriteLine($"Total analyzed: {totalAnalyzed}");
        _output.WriteLine($"Passed: {passCount}");
        _output.WriteLine($"Failed: {failCount}");
        _output.WriteLine($"Crashes: {crashes.Count}");
        _output.WriteLine($"Unexpected diagnostics: {unexpectedDiagnostics.Count}");
        _output.WriteLine($"Unexpected warnings (failures): {unexpectedWarnings.Count}");
        _output.WriteLine($"Advisory warnings (no .warning file): {advisoryWarnings.Count}");
        _output.WriteLine($"Missing errors: {missingErrors.Count}");
        _output.WriteLine($"Report written to: {reportPath}");

        // Hard assertion: no crashes
        Assert.Empty(crashes);
    }

    private IReadOnlyList<CompilerDiagnostic> AnalyzeFixture(TestFixtureInfo fixture)
    {
        if (fixture.IsMultiFile)
        {
            var projectDir = fixture.SpyFilePath;
            var spyFiles = Directory.GetFiles(projectDir, "*.spy", SearchOption.AllDirectories).ToList();
            var mainSpy = Path.Combine(projectDir, "main.spy");
            var entryPoint = File.Exists(mainSpy) ? "main.spy" : Path.GetFileName(spyFiles[0]);

            var config = new ProjectConfig
            {
                ProjectDirectory = projectDir,
                ProjectFilePath = Path.Combine(projectDir, "project.spyproj"),
                RootNamespace = "Test",
                OutputType = "exe",
                EntryPoint = entryPoint,
                SourceFiles = spyFiles,
            };

            var result = _api.AnalyzeProject(config);
            return result.Diagnostics.GetAll();
        }
        else
        {
            var source = File.ReadAllText(fixture.SpyFilePath);
            var result = _api.Analyze(source);
            return result.Diagnostics;
        }
    }
}
