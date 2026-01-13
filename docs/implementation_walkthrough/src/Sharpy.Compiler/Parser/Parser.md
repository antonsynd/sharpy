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

---

## 2. Class Structure

### Core State

```csharp
public class Parser
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

### Helper Properties

```csharp
private Token Current => _position < _tokens.Count ? _tokens[_position] : _tokens[^1];
private Token Previous => _position > 0 ? _tokens[_position - 1] : _tokens[0];
private Token Peek(int offset = 1) => ...
private bool IsAtEnd => Current.Type == TokenType.Eof;
```

These properties provide clean navigation through the token stream:
- `Current`: Token at current position (safe against overflow)
- `Previous`: Last consumed token (useful for end location tracking)
- `Peek(n)`: Look ahead `n` tokens without consuming
- `IsAtEnd`: Check if we've reached end-of-file

---

## 3. Key Methods

### 3.1 Entry Point

#### `ParseModule()`

**Purpose**: Parse an entire Sharpy source file into a `Module` AST node.

**Algorithm**:
1. Skip leading newlines
2. Check for optional module docstring (first string literal)
3. Parse statements until EOF
4. Return `Module` node containing all statements

```csharp
public Module ParseModule()
{
    var statements = new List<Statement>();
    string? docString = null;

    SkipNewlines();

    // Module-level docstring
    if (Current.Type == TokenType.String) {
        docString = Current.Value;
        Advance();
    }

    while (!IsAtEnd) {
        statements.Add(ParseStatement());
        SkipNewlines();
    }

    return new Module { Body = statements, DocString = docString, ... };
}
```

**When to use**: This is called once per source file by the compiler's orchestration layer.

---

### 3.2 Statement Parsing

#### `ParseStatement()`

**Purpose**: Dispatcher that routes to specific statement parsers based on current token.

**Design pattern**: Uses C# switch expressions for clean, efficient dispatching:

```csharp
private Statement ParseStatement()
{
    if (Current.Type == TokenType.At)
        return ParseDecoratedStatement();  // @decorator

    return Current.Type switch
    {
        TokenType.Def => ParseFunctionDef(),
        TokenType.Class => ParseClassDef(),
        TokenType.Struct => ParseStructDef(),
        TokenType.Interface => ParseInterfaceDef(),
        TokenType.Enum => ParseEnumDef(),
        TokenType.If => ParseIfStatement(),
        TokenType.While => ParseWhileStatement(),
        TokenType.For => ParseForStatement(),
        TokenType.Try => ParseTryStatement(),
        TokenType.Return => ParseReturnStatement(),
        TokenType.Raise => ParseRaiseStatement(),
        TokenType.Assert => ParseAssertStatement(),
        TokenType.Pass => ParsePassStatement(),
        TokenType.Break => ParseBreakStatement(),
        TokenType.Continue => ParseContinueStatement(),
        TokenType.Import => ParseImportStatement(),
        TokenType.From => ParseFromImportStatement(),
        TokenType.Const => ParseConstDeclaration(),
        _ => ParseSimpleStatement()  // Assignments, expressions, declarations
    };
}
```

**Important**: The default case `ParseSimpleStatement()` handles the most complex logic since it must disambiguate between:
- Variable declarations (`x: int = 5`)
- Assignments (`x = 5`, `x += 5`)
- Tuple unpacking (`x, y = a, b`)
- Expression statements (`print(42)`)

---

#### `ParseSimpleStatement()` - The Tricky One

This method deserves special attention as it handles multiple overlapping syntaxes:

**Algorithm**:
1. Parse an expression
2. Check for comma → Could be tuple unpacking: `x, y = ...`
3. Check for assignment operator → Assignment: `x = ...`
4. Check for colon → Variable declaration: `x: int = ...`
5. Otherwise → Expression statement: `func()`

```csharp
private Statement ParseSimpleStatement()
{
    var expr = ParseExpression();

    // Tuple unpacking: x, y = ...
    if (Current.Type == TokenType.Comma) {
        var elements = new List<Expression> { expr };
        while (Current.Type == TokenType.Comma) {
            Advance();
            elements.Add(ParseExpression());
        }

        if (IsAssignmentOperator(Current.Type)) {
            var tuple = new TupleLiteral { Elements = elements };
            // ... create Assignment node
        }
    }

    // Assignment: x = value
    if (IsAssignmentOperator(Current.Type)) {
        // ... create Assignment node
    }

    // Type annotation: x: int = value
    if (Current.Type == TokenType.Colon) {
        // ... create VariableDeclaration node
    }

    // Expression statement
    return new ExpressionStatement { Expression = expr };
}
```

**Debugging tip**: If you see unexpected parse errors with assignments or declarations, add logging here to see which branch is taken.

---

#### Assignment Operators

The parser supports a full range of compound assignment operators via `TokenTypeToAssignmentOperator()`:

```csharp
private AssignmentOperator TokenTypeToAssignmentOperator(TokenType type) => type switch
{
    TokenType.Assign => AssignmentOperator.Assign,           // =
    TokenType.PlusAssign => AssignmentOperator.PlusAssign,   // +=
    TokenType.MinusAssign => AssignmentOperator.MinusAssign, // -=
    TokenType.StarAssign => AssignmentOperator.StarAssign,   // *=
    TokenType.SlashAssign => AssignmentOperator.SlashAssign, // /=
    TokenType.DoubleSlashAssign => AssignmentOperator.DoubleSlashAssign, // //=
    TokenType.PercentAssign => AssignmentOperator.PercentAssign, // %=
    TokenType.DoubleStarAssign => AssignmentOperator.PowerAssign, // **=
    TokenType.AmpersandAssign => AssignmentOperator.AndAssign,    // &=
    TokenType.PipeAssign => AssignmentOperator.OrAssign,          // |=
    TokenType.CaretAssign => AssignmentOperator.XorAssign,        // ^=
    TokenType.LeftShiftAssign => AssignmentOperator.LeftShiftAssign,   // <<=
    TokenType.RightShiftAssign => AssignmentOperator.RightShiftAssign, // >>=
    _ => throw new ParserError(...)
};
```

---

#### `ParseFunctionDef()`

**Purpose**: Parse function definitions.

**Sharpy syntax**:
```python
def function_name(param1: type1, param2: type2 = default) -> return_type:
    """Optional docstring"""
    # function body
