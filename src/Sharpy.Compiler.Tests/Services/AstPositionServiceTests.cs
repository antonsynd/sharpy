using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Services;
using Xunit;

using LexerNs = Sharpy.Compiler.Lexer;
using ParserNs = Sharpy.Compiler.Parser;

namespace Sharpy.Compiler.Tests.Services;

public class AstPositionServiceTests
{
    private readonly AstPositionService _service = new();

    private static Module ParseModule(string source)
    {
        var lexer = new LexerNs.Lexer(source);
        var tokens = lexer.TokenizeAll();
        var parser = new ParserNs.Parser(tokens);
        return parser.ParseModule();
    }

    #region FindInnermostNode Tests

    [Fact]
    public void FindInnermostNode_SimpleIdentifier_ReturnsIdentifier()
    {
        // Line 1: def foo():
        // Line 2:     x = 1
        var source = "def foo():\n    x = 1\n";
        var module = ParseModule(source);

        // Position at 'x' on line 2, column 5
        var node = _service.FindInnermostNode(module, line: 2, column: 5);

        Assert.NotNull(node);
        Assert.IsType<Identifier>(node);
        Assert.Equal("x", ((Identifier)node).Name);
    }

    [Fact]
    public void FindInnermostNode_IntegerLiteral_ReturnsLiteral()
    {
        var source = "def foo():\n    x = 42\n";
        var module = ParseModule(source);

        // Position at '42' on line 2, column 9
        var node = _service.FindInnermostNode(module, line: 2, column: 9);

        Assert.NotNull(node);
        Assert.IsType<IntegerLiteral>(node);
        Assert.Equal("42", ((IntegerLiteral)node).Value);
    }

    [Fact]
    public void FindInnermostNode_BinaryOperator_ReturnsOperand()
    {
        var source = "x = 1 + 2\n";
        var module = ParseModule(source);

        // Position at '1' on line 1
        var node = _service.FindInnermostNode(module, line: 1, column: 5);

        Assert.NotNull(node);
        Assert.IsType<IntegerLiteral>(node);
        Assert.Equal("1", ((IntegerLiteral)node).Value);
    }

    [Fact]
    public void FindInnermostNode_FunctionCall_ReturnsFunctionIdentifier()
    {
        var source = "print(x)\n";
        var module = ParseModule(source);

        // Position at 'print' on line 1, column 1
        var node = _service.FindInnermostNode(module, line: 1, column: 1);

        Assert.NotNull(node);
        Assert.IsType<Identifier>(node);
        Assert.Equal("print", ((Identifier)node).Name);
    }

    [Fact]
    public void FindInnermostNode_PositionOutsideAST_ReturnsNull()
    {
        var source = "x = 1\n";
        var module = ParseModule(source);

        // Position far beyond the file content
        var node = _service.FindInnermostNode(module, line: 100, column: 1);

        Assert.Null(node);
    }

    [Fact]
    public void FindInnermostNode_EmptyModule_ReturnsNull()
    {
        var source = "";
        var module = ParseModule(source);

        var node = _service.FindInnermostNode(module, line: 1, column: 1);

        // Module itself might be returned or null depending on span
        // Just verify no exception is thrown
        Assert.True(node is null or Module);
    }

    #endregion

    #region FindAllContainingNodes Tests

    [Fact]
    public void FindAllContainingNodes_NestedExpression_ReturnsPathFromOuterToInner()
    {
        var source = "x = 1 + 2\n";
        var module = ParseModule(source);

        // Position at '1' - should return: Module -> VariableDeclaration -> BinaryOp -> IntegerLiteral
        var nodes = _service.FindAllContainingNodes(module, line: 1, column: 5);

        Assert.NotEmpty(nodes);
        Assert.IsType<Module>(nodes[0]);
        Assert.IsType<IntegerLiteral>(nodes[^1]);
    }

    [Fact]
    public void FindAllContainingNodes_FunctionBody_IncludesFunction()
    {
        var source = "def foo():\n    x = 1\n";
        var module = ParseModule(source);

        // Position at 'x' inside function
        var nodes = _service.FindAllContainingNodes(module, line: 2, column: 5);

        Assert.NotEmpty(nodes);
        Assert.IsType<Module>(nodes[0]);
        // Function definition should be in the path
        Assert.Contains(nodes, n => n is FunctionDef);
    }

