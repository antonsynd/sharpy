using Sharpy.Compiler.Diagnostics;

namespace Sharpy.Compiler.Formatting;

/// <summary>
/// What <see cref="FormatterService.FormatRange"/> returns: the hunks of the checked whole-document
/// output that the selection touches, the text they produce, and the diagnostics.
/// </summary>
internal sealed record FormatRangeResult
{
    /// <summary>The selected hunks over the SOURCE's lines, sorted and non-adjacent; empty when nothing changes or the result was declined.</summary>
    public IReadOnlyList<LineHunk> Hunks { get; init; } = Array.Empty<LineHunk>();

    /// <summary><c>LineDiff.Apply(source, Hunks)</c> — the source itself when there are no hunks.</summary>
    public string AppliedText { get; init; } = "";

    /// <summary>
    /// The source's lex/parse errors (<see cref="SourceParses"/> is false — the caller takes its
    /// indent-only fallback), or the SPY0912 that declined the whole-document output or the applied text.
    /// </summary>
    public IReadOnlyList<CompilerDiagnostic> Diagnostics { get; init; } = Array.Empty<CompilerDiagnostic>();

    /// <summary>Whether the source lexed and parsed; when false, nothing was formatted.</summary>
    public bool SourceParses { get; init; }
}
