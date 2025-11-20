using FluentAssertions;
using LexerNs = Sharpy.Compiler.Lexer;
using TokenType = Sharpy.Compiler.Lexer.TokenType;
using Xunit;

namespace Sharpy.Compiler.Tests.Lexer;

/// <summary>
/// Comprehensive tests for line and column position tracking in the lexer.
/// Tests cover a wide range of Sharpy constructs and edge cases.
/// </summary>
public class LexerPositionTests
{
    private static List<LexerNs.Token> Tokenize(string source)
    {
        var lexer = new LexerNs.Lexer(source);
        return lexer.TokenizeAll();
    }

    #region Basic Position Tracking

    [Fact]
    public void Position_SingleToken_StartsAtLine1Column1()
    {
        var tokens = Tokenize("x");
        var token = tokens[0];
        token.Line.Should().Be(1);
        token.Column.Should().Be(1);
    }

    [Fact]
    public void Position_MultipleTokensOnSameLine_CorrectColumns()
    {
        var tokens = Tokenize("x = 42");

        tokens[0].Should().Match<LexerNs.Token>(t => t.Type == TokenType.Identifier && t.Line == 1 && t.Column == 1);
        tokens[1].Should().Match<LexerNs.Token>(t => t.Type == TokenType.Assign && t.Line == 1 && t.Column == 3);
        tokens[2].Should().Match<LexerNs.Token>(t => t.Type == TokenType.Integer && t.Line == 1 && t.Column == 5);
    }

    [Fact]
    public void Position_MultilineCode_CorrectLineNumbers()
    {
        var source = @"x = 1
y = 2
z = 3";
        var tokens = Tokenize(source);

        var xToken = tokens.First(t => t.Value == "x");
        var yToken = tokens.First(t => t.Value == "y");
        var zToken = tokens.First(t => t.Value == "z");

        xToken.Line.Should().Be(1);
        yToken.Line.Should().Be(2);
        zToken.Line.Should().Be(3);
    }

    [Fact]
    public void Position_TokensAfterNewline_ResetToColumn1()
    {
        var source = @"x = 1
y = 2";
        var tokens = Tokenize(source);

        var yToken = tokens.First(t => t.Value == "y");
        yToken.Line.Should().Be(2);
        yToken.Column.Should().Be(1);
    }

    #endregion

    #region Keywords and Identifiers

    [Fact]
    public void Position_KeywordsTrackedCorrectly()
    {
        var source = "def class if else while for";
        var tokens = Tokenize(source);

        tokens[0].Should().Match<LexerNs.Token>(t => t.Type == TokenType.Def && t.Column == 1);
        tokens[1].Should().Match<LexerNs.Token>(t => t.Type == TokenType.Class && t.Column == 5);
        tokens[2].Should().Match<LexerNs.Token>(t => t.Type == TokenType.If && t.Column == 11);
        tokens[3].Should().Match<LexerNs.Token>(t => t.Type == TokenType.Else && t.Column == 14);
        tokens[4].Should().Match<LexerNs.Token>(t => t.Type == TokenType.While && t.Column == 19);
        tokens[5].Should().Match<LexerNs.Token>(t => t.Type == TokenType.For && t.Column == 25);
    }

    [Fact]
    public void Position_LongIdentifier_StartsAtCorrectPosition()
    {
        var source = "    very_long_identifier_name"; // 4 spaces
        var tokens = Tokenize(source);

        var token = tokens[1]; // tokens[0] is INDENT
        token.Line.Should().Be(1);
        token.Column.Should().Be(5); // After 4 spaces
    }

    #endregion

    #region Operators

    [Fact]
    public void Position_SingleCharOperators_TrackedCorrectly()
    {
        var source = "a+b-c*d/e%f";
        var tokens = Tokenize(source);

        tokens.Should().Contain(t => t.Type == TokenType.Plus && t.Column == 2);
        tokens.Should().Contain(t => t.Type == TokenType.Minus && t.Column == 4);
        tokens.Should().Contain(t => t.Type == TokenType.Star && t.Column == 6);
        tokens.Should().Contain(t => t.Type == TokenType.Slash && t.Column == 8);
        tokens.Should().Contain(t => t.Type == TokenType.Percent && t.Column == 10);
    }

