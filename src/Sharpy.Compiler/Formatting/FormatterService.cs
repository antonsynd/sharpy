using Sharpy.Compiler.Logging;
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
        options ??= FormatOptions.Default;

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

        var indentString = options.UseTabs ? "\t" : new string(' ', options.IndentSize);
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

        return new FormatterResult
        {
            FormattedText = formatted,
            HasChanges = formatted != source
        };
    }
}
