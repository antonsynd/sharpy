using Sharpy.Compiler.Lexer;

namespace Sharpy.Lsp;

internal static class IndentationService
{
    internal static (Dictionary<int, int> LineIndent, List<Token> Tokens) BuildIndentMap(string source)
    {
        var lexer = new Compiler.Lexer.Lexer(source);
        List<Token> tokens;
        try
        {
            tokens = lexer.TokenizeAll();
        }
        catch (Exception)
        {
            return (new Dictionary<int, int>(), new List<Token>());
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
                    if (!lineIndent.ContainsKey(token.Line))
                    {
                        lineIndent[token.Line] = currentIndent;
                    }
                    break;
            }
        }

        return (lineIndent, tokens);
    }

    internal static HashSet<int> FindMultiLineStringLines(List<Token> tokens)
    {
        var result = new HashSet<int>();

        foreach (var token in tokens)
        {
            if (token.Type != TokenType.String && token.Type != TokenType.FStringText)
                continue;

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

            var endLine = startLine + lineCount;
            for (var line = startLine; line <= endLine; line++)
            {
                result.Add(line);
            }
        }

        return result;
    }
}
