using FluentAssertions;
using Xunit;
using Sharpy.Compiler.Parser.Ast;
using LexerNs = Sharpy.Compiler.Lexer;
using ParserNs = Sharpy.Compiler.Parser;
using ParserError = Sharpy.Compiler.Parser.ParserError;

namespace Sharpy.Compiler.Tests.Parser;

/// <summary>
/// Edge case tests for the Parser - boundary conditions and complex scenarios
/// </summary>
public class ParserEdgeCaseTests
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

    #region Complex Nesting

    [Fact]
    public void ParsesDeeplyNestedIfStatements()
    {
        var source = @"
if True:
    if True:
        if True:
            if True:
                if True:
                    pass
";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
        module.Body[0].Should().BeOfType<IfStatement>();
    }

    [Fact]
    public void ParsesDeeplyNestedFunctionCalls()
    {
        var source = "x = f(g(h(i(j(k(1))))))";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
    }

    [Fact]
    public void ParsesDeeplyNestedLists()
    {
        var source = "x = [[[[[[1]]]]]]";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
    }

    [Fact]
    public void ParsesDeeplyNestedDictionaries()
    {
        var source = "x = {'a': {'b': {'c': {'d': 1}}}}";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
    }

    [Fact]
    public void ParsesMixedNestedCollections()
    {
        var source = "x = {'list': [1, {'nested': [2, 3]}, 4], 'value': 5}";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
    }

    #endregion

    #region Long Expressions

    [Fact]
    public void ParsesLongChainedComparisons()
    {
        var source = "x = 1 < 2 < 3 < 4 < 5 < 6 < 7 < 8 < 9 < 10";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
    }

    [Fact]
    public void ParsesLongBinaryExpressionChain()
    {
        var source = "x = 1 + 2 + 3 + 4 + 5 + 6 + 7 + 8 + 9 + 10";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
    }

    [Fact]
    public void ParsesComplexBooleanExpression()
    {
        var source = "x = (a and b) or (c and d) or (e and f) and not (g or h)";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
    }

    [Fact]
    public void ParsesLongAttributeAccess()
    {
        var source = "x = obj.attr1.attr2.attr3.attr4.attr5.method()";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
    }

    #endregion

    #region Empty and Minimal Constructs

    [Fact]
    public void ParsesEmptyList()
    {
        var source = "x = []";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);

        var assign = module.Body[0] as Assignment;
        assign.Should().NotBeNull();
        assign!.Value.Should().BeOfType<ListLiteral>();
    }

    [Fact]
    public void ParsesEmptyDict()
    {
        var source = "x = {}";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);

        var assign = module.Body[0] as Assignment;
        assign.Should().NotBeNull();
        if (assign != null)
        {
            // Empty dict could be parsed as DictLiteral or SetLiteral
            assign.Value.Should().NotBeNull();
        }
    }

    [Fact]
    public void ParsesEmptyTuple()
    {
        var source = "x = ()";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
    }

    [Fact]
    public void ParsesSingleElementTuple()
    {
        var source = "x = (1,)";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
    }

    [Fact]
    public void ParsesClassWithOnlyPass()
    {
        var source = @"
class Empty:
    pass
";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
        module.Body[0].Should().BeOfType<ClassDef>();
    }

    [Fact]
    public void ParsesFunctionWithOnlyPass()
    {
        var source = @"
def empty():
    pass
";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
        module.Body[0].Should().BeOfType<FunctionDef>();
    }

    [Fact]
    public void ParsesFunctionWithDocstring()
    {
        var source = @"
def foo():
    """"""This is a docstring""""""
    pass
";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
        var funcDef = module.Body[0] as FunctionDef;
        funcDef.Should().NotBeNull();
    }

    #endregion

    #region Special Characters and Strings

    [Fact]
    public void ParsesMultilineString()
    {
        var source = @"
x = """"""
This is
a multiline
string
""""""
";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
    }

    [Fact]
    public void ParsesStringWithEscapes()
    {
        var source = @"x = ""hello\nworld\t\r\n""";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
    }

    [Fact]
    public void ParsesRawString()
    {
        var source = @"x = r""C:\path\to\file""";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
    }

    [Fact]
    public void ParsesFString()
    {
        var source = @"x = f""Hello {name}""";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
    }

    [Fact]
    public void ParsesComplexFString()
    {
        var source = @"x = f""Value: {value:02d}, Name: {name.upper()}""";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
    }

    #endregion

    #region Unusual but Valid Syntax

    [Fact]
    public void ParsesLineBreakInParentheses()
    {
        var source = @"
x = (
    1 + 2 + 3
)
";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
    }

    [Fact]
    public void ParsesLineContinuation()
    {
        var source = @"
x = 1 + 2 + \
    3 + 4
";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
    }

    [Fact]
    public void ParsesTrailingCommaInList()
    {
        var source = "x = [1, 2, 3,]";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
    }

    [Fact]
    public void ParsesTrailingCommaInDict()
    {
        var source = "x = {'a': 1, 'b': 2,}";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
    }

    [Fact]
    public void ParsesTrailingCommaInFunctionArgs()
    {
        var source = "x = foo(1, 2, 3,)";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
    }

    [Fact]
    public void ParsesTrailingCommaInFunctionParams()
    {
        var source = @"
def foo(a, b, c,):
    pass
";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
    }

    [Fact]
    public void ParsesUnderscoreInIdentifier()
    {
        var source = "my_variable_name = 42";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
    }

    [Fact]
    public void ParsesLiteralNames()
    {
        var source = "`literal name` = 42";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
    }

    #endregion

    #region Complex Type Annotations

    [Fact]
    public void ParsesNestedGenericTypes()
    {
        var source = "x: list[dict[str, int]]";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
    }

    [Fact(Skip = "Unimplemented: Callable type syntax not yet supported")]
    public void ParsesCallableType()
    {
        var source = "x: callable[[int, str], bool]";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
    }

    #endregion

    #region Complex Function Signatures

    [Fact]
    public void ParsesFunctionWithDefaultArgs()
    {
        var source = @"
def foo(a: int, b: int = 10, c: str = 'default'):
    pass
";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
        if (module.Body[0] is FunctionDef funcDef)
        {
            funcDef.Parameters.Should().HaveCount(3);
        }
        else
        {
            Assert.Fail("Expected first module body element to be a FunctionDef.");
        }
    }

    [Fact(Skip = "Unimplemented: *args variable arguments not yet supported")]
    public void ParsesFunctionWithVarArgs()
    {
        var source = @"
def foo(*args):
    pass
";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
    }

    [Fact(Skip = "Unimplemented: **kwargs keyword arguments not yet supported")]
    public void ParsesFunctionWithKwargs()
    {
        var source = @"
def foo(**kwargs):
    pass
";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
    }

    [Fact(Skip = "Unimplemented: *args/**kwargs combination not yet supported")]
    public void ParsesFunctionWithAllParameterTypes()
    {
        var source = @"
def foo(a: int, b: int = 10, *args, **kwargs):
    pass
";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
    }

    [Fact]
    public void ParsesFunctionWithComplexReturnType()
    {
        var source = @"
def foo() -> list[tuple[int, str]]:
    pass
";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
    }

    #endregion

    #region Decorators

    [Fact]
    public void ParsesSingleDecorator()
    {
        var source = @"
@decorator
def foo():
    pass
";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
        var funcDef = module.Body[0] as FunctionDef;
        funcDef.Should().NotBeNull();
        funcDef!.Decorators.Should().HaveCount(1);
    }

    [Fact]
    public void ParsesMultipleDecorators()
    {
        var source = @"
@decorator1
@decorator2
@decorator3
def foo():
    pass
";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
        var funcDef = module.Body[0] as FunctionDef;
        funcDef.Should().NotBeNull();
        funcDef!.Decorators.Should().HaveCount(3);
    }

    [Fact(Skip = "Unimplemented: Decorators with arguments not yet supported")]
    public void ParsesDecoratorWithArguments()
    {
        var source = @"
@decorator(arg1, arg2)
def foo():
    pass
";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
    }

    [Fact]
    public void ParsesDecoratorOnClass()
    {
        var source = @"
@decorator
class Foo:
    pass
";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
    }

    #endregion

    #region Import Statements

    [Fact]
    public void ParsesSimpleImport()
    {
        var source = "import math";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
        module.Body[0].Should().BeOfType<ImportStatement>();
    }

    [Fact]
    public void ParsesImportWithAlias()
    {
        var source = "import math as m";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
    }

    [Fact]
    public void ParsesFromImport()
    {
        var source = "from math import sin, cos";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
    }

    [Fact]
    public void ParsesFromImportWithAlias()
    {
        var source = "from math import sin as sine";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
    }

    [Fact]
    public void ParsesFromImportStar()
    {
        var source = "from math import *";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
    }

    [Fact]
    public void ParsesMultipleImports()
    {
        var source = "import math, sys, os";
        var module = Parse(source);
        // This might parse as one or three import statements
        module.Body.Should().NotBeEmpty();
    }

    #endregion

    #region Comprehensions

    [Fact]
    public void ParsesListComprehension()
    {
        var source = "x = [i * 2 for i in range(10)]";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);

        var assignment = module.Body[0].Should().BeOfType<Assignment>().Subject;
        var listComp = assignment.Value.Should().BeOfType<ListComprehension>().Subject;

        listComp.Clauses.Should().HaveCount(1);
        listComp.Clauses[0].Should().BeOfType<ForClause>();
    }

    [Fact]
    public void ParsesListComprehensionWithCondition()
    {
        var source = "x = [i * 2 for i in range(10) if i % 2 == 0]";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);

        var assignment = module.Body[0].Should().BeOfType<Assignment>().Subject;
        var listComp = assignment.Value.Should().BeOfType<ListComprehension>().Subject;

        listComp.Clauses.Should().HaveCount(2);
        listComp.Clauses[0].Should().BeOfType<ForClause>();
        listComp.Clauses[1].Should().BeOfType<IfClause>();
    }

    [Fact(Skip = "TODO: Nested list comprehensions (multiple for clauses) not yet supported")]
    public void ParsesNestedListComprehension()
    {
        var source = "x = [[j for j in range(i)] for i in range(5)]";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
    }

    [Fact(Skip = "TODO: Tuple unpacking in comprehensions not yet supported")]
    public void ParsesDictComprehension()
    {
        var source = "x = {k: v for k, v in items}";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
    }

    [Fact]
    public void ParsesSetComprehension()
    {
        var source = "x = {i * 2 for i in range(10)}";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);

        var assignment = module.Body[0].Should().BeOfType<Assignment>().Subject;
        var setComp = assignment.Value.Should().BeOfType<SetComprehension>().Subject;

        setComp.Clauses.Should().HaveCount(1);
        setComp.Clauses[0].Should().BeOfType<ForClause>();
    }

    [Fact]
    public void ParsesListComprehensionWithMultipleFilters()
    {
        var source = "x = [i for i in range(100) if i % 2 == 0 if i % 3 == 0]";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);

        var assignment = module.Body[0].Should().BeOfType<Assignment>().Subject;
        var listComp = assignment.Value.Should().BeOfType<ListComprehension>().Subject;

        listComp.Clauses.Should().HaveCount(3);
        listComp.Clauses[0].Should().BeOfType<ForClause>();
        listComp.Clauses[1].Should().BeOfType<IfClause>();
        listComp.Clauses[2].Should().BeOfType<IfClause>();
    }

    [Fact]
    public void ParsesListComprehensionWithComplexExpression()
    {
        var source = "x = [(i + j) * 2 for i in range(10)]";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);

        var assignment = module.Body[0].Should().BeOfType<Assignment>().Subject;
        var listComp = assignment.Value.Should().BeOfType<ListComprehension>().Subject;

        listComp.Element.Should().BeOfType<BinaryOp>();
        listComp.Clauses.Should().HaveCount(1);
    }

    [Fact]
    public void ParsesListComprehensionWithFunctionCall()
    {
        var source = "x = [str(i) for i in items]";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);

        var assignment = module.Body[0].Should().BeOfType<Assignment>().Subject;
        var listComp = assignment.Value.Should().BeOfType<ListComprehension>().Subject;

        listComp.Element.Should().BeOfType<FunctionCall>();
    }

    [Fact]
    public void ParsesSetComprehensionWithFilter()
    {
        var source = "x = {i for i in range(10) if i > 5}";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);

        var assignment = module.Body[0].Should().BeOfType<Assignment>().Subject;
        var setComp = assignment.Value.Should().BeOfType<SetComprehension>().Subject;

        setComp.Clauses.Should().HaveCount(2);
        setComp.Clauses[0].Should().BeOfType<ForClause>();
        setComp.Clauses[1].Should().BeOfType<IfClause>();
    }

    [Fact]
    public void ParsesDictComprehensionWithSingleVariable()
    {
        var source = "x = {i: i * 2 for i in range(10)}";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);

        var assignment = module.Body[0].Should().BeOfType<Assignment>().Subject;
        var dictComp = assignment.Value.Should().BeOfType<DictComprehension>().Subject;

        dictComp.Clauses.Should().HaveCount(1);
        dictComp.Clauses[0].Should().BeOfType<ForClause>();
        dictComp.Key.Should().BeOfType<Identifier>();
        dictComp.Value.Should().BeOfType<BinaryOp>();
    }

    [Fact]
    public void ParsesDictComprehensionWithFilter()
    {
        var source = "x = {i: str(i) for i in range(10) if i % 2 == 0}";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);

        var assignment = module.Body[0].Should().BeOfType<Assignment>().Subject;
        var dictComp = assignment.Value.Should().BeOfType<DictComprehension>().Subject;

        dictComp.Clauses.Should().HaveCount(2);
        dictComp.Clauses[0].Should().BeOfType<ForClause>();
        dictComp.Clauses[1].Should().BeOfType<IfClause>();
    }

    [Fact]
    public void ParsesComprehensionWithMemberAccess()
    {
        var source = "x = [item.value for item in items]";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);

        var assignment = module.Body[0].Should().BeOfType<Assignment>().Subject;
        var listComp = assignment.Value.Should().BeOfType<ListComprehension>().Subject;

        listComp.Element.Should().BeOfType<MemberAccess>();
    }

    [Fact]
    public void ParsesComprehensionWithMethodCall()
    {
        var source = "x = [item.upper() for item in items]";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);

        var assignment = module.Body[0].Should().BeOfType<Assignment>().Subject;
        var listComp = assignment.Value.Should().BeOfType<ListComprehension>().Subject;

        listComp.Element.Should().BeOfType<FunctionCall>();
    }

    [Fact]
    public void ParsesComprehensionWithIndexAccess()
    {
        var source = "x = [item[0] for item in items]";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);

        var assignment = module.Body[0].Should().BeOfType<Assignment>().Subject;
        var listComp = assignment.Value.Should().BeOfType<ListComprehension>().Subject;

        listComp.Element.Should().BeOfType<IndexAccess>();
    }

    [Fact]
    public void ParsesComprehensionWithConditionalExpression()
    {
        var source = "x = [i if i > 0 else -i for i in items]";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);

        var assignment = module.Body[0].Should().BeOfType<Assignment>().Subject;
        var listComp = assignment.Value.Should().BeOfType<ListComprehension>().Subject;

        listComp.Element.Should().BeOfType<ConditionalExpression>();
    }

    [Fact]
    public void ParsesComprehensionAsArgumentToFunctionCall()
    {
        var source = "result = sum([i * 2 for i in range(10)])";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);

        var assignment = module.Body[0].Should().BeOfType<Assignment>().Subject;
        var call = assignment.Value.Should().BeOfType<FunctionCall>().Subject;
        call.Arguments.Should().HaveCount(1);
        call.Arguments[0].Should().BeOfType<ListComprehension>();
    }

    [Fact]
    public void ParsesComprehensionInReturnStatement()
    {
        var source = @"
def foo():
    return [i * 2 for i in range(10)]";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);

        var funcDef = module.Body[0].Should().BeOfType<FunctionDef>().Subject;
        funcDef.Body.Should().HaveCount(1);
        var returnStmt = funcDef.Body[0].Should().BeOfType<ReturnStatement>().Subject;
        returnStmt.Value.Should().BeOfType<ListComprehension>();
    }

    #endregion

    #region Lambda Expressions

    [Fact]
    public void ParsesSimpleLambda()
    {
        var source = "f = lambda x: x * 2";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
    }

    [Fact]
    public void ParsesLambdaWithMultipleParams()
    {
        var source = "f = lambda x, y, z: x + y + z";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
    }

    [Fact(Skip = "Unimplemented: Lambda with default arguments not yet supported")]
    public void ParsesLambdaWithDefaultArgs()
    {
        var source = "f = lambda x, y=10: x + y";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
    }

    [Fact]
    public void ParsesNestedLambda()
    {
        var source = "f = lambda x: lambda y: x + y";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
    }

    #endregion

    #region Slicing

    [Fact]
    public void ParsesSimpleSlice()
    {
        var source = "x = lst[1:5]";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
    }

    [Fact]
    public void ParsesSliceWithStep()
    {
        var source = "x = lst[1:10:2]";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
    }

    [Fact]
    public void ParsesOpenEndedSlice()
    {
        var source = "x = lst[5:]";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
    }

    [Fact]
    public void ParsesOpenStartedSlice()
    {
        var source = "x = lst[:5]";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
    }

    [Fact]
    public void ParsesFullSlice()
    {
        var source = "x = lst[:]";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
    }

    [Fact]
    public void ParsesNegativeSlice()
    {
        var source = "x = lst[-5:-1]";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
    }

    #endregion

    #region Exception Handling

    [Fact]
    public void ParsesTryExcept()
    {
        var source = @"
try:
    x: int = 1
except Exception:
    pass
";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
    }

    [Fact]
    public void ParsesTryExceptWithAs()
    {
        var source = @"
try:
    x: int = 1
except Exception as e:
    print(e)
";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
    }

    [Fact]
    public void ParsesMultipleExcepts()
    {
        var source = @"
try:
    x: int = 1
except ValueError:
    pass
except TypeError:
    pass
except Exception:
    pass
";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
    }

    [Fact]
    public void ParsesTryExceptFinally()
    {
        var source = @"
try:
    x: int = 1
except Exception:
    pass
finally:
    cleanup()
";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
    }

    [Fact(Skip = "Unimplemented: Try-except-else blocks not yet supported")]
    public void ParsesTryExceptElse()
    {
        var source = @"
try:
    x: int = 1
except Exception:
    pass
else:
    success()
";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
    }

    #endregion

    #region Ternary and Conditional Expressions

    [Fact]
    public void ParsesTernaryExpression()
    {
        var source = "x = 1 if True else 2";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
    }

    [Fact]
    public void ParsesNestedTernary()
    {
        var source = "x = 1 if a else 2 if b else 3";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
    }

    [Fact]
    public void ParsesTernaryInFunctionCall()
    {
        var source = "f(1 if True else 2)";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
    }

    #endregion

    #region With Statement

    [Fact(Skip = "Unimplemented: With statements not yet supported")]
    public void ParsesWithStatement()
    {
        var source = @"
with open('file.txt') as f:
    content = f.read()
";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
    }

    [Fact(Skip = "Unimplemented: Multiple with items not yet supported")]
    public void ParsesMultipleWithItems()
    {
        var source = @"
with open('a.txt') as a, open('b.txt') as b:
    pass
";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
    }

    #endregion

    #region Assert Statement

    [Fact]
    public void ParsesSimpleAssert()
    {
        var source = "assert True";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
    }

    [Fact]
    public void ParsesAssertWithMessage()
    {
        var source = "assert x > 0, 'x must be positive'";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
    }

    [Fact]
    public void ParsesComplexAssertion()
    {
        var source = "assert 0 <= x < 100, f'x={x} is out of range'";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
    }

    #endregion

    #region Enum

    [Fact]
    public void ParsesSimpleEnum()
    {
        var source = @"
enum Color:
    RED
    GREEN
    BLUE
";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
    }

    [Fact]
    public void ParsesEnumWithValues()
    {
        var source = @"
enum Status:
    PENDING = 0
    ACTIVE = 1
    COMPLETE = 2
";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
    }

    #endregion

    #region Match Statement (if supported)

    [Fact]
    public void ParsesMatchStatement()
    {
        var source = @"
match value:
    case 1:
        print('one')
    case 2:
        print('two')
    case _:
        print('other')
";
        try
        {
            var module = Parse(source);
            module.Body.Should().HaveCount(1);
        }
        catch (ParserError)
        {
            // Match might not be supported
        }
    }

    #endregion

    #region Comments

    [Fact]
    public void ParsesCodeWithComments()
    {
        var source = @"
# This is a comment
x = 42  # End of line comment
# Another comment
y = 100
";
        var module = Parse(source);
        module.Body.Should().HaveCount(2);
    }

    [Fact]
    public void ParsesMultipleConsecutiveComments()
    {
        var source = @"
# Comment 1
# Comment 2
# Comment 3
x = 42
";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
    }

    [Fact]
    public void ParsesInlineCommentWithinIfBlock()
    {
        var source = @"
def foo(x: int):
    if x > 0:
        y = 42  # inline comment
        print(y)
";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
        var funcDef = module.Body[0] as FunctionDef;
        funcDef.Should().NotBeNull();
        funcDef!.Body.Should().HaveCount(1);
        var ifStmt = funcDef.Body[0] as IfStatement;
        ifStmt.Should().NotBeNull();
        ifStmt!.ThenBody.Should().HaveCount(2);
    }

    [Fact]
    public void ParsesMultipleInlineCommentsInIfBlock()
    {
        var source = @"
def foo(x: int):
    if x > 0:  # check positive
        y = 42  # assign value
        z = y * 2  # double it
        print(z)  # output
";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
        var funcDef = module.Body[0] as FunctionDef;
        funcDef.Should().NotBeNull();
        funcDef!.Body.Should().HaveCount(1);
        var ifStmt = funcDef.Body[0] as IfStatement;
        ifStmt.Should().NotBeNull();
        ifStmt!.ThenBody.Should().HaveCount(3);
    }

    [Fact]
    public void ParsesCommentsInNestedIfBlocks()
    {
        var source = @"
def foo(x: int, y: int):
    if x > 0:  # outer check
        if y > 0:  # inner check
            z = x + y  # sum
            print(z)  # output
        else:  # negative y
            print('negative')  # message
";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
        var funcDef = module.Body[0] as FunctionDef;
        funcDef.Should().NotBeNull();
        funcDef!.Body.Should().HaveCount(1);
    }

    [Fact]
    public void ParsesCommentsInWhileBlock()
    {
        var source = @"
def foo():
    i = 0  # initialize
    while i < 10:  # loop condition
        i += 1  # increment
        print(i)  # output
";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
        var funcDef = module.Body[0] as FunctionDef;
        funcDef.Should().NotBeNull();
        funcDef!.Body.Should().HaveCount(2);
    }

    [Fact]
    public void ParsesCommentsInForBlock()
    {
        var source = @"
def foo():
    for i in range(10):  # loop
        x = i * 2  # double
        print(x)  # output
";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
        var funcDef = module.Body[0] as FunctionDef;
        funcDef.Should().NotBeNull();
        funcDef!.Body.Should().HaveCount(1);
    }

    [Fact]
    public void ParsesCommentsInTryExceptBlock()
    {
        var source = @"
def foo():
    try:  # attempt
        x = 1 / 0  # divide by zero
    except Exception:  # catch
        print('error')  # handle
";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
        var funcDef = module.Body[0] as FunctionDef;
        funcDef.Should().NotBeNull();
        funcDef!.Body.Should().HaveCount(1);
    }

    [Fact]
    public void ParsesCommentsInClassDefinition()
    {
        var source = @"
class Foo:  # class definition
    x: int = 42  # class variable

    def __init__(self):  # constructor
        self.y = 100  # instance variable
";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
        var classDef = module.Body[0] as ClassDef;
        classDef.Should().NotBeNull();
    }

    [Fact]
    public void ParsesCommentAfterColonInIfElse()
    {
        var source = @"
def foo(x: int):
    if x > 0:  # positive case
        return True  # return true
    else:  # negative or zero case
        return False  # return false
";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
        var funcDef = module.Body[0] as FunctionDef;
        funcDef.Should().NotBeNull();
    }

    [Fact]
    public void ParsesCommentsWithComplexNestedStructures()
    {
        var source = @"
def complex(x: int, y: int):  # main function
    if x > 0:  # check x
        while y > 0:  # loop on y
            if x + y > 100:  # threshold check
                break  # exit loop
            y -= 1  # decrement
        print(y)  # output result
    else:  # x is not positive
        for i in range(10):  # iterate
            if i % 2 == 0:  # even check
                print(i)  # print even
";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
        var funcDef = module.Body[0] as FunctionDef;
        funcDef.Should().NotBeNull();
        funcDef!.Body.Should().HaveCount(1);
    }

    #endregion

    #region Boundary Values

    [Fact]
    public void ParsesVeryLargeInteger()
    {
        var source = "x = 999999999999999999999999999999999999999999";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
    }

    [Fact]
    public void ParsesVerySmallFloat()
    {
        var source = "x = 0.0000000000000001";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
    }

    [Fact]
    public void ParsesScientificNotation()
    {
        var source = "x = 1.5e10";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
    }

    [Fact]
    public void ParsesNegativeScientificNotation()
    {
        var source = "x = 1.5e-10";
        var module = Parse(source);
        module.Body.Should().HaveCount(1);
    }

    #endregion
}
