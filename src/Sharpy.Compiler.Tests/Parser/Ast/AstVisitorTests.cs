using System.Collections.Immutable;
using Sharpy.Compiler.Parser.Ast;
using Xunit;

namespace Sharpy.Compiler.Tests.Parser.Ast;

public class AstVisitorTests
{
    [Fact]
    public void Visit_Module_TraversesAllChildren()
    {
        // Arrange
        var module = new Module
        {
            Body = ImmutableArray.Create<Statement>(
                new ExpressionStatement { Expression = new IntegerLiteral { Value = "1" } },
                new ExpressionStatement { Expression = new IntegerLiteral { Value = "2" } }
            )
        };

        var visitor = new NodeCountingVisitor();

        // Act
        visitor.Visit(module);

        // Assert
        Assert.Equal(5, visitor.Count); // Module + 2 ExprStmt + 2 IntLit
    }

    [Fact]
    public void Visit_OverriddenMethod_GetsCalled()
    {
        var literal = new IntegerLiteral { Value = "42" };
        var visitor = new IntegerCollectingVisitor();
        visitor.Visit(literal);
        Assert.Single(visitor.Values);
        Assert.Equal("42", visitor.Values[0]);
    }

    [Fact]
    public void DefaultVisit_TraversesChildren()
    {
        var binOp = new BinaryOp
        {
            Left = new IntegerLiteral { Value = "1" },
            Right = new IntegerLiteral { Value = "2" },
            Operator = BinaryOperator.Add
        };

        var visitor = new IntegerCollectingVisitor();
        visitor.Visit(binOp);
        Assert.Equal(2, visitor.Values.Count);
    }

    [Fact]
    public void GenericVisitor_ReturnsValues()
    {
        var literal = new IntegerLiteral { Value = "42" };
        var visitor = new TypeNameVisitor();
        var result = visitor.Visit(literal);
        Assert.Equal("IntegerLiteral", result);
    }

    [Fact]
    public void GenericVisitor_DefaultVisit_ReturnsDefault()
    {
        // PassStatement has no children and no specific override in TypeNameVisitor
        var pass = new PassStatement();
        var visitor = new TypeNameVisitor();
        var result = visitor.Visit(pass);
        Assert.Null(result); // default! for string is null
    }

    [Fact]
    public void CategoryMethod_VisitExpression_IsCalledForUnoverriddenExpressions()
    {
        var literal = new FloatLiteral { Value = "3.14" };
        var visitor = new ExpressionCountingVisitor();
        visitor.Visit(literal);
        Assert.Equal(1, visitor.ExpressionCount);
    }

    [Fact]
    public void CategoryMethod_VisitStatement_IsCalledForUnoverriddenStatements()
    {
        var pass = new PassStatement();
        var visitor = new StatementCountingVisitor();
        visitor.Visit(pass);
        Assert.Equal(1, visitor.StatementCount);
    }

    [Fact]
    public void CategoryMethod_VisitPattern_IsCalledForUnoverriddenPatterns()
    {
        var wildcard = new WildcardPattern();
        var visitor = new PatternCountingVisitor();
        visitor.Visit(wildcard);
        Assert.Equal(1, visitor.PatternCount);
    }

    [Fact]
    public void CategoryMethod_VisitComprehensionClause_IsCalledForUnoverriddenClauses()
    {
        var ifClause = new IfClause { Condition = new BooleanLiteral { Value = true } };
        var visitor = new ClauseCountingVisitor();
        visitor.Visit(ifClause);
        Assert.Equal(1, visitor.ClauseCount);
    }

    [Fact]
    public void Visit_NestedExpression_TraversesDepthFirst()
    {
        // (1 + 2) * 3
        var tree = new BinaryOp
        {
            Left = new BinaryOp
            {
                Left = new IntegerLiteral { Value = "1" },
                Right = new IntegerLiteral { Value = "2" },
                Operator = BinaryOperator.Add
            },
            Right = new IntegerLiteral { Value = "3" },
            Operator = BinaryOperator.Multiply
        };

        var visitor = new IntegerCollectingVisitor();
        visitor.Visit(tree);
        Assert.Equal(3, visitor.Values.Count);
        Assert.Equal("1", visitor.Values[0]);
        Assert.Equal("2", visitor.Values[1]);
        Assert.Equal("3", visitor.Values[2]);
    }

    [Fact]
    public void Visit_IfStatement_TraversesAllBranches()
    {
        var ifStmt = new IfStatement
        {
            Test = new BooleanLiteral { Value = true },
            ThenBody = ImmutableArray.Create<Statement>(
                new ExpressionStatement { Expression = new IntegerLiteral { Value = "1" } }
            ),
            ElseBody = ImmutableArray.Create<Statement>(
                new ExpressionStatement { Expression = new IntegerLiteral { Value = "2" } }
            )
        };

        var visitor = new IntegerCollectingVisitor();
        visitor.Visit(ifStmt);
        Assert.Equal(2, visitor.Values.Count);
    }

