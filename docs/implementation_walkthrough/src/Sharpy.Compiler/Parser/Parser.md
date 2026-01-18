# Walkthrough: Parser.cs

**Source File**: `src/Sharpy.Compiler/Parser/Parser.cs`

---

## 1. Overview

The `Parser` class is the heart of Sharpy's syntax analysis phase. It transforms a flat stream of tokens (produced by the Lexer) into a hierarchical Abstract Syntax Tree (AST) that represents the semantic structure of Sharpy code.

**Key responsibilities:**
- Convert tokens into AST nodes (statements and expressions)
- Enforce Sharpy's grammar rules
- Handle Python-like syntax (indentation, colons, decorators)
- Support operator precedence and associativity
- Track source locations for error reporting

**Pipeline position:**
```
Source Code → Lexer → [TOKENS] → Parser → [AST] → Semantic Analysis → Code Generation
```

The parser uses a **recursive descent** approach with **precedence climbing** for expressions, making it both efficient and easy to understand/maintain.

### Partial Class Architecture

**IMPORTANT**: The `Parser` class is split across multiple files using C# partial classes for better maintainability:

- **`Parser.cs`** (this file) - Core infrastructure, entry points, and statement dispatching
- **[`Parser.Statements.cs`](Parser.Statements.md)** - Statement parsing (if, while, for, return, etc.)
- **[`Parser.Expressions.cs`](Parser.Expressions.md)** - Expression parsing with precedence climbing
- **[`Parser.Primaries.cs`](Parser.Primaries.md)** - Primary expressions (literals, identifiers, collections)
- **[`Parser.Definitions.cs`](Parser.Definitions.md)** - Type and function definitions (class, struct, def, etc.)
- **[`Parser.Types.cs`](Parser.Types.md)** - Type annotation parsing

This walkthrough focuses on the core `Parser.cs` file. **See the linked documents for details on specific parsing methods**.

---

## 2. Class Structure

### Core State

```csharp
public partial class Parser
{
    private readonly List<Token> _tokens;      // Input token stream
    private int _position;                      // Current position in token stream
    private readonly ICompilerLogger _logger;   // For diagnostics and debugging
}
```

**Key design decisions:**
- **Immutable token list**: Tokens are read-only; only `_position` changes
- **Single-pass**: Parser makes one forward pass through tokens
- **No backtracking**: Uses lookahead (via `Peek()`) instead of backtracking
- **Location tracking**: Every AST node captures `LineStart`, `ColumnStart`, `LineEnd`, `ColumnEnd`
- **Partial classes**: Large parser split into logical groupings for maintainability

### Constructor

```csharp
public Parser(List<Token> tokens, ICompilerLogger? logger = null)
{
    _tokens = tokens;
    _position = 0;
    _logger = logger ?? NullLogger.Instance;
    _logger.LogInfo($"Parser initialized, token count: {tokens.Count}");
}
```

**Parameters:**
- `tokens`: Complete token stream from the Lexer (must include EOF token at end)
- `logger`: Optional logger for diagnostics (defaults to `NullLogger` if not provided)

**Note**: The parser expects the token list to be complete and well-formed. The last token should always be `TokenType.Eof`.

### Helper Properties

```csharp
private Token Current => _position < _tokens.Count ? _tokens[_position] : _tokens[^1];
private Token Previous => _position > 0 ? _tokens[_position - 1] : _tokens[0];
private Token Peek(int offset = 1) => _position + offset < _tokens.Count
    ? _tokens[_position + offset]
    : _tokens[^1];
private bool IsAtEnd => Current.Type == TokenType.Eof;
```

These properties provide clean navigation through the token stream:
- **`Current`**: Token at current position (safe against overflow - returns EOF if past end)
- **`Previous`**: Last consumed token (useful for tracking end locations in AST nodes)
- **`Peek(n)`**: Look ahead `n` tokens without consuming (default offset = 1)
- **`IsAtEnd`**: Check if we've reached end-of-file

**Design pattern**: These read-only properties provide safe access to the token stream without exposing the mutable `_position` field.

---

## 3. Key Methods in Parser.cs