```

**Key steps**:
1. Consume `def` keyword
2. Parse function name
3. Parse parameter list (with types and defaults)
4. Parse optional return type annotation (`-> type`)
5. Expect colon and newline
6. Parse indented body block
7. Check for optional docstring (first statement if it's a string)

```csharp
private FunctionDef ParseFunctionDef()
{
    Expect(TokenType.Def);
    var name = ExpectIdentifier();
    Expect(TokenType.LeftParen);
    var parameters = ParseParameters();
    Expect(TokenType.RightParen);

    TypeAnnotation? returnType = null;
    if (Current.Type == TokenType.Arrow) {
        Advance();
        returnType = ParseTypeAnnotation();
    }

    Expect(TokenType.Colon);
    ExpectNewline();
    Expect(TokenType.Indent);

    // Check for docstring
    string? docString = null;
    if (Current.Type == TokenType.String) {
        docString = Current.Value;
        Advance();
        SkipNewlines();
    }

    var body = ParseBlock();
    Expect(TokenType.Dedent);

    return new FunctionDef { Name = name, Parameters = parameters, ... };
}
```

**Important**: The Lexer handles indentation tracking (INDENT/DEDENT tokens), so the parser doesn't need to count spaces—it just expects INDENT/DEDENT at appropriate places.

---

#### `ParseClassDef()` and `ParseStructDef()`

These follow similar patterns to `ParseFunctionDef()` but handle:

**Generic type parameters**:
```python
class MyClass[T, U]:  # Type parameters in brackets
    pass
```

**Base classes/interfaces**:
```python
class MyClass(BaseClass, Interface1, Interface2):
    pass

struct MyStruct(IComparable, IEquatable):  # Structs can only implement interfaces
    pass
```

**Parsing flow**:
1. Consume `class` or `struct` keyword
2. Parse name
3. Parse optional type parameters `[T, U]`
4. Parse optional base classes/interfaces `(...)`
5. Parse body with docstring

---

#### `ParseInterfaceDef()`

**Purpose**: Parse interface definitions.

**Sharpy syntax**:
```python
interface IMyInterface[T](IBaseInterface1, IBaseInterface2):
    """Optional docstring"""
    def method(x: int) -> str:
        pass
```

Similar to class/struct parsing but stored in an `InterfaceDef` AST node with `BaseInterfaces` instead of `BaseClasses`.

---

#### `ParseEnumDef()`

**Purpose**: Parse enumeration definitions.

**Sharpy syntax**:
```python
enum Color:
    """Optional docstring"""
    Red = 1
    Green = 2
    Blue = 3
```

**Key features**:
- Each member can have an optional value assignment
- `pass` statement is allowed for consistency but results in an error if no real members exist
- Validation: enum must have at least one member

```csharp
private EnumDef ParseEnumDef()
{
    Expect(TokenType.Enum);
    var name = ExpectIdentifier();
    Expect(TokenType.Colon);
    ExpectNewline();
    Expect(TokenType.Indent);

    // Check for docstring
    // ...

    var members = new List<EnumMember>();

    while (Current.Type != TokenType.Dedent && !IsAtEnd) {
        // Handle pass statement
        if (Current.Type == TokenType.Pass) {
            Advance();
            ExpectNewline();
            continue;
        }

        var memberName = ExpectIdentifier();
        Expression? value = null;

        if (Current.Type == TokenType.Assign) {
            Advance();
            value = ParseExpression();
        }

        members.Add(new EnumMember { Name = memberName, Value = value, ... });
        ExpectNewline();
    }

    Expect(TokenType.Dedent);

    // Validation: enum must have at least one member
    if (members.Count == 0)
        throw new ParserError($"Enum '{name}' must have at least one member", ...);

    return new EnumDef { Name = name, Members = members, ... };
}
```

---

#### `ParseDecoratedStatement()`

**Purpose**: Parse statements preceded by decorators.

**Sharpy syntax**:
```python
@decorator1
@decorator2
def my_function():
    pass

@singleton
class MyClass:
    pass
```

**Algorithm**:
1. Collect all consecutive `@decorator` lines
2. Parse the decorated definition (function, class, or struct)
3. Attach decorators to the definition using record `with` expressions

```csharp
private Statement ParseDecoratedStatement()
{
    var decorators = new List<Decorator>();

    while (Current.Type == TokenType.At) {
        Advance();  // Skip @
        if (Current.Type != TokenType.Identifier)
            throw new ParserError("Expected decorator name", ...);

        var decoratorName = Current.Value;
        Advance();

        decorators.Add(new Decorator { Name = decoratorName, ... });
        ExpectNewline();
    }

    // Parse the decorated definition
    Statement stmt = Current.Type switch
    {
        TokenType.Def => ParseFunctionDef(),
        TokenType.Class => ParseClassDef(),
        TokenType.Struct => ParseStructDef(),
        _ => throw new ParserError("Decorators can only be applied to functions, classes, or structs", ...)
    };

    // Attach decorators using record 'with' expressions
    return stmt switch
    {
        FunctionDef func => func with { Decorators = decorators },
        ClassDef cls => cls with { Decorators = decorators },
        StructDef str => str with { Decorators = decorators },
        _ => throw new ParserError("Unexpected decorated statement type", ...)
    };
}
```

---

#### `ParseIfStatement()`

**Purpose**: Parse if/elif/else chains.

**Sharpy syntax**:
```python
if condition1:
    # body
elif condition2:
    # body
else:
    # body
