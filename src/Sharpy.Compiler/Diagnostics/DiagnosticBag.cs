using Sharpy.Compiler.Text;

namespace Sharpy.Compiler.Diagnostics;

/// <summary>
/// Severity level for compiler diagnostics.
/// Named to avoid conflict with Microsoft.CodeAnalysis.DiagnosticSeverity.
/// </summary>
public enum CompilerDiagnosticSeverity
{
    Error,
    Warning,
    Info,
    Hint
}

/// <summary>
/// Phase of compilation where the diagnostic originated.
/// </summary>
public enum CompilerPhase
{
    Lexer,
    Parser,
    NameResolution,
    ImportResolution,
    TypeChecking,
    Validation,
    CodeGeneration,
    Assembly,
    Unknown
}

/// <summary>
/// A single diagnostic message with location and severity.
/// Named CompilerDiagnostic to avoid conflict with Microsoft.CodeAnalysis.Diagnostic.
/// </summary>
public record CompilerDiagnostic(
    string Message,
    CompilerDiagnosticSeverity Severity,
    int? Line = null,
    int? Column = null,
    string? FilePath = null,
    string? Code = null,
    CompilerPhase Phase = CompilerPhase.Unknown,
    TextSpan? Span = null
)
{
    public bool IsError => Severity == CompilerDiagnosticSeverity.Error;
    public bool IsWarning => Severity == CompilerDiagnosticSeverity.Warning;

    public override string ToString()
    {
        var prefix = Severity switch
        {
            CompilerDiagnosticSeverity.Error => "error",
            CompilerDiagnosticSeverity.Warning => "warning",
            CompilerDiagnosticSeverity.Info => "info",
            CompilerDiagnosticSeverity.Hint => "hint",
            _ => "diagnostic"
        };

        var location = Line.HasValue && Column.HasValue
            ? $"({Line},{Column})"
            : Line.HasValue
                ? $"({Line})"
                : "";

        var file = !string.IsNullOrEmpty(FilePath) ? $"{FilePath}" : "";
        var code = !string.IsNullOrEmpty(Code) ? $" {Code}:" : ":";
        var span = Span.HasValue ? $" {Span.Value}" : "";

        return $"{file}{location}: {prefix}{code} {Message}{span}";
    }
}

/// <summary>
/// Thread-safe collection of diagnostics.
/// Supports future parallel compilation scenarios.
/// </summary>
public class DiagnosticBag
{
    private readonly List<CompilerDiagnostic> _diagnostics = new();
    private readonly object _lock = new();
    private readonly HashSet<string> _suppressedWarnings;
    private readonly bool _warningsAsErrors;

    /// <summary>
    /// Tracks diagnostics that have already been added, using (Code, Line, Column, Message?) as the key.
    /// This prevents duplicate diagnostics from being shown to the user when multiple validators
    /// catch the same issue.
    /// </summary>
    private readonly HashSet<(string?, int?, int?, string?)> _seenDiagnostics = new();

    public DiagnosticBag() : this(warningsAsErrors: false, suppressedWarnings: null) { }

