using System.Text;
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
            if (IsAtEnd) break;

            var stmt = ParseStatement();
            statements.Add(stmt);
            SkipNewlines();
        }

        _logger.LogInfo($"Module parsing completed in {startTime.ElapsedMilliseconds}ms, {statements.Count} statements");

        return new Module
        {
            Body = statements,
            DocString = docString,
            LineStart = 1,
            ColumnStart = 1,
            LineEnd = Current.Line,
            ColumnEnd = Current.Column
        };
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
            Advance();  // Skip @
            if (Current.Type != TokenType.Identifier)
                throw new ParserError("Expected decorator name", Current.Line, Current.Column);

            var decoratorName = Current.Value;
            Advance();
            var decoratorEndLine = Peek(-1).Line;
            var decoratorEndColumn = Peek(-1).Column + Peek(-1).Value.Length;

            decorators.Add(new Decorator
            {
                Name = decoratorName,
                LineStart = decoratorStartLine,
                ColumnStart = decoratorStartColumn,
                LineEnd = decoratorEndLine,
                ColumnEnd = decoratorEndColumn
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
            FunctionDef func => func with { Decorators = decorators },
            ClassDef cls => cls with { Decorators = decorators },
            StructDef str => str with { Decorators = decorators },
            _ => throw new ParserError("Unexpected decorated statement type", Current.Line, Current.Column)
        };
    }

}
