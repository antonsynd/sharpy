using Sharpy.TestInfrastructure.Integration;
using Xunit.Abstractions;

namespace Sharpy.Stdlib.Tests.Integration;

public abstract class StdlibIntegrationTestBase : IntegrationTestBase
{
    private static readonly string[] StdlibPaths = ResolveStdlibPaths();

    protected StdlibIntegrationTestBase(ITestOutputHelper output) : base(output)
    {
    }

    protected override IEnumerable<string> GetAdditionalReferenceAssemblyPaths() => StdlibPaths;

    private static string[] ResolveStdlibPaths()
    {
        var testDir = System.IO.Path.GetDirectoryName(typeof(StdlibIntegrationTestBase).Assembly.Location)!;
        var path = FindAssembly(testDir, "Sharpy.Stdlib", "Sharpy.Stdlib.dll");
        return path != null ? new[] { path } : Array.Empty<string>();
    }
}
