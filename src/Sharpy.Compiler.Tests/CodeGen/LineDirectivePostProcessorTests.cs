using Xunit;
using FluentAssertions;
using Sharpy.Compiler.CodeGen;

namespace Sharpy.Compiler.Tests.CodeGen;

[Collection("Sequential")]
public class LineDirectivePostProcessorTests
{
    [Fact]
    public void Process_CorrectCharOffset_FromIndentation()
    {
        var input = "#line (1, 5) - (1, 20) 1 \"test.spy\"\n        var x = 42;\n";
        var result = LineDirectivePostProcessor.Process(input);

        result.Should().Contain("#line (1, 5) - (1, 20) 8 \"test.spy\"",
            "charOffset should be 8 (the indentation of the next line)");
    }

    [Fact]
    public void Process_MinimumCharOffset_IsOne()
    {
        var input = "#line (1, 5) - (1, 20) 1 \"test.spy\"\nvar x = 42;\n";
        var result = LineDirectivePostProcessor.Process(input);

        result.Should().Contain("#line (1, 5) - (1, 20) 1 \"test.spy\"",
            "charOffset should be 1 when line has no indentation");
    }

    [Fact]
    public void Process_NoDirectives_PassesThrough()
    {
        var input = "var x = 42;\nvar y = 10;\n";
        var result = LineDirectivePostProcessor.Process(input);

        result.Should().Be(input);
    }

    [Fact]
    public void Process_EmptyInput_ReturnsEmpty()
    {
        var result = LineDirectivePostProcessor.Process("");
        result.Should().Be("");
    }

    [Fact]
    public void Process_SingleLine_NoDirective_PassesThrough()
    {
        var input = "var x = 42;";
        var result = LineDirectivePostProcessor.Process(input);
        result.Should().Be(input);
    }

    [Fact]
    public void Process_LineHidden_InsertedForMultiLineConstruct()
    {
        var input =
            "#line (1, 5) - (1, 30) 1 \"test.spy\"\n" +
            "        var items = new List<int>(new int[]\n" +
            "        {\n" +
            "            1,\n" +
            "            2\n" +
            "        });\n" +
            "#line (2, 5) - (2, 20) 1 \"test.spy\"\n" +
            "        var y = 10;\n";

        var result = LineDirectivePostProcessor.Process(input);

        result.Should().Contain("#line hidden",
            "multi-line construct should get #line hidden");
    }

    [Fact]
    public void Process_NoLineHidden_ForSingleLineConstruct()
    {
        var input =
            "#line (1, 5) - (1, 30) 1 \"test.spy\"\n" +
            "        var x = 42;\n" +
            "#line (2, 5) - (2, 20) 1 \"test.spy\"\n" +
            "        var y = 10;\n";

        var result = LineDirectivePostProcessor.Process(input);

        result.Should().NotContain("#line hidden",
            "single-line construct should not get #line hidden");
    }

    [Fact]
    public void Process_LineDefault_EmittedWhenNoFollowingDirective()
    {
        var input =
            "#line (1, 5) - (1, 30) 1 \"test.spy\"\n" +
            "        var items = new List<int>(new int[]\n" +
            "        {\n" +
            "            1\n" +
            "        });\n";

        var result = LineDirectivePostProcessor.Process(input);

        result.Should().Contain("#line hidden");
        result.Should().Contain("#line default",
            "#line default should restore mapping after #line hidden at end of file");
    }

    [Fact]
    public void Process_NoLineDefault_WhenFollowingDirectiveExists()
    {
        var input =
            "#line (1, 5) - (1, 30) 1 \"test.spy\"\n" +
            "        var items = new List<int>(new int[]\n" +
            "        {\n" +
            "            1\n" +
            "        });\n" +
            "#line (2, 5) - (2, 20) 1 \"test.spy\"\n" +
            "        var y = 10;\n";

        var result = LineDirectivePostProcessor.Process(input);

        result.Should().NotContain("#line default",
            "#line default should not appear when a subsequent #line directive restores mapping");
    }

    [Fact]
    public void Process_PreservesBasicLineDirectives()
    {
        var input = "#line 5 \"test.spy\"\n    {\nvar x = 1;\n";
        var result = LineDirectivePostProcessor.Process(input);

        result.Should().Contain("#line 5 \"test.spy\"",
            "basic #line directives should pass through unchanged");
    }

    [Fact]
    public void Process_MultipleDirectives_EachGetsCorrectedCharOffset()
    {
        var input =
            "#line (1, 5) - (1, 20) 1 \"test.spy\"\n" +
            "    var x = 42;\n" +
            "#line (2, 5) - (2, 20) 1 \"test.spy\"\n" +
            "            var y = 10;\n";

        var result = LineDirectivePostProcessor.Process(input);

        result.Should().Contain("#line (1, 5) - (1, 20) 4 \"test.spy\"",
            "first directive charOffset should be 4");
        result.Should().Contain("#line (2, 5) - (2, 20) 12 \"test.spy\"",
            "second directive charOffset should be 12");
    }
}
