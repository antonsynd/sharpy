using System.Collections.Immutable;
using Sharpy.Compiler.CodeGen;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Tests.Helpers;
using Xunit;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// Tests that unrecognized AST statement types in code generation emit SPY0510 diagnostics
/// instead of being silently dropped.
/// </summary>
[Collection("Sequential")]
public class UnrecognizedStatementDiagnosticTests
{
    /// <summary>
    /// A custom statement type that the RoslynEmitter does not recognize.
    /// Used to verify that unrecognized statement types produce diagnostics.
    /// </summary>
    private record FakeStatement : Statement;

    [Fact]
    public void GenerateBodyStatement_UnrecognizedStatement_EmitsSPY0510()
    {
        var builtins = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtins);
        var context = new CodeGenContext(symbolTable, builtins);
        var emitter = new RoslynEmitter(context);

        var fakeStmt = new FakeStatement { LineStart = 5, ColumnStart = 3 };

        // Call the private GenerateBodyStatement via reflection
        var method = typeof(RoslynEmitter).GetMethod(
            "GenerateBodyStatement",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        method!.Invoke(emitter, new object[] { fakeStmt });

        context.Diagnostics.ShouldHaveErrorWithCode(DiagnosticCodes.CodeGen.UnrecognizedStatementType);

        var error = context.Diagnostics.GetErrors().First(e => e.Code == DiagnosticCodes.CodeGen.UnrecognizedStatementType);
        Assert.Contains("FakeStatement", error.Message);
        Assert.Contains("compiler bug", error.Message);
    }

    [Fact]
    public void GenerateClassMembers_UnrecognizedStatement_EmitsSPY0510()
    {
        var builtins = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtins);

        // Register a class symbol so codegen can process it
        var classSymbol = new TypeSymbol
        {
            Name = "TestClass",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class
        };
        symbolTable.TryDefine(classSymbol);

        var context = new CodeGenContext(symbolTable, builtins);
        var emitter = new RoslynEmitter(context);

        var fakeStmt = new FakeStatement { LineStart = 10, ColumnStart = 1 };
        var classDef = new ClassDef
        {
            Name = "TestClass",
            Body = ImmutableArray.Create<Statement>(fakeStmt)
        };

        var module = new Module
        {
            Body = ImmutableArray.Create<Statement>(classDef)
        };

        emitter.GenerateCompilationUnit(module);

        context.Diagnostics.ShouldHaveErrorWithCode(DiagnosticCodes.CodeGen.UnrecognizedStatementType);

        var error = context.Diagnostics.GetErrors().First(e => e.Code == DiagnosticCodes.CodeGen.UnrecognizedStatementType);
        Assert.Contains("FakeStatement", error.Message);
    }
}
