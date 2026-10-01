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

    /// <summary>
    /// A token the unparser legitimately RE-SPELLS at the end of a line is not a move (P22b
    /// verify-round R1): a type shorthand is written canonically, so the bracket that closes the
    /// line changes (<c>{int}</c> → <c>set[int]</c>); a partial-application placeholder is written as
    /// the lambda the parser lowered it to, so the last argument changes (<c>_</c> →
    /// <c>__placeholder_0</c>, or the keyword's name for <c>y=_</c>). Each pair is
    /// (source, the unparser's text) with the comment inline after the statement or on the next line.
    /// </summary>
    [Theory]
    [InlineData("x: {int}  # c\n", "x: set[int]  # c\n")]
    [InlineData("d: {str: int}  # c\n", "d: dict[str, int]  # c\n")]
    [InlineData("t: (int, int)  # c\n", "t: tuple[int, int]  # c\n")]
    [InlineData("f: (int) -> int  # c\n", "f: function[int, int]  # c\n")]
    [InlineData("counter: () -> int  # c\n", "counter: function[int]  # c\n")]
    [InlineData("class C:\n    x: {int}  # c\n    y: int\n", "class C:\n    x: set[int]  # c\n    y: int\n")]
    [InlineData("x: {int}\n# c\ny = 1\n", "x: set[int]\n# c\ny = 1\n")]
    [InlineData("f: (int) -> int\n# c\ny = 1\n", "f: function[int, int]\n# c\ny = 1\n")]
    [InlineData("g = add(5, _)  # c\nprint(g(3))\n", "g = lambda __placeholder_0: add(5, __placeholder_0)  # c\nprint(g(3))\n")]
    [InlineData("x = 5 |> f(_)  # c\n", "x = 5 |> (lambda __placeholder_0: f(__placeholder_0))  # c\n")]
    [InlineData("fix_x = f(x=5, y=_)  # c\n", "fix_x = lambda y: f(x=5, y=y)  # c\n")]
    [InlineData("fix_x = f(x=5, y=_)\n# c\nprint(fix_x(7))\n", "fix_x = lambda y: f(x=5, y=y)\n# c\nprint(fix_x(7))\n")]
    // A placeholder expression statement gains a lambda header IN FRONT: the comment before it keeps its neighbour.
    [InlineData("def main():  # c\n    print(_)\n", "def main():  # c\n    lambda __placeholder_0: print(__placeholder_0)\n")]
    // The zero-arity tuple's spellings `()` and `tuple[()]` are written as bare `tuple` (#2169 owns the spelling).
    [InlineData("def f() -> ():  # c\n    return ()\n", "def f() -> tuple:  # c\n    return ()\n")]
    [InlineData("def f() -> tuple[()]:  # c\n    return ()\n", "def f() -> tuple:  # c\n    return ()\n")]
    [InlineData("y = x as? tuple[()]  # c\n", "y = x as? tuple  # c\n")]
    [InlineData("n: ((),) = ((),)  # c\n", "n: tuple[tuple] = ((),)  # c\n")]
    [InlineData("v: ()  # c\n", "v: tuple  # c\n")]
    // An empty pair the unparser DROPS (a field-less union case) is no tuple: brackets are layout.
    [InlineData("union U:\n    case Empty()  # c\n    case K(x: int)\n", "union U:\n    case Empty  # c\n    case K(x: int)\n")]
    public void ATokenRespelledByTheUnparser_DoesNotMoveTheComment(string source, string formatted)
    {
        CommentAnchors.FirstMoved(Anchors(source), Anchors(formatted)).Should().BeNull();
    }

    /// <summary>
    /// Positive controls for the re-spelling keys: a comment that really moves between the SAME
    /// re-spelled tokens is still a move — to the next statement ending in the same shorthand or
    /// placeholder (the other neighbour changes), or from inside a bracket to after it (only the
    /// bracket depth changes: every closing bracket is skipped as a neighbour, so
    /// <c>[1, 2  # c</c> ⏎ <c>]</c> and <c>[1, 2]  # c</c> have the same neighbours).
    /// </summary>
    [Theory]
    [InlineData("x: {int}  # c\ny: {int}\n", "x: set[int]\ny: set[int]  # c\n")]
    [InlineData("g = add(5, _)  # c\nh = add(6, _)\n", "g = lambda __placeholder_0: add(5, __placeholder_0)\nh = lambda __placeholder_0: add(6, __placeholder_0)  # c\n")]
    [InlineData("a = f(y=_)  # c\nb = f(y=_)\n", "a = lambda y: f(y=y)\nb = lambda y: f(y=y)  # c\n")]
    [InlineData("xs = [1, 2  # c\n]\n", "xs = [1, 2]  # c\n")]
    [InlineData("s = {1, 2  # c\n}\n", "s = {1, 2}  # c\n")]
    [InlineData("print(1, 2  # c\n)\n", "print(1, 2)  # c\n")]
    [InlineData("xs = [  # c\n    1]\n", "xs = [1]  # c\n")]
    [InlineData("a = ()  # c\nb = ()\n", "a = ()\nb = ()  # c\n")]
    [InlineData("def f() -> ():  # c\n    return ()\n", "def f() -> tuple:\n    return ()  # c\n")]
    [InlineData("x = 1  # c\nprint(_)\n", "x = 1\nlambda __placeholder_0: print(__placeholder_0)  # c\n")]
    public void ACommentMovedBetweenRespelledTokens_IsStillAMove(string source, string moved)
    {
        CommentAnchors.FirstMoved(Anchors(source), Anchors(moved)).Should().NotBeNull();
    }
}
