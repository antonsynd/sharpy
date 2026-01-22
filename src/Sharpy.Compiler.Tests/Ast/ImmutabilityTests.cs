using FluentAssertions;
using Xunit;
using Sharpy.Compiler.Parser.Ast;
using System.Collections.Immutable;

namespace Sharpy.Compiler.Tests.Ast;

/// <summary>
/// Tests that verify AST immutability guarantees.
/// These tests confirm that AST properties use ImmutableArray after migration.
/// </summary>
public class ImmutabilityTests
{
    [Fact]
    public void Module_Body_Is_Immutable()
    {
        var module = new Module { Body = ImmutableArray<Statement>.Empty };
        module.Body.Should().BeOfType<ImmutableArray<Statement>>();
    }

    [Fact]
    public void FunctionDef_Parameters_Is_Immutable()
    {
        var func = new FunctionDef
        {
            Name = "test",
            Parameters = ImmutableArray<Parameter>.Empty,
            Body = ImmutableArray<Statement>.Empty
        };
        func.Parameters.Should().BeOfType<ImmutableArray<Parameter>>();
        func.Body.Should().BeOfType<ImmutableArray<Statement>>();
    }

    [Fact]
    public void ClassDef_Body_Is_Immutable()
    {
        var classDef = new ClassDef
        {
            Name = "TestClass",
            Body = ImmutableArray<Statement>.Empty
        };
        classDef.Body.Should().BeOfType<ImmutableArray<Statement>>();
    }

    [Fact]
    public void IfStatement_Collections_Are_Immutable()
    {
        var ifStmt = new IfStatement
        {
            Test = new BooleanLiteral { Value = true },
            ThenBody = ImmutableArray<Statement>.Empty,
            ElifClauses = ImmutableArray<ElifClause>.Empty,
            ElseBody = ImmutableArray<Statement>.Empty
        };
        ifStmt.ThenBody.Should().BeOfType<ImmutableArray<Statement>>();
        ifStmt.ElifClauses.Should().BeOfType<ImmutableArray<ElifClause>>();
        ifStmt.ElseBody.Should().BeOfType<ImmutableArray<Statement>>();
    }

    [Fact]
    public void ListLiteral_Elements_Is_Immutable()
    {
        var list = new ListLiteral { Elements = ImmutableArray<Expression>.Empty };
        list.Elements.Should().BeOfType<ImmutableArray<Expression>>();
    }

    [Fact]
    public void FunctionCall_Arguments_Are_Immutable()
    {
        var call = new FunctionCall
        {
            Function = new Identifier { Name = "test" },
            Arguments = ImmutableArray<Expression>.Empty,
            KeywordArguments = ImmutableArray<KeywordArgument>.Empty
        };
        call.Arguments.Should().BeOfType<ImmutableArray<Expression>>();
        call.KeywordArguments.Should().BeOfType<ImmutableArray<KeywordArgument>>();
    }

    [Fact]
    public void TypeAnnotation_TypeArguments_Is_Immutable()
    {
        var typeAnnotation = new TypeAnnotation
        {
            Name = "list",
            TypeArguments = ImmutableArray<TypeAnnotation>.Empty
        };
        typeAnnotation.TypeArguments.Should().BeOfType<ImmutableArray<TypeAnnotation>>();
    }

    [Fact]
    public void TryStatement_Collections_Are_Immutable()
    {
        var tryStmt = new TryStatement
        {
            Body = ImmutableArray<Statement>.Empty,
            Handlers = ImmutableArray<ExceptHandler>.Empty,
            ElseBody = ImmutableArray<Statement>.Empty,
            FinallyBody = ImmutableArray<Statement>.Empty
        };
        tryStmt.Body.Should().BeOfType<ImmutableArray<Statement>>();
        tryStmt.Handlers.Should().BeOfType<ImmutableArray<ExceptHandler>>();
    }

    [Fact]
    public void ForStatement_Collections_Are_Immutable()
    {
        var forStmt = new ForStatement
        {
            Target = new Identifier { Name = "i" },
            Iterator = new FunctionCall { Function = new Identifier { Name = "range" } },
            Body = ImmutableArray<Statement>.Empty,
            ElseBody = ImmutableArray<Statement>.Empty
        };
        forStmt.Body.Should().BeOfType<ImmutableArray<Statement>>();
        forStmt.ElseBody.Should().BeOfType<ImmutableArray<Statement>>();
    }

    [Fact]
    public void ComparisonChain_Collections_Are_Immutable()
    {
        var chain = new ComparisonChain
        {
            Operands = ImmutableArray<Expression>.Empty,
            Operators = ImmutableArray<ComparisonOperator>.Empty
        };
        chain.Operands.Should().BeOfType<ImmutableArray<Expression>>();
        chain.Operators.Should().BeOfType<ImmutableArray<ComparisonOperator>>();
    }

    [Fact]
    public void EnumDef_Members_Is_Immutable()
    {
        var enumDef = new EnumDef
        {
            Name = "TestEnum",
            Members = ImmutableArray<EnumMember>.Empty
        };
        enumDef.Members.Should().BeOfType<ImmutableArray<EnumMember>>();
    }

    [Fact]
    public void InterfaceDef_Collections_Are_Immutable()
    {
        var interfaceDef = new InterfaceDef
        {
            Name = "ITest",
            TypeParameters = ImmutableArray<TypeParameterDef>.Empty,
            BaseInterfaces = ImmutableArray<TypeAnnotation>.Empty,
            Body = ImmutableArray<Statement>.Empty
        };
        interfaceDef.TypeParameters.Should().BeOfType<ImmutableArray<TypeParameterDef>>();
        interfaceDef.BaseInterfaces.Should().BeOfType<ImmutableArray<TypeAnnotation>>();
        interfaceDef.Body.Should().BeOfType<ImmutableArray<Statement>>();
    }
}
