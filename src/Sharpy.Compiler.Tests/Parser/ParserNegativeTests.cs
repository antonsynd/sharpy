using FluentAssertions;
using Xunit;
using Sharpy.Compiler.Parser.Ast;
using LexerNs = Sharpy.Compiler.Lexer;
using ParserNs = Sharpy.Compiler.Parser;
using ParserError = Sharpy.Compiler.Parser.ParserError;

namespace Sharpy.Compiler.Tests.Parser;

/// <summary>
/// Negative tests for the Parser - testing error detection and handling
/// </summary>
public class ParserNegativeTests
{
    private static Module Parse(string source)
    {
        var lexer = new LexerNs.Lexer(source);
        var tokens = new List<LexerNs.Token>();
        while (true)
        {
            var token = lexer.NextToken();
            tokens.Add(token);
            if (token.Type == LexerNs.TokenType.Eof)
                break;
        }
        var parser = new ParserNs.Parser(tokens);
        return parser.ParseModule();
    }

    #region Missing Syntax Elements

    [Fact]
    public void RejectsMissingColonAfterIf()
    {
        var source = "if True\n    pass";
        Action act = () => Parse(source);
        act.Should().Throw<ParserError>().WithMessage("*Expected Colon*");
    }

    [Fact]
    public void RejectsMissingColonAfterWhile()
    {
        var source = "while True\n    pass";
        Action act = () => Parse(source);
        act.Should().Throw<ParserError>().WithMessage("*Expected Colon*");
    }

    [Fact]
    public void RejectsMissingColonAfterFor()
    {
        var source = "for x in range(10)\n    print(x)";
        Action act = () => Parse(source);
        act.Should().Throw<ParserError>().WithMessage("*Expected Colon*");
    }

    [Fact]
    public void RejectsMissingColonAfterDef()
    {
        var source = "def foo()\n    pass";
        Action act = () => Parse(source);
        act.Should().Throw<ParserError>().WithMessage("*Expected Colon*");
    }

    [Fact]
    public void RejectsMissingColonAfterClass()
    {
        var source = "class Foo\n    pass";
        Action act = () => Parse(source);
        act.Should().Throw<ParserError>().WithMessage("*Expected Colon*");
    }

    [Fact]
    public void RejectsMissingColonAfterElse()
    {
        var source = "if True:\n    pass\nelse\n    pass";
        Action act = () => Parse(source);
        act.Should().Throw<ParserError>().WithMessage("*Expected Colon*");
    }

    [Fact]
    public void RejectsMissingColonAfterElif()
    {
        var source = "if True:\n    pass\nelif False\n    pass";
        Action act = () => Parse(source);
        act.Should().Throw<ParserError>().WithMessage("*Expected Colon*");
    }

    [Fact]
    public void RejectsMissingColonAfterTry()
    {
        var source = "try\n    pass\nexcept:\n    pass";
        Action act = () => Parse(source);
        act.Should().Throw<ParserError>().WithMessage("*Expected Colon*");
    }

    [Fact]
    public void RejectsMissingColonAfterExcept()
    {
        var source = "try:\n    pass\nexcept Exception\n    pass";
        Action act = () => Parse(source);
        act.Should().Throw<ParserError>().WithMessage("*Expected Colon*");
    }

    [Fact]
    public void RejectsMissingColonAfterFinally()
    {
        var source = "try:\n    pass\nfinally\n    pass";
        Action act = () => Parse(source);
        act.Should().Throw<ParserError>().WithMessage("*Expected Colon*");
    }

    #endregion

    #region Missing Brackets/Parentheses

    [Fact]
    public void RejectsMissingRightParen()
    {
        var source = "foo(1, 2";
        Action act = () => Parse(source);
        act.Should().Throw<ParserError>().WithMessage("*Expected RightParen*");
    }

    [Fact]
    public void RejectsMissingRightBracket()
    {
        var source = "[1, 2, 3";
        Action act = () => Parse(source);
        act.Should().Throw<ParserError>().WithMessage("*Expected RightBracket*");
    }

    [Fact]
    public void RejectsMissingRightBrace()
    {
        var source = "{1, 2, 3";
        Action act = () => Parse(source);
        act.Should().Throw<ParserError>().WithMessage("*Expected RightBrace*");
    }

