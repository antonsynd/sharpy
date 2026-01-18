# Walkthrough: Parser.Primaries.cs

**Source File**: `src/Sharpy.Compiler/Parser/Parser.Primaries.cs`

---

## Overview

This file is a partial class extension of the `Parser` class that handles **primary expression parsing** — the fundamental building blocks of expressions in Sharpy. Primary expressions are the atomic units that can't be broken down further: literals (integers, strings, booleans), identifiers, collection literals (lists, dictionaries, sets, tuples), and special constructs like lambda expressions and comprehensions.

**Role in the Compiler Pipeline**:
- **Upstream**: Receives tokens from the Lexer (via the `Current` token cursor)
- **Downstream**: Returns `Expression` AST nodes to higher-level expression parsers
- **Position**: Bottom layer of the recursive descent expression parser (called by operator precedence methods in `Parser.Expressions.cs`)

This file implements the foundational layer of the [Recursive Descent Parser](https://en.wikipedia.org/wiki/Recursive_descent_parser) pattern, where `ParsePrimary()` is the base case that all other expression parsing methods eventually bottom out to.

---

## Class/Type Structure

### Partial Class: `Parser`

This file extends the main `Parser` class with primary expression parsing logic. The `Parser` class is split across multiple files:
- `Parser.cs` - Core parser infrastructure (token management, error handling)
- `Parser.Expressions.cs` - Operator precedence and compound expressions
- `Parser.Primaries.cs` - **This file** - Literal and atomic expressions
- `Parser.Statements.cs` - Statement parsing (for, if, while, etc.)
- `Parser.Definitions.cs` - Top-level definitions (classes, functions)
- `Parser.Types.cs` - Type annotations and f-string parsing

### Main Method

```csharp
private Expression ParsePrimary()
```

This is the **single entry point** for primary expression parsing. It uses a large `switch` statement on `Current.Type` (the current token) to determine which type of primary expression to parse.

---

## Key Functions/Methods

### ParsePrimary() - The Core Dispatcher

**Purpose**: Dispatches to the appropriate parsing logic based on the current token type.

**Algorithm**:
1. Capture current position (`startLine`, `startColumn`) for AST node location tracking
2. Switch on `Current.Type` to determine expression type
3. Parse the specific primary expression
4. Return an AST node with location information attached

**Return Value**: An `Expression` AST node (one of ~15 different expression types)

**Connection to Pipeline**:
- Called by higher precedence parsers (e.g., `ParsePostfix()`, `ParseUnary()` in `Parser.Expressions.cs`)
- Calls back to `ParseExpression()` for nested expressions (recursive descent)

---

### Numeric Literals: Integer and Float

#### Integer Parsing (lines 20-45)

**Handles**: `TokenType.Integer` tokens (e.g., `42`, `100L`, `0xFF`)

**Key Logic**:
```csharp
// Extract suffix if present (L, U, UL, etc.)
if (tokenValue.Length > 0 && char.IsLetter(tokenValue[tokenValue.Length - 1]))
{
    // Check for two-letter suffix
    if (tokenValue.Length > 1 && char.IsLetter(tokenValue[tokenValue.Length - 2]))
    {
        suffix = tokenValue.Substring(tokenValue.Length - 2);  // "UL"
        value = tokenValue.Substring(0, tokenValue.Length - 2);
    }
    else
    {
        suffix = tokenValue.Substring(tokenValue.Length - 1);  // "L"
        value = tokenValue.Substring(0, tokenValue.Length - 1);
    }
}
```

**Returns**: `IntegerLiteral` with:
- `Value`: The numeric portion (as string, to preserve exact representation)
- `Suffix`: Type suffix like `"L"` (long), `"U"` (unsigned), `"UL"` (unsigned long)

**Design Note**: Suffixes enable C#-like type hints (e.g., `42L` for long integers).

#### Float Parsing (lines 47-63)

**Handles**: `TokenType.Float` tokens (e.g., `3.14`, `2.5f`, `1.0m`)

**Similar to Integer**: Extracts single-letter suffix (`f`, `F`, `d`, `D`, `m`, `M`)

**Returns**: `FloatLiteral` with:
- `Value`: The numeric portion
- `Suffix`: Type suffix like `"f"` (float), `"d"` (double), `"m"` (decimal)

---

### String Literals

#### Regular Strings (lines 65-70)

**Handles**: `TokenType.String` tokens (e.g., `"hello"`)

**Returns**: `StringLiteral` with `IsRaw = false`

**Note**: The lexer has already processed escape sequences (e.g., `\n`, `\t`), so the parser receives the processed string value.

#### Raw Strings (lines 72-77)

**Handles**: `TokenType.RawString` tokens (e.g., `r"C:\path\to\file"`)

**Returns**: `StringLiteral` with `IsRaw = true`

**Use Case**: Raw strings preserve backslashes literally (useful for regex, file paths).

#### F-Strings (lines 79-83)

**Handles**: `TokenType.FStringStart` tokens (e.g., `f"Hello {name}"`)

**Delegates to**: `ParseSegmentedFString()` in `Parser.Types.cs:414`

**Returns**: `FStringLiteral` with interpolated expressions

**Complex Logic**: F-strings require special lexer/parser coordination:
- Lexer emits `FStringStart`, `FStringText`, `FStringExprStart`, expression tokens, `FStringExprEnd`, `FStringEnd`
- Parser reconstructs the f-string from these segments

---

### Boolean and Special Literals

#### Boolean Literals (lines 85-91)

**Handles**: `TokenType.True` and `TokenType.False`

**Returns**: `BooleanLiteral { Value = true/false }`

**Simple**: Just consumes the token and wraps it in an AST node.

#### None Literal (lines 93-95)

**Handles**: `TokenType.None` (Python's `None`, similar to C#'s `null`)

**Returns**: `NoneLiteral`

**Code Gen Note**: Later translated to `null` in C# code generation.

#### Ellipsis Literal (lines 97-99)

**Handles**: `TokenType.Ellipsis` (`...` token)

**Returns**: `EllipsisLiteral`

**Use Cases**:
- Placeholder in type annotations (e.g., `Callable[..., int]`)
- Slicing (e.g., `arr[..., 0]`)

---

### Identifiers and Special Keywords

#### Identifier (lines 101-106)

**Handles**: `TokenType.Identifier` (e.g., `x`, `my_variable`)

**Returns**: `Identifier { Name = "x" }`

**Important**: This is the primary expression for variable references, function names, etc.

#### Super Expression (lines 108-115)

**Handles**: `TokenType.Super` (calls parent class constructor)

**Syntax**: Must be followed by `()` (i.e., `super()`)

**Returns**: `SuperExpression`

**Validation**: Enforces that `super` can only be called as `super()`, not as a standalone identifier.

---

### Parenthesized Expressions and Tuples

#### Parentheses: `(expr)` or Tuple (lines 117-149)

**Handles**: `TokenType.LeftParen`

**Three Cases**:

1. **Empty Tuple**: `()` → `TupleLiteral { Elements = [] }`

2. **Tuple with Trailing Comma**: `(expr,)` or `(a, b, c)` → `TupleLiteral`
   ```csharp
   if (Current.Type == TokenType.Comma) {
       var elements = new List<Expression> { expr };
       while (Current.Type == TokenType.Comma) {
           Advance();
           if (Current.Type == TokenType.RightParen) break;  // Trailing comma
           elements.Add(ParseExpression());
       }
       return new TupleLiteral { Elements = elements };
   }
   ```

3. **Parenthesized Expression**: `(expr)` → `Parenthesized { Expression = expr }`

**Design Decision**: The comma determines if it's a tuple:
- `(x)` is parenthesized
- `(x,)` is a 1-element tuple
- `(x, y)` is a 2-element tuple

**Why Parenthesized?**: Preserves grouping for precedence (e.g., `(a + b) * c`)

---

### List Literals and Comprehensions

#### Lists: `[...]` (lines 151-194)

**Handles**: `TokenType.LeftBracket`

**Three Cases**:

1. **Empty List**: `[]` → `ListLiteral { Elements = [] }`

2. **List Comprehension**: `[expr for x in iterable]`
   ```csharp
   if (Current.Type == TokenType.For) {
       var clauses = ParseComprehensionClauses();  // Defined in Parser.Statements.cs:202
       return new ListComprehension {
           Element = firstExpr,
           Clauses = clauses
       };
   }
   ```

3. **Regular List**: `[a, b, c]` → `ListLiteral { Elements = [...] }`
   - Supports trailing commas: `[1, 2, 3,]`

**Comprehension Example**:
```python
# Sharpy code
squares = [x * x for x in range(10) if x % 2 == 0]

# AST: ListComprehension {
#   Element = BinaryOp(x * x),
#   Clauses = [
#     ForClause(x, range(10)),
#     IfClause(x % 2 == 0)
#   ]
# }
```

---

### Dictionary and Set Literals

#### Braces: `{...}` - Dict, Set, or Comprehensions (lines 196-291)

**Handles**: `TokenType.LeftBrace`

**Complex Disambiguation**: Braces can represent:
- Empty dict: `{}`
- Empty set: `{/}` (special Sharpy v0.2 syntax)
- Dict literal: `{key: value, ...}`
- Set literal: `{a, b, c}`
- Dict comprehension: `{k: v for x in iter}`
- Set comprehension: `{expr for x in iter}`

**Algorithm**:
```csharp
if (Current.Type == TokenType.Slash) {
    // Empty set: {/}
    return new SetLiteral { Elements = [] };
}

if (Current.Type == TokenType.RightBrace) {
    // Empty dict: {}
    return new DictLiteral { Entries = [] };
}

var firstExpr = ParseExpression();

if (Current.Type == TokenType.Colon) {
    // Dictionary: {key: value, ...}
    // or Dict comprehension: {key: value for x in iter}
} else {
    // Set: {a, b, c}
    // or Set comprehension: {expr for x in iter}
}
```

**Empty Set Syntax**: Python has an ambiguity where `{}` is an empty dict (not set). Sharpy introduces `{/}` as unambiguous empty set syntax.

**Dict Entry Parsing** (lines 249-252):
```csharp
var key = ParseExpression();
Expect(TokenType.Colon);
var value = ParseExpression();
entries.Add(new DictEntry { Key = key, Value = value });
```

---

### Lambda Expressions

#### Lambda: `lambda x, y: x + y` (lines 293-325)

**Handles**: `TokenType.Lambda`

**Syntax**:
- `lambda: expr` (no parameters)
- `lambda x: expr` (single parameter)
- `lambda x, y, z: expr` (multiple parameters)

**Parsing Logic**:
```csharp
Advance(); // Skip 'lambda' keyword
var parameters = new List<Parameter>();

if (Current.Type != TokenType.Colon) {
    do {
        var name = ExpectIdentifier();
        parameters.Add(new Parameter { Name = name });

        if (Current.Type == TokenType.Comma)
            Advance();
        else
            break;
    } while (true);
}

Expect(TokenType.Colon);
var body = ParseExpression();  // Recursive call

return new LambdaExpression { Parameters = parameters, Body = body };
```

**Returns**: `LambdaExpression` with:
- `Parameters`: List of parameter names (no type annotations in lambdas)
- `Body`: Single expression (not a statement block)

**Limitation**: Lambdas in Sharpy are expression-only (like Python), not statement lambdas.

---

## Dependencies

### Internal Sharpy Namespaces

- **`Sharpy.Compiler.Lexer`**: Token types (`TokenType.Integer`, etc.)
- **`Sharpy.Compiler.Parser.Ast`**: AST node types (`Expression`, `IntegerLiteral`, `ListComprehension`, etc.)
- **`Sharpy.Compiler.Logging`**: Error reporting (via `ParserError`)

### Parser Infrastructure (from other partial files)

- **`Current`**: Property holding the current token (from `Parser.cs`)
- **`Advance()`**: Move to next token (from `Parser.cs`)
- **`Expect(TokenType)`**: Consume token or throw error (from `Parser.cs`)
- **`ExpectIdentifier()`**: Consume identifier token and return name (from `Parser.cs`)
- **`ParseExpression()`**: Entry point for expression parsing (from `Parser.Expressions.cs`)
- **`ParseComprehensionClauses()`**: Parse `for`/`if` clauses in comprehensions (from `Parser.Statements.cs:202`)
- **`ParseSegmentedFString()`**: Parse f-string segments (from `Parser.Types.cs:414`)

---

## Patterns and Design Decisions

### 1. **Token-Driven Dispatch Pattern**

The `switch (Current.Type)` pattern is classic recursive descent parsing:
- Each token type maps to exactly one primary expression type
- No lookahead needed (LL(1) grammar for primaries)

### 2. **Location Tracking**

Every AST node captures source location:
```csharp
var startLine = Current.Line;
var startColumn = Current.Column;
// ... parse expression ...
return new SomeExpression {
    LineStart = startLine,
    ColumnStart = startColumn,
    LineEnd = Current.Line,    // After parsing
    ColumnEnd = Current.Column
};
```

**Purpose**: Enables precise error messages and IDE integration (e.g., "Error at line 42, column 10").

### 3. **Suffix Extraction for Numeric Literals**

Rather than handling suffixes in the lexer, the parser extracts them:
- **Lexer**: Emits `"42L"` as a single `TokenType.Integer` token
- **Parser**: Splits into `Value = "42"` and `Suffix = "L"`

**Rationale**: Keeps lexer simple; parser knows semantic meaning of suffixes.

### 4. **Recursive Calls for Nested Expressions**

The parser calls back to `ParseExpression()` for nested structures:
- List elements: `elements.Add(ParseExpression())`
- Dict values: `var value = ParseExpression()`
- Lambda body: `var body = ParseExpression()`

**Why `ParseExpression()` not `ParsePrimary()`?** Because nested expressions can be complex (e.g., `[a + b, c * d]`), so we need full expression parsing with operator precedence.

### 5. **Trailing Comma Support**

Lists, tuples, dicts, and sets all allow trailing commas:
```csharp
while (Current.Type == TokenType.Comma) {
    Advance();
    if (Current.Type == TokenType.RightBracket)
        break;  // Trailing comma detected
    elements.Add(ParseExpression());
}
```

**Benefit**: Cleaner diffs in version control (adding elements doesn't modify previous line).

### 6. **Comprehension Detection**

After parsing the first expression in `[...]` or `{...}`, check for `TokenType.For`:
```csharp
if (Current.Type == TokenType.For) {
    // It's a comprehension!
    var clauses = ParseComprehensionClauses();
    return new ListComprehension { ... };
}
```

**Insight**: Comprehensions are syntactically identical to regular collections until the `for` keyword appears.

---

## Debugging Tips

### 1. **Token Stream Inspection**

If parsing fails unexpectedly, examine the token stream:
- Add breakpoint at line 18 (`switch (Current.Type)`)
- Check `Current.Type`, `Current.Value`, `Current.Line`, `Current.Column`
- Verify lexer emitted expected tokens

### 2. **Common Error: Missing `Advance()` Call**

If the parser hangs in infinite loop:
- Ensure every token consumption path calls `Advance()`
- Example bug: Forgetting `Advance()` after `TokenType.Integer` would cause `Current` to never progress

### 3. **Comprehension Parsing Issues**

If comprehensions fail to parse:
- Check that `ParseComprehensionClauses()` in `Parser.Statements.cs:202` is working
- Verify lexer emits `TokenType.For` and `TokenType.In` correctly

### 4. **F-String Debugging**

F-strings involve complex lexer/parser interaction:
- Lexer must emit `FStringStart`, `FStringText`, `FStringExprStart`, etc.
- Check `ParseSegmentedFString()` in `Parser.Types.cs:414` for segment reconstruction
- Common issue: Unbalanced braces in f-string expressions

### 5. **Location Tracking Bugs**

If error messages show wrong line numbers:
- Ensure `startLine/startColumn` are captured **before** `Advance()`
- Ensure `LineEnd/ColumnEnd` are captured **after** parsing completes

### 6. **Tuple vs. Parenthesized Ambiguity**

If `(x)` is incorrectly parsed as tuple:
- Check comma detection logic at line 131: `if (Current.Type == TokenType.Comma)`
- `(x)` should become `Parenthesized`, not `TupleLiteral`

---

## Contribution Guidelines

### What Kinds of Changes Might Be Made

1. **Adding New Literal Types**
   - Example: Binary literals (`0b1010`), complex numbers (`3+4j`)
   - Add new `case TokenType.Binary:` branch to `ParsePrimary()`
   - Define corresponding AST node in `Sharpy.Compiler.Parser.Ast`

2. **Extending Comprehension Syntax**
   - Example: Add walrus operator (`:=`) in comprehensions
   - Modify comprehension detection logic (lines 165, 224, 262)
   - Update `ParseComprehensionClauses()` in `Parser.Statements.cs`

3. **New Collection Syntax**
   - Example: Frozen sets (`frozenset{1, 2, 3}`)
   - Add keyword detection before `{` parsing
   - Create new `FrozenSetLiteral` AST node

4. **Improving Error Messages**
   - Replace generic `throw new ParserError(...)` with specific messages
   - Example: Line 328 could say "Expected expression but found {Current.Type}"

5. **Performance Optimizations**
   - Use `StringBuilder` for suffix extraction (lines 26-42)
   - Cache `Current.Type` to avoid repeated property access

### Testing Considerations

When modifying this file:
- **Add unit tests** for new literal types or syntax
- **Test edge cases**: Empty collections, trailing commas, nested structures
- **Test error cases**: Missing closing bracket, invalid suffix, etc.
- **Integration tests**: Ensure code generation works for new AST nodes

### Style Guidelines

- **Maintain consistency**: Follow existing pattern of `startLine/startColumn` capture
- **Add comments**: Explain non-obvious logic (e.g., why `{/}` is empty set)
- **Keep switch exhaustive**: Cover all possible `TokenType` values or have default error

---

## Cross-References

### Related Parser Partial Files

This file is part of the `Parser` partial class split. Related files:

- **[Parser.cs](./Parser.md)** - Core parser infrastructure (token management, `Advance()`, `Expect()`)
- **[Parser.Expressions.cs](./Parser.Expressions.md)** - Operator precedence, binary/unary operators (calls `ParsePrimary()`)
- **[Parser.Statements.cs](./Parser.Statements.md)** - Contains `ParseComprehensionClauses()` (lines 202+)
- **Parser.Types.cs** - Contains `ParseSegmentedFString()` (line 414), type annotation parsing
- **[Parser.Definitions.cs](./Parser.Definitions.md)** - Top-level definitions (classes, functions)

### AST Nodes Defined in This File

All AST nodes are defined in `Sharpy.Compiler.Parser.Ast.Expression`:
- `IntegerLiteral`, `FloatLiteral`, `StringLiteral`, `BooleanLiteral`
- `NoneLiteral`, `EllipsisLiteral`, `Identifier`
- `ListLiteral`, `DictLiteral`, `SetLiteral`, `TupleLiteral`
- `ListComprehension`, `DictComprehension`, `SetComprehension`
- `LambdaExpression`, `SuperExpression`, `Parenthesized`, `FStringLiteral`

### Code Generation

The AST nodes produced by this file are consumed by:
- **RoslynEmitter.Expressions.cs** - Converts expression AST to C# Roslyn syntax trees

---

## Summary

`Parser.Primaries.cs` is the **foundation of expression parsing** in Sharpy. It handles:
- ✅ All literal types (numbers, strings, booleans, None, ellipsis)
- ✅ Identifiers and special keywords (super)
- ✅ Collection literals (lists, dicts, sets, tuples)
- ✅ Comprehensions (list/dict/set comprehensions)
- ✅ Lambda expressions
- ✅ Parenthesized expressions

**Key Takeaways**:
1. Uses **token-driven dispatch** (switch on `Current.Type`)
2. **Recursive descent**: Calls back to `ParseExpression()` for nested structures
3. **Location tracking**: Every AST node captures source position for errors
4. **Comprehensive syntax**: Handles Python-like literals + C#-style type suffixes
5. **Smart disambiguation**: Distinguishes dicts from sets, tuples from parentheses, comprehensions from literals

For newcomers: Start by reading `Parser.cs` to understand the token stream infrastructure, then study this file to see how individual expressions are built. Next, explore `Parser.Expressions.cs` to see how primaries combine into complex expressions.