```

**Key challenge**: Handling multiple `elif` clauses without recursion overhead.

```csharp
private IfStatement ParseIfStatement()
{
    Expect(TokenType.If);
    var test = ParseExpression();
    Expect(TokenType.Colon);
    ExpectNewline();
    Expect(TokenType.Indent);
    var thenBody = ParseBlock();
    Expect(TokenType.Dedent);

    var elifClauses = new List<ElifClause>();
    while (Current.Type == TokenType.Elif) {
        Advance();
        var elifTest = ParseExpression();
        // ... parse elif body
        elifClauses.Add(new ElifClause { Test = elifTest, Body = elifBody });
    }

    var elseBody = new List<Statement>();
    if (Current.Type == TokenType.Else) {
        // ... parse else body
    }

    return new IfStatement { Test = test, ThenBody = thenBody, ElifClauses = elifClauses, ElseBody = elseBody };
}
```

**Design note**: `elif` is stored as a separate list rather than nesting `IfStatement` nodes, making codegen simpler.

---

#### `ParseForStatement()`

**Purpose**: Parse for loops with iteration.

**Sharpy syntax**:
```python
for x in iterable:
    # body

for x, y in pairs:  # Tuple unpacking
    # body
```

**Tricky part**: `ParseForTarget()` must parse the target variable(s) without consuming the `in` keyword.

```csharp
private ForStatement ParseForStatement()
{
    Expect(TokenType.For);
    var target = ParseForTarget();  // Special parsing to stop before 'in'
    Expect(TokenType.In);
    var iterator = ParseExpression();
    Expect(TokenType.Colon);
    // ... parse body
}

private Expression ParseForTarget()
{
    // Parse identifier or tuple, stopping before 'in'
    var first = ParsePrimary();

    if (Current.Type == TokenType.Comma) {
        // Tuple unpacking: for x, y in ...
        var elements = new List<Expression> { first };
        while (Current.Type == TokenType.Comma) {
            Advance();
            if (Current.Type == TokenType.In) break;  // Stop before 'in'
            elements.Add(ParsePrimary());
        }
        return new TupleLiteral { Elements = elements };
    }

    return first;
}
```

**Gotcha**: We use `ParsePrimary()` instead of `ParseExpression()` to avoid consuming `in` as a comparison operator.

---

#### `ParseTryStatement()`

**Purpose**: Parse try/except/finally blocks.

**Sharpy syntax**:
```python
try:
    # risky code
except ExceptionType as e:
    # handler
except:  # Catch-all
    # handler
finally:
    # cleanup
```

```csharp
private TryStatement ParseTryStatement()
{
    Expect(TokenType.Try);
    // ... parse try body

    var handlers = new List<ExceptHandler>();
    while (Current.Type == TokenType.Except) {
        Advance();

        TypeAnnotation? exceptionType = null;
        string? name = null;

        if (Current.Type != TokenType.Colon) {
            exceptionType = ParseTypeAnnotation();
            if (Current.Type == TokenType.As) {
                Advance();
                name = ExpectIdentifier();
            }
        }

        // ... parse handler body
        handlers.Add(new ExceptHandler { ExceptionType = exceptionType, Name = name, Body = handlerBody });
    }

    var finallyBody = new List<Statement>();
    if (Current.Type == TokenType.Finally) {
        // ... parse finally body
    }

    return new TryStatement { Body = body, Handlers = handlers, FinallyBody = finallyBody };
}
```

---

#### `ParseRaiseStatement()`

**Purpose**: Parse exception raising statements.

**Sharpy syntax**:
```python
raise                           # Re-raise current exception
raise ValueError("message")     # Raise new exception
raise NewError() from original  # Exception chaining
```

```csharp
private RaiseStatement ParseRaiseStatement()
{
    Expect(TokenType.Raise);

    Expression? exception = null;
    Expression? cause = null;

    if (Current.Type != TokenType.Newline && !IsAtEnd) {
        exception = ParseExpression();

        // raise ... from cause
        if (Current.Type == TokenType.From) {
            Advance();
            cause = ParseExpression();
        }
    }

    ExpectNewline();

    return new RaiseStatement { Exception = exception, Cause = cause, ... };
}
```

---

#### `ParseConstDeclaration()`

**Purpose**: Parse constant variable declarations.

**Sharpy syntax**:
```python
const PI: float = 3.14159
const MAX_SIZE: int = 100
```

```csharp
private VariableDeclaration ParseConstDeclaration()
{
    Expect(TokenType.Const);
    var name = ExpectIdentifier();
    Expect(TokenType.Colon);
    var type = ParseTypeAnnotation();
    Expect(TokenType.Assign);
    var value = ParseExpression();
    ExpectNewline();

    return new VariableDeclaration {
        Name = name,
        Type = type,
        InitialValue = value,
        IsConst = true,
        ...
    };
}
```

---

#### `ParseImportStatement()` and `ParseFromImportStatement()`

**Purpose**: Parse import declarations.

**Sharpy syntax**:
```python
import module1, module2.submodule as alias
from module import name1, name2 as alias
from module import *
```

**Helper**: `ParseDottedName()` handles `module.submodule.name` sequences.

```csharp
private string ParseDottedName()
{
    var parts = new List<string> { ExpectIdentifier() };

    while (Current.Type == TokenType.Dot) {
        Advance();
        parts.Add(ExpectIdentifier());
    }

    return string.Join(".", parts);  // "System.Collections.Generic"
}
```

---

### 3.3 Expression Parsing (Precedence Climbing)

**Design pattern**: The parser uses **precedence climbing** to handle operator precedence correctly without explicit precedence tables.

#### Expression Precedence Hierarchy (Lowest to Highest)

1. **Conditional**: `x if test else y`
2. **Null coalesce**: `??`
3. **Logical OR**: `or`
4. **Logical AND**: `and`
5. **Logical NOT**: `not`
6. **Comparison**: `==`, `!=`, `<`, `<=`, `>`, `>=`, `in`, `is`
7. **Pipe forward**: `|>` (functional pipeline operator)
8. **Bitwise OR**: `|`
9. **Bitwise XOR**: `^`
10. **Bitwise AND**: `&`
11. **Shift**: `<<`, `>>`
12. **Additive**: `+`, `-`
13. **Multiplicative**: `*`, `/`, `//`, `%`
14. **Unary**: `+x`, `-x`, `~x`, `not x`
15. **Power**: `**` (right-associative!)
16. **Postfix**: `.member`, `[index]`, `(call)`, `as Type`, `to Type`
17. **Primary**: literals, identifiers, parentheses, collections

