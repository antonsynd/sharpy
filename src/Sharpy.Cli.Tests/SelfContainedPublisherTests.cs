using FluentAssertions;
using Sharpy.Cli;
using Xunit;

namespace Sharpy.Cli.Tests;

/// <summary>
/// The self-contained publish entry point (#1483). Before the fix, the wrapper's entry call
/// interpolated the raw file stem as a C# identifier (<c>sc_probe.Main()</c>) against the emitter's
/// mangled module class (<c>ScProbe</c>), so EVERY publish failed to compile with CS0103. This is the
/// fast, deterministic guard for the wrapper half of the fix — that the reflection entry point uses
/// the mangled type name — without shelling out to <c>dotnet publish</c>. The caller half (mangling
/// via NameMangler.ComputeModuleClassName) and the end-to-end publish-and-run are covered by
/// <c>E2E/SelfContainedDeploymentTests</c>.
/// </summary>
public class SelfContainedPublisherTests
{
    [Fact]
    public void BuildEntryPointSource_ReflectsTheMangledType_NotTheRawStem()
    {
        // The wrapper reflection-loads the program and invokes Main on the MANGLED module class.
        var source = SelfContainedPublisher.BuildEntryPointSource("__sharpy_program.dll", "ScProbe");

        source.Should().Contain("GetType(\"ScProbe\")");
        source.Should().Contain("GetMethod(\"Main\")");
        // The raw stem must never appear as the reflected type — that was the CS0103 defect.
        source.Should().NotContain("GetType(\"sc_probe\")");
        // The program is loaded by its copied file name from the published directory.
        source.Should().Contain("__sharpy_program.dll");
        source.Should().Contain("AppContext.BaseDirectory");
        // A dedicated load context, because the program's identity may equal the wrapper's.
        source.Should().Contain("AssemblyLoadContext(");
        // MUTATION: pass the raw stem to entryTypeName in RunCommand/CompileCommand (revert to
        // Path.GetFileNameWithoutExtension) → ComputeModuleClassName no longer feeds this, the wrapper
        // reflects GetType("sc_probe"), the type is not found, and the E2E publish-and-run goes red.
    }
}
