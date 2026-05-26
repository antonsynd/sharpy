using FluentAssertions;
using Xunit;

namespace Sharpy.Cli.Tests.Commands;

public class ReplCommandTests
{
    [Fact]
    public void Repl_IsRegistered_AndParses()
    {
        var result = CliTestHarness.Parse("repl");

        result.Errors.Should().BeEmpty();
        result.CommandResult.Command.Name.Should().Be("repl");
    }

    [Fact]
    public void Repl_RejectsUnknownArgument()
    {
        var result = CliTestHarness.Parse("repl extra-arg");

        result.Errors.Should().NotBeEmpty();
    }
}