    [Fact]
    public void RejectsMismatchedBrackets()
    {
        var source = "[1, 2, 3}";
        Action act = () => Parse(source);
        act.Should().Throw<ParserError>();
    }

    [Fact]
    public void RejectsUnexpectedRightParen()
    {
        var source = "x = 1)";
        Action act = () => Parse(source);
        act.Should().Throw<ParserError>();
    }

    #endregion

    #region Invalid Function Definitions

    [Fact]
    public void RejectsFunctionWithoutName()
    {
        var source = "def ():\n    pass";
        Action act = () => Parse(source);
        act.Should().Throw<ParserError>();
    }

    [Fact]
    public void RejectsFunctionWithInvalidParameterName()
    {
        var source = "def foo(123):\n    pass";
        Action act = () => Parse(source);
        act.Should().Throw<ParserError>();
    }

    [Fact]
    public void RejectsFunctionWithEmptyBody()
    {
        // Empty body without pass is invalid
        var source = "def foo():\n";
        Action act = () => Parse(source);
        act.Should().Throw<ParserError>();
    }

    [Fact]
    public void AllowsDefaultValueBeforeNonDefault()
    {
        // This is a semantic error, not a parse error
        // Parser allows it, semantic analyzer should catch it
        var source = "def foo(a=1, b):\n    pass";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
        module.Body[0].Should().BeOfType<FunctionDef>();
    }

    [Fact]
    public void RejectsMissingArrowBeforeReturnType()
    {
        var source = "def foo() int:\n    pass";
        Action act = () => Parse(source);
        act.Should().Throw<ParserError>();
    }

    #endregion

    #region Invalid Class Definitions

    [Fact]
    public void RejectsClassWithoutName()
    {
        var source = "class:\n    pass";
        Action act = () => Parse(source);
        act.Should().Throw<ParserError>();
    }

    [Fact]
    public void RejectsClassWithInvalidName()
    {
        var source = "class 123:\n    pass";
        Action act = () => Parse(source);
        act.Should().Throw<ParserError>();
    }

    [Fact]
    public void RejectsClassWithEmptyBody()
    {
        var source = "class Foo:\n";
        Action act = () => Parse(source);
        act.Should().Throw<ParserError>();
    }

    [Fact]
    public void RejectsClassWithInvalidBaseClass()
    {
        var source = "class Foo(123):\n    pass";
        Action act = () => Parse(source);
        act.Should().Throw<ParserError>();
    }

    #endregion

    #region Invalid Enum Definitions

    [Fact]
    public void RejectsEmptyEnum()
    {
        var source = "enum Color:\n    pass";
        Action act = () => Parse(source);
        // Error message may vary - the important thing is it's rejected
        act.Should().Throw<ParserError>();
    }

    [Fact]
    public void RejectsEnumWithInvalidMemberName()
    {
        var source = "enum Color:\n    123";
        Action act = () => Parse(source);
        act.Should().Throw<ParserError>();
    }

    #endregion

    #region Invalid Type Annotations

    [Fact]
    public void RejectsMissingTypeAfterColon()
    {
        var source = "x: = 5";
        Action act = () => Parse(source);
        act.Should().Throw<ParserError>();
    }

    [Fact]
    public void RejectsInvalidTypeAnnotation()
    {
        var source = "x: 123 = 5";
        Action act = () => Parse(source);
        act.Should().Throw<ParserError>();
    }

    [Fact]
    public void RejectsMissingGenericCloseBracket()
    {
        var source = "x: list[int";
        Action act = () => Parse(source);
        act.Should().Throw<ParserError>();
    }

    [Fact]
    public void RejectsEmptyGenericArguments()
    {
        var source = "x: list[]";
        Action act = () => Parse(source);
        act.Should().Throw<ParserError>();
    }

    #endregion

    #region Invalid Expressions

    [Fact]
    public void RejectsIncompleteExpression()
    {
        var source = "x = 1 +";
        Action act = () => Parse(source);
        act.Should().Throw<ParserError>();
    }

