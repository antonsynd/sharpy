using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.CodeGen;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Xunit;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Sharpy.Compiler.Tests.CodeGen;

public class CodeEmitterFactoryTests
{
    private class MockCodeEmitter : ICodeEmitter
    {
        public bool WasCalled { get; private set; }

        public CompilationUnitSyntax GenerateCompilationUnit(Module module)
        {
            WasCalled = true;
            return CompilationUnit();
        }
    }

    private class MockCodeEmitterFactory : ICodeEmitterFactory
    {
        public MockCodeEmitter? LastCreatedEmitter { get; private set; }
        public int CreateCallCount { get; private set; }

        public ICodeEmitter Create(CodeGenContext context, CancellationToken cancellationToken = default)
        {
            CreateCallCount++;
            LastCreatedEmitter = new MockCodeEmitter();
            return LastCreatedEmitter;
        }
    }

    [Fact]
    public void FileCompilationPipeline_UsesInjectedFactory()
    {
        var mockFactory = new MockCodeEmitterFactory();
        var builtins = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtins);
        var semanticInfo = new SemanticInfo();
        var semanticBinding = new SemanticBinding();
        var logger = NullLogger.Instance;

        var pipeline = new FileCompilationPipeline(
            symbolTable, semanticInfo, semanticBinding, logger, mockFactory);

        var module = new Module
        {
            Body = ImmutableArray.Create<Statement>(
                new ExpressionStatement
                {
                    Expression = new FunctionCall
                    {
                        Function = new Identifier { Name = "print" },
                        Arguments = ImmutableArray.Create<Expression>(
                            new IntegerLiteral { Value = "42" })
                    }
                })
        };

        pipeline.ResolveNames(module, CancellationToken.None);

        var importResolver = new ImportResolver(logger);

        var result = pipeline.GenerateCode(
            module, "<test>", importResolver, builtins,
            isEntryPoint: true, projectNamespace: "Test",
            logger, CancellationToken.None);

        Assert.Equal(1, mockFactory.CreateCallCount);
        Assert.NotNull(mockFactory.LastCreatedEmitter);
        Assert.True(mockFactory.LastCreatedEmitter.WasCalled);
    }

    [Fact]
    public void FileCompilationPipeline_DefaultsToRoslynEmitterFactory()
    {
        var builtins = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtins);
        var semanticInfo = new SemanticInfo();
        var semanticBinding = new SemanticBinding();
        var logger = NullLogger.Instance;

        var pipeline = new FileCompilationPipeline(
            symbolTable, semanticInfo, semanticBinding, logger);

        var module = new Module
        {
            Body = ImmutableArray.Create<Statement>(
                new ExpressionStatement
                {
                    Expression = new FunctionCall
                    {
                        Function = new Identifier { Name = "print" },
                        Arguments = ImmutableArray.Create<Expression>(
                            new IntegerLiteral { Value = "42" })
                    }
                })
        };

        pipeline.ResolveNames(module, CancellationToken.None);

        var importResolver = new ImportResolver(logger);

        var result = pipeline.GenerateCode(
            module, "<test>", importResolver, builtins,
            isEntryPoint: true, projectNamespace: "Test",
            logger, CancellationToken.None);

        Assert.NotNull(result.CSharpCode);
        Assert.NotEmpty(result.CSharpCode);
    }
}
