using Sharpy.Compiler.Shared;
using Xunit;

namespace Sharpy.Compiler.Tests.Shared;

public class CSharpKeywordsTests
{
    [Theory]
    [InlineData("int")]
    [InlineData("class")]
    [InlineData("return")]
    [InlineData("while")]
    [InlineData("string")]
    [InlineData("object")]
    [InlineData("namespace")]
    [InlineData("void")]
    [InlineData("null")]
    [InlineData("true")]
    [InlineData("false")]
    public void IsKeyword_ReturnsTrue_ForCSharpKeywords(string keyword)
    {
        Assert.True(CSharpKeywords.IsKeyword(keyword));
    }

    [Theory]
    [InlineData("print")]
    [InlineData("def")]
    [InlineData("self")]
    [InlineData("myVariable")]
    [InlineData("Hello")]
    [InlineData("INT")]
    [InlineData("Class")]
    public void IsKeyword_ReturnsFalse_ForNonKeywords(string name)
    {
        Assert.False(CSharpKeywords.IsKeyword(name));
    }

    [Fact]
    public void IsKeyword_ReturnsFalse_ForEmptyString()
    {
        Assert.False(CSharpKeywords.IsKeyword(""));
    }

    [Fact]
    public void IsKeyword_IsCaseSensitive()
    {
        // C# keywords are lowercase only
        Assert.True(CSharpKeywords.IsKeyword("int"));
        Assert.False(CSharpKeywords.IsKeyword("Int"));
        Assert.False(CSharpKeywords.IsKeyword("INT"));
    }

    [Theory]
    [InlineData("int", "@int")]
    [InlineData("class", "@class")]
    [InlineData("return", "@return")]
    [InlineData("namespace", "@namespace")]
    public void EscapeIfNeeded_PrefixesAt_ForKeywords(string keyword, string expected)
    {
        Assert.Equal(expected, CSharpKeywords.EscapeIfNeeded(keyword));
    }

    [Theory]
    [InlineData("myVar")]
    [InlineData("print")]
    [InlineData("def")]
    [InlineData("self")]
    public void EscapeIfNeeded_ReturnsUnchanged_ForNonKeywords(string name)
    {
        Assert.Equal(name, CSharpKeywords.EscapeIfNeeded(name));
    }

    [Fact]
    public void EscapeIfNeeded_ReturnsUnchanged_ForEmptyString()
    {
        Assert.Equal("", CSharpKeywords.EscapeIfNeeded(""));
    }

    [Fact]
    public void EscapeIfNeeded_ReturnsUnchanged_ForAlreadyEscapedName()
    {
        // A name starting with @ is not in the keyword set, so it passes through
        Assert.Equal("@int", CSharpKeywords.EscapeIfNeeded("@int"));
    }

    [Fact]
    public void All_ContainsAllExpectedKeywords()
    {
        // Verify a representative sample of all keyword categories
        var expectedKeywords = new[]
        {
            // Type keywords
            "bool", "byte", "char", "decimal", "double", "float", "int", "long",
            "object", "sbyte", "short", "string", "uint", "ulong", "ushort",
            // Statement keywords
            "break", "case", "continue", "do", "else", "for", "foreach", "goto",
            "if", "return", "switch", "throw", "try", "while",
            // Access modifiers
            "internal", "private", "protected", "public",
            // Class-related
            "abstract", "class", "const", "enum", "event", "explicit", "extern",
            "implicit", "interface", "namespace", "new", "operator", "override",
            "params", "readonly", "ref", "sealed", "sizeof", "stackalloc",
            "static", "struct", "this", "typeof", "unsafe", "virtual", "volatile",
            // Other
            "as", "base", "catch", "checked", "default", "delegate", "finally",
            "fixed", "in", "is", "lock", "null", "out", "true", "false",
            "unchecked", "using", "void"
        };

        foreach (var keyword in expectedKeywords)
        {
            Assert.True(CSharpKeywords.All.Contains(keyword), $"Expected '{keyword}' to be in keyword set");
        }
    }

    [Fact]
    public void All_HasAtLeast70Keywords()
    {
        // C# has 84+ reserved keywords
        Assert.True(CSharpKeywords.All.Count >= 70);
    }

    [Theory]
    [InlineData("async")]
    [InlineData("await")]
    [InlineData("var")]
    [InlineData("dynamic")]
    [InlineData("yield")]
    [InlineData("nameof")]
    [InlineData("when")]
    [InlineData("get")]
    [InlineData("set")]
    [InlineData("value")]
    [InlineData("partial")]
    [InlineData("where")]
    [InlineData("global")]
    public void IsKeyword_ReturnsFalse_ForContextualKeywords(string contextualKeyword)
    {
        // Contextual keywords are NOT reserved keywords in C# — they only have special
        // meaning in certain contexts, so they should not be escaped
        Assert.False(CSharpKeywords.IsKeyword(contextualKeyword));
    }

    [Theory]
    [InlineData("async", "async")]
    [InlineData("await", "await")]
    [InlineData("var", "var")]
    [InlineData("dynamic", "dynamic")]
    public void EscapeIfNeeded_ReturnsUnchanged_ForContextualKeywords(string contextualKeyword, string expected)
    {
        // Contextual keywords are not escaped because they are not reserved
        Assert.Equal(expected, CSharpKeywords.EscapeIfNeeded(contextualKeyword));
    }

    [Theory]
    [InlineData("myVariable", "myVariable")]
    [InlineData("Print", "Print")]
    [InlineData("hello_world", "hello_world")]
    [InlineData("_private", "_private")]
    [InlineData("x123", "x123")]
    [InlineData("SomeClassName", "SomeClassName")]
    public void EscapeIfNeeded_ReturnsUnchanged_ForRegularIdentifiers(string input, string expected)
    {
        Assert.Equal(expected, CSharpKeywords.EscapeIfNeeded(input));
    }

    [Fact]
    public void All_DoesNotContainContextualKeywords()
    {
        // Verify that contextual keywords are intentionally excluded
        var contextualKeywords = new[] { "async", "await", "var", "dynamic", "yield", "nameof", "when", "get", "set", "value", "partial" };
        foreach (var keyword in contextualKeywords)
        {
            Assert.False(CSharpKeywords.All.Contains(keyword), $"Contextual keyword '{keyword}' should not be in reserved keyword set");
        }
    }

    [Fact]
    public void All_ContainsExactCount()
    {
        // The set should have a specific, stable count of C# reserved keywords
        // This ensures we don't accidentally add or remove keywords
        Assert.Equal(77, CSharpKeywords.All.Count);
    }
}
