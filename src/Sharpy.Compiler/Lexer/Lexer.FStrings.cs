using System.Text;
using Sharpy.Compiler.Diagnostics;

namespace Sharpy.Compiler.Lexer;

public partial class Lexer
{
    private class FStringContext
    {
        public char QuoteChar { get; set; }
        public bool IsTriple { get; set; }
        public int BraceDepth { get; set; }
        public bool InFormatSpec { get; set; }  // Tracks if we're processing format specification after ':'
        public int DedentAmount { get; set; }   // PEP 822: number of whitespace chars to strip after each \n (0 = no dedent)
    }

    /// <summary>
    /// Read the start of an f-string and enter f-string mode
    /// </summary>
    private Token ReadFStringStart()
    {
        var startLine = _line;
        var startColumn = _column;
        var startPosition = _position;

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
        }

        // Push f-string context onto stack
        _fstringStack.Push(new FStringContext
        {
            QuoteChar = quote,
            IsTriple = isTriple,
            BraceDepth = 0
        });

        return CreateToken(TokenType.FStringStart, isTriple ? $"f{quote}{quote}{quote}" : $"f{quote}", startLine, startColumn, startPosition);
    }

    /// <summary>
    /// Read the start of a dedented f-string (df"..."), per PEP 822.
    /// For triple-quoted df-strings, determines the dedent amount from the closing """ line
    /// via a pre-scan, and stores it on the f-string context so NextFStringToken strips
    /// matching whitespace from each subsequent line's FStringText.
    /// For single-quoted df"...", this is equivalent to a regular f-string (no dedent).
    /// </summary>
    private Token ReadDedentedFStringStart()
    {
        var startLine = _line;
        var startColumn = _column;
        var startPosition = _position;

        // Skip 'd'
        _position++;
        _column++;
        // Skip 'f'
        _position++;
        _column++;

        var quote = _source[_position];
        _position++;
        _column++;

        // Check for triple-quoted df-string
        var isTriple = _position + 1 < _source.Length &&
                       _source[_position] == quote &&
                       _source[_position + 1] == quote;

        int dedentAmount = 0;
        if (isTriple)
        {
            _position += 2;
            _column += 2;

            dedentAmount = PrescanDedentedTripleFString(quote);

            // Skip a leading newline immediately after the opening """ (and the
            // dedent whitespace that follows on the next line). This matches the
            // plain d-string behaviour where the first empty/whitespace-only line
            // is removed.
            if (dedentAmount > 0 && _position < _source.Length)
            {
                if (_source[_position] == '\r' && _position + 1 < _source.Length && _source[_position + 1] == '\n')
                {
                    _position += 2;
                    _line++;
                    _column = 1;
                    SkipDedentWhitespace(dedentAmount);
                }
                else if (_source[_position] == '\n')
                {
                    _position++;
                    _line++;
                    _column = 1;
                    SkipDedentWhitespace(dedentAmount);
                }
            }
        }

        // Push f-string context onto stack
        _fstringStack.Push(new FStringContext
        {
            QuoteChar = quote,
            IsTriple = isTriple,
            BraceDepth = 0,
            DedentAmount = dedentAmount
        });

        return CreateToken(TokenType.FStringStart, isTriple ? $"df{quote}{quote}{quote}" : $"df{quote}", startLine, startColumn, startPosition);
    }

    /// <summary>
    /// Scan forward from the current position to find the closing """ of a triple-quoted
    /// f-string, and return the number of whitespace characters on its line (the dedent amount).
    /// Tracks brace depth so that """ sequences inside interpolated expressions are ignored.
    /// Returns 0 if no valid whitespace-only line before the close could be determined.
    /// Does not mutate lexer state.
    /// </summary>
    private int PrescanDedentedTripleFString(char quote)
    {
        int i = _position;
        int lastLineStart = i;
        int braceDepth = 0;
        while (i < _source.Length)
        {
            char c = _source[i];

            // Skip backslash escapes (only meaningful outside expressions, but harmless inside)
            if (c == '\\' && i + 1 < _source.Length)
            {
                i += 2;
                continue;
            }

            if (braceDepth == 0)
            {
                // Check for closing triple quote
                if (c == quote && i + 2 < _source.Length &&
                    _source[i + 1] == quote && _source[i + 2] == quote)
                {
                    int count = 0;
                    for (int j = lastLineStart; j < i; j++)
                    {
                        if (_source[j] == ' ' || _source[j] == '\t')
                            count++;
                        else
                            return 0;
                    }
                    return count;
                }

                // Handle escaped braces {{ and }}
                if (c == '{' && i + 1 < _source.Length && _source[i + 1] == '{')
                {
                    i += 2;
                    continue;
                }
                if (c == '}' && i + 1 < _source.Length && _source[i + 1] == '}')
                {
                    i += 2;
                    continue;
                }

                if (c == '{')
                {
                    braceDepth++;
                    i++;
                    continue;
                }
            }
            else
            {
                if (c == '{')
                {
                    braceDepth++;
                    i++;
                    continue;
                }
                if (c == '}')
                {
                    braceDepth--;
                    i++;
                    continue;
                }
            }

            if (c == '\n')
                lastLineStart = i + 1;

            i++;
        }
        return 0;
    }

    /// <summary>
    /// Returns true if the current position is at a newline that is immediately
    /// followed by DedentAmount whitespace chars (or fewer, if the close is less indented)
    /// and then the closing triple quote. Used to drop the pre-close newline in df-strings.
    /// Does not mutate lexer state.
    /// </summary>
    private bool IsPreCloseNewline(FStringContext context)
    {
        int i = _position;
        // Advance past the newline (\r\n or \n)
        if (i < _source.Length && _source[i] == '\r')
            i++;
        if (i < _source.Length && _source[i] == '\n')
            i++;

        // Skip up to DedentAmount whitespace chars
        int remaining = context.DedentAmount;
        while (remaining > 0 && i < _source.Length)
        {
            var c = _source[i];
            if (c == ' ' || c == '\t')
            {
                i++;
                remaining--;
            }
            else
            {
                break;
            }
        }

        // Check for closing triple quote
        return i + 2 < _source.Length &&
               _source[i] == context.QuoteChar &&
               _source[i + 1] == context.QuoteChar &&
               _source[i + 2] == context.QuoteChar;
    }

    /// <summary>
    /// Skip up to DedentAmount leading whitespace characters from the current position,
    /// advancing _position and _column. Stops if a non-whitespace char is encountered.
    /// </summary>
    private void SkipDedentWhitespace(int dedentAmount)
    {
        int remaining = dedentAmount;
        while (remaining > 0 && _position < _source.Length)
        {
            var c = _source[_position];
            if (c == ' ' || c == '\t')
            {
                _position++;
                _column++;
                remaining--;
            }
            else
            {
                break;
            }
        }
    }

    /// <summary>
    /// Get the next token while inside an f-string
    /// </summary>
    private Token NextFStringToken()
    {
        if (_position >= _source.Length)
        {
            throw ReportError("Unterminated f-string", _line, _column, DiagnosticCodes.Lexer.UnterminatedFString);
        }

        var context = _fstringStack.Peek();
        var startLine = _line;
        var startColumn = _column;
        var startPosition = _position;

        // If we're inside an expression (brace depth > 0), tokenize normally
        if (context.BraceDepth > 0)
        {
            var current = _source[_position];

            // Handle closing brace
            if (current == '}')
            {
                context.BraceDepth--;
                _position++;
                _column++;

                if (context.BraceDepth == 0)
                {
                    // End of expression - reset format spec flag
                    context.InFormatSpec = false;
                    return CreateToken(TokenType.FStringExprEnd, "}", startLine, startColumn, startPosition);
                }
                else
                {
                    // Nested closing brace within expression
                    return CreateToken(TokenType.RightBrace, "}", startLine, startColumn, startPosition);
                }
            }

            // Handle opening brace (nested within expression)
            if (current == '{')
            {
                context.BraceDepth++;
                _position++;
                _column++;
                return CreateToken(TokenType.LeftBrace, "{", startLine, startColumn, startPosition);
            }

            // For everything else, tokenize normally (but skip indentation handling)
            SkipWhitespace();

            if (_position >= _source.Length)
                throw ReportError("Unterminated f-string expression", _line, _column, DiagnosticCodes.Lexer.UnterminatedFStringExpression);

            current = _source[_position];
            startLine = _line;
            startColumn = _column;
            startPosition = _position;

            // Check for braces again after skipping whitespace
            if (current == '}')
            {
                context.BraceDepth--;
                _position++;
                _column++;

                // End of expression if brace depth is zero, otherwise nested closing brace within expression
                if (context.BraceDepth == 0)
                {
                    context.InFormatSpec = false;  // Reset format spec flag
                    return CreateToken(TokenType.FStringExprEnd, "}", startLine, startColumn, startPosition);
                }
                else
                {
                    return CreateToken(TokenType.RightBrace, "}", startLine, startColumn, startPosition);
                }
            }

            if (current == '{')
            {
                context.BraceDepth++;
                _position++;
                _column++;
                return CreateToken(TokenType.LeftBrace, "{", startLine, startColumn, startPosition);
            }

            // Check for format specification start (: at BraceDepth == 1)
            if (current == ':' && context.BraceDepth == 1 && !context.InFormatSpec)
            {
                // Consume the colon
                _position++;
                _column++;

                // Mark that we're in format spec mode
                context.InFormatSpec = true;

                // Now read the format spec until we hit the closing }
                var formatSpecBuilder = new StringBuilder();
                var formatSpecStartLine = _line;
                var formatSpecStartColumn = _column;
                var formatSpecStartPosition = _position;
                var nestedBraceDepth = 0;  // Track nested braces in format spec

                while (_position < _source.Length)
                {
                    var fsc = _source[_position];

                    // Check for nested opening brace in format spec
                    if (fsc == '{')
                    {
                        nestedBraceDepth++;
                        formatSpecBuilder.Append(fsc);
                        _position++;
                        _column++;
                    }
                    // Check for closing brace
                    else if (fsc == '}')
                    {
                        if (nestedBraceDepth > 0)
                        {
                            // This is a nested closing brace, include it in format spec
                            nestedBraceDepth--;
                            formatSpecBuilder.Append(fsc);
                            _position++;
                            _column++;
                        }
                        else
                        {
                            // This is the end of the expression, emit the format spec token
                            // Don't consume the }, it will be handled on next call
                            context.InFormatSpec = false;
                            return CreateToken(TokenType.FStringFormatSpec, formatSpecBuilder.ToString(), formatSpecStartLine, formatSpecStartColumn, formatSpecStartPosition);
                        }
                    }
                    // Regular character in format spec
                    else
                    {
                        formatSpecBuilder.Append(fsc);
                        _position++;
                        _column++;

                        // Handle newlines
                        if (fsc == '\n')
                        {
                            _line++;
                            _column = 1;
                        }
                    }
                }

                throw ReportError("Unterminated format specification in f-string", _line, _column, DiagnosticCodes.Lexer.UnterminatedFormatSpec);
            }

            // Nested f-string start (e.g., f"outer {f'inner {x}'}")
            if (current == 'f' && (_position + 1 < _source.Length) &&
                (_source[_position + 1] == '"' || _source[_position + 1] == '\''))
                return ReadFStringStart();

            // String literals inside expressions
            if (current == '"' || current == '\'')
                return ReadString();

            // Numbers
            if (char.IsDigit(current))
                return ReadNumber();

            // Identifiers and keywords
            if (char.IsLetter(current) || current == '_')
                return ReadIdentifierOrKeyword();

            // Operators and delimiters
            return ReadOperatorOrDelimiter();
        }

        // We're reading literal text or looking for expression start or string end
        var sb = new StringBuilder();

        while (_position < _source.Length)
        {
            var c = _source[_position];

            // Check for end of f-string
            if (c == context.QuoteChar)
            {
                // Check for triple-quote end
                if (context.IsTriple)
                {
                    if (_position + 2 < _source.Length &&
                        _source[_position + 1] == context.QuoteChar &&
                        _source[_position + 2] == context.QuoteChar)
                    {
                        // Emit any accumulated text first
                        if (sb.Length > 0)
                        {
                            // Return the text token. On next call, we'll handle the closing triple-quote
                            return CreateToken(TokenType.FStringText, sb.ToString(), startLine, startColumn, startPosition);
                        }

                        // End of f-string
                        _position += 3;
                        _column += 3;
                        _fstringStack.Pop();
                        return CreateToken(TokenType.FStringEnd, new string(context.QuoteChar, 3), startLine, startColumn, startPosition);
                    }
                    // Not end of triple-quote, treat as regular character
                    sb.Append(c);
                    _position++;
                    _column++;
                }
                else
                {
                    // Single-quoted f-string end
                    // Emit any accumulated text first
                    if (sb.Length > 0)
                    {
                        // Return the text token. On next call, we'll handle the closing quote
                        // Don't pop the stack yet!
                        return CreateToken(TokenType.FStringText, sb.ToString(), startLine, startColumn, startPosition);
                    }

                    // End of f-string
                    _position++;
                    _column++;
                    _fstringStack.Pop();
                    return CreateToken(TokenType.FStringEnd, context.QuoteChar.ToString(), startLine, startColumn, startPosition);
                }
            }
            // Check for expression start
            else if (c == '{')
            {
                // Check for escaped brace {{
                if (_position + 1 < _source.Length && _source[_position + 1] == '{')
                {
                    sb.Append('{');
                    _position += 2;
                    _column += 2;
                }
                else
                {
                    // Start of interpolated expression
                    // Emit accumulated text first if any
                    if (sb.Length > 0)
                    {
                        // Return the text token. On next call, we'll handle the {
                        return CreateToken(TokenType.FStringText, sb.ToString(), startLine, startColumn, startPosition);
                    }

                    // Start expression - consume the { and increment brace depth
                    _position++;
                    _column++;
                    context.BraceDepth = 1;
                    return CreateToken(TokenType.FStringExprStart, "{", startLine, startColumn, startPosition);
                }
            }
            // Check for escaped closing brace }}
            else if (c == '}')
            {
                if (_position + 1 < _source.Length && _source[_position + 1] == '}')
                {
                    sb.Append('}');
                    _position += 2;
                    _column += 2;
                }
                else
                {
                    // Unmatched closing brace in f-string
                    throw ReportError("Unmatched '}' in f-string", _line, _column, DiagnosticCodes.Lexer.UnmatchedBraceInFString);
                }
            }
            // Handle escape sequences
            else if (c == '\\')
            {
                _position++;
                _column++;
                if (_position >= _source.Length)
                    throw ReportError("Unterminated f-string", _line, _column, DiagnosticCodes.Lexer.UnterminatedFString);

                sb.Append(ProcessEscapeSequence());
            }
            // Handle newlines in triple-quoted f-strings
            else if (c == '\n')
            {
                if (!context.IsTriple)
                {
                    throw ReportError("Unterminated f-string", _line, _column, DiagnosticCodes.Lexer.UnterminatedFString);
                }

                // PEP 822: For dedented f-strings, if the newline is followed by
                // DedentAmount whitespace chars and then the closing """, that newline
                // is part of the closing delimiter and should not be emitted.
                if (context.DedentAmount > 0 && IsPreCloseNewline(context))
                {
                    // Consume the \n and the dedent whitespace; leave position at """.
                    _position++;
                    _line++;
                    _column = 1;
                    SkipDedentWhitespace(context.DedentAmount);
                    continue;
                }

                sb.Append(c);
                _position++;
                _line++;
                _column = 1;

                if (context.DedentAmount > 0)
                    SkipDedentWhitespace(context.DedentAmount);
            }
            else if (c == '\r')
            {
                if (!context.IsTriple)
                {
                    throw ReportError("Unterminated f-string", _line, _column, DiagnosticCodes.Lexer.UnterminatedFString);
                }

                if (context.DedentAmount > 0 && IsPreCloseNewline(context))
                {
                    if (_position + 1 < _source.Length && _source[_position + 1] == '\n')
                        _position += 2;
                    else
                        _position++;
                    _line++;
                    _column = 1;
                    SkipDedentWhitespace(context.DedentAmount);
                    continue;
                }

                if (_position + 1 < _source.Length && _source[_position + 1] == '\n')
                {
                    sb.Append('\n');
                    _position += 2;
                }
                else
                {
                    sb.Append('\n');
                    _position++;
                }
                _line++;
                _column = 1;

                if (context.DedentAmount > 0)
                    SkipDedentWhitespace(context.DedentAmount);
            }
            // Regular character
            else
            {
                sb.Append(c);
                _position++;
                _column++;
            }
        }

        // Reached end of source while in f-string
        throw ReportError("Unterminated f-string", _line, _column, DiagnosticCodes.Lexer.UnterminatedFString);
    }
}