### 3.1 ParseModule() - Entry Point

**Purpose**: Parse an entire Sharpy source file into a `Module` AST node.

**Signature**:
```csharp
public Module ParseModule()
```

**Algorithm**:
1. Skip leading newlines/whitespace
2. Check for optional module docstring (first string literal at module level)
3. Parse statements in a loop until EOF
4. Return `Module` node containing all statements and metadata

**Implementation walkthrough**:

```csharp
public Module ParseModule()
{
    _logger.LogInfo("Starting module parsing");
    var startTime = System.Diagnostics.Stopwatch.StartNew();

    var statements = new List<Statement>();
    string? docString = null;

    // Skip leading newlines
    SkipNewlines();

    // Check for module docstring (Python convention: first statement is a string)
    if (Current.Type == TokenType.String)
    {
        docString = Current.Value;
        Advance();
        SkipNewlines();
    }

    // Parse all statements until EOF
    while (!IsAtEnd)
    {
        SkipNewlines();
        if (IsAtEnd) break;

        var stmt = ParseStatement();  // Dispatch to statement parser
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
```

**Key points:**
- **Docstring handling**: Sharpy follows Python's convention where a module-level string literal serves as documentation
- **Newline skipping**: Liberal newline skipping allows flexible formatting in source files
- **Performance tracking**: Uses Stopwatch for diagnostics (visible via logger)
- **Location tracking**: Module spans from line 1, column 1 to the EOF token's location

**When to call**: This is the public entry point called by `Compiler.cs` for each source file.

**Downstream**: The resulting `Module` object is passed to semantic analysis and code generation phases.

---

### 3.2 ParseStatement() - Statement Dispatcher

**Purpose**: Dispatch to the appropriate statement parser based on the current token type.

**Signature**:
```csharp
private Statement ParseStatement()
```

**Implementation**:

```csharp
private Statement ParseStatement()
{
    // Decorators (for functions, classes, structs)
    if (Current.Type == TokenType.At)
        return ParseDecoratedStatement();

    // Special handling for 'try' - disambiguate statement vs expression
    if (Current.Type == TokenType.Try)
    {
        // Check if next token is ':' (try statement) or something else (try expression)
        if (Peek().Type == TokenType.Colon)
            return ParseTryStatement();
        // Otherwise it's a try expression, fall through to ParseSimpleStatement
        return ParseSimpleStatement();
    }

    // Special handling for 'maybe' - always an expression at statement level
    if (Current.Type == TokenType.Maybe)
        return ParseSimpleStatement();

    // Pattern match on token type to dispatch
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
```

**Key design decisions:**

1. **Decorator handling first**: `@decorator` syntax is checked before other patterns since decorators can precede multiple statement types

2. **Try/maybe disambiguation**:
   - `try:` followed by a colon is a try statement (exception handling block)
   - `try expr` is a try expression (returns an `Option` type)
   - `maybe expr` is always an expression (also returns an `Option` type)
   - This lookahead prevents ambiguity in the grammar

3. **Switch expression pattern matching**: Uses C# 8.0 switch expressions for clean dispatching

4. **Fallthrough to `ParseSimpleStatement()`**: Any statement that doesn't match the explicit patterns falls through to simple statement parsing, which handles:
   - Expression statements (function calls, assignments, etc.)
   - Augmented assignments (`x += 1`)
   - Multi-target assignments (`a = b = c`)
   - Annotated assignments (`x: int = 5`)

**Where parsing methods are located:**
- **Definition parsers** (Def, Class, Struct, etc.): See [`Parser.Definitions.cs`](Parser.Definitions.md)
- **Control flow parsers** (If, While, For, etc.): See [`Parser.Statements.cs`](Parser.Statements.md)
- **Simple statement parser**: See [`Parser.Statements.cs`](Parser.Statements.md)

---

### 3.3 ParseDecoratedStatement() - Decorator Support

**Purpose**: Parse decorator syntax (`@decorator_name`) and attach decorators to function/class/struct definitions.

**Signature**:
```csharp
private Statement ParseDecoratedStatement()
```

