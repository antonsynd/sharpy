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
/// <c>Directory.GetFiles</c>, and <c>new Matcher(</c> calls. Only
/// <c>SourceGlob.cs</c> itself may contain these. Test projects keep the
/// existing #1660 predicate rule (test bodies enumerate their own temp dirs).
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

    private static readonly Regex RawEnumerationPattern = new(
        @"\bDirectory\s*\.\s*(EnumerateFiles|GetFiles)\b|"
        + @"\bSystem\s*\.\s*IO\s*\.\s*Directory\s*\.\s*(EnumerateFiles|GetFiles)\b|"
        + @"\bnew\s+Matcher\s*\(",
        RegexOptions.Compiled);

    private static readonly string[] ProductionProjects =
    [
        "Sharpy.Compiler",
        "Sharpy.Cli",
        "Sharpy.Lsp",
        "Sharpy.TestInfrastructure",
        "Sharpy.Compiler.Benchmarks",
    ];

    private const string SeamFile = "SourceGlob.cs";

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

                var fileName = Path.GetFileName(csFile);
                if (fileName == SeamFile)
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

    [Fact]
    public void SourceGlob_Exists()
    {
        var repoRoot = FindRepoRoot();
        var seamPath = Path.Combine(repoRoot, "src", "Sharpy.Compiler", "Project", SeamFile);
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
