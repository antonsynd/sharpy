using FluentAssertions;
using LexerNs = Sharpy.Compiler.Lexer;
using TokenType = Sharpy.Compiler.Lexer.TokenType;
#pragma warning disable CS0618 // LexerError is obsolete
using LexerError = Sharpy.Compiler.Lexer.LexerError;
#pragma warning restore CS0618
using Xunit;

namespace Sharpy.Compiler.Tests.Lexer;

/// <summary>
/// Phase 0.1.0 Exit Criteria Tests for the Sharpy Lexer.
/// These tests verify all exit criteria for phase 0.1.0 of the lexer implementation.
/// </summary>
public class Phase010ExitCriteriaTests
{
    private static List<LexerNs.Token> Tokenize(string source)
    {
        var lexer = new LexerNs.Lexer(source);
        return lexer.TokenizeAll();
    }

    private static LexerNs.Token SingleToken(string source)
    {
        var tokens = Tokenize(source);
        tokens.Should().HaveCount(2);
        tokens[1].Type.Should().Be(LexerNs.TokenType.Eof);
        return tokens[0];
    }

    /// <summary>
    /// Tokenize and assert that at least one error was collected.
    /// Returns the diagnostic messages for further assertions.
    /// </summary>
    private static string TokenizeExpectingError(string source)
    {
        var lexer = new LexerNs.Lexer(source);
        lexer.TokenizeAll();
        Assert.True(lexer.Diagnostics.HasErrors, "Expected lexer to report an error for input: " + source);
        return string.Join("\n", lexer.Diagnostics.GetErrors().Select(d => d.Message));
    }

    #region Exit Criteria: All Token Types Recognized

