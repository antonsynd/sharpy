using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Sharpy.Lsp.Handlers;

/// <summary>
/// Handles textDocument/formatting requests.
/// Normalizes indentation to consistent 4-space width based on Python-like block structure.
/// </summary>
internal sealed class SharplyFormattingHandler : DocumentFormattingHandlerBase
{
    private readonly SharplyWorkspace _workspace;

    // Keywords that decrease indent when they appear at the start of a line
    private static readonly HashSet<string> DedentKeywords = new(StringComparer.Ordinal)
    {
        "elif", "else", "except", "finally", "case"
    };

    public SharplyFormattingHandler(SharplyWorkspace workspace)
    {
        _workspace = workspace;
    }

    public override Task<TextEditContainer?> Handle(
        DocumentFormattingParams request,
        CancellationToken ct)
    {
        var uri = request.TextDocument.Uri.ToString();
        var doc = _workspace.GetDocument(uri);

        if (doc == null)
            return Task.FromResult<TextEditContainer?>(null);

        var text = doc.Text;
        var tabSize = (int)request.Options.TabSize;
        var insertSpaces = request.Options.InsertSpaces;
        var indentStr = insertSpaces ? new string(' ', tabSize) : "\t";

        var lines = text.Split('\n');
        var formatted = new List<string>();
        var indentLevel = 0;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');
            var trimmed = line.TrimStart();

            // Empty lines stay empty
            if (trimmed.Length == 0)
            {
                formatted.Add("");
                continue;
            }

            // Check if this line starts with a dedent keyword
            var firstWord = GetFirstWord(trimmed);
            if (DedentKeywords.Contains(firstWord) && indentLevel > 0)
            {
                indentLevel--;
            }

            // Handle pass at current level (don't change indent for pass itself)
            var formattedLine = string.Concat(Enumerable.Repeat(indentStr, indentLevel)) + trimmed;
            formatted.Add(formattedLine);

            // Check if this line ends with colon (increases indent for next line)
            var withoutComment = StripTrailingComment(trimmed);
            if (withoutComment.EndsWith(':'))
            {
                indentLevel++;
            }
        }

        // Build the formatted text
        var formattedText = string.Join("\n", formatted);

        // If nothing changed, return null (no edits needed)
        if (formattedText == text)
            return Task.FromResult<TextEditContainer?>(null);

        // Replace the entire document
        var lastLine = lines.Length - 1;
        var lastCol = lines[lastLine].TrimEnd('\r').Length;

        var edits = new List<TextEdit>
        {
            new()
            {
                Range = new LspRange(
                    new Position(0, 0),
                    new Position(lastLine, lastCol)),
                NewText = formattedText
            }
        };

        return Task.FromResult<TextEditContainer?>(new TextEditContainer(edits));
    }

    private static string GetFirstWord(string trimmedLine)
    {
        var end = 0;
        while (end < trimmedLine.Length && char.IsLetterOrDigit(trimmedLine[end]) || end < trimmedLine.Length && trimmedLine[end] == '_')
        {
            end++;
        }
        return trimmedLine[..end];
    }

    private static string StripTrailingComment(string line)
    {
        // Simple heuristic: find # not inside a string
        var inSingleQuote = false;
        var inDoubleQuote = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '\'' && !inDoubleQuote)
                inSingleQuote = !inSingleQuote;
            else if (c == '"' && !inSingleQuote)
                inDoubleQuote = !inDoubleQuote;
            else if (c == '#' && !inSingleQuote && !inDoubleQuote)
            {
                return line[..i].TrimEnd();
            }
        }
        return line.TrimEnd();
    }

    protected override DocumentFormattingRegistrationOptions CreateRegistrationOptions(
        DocumentFormattingCapability capability,
        ClientCapabilities clientCapabilities)
    {
        return new DocumentFormattingRegistrationOptions
        {
            DocumentSelector = TextDocumentSelector.ForPattern("**/*.spy")
        };
    }
}
