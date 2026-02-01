using Sharpy.Compiler.Logging;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Factory for creating pre-configured validation pipelines.
/// </summary>
public static class ValidationPipelineFactory
{
    /// <summary>
    /// Create the default pipeline with all standard validators.
    /// Uses CFG-based control flow analysis (V3) which handles unreachable code
    /// detection, missing return paths, and break/continue validation via graph analysis.
    /// </summary>
    public static ValidationPipeline CreateDefault(ICompilerLogger? logger = null)
    {
        return new ValidationPipeline(logger)
            // Order values determine execution sequence
            .AddValidator(new ModuleLevelValidatorV2())       // Order: 50 (earliest, validates module structure)
            .AddValidator(new DecoratorValidatorV2())         // Order: 60 (validates decorator usage)
            .AddValidator(new SignatureValidatorV2())         // Order: 150 (early, validates dunder signatures)
            .AddValidator(new DefaultParameterValidatorV2())  // Order: 250
            .AddValidator(new ControlFlowValidatorV3())       // Order: 400 (CFG-based, handles unreachable code)
            .AddValidator(new UnusedVariableValidator())      // Order: 420 (unused variable warnings)
            .AddValidator(new AccessValidatorV2())            // Order: 450
            .AddValidator(new ProtocolValidatorV2())          // Order: 500
            .AddValidator(new OperatorValidatorV2())          // Order: 500
            ;
    }

    /// <summary>
    /// Create a pipeline using AST-walking control flow validator (V2).
    /// V2 uses direct AST traversal instead of building a CFG.
    /// </summary>
    public static ValidationPipeline CreateWithAstControlFlow(ICompilerLogger? logger = null)
    {
        return new ValidationPipeline(logger)
            .AddValidator(new ModuleLevelValidatorV2())       // Order: 50 (earliest)
            .AddValidator(new DecoratorValidatorV2())         // Order: 60
            .AddValidator(new SignatureValidatorV2())
            .AddValidator(new DefaultParameterValidatorV2())
            .AddValidator(new ControlFlowValidatorV2())       // AST-walking validator
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
            .AddValidator(new ControlFlowValidatorV3());  // V3 CFG-based for quick checks
        // Skip signature validators, protocol validators, etc.
    }
}
