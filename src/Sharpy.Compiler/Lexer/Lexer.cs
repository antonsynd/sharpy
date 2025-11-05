using System.Text;

namespace Sharpy.Compiler.Lexer;

/// <summary>
/// Tokenizes Sharpy source code into a stream of tokens.
/// Implements indentation-based syntax with INDENT/DEDENT tokens.
/// </summary>
public class Lexer
{
    private readonly string _source;
    private int _position;
    private int _line = 1;
    private int _column = 1;
    private readonly Stack<int> _indentStack = new();
    private readonly Queue<Token> _pendingTokens = new();
    private bool _atLineStart = true;
    private bool _parenDepth = false;  // Track if we're inside (), [], or {}
    private int _bracketDepth = 0;

    // Keywords mapping
    private static readonly Dictionary<string, TokenType> Keywords = new()
    {
        // Control flow
        { "def", TokenType.Def },
        { "class", TokenType.Class },
        { "struct", TokenType.Struct },
        { "interface", TokenType.Interface },
        { "enum", TokenType.Enum },
        { "if", TokenType.If },
        { "else", TokenType.Else },
        { "elif", TokenType.Elif },
        { "while", TokenType.While },
        { "for", TokenType.For },
        { "in", TokenType.In },
        { "return", TokenType.Return },
        { "break", TokenType.Break },
        { "continue", TokenType.Continue },
        { "pass", TokenType.Pass },
        { "try", TokenType.Try },
        { "except", TokenType.Except },
        { "finally", TokenType.Finally },
        { "raise", TokenType.Raise },
        { "assert", TokenType.Assert },

        // Import
        { "import", TokenType.Import },
        { "from", TokenType.From },
        { "as", TokenType.As },

        // Type/Value
        { "auto", TokenType.Auto },
        { "const", TokenType.Const },
        { "lambda", TokenType.Lambda },

        // Boolean values and operators
        { "True", TokenType.True },
        { "False", TokenType.False },
        { "None", TokenType.None },
        { "and", TokenType.And },
        { "or", TokenType.Or },
        { "not", TokenType.Not },
        { "is", TokenType.Is },
    };

    public Lexer(string source)
    {
        _source = source;
        _indentStack.Push(0);  // Base indentation level
    }

    /// <summary>
    /// Tokenize the entire source into a list of tokens
    /// </summary>
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