    [Fact]
    public void Visit_FunctionCall_TraversesAllArguments()
    {
        var call = new FunctionCall
        {
            Function = new Identifier { Name = "foo" },
            Arguments = ImmutableArray.Create<Expression>(
                new IntegerLiteral { Value = "1" },
                new IntegerLiteral { Value = "2" }
            ),
            KeywordArguments = ImmutableArray.Create(
                new KeywordArgument { Name = "key", Value = new IntegerLiteral { Value = "3" } }
            )
        };

        var visitor = new IntegerCollectingVisitor();
        visitor.Visit(call);
        Assert.Equal(3, visitor.Values.Count);
    }

    [Fact]
    public void Visit_ListComprehension_TraversesClauses()
    {
        var comprehension = new ListComprehension
        {
            Element = new Identifier { Name = "x" },
            Clauses = ImmutableArray.Create<ComprehensionClause>(
                new ForClause
                {
                    Target = new Identifier { Name = "x" },
                    Iterator = new Identifier { Name = "items" }
                },
                new IfClause { Condition = new BooleanLiteral { Value = true } }
            )
        };

        var visitor = new NodeCountingVisitor();
        visitor.Visit(comprehension);
        // ListComprehension + x + ForClause + x + items + IfClause + True = 7
        Assert.Equal(7, visitor.Count);
    }

    [Fact]
    public void AllConcreteNodeTypes_HaveVisitMethod()
    {
        // Use reflection to verify all concrete Node subtypes have a Visit method
        var nodeType = typeof(Node);
        var assembly = nodeType.Assembly;

        var concreteNodeTypes = assembly.GetTypes()
            .Where(t => t.IsSubclassOf(nodeType) && !t.IsAbstract)
            .ToList();

        var visitorType = typeof(AstVisitor);
        var visitMethods = visitorType.GetMethods()
            .Where(m => m.Name.StartsWith("Visit") && m.GetParameters().Length == 1)
            .Select(m => m.GetParameters()[0].ParameterType)
            .ToHashSet();

        foreach (var type in concreteNodeTypes)
        {
            Assert.True(visitMethods.Contains(type),
                $"AstVisitor is missing a Visit method for {type.Name}");
        }
    }

    [Fact]
    public void AllConcreteNodeTypes_HaveVisitMethodInGenericVisitor()
    {
        // Use reflection to verify all concrete Node subtypes have a Visit method in AstVisitor<T>
        var nodeType = typeof(Node);
        var assembly = nodeType.Assembly;

        var concreteNodeTypes = assembly.GetTypes()
            .Where(t => t.IsSubclassOf(nodeType) && !t.IsAbstract)
            .ToList();

        var visitorType = typeof(AstVisitor<string>);
        var visitMethods = visitorType.GetMethods()
            .Where(m => m.Name.StartsWith("Visit") && m.GetParameters().Length == 1)
            .Select(m => m.GetParameters()[0].ParameterType)
            .ToHashSet();

        foreach (var type in concreteNodeTypes)
        {
            Assert.True(visitMethods.Contains(type),
                $"AstVisitor<T> is missing a Visit method for {type.Name}");
        }
    }

    #region Helper visitors for tests

    private class NodeCountingVisitor : AstVisitor
    {
        public int Count { get; private set; }

        public override void DefaultVisit(Node node)
        {
            Count++;
            base.DefaultVisit(node);
        }
    }

    private class IntegerCollectingVisitor : AstVisitor
    {
        public List<string> Values { get; } = new();

        public override void VisitIntegerLiteral(IntegerLiteral node)
        {
            Values.Add(node.Value);
        }
    }

    private class ExpressionCountingVisitor : AstVisitor
    {
        public int ExpressionCount { get; private set; }

        public override void VisitExpression(Expression node)
        {
            ExpressionCount++;
            // Do not call base to avoid double-counting via DefaultVisit
        }
    }

    private class StatementCountingVisitor : AstVisitor
    {
        public int StatementCount { get; private set; }

        public override void VisitStatement(Statement node)
        {
            StatementCount++;
        }
    }

    private class PatternCountingVisitor : AstVisitor
    {
        public int PatternCount { get; private set; }

        public override void VisitPattern(Pattern node)
        {
            PatternCount++;
        }
    }

    private class ClauseCountingVisitor : AstVisitor
    {
        public int ClauseCount { get; private set; }

        public override void VisitComprehensionClause(ComprehensionClause node)
        {
            ClauseCount++;
        }
    }

    private class TypeNameVisitor : AstVisitor<string>
    {
        public override string VisitIntegerLiteral(IntegerLiteral node) => "IntegerLiteral";
    }

    #endregion
}
