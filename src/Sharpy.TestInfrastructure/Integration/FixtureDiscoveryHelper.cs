using Sharpy.Compiler.Shared;

namespace Sharpy.TestInfrastructure.Integration;

/// <summary>
/// Structured metadata for a discovered test fixture.
/// </summary>
public record TestFixtureInfo
{
    public required string TestName { get; init; }
    public required string SpyFilePath { get; init; }
    public string? ExpectedFile { get; init; }
    public string? ErrorFile { get; init; }
    public string? RuntimeErrorFile { get; init; }
    public string? WarningFile { get; init; }
    public string? ExpectedCsFile { get; init; }
    public bool IsMultiFile { get; init; }
    public string Category { get; init; } = "";

    /// <summary>
    /// Experimental feature names declared in this fixture's <c>.features</c> sidecar
    /// (empty when there is no sidecar). These are enabled compilation-wide when the
    /// fixture is compiled, gating features such as <c>matmul</c> and <c>defer</c>.
    /// </summary>
    public IReadOnlyList<string> Features { get; init; } = Array.Empty<string>();
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
                var runtimeErrorFile = Path.Combine(multiFileRoot, "main.runtime-error");
                var warningFile = Path.Combine(multiFileRoot, "main.warning");
                var expectedCsFile = Path.Combine(multiFileRoot, "main.expected.cs");
                var featuresFile = Path.Combine(multiFileRoot, "main.features");

                yield return new TestFixtureInfo
                {
                    TestName = testName,
                    SpyFilePath = multiFileRoot,
                    ExpectedFile = File.Exists(expectedFile) ? expectedFile : null,
                    ErrorFile = File.Exists(errorFile) ? errorFile : null,
                    RuntimeErrorFile = File.Exists(runtimeErrorFile) ? runtimeErrorFile : null,
                    WarningFile = File.Exists(warningFile) ? warningFile : null,

                    ExpectedCsFile = File.Exists(expectedCsFile) ? expectedCsFile : null,
                    IsMultiFile = true,
                    Category = category,
                    Features = ReadFeaturesFile(featuresFile),
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
                var runtimeErrorFile = spyFile.Replace(".spy", ".runtime-error", StringComparison.Ordinal);
                var warningFile = Path.ChangeExtension(spyFile, ".warning");
                var expectedCsFile = spyFile.Replace(".spy", ".expected.cs", StringComparison.Ordinal);
                var featuresFile = Path.ChangeExtension(spyFile, ".features");

                yield return new TestFixtureInfo
                {
                    TestName = testName,
                    SpyFilePath = spyFile,
                    ExpectedFile = File.Exists(expectedFile) ? expectedFile : null,
                    ErrorFile = File.Exists(errorFile) ? errorFile : null,
                    RuntimeErrorFile = File.Exists(runtimeErrorFile) ? runtimeErrorFile : null,
                    WarningFile = File.Exists(warningFile) ? warningFile : null,

                    ExpectedCsFile = File.Exists(expectedCsFile) ? expectedCsFile : null,
                    IsMultiFile = false,
                    Category = category,
                    Features = ReadFeaturesFile(featuresFile),
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

    /// <summary>
    /// Reads a fixture's <c>.features</c> sidecar, if present. Each non-blank, non-comment
    /// line names one experimental feature to enable compilation-wide for the fixture
    /// (<c># ...</c> comments and blank lines are tolerated). Every name is validated against
    /// <see cref="FeatureFlags.KnownFeatures"/>; an unknown name throws loudly (naming both the
    /// sidecar file and the bad name) rather than being silently ignored, so a typo can never
    /// quietly leave a gated feature disabled.
    /// </summary>
    /// <returns>The declared feature names, or an empty list when the sidecar is absent.</returns>
    public static IReadOnlyList<string> ReadFeaturesFile(string featuresFilePath)
    {
        if (!File.Exists(featuresFilePath))
        {
            return Array.Empty<string>();
        }

        var features = new List<string>();
        foreach (var rawLine in File.ReadAllLines(featuresFilePath))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            if (!FeatureFlags.TryValidate(line, out var error))
            {
                throw new InvalidOperationException(
                    $"Invalid '.features' sidecar '{featuresFilePath}': {error}");
            }

            features.Add(line);
        }

        return features;
    }

    private static string ExtractCategory(string relativePath)
    {
        var separatorIndex = relativePath.IndexOf(Path.DirectorySeparatorChar, StringComparison.Ordinal);
        if (separatorIndex < 0)
        {
            separatorIndex = relativePath.IndexOf('/', StringComparison.Ordinal);
        }

        return separatorIndex >= 0 ? relativePath.Substring(0, separatorIndex) : "";
    }
}
