using System.Collections.Immutable;
using System.Text;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Lexer;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Parser;

/// <summary>
/// Parser partial class: Type annotation parsing and utilities
/// </summary>
public partial class Parser
{
    private TypeAnnotation ParseTypeAnnotation()
    {
        var baseType = ParseTypeAnnotationCore();
        var startToken = baseType; // reuse position from core parse

        // C# nullable suffix T | None (lowest precedence)
        if (Current.Type == TokenType.Pipe)
        {
            var pipeToken = Current;
            Advance(); // consume '|'

            // Must be followed by 'None'
            if (Current.Type != TokenType.None)
            {
                throw ReportError(
                    "Only '| None' is allowed for nullable types. " +
                    "Free unions like 'int | str' are not supported. " +
                    "Use 'union' declarations for custom sum types.",
                    Current.Line,
                    Current.Column,
                    DiagnosticCodes.Parser.FreeUnionNotSupported
                );
            }

            Advance(); // consume 'None'

            var endToken = Previous;
            var endLine = endToken.Line;
            var endColumn = endToken.Column + endToken.Value.Length;

            baseType = baseType with
            {
                IsCSharpNullable = true,
                LineEnd = endLine,
                ColumnEnd = endColumn,
                Span = baseType.Span != null && endToken.GetSpan() != null
                    ? Text.TextSpan.FromBounds(baseType.Span.Value.Start, endToken.GetSpan()!.Value.End)
                    : null
            };
        }

        return baseType;
    }

    /// <summary>
    /// Parses a type annotation without the | None suffix.
    /// Used for recursive type parsing (e.g., the error type in T !E)
    /// where | None should not be consumed.
    /// </summary>
    private TypeAnnotation ParseTypeAnnotationCore()
    {
        var startLine = Current.Line;
        var startColumn = Current.Column;
        var startToken = Current;

        TypeAnnotation baseType;

        // Check for shorthand forms first
        if (Current.Type == TokenType.LeftBracket)
        {
            // [T] list shorthand
            baseType = ParseListTypeShorthand(startLine, startColumn, startToken);
        }
        else if (Current.Type == TokenType.LeftBrace)
        {
            // {T} set or {K: V} dict shorthand
            baseType = ParseSetOrDictTypeShorthand(startLine, startColumn, startToken);
        }
        else if (Current.Type == TokenType.LeftParen)
        {
            // () empty tuple, (T) single tuple, (T, U) tuple, or (T) -> U function type
            baseType = ParseTupleOrFunctionTypeShorthand(startLine, startColumn, startToken);
        }
        else
        {
            // Standard type: identifier with optional generic args
            baseType = ParseStandardTypeAnnotation(startLine, startColumn, startToken);
        }

        // Check for array suffix: T[]
        while (Current.Type == TokenType.LeftBracket && Peek().Type == TokenType.RightBracket)
        {
            Advance(); // consume '['
            Advance(); // consume ']'
            var endToken = Previous;

            var endLine = endToken.Line;
            var endColumn = endToken.Column + endToken.Value.Length;

            baseType = new TypeAnnotation
            {
                Name = "array",
                TypeArguments = ImmutableArray.Create(baseType),
                IsOptional = false,
                LineStart = startLine,
                ColumnStart = startColumn,
                LineEnd = endLine,
                ColumnEnd = endColumn,
                Span = GetSpanFromTokens(startToken, endToken)
            };
        }

        // Result type suffix T !E (binds tighter than ? and | None)
        if (Current.Type == TokenType.Bang)
        {
            Advance(); // consume '!'

            // Parse the error type (without | None — it binds to the outer type)
            var errorType = ParseTypeAnnotationCore();

            var endToken = Previous;
            var endLine = endToken.Line;
            var endColumn = endToken.Column + endToken.Value.Length;

            baseType = baseType with
            {
                ErrorType = errorType,
                LineEnd = endLine,
                ColumnEnd = endColumn,
                Span = GetSpanFromTokens(startToken, endToken)
            };
        }

        // Optional type suffix T?
        if (Current.Type == TokenType.Question)
        {
            Advance();
            var endToken = Previous;
            var endLine = endToken.Line;
            var endColumn = endToken.Column + endToken.Value.Length;

            baseType = baseType with
            {
                IsOptional = true,
                LineEnd = endLine,
                ColumnEnd = endColumn,
                Span = GetSpanFromTokens(startToken, endToken)
            };
        }

        return baseType;
    }

