using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Lexer;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Pretty;
using Sharpy.Compiler.Text;

namespace Sharpy.Compiler.Formatting;

public static class FormatterService
{
    /// <summary>
    /// Strips trailing spaces/tabs from every line whose line break is code — never from a line that
    /// ends inside a string literal or an f-/t-string (a triple-quoted string's inner line, a
    /// multi-line replacement field), where the whitespace is the program's data (#2062). The spans
    /// come from lexing <paramref name="text"/>; if it does not lex, no line is stripped.
    /// </summary>
    internal static string StripTrailingWhitespace(string text, FormatOptions options)
    {
        var lexer = new Lexer.Lexer(text, NullLogger.Instance);
        var tokens = lexer.TokenizeAll();
        var literalLines = lexer.Diagnostics.HasErrors
            ? null
            : Lexer.LiteralSpans.LinesStartingInside(text, Lexer.LiteralSpans.Of(tokens));

        var lines = text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            // Line i (0-based) ends inside a literal when line i + 1 (1-based i + 2) starts inside one.
            if (literalLines == null || literalLines.Contains(i + 2))
                continue;
            lines[i] = lines[i].TrimEnd(' ', '\t');
        }

        var result = string.Join("\n", lines);

        if (options.TrailingNewline && result.Length > 0 && !result.EndsWith("\n", StringComparison.Ordinal))
            result += "\n";

