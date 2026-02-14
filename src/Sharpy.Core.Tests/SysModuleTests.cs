using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class SysModule_Tests
{
    // --- Constants ---

    [Fact]
    public void Sys_Stdout_IsOne()
    {
        Sharpy.Sys.Stdout.Should().Be(1u);
    }

    [Fact]
    public void Sys_Stderr_IsTwo()
    {
        Sharpy.Sys.Stderr.Should().Be(2u);
    }

    [Fact]
    public void Sys_Stddev_IsZero()
    {
        Sharpy.Sys.Stddev.Should().Be(0u);
    }

    // --- Argv ---

    [Fact]
    public void Sys_Argv_IsNotNull()
    {
        Sharpy.Sys.Argv.Should().NotBeNull();
    }

    [Fact]
    public void Sys_Argv_ReturnsCopy()
    {
        var argv1 = Sharpy.Sys.Argv;
        var argv2 = Sharpy.Sys.Argv;

        // Should be equal content but different array instances
        argv1.Should().Equal(argv2);
        argv1.Should().NotBeSameAs(argv2);
    }

    // --- Version ---

    [Fact]
    public void Sys_Version_ContainsSharpy()
    {
        Sharpy.Sys.Version.Should().Contain("Sharpy");
    }

    [Fact]
    public void Sys_Version_IsNotEmpty()
    {
        Sharpy.Sys.Version.Should().NotBeNullOrEmpty();
    }

    // --- Platform ---

    [Fact]
    public void Sys_Platform_IsRecognizedValue()
    {
        var platform = Sharpy.Sys.Platform;
        platform.Should().NotBeNullOrEmpty();

        // Should be one of the known platforms
        platform.Should().BeOneOf("win32", "linux", "darwin", "unknown");
    }

    // --- Stdin ---

    [Fact]
    public void Sys_Stdin_IsNotNull()
    {
        Sharpy.Sys.Stdin.Should().NotBeNull();
    }

    // --- Executable ---

    [Fact]
    public void Sys_Executable_IsNotNull()
    {
        Sharpy.Sys.Executable.Should().NotBeNull();
    }

    // --- Path ---

    [Fact]
    public void Sys_Path_IsNotNull()
    {
        Sharpy.Sys.Path.Should().NotBeNull();
    }

    [Fact]
    public void Sys_Path_ContainsCurrentDirectory()
    {
        Sharpy.Sys.Path.Should().NotBeEmpty();
    }

    [Fact]
    public void Sys_Path_ReturnsCopy()
    {
        var path1 = Sharpy.Sys.Path;
        var path2 = Sharpy.Sys.Path;

        path1.Should().Equal(path2);
        path1.Should().NotBeSameAs(path2);
    }
}
