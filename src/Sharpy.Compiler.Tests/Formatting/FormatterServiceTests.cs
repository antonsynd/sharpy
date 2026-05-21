using FluentAssertions;
using Sharpy.Compiler.Formatting;
using Xunit;

namespace Sharpy.Compiler.Tests.Formatting;

public class FormatterServiceTests
{
    [Fact]
    public void Format_SimpleAssignment_ProducesCorrectOutput()
    {
        var result = FormatterService.Format("x = 1\n");
        result.FormattedText.Should().Contain("x = 1");
        result.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Format_AlreadyFormatted_HasChangesIsFalse()
    {
        var source = "x = 1\n";
        var result = FormatterService.Format(source);
        result.HasChanges.Should().BeFalse();
        result.FormattedText.Should().Be(source);
    }

    [Fact]
    public void Format_PreservesComments()
    {
        var source = "# header\nx = 1  # inline\n";
        var result = FormatterService.Format(source);
        result.FormattedText.Should().Contain("# header");
        result.FormattedText.Should().Contain("# inline");
    }

    [Fact]
    public void Format_InsertsBlankLinesBetweenTopLevelDefs()
    {
        var source = "def foo():\n    pass\ndef bar():\n    pass\n";
        var result = FormatterService.Format(source);
        result.FormattedText.Should().Contain("pass\n\n\ndef bar():");
        result.HasChanges.Should().BeTrue();
    }

    [Fact]
    public void Format_SyntaxErrors_ReturnsOriginalSource()
    {
        var source = "def foo(\n";
        var result = FormatterService.Format(source);
        result.FormattedText.Should().Be(source);
        result.HasChanges.Should().BeFalse();
        result.Diagnostics.Should().NotBeEmpty();
    }

    [Fact]
    public void Format_CustomIndentSize_TwoSpaces()
    {
        var source = "def foo():\n    pass\n";
        var options = new FormatOptions { IndentSize = 2 };
        var result = FormatterService.Format(source, options);
        result.FormattedText.Should().Contain("  pass");
        result.FormattedText.Should().NotContain("    pass");
    }

    [Fact]
    public void Format_TabMode()
    {
        var source = "def foo():\n    pass\n";
        var options = new FormatOptions { UseTabs = true };
        var result = FormatterService.Format(source, options);
        result.FormattedText.Should().Contain("\tpass");
    }

    [Fact]
    public void Format_Idempotent()
    {
        var source = "def foo():\n    pass\ndef bar():\n    pass\n";
        var first = FormatterService.Format(source);
        var second = FormatterService.Format(first.FormattedText);
        second.FormattedText.Should().Be(first.FormattedText);
        second.HasChanges.Should().BeFalse();
    }

    [Fact]
    public void Format_EmptyInput()
    {
        var result = FormatterService.Format("");
        result.FormattedText.Should().BeEmpty();
        result.HasChanges.Should().BeFalse();
    }

    [Fact]
    public void Format_MultiLineString_ContentPreserved()
    {
        var source = "x = \"\"\"hello\n  world\"\"\"\n";
        var result = FormatterService.Format(source);
        result.FormattedText.Should().Contain("hello\n  world");
    }

    [Fact]
    public void Format_StripsTrailingWhitespace()
    {
        var source = "x = 1   \ny = 2  \n";
        var result = FormatterService.Format(source);
        result.FormattedText.Should().Be("x = 1\ny = 2\n");
    }

    [Fact]
    public void Format_StripsTrailingWhitespace_OnBlankLines()
    {
        var source = "x = 1\n   \ny = 2\n";
        var result = FormatterService.Format(source);
        var lines = result.FormattedText.Split('\n');
        lines.Should().AllSatisfy(line => line.Should().Be(line.TrimEnd(' ', '\t')));
    }

    [Fact]
    public void Format_StripsTrailingTabs()
    {
        var source = "x = 1\t\t\n";
        var result = FormatterService.Format(source);
        result.FormattedText.Should().Be("x = 1\n");
    }

    [Fact]
    public void StripTrailingWhitespace_PreservesTrailingNewline()
    {
        var result = FormatterService.StripTrailingWhitespace("x = 1  \ny = 2  ", FormatOptions.Default);
        result.Should().EndWith("\n");
    }

    [Fact]
    public void StripTrailingWhitespace_NoTrailingNewline_WhenDisabled()
    {
        var options = new FormatOptions { TrailingNewline = false };
        var result = FormatterService.StripTrailingWhitespace("x = 1  ", options);
        result.Should().Be("x = 1");
    }

    [Fact]
    public void Format_MultipleDecorators_BeforeFunction()
    {
        var source = "def foo():\n    pass\n@staticmethod\n@abstractmethod\ndef bar():\n    pass\n";
        var result = FormatterService.Format(source);
        result.FormattedText.Should().Contain("pass\n\n\n@staticmethod\n@abstractmethod\ndef bar():");
    }

    [Fact]
    public void Format_NestedClassWithMethods()
    {
        var source = "class Outer:\n    class Inner:\n        def a(self):\n            pass\n        def b(self):\n            pass\n";
        var result = FormatterService.Format(source);
        result.FormattedText.Should().Contain("pass\n\n        def b(self):");
    }

    [Fact]
    public void Format_ImportThenAssignmentThenFunction()
    {
        var source = "import os\nx = 1\ndef foo():\n    pass\n";
        var result = FormatterService.Format(source);
        result.FormattedText.Should().Contain("x = 1\n\n\ndef foo():");
    }

    [Fact]
    public void Format_FunctionWithPassOnly()
    {
        var source = "def foo():\n    pass\n";
        var result = FormatterService.Format(source);
        result.FormattedText.Should().Contain("def foo():\n    pass\n");
    }

    [Fact]
    public void Format_BodyCommentPreserved()
    {
        var source = "def foo():\n    # body comment\n    x = 1\n";
        var result = FormatterService.Format(source);
        result.FormattedText.Should().Contain("# body comment\n    x = 1");
    }
}