    /// <summary>
    /// Get the next token from the source
    /// </summary>
    public Token NextToken()
    {
        // Return pending tokens first (INDENT/DEDENT)
        if (_pendingTokens.Count > 0)
            return _pendingTokens.Dequeue();

        // Handle end of file
        if (_position >= _source.Length)
        {
            // Generate DEDENTs for remaining indentation levels
            if (_indentStack.Count > 1)
            {
                _indentStack.Pop();
                return new Token(TokenType.Dedent, "", _line, _column);
            }
            return new Token(TokenType.Eof, "", _line, _column);
        }

        // Handle indentation at line start
        if (_atLineStart && _bracketDepth == 0)
        {
            var indentLevel = MeasureIndentation();
            var currentIndent = _indentStack.Peek();

            if (indentLevel > currentIndent)
            {
                _indentStack.Push(indentLevel);
                _atLineStart = false;
                return new Token(TokenType.Indent, "", _line, 1);
            }
            else if (indentLevel < currentIndent)
            {
                // Generate DEDENT tokens
                var dedents = new List<Token>();
                while (_indentStack.Count > 1 && _indentStack.Peek() > indentLevel)
                {
                    _indentStack.Pop();
                    dedents.Add(new Token(TokenType.Dedent, "", _line, 1));
                }

                // No need to check here - MeasureIndentation already validated

                _atLineStart = false;

                // Queue remaining dedents and return first
                for (int i = 1; i < dedents.Count; i++)
                    _pendingTokens.Enqueue(dedents[i]);

                return dedents[0];
            }

            _atLineStart = false;
        }

        // Skip whitespace (except newlines)
        SkipWhitespace();

        if (_position >= _source.Length)
            return NextToken();  // Recurse to handle EOF

        var startLine = _line;
        var startColumn = _column;
        var current = _source[_position];

        // Comments
        if (current == '#')
            return ReadComment();

        // Backslash line continuation
        if (current == '\\')
        {
            var nextPos = _position + 1;

            // If next character doesn't exist
            if (nextPos >= _source.Length)
            {
                // Backslash at EOF - check if it's the only token
                if (_position == 0)
                {
                    // Just a standalone backslash token
                    // Fall through to treat as backslash operator/delimiter
                }
                else
                {
                    // Backslash at EOF after other content - likely a mistake
                    throw new LexerError("Backslash at end of file", _line, _column);
                }
            }
            else
            {
                // Check for trailing whitespace after backslash
                var tempPos = nextPos;
                while (tempPos < _source.Length && (_source[tempPos] == ' ' || _source[tempPos] == '\t'))
                    tempPos++;

                // After skipping whitespace, check if we have a newline
                if (tempPos < _source.Length && (_source[tempPos] == '\n' || _source[tempPos] == '\r'))
                {
                    if (tempPos != nextPos)
                    {
                        // There was whitespace between \ and newline
                        throw new LexerError("Backslash line continuation cannot have trailing whitespace", _line, _column);
                    }

                    // Valid line continuation - skip the backslash and newline
                    _position = tempPos;
                    if (_source[_position] == '\r' && _position + 1 < _source.Length && _source[_position + 1] == '\n')
                        _position += 2;
                    else
                        _position++;

                    _line++;
                    _column = 1;
                    return NextToken();
                }
                else if (tempPos >= _source.Length && tempPos != nextPos)
                {
                    // Backslash followed by whitespace at end of file
                    throw new LexerError("Backslash at end of file", _line, _column);
                }
                // Otherwise, fall through to treat backslash as operator
            }
        }

        // Newlines
        if (current == '\n' || (current == '\r' && Peek() == '\n'))
        {
            if (_bracketDepth > 0)
            {
                // Inside brackets, skip newlines
                if (current == '\r')
                {
                    _position++;
                    _column++;
                }
                _position++;
                _line++;
                _column = 1;
                _atLineStart = true;
                return NextToken();
            }

            // Normal newline token
            if (current == '\r')
            {
                _position++;
                _column++;
            }
            _position++;
            _line++;
            _column = 1;
            _atLineStart = true;
            return new Token(TokenType.Newline, "\n", startLine, startColumn);
        }

        // String literals
        if (current == '"' || current == '\'')
            return ReadString();

        // F-strings
        if (current == 'f' && (_position + 1 < _source.Length) &&
            (_source[_position + 1] == '"' || _source[_position + 1] == '\''))
            return ReadFString();

        // Raw strings
        if (current == 'r' && (_position + 1 < _source.Length) &&
            (_source[_position + 1] == '"' || _source[_position + 1] == '\''))
            return ReadRawString();

        // Backtick-delimited literal names
        if (current == '`')
            return ReadLiteralName();

        // Numbers
        if (char.IsDigit(current))
            return ReadNumber();

        // Identifiers and keywords
        if (char.IsLetter(current) || current == '_')
            return ReadIdentifierOrKeyword();

        // Operators and delimiters
        return ReadOperatorOrDelimiter();
    }