    /// <summary>
    /// Parses standard type annotation: identifier with optional generic arguments.
    /// Handles: int, list[T], dict[K, V], auto, None
    /// </summary>
    private TypeAnnotation ParseStandardTypeAnnotation(int startLine, int startColumn, Token startToken)
    {
        // Handle 'auto' keyword for type inference
        string name;
        if (Current.Type == TokenType.Auto)
        {
            name = "auto";
            Advance();
        }
        // Handle 'None' as a type name (for -> None return annotations)
        else if (Current.Type == TokenType.None)
        {
            name = "None";
            Advance();
        }
        else
        {
            name = ExpectIdentifier();
        }

        var typeArgs = new List<TypeAnnotation>();

        // Generic type arguments [T, U]
        if (Current.Type == TokenType.LeftBracket && Peek().Type != TokenType.RightBracket)
        {
            Advance();
            do
            {
                typeArgs.Add(ParseTypeAnnotation());

                if (Current.Type == TokenType.Comma)
                    Advance();
                else
                    break;
            } while (true);
            Expect(TokenType.RightBracket);
        }

        var endToken = Previous;
        var endLine = endToken.Line;
        var endColumn = endToken.Column + endToken.Value.Length;

        return new TypeAnnotation
        {
            Name = name,
            TypeArguments = typeArgs.ToImmutableArray(),
            IsOptional = false,
            LineStart = startLine,
            ColumnStart = startColumn,
            LineEnd = endLine,
            ColumnEnd = endColumn,
            Span = GetSpanFromTokens(startToken, endToken)
        };
    }

    /// <summary>
    /// Parses [T] list shorthand. Produces same AST as list[T].
    /// </summary>
    private TypeAnnotation ParseListTypeShorthand(int startLine, int startColumn, Token startToken)
    {
        Advance(); // consume '['

        // Check for empty brackets - error case
        if (Current.Type == TokenType.RightBracket)
        {
            throw ReportError("List type shorthand requires an element type: [T]", Current.Line, Current.Column, DiagnosticCodes.Parser.EmptyListShorthand);
        }

        var elementType = ParseTypeAnnotation();
        Expect(TokenType.RightBracket);

        var endToken = Previous;
        var endLine = endToken.Line;
        var endColumn = endToken.Column + endToken.Value.Length;

        return new TypeAnnotation
        {
            Name = "list",
            TypeArguments = ImmutableArray.Create(elementType),
            IsOptional = false,
            LineStart = startLine,
            ColumnStart = startColumn,
            LineEnd = endLine,
            ColumnEnd = endColumn,
            Span = GetSpanFromTokens(startToken, endToken)
        };
    }

    /// <summary>
    /// Parses {T} set or {K: V} dict shorthand.
    /// Presence of ':' distinguishes dict from set.
    /// </summary>
    private TypeAnnotation ParseSetOrDictTypeShorthand(int startLine, int startColumn, Token startToken)
    {
        Advance(); // consume '{'

        // Check for empty braces - error case
        if (Current.Type == TokenType.RightBrace)
        {
            throw ReportError("Set/dict type shorthand requires type arguments: {T} for set or {K: V} for dict", Current.Line, Current.Column, DiagnosticCodes.Parser.EmptySetDictShorthand);
        }

        var firstType = ParseTypeAnnotation();

        // Check if this is a dict (has ':')
        if (Current.Type == TokenType.Colon)
        {
            Advance(); // consume ':'
            var valueType = ParseTypeAnnotation();
            Expect(TokenType.RightBrace);

            var endToken = Previous;
            var endLine = endToken.Line;
            var endColumn = endToken.Column + endToken.Value.Length;

            return new TypeAnnotation
            {
                Name = "dict",
                TypeArguments = ImmutableArray.Create(firstType, valueType),
                IsOptional = false,
                LineStart = startLine,
                ColumnStart = startColumn,
                LineEnd = endLine,
                ColumnEnd = endColumn,
                Span = GetSpanFromTokens(startToken, endToken)
            };
        }

        // Otherwise it's a set
        Expect(TokenType.RightBrace);

        var setEndToken = Previous;
        var setEndLine = setEndToken.Line;
        var setEndColumn = setEndToken.Column + setEndToken.Value.Length;

        return new TypeAnnotation
        {
            Name = "set",
            TypeArguments = ImmutableArray.Create(firstType),
            IsOptional = false,
            LineStart = startLine,
            ColumnStart = startColumn,
            LineEnd = setEndLine,
            ColumnEnd = setEndColumn,
            Span = GetSpanFromTokens(startToken, setEndToken)
        };
    }

