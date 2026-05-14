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
}