**Decorator syntax in Sharpy**:
```python
@static
@override
def my_method(self, x: int) -> str:
    return f"Value: {x}"
```

**Implementation walkthrough**:

```csharp
private Statement ParseDecoratedStatement()
{
    var decorators = new List<Decorator>();

    // Collect all decorators
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

        ExpectNewline();  // Each decorator must be on its own line
    }

    // Parse the decorated definition
    Statement stmt = Current.Type switch
    {
        TokenType.Def => ParseFunctionDef(),
        TokenType.Class => ParseClassDef(),
        TokenType.Struct => ParseStructDef(),
        _ => throw new ParserError(
            "Decorators can only be applied to functions, classes, or structs",
            Current.Line, Current.Column)
    };

    // Attach decorators to the definition using record 'with' syntax
    return stmt switch
    {
        FunctionDef func => func with { Decorators = decorators },
        ClassDef cls => cls with { Decorators = decorators },
        StructDef str => str with { Decorators = decorators },
        _ => throw new ParserError("Unexpected decorated statement type", Current.Line, Current.Column)
    };
}
```

**Key points:**

1. **Multiple decorators**: The while loop allows stacking multiple decorators
   ```python
   @static
   @inline
   @pure
   def foo(): pass
   ```

2. **Location tracking**: Each decorator tracks its own source location for error reporting

3. **Grammar enforcement**:
   - Decorators must be followed by newlines (`ExpectNewline()`)
   - Only functions, classes, and structs can be decorated
   - Decorator names must be identifiers (no complex expressions)

4. **Immutable AST pattern**: Uses C# record `with` expressions to create new AST nodes with decorators attached

**Decorator semantics**:
- Decorators are **not** evaluated at parse time
- They're stored as simple name strings in the AST
- Semantic analysis resolves decorator names and validates their usage
- Code generation translates decorators to C# attributes (e.g., `@static` → `[Static]`)

**Common decorators in Sharpy**:
- `@static` - Static method (no `self` parameter)
- `@override` - Method overrides base class method
- `@abstract` - Abstract method declaration
- `@property` - Property accessor
- Custom decorators defined by users

---

## 4. Helper Methods (Defined in Partial Class Files)

While `Parser.cs` contains the core infrastructure, many helper methods are defined in the partial class files. Here's a quick reference:

### Navigation and Consumption
- **`Advance()`**: Move to next token, return previous token (in `Parser.Expressions.cs`)
- **`SkipNewlines()`**: Skip all consecutive newline tokens (in `Parser.Statements.cs`)
- **`Match(TokenType... types)`**: Check if current token matches any type (in `Parser.Expressions.cs`)

### Expectation and Validation
- **`Expect(TokenType type)`**: Consume token or throw error (in `Parser.Statements.cs`)
- **`ExpectNewline()`**: Expect newline or EOF (in `Parser.Statements.cs`)
- **`ExpectIndent()`**: Expect indentation increase (in `Parser.Statements.cs`)
- **`ExpectDedent()`**: Expect indentation decrease (in `Parser.Statements.cs`)

### Statement Parsing
See [`Parser.Statements.cs`](Parser.Statements.md) for:
- `ParseIfStatement()`, `ParseWhileStatement()`, `ParseForStatement()`
- `ParseReturnStatement()`, `ParseRaiseStatement()`, `ParseAssertStatement()`
- `ParseBreakStatement()`, `ParseContinueStatement()`, `ParsePassStatement()`
- `ParseImportStatement()`, `ParseFromImportStatement()`
- `ParseSimpleStatement()` (handles expression statements, assignments)
- `ParseBlock()` (indented code blocks)

### Expression Parsing
See [`Parser.Expressions.cs`](Parser.Expressions.md) for:
- `ParseExpression()` - Entry point for expression parsing
- `ParseOrExpression()`, `ParseAndExpression()` - Logical operators
- `ParseNotExpression()` - Logical not
- `ParseComparisonExpression()` - Comparison chains
- `ParseAdditionExpression()`, `ParseMultiplicationExpression()` - Arithmetic
- `ParseUnaryExpression()` - Unary operators
- `ParsePostfixExpression()` - Calls, indexing, member access

