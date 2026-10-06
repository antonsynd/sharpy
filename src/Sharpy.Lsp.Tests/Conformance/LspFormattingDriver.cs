using System.Text;
// The enclosing `Sharpy` namespace exposes Sharpy.List<T> (Core's Pythonic list), which shadows
// System's List<T> here; SCG-qualify the concrete lists this driver constructs.
using SCG = System.Collections.Generic;
using Microsoft.Extensions.Logging.Abstractions;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler;
using Sharpy.Lsp.Handlers;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Sharpy.Lsp.Tests.Conformance;

/// <summary>The three LSP formatting requests a client can send for a <c>.spy</c> document.</summary>
public enum LspFormattingRouteKind
{
    /// <summary><c>textDocument/formatting</c> (Format Document).</summary>
    Full,

    /// <summary><c>textDocument/rangeFormatting</c> (Format Selection).</summary>
    Range,

    /// <summary><c>textDocument/onTypeFormatting</c> (format on type).</summary>
    OnType,
}

/// <summary>One roster row: the request kind and the production handler type that serves it.</summary>
public sealed record LspFormattingRoute(LspFormattingRouteKind Kind, Type HandlerType);

/// <summary>
/// Thrown by <see cref="LspFormattingDriver.ApplyStrict"/> when a handler's edits are not a legal
/// LSP edit list for the document: a position outside the document, <c>start &gt; end</c>, or two
/// overlapping ranges. Carries the edits so a caller can report them.
/// </summary>
public sealed class StrictEditApplicationException : InvalidOperationException
{
    public StrictEditApplicationException(string message, IReadOnlyList<TextEdit> edits)
        : base(message)
    {
        Edits = edits;
    }

    public IReadOnlyList<TextEdit> Edits { get; }
}

/// <summary>
/// Drives the REAL LSP formatting handlers the way a client does, and applies their edits the way
/// a client must (#2168, plan P22e): one request is <c>OpenDocument</c> → <c>Handle</c> →
/// <c>CloseDocument</c>, and the returned edits are applied by <see cref="ApplyStrict"/>, which
/// refuses any edit list a conforming LSP client would have to reject or could only guess at.
///
/// <para>
/// <b>Thread safety.</b> An instance is safe to share across threads: the workspace's document and
/// timer tables are concurrent dictionaries, the handlers hold no per-request state, and every
/// request opens its document under a fresh URI (an atomic counter), so concurrent requests never
/// see each other's text. Nothing outlives a request — the document is closed in a <c>finally</c>,
/// which also disposes the debounced-analysis timer <c>OpenDocument</c> scheduled (a driver that
/// left documents open would semantically analyse every one of them).
/// </para>
///
/// <para>
/// <b>Reuse.</b> The route methods and the applier know nothing about the formatter or any
/// formatting oracle; they observe only what a client observes (edits, and the text those edits
/// produce). Other edit-returning requests (code actions, P47) can be driven through the same
/// <see cref="ApplyStrict"/>.
/// </para>
/// </summary>
public sealed class LspFormattingDriver : IDisposable
{
    /// <summary>
    /// The trigger character every <see cref="OnType"/> request sends. The on-type handler ignores
    /// <c>request.Character</c> (<c>OnTypeFormattingHandler.cs</c>), so a request is fully described by
    /// (document, line); <c>"\n"</c> is the registration's <c>FirstTriggerCharacter</c>.
    /// </summary>
    public const string OnTypeTriggerCharacter = "\n";

    private static long s_nextDocumentId;

    private readonly SharpyWorkspace _workspace;
    private readonly SharpyFormattingHandler _full;
    private readonly SharpyRangeFormattingHandler _range;
    private readonly SharpyOnTypeFormattingHandler _onType;

    public LspFormattingDriver()
    {
        _workspace = new SharpyWorkspace(new CompilerApi(), NullLogger<SharpyWorkspace>.Instance);
        _full = new SharpyFormattingHandler(_workspace);
        _range = new SharpyRangeFormattingHandler(_workspace);
        _onType = new SharpyOnTypeFormattingHandler(_workspace);
    }

