using Sharpy.Compiler.Project;
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
    /// The <see cref="FixtureRoot.Label"/> of the corpus this fixture came from — empty for the
    /// primary root. A cross-corpus sweep reads this to decide what a fixture needs (stdlib
    /// fixtures need <c>Sharpy.Stdlib.dll</c> referenced) and to report per-root coverage.
    /// </summary>
    public string RootLabel { get; init; } = "";

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
    /// <summary>
    /// Discovers all test fixtures under one directory.
    /// Supports both single-file tests and multi-file tests (including packages with subdirectories).
    /// </summary>
    /// <remarks>
    /// The path is required. There used to be a host-anchored default, which meant a sweep's
    /// corpus was whatever fixture directory sat next to the test project hosting it — unstated at
    /// the call site and unnoticed when wrong (#1338). Callers covering a named corpus use
    /// <see cref="DiscoverFixturesFrom"/>; this overload is for the paths that are not named roots
    /// (the file-based harness base class, told its root by the concrete test class, and the
    /// temp-directory discovery unit tests).
    /// </remarks>
    public static IEnumerable<TestFixtureInfo> DiscoverFixtures(string fixturesPath)
        => DiscoverFrom(new FixtureRoot { Path = fixturesPath });

    /// <summary>
    /// Discovers fixtures across one or more declared corpora. Each root's fixtures carry that
    /// root's <see cref="FixtureRoot.Label"/> as a test-name prefix and in
    /// <see cref="TestFixtureInfo.RootLabel"/>, so a cross-corpus sweep's keys stay unambiguous.
    /// </summary>
    public static IEnumerable<TestFixtureInfo> DiscoverFixturesFrom(params FixtureRoot[] roots)
        => roots.SelectMany(DiscoverFrom);

    /// <summary>
    /// Path segments that hold build output or compiler scratch rather than corpus. A multi-file
    /// fixture is compiled in place, so <c>bin</c>/<c>obj</c> appear inside the tree; and when a
    /// compilation CRASHES the bundle it writes under <c>&lt;output&gt;/.sharpy-crash/&lt;stamp&gt;/</c>
    /// includes <em>copies of the sources</em>. Without this exclusion the next run discovers those
    /// copies as fixtures, and since a crash bundle carries no expectation file they fail as
    /// "Missing expected output file" — a red suite caused by an earlier run rather than by any
    /// change under test, self-inflicted and reproducible only after a crash (#1484).
    /// </summary>
    /// <summary>
    /// Whether <paramref name="path"/> lies under a build-output or scratch directory relative to
    /// the corpus root. Delegates to <see cref="Sharpy.Compiler.Diagnostics.CrashBundleWriter.IsNonSourceSegment"/>
    /// so the compiler, formatter, and test infrastructure share one predicate (#1660).
    /// </summary>
    private static bool IsNonCorpus(string basePath, string path)
        => Compiler.Diagnostics.CrashBundleWriter.IsNonSourceSegment(
            Path.GetRelativePath(basePath, path));

    private static IEnumerable<TestFixtureInfo> DiscoverFrom(FixtureRoot root)
    {
        var basePath = root.Path;

        if (!Directory.Exists(basePath))
        {
            yield break;
        }

        // First pass: identify all multi-file test root directories
        var multiFileTestRoots = new HashSet<string>();
        foreach (var dir in SourceGlob.EnumerateSourceDirectories(basePath, "*", SearchOption.AllDirectories))
        {
            if (IsNonCorpus(basePath, dir))
            {
                continue;
            }

            var hasMainSpy = File.Exists(Path.Combine(dir, "main.spy"));
            var hasMainExpected = File.Exists(Path.Combine(dir, "main.expected"));
            var hasMainError = File.Exists(Path.Combine(dir, "main.error"));

            if (hasMainSpy || hasMainExpected || hasMainError)
            {
                var spyFilesCount = SourceGlob.EnumerateSourceFiles(dir, "*.spy", SearchOption.AllDirectories)
                    .Count(f => !IsNonCorpus(basePath, f));
                if (spyFilesCount > 1)
                {
                    multiFileTestRoots.Add(dir);
                }
            }
        }

        var processedDirectories = new HashSet<string>();

        foreach (var spyFile in SourceGlob.EnumerateSourceFiles(basePath, "*.spy", SearchOption.AllDirectories))
        {
            if (IsNonCorpus(basePath, spyFile))
            {
                continue;
            }

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
                var testName = Qualify(root, relativePath.Replace(Path.DirectorySeparatorChar, '/'));
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
                    RootLabel = root.Label,
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
                var testName = Qualify(root, Path.ChangeExtension(relativePath, null)
                    .Replace(Path.DirectorySeparatorChar, '/'));
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
                    RootLabel = root.Label,
                    Features = ReadFeaturesFile(featuresFile),
                };
            }
        }
    }

    /// <summary>
    /// Qualifies a root-relative test name with the root's label. The primary root's label is
    /// empty, so its names are unchanged — the allowlists keyed on them keep matching.
    /// </summary>
    private static string Qualify(FixtureRoot root, string testName)
        => root.Label.Length == 0 ? testName : $"{root.Label}/{testName}";

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
