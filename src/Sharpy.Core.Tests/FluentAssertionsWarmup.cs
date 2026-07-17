using System.Runtime.CompilerServices;
using FluentAssertions;

namespace Sharpy.Core.Tests;

/// <summary>
/// FluentAssertions 8.x (Xceed) lazily prints its license banner to
/// <see cref="System.Console.Out"/> on the first <c>.Should()</c> call in the
/// process. Stdout-capture tests redirect <see cref="System.Console.Out"/>, so a
/// banner emitted mid-test races into the captured buffer and corrupts the
/// assertion (see #1060). Performing one throwaway assertion from a
/// <see cref="ModuleInitializerAttribute"/> forces the banner onto the real
/// console at assembly load, before any test can redirect it.
/// </summary>
internal static class FluentAssertionsWarmup
{
    [ModuleInitializer]
    internal static void EmitLicenseBanner()
    {
        true.Should().BeTrue();
    }
}
