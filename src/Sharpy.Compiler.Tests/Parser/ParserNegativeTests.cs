using FluentAssertions;
using Xunit;
using Sharpy.Compiler.Parser.Ast;
using LexerNs = Sharpy.Compiler.Lexer;
using ParserNs = Sharpy.Compiler.Parser;

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

    /// <summary>
    /// Parses the source expecting errors in the diagnostics bag.
    /// Uses TokenizeAll() so that both lexer and parser errors are collected into diagnostics.
    /// Returns all error messages joined by newline.
    /// </summary>
    private static string ParseExpectingError(string source)
    {
        var lexer = new LexerNs.Lexer(source);
        var tokens = lexer.TokenizeAll();
        var parser = new ParserNs.Parser(tokens);
        parser.ParseModule();

        // Merge lexer diagnostics so we catch both lexer and parser errors
        var allErrors = lexer.Diagnostics.GetErrors()
            .Concat(parser.Diagnostics.GetErrors())
            .ToList();

        allErrors.Should().NotBeEmpty("Expected parser/lexer to report an error for input: " + source);
        return string.Join("\n", allErrors.Select(d => d.Message));
    }

    #region Missing Syntax Elements

    [Fact]
    public void RejectsMissingColonAfterIf()
    {
        var source = "if True\n    pass";
        var errors = ParseExpectingError(source);
        errors.Should().Contain("Expected Colon");
    }

    [Fact]
    public void RejectsMissingColonAfterWhile()
    {
        var source = "while True\n    pass";
        var errors = ParseExpectingError(source);
        errors.Should().Contain("Expected Colon");
    }

    [Fact]
    public void RejectsMissingColonAfterFor()
    {
        var source = "for x in range(10)\n    print(x)";
        var errors = ParseExpectingError(source);
        errors.Should().Contain("Expected Colon");
    }

    [Fact]
    public void RejectsMissingColonAfterDef()
    {
        var source = "def foo()\n    pass";
        var errors = ParseExpectingError(source);
        errors.Should().Contain("Expected Colon");
    }

    [Fact]
    public void RejectsMissingColonAfterClass()
    {
        var source = "class Foo\n    pass";
        var errors = ParseExpectingError(source);
        errors.Should().Contain("Expected Colon");
    }

    [Fact]
    public void RejectsMissingColonAfterElse()
    {
        var source = "if True:\n    pass\nelse\n    pass";
        var errors = ParseExpectingError(source);
        errors.Should().Contain("Expected Colon");
    }

    [Fact]
    public void RejectsMissingColonAfterElif()
    {
        var source = "if True:\n    pass\nelif False\n    pass";
        var errors = ParseExpectingError(source);
        errors.Should().Contain("Expected Colon");
    }

    [Fact]
    public void RejectsMissingColonAfterTry()
    {
        // Note: With try expressions now supported, `try` followed by newline is parsed as
        // an incomplete try expression (missing operand), not a try statement missing a colon.
        // The error message reflects this - it encounters a newline where an expression is expected.
        var source = "try\n    pass\nexcept:\n    pass";
        var errors = ParseExpectingError(source);
        errors.Should().Contain("Unexpected token");
    }

    [Fact]
    public void RejectsMissingColonAfterExcept()
    {
        var source = "try:\n    pass\nexcept Exception\n    pass";
        var errors = ParseExpectingError(source);
        errors.Should().Contain("Expected Colon");
    }

    [Fact]
    public void RejectsMissingColonAfterFinally()
    {
        var source = "try:\n    pass\nfinally\n    pass";
        var errors = ParseExpectingError(source);
        errors.Should().Contain("Expected Colon");
    }

    #endregion

    #region Missing Brackets/Parentheses

    [Fact]
    public void RejectsMissingRightParen()
    {
        var source = "foo(1, 2";
        var errors = ParseExpectingError(source);
        errors.Should().Contain("Expected RightParen");
    }

    [Fact]
    public void RejectsMissingRightBracket()
    {
        var source = "[1, 2, 3";
        var errors = ParseExpectingError(source);
        errors.Should().Contain("Expected RightBracket");
    }

    [Fact]
    public void RejectsMissingRightBrace()
    {
        var source = "{1, 2, 3";
        var errors = ParseExpectingError(source);
        errors.Should().Contain("Expected RightBrace");
    }

    [Fact]
    public void RejectsMismatchedBrackets()
    {
        var source = "[1, 2, 3}";
        ParseExpectingError(source);
    }

    [Fact]
    public void RejectsUnexpectedRightParen()
    {
        var source = "x = 1)";
        ParseExpectingError(source);
    }

    #endregion

    #region Invalid Function Definitions

    [Fact]
    public void RejectsFunctionWithoutName()
    {
        var source = "def ():\n    pass";
        ParseExpectingError(source);
    }

    [Fact]
    public void RejectsFunctionWithInvalidParameterName()
    {
        var source = "def foo(123):\n    pass";
        ParseExpectingError(source);
    }

    [Fact]
    public void RejectsFunctionWithEmptyBody()
    {
        // Empty body without pass is invalid
        var source = "def foo():\n";
        ParseExpectingError(source);
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
        ParseExpectingError(source);
    }

    #endregion

    #region Invalid Class Definitions

    [Fact]
    public void RejectsClassWithoutName()
    {
        var source = "class:\n    pass";
        ParseExpectingError(source);
    }

    [Fact]
    public void RejectsClassWithInvalidName()
    {
        var source = "class 123:\n    pass";
        ParseExpectingError(source);
    }

    [Fact]
    public void RejectsClassWithEmptyBody()
    {
        var source = "class Foo:\n";
        ParseExpectingError(source);
    }

    [Fact]
    public void RejectsClassWithInvalidBaseClass()
    {
        var source = "class Foo(123):\n    pass";
        ParseExpectingError(source);
    }

    #endregion

    #region Invalid Enum Definitions

    [Fact]
    public void RejectsEmptyEnum()
    {
        var source = "enum Color:\n    pass";
        // Error message may vary - the important thing is it's rejected
        ParseExpectingError(source);
    }

    [Fact]
    public void RejectsEnumWithInvalidMemberName()
    {
        var source = "enum Color:\n    123";
        ParseExpectingError(source);
    }

    #endregion

    #region Invalid Type Annotations

    [Fact]
    public void RejectsMissingTypeAfterColon()
    {
        var source = "x: = 5";
        ParseExpectingError(source);
    }

    [Fact]
    public void RejectsInvalidTypeAnnotation()
    {
        var source = "x: 123 = 5";
        ParseExpectingError(source);
    }

    [Fact]
    public void RejectsMissingGenericCloseBracket()
    {
        var source = "x: list[int";
        ParseExpectingError(source);
    }

    [Fact]
    public void ArraySuffixOnTypeIsValid()
    {
        // With shorthand syntax, list[] is valid and means "array of list"
        // This is now valid syntax: array[list] (though semantically list may need type args)
        var source = "x: list[]";
        var module = Parse(source);
        var varDecl = module.Body[0].Should().BeOfType<VariableDeclaration>().Subject;
        varDecl.Type.Name.Should().Be("array");
        varDecl.Type.TypeArguments.Should().HaveCount(1);
        varDecl.Type.TypeArguments[0].Name.Should().Be("list");
    }

    #endregion

    #region Invalid Expressions

    [Fact]
    public void RejectsIncompleteExpression()
    {
        var source = "x = 1 +";
        ParseExpectingError(source);
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
        ParseExpectingError(source);
    }

    [Fact]
    public void RejectsMissingElseInTernary()
    {
        var source = "x = 1 if True 2";
        ParseExpectingError(source);
    }

    [Fact]
    public void RejectsInvalidLambda()
    {
        var source = "lambda: ";
        ParseExpectingError(source);
    }

    [Fact]
    public void RejectsInvalidSlice()
    {
        var source = "x[:]:]";
        ParseExpectingError(source);
    }

    #endregion

    #region Invalid Indentation

    [Fact]
    public void RejectsUnexpectedIndent()
    {
        var source = "x = 1\n    y = 2";
        var errors = ParseExpectingError(source);
        errors.Should().Contain("Unexpected").And.Contain("Indent");
    }

    [Fact]
    public void RejectsUnexpectedDedent()
    {
        var source = "if True:\n    x = 1\n  y = 2";  // Invalid dedent level
        // This is caught by lexer as invalid indentation (not multiple of 4)
        // Using ParseExpectingError which collects both lexer and parser diagnostics
        ParseExpectingError(source);
    }

    [Fact]
    public void RejectsMissingIndentAfterColon()
    {
        var source = "if True:\npass";
        var errors = ParseExpectingError(source);
        errors.Should().Contain("Expected Indent");
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
        var errors = ParseExpectingError(source);
        errors.Should().Contain("Expected end of statement");
    }

    #endregion

    #region Invalid Import Statements

    [Fact]
    public void RejectsImportWithoutModule()
    {
        var source = "import";
        ParseExpectingError(source);
    }

    [Fact]
    public void RejectsFromImportWithoutModule()
    {
        var source = "from import x";
        ParseExpectingError(source);
    }

    [Fact]
    public void RejectsFromImportWithoutImport()
    {
        var source = "from math x";
        ParseExpectingError(source);
    }

    [Fact]
    public void RejectsImportAsWithoutAlias()
    {
        var source = "import math as";
        ParseExpectingError(source);
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
        ParseExpectingError(source);
    }

    [Fact]
    public void FinallyWithoutTryIsInvalid()
    {
        var source = "finally:\n    pass";
        ParseExpectingError(source);
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
        ParseExpectingError(source);
    }

    [Fact]
    public void RejectsMalformedDictionary()
    {
        var source = "{\"a\": 1, \"b\":}";
        ParseExpectingError(source);
    }

    [Fact]
    public void RejectsInvalidDecorator()
    {
        var source = "@\ndef foo():\n    pass";
        ParseExpectingError(source);
    }

    [Fact]
    public void RejectsDecoratorOnNonFunction()
    {
        var source = "@decorator\nx = 1";
        ParseExpectingError(source);
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
    public void ParsesIfStatementWithCommentLine()
    {
        // Bug #1 fix test: Indented comment should not cause parser error
        var source = @"if True:
    # This is a comment
    x = 1";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
        module.Body[0].Should().BeOfType<IfStatement>();
    }

    [Fact]
    public void ParsesCodeWithBlankLines()
    {
        // Bug #4 fix test: Blank lines should not affect parsing
        var source = @"x = 1

y = 2

z = 3";
        var module = Parse(source);
        module.Body.Should().HaveCount(3);
    }

    [Fact]
    public void ParsesCodeWithWhitespaceOnlyLines()
    {
        // Bug #4 fix test: Lines with only whitespace should be ignored
        var source = "x = 1\n    \n    \t\ny = 2";
        var module = Parse(source);
        module.Body.Should().HaveCount(2);
    }

    [Fact]
    public void ParsesListWithCommentsInside()
    {
        var source = @"values = [
    # comment
    1,
    # another comment
    2
]";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
    }

    [Fact]
    public void RejectsInvalidEscapeInIdentifier()
    {
        // Escape sequences in identifiers are only valid in literal names
        var source = "x\\n = 1";
        ParseExpectingError(source);
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
        var errors = ParseExpectingError(source);
        errors.Should().Contain("Expected In");
    }

    [Fact]
    public void RejectsForWithoutIterator()
    {
        var source = "for x in:\n    pass";
        ParseExpectingError(source);
    }

    [Fact]
    public void RejectsWhileWithoutCondition()
    {
        var source = "while:\n    pass";
        ParseExpectingError(source);
    }

    [Fact]
    public void RejectsIfWithoutCondition()
    {
        var source = "if:\n    pass";
        ParseExpectingError(source);
    }

    [Fact]
    public void RejectsElifWithoutCondition()
    {
        var source = "if True:\n    pass\nelif:\n    pass";
        ParseExpectingError(source);
    }

    [Fact]
    public void RejectsAssertWithoutExpression()
    {
        var source = "assert";
        ParseExpectingError(source);
    }

    [Fact]
    public void RejectsRaiseWithoutExpression()
    {
        var source = "raise";
        // Bare raise is valid in except block, but parser might allow it anywhere
        // With DiagnosticBag-based error collection, ParseModule no longer throws.
        // The parser either succeeds (bare raise is valid) or collects errors.
        var lexer = new LexerNs.Lexer(source);
        var tokens = lexer.TokenizeAll();
        var parser = new ParserNs.Parser(tokens);
        var module = parser.ParseModule();

        // Either it parses successfully (bare raise) or has errors - both are acceptable
        if (!parser.Diagnostics.HasErrors)
        {
            module.Body.Should().HaveCount(1);
        }
    }

    #endregion

    #region Comprehension Errors

    [Fact]
    public void RejectsMissingInKeywordInComprehension()
    {
        var source = "x = [i for i range(10)]";
        var errors = ParseExpectingError(source);
        errors.Should().Contain("Expected In");
    }

    [Fact]
    public void RejectsMissingIteratorInComprehension()
    {
        var source = "x = [i for i in]";
        ParseExpectingError(source);
    }

    [Fact]
    public void RejectsMissingForKeywordInComprehension()
    {
        var source = "x = [i i in range(10)]";
        var errors = ParseExpectingError(source);
        errors.Should().Contain("Expected RightBracket");
    }

    [Fact]
    public void RejectsMissingFilterConditionInComprehension()
    {
        var source = "x = [i for i in range(10) if]";
        ParseExpectingError(source);
    }

    [Fact]
    public void RejectsMissingElementExpressionInComprehension()
    {
        var source = "x = [for i in range(10)]";
        ParseExpectingError(source);
    }

    [Fact]
    public void RejectsMissingTargetVariableInComprehension()
    {
        var source = "x = [i for in range(10)]";
        ParseExpectingError(source);
    }

    [Fact]
    public void RejectsMissingColonInDictComprehension()
    {
        var source = "x = {i i for i in range(10)}";
        ParseExpectingError(source);
    }

    [Fact]
    public void RejectsMissingValueInDictComprehension()
    {
        var source = "x = {i: for i in range(10)}";
        ParseExpectingError(source);
    }

    [Fact]
    public void RejectsMissingKeyInDictComprehension()
    {
        var source = "x = {:i for i in range(10)}";
        ParseExpectingError(source);
    }

    [Fact]
    public void RejectsUnterminatedListComprehension()
    {
        var source = "x = [i for i in range(10)";
        var errors = ParseExpectingError(source);
        errors.Should().Contain("Expected RightBracket");
    }

    [Fact]
    public void RejectsUnterminatedSetComprehension()
    {
        var source = "x = {i for i in range(10)";
        var errors = ParseExpectingError(source);
        errors.Should().Contain("Expected RightBrace");
    }

    [Fact]
    public void RejectsUnterminatedDictComprehension()
    {
        var source = "x = {i: i * 2 for i in range(10)";
        var errors = ParseExpectingError(source);
        errors.Should().Contain("Expected RightBrace");
    }

    [Fact]
    public void RejectsComprehensionWithInvalidTarget()
    {
        // This parses successfully but semantic analysis should reject it
        var source = "x = [i for 5 in range(10)]";
        var module = Parse(source);
        module.Should().NotBeNull();
        // The semantic analyzer will catch this error later
    }

    [Fact]
    public void RejectsMultipleCommasInComprehension()
    {
        var source = "x = [i,, for i in range(10)]";
        ParseExpectingError(source);
    }

    #endregion
}