    private int MeasureIndentation()
    {
        var indent = 0;
        var tempPos = _position;
        var hasSpaces = false;
        var hasTabs = false;

        while (tempPos < _source.Length)
        {
            var c = _source[tempPos];

            if (c == ' ')
            {
                hasSpaces = true;
                indent++;
                tempPos++;
            }
            else if (c == '\t')
            {
                hasTabs = true;
                tempPos++;
            }
            else if (c == '\n' || c == '\r' || c == '#')
            {
                // Empty line or comment line - skip indentation measurement
                return _indentStack.Peek();
            }
            else if (tempPos >= _source.Length)
            {
                // Whitespace-only line at EOF - treat as empty line
                return _indentStack.Peek();
            }
            else
            {
                break;
            }
        }

        // Whitespace-only line - treat as empty
        if (tempPos >= _source.Length)
        {
            return _indentStack.Peek();
        }

        // Check for mixed tabs and spaces
        if (hasSpaces && hasTabs)
            throw new LexerError("Mixed tabs and spaces in indentation", _line, 1);

        // No tabs allowed at all
        if (hasTabs)
            throw new LexerError("Tabs are not allowed for indentation. Use 4 spaces.", _line, 1);

        // Validate 4-space indentation
        if (indent % 4 != 0)
            throw new LexerError($"Indentation must be multiple of 4 spaces (found {indent})", _line, 1);

        // Check for indentation mismatch - indent level not matching any previous level
        if (indent < _indentStack.Peek())
        {
            // Dedenting - need to check if it matches a previous level
            var foundMatch = false;
            foreach (var level in _indentStack)
            {
                if (level == indent)
                {
                    foundMatch = true;
                    break;
                }
            }
            if (!foundMatch)
            {
                throw new LexerError("Indentation mismatch", _line, 1);
            }
        }

        _position = tempPos;
        _column = indent + 1;
        return indent;
    }

    private void SkipWhitespace()
    {
        while (_position < _source.Length)
        {
            var c = _source[_position];
            if (c == ' ' || c == '\t')
            {
                _position++;
                _column++;
            }
            else
            {
                break;
            }
        }
    }

    private char Peek(int offset = 1)
    {
        var pos = _position + offset;
        return pos < _source.Length ? _source[pos] : '\0';
    }

    private Token ReadComment()
    {
        var startColumn = _column;
        var sb = new StringBuilder();

        // Skip the '#'
        _position++;
        _column++;

        while (_position < _source.Length && _source[_position] != '\n' && _source[_position] != '\r')
        {
            sb.Append(_source[_position]);
            _position++;
            _column++;
        }

        // Comments are typically skipped, but we can return them for doc tools
        // For now, skip and get next token
        return NextToken();
    }

    private Token ReadString()
    {
        var startLine = _line;
        var startColumn = _column;
        var quote = _source[_position];
        var sb = new StringBuilder();

        _position++;
        _column++;

        // Check for triple-quoted string
        var isTriple = _position + 1 < _source.Length &&
                       _source[_position] == quote &&
                       _source[_position + 1] == quote;

        if (isTriple)
        {
            _position += 2;
            _column += 2;
            return ReadTripleQuotedString(quote, startLine, startColumn);
        }

        // Single-line string
        while (_position < _source.Length)
        {
            var c = _source[_position];

            if (c == quote)
            {
                _position++;
                _column++;
                return new Token(TokenType.String, sb.ToString(), startLine, startColumn);
            }

            if (c == '\\')
            {
                _position++;
                _column++;
                if (_position >= _source.Length)
                    throw new LexerError("Unterminated string literal", _line, _column);

                var escaped = _source[_position];
                sb.Append(ProcessEscapeSequence(escaped));
                _position++;
                _column++;
            }
            else if (c == '\n' || c == '\r')
            {
                throw new LexerError("Unterminated string literal", _line, _column);
            }
            else
            {
                sb.Append(c);
                _position++;
                _column++;
            }
        }

        throw new LexerError("Unterminated string literal", _line, _column);
    }

