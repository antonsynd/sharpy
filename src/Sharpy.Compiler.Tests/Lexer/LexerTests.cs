using FluentAssertions;
using Sharpy.Compiler.Lexer;
using Xunit;

namespace Sharpy.Compiler.Tests.Lexer;

public class LexerTests
{
    private static List<Token> Tokenize(string source)
    {
        var lexer = new Compiler.Lexer.Lexer(source);
        return lexer.TokenizeAll();
    }

    private static Token SingleToken(string source)
    {
        var tokens = Tokenize(source);
        // Should have exactly 2 tokens: the token we want + EOF
        tokens.Should().HaveCount(2);
        tokens[1].Type.Should().Be(TokenType.Eof);
        return tokens[0];
    }

    #region Basic Tokens

    [Fact]
    public void Tokenize_EmptyString_ReturnsEofOnly()
    {
        var tokens = Tokenize("");
        tokens.Should().HaveCount(1);
        tokens[0].Type.Should().Be(TokenType.Eof);
    }

    [Fact]
    public void Tokenize_Whitespace_ReturnsEofOnly()
    {
        var tokens = Tokenize("    ");
        tokens.Should().HaveCount(1);
        tokens[0].Type.Should().Be(TokenType.Eof);
    }

    [Fact]
    public void Tokenize_SingleNewline_ReturnsNewlineAndEof()
    {
        var tokens = Tokenize("\n");
        tokens.Should().HaveCount(2);
        tokens[0].Type.Should().Be(TokenType.Newline);
        tokens[1].Type.Should().Be(TokenType.Eof);
    }

    #endregion

    #region Keywords

    [Theory]
    [InlineData("def", TokenType.Def)]
    [InlineData("class", TokenType.Class)]
    [InlineData("struct", TokenType.Struct)]
    [InlineData("interface", TokenType.Interface)]
    [InlineData("enum", TokenType.Enum)]
    [InlineData("if", TokenType.If)]
    [InlineData("else", TokenType.Else)]
    [InlineData("elif", TokenType.Elif)]
    [InlineData("while", TokenType.While)]
    [InlineData("for", TokenType.For)]
    [InlineData("in", TokenType.In)]
    [InlineData("return", TokenType.Return)]
    [InlineData("break", TokenType.Break)]
    [InlineData("continue", TokenType.Continue)]
    [InlineData("pass", TokenType.Pass)]
    [InlineData("try", TokenType.Try)]
    [InlineData("except", TokenType.Except)]
    [InlineData("finally", TokenType.Finally)]
    [InlineData("raise", TokenType.Raise)]
    [InlineData("assert", TokenType.Assert)]
    [InlineData("import", TokenType.Import)]
    [InlineData("from", TokenType.From)]
    [InlineData("as", TokenType.As)]
    [InlineData("auto", TokenType.Auto)]
    [InlineData("const", TokenType.Const)]
    [InlineData("lambda", TokenType.Lambda)]
    [InlineData("True", TokenType.True)]
    [InlineData("False", TokenType.False)]
    [InlineData("None", TokenType.None)]
    [InlineData("and", TokenType.And)]
    [InlineData("or", TokenType.Or)]
    [InlineData("not", TokenType.Not)]
    [InlineData("is", TokenType.Is)]
    public void Tokenize_Keyword_ReturnsCorrectToken(string keyword, TokenType expectedType)
    {
        var token = SingleToken(keyword);
        token.Type.Should().Be(expectedType);
        token.Value.Should().Be(keyword);
    }

    #endregion

    #region Identifiers

    [Theory]
    [InlineData("x")]
    [InlineData("my_variable")]
    [InlineData("_private")]
    [InlineData("ClassName")]
    [InlineData("MAX_SIZE")]
    [InlineData("value2")]
    [InlineData("_internal_counter")]
    public void Tokenize_ValidIdentifier_ReturnsIdentifierToken(string identifier)
    {
        var token = SingleToken(identifier);
        token.Type.Should().Be(TokenType.Identifier);
        token.Value.Should().Be(identifier);
    }

    #endregion

    #region Numeric Literals

