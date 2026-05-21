using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Integration;

public abstract class StdlibAwareIntegrationTestBase : IntegrationTestBase
{
    protected StdlibAwareIntegrationTestBase(ITestOutputHelper output) : base(output)
    {
    }

    protected override IEnumerable<string> GetAdditionalReferenceAssemblyPaths()
    {
        yield return SharpyStdlibReference.Location;
    }
}