    [Fact]
    public void ExitCriteria_AllTokenTypesRecognized()
    {
        // This test verifies that all token types defined in TokenType enum can be lexed
        var tokenExamples = new Dictionary<TokenType, string>
        {
            // Literals
            { TokenType.Integer, "42" },
            { TokenType.Float, "3.14" },
            { TokenType.String, "\"hello\"" },
            { TokenType.RawString, "r\"raw\"" },
            { TokenType.True, "True" },
            { TokenType.False, "False" },
            { TokenType.None, "None" },

            // Keywords - Control Flow
            { TokenType.Def, "def" },
            { TokenType.Class, "class" },
            { TokenType.Struct, "struct" },
            { TokenType.Interface, "interface" },
            { TokenType.Enum, "enum" },
            { TokenType.If, "if" },
            { TokenType.Else, "else" },
            { TokenType.Elif, "elif" },
            { TokenType.While, "while" },
            { TokenType.For, "for" },
            { TokenType.In, "in" },
            { TokenType.Return, "return" },
            { TokenType.Break, "break" },
            { TokenType.Continue, "continue" },
            { TokenType.Pass, "pass" },
            { TokenType.Try, "try" },
            { TokenType.Except, "except" },
            { TokenType.Finally, "finally" },
            { TokenType.Raise, "raise" },
            { TokenType.Assert, "assert" },
            { TokenType.With, "with" },

            // Keywords - Import
            { TokenType.Import, "import" },
            { TokenType.From, "from" },
            { TokenType.As, "as" },

            // Keywords - Type/Value
            { TokenType.Auto, "auto" },
            { TokenType.Const, "const" },
            { TokenType.Lambda, "lambda" },
            { TokenType.Type, "type" },

            // Keywords - Pattern Matching
            { TokenType.Match, "match" },
            { TokenType.Case, "case" },

            // Keywords - Async
            { TokenType.Async, "async" },
            { TokenType.Await, "await" },
            { TokenType.Yield, "yield" },

            // Keywords - Members
            { TokenType.Property, "property" },
            { TokenType.Event, "event" },

            // Keywords - Other
            { TokenType.Del, "del" },
            { TokenType.To, "to" },
            { TokenType.Maybe, "maybe" },

            // Future Keywords (reserved)
            { TokenType.Defer, "defer" },
            { TokenType.Do, "do" },

            // Boolean operators (keywords)
            { TokenType.And, "and" },
            { TokenType.Or, "or" },
            { TokenType.Not, "not" },
            { TokenType.Is, "is" },

            // Operators - Arithmetic
            { TokenType.Plus, "+" },
            { TokenType.Minus, "-" },
            { TokenType.Star, "*" },
            { TokenType.Slash, "/" },
            { TokenType.DoubleSlash, "//" },
            { TokenType.Percent, "%" },
            { TokenType.DoubleStar, "**" },

            // Operators - Comparison
            { TokenType.Equal, "==" },
            { TokenType.NotEqual, "!=" },
            { TokenType.Less, "<" },
            { TokenType.Greater, ">" },
            { TokenType.LessEqual, "<=" },
            { TokenType.GreaterEqual, ">=" },

            // Operators - Bitwise
            { TokenType.Ampersand, "&" },
            { TokenType.Pipe, "|" },
            { TokenType.Caret, "^" },
            { TokenType.Tilde, "~" },
            { TokenType.LeftShift, "<<" },
            { TokenType.RightShift, ">>" },

            // Operators - Assignment
            { TokenType.Assign, "=" },
            { TokenType.PlusAssign, "+=" },
            { TokenType.MinusAssign, "-=" },
            { TokenType.StarAssign, "*=" },
            { TokenType.SlashAssign, "/=" },
            { TokenType.DoubleSlashAssign, "//=" },
            { TokenType.PercentAssign, "%=" },
            { TokenType.DoubleStarAssign, "**=" },
            { TokenType.AmpersandAssign, "&=" },
            { TokenType.PipeAssign, "|=" },
            { TokenType.CaretAssign, "^=" },
            { TokenType.LeftShiftAssign, "<<=" },
            { TokenType.RightShiftAssign, ">>=" },

            // Operators - Special
            { TokenType.Question, "?" },
            { TokenType.NullConditional, "?." },
            { TokenType.NullCoalesce, "??" },
            { TokenType.Ellipsis, "..." },
            { TokenType.PipeForward, "|>" },

            // Delimiters
            { TokenType.LeftParen, "(" },
            { TokenType.RightParen, ")" },
            { TokenType.LeftBracket, "[" },
            { TokenType.RightBracket, "]" },
            { TokenType.LeftBrace, "{" },
            { TokenType.RightBrace, "}" },
            { TokenType.Comma, "," },
            { TokenType.Colon, ":" },
            { TokenType.Semicolon, ";" },
            { TokenType.Dot, "." },
            { TokenType.Arrow, "->" },
            { TokenType.At, "@" },
            { TokenType.Backslash, "\\" },

            // Identifier
            { TokenType.Identifier, "my_variable" },
        };

        foreach (var (expectedType, source) in tokenExamples)
        {
            var token = SingleToken(source);
            token.Type.Should().Be(expectedType, $"source '{source}' should produce token type {expectedType}");
        }
    }

    [Fact]
    public void ExitCriteria_FStringTokensRecognized()
    {
        // F-string tokens require context, test separately
        var source = "f\"Hello {name}\"";
        var tokens = Tokenize(source);

        tokens.Should().Contain(t => t.Type == TokenType.FStringStart);
        tokens.Should().Contain(t => t.Type == TokenType.FStringText);
        tokens.Should().Contain(t => t.Type == TokenType.FStringExprStart);
        tokens.Should().Contain(t => t.Type == TokenType.FStringExprEnd);
        tokens.Should().Contain(t => t.Type == TokenType.FStringEnd);
    }

    [Fact]
    public void ExitCriteria_FStringFormatSpecRecognized()
    {
        var source = "f\"{value:.2f}\"";
        var tokens = Tokenize(source);

        tokens.Should().Contain(t => t.Type == TokenType.FStringFormatSpec);
    }

