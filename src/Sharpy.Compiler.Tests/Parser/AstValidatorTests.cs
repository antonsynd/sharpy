using System.Collections.Immutable;
using FluentAssertions;
using Xunit;
using Sharpy.Compiler.Parser.Ast;
using LexerNs = Sharpy.Compiler.Lexer;
using ParserNs = Sharpy.Compiler.Parser;

namespace Sharpy.Compiler.Tests.Parser;

/// <summary>
/// Tests for <see cref="AstValidator"/> recursive AST validation.
/// </summary>
public class AstValidatorTests
{
    private static Module Parse(string source)
    {
        var lexer = new LexerNs.Lexer(source);
        var tokens = lexer.TokenizeAll();
        var parser = new ParserNs.Parser(tokens);
        return parser.ParseModule();
    }

    #region ValidateTree Tests

    [Fact]
    public void ValidateTree_ValidModule_PassesValidation()
    {
        // Arrange
        var module = Parse("x = 1 + 2");

        // Act & Assert - should not throw
        AstValidator.ValidateTree(module);
    }

    [Fact]
    public void ValidateTree_ComplexProgram_PassesValidation()
    {
        // Arrange
        var source = @"
def foo(x: int, y: int) -> int:
    if x > y:
        return x
    else:
        return y

class Point:
    def __init__(self, x: int, y: int) -> None:
        self.x = x
        self.y = y

result = foo(1, 2)
print(result)
";
        var module = Parse(source);

        // Act & Assert - should not throw
        AstValidator.ValidateTree(module);
    }

    [Fact]
    public void ValidateTree_Comprehensions_PassesValidation()
    {
        // Arrange
        var module = Parse("[x * 2 for x in range(10) if x > 5]");

        // Act & Assert - should not throw
        AstValidator.ValidateTree(module);
    }

    [Fact]
    public void ValidateTree_NestedExpressions_PassesValidation()
    {
        // Arrange
        var module = Parse("result = (1 + 2) * (3 - 4) / (5 % 6)");

        // Act & Assert - should not throw
        AstValidator.ValidateTree(module);
    }

    [Fact]
    public void ValidateTree_ControlFlow_PassesValidation()
    {
        // Arrange
        var source = @"
x = 0
while x < 10:
    if x % 2 == 0:
        x = x + 1
        continue
    x = x + 2
    break
";
        var module = Parse(source);

        // Act & Assert - should not throw
        AstValidator.ValidateTree(module);
    }

    [Fact]
    public void ValidateTree_TryExcept_PassesValidation()
    {
        // Arrange
        var source = @"
try:
    x = 1 / 0
except ZeroDivisionError as e:
    print(e)
finally:
    print(""done"")
";
        var module = Parse(source);

        // Act & Assert - should not throw
        AstValidator.ValidateTree(module);
    }

    [Fact]
    public void ValidateTree_Lambda_PassesValidation()
    {
        // Arrange
        var module = Parse("f = lambda x, y: x + y");

        // Act & Assert - should not throw
        AstValidator.ValidateTree(module);
    }

    [Fact]
    public void ValidateTree_DictComprehension_PassesValidation()
    {
        // Arrange
        var module = Parse("{k: v for k, v in items}");

        // Act & Assert - should not throw
        AstValidator.ValidateTree(module);
    }

    [Fact]
    public void ValidateTree_ChainedComparison_PassesValidation()
    {
        // Arrange
        var module = Parse("result = 1 < x < 10");

        // Act & Assert - should not throw
        AstValidator.ValidateTree(module);
    }

    #endregion

    #region CountNodes Tests

    [Fact]
    public void CountNodes_EmptyModule_ReturnsOne()
    {
        // Arrange
        var module = new Module();

        // Act
        var count = AstValidator.CountNodes(module);

        // Assert
        count.Should().Be(1);
    }

    [Fact]
    public void CountNodes_SimpleExpression_ReturnsCorrectCount()
    {
        // Arrange - "x" is Module -> ExpressionStatement -> Identifier = 3 nodes
        var module = Parse("x");

        // Act
        var count = AstValidator.CountNodes(module);

        // Assert
        count.Should().Be(3);
    }

    [Fact]
    public void CountNodes_BinaryOp_ReturnsCorrectCount()
    {
        // Arrange - "1 + 2" is Module -> ExpressionStatement -> BinaryOp -> (IntegerLiteral, IntegerLiteral)
        // = 5 nodes
        var module = Parse("1 + 2");

        // Act
        var count = AstValidator.CountNodes(module);

        // Assert
        count.Should().Be(5);
    }