    [Fact]
    public void Position_TwoCharOperators_TrackedCorrectly()
    {
        var source = "x == y != z <= a >= b";
        var tokens = Tokenize(source);

        tokens.Should().Contain(t => t.Type == TokenType.Equal && t.Column == 3);
        tokens.Should().Contain(t => t.Type == TokenType.NotEqual && t.Column == 8);
        tokens.Should().Contain(t => t.Type == TokenType.LessEqual && t.Column == 13);
        tokens.Should().Contain(t => t.Type == TokenType.GreaterEqual && t.Column == 18);
    }

    [Fact]
    public void Position_ThreeCharOperators_TrackedCorrectly()
    {
        var source = "x <<= 1\ny >>= 2\nz //= 3";
        var tokens = Tokenize(source);

        tokens.Should().Contain(t => t.Type == TokenType.LeftShiftAssign && t.Line == 1 && t.Column == 3);
        tokens.Should().Contain(t => t.Type == TokenType.RightShiftAssign && t.Line == 2 && t.Column == 3);
        tokens.Should().Contain(t => t.Type == TokenType.DoubleSlashAssign && t.Line == 3 && t.Column == 3);
    }

    [Fact]
    public void Position_SpecialOperators_TrackedCorrectly()
    {
        var source = "a ?. b ?? c ...";
        var tokens = Tokenize(source);

        tokens.Should().Contain(t => t.Type == TokenType.NullConditional && t.Column == 3);
        tokens.Should().Contain(t => t.Type == TokenType.NullCoalesce && t.Column == 8);
        tokens.Should().Contain(t => t.Type == TokenType.Ellipsis && t.Column == 13);
    }

    #endregion

    #region Literals

    [Fact]
    public void Position_IntegerLiterals_TrackedCorrectly()
    {
        var source = "0 42 1000000 0xFF 0b1010 0o777";
        var tokens = Tokenize(source);

        tokens[0].Should().Match<LexerNs.Token>(t => t.Type == TokenType.Integer && t.Column == 1);
        tokens[1].Should().Match<LexerNs.Token>(t => t.Type == TokenType.Integer && t.Column == 3);
        tokens[2].Should().Match<LexerNs.Token>(t => t.Type == TokenType.Integer && t.Column == 6);
        tokens[3].Should().Match<LexerNs.Token>(t => t.Type == TokenType.Integer && t.Column == 14); // After "0 42 1000000 "
        tokens[4].Should().Match<LexerNs.Token>(t => t.Type == TokenType.Integer && t.Column == 19); // After "0xFF "
        tokens[5].Should().Match<LexerNs.Token>(t => t.Type == TokenType.Integer && t.Column == 26); // After "0b1010 "
    }

    [Fact]
    public void Position_FloatLiterals_TrackedCorrectly()
    {
        var source = "3.14 0.5 1.23e10 2.5e-5";
        var tokens = Tokenize(source);

        tokens[0].Should().Match<LexerNs.Token>(t => t.Type == TokenType.Float && t.Column == 1);
        tokens[1].Should().Match<LexerNs.Token>(t => t.Type == TokenType.Float && t.Column == 6);
        tokens[2].Should().Match<LexerNs.Token>(t => t.Type == TokenType.Float && t.Column == 10);
        tokens[3].Should().Match<LexerNs.Token>(t => t.Type == TokenType.Float && t.Column == 18);
    }

    [Fact]
    public void Position_StringLiterals_TrackedCorrectly()
    {
        var source = "\"hello\" 'world'";
        var tokens = Tokenize(source);

        tokens[0].Should().Match<LexerNs.Token>(t => t.Type == TokenType.String && t.Column == 1);
        tokens[1].Should().Match<LexerNs.Token>(t => t.Type == TokenType.String && t.Column == 9);
    }

