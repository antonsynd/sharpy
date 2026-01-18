# Walkthrough: Lexer.cs

**Source File**: `src/Sharpy.Compiler/Lexer/Lexer.cs`

---

## Overview

The Lexer is the **first phase** of the Sharpy compiler pipeline. It transforms raw source code text (`.spy` files) into a stream of tokens that can be consumed by the Parser. Think of it as reading a sentence and breaking it into individual words, punctuation marks, and whitespace—except for code.

**Key Responsibilities:**
- Convert character stream into structured tokens (identifiers, keywords, numbers, strings, operators)
- Track source location (line and column) for error reporting
- Handle Python-style **indentation-based syntax** (INDENT/DEDENT tokens)
- Support advanced string literals (f-strings, raw strings, triple-quoted strings)
- Manage implicit line continuation inside brackets `()`, `[]`, `{}`
- Validate syntax rules (proper indentation, valid escape sequences, number formats)

**Pipeline Position:**
```
Source (.spy) → [LEXER] → Token Stream → Parser → AST → Semantic Analysis → RoslynEmitter → C#
```

**Upstream Component:** Raw .spy source files
**Downstream Component:** Parser (consumes token stream to build AST)

---

## Class/Type Structure

### Main Class: `Lexer`

The `Lexer` class is a **stateful scanner** that maintains position and context as it processes source code character by character.

#### Core State Fields

```csharp
private readonly string _source;          // The complete source code
private int _position;                     // Current character position (index into _source)
private int _line = 1;                     // Current line number (1-based)
private int _column = 1;                   // Current column number (1-based)
private readonly Stack<int> _indentStack;  // Stack of indentation levels
private readonly Queue<Token> _pendingTokens; // Buffered tokens (for DEDENT cascades)
private bool _atLineStart = true;          // Are we at the beginning of a line?
private int _bracketDepth = 0;             // Depth inside (), [], or {}
private readonly ICompilerLogger _logger;  // Optional diagnostic logger
```

**Why these matter:**
- `_position`, `_line`, `_column`: Track exact source location for every token (critical for error messages)
- `_indentStack`: Implements Python-style indentation tracking (base is 0, pushed/popped as indentation changes)
- `_pendingTokens`: When dedenting multiple levels at once, we queue extra DEDENT tokens
- `_atLineStart`: Tells us when to measure indentation (only at line start, not inside brackets)
- `_bracketDepth`: Enables implicit line continuation—newlines inside `()`, `[]`, `{}` are ignored

#### F-String State

F-strings (interpolated strings like `f"Hello {name}"`) require complex state management:

```csharp
private readonly Stack<FStringContext> _fstringStack;

private class FStringContext
{
    public char QuoteChar { get; set; }      // ' or "
    public bool IsTriple { get; set; }        // """ or '''
    public int BraceDepth { get; set; }       // Track nested { } in expressions
    public bool InFormatSpec { get; set; }    // Inside format spec after ':'
}
```

**Why a stack?** Nested f-strings are possible: `f"outer {f'inner {x}'}"`. Each level needs its own context.

#### Keyword Mapping

```csharp
private static readonly Dictionary<string, TokenType> Keywords = new()
{
    { "def", TokenType.Def },
    { "class", TokenType.Class },
    { "if", TokenType.If },
    { "True", TokenType.True },
    { "False", TokenType.False },
    { "None", TokenType.None },
    { "and", TokenType.And },
    { "or", TokenType.Or },
    { "not", TokenType.Not },
    // ... ~50 total keywords
};
```

This static dictionary enables O(1) keyword lookup after reading an identifier. See `docs/language_specification/keywords.md` for the complete keyword list.

---

## Key Functions/Methods

### 1. `NextToken()` - The Heart of the Lexer

**Location:** `src/Sharpy.Compiler/Lexer/Lexer.cs:142`

**What it does:** Returns the next token from the source stream. This is the core method that orchestrates all tokenization.

