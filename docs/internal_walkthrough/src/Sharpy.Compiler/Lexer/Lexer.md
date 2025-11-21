# Walkthrough: Lexer.cs

**Source File**: `src/Sharpy.Compiler/Lexer/Lexer.cs`

---

## 1. Overview

The `Lexer` class is the **first stage of the Sharpy compilation pipeline**. Its job is to transform raw source code (a string of characters) into a sequence of **tokens** — meaningful units like keywords, identifiers, operators, and literals that the parser can understand.

### What Makes Sharpy's Lexer Special?

Unlike many C-style languages, Sharpy (like Python) uses **indentation-based syntax**. This means the lexer must:
- Track indentation levels and emit `INDENT`/`DEDENT` tokens
- Handle implicit line continuation inside brackets `()`, `[]`, `{}`
- Support Python-style features like f-strings, raw strings, and triple-quoted strings
- Validate indentation rules (4-space indents, no tabs, no mixed indentation)

### Key Responsibilities

1. **Tokenization**: Convert source text into tokens (identifiers, keywords, numbers, strings, operators)
2. **Indentation Handling**: Generate `INDENT`/`DEDENT` tokens based on whitespace
3. **String Processing**: Handle regular strings, f-strings (with interpolation), raw strings, triple-quoted strings
4. **Number Parsing**: Support decimal, hexadecimal (`0x`), binary (`0b`), octal (`0o`), floats, scientific notation
5. **Error Reporting**: Track line/column positions and provide helpful error messages

---

## 2. Class Structure

### Core Fields

```csharp
private readonly string _source;           // Source code being tokenized
private int _position;                     // Current position in source
private int _line = 1;                     // Current line number (1-indexed)
private int _column = 1;                   // Current column number (1-indexed)
private readonly Stack<int> _indentStack;  // Stack of indentation levels
private readonly Queue<Token> _pendingTokens; // Queued DEDENT tokens
private bool _atLineStart = true;          // Are we at the start of a line?
private int _bracketDepth = 0;             // Depth of (), [], {} nesting
private readonly ICompilerLogger _logger;  // Optional logger for debugging
```

### Important Design Decisions

**Indentation Stack**: Tracks nested indentation levels. When indent increases, push the new level. When it decreases, pop levels and emit `DEDENT` tokens until we match a previous level.

**Pending Tokens Queue**: When dedenting multiple levels at once (e.g., from level 8 to level 0), we need to emit multiple `DEDENT` tokens. These are queued and returned one at a time.

**Bracket Depth Tracking**: Inside `()`, `[]`, or `{}`, newlines don't count as statement terminators, allowing multi-line expressions without backslash continuation.

### Keywords Dictionary

```csharp
private static readonly Dictionary<string, TokenType> Keywords = new()
{
    { "def", TokenType.Def },
    { "class", TokenType.Class },
    { "if", TokenType.If },
    { "True", TokenType.True },
    { "and", TokenType.And },
    // ... 40+ keywords total
};
```

This static dictionary maps keyword strings to their token types. It's checked whenever we read an identifier to determine if it's a keyword or a regular identifier.

---

## 3. Key Methods

### 3.1 `TokenizeAll()` - Main Entry Point

```csharp
public List<Token> TokenizeAll()
{
    var tokens = new List<Token>();
    while (true)
    {
        var token = NextToken();
        tokens.Add(token);
        if (token.Type == TokenType.Eof)
            break;
    }
    return tokens;
}
```

**Purpose**: Tokenize the entire source file into a list of tokens.

**How it works**: Repeatedly calls `NextToken()` until we hit EOF (end of file).

**Usage**: This is the primary method called by the compiler to get all tokens at once.

---

### 3.2 `NextToken()` - The Token State Machine

This is the **heart of the lexer**. It's a complex state machine that determines what type of token to read next.

**Flow**:

1. **Return pending tokens first** (queued `DEDENT` tokens from previous call)
2. **Handle EOF**: Emit any remaining `DEDENT` tokens, then `EOF`
3. **Handle indentation** at line start (if not inside brackets)
4. **Skip whitespace** (spaces/tabs, but NOT newlines)
5. **Dispatch based on current character**:
   - `#` → Comment (skip and recurse)
   - `\` → Line continuation
   - `\n` or `\r` → Newline token (or skip if inside brackets)
   - `"` or `'` → String literal
   - `f"` or `f'` → F-string
   - `r"` or `r'` → Raw string
   - `` ` `` → Literal name (backtick-delimited identifier)
   - Digit → Number
   - Letter or `_` → Identifier or keyword
   - Otherwise → Operator or delimiter

**Why this matters**: This dispatch logic determines how the lexer interprets every character. Understanding this flow is crucial for debugging tokenization issues.

---

### 3.3 `MeasureIndentation()` - Indentation Analysis

**Purpose**: Measure the indentation at the start of a line and validate it.

**Algorithm**:

```csharp
1. Count leading spaces (increment indent counter)
2. If we encounter a tab → ERROR (tabs not allowed)
3. If we encounter a newline or comment → return current indent (empty/comment line)
4. Validate:
   - No mixed spaces and tabs
   - Indent is multiple of 4
   - If dedenting, new level matches a previous level in the stack
