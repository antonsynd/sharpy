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

    /// <summary>
    /// The 1-based lines whose text must not be re-indented, stripped or blanked: every line that
    /// starts inside a string literal or an f-/t-string (a triple-quoted string's inner lines, a
    /// multi-line replacement field's continuation lines — re-indenting a hole changes a t-string's
    /// <c>Interpolation.expression</c>, #2022). The spans come from the one shared definition
    /// (<see cref="LiteralSpans"/>, #2062) that <c>FormatterService</c> uses.
    /// </summary>
    internal static HashSet<int> FindMultiLineStringLines(List<Token> tokens, string source) =>
        LiteralSpans.LinesStartingInside(source, LiteralSpans.Of(tokens));
}
