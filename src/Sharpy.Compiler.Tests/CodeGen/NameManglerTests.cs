using FluentAssertions;
using Sharpy.Compiler.Shared;
using Xunit;

namespace Sharpy.Compiler.Tests.CodeGen;

[Collection("Sequential")]
public class NameManglerTests
{
    #region PascalCase Conversion Tests

    [Theory]
    [InlineData("hello_world", "HelloWorld")]
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
    [InlineData("MAX_SIZE", "MaxSize")]
    [InlineData("PI", "Pi")]
    [InlineData("DEFAULT_TIMEOUT", "DefaultTimeout")]
    public void ToConstantCase_CapsSnakeCase_ConvertsToPascalCase(string input, string expected)
    {
        // Act
        var result = NameMangler.ToConstantCase(input);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("HTTP", "Http")]
    public void ToConstantCase_SingleWordUpper_Normalized(string input, string expected)
    {
        var result = NameMangler.ToConstantCase(input);
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

    #region Dunder Method Passthrough Tests

    [Theory]
    [InlineData("__init__", "__init__")]
    [InlineData("__str__", "__str__")]
    [InlineData("__repr__", "__repr__")]
    [InlineData("__eq__", "__eq__")]
    [InlineData("__add__", "__add__")]
    [InlineData("__sub__", "__sub__")]
    [InlineData("__getitem__", "__getitem__")]
    [InlineData("__setitem__", "__setitem__")]
    public void ToPascalCase_DunderMethod_PassesThrough(string dunderName, string expected)
    {
        // After Phase 5, dunders pass through ToPascalCase unchanged.
        // Callers use DunderMapping directly for dunder→C# name resolution.
        var result = NameMangler.ToPascalCase(dunderName);
        result.Should().Be(expected);
    }

    #endregion

    #region C# Keyword Escaping Tests

    [Theory]
    [InlineData("class", "Class")]
    [InlineData("if", "If")]
    [InlineData("while", "While")]
    [InlineData("return", "Return")]
    [InlineData("namespace", "Namespace")]
    [InlineData("static", "Static")]
    public void ToPascalCase_CSharpKeyword_CapitalizesWithoutEscaping(string keyword, string expected)
    {
        // Act
        var result = NameMangler.ToPascalCase(keyword);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("for_each", "ForEach")]
    [InlineData("is_valid", "IsValid")]
    public void ToPascalCase_CSharpKeyword_AsSnakeCase_NoEscape(string name, string expected)
    {
        // Act
        var result = NameMangler.ToPascalCase(name);

        // Assert
        result.Should().Be(expected);
        result.Should().NotStartWith("@");
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
        // camelCase is passed through as-is (no mangling)
        var result = NameMangler.ToPascalCase("myClass");
        result.Should().Be("myClass");
        result.Should().NotStartWith("@");
    }

    #endregion

    #region Uniqueness Tests

    #endregion

    #region Context-Aware Transform Tests

    [Theory]
    [InlineData(NameContext.Type, "MyClass", "MyClass")]  // Types preserve user's casing
    [InlineData(NameContext.Type, "my_class", "my_class")]  // Even snake_case is preserved
    [InlineData(NameContext.Method, "get_value", "GetValue")]
    [InlineData(NameContext.Function, "calculate", "Calculate")]
    [InlineData(NameContext.Variable, "user_name", "userName")]
    [InlineData(NameContext.Parameter, "item_count", "itemCount")]
    [InlineData(NameContext.Field, "private_data", "PrivateData")]
    [InlineData(NameContext.Constant, "MAX_SIZE", "MaxSize")]
    [InlineData(NameContext.EnumMember, "RED", "Red")]
    public void Transform_WithContext_TransformsCorrectly(NameContext context, string input, string expected)
    {
        // Act
        var result = NameMangler.Transform(input, context);

        // Assert
        result.Should().Be(expected);
    }

    #endregion

    #region Trailing Underscore Tests

    [Theory]
    [InlineData("x_", "x_")]
    [InlineData("my_var_", "myVar_")]
    [InlineData("some_name_", "someName_")]
    public void ToCamelCase_TrailingUnderscore_PreservesIt(string input, string expected)
    {
        // Python allows x_ and x as different variables
        var result = NameMangler.ToCamelCase(input);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("x__", "x__")]
    [InlineData("my_var__", "myVar__")]
    public void ToCamelCase_DoubleTrailingUnderscore_PreservesBoth(string input, string expected)
    {
        var result = NameMangler.ToCamelCase(input);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("my_func_", "MyFunc_")]
    [InlineData("get_value_", "GetValue_")]
    public void ToPascalCase_TrailingUnderscore_PreservesIt(string input, string expected)
    {
        var result = NameMangler.ToPascalCase(input);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("my_func__", "MyFunc__")]
    [InlineData("get_value__", "GetValue__")]
    public void ToPascalCase_DoubleTrailingUnderscore_PreservesBoth(string input, string expected)
    {
        var result = NameMangler.ToPascalCase(input);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("_private_", "_private_")]
    [InlineData("_private_var_", "_privateVar_")]
    public void ToCamelCase_PrivatePrefixAndTrailingUnderscore_PreservesBoth(string input, string expected)
    {
        var result = NameMangler.ToCamelCase(input);
        result.Should().Be(expected);
    }

    #endregion

    #region New Form Detection Tests

    [Theory]
    [InlineData("httpClient", "httpClient")]    // CamelCase passthrough
    [InlineData("foo__bar", "foo__bar")]         // Unrecognized passthrough
    [InlineData("__private_field", "__PrivateField")]  // Double-underscore prefix preserved
    [InlineData("__private", "__Private")]       // Double-underscore prefix preserved
    [InlineData("HTTP", "HTTP")]                 // SingleWordUpper preserved in PascalCase context
    public void ToPascalCase_FormDetection_HandlesCorrectly(string input, string expected)
    {
        var result = NameMangler.ToPascalCase(input);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("HttpClient", "httpClient")]     // PascalCase → camelCase
    [InlineData("HTTP", "http")]                 // SingleWordUpper → all lower
    [InlineData("MAX_SIZE", "maxSize")]           // SCREAMING → camelCase
    public void ToCamelCase_FormDetection_HandlesCorrectly(string input, string expected)
    {
        var result = NameMangler.ToCamelCase(input);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("RED", "Red")]
    [InlineData("DARK_BLUE", "DarkBlue")]
    [InlineData("MAX_RETRY_COUNT", "MaxRetryCount")]
    [InlineData("already_lower", "AlreadyLower")]
    [InlineData("`ExactName`", "ExactName")]
    [InlineData("", "")]
    public void ToEnumMemberName_ConvertsCorrectly(string input, string expected)
    {
        var result = NameMangler.ToEnumMemberName(input);
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
    [InlineData("MixedCase", "MixedCase")]  // PascalCase should be preserved
    [InlineData("PascalCase", "PascalCase")]  // PascalCase should be preserved
    public void ToPascalCase_NonSnakeCase_PreservesOrConverts(string input, string expected)
    {
        // Act
        var result = NameMangler.ToPascalCase(input);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("MyClass", "MyClass")]
    [InlineData("UserAccount", "UserAccount")]
    [InlineData("HttpClient", "HttpClient")]
    [InlineData("XMLParser", "XMLParser")]
    public void ToPascalCase_AlreadyPascalCase_PreservesExactCasing(string input, string expected)
    {
        // Act
        var result = NameMangler.ToPascalCase(input);

        // Assert
        result.Should().Be(expected);
    }

    #endregion
}
