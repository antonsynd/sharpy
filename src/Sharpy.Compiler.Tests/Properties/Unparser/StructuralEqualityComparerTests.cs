using System.Collections.Immutable;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Pretty;
using Xunit;

namespace Sharpy.Compiler.Tests.Properties.Unparser;

[Trait("Category", "Property")]
public class StructuralEqualityComparerTests
{
    private static readonly StructuralEqualityComparer Comparer = StructuralEqualityComparer.Instance;

    [Fact]
    public void SameNode_ReturnsTrue()
    {
        var node = new IntegerLiteral { Value = "42" };
        Assert.True(Comparer.Equals(node, node));
    }

    [Fact]
    public void EqualNodes_DifferentPositions_ReturnsTrue()
    {
        var a = new IntegerLiteral { Value = "42", LineStart = 1, ColumnStart = 5 };
        var b = new IntegerLiteral { Value = "42", LineStart = 3, ColumnStart = 10 };
        Assert.True(Comparer.Equals(a, b));
    }

    [Fact]
    public void DifferentValues_ReturnsFalse()
    {
        var a = new IntegerLiteral { Value = "42" };
        var b = new IntegerLiteral { Value = "43" };
        Assert.False(Comparer.Equals(a, b));
    }

    [Fact]
    public void DifferentNodeTypes_ReturnsFalse()
    {
        var a = new IntegerLiteral { Value = "1" };
        var b = new FloatLiteral { Value = "1.0" };
        Assert.False(Comparer.Equals(a, (Node)b));
    }

    [Fact]
    public void NullNodes_HandledCorrectly()
    {
        Assert.True(Comparer.Equals(null, null));
        Assert.False(Comparer.Equals(new PassStatement(), null));
        Assert.False(Comparer.Equals(null, new PassStatement()));
    }

    [Fact]
    public void BinaryOp_SameOperands_ReturnsTrue()
    {
        var a = new BinaryOp
        {
            Operator = BinaryOperator.Add,
            Left = new IntegerLiteral { Value = "1", LineStart = 1 },
            Right = new IntegerLiteral { Value = "2", LineStart = 1, ColumnStart = 5 },
            LineStart = 1
        };
        var b = new BinaryOp
        {
            Operator = BinaryOperator.Add,
            Left = new IntegerLiteral { Value = "1", LineStart = 5 },
            Right = new IntegerLiteral { Value = "2", LineStart = 7, ColumnStart = 3 },
            LineStart = 5
        };
        Assert.True(Comparer.Equals(a, b));
    }

    [Fact]
    public void BinaryOp_DifferentOperator_ReturnsFalse()
    {
        var left = new IntegerLiteral { Value = "1" };
        var right = new IntegerLiteral { Value = "2" };
        var a = new BinaryOp { Operator = BinaryOperator.Add, Left = left, Right = right };
        var b = new BinaryOp { Operator = BinaryOperator.Subtract, Left = left, Right = right };
        Assert.False(Comparer.Equals(a, b));
    }

    [Fact]
    public void Module_BodyEquality()
    {
        var body = ImmutableArray.Create<Statement>(
            new ExpressionStatement { Expression = new IntegerLiteral { Value = "1" } });
        var a = new Module { Body = body, LineStart = 1 };
        var b = new Module { Body = body, LineStart = 10 };
        Assert.True(Comparer.Equals(a, b));
    }

    [Fact]
    public void FunctionDef_ParameterEquality()
    {
        var makeFunc = (int line) => new FunctionDef
        {
            Name = "foo",
            Parameters = ImmutableArray.Create(
                new Parameter { Name = "x", Type = new TypeAnnotation { Name = "int" } }),
            ReturnType = new TypeAnnotation { Name = "str" },
            Body = ImmutableArray.Create<Statement>(new PassStatement()),
            LineStart = line
        };

        Assert.True(Comparer.Equals(makeFunc(1), makeFunc(99)));
    }

    [Fact]
    public void FunctionDef_DifferentName_ReturnsFalse()
    {
        var a = new FunctionDef { Name = "foo", Body = ImmutableArray<Statement>.Empty };
        var b = new FunctionDef { Name = "bar", Body = ImmutableArray<Statement>.Empty };
        Assert.False(Comparer.Equals(a, b));
    }

    [Fact]
    public void StringLiteral_RawFlagMatters()
    {
        var a = new StringLiteral { Value = "hello", IsRaw = false };
        var b = new StringLiteral { Value = "hello", IsRaw = true };
        Assert.False(Comparer.Equals(a, b));
    }

    [Fact]
    public void Decorator_QualifiedPartsEquality()
    {
        var body = ImmutableArray.Create<Statement>(new PassStatement());
        var dec1 = ImmutableArray.Create(new Decorator
        {
            QualifiedParts = ImmutableArray.Create("dataclass"),
            LineStart = 1
        });
        var dec2 = ImmutableArray.Create(new Decorator
        {
            QualifiedParts = ImmutableArray.Create("dataclass"),
            LineStart = 5
        });

        var a = new ClassDef { Name = "Foo", Body = body, Decorators = dec1, LineStart = 2 };
        var b = new ClassDef { Name = "Foo", Body = body, Decorators = dec2, LineStart = 6 };
        Assert.True(Comparer.Equals(a, b));
    }

    [Fact]
    public void TypeAnnotation_OptionalFlag()
    {
        var a = new VariableDeclaration
        {
            Name = "x",
            Type = new TypeAnnotation { Name = "int", IsOptional = true }
        };
        var b = new VariableDeclaration
        {
            Name = "x",
            Type = new TypeAnnotation { Name = "int", IsOptional = false }
        };
        Assert.False(Comparer.Equals(a, b));
    }

    [Fact]
    public void ComparisonChain_OperatorsAndOperands()
    {
        var a = new ComparisonChain
        {
            Operands = ImmutableArray.Create<Expression>(
                new Identifier { Name = "a" },
                new Identifier { Name = "b" },
                new Identifier { Name = "c" }),
            Operators = ImmutableArray.Create(
                ComparisonOperator.LessThan,
                ComparisonOperator.LessThan)
        };
        var b = new ComparisonChain
        {
            Operands = ImmutableArray.Create<Expression>(
                new Identifier { Name = "a", LineStart = 5 },
                new Identifier { Name = "b", LineStart = 5 },
                new Identifier { Name = "c", LineStart = 5 }),
            Operators = ImmutableArray.Create(
                ComparisonOperator.LessThan,
                ComparisonOperator.LessThan),
            LineStart = 5
        };
        Assert.True(Comparer.Equals(a, b));
    }
}
