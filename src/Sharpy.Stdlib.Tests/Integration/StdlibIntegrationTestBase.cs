using Sharpy.Compiler.Tests.Integration;
using Xunit.Abstractions;
using IOPath = System.IO.Path;

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
        var testDir = IOPath.GetDirectoryName(typeof(StdlibIntegrationTestBase).Assembly.Location)!;
        var possibleFrameworks = new[] { "net10.0", "netstandard2.1" };

        foreach (var tf in possibleFrameworks)
        {
            var candidate = IOPath.GetFullPath(IOPath.Combine(testDir, "..", "..", "..", "..", "Sharpy.Stdlib", "bin", "Debug", tf, "Sharpy.Stdlib.dll"));
            if (File.Exists(candidate))
                return new[] { candidate };
        }

        var siblingPath = IOPath.Combine(testDir, "Sharpy.Stdlib.dll");
        if (File.Exists(siblingPath))
            return new[] { siblingPath };

        return Array.Empty<string>();
    }
}
