using FluentAssertions;
using Xunit;

namespace Sharpy.Lsp.Tests;

public class DefaultReferenceSetTests
{
    [Fact]
    public void CoreAndStdlib_present_returns_two_references_and_no_warnings()
    {
        var tmpDir = Path.Combine(Path.GetTempPath(), $"sharpy-refset-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tmpDir);
        try
        {
            var corePath = Path.Combine(tmpDir, "Sharpy.Core.dll");
            var stdlibPath = Path.Combine(tmpDir, "Sharpy.Stdlib.dll");
            File.WriteAllBytes(corePath, Array.Empty<byte>());
            File.WriteAllBytes(stdlibPath, Array.Empty<byte>());

            var result = DefaultReferenceSet.Resolve(corePath);

            result.References.Should().HaveCount(2);
            result.References.Should().Contain(corePath);
            result.References.Should().Contain(stdlibPath);
            result.Warnings.Should().BeEmpty("a complete deployment has no warnings");
        }
        finally
        {
            Directory.Delete(tmpDir, recursive: true);
        }
    }

    [Fact]
    public void CoreOnly_missing_stdlib_returns_one_reference_and_one_warning()
    {
        var tmpDir = Path.Combine(Path.GetTempPath(), $"sharpy-refset-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tmpDir);
        try
        {
            var corePath = Path.Combine(tmpDir, "Sharpy.Core.dll");
            File.WriteAllBytes(corePath, Array.Empty<byte>());

            var result = DefaultReferenceSet.Resolve(corePath);

            result.References.Should().HaveCount(1);
            result.References.Should().Contain(corePath);
            result.Warnings.Should().ContainSingle()
                .Which.Should().Contain("Sharpy.Stdlib.dll")
                .And.Contain("deployment fault");
        }
        finally
        {
            Directory.Delete(tmpDir, recursive: true);
        }
    }

    [Fact]
    public void Missing_stdlib_warning_names_the_probed_path()
    {
        var tmpDir = Path.Combine(Path.GetTempPath(), $"sharpy-refset-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tmpDir);
        try
        {
            var corePath = Path.Combine(tmpDir, "Sharpy.Core.dll");
            File.WriteAllBytes(corePath, Array.Empty<byte>());
            var expectedPath = Path.Combine(tmpDir, "Sharpy.Stdlib.dll");

            var result = DefaultReferenceSet.Resolve(corePath);

            result.Warnings.Should().ContainSingle()
                .Which.Should().Contain(expectedPath,
                    "the warning must name the exact probed path so the deployment operator can act");
        }
        finally
        {
            Directory.Delete(tmpDir, recursive: true);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Empty_or_null_location_returns_no_references_and_a_warning(string? location)
    {
        var result = DefaultReferenceSet.Resolve(location!);

        result.References.Should().BeEmpty();
        result.Warnings.Should().ContainSingle()
            .Which.Should().Contain("empty location");
    }
}
