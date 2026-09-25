using System.Text;
using Sharpy.Compiler.Diagnostics;

namespace Sharpy.Compiler.Lexer;

public partial class Lexer
{
    private class FStringContext
    {
        public char QuoteChar { get; set; }
        public bool IsTriple { get; set; }
        public int DedentAmount { get; set; }   // PEP 822: number of whitespace chars to strip after each \n (0 = no dedent)
        public bool IsTString { get; set; }     // PEP 750: template string (t"...") — same scanning, different AST

        /// <summary>
        /// The replacement fields currently open, innermost on top. Empty = reading literal text.
        /// A field is pushed at each unescaped <c>{</c> (both a top-level hole and a nested hole
        /// inside a format spec, PEP 701) and popped at its matching <c>}</c>.
        /// </summary>
        public Stack<FStringField> Fields { get; } = new();
    }

    /// <summary>
    /// One open replacement field being lexed. While <see cref="InFormatSpec"/> is false the field's
    /// expression is being tokenized (with <see cref="InnerBraceDepth"/> tracking dict/set braces and
    /// <see cref="ParenDepth"/> tracking ()/[] so '='/'!' specifiers only fire at the field's top
    /// level); once ':' is consumed the field switches to format-spec text, where an unescaped
    /// <c>{</c> pushes a nested field.
    /// </summary>
    private class FStringField
    {
        public bool InFormatSpec { get; set; }     // true once ':' consumed for this field
        public int ParenDepth { get; set; }        // ()/[] nesting within the field's expression
        public int InnerBraceDepth { get; set; }   // dict/set {} nesting within the field's expression
        public int ExprStartPosition { get; set; } // source position of the first char after '{'
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
        });

        return CreateToken(TokenType.FStringStart, isTriple ? $"f{quote}{quote}{quote}" : $"f{quote}", startLine, startColumn, startPosition);
    }

    /// <summary>
    /// Read the start of a t-string (PEP 750) and enter f-string mode with IsTString flag.
    /// </summary>
    private Token ReadTStringStart()
    {
        var startLine = _line;
        var startColumn = _column;
        var startPosition = _position;

        _position++; // skip 't'
        _column++;

        var quote = _source[_position];
        _position++;
        _column++;

        var isTriple = _position + 1 < _source.Length &&
                       _source[_position] == quote &&
                       _source[_position + 1] == quote;

        if (isTriple)
        {
            _position += 2;
            _column += 2;
        }

        _fstringStack.Push(new FStringContext
        {
            QuoteChar = quote,
            IsTriple = isTriple,
            IsTString = true
        });

        return CreateToken(TokenType.FStringStart, isTriple ? $"t{quote}{quote}{quote}" : $"t{quote}", startLine, startColumn, startPosition);
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
    /// The hole's expression source text (PEP 750 <c>Interpolation.expression</c>, #1991): from just
    /// after the field's <c>{</c> to its top-level terminator at <paramref name="terminatorPosition"/>
    /// (<c>}</c>, <c>=</c>, <c>!</c> or <c>:</c>), leading whitespace kept, trailing whitespace stripped
    /// — python3.14: <c>t"{ x }"</c> → <c>' x'</c>. Attached to the terminator token, which the parser
    /// reads right after the expression.
    /// </summary>
    private string HoleExpressionText(FStringField field, int terminatorPosition) =>
        _source.Substring(field.ExprStartPosition, terminatorPosition - field.ExprStartPosition)
            .TrimEnd(' ', '\t', '\f', '\n', '\r');

    /// <summary>
    /// The hole's raw source (#2024): from just after the field's <c>{</c> to
    /// <paramref name="endPosition"/> — the conversion <c>!</c>, the spec <c>:</c>, the closing
    /// <c>}</c>, or (for the <c>=</c> form) just past the <c>=</c> and its trailing whitespace —
    /// untrimmed. The unparser writes it verbatim.
    /// </summary>
    private string HoleRawText(FStringField field, int endPosition) =>
        _source.Substring(field.ExprStartPosition, endPosition - field.ExprStartPosition);

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

        // If we have an open replacement field, tokenize the field (expression or format spec).
        if (context.Fields.Count > 0)
        {
            var field = context.Fields.Peek();

            // A field in format-spec mode reads spec text and pushes nested holes (PEP 701).
            if (field.InFormatSpec)
                return NextFStringSpecToken(context, field, atSpecStart: false);

            var current = _source[_position];

            // Handle closing brace
            if (current == '}')
            {
                _position++;
                _column++;

                if (field.InnerBraceDepth == 0)
                {
                    // End of this field's expression — pop the field.
                    context.Fields.Pop();
                    return CreateToken(TokenType.FStringExprEnd, "}", startLine, startColumn, startPosition,
                        fstringExpressionText: HoleExpressionText(field, startPosition),
                        fstringRawText: HoleRawText(field, startPosition));
                }
                else
                {
                    // Nested (dict/set) closing brace within the expression
                    field.InnerBraceDepth--;
                    return CreateToken(TokenType.RightBrace, "}", startLine, startColumn, startPosition);
                }
            }

            // Handle opening brace (nested dict/set within expression)
            if (current == '{')
            {
                field.InnerBraceDepth++;
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
                _position++;
                _column++;

                if (field.InnerBraceDepth == 0)
                {
                    context.Fields.Pop();
                    return CreateToken(TokenType.FStringExprEnd, "}", startLine, startColumn, startPosition,
                        fstringExpressionText: HoleExpressionText(field, startPosition),
                        fstringRawText: HoleRawText(field, startPosition));
                }
                else
                {
                    field.InnerBraceDepth--;
                    return CreateToken(TokenType.RightBrace, "}", startLine, startColumn, startPosition);
                }
            }

            if (current == '{')
            {
                field.InnerBraceDepth++;
                _position++;
                _column++;
                return CreateToken(TokenType.LeftBrace, "{", startLine, startColumn, startPosition);
            }

            // Check for the '=' self-documenting specifier at the top level of a replacement
            // field (#986), e.g. f'{x=}' / f'{x = }'. Only at the field's top level and outside any
            // ()/[] (so keyword args like dict(a=1) are not misread), and not part of '=='.
            if (current == '=' && field.InnerBraceDepth == 0 && field.ParenDepth == 0
                && (_position + 1 >= _source.Length || _source[_position + 1] != '='))
            {
                // Consume '=' and any trailing whitespace; the captured SourceText is the
                // verbatim inner text from just after '{' through '=' and trailing spaces,
                // which the emitter prints literally before the value (matches CPython).
                _position++;
                _column++;
                while (_position < _source.Length && (_source[_position] == ' ' || _source[_position] == '\t'))
                {
                    _position++;
                    _column++;
                }
                var selfDocText = _source.Substring(field.ExprStartPosition, _position - field.ExprStartPosition);
                // Value is the verbatim 'expr=' text the emitter prints literally, but the token's
                // SOURCE span is only the '=' (+trailing whitespace) it owns — the expression chars
                // are already covered by their own tokens. Without an explicit SourceLength, Length
                // would be Value.Length and the span would overrun the following '}' (#1016,
                // non-monotonic token positions).
                return CreateToken(TokenType.FStringSelfDoc, selfDocText, startLine, startColumn, startPosition,
                    sourceLength: _position - startPosition, fstringExpressionText: HoleExpressionText(field, startPosition),
                    fstringRawText: HoleRawText(field, _position));
            }

            // Check for conversion flag (!r / !s / !a) at the top level of a replacement
            // field, i.e. after the expression and before any ':' format spec or '}'.
            // Guard against the '!=' operator (next char '='), which is tokenized normally.
            if (current == '!' && field.InnerBraceDepth == 0 && field.ParenDepth == 0
                && (_position + 1 >= _source.Length || _source[_position + 1] != '='))
            {
                bool validFlag = _position + 1 < _source.Length &&
                    (_source[_position + 1] == 'r' || _source[_position + 1] == 's' || _source[_position + 1] == 'a');
                bool properlyTerminated = _position + 2 < _source.Length &&
                    (_source[_position + 2] == '}' || _source[_position + 2] == ':');

                if (validFlag && properlyTerminated)
                {
                    var conversion = _source[_position + 1];
                    _position += 2;  // consume '!' and the flag char
                    _column += 2;
                    return CreateToken(TokenType.FStringConversion, conversion.ToString(), startLine, startColumn, startPosition,
                        fstringExpressionText: HoleExpressionText(field, startPosition),
                        fstringRawText: HoleRawText(field, startPosition));
                }

                if (validFlag)
                {
                    // The flag itself is valid (!r/!s/!a); the replacement field is just missing its
                    // closing '}' or ':' format spec. Report the real cause, not a bogus bad-flag.
                    throw ReportError(
                        "Unterminated f-string expression: expected '}' or ':' after the conversion flag",
                        _line, _column, DiagnosticCodes.Lexer.UnterminatedFStringExpression);
                }

                var badChar = _position + 1 < _source.Length ? _source[_position + 1].ToString() : "";
                throw ReportError(
                    $"Invalid f-string conversion '!{badChar}'. Expected '!r', '!s', or '!a' followed by '}}' or ':'.",
                    _line, _column, DiagnosticCodes.Lexer.InvalidFStringConversion);
            }

            // Check for format specification start (: at the field's top level). The spec is a
            // mini f-string: a sequence of literal text and nested replacement fields (PEP 701).
            // Consume the ':' and switch the field to spec mode; the first token is the leading
            // spec text (possibly empty — which itself signals to the parser that a spec exists).
            if (current == ':' && field.InnerBraceDepth == 0 && field.ParenDepth == 0)
            {
                var expressionText = HoleExpressionText(field, startPosition);
                var rawText = HoleRawText(field, startPosition);
                _position++;
                _column++;
                field.InFormatSpec = true;
                var specStart = NextFStringSpecToken(context, field, atSpecStart: true);
                return specStart with { FStringExpressionText = expressionText, FStringRawText = rawText };
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

            // Track ()/[] nesting so '='/'!' specifiers are only recognised at the top level
            // of the replacement field (e.g. keyword args in dict(a=1) must not trigger '=').
            if (current == '(' || current == '[')
                field.ParenDepth++;
            else if ((current == ')' || current == ']') && field.ParenDepth > 0)
                field.ParenDepth--;

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

                    // Start expression - consume the { and push a new replacement field.
                    _position++;
                    _column++;
                    context.Fields.Push(new FStringField { ExprStartPosition = _position });
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

    /// <summary>
    /// Emit the next token for a field that is in format-spec mode. The spec is a mini f-string:
    /// a sequence of literal text (<see cref="TokenType.FStringFormatSpec"/>) and nested replacement
    /// fields (<c>FStringExprStart … FStringExprEnd</c>, PEP 701). An unescaped <c>{</c> pushes a
    /// nested field; an unescaped <c>}</c> ends this field's spec and pops it. This is re-entered
    /// once per call, so it returns exactly one token and advances past it.
    /// </summary>
    private Token NextFStringSpecToken(FStringContext context, FStringField field, bool atSpecStart)
    {
        if (_position >= _source.Length)
            throw ReportError("Unterminated format specification in f-string", _line, _column, DiagnosticCodes.Lexer.UnterminatedFormatSpec);

        var startLine = _line;
        var startColumn = _column;
        var startPosition = _position;

        var c = _source[_position];

        // The token right after ':' is ALWAYS the leading spec text (possibly empty), so the parser
        // sees at least one FStringFormatSpec and knows a spec exists — even for {x:{w}} and {x:}.
        // Subsequent calls dispatch on a leading '{'/'}' to push a nested field or close this one.
        if (!atSpecStart)
        {
            // An unescaped '{' opens a nested replacement field inside the spec.
            if (c == '{' && !(_position + 1 < _source.Length && _source[_position + 1] == '{'))
            {
                _position++;
                _column++;
                context.Fields.Push(new FStringField { ExprStartPosition = _position });
                return CreateToken(TokenType.FStringExprStart, "{", startLine, startColumn, startPosition);
            }

            // A '}' ends this field's spec and closes the field. Inside a replacement field a '}'
            // ALWAYS closes — there is no '}}' escape in spec context (python: f"{5:}}}" == "5}", the
            // first '}' closes the field). Without this, adjacent field-closes at depth >= 2
            // (f"{x:{y:{z}}}", tail "}}}") were mis-read as a '}}' escape and swallowed (#1884).
            if (c == '}')
            {
                _position++;
                _column++;
                context.Fields.Pop();
                return CreateToken(TokenType.FStringExprEnd, "}", startLine, startColumn, startPosition);
            }
        }

        // Otherwise accumulate literal spec text up to the next unescaped '{' or '}'.
        var sb = new StringBuilder();
        while (_position < _source.Length)
        {
            var fsc = _source[_position];
            if (fsc == '{' && _position + 1 < _source.Length && _source[_position + 1] == '{')
            {
                sb.Append('{');
                _position += 2;
                _column += 2;
                continue;
            }
            // A '}' always ends spec text (closes the field on the next call); no '}}' escape in
            // spec context (#1884). '{{' still escapes to a literal '{' in spec text above.
            if (fsc == '{' || fsc == '}')
                break;

            sb.Append(fsc);
            _position++;
            if (fsc == '\n')
            {
                _line++;
                _column = 1;
            }
            else
            {
                _column++;
            }
        }

        if (_position >= _source.Length)
            throw ReportError("Unterminated format specification in f-string", _line, _column, DiagnosticCodes.Lexer.UnterminatedFormatSpec);

        return CreateToken(TokenType.FStringFormatSpec, sb.ToString(), startLine, startColumn, startPosition);
    }
}