**Key parameters:** None (uses internal state)
**Returns:** `Token` - The next token in the stream

**Flow:**

```csharp
public Token NextToken()
{
    // 1. Return pending tokens first (DEDENT cascade)
    if (_pendingTokens.Count > 0)
        return _pendingTokens.Dequeue();

    // 2. F-string mode takes priority
    if (_fstringStack.Count > 0)
        return NextFStringToken();

    // 3. Handle EOF (and generate remaining DEDENTs)
    if (_position >= _source.Length)
    {
        if (_indentStack.Count > 1)
        {
            _indentStack.Pop();
            return new Token(TokenType.Dedent, "", _line, _column);
        }
        return new Token(TokenType.Eof, "", _line, _column);
    }

    // 4. Handle indentation at line start
    if (_atLineStart && _bracketDepth == 0)
    {
        // [Indentation logic - see MeasureIndentation()]
    }

    // 5. Skip whitespace
    SkipWhitespace();

    // 6. Dispatch based on current character
    var current = _source[_position];

    if (current == '#') return ReadComment();
    if (current == '\\') /* Handle line continuation */;
    if (current == '\n' || current == '\r') /* Handle newline */;
    if (current == '"' || current == '\'') return ReadString();
    if (current == 'f' && /*...*/) return ReadFStringStart();
    if (char.IsDigit(current)) return ReadNumber();
    if (char.IsLetter(current) || current == '_') return ReadIdentifierOrKeyword();

    return ReadOperatorOrDelimiter();
}
```

**Key insight:** The method is a **dispatcher**—it peeks at the current character and routes to specialized readers.

**Connects to upstream:** Reads from `_source` (raw source text)
**Connects to downstream:** Returns tokens consumed by the Parser

---

### 2. Indentation Handling - The Python Difference

One of Sharpy's most distinctive features is Python-style indentation instead of braces. The lexer generates **synthetic tokens** (`INDENT` and `DEDENT`) that the parser uses to determine block structure. See `docs/language_specification/indentation.md` for the full specification.

#### `MeasureIndentation()`

**Location:** `src/Sharpy.Compiler/Lexer/Lexer.cs:402`

**What it does:** Measures leading whitespace at line start and validates indentation rules.

```csharp
private int MeasureIndentation()
{
    var indent = 0;
    var tempPos = _position;
    var hasSpaces = false;
    var hasTabs = false;

    // Count leading whitespace
    while (tempPos < _source.Length)
    {
        var c = _source[tempPos];
        if (c == ' ') { hasSpaces = true; indent++; tempPos++; }
        else if (c == '\t') { hasTabs = true; tempPos++; }
        else break;
    }

    // Strict validation
    if (hasSpaces && hasTabs)
        throw new LexerError("Mixed tabs and spaces in indentation", _line, 1);

    if (hasTabs)
        throw new LexerError("Tabs are not allowed for indentation. Use 4 spaces.", _line, 1);

    if (indent % 4 != 0)
        throw new LexerError($"Indentation must be multiple of 4 spaces (found {indent})", _line, 1);

    // Check dedent matches a previous level
    if (indent < _indentStack.Peek())
    {
        if (!_indentStack.Contains(indent))
            throw new LexerError("Indentation mismatch", _line, 1);
    }

    _position = tempPos;
    _column = indent + 1;
    return indent;
}
```

**Why so strict?** Mixing tabs/spaces or inconsistent indentation leads to hard-to-debug errors. Sharpy enforces 4-space indentation by design.

#### Generating INDENT/DEDENT Tokens

In `NextToken()`, after measuring indentation (lines 214-244):