    [Fact]
    public void Position_MultilineString_StartsAtCorrectPosition()
    {
        var source = @"x = """"""
This is a
multiline
string""""""";
        var tokens = Tokenize(source);

        var stringToken = tokens.First(t => t.Type == TokenType.String);
        stringToken.Line.Should().Be(1);
        stringToken.Column.Should().Be(5);
    }

    [Fact]
    public void Position_FString_TrackedCorrectly()
    {
        var source = "x = f\"value: {y}\"";
        var tokens = Tokenize(source);

        var fstringToken = tokens.First(t => t.Type == TokenType.FString);
        fstringToken.Line.Should().Be(1);
        fstringToken.Column.Should().Be(5);
    }

    [Fact]
    public void Position_RawString_TrackedCorrectly()
    {
        var source = @"path = r""C:\Users\test""";
        var tokens = Tokenize(source);

        var rawStringToken = tokens.First(t => t.Type == TokenType.RawString);
        rawStringToken.Line.Should().Be(1);
        rawStringToken.Column.Should().Be(8);
    }

    #endregion

    #region Delimiters

    [Fact]
    public void Position_Parentheses_TrackedCorrectly()
    {
        var source = "foo(a, b)";
        var tokens = Tokenize(source);

        tokens.Should().Contain(t => t.Type == TokenType.LeftParen && t.Column == 4);
        tokens.Should().Contain(t => t.Type == TokenType.RightParen && t.Column == 9);
    }

    [Fact]
    public void Position_Brackets_TrackedCorrectly()
    {
        var source = "arr[0]";
        var tokens = Tokenize(source);

        tokens.Should().Contain(t => t.Type == TokenType.LeftBracket && t.Column == 4);
        tokens.Should().Contain(t => t.Type == TokenType.RightBracket && t.Column == 6);
    }

    [Fact]
    public void Position_Braces_TrackedCorrectly()
    {
        var source = "{\"key\": value}";
        var tokens = Tokenize(source);

        tokens.Should().Contain(t => t.Type == TokenType.LeftBrace && t.Column == 1);
        tokens.Should().Contain(t => t.Type == TokenType.RightBrace && t.Column == 14);
    }

    [Fact]
    public void Position_NestedDelimiters_TrackedCorrectly()
    {
        var source = "[[{()}]]";
        var tokens = Tokenize(source);

        tokens[0].Should().Match<LexerNs.Token>(t => t.Type == TokenType.LeftBracket && t.Column == 1);
        tokens[1].Should().Match<LexerNs.Token>(t => t.Type == TokenType.LeftBracket && t.Column == 2);
        tokens[2].Should().Match<LexerNs.Token>(t => t.Type == TokenType.LeftBrace && t.Column == 3);
        tokens[3].Should().Match<LexerNs.Token>(t => t.Type == TokenType.LeftParen && t.Column == 4);
        tokens[4].Should().Match<LexerNs.Token>(t => t.Type == TokenType.RightParen && t.Column == 5);
        tokens[5].Should().Match<LexerNs.Token>(t => t.Type == TokenType.RightBrace && t.Column == 6);
        tokens[6].Should().Match<LexerNs.Token>(t => t.Type == TokenType.RightBracket && t.Column == 7);
        tokens[7].Should().Match<LexerNs.Token>(t => t.Type == TokenType.RightBracket && t.Column == 8);
    }

    #endregion

    #region Indentation

    [Fact]
    public void Position_IndentToken_TrackedCorrectly()
    {
        var source = @"if True:
    pass";
        var tokens = Tokenize(source);

        var indentToken = tokens.First(t => t.Type == TokenType.Indent);
        indentToken.Line.Should().Be(2);
        indentToken.Column.Should().Be(1);
    }

    [Fact]
    public void Position_DedentToken_TrackedCorrectly()
    {
        var source = @"if True:
    pass
x = 1";
        var tokens = Tokenize(source);

        var dedentToken = tokens.First(t => t.Type == TokenType.Dedent);
        dedentToken.Line.Should().Be(3);
        dedentToken.Column.Should().Be(1);
    }

