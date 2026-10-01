using System.Linq;
using FluentAssertions;
using Sharpy.Compiler.Lexer;
using Sharpy.Compiler.Parser.Ast;
using Xunit;
using LexerNs = Sharpy.Compiler.Lexer;
using ParserNs = Sharpy.Compiler.Parser;

namespace Sharpy.Compiler.Tests.Parser;

/// <summary>
/// P22b Phase 4 Task 1 (#2077), Design Decision 5: every statement records where its HEADER ends —
/// its own header colon for a compound statement, its own end for a simple one — and the clause
/// headers (elif, except, case) and the else/finally lines are recorded at the same hook. The hook
/// is ExpectHeaderColon, called only at header sites, so a dict, slice, lambda or annotation colon
/// never ends a header and never donates its comment as the header-trailing trivia (the first-colon
/// hook it replaced did both).
/// </summary>
public class ParserHeaderEndTests
{
    private static Module Parse(string source)
    {
        var lexer = new LexerNs.Lexer(source, preserveTrivia: true);
        var tokens = lexer.TokenizeAll();
        lexer.Diagnostics.HasErrors.Should().BeFalse("the source must lex: " + source);
        var parser = new ParserNs.Parser(tokens);
        var module = parser.ParseModule();
        parser.Diagnostics.HasErrors.Should().BeFalse(
            "the source must parse: " + source + " — "
            + string.Join(" | ", parser.Diagnostics.GetErrors().Select(d => d.Code + " " + d.Message)));
        return module;
    }

    /// <summary>The exclusive offset just past the first occurrence of the text.</summary>
    private static int OffsetAfter(string source, string text) =>
        source.IndexOf(text, System.StringComparison.Ordinal) + text.Length;

    private static string Comments(System.Collections.Generic.IReadOnlyList<Trivia>? trivia) =>
        trivia == null ? "" : string.Join(" ", trivia.Where(t => t.Kind == TriviaKind.Comment).Select(t => t.Text));

    [Fact]
    public void If_HeaderEndsAtItsColon()
    {
        const string source = "if x:\n    pass\n";
        var stmt = Parse(source).Body.Single();
        stmt.HeaderLineEnd.Should().Be(1);
        stmt.HeaderEndOffset.Should().Be(OffsetAfter(source, "if x:"));
        stmt.LineEnd.Should().Be(2, "control: the statement itself ends with its body");
    }

    [Fact]
    public void Def_WithParametersOverTwoLines_HeaderEndsOnTheColonLine()
    {
        const string source = "def f(a,\n      b):\n    pass\n";
        var stmt = Parse(source).Body.Single();
        stmt.HeaderLineEnd.Should().Be(2);
        stmt.HeaderEndOffset.Should().Be(OffsetAfter(source, "b):"));
    }

    [Fact]
    public void Def_WithAnnotations_HeaderEndsAtTheLastColon_NotTheAnnotationColon()
    {
        const string source = "def f(x: int) -> None:\n    pass\n";
        var stmt = Parse(source).Body.Single();
        stmt.HeaderLineEnd.Should().Be(1);
        stmt.HeaderEndOffset.Should().Be(OffsetAfter(source, "-> None:"));
    }

    [Fact]
    public void For_OverADictLiteral_HeaderEndsAtTheHeaderColon_NotTheDictColon()
    {
        const string source = "for k in {1: 2}:\n    pass\n";
        var stmt = Parse(source).Body.Single();
        stmt.HeaderEndOffset.Should().Be(OffsetAfter(source, "{1: 2}:"));
    }

    [Fact]
    public void SimpleStatement_HeaderEndIsTheStatementEnd()
    {
        const string source = "x = {1: 2}\n";
        var stmt = Parse(source).Body.Single();
        stmt.HeaderLineEnd.Should().Be(stmt.LineEnd);
        stmt.HeaderEndOffset.Should().Be(stmt.Span!.Value.End);
        stmt.HeaderEndOffset.Should().Be(OffsetAfter(source, "{1: 2}"));
    }

