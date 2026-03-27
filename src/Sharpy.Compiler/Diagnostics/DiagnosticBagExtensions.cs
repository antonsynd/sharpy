using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Text;

namespace Sharpy.Compiler.Diagnostics;

/// <summary>
/// Extension methods that bundle DiagnosticBag + ICompilerLogger calls
/// into a single call site, eliminating the private AddError/AddWarning
/// helpers duplicated across NameResolver, TypeResolver, ImportResolver, etc.
/// </summary>
internal static class DiagnosticBagExtensions
{
    /// <summary>
    /// Adds an error diagnostic and optionally logs it.
    /// Replaces the private AddError pattern found in semantic analysis classes.
    /// </summary>
    internal static void AddPhaseError(
        this DiagnosticBag diagnostics,
        string message, CompilerPhase phase,
        TextSpan? span = null, int? line = null, int? column = null,
        string? filePath = null, string? code = null,
        ICompilerLogger? logger = null)
    {
        diagnostics.AddError(message, span, line, column, filePath, code: code, phase: phase);
        logger?.LogError(message, line ?? 0, column ?? 0);
    }

    /// <summary>
    /// Adds an error diagnostic from a locatable AST node and optionally logs it.
    /// </summary>
    internal static void AddPhaseError(
        this DiagnosticBag diagnostics,
        string message, CompilerPhase phase,
        ILocatable locatable,
        string? filePath = null, string? code = null,
        ICompilerLogger? logger = null)
    {
        diagnostics.AddError(message, locatable, filePath, code: code, phase: phase);
        logger?.LogError(message, 0, 0);
    }

    /// <summary>
    /// Adds a warning diagnostic and optionally logs it.
    /// </summary>
    internal static void AddPhaseWarning(
        this DiagnosticBag diagnostics,
        string message, CompilerPhase phase,
        TextSpan? span = null, int? line = null, int? column = null,
        string? filePath = null, string? code = null,
        ICompilerLogger? logger = null)
    {
        diagnostics.AddWarning(message, span, line, column, filePath, code: code, phase: phase);
        logger?.LogWarning(message, line ?? 0, column ?? 0);
    }
}
