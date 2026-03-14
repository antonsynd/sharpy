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
}