### Primary Parsing
See [`Parser.Primaries.cs`](Parser.Primaries.md) for:
- `ParsePrimary()` - Entry point for atomic expressions
- `ParseNumberLiteral()`, `ParseStringLiteral()`, `ParseBoolLiteral()`
- `ParseIdentifier()`, `ParseParenthesizedExpression()`
- `ParseListLiteral()`, `ParseDictLiteral()`, `ParseSetLiteral()`
- `ParseLambda()`, `ParseListComprehension()`

### Definition Parsing
See [`Parser.Definitions.cs`](Parser.Definitions.md) for:
- `ParseFunctionDef()` - Function definitions
- `ParseClassDef()`, `ParseStructDef()`, `ParseInterfaceDef()`
- `ParseEnumDef()`, `ParseTypeAlias()`
- `ParseParameters()` - Function parameter lists
- `ParseFieldDeclaration()` - Class/struct fields

### Type Parsing
See [`Parser.Types.cs`](Parser.Types.md) for:
- `ParseTypeAnnotation()` - Entry point for type parsing
- `ParseGenericType()`, `ParseUnionType()`, `ParseOptionalType()`
- `ParseListType()`, `ParseDictType()`, `ParseTupleType()`
- `ParseFunctionType()` - Function type signatures

---

## 5. Dependencies

### Internal Dependencies

**From `Sharpy.Compiler.Lexer`**:
- `Token` - Token data structure with type, value, and location
- `TokenType` - Enum of all token types (Keywords, Operators, Literals, etc.)

**From `Sharpy.Compiler.Parser.Ast`**:
- `Module` - Root AST node representing a source file
- `Statement` - Base class for all statement nodes
- `Expression` - Base class for all expression nodes
- `Decorator` - Decorator metadata attached to definitions
- Specific AST node types (`FunctionDef`, `ClassDef`, `IfStatement`, etc.)

**From `Sharpy.Compiler.Logging`**:
- `ICompilerLogger` - Interface for logging diagnostics
- `NullLogger` - No-op logger implementation (default)

### Upstream Component

**Lexer** (`src/Sharpy.Compiler/Lexer/Lexer.cs`):
- Provides the token stream
- Handles tokenization, indentation tracking, and basic syntax validation
- See [Lexer.md](../Lexer/Lexer.md) for details

### Downstream Components

**Semantic Analysis**:
- Receives the AST and performs type checking, name resolution, etc.
- Not yet fully documented

**Code Generator** (`src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`):
- Walks the AST and emits C# code via Roslyn
- See [RoslynEmitter.md](../CodeGen/RoslynEmitter.md) for details

---

## 6. Patterns and Design Decisions

### 6.1 Recursive Descent Parsing

The parser uses **recursive descent** where each grammar rule becomes a function:

```
Statement     → "if" Expression ":" Block
              | "while" Expression ":" Block
              | ...

Expression    → OrExpression

OrExpression  → AndExpression ("or" AndExpression)*

AndExpression → NotExpression ("and" NotExpression)*
```

Each rule is a method:
- `ParseStatement()` → handles statement alternatives
- `ParseExpression()` → entry point for expressions
- `ParseOrExpression()` → handles `or` operators
- `ParseAndExpression()` → handles `and` operators

**Benefits**:
- Direct mapping from grammar to code
- Easy to understand and maintain
- Natural support for nested structures

### 6.2 Precedence Climbing for Expressions

Expression parsing uses **precedence climbing** (a.k.a. operator-precedence parsing):

```
ParseExpression()          # Lowest precedence
  → ParseOrExpression()
    → ParseAndExpression()
      → ParseComparisonExpression()
        → ParseAdditionExpression()
          → ParseMultiplicationExpression()
            → ParseUnaryExpression()
              → ParsePostfixExpression()
                → ParsePrimary()  # Highest precedence
```

