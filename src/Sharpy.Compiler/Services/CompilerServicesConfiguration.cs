namespace Sharpy.Compiler.Services;

/// <summary>
/// Immutable configuration for CompilerServices.
/// Set once at construction time.
/// </summary>
public sealed class CompilerServicesConfiguration
{
    /// <summary>
    /// Maximum number of errors before stopping compilation.
    /// Default: 100
    /// </summary>
    public int MaxErrors { get; init; } = 100;

    /// <summary>
    /// Whether to continue compilation after encountering errors.
    /// Default: true (collect all errors)
    /// </summary>
    public bool ContinueAfterErrors { get; init; } = true;

    /// <summary>
    /// Enable verbose logging of service operations.
    /// Default: false
    /// </summary>
    public bool VerboseLogging { get; init; } = false;

    /// <summary>
    /// Current file path for error reporting.
    /// Can be updated as files are processed.
    /// </summary>
    public string? InitialFilePath { get; init; }

    /// <summary>
    /// Default configuration with sensible defaults.
    /// </summary>
    public static CompilerServicesConfiguration Default { get; } = new();
}
