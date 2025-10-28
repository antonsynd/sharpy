namespace Sharpy.Compiler.Lexer;

/// <summary>
/// Tokenizes Sharpy source code into a stream of tokens
/// </summary>
public class Lexer
{
    private readonly string _source;
    private int _position;
    private int _line = 1;
    private int _column = 1;
    private readonly List<int> _indentStack = new() { 0 };

    public Lexer(string source)
    {
        _source = source;
    }

    public List<Token> TokenizeAll()
    {
        var tokens = new List<Token>();

        while (true)
        {
            var token = NextToken();
            tokens.Add(token);

            if (token.Type == TokenType.Eof)
                break;
        }

        return tokens;
    }

    public Token NextToken()
    {
        // TODO: Implement tokenization logic
        // This is a placeholder - port from Rust implementation

        if (_position >= _source.Length)
            return new Token(TokenType.Eof, "", _line, _column);

        // Skip whitespace
        SkipWhitespace();

        if (_position >= _source.Length)
            return new Token(TokenType.Eof, "", _line, _column);

        var current = _source[_position];

        // Simple placeholder implementation
        if (char.IsDigit(current))
            return ReadNumber();

        if (char.IsLetter(current) || current == '_')
            return ReadIdentifierOrKeyword();

        // Add more token types...

        throw new LexerError($"Unexpected character: '{current}'", _line, _column);
    }

    private void SkipWhitespace()
    {
        while (_position < _source.Length &&
               char.IsWhiteSpace(_source[_position]) &&
               _source[_position] != '\n')
        {
            _position++;
            _column++;
        }
    }

    private Token ReadNumber()
    {
        var start = _position;
        var startColumn = _column;

        while (_position < _source.Length && char.IsDigit(_source[_position]))
        {
            _position++;
            _column++;
        }

        var value = _source[start.._position];
        return new Token(TokenType.Integer, value, _line, startColumn);
    }

    private Token ReadIdentifierOrKeyword()
    {
        var start = _position;
        var startColumn = _column;

        while (_position < _source.Length &&
               (char.IsLetterOrDigit(_source[_position]) || _source[_position] == '_'))
        {
            _position++;
            _column++;
        }

        var value = _source[start.._position];
        var type = value switch
        {
            "def" => TokenType.Def,
            "class" => TokenType.Class,
            "if" => TokenType.If,
            "else" => TokenType.Else,
            "while" => TokenType.While,
            "for" => TokenType.For,
            "return" => TokenType.Return,
            "True" => TokenType.True,
            "False" => TokenType.False,
            "None" => TokenType.None,
            "public" => TokenType.Public,
            "private" => TokenType.Private,
            _ => TokenType.Identifier
        };

        return new Token(type, value, _line, startColumn);
    }
}