    /// <summary>
    /// The roster a sweep iterates: one row per formatting handler the server registers. Its
    /// totality against the <c>Sharpy.Lsp</c> assembly is a test (<c>LspFormattingDriverTests</c>).
    /// </summary>
    public static IReadOnlyList<LspFormattingRoute> Routes { get; } = new[]
    {
        new LspFormattingRoute(LspFormattingRouteKind.Full, typeof(SharpyFormattingHandler)),
        new LspFormattingRoute(LspFormattingRouteKind.Range, typeof(SharpyRangeFormattingHandler)),
        new LspFormattingRoute(LspFormattingRouteKind.OnType, typeof(SharpyOnTypeFormattingHandler)),
    };

    /// <summary>
    /// Format Document. Returns the handler's edits (empty for a <c>null</c> result) and the text they
    /// produce under <see cref="ApplyStrict"/>; throws <see cref="StrictEditApplicationException"/>
    /// when the edits are not legal for <paramref name="text"/>.
    /// </summary>
    public (IReadOnlyList<TextEdit> Edits, string Applied) Full(string text)
    {
        var edits = WithOpenDocument(text, uri => _full.Handle(
            new DocumentFormattingParams
            {
                TextDocument = new TextDocumentIdentifier(uri),
                Options = DefaultOptions(),
            },
            CancellationToken.None));
        return (edits, ApplyStrict(text, edits));
    }

    /// <summary>Format Selection over <paramref name="range"/>; see <see cref="Full"/>.</summary>
    public (IReadOnlyList<TextEdit> Edits, string Applied) Range(string text, LspRange range)
    {
        var edits = WithOpenDocument(text, uri => _range.Handle(
            new DocumentRangeFormattingParams
            {
                TextDocument = new TextDocumentIdentifier(uri),
                Range = range,
                Options = DefaultOptions(),
            },
            CancellationToken.None));
        return (edits, ApplyStrict(text, edits));
    }

    /// <summary>
    /// Format on type at <paramref name="line"/>, position <c>(line, 0)</c>, trigger
    /// <see cref="OnTypeTriggerCharacter"/>; see <see cref="Full"/>.
    /// </summary>
    public (IReadOnlyList<TextEdit> Edits, string Applied) OnType(string text, int line)
    {
        var edits = WithOpenDocument(text, uri => _onType.Handle(
            new DocumentOnTypeFormattingParams
            {
                TextDocument = new TextDocumentIdentifier(uri),
                Position = new Position(line, 0),
                Character = OnTypeTriggerCharacter,
                Options = DefaultOptions(),
            },
            CancellationToken.None));
        return (edits, ApplyStrict(text, edits));
    }

    /// <summary>
    /// Documents currently open in the driver's workspace — zero between requests, by construction.
    /// A sweep can assert it to show the driver retained nothing.
    /// </summary>
    public int OpenDocumentCount => _workspace.GetAllDocumentUris().Count;

    private static FormattingOptions DefaultOptions() => new() { TabSize = 4, InsertSpaces = true };

    private IReadOnlyList<TextEdit> WithOpenDocument<TContainer>(string text, Func<string, Task<TContainer>> handle)
        where TContainer : TextEditContainer?
    {
        var id = Interlocked.Increment(ref s_nextDocumentId);
        var uri = $"file:///lsp-formatting-driver/doc{id}.spy";
        _workspace.OpenDocument(uri, text, 1);
        try
        {
            var task = handle(uri);
            // The formatting handlers are synchronous (Task.FromResult); a request is driven to completion
            // before its document is closed, so blocking here is the contract, not a shortcut.
#pragma warning disable VSTHRD002 // Synchronously waiting on tasks — see above
            var container = task.GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
            return container is null ? Array.Empty<TextEdit>() : container.ToArray();
        }
        finally
        {
            _workspace.CloseDocument(uri);
        }
    }