```csharp
var indentLevel = MeasureIndentation();
var currentIndent = _indentStack.Peek();

if (indentLevel > currentIndent)
{
    // Indenting deeper - push new level
    _indentStack.Push(indentLevel);
    _atLineStart = false;
    return new Token(TokenType.Indent, "", _line, 1);
}
else if (indentLevel < currentIndent)
{
    // Dedenting - may need multiple DEDENT tokens
    var dedents = new List<Token>();
    while (_indentStack.Count > 1 && _indentStack.Peek() > indentLevel)
    {
        _indentStack.Pop();
        dedents.Add(new Token(TokenType.Dedent, "", _line, 1));
    }

    // Return first DEDENT, queue the rest
    for (int i = 1; i < dedents.Count; i++)
        _pendingTokens.Enqueue(dedents[i]);

    return dedents[0];
}
```

**Example:**
```python
if x:
    if y:
        print("deep")
print("back to top")  # Generates 2 DEDENT tokens
```

**Important implementation detail:** When dedenting multiple levels, we generate multiple DEDENT tokens but can only return one per call. The extras are queued in `_pendingTokens` and returned on subsequent calls.

---

### 3. String Literal Handling

Sharpy supports multiple string literal types, following Python conventions.

#### Regular Strings (`ReadString()`)

**Location:** `src/Sharpy.Compiler/Lexer/Lexer.cs:510`

```csharp
private Token ReadString()
{
    var quote = _source[_position];  // ' or "
    _position++; _column++;

    // Check for triple-quoted strings
    var isTriple = _position + 1 < _source.Length &&
                   _source[_position] == quote &&
                   _source[_position + 1] == quote;

    if (isTriple) return ReadTripleQuotedString(quote, startLine, startColumn);

    // Single-line string
    var sb = new StringBuilder();
    while (_position < _source.Length)
    {
        var c = _source[_position];

        if (c == quote) { /*...*/ return new Token(TokenType.String, sb.ToString(), ...); }
        if (c == '\\') { sb.Append(ProcessEscapeSequence()); }
        else if (c == '\n' || c == '\r') throw new LexerError("Unterminated string literal", ...);
        else { sb.Append(c); _position++; _column++; }
    }

    throw new LexerError("Unterminated string literal", ...);
}
```

**Key features:**
- Single vs. triple-quoted detection
- Escape sequence processing (`\n`, `\t`, `\xHH`, `\uHHHH`, etc.)
- No newlines in single-quoted strings (throws error)

#### F-Strings (`ReadFStringStart()` and `NextFStringToken()`)

**Location:** `src/Sharpy.Compiler/Lexer/Lexer.cs:624` (start), `src/Sharpy.Compiler/Lexer/Lexer.cs:662` (content)

F-strings are **the most complex feature** in the lexer. They require:
1. Switching to a special "f-string mode"
2. Alternating between literal text and embedded expressions
3. Handling nested braces and format specifications

**Entry point:**
```csharp
private Token ReadFStringStart()
{
    _position++;  // Skip 'f'
    var quote = _source[_position];
    _position++;  // Skip quote

    var isTriple = /* check for triple quote */;
    if (isTriple) { _position += 2; }

    _fstringStack.Push(new FStringContext { QuoteChar = quote, IsTriple = isTriple });
    return new Token(TokenType.FStringStart, ..., ...);
}
```

**Token stream example:**
```python
f"Hello {name:>10}"
```
Produces:
```
FStringStart("f\"")
FStringText("Hello ")
FStringExprStart("{")
Identifier("name")
FStringFormatSpec(">10")
FStringExprEnd("}")
FStringEnd("\"")
```

**The `NextFStringToken()` method** (~300 lines) handles:
- Reading literal text until `{` or closing quote
- Switching to expression mode (tokenize normally)
- Detecting format specs (`:` at brace depth 1)
- Escaped braces (`{{` → `{`, `}}` → `}`)
- Nested braces in expressions

**Algorithm for expression mode:** When `BraceDepth > 0`, the lexer tokenizes normally but tracks brace nesting. When a `:` is encountered at depth 1, it switches to format spec mode and reads the format specification as a single token until the closing `}`.

---

### 4. Number Parsing (`ReadNumber()`)