    /// <summary>
    /// A simple statement's header ends on its OWN last line — read from the line break that ended
    /// it, never from <c>LineEnd</c>: <c>type</c> aliases and <c>const</c> declarations record
    /// <c>LineEnd</c> after consuming the terminator, as the NEXT statement's line (asserted as the
    /// positive control, so the cell discriminates), which made the trivia cursor read that
    /// statement's leading comment as an inner comment of this header (P22b, #2077).
    /// </summary>
    [Theory]
    [InlineData("type X = int  # c\n# next\ny = 1\n", 1)]
    [InlineData("const LIMIT: int = 200  # c\n# next\ny = 1\n", 1)]
    public void SimpleStatement_WhoseLineEndIsTheNextStatements_HeaderEndsOnItsOwnLine(string source, int line)
    {
        var module = Parse(source);
        var stmt = module.Body[0];
        stmt.LineEnd.Should().BeGreaterThan(line, "control: this statement kind records LineEnd past its terminator");
        stmt.HeaderLineEnd.Should().Be(line);
    }

    [Fact]
    public void SimpleStatement_EndingInAMultiLineString_HeaderEndsOnTheClosingLine()
    {
        const string source = "s = \"\"\"a\nb\"\"\"  # c\ny = 1\n";
        var stmt = Parse(source).Body[0];
        stmt.HeaderLineEnd.Should().Be(2);
    }

    /// <summary>
    /// A simple statement's header end OFFSET is its last code token's end, read from the tokens:
    /// an annotated declaration without a value records its span as the name's alone (asserted as
    /// the control), which cut the formatter's verbatim slice to <c>x</c>.
    /// </summary>
    [Fact]
    public void AnnotatedDeclaration_WithoutAValue_HeaderEndsAfterItsAnnotation()
    {
        const string source = "x: dict[str, int]  # c\ny = 1\n";
        var stmt = Parse(source).Body[0];
        stmt.Span!.Value.End.Should().BeLessThan(OffsetAfter(source, "x: dict[str, int]"), "control: the node's span is the name's alone");
        stmt.HeaderEndOffset.Should().Be(OffsetAfter(source, "x: dict[str, int]"));
        stmt.HeaderLineEnd.Should().Be(1);
    }

    /// <summary>
    /// <c>(c): int = 5</c> starts at its first token, the parenthesis — as an assignment does — so the
    /// formatter's verbatim slice of it keeps the parenthesis; the name keeps its own position.
    /// </summary>
    [Fact]
    public void ParenthesizedAnnotatedTarget_StartsAtTheParenthesis()
    {
        const string source = "(c): int = 5\n";
        var stmt = Parse(source).Body[0].Should().BeOfType<VariableDeclaration>().Subject;
        stmt.Span!.Value.Start.Should().Be(0);
        stmt.ColumnStart.Should().Be(1);
        stmt.NameColumnStart.Should().Be(2, "control: the name keeps its own position");
    }

    [Fact]
    public void ReturnStatement_HeaderEndsOnItsOwnLine()
    {
        const string source = "def f() -> int:\n    return 1  # c\n# next\ndef g():\n    pass\n";
        var ret = Parse(source).Body[0].Should().BeOfType<FunctionDef>().Subject.Body.Single();
        ret.HeaderLineEnd.Should().Be(2);
    }

    [Fact]
    public void Elif_And_Else_HeadersAreRecorded()
    {
        const string source = "if a:\n    pass\nelif b:\n    pass\nelse:\n    pass\n";
        var stmt = Parse(source).Body.Single().Should().BeOfType<IfStatement>().Subject;
        stmt.HeaderLineEnd.Should().Be(1, "a clause colon never ends the owning statement's header");
        var elif = stmt.ElifClauses.Single();
        elif.HeaderLineEnd.Should().Be(3);
        elif.HeaderEndOffset.Should().Be(OffsetAfter(source, "elif b:"));
        stmt.ElseHeaderLine.Should().Be(5);
    }

    [Fact]
    public void LoopElse_HeaderLinesAreRecorded_AndAbsentElseIsZero()
    {
        var loops = Parse("for k in xs:\n    pass\nelse:\n    pass\nwhile c:\n    pass\n").Body;
        loops[0].Should().BeOfType<ForStatement>().Which.ElseHeaderLine.Should().Be(3);
        loops[1].Should().BeOfType<WhileStatement>().Which.ElseHeaderLine.Should().Be(0);
    }

