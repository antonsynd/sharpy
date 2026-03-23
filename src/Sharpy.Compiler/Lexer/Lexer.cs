using System.Text;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Text;

namespace Sharpy.Compiler.Lexer;

/// <summary>
/// Tokenizes Sharpy source code into a stream of tokens.
/// Implements indentation-based syntax with INDENT/DEDENT tokens.
/// </summary>
public partial class Lexer
{
    /// <summary>
    /// Lightweight sentinel exception for unwinding the lexer call stack.
    /// The diagnostic is recorded into DiagnosticBag before this is thrown.
    /// </summary>
    private class LexerAbortException : Exception { }

    /// <summary>
    /// Record a lexer error into diagnostics and return an exception for stack unwinding.
    /// Usage: <c>throw ReportError("msg", line, col, code);</c>
    /// </summary>
    private LexerAbortException ReportError(string message, int line, int column, string code)
    {
        // Compute a TextSpan from line/column using SourceText if available,
        // otherwise fall back to the current _position with a length of 1.
        TextSpan? span = null;
        if (_sourceText != null)
        {
            var start = _sourceText.GetPosition(line, column);
            span = new TextSpan(start, System.Math.Max(1, _position - start));
        }
        else if (_position >= 0 && _position <= _source.Length)
        {
            // Best-effort: use _position as span start with length 1
            var start = System.Math.Max(0, _position > 0 ? _position - 1 : 0);
            span = new TextSpan(start, 1);
        }

        _diagnostics.AddError(message, span, line, column, code: code, phase: CompilerPhase.Lexer);
        return new LexerAbortException();
    }

    private readonly string _source;
    private readonly SourceText? _sourceText;
    private int _position;
    private int _line = 1;
    private int _column = 1;
    private readonly Stack<int> _indentStack = new();
    private readonly Queue<Token> _pendingTokens = new();
    private bool _atLineStart = true;
    private int _bracketDepth = 0;  // Track if we're inside (), [], or {}
    private readonly ICompilerLogger _logger;
    private readonly DiagnosticBag _diagnostics = new();
    private readonly CancellationToken _cancellationToken;
    private readonly bool _preserveTrivia;
    private List<Trivia>? _pendingTrivia;

    /// <summary>
    /// Diagnostics collected during lexing. Check HasErrors after TokenizeAll().
    /// </summary>
    public DiagnosticBag Diagnostics => _diagnostics;

    /// <summary>
    /// The SourceText being lexed, if one was provided.
    /// Available for downstream pipeline stages that need structured source access.
    /// </summary>
    public SourceText? SourceText => _sourceText;

    // F-string state tracking
    private readonly Stack<FStringContext> _fstringStack = new();

    // Keywords mapping
    private static readonly Dictionary<string, TokenType> Keywords = new()
    {
        // Control flow
        { "def", TokenType.Def },
        { "class", TokenType.Class },
        { "struct", TokenType.Struct },
        { "interface", TokenType.Interface },
        { "enum", TokenType.Enum },
        { "union", TokenType.Union },
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
        { "with", TokenType.With },

        // Import
        { "import", TokenType.Import },
        { "from", TokenType.From },
        { "as", TokenType.As },

        // Type/Value
        { "auto", TokenType.Auto },
        { "const", TokenType.Const },
        { "lambda", TokenType.Lambda },
        { "type", TokenType.Type },

        // Pattern Matching
        { "match", TokenType.Match },
        { "case", TokenType.Case },

        // Async
        { "async", TokenType.Async },
        { "await", TokenType.Await },
        { "yield", TokenType.Yield },

        // Members
        { "property", TokenType.Property },
        { "event", TokenType.Event },

        // Other
        { "delegate", TokenType.Delegate },
        { "del", TokenType.Del },
        { "to", TokenType.To },
        { "maybe", TokenType.Maybe },
        { "super", TokenType.Super },
        { "Self", TokenType.SelfType },

        // Future Keywords (reserved)
        { "defer", TokenType.Defer },
        { "do", TokenType.Do },

        // Rejected Keywords (Python keywords not supported in Sharpy)
        { "global", TokenType.Global },
        { "nonlocal", TokenType.Nonlocal },

        // Boolean values and operators
        { "True", TokenType.True },
        { "False", TokenType.False },
        { "None", TokenType.None },
        { "and", TokenType.And },
        { "or", TokenType.Or },
        { "not", TokenType.Not },
        { "is", TokenType.Is },
    };

    private static readonly IReadOnlySet<string> _keywordNames = new HashSet<string>(Keywords.Keys) { "self" };