5. Update _position and _column to skip the indentation
6. Return the measured indent level
```

**Critical Validation Rules**:
- **No tabs**: `throw new LexerError("Tabs are not allowed for indentation. Use 4 spaces.")`
- **Must be multiple of 4**: `if (indent % 4 != 0) throw ...`
- **Dedent must match previous level**: When dedenting, the new indent must match one of the previous levels in the stack, otherwise it's an indentation mismatch error

**Edge Cases**:
- Empty lines (only whitespace) → Don't change indentation
- Comment-only lines → Don't change indentation
- EOF after whitespace → Treat as empty line

---

### 3.4 String Reading Methods

The lexer supports **four types of string literals**:

#### `ReadString()` - Regular Strings

Handles single-line and triple-quoted strings with escape sequences.

```python
# Single-line
"hello"
'world'

# Triple-quoted (can span lines)
"""
Multi-line
string
"""
```

**Escape sequences supported**: `\n`, `\r`, `\t`, `\\`, `\'`, `\"`, `\xHH` (hex), `\uHHHH` (unicode), `\UHHHHHHHH` (long unicode), `\ooo` (octal)

#### `ReadFString()` - Format Strings (Interpolation)

```python
name = "Alice"
message = f"Hello, {name}!"  # f-string with {expression}
```

**How it works**:
- Lexer reads the entire f-string including `{...}` braces
- Tracks brace depth to handle nested braces
- Parser later extracts and parses expressions inside `{...}`

**Important**: The lexer doesn't parse the expressions inside `{}`, it just collects them as part of the string token. The parser handles the interpolation logic.

#### `ReadRawString()` - Raw Strings

```python
path = r"C:\Users\Alice\Documents"  # Backslashes are literal
```