    [Fact]
    public void ExitCriteria_SpecialTokensRecognized()
    {
        // Newline, Indent, Dedent require multi-line context
        var source = "if True:\n    pass\nx = 1";
        var tokens = Tokenize(source);

        tokens.Should().Contain(t => t.Type == TokenType.Newline);
        tokens.Should().Contain(t => t.Type == TokenType.Indent);
        tokens.Should().Contain(t => t.Type == TokenType.Dedent);
        tokens.Should().Contain(t => t.Type == TokenType.Eof);
    }

    [Fact]
    public void ExitCriteria_BacktickLiteralNameRecognized()
    {
        var source = "`literal name`";
        var token = SingleToken(source);
        token.Type.Should().Be(TokenType.Identifier);
        token.Value.Should().Be("literal name");
    }

    #endregion

    #region Exit Criteria: Indent/Dedent Emitted Correctly

    [Fact]
    public void ExitCriteria_IndentDedentEmittedCorrectly()
    {
        var source = @"if condition:
    statement1
    if nested:
        statement2
    statement3
final";
        var tokens = Tokenize(source);

        // Should have 2 indents (one for each level)
        tokens.Count(t => t.Type == TokenType.Indent).Should().Be(2);

        // Should have 2 dedents (returning from nested and from block)
        tokens.Count(t => t.Type == TokenType.Dedent).Should().Be(2);
    }

    [Fact]
    public void ExitCriteria_IndentMustBe4Spaces()
    {
        // Valid: 4-space indentation
        var validSource = "if x:\n    pass";
        var tokens = Tokenize(validSource);
        tokens.Should().Contain(t => t.Type == TokenType.Indent);

        // Invalid: 2-space indentation
        var invalidSource = "if x:\n  pass";
        var errors = TokenizeExpectingError(invalidSource);
        errors.Should().Contain("multiple of 4");
    }

    [Fact]
    public void ExitCriteria_TabsNotAllowed()
    {
        var source = "if x:\n\tpass";
        var errors = TokenizeExpectingError(source);
        errors.Should().Contain("Tabs are not allowed");
    }

    [Fact]
    public void ExitCriteria_MultipleDedentsEmittedAtOnce()
    {
        // When dedenting from level 2 to level 0, should emit 2 DEDENTs
        var source = @"if a:
    if b:
        pass
x = 1";
        var tokens = Tokenize(source);

        // Count dedents - should have 2 to go from indent 2 back to 0
        var dedentCount = tokens.Count(t => t.Type == TokenType.Dedent);
        dedentCount.Should().Be(2);
    }

    [Fact]
    public void ExitCriteria_IndentDedentNotEmittedInsideBrackets()
    {
        var source = @"x = [
    1,
    2,
    3
]";
        var tokens = Tokenize(source);