**Location:** `src/Sharpy.Compiler/Lexer/Lexer.cs:1146`

Supports multiple number formats:

```csharp
private Token ReadNumber()
{
    // Check for special prefixes
    if (_source[_position] == '0' && _position + 1 < _source.Length)
    {
        var next = _source[_position + 1];
        if (next == 'x' || next == 'X') return ReadHexNumber(...);      // 0x1A2F
        if (next == 'b' || next == 'B') return ReadBinaryNumber(...);   // 0b1010
        if (next == 'o' || next == 'O') return ReadOctalNumber(...);    // 0o755
    }

    // Regular decimal number
    // Read integer part (allowing underscores: 1_000_000)
    // Check for decimal point
    // Check for exponent (e or E)
    // Check for suffix (f, d, m, l, ul, etc.)

    return new Token(isFloat ? TokenType.Float : TokenType.Integer, sb.ToString(), ...);
}
```

**Features:**
- Underscore separators for readability: `1_000_000`
- Hex (`0x`), binary (`0b`), octal (`0o`) literals
- Scientific notation: `1.23e-4`
- Type suffixes: `3.14f`, `42L`, `100ul`

**Validation:**
- No consecutive underscores (lines 1181-1182, 1211-1213, 1253-1255)
- No trailing underscores (lines 1193-1194, 1224-1225, 1266-1267)
- Proper hex/binary/octal digits (enforced in `ReadHexNumber`, `ReadBinaryNumber`, `ReadOctalNumber`)

**Important implementation detail:** The underscore validation is spread across integer part, fractional part, and exponent. Each section independently validates underscore usage.

---

### 5. Operator and Delimiter Handling (`ReadOperatorOrDelimiter()`)

**Location:** `src/Sharpy.Compiler/Lexer/Lexer.cs:1510`

Handles all operators and punctuation. The tricky part: **multi-character operators**.

```csharp
private Token ReadOperatorOrDelimiter()
{
    // Try 3-character operators first
    if (_position + 2 < _source.Length)
    {
        var threeChar = _source.Substring(_position, 3);
        switch (threeChar)
        {
            case "...": return new Token(TokenType.Ellipsis, ..., ...);
            case "<<=": return new Token(TokenType.LeftShiftAssign, ..., ...);
            case ">>=": return new Token(TokenType.RightShiftAssign, ..., ...);
            // ...
        }
    }

    // Then 2-character operators
    if (_position + 1 < _source.Length)
    {
        var twoChar = _source.Substring(_position, 2);
        switch (twoChar)
        {
            case "==": return new Token(TokenType.Equal, ..., ...);
            case "!=": return new Token(TokenType.NotEqual, ..., ...);
            case "->": return new Token(TokenType.Arrow, ..., ...);
            // ... ~30 two-char operators
        }
    }

    // Finally, single-character
    var token = c switch
    {
        '+' => new Token(TokenType.Plus, "+", ...),
        '(' => new Token(TokenType.LeftParen, "(", ...),
        // ... all single-char operators
        _ => throw new LexerError($"Unexpected character: '{c}'", ...)
    };

    // Track bracket depth for implicit line continuation
    if (c == '(' || c == '[' || c == '{') _bracketDepth++;
    else if (c == ')' || c == ']' || c == '}') _bracketDepth--;

    return token;
}
```

**Key insight:** Longest match wins. Always check 3-char, then 2-char, then 1-char operators.

**Important for bracket tracking:** Lines 1682-1692 track `_bracketDepth`, which is crucial for implicit line continuation. When `_bracketDepth > 0`, newlines are ignored.

---

### 6. Helper Methods

#### `SkipWhitespace()`

**Location:** `src/Sharpy.Compiler/Lexer/Lexer.cs:466`

Skips spaces and tabs (but **not** newlines):
```csharp
private void SkipWhitespace()
{
    while (_position < _source.Length)
    {
        var c = _source[_position];
        if (c == ' ' || c == '\t')
        {
            _position++;
            _column++;
        }
        else break;
    }
}
```

