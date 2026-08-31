using System.Text.RegularExpressions;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Infrastructure;

/// <summary>
/// #1696: every file/glob enumeration in production code must route through
/// <see cref="Project.SourceGlob"/> so the <c>IsNonSourceSegment</c> predicate
/// (#1660) is applied consistently for source files, and artifact enumeration
/// is auditable in one place.
///
/// Scans the five production projects for raw <c>Directory.EnumerateFiles</c>,
/// <c>Directory.GetFiles</c>, <c>Directory.EnumerateDirectories</c>,
/// <c>Directory.GetDirectories</c>, and <c>new Matcher(</c> calls. Only
/// <c>SourceGlob.cs</c> itself (path-scoped) may contain these. Test projects keep the
/// existing #1660 predicate rule — enforced by
/// <see cref="Every_TestProject_SpyGlob_WithAllDirectories_Uses_IsNonSourceSegment"/>
/// below (test bodies may enumerate their own temp dirs, but a <c>*.spy</c> sweep
/// with <c>AllDirectories</c> must filter).
/// <c>Sharpy.Stdlib</c> and <c>Sharpy.Core</c> are excluded: their
/// <c>Directory</c> calls implement Python-facing runtime semantics (e.g.
/// <c>os.listdir</c>), not compiler enumeration.
/// </summary>
public class SourceGlobConformanceTests
{
    private readonly ITestOutputHelper _output;

    public SourceGlobConformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    // Matches the METHOD NAME, not the receiver: this catches Directory.X, the
    // System.IO-qualified spelling, DirectoryInfo instance calls, and a receiver
    // wrapped onto the previous line — all with one pattern. The scan is still
    // per-line (comment lines are skipped above); a call whose method name itself
    // is split across lines would evade it — no such spelling survives
    // `dotnet format`, and the recorded mutations cover the live spellings.
    private static readonly Regex RawEnumerationPattern = new(
        @"\b(EnumerateFiles|GetFiles|EnumerateDirectories|GetDirectories"
        + @"|EnumerateFileSystemEntries|GetFileSystemEntries)\s*\(|"
        + @"\bnew\s+Matcher\s*\(",
        RegexOptions.Compiled);

    private static readonly Regex SpyGlobPattern = new(
        @"""[*]\.spy"".*AllDirectories|AllDirectories.*""[*]\.spy""",
        RegexOptions.Compiled);

    private static readonly string[] ProductionProjects =
    [
        "Sharpy.Compiler",
        "Sharpy.Cli",
        "Sharpy.Lsp",
        "Sharpy.TestInfrastructure",
        "Sharpy.Compiler.Benchmarks",
    ];

    // Path-scoped, not basename-scoped: a decoy file named SourceGlob.cs elsewhere in a
    // production project must NOT be exempt from the scan.
    private const string SeamPath = "src/Sharpy.Compiler/Project/SourceGlob.cs";

    [Fact]
    public void No_Raw_Enumeration_Outside_SourceGlob_In_Production_Projects()
    {
        var repoRoot = FindRepoRoot();
        var violations = new List<string>();

        foreach (var project in ProductionProjects)
        {
            var projectDir = Path.Combine(repoRoot, "src", project);
            if (!Directory.Exists(projectDir))
                continue;

            foreach (var csFile in Directory.EnumerateFiles(
                projectDir, "*.cs", SearchOption.AllDirectories))
            {
                if (csFile.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") ||
                    csFile.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                    continue;

                var relativePath = Path.GetRelativePath(repoRoot, csFile)
                    .Replace(Path.DirectorySeparatorChar, '/');
                if (relativePath == SeamPath)
                    continue;

                var lines = File.ReadAllLines(csFile);
                for (int i = 0; i < lines.Length; i++)
                {
                    var trimmed = lines[i].TrimStart();
                    if (trimmed.StartsWith("//") || trimmed.StartsWith("///"))
                        continue;

                    if (RawEnumerationPattern.IsMatch(lines[i]))
                    {
                        var relative = Path.GetRelativePath(repoRoot, csFile);
                        violations.Add($"{relative}:{i + 1}: {trimmed}");
                    }
                }
            }
        }

        if (violations.Count > 0)
        {
            _output.WriteLine("Raw Directory.EnumerateFiles/GetFiles/new Matcher( outside SourceGlob.cs:");
            foreach (var v in violations)
                _output.WriteLine($"  {v}");
        }

        Assert.True(violations.Count == 0,
            "Raw file/glob enumeration outside SourceGlob.cs (#1696). " +
            $"Sites: {string.Join(", ", violations)}");
    }

    /// <summary>
    /// The #1660 rule for everything OUTSIDE the production projects (which the structural
    /// scan above bans from raw enumeration entirely): a <c>*.spy</c> glob with
    /// <c>AllDirectories</c> must apply <c>IsNonSourceSegment</c>/<c>IsNonCorpus</c> (or route
    /// through <c>SourceGlob</c>) on the same statement or an adjacent line, so stale build
    /// output and crash-bundle copies are never treated as sources by test sweeps. Restores
    /// the BASE-era guard that the #1696 rewrite replaced, scoped to the projects the
    /// structural scan does not reach.
    /// </summary>
    [Fact]
    public void Every_TestProject_SpyGlob_WithAllDirectories_Uses_IsNonSourceSegment()
    {
        var repoRoot = FindRepoRoot();
        var violations = new List<string>();
        var srcRoot = Path.Combine(repoRoot, "src");

        foreach (var projectDir in Directory.EnumerateDirectories(srcRoot))
        {
            var projectName = Path.GetFileName(projectDir);
            if (ProductionProjects.Contains(projectName))
                continue; // covered by the total raw-enumeration ban above
            if (projectName is "Sharpy.Core" or "Sharpy.Stdlib")
                continue; // Python-facing runtime semantics, not compiler input discovery

            foreach (var csFile in Directory.EnumerateFiles(
                projectDir, "*.cs", SearchOption.AllDirectories))
            {
                if (csFile.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") ||
                    csFile.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                    continue;

                var lines = File.ReadAllLines(csFile);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (!SpyGlobPattern.IsMatch(lines[i]))
                        continue;

                    var window = string.Join("\n", lines.Skip(Math.Max(0, i - 1)).Take(5));
                    if (!window.Contains("IsNonSourceSegment")
                        && !window.Contains("IsNonCorpus")
                        && !window.Contains("SourceGlob."))
                    {
                        var relative = Path.GetRelativePath(repoRoot, csFile);
                        violations.Add($"{relative}:{i + 1}");
                    }
                }
            }
        }

        if (violations.Count > 0)
        {
            _output.WriteLine("Glob sites missing IsNonSourceSegment filter:");
            foreach (var v in violations)
                _output.WriteLine($"  {v}");
        }

        Assert.True(violations.Count == 0,
            "*.spy glob with AllDirectories without IsNonSourceSegment/IsNonCorpus filter (#1660). " +
            $"Sites: {string.Join(", ", violations)}");
    }

    [Fact]
    public void SourceGlob_Exists()
    {
        var repoRoot = FindRepoRoot();
        var seamPath = Path.Combine(repoRoot,
            SeamPath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(seamPath),
            $"SourceGlob.cs must exist at {seamPath} (#1696)");
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            var gitPath = Path.Combine(dir, ".git");
            if (Directory.Exists(gitPath) || File.Exists(gitPath))
                return dir;
            dir = Path.GetDirectoryName(dir);
        }
        throw new InvalidOperationException("Cannot find repo root (.git directory or file)");
    }
}