        // No indent/dedent tokens inside brackets (implicit line continuation)
        tokens.Should().NotContain(t => t.Type == TokenType.Indent);
        tokens.Should().NotContain(t => t.Type == TokenType.Dedent);
    }

    [Fact]
    public void ExitCriteria_IndentLevelMismatchDetected()
    {
        // Dedenting to a level that doesn't match any previous indent
        var source = "if x:\n    y = 1\n      z = 2";  // 6 spaces is invalid
        TokenizeExpectingError(source);
    }

    #endregion

    #region Exit Criteria: Numeric Literals with Suffixes

    [Fact]
    public void ExitCriteria_NumericLiteralsWithSuffixes()
    {
        // Integer suffixes
        var integerSuffixes = new[]
        {
            ("42L", "42L"),   // long
            ("42l", "42l"),
            ("42U", "42U"),   // unsigned
            ("42u", "42u"),
            ("42UL", "42UL"), // unsigned long
            ("42ul", "42ul"),
            // Note: LU suffix order is not supported, only UL
        };

        foreach (var (input, expected) in integerSuffixes)
        {
            var token = SingleToken(input);
            token.Type.Should().Be(TokenType.Integer);
            token.Value.Should().Be(expected);
        }

        // Float suffixes
        var floatSuffixes = new[]
        {
            ("3.14f", "3.14f"),   // float
            ("3.14F", "3.14F"),
            ("3.14d", "3.14d"),   // double
            ("3.14D", "3.14D"),
            ("3.14m", "3.14m"),   // decimal
            ("3.14M", "3.14M"),
        };

        foreach (var (input, expected) in floatSuffixes)
        {
            var token = SingleToken(input);
            token.Type.Should().Be(TokenType.Float);
            token.Value.Should().Be(expected);
        }
    }

    [Fact]
    public void ExitCriteria_NumericLiteralsWithUnderscores()
    {
        // Underscores should be preserved or stripped consistently
        var token = SingleToken("1_000_000");
        token.Type.Should().Be(TokenType.Integer);
        // Lexer strips underscores
        token.Value.Should().Be("1000000");

        var floatToken = SingleToken("3.141_592_653");
        floatToken.Type.Should().Be(TokenType.Float);
        floatToken.Value.Should().Be("3.141592653");
    }

    [Fact]
    public void ExitCriteria_HexBinaryOctalLiterals()
    {
        // Hexadecimal
        var hexToken = SingleToken("0xFF");
        hexToken.Type.Should().Be(TokenType.Integer);
        hexToken.Value.Should().StartWith("0x");

        // Binary
        var binToken = SingleToken("0b1010");
        binToken.Type.Should().Be(TokenType.Integer);
        binToken.Value.Should().StartWith("0b");

        // Octal
        var octToken = SingleToken("0o77");
        octToken.Type.Should().Be(TokenType.Integer);
        octToken.Value.Should().StartWith("0o");
    }

    [Fact]
    public void ExitCriteria_ScientificNotation()
    {
        var cases = new[]
        {
            ("1e10", TokenType.Float),
            ("1E10", TokenType.Float),
            ("1e+10", TokenType.Float),
            ("1e-10", TokenType.Float),
            ("3.14e5", TokenType.Float),
        };

        foreach (var (input, expectedType) in cases)
        {
            var token = SingleToken(input);
            token.Type.Should().Be(expectedType);
        }
    }

    [Fact]
    public void ExitCriteria_FloatMustHaveDigitBeforeDecimal()
    {
        // .5 is not a valid float - must have digit before decimal
        TokenizeExpectingError(".5");
    }

    #endregion

    #region Exit Criteria: Complete Keyword Coverage

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
    [InlineData("type", TokenType.Type)]
    [InlineData("match", TokenType.Match)]
    [InlineData("case", TokenType.Case)]
    [InlineData("async", TokenType.Async)]
    [InlineData("await", TokenType.Await)]
    [InlineData("yield", TokenType.Yield)]
    [InlineData("property", TokenType.Property)]
    [InlineData("event", TokenType.Event)]
    [InlineData("del", TokenType.Del)]
    [InlineData("to", TokenType.To)]
    [InlineData("maybe", TokenType.Maybe)]
    [InlineData("defer", TokenType.Defer)]
    [InlineData("do", TokenType.Do)]
    [InlineData("and", TokenType.And)]
    [InlineData("or", TokenType.Or)]
    [InlineData("not", TokenType.Not)]
    [InlineData("is", TokenType.Is)]
    [InlineData("True", TokenType.True)]
    [InlineData("False", TokenType.False)]
    [InlineData("None", TokenType.None)]
    public void ExitCriteria_AllKeywordsRecognized(string keyword, TokenType expectedType)
    {
        var token = SingleToken(keyword);
        token.Type.Should().Be(expectedType);
        token.Value.Should().Be(keyword);
    }

    [Fact]
    public void ExitCriteria_KeywordsCaseSensitive()
    {
        // Python-style keywords are case-sensitive
        // "Def" should be identifier, not keyword
        var token = SingleToken("Def");
        token.Type.Should().Be(TokenType.Identifier);

        var token2 = SingleToken("CLASS");
        token2.Type.Should().Be(TokenType.Identifier);

        // But True/False/None are capitalized
        var trueToken = SingleToken("True");
        trueToken.Type.Should().Be(TokenType.True);

        var falseToken = SingleToken("true");
        falseToken.Type.Should().Be(TokenType.Identifier);  // lowercase is not the keyword
    }

    #endregion

    #region Exit Criteria: String Types

    [Fact]
    public void ExitCriteria_RegularStringsRecognized()
    {
        // Double quotes
        var doubleQuoted = SingleToken("\"hello world\"");
        doubleQuoted.Type.Should().Be(TokenType.String);
        doubleQuoted.Value.Should().Be("hello world");

        // Single quotes
        var singleQuoted = SingleToken("'hello world'");
        singleQuoted.Type.Should().Be(TokenType.String);
        singleQuoted.Value.Should().Be("hello world");
    }

    [Fact]
    public void ExitCriteria_RawStringsRecognized()
    {
        var rawDouble = SingleToken("r\"C:\\path\\to\\file\"");
        rawDouble.Type.Should().Be(TokenType.RawString);
        rawDouble.Value.Should().Be("C:\\path\\to\\file");

        var rawSingle = SingleToken("r'C:\\path\\to\\file'");
        rawSingle.Type.Should().Be(TokenType.RawString);
        rawSingle.Value.Should().Be("C:\\path\\to\\file");
    }

    [Fact]
    public void ExitCriteria_TripleQuotedStringsRecognized()
    {
        var tripleDouble = SingleToken("\"\"\"multi\nline\nstring\"\"\"");
        tripleDouble.Type.Should().Be(TokenType.String);
        tripleDouble.Value.Should().Contain("multi");
        tripleDouble.Value.Should().Contain("line");
        tripleDouble.Value.Should().Contain("string");

        var tripleSingle = SingleToken("'''multi\nline\nstring'''");
        tripleSingle.Type.Should().Be(TokenType.String);
    }

    [Fact]
    public void ExitCriteria_FStringsRecognized()
    {
        var tokens = Tokenize("f\"Hello {name}!\"");

        tokens.Should().Contain(t => t.Type == TokenType.FStringStart);
        tokens.Should().Contain(t => t.Type == TokenType.FStringText && t.Value == "Hello ");
        tokens.Should().Contain(t => t.Type == TokenType.FStringExprStart);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "name");
        tokens.Should().Contain(t => t.Type == TokenType.FStringExprEnd);
        tokens.Should().Contain(t => t.Type == TokenType.FStringText && t.Value == "!");
        tokens.Should().Contain(t => t.Type == TokenType.FStringEnd);
    }

    [Fact]
    public void ExitCriteria_FStringWithFormatSpec()
    {
        var tokens = Tokenize("f\"{value:.2f}\"");

        tokens.Should().Contain(t => t.Type == TokenType.FStringStart);
        tokens.Should().Contain(t => t.Type == TokenType.FStringExprStart);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "value");
        tokens.Should().Contain(t => t.Type == TokenType.FStringFormatSpec && t.Value == ".2f");
        tokens.Should().Contain(t => t.Type == TokenType.FStringExprEnd);
        tokens.Should().Contain(t => t.Type == TokenType.FStringEnd);
    }

    [Fact]
    public void ExitCriteria_EscapeSequencesProcessed()
    {
        var token = SingleToken("\"line1\\nline2\\ttab\"");
        token.Type.Should().Be(TokenType.String);
        token.Value.Should().Be("line1\nline2\ttab");
    }

    [Fact]
    public void ExitCriteria_RawStringNoEscapeProcessing()
    {
        var token = SingleToken("r\"\\n\\t\"");
        token.Type.Should().Be(TokenType.RawString);
        token.Value.Should().Be("\\n\\t");
    }

    #endregion

    #region Exit Criteria: All Operators

    [Theory]
    [InlineData("|>", TokenType.PipeForward)]
    [InlineData("??", TokenType.NullCoalesce)]
    [InlineData("?.", TokenType.NullConditional)]
    public void ExitCriteria_SpecialOperatorsRecognized(string op, TokenType expectedType)
    {
        var token = SingleToken(op);
        token.Type.Should().Be(expectedType);
    }

    [Fact]
    public void ExitCriteria_PipeForwardChaining()
    {
        var source = "data |> transform |> filter |> output";
        var tokens = Tokenize(source);

        tokens.Count(t => t.Type == TokenType.PipeForward).Should().Be(3);
    }

    [Fact]
    public void ExitCriteria_NullOperatorsInContext()
    {
        var source = "result = obj?.method() ?? default";
        var tokens = Tokenize(source);

        tokens.Should().Contain(t => t.Type == TokenType.NullConditional);
        tokens.Should().Contain(t => t.Type == TokenType.NullCoalesce);
    }

    [Fact]
    public void ExitCriteria_AllComparisonOperators()
    {
        var operators = new Dictionary<string, TokenType>
        {
            { "==", TokenType.Equal },
            { "!=", TokenType.NotEqual },
            { "<", TokenType.Less },
            { ">", TokenType.Greater },
            { "<=", TokenType.LessEqual },
            { ">=", TokenType.GreaterEqual },
        };

        foreach (var (op, expectedType) in operators)
        {
            var token = SingleToken(op);
            token.Type.Should().Be(expectedType);
        }
    }

    [Fact]
    public void ExitCriteria_AllArithmeticOperators()
    {
        var operators = new Dictionary<string, TokenType>
        {
            { "+", TokenType.Plus },
            { "-", TokenType.Minus },
            { "*", TokenType.Star },
            { "/", TokenType.Slash },
            { "//", TokenType.DoubleSlash },
            { "%", TokenType.Percent },
            { "**", TokenType.DoubleStar },
        };

        foreach (var (op, expectedType) in operators)
        {
            var token = SingleToken(op);
            token.Type.Should().Be(expectedType);
        }
    }

    [Fact]
    public void ExitCriteria_AllBitwiseOperators()
    {
        var operators = new Dictionary<string, TokenType>
        {
            { "&", TokenType.Ampersand },
            { "|", TokenType.Pipe },
            { "^", TokenType.Caret },
            { "~", TokenType.Tilde },
            { "<<", TokenType.LeftShift },
            { ">>", TokenType.RightShift },
        };

        foreach (var (op, expectedType) in operators)
        {
            var token = SingleToken(op);
            token.Type.Should().Be(expectedType);
        }
    }

    [Fact]
    public void ExitCriteria_AllAssignmentOperators()
    {
        var operators = new Dictionary<string, TokenType>
        {
            { "=", TokenType.Assign },
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
            { ">>=", TokenType.RightShiftAssign },
        };

        foreach (var (op, expectedType) in operators)
        {
            var token = SingleToken(op);
            token.Type.Should().Be(expectedType);
        }
    }

    #endregion

    #region Exit Criteria: Special Characters

    [Fact]
    public void ExitCriteria_CommentsSkipped()
    {
        var source = "x = 1  # This is a comment\ny = 2";
        var tokens = Tokenize(source);

        // Comments should be skipped
        tokens.Should().NotContain(t => t.Type == TokenType.Comment);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "x");
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "y");
    }

    [Fact]
    public void ExitCriteria_LineContinuationWithBackslash()
    {
        var source = "x = 1 + \\\n    2 + 3";
        var tokens = Tokenize(source);

        // Should not have newline token - line continuation joins lines
        tokens.Should().NotContain(t => t.Type == TokenType.Newline);
        tokens.Count(t => t.Type == TokenType.Integer).Should().Be(3);
    }

    [Fact]
    public void ExitCriteria_DecoratorAtSymbol()
    {
        var source = "@decorator\ndef func():\n    pass";
        var tokens = Tokenize(source);

        tokens.Should().Contain(t => t.Type == TokenType.At);
        tokens.Should().Contain(t => t.Type == TokenType.Identifier && t.Value == "decorator");
    }

    [Fact]
    public void ExitCriteria_EllipsisForPlaceholder()
    {
        var source = "def not_implemented():\n    ...";
        var tokens = Tokenize(source);

        tokens.Should().Contain(t => t.Type == TokenType.Ellipsis);
    }

    [Fact]
    public void ExitCriteria_ArrowForReturnType()
    {
        var source = "def func() -> int:\n    pass";
        var tokens = Tokenize(source);

        tokens.Should().Contain(t => t.Type == TokenType.Arrow);
    }

    #endregion

    #region Exit Criteria: Error Handling

    [Fact]
    public void ExitCriteria_UnterminatedStringError()
    {
        var errors = TokenizeExpectingError("\"unterminated string");
        errors.Should().Contain("Unterminated string");
    }

    [Fact]
    public void ExitCriteria_UnterminatedTripleStringError()
    {
        TokenizeExpectingError("\"\"\"unterminated");
    }

    [Fact]
    public void ExitCriteria_InvalidEscapeSequenceError()
    {
        var errors = TokenizeExpectingError("\"invalid\\xescape\"");
        errors.Should().Contain("escape sequence");
    }

    [Fact]
    public void ExitCriteria_UnexpectedCharacterError()
    {
        var errors = TokenizeExpectingError("x $ y");
        errors.Should().Contain("Unexpected character");
    }

    [Fact]
    public void ExitCriteria_InvalidIndentationError()
    {
        var errors = TokenizeExpectingError("if x:\n  pass");  // 2 spaces
        errors.Should().Contain("multiple of 4");
    }

    [Fact]
    public void ExitCriteria_TabIndentationError()
    {
        var errors = TokenizeExpectingError("if x:\n\tpass");
        errors.Should().Contain("Tabs are not allowed");
    }

    [Fact]
    public void ExitCriteria_ErrorIncludesLineAndColumn()
    {
        var source = "x = 1\ny = $";
        var lexer = new LexerNs.Lexer(source);
        lexer.TokenizeAll();
        lexer.Diagnostics.HasErrors.Should().BeTrue("Expected lexer to report an error for input: " + source);
        var errorDiagnostics = lexer.Diagnostics.GetErrors();
        errorDiagnostics.Should().Contain(d => d.Line == 2);
        errorDiagnostics.Should().Contain(d => d.Column != null);
    }

    [Fact]
    public void ExitCriteria_BackslashAtEndOfFileError()
    {
        TokenizeExpectingError("x = 1 \\");
    }

    [Fact]
    public void ExitCriteria_UnterminatedLiteralNameError()
    {
        var errors = TokenizeExpectingError("`unterminated");
        errors.Should().Contain("Unterminated literal name");
    }

    #endregion

    #region Exit Criteria: Position Tracking

    [Fact]
    public void ExitCriteria_PositionTrackingAccurate()
    {
        var source = "x = 42";
        var tokens = Tokenize(source);

        var xToken = tokens.First(t => t.Type == TokenType.Identifier);
        xToken.Line.Should().Be(1);
        xToken.Column.Should().Be(1);

        var assignToken = tokens.First(t => t.Type == TokenType.Assign);
        assignToken.Line.Should().Be(1);
        assignToken.Column.Should().Be(3);

        var intToken = tokens.First(t => t.Type == TokenType.Integer);
        intToken.Line.Should().Be(1);
        intToken.Column.Should().Be(5);
    }

    [Fact]
    public void ExitCriteria_MultiLinePositionTracking()
    {
        var source = "x = 1\ny = 2\nz = 3";
        var tokens = Tokenize(source);

        var identifiers = tokens.Where(t => t.Type == TokenType.Identifier).ToList();

        identifiers[0].Line.Should().Be(1);  // x
        identifiers[1].Line.Should().Be(2);  // y
        identifiers[2].Line.Should().Be(3);  // z
    }

    #endregion

    #region Exit Criteria: Comprehensive Integration

    [Fact]
    public void ExitCriteria_CompleteFunctionDefinition()
    {
        var source = @"@decorator
def greet(name: str, age: int = 0) -> str:
    """"""Greet a person.""""""
    if age > 0:
        return f""Hello {name}, you are {age}!""
    return f""Hello {name}!""";

        var tokens = Tokenize(source);

        // Should contain all expected token types
        tokens.Should().Contain(t => t.Type == TokenType.At);
        tokens.Should().Contain(t => t.Type == TokenType.Def);
        tokens.Should().Contain(t => t.Type == TokenType.Arrow);
        tokens.Should().Contain(t => t.Type == TokenType.String);  // docstring
        tokens.Should().Contain(t => t.Type == TokenType.If);
        tokens.Should().Contain(t => t.Type == TokenType.Return);
        tokens.Should().Contain(t => t.Type == TokenType.FStringStart);
        tokens.Should().Contain(t => t.Type == TokenType.Indent);
        tokens.Should().Contain(t => t.Type == TokenType.Dedent);
    }

    [Fact]
    public void ExitCriteria_CompleteClassDefinition()
    {
        var source = @"class Person:
    name: str
    age: int = 0

    def __init__(self, name: str, age: int = 0) -> None:
        self.name = name
        self.age = age

    def greet(self) -> str:
        return f""Hello, I am {self.name}""";

        var tokens = Tokenize(source);

        tokens.Should().Contain(t => t.Type == TokenType.Class);
        tokens.Should().Contain(t => t.Type == TokenType.Def);
        tokens.Should().Contain(t => t.Type == TokenType.Colon);
        tokens.Should().Contain(t => t.Type == TokenType.Assign);
        tokens.Count(t => t.Type == TokenType.Indent).Should().BeGreaterThan(0);
        tokens.Count(t => t.Type == TokenType.Dedent).Should().BeGreaterThan(0);
    }

    [Fact]
    public void ExitCriteria_MatchStatement()
    {
        var source = @"match value:
    case 1:
        return ""one""
    case 2:
        return ""two""
    case _:
        return ""other""";

        var tokens = Tokenize(source);

        tokens.Should().Contain(t => t.Type == TokenType.Match);
        tokens.Count(t => t.Type == TokenType.Case).Should().Be(3);
    }

    [Fact]
    public void ExitCriteria_NullSafetyPattern()
    {
        var source = "result = obj?.property ?? default_value";
        var tokens = Tokenize(source);

        tokens.Should().Contain(t => t.Type == TokenType.NullConditional);
        tokens.Should().Contain(t => t.Type == TokenType.NullCoalesce);
    }

    [Fact]
    public void ExitCriteria_PipelinePattern()
    {
        var source = @"result = data |> validate() |> transform() |> save()";
        var tokens = Tokenize(source);

        tokens.Count(t => t.Type == TokenType.PipeForward).Should().Be(3);
    }

    [Fact]
    public void ExitCriteria_AsyncAwaitPattern()
    {
        var source = @"async def fetch_data(url: str) -> dict:
    response = await http_get(url)
    return response";

        var tokens = Tokenize(source);

        tokens.Should().Contain(t => t.Type == TokenType.Async);
        tokens.Should().Contain(t => t.Type == TokenType.Await);
    }

    [Fact]
    public void ExitCriteria_GenericTypeSyntax()
    {
        var source = "def process(items: list[dict[str, int]]) -> list[str]:";
        var tokens = Tokenize(source);

        tokens.Count(t => t.Type == TokenType.LeftBracket).Should().Be(3);
        tokens.Count(t => t.Type == TokenType.RightBracket).Should().Be(3);
    }

    [Fact]
    public void ExitCriteria_NullableTypeSyntax()
    {
        var source = "x: int? = None";
        var tokens = Tokenize(source);

        tokens.Should().Contain(t => t.Type == TokenType.Question);
        tokens.Should().Contain(t => t.Type == TokenType.None);
    }

    #endregion
}
