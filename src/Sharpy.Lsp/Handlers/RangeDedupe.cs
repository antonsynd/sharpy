using System.Collections.Generic;
using OmniSharp.Extensions.LanguageServer.Protocol;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Sharpy.Lsp.Handlers;

/// <summary>
/// Deduplicates "the declaration, then every reference" result sets by source range (#1263).
/// </summary>
/// <remarks>
/// <para>
/// Rename, references and document-highlight each build their answer the same way: emit the
/// declaration, then emit every recorded reference. When the declaration is ALSO recorded as a
/// reference — which it is whenever the declaring occurrence is indexed — the same range comes out
/// twice. For highlights and references that is cosmetic duplication; for rename it is two
/// overlapping edits at identical ranges, which an editor may apply twice and corrupt the file.
/// </para>
/// <para>
/// One helper for all three because it is one defect: fixing it in the handler that happens to have
/// the visible symptom leaves the other two producing the same wrong set (the parallel-site
/// discipline from #1145).
/// </para>
/// </remarks>
internal sealed class RangeDedupe
{
    private readonly HashSet<(string Uri, int Line, int Start, int End)> _seen = new();

    /// <summary>
    /// Registers a range and reports whether it is new. A repeat of an already-registered range
    /// returns false and must be dropped by the caller.
    /// </summary>
    internal bool IsFirst(DocumentUri? uri, LspRange range) =>
        _seen.Add((
            uri?.ToString() ?? string.Empty,
            range.Start.Line,
            range.Start.Character,
            range.End.Character));

    /// <summary>Single-document overload, for handlers whose results carry no URI.</summary>
    internal bool IsFirst(LspRange range) => IsFirst(null, range);
}
