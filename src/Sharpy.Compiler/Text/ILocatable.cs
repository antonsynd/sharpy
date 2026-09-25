namespace Sharpy.Compiler.Text;

/// <summary>
/// Interface for elements that have a source location.
/// Implemented by AST nodes, tokens, and symbols.
/// </summary>
public interface ILocatable
{
    /// <summary>
    /// The span of this element in the source text.
    /// May be null if location is not tracked.
    /// </summary>
    TextSpan? Span { get; }

    /// <summary>
    /// The 1-based line of the element's first character, or null when not tracked. A diagnostic
    /// anchored to the element carries it beside the span, so no renderer is left with a span but
    /// no position (#2070).
    /// </summary>
    int? StartLine => null;

    /// <summary>The 1-based column of the element's first character, or null when not tracked.</summary>
    int? StartColumn => null;
}
