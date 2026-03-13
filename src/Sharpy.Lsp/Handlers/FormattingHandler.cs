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
internal sealed class SharpyFormattingHandler : DocumentFormattingHandlerBase
{
    private readonly SharpyWorkspace _workspace;

    public SharpyFormattingHandler(SharpyWorkspace workspace)
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

        // Tokenize to get Indent/Dedent positions and multi-line string ranges
        var (lineIndentLevels, tokens) = BuildIndentMap(text);

        // Find lines inside multi-line strings (these should not be re-indented)
        var multiLineStringLines = FindMultiLineStringLines(tokens);

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
    /// Also returns the token list for reuse by other analyses.
    /// </summary>
    private static (Dictionary<int, int> LineIndent, System.Collections.Generic.List<Token> Tokens) BuildIndentMap(string source)
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
            return (new Dictionary<int, int>(), new System.Collections.Generic.List<Token>());
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

        return (lineIndent, tokens);
    }

    /// <summary>
    /// Finds lines that are inside multi-line strings by examining lexer tokens.
    /// A string token that spans multiple lines indicates a triple-quoted string;
    /// interior lines (excluding the first) should not be re-indented.
    /// </summary>
    private static HashSet<int> FindMultiLineStringLines(System.Collections.Generic.List<Token> tokens)
    {
        var result = new HashSet<int>();

        foreach (var token in tokens)
        {
            if (token.Type != TokenType.String && token.Type != TokenType.FStringText)
                continue;

            // Count newlines in the token value to determine the end line.
            // The token's Value includes the full string content including quotes.
            var startLine = token.Line;
            var lineCount = 0;
            var value = token.Value;
            for (var i = 0; i < value.Length; i++)
            {
                if (value[i] == '\n')
                    lineCount++;
            }

            if (lineCount == 0)
                continue;

            // Mark all lines spanned by this multi-line string token
            var endLine = startLine + lineCount;
            for (var line = startLine; line <= endLine; line++)
            {
                result.Add(line);
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
