namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// Structured metadata for a discovered test fixture.
/// </summary>
public record TestFixtureInfo
{
    public required string TestName { get; init; }
    public required string SpyFilePath { get; init; }
    public string? ExpectedFile { get; init; }
    public string? ErrorFile { get; init; }
    public string? WarningFile { get; init; }
    public string? ExpectedCsFile { get; init; }
    public bool IsMultiFile { get; init; }
    public string Category { get; init; } = "";
}

/// <summary>
/// Discovers test fixtures from the TestFixtures directory.
/// Extracted from FileBasedIntegrationTests for reuse by other test classes.
/// </summary>
public static class FixtureDiscoveryHelper
{
    public static readonly string FixturesPath = Path.GetFullPath(Path.Combine(
        Path.GetDirectoryName(typeof(FixtureDiscoveryHelper).Assembly.Location)!,
        "..", "..", "..", "Integration", "TestFixtures"));

    /// <summary>
    /// Discovers all test fixtures by scanning the TestFixtures directory.
    /// Supports both single-file tests and multi-file tests (including packages with subdirectories).
    /// </summary>
    public static IEnumerable<TestFixtureInfo> DiscoverFixtures(string? fixturesPath = null)
    {
        var basePath = fixturesPath ?? FixturesPath;

        if (!Directory.Exists(basePath))
        {
            yield break;
        }

        // First pass: identify all multi-file test root directories
        var multiFileTestRoots = new HashSet<string>();
        foreach (var dir in Directory.EnumerateDirectories(basePath, "*", SearchOption.AllDirectories))
        {
            var hasMainSpy = File.Exists(Path.Combine(dir, "main.spy"));
            var hasMainExpected = File.Exists(Path.Combine(dir, "main.expected"));
            var hasMainError = File.Exists(Path.Combine(dir, "main.error"));

            if (hasMainSpy || hasMainExpected || hasMainError)
            {
                var spyFilesCount = Directory.GetFiles(dir, "*.spy", SearchOption.AllDirectories).Length;
                if (spyFilesCount > 1)
                {
                    multiFileTestRoots.Add(dir);
                }
            }
        }

        var processedDirectories = new HashSet<string>();

        foreach (var spyFile in Directory.EnumerateFiles(basePath, "*.spy", SearchOption.AllDirectories))
        {
            var spyDir = Path.GetDirectoryName(spyFile)!;
            var multiFileRoot = FindMultiFileTestRoot(spyDir, multiFileTestRoots);

            if (multiFileRoot != null)
            {
                if (processedDirectories.Contains(multiFileRoot))
                {
                    continue;
                }
                processedDirectories.Add(multiFileRoot);

                var skipFile = Path.Combine(multiFileRoot, "main.skip");
                if (File.Exists(skipFile))
                {
                    continue;
                }

                var relativePath = Path.GetRelativePath(basePath, multiFileRoot);
                var testName = relativePath.Replace(Path.DirectorySeparatorChar, '/');
                var category = ExtractCategory(relativePath);

                var expectedFile = Path.Combine(multiFileRoot, "main.expected");
                var errorFile = Path.Combine(multiFileRoot, "main.error");
                var warningFile = Path.Combine(multiFileRoot, "main.warning");
                var expectedCsFile = Path.Combine(multiFileRoot, "main.expected.cs");

                yield return new TestFixtureInfo
                {
                    TestName = testName,
                    SpyFilePath = multiFileRoot,
                    ExpectedFile = File.Exists(expectedFile) ? expectedFile : null,
                    ErrorFile = File.Exists(errorFile) ? errorFile : null,
                    WarningFile = File.Exists(warningFile) ? warningFile : null,

                    ExpectedCsFile = File.Exists(expectedCsFile) ? expectedCsFile : null,
                    IsMultiFile = true,
                    Category = category,
                };
            }
            else
            {
                var skipFile = Path.ChangeExtension(spyFile, ".skip");
                if (File.Exists(skipFile))
                {
                    continue;
                }

                var relativePath = Path.GetRelativePath(basePath, spyFile);
                var testName = Path.ChangeExtension(relativePath, null)
                    .Replace(Path.DirectorySeparatorChar, '/');
                var category = ExtractCategory(relativePath);

                var expectedFile = Path.ChangeExtension(spyFile, ".expected");
                var errorFile = Path.ChangeExtension(spyFile, ".error");
                var warningFile = Path.ChangeExtension(spyFile, ".warning");
                var expectedCsFile = spyFile.Replace(".spy", ".expected.cs");

                yield return new TestFixtureInfo
                {
                    TestName = testName,
                    SpyFilePath = spyFile,
                    ExpectedFile = File.Exists(expectedFile) ? expectedFile : null,
                    ErrorFile = File.Exists(errorFile) ? errorFile : null,
                    WarningFile = File.Exists(warningFile) ? warningFile : null,

                    ExpectedCsFile = File.Exists(expectedCsFile) ? expectedCsFile : null,
                    IsMultiFile = false,
                    Category = category,
                };
            }
        }
    }

    private static string? FindMultiFileTestRoot(string path, HashSet<string> multiFileTestRoots)
    {
        if (multiFileTestRoots.Contains(path))
        {
            return path;
        }

        foreach (var root in multiFileTestRoots)
        {
            if (path.StartsWith(root + Path.DirectorySeparatorChar))
            {
                return root;
            }
        }

        return null;
    }

    private static string ExtractCategory(string relativePath)
    {
        var separatorIndex = relativePath.IndexOf(Path.DirectorySeparatorChar);
        if (separatorIndex < 0)
        {
            separatorIndex = relativePath.IndexOf('/');
        }

        return separatorIndex >= 0 ? relativePath.Substring(0, separatorIndex) : "";
    }
}