**Key difference**: No escape sequence processing. `\n` is two characters: `\` and `n`.

#### Triple-Quoted Variants

All string types support triple-quoted variants (`"""` or `'''`) for multi-line strings. The lexer tracks line numbers correctly as it reads across newlines.

---

### 3.5 Number Reading Methods

The lexer supports **extensive numeric literal syntax**:

#### `ReadNumber()` - Decimal Numbers

```python
42           # Integer
3.14         # Float
1.5e10       # Scientific notation (1.5 × 10^10)
1_000_000    # Underscores for readability
42.0f        # Float suffix
123L         # Long suffix
```

**Validation**:
- Underscores: Cannot be consecutive (`1__2` is invalid), cannot end with underscore
- Scientific notation: Must have digits after `e` or `E`
- Suffixes: `f`, `F`, `d`, `D`, `m`, `M`, `l`, `L`, `u`, `U`, `ul`, `UL` (case-insensitive combinations)

#### `ReadHexNumber()` - Hexadecimal

```python
0xFF        # 255
0x1A2B      # 6699
0x_DEAD_BEEF  # Underscores allowed
```

Prefix: `0x` or `0X`, followed by hex digits (`0-9`, `a-f`, `A-F`)

#### `ReadBinaryNumber()` - Binary

```python
0b1010      # 10
0b_1111_0000  # 240
```

Prefix: `0b` or `0B`, followed by binary digits (`0`, `1`)

**Validation**: Non-binary digits like `2` raise an error.

#### `ReadOctalNumber()` - Octal

```python
0o755       # 493
0o_1234     # 668
```

Prefix: `0o` or `0O`, followed by octal digits (`0-7`)

**Validation**: Digits like `8` or `9` raise an error.

---

### 3.6 `ReadIdentifierOrKeyword()` - Identifiers and Keywords

```csharp
private Token ReadIdentifierOrKeyword()
{
    // Read [a-zA-Z_][a-zA-Z0-9_]*
    var value = sb.ToString();
    
    // Check if it's a keyword
    if (Keywords.TryGetValue(value, out var tokenType))
        return new Token(tokenType, value, ...);
    
    return new Token(TokenType.Identifier, value, ...);
}
```

**Algorithm**:
1. Read while current character is letter, digit, or underscore
2. Check if the result is a keyword (case-sensitive!)
3. If yes, return keyword token; otherwise, identifier token

**Important**: Keywords are case-sensitive (`True` is a keyword, but `true` is an identifier).

---

### 3.7 `ReadOperatorOrDelimiter()` - Operators

This method handles **all operators and punctuation**:

**Three-character operators**:
- `...` (ellipsis)
- `<<=`, `>>=`, `**=`, `//=` (augmented assignment)

**Two-character operators**:
- Comparisons: `==`, `!=`, `<=`, `>=`
- Bitwise shifts: `<<`, `>>`
- Arithmetic: `**` (power), `//` (floor division)
- Null operators: `?.` (null-conditional), `??` (null-coalesce)
- Arrow: `->` (function return type annotation)
- Augmented assignment: `+=`, `-=`, `*=`, `/=`, `%=`, `&=`, `|=`, `^=`

**Single-character operators**:
- Arithmetic: `+`, `-`, `*`, `/`, `%`
- Bitwise: `&`, `|`, `^`, `~`
- Comparison: `<`, `>`, `=`
- Delimiters: `(`, `)`, `[`, `]`, `{`, `}`, `,`, `:`, `;`, `.`, `@`, `?`

**Bracket Depth Tracking**:
```csharp
if (c == '(' || c == '[' || c == '{')
    _bracketDepth++;
else if (c == ')' || c == ']' || c == '}')
    _bracketDepth--;
```

This allows implicit line continuation inside brackets without needing `\` at line ends.

---

### 3.8 `ProcessEscapeSequence()` - Escape Sequence Handling

Called by string reading methods to convert escape sequences into actual characters.

**Supported sequences**:
- Standard: `\n` → newline, `\t` → tab, `\\` → backslash
- Hex: `\xHH` → character with hex value HH
- Unicode: `\uHHHH` → character with 4-digit unicode, `\UHHHHHHHH` → 8-digit unicode
- Octal: `\ooo` → character with octal value (max 255)

**Error handling**: Invalid escape sequences throw `LexerError` with specific message.

---

## 4. Dependencies

### Internal Dependencies

- **`Token.cs`**: The token data structure returned by the lexer
- **`TokenType.cs`**: Enum of all token types (Def, Class, If, Identifier, Integer, etc.)
- **`LexerError.cs`**: Exception type for lexical errors
- **`Sharpy.Compiler.Logging.ICompilerLogger`**: Logging interface (optional, defaults to NullLogger)

### External Dependencies

- **`System.Text.StringBuilder`**: Efficient string building while reading tokens
- **`System.Collections.Generic.Stack`**: For indentation level tracking
- **`System.Collections.Generic.Queue`**: For pending DEDENT tokens
- **`System.Collections.Generic.Dictionary`**: For keyword lookup

---

## 5. Patterns and Design Decisions

### 5.1 State Machine Architecture

The lexer is a **character-by-character state machine**:
- Current state is implicit (based on `_position`, `_line`, `_column`, `_atLineStart`, `_bracketDepth`)
- Each token reading method is a mini-state machine for that token type
- Recursive calls to `NextToken()` handle comments and EOF dedents

**Why this works**: Simple, predictable, and easy to debug. Each method has a clear responsibility.

### 5.2 Indentation as Tokens

Python-style indentation is converted to explicit `INDENT`/`DEDENT` tokens:

```python
if x > 0:     # After colon, next line must indent
    print(x)  # INDENT emitted here
    print(y)  
              # DEDENT emitted when returning to level 0
```

**Why this matters**: The parser doesn't need to understand indentation — it just sees `INDENT` and `DEDENT` tokens like opening/closing braces in C-style languages.

### 5.3 Two-Pass Operator Matching

Operators are checked from longest to shortest (3-char, 2-char, 1-char):

```csharp
if (_position + 2 < _source.Length)
    // Check 3-char operators like "..."
if (_position + 1 < _source.Length)
    // Check 2-char operators like "=="
// Single-char operators
```

**Why**: Prevents `>>` from being tokenized as two `>` tokens instead of a single right-shift.

### 5.4 Bracket-Aware Line Continuation

```python
result = (
    1 + 2 +
    3 + 4
)  # No backslash needed inside brackets
```

When `_bracketDepth > 0`, newlines are skipped and don't trigger indentation measurement.

**Implementation**: `ReadOperatorOrDelimiter()` updates `_bracketDepth` when it encounters `()`, `[]`, or `{}`.

### 5.5 Position Tracking

Every token records its `Line` and `Column`:
- Used for error messages ("Error at line 42, column 10")
- Critical for IDE features (go-to-definition, error squiggles)

**Consistency**: `_line` and `_column` are updated immediately after reading characters, ensuring accuracy.

---

## 6. Debugging Tips

### 6.1 Enable Logging

The lexer supports optional logging:

```csharp
var logger = new ConsoleLogger();
var lexer = new Lexer(source, logger);
```

This logs every token read, indentation changes, and position updates.

### 6.2 Common Errors and How to Fix Them

**"Indentation mismatch"**:
- Cause: Dedenting to a level that doesn't match any previous indent
- Fix: Check that dedents align with previous indents (must be multiple of 4)
- Debug: Print the indent stack when error occurs

**"Unterminated string literal"**:
- Cause: String spans a newline without being triple-quoted, or missing closing quote
- Fix: Check that single-line strings don't have newlines, or use `"""..."""`
- Debug: Check `_position` when error is thrown to see where the string started

**"Invalid escape sequence"**:
- Cause: Unknown escape like `\q` or incomplete escape like `\x` without hex digits
- Fix: Use valid escapes or raw strings (`r"..."`)
- Debug: Check which escape was attempted in the error message

**"Mixed tabs and spaces in indentation"**:
- Cause: Some lines use tabs, others use spaces
- Fix: Convert all tabs to spaces (4 spaces per indent level)
- Debug: Use a text editor that shows whitespace characters

### 6.3 Testing Strategy

**Test each token type independently**:
```csharp
[Fact]
public void TestTokenizeInteger()
{
    var lexer = new Lexer("42");
    var tokens = lexer.TokenizeAll();
    Assert.Equal(TokenType.Integer, tokens[0].Type);
    Assert.Equal("42", tokens[0].Value);
}
```

**Test edge cases**:
- Empty input
- Input with only whitespace
- Input with only comments
- Deeply nested indentation
- All operator combinations

**Test error cases**:
```csharp
[Fact]
public void TestInvalidIndentation()
{
    var lexer = new Lexer("  x = 1");  // 2 spaces, not 4
    Assert.Throws<LexerError>(() => lexer.TokenizeAll());
}
```

### 6.4 Useful Debugging Techniques

**Print token stream**:
```csharp
foreach (var token in tokens)
    Console.WriteLine($"{token.Type,-20} {token.Value,-20} L{token.Line}:C{token.Column}");
```

**Inspect indentation stack**:
Add temporary logging in `MeasureIndentation()` to see how the stack changes.

**Use the `--emit-tokens` flag**:
```bash
sharpyc build test.spy --emit-tokens
```

This prints the token stream to see exactly what the lexer produced.

---

## 7. Contribution Guidelines

### 7.1 When to Modify the Lexer

**Add new keywords**:
1. Add to `TokenType` enum (in `Token.cs`)
2. Add to `Keywords` dictionary in `Lexer.cs`
3. Add tests in `LexerTests.cs`

**Add new operators**:
1. Add to `TokenType` enum
2. Add to `ReadOperatorOrDelimiter()` (check length: 3-char, 2-char, or 1-char)
3. Add tests for the operator in isolation and combinations

**Add new literal types**:
1. Add to `TokenType` enum
2. Add recognition in `NextToken()` dispatch
3. Implement `ReadXYZ()` method following existing patterns
4. Add comprehensive tests (valid cases, invalid cases, edge cases)

### 7.2 Testing Checklist

Before submitting changes:

- [ ] Unit tests for the new feature pass
- [ ] Existing lexer tests still pass (`dotnet test --filter "FullyQualifiedName~Lexer"`)
- [ ] Manual testing with real Sharpy code
- [ ] Error messages are clear and actionable
- [ ] Position tracking (line/column) is accurate
- [ ] Documentation updated (this walkthrough, language reference)

### 7.3 Code Style

**Follow existing patterns**:
- Methods that read tokens return `Token`
- Methods that process characters update `_position`, `_line`, `_column`
- Always throw `LexerError` with line/column info for errors
- Use `StringBuilder` for accumulating strings
- Use `Peek()` helper to look ahead without advancing position

**Error messages should be helpful**:
```csharp
// ❌ Bad
throw new LexerError("Invalid number");

// ✅ Good
throw new LexerError($"Invalid binary digit: '{c}' (only 0 and 1 allowed)", _line, _column);
```

### 7.4 Common Pitfalls to Avoid

**Don't forget to update position**:
```csharp
// ❌ Forgot to update _column
_position++;

// ✅ Correct
_position++;
_column++;
```

**Don't break indentation tracking**:
- Always maintain `_atLineStart` state correctly
- Always validate indentation rules strictly
- Remember to check `_bracketDepth` before measuring indentation

**Don't modify `_source`**:
- The source string is `readonly` for a reason
- Use `_position` to track where you are, never mutate the source

### 7.5 Performance Considerations

**Current optimizations**:
- Keywords use dictionary lookup (O(1) average case)
- StringBuilder for string accumulation (avoid string concatenation)
- Single pass through source (no backtracking)

**Future optimization opportunities**:
- Token pooling to reduce allocations
- Lazy tokenization (only tokenize as parser requests)
- Parallel lexing of independent files

---

## 8. Example Walkthrough: Tokenizing a Simple Program

Let's trace how the lexer processes this code:

```python
def greet(name: str) -> str:
    return f"Hello, {name}!"
```

### Token Stream:

| Position | Character | Action | Token Emitted |
|----------|-----------|--------|---------------|
| 0 | `d` | Read identifier "def" | `Def` |
| 3 | ` ` | Skip whitespace | - |
| 4 | `g` | Read identifier "greet" | `Identifier("greet")` |
| 9 | `(` | Read delimiter | `LeftParen` |
| 10 | `n` | Read identifier "name" | `Identifier("name")` |
| 14 | `:` | Read delimiter | `Colon` |
| 15 | ` ` | Skip whitespace | - |
| 16 | `s` | Read identifier "str" | `Identifier("str")` |
| 19 | `)` | Read delimiter | `RightParen` |
| 20 | ` ` | Skip whitespace | - |
| 21 | `-` | Check for `->` | `Arrow` |
| 23 | ` ` | Skip whitespace | - |
| 24 | `s` | Read identifier "str" | `Identifier("str")` |
| 27 | `:` | Read delimiter | `Colon` |
| 28 | `\n` | Newline (line 2 starts) | `Newline` |
| 29 | ` ` | Measure indent (4 spaces) | `Indent` |
| 33 | `r` | Read identifier "return" | `Return` |
| 39 | ` ` | Skip whitespace | - |
| 40 | `f` | Read f-string | `FString("Hello, {name}!")` |
| 58 | `\n` | Newline (line 3) | `Newline` |
| 59 | EOF | Dedent to 0, then EOF | `Dedent`, `Eof` |

**Key observations**:
- Indentation before "return" triggers `Indent` token
- F-string reads the entire string including `{name}!` as the value
- At EOF, remaining indentation levels generate `Dedent` tokens

---

## 9. Related Files and Next Steps

### Related Files

- **`Token.cs`**: Token data structure definition
- **`TokenType.cs`**: Enum of all token types
- **`LexerError.cs`**: Lexer-specific exception type
- **`Parser/Parser.cs`**: Next stage that consumes tokens
- **`Sharpy.Compiler.Tests/Lexer/LexerTests.cs`**: Comprehensive test suite

### Next Steps After Understanding the Lexer

1. **Study the Parser**: See how tokens are assembled into an Abstract Syntax Tree (AST)
2. **Look at Token Types**: Understand all available token types in `TokenType.cs`
3. **Read Lexer Tests**: See examples of every token type and edge case
4. **Experiment**: Write small Sharpy snippets and use `--emit-tokens` to see tokenization

### Further Reading

- **Language Reference**: `docs/specs/language_reference.md` - Formal grammar and syntax rules
- **Python Lexer**: Python's tokenizer (inspiration for Sharpy's design)
- **Compiler Architecture**: `docs/architecture/compiler_pipeline.md` - How lexer fits into full pipeline

---

## Summary

The `Lexer` is a **robust, Python-inspired tokenizer** that handles:
- Indentation-based syntax with `INDENT`/`DEDENT` tokens
- Multiple string literal types (regular, f-strings, raw, triple-quoted)
- Comprehensive numeric literal support (decimal, hex, binary, octal, floats)
- All Sharpy operators and keywords
- Precise error reporting with line/column tracking

Understanding the lexer is **essential** for:
- Adding new language features
- Debugging syntax errors
- Understanding how Sharpy source code is initially processed
- Contributing to the compiler

Start with the high-level flow (`NextToken()` dispatch), then dive into specific token types as needed. The code is well-structured with clear separation of concerns—each token type has its own reading method.

Happy hacking! 🚀
