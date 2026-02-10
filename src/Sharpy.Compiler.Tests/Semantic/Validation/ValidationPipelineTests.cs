using System.Collections.Immutable;
using Xunit;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Validation;
using Sharpy.Compiler.Diagnostics;

namespace Sharpy.Compiler.Tests.Semantic.Validation;

public class ValidationPipelineTests
{
    private SemanticContext CreateTestContext()
    {
        var builtinRegistry = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtinRegistry);
        var semanticInfo = new SemanticInfo();
        var typeResolver = new TypeResolver(symbolTable, semanticInfo);
        return new SemanticContext(symbolTable, semanticInfo, typeResolver);
    }

    private Module CreateEmptyModule()
    {
        return new Module { Body = ImmutableArray<Statement>.Empty };
    }

    [Fact]
    public void AddValidator_AddsToList()
    {
        var pipeline = new ValidationPipeline();
        var validator = new TestValidator("Test", 100);

        pipeline.AddValidator(validator);

        Assert.Single(pipeline.Validators);
    }

    [Fact]
    public void AddValidator_SortsByOrder()
    {
        var pipeline = new ValidationPipeline();

        pipeline.AddValidator(new TestValidator("Third", 300));
        pipeline.AddValidator(new TestValidator("First", 100));
        pipeline.AddValidator(new TestValidator("Second", 200));

        Assert.Equal("First", pipeline.Validators[0].Name);
        Assert.Equal("Second", pipeline.Validators[1].Name);
        Assert.Equal("Third", pipeline.Validators[2].Name);
    }

    [Fact]
    public void Validate_RunsValidatorsInOrder()
    {
        var executionOrder = new List<string>();
        var pipeline = new ValidationPipeline();

        pipeline.AddValidator(new OrderTrackingValidator("Third", 300, executionOrder));
        pipeline.AddValidator(new OrderTrackingValidator("First", 100, executionOrder));
        pipeline.AddValidator(new OrderTrackingValidator("Second", 200, executionOrder));

        var context = CreateTestContext();
        pipeline.Validate(CreateEmptyModule(), context);

        Assert.Equal(new[] { "First", "Second", "Third" }, executionOrder);
    }

    [Fact]
    public void Validate_StopsOnMaxErrors()
    {
        var executionOrder = new List<string>();
        var pipeline = new ValidationPipeline();

        pipeline.AddValidator(new ErrorProducingValidator("First", 100, executionOrder, 10));
        pipeline.AddValidator(new OrderTrackingValidator("Second", 200, executionOrder));

        var context = CreateTestContext();
        context.MaxErrors = 5;
        pipeline.Validate(CreateEmptyModule(), context);

        // Second validator should not run because max errors exceeded
        Assert.Single(executionOrder);
        Assert.Equal("First", executionOrder[0]);
    }

    [Fact]
    public void Validate_ContinuesAfterErrorsIfConfigured()
    {
        var executionOrder = new List<string>();
        var pipeline = new ValidationPipeline();

        pipeline.AddValidator(new ErrorProducingValidator("First", 100, executionOrder, 1));
        pipeline.AddValidator(new OrderTrackingValidator("Second", 200, executionOrder));

        var context = CreateTestContext();
        context.ContinueAfterErrors = true;
        context.MaxErrors = 100;
        pipeline.Validate(CreateEmptyModule(), context);

        Assert.Equal(2, executionOrder.Count);
    }

    [Fact]
    public void RemoveValidator_RemovesByType()
    {
        var pipeline = new ValidationPipeline();
        pipeline.AddValidator(new TestValidator("Test1", 100));
        pipeline.AddValidator(new TestValidator("Test2", 200));

        pipeline.RemoveValidator<TestValidator>();

        Assert.Empty(pipeline.Validators);
    }

    [Fact]
    public void AddValidators_AddsMultipleValidators()
    {
        var pipeline = new ValidationPipeline();

        pipeline.AddValidators(
            new TestValidator("Second", 200),
            new TestValidator("First", 100),
            new TestValidator("Third", 300));

        Assert.Equal(3, pipeline.Validators.Count);
        Assert.Equal("First", pipeline.Validators[0].Name);
        Assert.Equal("Second", pipeline.Validators[1].Name);
        Assert.Equal("Third", pipeline.Validators[2].Name);
    }

    [Fact]
    public void Validate_ReturnsDiagnosticBag()
    {
        var pipeline = new ValidationPipeline();
        pipeline.AddValidator(new ErrorProducingValidator("Test", 100, new List<string>(), 3));

        var context = CreateTestContext();
        var result = pipeline.Validate(CreateEmptyModule(), context);

        Assert.NotNull(result);
        Assert.Equal(3, result.ErrorCount);
    }

    [Fact]
    public void Constructor_ReturnsEmptyPipeline()
    {
        var pipeline = new ValidationPipeline();

        Assert.Empty(pipeline.Validators);
    }

    [Fact]
    public void DefaultPipeline_HasAllValidators()
    {
        var pipeline = ValidationPipelineFactory.CreateDefault();
        var validators = pipeline.Validators.ToList();

        Assert.Equal(14, validators.Count);  // Includes warning validators
        Assert.Contains(validators, v => v is ModuleLevelValidator);
        Assert.Contains(validators, v => v is NamingConventionValidator);
        Assert.Contains(validators, v => v is DecoratorValidator);
        Assert.Contains(validators, v => v is SignatureValidator);
        Assert.Contains(validators, v => v is EqualityContractValidator);
        Assert.Contains(validators, v => v is InterfaceConflictValidator);
        Assert.Contains(validators, v => v is DefaultParameterValidator);
        Assert.Contains(validators, v => v is ControlFlowValidator);
        Assert.Contains(validators, v => v is UnusedVariableValidator);
        Assert.Contains(validators, v => v is UnusedImportValidator);
        Assert.Contains(validators, v => v is AccessValidator);
        Assert.Contains(validators, v => v is DunderInvocationValidator);
        Assert.Contains(validators, v => v is ProtocolValidator);
        Assert.Contains(validators, v => v is OperatorValidator);
    }

    [Fact]
    public void DefaultPipeline_ValidatorsInCorrectOrder()
    {
        var pipeline = ValidationPipelineFactory.CreateDefault();
        var orders = pipeline.Validators.Select(v => v.Order).ToList();

        // Should be sorted
        Assert.Equal(orders.OrderBy(o => o).ToList(), orders);

        // ModuleLevelValidator should be first (Order 50)
        Assert.Equal(50, orders[0]);
    }

    [Fact]
    public void DefaultPipeline_ModuleLevelValidatorFirst()
    {
        var pipeline = ValidationPipelineFactory.CreateDefault();
        var firstValidator = pipeline.Validators.FirstOrDefault();

        Assert.NotNull(firstValidator);
        Assert.IsType<ModuleLevelValidator>(firstValidator);
        Assert.Equal("ModuleLevelValidator", firstValidator.Name);
    }

    [Fact]
    public void DefaultPipeline_ControlFlowValidatorPresent()
    {
        var pipeline = ValidationPipelineFactory.CreateDefault();
        var controlFlowValidator = pipeline.Validators.FirstOrDefault(v => v is ControlFlowValidator);

        Assert.NotNull(controlFlowValidator);
        Assert.Equal(400, controlFlowValidator.Order);
    }

    [Fact]
    public void FastPipeline_HasOnlyControlFlowValidator()
    {
        var pipeline = ValidationPipelineFactory.CreateFast();
        var validators = pipeline.Validators.ToList();

        Assert.Single(validators);
        Assert.IsType<ControlFlowValidator>(validators[0]);
    }

    [Fact]
    public void MinimalPipeline_HasNoValidators()
    {
        var pipeline = ValidationPipelineFactory.CreateMinimal();

        Assert.Empty(pipeline.Validators);
    }

    [Fact]
    public void Validate_StopsWhenContinueAfterErrorsIsFalse()
    {
        var executionOrder = new List<string>();
        var pipeline = new ValidationPipeline();

        pipeline.AddValidator(new ErrorProducingValidator("First", 100, executionOrder, 1));
        pipeline.AddValidator(new OrderTrackingValidator("Second", 200, executionOrder));

        var context = CreateTestContext();
        context.ContinueAfterErrors = false;
        pipeline.Validate(CreateEmptyModule(), context);

        Assert.Single(executionOrder);
    }

    [Fact]
    public void Validate_ExceptionThrown_IncludesExceptionTypeInDiagnostic()
    {
        var pipeline = new ValidationPipeline();
        var exception = new NullReferenceException("Test null reference");
        pipeline.AddValidator(new ExceptionThrowingValidator("BrokenValidator", 100, exception));

        var context = CreateTestContext();
        context.ContinueAfterErrors = true;
        context.MaxErrors = 100;

        // Should not throw - exception should be caught and reported as diagnostic
        var result = pipeline.Validate(CreateEmptyModule(), context);

        // Verify diagnostic was added
        Assert.True(result.HasErrors);
        Assert.Equal(1, result.ErrorCount);

        // Verify the diagnostic message includes the exception type name
        var errors = result.GetErrors().ToList();
        Assert.Single(errors);
        Assert.Contains("NullReferenceException", errors[0].Message);
        Assert.Contains("BrokenValidator", errors[0].Message);
    }

    [Fact]
    public void Validate_ExceptionThrown_ContinuesToNextValidator()
    {
        var executionOrder = new List<string>();
        var pipeline = new ValidationPipeline();

        pipeline.AddValidator(new ExceptionThrowingValidator("BrokenValidator", 100, new InvalidOperationException("Test")));
        pipeline.AddValidator(new OrderTrackingValidator("SecondValidator", 200, executionOrder));

        var context = CreateTestContext();
        context.ContinueAfterErrors = true;
        context.MaxErrors = 100;

        pipeline.Validate(CreateEmptyModule(), context);

        // Second validator should still run after exception in first
        Assert.Single(executionOrder);
        Assert.Equal("SecondValidator", executionOrder[0]);
    }

    [Fact]
    public void Validate_ExceptionThrown_UsesCorrectDiagnosticCode()
    {
        var pipeline = new ValidationPipeline();
        pipeline.AddValidator(new ExceptionThrowingValidator("Validator", 100, new ArgumentException("Test")));

        var context = CreateTestContext();
        context.ContinueAfterErrors = true;

        var result = pipeline.Validate(CreateEmptyModule(), context);

        var errors = result.GetErrors().ToList();
        Assert.Single(errors);
        Assert.Equal(DiagnosticCodes.Infrastructure.CompilationFailed, errors[0].Code);
    }

    // Test helper classes
    private class TestValidator : ISemanticValidator
    {
        public string Name { get; }
        public int Order { get; }

        public TestValidator(string name, int order)
        {
            Name = name;
            Order = order;
        }

        public void Validate(Module module, SemanticContext context) { }
    }

    private class OrderTrackingValidator : ISemanticValidator
    {
        private readonly List<string> _executionOrder;

        public string Name { get; }
        public int Order { get; }

        public OrderTrackingValidator(string name, int order, List<string> executionOrder)
        {
            Name = name;
            Order = order;
            _executionOrder = executionOrder;
        }

        public void Validate(Module module, SemanticContext context)
        {
            _executionOrder.Add(Name);
        }
    }

    private class ErrorProducingValidator : ISemanticValidator
    {
        private readonly List<string> _executionOrder;
        private readonly int _errorCount;

        public string Name { get; }
        public int Order { get; }

        public ErrorProducingValidator(string name, int order, List<string> executionOrder, int errorCount)
        {
            Name = name;
            Order = order;
            _executionOrder = executionOrder;
            _errorCount = errorCount;
        }

        public void Validate(Module module, SemanticContext context)
        {
            _executionOrder.Add(Name);
            for (int i = 0; i < _errorCount; i++)
            {
                context.Diagnostics.AddError($"Error {i}");
            }
        }
    }

    private class ExceptionThrowingValidator : ISemanticValidator
    {
        private readonly Exception _exceptionToThrow;

        public string Name { get; }
        public int Order { get; }

        public ExceptionThrowingValidator(string name, int order, Exception exceptionToThrow)
        {
            Name = name;
            Order = order;
            _exceptionToThrow = exceptionToThrow;
        }

        public void Validate(Module module, SemanticContext context)
        {
            throw _exceptionToThrow;
        }
    }
}
