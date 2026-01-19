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
/// A single diagnostic message with location and severity.
/// Named CompilerDiagnostic to avoid conflict with Microsoft.CodeAnalysis.Diagnostic.
/// </summary>
public record CompilerDiagnostic(
    string Message,
    CompilerDiagnosticSeverity Severity,
    int? Line = null,
    int? Column = null,
    string? FilePath = null,
    string? Code = null  // e.g., "SHP001" for future error codes
)
{
    public bool IsError => Severity == CompilerDiagnosticSeverity.Error;

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

        return $"{file}{location}: {prefix}{code} {Message}";
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

    public void Add(CompilerDiagnostic diagnostic)
    {
        lock (_lock)
        {
            _diagnostics.Add(diagnostic);
        }
    }

    public void AddError(string message, int? line = null, int? column = null, string? filePath = null)
    {
        Add(new CompilerDiagnostic(message, CompilerDiagnosticSeverity.Error, line, column, filePath));
    }

    public void AddWarning(string message, int? line = null, int? column = null, string? filePath = null)
    {
        Add(new CompilerDiagnostic(message, CompilerDiagnosticSeverity.Warning, line, column, filePath));
    }

    public void AddRange(IEnumerable<CompilerDiagnostic> diagnostics)
    {
        lock (_lock)
        {
            _diagnostics.AddRange(diagnostics);
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
        }
    }

    /// <summary>
    /// Convert legacy SemanticErrors to Diagnostics.
    /// Use during migration period.
    /// </summary>
    public static DiagnosticBag FromSemanticErrors(IEnumerable<Semantic.SemanticError> errors)
    {
        var bag = new DiagnosticBag();
        foreach (var error in errors)
        {
            bag.AddError(error.Message.Replace("Semantic error: ", "")
                .Replace($"Semantic error at line {error.Line}: ", "")
                .Replace($"Semantic error at line {error.Line}, column {error.Column}: ", ""),
                error.Line, error.Column);
        }
        return bag;
    }

    /// <summary>
    /// Convert Diagnostics to legacy SemanticErrors.
    /// Use during migration period.
    /// </summary>
    public IReadOnlyList<Semantic.SemanticError> ToSemanticErrors()
    {
        return GetErrors()
            .Select(d => new Semantic.SemanticError(d.Message, d.Line, d.Column))
            .ToList();
    }
}
