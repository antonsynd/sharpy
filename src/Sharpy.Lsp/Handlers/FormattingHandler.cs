using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler.Lexer;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Sharpy.Lsp.Handlers;

/// <summary>
/// Handles textDocument/formatting requests.
/// Uses the Lexer's Indent/Dedent tokens to determine correct indentation levels,
/// then re-indents each line according to the user's tab size/spaces preference.
/// </summary>
internal sealed class SharplyFormattingHandler : DocumentFormattingHandlerBase
{
    private readonly SharplyWorkspace _workspace;

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

        // Tokenize to get Indent/Dedent positions
        var lineIndentLevels = BuildIndentMap(text);

        // Find lines inside multi-line strings (these should not be re-indented)
        var multiLineStringLines = FindMultiLineStringLines(text);

        var lines = text.Split('\n');
        var formatted = new List<string>();

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

            // Lines inside multi-line strings: preserve as-is
            if (multiLineStringLines.Contains(i + 1)) // tokens use 1-based lines
            {
                formatted.Add(line);
                continue;
            }

            // Apply indent level from token stream
            var level = lineIndentLevels.TryGetValue(i + 1, out var l) ? l : 0;
            var formattedLine = string.Concat(Enumerable.Repeat(indentStr, level)) + trimmed;
            formatted.Add(formattedLine);
        }

        var formattedText = string.Join("\n", formatted);

        if (formattedText == text)
            return Task.FromResult<TextEditContainer?>(null);

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

    /// <summary>
    /// Tokenizes the source and builds a map of 1-based line number to indent level.
    /// </summary>
    private static Dictionary<int, int> BuildIndentMap(string source)
    {
        var lexer = new Compiler.Lexer.Lexer(source);
        System.Collections.Generic.List<Token> tokens;
        try
        {
            tokens = lexer.TokenizeAll();
        }
        catch (Exception)
        {
            // If lexing fails (e.g., invalid syntax), return empty map (no reformatting).
            return new Dictionary<int, int>();
        }

        var lineIndent = new Dictionary<int, int>();
        var currentIndent = 0;

        foreach (var token in tokens)
        {
            switch (token.Type)
            {
                case TokenType.Indent:
                    currentIndent++;
                    break;
                case TokenType.Dedent:
                    if (currentIndent > 0)
                        currentIndent--;
                    break;
                case TokenType.Newline:
                case TokenType.Eof:
                    break;
                default:
                    // Record the indent level for the first real token on each line
                    if (!lineIndent.ContainsKey(token.Line))
                    {
                        lineIndent[token.Line] = currentIndent;
                    }
                    break;
            }
        }

        return lineIndent;
    }

    /// <summary>
    /// Finds lines that are inside multi-line (triple-quoted) strings.
    /// These lines should not be re-indented.
    /// </summary>
    private static HashSet<int> FindMultiLineStringLines(string source)
    {
        var result = new HashSet<int>();
        var lines = source.Split('\n');

        var inTripleQuote = false;
        char tripleQuoteChar = '"';

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');
            var lineNum = i + 1; // 1-based

            var j = 0;
            while (j < line.Length)
            {
                if (!inTripleQuote)
                {
                    // Look for triple quote start
                    if (j + 2 < line.Length &&
                        (line[j] == '"' || line[j] == '\'') &&
                        line[j + 1] == line[j] &&
                        line[j + 2] == line[j])
                    {
                        inTripleQuote = true;
                        tripleQuoteChar = line[j];
                        j += 3;
                        continue;
                    }
                    // Skip single-quoted strings
                    if (line[j] == '"' || line[j] == '\'')
                    {
                        var quote = line[j];
                        j++;
                        while (j < line.Length && line[j] != quote)
                        {
                            if (line[j] == '\\')
                                j++; // skip escaped char
                            j++;
                        }
                        if (j < line.Length)
                            j++; // skip closing quote
                        continue;
                    }
                    // Skip comments
                    if (line[j] == '#')
                        break;
                }
                else
                {
                    // Inside triple quote — mark this line as inside multi-line string
                    result.Add(lineNum);

                    // Look for closing triple quote
                    if (j + 2 < line.Length &&
                        line[j] == tripleQuoteChar &&
                        line[j + 1] == tripleQuoteChar &&
                        line[j + 2] == tripleQuoteChar)
                    {
                        inTripleQuote = false;
                        j += 3;
                        continue;
                    }
                }

                j++;
            }

            // If still in triple quote at end of line, mark it
            if (inTripleQuote)
            {
                result.Add(lineNum);
            }
        }

        return result;
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