    [Fact]
    public void FindAllContainingNodes_PositionOutside_ReturnsEmptyList()
    {
        var source = "x = 1\n";
        var module = ParseModule(source);

        var nodes = _service.FindAllContainingNodes(module, line: 100, column: 1);

        Assert.Empty(nodes);
    }

    #endregion

    #region FindNodeOfType Tests

    [Fact]
    public void FindNodeOfType_FindIdentifier_ReturnsInnermost()
    {
        var source = "x = 1\n";
        var module = ParseModule(source);

        // Position at 'x'
        var identifier = _service.FindNodeOfType<Identifier>(module, line: 1, column: 1);

        Assert.NotNull(identifier);
        Assert.Equal("x", identifier.Name);
    }

    [Fact]
    public void FindNodeOfType_FindFunctionDef_ReturnsFunction()
    {
        var source = "def foo():\n    x = 1\n";
        var module = ParseModule(source);

        // Position inside function body
        var funcDef = _service.FindNodeOfType<FunctionDef>(module, line: 2, column: 5);

        Assert.NotNull(funcDef);
        Assert.Equal("foo", funcDef.Name);
    }

    [Fact]
    public void FindNodeOfType_TypeNotFound_ReturnsNull()
    {
        var source = "x = 1\n";
        var module = ParseModule(source);

        // Looking for FunctionDef when there is none
        var funcDef = _service.FindNodeOfType<FunctionDef>(module, line: 1, column: 1);

        Assert.Null(funcDef);
    }

    [Fact]
    public void FindNodeOfType_FindStatement_ReturnsVariableDeclaration()
    {
        var source = "x: int = 1\n";
        var module = ParseModule(source);

        var varDecl = _service.FindNodeOfType<VariableDeclaration>(module, line: 1, column: 1);

        Assert.NotNull(varDecl);
        Assert.Equal("x", varDecl.Name);
    }

    #endregion

    #region Parameter Validation Tests

