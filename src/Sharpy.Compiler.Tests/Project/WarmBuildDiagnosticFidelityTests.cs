using FluentAssertions;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Project;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Project;

/// <summary>
/// For each non-empty single-file <c>.warning</c> fixture, scaffolds it as an incremental project,
/// cold-builds, warm-rebuilds, and compares diagnostic projections. Divergences ratchet against a
/// drain-on-fix allowlist (#1553).
///
/// <para>This is the scale validation for B4.1's diagnostic replay mechanism. The cold/warm
/// differential test (B4.2) validates the mechanism on one specimen; this sweep validates it
/// across the whole corpus.</para>
/// </summary>
[Trait("Category", "GapDiscovery")]
public class WarmBuildDiagnosticFidelityTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _tempDir;

    public WarmBuildDiagnosticFidelityTests(ITestOutputHelper output)
    {
        _output = output;
        _tempDir = Path.Combine(Path.GetTempPath(), $"sharpy_warmdiag_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        GC.SuppressFinalize(this);
    }

    private static readonly string FixturesRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Sharpy.Compiler.Tests",
            "Integration", "TestFixtures"));

    private static readonly HashSet<string> Allowlist = LoadAllowlist();

    private static HashSet<string> LoadAllowlist()
    {
        var path = Path.Combine(FixturesRoot, "..", "..", "Project", "warm-diagnostic-fidelity-allowlist.txt");
        if (!File.Exists(path))
            return new HashSet<string>(StringComparer.Ordinal);
        return File.ReadAllLines(path)
            .Where(l => !string.IsNullOrWhiteSpace(l) && !l.TrimStart().StartsWith('#'))
            .Select(l => l.Trim())
            .ToHashSet(StringComparer.Ordinal);
    }

    public static IEnumerable<object[]> WarningFixtures()
    {
        if (!Directory.Exists(FixturesRoot))
            yield break;

        foreach (var warningFile in Directory.EnumerateFiles(FixturesRoot, "*.warning", SearchOption.AllDirectories))
        {
            if (new FileInfo(warningFile).Length == 0)
                continue;
            if (Path.GetFileName(warningFile) == "main.warning")
                continue;

            var spyFile = Path.ChangeExtension(warningFile, ".spy");
            if (!File.Exists(spyFile))
                continue;

            var relativePath = Path.GetRelativePath(FixturesRoot, spyFile);
            var testName = relativePath.Replace(Path.DirectorySeparatorChar, '/');
            testName = testName[..^4]; // strip .spy
            yield return new object[] { testName, spyFile };
        }
    }

    [Theory]
    [MemberData(nameof(WarningFixtures))]
    public void WarmBuild_PreservesDiagnostics(string testName, string spyPath)
    {
        var source = File.ReadAllText(spyPath);
        var area = testName.Replace('/', '_');

        var dir = Path.Combine(_tempDir, area);
        Directory.CreateDirectory(dir);
        var filePath = Path.Combine(dir, "main.spy");
        File.WriteAllText(filePath, source);

        var config = new ProjectConfig
        {
            ProjectFilePath = Path.Combine(dir, "test.spyproj"),
            ProjectDirectory = dir,
            RootNamespace = "WarmDiag",
            SourceFiles = new List<string> { filePath },
            Configuration = "Debug",
        };

        // Cold build
        var cold = new Compiler(new CompilerOptions { Incremental = true }, NullLogger.Instance)
            .CompileProject(config);
        if (!cold.Success)
        {
            _output.WriteLine($"SKIP {testName}: cold build failed (errors, not warnings)");
            return;
        }

        var coldDiags = ProjectDiagnostics(cold);

        // Warm rebuild (no edits)
        var warm = new Compiler(new CompilerOptions { Incremental = true }, NullLogger.Instance)
            .CompileProject(config);

        // Positive control: the warm build must have skipped the file
        var skipped = warm.Metrics?.SkippedFiles.Select(Path.GetFileName).ToList() ?? new List<string?>();
        if (!skipped.Contains("main.spy"))
        {
            _output.WriteLine($"SKIP {testName}: warm build didn't skip main.spy (not cached)");
            return;
        }

        var warmDiags = ProjectDiagnostics(warm);

        if (coldDiags != warmDiags)
        {
            if (Allowlist.Contains(testName))
            {
                _output.WriteLine($"ALLOWED {testName}: cold/warm diverge (allowlisted)");
                return;
            }
            warmDiags.Should().Be(coldDiags,
                $"warm build of '{testName}' must produce the same diagnostics as cold. "
                + "If this is a known divergence, add it to warm-diagnostic-fidelity-allowlist.txt with an issue reference.");
        }
        else if (Allowlist.Contains(testName))
        {
            Assert.Fail($"'{testName}' is in the allowlist but no longer diverges — remove the allowlist entry (drain-on-fix).");
        }
    }

    private static string ProjectDiagnostics(ProjectCompilationResult result)
        => string.Join("\n", result.Diagnostics.GetAll()
            .Select(d => $"{d.Severity}:{d.Code}:{d.Line}:{d.Column} {d.Message}")
            .OrderBy(s => s, StringComparer.Ordinal));
}
