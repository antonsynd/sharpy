# Walkthrough: Parser.Primaries.cs

**Source File**: `src/Sharpy.Compiler/Parser/Parser.Primaries.cs`

---

## 1. Overview

This is a **partial class** of the main `Parser` class, responsible for parsing **primary expressions** - the fundamental building blocks of Sharpy expressions. Primary expressions are the highest-precedence expressions in the operator precedence hierarchy.

**Key responsibilities:**
- Parse all literal values (integers, floats, strings, booleans, None, ellipsis)
- Parse identifiers and variable references
- Parse collection literals (lists, dicts, sets, tuples)
- Parse comprehensions (list/set/dict comprehensions)
- Parse lambda expressions
- Parse the `super()` expression
- Parse parenthesized expressions
- Handle f-strings (formatted string literals)

**Pipeline position:**
```
Lexer → [TOKENS] → Parser.Primaries → [Primary Expression AST Nodes] → Parser.Expressions → Semantic Analysis
```

This is the **bottom of the precedence climbing chain** - all other expression parsing methods eventually call `ParsePrimary()` to get the base operands.

**Relationship to other Parser files:**
- Called by: `Parser.Expressions.ParsePostfix()` (for base expressions)
- Calls: Helper methods in `Parser.cs` and `Parser.Types.cs`
- Works with: `Parser.Statements.ParseComprehensionClauses()` for comprehension syntax

---

## 2. Class Structure

### This is a Partial Class

```csharp
public partial class Parser
{
    private Expression ParsePrimary()
    {
        // Main entry point - switches on token type
    }
}
```

The `Parser` class is split across multiple files:
- `Parser.cs` - Core infrastructure (token navigation, error handling)
- `Parser.Expressions.cs` - Operator precedence and binary/unary expressions
- `Parser.Primaries.cs` - **This file** - Literal and primary expressions
- `Parser.Statements.cs` - Statement parsing
- `Parser.Types.cs` - Type annotations and helper methods
- `Parser.Definitions.cs` - Function/class/struct definitions

**See also:**
- [Parser.md](Parser.md) - Main Parser class documentation
- [Parser.Expressions.md](Parser.Expressions.md) - Operator precedence and expression parsing
- [Parser.Statements.md](Parser.Statements.md) - Statement parsing

---

## 3. The ParsePrimary() Method

### 3.1 Overview

`ParsePrimary()` is the **entry point** for this file and serves as the **terminus** of the operator precedence chain. It uses a large `switch` statement to dispatch based on the current token type.

```csharp
private Expression ParsePrimary()
{
    var startLine = Current.Line;
    var startColumn = Current.Column;

    switch (Current.Type)
    {
        case TokenType.Integer: /* ... */
        case TokenType.Float: /* ... */
        case TokenType.String: /* ... */
        // ... many more cases
        default:
            throw new ParserError($"Unexpected token: {Current.Type}", Current.Line, Current.Column);
    }
}
```

**Key design pattern:**
1. Capture start position (line/column) at the beginning
2. Switch on token type to determine what kind of primary expression to parse
3. For each case:
   - Save the relevant token(s)
   - Advance the token stream
   - Extract any necessary values
   - Build and return an appropriate AST node with location info

---

## 4. Literal Parsing

### 4.1 Integer Literals

**Tokens**: `TokenType.Integer`

**Handles**: `42`, `1_000_000`, `0xFF`, `42L`, `42UL`

```csharp
case TokenType.Integer:
{
    var token = Current;
    var tokenValue = token.Value;
    Advance();

    // Extract suffix if present (L, U, UL, etc.)
    string value = tokenValue;
    string? suffix = null;

    if (tokenValue.Length > 0 && char.IsLetter(tokenValue[tokenValue.Length - 1]))
    {
        // Check for two-letter suffix (UL, ul, etc.)
        if (tokenValue.Length > 1 && char.IsLetter(tokenValue[tokenValue.Length - 2]))
        {
            suffix = tokenValue.Substring(tokenValue.Length - 2);
            value = tokenValue.Substring(0, tokenValue.Length - 2);
        }
        else
        {
            suffix = tokenValue.Substring(tokenValue.Length - 1);
            value = tokenValue.Substring(0, tokenValue.Length - 1);
        }
    }

    return new IntegerLiteral
    {
        Value = value,
        Suffix = suffix,
        // ... location info ...
    };
}
```

**Key implementation details:**
- The lexer has already validated the integer format
- Parser extracts C#-style type suffixes (L, U, UL) for .NET interop
- Suffix is stored separately so semantic analysis can determine the actual type
- Value preserves underscores and base prefixes (handled by semantic analysis later)