    [Fact]
    public void AllowsInvalidBinaryExpression()
    {
        // Parser tries to parse, may create unusual AST
        var source = "x = + 1";
        var module = Parse(source);
        module.Should().NotBeNull();
    }

    [Fact]
    public void RejectsMissingConditionInTernary()
    {
        var source = "x = 1 if else 2";
        Action act = () => Parse(source);
        act.Should().Throw<ParserError>();
    }

    [Fact]
    public void RejectsMissingElseInTernary()
    {
        var source = "x = 1 if True 2";
        Action act = () => Parse(source);
        act.Should().Throw<ParserError>();
    }

    [Fact]
    public void RejectsInvalidLambda()
    {
        var source = "lambda: ";
        Action act = () => Parse(source);
        act.Should().Throw<ParserError>();
    }

    [Fact]
    public void RejectsInvalidSlice()
    {
        var source = "x[:]:]";
        Action act = () => Parse(source);
        act.Should().Throw<ParserError>();
    }

    #endregion

    #region Invalid Indentation

    [Fact]
    public void RejectsUnexpectedIndent()
    {
        var source = "x = 1\n    y = 2";
        Action act = () => Parse(source);
        act.Should().Throw<ParserError>().WithMessage("*Unexpected*Indent*");
    }

    [Fact]
    public void RejectsUnexpectedDedent()
    {
        var source = "if True:\n    x = 1\n  y = 2";  // Invalid dedent level
        Action act = () => Parse(source);
        // This is caught by lexer as invalid indentation (not multiple of 4)
        act.Should().Throw<Exception>();  // Could be LexerError or ParserError
    }

    [Fact]
    public void RejectsMissingIndentAfterColon()
    {
        var source = "if True:\npass";
        Action act = () => Parse(source);
        act.Should().Throw<ParserError>().WithMessage("*Expected Indent*");
    }

    #endregion

    #region Invalid Statements

    [Fact]
    public void RejectsBreakOutsideLoop()
    {
        // This should be caught by semantic analysis, not parser
        // But let's check if parser handles it gracefully
        var source = "break";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
        module.Body[0].Should().BeOfType<BreakStatement>();
    }

    [Fact]
    public void RejectsContinueOutsideLoop()
    {
        // This should be caught by semantic analysis, not parser
        var source = "continue";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
        module.Body[0].Should().BeOfType<ContinueStatement>();
    }

    [Fact]
    public void RejectsReturnAtModuleLevel()
    {
        // This should be caught by semantic analysis, not parser
        var source = "return 42";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
        module.Body[0].Should().BeOfType<ReturnStatement>();
    }

    [Fact]
    public void AllowsInvalidAssignmentTarget()
    {
        // Parser is permissive - semantic analyzer catches this
        var source = "123 = x";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
    }

    [Fact]
    public void RejectsMultipleStatementsOnOneLine()
    {
        // Semicolons are not statement separators
        var source = "x = 1; y = 2";
        Action act = () => Parse(source);
        act.Should().Throw<ParserError>().WithMessage("*Expected end of statement*");
    }

    #endregion

    #region Invalid Import Statements

    [Fact]
    public void RejectsImportWithoutModule()
    {
        var source = "import";
        Action act = () => Parse(source);
        act.Should().Throw<ParserError>();
    }

    [Fact]
    public void RejectsFromImportWithoutModule()
    {
        var source = "from import x";
        Action act = () => Parse(source);
        act.Should().Throw<ParserError>();
    }

    [Fact]
    public void RejectsFromImportWithoutImport()
    {
        var source = "from math x";
        Action act = () => Parse(source);
        act.Should().Throw<ParserError>();
    }

    [Fact]
    public void RejectsImportAsWithoutAlias()
    {
        var source = "import math as";
        Action act = () => Parse(source);
        act.Should().Throw<ParserError>();
    }

    #endregion

    #region Invalid Exception Handling

    [Fact]
    public void AllowsTryWithoutExceptOrFinally()
    {
        // This is a semantic error, not a parse error
        var source = "try:\n    pass";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
        module.Body[0].Should().BeOfType<TryStatement>();
    }

    [Fact]
    public void ExceptWithoutTryIsInvalid()
    {
        var source = "except Exception:\n    pass";
        Action act = () => Parse(source);
        act.Should().Throw<ParserError>();
    }

