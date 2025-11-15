using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp;
using Sharpy.Compiler.CodeGen;
using Xunit;

namespace Sharpy.Compiler.Tests.CodeGen;

public class CodeValidatorTests
{
    [Fact]
    public void Validate_ValidClass_ReturnsTrue()
    {
        // Arrange
        var code = @"
public class Person
{
    public string Name { get; set; }
    public int Age { get; set; }
    
    public void Greet()
    {
        Console.WriteLine($""Hello, I'm {Name}"");
    }
}";
        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        var validator = new CodeValidator();

        // Act
        var result = validator.Validate(syntaxTree);

        // Assert
        result.Should().BeTrue();
        validator.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_SyntaxError_ReturnsFalse()
    {
        // Arrange
        var code = @"
public class Invalid
{
    public void Method(
    {
        // Missing closing paren in signature
    }
}";
        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        var validator = new CodeValidator();

        // Act
        var result = validator.Validate(syntaxTree);

        // Assert
        result.Should().BeFalse();
        validator.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public void Validate_AbstractMethodWithBody_AddsError()
    {
        // Arrange
        var code = @"
public abstract class Base
{
    public abstract void Method()
    {
        // Abstract methods cannot have a body
    }
}";
        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        var validator = new CodeValidator();

        // Act
        var result = validator.Validate(syntaxTree);

        // Assert
        result.Should().BeFalse();
        validator.Errors.Should().Contain(e => e.Contains("Abstract method") && e.Contains("cannot have a body"));
    }

    [Fact]
    public void Validate_DuplicateNonMethodMembers_AddsWarning()
    {
        // Arrange - Use fields instead of methods to avoid method overloading
        var code = @"
public class MyClass
{
    public int Value;
    public int Value;
}";
        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        var validator = new CodeValidator();

        // Act
        validator.Validate(syntaxTree);

        // Assert
        // This will have syntax errors due to duplicate field names, but we also check for custom warnings
        validator.Warnings.Should().Contain(w => w.Contains("duplicate member"));
    }

    [Fact]
    public void Validate_MethodOverloads_DoesNotWarnAboutDuplicates()
    {
        // Arrange - Method overloading is valid in C#
        var code = @"
public class MyClass
{
    public void DoSomething() { }
    public void DoSomething(int x) { }
}";
        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        var validator = new CodeValidator();

        // Act
        var result = validator.Validate(syntaxTree);

        // Assert
        result.Should().BeTrue();
        validator.Warnings.Should().NotContain(w => w.Contains("duplicate member"));
    }

    [Fact]
    public void Validate_InterfaceMethod_AllowsNoBody()
    {
        // Arrange
        var code = @"
public interface IDrawable
{
    void Draw();
}";
        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        var validator = new CodeValidator();

        // Act
        var result = validator.Validate(syntaxTree);

        // Assert
        result.Should().BeTrue();
        validator.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_VarWithoutInitializer_AddsWarning()
    {
        // Arrange
        var code = @"
public class MyClass
{
    public void Method()
    {
        var x;
    }
}";
        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        var validator = new CodeValidator();

        // Act
        validator.Validate(syntaxTree);

        // Assert
        // Note: var without initializer is a syntax error in C#, but our validator
        // detects it during AST traversal and adds a custom warning before Roslyn reports the error.
        // This warning provides additional context specific to the Sharpy → C# compilation process.
        validator.Warnings.Should().Contain(w => w.Contains("var") && w.Contains("without initializer"));
    }

    [Fact]
    public void Validate_MultipleErrorsAndWarnings_CollectsAll()
    {
        // Arrange
        var code = @"
public class MyClass
{
    public int Value;
    public int Value;
    
    public abstract void AbstractWithBody()
    {
        // Should not have body
    }
}";
        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        var validator = new CodeValidator();

        // Act
        var result = validator.Validate(syntaxTree);

        // Assert
        result.Should().BeFalse();
        validator.Errors.Should().NotBeEmpty();
        validator.Warnings.Should().NotBeEmpty();
    }

    [Fact]
    public void Validate_EmptyCode_ReturnsTrue()
    {
        // Arrange
        var code = "";
        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        var validator = new CodeValidator();

        // Act
        var result = validator.Validate(syntaxTree);

        // Assert
        result.Should().BeTrue();
        validator.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_MultipleValidations_ClearsErrorsAndWarnings()
    {
        // Arrange
        var invalidCode = @"public class Invalid { public void Method( { } }";
        var validCode = @"public class Valid { public void Method() { } }";
        var validator = new CodeValidator();

        // Act
        var result1 = validator.Validate(CSharpSyntaxTree.ParseText(invalidCode));
        var result2 = validator.Validate(CSharpSyntaxTree.ParseText(validCode));

        // Assert
        result1.Should().BeFalse();
        result2.Should().BeTrue();
        validator.Errors.Should().BeEmpty(); // Should be cleared
        validator.Warnings.Should().BeEmpty(); // Should be cleared
    }
}
