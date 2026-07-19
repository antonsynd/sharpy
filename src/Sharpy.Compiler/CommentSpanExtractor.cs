using Sharpy.Compiler.Lexer;

namespace Sharpy.Compiler;

/// <summary>
/// Extracts <see cref="CommentSpan"/>s from lexer trivia. Shared by the single-file
/// analyze facade and the project pipeline so both surface identical comment-location
/// data (used by LSP hover filtering). Comment trivia is only present when the lexer
/// ran with <c>preserveTrivia</c> enabled; otherwise the result is empty.
/// </summary>
internal static class CommentSpanExtractor
{
    public static IReadOnlyList<CommentSpan> Extract(IReadOnlyList<Token> tokens)
    {
        var spans = new List<CommentSpan>();
        var seen = new HashSet<(int, int)>();
        foreach (var tok in tokens)
        {
            AppendCommentSpans(tok.LeadingTrivia, spans, seen);
            AppendCommentSpans(tok.TrailingTrivia, spans, seen);
        }
        return spans;

        static void AppendCommentSpans(
            IReadOnlyList<Trivia>? trivia,
            List<CommentSpan> spans,
            HashSet<(int, int)> seen)
        {
            if (trivia == null)
                return;
            foreach (var t in trivia)
            {
                if (t.Kind != TriviaKind.Comment)
                    continue;
                var key = (t.Line, t.Column);
                if (!seen.Add(key))
                    continue;
                spans.Add(new CommentSpan(t.Line, t.Column, t.Column + t.Text.Length));
            }
        }
    }
}
