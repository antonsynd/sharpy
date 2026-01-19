using Xunit;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Validation;

namespace Sharpy.Compiler.Tests.Semantic.Validation;

public class TypeCheckerPipelineIntegrationTests
{
    private (SymbolTable symbolTable, SemanticInfo semanticInfo, TypeResolver typeResolver, Module module)
        SetupWithNameResolution(string code)
    {
        var lexer = new Sharpy.Compiler.Lexer.Lexer(code);
        var tokens = lexer.TokenizeAll();
        var parser = new Sharpy.Compiler.Parser.Parser(tokens);
        var module = parser.ParseModule();

        var builtinRegistry = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtinRegistry);
        var semanticInfo = new SemanticInfo();
        var typeResolver = new TypeResolver(symbolTable, semanticInfo);

        var nameResolver = new NameResolver(symbolTable);
        nameResolver.ResolveDeclarations(module);
        nameResolver.ResolveInheritance();

        return (symbolTable, semanticInfo, typeResolver, module);
    }

    [Fact]
    public void LegacyMode_StillWorks()
    {
        var code = @"
def foo() -> int:
    x = 5
";
        var (symbolTable, semanticInfo, typeResolver, module) = SetupWithNameResolution(code);

        // No pipeline (legacy mode)
        var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver);
        typeChecker.CheckModule(module);

        // Legacy mode still works - control flow errors should be present
        Assert.True(typeChecker.Errors.Any(e => e.Message.Contains("must return")));
    }

    [Fact]
    public void PipelineMode_WithControlFlowValidator()
    {
        var code = @"
def foo() -> int:
    x = 5
";
        var (symbolTable, semanticInfo, typeResolver, module) = SetupWithNameResolution(code);

        var pipeline = new ValidationPipeline()
            .AddValidator(new ControlFlowValidatorV2());

        var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver,
            validationPipeline: pipeline);
        typeChecker.CheckModule(module);

        // Pipeline mode should also report control flow errors
        Assert.True(typeChecker.Errors.Any(e => e.Message.Contains("must return")));
    }

    [Fact]
    public void PipelineMode_CombinesWithTypeErrors()
    {
        var code = @"
def foo() -> int:
    x: str = 5
";
        var (symbolTable, semanticInfo, typeResolver, module) = SetupWithNameResolution(code);

        var pipeline = new ValidationPipeline()
            .AddValidator(new ControlFlowValidatorV2());

        var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver,
            validationPipeline: pipeline);
        typeChecker.CheckModule(module);

        // Should have both type error AND control flow error
        var errors = typeChecker.Errors;
        Assert.True(errors.Any(e => e.Message.Contains("type")),
            "Should have type error");
        Assert.True(errors.Any(e => e.Message.Contains("must return")),
            "Should have control flow error");
    }

    [Fact]
    public void EmptyPipeline_DisablesLegacyControlFlowValidator()
    {
        var code = @"
def foo() -> int:
    x = 5
";
        var (symbolTable, semanticInfo, typeResolver, module) = SetupWithNameResolution(code);

        // Empty pipeline - no ControlFlowValidator added
        var pipeline = new ValidationPipeline();

        var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver,
            validationPipeline: pipeline);
        typeChecker.CheckModule(module);

        // With empty pipeline, legacy control flow validator is not used
        // So no control flow errors should be present
        Assert.False(typeChecker.Errors.Any(e => e.Message.Contains("must return")));
    }

    [Fact]
    public void CreateSemanticContext_ReturnsValidContext()
    {
        var code = @"
def foo() -> int:
    return 5
";
        var (symbolTable, semanticInfo, typeResolver, module) = SetupWithNameResolution(code);

        var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver);

        var context = typeChecker.CreateSemanticContext();

        Assert.NotNull(context);
        Assert.Same(symbolTable, context.SymbolTable);
        Assert.Same(semanticInfo, context.SemanticInfo);
        Assert.Same(typeResolver, context.TypeResolver);
    }

    [Fact]
    public void PipelineMode_ValidCodePasses()
    {
        var code = @"
def foo() -> int:
    return 5
";
        var (symbolTable, semanticInfo, typeResolver, module) = SetupWithNameResolution(code);

        var pipeline = new ValidationPipeline()
            .AddValidator(new ControlFlowValidatorV2());

        var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver,
            validationPipeline: pipeline);
        typeChecker.CheckModule(module);

        Assert.Empty(typeChecker.Errors);
    }

    [Fact]
    public void PipelineMode_MultipleValidators()
    {
        var code = @"
def foo() -> int:
    break
    return 5
";
        var (symbolTable, semanticInfo, typeResolver, module) = SetupWithNameResolution(code);

        var pipeline = new ValidationPipeline()
            .AddValidator(new ControlFlowValidatorV2());

        var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver,
            validationPipeline: pipeline);
        typeChecker.CheckModule(module);

        // Should detect break outside loop
        var errors = typeChecker.Errors;
        Assert.True(errors.Any(e => e.Message.Contains("'break' statement outside loop")));
    }

    [Fact]
    public void PipelineMode_PreserveErrorLocation()
    {
        var code = @"
def foo() -> int:
    x = 5
";
        var (symbolTable, semanticInfo, typeResolver, module) = SetupWithNameResolution(code);

        var pipeline = new ValidationPipeline()
            .AddValidator(new ControlFlowValidatorV2());

        var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver,
            validationPipeline: pipeline);
        typeChecker.CheckModule(module);

        var controlFlowError = typeChecker.Errors.FirstOrDefault(e => e.Message.Contains("must return"));
        Assert.NotNull(controlFlowError);
        Assert.True(controlFlowError.Line.HasValue, "Error should have line number");
    }
}
