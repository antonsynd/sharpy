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
}
