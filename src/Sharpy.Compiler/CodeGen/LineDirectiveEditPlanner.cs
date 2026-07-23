using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Sharpy.Compiler.CodeGen;

/// <summary>
/// A single edit the #line post-processing makes to the generated C# snapshot. Edits are computed
/// once (over the emitter's normalized text) by <see cref="LineDirectiveEditPlanner"/> and are the
/// SINGLE source of the arithmetic for both the text oracle
/// (<see cref="LineDirectivePostProcessor.Process"/>) and the zero-parse
/// <see cref="LineDirectiveTreeRewriter"/> — so the two can never drift (D4/D5, #1108).
/// </summary>
internal abstract record LineDirectiveEdit
{
    /// <summary>Absolute offset in the text snapshot the edit is anchored to (used for ordering and
    /// for mapping the edit onto the syntax tree).</summary>
    public abstract int SortPosition { get; }
}

/// <summary>
/// Replaces the placeholder character offset (<c>1</c>) an enhanced <c>#line (l,c)-(l,c) 1 "file"</c>
/// directive carries with the real indentation of the following line.
/// </summary>
/// <param name="DirectiveStart">Absolute start of the directive (the <c>#</c>), used to locate the
/// <c>LineSpanDirectiveTriviaSyntax</c> in the tree.</param>
/// <param name="PlaceholderPosition">Absolute position of the single placeholder digit <c>1</c> in the
/// text snapshot (the char the text applier rewrites).</param>
/// <param name="NewOffset">The corrected character offset (always &gt;= 1).</param>
internal sealed record CharOffsetEdit(int DirectiveStart, int PlaceholderPosition, int NewOffset)
    : LineDirectiveEdit
{
    public override int SortPosition => PlaceholderPosition;
}

/// <summary>
/// Inserts a whole directive line (<c>#line hidden</c> or the EOF <c>#line default</c>) at
/// <paramref name="Position"/>. <paramref name="Text"/> already carries the exact indentation and
/// trailing (and, for EOF <c>#line default</c>, leading) newline the oracle produces, so both appliers
/// splice it verbatim.
/// </summary>
internal sealed record LineInsertionEdit(int Position, string Text) : LineDirectiveEdit
{
    public override int SortPosition => Position;
}

/// <summary>
/// The ordered set of edits that turn the emitter's raw normalized C# into its post-processed form,
/// plus the newline the snapshot uses. Consumed by <see cref="LineDirectivePostProcessor.Process"/>
/// (text) and <see cref="LineDirectiveTreeRewriter"/> (tree).
/// </summary>
internal sealed record LineDirectiveEditPlan(string Newline, IReadOnlyList<LineDirectiveEdit> Edits)
{
    /// <summary>
    /// Applies the plan to <paramref name="text"/>, reproducing the historical
    /// <see cref="LineDirectivePostProcessor"/> byte-for-byte: every newline is normalized to
    /// <see cref="Newline"/> (the emitter prepends <c>#nullable enable</c> with LF while
    /// <c>NormalizeWhitespace</c> emits CRLF, so the raw snapshot is mixed), each placeholder char
    /// offset is rewritten, and the <c>#line hidden</c>/<c>#line default</c> lines are spliced in.
    /// </summary>
    public string ApplyToText(string text)
    {
        var sb = new StringBuilder(text.Length + 64);
        int editIdx = 0;
        int i = 0;
        while (i < text.Length)
        {
            // Insertions anchored exactly at i go in before the character at i is copied.
            while (editIdx < Edits.Count && Edits[editIdx].SortPosition == i
                && Edits[editIdx] is LineInsertionEdit ins)
            {
                sb.Append(ins.Text);
                editIdx++;
            }

            // A char-offset replacement rewrites the single placeholder digit at i.
            if (editIdx < Edits.Count && Edits[editIdx].SortPosition == i
                && Edits[editIdx] is CharOffsetEdit co)
            {
                sb.Append(co.NewOffset);
                i += 1; // skip the placeholder '1'
                editIdx++;
                continue;
            }

            char c = text[i];
            if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
            {
                sb.Append(Newline);
                i += 2;
                continue;
            }
            if (c == '\n')
            {
                sb.Append(Newline);
                i += 1;
                continue;
            }

            sb.Append(c);
            i++;
        }

        // Insertions anchored at EOF (e.g. the trailing #line default).
        while (editIdx < Edits.Count)
        {
            if (Edits[editIdx] is LineInsertionEdit tail)
                sb.Append(tail.Text);
            editIdx++;
        }

        return sb.ToString();
    }
}

