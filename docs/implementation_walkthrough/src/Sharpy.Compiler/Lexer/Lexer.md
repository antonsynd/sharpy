# Walkthrough: Lexer.cs

**Source File**: `src/Sharpy.Compiler/Lexer/Lexer.cs`

---

## Overview

The `Lexer` class is the first stage of the Sharpy compiler pipeline. Its job is to transform raw source code text into a stream of **tokens**—the basic building blocks that the parser will use to construct an Abstract Syntax Tree (AST).

**What makes this lexer special:**
- **Indentation-based syntax**: Like Python, Sharpy uses indentation for block structure. The lexer generates special `INDENT` and `DEDENT` tokens to represent these blocks.
- **F-string support**: Handles complex f-string literals with embedded expressions (e.g., `f"Hello {name}!"`).
- **Python-compatible features**: Supports triple-quoted strings, raw strings (r-strings), various numeric literals (hex, binary, octal), and line continuations.

**Pipeline position:**
```
Source (.spy) → [LEXER] → Tokens → Parser → AST → Semantic Analysis → Code Generation
```

---

## Class Structure

### Main Class: `Lexer`

```csharp
public class Lexer
```

The lexer is a **stateful scanner** that maintains:
- Position tracking (`_position`, `_line`, `_column`)
- Indentation stack for Python-style blocks
- F-string context stack for nested string interpolation
- Token queue for multi-token scenarios (like multiple DEDENTs)

### Key State Fields

```csharp
private readonly string _source;           // The source code being tokenized
private int _position;                     // Current character position
private int _line = 1;                     // Current line number
private int _column = 1;                   // Current column number
private readonly Stack<int> _indentStack;  // Stack of indentation levels
private readonly Queue<Token> _pendingTokens;  // Queued tokens (for INDENT/DEDENT)
private bool _atLineStart = true;          // Are we at the start of a line?
private int _bracketDepth = 0;             // Track nesting: (), [], {}
```

**Why track bracket depth?** Inside brackets/parens/braces, Python ignores newlines for implicit line continuation:
```python
my_list = [
    1, 2, 3,  # Newline ignored
    4, 5, 6
]
```

### F-String Context

```csharp
private class FStringContext
{
    public char QuoteChar { get; set; }      // " or '
    public bool IsTriple { get; set; }       // Triple-quoted?
    public int BraceDepth { get; set; }      // How many { deep are we?
    public bool InFormatSpec { get; set; }   // After : in {expr:format}?
}
```

F-strings can be nested and contain complex expressions. The lexer uses a stack to track this state as it switches between "literal text mode" and "expression mode."

### Keywords Dictionary

```csharp
private static readonly Dictionary<string, TokenType> Keywords = new()
{
    { "def", TokenType.Def },
    { "class", TokenType.Class },
    // ... 40+ keywords
}
```

This maps identifier strings to their keyword token types. Keywords are reserved words that can't be used as variable names.

---

## Constructor

```csharp
public Lexer(string source, ICompilerLogger? logger = null, 
             int startLine = 1, int startColumn = 1)
```

**Parameters:**
- `source`: The Sharpy source code to tokenize
- `logger`: Optional logger for diagnostics
- `startLine`/`startColumn`: Used when lexing fragments (like f-string expressions)

