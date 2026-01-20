using FluentAssertions;
using Xunit;
using Sharpy.Compiler.Parser.Ast;
using System.Collections.Immutable;

namespace Sharpy.Compiler.Tests.Ast;

/// <summary>
/// Tests that verify AST immutability guarantees.
/// These tests verify that AST properties are ImmutableArray after migration.
/// </summary>
public class ImmutabilityTests
{
    [Fact(Skip = "Enable after Phase 2 - currently List<T>")]
    public void Module_Body_Is_Immutable()
    {
        // This test verifies the type after migration
        // Module.Body should be ImmutableArray<Statement>
        var module = new Module();
        // After migration, uncomment: module.Body.GetType().Should().Be(typeof(ImmutableArray<Statement>));
    }

    [Fact(Skip = "Enable after Phase 2 - currently List<T>")]
    public void FunctionDef_Parameters_Is_Immutable()
    {
        // FunctionDef.Parameters should be ImmutableArray<Parameter>
        var func = new FunctionDef { Name = "test" };
        // After migration, uncomment: func.Parameters.GetType().Should().Be(typeof(ImmutableArray<Parameter>));
    }

    [Fact(Skip = "Enable after Phase 2 - currently List<T>")]
    public void ClassDef_Body_Is_Immutable()
    {
        // ClassDef.Body should be ImmutableArray<Statement>
        var classDef = new ClassDef { Name = "TestClass" };
        // After migration, uncomment: classDef.Body.GetType().Should().Be(typeof(ImmutableArray<Statement>));
    }

    [Fact(Skip = "Enable after Phase 2 - currently List<T>")]
    public void IfStatement_ElifClauses_Is_Immutable()
    {
        // IfStatement.ElifClauses should be ImmutableArray<ElifClause>
        var ifStmt = new IfStatement
        {
            Test = new BooleanLiteral { Value = true }
        };
        // After migration, uncomment: ifStmt.ElifClauses.GetType().Should().Be(typeof(ImmutableArray<ElifClause>));
    }

    [Fact(Skip = "Enable after Phase 2 - currently List<T>")]
    public void ListLiteral_Elements_Is_Immutable()
    {
        // ListLiteral.Elements should be ImmutableArray<Expression>
        var list = new ListLiteral();
        // After migration, uncomment: list.Elements.GetType().Should().Be(typeof(ImmutableArray<Expression>));
    }

    [Fact(Skip = "Enable after Phase 2 - currently List<T>")]
    public void FunctionCall_Arguments_Is_Immutable()
    {
        // FunctionCall.Arguments should be ImmutableArray<Expression>
        var call = new FunctionCall
        {
            Function = new Identifier { Name = "test" }
        };
        // After migration, uncomment: call.Arguments.GetType().Should().Be(typeof(ImmutableArray<Expression>));
    }

    [Fact(Skip = "Enable after Phase 2 - currently List<T>")]
    public void TypeAnnotation_TypeArguments_Is_Immutable()
    {
        // TypeAnnotation.TypeArguments should be ImmutableArray<TypeAnnotation>
        var typeAnnotation = new TypeAnnotation { Name = "list" };
        // After migration, uncomment: typeAnnotation.TypeArguments.GetType().Should().Be(typeof(ImmutableArray<TypeAnnotation>));
    }
}
