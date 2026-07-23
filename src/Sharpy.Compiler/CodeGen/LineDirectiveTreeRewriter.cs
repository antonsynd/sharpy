using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Sharpy.Compiler.CodeGen;

/// <summary>
/// Applies a <see cref="LineDirectiveEditPlan"/> to the emitter's compilation unit as a syntax-tree
/// rewrite, so the default (<c>EmitLineDirectives</c>-on) emit path can hand the tree straight to
/// <c>CSharpCompilation.Create</c> with no <c>ParseText</c> round-trip (#1108, restoring the #1095
/// zero-parse win on the default path).
/// </summary>
/// <remarks>
/// <para>
/// Everything the #line post-processing does is a <b>leading-trivia</b> change: rewriting an enhanced
/// directive's placeholder char offset, splicing <c>#line hidden</c>/<c>#line default</c> lines, and
/// normalizing the <c>#nullable enable</c> newline the emitter prepends with LF after
/// <c>NormalizeWhitespace</c> emitted CRLF. Token <em>text</em> never changes, so the rewrite is a
/// single <c>ReplaceTokens</c> pass (plus a final <c>WithEndOfFileToken</c> for the EOF
/// <c>#line default</c>) — the emitter's green nodes are preserved and re-rooted, not reparsed.
/// </para>
/// <para>
/// The plan is computed by <see cref="LineDirectiveEditPlanner"/> over the same normalized snapshot
/// (<c>root.ToFullString()</c>), so its absolute positions map 1:1 onto the tree, and the resulting
/// text is byte-identical to <see cref="LineDirectivePostProcessor.Process"/> — asserted here in Debug
/// and pinned corpus-wide by <c>ReparseEquivalenceConformanceTests</c> (D4/D6).
/// </para>
/// </remarks>
internal static class LineDirectiveTreeRewriter
{
    /// <summary>
    /// Returns <paramref name="root"/> with <paramref name="plan"/> applied. <paramref name="plan"/>
    /// must have been produced from <c>root.ToFullString()</c> so its positions align with the tree.
    /// </summary>
    public static CompilationUnitSyntax Apply(CompilationUnitSyntax root, LineDirectiveEditPlan plan)
    {
        var byToken = new Dictionary<SyntaxToken, List<LineDirectiveEdit>>();
        var eofInsertions = new List<LineInsertionEdit>();
        int fullEnd = root.FullSpan.End;

        foreach (var edit in plan.Edits)
        {
            int anchor = edit switch
            {
                CharOffsetEdit co => co.DirectiveStart,
                LineInsertionEdit ins => ins.Position,
                _ => throw new InvalidOperationException($"Unknown edit {edit.GetType().Name}")
            };

            // An insertion anchored at (or past) end-of-file is the trailing #line default; it belongs
            // to the end-of-file token, which ReplaceTokens does not visit — handled separately below.
            if (anchor >= fullEnd)
            {
                eofInsertions.Add((LineInsertionEdit)edit);
                continue;
            }

            var token = root.FindToken(anchor);
            if (!byToken.TryGetValue(token, out var list))
                byToken[token] = list = new List<LineDirectiveEdit>();
            list.Add(edit);
        }

        // The #nullable-enable pragma is the only lone-LF source in the snapshot (it is prepended after
        // NormalizeWhitespace); normalize the first token's leading trivia to the plan newline so the
        // rewritten text matches the oracle, which normalizes globally.
        var firstToken = root.GetFirstToken();

        var tokensToReplace = new HashSet<SyntaxToken>(byToken.Keys) { firstToken };

        var result = root.ReplaceTokens(tokensToReplace, (original, _) =>
        {
            var leading = original.LeadingTrivia;
            if (byToken.TryGetValue(original, out var edits))
                leading = ApplyLeadingEdits(leading, edits);
            if (original == firstToken)
                leading = NormalizeNewlines(leading, plan.Newline);
            return original.WithLeadingTrivia(leading);
        });

        if (eofInsertions.Count > 0)
        {
            var eof = result.EndOfFileToken;
            var leading = eof.LeadingTrivia.ToList();
            foreach (var ins in eofInsertions.OrderBy(e => e.Position))
                leading.AddRange(ParseLeadingTrivia(ins.Text));
            result = result.WithEndOfFileToken(eof.WithLeadingTrivia(TriviaList(leading)));
        }

        Debug.Assert(
            result.ToFullString() == plan.ApplyToText(root.ToFullString()),
            "LineDirectiveTreeRewriter output must be byte-identical to LineDirectivePostProcessor.Process.");

        return result;
    }

    /// <summary>
    /// Applies a token's char-offset replacements and <c>#line hidden</c> insertions to its leading
    /// trivia. Char-offset edits swap only the directive's offset token (preserving its exact spacing
    /// and trailing newline); insertions are placed by absolute position so both newline-attachment
    /// conventions (trailing-of-previous vs leading-of-this) resolve correctly.
    /// </summary>
    private static SyntaxTriviaList ApplyLeadingEdits(SyntaxTriviaList leading, List<LineDirectiveEdit> edits)
    {
        var list = leading.ToList();

        foreach (var co in edits.OfType<CharOffsetEdit>())
        {
            int idx = list.FindIndex(t =>
                t.IsKind(SyntaxKind.LineSpanDirectiveTrivia) && t.FullSpan.Start == co.DirectiveStart);
            if (idx < 0)
            {
                throw new InvalidOperationException(
                    $"Enhanced #line directive not found at position {co.DirectiveStart}.");
            }

            var structure = (LineSpanDirectiveTriviaSyntax)list[idx].GetStructure()!;
            var newOffset = Literal(co.NewOffset).WithTriviaFrom(structure.CharacterOffset);
            list[idx] = Trivia(structure.WithCharacterOffset(newOffset));
        }

        // Insert from the highest position first so earlier insertion indices stay valid. Indices are
        // computed against the ORIGINAL leading spans (char-offset swaps above preserve count/order).
        foreach (var ins in edits.OfType<LineInsertionEdit>().OrderByDescending(e => e.Position))
        {
            int insertIdx = leading.Count(t => t.FullSpan.End <= ins.Position);
            list.InsertRange(insertIdx, ParseLeadingTrivia(ins.Text));
        }

        return TriviaList(list);
    }

    /// <summary>
    /// Normalizes every newline in <paramref name="leading"/> to <paramref name="newline"/>, re-parsing
    /// the trivia text (tiny — the #nullable pragma). Returns the input unchanged when already uniform.
    /// </summary>
    private static SyntaxTriviaList NormalizeNewlines(SyntaxTriviaList leading, string newline)
    {
        var text = leading.ToFullString();
        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal);
        if (newline == "\r\n")
            normalized = normalized.Replace("\n", "\r\n", StringComparison.Ordinal);
        return normalized == text ? leading : ParseLeadingTrivia(normalized);
    }
}
