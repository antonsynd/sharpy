using System.Collections.Immutable;
using System.Text;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Lexer;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Parser;

/// <summary>
/// Parses Sharpy tokens into an Abstract Syntax Tree (AST).
/// Implements recursive descent parsing with operator precedence.
/// </summary>
public partial class Parser
{
    private readonly List<Token> _tokens;
    private int _position;
    private readonly ICompilerLogger _logger;
    private readonly DiagnosticBag _diagnostics = new();

    /// <summary>
    /// Diagnostics collected during parsing.
    /// </summary>
    public DiagnosticBag Diagnostics => _diagnostics;

    /// <summary>
    /// Flag indicating whether we're currently parsing inside an interface definition.
    /// When true, interface methods can omit the colon and body (e.g., `def foo(self) -> str`).
    /// </summary>
    private bool _parsingInterface;

    public Parser(List<Token> tokens, ICompilerLogger? logger = null)
    {
        _tokens = tokens;
        _position = 0;
        _logger = logger ?? NullLogger.Instance;
        _logger.LogInfo($"Parser initialized, token count: {tokens.Count}");
    }

    private Token Current => _position < _tokens.Count ? _tokens[_position] : _tokens[^1];
    private Token Previous => _position > 0 ? _tokens[_position - 1] : _tokens[0];
    private Token Peek(int offset = 1) => _position + offset < _tokens.Count ? _tokens[_position + offset] : _tokens[^1];
    private bool IsAtEnd => Current.Type == TokenType.Eof;

    /// <summary>
    /// Maximum number of parser errors before aborting.
    /// </summary>
    private const int MaxErrors = 5;

    /// <summary>
    /// Parse the entire module
    /// </summary>
    public Module ParseModule()
    {
        _logger.LogInfo("Starting module parsing");
        var startTime = System.Diagnostics.Stopwatch.StartNew();

        var statements = new List<Statement>();
        string? docString = null;

        // Skip leading newlines
        SkipNewlines();

        // Check for module docstring
        if (Current.Type == TokenType.String)
        {
            docString = Current.Value;
            Advance();
            SkipNewlines();
        }

        while (!IsAtEnd)
        {
            SkipNewlines();
            if (IsAtEnd)
                break;

            try
            {
                var stmt = ParseStatement();
                statements.Add(stmt);
            }
#pragma warning disable CS0618 // ParserError is obsolete
            catch (ParserError ex)
#pragma warning restore CS0618
            {
                var rawMessage = ex.Message;
                var colonIndex = rawMessage.IndexOf(": ", StringComparison.Ordinal);
                if (colonIndex >= 0 && rawMessage.StartsWith("Parser error", StringComparison.Ordinal))
                    rawMessage = rawMessage[(colonIndex + 2)..];

                _diagnostics.AddError(rawMessage, ex.Line, ex.Column,
                    code: MapParserErrorToCode(rawMessage),
                    phase: CompilerPhase.Parser);

                // Stop after MaxErrors to avoid cascading false errors
                if (_diagnostics.ErrorCount >= MaxErrors)
                    break;

                // Panic-mode recovery: synchronize to next statement boundary
                Synchronize();
            }
            SkipNewlines();
        }

        _logger.LogInfo($"Module parsing completed in {startTime.ElapsedMilliseconds}ms, {statements.Count} statements");

        return new Module
        {
            Body = statements.ToImmutableArray(),
            DocString = docString,
            LineStart = 1,
            ColumnStart = 1,
            LineEnd = Current.Line,
            ColumnEnd = Current.Column
        };
    }

    /// <summary>
    /// Panic-mode recovery: skip tokens until a statement boundary is reached.
    /// </summary>
    private void Synchronize()
    {
        while (!IsAtEnd)
        {
            // If we just passed a newline, we're at a statement boundary
            if (Previous.Type == TokenType.Newline || Previous.Type == TokenType.Dedent)
                return;

            // These tokens begin a new statement
            switch (Current.Type)
            {
                case TokenType.Def:
                case TokenType.Class:
                case TokenType.Struct:
                case TokenType.Interface:
                case TokenType.Enum:
                case TokenType.If:
                case TokenType.While:
                case TokenType.For:
                case TokenType.Return:
                case TokenType.Import:
                case TokenType.From:
                case TokenType.Raise:
                case TokenType.Assert:
                case TokenType.Pass:
                case TokenType.Break:
                case TokenType.Continue:
                case TokenType.Const:
                case TokenType.Type:
                case TokenType.At:
                case TokenType.Dedent:
                    return;
            }

            Advance();
        }
    }

