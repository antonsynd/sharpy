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
    TextSpan? Span = null,
    IReadOnlyDictionary<string, string>? Data = null
)
{
    public bool IsError => Severity == CompilerDiagnosticSeverity.Error;
    public bool IsWarning => Severity == CompilerDiagnosticSeverity.Warning;
    public bool IsHint => Severity == CompilerDiagnosticSeverity.Hint;

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
    private int _errorCount;
    private int _warningCount;
    private int _hintCount;

    /// <summary>
    /// Tracks diagnostics that have already been added, using (Code, Line, Column, Message?, SpanStart, SpanLength) as the key.
    /// This prevents duplicate diagnostics from being shown to the user when multiple validators
    /// catch the same issue, while still allowing distinct diagnostics that share code+line+column
    /// but differ in TextSpan (i.e., different AST nodes on the same line).
    /// </summary>
    private readonly HashSet<(string?, int?, int?, string?, int?, int?)> _seenDiagnostics = new();

    /// <summary>
    /// Tracks identifiers that are root causes of errors.
    /// When an identifier is marked as a root cause (e.g., from a failed import),
    /// subsequent errors about that identifier can be suppressed to avoid cascading noise.
    /// For example, if "from nonexistent import foo" fails, we mark "foo" as a root cause
    /// so that "undefined identifier 'foo'" errors are suppressed.
    /// </summary>
    private readonly HashSet<string> _rootCauseIdentifiers = new(StringComparer.OrdinalIgnoreCase);

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
        // Apply suppression: skip warnings/hints whose code is in the suppressed set.
        // Hints share the same suppression mechanism as warnings.
        if ((diagnostic.IsWarning || diagnostic.IsHint)
            && !string.IsNullOrEmpty(diagnostic.Code)
            && _suppressedWarnings.Contains(diagnostic.Code))
            return;

        // Apply promotion: warnings become errors when WarningsAsErrors is enabled.
        // Hints are NOT promoted — they are advisory diagnostics about behavioral
        // differences from Python/C# and remain hint-severity even under -Werror.
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

            if (diagnostic.IsError)
                _errorCount++;
            else if (diagnostic.IsWarning)
                _warningCount++;
            else if (diagnostic.IsHint)
                _hintCount++;
        }
    }

    /// <summary>
    /// Gets a unique key for deduplication purposes.
    /// Diagnostics with codes are deduplicated by (Code, Line, Column, SpanStart, SpanLength).
    /// Diagnostics without codes use the message as part of the key.
    /// Including span information prevents false deduplication when two distinct AST nodes
    /// on the same line produce the same diagnostic code.
    /// </summary>
    private static (string?, int?, int?, string?, int?, int?) GetDeduplicationKey(CompilerDiagnostic diagnostic)
    {
        if (string.IsNullOrEmpty(diagnostic.Code))
        {
            // No code - use message as fallback for uniqueness
            return (null, diagnostic.Line, diagnostic.Column, diagnostic.Message,
                diagnostic.Span?.Start, diagnostic.Span?.Length);
        }
        return (diagnostic.Code, diagnostic.Line, diagnostic.Column, null,
            diagnostic.Span?.Start, diagnostic.Span?.Length);
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
        string? filePath = null, string? code = null, CompilerPhase phase = CompilerPhase.Unknown,
        IReadOnlyDictionary<string, string>? data = null)
    {
        if (!string.IsNullOrEmpty(code) && _suppressedWarnings.Contains(code))
            return;
        var severity = _warningsAsErrors ? CompilerDiagnosticSeverity.Error : CompilerDiagnosticSeverity.Warning;
        Add(new CompilerDiagnostic(message, severity, line, column, filePath, code, phase, span, data));
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

    public void AddInfo(string message, int? line = null, int? column = null, string? filePath = null,
        string? code = null, CompilerPhase phase = CompilerPhase.Unknown)
    {
        Add(new CompilerDiagnostic(message, CompilerDiagnosticSeverity.Info, line, column, filePath, code, phase));
    }

    /// <summary>
    /// Adds a hint-severity diagnostic. Hints are advisory notes about behavioral
    /// differences from Python/C# (e.g., string indexing, struct value semantics).
    /// Hints share the same suppression mechanism as warnings (via the suppressed-warning
    /// set) but are NOT promoted to errors when WarningsAsErrors is enabled.
    /// </summary>
    public void AddHint(string message, int? line = null, int? column = null, string? filePath = null,
        string? code = null, CompilerPhase phase = CompilerPhase.Unknown)
    {
        if (!string.IsNullOrEmpty(code) && _suppressedWarnings.Contains(code))
            return;
        Add(new CompilerDiagnostic(message, CompilerDiagnosticSeverity.Hint, line, column, filePath, code, phase));
    }

    /// <summary>
    /// Adds a hint-severity diagnostic with an associated text span and optional data dictionary.
    /// </summary>
    public void AddHint(string message, TextSpan? span, int? line = null, int? column = null,
        string? filePath = null, string? code = null, CompilerPhase phase = CompilerPhase.Unknown,
        IReadOnlyDictionary<string, string>? data = null)
    {
        if (!string.IsNullOrEmpty(code) && _suppressedWarnings.Contains(code))
            return;
        Add(new CompilerDiagnostic(message, CompilerDiagnosticSeverity.Hint, line, column, filePath, code, phase, span, data));
    }

    /// <summary>
    /// Adds a hint-severity diagnostic anchored to an <see cref="ILocatable"/> AST node.
    /// </summary>
    public void AddHint(string message, ILocatable locatable, string? filePath = null,
        string? code = null, CompilerPhase phase = CompilerPhase.Unknown)
    {
        if (!string.IsNullOrEmpty(code) && _suppressedWarnings.Contains(code))
            return;
        Add(new CompilerDiagnostic(message, CompilerDiagnosticSeverity.Hint, Span: locatable.Span,
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
    /// Also transfers root cause identifiers from the other bag.
    /// </summary>
    public void Merge(DiagnosticBag other)
    {
        AddRange(other.GetAll());
        // Transfer root cause identifiers
        foreach (var identifier in other.GetRootCauses())
        {
            MarkAsRootCause(identifier);
        }
    }

    public bool HasErrors
    {
        get
        {
            lock (_lock)
            {
                return _errorCount > 0;
            }
        }
    }

    public int ErrorCount
    {
        get
        {
            lock (_lock)
            {
                return _errorCount;
            }
        }
    }

    public int WarningCount
    {
        get
        {
            lock (_lock)
            {
                return _warningCount;
            }
        }
    }

    public int HintCount
    {
        get
        {
            lock (_lock)
            {
                return _hintCount;
            }
        }
    }

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

    public IReadOnlyList<CompilerDiagnostic> GetHints()
    {
        lock (_lock)
        {
            return _diagnostics.Where(d => d.Severity == CompilerDiagnosticSeverity.Hint).ToList();
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _diagnostics.Clear();
            _seenDiagnostics.Clear();
            _rootCauseIdentifiers.Clear();
            _errorCount = 0;
            _warningCount = 0;
            _hintCount = 0;
        }
    }

    /// <summary>
    /// Adds an error and marks the given identifier as a root cause.
    /// Subsequent errors related to this identifier can be suppressed using <see cref="IsRootCause"/>.
    /// Use this for errors like "module not found" where downstream "undefined identifier" errors
    /// are just noise caused by the original error.
    /// </summary>
    /// <param name="identifier">The identifier that failed to resolve (e.g., module name, imported symbol name)</param>
    /// <param name="message">The error message</param>
    /// <param name="line">Line number of the error</param>
    /// <param name="column">Column number of the error</param>
    /// <param name="filePath">File path where error occurred</param>
    /// <param name="code">Diagnostic code</param>
    /// <param name="phase">Compiler phase where error occurred</param>
    public void AddRootCauseError(string identifier, string message, int? line = null, int? column = null,
        string? filePath = null, string? code = null, CompilerPhase phase = CompilerPhase.Unknown)
    {
        lock (_lock)
        {
            _rootCauseIdentifiers.Add(identifier);
        }
        AddError(message, line, column, filePath, code, phase);
    }

    /// <summary>
    /// Adds an error with text span and marks the given identifier as a root cause.
    /// </summary>
    public void AddRootCauseError(string identifier, string message, TextSpan? span, int? line = null,
        int? column = null, string? filePath = null, string? code = null, CompilerPhase phase = CompilerPhase.Unknown)
    {
        lock (_lock)
        {
            _rootCauseIdentifiers.Add(identifier);
        }
        AddError(message, span, line, column, filePath, code, phase);
    }

    /// <summary>
    /// Checks whether the given identifier is a known root cause of errors.
    /// When true, callers may choose to suppress downstream errors about this identifier
    /// since the user has already been informed of the root cause.
    /// </summary>
    /// <param name="identifier">The identifier to check</param>
    /// <returns>True if this identifier was marked as a root cause via <see cref="AddRootCauseError"/></returns>
    public bool IsRootCause(string identifier)
    {
        lock (_lock)
        {
            return _rootCauseIdentifiers.Contains(identifier);
        }
    }

    /// <summary>
    /// Marks an identifier as a root cause without adding an error.
    /// Use this when the error has already been reported but you want to suppress cascading errors.
    /// </summary>
    /// <param name="identifier">The identifier to mark as a root cause</param>
    public void MarkAsRootCause(string identifier)
    {
        lock (_lock)
        {
            _rootCauseIdentifiers.Add(identifier);
        }
    }

    /// <summary>
    /// Marks multiple identifiers as root causes.
    /// </summary>
    /// <param name="identifiers">The identifiers to mark as root causes</param>
    public void MarkAsRootCauses(IEnumerable<string> identifiers)
    {
        lock (_lock)
        {
            foreach (var id in identifiers)
            {
                _rootCauseIdentifiers.Add(id);
            }
        }
    }

    /// <summary>
    /// Gets all root cause identifiers. Used when merging diagnostic bags.
    /// </summary>
    internal IReadOnlyCollection<string> GetRootCauses()
    {
        lock (_lock)
        {
            return _rootCauseIdentifiers.ToList();
        }
    }

}
