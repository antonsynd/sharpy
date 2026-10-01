using FluentAssertions;
using Sharpy.Compiler.Formatting;
using Xunit;
using SLexer = Sharpy.Compiler.Lexer.Lexer;

namespace Sharpy.Compiler.Tests.Formatting;

/// <summary>
/// The comment-placement oracle (O4b, P22b #2077) keys a comment's neighbours by kind and value —
/// except an f-/t-string's delimiters, whose value is the quote spelling: the unparser writes a
/// triple-quoted f-string single-quoted (P22), which moves no comment.
/// </summary>
public class CommentAnchorsTests
{
    private static List<CommentAnchors.Anchor> Anchors(string source)
    {
        var lexer = new SLexer(source, preserveTrivia: true);
        var tokens = lexer.TokenizeAll();
        lexer.Diagnostics.HasErrors.Should().BeFalse(source);
        return CommentAnchors.Of(tokens);
    }

    [Theory]
    [InlineData("x = 1\ns = f\"\"\"{x}\"\"\"  # c\nprint(s)\n", "x = 1\ns = f\"{x}\"  # c\nprint(s)\n")]
    [InlineData("x = 1\ns = t\"\"\"{x}\"\"\"  # c\nprint(s)\n", "x = 1\ns = t\"{x}\"  # c\nprint(s)\n")]
    // A raw string written as the plain string it denotes (`r"Doc."` → `"""Doc."""`, a docstring).
    [InlineData("def f():\n    r\"Doc.\"  # c\n    pass\n", "def f():\n    \"\"\"Doc.\"\"\"  # c\n    pass\n")]
    public void ALiteralRespelledByTheUnparser_DoesNotMoveTheComment(string source, string formatted)
    {
        CommentAnchors.FirstMoved(Anchors(source), Anchors(formatted)).Should().BeNull();
    }

    /// <summary>Positive control: a comment that really moves next to an f-string is still a move.</summary>
    [Fact]
    public void ACommentMovedOffTheFStringLine_IsStillAMove()
    {
        var moved = CommentAnchors.FirstMoved(
            Anchors("x = 1\ns = f\"\"\"{x}\"\"\"  # c\nprint(s)\n"),
            Anchors("x = 1\ns = f\"{x}\"\n# c\nprint(s)\n"));

        moved.Should().NotBeNull();
        moved!.Value.Before.Inline.Should().BeTrue();
        moved.Value.After.Inline.Should().BeFalse();
    }

    /// <summary>
    /// Positive control: an inline comment moved from one f-string line to the next one stays inline
    /// after an f-string delimiter — the kind-only key still sees the move, through the other
    /// neighbour.
    /// </summary>
    [Fact]
    public void AnInlineCommentMovedToTheNextFStringLine_IsStillAMove()
    {
        var moved = CommentAnchors.FirstMoved(
            Anchors("x = 1\na = f\"\"\"{x}\"\"\"  # c\nb = f\"{x}\"\nprint(a, b)\n"),
            Anchors("x = 1\na = f\"{x}\"\nb = f\"{x}\"  # c\nprint(a, b)\n"));

        moved.Should().NotBeNull();
        moved!.Value.Before.After.Should().NotBe(moved.Value.After.After);
    }
}