**Each level handles operators of the same precedence**:
- `ParseOrExpression()` handles `or`
- `ParseAndExpression()` handles `and`
- `ParseComparisonExpression()` handles `==`, `!=`, `<`, `>`, `<=`, `>=`, `in`, `is`
- `ParseAdditionExpression()` handles `+`, `-`
- `ParseMultiplicationExpression()` handles `*`, `/`, `//`, `%`, `**`
- `ParseUnaryExpression()` handles `-`, `+`, `not`
- `ParsePostfixExpression()` handles `()`, `[]`, `.`

**Benefits**:
- Correct operator precedence automatically
- Easy to add new operators (just add at the right level)
- No separate precedence table needed

### 6.3 Lookahead Instead of Backtracking

The parser uses **lookahead** (via `Peek()`) to disambiguate syntax without backtracking:

```csharp
// Disambiguate try statement vs try expression
if (Current.Type == TokenType.Try)
{
    if (Peek().Type == TokenType.Colon)  // Look ahead one token
        return ParseTryStatement();      // try:
    else
        return ParseSimpleStatement();   // try expr
}
```

**Benefits**:
- Single-pass parsing (efficient)
- Predictable performance (no exponential backtracking)
- Simpler mental model

**Tradeoff**: Some grammar ambiguities require manual disambiguation logic.

### 6.4 Immutable AST with Records

All AST nodes are C# records (immutable data classes):

```csharp
public record FunctionDef : Statement
{
    public required string Name { get; init; }
    public required List<Parameter> Parameters { get; init; }
    public required TypeAnnotation? ReturnType { get; init; }
    public required List<Statement> Body { get; init; }
    public List<Decorator> Decorators { get; init; } = new();
    ...
}
```

**Benefits**:
- Thread-safe (no mutation races)
- Easy to clone with modifications (`func with { Decorators = decorators }`)
- Value equality by default
- Clear intent (initialization only)

### 6.5 Source Location Tracking

Every AST node includes source location:

```csharp
public abstract record AstNode
{
    public required int LineStart { get; init; }
    public required int ColumnStart { get; init; }
    public required int LineEnd { get; init; }
    public required int ColumnEnd { get; init; }
}
```

**Used for**:
- Error messages showing exact source location
- IDE features (go-to-definition, hover tooltips)
- Source maps for debugging compiled code

**Pattern**: Track start location before parsing, end location after:
```csharp
var startLine = Current.Line;
var startColumn = Current.Column;
// ... parsing logic ...
var endLine = Previous.Line;
var endColumn = Previous.Column + Previous.Value.Length;
```

### 6.6 Partial Classes for Organization

The `Parser` class is split across multiple files using `partial class`:

```
Parser.cs             - Core infrastructure (this file)
Parser.Statements.cs  - Statement parsing
Parser.Expressions.cs - Expression parsing
Parser.Primaries.cs   - Primary expressions
Parser.Definitions.cs - Type/function definitions
Parser.Types.cs       - Type annotations
```

**Benefits**:
- Better organization (group related functionality)
- Easier navigation (smaller files)
- Parallel development (multiple people can work on different files)
- Logical separation of concerns

**Tradeoff**: Need to understand which file contains which methods.

---

## 7. Error Handling

### Parser Errors

The parser throws `ParserError` exceptions on syntax errors:

```csharp
if (Current.Type != TokenType.Identifier)
    throw new ParserError("Expected identifier", Current.Line, Current.Column);
```

**Error information includes**:
- Human-readable message
- Line and column numbers
- Context about what was expected

**Design decision**: **No error recovery**. The parser fails fast on the first error. This is appropriate for a batch compiler but would need enhancement for IDE scenarios where you want to show multiple errors and provide partial results.

### Common Error Scenarios

1. **Unexpected token**:
   ```csharp
   throw new ParserError($"Expected {expected}, got {Current.Type}", Current.Line, Current.Column);
   ```

2. **Missing delimiter**:
   ```csharp
   if (Current.Type != TokenType.RightParen)
       throw new ParserError("Expected ')'", Current.Line, Current.Column);
   ```

3. **Invalid syntax**:
   ```csharp
   throw new ParserError("Invalid syntax in parameter list", Current.Line, Current.Column);
   ```

### Error Message Quality

Good error messages are essential for developer experience:

```csharp
// Good: Specific about what's wrong
throw new ParserError("Decorators can only be applied to functions, classes, or structs", ...);

// Bad: Vague
throw new ParserError("Syntax error", ...);
```

---

## 8. Debugging Tips

### 8.1 Understanding Parse Failures

When the parser fails:

1. **Check the token stream first**:
   ```csharp
   // Add logging to see what tokens the parser is seeing
   _logger.LogInfo($"Current token: {Current.Type} = '{Current.Value}' at {Current.Line}:{Current.Column}");
   ```

2. **Use AstDumper to visualize partial results**:
   ```csharp
   var module = parser.ParseModule();
   var dumper = new AstDumper();
   Console.WriteLine(dumper.Dump(module));
   ```

3. **Check indentation**: Many parsing issues are actually indentation issues caught by the lexer

### 8.2 Adding Debug Logging

To trace parsing decisions, add logging in key methods:

```csharp
private Statement ParseStatement()
{
    _logger.LogDebug($"ParseStatement: Current token = {Current.Type}");

    // ... rest of method
}
```

Then compile with a verbose logger:

```csharp
var logger = new ConsoleLogger(LogLevel.Debug);
var parser = new Parser(tokens, logger);
```

### 8.3 Common Pitfalls

1. **Forgetting to consume tokens**: Infinite loops if you don't call `Advance()`
   ```csharp
   // Wrong: Infinite loop
   while (Current.Type == TokenType.Comma) {
       // Forgot to Advance()!
   }

   // Right:
   while (Current.Type == TokenType.Comma) {
       Advance();
       // ...
   }
   ```

2. **Off-by-one in Peek()**: Remember `Peek(0)` is the current token, `Peek(1)` is the next
   ```csharp
   // Wrong: This is the current token, not the next
   if (Peek(0).Type == TokenType.Colon) ...

   // Right: Look ahead by 1
   if (Peek(1).Type == TokenType.Colon) ...
   // Or just use Peek() which defaults to offset=1
   if (Peek().Type == TokenType.Colon) ...
   ```

3. **Location tracking errors**: Capture start location BEFORE consuming tokens
   ```csharp
   // Wrong: Start location is wrong
   Advance();
   var startLine = Current.Line;

   // Right: Capture before advancing
   var startLine = Current.Line;
   Advance();
   ```

### 8.4 Testing Strategy

**Unit tests**: Test individual parsing methods with crafted token streams

**Integration tests**: Test `ParseModule()` with real source code strings

**Example test pattern**:
```csharp
[Test]
public void TestParseFunctionDef()
{
    var source = "def foo(x: int) -> str:\n    return str(x)";
    var lexer = new Lexer(source);
    var tokens = lexer.Tokenize();
    var parser = new Parser(tokens);
    var module = parser.ParseModule();

    Assert.That(module.Body, Has.Count.EqualTo(1));
    Assert.That(module.Body[0], Is.InstanceOf<FunctionDef>());
    var func = (FunctionDef)module.Body[0];
    Assert.That(func.Name, Is.EqualTo("foo"));
    // ... more assertions
}
```

---

## 9. Contribution Guidelines

### 9.1 Adding New Statement Types

To add a new statement type (e.g., `with` statement):

1. **Add token type** in `Lexer/TokenType.cs`:
   ```csharp
   public enum TokenType {
       ...
       With,
       ...
   }
   ```

2. **Add keyword** in `Lexer/Lexer.cs`:
   ```csharp
   private static readonly Dictionary<string, TokenType> Keywords = new() {
       ...
       { "with", TokenType.With },
       ...
   };
   ```

3. **Define AST node** in `Parser/Ast/Statement.cs`:
   ```csharp
   public record WithStatement : Statement
   {
       public required Expression ContextExpr { get; init; }
       public required string? TargetName { get; init; }
       public required List<Statement> Body { get; init; }
   }
   ```

