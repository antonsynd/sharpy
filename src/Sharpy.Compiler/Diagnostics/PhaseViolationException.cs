namespace Sharpy.Compiler.Diagnostics;

/// <summary>
/// Exception thrown when a phase violation occurs during compilation.
/// </summary>
/// <remarks>
/// <para>
/// The compiler has explicit phase boundaries enforced by SemanticBinding.FreezeXxx() methods.
/// When code attempts to mutate data after its corresponding phase is frozen (e.g., setting
/// a symbol's type after type checking is complete), this exception is thrown.
/// </para>
/// <para>
/// This dedicated exception type (inheriting from InvalidOperationException) provides:
/// - Immediate identification of phase violations in stack traces
/// - Context about which operation was attempted
/// - The symbol name that triggered the violation (when available)
/// - The expected phase that was already frozen
/// </para>
/// <para>
/// This exception is always thrown (not DEBUG-only) because phase violations indicate
/// a compiler bug that should be caught in any build configuration.
/// </para>
/// </remarks>
public sealed class PhaseViolationException : InvalidOperationException
{
    /// <summary>
    /// The operation that was attempted after the phase was frozen
    /// (e.g., "set variable type", "set base type", "add interface").
    /// </summary>
    public string Operation { get; }

    /// <summary>
    /// The name of the symbol that triggered the violation, if available.
    /// </summary>
    public string? SymbolName { get; }

    /// <summary>
    /// The phase that was expected to still be active but was already frozen
    /// (e.g., "type checking", "inheritance resolution").
    /// </summary>
    public string ExpectedPhase { get; }

    /// <summary>
    /// Creates a new phase violation exception with a symbol name.
    /// </summary>
    /// <param name="operation">The operation that was attempted (e.g., "set variable type").</param>
    /// <param name="expectedPhase">The phase that should have been active (e.g., "type checking").</param>
    /// <param name="symbolName">The name of the symbol involved in the violation.</param>
    public PhaseViolationException(string operation, string expectedPhase, string? symbolName = null)
        : base(FormatMessage(operation, expectedPhase, symbolName))
    {
        Operation = operation;
        ExpectedPhase = expectedPhase;
        SymbolName = symbolName;
    }

    private static string FormatMessage(string operation, string expectedPhase, string? symbolName)
    {
        var target = symbolName != null ? $" for '{symbolName}'" : "";
        return $"Phase violation: Cannot {operation}{target} after {expectedPhase} phase is frozen. " +
               "This is a compiler bug - data was written after the phase boundary that froze this store. " +
               "Please report this issue.";
    }
}
