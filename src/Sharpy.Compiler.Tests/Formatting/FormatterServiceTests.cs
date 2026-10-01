using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Formatting;
using Xunit;
using SLexer = Sharpy.Compiler.Lexer.Lexer;
using SParser = Sharpy.Compiler.Parser.Parser;

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

    // Sharpy indentation is exactly 4 spaces per level, no tabs (indentation.md). Until 2026-09-30
    // FormatOptions.IndentSize/UseTabs wrote 2-space or tab indentation — a file that does not lex
    // (SPY0013/SPY0012); owner ruling 2026-09-30 (P22b): the formatter always writes 4 spaces and has
    // no indentation option.
    [Fact]
    public void Format_AlwaysWritesFourSpaceIndentation()
    {
        var source = "def foo():\n        if True:\n                pass\n";
        var result = FormatterService.Format(source);
        result.Diagnostics.Should().BeEmpty();
        result.FormattedText.Should().Be("def foo():\n    if True:\n        pass\n");
    }

    [Fact]
    public void FormatOptions_HasNoIndentationKnob()
    {
        // A re-added IndentSize/UseTabs would be a knob whose only effect is an unlexable file.
        typeof(FormatOptions).GetProperty("IndentSize").Should().BeNull();
        typeof(FormatOptions).GetProperty("UseTabs").Should().BeNull();
        typeof(FormatOptions).GetProperty("LineEnding").Should().NotBeNull("positive control: the reflection sees the record's properties");
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

    [Fact]
    public void Format_NestedTypeDefinitions_GetBlankLines()
    {
        var source = "class Outer:\n    class Inner1:\n        pass\n    class Inner2:\n        pass\n";
        var result = FormatterService.Format(source);
        result.FormattedText.Should().Contain("pass\n\n    class Inner2:");
    }

    // -----------------------------------------------------------------------
    // The meaning-preservation net (P22b Phase 2, SPY0912): Format re-reads its own output and
    // refuses it — source returned unchanged — when it would not re-parse, change the AST, drop a
    // comment or drop a backtick escape. Each Net_* cell hand-damages ONE invariant; the positive
    // controls show an undamaged (source, Format(source)) pair passes.
    // -----------------------------------------------------------------------

    private static Sharpy.Compiler.Parser.Ast.Module ParseWithTrivia(string source)
    {
        var tokens = new SLexer(source, preserveTrivia: true).TokenizeAll();
        var parser = new SParser(tokens);
        var module = parser.ParseModule();
        parser.Diagnostics.HasErrors.Should().BeFalse("the net's source must parse");
        return module;
    }

    private static CompilerDiagnostic? Net(string source, string output) =>
        FormatterService.CheckMeaningPreserved(source, ParseWithTrivia(source), output);

    private static void AssertDeclined(CompilerDiagnostic? diagnostic, string what)
    {
        diagnostic.Should().NotBeNull();
        diagnostic!.Code.Should().Be(DiagnosticCodes.Infrastructure.FormatterDeclined);
        diagnostic.IsError.Should().BeTrue();
        diagnostic.Message.Should().StartWith("formatting declined: the output would ");
        diagnostic.Message.Should().Contain(what);
        diagnostic.Message.Should().EndWith("; the file was left unchanged");
    }

    [Fact]
    public void Net_RefusesAnOutputThatDoesNotReparse()
    {
        var source = "def main():\n    x = 1\n    print(x)\n";
        var output = "def main():\n    x = = 1\n    print(x)\n";

        AssertDeclined(Net(source, output), "would not re-parse (first error at formatted line 2");
    }

    [Fact]
    public void Net_RefusesAnOutputThatChangesTheStructure()
    {
        // Same comments, same escapes, parses — only the AST differs.
        var source = "x = 1\ny = 2  # two\n";
        var output = "x = 1\ny = 3  # two\n";

        var diagnostic = Net(source, output);

        AssertDeclined(diagnostic, "would change the program's structure (first differing statement at line 2)");
        diagnostic!.Line.Should().Be(2);
    }

    [Fact]
    public void Net_RefusesAnOutputMissingAComment()
    {
        // #2077's live cell: the bracket comment is gone and nothing else changed — the structural
        // comparer ignores trivia, so only the comment-sequence check sees it.
        var source = "def main():\n    xs = [1,  # inner\n        2]\n    print(xs)\n";
        var output = "def main():\n    xs = [1, 2]\n    print(xs)\n";

        var diagnostic = Net(source, output);

        AssertDeclined(diagnostic, "would drop comment '# inner' at line 2");
        diagnostic!.Line.Should().Be(2);
    }

    [Fact]
    public void Net_RefusesAnOutputAddingOrReorderingAComment()
    {
        AssertDeclined(Net("x = 1  # a\n", "x = 1  # a\n# b\n"), "would add comment '# b'");
        AssertDeclined(Net("# a\n# b\nx = 1\n", "# b\n# a\nx = 1\n"), "would move comment '# a' at line 1 out of source order");
    }

    [Fact]
    public void Net_RefusesAnOutputDroppingAnEscape()
    {
        // #2157's cell on a NON-keyword name, so the damaged output still parses and (with no escape
        // flag on KeywordArgument at 4ef844961) is structurally equal — only the token-based escape
        // check sees it. The message is asserted, so a structural refusal cannot stand in for it.
        var source = "def f(`x`: int) -> int:\n    return `x`\n\n\ndef main():\n    print(f(`x`=7))\n";
        var output = "def f(`x`: int) -> int:\n    return `x`\n\n\ndef main():\n    print(f(x=7))\n";

        var diagnostic = Net(source, output);

        AssertDeclined(diagnostic, "would drop the backtick escape on 'x' at line 6");
        diagnostic!.Line.Should().Be(6);
    }

    [Fact]
    public void Net_ComparesCommentTextWithoutTheTrailingWhitespaceTheFormatterStrips()
    {
        Net("x = 1  # c   \n", "x = 1  # c\n").Should().BeNull();
    }

    [Theory]
    [InlineData("def foo():\n    pass\ndef bar():  # c\n    return 1\n")]
    [InlineData("# header\nx   =   1  # inline\ndef f(`class`: int) -> int:\n        return `class`\n")]
    [InlineData("def main():\n        s = \"\"\"p  \nq\"\"\"  # note\n        print(s)\n")]
    public void Net_PositiveControl_AnUndamagedFormatPasses(string source)
    {
        var result = FormatterService.Format(source);

        result.Diagnostics.Should().BeEmpty();
        result.HasChanges.Should().BeTrue("the control must exercise a real rewrite, not the identity");
        Net(source, result.FormattedText).Should().BeNull();
    }

    [Fact]
    public void Format_DeclinesAnOutputDroppingABracketComment_LeavesSourceUnchanged()
    {
        // #2077 live cell end to end: at 4ef844961 Format returned `xs = [1, 2]` (comment gone).
        var source = "def main():\n    xs = [1,  # inner\n        2]\n    print(xs)\n";

        var result = FormatterService.Format(source, filePath: "p2077.spy");

        result.FormattedText.Should().Be(source);
        result.HasChanges.Should().BeFalse();
        result.Diagnostics.Should().ContainSingle();
        AssertDeclined(result.Diagnostics[0], "would drop comment '# inner' at line 2");
        result.Diagnostics[0].FilePath.Should().Be("p2077.spy");
    }

    [Fact]
    public void Format_KeepsAKeywordArgumentEscape_TheFormerRefusalCellFormatsClean()
    {
        // #2157 live cell end to end. At 4ef844961 Format returned `f(class=7)`, which does not parse;
        // with the SPY0912 net (Phase 2) it declined with "would drop the backtick escape on 'class'
        // at line 6". Phase 3 (KeywordArgument.IsNameBacktickEscaped written through WriteName) flips
        // the cell: the escape is kept and Format reports nothing.
        var source = "def f(`class`: int) -> int:\n    return `class`\n\n\ndef main():\n    print(f(`class`=7))\n";

        var result = FormatterService.Format(source);

        result.Diagnostics.Should().BeEmpty();
        result.FormattedText.Should().Contain("print(f(`class`=7))");
        Net(source, result.FormattedText).Should().BeNull();
    }
}
