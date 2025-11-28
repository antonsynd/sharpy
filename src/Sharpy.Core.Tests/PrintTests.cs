using Xunit;
using FluentAssertions;
using System.IO;

namespace Sharpy.Core.Tests;

public class Print_Tests
{
    [Fact]
    public void Print_SingleValue_WritesToStdout()
    {
        // Arrange
        var writer = new StringWriter();
        Console.SetOut(writer);

        // Act
        Exports.Print("test");

        // Assert
        var output = writer.ToString();
        output.Should().NotBeEmpty();
    }

    [Fact]
    public void Print_WithCustomEnd_UsesCustomTerminator()
    {
        // Arrange
        var writer = new StringWriter();
        Console.SetOut(writer);

        // Act
        Exports.PrintWithOptions(["Hello"], end: "");
        Exports.PrintWithOptions(["World"], end: "");

        // Assert
        var output = writer.ToString();
        output.Should().Be("HelloWorld");
    }

    [Fact]
    public void Print_WithCustomSep_UsesCustomSeparator()
    {
        // Arrange
        var writer = new StringWriter();
        Console.SetOut(writer);

        // Act
        Exports.PrintWithOptions(["a", "b", "c"], sep: "|");

        // Assert
        var output = writer.ToString();
        output.Should().Contain("a|b|c");
    }

    [Fact]
    public void Print_MultipleArgs_SeparatesWithSpace()
    {
        // Arrange
        var writer = new StringWriter();
        Console.SetOut(writer);

        // Act
        Exports.Print("Hello", "World");

        // Assert
        var output = writer.ToString();
        output.Should().Contain("Hello World");
    }

    [Fact]
    public void Print_NullValue_PrintsNone()
    {
        // Arrange
        var writer = new StringWriter();
        Console.SetOut(writer);

        // Act
        Exports.Print((object?)null);

        // Assert
        var output = writer.ToString();
        output.Should().Contain("None");
    }

    [Fact]
    public void Print_CustomEndNoNewline_DoesNotAddNewline()
    {
        // Arrange
        var writer = new StringWriter();
        Console.SetOut(writer);

        // Act
        Exports.PrintWithOptions(["test"], end: "");

        // Assert
        var output = writer.ToString();
        output.Should().Be("test");
        output.Should().NotEndWith("\n");
    }

    [Fact]
    public void Print_CustomSepAndEnd_UsesBoth()
    {
        // Arrange
        var writer = new StringWriter();
        Console.SetOut(writer);

        // Act
        Exports.PrintWithOptions(["a", "b", "c"], sep: "-", end: "!\n");

        // Assert
        var output = writer.ToString();
        output.Should().Contain("a-b-c!");
    }
}