    /// <summary>
    /// Applies <paramref name="edits"/> to <paramref name="text"/> with LSP semantics, strictly:
    /// <list type="bullet">
    /// <item>a position is (line, UTF-16 character); lines are ended by <c>\r\n</c>, <c>\n</c> or a lone
    /// <c>\r</c>, so a document with <c>k</c> line breaks has lines <c>0..k</c> (the last may be empty,
    /// and <c>[k:0-k:0]</c> on it is legal);</item>
    /// <item>a line past the last line, or a character past its line's CONTENT (into or beyond the line
    /// break), throws — the LSP spec lets a lenient client clamp these, and a lenient applier is
    /// exactly what would hide a handler's off-by-one;</item>
    /// <item><c>start &gt; end</c> throws; two edits whose ranges overlap throw (touching is legal, and
    /// inserts at one position keep their array order, per the spec);</item>
    /// <item>every range addresses the ORIGINAL text — edits never see each other's effect.</item>
    /// </list>
    /// </summary>
    public static string ApplyStrict(string text, IReadOnlyList<TextEdit> edits)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(edits);
        if (edits.Count == 0)
            return text;

        var lines = LineTable(text);
        var spans = new (int Start, int End, int Index, string NewText)[edits.Count];
        for (var i = 0; i < edits.Count; i++)
        {
            var edit = edits[i];
            if (edit.Range is null)
                throw new StrictEditApplicationException($"edit #{i} has no range", edits);
            var start = Offset(lines, edit.Range.Start, i, "start", edits);
            var end = Offset(lines, edit.Range.End, i, "end", edits);
            if (start > end)
                throw new StrictEditApplicationException(
                    $"edit #{i} {Describe(edit.Range)}: start is after end", edits);
            spans[i] = (start, end, i, edit.NewText ?? string.Empty);
        }

        // Stable order: by start, then end (an insert sorts before a replacement starting at its
        // position), then array order (inserts at one position keep the order the server sent).
        Array.Sort(spans, (a, b) =>
        {
            var c = a.Start.CompareTo(b.Start);
            if (c != 0)
                return c;
            c = a.End.CompareTo(b.End);
            return c != 0 ? c : a.Index.CompareTo(b.Index);
        });

        var sb = new StringBuilder(text.Length);
        var cursor = 0;
        for (var k = 0; k < spans.Length; k++)
        {
            var span = spans[k];
            if (span.Start < cursor)
            {
                var prior = spans[k - 1];
                throw new StrictEditApplicationException(
                    $"edit #{span.Index} {Describe(edits[span.Index].Range)} overlaps edit #{prior.Index} "
                    + Describe(edits[prior.Index].Range),
                    edits);
            }
            sb.Append(text, cursor, span.Start - cursor);
            sb.Append(span.NewText);
            cursor = span.End;
        }
        sb.Append(text, cursor, text.Length - cursor);
        return sb.ToString();
    }

    /// <summary>Start offset and content length of every line (line breaks: <c>\r\n</c>, <c>\n</c>, <c>\r</c>).</summary>
    private static SCG.List<(int Start, int Length)> LineTable(string text)
    {
        var lines = new SCG.List<(int Start, int Length)>();
        var lineStart = 0;
        var i = 0;
        while (i < text.Length)
        {
            var ch = text[i];
            if (ch == '\r' || ch == '\n')
            {
                lines.Add((lineStart, i - lineStart));
                i += ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n' ? 2 : 1;
                lineStart = i;
            }
            else
            {
                i++;
            }
        }
        lines.Add((lineStart, text.Length - lineStart));
        return lines;
    }

    private static int Offset(
        SCG.List<(int Start, int Length)> lines, Position position, int index, string which,
        IReadOnlyList<TextEdit> edits)
    {
        if (position is null)
            throw new StrictEditApplicationException($"edit #{index} has no {which} position", edits);
        if (position.Line < 0 || position.Line >= lines.Count)
            throw new StrictEditApplicationException(
                $"edit #{index} {which} ({position.Line}:{position.Character}) is past the document's last line "
                + $"({lines.Count - 1})",
                edits);
        var line = lines[position.Line];
        if (position.Character < 0 || position.Character > line.Length)
            throw new StrictEditApplicationException(
                $"edit #{index} {which} ({position.Line}:{position.Character}) is outside line {position.Line}, "
                + $"whose content is {line.Length} UTF-16 units",
                edits);
        return line.Start + position.Character;
    }

    private static string Describe(LspRange range)
        => $"[{range.Start.Line}:{range.Start.Character}-{range.End.Line}:{range.End.Character}]";

    public void Dispose() => _workspace.Dispose();
}