    private Token ReadTripleQuotedString(char quote, int startLine, int startColumn)
    {
        var sb = new StringBuilder();

        while (_position < _source.Length)
        {
            // Check for end triple quote
            if (_source[_position] == quote &&
                _position + 2 < _source.Length &&
                _source[_position + 1] == quote &&
                _source[_position + 2] == quote)
            {
                _position += 3;
                _column += 3;
                return new Token(TokenType.String, sb.ToString(), startLine, startColumn);
            }

            var c = _source[_position];

            if (c == '\n')
            {
                sb.Append(c);
                _position++;
                _line++;
                _column = 1;
            }
            else if (c == '\r' && Peek() == '\n')
            {
                sb.Append('\n');
                _position += 2;
                _line++;
                _column = 1;
            }
            else if (c == '\\')
            {
                _position++;
                _column++;
                if (_position >= _source.Length)
                    throw new LexerError("Unterminated string literal", _line, _column);

                var escaped = _source[_position];
                sb.Append(ProcessEscapeSequence(escaped));
                _position++;
                _column++;
            }
            else
            {
                sb.Append(c);
                _position++;
                _column++;
            }
        }

        throw new LexerError("Unterminated triple-quoted string", _line, _column);
    }

    private Token ReadFString()
    {
        var startLine = _line;
        var startColumn = _column;

        // Skip 'f'
        _position++;
        _column++;

        var quote = _source[_position];
        _position++;
        _column++;

        // Check for triple-quoted f-string
        var isTriple = _position + 1 < _source.Length &&
                       _source[_position] == quote &&
                       _source[_position + 1] == quote;

        if (isTriple)
        {
            _position += 2;
            _column += 2;
            return ReadTripleQuotedFString(quote, startLine, startColumn);
        }

        // Single-line f-string
        var sb = new StringBuilder();

        while (_position < _source.Length)
        {
            var c = _source[_position];

            if (c == quote)
            {
                _position++;
                _column++;
                return new Token(TokenType.FString, sb.ToString(), startLine, startColumn);
            }

            if (c == '{')
            {
                // Start of interpolation - for now, collect as-is
                // Parser will handle interpolation parsing
                sb.Append(c);
                _position++;
                _column++;

                // Track brace depth for nested expressions
                var braceDepth = 1;
                while (_position < _source.Length && braceDepth > 0)
                {
                    c = _source[_position];
                    sb.Append(c);

                    if (c == '{') braceDepth++;
                    else if (c == '}') braceDepth--;

                    _position++;
                    _column++;
                }
            }
            else if (c == '\\')
            {
                _position++;
                _column++;
                if (_position >= _source.Length)
                    throw new LexerError("Unterminated f-string literal", _line, _column);

                var escaped = _source[_position];
                sb.Append(ProcessEscapeSequence(escaped));
                _position++;
                _column++;
            }
            else if (c == '\n' || c == '\r')
            {
                throw new LexerError("Unterminated f-string literal", _line, _column);
            }
            else
            {
                sb.Append(c);
                _position++;
                _column++;
            }
        }

        throw new LexerError("Unterminated f-string literal", _line, _column);
    }

    private Token ReadTripleQuotedFString(char quote, int startLine, int startColumn)
    {
        var sb = new StringBuilder();

        while (_position < _source.Length)
        {
            // Check for end triple quote
            if (_source[_position] == quote &&
                _position + 2 < _source.Length &&
                _source[_position + 1] == quote &&
                _source[_position + 2] == quote)
            {
                _position += 3;
                _column += 3;
                return new Token(TokenType.FString, sb.ToString(), startLine, startColumn);
            }

            var c = _source[_position];

            if (c == '{')
            {
                sb.Append(c);
                _position++;
                _column++;

                var braceDepth = 1;
                while (_position < _source.Length && braceDepth > 0)
                {
                    c = _source[_position];
                    sb.Append(c);

                    if (c == '{') braceDepth++;
                    else if (c == '}') braceDepth--;
                    else if (c == '\n')
                    {
                        _line++;
                        _column = 0;
                    }

                    _position++;
                    _column++;
                }
            }
            else if (c == '\n')
            {
                sb.Append(c);
                _position++;
                _line++;
                _column = 1;
            }
            else if (c == '\r' && Peek() == '\n')
            {
                sb.Append('\n');
                _position += 2;
                _line++;
                _column = 1;
            }
            else
            {
                sb.Append(c);
                _position++;
                _column++;
            }
        }

        throw new LexerError("Unterminated triple-quoted f-string", _line, _column);
    }

