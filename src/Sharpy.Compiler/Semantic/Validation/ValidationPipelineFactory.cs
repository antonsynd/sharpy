using Sharpy.Compiler.Logging;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Factory for creating pre-configured validation pipelines.
/// </summary>
public static class ValidationPipelineFactory
{
    /// <summary>
    /// Create the default pipeline with all standard validators.
    /// Uses CFG-based control flow analysis (V3) for more accurate results.
    /// </summary>
    public static ValidationPipeline CreateDefault(ICompilerLogger? logger = null)
    {
        return new ValidationPipeline(logger)
            // Order values determine execution sequence
            .AddValidator(new SignatureValidatorV2())         // Order: 150 (early, validates dunder signatures)
            .AddValidator(new DefaultParameterValidatorV2())  // Order: 250
            .AddValidator(new ControlFlowValidatorV3())       // Order: 400 (CFG-based, more accurate)
            .AddValidator(new AccessValidatorV2())            // Order: 450
            .AddValidator(new ProtocolValidatorV2())          // Order: 500
            .AddValidator(new OperatorValidatorV2())          // Order: 500
            ;
    }

    /// <summary>
    /// Create a pipeline using the legacy AST-walking control flow validator (V2).
    /// Use this if you need faster compilation at the cost of accuracy.
    /// </summary>
    public static ValidationPipeline CreateWithLegacyControlFlow(ICompilerLogger? logger = null)
    {
        return new ValidationPipeline(logger)
            .AddValidator(new SignatureValidatorV2())
            .AddValidator(new DefaultParameterValidatorV2())
            .AddValidator(new ControlFlowValidatorV2())       // Legacy AST-walking validator
            .AddValidator(new AccessValidatorV2())
            .AddValidator(new ProtocolValidatorV2())
            .AddValidator(new OperatorValidatorV2())
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
            .AddValidator(new ControlFlowValidatorV2());  // V2 is faster for quick checks
        // Skip signature validators, protocol validators, etc.
    }
}
