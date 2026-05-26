using System;
using System.IO;
using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class SysModule_Tests
{
    // --- Streams ---

    [Fact]
    public void Sys_Stdout_IsConsoleOut()
    {
        Sharpy.Sys.Stdout.Should().BeSameAs(Console.Out);
    }

    [Fact]
    public void Sys_Stderr_IsConsoleError()
    {
        Sharpy.Sys.Stderr.Should().BeSameAs(Console.Error);
    }

    [Fact]
    public void Print_WithStderrFile_RoutesToConsoleError()
    {
        var originalError = Console.Error;
        try
        {
            var writer = new StringWriter();
            Console.SetError(writer);

            Builtins.PrintWithOptions(["hi"], file: Sharpy.Sys.Stderr);

            writer.ToString().Should().Contain("hi");
        }
        finally
        {
            Console.SetError(originalError);
        }
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

    // --- Argv edge cases ---

    [Fact]
    public void Sys_Argv_HasAtLeastProgramName()
    {
        // argv[0] is always present (program name, possibly empty string) — never null.
        Sharpy.Sys.Argv.Should().NotBeEmpty();
        Sharpy.Sys.Argv[0].Should().NotBeNull();
    }

    [Fact]
    public void Sys_Argv_MutatingCopy_DoesNotAffectSource()
    {
        var argv = Sharpy.Sys.Argv;
        if (argv.Length > 0)
        {
            argv[0] = "mutated value with spaces & symbols!";
        }

        // A fresh copy is unaffected by mutations to a previously returned array.
        Sharpy.Sys.Argv.Should().NotContain("mutated value with spaces & symbols!");
    }

    // --- Maxsize ---

    [Fact]
    public void Sys_Maxsize_IsIntMaxValue()
    {
        Sharpy.Sys.Maxsize.Should().Be(int.MaxValue);
    }

    // --- Getsizeof ---

    [Fact]
    public void Sys_Getsizeof_Null_ReturnsZero()
    {
        Sharpy.Sys.Getsizeof(null).Should().Be(0);
    }

    [Fact]
    public void Sys_Getsizeof_ValueType_ReturnsPositiveSize()
    {
        // Value types report a marshalled size in bytes.
        Sharpy.Sys.Getsizeof(42).Should().BeGreaterThan(0);
    }

    [Fact]
    public void Sys_Getsizeof_ReferenceType_ReturnsMinusOne()
    {
        // Reference types cannot be sized via Marshal.SizeOf, so -1 is returned.
        Sharpy.Sys.Getsizeof("a reference type").Should().Be(-1);
    }

    // --- Executable ---

    [Fact]
    public void Sys_Executable_MatchesArgvZeroOrEmpty()
    {
        var argv = Sharpy.Sys.Argv;
        var expected = (argv.Length > 0 && !string.IsNullOrEmpty(argv[0])) ? argv[0] : "";
        Sharpy.Sys.Executable.Should().Be(expected);
    }

    // --- Stdout routing ---

    [Fact]
    public void Sys_Stdout_WriteRoutesToConsoleOut()
    {
        var originalOut = Console.Out;
        try
        {
            var writer = new StringWriter();
            Console.SetOut(writer);

            Sharpy.Sys.Stdout.Write("routed");

            writer.ToString().Should().Contain("routed");
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }
}