**Call chain example**:
```
ParseExpression()
  → ParseConditionalExpression()
    → ParseNullCoalesce()
      → ParseLogicalOr()
        → ParseLogicalAnd()
          → ... [descending precedence]
            → ParsePrimary()
```

---

#### `ParseExpression()`

Entry point for expression parsing—just delegates to `ParseConditionalExpression()`.

---

#### `ParseConditionalExpression()`

**Purpose**: Parse ternary conditional expressions.

**Sharpy syntax**: `value if test else alternative` (Python-style)

```csharp
private Expression ParseConditionalExpression()
{
    var expr = ParseNullCoalesce();  // Parse higher-precedence expression first

    if (Current.Type == TokenType.If) {
        Advance();
        var test = ParseNullCoalesce();
        Expect(TokenType.Else);
        var elseValue = ParseConditionalExpression();  // Right-recursive

        return new ConditionalExpression {
            Test = test,
            ThenValue = expr,      // Note: value comes BEFORE test in syntax
            ElseValue = elseValue
        };
    }

    return expr;
}
```

**Why this works**: `value if test else alternative` parses as:
1. Parse `value` (the then-value)
2. See `if`, so it's a conditional
3. Parse `test` (the condition)
4. Parse `alternative` (the else-value, recursively for chaining)

---

#### `ParseLogicalOr()`, `ParseLogicalAnd()`, etc.

**Pattern**: All binary operators at the same precedence level use a similar left-associative loop:

```csharp
private Expression ParseBinaryOperator()
{
    var left = ParseNextHigherPrecedence();

    while (Current.Type == TargetOperatorToken) {
        Advance();
        var right = ParseNextHigherPrecedence();

        left = new BinaryOp {
            Operator = ...,
            Left = left,
            Right = right
        };
    }

    return left;
}
```

This naturally creates **left-associative** parsing: `a + b + c` becomes `(a + b) + c`.

---

#### `ParsePipe()`

**Purpose**: Parse the pipe forward operator (`|>`).

**Sharpy syntax**:
```python
data |> transform |> filter |> output
```

The pipe forward operator enables functional-style data transformation pipelines. It has lower precedence than bitwise operations but higher than comparisons.

```csharp
private Expression ParsePipe()
{
    var left = ParseBitwiseOr();

    while (Current.Type == TokenType.PipeForward) {
        Advance();
        var right = ParseBitwiseOr();

        left = new BinaryOp {
            Operator = BinaryOperator.PipeForward,
            Left = left,
            Right = right,
            ...
        };
    }

    return left;
}
```

---

#### `ParseComparison()` - Special Cases

**Purpose**: Parse comparison operators, including chained comparisons.

**Sharpy features**:
- **Chained comparisons**: `a < b < c` means `a < b and b < c`
- **Multi-token operators**: `is not`, `not in`
- **Type checks**: `x is SomeType` (checked via lookahead)

```csharp
private Expression ParseComparison()
{
    var left = ParsePipe();

    // Special case: "is Type" for isinstance checks
    if (Current.Type == TokenType.Is && Peek(1).Type == TokenType.Identifier) {
        if (IsTypeName(Peek(1).Value)) {
            Advance();  // skip 'is'
            var type = ParseTypeAnnotation();
            return new TypeCheck { Value = left, CheckType = type };
        }
    }

    // Regular comparison chain
    var operators = new List<ComparisonOperator>();
    var operands = new List<Expression> { left };

    while (IsComparisonOperator(Current.Type)) {
        var op = Current.Type;
        Advance();

        // Handle multi-token operators
        if (op == TokenType.Is && Current.Type == TokenType.Not) {
            Advance();
            operators.Add(ComparisonOperator.IsNot);
        } else if (op == TokenType.Not && Current.Type == TokenType.In) {
            Advance();
            operators.Add(ComparisonOperator.NotIn);
        } else {
            operators.Add(TokenTypeToComparisonOperator(op));
        }

        operands.Add(ParsePipe());
    }

    if (operators.Count == 0)
        return left;

    if (operators.Count == 1) {
        // Single comparison: convert to BinaryOp
        return new BinaryOp { ... };
    }

    // Comparison chain
    return new ComparisonChain { Operands = operands, Operators = operators };
}
```

**Why separate `ComparisonChain`?**
- Easier codegen: `a < b < c` can be efficiently compiled to `.NET` code that evaluates `b` only once
- Clear semantic representation

---

#### `ParsePower()`

**Special case**: Power operator `**` is **right-associative** (unlike most operators).

```csharp
private Expression ParsePower()
{
    var left = ParsePostfix();

    if (Current.Type == TokenType.DoubleStar) {
        Advance();
        var right = ParseUnary();  // Right-recursive!

        return new BinaryOp {
            Operator = BinaryOperator.Power,
            Left = left,
            Right = right
        };
    }

    return left;
}
```

**Result**: `2 ** 3 ** 4` parses as `2 ** (3 ** 4)` (right-to-left), matching Python semantics.

---

#### `ParsePostfix()`

**Purpose**: Parse postfix operations (member access, indexing, calls, type operations).

**Operations**:
- **Member access**: `obj.member`, `obj?.member` (null-conditional)
- **Indexing**: `obj[index]`, `obj[start:stop:step]` (slicing)
- **Function calls**: `func(arg1, arg2, key=value)`
- **Type cast**: `value as TargetType` (safe cast, returns null on failure)
- **Type coercion**: `value to TargetType` (throws on failure, or returns null for nullable types)

