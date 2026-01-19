using Sharpy.Compiler.Logging;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Factory for creating pre-configured validation pipelines.
/// </summary>
public static class ValidationPipelineFactory
{
    /// <summary>
    /// Create the default pipeline with all standard validators.
    /// This matches the behavior of the pre-pipeline TypeChecker.
    /// </summary>
    public static ValidationPipeline CreateDefault(ICompilerLogger? logger = null)
    {
        return new ValidationPipeline(logger)
            // Order values determine execution sequence
            .AddValidator(new ControlFlowValidatorV2())
            // Add other V2 validators as they are migrated:
            // .AddValidator(new OperatorValidatorV2())
            // .AddValidator(new AccessValidatorV2())
            // .AddValidator(new ProtocolValidatorV2())
            // .AddValidator(new DefaultParameterValidatorV2())
            // .AddValidator(new OperatorSignatureValidatorV2())
            // .AddValidator(new ProtocolSignatureValidatorV2())
            ;
    }

    /// <summary>
    /// Create a minimal pipeline for testing.
    /// </summary>
    public static ValidationPipeline CreateMinimal(ICompilerLogger? logger = null)
    {
        return new ValidationPipeline(logger);
    }

    /// <summary>
    /// Create pipeline for fast compilation (skip expensive validators).
    /// Useful for IDE/LSP scenarios where speed matters more than completeness.
    /// </summary>
    public static ValidationPipeline CreateFast(ICompilerLogger? logger = null)
    {
        return new ValidationPipeline(logger)
            .AddValidator(new ControlFlowValidatorV2());
            // Skip signature validators, protocol validators, etc.
    }
}
