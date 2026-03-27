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
    [InlineData("__bool__", "__bool__")]
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

    #region Screaming Snake Case Tests

    [Theory]
    [InlineData("MAX_RETRIES", "MaxRetries")]
    [InlineData("HTTP_STATUS_CODE", "HttpStatusCode")]
    [InlineData("API_KEY", "ApiKey")]
    public void ToPascalCase_ScreamingSnakeCase_NormalizesToPascalCase(string input, string expected)
    {
        var result = NameMangler.ToPascalCase(input);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("MAX_RETRIES", "maxRetries")]
    [InlineData("HTTP_STATUS_CODE", "httpStatusCode")]
    public void ToCamelCase_ScreamingSnakeCase_NormalizesToCamelCase(string input, string expected)
    {
        var result = NameMangler.ToCamelCase(input);
        result.Should().Be(expected);
    }

    #endregion

    #region Consecutive Underscore Tests

    [Theory]
    [InlineData("foo__bar", "foo__bar")]       // Unrecognized, passed through
    [InlineData("a__b__c", "a__b__c")]         // Unrecognized, passed through
    public void ToPascalCase_ConsecutiveUnderscores_PassesThroughUnrecognized(string input, string expected)
    {
        var result = NameMangler.ToPascalCase(input);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("foo__bar", "foo__bar")]
    [InlineData("a__b__c", "a__b__c")]
    public void ToCamelCase_ConsecutiveUnderscores_PassesThroughUnrecognized(string input, string expected)
    {
        var result = NameMangler.ToCamelCase(input);
        result.Should().Be(expected);
    }

    #endregion

    #region Names Starting With Digits

    [Theory]
    [InlineData("2d_vector", "2dVector")]       // Starts with digit — snake_case detected, segments capitalized
    [InlineData("3x3_matrix", "3x3Matrix")]
    public void ToPascalCase_StartsWithDigit_CapitalizesSegments(string input, string expected)
    {
        // Digits don't affect upper/lower classification, so these are treated as snake_case
        var result = NameMangler.ToPascalCase(input);
        result.Should().Be(expected);
    }

    #endregion

    #region Double Private Prefix Tests

    [Theory]
    [InlineData("__private_method", "__privateMethod")]
    [InlineData("__internal_data", "__internalData")]
    public void ToCamelCase_DoublePrivatePrefix_PreservesPrefixAndAppliesCamelCase(string input, string expected)
    {
        var result = NameMangler.ToCamelCase(input);
        result.Should().Be(expected);
    }

    #endregion

    #region DunderNameMapping Coverage Tests

    [Theory]
    [InlineData("__init__", "Constructor")]
    [InlineData("__str__", "ToString")]
    [InlineData("__repr__", "ToString")]
    [InlineData("__eq__", "Equals")]
    [InlineData("__ne__", "NotEquals")]
    [InlineData("__hash__", "GetHashCode")]
    [InlineData("__getitem__", "GetItem")]
    [InlineData("__setitem__", "SetItem")]
    [InlineData("__len__", "Count")]
    [InlineData("__contains__", "Contains")]
    [InlineData("__iter__", "GetEnumerator")]
    [InlineData("__reversed__", "GetReverseEnumerator")]
    public void DunderNameMapping_KnownDunders_MapToExpectedCSharpNames(string dunder, string expected)
    {
        DunderNameMapping.GetCSharpName(dunder).Should().Be(expected);
    }

    [Theory]
    [InlineData("__add__")]
    [InlineData("__sub__")]
    [InlineData("__mul__")]
    [InlineData("__neg__")]
    [InlineData("__bool__")]
    public void DunderNameMapping_OperatorDunders_ReturnNull(string dunder)
    {
        // Operator dunders are not in the name mapping — they use inlined codegen paths
        DunderNameMapping.GetCSharpName(dunder).Should().BeNull();
    }

    [Theory]
    [InlineData("__init__")]
    [InlineData("__str__")]
    [InlineData("__eq__")]
    [InlineData("__len__")]
    public void DunderNameMapping_HasMapping_ReturnsTrueForMappedDunders(string dunder)
    {
        DunderNameMapping.HasMapping(dunder).Should().BeTrue();
    }

    [Theory]
    [InlineData("__add__")]
    [InlineData("__unknown_method__")]
    [InlineData("not_a_dunder")]
    public void DunderNameMapping_HasMapping_ReturnsFalseForUnmappedNames(string name)
    {
        DunderNameMapping.HasMapping(name).Should().BeFalse();
    }

    [Theory]
    [InlineData("__init__", "Constructor")]
    [InlineData("__str__", "ToString")]
    public void DunderNameMapping_ResolveCSharpName_ReturnsMappingForKnownDunders(string dunder, string expected)
    {
        DunderNameMapping.ResolveCSharpName(dunder).Should().Be(expected);
    }

    [Theory]
    [InlineData("hello")]
    [InlineData("_private")]
    [InlineData("__x__")]     // Too short (length 5, needs > 5)
    public void DunderNameMapping_ResolveCSharpName_ReturnsNullForNonDunders(string name)
    {
        DunderNameMapping.ResolveCSharpName(name).Should().BeNull();
    }

    #endregion

    #region ToNamespacePart Tests

    [Fact]
    public void ToNamespacePart_EmptyString_ReturnsEmpty()
    {
        NameMangler.ToNamespacePart("").Should().Be("");
    }

    [Fact]
    public void ToNamespacePart_Null_ReturnsNull()
    {
        NameMangler.ToNamespacePart(null!).Should().BeNull();
    }

    [Theory]
    [InlineData("`MyName`", "MyName")]
    [InlineData("`SomeLib`", "SomeLib")]
    public void ToNamespacePart_BacktickEscaped_StripsBackticks(string input, string expected)
    {
        NameMangler.ToNamespacePart(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("``")]
    [InlineData("`")]
    public void ToNamespacePart_BacktickEdgeCase_ReturnsAsIs(string input)
    {
        NameMangler.ToNamespacePart(input).Should().Be(input);
    }

    [Theory]
    [InlineData("io", "IO")]
    [InlineData("api", "API")]
    [InlineData("json", "JSON")]
    [InlineData("IO", "IO")]
    [InlineData("Api", "API")]
    [InlineData("http", "HTTP")]
    [InlineData("xml", "XML")]
    [InlineData("csv", "CSV")]
    [InlineData("guid", "GUID")]
    public void ToNamespacePart_UppercaseAcronyms_ReturnsAllCaps(string input, string expected)
    {
        NameMangler.ToNamespacePart(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("my_module", "MyModule")]
    [InlineData("sub_module", "SubModule")]
    [InlineData("my_cool_lib", "MyCoolLib")]
    public void ToNamespacePart_SnakeCase_ConvertsToPascalCase(string input, string expected)
    {
        NameMangler.ToNamespacePart(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("system", "System")]
    [InlineData("module", "Module")]
    [InlineData("animal", "Animal")]
    public void ToNamespacePart_SingleWord_CapitalizesFirst(string input, string expected)
    {
        NameMangler.ToNamespacePart(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("3d_graphics", "_3dGraphics")]
    [InlineData("2d", "_2d")]
    public void ToNamespacePart_DigitPrefix_PrefixesWithUnderscore(string input, string expected)
    {
        NameMangler.ToNamespacePart(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("my-module", "MyModule")]
    [InlineData("some.lib", "SomeLib")]
    public void ToNamespacePart_InvalidChars_ReplacesWithUnderscoreAndSplits(string input, string expected)
    {
        NameMangler.ToNamespacePart(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("___")]
    [InlineData("_")]
    [InlineData("__")]
    public void ToNamespacePart_OnlyUnderscores_ReturnsUnderscore(string input)
    {
        NameMangler.ToNamespacePart(input).Should().Be("_");
    }

    [Theory]
    [InlineData("http_server", "HttpServer")]
    [InlineData("json_parser", "JsonParser")]
    public void ToNamespacePart_AcronymInCompound_DoesNotMatchAcronym(string input, string expected)
    {
        // "http_server" is not an exact acronym match, so it uses normal PascalCase
        NameMangler.ToNamespacePart(input).Should().Be(expected);
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