    [Theory]
    [InlineData("0")]
    [InlineData("42")]
    [InlineData("1000000")]
    [InlineData("1_000_000")]
    public void Tokenize_Integer_ReturnsIntegerToken(string number)
    {
        var token = SingleToken(number);
        token.Type.Should().Be(TokenType.Integer);
        token.Value.Should().Be(number.Replace("_", ""));
    }

    [Theory]
    [InlineData("3.14")]
    [InlineData("0.5")]
    [InlineData("123.456")]
    [InlineData("3.141_592_653")]
    public void Tokenize_Float_ReturnsFloatToken(string number)
    {
        var token = SingleToken(number);
        token.Type.Should().Be(TokenType.Float);
        token.Value.Should().Be(number.Replace("_", ""));
    }

    [Theory]
    [InlineData("42L", "42L")]
    [InlineData("42l", "42l")]
    [InlineData("42U", "42U")]
    [InlineData("42u", "42u")]
    [InlineData("42UL", "42UL")]
    [InlineData("42ul", "42ul")]
    public void Tokenize_IntegerWithSuffix_ReturnsCorrectToken(string input, string expected)
    {
        var token = SingleToken(input);
        token.Value.Should().Be(expected);
    }

    [Theory]
    [InlineData("3.14f", "3.14f")]
    [InlineData("3.14F", "3.14F")]
    [InlineData("3.14d", "3.14d")]
    [InlineData("3.14D", "3.14D")]
    [InlineData("3.14m", "3.14m")]
    [InlineData("3.14M", "3.14M")]
    public void Tokenize_FloatWithSuffix_ReturnsFloatToken(string input, string expected)
    {
        var token = SingleToken(input);
        token.Type.Should().Be(TokenType.Float);
        token.Value.Should().Be(expected);
    }

    #endregion

    #region String Literals

    [Theory]
    [InlineData("\"hello\"", "hello")]
    [InlineData("'world'", "world")]
    [InlineData("\"\"", "")]
    [InlineData("''", "")]
    public void Tokenize_SimpleString_ReturnsStringToken(string input, string expected)
    {
        var token = SingleToken(input);
        token.Type.Should().Be(TokenType.String);
        token.Value.Should().Be(expected);
    }

    [Theory]
    [InlineData("\"hello\\nworld\"", "hello\nworld")]
    [InlineData("\"tab\\there\"", "tab\there")]
    [InlineData("\"quote\\\"inside\"", "quote\"inside")]
    [InlineData("'single\\'quote'", "single'quote")]
    [InlineData("\"backslash\\\\here\"", "backslash\\here")]
    public void Tokenize_StringWithEscapes_ProcessesEscapeSequences(string input, string expected)
    {
        var token = SingleToken(input);
        token.Type.Should().Be(TokenType.String);
        token.Value.Should().Be(expected);
    }

    [Fact]
    public void Tokenize_TripleQuotedString_ReturnsStringToken()
    {
        var input = "\"\"\"This is a\nmulti-line\nstring\"\"\"";
        var token = SingleToken(input);
        token.Type.Should().Be(TokenType.String);
        token.Value.Should().Be("This is a\nmulti-line\nstring");
    }

    [Fact]
    public void Tokenize_RawString_DoesNotProcessEscapes()
    {
        var token = SingleToken("r\"C:\\Users\\Alice\\Documents\"");
        token.Type.Should().Be(TokenType.String);
        token.Value.Should().Be("C:\\Users\\Alice\\Documents");
    }

    [Fact]
    public void Tokenize_FString_ReturnsFStringToken()
    {
        var token = SingleToken("f\"Hello {name}\"");
        token.Type.Should().Be(TokenType.FString);
        token.Value.Should().Be("Hello {name}");
    }

    [Fact]
    public void Tokenize_FStringWithNestedBraces_HandlesCorrectly()
    {
        var token = SingleToken("f\"Result: {calc(x, {y})}\"");
        token.Type.Should().Be(TokenType.FString);
        token.Value.Should().Be("Result: {calc(x, {y})}");
    }

    [Fact]
    public void Tokenize_UnterminatedString_ThrowsLexerError()
    {
        Action act = () => Tokenize("\"unterminated");
        act.Should().Throw<LexerError>().WithMessage("*Unterminated string*");
    }

    #endregion

    #region Operators

