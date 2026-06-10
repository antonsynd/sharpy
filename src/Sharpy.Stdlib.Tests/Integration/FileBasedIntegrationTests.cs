using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;
using IOPath = System.IO.Path;

namespace Sharpy.Stdlib.Tests.Integration;

[Collection("HeavyCompilation")]
public class FileBasedIntegrationTests : FileBasedIntegrationTestsBase
{
    private static readonly string FixturesPathValue = IOPath.GetFullPath(IOPath.Combine(
        IOPath.GetDirectoryName(typeof(FileBasedIntegrationTests).Assembly.Location)!,
        "..", "..", "..", "Integration", "TestFixtures"));

    private static readonly string[] StdlibPaths = ResolveStdlibPaths();

    protected override string FixturesPath => FixturesPathValue;

    public FileBasedIntegrationTests(ITestOutputHelper output) : base(output)
    {
    }

    protected override IEnumerable<string> GetAdditionalReferenceAssemblyPaths() => StdlibPaths;

    public static IEnumerable<object[]> GetTestFixtures()
        => DiscoverTestFixtures(FixturesPathValue);

    [Theory]
    [MemberData(nameof(GetTestFixtures))]
    public void RunTestFixture(string testName, string path, bool isMultiFile)
        => RunTestFixtureImpl(testName, path, isMultiFile);

    private static string[] ResolveStdlibPaths()
    {
        var testDir = IOPath.GetDirectoryName(typeof(FileBasedIntegrationTests).Assembly.Location)!;
        var path = FindAssembly(testDir, "Sharpy.Stdlib", "Sharpy.Stdlib.dll");
        return path != null ? new[] { path } : Array.Empty<string>();
    }
}