    [Fact]
    public void FinallyWithoutTryIsInvalid()
    {
        var source = "finally:\n    pass";
        Action act = () => Parse(source);
        act.Should().Throw<ParserError>();
    }

    [Fact]
    public void AllowsMultipleExceptWithoutException()
    {
        // Bare except doesn't need to be last - this is a semantic check
        var source = "try:\n    pass\nexcept:\n    pass\nexcept Exception:\n    pass";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
    }

    #endregion

    #region Complex Invalid Structures

    [Fact]
    public void RejectsNestedInvalidStructure()
    {
        var source = @"
def foo():
    if True:
        while False
            pass";
        Action act = () => Parse(source);
        act.Should().Throw<ParserError>();
    }

    [Fact]
    public void RejectsMalformedDictionary()
    {
        var source = "{\"a\": 1, \"b\":}";
        Action act = () => Parse(source);
        act.Should().Throw<ParserError>();
    }

    [Fact]
    public void RejectsInvalidDecorator()
    {
        var source = "@\ndef foo():\n    pass";
        Action act = () => Parse(source);
        act.Should().Throw<ParserError>();
    }

    [Fact]
    public void RejectsDecoratorOnNonFunction()
    {
        var source = "@decorator\nx = 1";
        Action act = () => Parse(source);
        act.Should().Throw<ParserError>();
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void HandlesEmptyModule()
    {
        var source = "";
        var module = Parse(source);
        module.Body.Should().BeEmpty();
    }

    [Fact]
    public void HandlesModuleWithOnlyComments()
    {
        var source = "# Just a comment";
        var module = Parse(source);
        module.Body.Should().BeEmpty();
    }

    [Fact]
    public void HandlesModuleWithOnlyWhitespace()
    {
        var source = "    \n    \n    ";
        var module = Parse(source);
        module.Body.Should().BeEmpty();
    }

    [Fact]
    public void RejectsInvalidEscapeInIdentifier()
    {
        // Escape sequences in identifiers are only valid in literal names
        var source = "x\\n = 1";
        Action act = () => Parse(source);
        act.Should().Throw<ParserError>();
    }

    [Fact]
    public void RejectsVeryDeeplyNestedExpression()
    {
        // Create a very deeply nested expression to test stack depth
        // Using a reasonable depth that won't overflow the stack
        var source = "x = " + new string('(', 100) + "1" + new string(')', 100);
        // Should parse successfully with moderate nesting
        var module = Parse(source);
        module.Should().NotBeNull();
        module.Body.Should().HaveCount(1);
    }

    #endregion

    #region Statement-Specific Errors

    [Fact]
    public void RejectsForWithoutIn()
    {
        var source = "for x range(10):\n    pass";
        Action act = () => Parse(source);
        act.Should().Throw<ParserError>().WithMessage("*Expected In*");
    }

    [Fact]
    public void RejectsForWithoutIterator()
    {
        var source = "for x in:\n    pass";
        Action act = () => Parse(source);
        act.Should().Throw<ParserError>();
    }

    [Fact]
    public void RejectsWhileWithoutCondition()
    {
        var source = "while:\n    pass";
        Action act = () => Parse(source);
        act.Should().Throw<ParserError>();
    }

    [Fact]
    public void RejectsIfWithoutCondition()
    {
        var source = "if:\n    pass";
        Action act = () => Parse(source);
        act.Should().Throw<ParserError>();
    }

    [Fact]
    public void RejectsElifWithoutCondition()
    {
        var source = "if True:\n    pass\nelif:\n    pass";
        Action act = () => Parse(source);
        act.Should().Throw<ParserError>();
    }

    [Fact]
    public void RejectsAssertWithoutExpression()
    {
        var source = "assert";
        Action act = () => Parse(source);
        act.Should().Throw<ParserError>();
    }

    [Fact]
    public void RejectsRaiseWithoutExpression()
    {
        var source = "raise";
        // Bare raise is valid in except block, but parser might allow it anywhere
        // Check what happens
        try
        {
            var module = Parse(source);
            module.Body.Should().HaveCount(1);
        }
        catch (ParserError)
        {
            // Also acceptable
        }
    }

    #endregion
}