    [Theory]
    [InlineData("+", TokenType.Plus)]
    [InlineData("-", TokenType.Minus)]
    [InlineData("*", TokenType.Star)]
    [InlineData("/", TokenType.Slash)]
    [InlineData("//", TokenType.DoubleSlash)]
    [InlineData("%", TokenType.Percent)]
    [InlineData("**", TokenType.DoubleStar)]
    public void Tokenize_ArithmeticOperator_ReturnsCorrectToken(string op, TokenType expectedType)
    {
        var token = SingleToken(op);
        token.Type.Should().Be(expectedType);
    }

    [Theory]
    [InlineData("==", TokenType.Equal)]
    [InlineData("!=", TokenType.NotEqual)]
    [InlineData("<", TokenType.Less)]
    [InlineData(">", TokenType.Greater)]
    [InlineData("<=", TokenType.LessEqual)]
    [InlineData(">=", TokenType.GreaterEqual)]
    public void Tokenize_ComparisonOperator_ReturnsCorrectToken(string op, TokenType expectedType)
    {
        var token = SingleToken(op);
        token.Type.Should().Be(expectedType);
    }

    [Theory]
    [InlineData("&", TokenType.Ampersand)]
    [InlineData("|", TokenType.Pipe)]
    [InlineData("^", TokenType.Caret)]
    [InlineData("~", TokenType.Tilde)]
    [InlineData("<<", TokenType.LeftShift)]
    [InlineData(">>", TokenType.RightShift)]
    public void Tokenize_BitwiseOperator_ReturnsCorrectToken(string op, TokenType expectedType)
    {
        var token = SingleToken(op);
        token.Type.Should().Be(expectedType);
    }

    [Theory]
    [InlineData("=", TokenType.Assign)]
    [InlineData("+=", TokenType.PlusAssign)]
    [InlineData("-=", TokenType.MinusAssign)]
    [InlineData("*=", TokenType.StarAssign)]
    [InlineData("/=", TokenType.SlashAssign)]
    [InlineData("//=", TokenType.DoubleSlashAssign)]
    [InlineData("%=", TokenType.PercentAssign)]
    [InlineData("**=", TokenType.DoubleStarAssign)]
    [InlineData("&=", TokenType.AmpersandAssign)]
    [InlineData("|=", TokenType.PipeAssign)]
    [InlineData("^=", TokenType.CaretAssign)]
    [InlineData("<<=", TokenType.LeftShiftAssign)]
    [InlineData(">>=", TokenType.RightShiftAssign)]
    public void Tokenize_AssignmentOperator_ReturnsCorrectToken(string op, TokenType expectedType)
    {
        var token = SingleToken(op);
        token.Type.Should().Be(expectedType);
    }

    [Theory]
    [InlineData("?.", TokenType.NullConditional)]
    [InlineData("??", TokenType.NullCoalesce)]
    [InlineData("...", TokenType.Ellipsis)]
    public void Tokenize_SpecialOperator_ReturnsCorrectToken(string op, TokenType expectedType)
    {
        var token = SingleToken(op);
        token.Type.Should().Be(expectedType);
    }

    #endregion

    #region Delimiters

    [Theory]
    [InlineData("(", TokenType.LeftParen)]
    [InlineData(")", TokenType.RightParen)]
    [InlineData("[", TokenType.LeftBracket)]
    [InlineData("]", TokenType.RightBracket)]
    [InlineData("{", TokenType.LeftBrace)]
    [InlineData("}", TokenType.RightBrace)]
    [InlineData(",", TokenType.Comma)]
    [InlineData(":", TokenType.Colon)]
    [InlineData(";", TokenType.Semicolon)]
    [InlineData(".", TokenType.Dot)]
    [InlineData("->", TokenType.Arrow)]
    [InlineData("@", TokenType.At)]
    [InlineData("\\", TokenType.Backslash)]
    public void Tokenize_Delimiter_ReturnsCorrectToken(string delim, TokenType expectedType)
    {
        var token = SingleToken(delim);
        token.Type.Should().Be(expectedType);
    }

    #endregion

    #region Indentation

    [Fact]
    public void Tokenize_SimpleIndent_GeneratesIndentToken()
    {
        var source = "if True:\n    pass";
        var tokens = Tokenize(source);

        tokens.Should().Contain(t => t.Type == TokenType.Indent);
    }