#### `Peek(int offset = 1)`

**Location:** `src/Sharpy.Compiler/Lexer/Lexer.cs:483`

Looks ahead without advancing position:
```csharp
private char Peek(int offset = 1)
{
    var pos = _position + offset;
    return pos < _source.Length ? _source[pos] : '\0';
}
```

Useful for checking multi-character operators: `=` vs `==`.

#### `ProcessEscapeSequence()`

**Location:** `src/Sharpy.Compiler/Lexer/Lexer.cs:1053`

Handles all escape sequences in strings:
- Simple escapes: `\n`, `\t`, `\\`, `\"`, `\'`
- Hex escapes: `\xHH` (2 digits)
- Unicode escapes: `\uHHHH` (4 digits), `\UHHHHHHHH` (8 digits)
- Octal escapes: `\ooo` (1-3 digits)

---

## Special Features

### Implicit Line Continuation

Python allows multi-line expressions inside brackets without backslashes:

```python
result = (
    very_long_function_name(
        argument1,
        argument2
    )
)
```

The lexer tracks `_bracketDepth` and **skips newlines** when `_bracketDepth > 0` (lines 322-343):

```csharp
// In NextToken(), handling newlines:
if (current == '\n' || current == '\r')
{
    if (_bracketDepth > 0)
    {
        // Inside brackets - skip newline, don't produce token
        _position++;
        _line++;
        _column = 1;
        _atLineStart = true;
        return NextToken();  // Recurse to get next real token
    }

    // Normal newline token
    return new Token(TokenType.Newline, "\n", ...);
}
```

### Explicit Line Continuation (Backslash)

**Location:** `src/Sharpy.Compiler/Lexer/Lexer.cs:263-317`

```python
x = very_long_expression + \
    continuation_on_next_line
```

