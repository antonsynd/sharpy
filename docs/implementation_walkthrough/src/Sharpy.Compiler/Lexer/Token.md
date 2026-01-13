# Walkthrough: Token.cs

**Source File**: `src/Sharpy.Compiler/Lexer/Token.cs`

---

## Overview

The `Token.cs` file defines the fundamental data structures for **lexical analysis** (tokenization)—the first phase of the Sharpy compiler pipeline. It contains:

- **`TokenType`**: An enum cataloging all possible token categories in the Sharpy language
- **`Token`**: A record representing a single lexical unit with its type, value, and source position

**Pipeline Position:**
```
Source (.spy) → [LEXER produces Tokens] → Parser → AST → Semantic Analysis → Code Generation
```

Think of tokens as the "words" of the programming language. Just as reading prose involves recognizing individual words and punctuation, the lexer breaks source code into tokens that the parser can work with. This file defines **what** those tokens look like; the actual tokenization logic lives in `Lexer.cs`.

**Key Insight:** This file contains no logic—it's purely **data definitions**. The `Token` record is the contract between the Lexer (producer) and Parser (consumer).

---

## Class/Type Structure

### `TokenType` Enum

```csharp
public enum TokenType
{
    // ~80 different token types organized into logical groups
}
```

The `TokenType` enum exhaustively lists every distinct lexical element that can appear in Sharpy source code. Understanding these categories is essential for working anywhere in the compiler.

#### Literals (lines 8-21)

Represent constant values in source code:

| Token Type | Description | Example |
|------------|-------------|---------|
| `Integer` | Whole numbers | `42`, `0xFF`, `0b1010`, `1_000_000` |
| `Float` | Floating-point numbers | `3.14`, `1e10`, `2.5f` |
| `String` | Regular string literals | `"hello"`, `'world'`, `"""multi"""` |
| `RawString` | Raw strings (r-prefix, no escape processing) | `r"C:\path"`, `r'\d+\.\d+'` |
| `True`, `False` | Boolean literals | `True`, `False` |
| `None` | Null/none value | `None` |

#### F-String Tokens (lines 13-18)

F-strings require multiple token types because they interleave literal text with embedded expressions:

```python
f"Hello {name}!"
```

Tokenizes to this sequence:
```
FStringStart("f\"") → FStringText("Hello ") → FStringExprStart("{")
→ Identifier("name") → FStringExprEnd("}") → FStringText("!")
→ FStringEnd("\"")
```

| Token Type | Description |
|------------|-------------|
| `FStringStart` | Opening `f"` or `f'` |
| `FStringText` | Literal text segments between expressions |
| `FStringExprStart` | `{` starting an interpolated expression |
| `FStringExprEnd` | `}` ending an interpolated expression |
| `FStringFormatSpec` | Format spec after `:` (e.g., `.2f`, `>10`) |
| `FStringEnd` | Closing quote |

**Why so many token types for f-strings?** The lexer needs to track state as it transitions between "literal text mode" and "expression mode." Using distinct tokens makes this state machine explicit and allows the parser to handle embedded expressions correctly.

#### Keywords (lines 24-86)

Reserved words organized by purpose. Each keyword gets its own enum value (e.g., `Def` for `def`) rather than a generic `Keyword` type—this enables type-safe pattern matching in the parser.

**Control Flow:**
```csharp
Def, Class, Struct, Interface, Enum,
If, Else, Elif, While, For, In, Return, Break, Continue, Pass,
Try, Except, Finally, Raise, Assert, With
```

**Imports:**
```csharp
Import, From, As
```

**Type/Value:**
```csharp
Auto,     // Type inference
Const,    // Constant declaration
Lambda,   // Lambda expressions
Type,     // Type alias declaration
```

**Pattern Matching:**
```csharp
Match, Case
```

**Async:**
```csharp
Async, Await, Yield
```

**Members:**
```csharp
Property, Event
```

**Boolean Operators (as keywords, following Python):**
```csharp
And, Or, Not, Is
```

**Other:**
```csharp
Del,     // Delete statement
To,      // Type coercion operator
Maybe,   // Optional from nullable expressions
```

**Future Reserved (not yet implemented):**
```csharp
Defer, Do
```

See `docs/language_specification/keywords.md` for the complete keyword reference.

#### Operators (lines 88-133)

Mathematical, comparison, bitwise, and assignment operators:

