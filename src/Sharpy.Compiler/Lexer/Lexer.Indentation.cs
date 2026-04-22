using System.Text;
using Sharpy.Compiler.Diagnostics;

namespace Sharpy.Compiler.Lexer;

public partial class Lexer
{
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
            else
            {
                break;
            }
        }

        // Check for mixed tabs and spaces
        if (hasSpaces && hasTabs)
            throw ReportError("Mixed tabs and spaces in indentation", _line, 1, DiagnosticCodes.Lexer.MixedTabsAndSpaces);

        // No tabs allowed at all
        if (hasTabs)
            throw ReportError("Tabs are not allowed for indentation. Use 4 spaces.", _line, 1, DiagnosticCodes.Lexer.TabsNotAllowed);

        // Validate 4-space indentation
        if (indent % 4 != 0)
            throw ReportError($"Indentation must be multiple of 4 spaces (found {indent})", _line, 1, DiagnosticCodes.Lexer.InvalidIndentation);

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
                throw ReportError("Indentation mismatch", _line, 1, DiagnosticCodes.Lexer.IndentationMismatch);
            }
        }

        _position = tempPos;
        _column = indent + 1;
        return indent;
    }

    /// <summary>
    /// Handles indentation processing at the start of a line.
    /// Skips blank and comment-only lines, and produces INDENT/DEDENT tokens.
    /// Returns null if no indentation token was produced and the caller should continue.
    /// </summary>
    private Token? HandleLineStartIndentation()
    {
        // Skip whitespace to see what comes next
        var savedPos = _position;
        var savedCol = _column;
        SkipWhitespace();

        // Check if this is a blank line or comment line
        if (_position >= _source.Length || _source[_position] == '\n' || _source[_position] == '\r' || _source[_position] == '#')
        {
            // This is a blank or comment line - skip it entirely
            if (_position < _source.Length && _source[_position] == '#')
            {
                if (_preserveTrivia)
                {
                    var commentStartLine = _line;
                    var commentStartCol = _column;
                    var commentStartPos = _position;
                    var csb = new StringBuilder();
                    csb.Append('#');
                    _position++;
                    _column++;
                    while (_position < _source.Length && _source[_position] != '\n' && _source[_position] != '\r')
                    {
                        csb.Append(_source[_position]);
                        _position++;
                        _column++;
                    }
                    _pendingTrivia!.Add(new Trivia
                    {
                        Kind = TriviaKind.Comment,
                        Text = csb.ToString(),
                        Line = commentStartLine,
                        Column = commentStartCol,
                        Position = commentStartPos
                    });
                }
                else
                {
                    // Skip the comment
                    while (_position < _source.Length && _source[_position] != '\n' && _source[_position] != '\r')
                    {
                        _position++;
                        _column++;
                    }
                }
            }

            // Skip the newline if present
            if (_position < _source.Length && (_source[_position] == '\n' || _source[_position] == '\r'))
            {
                if (_source[_position] == '\r')
                {
                    _position++;
                    if (_position < _source.Length && _source[_position] == '\n')
                        _position++;
                }
                else
                {
                    _position++;
                }
                _line++;
                _column = 1;
                _atLineStart = true;
            }

            // Return null so the caller's loop re-enters (no recursion)
            return null;
        }

        // Restore position to measure indentation properly
        _position = savedPos;
        _column = savedCol;

        var indentLevel = MeasureIndentation();
        var currentIndent = _indentStack.Peek();

        if (indentLevel > currentIndent)
        {
            _indentStack.Push(indentLevel);
            _atLineStart = false;
            _logger.LogIndentChange(currentIndent, indentLevel);
            return CreateToken(TokenType.Indent, "", _line, 1, _position);
        }
        else if (indentLevel < currentIndent)
        {
            // Generate DEDENT tokens
            var dedents = new List<Token>();
            while (_indentStack.Count > 1 && _indentStack.Peek() > indentLevel)
            {
                _indentStack.Pop();
                dedents.Add(CreateToken(TokenType.Dedent, "", _line, 1, _position));
            }

            // No need to check here - MeasureIndentation already validated

            _atLineStart = false;
            _logger.LogIndentChange(currentIndent, indentLevel);

            // Queue remaining dedents and return first
            for (int i = 1; i < dedents.Count; i++)
                _pendingTokens.Enqueue(dedents[i]);

            return dedents[0];
        }

        _atLineStart = false;
        return null;
    }
}
