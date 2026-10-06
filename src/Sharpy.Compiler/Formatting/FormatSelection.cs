namespace Sharpy.Compiler.Formatting;

/// <summary>
/// An editor selection as Format Selection receives it, in plain 0-based ints (the Compiler does not
/// reference LSP types): the start line, and the end line and character. The start character does
/// not matter — a hunk is a run of whole lines.
/// </summary>
internal readonly record struct FormatSelection(int StartLine, int EndLine, int EndCharacter)
{
    /// <summary>
    /// The selected source lines <c>[s, e]</c> (P22e decision 2): <c>s = StartLine</c>; <c>e = EndLine</c>,
    /// minus one when <c>EndCharacter == 0</c> and <c>EndLine &gt; StartLine</c> (an editor's whole-line
    /// selection ends at column 0 of the next line); when the document ends with a line break and
    /// <c>e</c> reaches its last line before that break (<c>e ≥ n − 2</c>), the empty final line
    /// <c>n − 1</c> is selected too. <paramref name="lines"/> is <see cref="LineDiff.Split"/>'s.
    /// </summary>
    public (int Start, int End) SelectedLines(IReadOnlyList<string> lines)
    {
        var n = lines.Count;
        var e = EndLine;
        if (EndCharacter == 0 && EndLine > StartLine)
            e--;
        var endsWithLineBreak = n >= 2 && lines[n - 1].Length == 0;
        if (endsWithLineBreak && e >= n - 2)
            e = n - 1;
        return (StartLine, Math.Min(e, n - 1));
    }

    /// <summary>
    /// Whether the selection <c>[s, e]</c> of an <paramref name="n"/>-line document takes
    /// <paramref name="hunk"/> (decision 2, as corrected at /implement-plan): a replacement or deletion of
    /// <c>[a, b)</c> iff it touches the selection (<c>a ≤ e ∧ b − 1 ≥ s</c>); a pure insertion before line
    /// <c>a</c> iff both of its neighbours that exist are selected
    /// (<c>(a == 0 ∨ s ≤ a − 1 ≤ e) ∧ (a == n ∨ s ≤ a ≤ e)</c>) — so formatting a function's body does
    /// not add the blank lines above its <c>def</c>. The route-parity sweep restates this predicate for
    /// its <c>local</c> oracle; the two must agree.
    /// </summary>
    public static bool Selects(LineHunk hunk, int s, int e, int n)
    {
        int a = hunk.Start, b = hunk.End;
        if (b > a)
            return a <= e && b - 1 >= s;
        return (a == 0 || (a - 1 >= s && a - 1 <= e)) && (a == n || (a >= s && a <= e));
    }
}