**Examples:**
- `42` → `Value="42"`, `Suffix=null`
- `42L` → `Value="42"`, `Suffix="L"`
- `0xFFUL` → `Value="0xFF"`, `Suffix="UL"`

### 4.2 Float Literals

**Tokens**: `TokenType.Float`

**Handles**: `3.14`, `1e-10`, `3.14f`, `2.5m`

```csharp
case TokenType.Float:
{
    var token = Current;
    var tokenValue = token.Value;
    Advance();

    // Extract suffix if present (f, F, d, D, m, M)
    string value = tokenValue;
    string? suffix = null;

    if (tokenValue.Length > 0 && char.IsLetter(tokenValue[tokenValue.Length - 1]))
    {
        suffix = tokenValue.Substring(tokenValue.Length - 1);
        value = tokenValue.Substring(0, tokenValue.Length - 1);
    }

    return new FloatLiteral { Value = value, Suffix = suffix, /* ... */ };
}
```

**Key implementation details:**
- Similar to integer literals, but only single-letter suffixes
- Common suffixes: `f`/`F` (float), `d`/`D` (double), `m`/`M` (decimal)
- Scientific notation is preserved in the value string

### 4.3 String Literals

**Tokens**: `TokenType.String`, `TokenType.RawString`

**Handles**: `"hello"`, `'world'`, `r"C:\path"`, `"""multi-line"""`

```csharp
case TokenType.String:
{
    var token = Current;
    var value = token.Value;
    Advance();
    return new StringLiteral
    {
        Value = value,
        IsRaw = false,
        // ... location info ...
    };
}

case TokenType.RawString:
{
    var token = Current;
    var value = token.Value;
    Advance();
    return new StringLiteral
    {
        Value = value,
        IsRaw = true,  // Escape sequences NOT processed
        // ... location info ...
    };
}
```

**Key implementation details:**
- Lexer has already extracted the string content (removed quotes, handled escapes)
- `IsRaw` flag indicates whether escape sequences should be processed
- Multi-line strings and triple-quoted strings are handled by the lexer

### 4.4 F-Strings (Formatted Strings)

**Tokens**: `TokenType.FStringStart`

**Handles**: `f"Hello {name}"`, `f"Value: {x:.2f}"`

```csharp
case TokenType.FStringStart:
{
    var startToken = Current;
    // Segmented f-string lexing
    return ParseSegmentedFString(startLine, startColumn, startToken);
}
```

**Key implementation details:**
- F-strings use a **segmented lexing** approach (lexer and parser collaborate)
- The lexer emits `FStringStart`, then alternates between `FStringText` and expression tokens
- `ParseSegmentedFString()` is defined in `Parser.Types.cs` and handles the complex logic
- This allows embedded expressions to be parsed recursively

**Why segmented?** F-strings can contain arbitrary expressions: `f"{obj.method(x, y)}"` requires full expression parsing inside the string.

### 4.5 Boolean and Special Literals

**Tokens**: `TokenType.True`, `TokenType.False`, `TokenType.None`, `TokenType.Ellipsis`

**Handles**: `True`, `False`, `None`, `...`

```csharp
case TokenType.True:
{
    var token = Current;
    Advance();
    return new BooleanLiteral { Value = true, /* ... */ };
}

case TokenType.False:
{
    var token = Current;
    Advance();
    return new BooleanLiteral { Value = false, /* ... */ };
}

case TokenType.None:
{
    var token = Current;
    Advance();
    return new NoneLiteral { /* ... */ };
}

case TokenType.Ellipsis:
{
    var token = Current;
    Advance();
    return new EllipsisLiteral { /* ... */ };
}
```

**Key implementation details:**
- These are simple token-to-AST conversions
- `None` maps to C# `null` (handled by RoslynEmitter)
- `Ellipsis` (`...`) is used in type annotations and as a placeholder expression
- All include proper location tracking for error messages

---

## 5. Identifiers and Variables

### 5.1 Identifier Parsing

**Tokens**: `TokenType.Identifier`

**Handles**: `x`, `my_variable`, `UserAccount`

```csharp
case TokenType.Identifier:
{
    var identToken = Current;
    var name = identToken.Value;
    Advance();
    return new Identifier
    {
        Name = name,
        // ... location info ...
    };
}
```

**Key implementation details:**
- Simple capture of the identifier name
- Semantic analysis will later resolve whether this is a local variable, parameter, class member, etc.
- No distinction between Pascal case, snake_case, etc. at parse time