    private Token ReadRawString()
    {
        var startLine = _line;
        var startColumn = _column;

        // Skip 'r'
        _position++;
        _column++;

        var quote = _source[_position];
        _position++;
        _column++;

        // Check for triple-quoted raw string
        var isTriple = _position + 1 < _source.Length &&
                       _source[_position] == quote &&
                       _source[_position + 1] == quote;

        if (isTriple)
        {
            _position += 2;
            _column += 2;

            var sb = new StringBuilder();
            while (_position < _source.Length)
            {
                if (_source[_position] == quote &&
                    _position + 2 < _source.Length &&
                    _source[_position + 1] == quote &&
                    _source[_position + 2] == quote)
                {
                    _position += 3;
                    _column += 3;
                    return new Token(TokenType.String, sb.ToString(), startLine, startColumn);
                }

                var c = _source[_position];
                if (c == '\n')
                {
                    sb.Append(c);
                    _position++;
                    _line++;
                    _column = 1;
                }
                else
                {
                    sb.Append(c);
                    _position++;
                    _column++;
                }
            }
            throw new LexerError("Unterminated raw string", _line, _column);
        }

        // Single-line raw string
        var sb2 = new StringBuilder();
        while (_position < _source.Length)
        {
            var c = _source[_position];

            if (c == quote)
            {
                _position++;
                _column++;
                return new Token(TokenType.String, sb2.ToString(), startLine, startColumn);
            }

            if (c == '\n' || c == '\r')
                throw new LexerError("Unterminated raw string", _line, _column);

            sb2.Append(c);
            _position++;
            _column++;
        }

        throw new LexerError("Unterminated raw string", _line, _column);
    }

    private char ProcessEscapeSequence(char escaped)
    {
        return escaped switch
        {
            'n' => '\n',
            'r' => '\r',
            't' => '\t',
            'b' => '\b',
            'f' => '\f',
            '0' => '\0',
            '\\' => '\\',
            '\'' => '\'',
            '"' => '"',
            'a' => '\a',
            'v' => '\v',
            _ => throw new LexerError($"Invalid escape sequence: \\{escaped}", _line, _column)
        };
    }

