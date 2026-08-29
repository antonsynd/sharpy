using System.Text.RegularExpressions;
using Sharpy.Compiler.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Infrastructure;

/// <summary>
/// #1660: every <c>*.spy</c> glob with <see cref="System.IO.SearchOption.AllDirectories"/>
/// must filter through <see cref="CrashBundleWriter.IsNonSourceSegment"/> so stale build
/// output and crash-bundle copies are never treated as sources.
///
/// This test scans the source text of every assembly that globs <c>*.spy</c> and asserts
/// that the <c>IsNonSourceSegment</c> helper name appears on the same statement or the
/// preceding line. It cannot use reflection (the call sites are method bodies), so it
/// relies on a text pattern — intentionally brittle to force a conscious review of any
/// new glob site.
/// </summary>
public class SourceGlobConformanceTests
{
    private readonly ITestOutputHelper _output;

    public SourceGlobConformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static readonly Regex GlobPattern = new(
        @"""[*]\.spy"".*AllDirectories|AllDirectories.*""[*]\.spy""",
        RegexOptions.Compiled);

    private static readonly string[] AllowedWithoutFilter =
    [
        // FixtureDiscoveryHelper delegates to IsNonSourceSegment via IsNonCorpus
        "FixtureDiscoveryHelper.cs",
    ];

    [Fact]
    public void Every_SpyGlob_WithAllDirectories_Uses_IsNonSourceSegment()
    {
        var repoRoot = FindRepoRoot();
        var violations = new List<string>();

        foreach (var csFile in Directory.EnumerateFiles(
            Path.Combine(repoRoot, "src"), "*.cs", SearchOption.AllDirectories))
        {
            if (csFile.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") ||
                csFile.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                continue;

            var fileName = Path.GetFileName(csFile);
            if (AllowedWithoutFilter.Contains(fileName))
                continue;

            var lines = File.ReadAllLines(csFile);
            for (int i = 0; i < lines.Length; i++)
            {
                if (!GlobPattern.IsMatch(lines[i]))
                    continue;

                var window = string.Join("\n", lines.Skip(Math.Max(0, i - 1)).Take(5));
                if (!window.Contains("IsNonSourceSegment") && !window.Contains("IsNonCorpus"))
                {
                    var relative = Path.GetRelativePath(repoRoot, csFile);
                    violations.Add($"{relative}:{i + 1}");
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
            "*.spy glob with AllDirectories without IsNonSourceSegment filter (#1660). " +
            $"Sites: {string.Join(", ", violations)}");
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