**Initialization:**
- Pushes 0 onto indent stack (base indentation level)
- If starting at non-default position, disables indentation measurement (we're lexing a fragment)

---

## Key Public Methods

### `TokenizeAll()` - Get All Tokens at Once

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

**Use case:** When you need all tokens upfront (e.g., for testing or IDE features).

### `NextToken()` - The Core Token Generator

```csharp
public Token NextToken()
```

This is the heart of the lexer. It's a **big state machine** that:
1. Returns any pending tokens (INDENT/DEDENT) first
2. Handles f-string mode if active
3. Checks for EOF and generates final DEDENTs
4. Measures indentation at line starts
5. Delegates to specific read methods based on current character

**Key flow:**
```
NextToken()
  └─ Pending tokens? → Return queued token
  └─ In f-string? → NextFStringToken()
  └─ At EOF? → Generate DEDENTs, then EOF
  └─ At line start? → MeasureIndentation() → Generate INDENT/DEDENT
  └─ Skip whitespace
  └─ Switch on current character:
       ├─ # → ReadComment()
       ├─ " or ' → ReadString()
       ├─ f" or f' → ReadFStringStart()
       ├─ Digit → ReadNumber()
       ├─ Letter/_ → ReadIdentifierOrKeyword()
       └─ Operator char → ReadOperatorOrDelimiter()
```

---

## Indentation Handling

### The Challenge

Python-style indentation is tricky:
```python
def foo():
    if x:        # INDENT after colon
        print(x)
    print(y)     # DEDENT back to function level
print(z)         # DEDENT back to module level
```

The lexer must generate invisible `INDENT` and `DEDENT` tokens that represent block boundaries.

### `MeasureIndentation()` - Enforcing Indentation Rules

```csharp
private int MeasureIndentation()
```

**Rules enforced:**
1. **No tabs allowed** - Only spaces for indentation
2. **Must be multiple of 4** - Enforces consistent style
3. **No mixed tabs and spaces**
4. **Must match a previous level when dedenting** - Prevents "orphan" indentations

**Example:**
```python
def foo():      # Indent 0
    x = 1       # Indent 4 → INDENT token
    if True:    # Indent 4 (same)
        y = 2   # Indent 8 → INDENT token
    z = 3       # Indent 4 → DEDENT token
```

**Returns:** The indentation level (number of spaces)

**Throws:** `LexerError` for any indentation violations

### Generating INDENT/DEDENT Tokens

In `NextToken()` after measuring indentation:

```csharp
if (indentLevel > currentIndent)
{
    // Push new level and return INDENT
    _indentStack.Push(indentLevel);
    return new Token(TokenType.Indent, "", _line, 1);
}
else if (indentLevel < currentIndent)
{
    // Pop levels and generate DEDENTs
    var dedents = new List<Token>();
    while (_indentStack.Peek() > indentLevel)
    {
        _indentStack.Pop();
        dedents.Add(new Token(TokenType.Dedent, "", _line, 1));
    }
    // Queue extra DEDENTs, return first
    for (int i = 1; i < dedents.Count; i++)
        _pendingTokens.Enqueue(dedents[i]);
    return dedents[0];
}
```

**Why queue tokens?** When dedenting multiple levels, we need to return multiple DEDENT tokens, but `NextToken()` can only return one at a time.

### Blank Lines and Comments

Blank lines and comment-only lines are **ignored** for indentation:
```python
def foo():
    x = 1
              # This comment's indentation doesn't matter
                  
    y = 2     # Still at same level
```

The lexer skips these lines and recursively calls `NextToken()` to get the next real token.

---

## String Literal Handling

### Regular Strings: `ReadString()`

Handles:
- Single and double quotes: `"hello"`, `'world'`
- Triple-quoted strings: `"""multi\nline"""`
- Escape sequences: `\n`, `\t`, `\x41`, `\u0041`, `\U00000041`

**Triple-quoted strings:**
```csharp
private Token ReadTripleQuotedString(char quote, int startLine, int startColumn)
```
Can span multiple lines and preserve formatting. Ends when three matching quotes are found.

### Raw Strings: `ReadRawString()`

Prefixed with `r`, raw strings don't process escape sequences:
```python
r"C:\Users\name"  # Backslashes are literal
```

### F-Strings (Format Strings)

The most complex feature. F-strings allow embedding expressions:
```python
name = "Alice"
greeting = f"Hello {name}!"  # → "Hello Alice!"
```

#### Three-Phase F-String Tokenization

**Phase 1: Start** - `ReadFStringStart()`
- Recognizes `f"` or `f'` (or triple-quoted versions)
- Pushes `FStringContext` onto stack
- Returns `FStringStart` token

**Phase 2: Content** - `NextFStringToken()`
The lexer alternates between two modes:

1. **Literal Text Mode** (BraceDepth = 0)
   - Reads characters as literal text
   - Watches for:
     - `{` → Start of expression (unless `{{` = escaped brace)
     - `}` → Error (unmatched, unless `}}` = escaped)
     - Quote character → End of f-string

2. **Expression Mode** (BraceDepth > 0)
   - Tokenizes normally (like regular code)
   - Tracks brace nesting for dict literals: `f"{{'a': 1}}"`
   - Watches for:
     - `:` at depth 1 → Format specification
     - `}` → End of expression (or nested closing brace)

**Example token sequence:**
```python
f"Value: {x:>10}"
```
Produces:
```
FStringStart("f\"")
FStringText("Value: ")
FStringExprStart("{")
Identifier("x")
FStringFormatSpec(">10")
FStringExprEnd("}")
FStringEnd("\"")
```

**Phase 3: End** - In `NextFStringToken()`
- Recognizes closing quote (or triple quotes)
- Pops `FStringContext` from stack
- Returns `FStringEnd` token

#### Format Specifications

After `:` in an f-string expression, the format spec is read as a whole token:
```python
f"{value:.2f}"  # Format spec: ".2f"
```

This is handled specially to avoid tokenizing the format spec (which has its own mini-language).

---

## Numeric Literal Handling

### `ReadNumber()` - Main Entry Point

Detects number type from prefix:
- `0x...` → Hexadecimal
- `0b...` → Binary  
- `0o...` → Octal
- Otherwise → Decimal (integer or float)

### Decimal Numbers

**Features:**
- Digit separators: `1_000_000`
- Floats with decimal: `3.14159`
- Scientific notation: `6.022e23`, `1.23e-4`
- Type suffixes: `3.14f` (float), `100L` (long), `5u` (unsigned)

**Validation:**
- No consecutive underscores: `1__000` ❌
- No trailing underscores: `1000_` ❌
- No leading underscores: `_1000` ❌

### Special Base Numbers

**Hexadecimal** (`ReadHexNumber`): `0x1A2F`, `0xFF_00_00`
**Binary** (`ReadBinaryNumber`): `0b1010`, `0b1111_0000`
**Octal** (`ReadOctalNumber`): `0o755`, `0o644`

Each enforces:
- At least one digit after prefix
- Only valid digits for the base
- Proper underscore usage

---

## Identifier and Keyword Handling

### `ReadIdentifierOrKeyword()`

```csharp
private Token ReadIdentifierOrKeyword()
{
    // Read letters, digits, underscores
    var value = ReadLettersDigitsUnderscores();
    
    // Check if it's a keyword
    if (Keywords.TryGetValue(value, out var tokenType))
        return new Token(tokenType, value, startLine, startColumn);
    
    // Otherwise it's an identifier
    return new Token(TokenType.Identifier, value, startLine, startColumn);
}
```

**Rules:**
- Must start with letter or underscore
- Can contain letters, digits, underscores
- Case-sensitive: `True` (keyword) vs `true` (identifier)

### Literal Names: Backtick-Delimited Identifiers

```csharp
private Token ReadLiteralName()
```

Allows using reserved words as identifiers:
```python
`class` = 42  # 'class' is normally a keyword
```

Useful for interop with .NET where C# keywords might be member names.

---

## Operator and Delimiter Handling

### `ReadOperatorOrDelimiter()` - The Big Switch

Handles:
- **Three-character operators**: `...`, `<<=`, `>>=`, `**=`, `//=`
- **Two-character operators**: `==`, `!=`, `<=`, `>=`, `<<`, `>>`, `**`, `//`, `->`, `?.`, `??`, and all compound assignments
- **Single-character tokens**: `+`, `-`, `*`, `/`, `(`, `)`, `[`, `]`, `{`, `}`, etc.

**Bracket depth tracking:** 
```csharp
if (c == '(' || c == '[' || c == '{')
    _bracketDepth++;
else if (c == ')' || c == ']' || c == '}')
    _bracketDepth--;
```

This enables implicit line continuation inside brackets.

---

## Line Continuation

### Backslash Continuation

```python
result = some_long_function_name() \
         + another_function()
```

Handled in `NextToken()`:
```csharp
if (current == '\\')
{
    // Check for newline after backslash
    // Skip both and continue tokenizing
    // Error if trailing whitespace
}
```

### Implicit Continuation (Inside Brackets)

When `_bracketDepth > 0`, newlines are silently skipped:
```python
my_dict = {
    'key1': 'value1',  # Newline ignored
    'key2': 'value2'
}
```

---

## Comment Handling

### `ReadComment()`

```csharp
private Token ReadComment()
{
    // Skip the '#'
    // Read until end of line
    // Return NextToken() (comments are skipped)
}
```

Comments are **not returned as tokens** by default (they're skipped). If you need them for documentation tools, the token type exists but isn't currently used.

---

## Escape Sequence Processing

### `ProcessEscapeSequence()` - Universal Escape Handler

Called from string/f-string readers. Handles:

**Simple escapes:**
- `\n` → newline
- `\t` → tab
- `\\` → backslash
- `\"` → double quote
- `\'` → single quote

**Numeric escapes:**
- `\xHH` → Hex (2 digits): `\x41` = 'A'
- `\uHHHH` → Unicode (4 digits): `\u0041` = 'A'
- `\UHHHHHHHH` → Unicode (8 digits): `\U00000041` = 'A'
- `\ooo` → Octal (1-3 digits): `\101` = 'A'

**Error handling:**
```csharp
default:
    throw new LexerError($"Invalid escape sequence: \\{escaped}", _line, _column);
```

---

## Helper Methods

### `SkipWhitespace()`

Skips spaces and tabs (but **not** newlines):
```csharp
private void SkipWhitespace()
{
    while (_position < _source.Length)
    {
        var c = _source[_position];
        if (c == ' ' || c == '\t')
            Advance();
        else
            break;
    }
}
```

### `Peek(int offset = 1)`

Looks ahead without advancing position:
```csharp
private char Peek(int offset = 1)
{
    var pos = _position + offset;
    return pos < _source.Length ? _source[pos] : '\0';
}
```

Useful for checking multi-character operators: `=` vs `==`.

### `LogAndReturn(Token token)`

Wrapper for logging before returning tokens:
```csharp
private Token LogAndReturn(Token token)
{
    _logger.LogTokenRead(token.Type.ToString(), token.Line, token.Column, token.Value);
    return token;
}
```

---

## Dependencies

### Internal Dependencies

```csharp
using Sharpy.Compiler.Logging;
```

- **`Token`** (`Token.cs`): The data structure representing tokens
- **`TokenType`** (`Token.cs`): Enum of all possible token types  
- **`LexerError`** (`LexerError.cs`): Exception thrown on lexical errors
- **`ICompilerLogger`**: Interface for diagnostic logging

### External Dependencies

- `System.Text.StringBuilder`: Efficient string building during token assembly
- `System.Collections.Generic`: Stack, Queue, Dictionary for state management

---

## Patterns and Design Decisions

### 1. **Recursive Descent Reading**

Each token type has a dedicated `ReadXxx()` method that knows how to parse that specific construct. This makes the lexer easy to extend and debug.

### 2. **Lookahead for Multi-Character Operators**

Instead of a complex DFA, the lexer checks prefixes explicitly:
```csharp
if (_position + 2 < _source.Length)
{
    var threeChar = _source.Substring(_position, 3);
    if (threeChar == "...") return TokenType.Ellipsis;
}
```

**Trade-off:** Simple but not the fastest approach. For Sharpy's scale, readability wins.

### 3. **State Stack for F-Strings**

F-strings can nest (f-string → expression → nested f-string). Using a stack makes this natural:
```csharp
_fstringStack.Push(new FStringContext { ... });
// ... process ...
_fstringStack.Pop();
```

### 4. **Token Queue for Multi-Token Scenarios**

When dedenting multiple levels, we need to emit multiple DEDENT tokens. The queue pattern:
```csharp
_pendingTokens.Enqueue(token);
// Later...
if (_pendingTokens.Count > 0)
    return _pendingTokens.Dequeue();
```

This keeps `NextToken()` simple (always returns exactly one token).

### 5. **Position Tracking**

Every token remembers its source location (`Line`, `Column`). This is crucial for:
- Error reporting
- IDE features (go-to-definition, hover)
- Source maps for debugging

### 6. **Error Recovery**

The lexer uses **fail-fast** error handling:
```csharp
throw new LexerError("descriptive message", _line, _column);
```

It doesn't try to recover from errors—the compiler will stop and report the problem. This keeps the lexer simple and error messages clear.

---

## Debugging Tips

### 1. **Visualize Token Stream**

Add logging to see what tokens are produced:
```csharp
var lexer = new Lexer(source, new ConsoleLogger());
var tokens = lexer.TokenizeAll();
```

Or in the CLI:
```bash
dotnet run --project src/Sharpy.Cli -- --emit-tokens file.spy
```

### 2. **Check Indentation Stack**

If INDENT/DEDENT tokens seem wrong, inspect `_indentStack`:
```csharp
// Add breakpoint here
if (_atLineStart && _bracketDepth == 0)
{
    var indentLevel = MeasureIndentation();
    // Stack should contain [0, 4, 8, ...] etc.
}
```

### 3. **F-String Debugging**

F-string issues? Track the context stack:
```csharp
// In NextFStringToken()
var context = _fstringStack.Peek();
// Check: BraceDepth, InFormatSpec
```

### 4. **Position Verification**

If tokens have wrong locations, verify `_line` and `_column` are updated correctly after:
- Newlines: `_line++; _column = 1;`
- Characters: `_column++`

### 5. **Escape Sequence Issues**

Test escape processing in isolation:
```python
# Test file
s = "\x41\n\t\u0041"
```

Check that `ProcessEscapeSequence()` is called from both regular strings and f-strings.

---

## Contribution Guidelines

### Adding a New Keyword

1. **Add to `TokenType` enum** (`Token.cs`):
   ```csharp
   public enum TokenType
   {
       // ...
       YourNewKeyword,
   }
   ```

2. **Add to Keywords dictionary**:
   ```csharp
   { "yournewkeyword", TokenType.YourNewKeyword }
   ```

3. **Add tests** in `Lexer/LexerTests.cs`:
   ```csharp
   [Fact]
   public void TestYourNewKeyword()
   {
       var lexer = new Lexer("yournewkeyword");
       var token = lexer.NextToken();
       Assert.Equal(TokenType.YourNewKeyword, token.Type);
   }
   ```

### Adding a New Operator

1. **Add to `TokenType` enum**

2. **Add to `ReadOperatorOrDelimiter()`**:
   ```csharp
   case "@@":  // New operator
       _position += 2;
       _column += 2;
       return new Token(TokenType.YourOperator, "@@", startLine, startColumn);
   ```

3. **Consider operator precedence** (handled by parser, not lexer)

### Adding a New String Prefix

Example: `b"bytes"` for byte strings:

1. **Add token type**: `TokenType.ByteString`

2. **Add check in `NextToken()`**:
   ```csharp
   if (current == 'b' && Peek() is '"' or '\'')
       return ReadByteString();
   ```

3. **Implement reader method**:
   ```csharp
   private Token ReadByteString()
   {
       // Similar to ReadRawString()
   }
   ```

### Improving Error Messages

Look for generic errors and make them specific:
```csharp
// Before
throw new LexerError("Invalid token", _line, _column);

// After
throw new LexerError($"Invalid token '{c}' (did you mean '=='?)", _line, _column);
```

### Performance Optimization

**Current bottlenecks:**
1. String concatenation in numeric readers (use `StringBuilder`)
2. `Substring` calls in operator checking (could use `Span<char>`)
3. Repeated bounds checking (could batch)

**Before optimizing:**
- Profile with real-world Sharpy code
- Ensure tests pass
- Measure improvement

### Testing Philosophy

**For every new feature, add tests for:**
- ✅ Happy path (valid input)
- ✅ Edge cases (empty, single character)
- ✅ Error cases (malformed input)
- ✅ Boundary conditions (EOF, line boundaries)

**Example test structure:**
```csharp
[Fact]
public void TestFeatureName_ValidInput()
{
    var lexer = new Lexer("valid input");
    var token = lexer.NextToken();
    Assert.Equal(expectedType, token.Type);
    Assert.Equal(expectedValue, token.Value);
}

[Fact]
public void TestFeatureName_InvalidInput_ThrowsError()
{
    var lexer = new Lexer("invalid input");
    Assert.Throws<LexerError>(() => lexer.NextToken());
}
```

---

## Common Pitfalls for New Contributors

### 1. **Forgetting to Update Column Position**

Every character consumed must update `_column`:
```csharp
_position++;
_column++;  // Don't forget this!
```

### 2. **Not Handling EOF**

Always check before accessing `_source[_position]`:
```csharp
if (_position >= _source.Length)
    throw new LexerError("Unexpected end of file", _line, _column);
```

### 3. **Breaking Indentation Invariants**

The indent stack must always:
- Start with [0]
- Only contain values that are multiples of 4
- Be ordered (bottom to top: increasing)

### 4. **F-String Brace Counting**

When in f-string expression mode, remember:
- `{` inside the expression increments `BraceDepth`
- Only when `BraceDepth` returns to 0 do we exit expression mode
- Format specs (after `:`) need special handling

### 5. **Bracket Depth Going Negative**

Unmatched closing brackets shouldn't crash:
```csharp
if (_bracketDepth < 0)
    _bracketDepth = 0;  // Defensive: parser will catch the error
```

### 6. **Newline Normalization**

Handle both `\n` and `\r\n`:
```csharp
if (_source[_position] == '\r')
{
    _position++;
    if (_position < _source.Length && _source[_position] == '\n')
        _position++;  // Skip \r\n
}
```

---

## Architecture Context

### Where Lexer Fits in the Compiler

```
┌──────────────┐
│ Source Code  │
│   (.spy)     │
└──────┬───────┘
       │
       ▼
┌──────────────┐  ← YOU ARE HERE
│    Lexer     │
│ (Tokenizer)  │
└──────┬───────┘
       │ Token Stream
       ▼
┌──────────────┐
│    Parser    │
│  (AST Gen)   │
└──────┬───────┘
       │ AST
       ▼
┌──────────────┐
│   Semantic   │
│   Analysis   │
└──────┬───────┘
       │ Annotated AST
       ▼
┌──────────────┐
│   Code Gen   │
│  (Roslyn)    │
└──────┬───────┘
       │ C# Code
       ▼
┌──────────────┐
│.NET Compiler │
└──────────────┘
```

### Lexer's Responsibilities

**What the lexer DOES:**
- ✅ Break source into tokens
- ✅ Generate INDENT/DEDENT for blocks
- ✅ Handle all literal types (strings, numbers)
- ✅ Track source locations
- ✅ Detect lexical errors (invalid characters, malformed literals)

**What the lexer DOESN'T do:**
- ❌ Check syntax (parser's job)
- ❌ Resolve names (semantic analyzer's job)
- ❌ Type checking (semantic analyzer's job)
- ❌ Generate code (code generator's job)

**Example:** The lexer accepts `def 123 class`:
```python
def 123 class  # Lexer: "OK, three tokens!"
               # Parser: "Syntax error: expected identifier after 'def'"
```

---

## Related Files

- **`Token.cs`**: Token type enum and Token record class
- **`LexerError.cs`**: Exception type for lexical errors
- **`Parser/Parser.cs`**: Consumes tokens to build AST
- **`Lexer/LexerTests.cs`**: Test suite for this class

---

## Further Reading

### Python Lexical Analysis
- [Python Language Reference: Lexical Analysis](https://docs.python.org/3/reference/lexical_analysis.html)
- Understanding Python's indentation rules helps when debugging Sharpy's implementation

### Compiler Theory
- **"Crafting Interpreters" by Bob Nystrom** - Chapter on Scanning
- **"Modern Compiler Implementation" (Tiger Book)** - Lexical Analysis chapter

### F-String Specification
- [PEP 498 – Literal String Interpolation](https://www.python.org/dev/peps/pep-0498/)
- The reference for Python's f-string behavior that Sharpy emulates

---

## Summary

The Lexer is a sophisticated scanner that transforms Sharpy source code into a token stream while:
- Enforcing Python-style indentation rules
- Handling complex f-string interpolation
- Supporting rich numeric and string literal formats
- Maintaining precise source location tracking for great error messages

When working with the lexer, remember: **it's all about character-by-character state management**. Each method is responsible for consuming a specific pattern and advancing the position correctly. The test suite is your friend—run it often!

**Next steps after understanding the lexer:**
1. Study `Parser.cs` to see how tokens become an AST
2. Look at test files to see example token sequences
3. Try adding a simple feature (like a new keyword) end-to-end

Welcome to the Sharpy compiler! 🎉
