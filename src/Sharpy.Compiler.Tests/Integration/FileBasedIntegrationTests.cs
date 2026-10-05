using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

// The fixture corpus runs as ShardCount classes in a NAMESPACE named FileBasedIntegrationTests, so
// every row's FullyQualifiedName still starts `Sharpy.Compiler.Tests.Integration.FileBasedIntegrationTests.`
// and `~FileBasedIntegrationTests` / `DisplayName~` / the CI trailing-dot shard filter select all of
// them unchanged (#2179). One class would serialise the whole corpus: xUnit v2 never runs two rows
// of one class concurrently.
namespace Sharpy.Compiler.Tests.Integration.FileBasedIntegrationTests;

public abstract class CompilerFixtureShard : FileBasedIntegrationTestsBase
{
    /// <summary>
    /// K. Sized so that no shard is the long pole of a parallel run: as one class the corpus took
    /// 657 s at one thread @ 116c6e29d, against 576 s for the next-longest class
    /// (LetEmissionInvarianceTests), so 8 shards of ~80 s fill 8 threads without bounding them.
    /// Adding a shard means adding a ShardN class below; the totality test pins the class count.
    /// </summary>
    internal const int ShardCount = 8;

    private static readonly string FixturesPathValue = FixtureRoots.CompilerTests.Path;

    protected override string FixturesPath => FixturesPathValue;

    protected CompilerFixtureShard(ITestOutputHelper output) : base(output)
    {
    }

    protected static IEnumerable<object[]> FixturesOfShard(int shard)
        => DiscoverTestFixtures(FixturesPathValue, shard, ShardCount);
}

public class Shard0 : CompilerFixtureShard
{
    public Shard0(ITestOutputHelper output) : base(output) { }

    public static IEnumerable<object[]> GetTestFixtures() => FixturesOfShard(0);

    [Theory]
    [MemberData(nameof(GetTestFixtures))]
    public void RunTestFixture(string testName, string path, bool isMultiFile)
        => RunTestFixtureImpl(testName, path, isMultiFile);
}

public class Shard1 : CompilerFixtureShard
{
    public Shard1(ITestOutputHelper output) : base(output) { }

    public static IEnumerable<object[]> GetTestFixtures() => FixturesOfShard(1);

    [Theory]
    [MemberData(nameof(GetTestFixtures))]
    public void RunTestFixture(string testName, string path, bool isMultiFile)
        => RunTestFixtureImpl(testName, path, isMultiFile);
}

public class Shard2 : CompilerFixtureShard
{
    public Shard2(ITestOutputHelper output) : base(output) { }

    public static IEnumerable<object[]> GetTestFixtures() => FixturesOfShard(2);

    [Theory]
    [MemberData(nameof(GetTestFixtures))]
    public void RunTestFixture(string testName, string path, bool isMultiFile)
        => RunTestFixtureImpl(testName, path, isMultiFile);
}

public class Shard3 : CompilerFixtureShard
{
    public Shard3(ITestOutputHelper output) : base(output) { }

    public static IEnumerable<object[]> GetTestFixtures() => FixturesOfShard(3);

    [Theory]
    [MemberData(nameof(GetTestFixtures))]
    public void RunTestFixture(string testName, string path, bool isMultiFile)
        => RunTestFixtureImpl(testName, path, isMultiFile);
}

public class Shard4 : CompilerFixtureShard
{
    public Shard4(ITestOutputHelper output) : base(output) { }

    public static IEnumerable<object[]> GetTestFixtures() => FixturesOfShard(4);

    [Theory]
    [MemberData(nameof(GetTestFixtures))]
    public void RunTestFixture(string testName, string path, bool isMultiFile)
        => RunTestFixtureImpl(testName, path, isMultiFile);
}

public class Shard5 : CompilerFixtureShard
{
    public Shard5(ITestOutputHelper output) : base(output) { }

    public static IEnumerable<object[]> GetTestFixtures() => FixturesOfShard(5);

    [Theory]
    [MemberData(nameof(GetTestFixtures))]
    public void RunTestFixture(string testName, string path, bool isMultiFile)
        => RunTestFixtureImpl(testName, path, isMultiFile);
}

public class Shard6 : CompilerFixtureShard
{
    public Shard6(ITestOutputHelper output) : base(output) { }

    public static IEnumerable<object[]> GetTestFixtures() => FixturesOfShard(6);

    [Theory]
    [MemberData(nameof(GetTestFixtures))]
    public void RunTestFixture(string testName, string path, bool isMultiFile)
        => RunTestFixtureImpl(testName, path, isMultiFile);
}

public class Shard7 : CompilerFixtureShard
{
    public Shard7(ITestOutputHelper output) : base(output) { }

    public static IEnumerable<object[]> GetTestFixtures() => FixturesOfShard(7);

    [Theory]
    [MemberData(nameof(GetTestFixtures))]
    public void RunTestFixture(string testName, string path, bool isMultiFile)
        => RunTestFixtureImpl(testName, path, isMultiFile);
}