**Arithmetic:**
- `Plus` (+), `Minus` (-), `Star` (*), `Slash` (/)
- `DoubleSlash` (//) — floor division
- `Percent` (%) — modulo
- `DoubleStar` (**) — exponentiation

**Comparison:**
- `Equal` (==), `NotEqual` (!=)
- `Less` (<), `Greater` (>), `LessEqual` (<=), `GreaterEqual` (>=)

**Bitwise:**
- `Ampersand` (&), `Pipe` (|), `Caret` (^), `Tilde` (~)
- `LeftShift` (<<), `RightShift` (>>)

**Assignment:**
- `Assign` (=)
- Compound: `PlusAssign` (+=), `MinusAssign` (-=), `StarAssign` (*=), etc.

**Special Operators:**
- `Question` (?) — nullable type marker
- `NullConditional` (?.) — safe navigation
- `NullCoalesce` (??) — null coalescing
- `Ellipsis` (...) — spread/rest operator
- `PipeForward` (|>) — pipeline operator

#### Delimiters (lines 135-148)

Structural punctuation:

```csharp
LeftParen, RightParen,     // ( )
LeftBracket, RightBracket, // [ ]
LeftBrace, RightBrace,     // { }
Comma, Colon, Semicolon,   // , : ;
Dot, Arrow, At, Backslash  // . -> @ \
```

**Note:** Brackets are significant for implicit line continuation—inside `()`, `[]`, or `{}`, newlines are ignored. The lexer tracks bracket depth to implement this.

#### Structural Tokens (lines 150-158)

Special tokens for Python-style block structure and document boundaries:

| Token Type | Description |
|------------|-------------|
| `Newline` | End of a logical line (significant in Python-style syntax) |
| `Indent` | Indentation increase → block start |
| `Dedent` | Indentation decrease → block end |
| `Eof` | End of file |
| `Backtick` | For literal/escaped identifiers (`` `class` ``) |
| `Comment` | Comment text (typically skipped but available for tooling) |

**Indentation tokens** are what make Python-style syntax possible. The lexer converts invisible whitespace changes into explicit `Indent`/`Dedent` tokens:

```python
def foo():      # ← Newline
    x = 1       # ← Indent (entering function body)
    if True:    # ← Newline
        y = 2   # ← Indent (entering if body)
    z = 3       # ← Dedent (exiting if body)
                # ← Dedent (exiting function at EOF)
```

### `Token` Record

```csharp
public record Token
{
    public TokenType Type { get; init; }
    public string Value { get; init; } = string.Empty;
    public int Line { get; init; }
    public int Column { get; init; }

    public Token(TokenType type, string value, int line, int column)
    {
        Type = type;
        Value = value;
        Line = line;
        Column = column;
    }
}
```

The `Token` record is an **immutable data container** representing a single token:

| Property | Description | Example |
|----------|-------------|---------|
| `Type` | Which kind of token (from `TokenType` enum) | `TokenType.Identifier` |
| `Value` | The actual text from source code | `"myVariable"`, `"+"`, `"42"` |
| `Line` | 1-indexed line number in source | `42` |
| `Column` | 1-indexed column position | `10` |

**Why use a `record`?**
- **Immutability**: Tokens can't be accidentally modified after creation (thread-safe)
- **Value equality**: Two tokens with same fields are equal (great for testing)
- **Built-in `ToString()`**: Useful for debugging
- **Pattern matching**: Works well with C# switch expressions in the parser

---

## Key Functions/Methods

### Constructor

```csharp
public Token(TokenType type, string value, int line, int column)
```

Creates a new token instance. This is the only way to create tokens (no factory methods or builders).

**Usage in Lexer.cs:**
```csharp
// When the lexer recognizes "def" at line 1, column 5
return new Token(TokenType.Def, "def", _line, _column);

// When it finds an identifier
return new Token(TokenType.Identifier, identifierText, startLine, startColumn);
```

**Design Note:** The constructor is intentionally simple because tokens are pure data carriers. All intelligence lives in the lexer that creates them.

---

## Dependencies

### Inbound Dependencies (Who Uses This File)

This file is **foundational**—many parts of the compiler depend on it:

- **`Lexer.cs`**: Creates `Token` instances during tokenization
- **`Parser/Parser.cs`**: Consumes token streams, pattern matches on `TokenType`
- **`LexerError.cs`**: References token types for error messages
- **`ParserError.cs`**: Uses token positions for error reporting
- **AST node classes**: Store position info using `Line` and `Column`
- **`Diagnostics/`**: Uses position info for compiler messages
- **Test files**: Verify token sequences

### Outbound Dependencies (What This File Needs)

**None.** This file is completely self-contained—no imports beyond the `namespace` declaration. This is intentional for a foundational data structure.

---

## Patterns and Design Decisions

### 1. Single Large Enum for All Token Types

**Decision:** Use one `TokenType` enum rather than separate enums for keywords, operators, etc.

**Benefits:**
- Simple switch statements in the parser
- Easy token comparisons
- Straightforward serialization and debugging

**Trade-off:** The enum is large (~80 values), but comments organize it into navigable sections.

### 2. Keywords as Distinct Token Types

**Decision:** Each keyword gets its own enum value (`Def`, `Class`, `If`) rather than a generic `Keyword` type with a string value.

**Benefits:**
```csharp
// Parser can use type-safe pattern matching
switch (token.Type)
{
    case TokenType.Def:
        return ParseFunctionDef();
    case TokenType.Class:
        return ParseClassDef();
    // ...
}

// No string comparisons needed
// Can't accidentally check for misspelled keyword
```

### 3. F-String Decomposition

**Decision:** F-strings are tokenized into multiple specialized token types.

**Why:** This mirrors Python's approach and enables:
- Proper expression parsing within f-strings
- Nested f-string support
- Clean separation between lexer and parser responsibilities

The lexer handles f-string structure; the parser handles embedded expressions.

### 4. Boolean Operators as Keywords

**Decision:** `And`, `Or`, `Not`, and `Is` are keywords, not operators like `&&` or `||`.

**Why:** This follows Python's syntax:
```python
# Sharpy/Python style
if x and y:
    pass

# NOT C#/Java style
# if (x && y) { }
```

### 5. Position Tracking on Every Token

**Decision:** Every token carries `Line` and `Column` information.

**Why:** Essential for high-quality error messages:
```
Error: Undefined variable 'x' at line 42, column 10
    if x > 0:
       ^
```

**Cost:** ~8 bytes per token (two ints), negligible compared to the value of good diagnostics.

### 6. Immutable Record Type

**Decision:** Use C# `record` with `init`-only properties.

**Benefits:**
- Thread-safe by default (important for potential parallel analysis)
- Value semantics make testing easy
- Once created, tokens are snapshots that never change

---

## Debugging Tips

### 1. Inspecting Token Streams

When debugging lexer/parser issues, dump the token stream:

```csharp
var lexer = new Lexer(source);
var tokens = lexer.TokenizeAll();
foreach (var token in tokens)
{
    Console.WriteLine($"{token.Type,-20} '{token.Value}' at {token.Line}:{token.Column}");
}
```

**Output example:**
```
Def                  'def' at 1:1
Identifier           'hello' at 1:5
LeftParen            '(' at 1:10
RightParen           ')' at 1:11
Colon                ':' at 1:12
Newline              '' at 1:13
Indent               '' at 2:1
```

### 2. Using the CLI

The Sharpy CLI can emit tokens:
```bash
dotnet run --project src/Sharpy.Cli -- --emit-tokens file.spy
```

### 3. Checking Token Values

For tokens like `Identifier` or `String`, the content is in `Value`:
```csharp
if (token.Type == TokenType.Identifier)
{
    var name = token.Value;  // e.g., "myVariable"
}
```

For keywords and operators, use `Type` (not `Value`) for checks:
```csharp
// Don't do this (fragile, typo-prone):
if (token.Value == "def")

// Do this (type-safe):
if (token.Type == TokenType.Def)
```

### 4. INDENT/DEDENT Debugging

`Indent` and `Dedent` tokens have empty `Value` strings—the information is purely in the token type. When debugging indentation issues:
- Look at the sequence of `Indent`/`Dedent` tokens relative to `Newline` tokens
- Check the lexer's indentation stack state

### 5. Record Equality for Testing

Since `Token` is a record, you can compare tokens directly:
```csharp
var expected = new Token(TokenType.Plus, "+", 1, 5);
var actual = lexer.NextToken();
Assert.Equal(expected, actual);  // Works!
```

### 6. Common Issues

| Issue | Check | Solution |
|-------|-------|----------|
| Token positions off by one | Are Line/Column 0-indexed or 1-indexed? | They should be 1-indexed |
| F-string parsing fails | Is lexer tracking state correctly? | Add logging for state transitions |
| Indentation errors | Are Indent/Dedent tokens generated correctly? | Print the indentation stack |

---

## Contribution Guidelines

### Adding a New Keyword

1. **Add to `TokenType` enum** in the appropriate category:
   ```csharp
   // Keywords - Your Category
   YourKeyword,
   ```

2. **Update the keyword dictionary** in `Lexer.cs`:
   ```csharp
   { "yourkeyword", TokenType.YourKeyword }
   ```

3. **Update language specification** in `docs/language_specification/keywords.md`

4. **Add parser handling** if the keyword introduces new syntax

5. **Write tests** covering:
   - Lexer recognizes the keyword
   - Parser handles it correctly
   - Error cases (keyword used as identifier)

### Adding a New Operator

1. **Add to `TokenType` enum** with a comment showing the symbol:
   ```csharp
   // Operators - Special
   YourOperator,    // @@ (describe the symbol)
   ```

2. **Update `ReadOperatorOrDelimiter()`** in `Lexer.cs`

3. **Update parser** to handle the operator in expressions

4. **Document precedence** if it's a binary operator

### Adding a New Literal Type

Example: Adding byte string literals (`b"..."`)

1. **Add token type(s)**:
   ```csharp
   ByteString,  // b"bytes"
   ```

2. **Update lexer** to recognize the prefix and call appropriate reader

3. **Update parser** to create appropriate AST node

4. **Update code generator** to emit correct C# code

### Modifying Token Structure

**Caution:** The `Token` record is used throughout the codebase. Changes ripple widely.

**Safe additions:**
```csharp
// Add optional properties with default values
public string? Trivia { get; init; } = null;  // For preserving whitespace/comments
```

**Breaking changes** (require updates everywhere):
- Changing constructor signature
- Removing properties
- Renaming properties

**Best practice:** If you need additional metadata, consider a separate data structure rather than modifying `Token`.

### Testing Checklist

When modifying this file:
- [ ] All lexer tests pass (`dotnet test --filter "FullyQualifiedName~Lexer"`)
- [ ] Parser tests still pass (they consume tokens)
- [ ] Integration tests compile sample programs successfully
- [ ] Error messages display correct line/column numbers
- [ ] New token types are documented in language specs

---

## Relationship to Language Specification

The token types correspond directly to specification documents:

| Spec Document | Relevant Token Types |
|---------------|----------------------|
| `docs/language_specification/keywords.md` | All keyword tokens |
| `docs/language_specification/lexer_implementation.md` | Structural tokens, state machine |
| `docs/language_specification/string_literals.md` | `String`, `RawString`, f-string tokens |

When the language spec changes, this file must be updated to match. The enum serves as the **executable definition** of what tokens exist in the language.

---

## Related Files

- **`Lexer/Lexer.cs`**: The tokenization engine that creates `Token` instances
- **`Lexer/LexerError.cs`**: Exception type for lexical errors
- **`Parser/Parser.cs`**: Consumes token streams and builds the AST
- **`Lexer/LexerTests.cs`**: Unit tests for tokenization

---

## Quick Reference: Token Categories

| Category | Example Types | Purpose |
|----------|---------------|---------|
| **Literals** | `Integer`, `String`, `FStringStart` | Constant values in code |
| **Keywords** | `Def`, `Class`, `If`, `While` | Reserved language words |
| **Operators** | `Plus`, `Equal`, `And` | Operations on values |
| **Delimiters** | `LeftParen`, `Comma`, `Colon` | Structure and punctuation |
| **Whitespace** | `Indent`, `Dedent`, `Newline` | Python-style indentation |
| **Special** | `Eof`, `Comment`, `Backtick` | Meta-tokens and end markers |

---

## Summary

`Token.cs` is a small but foundational file that defines:
- **What tokens exist** in the Sharpy language (via `TokenType`)
- **How tokens are represented** at runtime (via `Token` record)

Understanding this file is essential because every phase of the compiler after lexing works with tokens. The parser pattern-matches on `TokenType` to build the AST, error reporters use source locations for messages, and tooling uses tokens for syntax highlighting.

**Key takeaways:**
- `TokenType` is exhaustive—if it can appear in source code, there's a token type for it
- `Token` is immutable and carries source location for error reporting
- Keywords are distinct types (not strings) for type safety
- F-strings use multiple token types to handle embedded expressions
- Indentation tokens (`Indent`/`Dedent`) make Python-style syntax possible

When you encounter a lexer or parser issue, this file is your reference for what tokens should be in play.

**Next steps after understanding Token.cs:**
1. Study `Lexer.cs` to see how tokens are created
2. Look at `LexerTests.cs` to see expected token sequences
3. Read `Parser.cs` to see how tokens become an AST
