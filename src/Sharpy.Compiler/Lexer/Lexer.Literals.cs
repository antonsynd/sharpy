using System.Text;
using Sharpy.Compiler.Diagnostics;

namespace Sharpy.Compiler.Lexer;

public partial class Lexer
{
    // String/raw-string tokens set SourceLength so Token.Length reflects the full source
    // extent (including quotes and r prefix) for accurate diagnostic spans and LSP features.
    // FString tokens (Lexer.FStrings.cs) don't need this because their Value already
    // equals the source text for each segment.
    private Token ReadString()
    {
        var startLine = _line;
        var startColumn = _column;
        var startPosition = _position;
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
            return ReadTripleQuotedString(quote, startLine, startColumn, startPosition);
        }

        // Single-line string
        while (_position < _source.Length)
        {
            var c = _source[_position];

            if (c == quote)
            {
                _position++;
                _column++;
                var sourceLength = _position - startPosition;
                return CreateToken(TokenType.String, sb.ToString(), startLine, startColumn, startPosition, sourceLength);
            }

            if (c == '\\')
            {
                _position++;
                _column++;
                if (_position >= _source.Length)
                    throw ReportError("Unterminated string literal", _line, _column, DiagnosticCodes.Lexer.UnterminatedString);

                sb.Append(ProcessEscapeSequence());
            }
            else if (c == '\n' || c == '\r')
            {
                throw ReportError("Unterminated string literal", _line, _column, DiagnosticCodes.Lexer.UnterminatedString);
            }
            else
            {
                sb.Append(c);
                _position++;
                _column++;
            }
        }

