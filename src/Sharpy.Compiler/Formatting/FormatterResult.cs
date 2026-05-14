using Sharpy.Compiler.Diagnostics;

namespace Sharpy.Compiler.Formatting;

public record FormatterResult
{
    public string FormattedText { get; init; } = "";
    public bool HasChanges { get; init; }
    public IReadOnlyList<CompilerDiagnostic> Diagnostics { get; init; } = Array.Empty<CompilerDiagnostic>();
}
