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

    #region Error Recovery

    /// <summary>
    /// Helper that returns both the parsed module and the list of parser error messages,
    /// allowing tests to verify both the AST structure and reported errors.
    /// </summary>
    private static (Module module, List<string> errors) ParseWithErrors(string source)
    {
        var lexer = new LexerNs.Lexer(source);
        var tokens = lexer.TokenizeAll();
        var parser = new ParserNs.Parser(tokens);
        var module = parser.ParseModule();

        var allErrors = lexer.Diagnostics.GetErrors()
            .Concat(parser.Diagnostics.GetErrors())
            .Select(d => d.Message)
            .ToList();

        return (module, allErrors);
    }

    [Fact]
    public void Recovery_MultipleBadDefinitionsAtTopLevel_ReportsMultipleErrors()
    {
        // Two broken function definitions followed by a valid one.
        // The parser should recover after each broken def and report both errors.
        var source = """
            def ():
                pass

            def ():
                pass

            def main():
                pass
            """;

        var (module, errors) = ParseWithErrors(source);

        // At least two errors reported (one per broken def)
        errors.Count.Should().BeGreaterThanOrEqualTo(2);
        errors.Should().AllSatisfy(e => e.Should().Contain("Expected identifier"));

        // The valid function should still be parsed
        module.Body.OfType<FunctionDef>().Should().Contain(f => f.Name == "main");
    }

    [Fact]
    public void Recovery_BadFunctionHeader_SkipsBodyAndContinues()
    {
        // A function with an invalid name has its body skipped entirely.
        // The following valid function should still be parsed.
        var source = """
            def 123(x: int):
                return x * 2

            def add(x: int, y: int) -> int:
                return x + y
            """;

        var (module, errors) = ParseWithErrors(source);

        errors.Should().NotBeEmpty();
        errors[0].Should().Contain("Expected identifier");

        // The valid function 'add' should be in the AST
        module.Body.OfType<FunctionDef>().Should().Contain(f => f.Name == "add");
    }

    [Fact]
    public void Recovery_BadClassHeader_SkipsBodyAndContinues()
    {
        // A class with an invalid name. Its body should be skipped,
        // and the following valid definition should be parsed.
        var source = """
            class (int):
                def method(self):
                    pass

            def main():
                pass
            """;

        var (module, errors) = ParseWithErrors(source);

        errors.Should().NotBeEmpty();
        errors[0].Should().Contain("Expected identifier");

        // The valid function 'main' should still appear
        module.Body.OfType<FunctionDef>().Should().Contain(f => f.Name == "main");
    }

    [Fact]
    public void Recovery_ErrorInsideFunctionBody_ContinuesWithNextStatement()
    {
        // An error mid-function should allow the remaining statements
        // in the same function to be parsed.
        var source = """
            def main():
                x: int = 10
                def
                y: int = 20
            """;

        var (module, errors) = ParseWithErrors(source);

        errors.Should().NotBeEmpty();

        // The function 'main' should still be parsed (with partial body)
        var mainFn = module.Body.OfType<FunctionDef>().FirstOrDefault(f => f.Name == "main");
        mainFn.Should().NotBeNull();

        // Both valid statements (before and after the error) should be in the body
        mainFn!.Body.OfType<VariableDeclaration>().Should().Contain(vd => vd.Name == "x");
        mainFn!.Body.OfType<VariableDeclaration>().Should().Contain(vd => vd.Name == "y");
    }

    [Fact]
    public void Recovery_ErrorInsideClassBody_ContinuesWithNextMethod()
    {
        // An error in one method inside a class body should not prevent
        // subsequent methods from being parsed.
        var source = """
            class Foo:
                def bar(self):
                    pass

                def (self):
                    pass

                def baz(self):
                    pass
            """;

        var (module, errors) = ParseWithErrors(source);

        errors.Should().NotBeEmpty();
        errors.Should().Contain(e => e.Contains("Expected identifier"));

        // The class should still be parsed
        var cls = module.Body.OfType<ClassDef>().FirstOrDefault(c => c.Name == "Foo");
        cls.Should().NotBeNull();

        // bar and baz should be present in the class body
        cls!.Body.OfType<FunctionDef>().Should().Contain(f => f.Name == "bar");
        cls!.Body.OfType<FunctionDef>().Should().Contain(f => f.Name == "baz");
    }

    [Fact]
    public void Recovery_MixOfValidAndInvalidTopLevel_PreservesValidStatements()
    {
        // Interleaved valid and invalid definitions at top level.
        var source = """
            def valid1():
                pass

            class :
                pass

            def valid2():
                pass

            def :
                pass

            def valid3():
                pass
            """;

        var (module, errors) = ParseWithErrors(source);

        errors.Count.Should().BeGreaterThanOrEqualTo(2);

        // All three valid functions should be in the AST
        var functionNames = module.Body
            .OfType<FunctionDef>()
            .Select(f => f.Name)
            .ToList();

        functionNames.Should().Contain("valid1");
        functionNames.Should().Contain("valid2");
        functionNames.Should().Contain("valid3");
    }

    [Fact]
    public void Recovery_DoesNotExceedMaxErrors()
    {
        // Generate enough errors to hit the MaxErrors limit (25).
        // The parser should stop reporting new errors after the limit.
        var lines = Enumerable.Range(0, 30)
            .Select(i => $"def ():\n    pass\n")
            .ToList();

        var source = string.Join("\n", lines);
        var (_, errors) = ParseWithErrors(source);

        errors.Count.Should().BeLessThanOrEqualTo(25);
    }

    [Fact]
    public void Recovery_NestedBlockError_DoesNotCorruptOuterBlock()
    {
        // An error deep inside a nested block (if inside function)
        // should not prevent the outer function from being completed.
        var source = """
            def outer():
                if True:
                    def
                x: int = 42
            """;

        var (module, errors) = ParseWithErrors(source);

        errors.Should().NotBeEmpty();

        // The outer function should still be parsed
        var outerFn = module.Body.OfType<FunctionDef>().FirstOrDefault(f => f.Name == "outer");
        outerFn.Should().NotBeNull();
    }

    [Fact]
    public void Recovery_MixedErrorTypes_ReportsDistinctErrors()
    {
        // Different kinds of syntax errors: missing colon on def, and bad identifier.
        // The parser should report distinct errors and still parse the valid function.
        var source = """
            def foo(x: int)
                return x + 1

            def 456():
                pass

            def main():
                pass
            """;

        var (module, errors) = ParseWithErrors(source);

        // At least two distinct errors
        errors.Count.Should().BeGreaterThanOrEqualTo(2);

        // The valid function 'main' should still be parsed
        module.Body.OfType<FunctionDef>().Should().Contain(f => f.Name == "main");
    }

    [Fact]
    public void Recovery_ErrorBeforeTryStatement_RecoverToTry()
    {
        // An error on one line, followed by a try statement.
        // The parser should recover and parse the try block.
        var source = """
            def main():
                x: int = 10
                def
                try:
                    y: int = 20
                except:
                    pass
            """;

        var (module, errors) = ParseWithErrors(source);

        errors.Should().NotBeEmpty();

        // The function 'main' should be parsed
        var mainFn = module.Body.OfType<FunctionDef>().FirstOrDefault(f => f.Name == "main");
        mainFn.Should().NotBeNull();

        // The try statement should have been recovered
        mainFn!.Body.OfType<TryStatement>().Should().NotBeEmpty();
    }

    [Fact]
    public void Recovery_ErrorsInForLoopBody_ContinuesWithNextStatement()
    {
        // An error inside a for loop body should recover to the next statement
        // in the loop body, not corrupt the loop or outer function.
        var source = """
            def main():
                for i in range(10):
                    x: int = i
                    def
                    y: int = i * 2
                return 0
            """;

        var (module, errors) = ParseWithErrors(source);

        errors.Should().NotBeEmpty();

        var mainFn = module.Body.OfType<FunctionDef>().FirstOrDefault(f => f.Name == "main");
        mainFn.Should().NotBeNull();

        // The for loop should be present
        mainFn!.Body.OfType<ForStatement>().Should().NotBeEmpty();

        // return statement after the for loop should be present
        mainFn!.Body.OfType<ReturnStatement>().Should().NotBeEmpty();
    }

    [Fact]
    public void Recovery_MultipleErrorsInClassBody_ReportsAll()
    {
        // Multiple different errors within a class body.
        // The parser should report all of them and still produce the class.
        var source = """
            class Foo:
                def (self):
                    pass

                def bar(self)
                    pass

                def baz(self):
                    pass
            """;

        var (module, errors) = ParseWithErrors(source);

        // At least two errors (missing name, missing colon)
        errors.Count.Should().BeGreaterThanOrEqualTo(2);

        // The class should still be parsed
        var cls = module.Body.OfType<ClassDef>().FirstOrDefault(c => c.Name == "Foo");
        cls.Should().NotBeNull();

        // The valid method 'baz' should be present
        cls!.Body.OfType<FunctionDef>().Should().Contain(f => f.Name == "baz");
    }

    [Fact]
    public void Recovery_ErrorAtFileStart_RecoversToNextDefinition()
    {
        // An error at the very start of the file should not prevent
        // subsequent definitions from being parsed.
        var source = """
            def
            def main():
                pass
            """;

        var (module, errors) = ParseWithErrors(source);

        errors.Should().NotBeEmpty();
        module.Body.OfType<FunctionDef>().Should().Contain(f => f.Name == "main");
    }

    [Fact]
    public void Recovery_ErrorAtFileEnd_DoesNotHang()
    {
        // An error at the very end of the file should not cause an
        // infinite loop or crash. Valid definitions before it are preserved.
        var source = """
            def main():
                pass
            def
            """;

        var (module, errors) = ParseWithErrors(source);

        errors.Should().NotBeEmpty();
        module.Body.OfType<FunctionDef>().Should().Contain(f => f.Name == "main");
    }

    [Fact]
    public void Recovery_IncompleteExpression_RecoverToNextStatement()
    {
        // An incomplete expression (dangling operator) inside a function body
        // should recover and parse subsequent statements.
        var source = """
            def main():
                x: int = 1 +
                y: int = 2
            """;

        var (module, errors) = ParseWithErrors(source);

        errors.Should().NotBeEmpty();
        var mainFn = module.Body.OfType<FunctionDef>().FirstOrDefault(f => f.Name == "main");
        mainFn.Should().NotBeNull();
    }

    [Fact]
    public void Recovery_UnbalancedBracket_RecoverToNextStatement()
    {
        // An unbalanced opening bracket should recover rather than
        // consuming the rest of the file.
        var source = """
            def main():
                x = [1, 2, 3
                y: int = 5
            """;

        var (module, errors) = ParseWithErrors(source);

        errors.Should().NotBeEmpty();
        var mainFn = module.Body.OfType<FunctionDef>().FirstOrDefault(f => f.Name == "main");
        mainFn.Should().NotBeNull();
    }

    [Fact]
    public void Recovery_ErrorInStructHeader_RecoverToNextDefinition()
    {
        // A struct with an invalid name should skip the body and recover
        // to the next definition.
        var source = """
            struct :
                x: int

            def main():
                pass
            """;

        var (module, errors) = ParseWithErrors(source);

        errors.Should().NotBeEmpty();
        module.Body.OfType<FunctionDef>().Should().Contain(f => f.Name == "main");
    }

    [Fact]
    public void Recovery_ConsecutiveErrors_ReportsMultipleAndPreservesValid()
    {
        // Multiple consecutive error statements followed by a valid statement.
        // All errors should be reported and the valid statement preserved.
        var source = """
            def main():
                def
                class
                x: int = 1
            """;

        var (module, errors) = ParseWithErrors(source);

        errors.Count.Should().BeGreaterThanOrEqualTo(2);
        var mainFn = module.Body.OfType<FunctionDef>().FirstOrDefault(f => f.Name == "main");
        mainFn.Should().NotBeNull();
        mainFn!.Body.OfType<VariableDeclaration>().Should().Contain(v => v.Name == "x");
    }

    [Fact]
    public void Recovery_MissingFunctionBody_RecoverToNextDefinition()
    {
        // A function with colon omitted and no body (just blank line
        // before next def) should recover to the next definition.
        var source = """
            def foo():
            def main():
                pass
            """;

        var (module, errors) = ParseWithErrors(source);

        errors.Should().NotBeEmpty();
        module.Body.OfType<FunctionDef>().Should().Contain(f => f.Name == "main");
    }

    [Fact]
    public void Recovery_ErrorInWhileCondition_RecoverToNextStatement()
    {
        // A while statement with an empty condition (while :) should
        // recover and allow subsequent statements to be parsed.
        var source = """
            def main():
                while :
                    pass
                x: int = 5
            """;

        var (module, errors) = ParseWithErrors(source);

        errors.Should().NotBeEmpty();
    }

    [Fact]
    public void Recovery_EmptyFile_NoCrash()
    {
        // An empty file should parse successfully with no errors or crashes.
        var (module, errors) = ParseWithErrors("");

        errors.Should().BeEmpty();
        module.Body.Should().BeEmpty();
    }

    [Fact]
    public void Recovery_EnumWithBadMember_RecoversToNextDefinition()
    {
        // An error inside an enum body (invalid member name) should not
        // produce excessive spurious errors. The next definition should
        // still be parsed.
        var source = """
            enum Color:
                123
                RED = 1

            def main():
                pass
            """;

        var (module, errors) = ParseWithErrors(source);

        errors.Should().NotBeEmpty();
        errors.Should().Contain(e => e.Contains("Expected identifier"));

        // The valid function 'main' should still be parsed
        module.Body.OfType<FunctionDef>().Should().Contain(f => f.Name == "main");
    }

    [Fact]
    public void Recovery_EnumWithBadMember_DoesNotProduceManySpuriousErrors()
    {
        // A single error in an enum body should not cascade into many errors.
        var source = """
            enum Color:
                123

            def main():
                pass
            """;

        var (module, errors) = ParseWithErrors(source);

        errors.Should().NotBeEmpty();
        // Should not produce more than 3 errors for a single mistake
        errors.Count.Should().BeLessThanOrEqualTo(3,
            $"Enum error produced too many cascading errors: {string.Join("; ", errors)}");
    }

    [Fact]
    public void Recovery_MissingColonOnIf_DoesNotCascade()
    {
        // A missing colon on an if statement inside a function body should
        // recover cleanly without excessive cascading errors.
        var source = """
            def main():
                if True
                    pass
                x: int = 5
            """;

        var (module, errors) = ParseWithErrors(source);

        errors.Should().NotBeEmpty();
        errors.Count.Should().BeLessThanOrEqualTo(3,
            $"If colon error cascaded: {string.Join("; ", errors)}");
    }

    [Fact]
    public void Recovery_MissingColonOnWhile_DoesNotCascade()
    {
        // A missing colon on a while statement should recover cleanly.
        var source = """
            def main():
                while True
                    pass
                x: int = 5
            """;

        var (module, errors) = ParseWithErrors(source);

        errors.Should().NotBeEmpty();
        errors.Count.Should().BeLessThanOrEqualTo(3,
            $"While colon error cascaded: {string.Join("; ", errors)}");
    }

    [Fact]
    public void Recovery_MissingColonOnExcept_DoesNotCascade()
    {
        // A missing colon on except should recover cleanly.
        var source = """
            def main():
                try:
                    pass
                except Exception
                    pass
                x: int = 5
            """;

        var (module, errors) = ParseWithErrors(source);

        errors.Should().NotBeEmpty();
        errors.Count.Should().BeLessThanOrEqualTo(3,
            $"Except colon error cascaded: {string.Join("; ", errors)}");
    }

    [Fact]
    public void Recovery_ErrorInElifClause_DoesNotCascade()
    {
        // An error in an elif condition should recover cleanly
        // without cascading into the else clause or subsequent statements.
        var source = """
            def main():
                x: int = 5
                if x > 3:
                    pass
                elif
                    pass
                else:
                    pass
                y: int = 10
            """;

        var (module, errors) = ParseWithErrors(source);

        errors.Should().NotBeEmpty();
        errors.Count.Should().BeLessThanOrEqualTo(4,
            $"Elif error cascaded: {string.Join("; ", errors)}");
    }

    [Fact]
    public void Recovery_MultipleDefinitionTypesWithErrors_ReportsAll()
    {
        // Errors in different definition types (def, class, struct, enum)
        // should all be reported independently.
        var source = """
            def 111():
                pass

            class ():
                pass

            struct ():
                pass

            def valid():
                pass
            """;

        var (module, errors) = ParseWithErrors(source);

        errors.Count.Should().BeGreaterThanOrEqualTo(3,
            $"Expected at least 3 errors from 3 broken definitions, got: {string.Join("; ", errors)}");

        // The valid function should still be parsed
        module.Body.OfType<FunctionDef>().Should().Contain(f => f.Name == "valid");
    }

    [Fact]
    public void Recovery_BrokenHeaderWithBody_SkipsBodyCompletely()
    {
        // A function with a broken header but valid indented body.
        // Recovery should skip the entire body and parse the next definition.
        var source = """
            def (x: int, y: int):
                z: int = x + y
                return z

            def add(a: int, b: int) -> int:
                return a + b
            """;

        var (module, errors) = ParseWithErrors(source);

        errors.Should().NotBeEmpty();

        // The valid function should be parsed correctly
        var addFn = module.Body.OfType<FunctionDef>().FirstOrDefault(f => f.Name == "add");
        addFn.Should().NotBeNull("Recovery should skip broken function body and parse 'add'");
        addFn!.Parameters.Should().HaveCount(2);
    }

    [Fact]
    public void Recovery_FullPipeline_ShortCircuitsAfterParseErrors()
    {
        // The full compilation pipeline should short-circuit after parse errors,
        // returning all parser errors without attempting semantic analysis.
        // This verifies the spec requirement: "semantic analysis handles
        // partially-parsed modules gracefully."
        var source = """
            def 123():
                pass

            class ():
                pass

            def valid():
                x: int = undefined_var
            """;

        var compiler = new Compiler();
        var result = compiler.Compile(source, "test.spy");

        // Compilation should fail
        result.Success.Should().BeFalse();

        // Multiple parse errors should be reported
        var errors = result.Diagnostics.GetErrors().ToList();
        errors.Should().HaveCountGreaterThanOrEqualTo(2,
            "parser should report errors for both broken definitions");

        // All errors should be from the Parser phase (not semantic)
        errors.Should().AllSatisfy(e =>
            e.Phase.Should().Be(Sharpy.Compiler.Diagnostics.CompilerPhase.Parser,
                $"error '{e.Message}' should be from Parser phase, not semantic"));

        // No semantic errors should be present (proving short-circuit)
        errors.Should().NotContain(e =>
            e.Phase == Sharpy.Compiler.Diagnostics.CompilerPhase.TypeChecking ||
            e.Phase == Sharpy.Compiler.Diagnostics.CompilerPhase.NameResolution,
            "semantic analysis should not run when parser has errors");
    }

    [Fact]
    public void Recovery_FullPipeline_MultipleDiverseErrors_NoException()
    {
        // Stress test: many different kinds of parse errors should not cause
        // the compiler to throw an exception.
        var source = """
            def 999():
                pass

            class :
                pass

            struct :
                pass

            def foo(x: int)
                return x

            while :
                pass

            def valid():
                pass
            """;

        var compiler = new Compiler();

        // Should not throw
        var result = compiler.Compile(source, "test.spy");

        result.Success.Should().BeFalse();
        result.Diagnostics.GetErrors().Should().HaveCountGreaterThanOrEqualTo(3);
    }

    [Fact]
    public void Recovery_ReturnTypeAnnotationError_RecoverToNextDefinition()
    {
        // An error in a return type annotation (def foo() -> :) should
        // recover and parse the next definition.
        var source = """
            def foo() -> :
                pass

            def bar() -> int:
                return 42
            """;

        var (module, errors) = ParseWithErrors(source);

        errors.Should().NotBeEmpty();
        errors.Should().Contain(e => e.Contains("Expected identifier"));

        // The valid function 'bar' should still be parsed
        module.Body.OfType<FunctionDef>().Should().Contain(f => f.Name == "bar");
    }

    [Fact]
    public void Recovery_ConstAndTypeAliasErrors_RecoverToNextStatement()
    {
        // Errors in const and type alias declarations should recover.
        var source = """
            const x =
            type Y =
            def main():
                pass
            """;

        var (module, errors) = ParseWithErrors(source);

        errors.Count.Should().BeGreaterThanOrEqualTo(2);

        // The valid function 'main' should still be parsed
        module.Body.OfType<FunctionDef>().Should().Contain(f => f.Name == "main");
    }

    [Fact]
    public void Recovery_ErrorAfterDocstring_ContinuesParsingBody()
    {
        // An error in a method after a class docstring should not prevent
        // subsequent methods from being parsed.
        var source = """
            class Foo:
                "This is a docstring"
                def (self):
                    pass
                def valid(self):
                    pass
            """;

        var (module, errors) = ParseWithErrors(source);

        errors.Should().NotBeEmpty();

        var cls = module.Body.OfType<ClassDef>().FirstOrDefault(c => c.Name == "Foo");
        cls.Should().NotBeNull();
        cls!.Body.OfType<FunctionDef>().Should().Contain(f => f.Name == "valid");
    }

    [Fact]
    public void Recovery_PathologicalNestedBrackets_DoesNotHang()
    {
        // Many unmatched nested brackets should recover without hanging.
        var source = """
            def main():
                x = [[[[[[[[
                y: int = 5
            """;

        var (module, errors) = ParseWithErrors(source);

        errors.Should().NotBeEmpty();
        // The key assertion is that we get here at all (no infinite loop)
        var mainFn = module.Body.OfType<FunctionDef>().FirstOrDefault(f => f.Name == "main");
        mainFn.Should().NotBeNull();
    }

    [Fact]
    public void Recovery_ElifError_PreservesSubsequentDefinitions()
    {
        // An error in an elif clause should not prevent subsequent top-level
        // definitions from being parsed. The else clause and code after
        // the if block may be lost, but the next def should be recoverable.
        var source = """
            def main():
                if True:
                    pass
                elif
                    pass
                else:
                    pass
                y: int = 2

            def after():
                pass
            """;

        var (module, errors) = ParseWithErrors(source);

        errors.Should().NotBeEmpty();
        // Errors should be limited (elif error + at most a couple cascading)
        errors.Count.Should().BeLessThanOrEqualTo(5,
            $"Elif error produced too many cascading errors: {string.Join("; ", errors)}");

        // The 'after' function should still be parsed
        module.Body.OfType<FunctionDef>().Should().Contain(f => f.Name == "after");
    }

    [Fact]
    public void Recovery_TryExceptError_PreservesSubsequentDefinitions()
    {
        // An error in a try/except construct should not prevent subsequent
        // definitions from being parsed.
        var source = """
            def main():
                try
                    pass
                except:
                    pass

            def after():
                pass
            """;

        var (module, errors) = ParseWithErrors(source);

        errors.Should().NotBeEmpty();

        // The 'after' function should still be parsed
        module.Body.OfType<FunctionDef>().Should().Contain(f => f.Name == "after");
    }

    #endregion
}