        throw ReportError("Unterminated string literal", _line, _column, DiagnosticCodes.Lexer.UnterminatedString);
    }

    private Token ReadTripleQuotedString(char quote, int startLine, int startColumn, int startPosition)
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
                var sourceLength = _position - startPosition;
                return CreateToken(TokenType.String, sb.ToString(), startLine, startColumn, startPosition, sourceLength);
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
                    throw ReportError("Unterminated string literal", _line, _column, DiagnosticCodes.Lexer.UnterminatedString);

                sb.Append(ProcessEscapeSequence());
            }
            else
            {
                sb.Append(c);
                _position++;
                _column++;
            }
        }

        throw ReportError("Unterminated triple-quoted string", _line, _column, DiagnosticCodes.Lexer.UnterminatedString);
    }


    private Token ReadRawString()
    {
        var startLine = _line;
        var startColumn = _column;
        var startPosition = _position;

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
                    var sourceLength = _position - startPosition;
                    return CreateToken(TokenType.RawString, sb.ToString(), startLine, startColumn, startPosition, sourceLength);
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
            throw ReportError("Unterminated raw string", _line, _column, DiagnosticCodes.Lexer.UnterminatedRawString);
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
                var sourceLength = _position - startPosition;
                return CreateToken(TokenType.RawString, sb2.ToString(), startLine, startColumn, startPosition, sourceLength);
            }

            if (c == '\n' || c == '\r')
                throw ReportError("Unterminated raw string", _line, _column, DiagnosticCodes.Lexer.UnterminatedRawString);

            sb2.Append(c);
            _position++;
            _column++;
        }

        throw ReportError("Unterminated raw string", _line, _column, DiagnosticCodes.Lexer.UnterminatedRawString);
    }

    private char ProcessEscapeSequence()
    {
        var escaped = _source[_position];
        _position++;
        _column++;

        switch (escaped)
        {
            case 'n':
                return '\n';
            case 'r':
                return '\r';
            case 't':
                return '\t';
            case 'b':
                return '\b';
            case 'f':
                return '\f';
            case '0':
                return '\0';
            case '\\':
                return '\\';
            case '\'':
                return '\'';
            case '"':
                return '"';
            case '/':
                return '/';
            case 'a':
                return '\a';
            case 'v':
                return '\v';

            // Hex escape: \xhh (2 hex digits)
            case 'x':
                {
                    if (_position + 1 >= _source.Length)
                        throw ReportError("Invalid hex escape sequence", _line, _column, DiagnosticCodes.Lexer.InvalidHexEscape);

                    var hex = _source.Substring(_position, 2);
                    if (!int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var value))
                        throw ReportError($"Invalid hex escape sequence: \\x{hex}", _line, _column, DiagnosticCodes.Lexer.InvalidHexEscape);

                    _position += 2;
                    _column += 2;
                    return (char)value;
                }

            // Unicode escape: \uhhhh (4 hex digits) or \Uhhhhhhhh (8 hex digits)
            case 'u':
                {
                    if (_position + 3 >= _source.Length)
                        throw ReportError("Invalid unicode escape sequence", _line, _column, DiagnosticCodes.Lexer.InvalidUnicodeEscape);

                    var hex = _source.Substring(_position, 4);
                    if (!int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var value))
                        throw ReportError($"Invalid unicode escape sequence: \\u{hex}", _line, _column, DiagnosticCodes.Lexer.InvalidUnicodeEscape);

                    _position += 4;
                    _column += 4;
                    return (char)value;
                }

            case 'U':
                {
                    if (_position + 7 >= _source.Length)
                        throw ReportError("Invalid unicode escape sequence", _line, _column, DiagnosticCodes.Lexer.InvalidUnicodeEscape);

                    var hex = _source.Substring(_position, 8);
                    if (!int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var value))
                        throw ReportError($"Invalid unicode escape sequence: \\U{hex}", _line, _column, DiagnosticCodes.Lexer.InvalidUnicodeEscape);

                    _position += 8;
                    _column += 8;
                    return (char)value;
                }

            // Octal escape: \ooo (1-3 octal digits)
            case char c when c >= '0' && c <= '7':
                {
                    var octal = c.ToString();

                    // Read up to 2 more octal digits (we already have the first one)
                    for (int i = 0; i < 2 && _position < _source.Length; i++)
                    {
                        var nextChar = _source[_position];
                        if (nextChar < '0' || nextChar > '7')
                            break;

                        octal += nextChar;
                        _position++;
                        _column++;
                    }

                    var value = Convert.ToInt32(octal, 8);
                    if (value > 255)
                        throw ReportError($"Octal escape value {octal} exceeds maximum (377)", _line, _column, DiagnosticCodes.Lexer.OctalEscapeOverflow);

                    return (char)value;
                }

            default:
                throw ReportError($"Invalid escape sequence: \\{escaped}", _line, _column, DiagnosticCodes.Lexer.InvalidEscapeSequence);
        }
    }

    private Token ReadNumber()
    {
        var startLine = _line;
        var startColumn = _column;
        var startPosition = _position;
        var sb = new StringBuilder();
        var isFloat = false;

        // Leading-decimal float literal (.5, .123)
        if (_source[_position] == '.')
        {
            isFloat = true;
            sb.Append("0.");
            _position++;
            _column++;

            // Read fractional part
            char? lastCharFrac = '.';
            while (_position < _source.Length && (char.IsDigit(_source[_position]) || _source[_position] == '_'))
            {
                var c = _source[_position];

                if (c == '_' && lastCharFrac == '_')
                    throw ReportError("Invalid number: consecutive underscores not allowed", startLine, startColumn, DiagnosticCodes.Lexer.InvalidNumber);

                if (c != '_')
                    sb.Append(c);

                lastCharFrac = c;
                _position++;
                _column++;
            }

            if (lastCharFrac == '_')
                throw ReportError("Invalid number: cannot end with underscore", startLine, startColumn, DiagnosticCodes.Lexer.InvalidNumber);

            // Check for scientific notation
            if (_position < _source.Length && (_source[_position] == 'e' || _source[_position] == 'E'))
            {
                sb.Append(_source[_position]);
                _position++;
                _column++;

                if (_position < _source.Length && (_source[_position] == '+' || _source[_position] == '-'))
                {
                    sb.Append(_source[_position]);
                    _position++;
                    _column++;
                }

                if (_position >= _source.Length || !char.IsDigit(_source[_position]))
                    throw ReportError("Invalid scientific notation: expected exponent digits", startLine, startColumn, DiagnosticCodes.Lexer.InvalidNumber);

                char? lastCharExp = null;
                while (_position < _source.Length && (char.IsDigit(_source[_position]) || _source[_position] == '_'))
                {
                    var c = _source[_position];

                    if (c == '_' && lastCharExp == '_')
                        throw ReportError("Invalid number: consecutive underscores not allowed", startLine, startColumn, DiagnosticCodes.Lexer.InvalidNumber);

                    if (c != '_')
                        sb.Append(c);

                    lastCharExp = c;
                    _position++;
                    _column++;
                }

                if (lastCharExp == '_')
                    throw ReportError("Invalid number: cannot end with underscore", startLine, startColumn, DiagnosticCodes.Lexer.InvalidNumber);
            }

            return CreateToken(TokenType.Float, sb.ToString(), startLine, startColumn, startPosition);
        }

        // Check for hex, binary, or octal prefix
        if (_source[_position] == '0' && _position + 1 < _source.Length)
        {
            var nextChar = _source[_position + 1];
            if (nextChar == 'x' || nextChar == 'X')
            {
                // Hexadecimal
                return ReadHexNumber(startLine, startColumn, startPosition);
            }
            else if (nextChar == 'b' || nextChar == 'B')
            {
                // Binary
                return ReadBinaryNumber(startLine, startColumn, startPosition);
            }
            else if (nextChar == 'o' || nextChar == 'O')
            {
                // Octal
                return ReadOctalNumber(startLine, startColumn, startPosition);
            }
        }

        // Read integer part
        char? lastChar = null;
        while (_position < _source.Length && (char.IsDigit(_source[_position]) || _source[_position] == '_'))
        {
            var c = _source[_position];

            // Check for consecutive underscores
            if (c == '_' && lastChar == '_')
                throw ReportError("Invalid number: consecutive underscores not allowed", startLine, startColumn, DiagnosticCodes.Lexer.InvalidNumber);

            if (c != '_')
                sb.Append(c);

            lastChar = c;
            _position++;
            _column++;
        }

        // Check if number ends with underscore
        if (lastChar == '_')
            throw ReportError("Invalid number: cannot end with underscore", startLine, startColumn, DiagnosticCodes.Lexer.InvalidNumber);

        // Check for decimal point
        if (_position < _source.Length && _source[_position] == '.' &&
            _position + 1 < _source.Length && char.IsDigit(_source[_position + 1]))
        {
            isFloat = true;
            sb.Append('.');
            _position++;
            _column++;

            // Read fractional part
            lastChar = '.';  // Reset for fractional part
            while (_position < _source.Length && (char.IsDigit(_source[_position]) || _source[_position] == '_'))
            {
                var c = _source[_position];

                // Check for consecutive underscores
                if (c == '_' && lastChar == '_')
                    throw ReportError("Invalid number: consecutive underscores not allowed", startLine, startColumn, DiagnosticCodes.Lexer.InvalidNumber);

                if (c != '_')
                    sb.Append(c);

                lastChar = c;
                _position++;
                _column++;
            }

            // Check if fractional part ends with underscore
            if (lastChar == '_')
                throw ReportError("Invalid number: cannot end with underscore", startLine, startColumn, DiagnosticCodes.Lexer.InvalidNumber);
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
                throw ReportError("Invalid scientific notation: expected exponent digits", startLine, startColumn, DiagnosticCodes.Lexer.InvalidNumber);

            lastChar = null;
            while (_position < _source.Length && (char.IsDigit(_source[_position]) || _source[_position] == '_'))
            {
                var c = _source[_position];

                // Check for consecutive underscores
                if (c == '_' && lastChar == '_')
                    throw ReportError("Invalid number: consecutive underscores not allowed", startLine, startColumn, DiagnosticCodes.Lexer.InvalidNumber);

                if (c != '_')
                    sb.Append(c);

                lastChar = c;
                _position++;
                _column++;
            }

            // Check if exponent ends with underscore
            if (lastChar == '_')
                throw ReportError("Invalid number: cannot end with underscore", startLine, startColumn, DiagnosticCodes.Lexer.InvalidNumber);
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
                throw ReportError($"Invalid numeric suffix: {suffix}", startLine, startColumn, DiagnosticCodes.Lexer.InvalidNumericSuffix);

            sb.Append(suffix);

            // Suffix on integer makes it that type, but we still parse as Float token if it has suffix
            if (suffix.ToLower() == "f" || suffix.ToLower() == "d" || suffix.ToLower() == "m")
                isFloat = true;
        }

        var tokenType = isFloat ? TokenType.Float : TokenType.Integer;
        return CreateToken(tokenType, sb.ToString(), startLine, startColumn, startPosition);
    }

    private Token ReadHexNumber(int startLine, int startColumn, int startPosition)
    {
        var sb = new StringBuilder();
        sb.Append("0x");
        _position += 2;
        _column += 2;

        var hasDigits = false;
        char? lastChar = null;

        while (_position < _source.Length)
        {
            var c = _source[_position];
            if (char.IsDigit(c) || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'))
            {
                sb.Append(c);
                hasDigits = true;
                lastChar = c;
                _position++;
                _column++;
            }
            else if (c == '_')
            {
                // Check for consecutive underscores
                if (lastChar == '_')
                    throw ReportError("Invalid number: consecutive underscores not allowed", startLine, startColumn, DiagnosticCodes.Lexer.InvalidHexLiteral);

                lastChar = '_';
                _position++;
                _column++;
            }
            else
            {
                break;
            }
        }

        if (!hasDigits)
            throw ReportError("Invalid hexadecimal literal: no digits after 0x", startLine, startColumn, DiagnosticCodes.Lexer.InvalidHexLiteral);

        // Check if number ends with underscore
        if (lastChar == '_')
            throw ReportError("Invalid number: cannot end with underscore", startLine, startColumn, DiagnosticCodes.Lexer.InvalidHexLiteral);

        return CreateToken(TokenType.Integer, sb.ToString(), startLine, startColumn, startPosition);
    }

    private Token ReadBinaryNumber(int startLine, int startColumn, int startPosition)
    {
        var sb = new StringBuilder();
        sb.Append("0b");
        _position += 2;
        _column += 2;

        var hasDigits = false;
        char? lastChar = null;

        while (_position < _source.Length)
        {
            var c = _source[_position];
            if (c == '0' || c == '1')
            {
                sb.Append(c);
                hasDigits = true;
                lastChar = c;
                _position++;
                _column++;
            }
            else if (c == '_')
            {
                // Check for consecutive underscores
                if (lastChar == '_')
                    throw ReportError("Invalid number: consecutive underscores not allowed", startLine, startColumn, DiagnosticCodes.Lexer.InvalidBinaryLiteral);

                lastChar = '_';
                _position++;
                _column++;
            }
            else if (char.IsDigit(c))
            {
                // Invalid binary digit (2-9)
                throw ReportError($"Invalid binary digit: '{c}' (only 0 and 1 allowed)", startLine, startColumn, DiagnosticCodes.Lexer.InvalidBinaryLiteral);
            }
            else
            {
                break;
            }
        }

        if (!hasDigits)
            throw ReportError("Invalid binary literal: no digits after 0b", startLine, startColumn, DiagnosticCodes.Lexer.InvalidBinaryLiteral);

        // Check if number ends with underscore
        if (lastChar == '_')
            throw ReportError("Invalid number: cannot end with underscore", startLine, startColumn, DiagnosticCodes.Lexer.InvalidBinaryLiteral);

        return CreateToken(TokenType.Integer, sb.ToString(), startLine, startColumn, startPosition);
    }

    private Token ReadOctalNumber(int startLine, int startColumn, int startPosition)
    {
        var sb = new StringBuilder();
        sb.Append("0o");
        _position += 2;
        _column += 2;

        var hasDigits = false;
        char? lastChar = null;

        while (_position < _source.Length)
        {
            var c = _source[_position];
            if (c >= '0' && c <= '7')
            {
                sb.Append(c);
                hasDigits = true;
                lastChar = c;
                _position++;
                _column++;
            }
            else if (c == '_')
            {
                // Check for consecutive underscores
                if (lastChar == '_')
                    throw ReportError("Invalid number: consecutive underscores not allowed", startLine, startColumn, DiagnosticCodes.Lexer.InvalidOctalLiteral);

                lastChar = '_';
                _position++;
                _column++;
            }
            else if (char.IsDigit(c))
            {
                // Invalid octal digit (8-9)
                throw ReportError($"Invalid octal digit: '{c}' (only 0-7 allowed)", startLine, startColumn, DiagnosticCodes.Lexer.InvalidOctalLiteral);
            }
            else
            {
                break;
            }
        }

        if (!hasDigits)
            throw ReportError("Invalid octal literal: no digits after 0o", startLine, startColumn, DiagnosticCodes.Lexer.InvalidOctalLiteral);

        // Check if number ends with underscore
        if (lastChar == '_')
            throw ReportError("Invalid number: cannot end with underscore", startLine, startColumn, DiagnosticCodes.Lexer.InvalidOctalLiteral);

        return CreateToken(TokenType.Integer, sb.ToString(), startLine, startColumn, startPosition);
    }
}
