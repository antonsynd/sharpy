using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp;
using Sharpy.Compiler.CodeGen;
using Sharpy.Compiler.Diagnostics;
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
        validator.Diagnostics.HasErrors.Should().BeFalse();
        validator.Diagnostics.ErrorCount.Should().Be(0);
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
        validator.Diagnostics.HasErrors.Should().BeTrue();
        validator.Diagnostics.GetErrors().Should().AllSatisfy(d =>
            d.Phase.Should().Be(CompilerPhase.CodeGeneration));
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
        validator.Diagnostics.GetErrors().Should().Contain(d =>
            d.Message.Contains("Abstract method") && d.Message.Contains("cannot have a body"));
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
        validator.Diagnostics.GetWarnings().Should().Contain(d =>
            d.Message.Contains("duplicate member"));
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
        validator.Diagnostics.WarningCount.Should().Be(0);
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
        validator.Diagnostics.HasErrors.Should().BeFalse();
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
        validator.Diagnostics.GetWarnings().Should().Contain(d =>
            d.Message.Contains("var") && d.Message.Contains("without initializer"));
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
        validator.Diagnostics.ErrorCount.Should().BeGreaterThan(0);
        validator.Diagnostics.WarningCount.Should().BeGreaterThan(0);
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
        validator.Diagnostics.GetAll().Should().BeEmpty();
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
        validator.Diagnostics.GetAll().Should().BeEmpty(); // Should be cleared
    }

    [Fact]
    public void Validate_StructuredDiagnostics_HavePhaseAndLocation()
    {
        // Arrange
        var code = @"
public abstract class Base
{
    public abstract void Method()
    {
    }
}";
        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        var validator = new CodeValidator();

        // Act
        validator.Validate(syntaxTree);

        // Assert - verify structured data on diagnostics from validation rules
        var validationErrors = validator.Diagnostics.GetErrors()
            .Where(d => d.Message.Contains("Abstract method"))
            .ToList();
        validationErrors.Should().NotBeEmpty();
        validationErrors.Should().AllSatisfy(d =>
        {
            d.Phase.Should().Be(CompilerPhase.CodeGeneration);
            d.Line.Should().NotBeNull();
            d.Column.Should().NotBeNull();
        });
    }
}