```csharp
private Expression ParsePostfix()
{
    var expr = ParsePrimary();

    while (true) {
        if (Current.Type == TokenType.Dot || Current.Type == TokenType.NullConditional) {
            // Member access
            var isNullConditional = (Current.Type == TokenType.NullConditional);
            Advance();
            var member = ExpectIdentifierOrKeyword();  // Allows keywords as member names
            expr = new MemberAccess { Object = expr, Member = member, IsNullConditional = isNullConditional };
        }
        else if (Current.Type == TokenType.LeftBracket) {
            // Indexing or slicing
            Advance();
            var index = ParseSliceOrIndex();
            Expect(TokenType.RightBracket);
            expr = index with { Object = expr };
        }
        else if (Current.Type == TokenType.LeftParen) {
            // Function call with positional and keyword arguments
            Advance();
            var args = new List<Expression>();
            var kwargs = new List<KeywordArgument>();
            // ... parse arguments
            Expect(TokenType.RightParen);
            expr = new FunctionCall { Function = expr, Arguments = args, KeywordArguments = kwargs };
        }
        else if (Current.Type == TokenType.As) {
            // Type cast (safe, returns null on failure)
            Advance();
            var targetType = ParseTypeAnnotation();
            expr = new TypeCast { Value = expr, TargetType = targetType };
        }
        else if (Current.Type == TokenType.To) {
            // Type coercion (throws InvalidCastException on failure for T, returns None for T?)
            Advance();
            var targetType = ParseTypeAnnotation();
            expr = new TypeCoercion { Value = expr, TargetType = targetType };
        }
        else {
            break;
        }
    }

    return expr;
}
```

**Chaining**: This naturally handles chained operations like `obj.method()[0].property`.

---

#### `ExpectIdentifierOrKeyword()`

**Purpose**: Allow keywords as member names in member access expressions.

**Motivation**: .NET types may have members named after Sharpy keywords (e.g., `object.type`, `collection.count`).

```csharp
private string ExpectIdentifierOrKeyword()
{
    if (Current.Type == TokenType.Identifier || IsKeywordToken(Current.Type)) {
        var value = Current.Value;
        Advance();
        return value;
    }
    throw new ParserError($"Expected identifier, got {Current.Type}", ...);
}

private static bool IsKeywordToken(TokenType type)
{
    return type switch
    {
        TokenType.Def or TokenType.Class or TokenType.Struct or TokenType.Interface or
        TokenType.Enum or TokenType.If or TokenType.Else or TokenType.Elif or
        TokenType.While or TokenType.For or TokenType.In or TokenType.Return or
        // ... many more keywords
        TokenType.True or TokenType.False or TokenType.None
            => true,
        _ => false
    };
}
```

---

#### `ParseSliceOrIndex()`

**Purpose**: Disambiguate between indexing and slicing.

**Syntax**:
- Index: `[expr]`
- Slice: `[start:stop]`, `[:stop]`, `[start:]`, `[::step]`, etc.

```csharp
private Expression ParseSliceOrIndex()
{
    Expression? start = null;
    Expression? stop = null;
    Expression? step = null;
    bool isSlice = false;

    // [start:stop:step] or [index]
    if (Current.Type != TokenType.Colon)
        start = ParseExpression();

    if (Current.Type == TokenType.Colon) {
        isSlice = true;
        Advance();

        if (Current.Type != TokenType.Colon && Current.Type != TokenType.RightBracket)
            stop = ParseExpression();

        if (Current.Type == TokenType.Colon) {
            Advance();
            if (Current.Type != TokenType.RightBracket)
                step = ParseExpression();
        }
    }

    if (isSlice)
        return new SliceAccess { Start = start, Stop = stop, Step = step, Object = null! };
    else
        return new IndexAccess { Index = start!, Object = null! };
}
```

**Note**: The `Object` property is filled in by the caller (`ParsePostfix()`).

---

#### `ParsePrimary()`

**Purpose**: Parse the lowest-level expressions (literals, identifiers, collections).

**Handles**:
- **Literals**: integers, floats, strings, booleans, `None`, `...` (ellipsis)
- **Identifiers**: variable names
- **Collections**: lists `[...]`, tuples `(...)`, sets `{...}`, dicts `{k:v}`
- **Empty set**: `{/}` (special Sharpy syntax to distinguish from empty dict)
- **Comprehensions**: `[x for x in items]`, `{x for x in items}`, `{k:v for ...}`
- **Lambda expressions**: `lambda x, y: x + y`
- **F-strings**: `f"Hello {name}"`

**Example: Collection parsing**:
```csharp
case TokenType.LeftBrace:
{
    Advance();

    // Empty set {/} - special Sharpy syntax
    if (Current.Type == TokenType.Slash) {
        Advance();
        Expect(TokenType.RightBrace);
        return new SetLiteral { Elements = new List<Expression>() };
    }

    // Empty dict {}
    if (Current.Type == TokenType.RightBrace) {
        Advance();
        return new DictLiteral { Entries = new List<DictEntry>() };
    }

    var firstExpr = ParseExpression();

    // Dict {key: value, ...}
    if (Current.Type == TokenType.Colon) {
        // ... parse dict literal or dict comprehension
    }
    // Set {elem1, elem2, ...}
    else {
        // ... parse set literal or set comprehension
    }
}
```

**Design note**:
- `{}` is an empty dict (matching Python semantics)
- `{/}` is an empty set (Sharpy-specific, since Python requires `set()`)

---

#### `ParseSegmentedFString()`

**Purpose**: Parse f-strings with interpolated expressions.

**Lexer contract**: The Lexer emits special tokens for f-strings:
- `FStringStart`: Beginning of f-string
- `FStringText`: Literal text segments
- `FStringExprStart`: `{` starting an expression
- `FStringExprEnd`: `}` ending an expression
- `FStringFormatSpec`: Format specification (e.g., `:.2f`)
- `FStringEnd`: End of f-string

```csharp
private FStringLiteral ParseSegmentedFString(int startLine, int startColumn)
{
    var parts = new List<FStringPart>();

    Expect(TokenType.FStringStart);

    while (Current.Type != TokenType.FStringEnd) {
        if (Current.Type == TokenType.FStringText) {
            // Literal text
            parts.Add(new FStringPart { Text = Current.Value });
            Advance();
        }
        else if (Current.Type == TokenType.FStringExprStart) {
            // Interpolated expression
            Advance();
            var expr = ParseExpression();

            string? formatSpec = null;
            if (Current.Type == TokenType.FStringFormatSpec) {
                formatSpec = Current.Value;
                Advance();
            }

            parts.Add(new FStringPart { Expression = expr, FormatSpec = formatSpec });
            Expect(TokenType.FStringExprEnd);
        }
    }

    Expect(TokenType.FStringEnd);

    return new FStringLiteral { Parts = parts };
}
```

