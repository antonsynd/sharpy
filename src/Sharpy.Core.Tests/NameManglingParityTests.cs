using Xunit;

namespace Sharpy.Core.Tests
{
    /// <summary>
    /// The forward member rule moved into Sharpy.Core (#2040, R-CG) — pinned against a LITERAL table,
    /// not against the compiler, so the move is checked byte for byte. Core's rule returns the
    /// UNESCAPED metadata name (<c>class</c> stays <c>class</c>); keyword escaping is the compiler
    /// wrapper's concern (<c>NameMangler</c>), whose own <c>NameManglerTests</c> still pin
    /// <c>@class</c>. Rows cover every <see cref="NameForm"/>, both private prefixes, trailing
    /// underscores, digits and the degenerate underscore-only names.
    /// </summary>
    public class NameManglingParityTests
    {
        [Theory]
        [InlineData("hello_world", "HelloWorld")]
        [InlineData("a_b_c", "ABC")]
        [InlineData("simple", "Simple")]
        [InlineData("n_items", "NItems")]
        [InlineData("MAX_RETRIES", "MaxRetries")]
        [InlineData("API_KEY", "ApiKey")]
        [InlineData("HttpClient", "HttpClient")]
        [InlineData("httpClient", "httpClient")]
        [InlineData("HTTP", "HTTP")]
        [InlineData("__init__", "__init__")]
        [InlineData("foo__bar", "foo__bar")]
        [InlineData("Foo_bar", "Foo_bar")]
        [InlineData("_private_method", "_PrivateMethod")]
        [InlineData("__private_field", "__PrivateField")]
        [InlineData("my_func_", "MyFunc_")]
        [InlineData("get_value__", "GetValue__")]
        [InlineData("2d_vector", "2dVector")]
        [InlineData("class", "Class")]
        [InlineData("_", "_")]
        [InlineData("__", "____")]
        [InlineData("", "")]
        public void ToPascalCase(string input, string expected)
            => Assert.Equal(expected, NameMangling.ToPascalCase(input));

        [Theory]
        [InlineData("my_variable", "myVariable")]
        [InlineData("a_b_c", "aBC")]
        [InlineData("simple", "simple")]
        [InlineData("MAX_SIZE", "maxSize")]
        [InlineData("HttpClient", "httpClient")]
        [InlineData("HTTP", "http")]
        [InlineData("httpClient", "httpClient")]
        [InlineData("__add__", "__add__")]
        [InlineData("foo__bar", "foo__bar")]
        [InlineData("_private_var", "_privateVar")]
        [InlineData("__internal_data", "__internalData")]
        [InlineData("my_var_", "myVar_")]
        [InlineData("_private_", "_private_")]
        [InlineData("class", "class")]
        [InlineData("", "")]
        public void ToCamelCase(string input, string expected)
            => Assert.Equal(expected, NameMangling.ToCamelCase(input));

        [Theory]
        [InlineData("MAX_SIZE", "MAX_SIZE")]
        [InlineData("_PRIVATE_CONST", "_PRIVATE_CONST")]
        [InlineData("PI", "PI")]
        [InlineData("V2", "V2")]
        [InlineData("max_size", "MaxSize")]
        [InlineData("single", "Single")]
        [InlineData("HttpClient", "HttpClient")]
        [InlineData("", "")]
        public void ToConstantCase(string input, string expected)
            => Assert.Equal(expected, NameMangling.ToConstantCase(input));

        [Theory]
        [InlineData("RED", "RED")]
        [InlineData("DARK_BLUE", "DARK_BLUE")]
        [InlineData("HTTP_200", "HTTP_200")]
        [InlineData("already_lower", "AlreadyLower")]
        [InlineData("dark__blue", "DarkBlue")]
        [InlineData("mixedCase", "Mixedcase")]
        [InlineData("", "")]
        public void ToEnumMemberName(string input, string expected)
            => Assert.Equal(expected, NameMangling.ToEnumMemberName(input));

        [Theory]
        [InlineData("get_user_name", NameForm.SnakeCase)]
        [InlineData("HttpClient", NameForm.PascalCase)]
        [InlineData("httpClient", NameForm.CamelCase)]
        [InlineData("MAX_SIZE", NameForm.ScreamingSnakeCase)]
        [InlineData("hello", NameForm.SingleWordLower)]
        [InlineData("HTTP", NameForm.SingleWordUpper)]
        [InlineData("__init__", NameForm.Dunder)]
        [InlineData("foo__bar", NameForm.Unrecognized)]
        [InlineData("Foo_bar", NameForm.Unrecognized)]
        public void NameFormDetector_Detect(string input, NameForm expected)
            => Assert.Equal(expected, NameFormDetector.Detect(input));
    }
}