    [Fact]
    public void Position_NestedIndentation_TrackedCorrectly()
    {
        var source = @"if True:
    if False:
        pass";
        var tokens = Tokenize(source);

        var indentTokens = tokens.Where(t => t.Type == TokenType.Indent).ToList();
        indentTokens.Should().HaveCount(2);
        indentTokens[0].Line.Should().Be(2);
        indentTokens[1].Line.Should().Be(3);
    }

    #endregion

    #region Complex Constructs

    [Fact]
    public void Position_FunctionDefinition_AllTokensTracked()
    {
        var source = @"def greet(name: str) -> str:
    return f""Hello, {name}""";
        var tokens = Tokenize(source);

        tokens.Should().Contain(t => t.Type == TokenType.Def && t.Line == 1 && t.Column == 1);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "greet" && t.Line == 1 && t.Column == 5);
        tokens.Should().Contain(t => t.Type == TokenType.LeftParen && t.Line == 1 && t.Column == 10);
        tokens.Should().Contain(t => t.Type == TokenType.Arrow && t.Line == 1 && t.Column == 22);
        tokens.Should().Contain(t => t.Type == TokenType.Return && t.Line == 2 && t.Column == 5);
    }

    [Fact]
    public void Position_ClassDefinition_AllTokensTracked()
    {
        var source = @"class Person:
    name: str
    age: int";
        var tokens = Tokenize(source);

        tokens.Should().Contain(t => t.Type == TokenType.Class && t.Line == 1 && t.Column == 1);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "Person" && t.Line == 1 && t.Column == 7);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "name" && t.Line == 2 && t.Column == 5);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "age" && t.Line == 3 && t.Column == 5);
    }

    [Fact]
    public void Position_IfElifElse_AllTokensTracked()
    {
        var source = @"if x > 0:
    print(""positive"")
elif x < 0:
    print(""negative"")
else:
    print(""zero"")";
        var tokens = Tokenize(source);

        tokens.Should().Contain(t => t.Type == TokenType.If && t.Line == 1 && t.Column == 1);
        tokens.Should().Contain(t => t.Type == TokenType.Elif && t.Line == 3 && t.Column == 1);
        tokens.Should().Contain(t => t.Type == TokenType.Else && t.Line == 5 && t.Column == 1);
    }

    [Fact]
    public void Position_ForLoop_AllTokensTracked()
    {
        var source = @"for i in range(10):
    print(i)";
        var tokens = Tokenize(source);

        tokens.Should().Contain(t => t.Type == TokenType.For && t.Line == 1 && t.Column == 1);
        tokens.Should().Contain(t => t.Type == TokenType.In && t.Line == 1 && t.Column == 7);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "range" && t.Line == 1 && t.Column == 10);
    }

    [Fact]
    public void Position_WhileLoop_AllTokensTracked()
    {
        var source = @"while x > 0:
    x = x - 1";
        var tokens = Tokenize(source);

        tokens.Should().Contain(t => t.Type == TokenType.While && t.Line == 1 && t.Column == 1);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "x" && t.Line == 2 && t.Column == 5);
    }

    [Fact]
    public void Position_TryExceptFinally_AllTokensTracked()
    {
        var source = @"try:
    risky()
except Exception as e:
    handle(e)
finally:
    cleanup()";
        var tokens = Tokenize(source);

        tokens.Should().Contain(t => t.Type == TokenType.Try && t.Line == 1 && t.Column == 1);
        tokens.Should().Contain(t => t.Type == TokenType.Except && t.Line == 3 && t.Column == 1);
        tokens.Should().Contain(t => t.Type == TokenType.As && t.Line == 3 && t.Column == 18);
        tokens.Should().Contain(t => t.Type == TokenType.Finally && t.Line == 5 && t.Column == 1);
    }

    [Fact]
    public void Position_ImportStatements_AllTokensTracked()
    {
        var source = @"import math
from os import path
from sys import argv as arguments";
        var tokens = Tokenize(source);

        tokens.Should().Contain(t => t.Type == TokenType.Import && t.Line == 1 && t.Column == 1);
        tokens.Should().Contain(t => t.Type == TokenType.From && t.Line == 2 && t.Column == 1);
        tokens.Should().Contain(t => t.Type == TokenType.Import && t.Line == 2 && t.Column == 9); // After "from os "
        tokens.Should().Contain(t => t.Type == TokenType.As && t.Line == 3 && t.Column == 22); // After "from sys import argv "
    }

    [Fact]
    public void Position_LambdaExpression_AllTokensTracked()
    {
        var source = "f = lambda x, y: x + y";
        var tokens = Tokenize(source);

        tokens.Should().Contain(t => t.Type == TokenType.Lambda && t.Line == 1 && t.Column == 5);
        tokens.Should().Contain(t => t.Type == TokenType.Colon && t.Line == 1 && t.Column == 16);
    }

    [Fact]
    public void Position_Decorator_AllTokensTracked()
    {
        var source = @"@staticmethod
def foo():
    pass";
        var tokens = Tokenize(source);

        tokens.Should().Contain(t => t.Type == TokenType.At && t.Line == 1 && t.Column == 1);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "staticmethod" && t.Line == 1 && t.Column == 2);
    }

    [Fact]
    public void Position_ListComprehension_AllTokensTracked()
    {
        var source = "[x for x in range(10)]";
        var tokens = Tokenize(source);

        tokens[0].Should().Match<LexerNs.Token>(t => t.Type == TokenType.LeftBracket && t.Column == 1);
        tokens.Should().Contain(t => t.Type == TokenType.For && t.Column == 4);
        tokens.Should().Contain(t => t.Type == TokenType.In && t.Column == 10);
        tokens.Should().Contain(t => t.Type == TokenType.RightBracket && t.Column == 22);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Position_EmptyLines_DoNotAffectTracking()
    {
        var source = @"x = 1


y = 2";
        var tokens = Tokenize(source);

        var yToken = tokens.First(t => t.Value == "y");
        yToken.Line.Should().Be(4);
        yToken.Column.Should().Be(1);
    }

    [Fact]
    public void Position_WindowsLineEndings_TrackedCorrectly()
    {
        var source = "x = 1\r\ny = 2\r\nz = 3";
        var tokens = Tokenize(source);

        var yToken = tokens.First(t => t.Value == "y");
        var zToken = tokens.First(t => t.Value == "z");

        yToken.Line.Should().Be(2);
        zToken.Line.Should().Be(3);
    }

    [Fact]
    public void Position_MixedLineEndings_TrackedCorrectly()
    {
        var source = "x = 1\ny = 2\r\nz = 3";
        var tokens = Tokenize(source);

        tokens.First(t => t.Value == "x").Line.Should().Be(1);
        tokens.First(t => t.Value == "y").Line.Should().Be(2);
        tokens.First(t => t.Value == "z").Line.Should().Be(3);
    }

    [Fact]
    public void Position_UnicodeIdentifiers_TrackedCorrectly()
    {
        var source = "café = résumé";
        var tokens = Tokenize(source);

        tokens[0].Should().Match<LexerNs.Token>(t => t.Value == "café" && t.Column == 1);
        tokens[1].Should().Match<LexerNs.Token>(t => t.Type == TokenType.Assign && t.Column == 6);
        tokens[2].Should().Match<LexerNs.Token>(t => t.Value == "résumé" && t.Column == 8);
    }

    [Fact]
    public void Position_VeryLongLine_TrackedCorrectly()
    {
        var identifier = new string('x', 100);
        var source = $"{identifier} = 42";
        var tokens = Tokenize(source);

        tokens[0].Column.Should().Be(1);
        tokens[1].Column.Should().Be(102); // After 100-char identifier + space
        tokens[2].Column.Should().Be(104); // After '= '
    }

    [Fact]
    public void Position_LineContinuation_TrackedCorrectly()
    {
        var source = @"x = 1 + \
    2 + \
    3";
        var tokens = Tokenize(source);

        // After line continuation, position continues on next line
        tokens.Should().Contain(t => t.Type == TokenType.Integer && t.Value == "2" && t.Line == 2);
        tokens.Should().Contain(t => t.Type == TokenType.Integer && t.Value == "3" && t.Line == 3);
    }

    [Fact]
    public void Position_ImplicitLineContinuation_InParens_TrackedCorrectly()
    {
        var source = @"result = (
    1 +
    2
)";
        var tokens = Tokenize(source);

        tokens.Should().Contain(t => t.Type == TokenType.Integer && t.Value == "1" && t.Line == 2);
        tokens.Should().Contain(t => t.Type == TokenType.Integer && t.Value == "2" && t.Line == 3);
    }

    [Fact]
    public void Position_Comment_DoesNotProduceToken()
    {
        var source = @"x = 1  # This is a comment
y = 2";
        var tokens = Tokenize(source);

        // Comments should not produce tokens
        tokens.Should().NotContain(t => t.Type == TokenType.Comment);

        var yToken = tokens.First(t => t.Value == "y");
        yToken.Line.Should().Be(2);
    }

    [Fact]
    public void Position_LiteralNames_TrackedCorrectly()
    {
        var source = "`literal name` = `class`";
        var tokens = Tokenize(source);

        tokens[0].Should().Match<LexerNs.Token>(t => t.Type == TokenType.Identifier && t.Column == 1);
        tokens[1].Should().Match<LexerNs.Token>(t => t.Type == TokenType.Assign && t.Column == 16);
        tokens[2].Should().Match<LexerNs.Token>(t => t.Type == TokenType.Identifier && t.Column == 18);
    }

    [Fact]
    public void Position_NullableType_TrackedCorrectly()
    {
        var source = "x: int?";
        var tokens = Tokenize(source);

        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "int" && t.Column == 4);
        tokens.Should().Contain(t => t.Type == TokenType.Question && t.Column == 7);
    }

    [Fact]
    public void Position_GenericType_TrackedCorrectly()
    {
        var source = "x: list[int]";
        var tokens = Tokenize(source);

        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "list" && t.Column == 4);
        tokens.Should().Contain(t => t.Type == TokenType.LeftBracket && t.Column == 8);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "int" && t.Column == 9);
        tokens.Should().Contain(t => t.Type == TokenType.RightBracket && t.Column == 12);
    }

    [Fact]
    public void Position_NestedGenericType_TrackedCorrectly()
    {
        var source = "x: dict[str, list[int]]";
        var tokens = Tokenize(source);

        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "dict" && t.Column == 4);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "str" && t.Column == 9);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "list" && t.Column == 14);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "int" && t.Column == 19);
    }

    [Fact]
    public void Position_ChainedMemberAccess_TrackedCorrectly()
    {
        var source = "obj.method().property.field";
        var tokens = Tokenize(source);

        tokens[0].Should().Match<LexerNs.Token>(t => t.Value == "obj" && t.Column == 1);
        tokens[1].Should().Match<LexerNs.Token>(t => t.Type == TokenType.Dot && t.Column == 4);
        tokens[2].Should().Match<LexerNs.Token>(t => t.Value == "method" && t.Column == 5);
        tokens[3].Should().Match<LexerNs.Token>(t => t.Type == TokenType.LeftParen && t.Column == 11);
        tokens[4].Should().Match<LexerNs.Token>(t => t.Type == TokenType.RightParen && t.Column == 12);
        tokens[5].Should().Match<LexerNs.Token>(t => t.Type == TokenType.Dot && t.Column == 13);
        tokens[6].Should().Match<LexerNs.Token>(t => t.Value == "property" && t.Column == 14);
        tokens[7].Should().Match<LexerNs.Token>(t => t.Type == TokenType.Dot && t.Column == 22);
        tokens[8].Should().Match<LexerNs.Token>(t => t.Value == "field" && t.Column == 23);
    }

    [Fact]
    public void Position_SliceAccess_TrackedCorrectly()
    {
        var source = "arr[1:10:2]";
        var tokens = Tokenize(source);

        tokens.Should().Contain(t => t.Type == TokenType.LeftBracket && t.Column == 4);
        tokens.Should().Contain(t => t.Type == TokenType.Colon && t.Column == 6);
        tokens.Should().Contain(t => t.Type == TokenType.RightBracket && t.Column == 11);
    }

    [Fact]
    public void Position_ComplexExpression_AllPositionsCorrect()
    {
        var source = "result = (a + b) * c - d / e ** f";
        var tokens = Tokenize(source);

        tokens[0].Should().Match<LexerNs.Token>(t => t.Value == "result" && t.Column == 1);
        tokens[1].Should().Match<LexerNs.Token>(t => t.Type == TokenType.Assign && t.Column == 8);
        tokens[2].Should().Match<LexerNs.Token>(t => t.Type == TokenType.LeftParen && t.Column == 10);
        tokens.Should().Contain(t => t.Type == TokenType.Plus && t.Column == 13);
        tokens.Should().Contain(t => t.Type == TokenType.RightParen && t.Column == 16);
        tokens.Should().Contain(t => t.Type == TokenType.Star && t.Column == 18);
        tokens.Should().Contain(t => t.Type == TokenType.Minus && t.Column == 22);
        tokens.Should().Contain(t => t.Type == TokenType.Slash && t.Column == 26);
        tokens.Should().Contain(t => t.Type == TokenType.DoubleStar && t.Column == 30);
    }

    [Fact]
    public void Position_BitwiseOperations_TrackedCorrectly()
    {
        var source = "result = (a & b) | (c ^ d) << 2 >> 1";
        var tokens = Tokenize(source);

        tokens.Should().Contain(t => t.Type == TokenType.Ampersand && t.Column == 13);
        tokens.Should().Contain(t => t.Type == TokenType.Pipe && t.Column == 18);
        tokens.Should().Contain(t => t.Type == TokenType.Caret && t.Column == 23);
        tokens.Should().Contain(t => t.Type == TokenType.LeftShift && t.Column == 28);
        tokens.Should().Contain(t => t.Type == TokenType.RightShift && t.Column == 33); // After "<< 2 "
    }

    [Fact]
    public void Position_AllAugmentedAssignments_TrackedCorrectly()
    {
        var source = @"x += 1
y -= 2
z *= 3
a /= 4
b //= 5
c %= 6
d **= 2
e &= 1
f |= 1
g ^= 1
h <<= 1
i >>= 1";
        var tokens = Tokenize(source);

        tokens.Should().Contain(t => t.Type == TokenType.PlusAssign && t.Line == 1);
        tokens.Should().Contain(t => t.Type == TokenType.MinusAssign && t.Line == 2);
        tokens.Should().Contain(t => t.Type == TokenType.StarAssign && t.Line == 3);
        tokens.Should().Contain(t => t.Type == TokenType.SlashAssign && t.Line == 4);
        tokens.Should().Contain(t => t.Type == TokenType.DoubleSlashAssign && t.Line == 5);
        tokens.Should().Contain(t => t.Type == TokenType.PercentAssign && t.Line == 6);
        tokens.Should().Contain(t => t.Type == TokenType.DoubleStarAssign && t.Line == 7);
        tokens.Should().Contain(t => t.Type == TokenType.AmpersandAssign && t.Line == 8);
        tokens.Should().Contain(t => t.Type == TokenType.PipeAssign && t.Line == 9);
        tokens.Should().Contain(t => t.Type == TokenType.CaretAssign && t.Line == 10);
        tokens.Should().Contain(t => t.Type == TokenType.LeftShiftAssign && t.Line == 11);
        tokens.Should().Contain(t => t.Type == TokenType.RightShiftAssign && t.Line == 12);
    }

    [Fact]
    public void Position_BooleanLiterals_TrackedCorrectly()
    {
        var source = "x = True and False or None";
        var tokens = Tokenize(source);

        tokens.Should().Contain(t => t.Type == TokenType.True && t.Column == 5);
        tokens.Should().Contain(t => t.Type == TokenType.And && t.Column == 10);
        tokens.Should().Contain(t => t.Type == TokenType.False && t.Column == 14);
        tokens.Should().Contain(t => t.Type == TokenType.Or && t.Column == 20);
        tokens.Should().Contain(t => t.Type == TokenType.None && t.Column == 23);
    }

    [Fact]
    public void Position_StructDefinition_TrackedCorrectly()
    {
        var source = @"struct Point:
    x: int
    y: int";
        var tokens = Tokenize(source);

        tokens.Should().Contain(t => t.Type == TokenType.Struct && t.Line == 1 && t.Column == 1);
        tokens.Should().Contain(t => t.Value == "Point" && t.Line == 1 && t.Column == 8);
    }

    [Fact]
    public void Position_InterfaceDefinition_TrackedCorrectly()
    {
        var source = @"interface IDrawable:
    def draw():
        ...";
        var tokens = Tokenize(source);

        tokens.Should().Contain(t => t.Type == TokenType.Interface && t.Line == 1 && t.Column == 1);
        tokens.Should().Contain(t => t.Value == "IDrawable" && t.Line == 1 && t.Column == 11);
        tokens.Should().Contain(t => t.Type == TokenType.Ellipsis && t.Line == 3);
    }

    [Fact]
    public void Position_EnumDefinition_TrackedCorrectly()
    {
        var source = @"enum Color:
    RED = 0
    GREEN = 1
    BLUE = 2";
        var tokens = Tokenize(source);

        tokens.Should().Contain(t => t.Type == TokenType.Enum && t.Line == 1 && t.Column == 1);
        tokens.Should().Contain(t => t.Value == "RED" && t.Line == 2);
        tokens.Should().Contain(t => t.Value == "GREEN" && t.Line == 3);
        tokens.Should().Contain(t => t.Value == "BLUE" && t.Line == 4);
    }

    [Fact]
    public void Position_ConstDeclaration_TrackedCorrectly()
    {
        var source = "const MAX_SIZE: int = 100";
        var tokens = Tokenize(source);

        tokens.Should().Contain(t => t.Type == TokenType.Const && t.Column == 1);
        tokens.Should().Contain(t => t.Value == "MAX_SIZE" && t.Column == 7);
    }

    [Fact]
    public void Position_AutoDeclaration_TrackedCorrectly()
    {
        var source = "x: auto = 42";
        var tokens = Tokenize(source);

        tokens.Should().Contain(t => t.Type == TokenType.Auto && t.Column == 4);
    }

    [Fact]
    public void Position_CompleteProgram_AllPositionsCorrect()
    {
        var source = @"# Simple calculator
def add(a: int, b: int) -> int:
    return a + b

class Calculator:
    def multiply(self, x: int, y: int) -> int:
        return x * y

calc = Calculator()
result = calc.multiply(add(2, 3), 4)
print(f""Result: {result}"")";

        var tokens = Tokenize(source);

        // Verify key positions
        tokens.Should().Contain(t => t.Type == TokenType.Def && t.Line == 2 && t.Column == 1);
        tokens.Should().Contain(t => t.Type == TokenType.Class && t.Line == 5 && t.Column == 1);
        tokens.Should().Contain(t => t.Value == "calc" && t.Line == 9 && t.Column == 1);
        tokens.Should().Contain(t => t.Value == "result" && t.Line == 10 && t.Column == 1);
        tokens.Should().Contain(t => t.Value == "print" && t.Line == 11 && t.Column == 1);
    }

    #endregion

    #region Whitespace Handling

    [Fact]
    public void Position_LeadingWhitespace_DoesNotAffectPosition()
    {
        var source = "    x = 1"; // 4 spaces - valid indentation
        var tokens = Tokenize(source);

        // tokens[0] is INDENT, identifier is tokens[1] at column 5 (after 4 spaces)
        tokens[1].Should().Match<LexerNs.Token>(t => t.Value == "x" && t.Column == 5);
    }

    [Fact]
    public void Position_TrailingWhitespace_DoesNotProduceTokens()
    {
        var source = "x = 1     ";
        var tokens = Tokenize(source);

        // Should only have identifier, assign, integer, EOF
        tokens.Should().HaveCount(4);
    }

    [Fact]
    public void Position_MultipleSpacesBetweenTokens_CorrectColumns()
    {
        var source = "x    =    42";
        var tokens = Tokenize(source);

        tokens[0].Column.Should().Be(1);
        tokens[1].Column.Should().Be(6);
        tokens[2].Column.Should().Be(11);
    }

    #endregion
}
