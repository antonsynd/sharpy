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
        public CodeGenContext? LastContext { get; private set; }
        public int CreateCallCount { get; private set; }

        public ICodeEmitter Create(CodeGenContext context, CancellationToken cancellationToken = default)
        {
            CreateCallCount++;
            LastContext = context;
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
            logger, Sharpy.Compiler.Shared.FeatureFlags.None, CancellationToken.None);

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
            logger, Sharpy.Compiler.Shared.FeatureFlags.None, CancellationToken.None);

        Assert.NotNull(result.CSharpCode);
        Assert.NotEmpty(result.CSharpCode);
    }

    [Fact]
    public void GenerateCode_ThreadsUnionOfCompilationWideAndFutureFeaturesIntoContext()
    {
        var mockFactory = new MockCodeEmitterFactory();
        var builtins = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtins);
        var semanticInfo = new SemanticInfo();
        var semanticBinding = new SemanticBinding();
        var logger = NullLogger.Instance;

        var pipeline = new FileCompilationPipeline(
            symbolTable, semanticInfo, semanticBinding, logger, mockFactory);

        // A semantic-scoped feature enabled per-file via `from __future__ import`.
        const string filePath = "test.spy";
        var source = new Sharpy.Compiler.Text.SourceText(
            "from __future__ import __test_feature\n", filePath);
        var lexResult = FileCompilationPipeline.Lex(source, logger);
        var parseResult = FileCompilationPipeline.Parse(lexResult.Tokens, logger);
        Assert.NotNull(parseResult.Module);
        var module = parseResult.Module!;

        var nameResult = pipeline.ResolveNames(module);
        var importResult = pipeline.ResolveImports(
            module, nameResult.NameResolver, filePath, moduleRegistry: null);

        // A distinct compilation-wide feature; Enable does not validate names.
        var compilationFeatures = Sharpy.Compiler.Shared.FeatureFlags.None.Enable("compilation_wide");

        pipeline.GenerateCode(
            module, filePath, importResult.ImportResolver, builtins,
            isEntryPoint: true, projectNamespace: "Test",
            logger, compilationFeatures, CancellationToken.None);

        Assert.NotNull(mockFactory.LastContext);
        var features = mockFactory.LastContext!.Features;
        // The context receives the union of both sources.
        Assert.True(features.IsEnabled("__test_feature"),
            "per-file `from __future__ import` feature should reach the codegen context");
        Assert.True(features.IsEnabled("compilation_wide"),
            "compilation-wide feature should reach the codegen context");
    }
}
