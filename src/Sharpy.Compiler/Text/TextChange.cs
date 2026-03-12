namespace Sharpy.Compiler.Text;

/// <summary>
/// Represents a text change to apply to a SourceText.
/// </summary>
public sealed record TextChange(TextSpan Span, string NewText);