    /// <summary>
    /// Maps a parser error message to a diagnostic code.
    /// </summary>
    private static string? MapParserErrorToCode(string message)
    {
        if (message.Contains("Unexpected token"))
            return DiagnosticCodes.Parser.UnexpectedToken;
        if (message.Contains("Expected identifier"))
            return DiagnosticCodes.Parser.ExpectedIdentifier;
        if (message.Contains("Expected newline"))
            return DiagnosticCodes.Parser.ExpectedNewline;
        if (message.Contains("Expected end of statement"))
            return DiagnosticCodes.Parser.ExpectedEndOfStatement;
        if (message.StartsWith("Expected "))
            return DiagnosticCodes.Parser.ExpectedToken;
        if (message.Contains("Decorators can only be applied"))
            return DiagnosticCodes.Parser.InvalidDecoratorTarget;
        if (message.Contains("decorator name"))
            return DiagnosticCodes.Parser.ExpectedDecoratorName;
        if (message.Contains("Tuple expression not allowed"))
            return DiagnosticCodes.Parser.TupleAsStatement;
        if (message.Contains("Invalid type annotation"))
            return DiagnosticCodes.Parser.InvalidTypeAnnotationTarget;
        if (message.Contains("must have at least one member"))
            return DiagnosticCodes.Parser.EmptyEnum;
        if (message.Contains("Positional argument cannot follow keyword"))
            return DiagnosticCodes.Parser.PositionalAfterKeyword;
        if (message.Contains("Only one variadic parameter"))
            return DiagnosticCodes.Parser.MultipleVariadic;
        if (message.Contains("Variadic parameter") && message.Contains("default"))
            return DiagnosticCodes.Parser.VariadicWithDefault;
        if (message.Contains("Variadic parameter") && message.Contains("last"))
            return DiagnosticCodes.Parser.VariadicNotLast;
        if (message.Contains("List type shorthand"))
            return DiagnosticCodes.Parser.EmptyListShorthand;
        if (message.Contains("Set/dict type shorthand"))
            return DiagnosticCodes.Parser.EmptySetDictShorthand;
        if (message.Contains("Expected module name"))
            return DiagnosticCodes.Parser.ExpectedModuleName;
        if (message.Contains("free-form union"))
            return DiagnosticCodes.Parser.FreeUnionNotSupported;
        return null;
    }

    private Statement ParseStatement()
    {
        // Decorators (for functions, classes, structs)
        if (Current.Type == TokenType.At)
            return ParseDecoratedStatement();

        // Special handling for 'try' - it can be either a statement (try:) or an expression (try expr)
        // Disambiguate by looking ahead: try statement has 'try:' while try expression has 'try expr'
        if (Current.Type == TokenType.Try)
        {
            // Check if next token is ':' (try statement) or '[' for try[Type] expression
            // or something else (try expression)
            if (Peek().Type == TokenType.Colon)
                return ParseTryStatement();
            // Otherwise it's a try expression, fall through to ParseSimpleStatement
            return ParseSimpleStatement();
        }

        // Special handling for 'maybe' - it's always an expression at statement level
        if (Current.Type == TokenType.Maybe)
            return ParseSimpleStatement();

        return Current.Type switch
        {
            TokenType.Def => ParseFunctionDef(),
            TokenType.Class => ParseClassDef(),
            TokenType.Struct => ParseStructDef(),
            TokenType.Interface => ParseInterfaceDef(),
            TokenType.Enum => ParseEnumDef(),
            TokenType.Type => ParseTypeAlias(),
            TokenType.If => ParseIfStatement(),
            TokenType.While => ParseWhileStatement(),
            TokenType.For => ParseForStatement(),
            TokenType.Return => ParseReturnStatement(),
            TokenType.Raise => ParseRaiseStatement(),
            TokenType.Assert => ParseAssertStatement(),
            TokenType.Pass => ParsePassStatement(),
            TokenType.Break => ParseBreakStatement(),
            TokenType.Continue => ParseContinueStatement(),
            TokenType.Import => ParseImportStatement(),
            TokenType.From => ParseFromImportStatement(),
            TokenType.Const => ParseConstDeclaration(),
            _ => ParseSimpleStatement()
        };
    }

    private Statement ParseDecoratedStatement()
    {
        var decorators = new List<Decorator>();

        while (Current.Type == TokenType.At)
        {
            var decoratorStartLine = Current.Line;
            var decoratorStartColumn = Current.Column;
            var decoratorStartToken = Current;
            Advance();  // Skip @
            if (Current.Type != TokenType.Identifier)
                throw new ParserError("Expected decorator name", Current.Line, Current.Column);

            var decoratorName = Current.Value;
            Advance();
            var decoratorEndToken = Previous;
            var decoratorEndLine = decoratorEndToken.Line;
            var decoratorEndColumn = decoratorEndToken.Column + decoratorEndToken.Value.Length;

            decorators.Add(new Decorator
            {
                Name = decoratorName,
                LineStart = decoratorStartLine,
                ColumnStart = decoratorStartColumn,
                LineEnd = decoratorEndLine,
                ColumnEnd = decoratorEndColumn,
                Span = GetSpanFromTokens(decoratorStartToken, decoratorEndToken)
            });
            ExpectNewline();
        }

        // Parse the decorated definition
        Statement stmt = Current.Type switch
        {
            TokenType.Def => ParseFunctionDef(),
            TokenType.Class => ParseClassDef(),
            TokenType.Struct => ParseStructDef(),
            _ => throw new ParserError("Decorators can only be applied to functions, classes, or structs", Current.Line, Current.Column)
        };

        // Attach decorators
        return stmt switch
        {
            FunctionDef func => func with { Decorators = decorators.ToImmutableArray() },
            ClassDef cls => cls with { Decorators = decorators.ToImmutableArray() },
            StructDef str => str with { Decorators = decorators.ToImmutableArray() },
            _ => throw new ParserError("Unexpected decorated statement type", Current.Line, Current.Column)
        };
    }

}
