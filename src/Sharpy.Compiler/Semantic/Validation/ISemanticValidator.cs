using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Text;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Interface for all semantic validation passes.
/// Each validator performs a specific aspect of semantic analysis.
///
/// Design notes for future features:
/// - LSP: Validators can be re-run incrementally on changed nodes
/// - Parallel: Validators should not hold state between calls
/// - ADTs: New validators (e.g., ExhaustivenessValidator) can be added
/// </summary>
internal interface ISemanticValidator
{
    /// <summary>
    /// Unique identifier for this validator (for logging/debugging).
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Order hint for pipeline execution (lower = earlier).
    /// NameResolution: 100, TypeResolution: 200, TypeChecking: 300, etc.
    /// </summary>
    int Order { get; }

    /// <summary>
    /// Validates the AST and reports diagnostics.
    /// </summary>
    /// <param name="module">The AST module to validate</param>
    /// <param name="context">Shared context with symbols, types, and caches</param>
    void Validate(Module module, SemanticContext context);
}

/// <summary>
/// Base class providing common functionality for validators.
/// Validators can inherit from this or implement ISemanticValidator directly.
/// </summary>
internal abstract class SemanticValidatorBase : ISemanticValidator
{
    public abstract string Name { get; }
    public abstract int Order { get; }

    public abstract void Validate(Module module, SemanticContext context);

    /// <summary>
    /// Convenience method to add an error to the context's diagnostics.
    /// </summary>
    protected void AddError(SemanticContext context, string message, int? line = null, int? column = null,
        string? code = null, TextSpan? span = null)
    {
        context.Diagnostics.AddError(message, span, line, column, context.CurrentFilePath, code: code, phase: CompilerPhase.Validation);
    }

    /// <summary>
    /// Convenience method to add a warning to the context's diagnostics.
    /// </summary>
    protected void AddWarning(SemanticContext context, string message, int? line = null, int? column = null,
        string? code = null, TextSpan? span = null)
    {
        context.Diagnostics.AddWarning(message, span, line, column, context.CurrentFilePath, code: code, phase: CompilerPhase.Validation);
    }
}