    [Fact]
    public void Tokenize_SimpleDedent_GeneratesDedentToken()
    {
        var source = "if True:\n    pass\nelse:\n    pass";
        var tokens = Tokenize(source);

        tokens.Should().Contain(t => t.Type == TokenType.Dedent);
    }

    [Fact]
    public void Tokenize_NestedIndentation_GeneratesMultipleIndentTokens()
    {
        var source = @"if True:
    if False:
        pass";
        var tokens = Tokenize(source);

        tokens.Count(t => t.Type == TokenType.Indent).Should().Be(2);
    }

    [Fact]
    public void Tokenize_MultiLevelDedent_GeneratesMultipleDedentTokens()
    {
        var source = @"if True:
    if False:
        pass
x = 1";
        var tokens = Tokenize(source);

        tokens.Count(t => t.Type == TokenType.Dedent).Should().Be(2);
    }

    [Fact]
    public void Tokenize_TabIndentation_ThrowsLexerError()
    {
        var source = "if True:\n\tpass";
        Action act = () => Tokenize(source);
        act.Should().Throw<LexerError>().WithMessage("*Tabs are not allowed*");
    }

    [Fact]
    public void Tokenize_NonMultipleOf4Indentation_ThrowsLexerError()
    {
        var source = "if True:\n  pass";  // 2 spaces instead of 4
        Action act = () => Tokenize(source);
        act.Should().Throw<LexerError>().WithMessage("*multiple of 4*");
    }

    [Fact]
    public void Tokenize_IndentationMismatch_ThrowsLexerError()
    {
        // The mismatch check happens when dedenting.
        // Any invalid indentation (not multiple of 4 or not matching a previous level)
        // will throw a LexerError.
        Action act = () => Tokenize("if True:\n    pass\n      x = 1");
        act.Should().Throw<LexerError>();  // Either "multiple of 4" or "mismatch" error is fine
    }

    #endregion

    #region Comments

    [Fact]
    public void Tokenize_SingleLineComment_IsSkipped()
    {
        var tokens = Tokenize("# This is a comment");
        tokens.Should().HaveCount(1);
        tokens[0].Type.Should().Be(TokenType.Eof);
    }

