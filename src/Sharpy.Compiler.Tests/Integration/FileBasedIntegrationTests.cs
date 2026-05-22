using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Integration;

[Collection("HeavyCompilation")]
public class FileBasedIntegrationTests : FileBasedIntegrationTestsBase
{
    private static readonly string FixturesPathValue = FixtureDiscoveryHelper.FixturesPath;

    protected override string FixturesPath => FixturesPathValue;

    public FileBasedIntegrationTests(ITestOutputHelper output) : base(output)
    {
    }

    public static IEnumerable<object[]> GetTestFixtures()
        => DiscoverTestFixtures(FixturesPathValue);

    [Theory]
    [MemberData(nameof(GetTestFixtures))]
    public void RunTestFixture(string testName, string path, bool isMultiFile)
        => RunTestFixtureImpl(testName, path, isMultiFile);
}