    public DiagnosticBag(bool warningsAsErrors = false, HashSet<string>? suppressedWarnings = null)
    {
        _warningsAsErrors = warningsAsErrors;
        // Defensive copy: DiagnosticBag claims thread-safety via lock(_lock),
        // so the suppressed set must not be shared with callers.
        _suppressedWarnings = suppressedWarnings != null
            ? new HashSet<string>(suppressedWarnings, StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    public void Add(CompilerDiagnostic diagnostic)
    {
        // Apply suppression: skip warnings whose code is in the suppressed set
        if (diagnostic.IsWarning && !string.IsNullOrEmpty(diagnostic.Code) && _suppressedWarnings.Contains(diagnostic.Code))
            return;

        // Apply promotion: warnings become errors when WarningsAsErrors is enabled
        if (diagnostic.IsWarning && _warningsAsErrors)
        {
            diagnostic = diagnostic with { Severity = CompilerDiagnosticSeverity.Error };
        }

        // Deduplicate by code and location.
        // For diagnostics with codes, we use (Code, Line, Column) as the key.
        // For diagnostics without codes, we include the message to distinguish them.
        var key = GetDeduplicationKey(diagnostic);

        lock (_lock)
        {
            if (!_seenDiagnostics.Add(key))
                return; // Skip duplicate

            _diagnostics.Add(diagnostic);
        }
    }

    /// <summary>
    /// Gets a unique key for deduplication purposes.
    /// Diagnostics with codes are deduplicated by (Code, Line, Column).
    /// Diagnostics without codes use the message as part of the key.
    /// </summary>
    private static (string?, int?, int?, string?) GetDeduplicationKey(CompilerDiagnostic diagnostic)
    {
        if (string.IsNullOrEmpty(diagnostic.Code))
        {
            // No code - use message as fallback for uniqueness
            return (null, diagnostic.Line, diagnostic.Column, diagnostic.Message);
        }
        return (diagnostic.Code, diagnostic.Line, diagnostic.Column, null);
    }

    public void AddError(string message, int? line = null, int? column = null, string? filePath = null,
        string? code = null, CompilerPhase phase = CompilerPhase.Unknown)
    {
        Add(new CompilerDiagnostic(message, CompilerDiagnosticSeverity.Error, line, column, filePath, code, phase));
    }

    public void AddError(string message, TextSpan? span, int? line = null, int? column = null,
        string? filePath = null, string? code = null, CompilerPhase phase = CompilerPhase.Unknown)
    {
        Add(new CompilerDiagnostic(message, CompilerDiagnosticSeverity.Error, line, column, filePath, code, phase, span));
    }

    public void AddError(string message, ILocatable locatable, string? filePath = null,
        string? code = null, CompilerPhase phase = CompilerPhase.Unknown)
    {
        Add(new CompilerDiagnostic(message, CompilerDiagnosticSeverity.Error, Span: locatable.Span,
            FilePath: filePath, Code: code, Phase: phase));
    }

    public void AddWarning(string message, int? line = null, int? column = null, string? filePath = null,
        string? code = null, CompilerPhase phase = CompilerPhase.Unknown)
    {
        if (!string.IsNullOrEmpty(code) && _suppressedWarnings.Contains(code))
            return;
        var severity = _warningsAsErrors ? CompilerDiagnosticSeverity.Error : CompilerDiagnosticSeverity.Warning;
        Add(new CompilerDiagnostic(message, severity, line, column, filePath, code, phase));
    }

    public void AddWarning(string message, TextSpan? span, int? line = null, int? column = null,
        string? filePath = null, string? code = null, CompilerPhase phase = CompilerPhase.Unknown)
    {
        if (!string.IsNullOrEmpty(code) && _suppressedWarnings.Contains(code))
            return;
        var severity = _warningsAsErrors ? CompilerDiagnosticSeverity.Error : CompilerDiagnosticSeverity.Warning;
        Add(new CompilerDiagnostic(message, severity, line, column, filePath, code, phase, span));
    }

    public void AddWarning(string message, ILocatable locatable, string? filePath = null,
        string? code = null, CompilerPhase phase = CompilerPhase.Unknown)
    {
        if (!string.IsNullOrEmpty(code) && _suppressedWarnings.Contains(code))
            return;
        var severity = _warningsAsErrors ? CompilerDiagnosticSeverity.Error : CompilerDiagnosticSeverity.Warning;
        Add(new CompilerDiagnostic(message, severity, Span: locatable.Span,
            FilePath: filePath, Code: code, Phase: phase));
    }

    public void AddRange(IEnumerable<CompilerDiagnostic> diagnostics)
    {
        foreach (var diagnostic in diagnostics)
        {
            Add(diagnostic);
        }
    }

    /// <summary>
    /// Merge diagnostics from another bag (useful for aggregating from sub-validators).
    /// </summary>
    public void Merge(DiagnosticBag other)
    {
        AddRange(other.GetAll());
    }

    public bool HasErrors => _diagnostics.Any(d => d.IsError);

    public int ErrorCount => _diagnostics.Count(d => d.IsError);

    public int WarningCount => _diagnostics.Count(d => d.Severity == CompilerDiagnosticSeverity.Warning);

    public IReadOnlyList<CompilerDiagnostic> GetAll()
    {
        lock (_lock)
        {
            return _diagnostics.ToList();
        }
    }

    public IReadOnlyList<CompilerDiagnostic> GetErrors()
    {
        lock (_lock)
        {
            return _diagnostics.Where(d => d.IsError).ToList();
        }
    }

    public IReadOnlyList<CompilerDiagnostic> GetWarnings()
    {
        lock (_lock)
        {
            return _diagnostics.Where(d => d.Severity == CompilerDiagnosticSeverity.Warning).ToList();
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _diagnostics.Clear();
            _seenDiagnostics.Clear();
        }
    }

}
