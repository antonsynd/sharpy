using FluentAssertions;
using Sharpy.Compiler.Lexer;
using Sharpy.Compiler.Text;
using Xunit;
using SpyLexer = Sharpy.Compiler.Lexer.Lexer;

namespace Sharpy.Compiler.Tests;

/// <summary>
/// Unit tests for <see cref="CommentSpanExtractor"/> — the shared trivia-to-<see cref="CommentSpan"/>
/// extraction used by the analyze paths (#1087). Comment trivia only exists when the lexer runs
/// with <c>preserveTrivia</c>; without it the extraction must be empty.
/// </summary>
public class CommentSpanExtractorTests
{
    private static IReadOnlyList<Token> Lex(string source, bool preserveTrivia)
    {
        var lexer = new SpyLexer(new SourceText(source, "test.spy"), preserveTrivia: preserveTrivia);
        return lexer.TokenizeAll();
    }

    [Fact]
    public void Extract_WithTrivia_SurfacesLeadingAndTrailingComments()
    {
        var tokens = Lex("# leading\ndef f() -> int:\n    return 1  # trailing\n", preserveTrivia: true);

        var spans = CommentSpanExtractor.Extract(tokens);

        spans.Should().HaveCount(2);
        spans.Should().Contain(s => s.Line == 1, "the leading comment is on line 1");
        spans.Should().Contain(s => s.Line == 3, "the trailing comment is on line 3");
        spans.Should().OnlyContain(s => s.EndColumn > s.StartColumn,
            "every comment span must cover at least one character");
    }

    [Fact]
    public void Extract_SpanWidth_MatchesCommentText()
    {
        const string comment = "# hi";
        var tokens = Lex($"{comment}\ndef f() -> int:\n    return 1\n", preserveTrivia: true);

        var spans = CommentSpanExtractor.Extract(tokens);

        var span = spans.Should().ContainSingle().Subject;
        (span.EndColumn - span.StartColumn).Should().Be(comment.Length,
            "the span covers the comment text including the '#'");
    }

    [Fact]
    public void Extract_SameCommentVisibleFromTwoTokens_IsDeduplicated()
    {
        // A comment between tokens can appear as one token's trailing trivia and the next
        // token's leading trivia; the extractor must report it once.
        var tokens = Lex("x: int = 1  # shared\ny: int = 2\n", preserveTrivia: true);

        var spans = CommentSpanExtractor.Extract(tokens);

        spans.Should().OnlyHaveUniqueItems();
        spans.Should().ContainSingle(s => s.Line == 1);
    }

    [Fact]
    public void Extract_WithoutTrivia_ReturnsEmpty()
    {
        var tokens = Lex("# leading\ndef f() -> int:\n    return 1  # trailing\n", preserveTrivia: false);

        CommentSpanExtractor.Extract(tokens).Should().BeEmpty(
            "comment trivia is only recorded when the lexer preserves trivia");
    }
}