4. **Add parser method** in `Parser.Statements.cs`:
   ```csharp
   private Statement ParseWithStatement()
   {
       var startLine = Current.Line;
       var startColumn = Current.Column;
       Advance(); // consume 'with'

       var expr = ParseExpression();

       string? targetName = null;
       if (Current.Type == TokenType.As) {
           Advance();
           if (Current.Type != TokenType.Identifier)
               throw new ParserError("Expected identifier after 'as'", ...);
           targetName = Current.Value;
           Advance();
       }

       Expect(TokenType.Colon);
       ExpectNewline();
       var body = ParseBlock();

       return new WithStatement {
           ContextExpr = expr,
           TargetName = targetName,
           Body = body,
           LineStart = startLine,
           ColumnStart = startColumn,
           LineEnd = Previous.Line,
           ColumnEnd = Previous.Column
       };
   }
   ```

5. **Update dispatcher** in `Parser.cs`:
   ```csharp
   private Statement ParseStatement()
   {
       return Current.Type switch
       {
           ...
           TokenType.With => ParseWithStatement(),
           ...
       };
   }
   ```

6. **Update code generator** in `CodeGen/RoslynEmitter.cs`:
   ```csharp
   private StatementSyntax EmitStatement(Statement stmt)
   {
       return stmt switch
       {
           ...
           WithStatement withStmt => EmitWithStatement(withStmt),
           ...
       };
   }
   ```

7. **Add tests**: Create test cases in `Sharpy.Compiler.Tests`

### 9.2 Adding New Expression Types

Similar process, but:
- Define AST node in `Parser/Ast/Expression.cs`
- Add parser method in `Parser.Expressions.cs` or `Parser.Primaries.cs`
- Update expression parsing at the appropriate precedence level
- Update code generator

### 9.3 Adding New Operators

To add a new binary operator (e.g., `>>` for bit shift):

1. Add token type: `TokenType.RightShift`
2. Add lexer support (may already exist)
3. Add to operator precedence level in `Parser.Expressions.cs`:
   ```csharp
   private Expression ParseShiftExpression()
   {
       var left = ParseAdditionExpression();
       while (Current.Type is TokenType.LeftShift or TokenType.RightShift)
       {
           var op = Current.Type == TokenType.LeftShift
               ? BinaryOperator.LeftShift
               : BinaryOperator.RightShift;
           Advance();
           var right = ParseAdditionExpression();
           left = new BinaryOp { Left = left, Operator = op, Right = right, ... };
       }
       return left;
   }
   ```
4. Update precedence climbing chain to call `ParseShiftExpression()` at the right level
5. Update code generator to emit corresponding C# code

### 9.4 Code Style Guidelines

**When working on the parser**:
- **Follow existing patterns**: Match the style of similar parsing methods
- **Use descriptive variable names**: `funcName`, not `n`
- **Add location tracking**: Every AST node needs `LineStart`, `ColumnStart`, `LineEnd`, `ColumnEnd`
- **Write clear error messages**: Say what was expected and what was found
- **Add XML documentation comments** for public methods
- **Log important decisions** when helpful for debugging
- **Keep methods focused**: Extract helpers if a method gets too long (>50 lines)
- **Update cross-references**: If you add a new partial class file, update this document

**Testing**:
- Add unit tests for new parsing methods
- Add integration tests with real source code
- Test error cases (what happens with invalid syntax?)
- Run the full test suite before submitting changes

---

## 10. Performance Characteristics

### Time Complexity

- **Overall**: O(n) where n = number of tokens
- **Each token is visited exactly once** during parsing
- **No backtracking** means predictable performance
- **Lookahead** is O(1) (direct array access)

### Space Complexity

- **Token storage**: O(n) for input token list
- **AST storage**: O(n) for output AST
- **Call stack**: O(d) where d = maximum nesting depth of expressions/blocks

### Performance Bottlenecks

1. **List growth**: AST node lists (statements, parameters, etc.) may resize multiple times
   - **Mitigation**: Could pre-allocate lists if counts are known

2. **String allocations**: Token values are strings (immutable)
   - **Acceptable**: Strings are cached in the lexer

3. **Exception overhead**: `ParserError` uses exceptions for error handling
   - **Acceptable**: Exceptions are rare in valid code

