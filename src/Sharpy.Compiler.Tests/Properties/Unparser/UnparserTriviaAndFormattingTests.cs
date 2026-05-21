using FluentAssertions;
using Sharpy.Compiler.Pretty;
using Xunit;
using SharpyLexer = Sharpy.Compiler.Lexer.Lexer;
using SharpyParser = Sharpy.Compiler.Parser.Parser;

namespace Sharpy.Compiler.Tests.Properties.Unparser;

public class UnparserTriviaAndFormattingTests
{
    private static string FormatSource(string source, FormatOptions? formatOptions = null)
    {
        var lexer = new SharpyLexer(source, preserveTrivia: true);
        var tokens = lexer.TokenizeAll();
        var parser = new SharpyParser(tokens);
        var module = parser.ParseModule();
        var options = new UnparseOptions
        {
            PreserveTrivia = true,
            Formatting = formatOptions ?? FormatOptions.Default
        };
        return Pretty.Unparser.Unparse(module, options);
    }

    private static string UnparseWithTrivia(string source)
    {
        var lexer = new SharpyLexer(source, preserveTrivia: true);
        var tokens = lexer.TokenizeAll();
        var parser = new SharpyParser(tokens);
        var module = parser.ParseModule();
        var options = new UnparseOptions { PreserveTrivia = true };
        return Pretty.Unparser.Unparse(module, options);
    }

    #region Trivia preservation

    [Fact]
    public void LeadingComment_PreservedAboveStatement()
    {
        var source = "# my comment\nx = 1\n";
        var result = UnparseWithTrivia(source);
        result.Should().Contain("# my comment\n");
        result.Should().Contain("x = 1\n");
        result.IndexOf("# my comment").Should().BeLessThan(result.IndexOf("x = 1"));
    }

    [Fact]
    public void TrailingComment_PreservedInline()
    {
        var source = "x = 1  # inline\n";
        var result = UnparseWithTrivia(source);
        result.Should().Contain("x = 1  # inline\n");
    }

    [Fact]
    public void MultipleLeadingComments_AllPreserved()
    {
        var source = "# first\n# second\nx = 1\n";
        var result = UnparseWithTrivia(source);
        result.Should().Contain("# first\n");
        result.Should().Contain("# second\n");
    }

    [Fact]
    public void CompoundStatement_ColonComment_Preserved()
    {
        var source = "def foo():  # my func\n    pass\n";
        var result = UnparseWithTrivia(source);
        result.Should().Contain("def foo():  # my func\n");
    }

    [Fact]
    public void MixedLeadingAndTrailing_BothPreserved()
    {
        var source = "# above\nx = 1  # inline\n";
        var result = UnparseWithTrivia(source);
        result.Should().Contain("# above\n");
        result.Should().Contain("x = 1  # inline\n");
    }

    #endregion

    #region Blank line rules

    [Fact]
    public void TwoBlankLines_BetweenTopLevelDefs()
    {
        var source = "def foo():\n    pass\ndef bar():\n    pass\n";
        var result = FormatSource(source);
        result.Should().Contain("pass\n\n\ndef bar():");
    }

    [Fact]
    public void OneBlankLine_AfterImportsBeforeDef()
    {
        var source = "import os\ndef foo():\n    pass\n";
        var result = FormatSource(source);
        result.Should().Contain("import os\n\n\ndef foo():");
    }

    [Fact]
    public void NoBlankLines_BetweenConsecutiveImports()
    {
        var source = "import os\nimport sys\n";
        var result = FormatSource(source);
        result.Should().Be("import os\nimport sys\n");
    }

    [Fact]
    public void OneBlankLine_BetweenClassMembers()
    {
        var source = "class Foo:\n    def a(self):\n        pass\n    def b(self):\n        pass\n";
        var result = FormatSource(source);
        result.Should().Contain("pass\n\n    def b(self):");
    }

    [Fact]
    public void TrailingNewline_Ensured()
    {
        var source = "x = 1";
        var result = FormatSource(source);
        result.Should().EndWith("\n");
    }

    #endregion

    #region Idempotence

    [Fact]
    public void Formatting_IsIdempotent()
    {
        var source = "import os\ndef foo():\n    pass\ndef bar():\n    pass\n";
        var first = FormatSource(source);
        var second = FormatSource(first);
        second.Should().Be(first);
    }

    [Fact]
    public void TriviaFormatting_IsIdempotent()
    {
        var source = "# header\nimport os\ndef foo():  # my func\n    pass\n";
        var first = FormatSource(source);
        var second = FormatSource(first);
        second.Should().Be(first);
    }

    [Fact]
    public void Formatting_EmptyFile_IsIdempotent()
    {
        var source = "";
        var first = FormatSource(source);
        var second = FormatSource(first);
        second.Should().Be(first);
    }

    [Fact]
    public void Formatting_SingleStatement_IsIdempotent()
    {
        var source = "x = 1\n";
        var first = FormatSource(source);
        var second = FormatSource(first);
        second.Should().Be(first);
    }

    [Fact]
    public void Formatting_ClassWithMethods_IsIdempotent()
    {
        var source = "class Foo:\n    def a(self):\n        pass\n    def b(self):\n        pass\n";
        var first = FormatSource(source);
        var second = FormatSource(first);
        second.Should().Be(first);
    }

    [Fact]
    public void Formatting_DecoratedFunctions_IsIdempotent()
    {
        var source = "def foo():\n    pass\n@staticmethod\ndef bar():\n    pass\n";
        var first = FormatSource(source);
        var second = FormatSource(first);
        second.Should().Be(first);
    }

    [Fact]
    public void Formatting_NestedClasses_IsIdempotent()
    {
        var source = "class Outer:\n    class Inner:\n        def method(self):\n            pass\n    def outer_method(self):\n        pass\n";
        var first = FormatSource(source);
        var second = FormatSource(first);
        second.Should().Be(first);
    }

    [Fact]
    public void Formatting_ImportsAndFunctions_IsIdempotent()
    {
        var source = "import os\nimport sys\ndef main():\n    pass\n";
        var first = FormatSource(source);
        var second = FormatSource(first);
        second.Should().Be(first);
    }

    [Fact]
    public void Formatting_CommentsInBody_IsIdempotent()
    {
        var source = "# header\nimport os\ndef foo():  # my func\n    # body comment\n    x = 1  # inline\n    return x\n";
        var first = FormatSource(source);
        var second = FormatSource(first);
        second.Should().Be(first);
    }

    #endregion
}
