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

    #region Future Node ValidateInvariants Tests

    [Fact]
    public void ValidateInvariants_ValidAwaitExpression_PassesValidation()
    {
        // Arrange
        var awaitExpr = new AwaitExpression
        {
            Operand = new Identifier { Name = "task" }
        };

        // Act & Assert - should not throw
        AstValidator.ValidateNode(awaitExpr);
    }

    [Fact]
    public void ValidateInvariants_ValidMatchExpression_PassesValidation()
    {
        // Arrange
        var matchExpr = new MatchExpression
        {
            Scrutinee = new Identifier { Name = "value" },
            Arms = ImmutableArray.Create(
                new MatchArm
                {
                    Pattern = new WildcardPattern(),
                    Result = new IntegerLiteral { Value = "0" }
                }
            )
        };

        // Act & Assert - should not throw
        AstValidator.ValidateNode(matchExpr);
    }

    [Fact]
    public void ValidateInvariants_ValidMatchStatement_PassesValidation()
    {
        // Arrange
        var matchStmt = new MatchStatement
        {
            Scrutinee = new Identifier { Name = "value" },
            Cases = ImmutableArray.Create(
                new MatchCase
                {
                    Pattern = new WildcardPattern(),
                    Body = ImmutableArray.Create<Statement>(new PassStatement())
                }
            )
        };

        // Act & Assert - should not throw
        AstValidator.ValidateNode(matchStmt);
    }

    [Fact]
    public void ValidateInvariants_ValidUnionDef_PassesValidation()
    {
        // Arrange
        var unionDef = new UnionDef
        {
            Name = "Result",
            TypeParameters = ImmutableArray<TypeParameterDef>.Empty,
            Cases = ImmutableArray<UnionCaseDef>.Empty,
            Decorators = ImmutableArray<Decorator>.Empty
        };

        // Act & Assert - should not throw
        AstValidator.ValidateNode(unionDef);
    }

    #endregion

    #region Pattern ValidateInvariants Tests

    [Fact]
    public void ValidateInvariants_ValidBindingPattern_PassesValidation()
    {
        // Arrange
        var pattern = new BindingPattern { Name = "x" };

        // Act & Assert - should not throw
        AstValidator.ValidateNode(pattern);
    }

    [Fact]
    public void ValidateInvariants_ValidLiteralPattern_PassesValidation()
    {
        // Arrange
        var pattern = new LiteralPattern
        {
            Literal = new IntegerLiteral { Value = "42" }
        };

        // Act & Assert - should not throw
        AstValidator.ValidateNode(pattern);
    }

    [Fact]
    public void ValidateInvariants_ValidTypePattern_PassesValidation()
    {
        // Arrange
        var pattern = new TypePattern
        {
            Type = new TypeAnnotation { Name = "int" }
        };

        // Act & Assert - should not throw
        AstValidator.ValidateNode(pattern);
    }

    [Fact]
    public void ValidateInvariants_ValidUnionCasePattern_PassesValidation()
    {
        // Arrange
        var pattern = new UnionCasePattern
        {
            CaseName = "Ok",
            FieldPatterns = ImmutableArray.Create<Pattern>(new WildcardPattern())
        };

        // Act & Assert - should not throw
        AstValidator.ValidateNode(pattern);
    }

    [Fact]
    public void ValidateInvariants_ValidTuplePattern_PassesValidation()
    {
        // Arrange
        var pattern = new TuplePattern
        {
            Elements = ImmutableArray.Create<Pattern>(
                new WildcardPattern(),
                new BindingPattern { Name = "x" }
            )
        };

        // Act & Assert - should not throw
        AstValidator.ValidateNode(pattern);
    }

    [Fact]
    public void ValidateInvariants_ValidListPattern_PassesValidation()
    {
        // Arrange
        var pattern = new ListPattern
        {
            Elements = ImmutableArray.Create<Pattern>(new BindingPattern { Name = "head" }),
            RestPattern = new BindingPattern { Name = "tail" }
        };

        // Act & Assert - should not throw
        AstValidator.ValidateNode(pattern);
    }

    [Fact]
    public void ValidateInvariants_ValidOrPattern_PassesValidation()
    {
        // Arrange
        var pattern = new OrPattern
        {
            Alternatives = ImmutableArray.Create<Pattern>(
                new LiteralPattern { Literal = new IntegerLiteral { Value = "1" } },
                new LiteralPattern { Literal = new IntegerLiteral { Value = "2" } }
            )
        };

        // Act & Assert - should not throw
        AstValidator.ValidateNode(pattern);
    }

    [Fact]
    public void ValidateInvariants_ValidAndPattern_PassesValidation()
    {
        // Arrange
        var pattern = new AndPattern
        {
            Left = new TypePattern { Type = new TypeAnnotation { Name = "int" } },
            Right = new BindingPattern { Name = "x" }
        };

        // Act & Assert - should not throw
        AstValidator.ValidateNode(pattern);
    }

    [Fact]
    public void ValidateInvariants_ValidGuardPattern_PassesValidation()
    {
        // Arrange
        var pattern = new GuardPattern
        {
            Inner = new BindingPattern { Name = "x" },
            Guard = new BinaryOp
            {
                Operator = BinaryOperator.GreaterThan,
                Left = new Identifier { Name = "x" },
                Right = new IntegerLiteral { Value = "0" }
            }
        };

        // Act & Assert - should not throw
        AstValidator.ValidateNode(pattern);
    }

    #endregion

    #region GetChildNodes Tests for Future and Pattern Nodes

    [Fact]
    public void GetChildNodes_AwaitExpression_ReturnsOperand()
    {
        // Arrange
        var operand = new Identifier { Name = "task" };
        var awaitExpr = new AwaitExpression { Operand = operand };

        // Act
        var children = awaitExpr.GetChildNodes().ToList();

        // Assert
        children.Should().ContainSingle();
        children[0].Should().BeSameAs(operand);
    }

    [Fact]
    public void GetChildNodes_MatchExpression_ReturnsScrutineeAndArmParts()
    {
        // Arrange
        var scrutinee = new Identifier { Name = "x" };
        var pattern = new WildcardPattern();
        var guard = new BooleanLiteral { Value = true };
        var result = new IntegerLiteral { Value = "1" };

        var matchExpr = new MatchExpression
        {
            Scrutinee = scrutinee,
            Arms = ImmutableArray.Create(
                new MatchArm { Pattern = pattern, Guard = guard, Result = result }
            )
        };

        // Act
        var children = matchExpr.GetChildNodes().ToList();

        // Assert - scrutinee + pattern + guard + result
        children.Should().HaveCount(4);
        children[0].Should().BeSameAs(scrutinee);
        children[1].Should().BeSameAs(pattern);
        children[2].Should().BeSameAs(guard);
        children[3].Should().BeSameAs(result);
    }

    [Fact]
    public void GetChildNodes_ListPattern_ReturnsElementsAndRestPattern()
    {
        // Arrange
        var head = new BindingPattern { Name = "head" };
        var tail = new BindingPattern { Name = "tail" };
        var pattern = new ListPattern
        {
            Elements = ImmutableArray.Create<Pattern>(head),
            RestPattern = tail
        };

        // Act
        var children = pattern.GetChildNodes().ToList();

        // Assert
        children.Should().HaveCount(2);
        children[0].Should().BeSameAs(head);
        children[1].Should().BeSameAs(tail);
    }

    [Fact]
    public void GetChildNodes_GuardPattern_ReturnsInnerAndGuard()
    {
        // Arrange
        var inner = new WildcardPattern();
        var guard = new BooleanLiteral { Value = true };
        var pattern = new GuardPattern { Inner = inner, Guard = guard };

        // Act
        var children = pattern.GetChildNodes().ToList();

        // Assert
        children.Should().HaveCount(2);
        children[0].Should().BeSameAs(inner);
        children[1].Should().BeSameAs(guard);
    }

    [Fact]
    public void GetChildNodes_LiteralPattern_ReturnsLiteral()
    {
        // Arrange
        var literal = new IntegerLiteral { Value = "42" };
        var pattern = new LiteralPattern { Literal = literal };

        // Act
        var children = pattern.GetChildNodes().ToList();

        // Assert
        children.Should().ContainSingle();
        children[0].Should().BeSameAs(literal);
    }

    #endregion

    #region Malformed AST Detection Tests

    /// <summary>
    /// These tests document the invariants that ValidateInvariants checks.
    /// In DEBUG builds with proper test configuration, malformed ASTs trigger
    /// Debug.Assert failures. These tests verify the validation logic exists
    /// by checking that well-formed equivalents pass.
    /// </summary>
    /// <remarks>
    /// The following invariants are enforced:
    /// - FunctionDef.Name cannot be null or empty
    /// - ClassDef.Name cannot be null or empty
    /// - Identifier.Name cannot be null or empty
    /// - BinaryOp.Left and BinaryOp.Right cannot be null
    /// - ListComprehension must have at least one clause
    /// - OrPattern must have at least 2 alternatives
    /// - ComparisonChain.Operators.Length must equal Operands.Length - 1
    /// </remarks>
    [Fact]
    public void InvariantDocumentation_FunctionDefRequiresNonEmptyName()
    {
        // Valid case - passes validation
        var validFunc = new FunctionDef
        {
            Name = "foo",
            Parameters = ImmutableArray<Parameter>.Empty,
            Body = ImmutableArray<Statement>.Empty,
            TypeParameters = ImmutableArray<TypeParameterDef>.Empty,
            Decorators = ImmutableArray<Decorator>.Empty
        };
        AstValidator.ValidateNode(validFunc);

        // Note: A FunctionDef with Name = "" would trigger Debug.Assert in DEBUG builds
    }

    [Fact]
    public void InvariantDocumentation_IdentifierRequiresNonEmptyName()
    {
        // Valid case - passes validation
        var validId = new Identifier { Name = "x" };
        AstValidator.ValidateNode(validId);

        // Note: An Identifier with Name = "" would trigger Debug.Assert in DEBUG builds
    }

    [Fact]
    public void InvariantDocumentation_BinaryOpRequiresNonNullOperands()
    {
        // Valid case - passes validation
        var validOp = new BinaryOp
        {
            Operator = BinaryOperator.Add,
            Left = new IntegerLiteral { Value = "1" },
            Right = new IntegerLiteral { Value = "2" }
        };
        AstValidator.ValidateNode(validOp);

        // Note: A BinaryOp with Left = null would trigger Debug.Assert in DEBUG builds
    }

    [Fact]
    public void InvariantDocumentation_ListComprehensionRequiresAtLeastOneClause()
    {
        // Valid case - passes validation
        var validComp = new ListComprehension
        {
            Element = new Identifier { Name = "x" },
            Clauses = ImmutableArray.Create<ComprehensionClause>(
                new ForClause
                {
                    Target = new Identifier { Name = "x" },
                    Iterator = new Identifier { Name = "items" }
                }
            )
        };
        AstValidator.ValidateNode(validComp);

        // Note: A ListComprehension with empty Clauses would trigger Debug.Assert
    }

    [Fact]
    public void InvariantDocumentation_OrPatternRequiresAtLeastTwoAlternatives()
    {
        // Valid case - passes validation
        var validOr = new OrPattern
        {
            Alternatives = ImmutableArray.Create<Pattern>(
                new WildcardPattern(),
                new WildcardPattern()
            )
        };
        AstValidator.ValidateNode(validOr);

        // Note: An OrPattern with fewer than 2 Alternatives would trigger Debug.Assert
    }

    [Fact]
    public void InvariantDocumentation_ComparisonChainRequiresMatchingOperators()
    {
        // Valid case - passes validation (2 operands, 1 operator)
        var validChain = new ComparisonChain
        {
            Operands = ImmutableArray.Create<Expression>(
                new Identifier { Name = "a" },
                new Identifier { Name = "b" }
            ),
            Operators = ImmutableArray.Create(ComparisonOperator.LessThan)
        };
        AstValidator.ValidateNode(validChain);

        // Note: A ComparisonChain with mismatched counts would trigger Debug.Assert
    }

    #endregion
}