**Resolution happens later:**
- The `Identifier` AST node is decorated with semantic info during semantic analysis
- Type information, scope, and binding are not available at parse time

---

## 6. Super Expression

### 6.1 Super() Parsing

**Tokens**: `TokenType.Super`

**Handles**: `super()`

```csharp
case TokenType.Super:
{
    var startToken = Current;
    Advance();
    // Expect super() - must be followed by ()
    Expect(TokenType.LeftParen);
    Expect(TokenType.RightParen);
    return new SuperExpression
    {
        // ... location info ...
    };
}
```

**Key implementation details:**
- Python-style `super()` requires empty parentheses (unlike some languages)
- Parser enforces this syntax requirement
- `super()` can only be used in specific contexts (validated during semantic analysis):
  - `__init__` methods to call `super().__init__(...)`
  - Dunder methods to call `super().__dunder__(...)`
  - `@override` methods to call `super().method(...)`

**Example usage:**
```python
class Child(Parent):
    def __init__(self, name: str):
        super().__init__()
        self.name = name
```

---

## 7. Parentheses and Tuples

### 7.1 Disambiguating Parenthesized vs Tuple

**Tokens**: `TokenType.LeftParen`

**Handles**: `()`, `(x)`, `(x,)`, `(x, y, z)`

This is one of the most complex cases because parentheses serve multiple purposes:

```csharp
case TokenType.LeftParen:
{
    var startToken = Current;
    Advance();

    // Empty tuple ()
    if (Current.Type == TokenType.RightParen)
    {
        Advance();
        return new TupleLiteral
        {
            Elements = ImmutableArray<Expression>.Empty,
            // ... location info ...
        };
    }

    var expr = ParseExpression();

    // Tuple (expr,) or (expr, expr2, ...)
    if (Current.Type == TokenType.Comma)
    {
        var elements = new List<Expression> { expr };

        while (Current.Type == TokenType.Comma)
        {
            Advance();
            if (Current.Type == TokenType.RightParen)
                break;
            elements.Add(ParseExpression());
        }

        Expect(TokenType.RightParen);
        return new TupleLiteral
        {
            Elements = elements.ToImmutableArray(),
            // ... location info ...
        };
    }

    Expect(TokenType.RightParen);
    return new Parenthesized
    {
        Expression = expr,
        // ... location info ...
    };
}
```

**Disambiguation rules:**
1. `()` → Empty tuple
2. `(x,)` → Single-element tuple (trailing comma is required!)
3. `(x, y)` → Multi-element tuple
4. `(x)` → Parenthesized expression (NOT a tuple)