        return result;
    }

    public static FormatterResult Format(string source, FormatOptions? options = null, string? filePath = null)
    {
        var result = FormatUnchecked(source, options, filePath, out var sourceAst);
        if (sourceAst == null)
            return result;

        // The net observes the FINAL text (after whitespace stripping), so a regression in
        // stripping is caught as well as one in the unparser.
        var declined = CheckMeaningPreserved(source, sourceAst, result.FormattedText);
        if (declined != null)
        {
            return new FormatterResult
            {
                FormattedText = source,
                HasChanges = false,
                Diagnostics = new[] { declined with { FilePath = filePath } }
            };
        }

        return result;
    }

    /// <summary>
    /// The formatter's output WITHOUT the meaning-preservation net (<see cref="CheckMeaningPreserved"/>):
    /// the text <see cref="Format"/> writes when the net accepts it. Only the P22b sweeps
    /// (FormatterMeaningPreservationSweepTests, FormatterEmitInvarianceSweepTests) call it: a refused
    /// output is the source unchanged, which would make every damage oracle pass trivially, so the
    /// sweeps observe the raw output and check the net's verdict against it. Never a production path.
    /// </summary>
    internal static FormatterResult FormatUnchecked(string source, FormatOptions? options = null, string? filePath = null)
        => FormatUnchecked(source, options, filePath, out _);

    /// <summary>Lex, parse, unparse and strip; <paramref name="sourceAst"/> is null when the source does not lex or parse (the result then carries those diagnostics).</summary>
    private static FormatterResult FormatUnchecked(string source, FormatOptions? options, string? filePath, out Module? sourceAst)
    {
        options ??= FormatOptions.Default;
        sourceAst = null;

        var sourceText = new SourceText(source, filePath ?? "<format>");
        var logger = NullLogger.Instance;

        var lexResult = FileCompilationPipeline.Lex(sourceText, logger, preserveTrivia: true);
        if (lexResult.HasErrors)
        {
            return new FormatterResult
            {
                FormattedText = source,
                HasChanges = false,
                Diagnostics = lexResult.Diagnostics.GetAll()
            };
        }

        var parseResult = FileCompilationPipeline.Parse(lexResult.Tokens, logger);
        if (parseResult.HasErrors || parseResult.Module == null)
        {
            return new FormatterResult
            {
                FormattedText = source,
                HasChanges = false,
                Diagnostics = parseResult.Diagnostics.GetAll()
            };
        }

        // Always the language's indentation unit — never a caller-chosen width (indentation.md).
        var indentString = new string(' ', Lexer.Lexer.IndentWidth);
        var unparseOptions = new UnparseOptions
        {
            IndentString = indentString,
            LineEnding = options.LineEnding,
            PreserveTrivia = true,
            Formatting = new Pretty.FormatOptions
            {
                BlankLinesAroundTopLevelDefs = options.BlankLinesAroundTopLevelDefs,
                BlankLinesBetweenClassMembers = options.BlankLinesBetweenClassMembers,
                TrailingNewline = options.TrailingNewline
            }
        };

        var raw = Unparser.Unparse(parseResult.Module, unparseOptions);
        var formatted = StripTrailingWhitespace(raw, options);
        sourceAst = parseResult.Module;

        return new FormatterResult
        {
            FormattedText = formatted,
            HasChanges = formatted != source
        };
    }

    /// <summary>
    /// The formatter's meaning-preservation net (SPY0912, P22b). Formatting may change layout
    /// only; it must never change what the file says. <paramref name="output"/> is refused when,
    /// compared with <paramref name="source"/>:
    /// <list type="bullet">
    /// <item>it does not re-lex;</item>
    /// <item>its comment SEQUENCE differs (a comment dropped, added or reordered — comment text is
    /// compared without trailing spaces/tabs, which the formatter strips by design);</item>
    /// <item>a comment keeps its place in the sequence but changes its attachment
    /// (<see cref="CommentAnchors"/>: inline or own-line, neighbouring code tokens, block depth);</item>
    /// <item>the multiset of backtick-escaped identifier tokens differs — token-based on purpose:
    /// the structural comparer does not see every escape flag, so this check must not depend on it;</item>
    /// <item>it does not re-parse;</item>
    /// <item>its normalised AST is not structurally equal to <paramref name="sourceAst"/>'s.</item>
    /// </list>
    /// The lex-level checks run before the parse so that a dropped escape which also breaks the
    /// parse (<c>f(`class`=7)</c> → <c>f(class=7)</c>) is reported as the drop, its cause.
    /// Returns <see langword="null"/> when the output preserves meaning. A non-null result always
    /// indicates a formatter bug — the caller leaves the file unchanged.
    /// </summary>
    internal static CompilerDiagnostic? CheckMeaningPreserved(string source, Module sourceAst, string output)
    {
        var logger = NullLogger.Instance;

        var sourceLex = FileCompilationPipeline.Lex(new SourceText(source, "<format>"), logger, preserveTrivia: true);
        var outputLex = FileCompilationPipeline.Lex(new SourceText(output, "<format-output>"), logger, preserveTrivia: true);
        if (outputLex.HasErrors)
            return Declined(DescribeReparseFailure(outputLex.Diagnostics.GetAll()), line: null);

        var commentVerdict = CompareSequences(CommentsOf(sourceLex.Tokens), CommentsOf(outputLex.Tokens),
            dropped: c => $"drop comment '{c.Text}' at line {c.Line}",
            added: c => $"add comment '{c.Text}' (formatted line {c.Line})",
            moved: c => $"move comment '{c.Text}' at line {c.Line} out of source order");
        if (commentVerdict != null)
            return Declined(commentVerdict.Value.What, commentVerdict.Value.Line);

        var escapeVerdict = CompareSequences(EscapedNamesOf(sourceLex.Tokens), EscapedNamesOf(outputLex.Tokens),
            dropped: e => $"drop the backtick escape on '{e.Text}' at line {e.Line}",
            added: e => $"add a backtick escape on '{e.Text}' (formatted line {e.Line})",
            moved: e => $"reorder the backtick-escaped name '{e.Text}' at line {e.Line}");
        if (escapeVerdict != null)
            return Declined(escapeVerdict.Value.What, escapeVerdict.Value.Line);

        var outputParse = FileCompilationPipeline.Parse(outputLex.Tokens, logger);
        if (outputParse.HasErrors || outputParse.Module == null)
            return Declined(DescribeReparseFailure(outputParse.Diagnostics.GetAll()), line: null);

        var sourceNorm = AstNormalizer.Instance.NormalizeModule(sourceAst);
        var outputNorm = AstNormalizer.Instance.NormalizeModule(outputParse.Module);
        if (!StructuralEqualityComparer.Instance.Equals(sourceNorm, outputNorm))
        {
            var line = FirstDifferingStatementLine(sourceAst, sourceNorm, outputNorm);
            return Declined($"change the program's structure (first differing statement at line {line})", line);
        }

        // Same program, same comments in the same order — each must also keep its attachment (#2077).
        // Checked last: a structural change also changes a comment's neighbours, and is the cause.
        if (CommentAnchors.FirstMoved(CommentAnchors.Of(sourceLex.Tokens), CommentAnchors.Of(outputLex.Tokens)) is { } moved)
            return Declined($"move comment '{moved.Before.Text}' at line {moved.Before.Line} ({CommentAnchors.Describe(moved.Before, moved.After)})", moved.Before.Line);

        return null;
    }

    private static CompilerDiagnostic Declined(string what, int? line) =>
        new(
            $"formatting declined: the output would {what}; the file was left unchanged",
            CompilerDiagnosticSeverity.Error,
            Line: line,
            Column: line.HasValue ? 1 : null,
            Code: DiagnosticCodes.Infrastructure.FormatterDeclined);

    private static string DescribeReparseFailure(IReadOnlyList<CompilerDiagnostic> diagnostics)
    {
        var first = diagnostics.FirstOrDefault(d => d.IsError);
        if (first == null)
            return "not re-parse";
        var where = first.Line.HasValue ? $" at formatted line {first.Line}" : "";
        var code = first.Code != null ? $"{first.Code} " : "";
        return $"not re-parse (first error{where}: {code}{first.Message})";
    }

    private readonly record struct Located(string Text, int Line);

    /// <summary>Comments in source order; text without trailing spaces/tabs (the formatter strips those).</summary>
    private static List<Located> CommentsOf(IReadOnlyList<Token> tokens)
    {
        var result = new List<Located>();
        foreach (var token in tokens)
        {
            AddComments(token.LeadingTrivia, result);
            AddComments(token.TrailingTrivia, result);
        }
        return result;

        static void AddComments(IReadOnlyList<Trivia>? trivia, List<Located> into)
        {
            if (trivia == null)
                return;
            foreach (var t in trivia)
            {
                if (t.Kind == TriviaKind.Comment)
                    into.Add(new Located(t.Text.TrimEnd(' ', '\t'), t.Line));
            }
        }
    }

    /// <summary>Backtick-escaped identifier tokens in source order.</summary>
    private static List<Located> EscapedNamesOf(IReadOnlyList<Token> tokens) =>
        tokens.Where(t => t.IsBacktickEscaped).Select(t => new Located(t.Value, t.Line)).ToList();

    /// <summary>
    /// Compares two source-ordered sequences and describes the first difference: an item of
    /// <paramref name="before"/> that <paramref name="after"/> has fewer of (dropped), an item of
    /// <paramref name="after"/> that <paramref name="before"/> has fewer of (added), or equal
    /// multisets in a different order (moved).
    /// </summary>
    private static (string What, int? Line)? CompareSequences(
        List<Located> before, List<Located> after,
        Func<Located, string> dropped, Func<Located, string> added, Func<Located, string> moved)
    {
        var common = Math.Min(before.Count, after.Count);
        var k = 0;
        while (k < common && before[k].Text == after[k].Text)
            k++;
        if (k == before.Count && k == after.Count)
            return null;

        int Count(List<Located> items, string text) => items.Count(i => i.Text == text);

        for (var i = k; i < before.Count; i++)
        {
            if (Count(before, before[i].Text) > Count(after, before[i].Text))
                return (dropped(before[i]), before[i].Line);
        }

        for (var i = k; i < after.Count; i++)
        {
            if (Count(after, after[i].Text) > Count(before, after[i].Text))
                return (added(after[i]), null);
        }

        return (moved(before[k]), before[k].Line);
    }

    /// <summary>
    /// The source line of the first top-level statement whose normalised form differs; the
    /// module's first line when every shared statement is equal (a statement added or removed at
    /// the end, or a module-level difference such as the docstring).
    /// </summary>
    private static int FirstDifferingStatementLine(Module sourceAst, Module sourceNorm, Module outputNorm)
    {
        var common = Math.Min(sourceNorm.Body.Length, outputNorm.Body.Length);
        for (var i = 0; i < common; i++)
        {
            if (!StructuralEqualityComparer.Instance.Equals(sourceNorm.Body[i], outputNorm.Body[i]))
                return Math.Max(1, sourceAst.Body[i].LineStart);
        }
        if (sourceAst.Body.Length > common)
            return Math.Max(1, sourceAst.Body[common].LineStart);
        if (sourceAst.Body.Length > 0)
            return Math.Max(1, sourceAst.Body[^1].LineEnd);
        return 1;
    }
}