    [Fact]
    public void CountNodes_FunctionDef_IncludesBodyNodes()
    {
        // Arrange
        var module = Parse(@"
def foo():
    return 1
");

        // Act
        var count = AstValidator.CountNodes(module);

        // Assert - Module -> FunctionDef -> ReturnStatement -> IntegerLiteral = 4+ nodes
        count.Should().BeGreaterThan(3);
    }

    [Fact]
    public void CountNodes_NullRoot_ReturnsZero()
    {
        // Act
        var count = AstValidator.CountNodes(null!);

        // Assert
        count.Should().Be(0);
    }

    #endregion

    #region GetChildNodes Coverage Tests

    [Fact]
    public void GetChildNodes_Module_ReturnsBodyStatements()
    {
        // Arrange
        var module = Parse("x = 1\ny = 2");

        // Act
        var children = module.GetChildNodes().ToList();

        // Assert
        children.Should().HaveCount(2);
        children.Should().AllBeAssignableTo<Statement>();
    }

    [Fact]
    public void GetChildNodes_BinaryOp_ReturnsBothOperands()
    {
        // Arrange
        var module = Parse("1 + 2");
        var binaryOp = ((ExpressionStatement)module.Body[0]).Expression as BinaryOp;

        // Act
        var children = binaryOp!.GetChildNodes().ToList();

        // Assert
        children.Should().HaveCount(2);
        children[0].Should().BeOfType<IntegerLiteral>();
        children[1].Should().BeOfType<IntegerLiteral>();
    }

    [Fact]
    public void GetChildNodes_IfStatement_ReturnsAllBranches()
    {
        // Arrange
        var module = Parse(@"
if x:
    a = 1
elif y:
    b = 2
else:
    c = 3
");
        var ifStmt = module.Body[0] as IfStatement;

        // Act
        var children = ifStmt!.GetChildNodes().ToList();

        // Assert - Test + ThenBody(1) + ElifTest + ElifBody(1) + ElseBody(1)
        children.Should().HaveCountGreaterThanOrEqualTo(5);
    }

    [Fact]
    public void GetChildNodes_ForStatement_ReturnsTargetIteratorAndBody()
    {
        // Arrange
        var module = Parse(@"
for x in items:
    print(x)
");
        var forStmt = module.Body[0] as ForStatement;

        // Act
        var children = forStmt!.GetChildNodes().ToList();

        // Assert - Target + Iterator + Body(1)
        children.Should().HaveCount(3);
    }

    [Fact]
    public void GetChildNodes_FunctionCall_ReturnsFunctionAndArguments()
    {
        // Arrange
        var module = Parse("foo(1, 2, 3)");
        var call = ((ExpressionStatement)module.Body[0]).Expression as FunctionCall;

        // Act
        var children = call!.GetChildNodes().ToList();

        // Assert - Function + 3 arguments
        children.Should().HaveCount(4);
    }

    [Fact]
    public void GetChildNodes_ListComprehension_ReturnsElementAndClauses()
    {
        // Arrange
        var module = Parse("[x for x in items if x > 0]");
        var comp = ((ExpressionStatement)module.Body[0]).Expression as ListComprehension;

        // Act
        var children = comp!.GetChildNodes().ToList();

        // Assert - Element + ForClause + IfClause
        children.Should().HaveCount(3);
    }

    [Fact]
    public void GetChildNodes_SliceAccess_ReturnsNonNullParts()
    {
        // Arrange
        var module = Parse("x[1:10:2]");
        var slice = ((ExpressionStatement)module.Body[0]).Expression as SliceAccess;

        // Act
        var children = slice!.GetChildNodes().ToList();

        // Assert - Object + Start + Stop + Step
        children.Should().HaveCount(4);
    }

    [Fact]
    public void GetChildNodes_SliceAccess_OmitsNullParts()
    {
        // Arrange - x[::] has only Object, no Start/Stop/Step
        var module = Parse("x[::]");
        var slice = ((ExpressionStatement)module.Body[0]).Expression as SliceAccess;

        // Act
        var children = slice!.GetChildNodes().ToList();

        // Assert - Only Object
        children.Should().HaveCount(1);
    }

    #endregion

    #region ValidateInvariants Individual Node Tests

    [Fact]
    public void ValidateInvariants_ValidFunctionDef_PassesValidation()
    {
        // Arrange
        var funcDef = new FunctionDef
        {
            Name = "foo",
            Parameters = ImmutableArray<Parameter>.Empty,
            Body = ImmutableArray<Statement>.Empty,
            TypeParameters = ImmutableArray<TypeParameterDef>.Empty,
            Decorators = ImmutableArray<Decorator>.Empty
        };

        // Act & Assert - should not throw
        AstValidator.ValidateNode(funcDef);
    }

    [Fact]
    public void ValidateInvariants_ValidClassDef_PassesValidation()
    {
        // Arrange
        var classDef = new ClassDef
        {
            Name = "MyClass",
            Body = ImmutableArray<Statement>.Empty,
            TypeParameters = ImmutableArray<TypeParameterDef>.Empty,
            BaseClasses = ImmutableArray<TypeAnnotation>.Empty,
            Decorators = ImmutableArray<Decorator>.Empty
        };

        // Act & Assert - should not throw
        AstValidator.ValidateNode(classDef);
    }

    [Fact]
    public void ValidateInvariants_ValidIdentifier_PassesValidation()
    {
        // Arrange
        var identifier = new Identifier { Name = "x" };

        // Act & Assert - should not throw
        AstValidator.ValidateNode(identifier);
    }

    [Fact]
    public void ValidateInvariants_ValidBinaryOp_PassesValidation()
    {
        // Arrange
        var binaryOp = new BinaryOp
        {
            Operator = BinaryOperator.Add,
            Left = new IntegerLiteral { Value = "1" },
            Right = new IntegerLiteral { Value = "2" }
        };

        // Act & Assert - should not throw
        AstValidator.ValidateNode(binaryOp);
    }

    #endregion
}
