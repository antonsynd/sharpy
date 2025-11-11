using FluentAssertions;
using Sharpy.Compiler.CodeGen;
using Xunit;

namespace Sharpy.Compiler.Tests.CodeGen;

[Collection("Sequential")]
public class NameManglerTests
{
    public NameManglerTests()
    {
        // Reset state before each test
        NameMangler.Reset();
    }

    #region PascalCase Conversion Tests

    [Theory]
    [InlineData("my_function", "MyFunction")]
    [InlineData("calculate_total", "CalculateTotal")]
    [InlineData("get_user_name", "GetUserName")]
    [InlineData("simple", "Simple")]
    [InlineData("a_b_c", "ABC")]
    public void ToPascalCase_SnakeCase_ConvertsToPascalCase(string input, string expected)
    {
        // Act
        var result = NameMangler.ToPascalCase(input);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void ToPascalCase_EmptyString_ReturnsEmpty()
    {
        // Act
        var result = NameMangler.ToPascalCase("");

        // Assert
        result.Should().Be("");
    }

    [Theory]
    [InlineData("_private_method", "_PrivateMethod")]
    [InlineData("_internal_var", "_InternalVar")]
    public void ToPascalCase_PrivatePrefix_PreservesUnderscore(string input, string expected)
    {
        // Act
        var result = NameMangler.ToPascalCase(input);

        // Assert
        result.Should().Be(expected);
    }

    #endregion

    #region CamelCase Conversion Tests

    [Theory]
    [InlineData("my_variable", "myVariable")]
    [InlineData("user_name", "userName")]
    [InlineData("item_count", "itemCount")]
    [InlineData("simple", "simple")]
    [InlineData("a_b_c", "aBC")]
    public void ToCamelCase_SnakeCase_ConvertsToCamelCase(string input, string expected)
    {
        // Act
        var result = NameMangler.ToCamelCase(input);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("_private_var", "_privateVar")]
    [InlineData("_internal", "_internal")]
    public void ToCamelCase_PrivatePrefix_PreservesUnderscore(string input, string expected)
    {
        // Act
        var result = NameMangler.ToCamelCase(input);

        // Assert
        result.Should().Be(expected);
    }

    #endregion

    #region Constant Case Tests

    [Theory]
    [InlineData("MAX_SIZE", "MAX_SIZE")]
    [InlineData("PI", "PI")]
    [InlineData("DEFAULT_TIMEOUT", "DEFAULT_TIMEOUT")]
    public void ToConstantCase_CapsSnakeCase_RemainsUnchanged(string input, string expected)
    {
        // Act
        var result = NameMangler.ToConstantCase(input);

        // Assert
        result.Should().Be(expected);
    }

    #endregion

    #region Literal Name Tests

    [Theory]
    [InlineData("`ExactName`", "ExactName")]
    [InlineData("`MyClass`", "MyClass")]
    [InlineData("`some_method`", "some_method")]
    public void ToPascalCase_LiteralName_RemovesBackticksKeepsOriginal(string input, string expected)
    {
        // Act
        var result = NameMangler.ToPascalCase(input);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("`exactVar`", "exactVar")]
    [InlineData("`myVar`", "myVar")]
    public void ToCamelCase_LiteralName_RemovesBackticksKeepsOriginal(string input, string expected)
    {
        // Act
        var result = NameMangler.ToCamelCase(input);

        // Assert
        result.Should().Be(expected);
    }

    #endregion

    #region Dunder Method Tests

    [Theory]
    [InlineData("__init__", "Constructor")]
    [InlineData("__str__", "ToString")]
    [InlineData("__repr__", "ToString")]
    [InlineData("__eq__", "Equals")]
    [InlineData("__add__", "Add")]
    [InlineData("__sub__", "Subtract")]
    [InlineData("__mul__", "Multiply")]
    [InlineData("__div__", "Divide")]
    [InlineData("__getitem__", "GetItem")]
    [InlineData("__setitem__", "SetItem")]
    public void ToPascalCase_DunderMethod_MapsToCorrectName(string dunderName, string expected)
    {
        // Act
        var result = NameMangler.ToPascalCase(dunderName);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void IsDunderMethod_ValidDunder_ReturnsTrue()
    {
        // Arrange
        var name = "__init__";

        // Act
        var result = NameMangler.IsDunderMethod(name);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("__x__")]
    [InlineData("init")]
    [InlineData("_private")]
    [InlineData("__too_short_")]
    public void IsDunderMethod_InvalidDunder_ReturnsFalse(string name)
    {
        // Act
        var result = NameMangler.IsDunderMethod(name);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void GetDunderMethodMapping_KnownDunder_ReturnsMapping()
    {
        // Act
        var result = NameMangler.GetDunderMethodMapping("__init__");

        // Assert
        result.Should().Be("Constructor");
    }

    [Fact]
    public void GetDunderMethodMapping_UnknownDunder_ReturnsNull()
    {
        // Act
        var result = NameMangler.GetDunderMethodMapping("__unknown__");

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region C# Keyword Escaping Tests

    [Theory]
    [InlineData("class", "@Class")]
    [InlineData("if", "@If")]
    [InlineData("while", "@While")]
    [InlineData("return", "@Return")]
    [InlineData("namespace", "@Namespace")]
    [InlineData("static", "@Static")]
    public void ToPascalCase_CSharpKeyword_EscapesWithAt(string keyword, string expected)
    {
        // Act
        var result = NameMangler.ToPascalCase(keyword);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("class", "@class")]
    [InlineData("for", "@for")]
    [InlineData("new", "@new")]
    public void ToCamelCase_CSharpKeyword_EscapesWithAt(string keyword, string expected)
    {
        // Act
        var result = NameMangler.ToCamelCase(keyword);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void ToPascalCase_NotKeyword_NoEscaping()
    {
        // Act
        var result = NameMangler.ToPascalCase("myClass");

        // Assert
        result.Should().Be("Myclass");
        result.Should().NotStartWith("@");
    }

    #endregion

    #region Uniqueness Tests

    [Fact]
    public void ToPascalCase_DuplicateName_AddsCounter()
    {
        // Act
        var first = NameMangler.ToPascalCase("my_method");
        var second = NameMangler.ToPascalCase("my_method");
        var third = NameMangler.ToPascalCase("my_method");

        // Assert
        first.Should().Be("MyMethod");
        second.Should().Be("MyMethod1");
        third.Should().Be("MyMethod2");
    }

    [Fact]
    public void Reset_ClearsUsedNames()
    {
        // Arrange
        var first = NameMangler.ToPascalCase("test");
        NameMangler.Reset();

        // Act
        var afterReset = NameMangler.ToPascalCase("test");

        // Assert
        first.Should().Be("Test");
        afterReset.Should().Be("Test"); // Should be the same since we reset
    }

    #endregion

    #region Context-Aware Transform Tests

    [Theory]
    [InlineData(NameContext.Type, "my_class", "MyClass")]
    [InlineData(NameContext.Method, "get_value", "GetValue")]
    [InlineData(NameContext.Function, "calculate", "Calculate")]
    [InlineData(NameContext.Variable, "user_name", "userName")]
    [InlineData(NameContext.Parameter, "item_count", "itemCount")]
    [InlineData(NameContext.Field, "private_data", "privateData")]
    [InlineData(NameContext.Constant, "MAX_SIZE", "MAX_SIZE")]
    public void Transform_WithContext_TransformsCorrectly(NameContext context, string input, string expected)
    {
        // Arrange
        NameMangler.Reset();

        // Act
        var result = NameMangler.Transform(input, context);

        // Assert
        result.Should().Be(expected);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void ToPascalCase_NullString_ReturnsNull()
    {
        // Act
        var result = NameMangler.ToPascalCase(null!);

        // Assert
        result.Should().BeNullOrEmpty();
    }

    [Fact]
    public void ToCamelCase_SingleUnderscore_ReturnsEmptyOrUnderscore()
    {
        // Act
        var result = NameMangler.ToCamelCase("_");

        // Assert
        // Single underscore should be handled gracefully
        result.Should().NotBeNull();
    }

    [Theory]
    [InlineData("ALREADY_UPPER", "AlreadyUpper")]
    [InlineData("MixedCase", "Mixedcase")]
    public void ToPascalCase_NonSnakeCase_StillConverts(string input, string expected)
    {
        // Act
        var result = NameMangler.ToPascalCase(input);

        // Assert
        result.Should().Be(expected);
    }

    #endregion
}