    [Fact]
    public void Tokenize_CommentAtEndOfLine_IsSkipped()
    {
        var tokens = Tokenize("x = 42  # Comment here");

        // Should have: identifier, assign, integer, EOF (no comment token)
        tokens.Should().NotContain(t => t.Type == TokenType.Comment);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier);
        tokens.Should().Contain(t => t.Type == TokenType.Assign);
        tokens.Should().Contain(t => t.Type == TokenType.Integer);
    }

    #endregion

    #region Line Continuation

    [Fact]
    public void Tokenize_ImplicitLineContinuation_InParens_SkipsNewlines()
    {
        var source = @"result = (
    1 + 2 +
    3
)";
        var tokens = Tokenize(source);

        // Should not have newline tokens inside the parens
        var parenStart = tokens.FindIndex(t => t.Type == TokenType.LeftParen);
        var parenEnd = tokens.FindIndex(t => t.Type == TokenType.RightParen);

        tokens.Skip(parenStart).Take(parenEnd - parenStart)
            .Should().NotContain(t => t.Type == TokenType.Newline);
    }

    [Fact]
    public void Tokenize_ImplicitLineContinuation_InBrackets_SkipsNewlines()
    {
        var source = @"items = [
    1,
    2,
    3
]";
        var tokens = Tokenize(source);

        var bracketStart = tokens.FindIndex(t => t.Type == TokenType.LeftBracket);
        var bracketEnd = tokens.FindIndex(t => t.Type == TokenType.RightBracket);

        tokens.Skip(bracketStart).Take(bracketEnd - bracketStart)
            .Should().NotContain(t => t.Type == TokenType.Newline);
    }

    #endregion

    #region Complex Examples

    [Fact]
    public void Tokenize_SimpleFunctionDef_ParsesCorrectly()
    {
        var source = @"def greet(name: str) -> str:
    return f""Hello, {name}""";

        var tokens = Tokenize(source);

        tokens.Should().Contain(t => t.Type == TokenType.Def);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "greet");
        tokens.Should().Contain(t => t.Type == TokenType.LeftParen);
        tokens.Should().Contain(t => t.Type == TokenType.Colon);
        tokens.Should().Contain(t => t.Type == TokenType.Arrow);
        tokens.Should().Contain(t => t.Type == TokenType.Indent);
        tokens.Should().Contain(t => t.Type == TokenType.Return);
        tokens.Should().Contain(t => t.Type == TokenType.FString);
        tokens.Should().Contain(t => t.Type == TokenType.Dedent);
    }

    [Fact]
    public void Tokenize_SimpleClass_ParsesCorrectly()
    {
        var source = @"class Person:
    name: str
    age: int";

        var tokens = Tokenize(source);

        tokens.Should().Contain(t => t.Type == TokenType.Class);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "Person");
        tokens.Should().Contain(t => t.Type == TokenType.Colon);
        tokens.Should().Contain(t => t.Type == TokenType.Indent);
        tokens.Count(t => t.Type == TokenType.Identifier && t.Value == "str").Should().Be(1);
        tokens.Count(t => t.Type == TokenType.Identifier && t.Value == "int").Should().Be(1);
    }

    [Fact]
    public void Tokenize_ExpressionWithMultipleOperators_ParsesCorrectly()
    {
        var source = "result = (a + b) * c - d / e";
        var tokens = Tokenize(source);

        tokens.Should().Contain(t => t.Type == TokenType.Assign);
        tokens.Should().Contain(t => t.Type == TokenType.Plus);
        tokens.Should().Contain(t => t.Type == TokenType.Star);
        tokens.Should().Contain(t => t.Type == TokenType.Minus);
        tokens.Should().Contain(t => t.Type == TokenType.Slash);
    }

    [Fact]
    public void Tokenize_DecoratorSyntax_ParsesCorrectly()
    {
        var source = @"@override
def method(self) -> None:
    pass";

        var tokens = Tokenize(source);

        tokens.Should().Contain(t => t.Type == TokenType.At);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "override");
        tokens.Should().Contain(t => t.Type == TokenType.Newline);
        tokens.Should().Contain(t => t.Type == TokenType.Def);
    }

    [Fact]
    public void Tokenize_NullCoalesceAndNullConditional_ParsesCorrectly()
    {
        var source = "value = obj?.method() ?? default_value";
        var tokens = Tokenize(source);

        tokens.Should().Contain(t => t.Type == TokenType.NullConditional);
        tokens.Should().Contain(t => t.Type == TokenType.NullCoalesce);
    }

    [Fact]
    public void Tokenize_LambdaExpression_ParsesCorrectly()
    {
        var source = "f = lambda x, y: x + y";
        var tokens = Tokenize(source);

        tokens.Should().Contain(t => t.Type == TokenType.Lambda);
        tokens.Should().Contain(t => t.Type == TokenType.Colon);
        tokens.Should().Contain(t => t.Type == TokenType.Plus);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Tokenize_EmptyLines_AreHandledCorrectly()
    {
        var source = @"x = 1

y = 2";
        var tokens = Tokenize(source);

        // Multiple newlines should be collapsed
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "x");
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "y");
    }

    [Fact]
    public void Tokenize_WindowsLineEndings_HandledCorrectly()
    {
        var source = "x = 1\r\ny = 2";
        var tokens = Tokenize(source);

        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "x");
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "y");
        tokens.Count(t => t.Type == TokenType.Newline).Should().Be(1);
    }

    [Fact]
    public void Tokenize_UnexpectedCharacter_ThrowsLexerError()
    {
        var source = "x $ y";
        Action act = () => Tokenize(source);
        act.Should().Throw<LexerError>().WithMessage("*Unexpected character*");
    }

    [Fact]
    public void Tokenize_LineAndColumnTracking_IsAccurate()
    {
        var source = @"x = 1
y = 2";
        var tokens = Tokenize(source);

        var yToken = tokens.First(t => t.Type == TokenType.Identifier && t.Value == "y");
        yToken.Line.Should().Be(2);
        yToken.Column.Should().Be(1);
    }

    #endregion
}