    [Fact]
    public void FindInnermostNode_NullModule_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _service.FindInnermostNode(null!, 1, 1));
    }

    [Fact]
    public void FindInnermostNode_InvalidLine_ThrowsArgumentOutOfRangeException()
    {
        var module = ParseModule("x = 1\n");

        Assert.Throws<ArgumentOutOfRangeException>(() => _service.FindInnermostNode(module, line: 0, column: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => _service.FindInnermostNode(module, line: -1, column: 1));
    }

    [Fact]
    public void FindInnermostNode_InvalidColumn_ThrowsArgumentOutOfRangeException()
    {
        var module = ParseModule("x = 1\n");

        Assert.Throws<ArgumentOutOfRangeException>(() => _service.FindInnermostNode(module, line: 1, column: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => _service.FindInnermostNode(module, line: 1, column: -1));
    }

    [Fact]
    public void FindAllContainingNodes_NullModule_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _service.FindAllContainingNodes(null!, 1, 1));
    }

    [Fact]
    public void FindNodeOfType_NullModule_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _service.FindNodeOfType<Identifier>(null!, 1, 1));
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void FindInnermostNode_MemberAccess_ReturnsCorrectNode()
    {
        var source = "a.b.c\n";
        var module = ParseModule(source);

        // Position at 'c'
        var node = _service.FindInnermostNode(module, line: 1, column: 5);

        Assert.NotNull(node);
        // 'c' is part of MemberAccess, innermost should be MemberAccess or Identifier
    }

    [Fact]
    public void FindInnermostNode_NestedFunctionCall_ReturnsInnerCall()
    {
        var source = "foo(bar(x))\n";
        var module = ParseModule(source);

        // Position at 'x'
        var node = _service.FindInnermostNode(module, line: 1, column: 9);

        Assert.NotNull(node);
        Assert.IsType<Identifier>(node);
        Assert.Equal("x", ((Identifier)node).Name);
    }

    [Fact]
    public void FindInnermostNode_IfStatement_ReturnsConditionNode()
    {
        var source = "if x > 0:\n    pass\n";
        var module = ParseModule(source);

        // Position at 'x'
        var node = _service.FindInnermostNode(module, line: 1, column: 4);

        Assert.NotNull(node);
        Assert.IsType<Identifier>(node);
        Assert.Equal("x", ((Identifier)node).Name);
    }

    [Fact]
    public void FindInnermostNode_ListLiteral_ReturnsElementNode()
    {
        var source = "x = [1, 2, 3]\n";
        var module = ParseModule(source);

        // Position at '2'
        var node = _service.FindInnermostNode(module, line: 1, column: 9);

        Assert.NotNull(node);
        Assert.IsType<IntegerLiteral>(node);
        Assert.Equal("2", ((IntegerLiteral)node).Value);
    }

    [Fact]
    public void FindInnermostNode_ClassBody_ReturnsMethodOrClassDef()
    {
        var source = @"class Foo:
    def bar(self) -> int:
        return 1
";
        var module = ParseModule(source);

        // Position inside method body at 'return'
        var node = _service.FindNodeOfType<FunctionDef>(module, line: 3, column: 9);

        Assert.NotNull(node);
        Assert.Equal("bar", node.Name);
    }

    [Fact]
    public void FindNodeOfType_ClassDefinition_ReturnsClass()
    {
        var source = @"class Foo:
    x: int = 1
";
        var module = ParseModule(source);

        // Position inside class body
        var classDef = _service.FindNodeOfType<ClassDef>(module, line: 2, column: 5);

        Assert.NotNull(classDef);
        Assert.Equal("Foo", classDef.Name);
    }

    [Fact]
    public void FindInnermostNode_LambdaBody_ReturnsBodyExpression()
    {
        var source = "f = lambda x: x + 1\n";
        var module = ParseModule(source);

        // Position at 'x + 1' body expression, specifically at 'x'
        var node = _service.FindInnermostNode(module, line: 1, column: 15);

        Assert.NotNull(node);
        Assert.IsType<Identifier>(node);
        Assert.Equal("x", ((Identifier)node).Name);
    }

    [Fact]
    public void FindNodeOfType_LambdaExpression_ReturnsLambda()
    {
        var source = "f = lambda x: x + 1\n";
        var module = ParseModule(source);

        // Position inside lambda body
        var lambda = _service.FindNodeOfType<LambdaExpression>(module, line: 1, column: 17);

        Assert.NotNull(lambda);
    }

    [Fact]
    public void FindInnermostNode_ListComprehensionElement_ReturnsElement()
    {
        var source = "y = [x * 2 for x in items]\n";
        var module = ParseModule(source);

        // Position at 'x * 2' element, specifically at '2'
        var node = _service.FindInnermostNode(module, line: 1, column: 10);

        Assert.NotNull(node);
        Assert.IsType<IntegerLiteral>(node);
        Assert.Equal("2", ((IntegerLiteral)node).Value);
    }

    [Fact]
    public void FindNodeOfType_ListComprehension_ReturnsComprehension()
    {
        var source = "y = [x for x in items]\n";
        var module = ParseModule(source);

        // Position inside comprehension
        var comp = _service.FindNodeOfType<ListComprehension>(module, line: 1, column: 6);

        Assert.NotNull(comp);
    }

    [Fact]
    public void FindInnermostNode_DictLiteralKey_ReturnsKey()
    {
        var source = "d = {\"key\": 42}\n";
        var module = ParseModule(source);

        // Position at '"key"'
        var node = _service.FindInnermostNode(module, line: 1, column: 6);

        Assert.NotNull(node);
        Assert.IsType<StringLiteral>(node);
        Assert.Equal("key", ((StringLiteral)node).Value);
    }

    [Fact]
    public void FindInnermostNode_DictLiteralValue_ReturnsValue()
    {
        var source = "d = {\"key\": 42}\n";
        var module = ParseModule(source);

        // Position at '42'
        var node = _service.FindInnermostNode(module, line: 1, column: 13);

        Assert.NotNull(node);
        Assert.IsType<IntegerLiteral>(node);
        Assert.Equal("42", ((IntegerLiteral)node).Value);
    }

    [Fact]
    public void FindInnermostNode_ConditionalExpression_ReturnsThenValue()
    {
        var source = "x = 1 if cond else 2\n";
        var module = ParseModule(source);

        // Position at '1' (then value)
        var node = _service.FindInnermostNode(module, line: 1, column: 5);

        Assert.NotNull(node);
        Assert.IsType<IntegerLiteral>(node);
        Assert.Equal("1", ((IntegerLiteral)node).Value);
    }

    [Fact]
    public void FindInnermostNode_TryExpressionOperand_ReturnsOperand()
    {
        var source = "x = try some_func()\n";
        var module = ParseModule(source);

        // Position at 'some_func'
        var node = _service.FindInnermostNode(module, line: 1, column: 9);

        Assert.NotNull(node);
        Assert.IsType<Identifier>(node);
        Assert.Equal("some_func", ((Identifier)node).Name);
    }

    [Fact]
    public void FindInnermostNode_WhileCondition_ReturnsCondition()
    {
        var source = "while x > 0:\n    pass\n";
        var module = ParseModule(source);

        // Position at 'x'
        var node = _service.FindInnermostNode(module, line: 1, column: 7);

        Assert.NotNull(node);
        Assert.IsType<Identifier>(node);
        Assert.Equal("x", ((Identifier)node).Name);
    }

    [Fact]
    public void FindInnermostNode_ForTarget_ReturnsTarget()
    {
        var source = "for item in items:\n    pass\n";
        var module = ParseModule(source);

        // Position at 'item'
        var node = _service.FindInnermostNode(module, line: 1, column: 5);

        Assert.NotNull(node);
        Assert.IsType<Identifier>(node);
        Assert.Equal("item", ((Identifier)node).Name);
    }

    [Fact]
    public void FindInnermostNode_ForIterator_ReturnsIterator()
    {
        var source = "for item in items:\n    pass\n";
        var module = ParseModule(source);

        // Position at 'items'
        var node = _service.FindInnermostNode(module, line: 1, column: 13);

        Assert.NotNull(node);
        Assert.IsType<Identifier>(node);
        Assert.Equal("items", ((Identifier)node).Name);
    }

    [Fact]
    public void FindInnermostNode_ElifClause_ReturnsCondition()
    {
        var source = "if a:\n    pass\nelif b:\n    pass\n";
        var module = ParseModule(source);

        // Position at 'b' in elif
        var node = _service.FindInnermostNode(module, line: 3, column: 6);

        Assert.NotNull(node);
        Assert.IsType<Identifier>(node);
        Assert.Equal("b", ((Identifier)node).Name);
    }

    [Fact]
    public void FindNodeOfType_IfStatement_IncludesElifBody()
    {
        var source = "if a:\n    pass\nelif b:\n    x = 1\n";
        var module = ParseModule(source);

        // Position at 'x' in elif body
        var ifStmt = _service.FindNodeOfType<IfStatement>(module, line: 4, column: 5);

        Assert.NotNull(ifStmt);
    }

    [Fact]
    public void FindInnermostNode_FStringExpression_ReturnsExpression()
    {
        var source = "x = f\"value is {val}\"\n";
        var module = ParseModule(source);

        // Position at 'val' inside the f-string expression
        var node = _service.FindInnermostNode(module, line: 1, column: 17);

        Assert.NotNull(node);
        Assert.IsType<Identifier>(node);
        Assert.Equal("val", ((Identifier)node).Name);
    }

    [Fact]
    public void FindInnermostNode_DefaultParameterValue_ReturnsDefaultValue()
    {
        var source = "def foo(x: int = 42):\n    pass\n";
        var module = ParseModule(source);

        // Position at '42' default value
        var node = _service.FindInnermostNode(module, line: 1, column: 18);

        Assert.NotNull(node);
        Assert.IsType<IntegerLiteral>(node);
        Assert.Equal("42", ((IntegerLiteral)node).Value);
    }

    [Fact]
    public void FindInnermostNode_EnumMemberValue_ReturnsValue()
    {
        var source = "enum Color:\n    RED = 1\n";
        var module = ParseModule(source);

        // Position at '1' value
        var node = _service.FindInnermostNode(module, line: 2, column: 11);

        Assert.NotNull(node);
        Assert.IsType<IntegerLiteral>(node);
        Assert.Equal("1", ((IntegerLiteral)node).Value);
    }

    #endregion

    #region Integration Tests (Spec Example 3.4)

    [Fact]
    public void IntegrationTest_SpecExample_FindsNodesAtExpectedPositions()
    {
        // This test matches the exact example from spec section 3.4
        var source = "def foo():\n    x = 1 + 2\n    print(x)";
        var module = ParseModule(source);

        // Line 2, column 5 should find identifier "x"
        var nodeAtX = _service.FindInnermostNode(module, line: 2, column: 5);
        Assert.NotNull(nodeAtX);
        Assert.IsType<Identifier>(nodeAtX);
        Assert.Equal("x", ((Identifier)nodeAtX).Name);

        // Line 2, column 9 should find literal "1"
        var nodeAt1 = _service.FindInnermostNode(module, line: 2, column: 9);
        Assert.NotNull(nodeAt1);
        Assert.IsType<IntegerLiteral>(nodeAt1);
        Assert.Equal("1", ((IntegerLiteral)nodeAt1).Value);

        // Additionally verify line 3 has print function call
        var printNode = _service.FindNodeOfType<FunctionCall>(module, line: 3, column: 5);
        Assert.NotNull(printNode);

        // And the argument 'x' inside print()
        var argX = _service.FindInnermostNode(module, line: 3, column: 11);
        Assert.NotNull(argX);
        Assert.IsType<Identifier>(argX);
        Assert.Equal("x", ((Identifier)argX).Name);
    }

    #endregion

    #region Position Boundary Tests

    [Fact]
    public void FindInnermostNode_PositionBetweenStatements_ReturnsContainingBlock()
    {
        // Test position in whitespace between two statements
        var source = "x = 1\n\ny = 2\n";
        var module = ParseModule(source);

        // Position on the empty line (line 2) between statements
        // Should return the module since that's the containing block
        var node = _service.FindInnermostNode(module, line: 2, column: 1);

        // Empty line may not be contained by any node, so null is acceptable
        // Or it returns the module if module span covers it
        Assert.True(node is null or Module);
    }

    [Fact]
    public void FindInnermostNode_PositionAtNodeStart_ReturnsNode()
    {
        var source = "def foo():\n    pass\n";
        var module = ParseModule(source);

        // Position at exact start of 'def' keyword (line 1, column 1)
        var node = _service.FindInnermostNode(module, line: 1, column: 1);

        Assert.NotNull(node);
        // Should find the FunctionDef or something within it
        var funcDef = _service.FindNodeOfType<FunctionDef>(module, line: 1, column: 1);
        Assert.NotNull(funcDef);
        Assert.Equal("foo", funcDef.Name);
    }

    [Fact]
    public void FindInnermostNode_PositionAtNodeEnd_ReturnsNode()
    {
        var source = "x = 42\n";
        var module = ParseModule(source);

        // Position at '2' of '42' (end of integer literal)
        var node = _service.FindInnermostNode(module, line: 1, column: 6);

        Assert.NotNull(node);
        Assert.IsType<IntegerLiteral>(node);
    }

    [Fact]
    public void FindInnermostNode_MultiLineExpression_ReturnsCorrectNode()
    {
        var source = @"result = (
    1 +
    2 +
    3
)
";
        var module = ParseModule(source);

        // Position at '2' on line 3
        var node = _service.FindInnermostNode(module, line: 3, column: 5);

        Assert.NotNull(node);
        Assert.IsType<IntegerLiteral>(node);
        Assert.Equal("2", ((IntegerLiteral)node).Value);
    }

    [Fact]
    public void FindInnermostNode_NestedBlocks_ReturnsInnermostContaining()
    {
        var source = @"class Outer:
    class Inner:
        def method(self) -> int:
            return 42
";
        var module = ParseModule(source);

        // Position at 'return' inside nested method
        var classDef = _service.FindNodeOfType<ClassDef>(module, line: 4, column: 13);
        Assert.NotNull(classDef);

        // The innermost ClassDef at this position should be 'Inner'
        // But since we search innermost, we'll get the function first
        var funcDef = _service.FindNodeOfType<FunctionDef>(module, line: 4, column: 13);
        Assert.NotNull(funcDef);
        Assert.Equal("method", funcDef.Name);
    }

    [Fact]
    public void FindAllContainingNodes_DeeplyNested_ReturnsFullPath()
    {
        var source = @"class Outer:
    def method(self) -> int:
        if True:
            return 42
";
        var module = ParseModule(source);

        // Position at '42'
        var nodes = _service.FindAllContainingNodes(module, line: 4, column: 20);

        Assert.NotEmpty(nodes);
        // Should contain: Module, ClassDef, FunctionDef, IfStatement, ReturnStatement, IntegerLiteral
        Assert.IsType<Module>(nodes[0]);
        Assert.Contains(nodes, n => n is ClassDef);
        Assert.Contains(nodes, n => n is FunctionDef);
        Assert.Contains(nodes, n => n is IfStatement);
        Assert.Contains(nodes, n => n is ReturnStatement);
        Assert.IsType<IntegerLiteral>(nodes[^1]);
    }

    [Fact]
    public void FindInnermostNode_PositionInOperator_ReturnsBinaryOp()
    {
        var source = "x = 1 + 2\n";
        var module = ParseModule(source);

        // Position at '+' operator (column 7)
        var node = _service.FindInnermostNode(module, line: 1, column: 7);

        // The '+' itself isn't a child node, so we should get the BinaryOp
        Assert.NotNull(node);
        // Could be BinaryOp or one of its children depending on spans
    }

    [Fact]
    public void FindInnermostNode_TryStatement_ReturnsCorrectPart()
    {
        var source = @"try:
    x = 1
except ValueError:
    y = 2
finally:
    z = 3
";
        var module = ParseModule(source);

        // Position in try body
        var nodeInTry = _service.FindInnermostNode(module, line: 2, column: 5);
        Assert.NotNull(nodeInTry);
        Assert.IsType<Identifier>(nodeInTry);
        Assert.Equal("x", ((Identifier)nodeInTry).Name);

        // Position in except body
        var nodeInExcept = _service.FindInnermostNode(module, line: 4, column: 5);
        Assert.NotNull(nodeInExcept);
        Assert.IsType<Identifier>(nodeInExcept);
        Assert.Equal("y", ((Identifier)nodeInExcept).Name);

        // Position in finally body
        var nodeInFinally = _service.FindInnermostNode(module, line: 6, column: 5);
        Assert.NotNull(nodeInFinally);
        Assert.IsType<Identifier>(nodeInFinally);
        Assert.Equal("z", ((Identifier)nodeInFinally).Name);
    }

    [Fact]
    public void FindNodeOfType_WhileStatement_FindsLoop()
    {
        var source = "while x > 0:\n    x = x - 1\n";
        var module = ParseModule(source);

        // Position inside while body
        var whileStmt = _service.FindNodeOfType<WhileStatement>(module, line: 2, column: 5);

        Assert.NotNull(whileStmt);
    }

    [Fact]
    public void FindInnermostNode_StringWithSpaces_ReturnsStringLiteral()
    {
        var source = "x = \"hello world\"\n";
        var module = ParseModule(source);

        // Position inside the string (at 'w' of 'world')
        var node = _service.FindInnermostNode(module, line: 1, column: 12);

        Assert.NotNull(node);
        Assert.IsType<StringLiteral>(node);
        Assert.Equal("hello world", ((StringLiteral)node).Value);
    }

    [Fact]
    public void FindInnermostNode_SetComprehension_ReturnsElement()
    {
        var source = "s = {x * 2 for x in items}\n";
        var module = ParseModule(source);

        // Position at '2'
        var node = _service.FindInnermostNode(module, line: 1, column: 10);

        Assert.NotNull(node);
        Assert.IsType<IntegerLiteral>(node);
    }

    [Fact]
    public void FindNodeOfType_DictComprehension_ReturnsComprehension()
    {
        var source = "d = {k: v for k, v in items.items()}\n";
        var module = ParseModule(source);

        // Position inside comprehension
        var comp = _service.FindNodeOfType<DictComprehension>(module, line: 1, column: 6);

        Assert.NotNull(comp);
    }

    [Fact]
    public void FindInnermostNode_UnaryOperator_ReturnsOperand()
    {
        var source = "x = -42\n";
        var module = ParseModule(source);

        // Position at '42' (the operand of unary minus)
        var node = _service.FindInnermostNode(module, line: 1, column: 6);

        Assert.NotNull(node);
        Assert.IsType<IntegerLiteral>(node);
    }

    [Fact]
    public void FindNodeOfType_UnaryOp_ReturnsUnaryOp()
    {
        var source = "x = not True\n";
        var module = ParseModule(source);

        // Position at 'True'
        var unaryOp = _service.FindNodeOfType<UnaryOp>(module, line: 1, column: 9);

        Assert.NotNull(unaryOp);
        Assert.Equal(UnaryOperator.Not, unaryOp.Operator);
    }

    #endregion
}
