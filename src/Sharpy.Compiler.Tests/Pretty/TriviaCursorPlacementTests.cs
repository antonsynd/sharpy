using FluentAssertions;
using Sharpy.Compiler.Lexer;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Pretty;
using Sharpy.Compiler.Text;
using Xunit;

namespace Sharpy.Compiler.Tests.PrettyTests;

/// <summary>
/// The trivia cursor classifies each blank-line run of the token stream (P50 Design Decision 1,
/// R-ET #2239): <see cref="BlankRunPlacement.Between"/> runs are written as one line by the formatter,
/// every other placement as none. One row per placement class, as the first item before its carrier
/// and after a comment (which makes a run <c>Between</c>), each on LF and CRLF text.
/// </summary>
public class TriviaCursorPlacementTests
{
    [Theory]
    // module start: before code, before a leading comment; after a leading comment → between
    [InlineData("\n\nx = 1\n", "ModuleStart")]
    [InlineData("\n# c\nx = 1\n", "ModuleStart")]
    [InlineData("# c\n\nx = 1\n", "Between")]
    // body start: before the first statement, before a leading comment, before a docstring; after a comment → between
    [InlineData("def f():\n\n    x = 1\n", "BodyStart")]
    [InlineData("def f():\n\n    # c\n    x = 1\n", "BodyStart")]
    [InlineData("def f():\n    # c\n\n    x = 1\n", "Between")]
    [InlineData("def f():\n\n    \"\"\"Doc.\"\"\"\n\n    x = 1\n", "BodyStart,Between")]
    [InlineData("class C:\n\n    @staticmethod\n    def make() -> int:\n        return 1\n", "BodyStart")]
    [InlineData("s = match n:\n\n    case 1: 1\n\n    case _: 2\n", "BodyStart,Between")]
    // after a decorator: between two decorators and before the header; a comment after the decorator → between
    [InlineData("@d\n\n@e\n\ndef f():\n    pass\n", "AfterDecorator,AfterDecorator")]
    [InlineData("@d  # c\n\ndef f():\n    pass\n", "AfterDecorator")]
    [InlineData("@d\n# c\n\ndef f():\n    pass\n", "Between")]
    // end of file: after code, after a trailing comment; before an end-of-file comment → between
    [InlineData("x = 1\n\n\n", "EndOfFile")]
    [InlineData("x = 1\n# c\n\n", "EndOfFile")]
    [InlineData("x = 1\n\n# c\n", "Between")]
    [InlineData("def f():\n    x = 1\n\n    # a\n\n    # b\n", "Between,Between")]
    // between: statements, after a block, before a clause header, after a match expression
    [InlineData("x = 1\n\n    \ny = 2\n", "Between")]
    [InlineData("if a:\n    x = 1\n\ny = 2\n", "Between")]
    [InlineData("if a:\n    x = 1\n\nelse:\n    x = 2\n", "Between")]
    [InlineData("def f(n: int) -> str:\n    s = match n:\n        case _: \"x\"\n\n    return s\n", "Between")]
    public void PlacementOf_ClassifiesEveryRun(string source, string expected)
    {
        Placements(source).Should().Equal(expected.Split(','));
        Placements(source.Replace("\n", "\r\n")).Should().Equal(expected.Split(','), "CRLF text has the same runs");
    }

    /// <summary>An AST-built cursor (no token stream) cannot classify a run: every run is <see cref="BlankRunPlacement.Unknown"/>.</summary>
    [Fact]
    public void PlacementOf_AnAstBuiltCursor_IsUnknown()
    {
        var tokens = Lex("x = 1\n\ny = 2\n");
        var run = tokens.SelectMany(t => t.LeadingTrivia ?? Array.Empty<Trivia>()).Single(t => t.Kind == TriviaKind.BlankLines);
        var module = FileCompilationPipeline.Parse(tokens, NullLogger.Instance).Module!;

        TriviaCursor.FromAst(module).PlacementOf(run).Should().Be(BlankRunPlacement.Unknown);
        TriviaCursor.FromTokens(tokens, "x = 1\n\ny = 2\n").PlacementOf(run).Should().Be(BlankRunPlacement.Between);
    }

    /// <summary>
    /// <see cref="TriviaCursor.TakeDetached"/> takes the items before an anchor up to and including the
    /// last blank-line run among them — what a blank line separates from the anchor — and leaves the
    /// comments directly above the anchor pending (owner ruling 2026-10-13, P50).
    /// </summary>
    [Theory]
    [InlineData("x = 1\n\n# a\n\ndef f():\n    pass\n", 5, "blank,# a,blank", "")]
    [InlineData("x = 1\n# a\n\n# b\ndef f():\n    pass\n", 5, "# a,blank", "# b")]
    [InlineData("x = 1\n# a\n# b\ndef f():\n    pass\n", 4, "", "# a,# b")]
    [InlineData("x = 1\n\n\ndef f():\n    pass\n", 4, "blank", "")]
    public void TakeDetached_TakesThroughTheLastBlankRun_LeavesTheAttachedComments(string source, int defLine, string detached, string attached)
    {
        var cursor = TriviaCursor.FromTokens(Lex(source), source);
        cursor.TakeBefore(2);

        Describe(cursor.TakeDetached(defLine)).Should().Be(detached);
        Describe(cursor.TakeBefore(defLine)).Should().Be(attached);

        static string Describe(List<Trivia> items)
            => string.Join(",", items.Select(t => t.Kind == TriviaKind.BlankLines ? "blank" : t.Text));
    }

    private static IReadOnlyList<Token> Lex(string source)
        => FileCompilationPipeline.Lex(new SourceText(source, "<placement>"), NullLogger.Instance, preserveTrivia: true).Tokens;

    private static List<string> Placements(string source)
    {
        var tokens = Lex(source);
        var cursor = TriviaCursor.FromTokens(tokens, source);
        return tokens
            .SelectMany(t => (t.LeadingTrivia ?? Array.Empty<Trivia>()).Concat(t.TrailingTrivia ?? Array.Empty<Trivia>()))
            .Where(t => t.Kind == TriviaKind.BlankLines)
            .OrderBy(t => t.Position)
            .Select(t => cursor.PlacementOf(t).ToString())
            .ToList();
    }
}
