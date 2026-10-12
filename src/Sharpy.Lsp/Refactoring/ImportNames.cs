using System.Collections.Immutable;
using System.Text;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Formatting;
using Sharpy.Compiler.Parser.Ast;
using SCG = System.Collections.Generic;

namespace Sharpy.Lsp.Refactoring;

/// <summary>
/// The source-text facts of an import's NAMES that Remove unused import and Organize Imports share (P47,
/// #2232, plan D4/D5): which names SPY0452 marks unused — the diagnostic names ONE imported name, at that
/// name's position — and the spans a provider deletes to drop some names of a statement while keeping the
/// rest verbatim. Nothing here re-renders an import.
/// </summary>
internal static class ImportNames
{
    /// <summary>The names of an import statement (none for <c>from m import *</c> and for any other statement).</summary>
    public static ImmutableArray<ImportAlias> Of(Statement statement) => statement switch
    {
        ImportStatement import => import.Names,
        FromImportStatement { ImportAll: false } fromImport => fromImport.Names,
        _ => ImmutableArray<ImportAlias>.Empty,
    };

    /// <summary>
    /// The 0-based document offsets <c>[Start, End)</c> to delete so that the names at
    /// <paramref name="removed"/> (indexes into <paramref name="names"/>; never all of them) leave the statement
    /// and every other byte stays. Each maximal run of removed names goes as a unit: when every name of the
    /// run stands alone on its line (a parenthesized import's one-name-per-line layout) those whole lines go,
    /// their trailing comma and comment with them (D5); otherwise the run and the comma that separates it
    /// from the kept name before it — or, for a run at the start, from the kept name after it.
    /// </summary>
    public static SCG.List<(int Start, int End)> RemovalSpans(
        SourceLines source, ImmutableArray<ImportAlias> names, IReadOnlySet<int> removed)
    {
        var spans = new SCG.List<(int, int)>();
        for (var i = 0; i < names.Length; i++)
        {
            if (!removed.Contains(i))
                continue;

            var j = i;
            while (j + 1 < names.Length && removed.Contains(j + 1))
                j++;

            var ownLines = true;
            for (var k = i; k <= j && ownLines; k++)
                ownLines = StandsAloneOnItsLine(source, names[k]);

            if (ownLines)
            {
                for (var k = i; k <= j; k++)
                    spans.Add((source.StartOffset(names[k].LineStart), source.StartOffset(names[k].LineStart + 1)));
            }
            else if (i > 0)
            {
                spans.Add((EndOffset(source, names[i - 1]), EndOffset(source, names[j])));
            }
            else
            {
                spans.Add((StartOffset(source, names[i]), StartOffset(source, names[j + 1])));
            }

            i = j;
        }

        return spans;
    }

    /// <summary>
    /// Lines <paramref name="first"/>..<paramref name="last"/> (1-based) with <paramref name="spans"/> deleted,
    /// split again on the one line table's rule.
    /// </summary>
    public static IReadOnlyList<string> LinesWithout(SourceLines source, int first, int last, SCG.IReadOnlyList<(int Start, int End)> spans)
    {
        var start = source.StartOffset(first);
        var end = source.StartOffset(last) + source.Line(last).Length;
        var text = new StringBuilder();
        var at = start;
        foreach (var (spanStart, spanEnd) in spans.OrderBy(s => s.Start))
        {
            var from = Math.Max(spanStart, at);
            if (from > at)
                text.Append(source.Text, at, Math.Min(from, end) - at);
            at = Math.Max(at, Math.Min(spanEnd, end));
        }

        if (end > at)
            text.Append(source.Text, at, end - at);
        return LineDiff.Split(text.ToString()).Lines;
    }

    /// <summary>The 0-based document offset where <paramref name="name"/> starts.</summary>
    public static int StartOffset(SourceLines source, ImportAlias name) => source.StartOffset(name.LineStart) + name.ColumnStart - 1;

    /// <summary>The 0-based document offset just past <paramref name="name"/> (its <c>as</c> name included).</summary>
    public static int EndOffset(SourceLines source, ImportAlias name) => source.StartOffset(name.LineEnd) + name.ColumnEnd - 1;

    /// <summary>
    /// True when <paramref name="name"/> is the only code on its line: nothing but indentation before it, and
    /// after it nothing but an optional comma and the line's inline comment. The line must not be the last.
    /// </summary>
    private static bool StandsAloneOnItsLine(SourceLines source, ImportAlias name)
    {
        if (name.LineStart != name.LineEnd || !source.Contains(name.LineStart + 1))
            return false;

        var content = source.Line(name.LineStart);
        var startColumn = Math.Min(content.Length, name.ColumnStart - 1);
        var endColumn = Math.Min(content.Length, name.ColumnEnd - 1);
        var after = content[endColumn..];
        if (source.InlineComment(name.LineStart) is { } comment && comment.Column - 1 >= endColumn)
            after = content[endColumn..Math.Min(content.Length, comment.Column - 1)];

        var rest = after.Trim();
        return string.IsNullOrWhiteSpace(content[..startColumn]) && (rest.Length == 0 || rest == ",");
    }
}

/// <summary>
/// The imported names SPY0452 marks unused for one request: a diagnostic of the request's context or of the
/// analysis whose start is a name's position marks that name; a context diagnostic that starts at no name's
/// position (a client that sends a line range) marks every name on its line.
/// </summary>
internal sealed class UnusedImportNames
{
    private readonly SCG.HashSet<(int Line, int Column)> _positions = new();
    private readonly SCG.HashSet<int> _lines = new();

    public UnusedImportNames(CodeActionProviderContext context, SCG.IEnumerable<ImportAlias> names)
    {
        var known = new SCG.HashSet<(int, int)>(names.Select(n => (n.LineStart, n.ColumnStart)));
        foreach (var d in context.Diagnostics)
        {
            if (d.Code?.String != DiagnosticCodes.Validation.UnusedImport)
                continue;
            var position = (d.Range.Start.Line + 1, d.Range.Start.Character + 1);
            if (known.Contains(position))
                _positions.Add(position);
            else
                _lines.Add(position.Item1);
        }

        if (context.Analysis?.Diagnostics is { } compilerDiagnostics)
        {
            foreach (var d in compilerDiagnostics)
            {
                if (d.Code == DiagnosticCodes.Validation.UnusedImport && d.Line is { } line && d.Column is { } column)
                    _positions.Add((line, column));
            }
        }
    }

    /// <summary>True when SPY0452 marks <paramref name="name"/> unused.</summary>
    public bool Contains(ImportAlias name) =>
        _positions.Contains((name.LineStart, name.ColumnStart)) || _lines.Contains(name.LineStart);
}
