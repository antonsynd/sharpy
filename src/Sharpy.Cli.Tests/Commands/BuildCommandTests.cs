using FluentAssertions;
using Xunit;

namespace Sharpy.Cli.Tests.Commands;

public class BuildCommandTests
{
    [Fact]
    public void Parses_MinimalInvocation()
    {
        var result = CliTestHarness.Parse("build main.spy");

        result.Errors.Should().BeEmpty();
        result.CommandResult.Command.Name.Should().Be("build");
        result.GetValue<FileInfo>("input")!.Name.Should().Be("main.spy");
    }

    [Fact]
    public void RequiresInputArgument()
    {
        var result = CliTestHarness.Parse("build");

        result.Errors.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData("exe")]
    [InlineData("library")]
    public void Parses_TypeOption(string type)
    {
        var result = CliTestHarness.Parse($"build main.spy --type {type}");

        result.Errors.Should().BeEmpty();
        result.GetValue<string?>("--type").Should().Be(type);
    }

    [Fact]
    public void TypeOption_ShortAlias()
    {
        var result = CliTestHarness.Parse("build main.spy -t library");

        result.Errors.Should().BeEmpty();
        result.GetValue<string?>("--type").Should().Be("library");
    }

    [Fact]
    public void Parses_OutputOption()
    {
        var result = CliTestHarness.Parse("build main.spy --output bin/app.dll");

        result.Errors.Should().BeEmpty();
        result.GetValue<FileInfo>("--output")!.Name.Should().Be("app.dll");
    }

    [Fact]
    public void Parses_ReferencesProjectReferencesAndModulePaths()
    {
        var result = CliTestHarness.Parse(
            "build main.spy --reference a.dll --project-reference lib.csproj --module-path mods");

        result.Errors.Should().BeEmpty();
        result.GetValue<string[]>("--reference").Should().Contain("a.dll");
        result.GetValue<string[]>("--project-reference").Should().Contain("lib.csproj");
        result.GetValue<string[]>("--module-path").Should().Contain("mods");
    }

    [Fact]
    public void Type_DefaultsToNull_WhenAbsent()
    {
        // Default ("exe") is applied in the action handler, not during parsing.
        var result = CliTestHarness.Parse("build main.spy");

        result.GetValue<string?>("--type").Should().BeNull();
    }

    [Fact]
    public void Rejects_UnknownOption()
    {
        var result = CliTestHarness.Parse("build main.spy --frobnicate");

        result.Errors.Should().NotBeEmpty();
    }
}