    [Fact]
    public void Except_Else_Finally_HeadersAreRecorded()
    {
        const string source = "try:\n    pass\nexcept E as e:\n    pass\nelse:\n    pass\nfinally:\n    pass\n";
        var stmt = Parse(source).Body.Single().Should().BeOfType<TryStatement>().Subject;
        stmt.HeaderLineEnd.Should().Be(1);
        var handler = stmt.Handlers.Single();
        handler.HeaderLineEnd.Should().Be(3);
        handler.HeaderEndOffset.Should().Be(OffsetAfter(source, "except E as e:"));
        stmt.ElseHeaderLine.Should().Be(5);
        stmt.FinallyHeaderLine.Should().Be(7);
    }

    [Fact]
    public void Case_HeaderIsRecorded_AndNeverEndsTheMatchHeader()
    {
        const string source = "match v:\n    case 1:\n        pass\n";
        var stmt = Parse(source).Body.Single().Should().BeOfType<MatchStatement>().Subject;
        stmt.HeaderLineEnd.Should().Be(1);
        stmt.HeaderEndOffset.Should().Be(OffsetAfter(source, "match v:"));
        stmt.Cases.Single().HeaderLineEnd.Should().Be(2);
        stmt.Cases.Single().HeaderEndOffset.Should().Be(OffsetAfter(source, "case 1:"));
    }

    [Fact]
    public void UnionMethod_IsParsedInItsOwnStatementFrame()
    {
        var union = Parse("union U:\n    case K(x: int)\n\n    def f(self) -> int:\n        return 1\n").Body.Single()
            .Should().BeOfType<UnionDef>().Subject;
        union.HeaderLineEnd.Should().Be(1);
        union.Body.Single().HeaderLineEnd.Should().Be(4);
    }

    // --- the trivia hook moved with the header colon --------------------------------------------

    [Fact]
    public void CaseComment_UnderAMatchWhoseColonHasNone_IsNotTheMatchHeaderComment()
    {
        // At 4ef844961 the first-colon hook fired at the case colon (the first colon carrying
        // trivia), and the formatter wrote the case comment on the match line.
        var stmt = Parse("match 3:\n    case 3:  # c\n        pass\n").Body.Single();
        Comments(stmt.TrailingTrivia).Should().NotContain("# c");
    }

    [Fact]
    public void MatchHeaderComment_IsStillTheMatchHeaderComment()
    {
        // Positive control for the cell above: the header colon's own comment is still captured.
        var stmt = Parse("match 3:  # m\n    case 3:\n        pass\n").Body.Single();
        Comments(stmt.TrailingTrivia).Should().Be("# m");
    }

    [Theory]
    // A later dict entry's colon and a lambda's colon are consumed by Expect(Colon) — the colons
    // the old first-colon hook fired at. (A FIRST dict entry and a parameter annotation consume their
    // colon by Advance and never reached the hook: their cells below are absence checks only.)
    [InlineData("x = {0: 1, 2:  # c\n     3}\n")]
    [InlineData("f = (lambda a:  # c\n     a)\n")]
    [InlineData("x = {1:  # c\n     2}\n")]
    public void InnerColonComment_IsNotHeaderTrivia(string source)
    {
        var stmt = Parse(source).Body.Single();
        Comments(stmt.TrailingTrivia).Should().NotContain("# c");
    }

    [Fact]
    public void AnnotationColonComment_InsideADefHeader_IsNotHeaderTrivia_AndTheHeaderEndsAtTheDefColon()
    {
        const string source = "def f(x:  # c\n      int):\n    pass\n";
        var stmt = Parse(source).Body.Single();
        Comments(stmt.TrailingTrivia).Should().NotContain("# c");
        stmt.HeaderLineEnd.Should().Be(2);
        stmt.HeaderEndOffset.Should().Be(OffsetAfter(source, "int):"));
    }

    [Fact]
    public void AutoPropertyWithObservers_HeaderIsTheDeclarationLine_AndAnObserverCommentIsNotItsComment()
    {
        var prop = Parse("class C:\n    property h: int  # p\n        after_set(o):  # o\n            pass\n").Body.Single()
            .Should().BeOfType<ClassDef>().Subject.Body.Single().Should().BeOfType<PropertyDef>().Subject;
        prop.HeaderLineEnd.Should().Be(2);
        Comments(prop.TrailingTrivia).Should().Be("# p");
    }
}