    /// <summary>
    /// All Sharpy keyword names, including the contextual keyword "self".
    /// </summary>
    public static IReadOnlySet<string> KeywordNames => _keywordNames;

    public Lexer(string source, ICompilerLogger? logger = null, int startLine = 1, int startColumn = 1,
        CancellationToken cancellationToken = default, bool preserveTrivia = false)
    {
        _source = source;
        _sourceText = null;
        _line = startLine;
        _column = startColumn;
        _indentStack.Push(0);  // Base indentation level
        _logger = logger ?? NullLogger.Instance;
        _cancellationToken = cancellationToken;
        _preserveTrivia = preserveTrivia;
        if (preserveTrivia)
            _pendingTrivia = new List<Trivia>();
        _logger.LogInfo($"Lexer initialized, source length: {source.Length}");

        // If we're starting at a non-default position, we're lexing a fragment (like an f-string expression)
        // In this case, don't measure indentation
        if (startLine != 1 || startColumn != 1)
        {
            _atLineStart = false;
        }
    }

    /// <summary>
    /// Creates a new Lexer from a SourceText, enabling structured source access
    /// for downstream pipeline stages (e.g., LSP, diagnostic rendering).
    /// </summary>
    /// <param name="sourceText">The source text to lex.</param>
    /// <param name="logger">Optional compiler logger.</param>
    public Lexer(SourceText sourceText, ICompilerLogger? logger = null,
        CancellationToken cancellationToken = default, bool preserveTrivia = false)
    {
        _source = sourceText.ToString();
        _sourceText = sourceText;
        _indentStack.Push(0);
        _logger = logger ?? NullLogger.Instance;
        _cancellationToken = cancellationToken;
        _preserveTrivia = preserveTrivia;
        if (preserveTrivia)
            _pendingTrivia = new List<Trivia>();
        _logger.LogInfo($"Lexer initialized from SourceText, source length: {_source.Length}");
    }

    /// <summary>
    /// Maximum number of lexer errors before aborting tokenization.
    /// Default is 25, matching the parser's error budget.
    /// </summary>
    public int MaxErrors { get; set; } = 25;

    /// <summary>
    /// Tokenize the entire source into a list of tokens.
    /// Lexer errors are collected into Diagnostics instead of propagating.
    /// On error, the lexer attempts to recover by skipping to the next newline
    /// and continuing tokenization. Stops after MaxErrors lexer errors.
    /// </summary>
    public List<Token> TokenizeAll()
    {
        var tokens = new List<Token>();

        while (true)
        {
            _cancellationToken.ThrowIfCancellationRequested();

            Token token;
            try
            {
                token = NextToken();
            }
            catch (LexerAbortException)
            {
                // Error already recorded in _diagnostics by ReportError()
                if (_diagnostics.ErrorCount >= MaxErrors || _position >= _source.Length)
                {
                    if (_diagnostics.ErrorCount >= MaxErrors)
                    {
                        _diagnostics.AddWarning(
                            $"Too many errors ({MaxErrors}); further errors suppressed. Use '--max-errors' to increase the limit.",
                            _line, _column,
                            code: DiagnosticCodes.Infrastructure.TooManyErrors,
                            phase: CompilerPhase.Lexer);
                    }
                    tokens.Add(new Token(TokenType.Eof, "", _line, _column, _position));
                    break;
                }

                RecoverFromError();
                continue;
            }

            tokens.Add(token);

            if (token.Type == TokenType.Eof)
                break;
        }

        _logger.LogInfo($"Lexing completed ({tokens.Count} tokens produced)");
        return tokens;
    }

    /// <summary>
    /// Attempt to recover from a lexer error by advancing to the next newline.
    /// Resets indentation and bracket state to avoid cascading errors from
    /// the corrupted line.
    /// </summary>
    private void RecoverFromError()
    {
        // Skip to the next newline character (\n or \r)
        while (_position < _source.Length && _source[_position] != '\n' && _source[_position] != '\r')
        {
            _position++;
            _column++;
        }

        // Advance past the newline if present (handle \n, \r\n, and bare \r)
        if (_position < _source.Length)
        {
            if (_source[_position] == '\r')
            {
                _position++;
                // Handle \r\n sequence
                if (_position < _source.Length && _source[_position] == '\n')
                {
                    _position++;
                }
            }
            else if (_source[_position] == '\n')
            {
                _position++;
            }
            _line++;
            _column = 1;
        }

        // Reset line-start state so indentation processing restarts cleanly
        _atLineStart = true;

        // Reset indent stack to base level to avoid cascading indent errors
        _indentStack.Clear();
        _indentStack.Push(0);

        // Clear any pending indent/dedent tokens that may be stale
        _pendingTokens.Clear();

        // Reset bracket depth — unmatched brackets from the error line shouldn't persist
        _bracketDepth = 0;

        // Clear f-string state — unterminated f-strings shouldn't affect recovery
        _fstringStack.Clear();
    }