    /// <summary>
    /// Parses tuple shorthand or function type.
    /// () = empty tuple, (T) = single tuple, (T, U) = tuple, (T) -> U = function type.
    /// Presence of '->' distinguishes function type from tuple.
    /// </summary>
    private TypeAnnotation ParseTupleOrFunctionTypeShorthand(int startLine, int startColumn, Token startToken)
    {
        Advance(); // consume '('

        var types = new List<TypeAnnotation>();
        var hasTrailingComma = false;

        // Parse type list
        if (Current.Type != TokenType.RightParen)
        {
            do
            {
                types.Add(ParseTypeAnnotation());

                if (Current.Type == TokenType.Comma)
                {
                    hasTrailingComma = true;
                    Advance();
                }
                else
                {
                    hasTrailingComma = false;
                    break;
                }
            } while (Current.Type != TokenType.RightParen);
        }

        Expect(TokenType.RightParen);

        // Check if this is a function type (has '->')
        if (Current.Type == TokenType.Arrow)
        {
            Advance(); // consume '->'
            var returnType = ParseTypeAnnotation();

            var funcEndToken = Previous;
            var funcEndLine = funcEndToken.Line;
            var funcEndColumn = funcEndToken.Column + funcEndToken.Value.Length;

            // For function types, we return a special representation
            // The Name "function" with TypeArguments containing params + return type
            // Last type argument is the return type
            var funcTypeArgs = new List<TypeAnnotation>(types) { returnType };

            return new TypeAnnotation
            {
                Name = "function",
                TypeArguments = funcTypeArgs.ToImmutableArray(),
                IsOptional = false,
                LineStart = startLine,
                ColumnStart = startColumn,
                LineEnd = funcEndLine,
                ColumnEnd = funcEndColumn,
                Span = GetSpanFromTokens(startToken, funcEndToken)
            };
        }

        // It's a tuple
        var endToken = Previous;
        var endLine = endToken.Line;
        var endColumn = endToken.Column + endToken.Value.Length;

        return new TypeAnnotation
        {
            Name = "tuple",
            TypeArguments = types.ToImmutableArray(),
            IsOptional = false,
            LineStart = startLine,
            ColumnStart = startColumn,
            LineEnd = endLine,
            ColumnEnd = endColumn,
            Span = GetSpanFromTokens(startToken, endToken)
        };
    }

    private void Advance() => _position++;

    /// <summary>
    /// Gets a TextSpan from a token, if position tracking is available.
    /// Returns null if the token doesn't have position information.
    /// </summary>
    private static Text.TextSpan? GetSpanFromToken(Token token)
    {
        return token.GetSpan();
    }

    /// <summary>
    /// Gets a TextSpan spanning from start token to end token (inclusive).
    /// Returns null if either token doesn't have position information.
    /// </summary>
    private static Text.TextSpan? GetSpanFromTokens(Token start, Token end)
    {
        var startSpan = start.GetSpan();
        var endSpan = end.GetSpan();

        if (startSpan == null || endSpan == null)
            return null;

        return Text.TextSpan.FromBounds(startSpan.Value.Start, endSpan.Value.End);
    }

    /// <summary>
    /// Combines two optional spans into a span covering both.
    /// Returns null if either span is null.
    /// </summary>
    private static Text.TextSpan? CombineSpans(Text.TextSpan? first, Text.TextSpan? second)
    {
        if (first == null || second == null)
            return null;
        return first.Value.Union(second.Value);
    }

    private void Expect(TokenType type)
    {
        if (Current.Type != type)
            throw ReportError($"Expected {type}, got {Current.Type}", Current.Line, Current.Column, DiagnosticCodes.Parser.ExpectedToken);
        Advance();
    }

    private string ExpectIdentifier()
    {
        if (Current.Type != TokenType.Identifier)
            throw ReportError($"Expected identifier, got {Current.Type}", Current.Line, Current.Column, DiagnosticCodes.Parser.ExpectedIdentifier);
        var value = Current.Value;
        Advance();
        return value;
    }

    /// <summary>
    /// Expects an identifier or keyword token and returns its value as a string.
    /// Used for member access where keywords can be used as member names (e.g., obj.property, obj.type).
    /// </summary>
    private string ExpectIdentifierOrKeyword()
    {
        if (Current.Type == TokenType.Identifier || IsKeywordToken(Current.Type))
        {
            var value = Current.Value;
            Advance();
            return value;
        }
        throw ReportError($"Expected identifier, got {Current.Type}", Current.Line, Current.Column, DiagnosticCodes.Parser.ExpectedIdentifier);
    }

