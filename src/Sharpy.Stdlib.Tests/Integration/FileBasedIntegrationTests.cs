using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;
using IOPath = System.IO.Path;

// The stdlib fixture corpus runs as ShardCount classes in a NAMESPACE named
// FileBasedIntegrationTests, so every row's FullyQualifiedName still starts
// `Sharpy.Stdlib.Tests.Integration.FileBasedIntegrationTests.` and `~FileBasedIntegrationTests` /
// `DisplayName~` select all of them unchanged (#2179). One class would serialise the corpus: xUnit
// v2 never runs two rows of one class concurrently.
namespace Sharpy.Stdlib.Tests.Integration.FileBasedIntegrationTests;

public abstract class StdlibFixtureShard : FileBasedIntegrationTestsBase
{
    /// <summary>
    /// K. The corpus is 241 rows (~24 s at one thread @ 3c91f6ade), so 4 shards of ~60 rows are
    /// enough to keep it off a parallel run's critical path. Adding a shard means adding a ShardN
    /// class below; the totality test pins the class count.
    /// </summary>
    internal const int ShardCount = 4;

    private static readonly string FixturesPathValue = FixtureRoots.StdlibTests.Path;

    private static readonly string[] StdlibPaths = ResolveStdlibPaths();

    protected override string FixturesPath => FixturesPathValue;

    protected StdlibFixtureShard(ITestOutputHelper output) : base(output)
    {
    }

    protected override IEnumerable<string> GetAdditionalReferenceAssemblyPaths() => StdlibPaths;

    protected static IEnumerable<object[]> FixturesOfShard(int shard)
        => DiscoverTestFixtures(FixturesPathValue, shard, ShardCount);

    private static string[] ResolveStdlibPaths()
    {
        var testDir = IOPath.GetDirectoryName(typeof(StdlibFixtureShard).Assembly.Location)!;
        var path = FindAssembly(testDir, "Sharpy.Stdlib", "Sharpy.Stdlib.dll");
        return path != null ? new[] { path } : Array.Empty<string>();
    }
}

public class Shard0 : StdlibFixtureShard
{
    public Shard0(ITestOutputHelper output) : base(output) { }

    public static IEnumerable<object[]> GetTestFixtures() => FixturesOfShard(0);

    [Theory]
    [MemberData(nameof(GetTestFixtures))]
    public void RunTestFixture(string testName, string path, bool isMultiFile)
        => RunTestFixtureImpl(testName, path, isMultiFile);
}

public class Shard1 : StdlibFixtureShard
{
    public Shard1(ITestOutputHelper output) : base(output) { }

    public static IEnumerable<object[]> GetTestFixtures() => FixturesOfShard(1);

    [Theory]
    [MemberData(nameof(GetTestFixtures))]
    public void RunTestFixture(string testName, string path, bool isMultiFile)
        => RunTestFixtureImpl(testName, path, isMultiFile);
}

public class Shard2 : StdlibFixtureShard
{
    public Shard2(ITestOutputHelper output) : base(output) { }

    public static IEnumerable<object[]> GetTestFixtures() => FixturesOfShard(2);

    [Theory]
    [MemberData(nameof(GetTestFixtures))]
    public void RunTestFixture(string testName, string path, bool isMultiFile)
        => RunTestFixtureImpl(testName, path, isMultiFile);
}

public class Shard3 : StdlibFixtureShard
{
    public Shard3(ITestOutputHelper output) : base(output) { }

    public static IEnumerable<object[]> GetTestFixtures() => FixturesOfShard(3);

    [Theory]
    [MemberData(nameof(GetTestFixtures))]
    public void RunTestFixture(string testName, string path, bool isMultiFile)
        => RunTestFixtureImpl(testName, path, isMultiFile);
}
