using FluentAssertions;
using Sharpy.Compiler.CodeGen;
using Sharpy.Compiler.Parser.Ast;
using Xunit;

namespace Sharpy.Compiler.Tests.CodeGen;

public class CodeGenExceptionTests
{
    [Fact]
    public void CodeGenException_WithMessageOnly_CreatesException()
    {
        // Arrange & Act
        var exception = new CodeGenException("Test error");

        // Assert
        exception.Message.Should().Be("Test error");
        exception.Line.Should().BeNull();
        exception.Column.Should().BeNull();
        exception.Node.Should().BeNull();
    }

    [Fact]
    public void CodeGenException_WithLocation_IncludesLocationInToString()
    {
        // Arrange & Act
        var exception = new CodeGenException("Test error", 10, 5);

        // Assert
        exception.Line.Should().Be(10);
        exception.Column.Should().Be(5);
        exception.ToString().Should().Contain("10:5");
        exception.ToString().Should().Contain("Test error");
    }

    [Fact]
    public void CodeGenException_WithNode_IncludesNodeLocation()
    {
        // Arrange
        var node = new IntegerLiteral
        {
            Value = "42",
            LineStart = 15,
            ColumnStart = 10
        };

        // Act
        var exception = new CodeGenException("Invalid integer", node);

        // Assert
        exception.Node.Should().Be(node);
        exception.Line.Should().Be(15);
        exception.Column.Should().Be(10);
        exception.ToString().Should().Contain("15:10");
    }

    [Fact]
    public void CodeGenException_WithInnerException_PreservesInnerException()
    {
        // Arrange
        var innerException = new InvalidOperationException("Inner error");

        // Act
        var exception = new CodeGenException("Outer error", innerException);

        // Assert
        exception.InnerException.Should().Be(innerException);
        exception.Message.Should().Be("Outer error");
    }

    [Fact]
    public void CodeGenException_WithLocationAndInnerException_PreservesBoth()
    {
        // Arrange
        var innerException = new InvalidOperationException("Inner error");

        // Act
        var exception = new CodeGenException("Outer error", 20, 15, innerException);

        // Assert
        exception.Line.Should().Be(20);
        exception.Column.Should().Be(15);
        exception.InnerException.Should().Be(innerException);
        exception.ToString().Should().Contain("20:15");
    }

    [Fact]
    public void CodeGenException_WithoutLocation_DoesNotIncludeLocationInToString()
    {
        // Arrange & Act
        var exception = new CodeGenException("Test error");

        // Assert
        // Should not contain line:column format (just has "Error:" which is expected)
        exception.ToString().Should().NotContain(" at ");
        exception.ToString().Should().Contain("CodeGen Error");
        exception.ToString().Should().Contain("Test error");
    }
}
