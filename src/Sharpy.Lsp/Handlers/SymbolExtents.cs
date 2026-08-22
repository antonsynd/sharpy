using Sharpy.Compiler.Semantic;

namespace Sharpy.Lsp.Handlers;

/// <summary>
/// How many source characters a symbol's name or a reference's identifier occupies. These
/// helpers RECOGNISE recorded extents — they never reconstruct one from <c>Name.Length</c>
/// (#1454, plan-80eee2 Design Decision 7). Lifted from <c>RenameHandler</c> so that every
/// consumer in the LSP layer reads the same measured length.
/// </summary>
internal static class SymbolExtents
{
    /// <summary>
    /// The two backticks an escaped spelling adds to the name's source extent.
    /// </summary>
    /// <remarks>
    /// This constant no longer CONSTRUCTS any extent — since #1454 every extent this class uses is
    /// one the parser recorded from the name token. Its single remaining use is
    /// <see cref="ReferenceExtentLength"/>, which RECOGNIZES an already-recorded span as the escaped
    /// spelling (plan-80eee2 Design Decision 7). Recognition and reconstruction are different jobs:
    /// the first asks what a measured number means, the second invents one.
    /// </remarks>
    internal const int BacktickPairLength = 2;

    /// <summary>
    /// How many characters this symbol's name occupies at its declaration, from the extent the
    /// parser recorded (#1454). Symbols with no parsed node — CLR imports — answer through
    /// <see cref="Symbol.EffectiveNameColumnEnd"/>'s fallback, which is where the old
    /// <c>Name.Length</c> + backtick-pair derivation now lives, once, on the symbol itself.
    /// </summary>
    /// <remarks>
    /// An edit sized to <c>Name.Length</c> against an escaped declaration replaces all but the last
    /// two characters and leaves backtick debris in the renamed source (#1281) — that is the defect
    /// the recorded extent removes the possibility of, rather than compensating for.
    /// </remarks>
    internal static int NameExtentLength(Symbol symbol) =>
        (symbol.EffectiveNameColumnEnd - symbol.EffectiveNameColumn) ?? symbol.Name.Length;

    /// <summary>
    /// How many characters one reference occupies. The recorded span is the identifier token's,
    /// which since #1281 covers both backticks of an escaped spelling — so each occurrence is
    /// replaced as it is written, whether or not the declaration was escaped.
    /// </summary>
    /// <remarks>
    /// A span matching neither spelling of the name is not this reference's extent: the root of a
    /// dotted escape (<c>`System.IO.Path`</c>, #713) carries the whole token's span on a segment
    /// symbol. Editing to the bare name there is the conservative choice — it under-reaches
    /// rather than eating the following segments.
    /// </remarks>
    /// <summary>
    /// Source-visible length of a name token that carries no recorded end column (Identifier
    /// reference, TypeAnnotation). The backtick flag bridges the gap: an escaped spelling's
    /// source length is the logical name length plus the backtick pair.
    /// </summary>
    internal static int SourceNameLength(string name, bool isBacktickEscaped) =>
        name.Length + (isBacktickEscaped ? BacktickPairLength : 0);

    internal static int ReferenceExtentLength(SymbolReference reference, string symbolName)
    {
        var spanLength = reference.Span.Length;

        return spanLength == symbolName.Length || spanLength == symbolName.Length + BacktickPairLength
            ? spanLength
            : symbolName.Length;
    }
}
