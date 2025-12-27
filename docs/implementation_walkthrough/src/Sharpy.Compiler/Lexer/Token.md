# Walkthrough: Token.cs

**Source File**: `src/Sharpy.Compiler/Lexer/Token.cs`

---

## 1. Overview

The `Token.cs` file is the fundamental data structure for the **lexical analysis** phase of the Sharpy compiler. It defines:

- **`TokenType`**: An enumeration of all possible token categories in the Sharpy language
- **`Token`**: A record representing a single lexical unit (token) with its type, value, and position

This file sits at the very beginning of the compilation pipeline. When source code is fed into the lexer, it's broken down into a stream of `Token` objects that the parser can then work with. Think of tokens as the "words" of the programming language, where the lexer is performing the equivalent of breaking prose into individual words and punctuation marks.

**Key Role**: This file doesn't contain any logic—it's purely **data definitions**. The actual tokenization logic lives in `Lexer.cs`, which creates instances of these types.

---

## 2. Class/Type Structure

### 2.1 `TokenType` Enum

```csharp
public enum TokenType
{
    // 135 different token types organized into logical groups
}
```

The `TokenType` enum is the heart of this file. It catalogs **every distinct lexical element** that can appear in Sharpy source code. The enum is organized into intuitive categories:

#### **Literals** (lines 8-21)
- Numeric: `Integer`, `Float`
- String types: `String`, `RawString` (Python's r-strings)
- F-string components: `FStringStart`, `FStringText`, `FStringExprStart`, etc.
  - *Note*: F-strings require multiple tokens because they mix literal text with embedded expressions
- Boolean and null: `True`, `False`, `None`

#### **Keywords** (lines 24-63)
Organized by purpose:
- **Control Flow**: `If`, `Elif`, `Else`, `While`, `For`, `Return`, `Break`, `Continue`, etc.
- **Definitions**: `Def`, `Class`, `Struct`, `Interface`, `Enum`
- **Imports**: `Import`, `From`, `As`
- **Type System**: `Auto` (type inference), `Const`, `Lambda`
- **Boolean Logic**: `And`, `Or`, `Not`, `Is` (keywords, not operators)

#### **Operators** (lines 65-109)
Grouped by function:
- **Arithmetic**: `Plus`, `Minus`, `Star`, `Slash`, `DoubleSlash` (floor division), `DoubleStar` (exponentiation)
- **Comparison**: `Equal`, `NotEqual`, `Less`, `Greater`, etc.
- **Bitwise**: `Ampersand`, `Pipe`, `Caret`, `Tilde`, `LeftShift`, `RightShift`
- **Assignment**: `Assign`, `PlusAssign`, `MinusAssign`, etc. (compound assignments)
- **Special**: `Question` (nullable type marker), `NullConditional` (`?.`), `NullCoalesce` (`??`), `Ellipsis` (`...`)

#### **Delimiters** (lines 111-124)
Punctuation and structural markers:
- Brackets: `LeftParen`, `RightParen`, `LeftBracket`, `RightBracket`, `LeftBrace`, `RightBrace`
- Separators: `Comma`, `Colon`, `Semicolon`, `Dot`
- Special: `Arrow` (function return type `->`, decorators `@`)

#### **Special Tokens** (lines 127-134)
- **Indentation-sensitive**: `Indent`, `Dedent`, `Newline` (Python-style whitespace significance)
- **Structural**: `Eof` (end of file)
- **Miscellaneous**: `Backtick` (for literal/escaped names), `Comment`, `Backslash` (line continuation)

### 2.2 `Token` Record

```csharp
public record Token
{
    public TokenType Type { get; init; }
    public string Value { get; init; } = string.Empty;
    public int Line { get; init; }
    public int Column { get; init; }
}
```

The `Token` record is a **immutable data container** representing a single token. It uses C# 9+ record syntax for concise, value-based equality.

**Properties**:

- **`Type`**: Which kind of token this is (from the `TokenType` enum)
- **`Value`**: The actual text from the source code (e.g., `"42"`, `"myVariable"`, `"+"`)
  - For keywords/operators, this might be redundant with the type, but it preserves the original source text
  - For identifiers and literals, this contains the actual value
- **`Line`** and **`Column`**: Position in the source file (1-indexed)
  - Critical for error reporting: "Syntax error at line 42, column 10"
  - Used throughout the compiler for diagnostics

**Why a record?**
- Immutability: Once created, tokens never change (safe for concurrent analysis)
- Value equality: Two tokens with same Type/Value/Line/Column are considered equal
- Concise syntax: `with` expressions allow non-destructive mutations if needed
- Pattern matching: Records work well with C# pattern matching in the parser

---

## 3. Key Functions/Methods

### 3.1 Constructor

```csharp
public Token(TokenType type, string value, int line, int column)
{
    Type = type;
    Value = value;
    Line = line;
    Column = column;
}
```

**Purpose**: Creates a new token instance with all required information.

**Parameters**:
- `type`: The category of token (keyword, operator, identifier, etc.)
- `value`: The literal text from source code
- `line`: 1-indexed line number in source file
- `column`: 1-indexed column number (character position in line)

**Usage Pattern** (in `Lexer.cs`):
```csharp
// When the lexer recognizes "def" at position 1:5
var token = new Token(TokenType.Def, "def", line: 1, column: 5);
```

**Design Note**: The constructor is straightforward because tokens are simple data carriers. All the intelligence lives in the lexer that creates them.

---

## 4. Dependencies

### 4.1 Inbound Dependencies (Who Uses This File)

- **`Lexer.cs`**: Creates `Token` instances during tokenization
- **`Parser/Parser.cs`**: Consumes token streams, pattern matches on `TokenType`
- **All Parser AST files**: Store position info using `Token.Line` and `Token.Column`
- **`Diagnostics/`**: Uses position info for error messages
- **Semantic analysis**: Indirectly via AST nodes that track token positions

### 4.2 Outbound Dependencies (What This File Needs)

**None**. This file is completely self-contained—no imports beyond the `namespace` declaration. This is intentional design for a foundational data structure.

---

## 5. Patterns and Design Decisions

### 5.1 Enum-Based Token Types

**Decision**: Use a single large enum rather than a class hierarchy.

**Rationale**:
- **Performance**: Enums are lightweight integers (no heap allocation)
- **Exhaustiveness**: Pattern matching can check all cases
- **Simplicity**: Easy to add new token types without complex inheritance

**Alternative Considered**: A class hierarchy (`Token` base with `KeywordToken`, `OperatorToken`, etc.) would be more OOP but adds unnecessary complexity for simple data.

### 5.2 Immutable Record

**Decision**: Use C# `record` with `init`-only properties.

**Benefits**:
- Thread-safe by default (important for parallel parsing experiments)
- Value semantics: Easy to compare tokens in tests
- Structural equality: `token1 == token2` works intuitively

**Impact**: Once created, tokens are snapshots of a moment in lexical analysis. If you need to "modify" a token, use `with` expressions:
```csharp
var newToken = oldToken with { Line = 5 };
```

### 5.3 Positional Information

**Decision**: Every token carries `Line` and `Column` information.

**Why**: Enables high-quality error messages throughout the pipeline:
```
Error: Undefined variable 'x' at line 42, column 10
    if x > 0:
       ^
```

**Cost**: ~8 bytes per token (two ints), but this is negligible compared to the value of good diagnostics.

### 5.4 F-String Token Decomposition

**Decision**: F-strings are broken into multiple token types (`FStringStart`, `FStringText`, `FStringExprStart`, etc.).

**Why**: F-strings like `f"Hello {name}, you are {age} years old"` mix literal text with expressions. The lexer needs to track state as it transitions between text and expressions.

**Example Token Stream**:
```python
f"Result: {x + 1}"
```
Produces:
1. `FStringStart` (`f"`)
2. `FStringText` (`"Result: "`)
3. `FStringExprStart` (`{`)
4. `Identifier` (`x`)
5. `Plus` (`+`)
6. `Integer` (`1`)
7. `FStringExprEnd` (`}`)
8. `FStringEnd` (`"`)

### 5.5 Python-Inspired Indentation Tokens

**Decision**: Include `Indent`, `Dedent`, and `Newline` as first-class tokens.

**Context**: Sharpy uses Python-style significant whitespace. The lexer converts invisible indentation changes into explicit `Indent` and `Dedent` tokens, making the parser's job simpler.

**Example**:
```python
def foo():      # <- Newline here
    x = 1       # <- Indent before this line
    if True:    # <- Newline
        y = 2   # <- Indent
                # <- Dedent after this
    z = 3       # <- Back to function body indent
                # <- Dedent to module level
```

---

## 6. Debugging Tips

### 6.1 Inspecting Token Streams

When debugging lexer issues, you can dump token streams:

```csharp
// In Lexer.cs or a test
var tokens = lexer.Tokenize();
foreach (var token in tokens)
{
    Console.WriteLine($"{token.Type,-20} '{token.Value}' at {token.Line}:{token.Column}");
}
```

**Output Example**:
```
Def                  'def' at 1:1
Identifier           'hello' at 1:5
LeftParen            '(' at 1:10
RightParen           ')' at 1:11
Colon                ':' at 1:12
Newline              '\n' at 1:13
Indent               '    ' at 2:1
```

### 6.2 Common Issues

**Issue**: Token positions are off by one.
- **Check**: Are Line/Column 0-indexed or 1-indexed? (They should be 1-indexed)
- **Solution**: Look for `line + 1` or `column + 1` in lexer code

**Issue**: F-string parsing fails.
- **Check**: Is the lexer tracking state correctly through `FStringStart → FStringExprStart → FStringExprEnd` transitions?
- **Solution**: Add logging in `Lexer.cs` to print state changes

**Issue**: Indentation errors in Python-style code.
- **Check**: Are `Indent`/`Dedent` tokens being generated correctly?
- **Tip**: Print the indentation stack in the lexer to see nesting levels

### 6.3 Test Strategies

**Unit Test Pattern**:
```csharp
[Fact]
public void Lexer_Tokenizes_DefKeyword()
{
    var lexer = new Lexer("def foo():");
    var tokens = lexer.Tokenize();
    
    Assert.Equal(TokenType.Def, tokens[0].Type);
    Assert.Equal("def", tokens[0].Value);
    Assert.Equal(1, tokens[0].Line);
    Assert.Equal(1, tokens[0].Column);
}
```

**Integration Test**: Compile a small Sharpy program and check tokens are passed correctly to the parser.

---

## 7. Contribution Guidelines

### 7.1 Adding a New Token Type

**When to add**: Introducing a new language feature that needs a new keyword, operator, or literal type.

**Steps**:
1. **Add to enum**: Insert in the appropriate category in `TokenType`
2. **Update lexer**: Modify `Lexer.cs` to recognize and emit the new token
3. **Add tests**: Create tests in `Lexer/LexerTests.cs`
4. **Update parser**: Add parsing logic for the new token in `Parser/Parser.cs`
5. **Document**: Add to language spec if it's a user-facing feature

**Example**: Adding a `match` statement keyword:
```csharp
// In TokenType enum (around line 40)
Match,

// In Lexer.cs
if (word == "match")
    return new Token(TokenType.Match, word, line, column);
```

### 7.2 Modifying Token Structure

**Caution**: The `Token` record is used throughout the codebase. Changes here ripple widely.

**Safe additions**:
- Add optional properties with default values:
  ```csharp
  public string? Trivia { get; init; } = null;  // For preserving whitespace/comments
  ```

**Breaking changes** (require updates everywhere):
- Changing constructor signature
- Removing properties
- Renaming properties

**Best practice**: If you need to track additional token metadata, consider adding it to a separate data structure (e.g., `TokenMetadata` dictionary) rather than modifying `Token` itself.

### 7.3 Refactoring Considerations

**Potential improvements**:
- **Span support**: Replace `string Value` with `ReadOnlySpan<char>` for zero-allocation tokenization (performance optimization)
- **Interning**: Intern common token values (keywords, operators) to reduce memory usage
- **Token trivia**: Add support for preserving whitespace/comments for code formatters

**Anti-patterns to avoid**:
- Adding behavior/methods to `Token` (keep it as pure data)
- Making properties mutable (breaks thread-safety guarantees)
- Storing complex objects in `Value` (keep it as a string)

### 7.4 Testing Checklist

When modifying this file:
- [ ] All lexer tests pass (`dotnet test --filter "FullyQualifiedName~Lexer"`)
- [ ] Parser tests still pass (they consume tokens)
- [ ] Integration tests compile sample programs successfully
- [ ] Error messages still display correct line/column numbers
- [ ] Added documentation for new token types in language specs

---

## 8. Related Files

- **`Lexer/Lexer.cs`**: The tokenization engine that creates `Token` instances
- **`Parser/Parser.cs`**: Consumes token streams and builds the AST
- **`Diagnostics/Logger.cs`**: Uses token position info for error reporting
- **`Lexer/LexerTests.cs`**: Unit tests for tokenization
- **`Parser/Ast/Node.cs`**: AST nodes store token positions for source mapping

---

## 9. Quick Reference: Token Categories

| Category | Example Types | Purpose |
|----------|---------------|---------|
| **Literals** | `Integer`, `String`, `FStringStart` | Constant values in code |
| **Keywords** | `Def`, `Class`, `If`, `While` | Reserved language words |
| **Operators** | `Plus`, `Equal`, `And` | Operations on values |
| **Delimiters** | `LeftParen`, `Comma`, `Colon` | Structure and punctuation |
| **Whitespace** | `Indent`, `Dedent`, `Newline` | Python-style indentation |
| **Special** | `Eof`, `Comment`, `Backtick` | Meta-tokens and end markers |

---

## 10. Learning Path

**For newcomers**, study files in this order:
1. **Token.cs** (you are here) - Understand the data
2. **Lexer.cs** - See how tokens are created
3. **LexerTests.cs** - Learn expected behavior through examples
4. **Parser.cs** - See how tokens are consumed
5. **AST nodes** - Understand how tokens become syntax trees

**Key insight**: Tokens are the **contract** between the lexer and parser. The lexer promises to produce well-formed tokens; the parser promises to handle any valid token sequence.

---

## Appendix: Historical Context

The token design reflects Sharpy's goal of being a **Pythonic language for .NET**:

- **Python influences**: 
  - Significant whitespace (`Indent`/`Dedent` tokens)
  - F-strings (complex tokenization for string interpolation)
  - Keyword operators (`and`, `or`, `not`, `is`)
  - Floor division (`//`) and exponentiation (`**`)

- **.NET influences**:
  - Null-conditional operators (`?.`, `??`)
  - Explicit type annotations (`->` for return types)
  - `Struct`, `Interface` keywords (not in Python)

This dual heritage is visible in every decision in `Token.cs`—it's not just a token file, it's a **map of two language worlds meeting**.
