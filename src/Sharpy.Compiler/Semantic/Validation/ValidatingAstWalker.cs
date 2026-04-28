using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Text;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Base class for validators that need to walk the AST using the visitor pattern.
/// Inherits from <see cref="AstVisitor"/> for typed dispatch and implements
/// <see cref="ISemanticValidator"/> for pipeline integration.
/// Subclasses override <c>VisitXxx()</c> methods for nodes they care about;
/// unoverridden methods automatically traverse into child nodes.
/// </summary>
internal abstract class ValidatingAstWalker : AstVisitor, ISemanticValidator
{
    public abstract string Name { get; }
    public abstract int Order { get; }

    protected SemanticContext Context { get; private set; } = null!;

    public virtual void Validate(Module module, SemanticContext context)
    {
        Context = context;
        Visit(module);
    }

    protected void AddError(string message, int? line = null, int? column = null,
        string? code = null, TextSpan? span = null)
    {
        Context.Diagnostics.AddError(message, span, line, column, Context.CurrentFilePath, code: code, phase: CompilerPhase.Validation);
    }

    protected void AddWarning(string message, int? line = null, int? column = null,
        string? code = null, TextSpan? span = null, IReadOnlyDictionary<string, string>? data = null)
    {
        Context.Diagnostics.AddWarning(message, span, line, column, Context.CurrentFilePath, code: code, phase: CompilerPhase.Validation, data: data);
    }

    /// <summary>
    /// Convenience method to emit a hint-severity diagnostic about a behavioral
    /// difference from Python/C#. Hints share suppression with warnings but are
    /// not promoted to errors under WarningsAsErrors.
    /// </summary>
    protected void AddHint(string message, int? line = null, int? column = null,
        string? code = null, TextSpan? span = null, IReadOnlyDictionary<string, string>? data = null)
    {
        Context.Diagnostics.AddHint(message, span, line, column, Context.CurrentFilePath, code: code, phase: CompilerPhase.Validation, data: data);
    }
}
