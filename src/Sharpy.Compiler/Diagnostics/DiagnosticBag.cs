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
    CompilerPhase Phase = CompilerPhase.Unknown
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

    public void AddError(string message, int? line = null, int? column = null, string? filePath = null,
        string? code = null, CompilerPhase phase = CompilerPhase.Unknown)
    {
        Add(new CompilerDiagnostic(message, CompilerDiagnosticSeverity.Error, line, column, filePath, code, phase));
    }

    public void AddWarning(string message, int? line = null, int? column = null, string? filePath = null,
        string? code = null, CompilerPhase phase = CompilerPhase.Unknown)
    {
        Add(new CompilerDiagnostic(message, CompilerDiagnosticSeverity.Warning, line, column, filePath, code, phase));
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
    /// Add a diagnostic from a LexerError exception.
    /// </summary>
#pragma warning disable CS0618 // LexerError is obsolete (retained as internal safety net)
    public void AddLexerError(Lexer.LexerError error, string? filePath = null)
    {
        // Extract the raw message from the formatted "Lexer error at line X, column Y: message"
        var rawMessage = ExtractRawMessage(error.Message, "Lexer error");
        Add(new CompilerDiagnostic(rawMessage, CompilerDiagnosticSeverity.Error,
            error.Line, error.Column, filePath, Code: error.Code, Phase: CompilerPhase.Lexer));
    }
#pragma warning restore CS0618

    /// <summary>
    /// Add a diagnostic from a ParserError exception.
    /// </summary>
#pragma warning disable CS0618 // ParserError is obsolete (retained as internal safety net)
    public void AddParserError(Parser.ParserError error, string? filePath = null)
    {
        // Extract the raw message from the formatted "Parser error at line X, column Y: message"
        var rawMessage = ExtractRawMessage(error.Message, "Parser error");
        Add(new CompilerDiagnostic(rawMessage, CompilerDiagnosticSeverity.Error,
            error.Line, error.Column, filePath, Code: error.Code, Phase: CompilerPhase.Parser));
    }
#pragma warning restore CS0618

    /// <summary>
    /// Extract the raw message from a formatted error message.
    /// E.g., "Lexer error at line 1, column 2: unexpected token" -> "unexpected token"
    /// </summary>
    private static string ExtractRawMessage(string formattedMessage, string prefix)
    {
        // Pattern: "Prefix error at line X, column Y: message" or "Prefix error at line X: message" or "Prefix error: message"
        var atIndex = formattedMessage.IndexOf($"{prefix} at line ", StringComparison.Ordinal);
        if (atIndex >= 0)
        {
            var colonIndex = formattedMessage.IndexOf(": ", atIndex + prefix.Length);
            if (colonIndex >= 0)
                return formattedMessage[(colonIndex + 2)..];
        }

        // Try "Prefix error: message"
        var prefixWithColon = $"{prefix}: ";
        if (formattedMessage.StartsWith(prefixWithColon, StringComparison.Ordinal))
            return formattedMessage[prefixWithColon.Length..];

        // Return as-is if no pattern matched
        return formattedMessage;
    }
}