    private Token ReadNumber()
    {
        var startLine = _line;
        var startColumn = _column;
        var sb = new StringBuilder();
        var isFloat = false;

        // Check for hex, binary, or octal prefix
        if (_source[_position] == '0' && _position + 1 < _source.Length)
        {
            var nextChar = _source[_position + 1];
            if (nextChar == 'x' || nextChar == 'X')
            {
                // Hexadecimal
                return ReadHexNumber(startLine, startColumn);
            }
            else if (nextChar == 'b' || nextChar == 'B')
            {
                // Binary
                return ReadBinaryNumber(startLine, startColumn);
            }
            else if (nextChar == 'o' || nextChar == 'O')
            {
                // Octal
                return ReadOctalNumber(startLine, startColumn);
            }
        }

        // Read integer part
        while (_position < _source.Length && (char.IsDigit(_source[_position]) || _source[_position] == '_'))
        {
            if (_source[_position] != '_')
                sb.Append(_source[_position]);
            _position++;
            _column++;
        }

        // Check for decimal point
        if (_position < _source.Length && _source[_position] == '.' &&
            _position + 1 < _source.Length && char.IsDigit(_source[_position + 1]))
        {
            isFloat = true;
            sb.Append('.');
            _position++;
            _column++;

            // Read fractional part
            while (_position < _source.Length && (char.IsDigit(_source[_position]) || _source[_position] == '_'))
            {
                if (_source[_position] != '_')
                    sb.Append(_source[_position]);
                _position++;
                _column++;
            }
        }

        // Check for scientific notation (e or E)
        if (_position < _source.Length && (_source[_position] == 'e' || _source[_position] == 'E'))
        {
            isFloat = true;
            sb.Append(_source[_position]);
            _position++;
            _column++;

            // Check for optional sign
            if (_position < _source.Length && (_source[_position] == '+' || _source[_position] == '-'))
            {
                sb.Append(_source[_position]);
                _position++;
                _column++;
            }

            // Read exponent digits
            if (_position >= _source.Length || !char.IsDigit(_source[_position]))
                throw new LexerError("Invalid scientific notation: expected exponent digits", startLine, startColumn);

            while (_position < _source.Length && (char.IsDigit(_source[_position]) || _source[_position] == '_'))
            {
                if (_source[_position] != '_')
                    sb.Append(_source[_position]);
                _position++;
                _column++;
            }
        }

        // Check for suffix (f, L, ul, etc.)
        if (_position < _source.Length && char.IsLetter(_source[_position]))
        {
            var suffix = _source[_position].ToString();
            _position++;
            _column++;

            // Check for two-letter suffix (ul, UL)
            if (_position < _source.Length && char.IsLetter(_source[_position]))
            {
                suffix += _source[_position];
                _position++;
                _column++;
            }

            // Validate suffix
            var validSuffixes = new[] { "f", "F", "d", "D", "m", "M", "l", "L", "u", "U", "ul", "UL", "uL", "Ul" };
            if (!validSuffixes.Contains(suffix))
                throw new LexerError($"Invalid numeric suffix: {suffix}", startLine, startColumn);

            sb.Append(suffix);

            // Suffix on integer makes it that type, but we still parse as Float token if it has suffix
            if (suffix.ToLower() == "f" || suffix.ToLower() == "d" || suffix.ToLower() == "m")
                isFloat = true;
        }

        var tokenType = isFloat ? TokenType.Float : TokenType.Integer;
        return new Token(tokenType, sb.ToString(), startLine, startColumn);
    }

