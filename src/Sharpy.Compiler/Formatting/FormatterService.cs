using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Pretty;
using Sharpy.Compiler.Text;

namespace Sharpy.Compiler.Formatting;

public static class FormatterService
{
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

        var formatted = Unparser.Unparse(parseResult.Module, unparseOptions);

        return new FormatterResult
        {
            FormattedText = formatted,
            HasChanges = formatted != source
        };
    }
}
