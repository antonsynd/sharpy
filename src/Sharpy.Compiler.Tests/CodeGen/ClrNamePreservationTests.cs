using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// Tests that the original CLR method name is preserved through the discovery
/// pipeline and emitted verbatim in generated C#, so acronym casing survives
/// the snake_case round-trip (e.g., is_os_platform → IsOSPlatform, not IsOsPlatform).
/// See https://github.com/antonsynd/sharpy/issues/705.
/// </summary>
public class ClrNamePreservationTests
{
    private readonly ITestOutputHelper _output;
    private readonly CompilerApi _api = new();

    public ClrNamePreservationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Compile_SnakeCaseClrMethodWithAcronym_EmitsOriginalClrName()
    {
        var source = @"
from system.runtime.interop_services import RuntimeInformation, OSPlatform

def main() -> None:
    is_win: bool = RuntimeInformation.is_os_platform(OSPlatform.windows)
    print(is_win)
";
        var result = _api.Compile(source);

        _output.WriteLine(result.GeneratedCSharp);
        result.Diagnostics.Where(d => d.IsError).Should().BeEmpty();
        result.GeneratedCSharp.Should().Contain("IsOSPlatform",
            "the original CLR acronym casing must be preserved");
        result.GeneratedCSharp.Should().NotContain("IsOsPlatform",
            "name mangling must not corrupt the acronym");
    }

    [Fact]
    public void Compile_PascalCaseClrMethodWithAcronym_PreservesOriginalClrName()
    {
        // When the user already writes the verbatim CLR name, it must round-trip unchanged.
        var source = @"
from system.runtime.interop_services import RuntimeInformation, OSPlatform

def main() -> None:
    is_win: bool = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
    print(is_win)
";
        var result = _api.Compile(source);

        _output.WriteLine(result.GeneratedCSharp);
        result.Diagnostics.Where(d => d.IsError).Should().BeEmpty();
        result.GeneratedCSharp.Should().Contain("IsOSPlatform");
        result.GeneratedCSharp.Should().NotContain("IsOsPlatform");
    }
}