**Example**: `f"Hello {name}, you have {count:d} items"` becomes:
- Text part: `"Hello "`
- Expression part: `name` (no format)
- Text part: `", you have "`
- Expression part: `count` (format: `"d"`)
- Text part: `" items"`

---

#### `ParseComprehensionClauses()`

**Purpose**: Parse the clauses in list/set/dict comprehensions.

**Syntax**: `[expr for x in iterable if condition for y in iterable2 ...]`

```csharp
private List<ComprehensionClause> ParseComprehensionClauses()
{
    var clauses = new List<ComprehensionClause>();

    while (true) {
        if (Current.Type == TokenType.For) {
            Advance();
            var target = ParseForTarget();  // Reuse for-loop target parsing
            Expect(TokenType.In);
            var iterator = ParseLogicalOr();  // Use lower precedence to avoid consuming too much

            clauses.Add(new ForClause { Target = target, Iterator = iterator, ... });
        }
        else if (Current.Type == TokenType.If) {
            Advance();
            var condition = ParseLogicalOr();

            clauses.Add(new IfClause { Condition = condition, ... });
        }
        else {
            break;
        }
    }

    return clauses;
}
```

**Important**: Uses `ParseLogicalOr()` instead of `ParseExpression()` to avoid consuming tokens that belong to the enclosing comprehension structure.

---

### 3.4 Type Annotation Parsing

#### `ParseTypeAnnotation()`

**Purpose**: Parse type annotations in variable declarations, parameters, and return types.

**Syntax**:
- Simple types: `int`, `str`, `MyClass`
- Generic types: `list[int]`, `dict[str, int]`
- Nullable types: `int?`, `str?`
- Special: `auto` (type inference), `None` (void return)

```csharp
private TypeAnnotation ParseTypeAnnotation()
{
    // Handle special keywords
    string name;
    if (Current.Type == TokenType.Auto) {
        name = "auto";
        Advance();
    } else if (Current.Type == TokenType.None) {
        name = "None";
        Advance();
    } else {
        name = ExpectIdentifier();
    }

    var typeArgs = new List<TypeAnnotation>();
    bool isNullable = false;

    // Generic type arguments [T, U]
    if (Current.Type == TokenType.LeftBracket) {
        Advance();
        do {
            typeArgs.Add(ParseTypeAnnotation());  // Recursive!
            if (Current.Type == TokenType.Comma)
                Advance();
            else
                break;
        } while (true);
        Expect(TokenType.RightBracket);
    }

    // Nullable suffix T?
    if (Current.Type == TokenType.Question) {
        isNullable = true;
        Advance();
    }

    return new TypeAnnotation {
        Name = name,
        TypeArguments = typeArgs,
        IsNullable = isNullable
    };
}
```

**Examples**:
- `int` → `TypeAnnotation { Name = "int" }`
- `list[str]` → `TypeAnnotation { Name = "list", TypeArguments = [TypeAnnotation { Name = "str" }] }`
- `dict[str, int]?` → `TypeAnnotation { Name = "dict", TypeArguments = [...], IsNullable = true }`

---

### 3.5 Helper Methods

#### Navigation

```csharp
private void Advance() => _position++;

private Token Current => _position < _tokens.Count ? _tokens[_position] : _tokens[^1];
private Token Peek(int offset = 1) => _position + offset < _tokens.Count ? _tokens[_position + offset] : _tokens[^1];
```

**Safety**: These methods never throw on out-of-bounds; they return the EOF token instead.

---

#### Expectations

```csharp
private void Expect(TokenType type)
{
    if (Current.Type != type)
        throw new ParserError($"Expected {type}, got {Current.Type}", Current.Line, Current.Column);
    Advance();
}

private string ExpectIdentifier()
{
    if (Current.Type != TokenType.Identifier)
        throw new ParserError($"Expected identifier, got {Current.Type}", Current.Line, Current.Column);
    var value = Current.Value;
    Advance();
    return value;
}
```

**Usage**: Call these when you know exactly what token should come next. They provide clear error messages if the token doesn't match.

---

#### Statement Endings

```csharp
private void ExpectNewline()
{
    if (Current.Type == TokenType.Newline)
        Advance();
    else if (!IsAtEnd)
        throw new ParserError($"Expected newline, got {Current.Type}", Current.Line, Current.Column);
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
        throw new ParserError($"Expected end of statement", Current.Line, Current.Column);
}

private void SkipNewlines()
{
    while (Current.Type == TokenType.Newline)
        Advance();
}
```

**Subtlety**: `ExpectStatementEnd()` is more lenient than `ExpectNewline()` because statements can end at dedent or EOF without an explicit newline.

---

#### Type Checking

```csharp
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
```

**Purpose**: Disambiguate `x is SomeType` (type check) from `x is something` (identity comparison).

---

## 4. Dependencies

### Internal Dependencies

**Lexer**:
- `List<Token>`: Input to the parser
- `TokenType`: Enum for token classification

**Ast**:
- `Module`, `Statement`, `Expression`: AST node types
- All node types are immutable records with location tracking

**Logging**:
- `ICompilerLogger`: For diagnostics and performance tracking

**Errors**:
- `ParserError`: Custom exception type for parse errors

### External Dependencies

- **System.Text**: For string building
- **C# 10+ features**: Records, pattern matching, switch expressions

---

## 5. Patterns and Design Decisions

### 5.1 Recursive Descent

**Definition**: Each grammar rule becomes a parsing method. Methods call each other recursively to match nested structures.

**Advantages**:
- Easy to understand and maintain
- Direct correspondence with grammar
- Good error messages (know exactly where we are in grammar)

**Example**: `ParseExpression()` → `ParseConditionalExpression()` → ... → `ParsePrimary()`

---

### 5.2 Precedence Climbing

**Problem**: How to parse `1 + 2 * 3` correctly (as `1 + (2 * 3)`)?

**Solution**: Each precedence level is a separate method. Higher precedence = called deeper in the call stack.

**Benefits**:
- No explicit precedence table
- Easy to add new operators (just add a new method at the right level)
- Associativity is built into the loop structure

---

### 5.3 Immutable AST Nodes