    /// <summary>
    /// Get the next token from the source
    /// </summary>
    public Token NextToken()
    {
        // Return pending tokens first (INDENT/DEDENT)
        if (_pendingTokens.Count > 0)
            return _pendingTokens.Dequeue();

        // If we're inside an f-string, handle f-string content
        if (_fstringStack.Count > 0)
        {
            return NextFStringToken();
        }

        // Handle end of file
        if (_position >= _source.Length)
        {
            // Generate DEDENTs for remaining indentation levels
            if (_indentStack.Count > 1)
            {
                _indentStack.Pop();
                return CreateToken(TokenType.Dedent, "", _line, _column, _position);
            }
            return CreateToken(TokenType.Eof, "", _line, _column, _position);
        }

        // Handle indentation at line start
        if (_atLineStart && _bracketDepth == 0)
        {
            var indentToken = HandleLineStartIndentation();
            if (indentToken != null)
                return indentToken;
        }

        // Skip whitespace (except newlines)
        SkipWhitespace();

        if (_position >= _source.Length)
            return NextToken();  // Recurse to handle EOF

        var startLine = _line;
        var startColumn = _column;
        var startPosition = _position;
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
                    throw ReportError("Backslash at end of file", _line, _column, DiagnosticCodes.Lexer.BackslashAtEof);
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
                        throw ReportError("Backslash line continuation cannot have trailing whitespace", _line, _column, DiagnosticCodes.Lexer.BackslashTrailingWhitespace);
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
                    throw ReportError("Backslash at end of file", _line, _column, DiagnosticCodes.Lexer.BackslashAtEof);
                }
                // Otherwise, fall through to treat backslash as operator
            }
        }

        // Newlines
        if (current == '\n' || current == '\r')
        {
            if (_bracketDepth > 0)
            {
                // Inside brackets, skip newlines
                if (current == '\r')
                {
                    _position++;
                    _column++;
                    // Check for \r\n sequence
                    if (_position < _source.Length && _source[_position] == '\n')
                    {
                        _position++;
                    }
                }
                else
                {
                    _position++;
                }
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
                // Check for \r\n sequence
                if (_position < _source.Length && _source[_position] == '\n')
                {
                    _position++;
                }
            }
            else
            {
                _position++;
            }
            _line++;
            _column = 1;
            _atLineStart = true;
            return LogAndReturn(CreateToken(TokenType.Newline, "\n", startLine, startColumn, startPosition));
        }

        // String literals
        if (current == '"' || current == '\'')
            return LogAndReturn(ReadString());

        // F-strings
        if (current == 'f' && (_position + 1 < _source.Length) &&
            (_source[_position + 1] == '"' || _source[_position + 1] == '\''))
            return LogAndReturn(ReadFStringStart());

        // Raw strings
        if (current == 'r' && (_position + 1 < _source.Length) &&
            (_source[_position + 1] == '"' || _source[_position + 1] == '\''))
            return LogAndReturn(ReadRawString());

        // Backtick-delimited literal names
        if (current == '`')
            return LogAndReturn(ReadLiteralName());

        // Numbers
        if (char.IsDigit(current))
            return LogAndReturn(ReadNumber());

        // Identifiers and keywords
        if (char.IsLetter(current) || current == '_')
            return LogAndReturn(ReadIdentifierOrKeyword());

        // Operators and delimiters
        return LogAndReturn(ReadOperatorOrDelimiter());
    }

    private Token LogAndReturn(Token token)
    {
        _logger.LogTokenRead(token.Type.ToString(), token.Line, token.Column, token.Value);
        return token;
    }

    /// <summary>
    /// Creates a token with position tracking.
    /// When trivia preservation is enabled, attaches any pending trivia as leading trivia.
    /// </summary>
    private Token CreateToken(TokenType type, string value, int startLine, int startColumn, int startPosition)
    {
        var token = new Token(type, value, startLine, startColumn, startPosition);

        if (_preserveTrivia && _pendingTrivia!.Count > 0)
        {
            token = token with { LeadingTrivia = _pendingTrivia.ToList() };
            _pendingTrivia.Clear();
        }

        return token;
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
        var startLine = _line;
        var startColumn = _column;
        var startPosition = _position;
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

        if (_preserveTrivia)
        {
            _pendingTrivia!.Add(new Trivia
            {
                Kind = TriviaKind.Comment,
                Text = "#" + sb.ToString(),
                Line = startLine,
                Column = startColumn,
                Position = startPosition
            });
        }

        // Comments are skipped in the token stream; get next token
        return NextToken();
    }

    private Token ReadIdentifierOrKeyword()
    {
        var startLine = _line;
        var startColumn = _column;
        var startPosition = _position;
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
            return CreateToken(tokenType, value, startLine, startColumn, startPosition);

        return CreateToken(TokenType.Identifier, value, startLine, startColumn, startPosition);
    }

    private Token ReadLiteralName()
    {
        var startLine = _line;
        var startColumn = _column;
        var startPosition = _position;
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
                return CreateToken(TokenType.Identifier, sb.ToString(), startLine, startColumn, startPosition);
            }

            if (c == '\n' || c == '\r')
            {
                throw ReportError("Unterminated literal name (backtick-delimited identifier)", _line, _column, DiagnosticCodes.Lexer.UnterminatedBacktickIdentifier);
            }

            sb.Append(c);
            _position++;
            _column++;
        }

        throw ReportError("Unterminated literal name (backtick-delimited identifier)", _line, _column, DiagnosticCodes.Lexer.UnterminatedBacktickIdentifier);
    }

    private Token ReadOperatorOrDelimiter()
    {
        var startLine = _line;
        var startColumn = _column;
        var startPosition = _position;
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
                    return CreateToken(TokenType.Ellipsis, threeChar, startLine, startColumn, startPosition);
                case "<<=":
                    _position += 3;
                    _column += 3;
                    return CreateToken(TokenType.LeftShiftAssign, threeChar, startLine, startColumn, startPosition);
                case ">>=":
                    _position += 3;
                    _column += 3;
                    return CreateToken(TokenType.RightShiftAssign, threeChar, startLine, startColumn, startPosition);
                case "**=":
                    _position += 3;
                    _column += 3;
                    return CreateToken(TokenType.DoubleStarAssign, threeChar, startLine, startColumn, startPosition);
                case "//=":
                    _position += 3;
                    _column += 3;
                    return CreateToken(TokenType.DoubleSlashAssign, threeChar, startLine, startColumn, startPosition);
                case "??=":
                    _position += 3;
                    _column += 3;
                    return CreateToken(TokenType.NullCoalesceAssign, threeChar, startLine, startColumn, startPosition);
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
                    return CreateToken(TokenType.Equal, twoChar, startLine, startColumn, startPosition);
                case "!=":
                    _position += 2;
                    _column += 2;
                    return CreateToken(TokenType.NotEqual, twoChar, startLine, startColumn, startPosition);
                case "<=":
                    _position += 2;
                    _column += 2;
                    return CreateToken(TokenType.LessEqual, twoChar, startLine, startColumn, startPosition);
                case ">=":
                    _position += 2;
                    _column += 2;
                    return CreateToken(TokenType.GreaterEqual, twoChar, startLine, startColumn, startPosition);
                case "<<":
                    _position += 2;
                    _column += 2;
                    return CreateToken(TokenType.LeftShift, twoChar, startLine, startColumn, startPosition);
                case ">>":
                    _position += 2;
                    _column += 2;
                    return CreateToken(TokenType.RightShift, twoChar, startLine, startColumn, startPosition);
                case "**":
                    _position += 2;
                    _column += 2;
                    return CreateToken(TokenType.DoubleStar, twoChar, startLine, startColumn, startPosition);
                case "//":
                    _position += 2;
                    _column += 2;
                    return CreateToken(TokenType.DoubleSlash, twoChar, startLine, startColumn, startPosition);
                case "->":
                    _position += 2;
                    _column += 2;
                    return CreateToken(TokenType.Arrow, twoChar, startLine, startColumn, startPosition);
                case "?.":
                    _position += 2;
                    _column += 2;
                    return CreateToken(TokenType.NullConditional, twoChar, startLine, startColumn, startPosition);
                case "??":
                    _position += 2;
                    _column += 2;
                    return CreateToken(TokenType.NullCoalesce, twoChar, startLine, startColumn, startPosition);
                case "+=":
                    _position += 2;
                    _column += 2;
                    return CreateToken(TokenType.PlusAssign, twoChar, startLine, startColumn, startPosition);
                case "-=":
                    _position += 2;
                    _column += 2;
                    return CreateToken(TokenType.MinusAssign, twoChar, startLine, startColumn, startPosition);
                case "*=":
                    _position += 2;
                    _column += 2;
                    return CreateToken(TokenType.StarAssign, twoChar, startLine, startColumn, startPosition);
                case "/=":
                    _position += 2;
                    _column += 2;
                    return CreateToken(TokenType.SlashAssign, twoChar, startLine, startColumn, startPosition);
                case "%=":
                    _position += 2;
                    _column += 2;
                    return CreateToken(TokenType.PercentAssign, twoChar, startLine, startColumn, startPosition);
                case "&=":
                    _position += 2;
                    _column += 2;
                    return CreateToken(TokenType.AmpersandAssign, twoChar, startLine, startColumn, startPosition);
                case "|=":
                    _position += 2;
                    _column += 2;
                    return CreateToken(TokenType.PipeAssign, twoChar, startLine, startColumn, startPosition);
                case "|>":
                    _position += 2;
                    _column += 2;
                    return CreateToken(TokenType.PipeForward, twoChar, startLine, startColumn, startPosition);
                case "^=":
                    _position += 2;
                    _column += 2;
                    return CreateToken(TokenType.CaretAssign, twoChar, startLine, startColumn, startPosition);
                case ":=":
                    _position += 2;
                    _column += 2;
                    return CreateToken(TokenType.ColonAssign, twoChar, startLine, startColumn, startPosition);
            }
        }

        // Float starting with decimal point (.5, .123)
        if (c == '.' && _position + 1 < _source.Length && char.IsDigit(_source[_position + 1]))
        {
            return LogAndReturn(ReadNumber());
        }

        // Single-character operators and delimiters
        _position++;
        _column++;

        var token = c switch
        {
            '+' => CreateToken(TokenType.Plus, "+", startLine, startColumn, startPosition),
            '-' => CreateToken(TokenType.Minus, "-", startLine, startColumn, startPosition),
            '*' => CreateToken(TokenType.Star, "*", startLine, startColumn, startPosition),
            '/' => CreateToken(TokenType.Slash, "/", startLine, startColumn, startPosition),
            '%' => CreateToken(TokenType.Percent, "%", startLine, startColumn, startPosition),
            '&' => CreateToken(TokenType.Ampersand, "&", startLine, startColumn, startPosition),
            '|' => CreateToken(TokenType.Pipe, "|", startLine, startColumn, startPosition),
            '^' => CreateToken(TokenType.Caret, "^", startLine, startColumn, startPosition),
            '~' => CreateToken(TokenType.Tilde, "~", startLine, startColumn, startPosition),
            '=' => CreateToken(TokenType.Assign, "=", startLine, startColumn, startPosition),
            '<' => CreateToken(TokenType.Less, "<", startLine, startColumn, startPosition),
            '>' => CreateToken(TokenType.Greater, ">", startLine, startColumn, startPosition),
            '?' => CreateToken(TokenType.Question, "?", startLine, startColumn, startPosition),
            '(' => CreateToken(TokenType.LeftParen, "(", startLine, startColumn, startPosition),
            ')' => CreateToken(TokenType.RightParen, ")", startLine, startColumn, startPosition),
            '[' => CreateToken(TokenType.LeftBracket, "[", startLine, startColumn, startPosition),
            ']' => CreateToken(TokenType.RightBracket, "]", startLine, startColumn, startPosition),
            '{' => CreateToken(TokenType.LeftBrace, "{", startLine, startColumn, startPosition),
            '}' => CreateToken(TokenType.RightBrace, "}", startLine, startColumn, startPosition),
            ',' => CreateToken(TokenType.Comma, ",", startLine, startColumn, startPosition),
            ':' => CreateToken(TokenType.Colon, ":", startLine, startColumn, startPosition),
            ';' => CreateToken(TokenType.Semicolon, ";", startLine, startColumn, startPosition),
            '.' => CreateToken(TokenType.Dot, ".", startLine, startColumn, startPosition),
            '@' => CreateToken(TokenType.At, "@", startLine, startColumn, startPosition),
            '!' => CreateToken(TokenType.Bang, "!", startLine, startColumn, startPosition),
            '\\' => CreateToken(TokenType.Backslash, "\\", startLine, startColumn, startPosition),
            _ => throw ReportError($"Unexpected character: '{c}'", startLine, startColumn, DiagnosticCodes.Lexer.UnexpectedCharacter)
        };

        // Track bracket depth for implicit line continuation
        if (c == '(' || c == '[' || c == '{')
        {
            _bracketDepth++;
        }
        else if (c == ')' || c == ']' || c == '}')
        {
            _bracketDepth--;
            // Prevent negative bracket depth from unmatched closing brackets
            if (_bracketDepth < 0)
                _bracketDepth = 0;
        }

        return token;
    }
}
