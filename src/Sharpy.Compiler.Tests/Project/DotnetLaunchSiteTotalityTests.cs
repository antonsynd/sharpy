using System.Text.RegularExpressions;
using Xunit;

namespace Sharpy.Compiler.Tests.Project;

/// <summary>
/// Every place a shipped assembly starts <c>dotnet</c> goes through <c>DotnetHost</c> (#2292): the
/// literal file name <c>"dotnet"</c> / <c>"dotnet.exe"</c> appears in product source only in the
/// resolver. A launch site that spells it itself skips the check that runs before any work, and on a
/// host without .NET it ends in an unhandled <c>Win32Exception</c> after compiling — the
/// self-contained release archive's <c>run</c> did exactly that at three sites.
/// </summary>
/// <remarks>
/// The product universe is enumerated, not listed: every directory under <c>src/</c> holding a
/// project, minus the test, benchmark and test-infrastructure projects, so a new shipped project is
/// scanned without editing this file. MUTATION (recorded in the #2292 commit): put
/// <c>FileName = "dotnet"</c> back at <c>RunCommand</c>'s launch → red naming that file and line.
/// </remarks>
public class DotnetLaunchSiteTotalityTests
{
    private static readonly Regex DotnetFileNameLiteral = new("\"dotnet(\\.exe)?\"", RegexOptions.Compiled);

    private const string Resolver = "src/Sharpy.Compiler/Project/DotnetHost.cs";

    [Fact]
    public void OnlyTheResolverSpellsTheDotnetFileName()
    {
        var root = FindRepoRoot();
        var hits = ProductSourceFiles(root)
            .SelectMany(file => File.ReadLines(file).Select((line, index) => (file, line, number: index + 1)))
            .Where(x => DotnetFileNameLiteral.IsMatch(x.line))
            .Select(x => $"{Path.GetRelativePath(root, x.file).Replace('\\', '/')}:{x.number}: {x.line.Trim()}")
            .ToList();

        // Positive control: the scan reads real files and the pattern matches real source — the
        // resolver's own spelling is found.
        Assert.Contains(hits, h => h.StartsWith(Resolver + ":", StringComparison.Ordinal));

        var offenders = hits.Where(h => !h.StartsWith(Resolver + ":", StringComparison.Ordinal)).ToList();
        Assert.True(offenders.Count == 0,
            "Start the dotnet DotnetHost resolved (CliHelpers.TryResolveDotnet before any work), never a bare "
            + "\"dotnet\" (#2292):\n" + string.Join("\n", offenders));
    }

    [Fact]
    public void TheProductUniverse_CoversTheShippedProjects()
    {
        // Anchors the enumeration to literals, so an exclusion rule that swallowed a shipped project
        // would show here rather than silently shrinking the scan.
        var projects = ProductProjectDirectories(FindRepoRoot()).Select(Path.GetFileName).ToHashSet();

        Assert.Contains("Sharpy.Cli", projects);
        Assert.Contains("Sharpy.Compiler", projects);
        Assert.Contains("Sharpy.Core", projects);
        Assert.Contains("Sharpy.Lsp", projects);
        Assert.Contains("Sharpy.Stdlib", projects);
        Assert.DoesNotContain("Sharpy.Cli.Tests", projects);
        Assert.DoesNotContain("Sharpy.TestInfrastructure", projects);
    }

    private static IEnumerable<string> ProductProjectDirectories(string root) =>
        Directory.EnumerateDirectories(Path.Combine(root, "src"))
            .Where(dir => Directory.EnumerateFiles(dir, "*.csproj").Any())
            .Where(dir =>
            {
                var name = Path.GetFileName(dir);
                return !name.EndsWith(".Tests", StringComparison.Ordinal)
                    && !name.EndsWith(".Benchmarks", StringComparison.Ordinal)
                    && name != "Sharpy.TestInfrastructure";
            });

    private static IEnumerable<string> ProductSourceFiles(string root) =>
        ProductProjectDirectories(root)
            .SelectMany(dir => Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories))
            .Where(file =>
            {
                var relative = file.Replace('\\', '/');
                return !relative.Contains("/bin/", StringComparison.Ordinal)
                    && !relative.Contains("/obj/", StringComparison.Ordinal);
            });

    private static string FindRepoRoot()
    {
        var current = AppContext.BaseDirectory;
        while (current != null)
        {
            if (Directory.Exists(Path.Combine(current, ".git")) || File.Exists(Path.Combine(current, ".git")))
                return current;
            current = Directory.GetParent(current)?.FullName;
        }

        throw new InvalidOperationException(
            $"Could not find repository root starting from '{AppContext.BaseDirectory}'.");
    }
}
