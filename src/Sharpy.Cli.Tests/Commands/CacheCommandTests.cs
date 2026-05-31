using FluentAssertions;
using Sharpy.Cli.Commands;
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
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = CacheCommand.ShowCacheInfo(ws.Root, stdout, stderr);

        exitCode.Should().Be(0);
        stdout.ToString().Should().Contain("Overload Discovery Cache Information:");
        stdout.ToString().Should().Contain("Cache Directory:");
        stdout.ToString().Should().Contain("Total Size:");
    }

    [Fact]
    public void Clear_ReportsSuccess()
    {
        using var ws = new TempWorkspace();
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = CacheCommand.ClearCache(ws.Root, stdout, stderr);

        exitCode.Should().Be(0);
        stdout.ToString().Should().Contain("cache cleared successfully");
    }

    [Fact]
    public void Info_CacheDirIsAFile_ReturnsExitCode1()
    {
        using var ws = new TempWorkspace();
        var filePath = ws.WriteFile("not-a-dir", "x");
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = CacheCommand.ShowCacheInfo(filePath, stdout, stderr);

        exitCode.Should().Be(1);
        stderr.ToString().Should().Contain("Error retrieving cache info");
    }

    [Fact]
    public void Clear_CacheDirIsAFile_ReturnsExitCode1()
    {
        using var ws = new TempWorkspace();
        var filePath = ws.WriteFile("not-a-dir", "x");
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = CacheCommand.ClearCache(filePath, stdout, stderr);

        exitCode.Should().Be(1);
        stderr.ToString().Should().Contain("Error clearing cache");
    }
}
