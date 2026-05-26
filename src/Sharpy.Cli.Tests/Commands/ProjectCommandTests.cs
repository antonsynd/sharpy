using FluentAssertions;
using Xunit;

namespace Sharpy.Cli.Tests.Commands;

public class ProjectCommandTests
{
    [Fact]
    public void Parses_WithProjectFileArgument()
    {
        var result = CliTestHarness.Parse("project app.spyproj");

        result.Errors.Should().BeEmpty();
        result.CommandResult.Command.Name.Should().Be("project");
        result.GetValue<FileInfo?>("project")!.Name.Should().Be("app.spyproj");
    }

    [Fact]
    public void Parses_WithoutProjectFile_ArgumentIsOptional()
    {
        // The project argument has ZeroOrOne arity (auto-discovery).
        var result = CliTestHarness.Parse("project");

        result.Errors.Should().BeEmpty();
        result.GetValue<FileInfo?>("project").Should().BeNull();
    }

    [Fact]
    public void Parses_IncrementalFlag()
    {
        var result = CliTestHarness.Parse("project app.spyproj --incremental");

        result.Errors.Should().BeEmpty();
        result.GetValue<bool>("--incremental").Should().BeTrue();
    }

    [Fact]
    public void Parses_CleanFlag()
    {
        var result = CliTestHarness.Parse("project app.spyproj --clean");

        result.Errors.Should().BeEmpty();
        result.GetValue<bool>("--clean").Should().BeTrue();
    }

    [Fact]
    public void Parses_EmitCsToDirectory()
    {
        var result = CliTestHarness.Parse("project app.spyproj --emit-cs-to generated");

        result.Errors.Should().BeEmpty();
        result.GetValue<DirectoryInfo?>("--emit-cs-to")!.Name.Should().Be("generated");
    }

    [Theory]
    [InlineData("Debug")]
    [InlineData("Release")]
    public void Parses_ConfigurationOption(string config)
    {
        var longForm = CliTestHarness.Parse($"project app.spyproj --configuration {config}");
        var shortForm = CliTestHarness.Parse($"project app.spyproj -c {config}");

        longForm.Errors.Should().BeEmpty();
        shortForm.Errors.Should().BeEmpty();
        longForm.GetValue<string?>("--configuration").Should().Be(config);
        shortForm.GetValue<string?>("--configuration").Should().Be(config);
    }

    [Fact]
    public void Rejects_UnknownOption()
    {
        var result = CliTestHarness.Parse("project app.spyproj --nope");

        result.Errors.Should().NotBeEmpty();
    }
}