    /// <summary>
    /// Checks if a token type is a keyword that can be used as an identifier in member access context.
    /// </summary>
    private static bool IsKeywordToken(TokenType type)
    {
        return type switch
        {
            // Control flow keywords
            TokenType.Def or TokenType.Class or TokenType.Struct or TokenType.Interface or
            TokenType.Enum or TokenType.If or TokenType.Else or TokenType.Elif or
            TokenType.While or TokenType.For or TokenType.In or TokenType.Return or
            TokenType.Break or TokenType.Continue or TokenType.Pass or TokenType.Try or
            TokenType.Except or TokenType.Finally or TokenType.Raise or TokenType.Assert or
            TokenType.With or
            // Import keywords
            TokenType.Import or TokenType.From or TokenType.As or
            // Type/Value keywords
            TokenType.Auto or TokenType.Const or TokenType.Lambda or TokenType.Type or
            // Pattern matching
            TokenType.Match or TokenType.Case or
            // Async keywords
            TokenType.Async or TokenType.Await or TokenType.Yield or
            // Member keywords
            TokenType.Property or TokenType.Event or
            // Other keywords
            TokenType.Del or TokenType.To or TokenType.Maybe or TokenType.Super or
            // Future keywords
            TokenType.Defer or TokenType.Do or
            // Boolean operators (keywords)
            TokenType.And or TokenType.Or or TokenType.Not or TokenType.Is or
            // Boolean literals
            TokenType.True or TokenType.False or TokenType.None
                => true,
            _ => false
        };
    }

    private void ExpectNewline()
    {
        if (Current.Type == TokenType.Newline)
            Advance();
        else if (!IsAtEnd)
            throw ReportError($"Expected newline, got {Current.Type}", Current.Line, Current.Column, DiagnosticCodes.Parser.ExpectedNewline);
    }

    private void ExpectStatementEnd()
    {
        // Simple statements can end with:
        // 1. Newline (normal case)
        // 2. Dedent (last statement in a block)
        // 3. EOF (last statement in file)
        if (Current.Type == TokenType.Newline)
            Advance();
        else if (Current.Type != TokenType.Dedent && !IsAtEnd)
            throw ReportError($"Expected end of statement, got {Current.Type}", Current.Line, Current.Column, DiagnosticCodes.Parser.ExpectedEndOfStatement);
    }

    private void SkipNewlines()
    {
        while (Current.Type == TokenType.Newline)
            Advance();
    }

    private bool IsTypeName(string name)
    {
        // Primitive types
        if (name is "int" or "float" or "str" or "bool" or "list" or "dict" or "set" or "tuple" or "object" or "any")
            return true;

        // User-defined types typically start with uppercase letter
        if (name.Length > 0 && char.IsUpper(name[0]))
            return true;

        return false;
    }

    /// <summary>
    /// Parse a segmented f-string (new lexer approach)
    /// FStringStart, (FStringText | FStringExprStart Expression [: FormatSpec] FStringExprEnd)*, FStringEnd
    /// </summary>
    private FStringLiteral ParseSegmentedFString(int startLine, int startColumn, Token startToken)
    {
        var parts = new List<FStringPart>();

        // Consume FStringStart
        Expect(TokenType.FStringStart);

        while (Current.Type != TokenType.FStringEnd && Current.Type != TokenType.Eof)
        {
            if (Current.Type == TokenType.FStringText)
            {
                // Text segment
                parts.Add(new FStringPart { Text = Current.Value, Expression = null });
                Advance();
            }
            else if (Current.Type == TokenType.FStringExprStart)
            {
                // Expression segment
                Advance(); // Skip FStringExprStart

                // Parse the expression (tokens are already emitted by lexer)
                var expr = ParseExpression();

                // Check for optional format spec token
                string? formatSpec = null;
                if (Current.Type == TokenType.FStringFormatSpec)
                {
                    formatSpec = Current.Value;
                    Advance();
                }

                parts.Add(new FStringPart { Text = null, Expression = expr, FormatSpec = formatSpec });

                // Expect FStringExprEnd
                Expect(TokenType.FStringExprEnd);
            }
            else
            {
                throw ReportError($"Unexpected token in f-string: {Current.Type}", Current.Line, Current.Column, DiagnosticCodes.Parser.UnexpectedToken);
            }
        }

        var endLine = Current.Line;
        var endColumn = Current.Column;
        var endToken = Current;

        // Consume FStringEnd
        Expect(TokenType.FStringEnd);

        return new FStringLiteral
        {
            Parts = parts.ToImmutableArray(),
            LineStart = startLine,
            ColumnStart = startColumn,
            LineEnd = endLine,
            ColumnEnd = endColumn,
            Span = GetSpanFromTokens(startToken, endToken)
        };
    }
}
