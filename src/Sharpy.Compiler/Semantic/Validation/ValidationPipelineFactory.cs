using Sharpy.Compiler.Logging;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Factory for creating pre-configured validation pipelines.
/// </summary>
public static class ValidationPipelineFactory
{
    /// <summary>
    /// Create the default pipeline with all standard validators.
    /// Uses AST-walking control flow analysis (V2) which correctly handles
    /// unreachable code detection (V3 CFG-based approach can't detect unreachable
    /// code because the CFG builder skips statements after terminators).
    /// </summary>
    public static ValidationPipeline CreateDefault(ICompilerLogger? logger = null)
    {
        return new ValidationPipeline(logger)
            // Order values determine execution sequence
            .AddValidator(new ModuleLevelValidatorV2())       // Order: 50 (earliest, validates module structure)
            .AddValidator(new SignatureValidatorV2())         // Order: 150 (early, validates dunder signatures)
            .AddValidator(new DefaultParameterValidatorV2())  // Order: 250
            .AddValidator(new ControlFlowValidatorV2())       // Order: 400 (AST-walking, handles unreachable code)
            .AddValidator(new AccessValidatorV2())            // Order: 450
            .AddValidator(new ProtocolValidatorV2())          // Order: 500
            .AddValidator(new OperatorValidatorV2())          // Order: 500
            ;
    }

    /// <summary>
    /// Create a pipeline using CFG-based control flow validator (V3).
    /// V3 is faster but doesn't detect unreachable code (CFG builder skips
    /// unreachable statements). Use for scenarios where unreachable code
    /// detection is not needed.
    /// </summary>
    public static ValidationPipeline CreateWithCfgControlFlow(ICompilerLogger? logger = null)
    {
        return new ValidationPipeline(logger)
            .AddValidator(new ModuleLevelValidatorV2())       // Order: 50 (earliest)
            .AddValidator(new SignatureValidatorV2())
            .AddValidator(new DefaultParameterValidatorV2())
            .AddValidator(new ControlFlowValidatorV3())       // CFG-based validator
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
