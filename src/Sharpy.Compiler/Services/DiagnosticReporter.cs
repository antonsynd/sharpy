using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Services;

/// <summary>
/// Implementation of IDiagnosticReporter using DiagnosticBag.
/// Provides centralized, consistent error reporting.
/// </summary>
public class DiagnosticReporter : IDiagnosticReporter
{
    private readonly DiagnosticBag _diagnostics;
    private readonly ICompilerLogger _logger;

    public DiagnosticReporter(ICompilerLogger? logger = null)
    {
        _diagnostics = new DiagnosticBag();
        _logger = logger ?? NullLogger.Instance;
    }

    public DiagnosticReporter(DiagnosticBag diagnostics, ICompilerLogger? logger = null)
    {
        _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        _logger = logger ?? NullLogger.Instance;
    }

    public void ReportError(string message, int? line = null, int? column = null)
    {
        _diagnostics.AddError(message, line, column, CurrentFilePath);
        _logger.LogError(message, line ?? 0, column ?? 0);
    }

    public void ReportError(string message, Node node)
    {
        ReportError(message, node.LineStart, node.ColumnStart);
    }

    public void ReportWarning(string message, int? line = null, int? column = null)
    {
        _diagnostics.AddWarning(message, line, column, CurrentFilePath);
        _logger.LogWarning(message, line ?? 0, column ?? 0);
    }

    public void ReportWarning(string message, Node node)
    {
        ReportWarning(message, node.LineStart, node.ColumnStart);
    }

    public DiagnosticBag Diagnostics => _diagnostics;

    public bool HasErrors => _diagnostics.HasErrors;

    public string? CurrentFilePath { get; set; }
}