    private Token ReadHexNumber(int startLine, int startColumn)
    {
        var sb = new StringBuilder();
        sb.Append("0x");
        _position += 2;
        _column += 2;

        var hasDigits = false;
        while (_position < _source.Length)
        {
            var c = _source[_position];
            if (char.IsDigit(c) || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'))
            {
                sb.Append(c);
                hasDigits = true;
                _position++;
                _column++;
            }
            else if (c == '_')
            {
                _position++;
                _column++;
            }
            else
            {
                break;
            }
        }

        if (!hasDigits)
            throw new LexerError("Invalid hexadecimal literal: no digits after 0x", startLine, startColumn);

        return new Token(TokenType.Integer, sb.ToString(), startLine, startColumn);
    }

    private Token ReadBinaryNumber(int startLine, int startColumn)
    {
        var sb = new StringBuilder();
        sb.Append("0b");
        _position += 2;
        _column += 2;

        var hasDigits = false;
        while (_position < _source.Length)
        {
            var c = _source[_position];
            if (c == '0' || c == '1')
            {
                sb.Append(c);
                hasDigits = true;
                _position++;
                _column++;
            }
            else if (c == '_')
            {
                _position++;
                _column++;
            }
            else
            {
                break;
            }
        }

        if (!hasDigits)
            throw new LexerError("Invalid binary literal: no digits after 0b", startLine, startColumn);

        return new Token(TokenType.Integer, sb.ToString(), startLine, startColumn);
    }

    private Token ReadOctalNumber(int startLine, int startColumn)
    {
        var sb = new StringBuilder();
        sb.Append("0o");
        _position += 2;
        _column += 2;

        var hasDigits = false;
        while (_position < _source.Length)
        {
            var c = _source[_position];
            if (c >= '0' && c <= '7')
            {
                sb.Append(c);
                hasDigits = true;
                _position++;
                _column++;
            }
            else if (c == '_')
            {
                _position++;
                _column++;
            }
            else
            {
                break;
            }
        }

        if (!hasDigits)
            throw new LexerError("Invalid octal literal: no digits after 0o", startLine, startColumn);

        return new Token(TokenType.Integer, sb.ToString(), startLine, startColumn);
    }

    private Token ReadIdentifierOrKeyword()
    {
        var startLine = _line;
        var startColumn = _column;
        var sb = new StringBuilder();

        while (_position < _source.Length &&
               (char.IsLetterOrDigit(_source[_position]) || _source[_position] == '_'))
        {
            sb.Append(_source[_position]);
            _position++;
            _column++;
        }

        var value = sb.ToString();

        // Check if it's a keyword
        if (Keywords.TryGetValue(value, out var tokenType))
            return new Token(tokenType, value, startLine, startColumn);

        return new Token(TokenType.Identifier, value, startLine, startColumn);
    }

    private Token ReadLiteralName()
    {
        var startLine = _line;
        var startColumn = _column;
        var sb = new StringBuilder();

        // Skip opening backtick
        _position++;
        _column++;

        while (_position < _source.Length)
        {
            var c = _source[_position];

            if (c == '`')
            {
                // Found closing backtick
                _position++;
                _column++;
                return new Token(TokenType.Identifier, sb.ToString(), startLine, startColumn);
            }

            if (c == '\n' || c == '\r')
            {
                throw new LexerError("Unterminated literal name (backtick-delimited identifier)", _line, _column);
            }

            sb.Append(c);
            _position++;
            _column++;
        }

        throw new LexerError("Unterminated literal name (backtick-delimited identifier)", _line, _column);
    }

    private Token ReadOperatorOrDelimiter()
    {
        var startLine = _line;
        var startColumn = _column;
        var c = _source[_position];

        // Two-character and three-character operators
        if (_position + 2 < _source.Length)
        {
            var threeChar = _source.Substring(_position, 3);
            switch (threeChar)
            {
                case "...":
                    _position += 3;
                    _column += 3;
                    return new Token(TokenType.Ellipsis, threeChar, startLine, startColumn);
                case "<<=":
                    _position += 3;
                    _column += 3;
                    return new Token(TokenType.LeftShiftAssign, threeChar, startLine, startColumn);
                case ">>=":
                    _position += 3;
                    _column += 3;
                    return new Token(TokenType.RightShiftAssign, threeChar, startLine, startColumn);
                case "**=":
                    _position += 3;
                    _column += 3;
                    return new Token(TokenType.DoubleStarAssign, threeChar, startLine, startColumn);
                case "//=":
                    _position += 3;
                    _column += 3;
                    return new Token(TokenType.DoubleSlashAssign, threeChar, startLine, startColumn);
            }
        }

        if (_position + 1 < _source.Length)
        {
            var twoChar = _source.Substring(_position, 2);
            switch (twoChar)
            {
                case "==":
                    _position += 2;
                    _column += 2;
                    return new Token(TokenType.Equal, twoChar, startLine, startColumn);
                case "!=":
                    _position += 2;
                    _column += 2;
                    return new Token(TokenType.NotEqual, twoChar, startLine, startColumn);
                case "<=":
                    _position += 2;
                    _column += 2;
                    return new Token(TokenType.LessEqual, twoChar, startLine, startColumn);
                case ">=":
                    _position += 2;
                    _column += 2;
                    return new Token(TokenType.GreaterEqual, twoChar, startLine, startColumn);
                case "<<":
                    _position += 2;
                    _column += 2;
                    return new Token(TokenType.LeftShift, twoChar, startLine, startColumn);
                case ">>":
                    _position += 2;
                    _column += 2;
                    return new Token(TokenType.RightShift, twoChar, startLine, startColumn);
                case "**":
                    _position += 2;
                    _column += 2;
                    return new Token(TokenType.DoubleStar, twoChar, startLine, startColumn);
                case "//":
                    _position += 2;
                    _column += 2;
                    return new Token(TokenType.DoubleSlash, twoChar, startLine, startColumn);
                case "->":
                    _position += 2;
                    _column += 2;
                    return new Token(TokenType.Arrow, twoChar, startLine, startColumn);
                case "?.":
                    _position += 2;
                    _column += 2;
                    return new Token(TokenType.NullConditional, twoChar, startLine, startColumn);
                case "??":
                    _position += 2;
                    _column += 2;
                    return new Token(TokenType.NullCoalesce, twoChar, startLine, startColumn);
                case "+=":
                    _position += 2;
                    _column += 2;
                    return new Token(TokenType.PlusAssign, twoChar, startLine, startColumn);
                case "-=":
                    _position += 2;
                    _column += 2;
                    return new Token(TokenType.MinusAssign, twoChar, startLine, startColumn);
                case "*=":
                    _position += 2;
                    _column += 2;
                    return new Token(TokenType.StarAssign, twoChar, startLine, startColumn);
                case "/=":
                    _position += 2;
                    _column += 2;
                    return new Token(TokenType.SlashAssign, twoChar, startLine, startColumn);
                case "%=":
                    _position += 2;
                    _column += 2;
                    return new Token(TokenType.PercentAssign, twoChar, startLine, startColumn);
                case "&=":
                    _position += 2;
                    _column += 2;
                    return new Token(TokenType.AmpersandAssign, twoChar, startLine, startColumn);
                case "|=":
                    _position += 2;
                    _column += 2;
                    return new Token(TokenType.PipeAssign, twoChar, startLine, startColumn);
                case "^=":
                    _position += 2;
                    _column += 2;
                    return new Token(TokenType.CaretAssign, twoChar, startLine, startColumn);
            }
        }

        // Single-character operators and delimiters
        _position++;
        _column++;

        var token = c switch
        {
            '+' => new Token(TokenType.Plus, "+", startLine, startColumn),
            '-' => new Token(TokenType.Minus, "-", startLine, startColumn),
            '*' => new Token(TokenType.Star, "*", startLine, startColumn),
            '/' => new Token(TokenType.Slash, "/", startLine, startColumn),
            '%' => new Token(TokenType.Percent, "%", startLine, startColumn),
            '&' => new Token(TokenType.Ampersand, "&", startLine, startColumn),
            '|' => new Token(TokenType.Pipe, "|", startLine, startColumn),
            '^' => new Token(TokenType.Caret, "^", startLine, startColumn),
            '~' => new Token(TokenType.Tilde, "~", startLine, startColumn),
            '=' => new Token(TokenType.Assign, "=", startLine, startColumn),
            '<' => new Token(TokenType.Less, "<", startLine, startColumn),
            '>' => new Token(TokenType.Greater, ">", startLine, startColumn),
            '?' => new Token(TokenType.Question, "?", startLine, startColumn),
            '(' => new Token(TokenType.LeftParen, "(", startLine, startColumn),
            ')' => new Token(TokenType.RightParen, ")", startLine, startColumn),
            '[' => new Token(TokenType.LeftBracket, "[", startLine, startColumn),
            ']' => new Token(TokenType.RightBracket, "]", startLine, startColumn),
            '{' => new Token(TokenType.LeftBrace, "{", startLine, startColumn),
            '}' => new Token(TokenType.RightBrace, "}", startLine, startColumn),
            ',' => new Token(TokenType.Comma, ",", startLine, startColumn),
            ':' => new Token(TokenType.Colon, ":", startLine, startColumn),
            ';' => new Token(TokenType.Semicolon, ";", startLine, startColumn),
            '.' => new Token(TokenType.Dot, ".", startLine, startColumn),
            '@' => new Token(TokenType.At, "@", startLine, startColumn),
            '\\' => new Token(TokenType.Backslash, "\\", startLine, startColumn),
            _ => throw new LexerError($"Unexpected character: '{c}'", startLine, startColumn)
        };

        // Track bracket depth for implicit line continuation
        if (c == '(' || c == '[' || c == '{')
            _bracketDepth++;
        else if (c == ')' || c == ']' || c == '}')
            _bracketDepth--;

        return token;
    }
}
