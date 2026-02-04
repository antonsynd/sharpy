using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Orchestrates semantic validation by running validators in order.
///
/// Design notes for future features:
/// - LSP: Pipeline can skip unchanged validators based on change tracking
/// - Parallel: Validators at same order level could potentially run in parallel
/// - Extensibility: New validators can be registered at runtime
/// </summary>
internal class ValidationPipeline
{
    private readonly List<ISemanticValidator> _validators = new();
    private readonly ICompilerLogger _logger;

    public ValidationPipeline(ICompilerLogger? logger = null)
    {
        _logger = logger ?? NullLogger.Instance;
    }

    /// <summary>
    /// Add a validator to the pipeline.
    /// Validators are automatically sorted by their Order property.
    /// </summary>
    public ValidationPipeline AddValidator(ISemanticValidator validator)
    {
        _validators.Add(validator);
        _validators.Sort((a, b) => a.Order.CompareTo(b.Order));
        return this;
    }

    /// <summary>
    /// Add multiple validators to the pipeline.
    /// </summary>
    public ValidationPipeline AddValidators(params ISemanticValidator[] validators)
    {
        foreach (var validator in validators)
        {
            AddValidator(validator);
        }
        return this;
    }

    /// <summary>
    /// Remove a validator by type.
    /// </summary>
    public ValidationPipeline RemoveValidator<T>() where T : ISemanticValidator
    {
        _validators.RemoveAll(v => v is T);
        return this;
    }

    /// <summary>
    /// Get all registered validators (for testing/debugging).
    /// </summary>
    public IReadOnlyList<ISemanticValidator> Validators => _validators.AsReadOnly();

    /// <summary>
    /// Run all validators on the module.
    /// </summary>
    /// <param name="module">The AST module to validate</param>
    /// <param name="context">The semantic context</param>
    /// <param name="cancellationToken">Optional cancellation token for LSP/IDE scenarios</param>
    /// <returns>The diagnostics collected during validation</returns>
    public DiagnosticBag Validate(Module module, SemanticContext context, CancellationToken cancellationToken = default)
    {
        _logger.LogInfo($"Starting validation pipeline with {_validators.Count} validators");

        foreach (var validator in _validators)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!context.ShouldContinue())
            {
                _logger.LogInfo($"Stopping validation pipeline (error limit reached or errors found)");
                break;
            }

            _logger.LogDebug($"Running validator: {validator.Name} (order: {validator.Order})");

            var errorsBefore = context.Diagnostics.ErrorCount;
            try
            {
                validator.Validate(module, context);
            }
            catch (OperationCanceledException)
            {
                throw; // Propagate cancellation
            }
            catch (Exception ex)
            {
                // Log full exception including stack trace for debugging
                _logger.LogError($"Validator {validator.Name} threw {ex.GetType().Name}: {ex}", 0, 0);

                // Include exception type in diagnostic for identification
                context.Diagnostics.AddError(
                    $"Internal compiler error ({ex.GetType().Name}) in {validator.Name}: {ex.Message}",
                    code: DiagnosticCodes.Infrastructure.CompilationFailed,
                    phase: CompilerPhase.Validation);
            }
            var errorsAfter = context.Diagnostics.ErrorCount;

            if (errorsAfter > errorsBefore)
            {
                _logger.LogDebug($"Validator {validator.Name} reported {errorsAfter - errorsBefore} error(s)");
            }
        }

        _logger.LogInfo($"Validation pipeline completed. Total errors: {context.Diagnostics.ErrorCount}");
        return context.Diagnostics;
    }

}