**Design**: All AST nodes are C# records (immutable).

**Benefits**:
- Thread-safe
- Easy to reason about
- Supports structural equality

**Pattern**: Use `with` expressions to create modified copies:
```csharp
expr = ia with { Object = newObject };
```

---

### 5.4 Location Tracking

**Every AST node tracks**:
- `LineStart`, `ColumnStart`: Beginning of the construct
- `LineEnd`, `ColumnEnd`: End of the construct

**Purpose**:
- Error reporting: "Error on line 42, column 10"
- IDE integration: Jump to definition, hover tooltips
- Debugging: Trace execution back to source

**Pattern**:
```csharp
var startLine = Current.Line;
var startColumn = Current.Column;

// ... parse construct

return new SomeNode {
    ...,
    LineStart = startLine,
    ColumnStart = startColumn,
    LineEnd = Current.Line,
    ColumnEnd = Current.Column
};
```

---

### 5.5 Error Handling

**Strategy**: Fail fast with descriptive errors.

**ParserError**:
- Includes line/column information
- Clear message about what was expected vs. what was found

**No error recovery**: If parsing fails, the entire compilation stops. This is intentional for a compiler (as opposed to an IDE where you'd want partial results).

---

### 5.6 Lookahead

**Used sparingly**: Most parsing is single-token lookahead via `Current`.

**Multi-token lookahead**:
- `Peek(1)`: Check next token without consuming
- Used for disambiguating ambiguous syntax (e.g., `is Type` vs. `is value`)

**Example**:
```csharp
if (Current.Type == TokenType.Is && Peek(1).Type == TokenType.Identifier) {
    if (IsTypeName(Peek(1).Value)) {
        // This is a type check, not identity comparison
    }
}
```

---

## 6. Debugging Tips

### 6.1 Add Logging

The parser accepts an `ICompilerLogger`. Add logging calls to understand the parse flow:

```csharp
_logger.LogInfo($"Parsing function definition at line {Current.Line}");
```

### 6.2 Dump the Token Stream

Before debugging the parser, verify the tokens are correct:

```csharp
foreach (var token in _tokens) {
    Console.WriteLine($"{token.Type}: {token.Value} @ {token.Line}:{token.Column}");
}
```

### 6.3 Use AstDumper

After parsing, dump the AST to see its structure:

```csharp
var module = parser.ParseModule();
var dumper = new AstDumper();
Console.WriteLine(dumper.Dump(module));
```

### 6.4 Common Issues

**Issue**: "Expected newline, got DEDENT"
- **Cause**: Statement ending logic is incorrect
- **Fix**: Check if `ExpectStatementEnd()` should be used instead of `ExpectNewline()`

**Issue**: "Unexpected token: Identifier"
- **Cause**: Identifier was consumed in wrong place
- **Fix**: Check lookahead logic; maybe you need `Peek()` instead of advancing

**Issue**: Precedence is wrong (e.g., `1 + 2 * 3` parsed as `(1 + 2) * 3`)
- **Cause**: Method call order in precedence chain is incorrect
- **Fix**: Lower precedence = higher in call chain. Multiplication should be called by addition, not vice versa.

**Issue**: Slicing doesn't work
- **Cause**: `ParseSliceOrIndex()` consumed too much
- **Fix**: Check that slice parsing stops at `]`, not earlier

---

### 6.5 Debugging Workflow

1. **Isolate the problem**: Create a minimal `.spy` file that reproduces the issue
2. **Check the tokens**: Run lexer separately and dump tokens
3. **Add breakpoints**: Set breakpoints in the relevant `Parse*` methods
4. **Watch `Current`**: Monitor what token the parser is looking at
5. **Check the AST**: After parsing, dump the AST to see if it's structurally correct

---

## 7. Contribution Guidelines

### 7.1 Adding a New Statement Type

**Example**: Add `match` statement (pattern matching)

1. **Add token type** in `Lexer/Token.cs`:
   ```csharp
   public enum TokenType {
       // ...
       Match,
       Case,
   }
   ```

2. **Update lexer** to recognize `match` and `case` keywords

3. **Create AST node** in `Parser/Ast/Statement.cs`:
   ```csharp
   public record MatchStatement : Statement
   {
       public required Expression Value { get; init; }
       public required List<MatchCase> Cases { get; init; }
   }
   ```

4. **Add parser method**:
   ```csharp
   private MatchStatement ParseMatchStatement()
   {
       Expect(TokenType.Match);
       var value = ParseExpression();
       Expect(TokenType.Colon);
       // ... parse cases
   }
   ```

5. **Wire into `ParseStatement()`**:
   ```csharp
   TokenType.Match => ParseMatchStatement(),
   ```

6. **Add tests** in `Sharpy.Compiler.Tests/Parser/ParserTests.cs`

7. **Update semantic analyzer** and **code generator** to handle the new node

---

### 7.2 Adding a New Expression Operator

**Example**: Add `@` matrix multiplication operator

1. **Add token** (if not already exists)

2. **Decide precedence**: Where does `@` fit? (Same as `*` in Python)

3. **Add to appropriate parsing method**:
   ```csharp
   private Expression ParseMultiplicative()
   {
       var left = ParseUnary();

       while (Current.Type == TokenType.Star ||
              Current.Type == TokenType.Slash ||
              Current.Type == TokenType.At) {  // NEW
           var op = Current.Type switch {
               TokenType.Star => BinaryOperator.Multiply,
               TokenType.Slash => BinaryOperator.Divide,
               TokenType.At => BinaryOperator.MatrixMultiply,  // NEW
               _ => throw new ParserError(...)
           };
           Advance();
           var right = ParseUnary();
           left = new BinaryOp { Operator = op, Left = left, Right = right };
       }

       return left;
   }
   ```

4. **Update AST enums** in `Parser/Ast/Expression.cs`:
   ```csharp
   public enum BinaryOperator {
       // ...
       MatrixMultiply,
   }
   ```

5. **Add tests** for precedence and associativity

---

### 7.3 Improving Error Messages

**Current**: "Expected identifier, got LeftParen"

**Better**: "Expected class name after 'class' keyword, got '('"

**How**:
1. Add context to `ExpectIdentifier()`:
   ```csharp
   private string ExpectIdentifier(string context = "identifier")
   {
       if (Current.Type != TokenType.Identifier)
           throw new ParserError($"Expected {context}, got {Current.Type}", ...);
       // ...
   }
   ```

2. Call with context:
   ```csharp
   var name = ExpectIdentifier("class name after 'class' keyword");
   ```

---

### 7.4 Performance Optimization

**Current bottlenecks**:
- Creating AST nodes (lots of small allocations)
- Position tracking in every node

**Potential optimizations**:
- **Object pooling**: Reuse AST node objects (conflicts with immutability)
- **Struct nodes**: Use structs for small nodes (requires careful design)
- **Lazy location tracking**: Only track locations when needed (debug mode vs. release)

**Before optimizing**:
- Profile with real code
- Parser is typically not the bottleneck (semantic analysis and codegen are slower)

---

### 7.5 Testing

**Unit tests** for each parsing method:
```csharp
[Fact]
public void TestParseIfStatement()
{
    var tokens = Lex("if x > 0:\n    print(x)\n");
    var parser = new Parser(tokens);
    var module = parser.ParseModule();

    Assert.Single(module.Body);
    var ifStmt = Assert.IsType<IfStatement>(module.Body[0]);
    Assert.IsType<BinaryOp>(ifStmt.Test);
    Assert.Single(ifStmt.ThenBody);
}
```

**Integration tests**: Full programs in `Sharpy.Compiler.Tests/Integration/`

---

### 7.6 Code Style

**Follow existing patterns**:
- Use `var` for local variables
- Record types for AST nodes
- Switch expressions for dispatching
- Early returns to reduce nesting
- `with` expressions for record copies

**Naming conventions**:
- Private fields: `_camelCase`
- Methods: `PascalCase`
- Parameters: `camelCase`

---

## 8. Advanced Topics

### 8.1 Handling Ambiguous Syntax

**Problem**: `for x in y` — is `in` a comparison operator or part of the `for` statement?

**Solution**: Context-dependent parsing. In `ParseForTarget()`, we stop before consuming `in`.

---

### 8.2 Comprehensions

**Syntax**: `[expr for x in items if condition for y in items2]`

**Parsing strategy**:
1. Parse the element expression
2. When you see `for`, call `ParseComprehensionClauses()`
3. Parse alternating `for` and `if` clauses

**Implementation**:
```csharp
private List<ComprehensionClause> ParseComprehensionClauses()
{
    var clauses = new List<ComprehensionClause>();

    while (true) {
        if (Current.Type == TokenType.For) {
            // ... parse for clause
            clauses.Add(new ForClause { ... });
        }
        else if (Current.Type == TokenType.If) {
            // ... parse if clause
            clauses.Add(new IfClause { ... });
        }
        else {
            break;
        }
    }

    return clauses;
}
```

---

### 8.3 Comparison Chains

**Python feature**: `1 < x < 10` means `1 < x and x < 10`

**Why special AST node?**
- Evaluate middle operands once
- Efficient short-circuit evaluation

**Codegen challenge**: Emit code that evaluates `x` only once and short-circuits correctly.

---

### 8.4 Null-Conditional Operator

**Syntax**: `obj?.member?.method()`

**Parsing**: Each `?.` is parsed as `MemberAccess` with `IsNullConditional = true`.

**Codegen**: Use C#'s `?.` operator or null-check + conditional.

---

### 8.5 Decorators

**Syntax**:
```python
@decorator1
@decorator2
def my_function():
    pass
```

**Parsing**:
1. `ParseDecoratedStatement()` collects all leading `@` decorators
2. Parse the definition (function, class, or struct)
3. Attach decorators to the definition node

**AST representation**:
```csharp
public record FunctionDef : Statement
{
    public List<Decorator> Decorators { get; init; } = new();
    // ...
}
```

---

### 8.6 Type Conversion Operators

Sharpy provides two type conversion operators:

**`as` (Safe Cast)**:
```python
result = value as SomeType  # Returns null if cast fails
```
Parsed as `TypeCast` node, compiles to C#'s `as` operator.

**`to` (Coercion)**:
```python
result = value to int           # Throws InvalidCastException on failure
result = value to int?          # Returns None on failure (nullable target)
```
Parsed as `TypeCoercion` node, compiles to explicit cast with optional null handling.

---

## 9. Future Enhancements

### 9.1 Error Recovery

**Current**: Parser fails on first error.

**Future**: Add error recovery to continue parsing after errors (useful for IDEs).

**Approach**:
- On error, synchronize to a known point (e.g., next statement)
- Insert a placeholder node
- Continue parsing

---

### 9.2 Incremental Parsing

**Current**: Parse the entire file every time.

**Future**: Re-parse only changed portions (useful for IDEs with live error checking).

**Approach**:
- Track which tokens correspond to which AST nodes
- On edit, invalidate affected subtree
- Reuse unchanged nodes

---

### 9.3 Better F-String Support

**Current**: Basic f-string parsing.

**Future**: Support nested f-strings, complex format specs, debugging syntax (`f"{x=}"`).

---

### 9.4 Macro System

**Idea**: Allow compile-time code generation with a macro syntax.

**Parsing challenge**: Macros expand before semantic analysis, so parser must support deferred expansion.

---

## 10. Summary

The `Parser` class is a well-structured recursive descent parser with precedence climbing for expressions. Key takeaways:

- **Entry point**: `ParseModule()` parses an entire source file
- **Statement parsing**: Dispatched via `ParseStatement()`, with `ParseSimpleStatement()` handling the complex cases
- **Expression parsing**: Precedence climbing from `ParseExpression()` down to `ParsePrimary()`
- **Immutable AST**: All nodes are records with location tracking
- **Clear error messages**: `Expect*()` methods provide good diagnostics
- **Extensible design**: Easy to add new operators, statements, or expressions

**When debugging**: Start with the token stream, add logging, and use `AstDumper` to visualize the result.

**When contributing**: Follow the existing patterns, add tests, and ensure semantic analysis and codegen are updated for new features.

---

**Happy parsing!**