/// <summary>
/// Computes the <see cref="LineDirectiveEditPlan"/> for a block of generated C# — the two operations
/// the old <c>LineDirectivePostProcessor</c> performed on the normalized text (#1077):
/// <list type="number">
/// <item><description>rewrite each enhanced <c>#line (l,c)-(l,c) 1 "file"</c> directive's placeholder
/// char offset (<c>1</c>) to the real indentation of the following line (spaces; tabs count 4);</description></item>
/// <item><description>after that first mapped line, if more non-blank code lines occur before the next
/// <c>#line</c> directive, insert <c>#line hidden</c> so the debugger does not map the intermediate
/// C# lines, and <c>#line default</c> at EOF when no further directive follows.</description></item>
/// </list>
/// The planner is the sole owner of this arithmetic; <see cref="LineDirectivePostProcessor.Process"/>
/// and <see cref="LineDirectiveTreeRewriter"/> merely apply the result (D4/D5, #1108).
/// </summary>
internal static class LineDirectiveEditPlanner
{
    private static readonly Regex EnhancedDirectiveRegex = new(
        @"^(\s*)#line \((\d+), (\d+)\) - \((\d+), (\d+)\) 1 (""[^""]*"")$",
        RegexOptions.Compiled);

    private static readonly Regex AnyLineDirectiveRegex = new(
        @"^\s*#line\b",
        RegexOptions.Compiled);

    private const int DefaultTabWidth = 4;

    public static LineDirectiveEditPlan Plan(string csharpCode)
    {
        var newline = csharpCode.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var lines = SplitLines(csharpCode);
        var edits = new List<LineDirectiveEdit>();

        for (int i = 0; i < lines.Count; i++)
        {
            var enhancedMatch = EnhancedDirectiveRegex.Match(lines[i].Content);
            if (!enhancedMatch.Success)
                continue;

            int charOffset = 1;
            if (i + 1 < lines.Count)
                charOffset = Math.Max(1, CountLeadingSpaces(lines[i + 1].Content));

            // The placeholder '1' is the single character immediately before the quoted file name
            // (group 6): the directive text is `... ) 1 "file"`, so it sits at group6.Index - 2.
            int placeholderInLine = enhancedMatch.Groups[6].Index - 2;
            edits.Add(new CharOffsetEdit(
                DirectiveStart: lines[i].Start,
                PlaceholderPosition: lines[i].Start + placeholderInLine,
                NewOffset: charOffset));

            if (i + 1 >= lines.Count)
                continue;

            int firstCodeLineIndex = i + 1;
            if (!NeedsLineHidden(lines, firstCodeLineIndex, out int nextDirectiveIndex))
                continue;

            var indent = enhancedMatch.Groups[1].Value;
            int secondCodeLineIndex = firstCodeLineIndex + 1;
            int hiddenPos = secondCodeLineIndex < lines.Count
                ? lines[secondCodeLineIndex].Start
                : csharpCode.Length;
            edits.Add(new LineInsertionEdit(hiddenPos, indent + "#line hidden" + newline));

            // The old post-processor re-emitted the remaining construct lines (including the trailing
            // empty split element when the file ends in a newline, and adding a terminating newline to
            // the final line otherwise) before appending #line default. Both cases reduce to inserting
            // a leading newline ahead of the directive at EOF.
            if (nextDirectiveIndex >= lines.Count)
            {
                edits.Add(new LineInsertionEdit(
                    csharpCode.Length, newline + indent + "#line default" + newline));
            }

            // Mirror the old control flow: the scanned construct lines are consumed.
            i = nextDirectiveIndex - 1;
        }

        edits.Sort(static (a, b) => a.SortPosition.CompareTo(b.SortPosition));
        return new LineDirectiveEditPlan(newline, edits);
    }

    /// <summary>
    /// Splits <paramref name="text"/> on <c>\r\n</c> or <c>\n</c> (matching <c>String.Split</c> with
    /// both separators) while recording each line's absolute start offset, so edits can be anchored to
    /// positions that map 1:1 onto the syntax tree.
    /// </summary>
    private static List<(string Content, int Start)> SplitLines(string text)
    {
        var result = new List<(string, int)>();
        int start = 0;
        int i = 0;
        while (i < text.Length)
        {
            if (text[i] == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
            {
                result.Add((text.Substring(start, i - start), start));
                i += 2;
                start = i;
            }
            else if (text[i] == '\n')
            {
                result.Add((text.Substring(start, i - start), start));
                i += 1;
                start = i;
            }
            else
            {
                i++;
            }
        }

        result.Add((text.Substring(start), start));
        return result;
    }

    private static bool NeedsLineHidden(
        List<(string Content, int Start)> lines, int codeLineIndex, out int nextDirectiveIndex)
    {
        nextDirectiveIndex = codeLineIndex + 1;
        int codeLineCount = 0;

        for (int j = codeLineIndex + 1; j < lines.Count; j++)
        {
            if (AnyLineDirectiveRegex.IsMatch(lines[j].Content))
            {
                nextDirectiveIndex = j;
                return codeLineCount > 0;
            }

            if (!string.IsNullOrWhiteSpace(lines[j].Content))
                codeLineCount++;
        }

        nextDirectiveIndex = lines.Count;
        return codeLineCount > 0;
    }

    private static int CountLeadingSpaces(string line)
    {
        int count = 0;
        foreach (char c in line)
        {
            if (c == ' ')
                count++;
            else if (c == '\t')
                count += DefaultTabWidth;
            else
                break;
        }
        return count;
    }
}