The lexer detects `\` followed immediately by newline (no trailing spaces allowed):

```csharp
if (current == '\\')
{
    // Skip whitespace after backslash
    var tempPos = nextPos;
    while (tempPos < _source.Length && (_source[tempPos] == ' ' || _source[tempPos] == '\t'))
        tempPos++;

    // Must be immediately followed by newline
    if (tempPos != nextPos && /* has newline */)
        throw new LexerError("Backslash line continuation cannot have trailing whitespace", ...);

    // Valid continuation - skip backslash and newline
    _position = tempPos;
    if (_source[_position] == '\r') _position++;
    if (_source[_position] == '\n') _position++;
    _line++;
    _column = 1;
    return NextToken();
}
```

### Blank Line and Comment Handling

**Location:** `src/Sharpy.Compiler/Lexer/Lexer.cs:167-208`

Blank lines and comment-only lines **do not** produce NEWLINE tokens or affect indentation:

```csharp
if (_atLineStart && _bracketDepth == 0)
{
    // Peek ahead to check if this is a blank or comment line
    var savedPos = _position;
    SkipWhitespace();

    if (_position >= _source.Length || _source[_position] == '\n' || _source[_position] == '#')
    {
        // Skip comment if present
        if (_source[_position] == '#') { /* skip to newline */ }

        // Skip newline
        // ... advance line counter

        // Recursively get next token (don't produce NEWLINE)
        return NextToken();
    }

    // Restore position to measure indentation properly
    _position = savedPos;
    var indentLevel = MeasureIndentation();
    // ...
}
```

**Why this matters:** This ensures that comments and blank lines don't affect the indentation logic, making the language more flexible for documentation.

---

## Dependencies

### Internal Dependencies

- **`Sharpy.Compiler.Logging.ICompilerLogger`**: Used for diagnostic logging (token reads, indentation changes)
- **`Token` struct** (defined in `Token.cs`): The output type containing `TokenType`, value, and position
- **`TokenType` enum** (defined in `TokenType.cs`): All possible token types (~100+ types)
- **`LexerError` class** (defined in `LexerError.cs`): Custom exception for lexical errors

See the [Token documentation](Token.md) for details on the Token structure.

### External Dependencies

- **`System.Text.StringBuilder`**: Efficient string building for tokens
- **`System.Collections.Generic`**: Stack, Queue, Dictionary

---

## Patterns and Design Decisions

### 1. **State Machine Pattern**

The lexer is a finite state machine with multiple modes:
- **Normal mode**: Reading top-level code
- **Indentation mode**: At line start, measuring indents
- **F-string mode**: Inside f-string (text vs. expression)
- **Expression mode** (in f-string): Tokenizing normally

State transitions happen via flags (`_atLineStart`, `_fstringStack.Count > 0`, `context.BraceDepth`).

### 2. **Lookahead Pattern**

Many decisions require peeking ahead:
- Triple-quoted strings: Check next 2 chars
- F-strings: Check for `f` followed by quote
- Multi-char operators: Try 3-char, 2-char, 1-char

The `Peek(int offset)` helper method simplifies lookahead.

### 3. **Queue for Token Buffering**

When dedenting multiple levels, we need to emit multiple DEDENT tokens but can only return one per call. Solution: queue the extras in `_pendingTokens` and return them on subsequent calls.

**Design rationale:** This keeps the `NextToken()` interface simple—it always returns exactly one token per call.

### 4. **Strict Error Reporting**

Every error includes precise location (`_line`, `_column`). This makes debugging much easier for users.

### 5. **Logging for Observability**

Optional logging via `ICompilerLogger` allows tracing:
- Every token read (`LogTokenRead`)
- Indentation changes (`LogIndentChange`)
- Lexer initialization

This is invaluable for debugging the compiler itself.

### 6. **Recursive Descent Reading**

Each token type has a dedicated `ReadXxx()` method that knows how to parse that specific construct. This makes the lexer easy to extend and debug.

---

## Debugging Tips

### 1. **Understanding Token Streams**

To debug parsing issues, first verify the token stream is correct. Use `TokenizeAll()`:

```csharp
var lexer = new Lexer(source, logger);
var tokens = lexer.TokenizeAll();
foreach (var token in tokens)
{
    Console.WriteLine($"{token.Type,-20} '{token.Value}' at {token.Line}:{token.Column}");
}
```

### 2. **Indentation Issues**

Common problems:
- **Mixed tabs/spaces**: The lexer throws an error, but check your editor settings
- **Wrong multiple**: Sharpy requires 4-space indents (not 2 or 3)
- **Dedent mismatch**: Dedenting to a level that was never indented to

Enable logging to see indentation changes:

```csharp
_logger.LogIndentChange(currentIndent, indentLevel);
```

### 3. **F-String Debugging**

F-strings have the most complex logic. To debug:
1. Check `_fstringStack` depth (should match nesting level)
2. Verify `BraceDepth` tracking (should be 0 outside expressions)
3. Look for unmatched `{` or `}` (common source of errors)

Set breakpoints at:
- Line 674: Expression mode detection
- Line 745: Format spec handling
- Line 834: Quote character detection

### 4. **Bracket Depth Tracking**

If newlines are unexpectedly ignored or not ignored, check `_bracketDepth`. Add assertions:

```csharp
if (_bracketDepth < 0)
    throw new InvalidOperationException("Bracket depth went negative!");