**Why the distinction?**
- Tuples are immutable value types in Sharpy (map to C# ValueTuple)
- Parenthesized expressions affect precedence but don't create a value
- `(x)` and `x` are semantically identical
- `(x,)` creates a `(T,)` tuple type

**Trailing comma handling:**
```python
(1, 2, 3)   # 3-element tuple
(1, 2, 3,)  # Also 3-element tuple (trailing comma allowed)
(1,)        # 1-element tuple (comma required!)
(1)         # Just the number 1 in parentheses
```

---

## 8. Collection Literals

### 8.1 List Literals and List Comprehensions

**Tokens**: `TokenType.LeftBracket`

**Handles**: `[]`, `[1, 2, 3]`, `[x for x in range(10)]`, `[x for x in items if x > 0]`

```csharp
case TokenType.LeftBracket:
{
    var startToken = Current;
    Advance();

    // Empty list []
    if (Current.Type == TokenType.RightBracket)
    {
        Advance();
        return new ListLiteral
        {
            Elements = ImmutableArray<Expression>.Empty,
            // ... location info ...
        };
    }

    var firstExpr = ParseExpression();

    // Check for list comprehension: [expr for x in iterable]
    if (Current.Type == TokenType.For)
    {
        var clauses = ParseComprehensionClauses();
        Expect(TokenType.RightBracket);
        return new ListComprehension
        {
            Element = firstExpr,
            Clauses = clauses.ToImmutableArray(),
            // ... location info ...
        };
    }

    // Regular list literal [elem1, elem2, ...]
    var elements = new List<Expression> { firstExpr };

    while (Current.Type == TokenType.Comma)
    {
        Advance();
        // Allow trailing comma: [1, 2, 3,]
        if (Current.Type == TokenType.RightBracket)
            break;
        elements.Add(ParseExpression());
    }

    Expect(TokenType.RightBracket);
    return new ListLiteral
    {
        Elements = elements.ToImmutableArray(),
        // ... location info ...
    };
}
```

**Key implementation details:**
- **Lookahead**: After parsing first expression, check for `for` keyword to detect comprehension
- Trailing commas are allowed in list literals: `[1, 2, 3,]`
- List comprehensions are parsed by `ParseComprehensionClauses()` (in `Parser.Statements.cs`)

**Comprehension syntax:**
```python
[x * 2 for x in range(10)]                    # Simple comprehension
[x for x in items if x > 0]                   # With condition
[x for x in items for y in x if y > 0]        # Nested (not yet fully supported)
```

### 8.2 Dict Literals and Dict Comprehensions

**Tokens**: `TokenType.LeftBrace`

**Handles**: `{}`, `{"a": 1}`, `{k: v for k, v in pairs}`, `{/}` (empty set)

This is the most complex case because `{}` can mean:
- Empty dict
- Dict literal
- Dict comprehension
- Set literal (if no colons)
- Set comprehension
- Empty set (special `{/}` syntax in v0.2)

```csharp
case TokenType.LeftBrace:
{
    var startToken = Current;
    Advance();

    // Empty set {/} - special v0.2 syntax
    if (Current.Type == TokenType.Slash)
    {
        Advance();
        Expect(TokenType.RightBrace);
        return new SetLiteral
        {
            Elements = ImmutableArray<Expression>.Empty,
            // ... location info ...
        };
    }

    // Empty dict {}
    if (Current.Type == TokenType.RightBrace)
    {
        Advance();
        return new DictLiteral
        {
            Entries = ImmutableArray<DictEntry>.Empty,
            // ... location info ...
        };
    }

    var firstExpr = ParseExpression();

    // Dict {key: value, ...} or dict comprehension {key: value for x in iterable}
    if (Current.Type == TokenType.Colon)
    {
        Advance();
        var firstValue = ParseExpression();

        // Check for dict comprehension: {key: value for x in iterable}
        if (Current.Type == TokenType.For)
        {
            var clauses = ParseComprehensionClauses();
            Expect(TokenType.RightBrace);
            return new DictComprehension
            {
                Key = firstExpr,
                Value = firstValue,
                Clauses = clauses.ToImmutableArray(),
                // ... location info ...
            };
        }

        // Regular dict literal
        var entries = new List<DictEntry> { new DictEntry { Key = firstExpr, Value = firstValue } };

        while (Current.Type == TokenType.Comma)
        {
            Advance();
            if (Current.Type == TokenType.RightBrace)
                break;

            var key = ParseExpression();
            Expect(TokenType.Colon);
            var value = ParseExpression();
            entries.Add(new DictEntry { Key = key, Value = value });
        }

        Expect(TokenType.RightBrace);
        return new DictLiteral
        {
            Entries = entries.ToImmutableArray(),
            // ... location info ...
        };
    }
    // Set {elem1, elem2, ...} or set comprehension {expr for x in iterable}
    else
    {
        // Check for set comprehension: {expr for x in iterable}
        if (Current.Type == TokenType.For)
        {
            var clauses = ParseComprehensionClauses();
            Expect(TokenType.RightBrace);
            return new SetComprehension
            {
                Element = firstExpr,
                Clauses = clauses.ToImmutableArray(),
                // ... location info ...
            };
        }

        // Regular set literal
        var elements = new List<Expression> { firstExpr };

        while (Current.Type == TokenType.Comma)
        {
            Advance();
            if (Current.Type == TokenType.RightBrace)
                break;
            elements.Add(ParseExpression());
        }

        Expect(TokenType.RightBrace);
        return new SetLiteral
        {
            Elements = elements.ToImmutableArray(),
            // ... location info ...
        };
    }
}
```

**Disambiguation logic:**

| Syntax | Result | Reasoning |
|--------|--------|-----------|
| `{}` | Empty dict | Default interpretation |
| `{/}` | Empty set | Special syntax (Python doesn't have this) |
| `{expr}` | Set with one element | No colon |
| `{expr, ...}` | Set literal | No colons |
| `{expr: value}` | Dict with one entry | Colon present |
| `{expr: value, ...}` | Dict literal | Colons present |
| `{expr for x in y}` | Set comprehension | No colon, `for` keyword |
| `{k: v for x in y}` | Dict comprehension | Colon, `for` keyword |

**Design decision - Why `{/}` for empty set?**
- Python uses `set()` for empty set because `{}` is ambiguous
- Sharpy chose `{/}` as a more concise literal syntax
- Matches the "empty" concept (slash often means "nothing" or "none")

### 8.3 Set Literals and Set Comprehensions

Handled in the same `TokenType.LeftBrace` case as dicts (see above). The distinction is made by the absence of colons.

**Examples:**
```python
{1, 2, 3}              # Set literal
{x for x in range(10)} # Set comprehension
{/}                    # Empty set
```

---

## 9. Lambda Expressions

### 9.1 Lambda Parsing

**Tokens**: `TokenType.Lambda`

**Handles**: `lambda: 42`, `lambda x: x * 2`, `lambda x, y: x + y`

```csharp
case TokenType.Lambda:
{
    var lambdaToken = Current;
    Advance();
    var parameters = new List<Parameter>();

    // Parse lambda parameters
    if (Current.Type != TokenType.Colon)
    {
        do
        {
            var name = ExpectIdentifier();
            parameters.Add(new Parameter { Name = name });

            if (Current.Type == TokenType.Comma)
                Advance();
            else
                break;
        } while (true);
    }

    Expect(TokenType.Colon);
    var body = ParseExpression();

    // Combine lambda token span with body span
    var lambdaSpan = GetSpanFromToken(lambdaToken);
    var combinedSpan = lambdaSpan != null && body.Span != null
        ? lambdaSpan.Value.Union(body.Span.Value)
        : (Text.TextSpan?)null;

    return new LambdaExpression
    {
        Parameters = parameters.ToImmutableArray(),
        Body = body,
        LineStart = startLine,
        ColumnStart = startColumn,
        LineEnd = body.LineEnd,
        ColumnEnd = body.ColumnEnd,
        Span = combinedSpan
    };
}
```

**Key implementation details:**
- Lambda body is a **single expression**, not a statement block
- Parameters are comma-separated, no parentheses: `lambda x, y: x + y`
- No type annotations on lambda parameters (inferred by semantic analysis)
- Colon separates parameters from body
- Span calculation unions the lambda token with the body span for better error messages

**Examples:**
```python
lambda: 42                    # No parameters
lambda x: x * 2               # One parameter
lambda x, y: x + y            # Multiple parameters
lambda x: lambda y: x + y     # Nested lambdas (currying)
```

**Limitations:**
- No default parameter values
- No `*args` or `**kwargs`
- No type annotations (unlike function definitions)

These are intentional design decisions to keep lambdas simple and lightweight.

---

## 10. Helper Methods and Dependencies

### 10.1 Token Navigation

These methods are defined in the main `Parser.cs`:

```csharp
private Token Current => _position < _tokens.Count ? _tokens[_position] : _tokens[^1];
private Token Previous => _position > 0 ? _tokens[_position - 1] : _tokens[0];
private Token Peek(int offset = 1) => _position + offset < _tokens.Count ? _tokens[_position + offset] : _tokens[^1];
private void Advance() => _position++;
```

**Usage in Parser.Primaries:**
- `Current` - Get the current token to inspect its type
- `Previous` - Get the previous token (for end location info)
- `Advance()` - Move to the next token

### 10.2 Expect Methods

Defined in `Parser.Types.cs`:

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
        throw new ParserError("Expected identifier", Current.Line, Current.Column);
    var name = Current.Value;
    Advance();
    return name;
}
```

**Usage in Parser.Primaries:**
- `Expect(TokenType.RightParen)` - Ensure closing parenthesis exists
- `ExpectIdentifier()` - Get and validate an identifier name

### 10.3 Span Calculation

Defined in `Parser.Types.cs`:

```csharp
private static Text.TextSpan? GetSpanFromToken(Token token)
{
    // Returns a TextSpan covering the token's source location
}

private static Text.TextSpan? GetSpanFromTokens(Token start, Token end)
{
    // Returns a TextSpan from start token to end token
}
```

**Purpose:**
- Tracks exact source code locations for error messages
- Used by IDE features (hover, go-to-definition)
- Enables precise diagnostics

**Pattern in Parser.Primaries:**
```csharp
return new SomeExpression
{
    // ... properties ...
    LineStart = startLine,
    ColumnStart = startColumn,
    LineEnd = Previous.Line,
    ColumnEnd = Previous.Column + Previous.Value.Length,
    Span = GetSpanFromTokens(startToken, Previous)
};
```

### 10.4 Expression Parsing

Defined in `Parser.Expressions.cs`:

```csharp
private Expression ParseExpression() => ParseWalrusExpression();
```

**Called from Parser.Primaries:**
- Used to recursively parse sub-expressions
- Examples:
  - List elements: `[expr1, expr2, expr3]`
  - Dict keys/values: `{key: value}`
  - Tuple elements: `(expr1, expr2)`
  - Comprehension bodies: `[expr for x in iterable]`
  - Lambda bodies: `lambda x: expr`

This creates the recursive structure of the parser.

### 10.5 Comprehension Parsing

Defined in `Parser.Statements.cs`:

```csharp
private List<ComprehensionClause> ParseComprehensionClauses()
{
    // Parses: for x in iterable [if condition] [for y in iterable2] [if condition2] ...
}
```

**Comprehension clause types:**
- `ForClause` - `for x in iterable`
- `IfClause` - `if condition`

**Example:**
```python
[x for x in range(10) if x % 2 == 0 for y in range(x)]
```
Produces:
1. `ForClause(x, range(10))`
2. `IfClause(x % 2 == 0)`
3. `ForClause(y, range(x))`

### 10.6 F-String Parsing

Defined in `Parser.Types.cs`:

```csharp
private FStringLiteral ParseSegmentedFString(int startLine, int startColumn, Token startToken)
{
    // Complex logic to handle f-string segments and embedded expressions
}
```

**Why segmented?**
- F-strings can contain arbitrary expressions: `f"{obj.method(x + y)}"`
- Lexer and parser collaborate:
  1. Lexer emits `FStringStart`
  2. Lexer emits `FStringText` for literal parts
  3. Lexer switches to expression mode for `{...}` parts
  4. Parser parses the expression recursively
  5. Repeat until `FStringEnd`

---

## 11. AST Node Types

All AST nodes returned by `ParsePrimary()` are defined in `src/Sharpy.Compiler/Parser/Ast/Expression.cs`:

### 11.1 Literal Nodes

| AST Type | Properties | Example |
|----------|-----------|---------|
| `IntegerLiteral` | `Value`, `Suffix` | `42L` |
| `FloatLiteral` | `Value`, `Suffix` | `3.14f` |
| `StringLiteral` | `Value`, `IsRaw` | `"hello"` |
| `FStringLiteral` | `Parts` | `f"x={x}"` |
| `BooleanLiteral` | `Value` | `True` |
| `NoneLiteral` | - | `None` |
| `EllipsisLiteral` | - | `...` |

### 11.2 Collection Nodes

| AST Type | Properties | Example |
|----------|-----------|---------|
| `ListLiteral` | `Elements` | `[1, 2, 3]` |
| `DictLiteral` | `Entries` (key-value pairs) | `{"a": 1}` |
| `SetLiteral` | `Elements` | `{1, 2, 3}` |
| `TupleLiteral` | `Elements` | `(1, 2, 3)` |

### 11.3 Comprehension Nodes

| AST Type | Properties | Example |
|----------|-----------|---------|
| `ListComprehension` | `Element`, `Clauses` | `[x for x in items]` |
| `SetComprehension` | `Element`, `Clauses` | `{x for x in items}` |
| `DictComprehension` | `Key`, `Value`, `Clauses` | `{k: v for k, v in pairs}` |

### 11.4 Other Primary Nodes

| AST Type | Properties | Example |
|----------|-----------|---------|
| `Identifier` | `Name` | `my_var` |
| `LambdaExpression` | `Parameters`, `Body` | `lambda x: x * 2` |
| `Parenthesized` | `Expression` | `(x + y)` |
| `SuperExpression` | - | `super()` |

All nodes inherit from `Expression` which inherits from `Node`, and all include location tracking:
- `LineStart`, `ColumnStart`, `LineEnd`, `ColumnEnd`
- `Span` (optional `TextSpan` for precise source mapping)

---

## 12. Patterns and Design Decisions

### 12.1 Immutability

All AST nodes use C# **record types** with `init` properties:

```csharp
public record IntegerLiteral : Expression
{
    public string Value { get; init; } = "";
    public string? Suffix { get; init; }
}
```

**Benefits:**
- Immutable AST (can't be accidentally modified)
- Structural equality (useful for testing)
- Easy to create modified copies with `with` syntax

**Critical rule from CLAUDE.md:**
> **Immutable AST** — annotations go in `SemanticInfo`, not AST nodes

### 12.2 Location Tracking

Every AST node includes source location:

```csharp
public abstract record Node
{
    public int LineStart { get; init; }
    public int ColumnStart { get; init; }
    public int LineEnd { get; init; }
    public int ColumnEnd { get; init; }
    public Text.TextSpan? Span { get; init; }
}
```

**Used for:**
- Error messages: "Type error at line 42, column 15"
- IDE features: hover tooltips, go-to-definition
- Debugging: precise source mapping

**Pattern:**
```csharp
var startLine = Current.Line;
var startColumn = Current.Column;
// ... parse expression ...
return new SomeExpression
{
    // ... properties ...
    LineStart = startLine,
    ColumnStart = startColumn,
    LineEnd = Previous.Line,
    ColumnEnd = Previous.Column + Previous.Value.Length,
    Span = GetSpanFromTokens(startToken, Previous)
};
```

### 12.3 Suffix Extraction Pattern

Numeric literals support C#-style type suffixes:

```csharp
string value = tokenValue;
string? suffix = null;

if (tokenValue.Length > 0 && char.IsLetter(tokenValue[tokenValue.Length - 1]))
{
    // Extract suffix (1 or 2 characters)
    // ...
}
```

**Why extract suffixes?**
- Allows Python-like syntax with .NET types: `42L` → `long`
- Semantic analysis uses suffix to determine exact type
- Supports: `L` (long), `U` (uint), `UL` (ulong), `f` (float), `d` (double), `m` (decimal)

### 12.4 Lookahead for Disambiguation

Several cases use lookahead to distinguish between similar syntaxes:

**Example 1: List literal vs comprehension**
```csharp
var firstExpr = ParseExpression();

if (Current.Type == TokenType.For)
{
    // It's a comprehension
}
else
{
    // It's a regular list
}
```

**Example 2: Dict vs Set**
```csharp
var firstExpr = ParseExpression();

if (Current.Type == TokenType.Colon)
{
    // It's a dict
}
else
{
    // It's a set
}
```

This **minimal lookahead** keeps the parser efficient while handling Python's flexible syntax.

### 12.5 Error Handling

The parser uses **exceptions** for error reporting:

```csharp
default:
    throw new ParserError($"Unexpected token: {Current.Type}", Current.Line, Current.Column);
```

**ParserError** includes:
- Error message
- Line and column numbers
- Token context

**No error recovery:** The parser stops at the first error. This is acceptable for:
- Compiler use case (fix error, re-compile)
- Fast feedback loop
- Clear error messages

**Future enhancement:** LSP/IDE mode might add error recovery for better IDE experience.

---

## 13. Debugging Tips

### 13.1 Use the `emit ast` Command

To see the AST produced by the parser:

```bash
dotnet run --project src/Sharpy.Cli -- emit ast file.spy
```

This shows the full AST tree including all primary expressions.

### 13.2 Add Logging

The parser supports logging via `ICompilerLogger`:

```csharp
_logger.LogInfo($"Parsing primary: {Current.Type}");
```

Enable verbose logging to trace parser execution.

### 13.3 Inspect Token Stream

If parsing produces unexpected results:

```bash
dotnet run --project src/Sharpy.Cli -- emit tokens file.spy
```

This shows what tokens the lexer produced, helping identify lexer vs parser issues.

### 13.4 Common Issues

**Issue: "Unexpected token" errors**
- Check that the lexer is producing the expected token types
- Verify the token stream with `emit tokens`
- Ensure the grammar is unambiguous

**Issue: Wrong AST node type**
- Check lookahead logic (e.g., dict vs set disambiguation)
- Verify `Current.Type` checks
- Look for missing cases in switch statement

**Issue: Location info is wrong**
- Ensure `startLine` and `startColumn` are captured **before** any `Advance()` calls
- Use `Previous` token for end location (after advancing)
- Check `GetSpanFromTokens()` arguments

### 13.5 Test-Driven Debugging

File-based tests are in `src/Sharpy.Compiler.Tests/Integration/TestFixtures/`:

**To add a test:**
1. Create `your_test.spy` with the code to parse
2. Create `your_test.expected` with expected output
3. Run: `dotnet test --filter "FileBasedIntegrationTests"`

**For parser-specific tests:**
Look at `src/Sharpy.Compiler.Tests/Parser/` for unit tests of specific parsing scenarios.

---

## 14. Contribution Guidelines

### 14.1 When to Modify This File

Add to `Parser.Primaries.cs` when:
- Adding a new literal type (e.g., complex numbers, raw bytes)
- Adding a new collection syntax
- Adding a new primary expression keyword
- Modifying comprehension syntax

**Don't modify for:**
- Binary/unary operators (those go in `Parser.Expressions.cs`)
- Statements (those go in `Parser.Statements.cs`)
- Type annotations (those go in `Parser.Types.cs`)

### 14.2 Adding a New Literal Type

**Example: Adding hexadecimal float literals**

1. Ensure the lexer produces the appropriate token (e.g., `TokenType.HexFloat`)
2. Add a case in `ParsePrimary()`:
   ```csharp
   case TokenType.HexFloat:
   {
       var token = Current;
       var value = token.Value;
       Advance();
       return new HexFloatLiteral
       {
           Value = value,
           LineStart = startLine,
           ColumnStart = startColumn,
           LineEnd = Previous.Line,
           ColumnEnd = Previous.Column + Previous.Value.Length,
           Span = GetSpanFromToken(token)
       };
   }
   ```
3. Define the AST node in `Expression.cs`
4. Add semantic analysis for the new type
5. Add RoslynEmitter code to generate C#

### 14.3 Code Style

Follow the existing patterns:
- Capture `startLine`/`startColumn` at the beginning
- Use `var` for local variables
- Include XML doc comments for public APIs
- Add comprehensive location tracking
- Use `ImmutableArray` for collections in AST nodes

### 14.4 Testing Requirements

**For any changes to Parser.Primaries.cs:**

1. Add unit tests in `src/Sharpy.Compiler.Tests/Parser/`
2. Add file-based integration tests if the feature interacts with other components
3. Test edge cases:
   - Empty collections
   - Single-element collections
   - Trailing commas
   - Nested structures
   - Error cases (malformed syntax)

**Run tests:**
```bash
dotnet test --filter "FullyQualifiedName~Parser"
```

### 14.5 Critical Rules (from CLAUDE.md)

1. **Never modify expected values to make tests pass** — fix the implementation
2. **Immutable AST** — annotations go in `SemanticInfo`, not AST nodes
3. **C# 9.0 target** — no newer language features

---

## 15. Cross-References

### 15.1 Related Partial Classes

This file is part of the `Parser` partial class. See also:

- [Parser.md](Parser.md) - Main parser class and architecture
- [Parser.Expressions.md](Parser.Expressions.md) - Operator precedence and expression parsing
- [Parser.Statements.md](Parser.Statements.md) - Statement parsing (comprehension clauses are here)
- [Parser.Types.md](Parser.Types.md) - Type annotations and helper methods
- [Parser.Definitions.md](Parser.Definitions.md) - Function/class/struct definitions

### 15.2 Dependencies

**Upstream (this file depends on):**
- `Sharpy.Compiler.Lexer` - Token types and lexer output
- `Sharpy.Compiler.Logging` - Logging infrastructure
- `Sharpy.Compiler.Parser.Ast` - AST node definitions

**Downstream (depends on this file):**
- `Parser.Expressions.cs` - Calls `ParsePrimary()` to get operands
- Semantic analysis - Processes the AST nodes produced here
- RoslynEmitter - Converts AST to C# Roslyn syntax trees

### 15.3 Key Files to Review

**For understanding primary expressions:**
1. `src/Sharpy.Compiler/Parser/Ast/Expression.cs` - AST node definitions
2. `src/Sharpy.Compiler/Parser/Parser.Expressions.cs` - How primaries fit into operator precedence
3. `src/Sharpy.Compiler/Lexer/Lexer.cs` - How tokens are produced

**For understanding the full pipeline:**
1. `src/Sharpy.Compiler/Parser/Parser.cs` - Entry point and infrastructure
2. `src/Sharpy.Compiler/SemanticAnalysis/` - What happens after parsing
3. `src/Sharpy.Compiler/CodeGen/RoslynEmitter.Expressions.cs` - How primaries become C#

---

## 16. Summary

`Parser.Primaries.cs` is the **foundation** of expression parsing in Sharpy. It handles the most fundamental syntactic constructs:

**What it does:**
✅ Parses all literal values
✅ Parses identifiers and `super()`
✅ Parses collection literals and comprehensions
✅ Handles complex disambiguation (tuples, dicts, sets)
✅ Supports lambda expressions
✅ Provides precise source location tracking

**What it doesn't do:**
❌ Doesn't handle operators (that's `Parser.Expressions.cs`)
❌ Doesn't handle statements (that's `Parser.Statements.cs`)
❌ Doesn't do semantic analysis (that comes later)
❌ Doesn't generate code (that's `RoslynEmitter`)

**Key takeaways for newcomers:**
1. This is a **partial class** - many helpers are in other files
2. The `ParsePrimary()` switch is the **bottom of the precedence chain**
3. **Lookahead** is used sparingly for disambiguation
4. All AST nodes are **immutable records** with full location tracking
5. The parser is **error-intolerant** (stops at first error)

**To explore further:**
- Read `Parser.Expressions.md` to understand how primaries fit into operator precedence
- Inspect the AST nodes in `src/Sharpy.Compiler/Parser/Ast/Expression.cs`
- Run `dotnet run --project src/Sharpy.Cli -- emit ast your_file.spy` to see the parser in action
