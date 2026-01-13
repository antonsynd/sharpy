using FluentAssertions;
using LexerNs = Sharpy.Compiler.Lexer;
using TokenType = Sharpy.Compiler.Lexer.TokenType;
using LexerError = Sharpy.Compiler.Lexer.LexerError;
using Xunit;

namespace Sharpy.Compiler.Tests.Lexer;

public class LexerTests
{
    private static List<LexerNs.Token> Tokenize(string source)
    {
        var lexer = new LexerNs.Lexer(source);
        return lexer.TokenizeAll();
    }

    private static LexerNs.Token SingleToken(string source)
    {
        var tokens = Tokenize(source);
        // Should have exactly 2 tokens: the token we want + EOF
        tokens.Should().HaveCount(2);
        tokens[1].Type.Should().Be(LexerNs.TokenType.Eof);
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
    public void Tokenize_SingleNewline_ReturnsEofOnly()
    {
        // A single newline is a blank line and should be skipped
        var tokens = Tokenize("\n");
        tokens.Should().HaveCount(1);
        tokens[0].Type.Should().Be(TokenType.Eof);
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
    [InlineData("with", TokenType.With)]
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

    [Fact]
    public void Tokenize_IdentifierStartingWithDigit_ThrowsLexerError()
    {
        var source = "2fast";
        Action act = () => Tokenize(source);
        act.Should().Throw<LexerError>();
    }

    // Note: According to the v0.1 spec, hyphens are not allowed in identifiers.
    // The current lexer behavior is correct: it tokenizes "my-var" as "my", "-", "var".
    // This is not a bug, but proper tokenization.

    [Fact]
    public void Tokenize_IdentifierWithHyphen_TokenizesAsThreeSeparateTokens()
    {
        // This test documents the correct behavior: hyphens are not part of identifiers,
        // so "my-var" is tokenized as identifier "my", minus operator "-", and identifier "var"
        var source = "my-var";
        var tokens = Tokenize(source);

        // Should have: identifier("my"), minus("-"), identifier("var"), EOF
        tokens.Should().HaveCount(4);
        tokens[0].Type.Should().Be(TokenType.Identifier);
        tokens[0].Value.Should().Be("my");
        tokens[1].Type.Should().Be(TokenType.Minus);
        tokens[2].Type.Should().Be(TokenType.Identifier);
        tokens[2].Value.Should().Be("var");
        tokens[3].Type.Should().Be(TokenType.Eof);
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

    // Note: The following features (hex/binary/octal literals and scientific notation)
    // are part of v0.6 Extended Numeric Literals according to the spec, but have been implemented ahead of schedule.
    // Tests for these features are in the sections below and are not skipped.
    // The float without leading digit restriction (.5) is properly enforced per v0.1 spec.

    [Fact]
    public void Tokenize_FloatWithoutDigitBeforeDecimal_ThrowsLexerError()
    {
        // v0.1 spec requires at least one digit before the decimal point
        var source = ".5";
        Action act = () => Tokenize(source);
        act.Should().Throw<LexerError>();
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
        token.Type.Should().Be(TokenType.RawString);
        token.Value.Should().Be("C:\\Users\\Alice\\Documents");
    }

    [Fact]
    public void Tokenize_FString_ReturnsFStringTokens()
    {
        var tokens = Tokenize("f\"Hello {name}\"");
        // Should emit: FStringStart, FStringText, FStringExprStart, Identifier, FStringExprEnd, FStringEnd, Eof
        tokens.Should().Contain(t => t.Type == TokenType.FStringStart);
        tokens.Should().Contain(t => t.Type == TokenType.FStringText && t.Value == "Hello ");
        tokens.Should().Contain(t => t.Type == TokenType.FStringExprStart);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "name");
        tokens.Should().Contain(t => t.Type == TokenType.FStringExprEnd);
        tokens.Should().Contain(t => t.Type == TokenType.FStringEnd);
    }

    [Fact]
    public void Tokenize_FStringWithNestedBraces_HandlesCorrectly()
    {
        var tokens = Tokenize("f\"Result: {calc(x, {y})}\"");
        // Should have FStringStart, FStringText, expression tokens, FStringEnd
        tokens.Should().Contain(t => t.Type == TokenType.FStringStart);
        tokens.Should().Contain(t => t.Type == TokenType.FStringText && t.Value == "Result: ");
        tokens.Should().Contain(t => t.Type == TokenType.FStringExprStart);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "calc");
        tokens.Should().Contain(t => t.Type == TokenType.LeftBrace); // nested brace
        tokens.Should().Contain(t => t.Type == TokenType.RightBrace); // nested brace
        tokens.Should().Contain(t => t.Type == TokenType.FStringExprEnd);
        tokens.Should().Contain(t => t.Type == TokenType.FStringEnd);
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
    [InlineData(":=", TokenType.ColonAssign)]
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
    [InlineData("|>", TokenType.PipeForward)]
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

    [Fact]
    public void Tokenize_EmptySetLiteral_ProducesThreeTokens()
    {
        // The empty set literal {/} should tokenize as LeftBrace, Slash, RightBrace
        var tokens = Tokenize("{/}");
        tokens.Should().HaveCount(4); // LeftBrace, Slash, RightBrace, EOF
        tokens[0].Type.Should().Be(TokenType.LeftBrace);
        tokens[1].Type.Should().Be(TokenType.Slash);
        tokens[2].Type.Should().Be(TokenType.RightBrace);
        tokens[3].Type.Should().Be(TokenType.Eof);
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

    [Fact]
    public void Tokenize_TripleQuotedString_NotTreatedAsComment()
    {
        var source = "\"\"\"This is a string, not a comment\"\"\"";
        var token = SingleToken(source);
        token.Type.Should().Be(TokenType.String);
        token.Value.Should().Be("This is a string, not a comment");
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

    [Fact]
    public void Tokenize_ExplicitLineContinuation_WithBackslash_JoinsLines()
    {
        var source = @"total = value1 + \
        value2 + \
        value3";
        var tokens = Tokenize(source);

        // Should have: identifier, assign, identifier, plus, identifier, plus, identifier, EOF
        // No newline tokens should appear
        tokens.Should().NotContain(t => t.Type == TokenType.Newline);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "total");
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "value1");
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "value2");
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "value3");
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
        // Check for f-string tokens instead of single FString token
        tokens.Should().Contain(t => t.Type == TokenType.FStringStart);
        tokens.Should().Contain(t => t.Type == TokenType.FStringEnd);
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
    public void Tokenize_PipeForwardOperator_ParsesCorrectly()
    {
        var source = "result = data |> transform() |> filter()";
        var tokens = Tokenize(source);

        tokens.Should().Contain(t => t.Type == TokenType.PipeForward);
        tokens.Count(t => t.Type == TokenType.PipeForward).Should().Be(2);
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

    #region Nullable Type Operator Tests

    [Fact]
    public void Tokenize_QuestionMark_SingleToken()
    {
        var token = SingleToken("?");
        token.Type.Should().Be(TokenType.Question);
        token.Value.Should().Be("?");
    }

    [Fact]
    public void Tokenize_NullableTypeAnnotation_ProducesCorrectTokens()
    {
        var source = "int?";
        var tokens = Tokenize(source);

        tokens.Should().HaveCount(3); // Identifier, Question, EOF
        tokens[0].Type.Should().Be(TokenType.Identifier);
        tokens[0].Value.Should().Be("int");
        tokens[1].Type.Should().Be(TokenType.Question);
    }

    [Fact]
    public void Tokenize_NullConditional_ProducesCorrectToken()
    {
        var token = SingleToken("?.");
        token.Type.Should().Be(TokenType.NullConditional);
        token.Value.Should().Be("?.");
    }

    [Fact]
    public void Tokenize_NullCoalesce_ProducesCorrectToken()
    {
        var token = SingleToken("??");
        token.Type.Should().Be(TokenType.NullCoalesce);
        token.Value.Should().Be("??");
    }

    [Fact]
    public void Tokenize_MixedQuestionOperators_ProducesCorrectTokens()
    {
        var source = "x? y?. z??";
        var tokens = Tokenize(source);

        tokens.Should().Contain(t => t.Type == TokenType.Question && t.Value == "?");
        tokens.Should().Contain(t => t.Type == TokenType.NullConditional && t.Value == "?.");
        tokens.Should().Contain(t => t.Type == TokenType.NullCoalesce && t.Value == "??");
    }

    #endregion

    #region Unicode and Special Character Tests

    [Fact]
    public void Tokenize_UnicodeIdentifiers_ProducesCorrectTokens()
    {
        var source = "café résumé λ μ Σ";
        var tokens = Tokenize(source);

        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "café");
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "résumé");
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "λ");
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "μ");
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "Σ");
    }

    [Fact]
    public void Tokenize_EmojiInIdentifier_ThrowsError()
    {
        var source = "emoji😀name";
        Action act = () => Tokenize(source);
        act.Should().Throw<LexerError>();
    }

    #endregion

    #region String Literal Edge Cases

    [Fact]
    public void Tokenize_VeryLongString_ProducesCorrectToken()
    {
        var longString = new string('a', 10000);
        var source = $"\"{longString}\"";
        var token = SingleToken(source);

        token.Type.Should().Be(TokenType.String);
        token.Value.Should().Be(longString);
    }

    [Fact]
    public void Tokenize_EmptyString_ProducesCorrectToken()
    {
        var token = SingleToken("\"\"");
        token.Type.Should().Be(TokenType.String);
        token.Value.Should().Be("");
    }

    [Fact]
    public void Tokenize_StringWithAllEscapeSequences_ProducesCorrectToken()
    {
        var source = @"""\\n\\t\\r\\\\\\'\\\""\\\a\\\b\\\f\\\v\\\0""";
        var token = SingleToken(source);
        token.Type.Should().Be(TokenType.String);
    }

    [Fact]
    public void Tokenize_StringWithInvalidEscape_ThrowsError()
    {
        var source = @"""invalid\xescape""";
        Action act = () => Tokenize(source);
        act.Should().Throw<LexerError>().WithMessage("*escape sequence*");
    }

    [Fact]
    public void Tokenize_UnterminatedString_ThrowsError()
    {
        var source = "\"unterminated";
        Action act = () => Tokenize(source);
        act.Should().Throw<LexerError>().WithMessage("*Unterminated string*");
    }

    [Fact]
    public void Tokenize_RawString_PreservesBackslashes()
    {
        var source = @"r""C:\path\to\file""";
        var token = SingleToken(source);
        token.Type.Should().Be(TokenType.RawString);
        token.Value.Should().Be(@"C:\path\to\file");
    }

    [Fact]
    public void Tokenize_TripleQuotedString_HandlesMultiline()
    {
        var source = "\"\"\"line1\nline2\nline3\"\"\"";
        var token = SingleToken(source);
        token.Type.Should().Be(TokenType.String);
        token.Value.Should().Contain("line1");
        token.Value.Should().Contain("line2");
        token.Value.Should().Contain("line3");
    }

    [Fact]
    public void Tokenize_UnterminatedTripleQuotedString_ThrowsError()
    {
        var source = "\"\"\"unterminated";
        Action act = () => Tokenize(source);
        act.Should().Throw<LexerError>(); // Message is "Unterminated triple-quoted string"
    }

    #endregion

    #region F-String Edge Cases

    [Fact]
    public void Tokenize_EmptyFString_ProducesCorrectTokens()
    {
        var source = "f\"\"";
        var tokens = Tokenize(source);
        // Should emit: FStringStart, FStringEnd (no text since it's empty), Eof
        tokens.Should().Contain(t => t.Type == TokenType.FStringStart);
        tokens.Should().Contain(t => t.Type == TokenType.FStringEnd);
    }

    [Fact]
    public void Tokenize_FStringWithExpression_ProducesCorrectTokens()
    {
        var source = "f\"value: {x}\"";
        var tokens = Tokenize(source);
        // Should emit: FStringStart, FStringText, FStringExprStart, Identifier, FStringExprEnd, FStringEnd, Eof
        tokens.Should().Contain(t => t.Type == TokenType.FStringStart);
        tokens.Should().Contain(t => t.Type == TokenType.FStringText && t.Value.Contains("value: "));
        tokens.Should().Contain(t => t.Type == TokenType.FStringExprStart);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "x");
        tokens.Should().Contain(t => t.Type == TokenType.FStringExprEnd);
        tokens.Should().Contain(t => t.Type == TokenType.FStringEnd);
    }

    [Fact]
    public void Tokenize_FStringWithMultipleExpressions_ProducesCorrectTokens()
    {
        var source = "f\"x={x}, y={y}\"";
        var tokens = Tokenize(source);
        // Should contain FStringStart, text segments, expression markers, and FStringEnd
        tokens.Should().Contain(t => t.Type == TokenType.FStringStart);
        tokens.Should().Contain(t => t.Type == TokenType.FStringText && t.Value.Contains("x="));
        tokens.Should().Contain(t => t.Type == TokenType.FStringExprStart);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "x");
        tokens.Should().Contain(t => t.Type == TokenType.FStringExprEnd);
        tokens.Should().Contain(t => t.Type == TokenType.FStringText && t.Value.Contains(", y="));
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "y");
        tokens.Should().Contain(t => t.Type == TokenType.FStringEnd);
    }

    [Fact]
    public void Tokenize_UnterminatedFString_ThrowsError()
    {
        var source = "f\"unterminated";
        Action act = () => Tokenize(source);
        act.Should().Throw<LexerError>();
    }

    #endregion

    #region Number Literal Edge Cases

    [Fact]
    public void Tokenize_VeryLargeInteger_ProducesCorrectToken()
    {
        var source = "999999999999999999999999999999";
        var token = SingleToken(source);
        token.Type.Should().Be(TokenType.Integer);
        token.Value.Should().Be(source);
    }

    [Fact]
    public void Tokenize_VerySmallFloat_ProducesCorrectToken()
    {
        var source = "0.00000000000000000001";
        var token = SingleToken(source);
        token.Type.Should().Be(TokenType.Float);
        token.Value.Should().Be(source);
    }

    [Fact]
    public void Tokenize_ScientificNotationEdgeCases_ProducesCorrectTokens()
    {
        // Scientific notation is part of v0.6 Extended Numeric Literals but implemented ahead of schedule
        var cases = new[] { "1e100", "1e-100", "1.5e50", "1.5e-50" };

        foreach (var source in cases)
        {
            var token = SingleToken(source);
            token.Type.Should().Be(TokenType.Float);
            token.Value.Should().Be(source);
        }
    }

    [Fact]
    public void Tokenize_HexWithAllDigits_ProducesCorrectToken()
    {
        // Hexadecimal literals are part of v0.6 Extended Numeric Literals but implemented ahead of schedule
        var source = "0x0123456789ABCDEF";
        var token = SingleToken(source);
        token.Type.Should().Be(TokenType.Integer);
        token.Value.Should().Be(source);
    }

    [Fact]
    public void Tokenize_BinaryWithLongSequence_ProducesCorrectToken()
    {
        // Binary literals are part of v0.6 Extended Numeric Literals but implemented ahead of schedule
        var source = "0b" + new string('1', 64);
        var token = SingleToken(source);
        token.Type.Should().Be(TokenType.Integer);
        token.Value.Should().Be(source);
    }

    [Fact]
    public void Tokenize_OctalWithAllDigits_ProducesCorrectToken()
    {
        // Octal literals are part of v0.6 Extended Numeric Literals but implemented ahead of schedule
        var source = "0o01234567";
        var token = SingleToken(source);
        token.Type.Should().Be(TokenType.Integer);
        token.Value.Should().Be(source);
    }

    [Fact]
    public void Tokenize_NumbersWithUnderscores_ProducesCorrectTokens()
    {
        // Test decimal and non-decimal numbers with underscores
        // Note: hex/binary/octal are v0.6 features but implemented ahead of schedule
        var cases = new[] { "1_000_000", "0x_DEAD_BEEF", "0b_1111_0000", "3.14_159_265" };

        foreach (var source in cases)
        {
            var tokens = Tokenize(source);
            tokens.Should().Contain(t => t.Type == TokenType.Integer || t.Type == TokenType.Float);
        }
    }

    [Fact]
    public void Tokenize_InvalidNumberFormat_ThrowsError()
    {
        var invalidCases = new[] { "0x", "0b", "0o", "1e" }; // 1.2.3 is actually valid - it's 1.2 . 3

        foreach (var source in invalidCases)
        {
            Action act = () => Tokenize(source);
            act.Should().Throw<LexerError>();
        }
    }

    #endregion

    #region Indentation Edge Cases

    [Fact]
    public void Tokenize_MixedTabsAndSpaces_ThrowsError()
    {
        var source = "if x:\n\ty = 1\n    z = 2";
        Action act = () => Tokenize(source);
        act.Should().Throw<LexerError>(); // Tabs or mixed tabs/spaces error
    }

    [Fact]
    public void Tokenize_InconsistentIndentation_ThrowsError()
    {
        var source = "if x:\n  y = 1\n   z = 2";
        Action act = () => Tokenize(source);
        act.Should().Throw<LexerError>(); // Any indentation error is fine (multiple of 4 or inconsistent)
    }

    [Fact]
    public void Tokenize_VeryDeeplyNestedIndentation_ProducesCorrectTokens()
    {
        var source = "if x:\n";
        for (int i = 1; i <= 20; i++)
        {
            source += new string(' ', i * 4) + $"level{i} = {i}\n";
        }

        var tokens = Tokenize(source);
        tokens.Count(t => t.Type == TokenType.Indent).Should().Be(20);
    }

    [Fact]
    public void Tokenize_IndentationInsideBrackets_NoIndentTokens()
    {
        var source = @"x = [
    1,
    2,
    3
]";
        var tokens = Tokenize(source);
        tokens.Should().NotContain(t => t.Type == TokenType.Indent);
        tokens.Should().NotContain(t => t.Type == TokenType.Dedent);
    }

    [Fact]
    public void Tokenize_EmptyLinesInIndentedBlock_HandledCorrectly()
    {
        var source = @"if x:
    y = 1

    z = 2";
        var tokens = Tokenize(source);
        tokens.Should().Contain(t => t.Type == TokenType.Indent);
        tokens.Count(t => t.Type == TokenType.Newline).Should().BeGreaterThan(0);
    }

    #endregion

    #region Operator Combination Tests

    [Fact]
    public void Tokenize_AllCompoundAssignmentOperators_ProducesCorrectTokens()
    {
        var operators = new Dictionary<string, TokenType>
        {
            { "+=", TokenType.PlusAssign },
            { "-=", TokenType.MinusAssign },
            { "*=", TokenType.StarAssign },
            { "/=", TokenType.SlashAssign },
            { "//=", TokenType.DoubleSlashAssign },
            { "%=", TokenType.PercentAssign },
            { "**=", TokenType.DoubleStarAssign },
            { "&=", TokenType.AmpersandAssign },
            { "|=", TokenType.PipeAssign },
            { "^=", TokenType.CaretAssign },
            { "<<=", TokenType.LeftShiftAssign },
            { ">>=", TokenType.RightShiftAssign }
        };

        foreach (var (op, tokenType) in operators)
        {
            var token = SingleToken(op);
            token.Type.Should().Be(tokenType);
            token.Value.Should().Be(op);
        }
    }

    [Fact]
    public void Tokenize_OperatorWithoutSpaces_ProducesCorrectTokens()
    {
        var source = "x+y*z/w-v%u**p//q&r|s^t<<a>>b";
        var tokens = Tokenize(source);

        tokens.Should().Contain(t => t.Type == TokenType.Plus);
        tokens.Should().Contain(t => t.Type == TokenType.Star);
        tokens.Should().Contain(t => t.Type == TokenType.Slash);
        tokens.Should().Contain(t => t.Type == TokenType.Minus);
        tokens.Should().Contain(t => t.Type == TokenType.Percent);
        tokens.Should().Contain(t => t.Type == TokenType.DoubleStar);
        tokens.Should().Contain(t => t.Type == TokenType.DoubleSlash);
        tokens.Should().Contain(t => t.Type == TokenType.Ampersand);
        tokens.Should().Contain(t => t.Type == TokenType.Pipe);
        tokens.Should().Contain(t => t.Type == TokenType.Caret);
        tokens.Should().Contain(t => t.Type == TokenType.LeftShift);
        tokens.Should().Contain(t => t.Type == TokenType.RightShift);
    }

    [Fact]
    public void Tokenize_ThreeCharacterOperatorSequence_ProducesCorrectTokens()
    {
        // Test that >>= is parsed as RightShiftAssign, not RightShift + Assign
        var token = SingleToken(">>=");
        token.Type.Should().Be(TokenType.RightShiftAssign);
        token.Value.Should().Be(">>=");
    }

    #endregion

    #region Line Continuation Tests

    [Fact]
    public void Tokenize_BackslashLineContinuation_ProducesCorrectTokens()
    {
        var source = "x = 1 + \\\n    2";
        var tokens = Tokenize(source);

        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "x");
        tokens.Should().Contain(t => t.Type == TokenType.Integer && t.Value == "1");
        tokens.Should().Contain(t => t.Type == TokenType.Plus);
        tokens.Should().Contain(t => t.Type == TokenType.Integer && t.Value == "2");
    }

    [Fact]
    public void Tokenize_BackslashAtEndOfFile_ThrowsError()
    {
        var source = "x = 1 \\";
        Action act = () => Tokenize(source);
        act.Should().Throw<LexerError>();
    }

    [Fact]
    public void Tokenize_BackslashWithSpaceAfter_ThrowsError()
    {
        var source = "x = 1 \\ \n2";
        Action act = () => Tokenize(source);
        act.Should().Throw<LexerError>();
    }

    #endregion

    #region Comment Edge Cases

    [Fact]
    public void Tokenize_VeryLongComment_IsIgnored()
    {
        var longComment = new string('x', 10000);
        var source = $"x = 1 # {longComment}\ny = 2";
        var tokens = Tokenize(source);

        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "x");
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "y");
        tokens.Should().NotContain(t => t.Value.Contains(longComment));
    }

    [Fact]
    public void Tokenize_CommentWithUnicodeCharacters_IsIgnored()
    {
        var source = "x = 1 # Comment with émojis 😀 and symbols ∑∫∆\ny = 2";
        var tokens = Tokenize(source);

        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "x");
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "y");
    }

    #endregion

    #region Error Recovery Tests

    [Fact]
    public void Tokenize_UnexpectedNullCharacter_ThrowsError()
    {
        var source = "x = \0 1";
        Action act = () => Tokenize(source);
        act.Should().Throw<LexerError>();
    }

    [Fact]
    public void Tokenize_ErrorMessageIncludesLineAndColumn()
    {
        var source = "x = 1\ny = $";
        Action act = () => Tokenize(source);
        act.Should().Throw<LexerError>()
            .WithMessage("*line*2*")
            .WithMessage("*column*");
    }

    #endregion

    #region Literal Name (Backtick) Tests

    [Fact]
    public void Tokenize_SimpleLiteralName_ProducesIdentifierToken()
    {
        var source = "`simple_name`";
        var token = SingleToken(source);
        token.Type.Should().Be(TokenType.Identifier);
        token.Value.Should().Be("simple_name");
    }

    [Fact]
    public void Tokenize_LiteralNameWithKeyword_ProducesIdentifierNotKeyword()
    {
        var source = "`class`";
        var token = SingleToken(source);
        token.Type.Should().Be(TokenType.Identifier);
        token.Value.Should().Be("class");
    }

    [Fact]
    public void Tokenize_LiteralNameWithSpaces_ProducesIdentifierWithSpaces()
    {
        var source = "`name with spaces`";
        var token = SingleToken(source);
        token.Type.Should().Be(TokenType.Identifier);
        token.Value.Should().Be("name with spaces");
    }

    [Fact]
    public void Tokenize_LiteralNameWithSpecialChars_ProducesCorrectToken()
    {
        var source = "`ExactMethodName`";
        var token = SingleToken(source);
        token.Type.Should().Be(TokenType.Identifier);
        token.Value.Should().Be("ExactMethodName");
    }

    [Fact]
    public void Tokenize_LiteralNameInImport_ProducesCorrectTokens()
    {
        var source = "from foo_bar.`abc` import *";
        var tokens = Tokenize(source);

        tokens.Should().Contain(t => t.Type == TokenType.From);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "foo_bar");
        tokens.Should().Contain(t => t.Type == TokenType.Dot);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "abc");
        tokens.Should().Contain(t => t.Type == TokenType.Import);
    }

    [Fact]
    public void Tokenize_LiteralNameAsFunction_ProducesCorrectTokens()
    {
        var source = "def `ExactMethodName`():";
        var tokens = Tokenize(source);

        tokens.Should().Contain(t => t.Type == TokenType.Def);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "ExactMethodName");
        tokens.Should().Contain(t => t.Type == TokenType.LeftParen);
        tokens.Should().Contain(t => t.Type == TokenType.RightParen);
        tokens.Should().Contain(t => t.Type == TokenType.Colon);
    }

    [Fact]
    public void Tokenize_UnterminatedLiteralName_ThrowsError()
    {
        var source = "`unterminated";
        Action act = () => Tokenize(source);
        act.Should().Throw<LexerError>().WithMessage("*Unterminated literal name*");
    }

    [Fact]
    public void Tokenize_LiteralNameWithNewline_ThrowsError()
    {
        var source = "`name\nwith newline`";
        Action act = () => Tokenize(source);
        act.Should().Throw<LexerError>().WithMessage("*Unterminated literal name*");
    }

    [Fact]
    public void Tokenize_EmptyLiteralName_ProducesEmptyIdentifier()
    {
        var source = "``";
        var token = SingleToken(source);
        token.Type.Should().Be(TokenType.Identifier);
        token.Value.Should().Be("");
    }

    [Fact]
    public void Tokenize_LiteralNameWithNumbers_ProducesCorrectToken()
    {
        var source = "`name123`";
        var token = SingleToken(source);
        token.Type.Should().Be(TokenType.Identifier);
        token.Value.Should().Be("name123");
    }

    [Fact]
    public void Tokenize_LiteralNameWithUnderscore_ProducesCorrectToken()
    {
        var source = "`_private_name_`";
        var token = SingleToken(source);
        token.Type.Should().Be(TokenType.Identifier);
        token.Value.Should().Be("_private_name_");
    }

    [Fact]
    public void Tokenize_MultipleLiteralNames_ProducesCorrectTokens()
    {
        var source = "`first` `second` `third`";
        var tokens = Tokenize(source);

        var identifiers = tokens.Where(t => t.Type == TokenType.Identifier).ToList();
        identifiers.Should().HaveCount(3);
        identifiers[0].Value.Should().Be("first");
        identifiers[1].Value.Should().Be("second");
        identifiers[2].Value.Should().Be("third");
    }

    #endregion

    #region Ellipsis Literal Tests (v0.1 Placeholder)

    [Fact]
    public void Tokenize_EllipsisAsPlaceholder_ProducesEllipsisToken()
    {
        var source = "def todo_function():\n    ...";
        var tokens = Tokenize(source);

        tokens.Should().Contain(t => t.Type == TokenType.Ellipsis);
    }

    [Fact]
    public void Tokenize_EllipsisInInterfaceMethod_ProducesCorrectTokens()
    {
        var source = "def draw(self) -> None:\n    ...";
        var tokens = Tokenize(source);

        tokens.Should().Contain(t => t.Type == TokenType.Def);
        tokens.Should().Contain(t => t.Type == TokenType.Ellipsis);
    }

    [Fact]
    public void Tokenize_EllipsisStandalone_ProducesEllipsisToken()
    {
        var token = SingleToken("...");
        token.Type.Should().Be(TokenType.Ellipsis);
        token.Value.Should().Be("...");
    }

    #endregion

    #region v0.1 Comprehensive Edge Cases

    [Fact]
    public void Tokenize_MultilineStringWithEscapedQuotes_TokenizesCorrectly()
    {
        var source = "\"\"\"Line one\\\"quoted\\\" text\nLine two\n\"\"\"";
        var tokens = Tokenize(source);

        var stringToken = tokens.Should().ContainSingle(t => t.Type == TokenType.String).Subject;
        stringToken.Value.Should().Contain("Line one");
        stringToken.Value.Should().Contain("quoted");
        stringToken.Value.Should().Contain("Line two");
    }

    [Fact]
    public void Tokenize_RawStringWithBackslashes_PreservesBackslashes()
    {
        var source = "r\"C:\\\\Users\\\\test\\\\file.txt\"";
        var tokens = Tokenize(source);

        var stringToken = tokens.Should().ContainSingle(t => t.Type == TokenType.RawString).Subject;
        stringToken.Value.Should().Contain("\\\\");
    }

    [Fact]
    public void Tokenize_ComplexOperatorChain_TokenizesAllOperators()
    {
        var source = "x ?? y";
        var tokens = Tokenize(source);

        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "x");
        tokens.Should().Contain(t => t.Type == TokenType.NullCoalesce);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "y");
    }

    [Fact]
    public void Tokenize_DecoratorWithArguments_TokenizesCorrectly()
    {
        var source = "@decorator(arg1, arg2)";
        var tokens = Tokenize(source);

        tokens.Should().Contain(t => t.Type == TokenType.At);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "decorator");
        tokens.Should().Contain(t => t.Type == TokenType.LeftParen);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "arg1");
        tokens.Should().Contain(t => t.Type == TokenType.Comma);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "arg2");
        tokens.Should().Contain(t => t.Type == TokenType.RightParen);
    }

    [Fact]
    public void Tokenize_ComplexGenericSyntax_TokenizesAllBrackets()
    {
        var source = "dict[str, list[int]]";
        var tokens = Tokenize(source);

        tokens.Should().Contain(t => t.Type == TokenType.LeftBracket);
        tokens.Should().Contain(t => t.Type == TokenType.Comma);
        var leftBrackets = tokens.Where(t => t.Type == TokenType.LeftBracket).ToList();
        var rightBrackets = tokens.Where(t => t.Type == TokenType.RightBracket).ToList();
        leftBrackets.Should().HaveCount(2);
        rightBrackets.Should().HaveCount(2);
    }

    [Fact]
    public void Tokenize_NullableGenericType_TokenizesNullOperator()
    {
        var source = "list[int]?";
        var tokens = Tokenize(source);

        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "list");
        tokens.Should().Contain(t => t.Type == TokenType.LeftBracket);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "int");
        tokens.Should().Contain(t => t.Type == TokenType.RightBracket);
        tokens.Should().Contain(t => t.Type == TokenType.Question);
    }

    [Fact]
    public void Tokenize_LiteralNameWithSpaces_TokenizesCorrectly()
    {
        var source = "`class name with spaces`";
        var tokens = Tokenize(source);

        var identifier = tokens.Should().ContainSingle(t => t.Type == TokenType.Identifier).Subject;
        identifier.Value.Should().Be("class name with spaces");
    }

    [Fact]
    public void Tokenize_LiteralNameWithKeyword_TokenizesAsIdentifier()
    {
        var source = "`class`";
        var tokens = Tokenize(source);

        var identifier = tokens.Should().ContainSingle(t => t.Type == TokenType.Identifier).Subject;
        identifier.Value.Should().Be("class");
    }

    [Fact]
    public void Tokenize_LiteralNameWithSpecialChars_TokenizesCorrectly()
    {
        var source = "`method-name+special`";
        var tokens = Tokenize(source);

        var identifier = tokens.Should().ContainSingle(t => t.Type == TokenType.Identifier).Subject;
        identifier.Value.Should().Be("method-name+special");
    }

    [Fact]
    public void Tokenize_FStringWithNestedBraces_TokenizesCorrectly()
    {
        var source = "f\"value: {x + 1}\"";
        var tokens = Tokenize(source);

        // Should contain FStringStart, FStringText, FStringExprStart, expression tokens, FStringExprEnd, FStringEnd
        tokens.Should().Contain(t => t.Type == TokenType.FStringStart);
        tokens.Should().Contain(t => t.Type == TokenType.FStringExprStart);
        tokens.Should().Contain(t => t.Type == TokenType.FStringExprEnd);
        tokens.Should().Contain(t => t.Type == TokenType.FStringEnd);
    }

    [Fact]
    public void Tokenize_FStringWithMultipleExpressions_TokenizesCorrectly()
    {
        var source = "f\"{x} and {y}\"";
        var tokens = Tokenize(source);

        // Should contain FStringStart, expression tokens, FStringEnd
        tokens.Should().Contain(t => t.Type == TokenType.FStringStart);
        tokens.Should().Contain(t => t.Type == TokenType.FStringExprStart);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "x");
        tokens.Should().Contain(t => t.Type == TokenType.FStringExprEnd);
        tokens.Should().Contain(t => t.Type == TokenType.FStringText && t.Value.Contains(" and "));
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "y");
        tokens.Should().Contain(t => t.Type == TokenType.FStringEnd);
    }

    [Fact]
    public void Tokenize_BitwiseShiftOperators_TokenizesSeparately()
    {
        var source = "x << 2 >> 1";
        var tokens = Tokenize(source);

        tokens.Should().Contain(t => t.Type == TokenType.LeftShift);
        tokens.Should().Contain(t => t.Type == TokenType.RightShift);
    }

    [Fact]
    public void Tokenize_AllAugmentedAssignmentOperators_TokenizesCorrectly()
    {
        var source = "a += 1\nb -= 1\nc *= 2\nd /= 2\ne //= 2\nf %= 3\ng **= 2\nh &= 1\ni |= 1\nj ^= 1\nk <<= 1\nl >>= 1";
        var tokens = Tokenize(source);

        tokens.Should().Contain(t => t.Type == TokenType.PlusAssign);
        tokens.Should().Contain(t => t.Type == TokenType.MinusAssign);
        tokens.Should().Contain(t => t.Type == TokenType.StarAssign);
        tokens.Should().Contain(t => t.Type == TokenType.SlashAssign);
        tokens.Should().Contain(t => t.Type == TokenType.DoubleSlashAssign);
        tokens.Should().Contain(t => t.Type == TokenType.PercentAssign);
        tokens.Should().Contain(t => t.Type == TokenType.DoubleStarAssign);
        tokens.Should().Contain(t => t.Type == TokenType.AmpersandAssign);
        tokens.Should().Contain(t => t.Type == TokenType.PipeAssign);
        tokens.Should().Contain(t => t.Type == TokenType.CaretAssign);
        tokens.Should().Contain(t => t.Type == TokenType.LeftShiftAssign);
        tokens.Should().Contain(t => t.Type == TokenType.RightShiftAssign);
    }

    [Fact]
    public void Tokenize_ConstWithAutoKeywords_TokenizesBothKeywords()
    {
        var source = "const auto x = 5";
        var tokens = Tokenize(source);

        tokens.Should().Contain(t => t.Type == TokenType.Const);
        tokens.Should().Contain(t => t.Type == TokenType.Auto);
    }

    [Fact]
    public void Tokenize_AllStructuralKeywords_TokenizesCorrectly()
    {
        var source = "class struct interface enum";
        var tokens = Tokenize(source);

        tokens.Should().Contain(t => t.Type == TokenType.Class);
        tokens.Should().Contain(t => t.Type == TokenType.Struct);
        tokens.Should().Contain(t => t.Type == TokenType.Interface);
        tokens.Should().Contain(t => t.Type == TokenType.Enum);
    }

    [Fact]
    public void Tokenize_ComplexNestedBrackets_TokenizesAllPairs()
    {
        var source = "[[{()}]]";
        var tokens = Tokenize(source);

        var leftBrackets = tokens.Count(t => t.Type == TokenType.LeftBracket);
        var rightBrackets = tokens.Count(t => t.Type == TokenType.RightBracket);
        var leftBraces = tokens.Count(t => t.Type == TokenType.LeftBrace);
        var rightBraces = tokens.Count(t => t.Type == TokenType.RightBrace);
        var leftParens = tokens.Count(t => t.Type == TokenType.LeftParen);
        var rightParens = tokens.Count(t => t.Type == TokenType.RightParen);

        leftBrackets.Should().Be(2);
        rightBrackets.Should().Be(2);
        leftBraces.Should().Be(1);
        rightBraces.Should().Be(1);
        leftParens.Should().Be(1);
        rightParens.Should().Be(1);
    }

    [Fact]
    public void Tokenize_NumericLiteralsWithAllSuffixes_TokenizesCorrectly()
    {
        var source = "42L 3.14f 2.5d";
        var tokens = Tokenize(source);

        var numbers = tokens.Where(t => t.Type == TokenType.Integer || t.Type == TokenType.Float).ToList();
        numbers.Should().HaveCount(3);
    }

    [Fact]
    public void Tokenize_HexBinaryOctalLiterals_TokenizesCorrectly()
    {
        // Hex/Binary/Octal literals are part of v0.6 Extended Numeric Literals but implemented ahead of schedule
        var source = "0xFF 0b1010 0o77";
        var tokens = Tokenize(source);

        var numbers = tokens.Where(t => t.Type == TokenType.Integer).ToList();
        numbers.Should().HaveCount(3);
        numbers[0].Value.Should().StartWith("0x");
        numbers[1].Value.Should().StartWith("0b");
        numbers[2].Value.Should().StartWith("0o");
    }

    [Fact]
    public void Tokenize_CodeWithoutComment_TokenizesWithoutComment()
    {
        var source = "x = 5";
        var tokens = Tokenize(source);

        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "x");
        tokens.Should().Contain(t => t.Type == TokenType.Assign);
        tokens.Should().Contain(t => t.Type == TokenType.Integer);
        // Comments are typically stripped by the lexer
    }

    [Fact]
    public void Tokenize_LineContinuationInExpression_HandlesCorrectly()
    {
        var source = "x = 1 + \\\n    2 + 3";
        var tokens = Tokenize(source);

        var numbers = tokens.Where(t => t.Type == TokenType.Integer).ToList();
        numbers.Should().HaveCount(3);
    }

    [Fact]
    public void Tokenize_ImplicitLineContinuationInList_HandlesCorrectly()
    {
        var source = "[\n    1,\n    2,\n    3\n]";
        var tokens = Tokenize(source);

        var numbers = tokens.Where(t => t.Type == TokenType.Integer).ToList();
        numbers.Should().HaveCount(3);
    }

    [Fact]
    public void Tokenize_ChainedComparisonOperators_TokenizesAll()
    {
        var source = "a < b <= c == d != e";
        var tokens = Tokenize(source);

        tokens.Should().Contain(t => t.Type == TokenType.Less);
        tokens.Should().Contain(t => t.Type == TokenType.LessEqual);
        tokens.Should().Contain(t => t.Type == TokenType.Equal);
        tokens.Should().Contain(t => t.Type == TokenType.NotEqual);
    }

    [Fact]
    public void Tokenize_LambdaArrow_TokenizesCorrectly()
    {
        var source = "lambda x: x + 1";
        var tokens = Tokenize(source);

        tokens.Should().Contain(t => t.Type == TokenType.Lambda);
        tokens.Should().Contain(t => t.Type == TokenType.Colon);
    }

    [Fact]
    public void Tokenize_DoubleStarInGeneric_TokenizesAsPower()
    {
        var source = "x**2";
        var tokens = Tokenize(source);

        tokens.Should().Contain(t => t.Type == TokenType.DoubleStar);
    }

    [Fact]
    public void Tokenize_AllBooleanLiterals_TokenizesAsKeywords()
    {
        var source = "True False None";
        var tokens = Tokenize(source);

        tokens.Should().Contain(t => t.Type == TokenType.True);
        tokens.Should().Contain(t => t.Type == TokenType.False);
        tokens.Should().Contain(t => t.Type == TokenType.None);
    }

    [Fact]
    public void Tokenize_ComplexIndentationMix_HandlesCorrectly()
    {
        var source = @"if True:
    if False:
        pass
    else:
        pass";
        var tokens = Tokenize(source);

        var indents = tokens.Count(t => t.Type == TokenType.Indent);
        var dedents = tokens.Count(t => t.Type == TokenType.Dedent);
        indents.Should().BeGreaterThan(0);
        dedents.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Tokenize_EmptyTripleQuotedString_TokenizesCorrectly()
    {
        var source = "\"\"\"\"\"\"";
        var tokens = Tokenize(source);

        tokens.Should().Contain(t => t.Type == TokenType.String);
    }

    [Fact]
    public void Tokenize_StringWithAllEscapeSequences_TokenizesCorrectly()
    {
        var source = "\"\\n\\t\\r\\\\\\\"\"";
        var tokens = Tokenize(source);

        var stringToken = tokens.Should().ContainSingle(t => t.Type == TokenType.String).Subject;
        stringToken.Value.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Tokenize_UnderscoreInNumbers_TokenizesCorrectly()
    {
        var source = "1_000_000";
        var tokens = Tokenize(source);

        // Lexer may strip underscores during tokenization
        var numberToken = tokens.Should().ContainSingle(t => t.Type == TokenType.Integer).Subject;
        numberToken.Value.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Tokenize_MultipleDecoratorsOnSeparateLines_TokenizesAll()
    {
        var source = "@staticmethod\n@override\ndef method():\n    pass";
        var tokens = Tokenize(source);

        var ats = tokens.Where(t => t.Type == TokenType.At).ToList();
        ats.Should().HaveCount(2);
    }

    [Fact]
    public void Tokenize_AllLogicalOperators_TokenizesCorrectly()
    {
        var source = "a and b or c not d";
        var tokens = Tokenize(source);

        tokens.Should().Contain(t => t.Type == TokenType.And);
        tokens.Should().Contain(t => t.Type == TokenType.Or);
        tokens.Should().Contain(t => t.Type == TokenType.Not);
    }

    #endregion
}