**Overall**: The parser is fast enough for interactive use. A typical 1000-line file parses in <10ms.

---

## 11. Future Enhancements

### 11.1 Error Recovery

**Current**: Parser fails on first error.

**Future**: Add error recovery to continue parsing after errors (useful for IDEs).

**Approach**:
- On error, synchronize to a known point (e.g., next statement)
- Insert a placeholder node (e.g., `ErrorStatement`)
- Continue parsing
- Collect all errors instead of throwing on first

**Benefits**:
- Show multiple errors in one compilation
- Provide partial AST for IDE features (syntax highlighting, code completion)

### 11.2 Incremental Parsing

**Current**: Parse the entire file every time.

**Future**: Re-parse only changed portions (useful for IDEs with live error checking).

**Approach**:
- Track which tokens correspond to which AST nodes
- On edit, invalidate affected subtree
- Reuse unchanged nodes

**Challenge**: Requires careful cache invalidation.

### 11.3 Better Diagnostics

**Current**: Simple error messages with line/column.

**Future**: Rich diagnostics with:
- Syntax highlighting of problematic code
- Suggestions for fixes
- Links to documentation

**Example**:
```
error: Expected ':' after function signature
  |
3 | def foo(x: int)
  |                ^ insert ':' here
  |
help: Function definitions require a colon before the body
```

### 11.4 Macro System

**Idea**: Allow compile-time code generation with a macro syntax.

**Parsing challenge**: Macros expand before semantic analysis, so parser must support deferred expansion.

**Example**:
```python
@macro
def repeat(n):
    # Generate n copies of the decorated block
    ...

@repeat(5)
def test_case_1():
    assert foo()
```

---

## 12. Related Documentation

### Partial Class Files (This Parser)

- **[`Parser.Statements.cs`](Parser.Statements.md)** - Control flow and simple statements
- **[`Parser.Expressions.cs`](Parser.Expressions.md)** - Expression parsing with precedence
- **[`Parser.Primaries.cs`](Parser.Primaries.md)** - Literals and primary expressions
- **[`Parser.Definitions.cs`](Parser.Definitions.md)** - Functions, classes, structs
- **[`Parser.Types.cs`](Parser.Types.md)** - Type annotations

### Supporting Files

- **[`AstDumper.cs`](AstDumper.md)** - Utility for visualizing AST as text
- **[`ParserError.cs`](ParserError.md)** - Exception type for parse errors
- **[AST Node Definitions](Ast/)** - Statement and Expression record types

### Upstream/Downstream

- **[`Lexer.cs`](../Lexer/Lexer.md)** - Tokenization (upstream)
- **[`Token.cs`](../Lexer/Token.md)** - Token data structure
- **[`RoslynEmitter.cs`](../CodeGen/RoslynEmitter.md)** - Code generation (downstream)
- **[`Compiler.cs`](../Compiler.md)** - Orchestrates the compilation pipeline

---

## 13. Summary

The `Parser.cs` file is the **core orchestrator** of the Sharpy parser. It provides:

1. **Entry point** (`ParseModule()`) - Parses an entire source file
2. **Statement dispatcher** (`ParseStatement()`) - Routes to specific statement parsers
3. **Decorator support** (`ParseDecoratedStatement()`) - Handles `@decorator` syntax
4. **Infrastructure** - Token navigation, location tracking, logging
5. **Partial class coordination** - Ties together parsing logic across multiple files

**Key design principles:**
- **Single-pass parsing** with lookahead (no backtracking)
- **Recursive descent** for clear grammar mapping
- **Precedence climbing** for expression parsing
- **Immutable AST** using C# records
- **Complete location tracking** for error reporting
- **Modular organization** via partial classes

**When working with the parser:**
- Start by reading this file to understand the overall structure
- Dive into specific partial class files for detailed parsing logic
- Use `AstDumper` to visualize AST output
- Add logging to trace parsing decisions
- Follow existing patterns when adding new features

**The parser is the bridge between raw source text and semantic meaning** - it enforces Sharpy's grammar and creates the structured representation that enables type checking, optimization, and code generation.

---

**Happy parsing!** 🚀
