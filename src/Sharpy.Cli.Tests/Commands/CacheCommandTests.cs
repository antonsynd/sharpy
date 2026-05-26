using FluentAssertions;
using Xunit;

namespace Sharpy.Cli.Tests.Commands;

public class CacheCommandTests
{
    [Fact]
    public void Clear_ParsesCacheDirOption()
    {
        var result = CliTestHarness.Parse("cache clear --cache-dir /tmp/cache");

        result.Errors.Should().BeEmpty();
        result.CommandResult.Command.Name.Should().Be("clear");
        result.GetValue<string?>("--cache-dir").Should().Be("/tmp/cache");
    }

    [Fact]
    public void Info_ParsesCacheDirOption()
    {
        var result = CliTestHarness.Parse("cache info --cache-dir /tmp/cache");

        result.Errors.Should().BeEmpty();
        result.CommandResult.Command.Name.Should().Be("info");
    }

    [Fact]
    public void UnknownSubcommand_ProducesError()
    {
        var result = CliTestHarness.Parse("cache bogus");

        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public void Info_ReportsCacheInformation()
    {
        using var ws = new TempWorkspace();

        var invocation = CliTestHarness.Invoke($"cache info --cache-dir \"{ws.Root}\"");

        invocation.ExitCode.Should().Be(0);
        invocation.StdOut.Should().Contain("Overload Discovery Cache Information:");
        invocation.StdOut.Should().Contain("Cache Directory:");
        invocation.StdOut.Should().Contain("Total Size:");
    }

    [Fact]
    public void Clear_ReportsSuccess()
    {
        using var ws = new TempWorkspace();

        var invocation = CliTestHarness.Invoke($"cache clear --cache-dir \"{ws.Root}\"");

        invocation.ExitCode.Should().Be(0);
        invocation.StdOut.Should().Contain("cache cleared successfully");
    }
}