```

The lexer already has defensive code for this at line 1690-1691.

### 5. **Position Tracking**

If error locations seem wrong, verify `_position`, `_line`, `_column` are incremented correctly after every character read. Common mistake: forgetting to increment after multi-char operators.

**Tip:** The `LogAndReturn` method (line 396) logs every token with its position, which helps verify tracking accuracy.

---

## Contribution Guidelines

### When to Modify This File

1. **Adding new keywords**: Update the `Keywords` dictionary (lines 34-101) and add corresponding `TokenType` enum values
2. **New operators**: Add cases to `ReadOperatorOrDelimiter()` (longest match first!)
3. **New string literal types**: Add a new `Read*String()` method and dispatcher case
4. **Bug fixes**: Especially around edge cases (empty files, lone characters, unusual indentation)

### What NOT to Change

1. **Indentation rules**: The 4-space requirement is by design (don't make it configurable)
2. **Token output format**: Parsers depend on the exact token stream structure
3. **Position tracking logic**: Extremely delicate—any change can break error reporting

### Testing Your Changes

After modifying the lexer:

1. **Run lexer unit tests**: `dotnet test --filter "FullyQualifiedName~Lexer"`
2. **Test edge cases**: Empty files, single characters, deeply nested structures
3. **Check error messages**: Ensure they're still helpful and accurate
4. **Run integration tests**: Verify parser and downstream components still work

### Common Pitfalls

- **Off-by-one errors**: Position tracking is 0-based, but line/column are 1-based
- **Forgetting to update column**: Every `_position++` usually needs `_column++`
- **Not handling `\r\n`**: Always check for both `\r` and `\n` (Windows vs. Unix)
- **Breaking lookahead**: When adding new token types, ensure longest match still wins

### Adding New Features

**Example: Adding a new keyword**

1. Add to `TokenType` enum in `Token.cs`:
   ```csharp
   NewKeyword,
   ```

2. Add to `Keywords` dictionary (line 34):
   ```csharp
   { "newkeyword", TokenType.NewKeyword },
   ```

3. Add tests in lexer test file:
   ```csharp
   [Fact]
   public void TestNewKeyword()
   {
       var lexer = new Lexer("newkeyword");
       var token = lexer.NextToken();
       Assert.Equal(TokenType.NewKeyword, token.Type);
   }
   ```

---

## Cross-References

### Related Files (Same Component)

- **`Token.cs`** ([Token.md](Token.md)): Token data structure and TokenType enum
- **`LexerError.cs`** ([LexerError.md](LexerError.md)): Lexical error exceptions

### Downstream Consumers

- **`Parser.cs`** ([../../Parser/Parser.md](../../Parser/Parser.md)): Consumes token stream to build AST
- **`Compiler.cs`** ([../../Compiler.md](../../Compiler.md)): Orchestrates lexer → parser → code gen pipeline

### Language Specifications

Reference these specifications for detailed behavior:
- **`docs/language_specification/lexer_implementation.md`**: Detailed lexer behavior specification
- **`docs/language_specification/indentation.md`**: Indentation rules and INDENT/DEDENT semantics
- **`docs/language_specification/keywords.md`**: Complete keyword list and reserved words
- **`docs/language_specification/identifiers.md`**: Identifier naming rules

---

## Summary

The Lexer is the **gateway** to the Sharpy compiler. It transforms raw text into structured tokens, handling:

- **Indentation-based syntax** (INDENT/DEDENT tokens)
- **Complex string literals** (f-strings, raw strings, triple-quoted)
- **Multiple number formats** (hex, binary, octal, floats with exponents)
- **Multi-character operators** (longest match)
- **Implicit line continuation** (inside brackets)

Understanding the lexer is crucial for:
- Debugging syntax errors
- Adding new language features
- Understanding how Sharpy code is parsed

**Key takeaways:**
1. The lexer is a **character-by-character state machine**
2. **Indentation handling** is the most Python-specific feature
3. **F-strings** are the most complex feature
4. **Position tracking** is critical for error reporting
5. **Token buffering** handles multi-token scenarios cleanly

The code is well-structured but dense—take time to trace through example inputs to build intuition for the state machine behavior. Start with simple cases (keywords, operators) before diving into complex features (f-strings, indentation).

**Next steps for newcomers:**
1. Read the [Token documentation](Token.md) to understand the output structure
2. Study test files to see example token sequences
3. Trace through `NextToken()` with a debugger on simple input
4. Try the Parser walkthrough to see how tokens are consumed
