using Sharpy.Compiler.Logging;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Factory for creating pre-configured validation pipelines.
/// </summary>
internal static class ValidationPipelineFactory
{
    /// <summary>
    /// Create the default pipeline with all standard validators.
    /// Uses CFG-based control flow analysis which handles unreachable code
    /// detection, missing return paths, and break/continue validation via graph analysis.
    /// </summary>
    public static ValidationPipeline CreateDefault(ICompilerLogger? logger = null)
    {
        return new ValidationPipeline(logger)
            // Order values determine execution sequence
            .AddValidator(new ModuleLevelValidator())       // Order: 50 (earliest, validates module structure)
            .AddValidator(new NamingConventionValidator())  // Order: 55 (naming convention warnings)
            .AddValidator(new DecoratorValidator())         // Order: 60 (validates decorator usage)
            .AddValidator(new SignatureValidator())         // Order: 150 (early, validates dunder signatures)
            .AddValidator(new GeneratorValidator())         // Order: 155 (generator guard rails)
            .AddValidator(new EqualityContractValidator())  // Order: 160 (warns on __eq__ without object overload)
            .AddValidator(new InterfaceConflictValidator()) // Order: 170 (detects conflicting synthesized interfaces)
            .AddValidator(new DefaultParameterValidator())  // Order: 250
            .AddValidator(new ControlFlowValidator())       // Order: 400 (CFG-based, handles unreachable code)
            .AddValidator(new PropertyValidator())           // Order: 410 (property declaration rules)
            .AddValidator(new UnusedVariableValidator())      // Order: 420 (unused variable warnings)
            .AddValidator(new UnusedImportValidator())       // Order: 430 (unused import warnings)
            .AddValidator(new AccessValidator())            // Order: 450
            .AddValidator(new DunderInvocationValidator())  // Order: 460 (dunder call rules)
            .AddValidator(new ProtocolValidator())          // Order: 500
            .AddValidator(new OperatorValidator())          // Order: 500
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
            .AddValidator(new ControlFlowValidator());  // CFG-based for quick checks
        // Skip signature validators, protocol validators, etc.
    }
}
