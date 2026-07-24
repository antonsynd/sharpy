namespace Sharpy.Compiler.Semantic;

/// <summary>
/// How a <c>list[T]</c> value is backed in the emitted C#, which decides whether the #1052
/// non-negative index fast path (<see cref="IndexAccessLowering.NativeUnchecked"/>) may target it:
/// only a genuine <see cref="SharpyList"/> exposes the <c>GetItemUnchecked</c> accessor, so it is
/// the sole kind that enables the fast path. Every other kind (and the conservative
/// <see cref="Unknown"/> default) stays on the ordinary negative-index-normalizing indexer.
/// </summary>
internal enum ListBackingKind
{
    /// <summary>
    /// A concrete <c>Sharpy.List&lt;T&gt;</c> (has <c>GetItemUnchecked</c>): list literals and
    /// comprehensions, non-variadic <c>list[T]</c> parameters, explicitly-annotated <c>list[T]</c>
    /// locals, and call results whose recorded return contract is a genuine <c>Sharpy.List&lt;T&gt;</c>
    /// (e.g. the #1085 static-extension <c>str.split</c> family). The only kind that enables the fast path.
    /// </summary>
    SharpyList,

    /// <summary>
    /// A CLR array surfaced as <c>list[T]</c> (no <c>GetItemUnchecked</c>): a <c>*args</c> variadic
    /// parameter emits as <c>T[]</c>. Tagging it <c>NativeUnchecked</c> would not compile.
    /// </summary>
    ClrArray,

    /// <summary>
    /// A <c>list[T]</c> value narrowed/rebound to the non-generic <c>Sharpy.IList</c> surface, which
    /// lacks <c>GetItemUnchecked</c>. Contract-only today (no narrowing site rebinds to <c>IList</c>);
    /// the default <see cref="Unknown"/> already covers such values conservatively.
    /// </summary>
    NarrowedIList,

    /// <summary>
    /// Backing not proven (interop returns, member/index accesses, unannotated inferred locals). The
    /// conservative default — the fast path never targets it, preserving #1052's "when in doubt,
    /// don't lower".
    /// </summary>
    Unknown
}
