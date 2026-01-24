# Walkthrough: Lexer.cs

**Source File**: `src/Sharpy.Compiler/Lexer/Lexer.cs`

---

## 1. Overview

The `Lexer` class is the first phase of the Sharpy compiler pipeline. It converts raw source text (`.spy` files) into a stream of tokens that the Parser can understand. This process is called **lexical analysis** or **tokenization**.

**Key Responsibilities:**
- Convert source characters into tokens (keywords, identifiers, literals, operators, etc.)
- Track Python-style indentation and emit INDENT/DEDENT tokens
- Handle f-string interpolation with nested expressions
- Manage bracket depth for implicit line continuation
- Track line/column positions for error reporting
- Skip comments and blank lines appropriately

**Pipeline Position:**
```
Source (.spy) → [LEXER] → Token Stream → Parser → AST → Semantic Analysis → RoslynEmitter → C#
```

## 2. Class Structure

### Core State Fields

```csharp
private readonly string _source;           // The complete source text being lexed
private int _position;                     // Current position in _source
private int _line = 1;                     // Current line number (1-indexed)
private int _column = 1;                   // Current column number (1-indexed)
private readonly Stack<int> _indentStack;  // Tracks indentation levels
private readonly Queue<Token> _pendingTokens; // Buffered tokens (for multiple DEDENTs)
private bool _atLineStart = true;          // Are we at the beginning of a line?
private int _bracketDepth = 0;             // Track nesting inside (), [], or {}
private readonly ICompilerLogger _logger;  // Optional logger for debugging
private readonly Stack<FStringContext> _fstringStack; // F-string state stack
```

**Important Design Decisions:**

1. **Indentation Stack**: Uses a stack to track nested indentation levels. Starts with `[0]` representing column 0. When indentation increases, push the new level and emit INDENT. When decreasing, pop levels and emit DEDENT for each.

2. **Pending Tokens Queue**: When dedenting multiple levels (e.g., from 8 spaces to 0), we need to emit multiple DEDENT tokens. Since `NextToken()` returns one token at a time, we queue the extras here.

3. **Bracket Depth**: Python allows implicit line continuation inside brackets. When `_bracketDepth > 0`, newlines are treated as whitespace, not NEWLINE tokens.

4. **F-String Stack**: F-strings can be nested (e.g., `f"outer {f'inner {x}'}"`) and contain complex expressions. The stack tracks each f-string context including its quote character, brace depth, and format spec state.

### F-String Context

```csharp
private class FStringContext
{
    public char QuoteChar { get; set; }      // ' or "
    public bool IsTriple { get; set; }       // Triple-quoted?
    public int BraceDepth { get; set; }      // Nesting level of { } inside expressions
    public bool InFormatSpec { get; set; }   // Are we processing :format_spec?
}
```

This nested class tracks the state of an f-string being lexed. The `BraceDepth` is crucial for handling nested dict literals inside interpolations: `f"{{'key': value}}"`.

### Keyword Dictionary

```csharp
private static readonly Dictionary<string, TokenType> Keywords = new()
{
    { "def", TokenType.Def },
    { "class", TokenType.Class },
    { "if", TokenType.If },
    // ... ~50 keywords total
};
```

Maps string keywords to their token types. This is consulted in `ReadIdentifierOrKeyword()` to distinguish keywords from identifiers.

## 3. Key Methods

### 3.1 Constructor

```csharp
public Lexer(string source, ICompilerLogger? logger = null, int startLine = 1, int startColumn = 1)
```

**Purpose**: Initialize the lexer with source text and optional logging.

**Key Details:**
- Starts with indentation stack containing `[0]` (base level)
- If `startLine != 1` or `startColumn != 1`, sets `_atLineStart = false`
  - This is used when lexing fragments (e.g., expressions inside f-strings)
  - Fragment lexing skips indentation measurement

**Why it matters**: The constructor sets up the initial state machine. The fragment detection is crucial for f-string expression lexing, where we need to tokenize Python expressions without treating their indentation as significant.

### 3.2 `NextToken()` - The Main State Machine

**Location**: `src/Sharpy.Compiler/Lexer/Lexer.cs:142`

This is the heart of the lexer. It's a state machine that returns one token each time it's called.

**Control Flow:**

```
1. Return pending tokens (DEDENT queue)
   ↓
2. If inside f-string, call NextFStringToken()
   ↓
3. If EOF, emit remaining DEDENTs, then EOF token
   ↓
4. If at line start (and not in brackets), handle indentation
   ↓
5. Skip whitespace
   ↓
6. Dispatch based on current character:
   - '#' → ReadComment()
   - '\' → Handle line continuation or treat as operator
   - '\n'/'\r' → Emit NEWLINE or skip if in brackets
   - '"'/"'" → ReadString()
   - 'f"'/'f\'' → ReadFStringStart()
   - 'r"'/'r\'' → ReadRawString()
   - '`' → ReadLiteralName()
   - digit → ReadNumber()
   - letter/'_' → ReadIdentifierOrKeyword()
   - operator/delimiter → ReadOperatorOrDelimiter()
```

**Indentation Handling** (lines 167-247):

This is the most complex part. When `_atLineStart` is true and we're not inside brackets:

1. **Skip blank/comment lines**: Peek ahead to see if the line is blank or contains only a comment. If so, skip the entire line recursively without emitting NEWLINE.

2. **Measure indentation**: Call `MeasureIndentation()` to count leading spaces.

3. **Compare with stack**:
   - **Indent**: `indentLevel > currentIndent` → Push new level, emit INDENT
   - **Dedent**: `indentLevel < currentIndent` → Pop levels until match, emit DEDENT(s)
   - **Same**: No token emitted

**Why blank lines are special**: Python (and Sharpy) ignores blank lines and comment-only lines for indentation purposes. This prevents spurious DEDENT tokens from empty lines inside functions.

**Connects to upstream**: Reads from `_source` (raw source text)
**Connects to downstream**: Returns tokens consumed by the Parser

### 3.3 `MeasureIndentation()`

**Location**: `src/Sharpy.Compiler/Lexer/Lexer.cs:411`

**Purpose**: Count leading spaces and validate indentation rules.

**Validation Rules:**
1. **No tabs**: Throws `LexerError` if any tabs found
2. **No mixed tabs/spaces**: Throws `LexerError` if both detected
3. **Multiple of 4**: Throws `LexerError` if indent count is not divisible by 4
4. **Must match previous level on dedent**: When dedenting, the new indent level must match some previously seen level in the stack

**Why these rules**: Sharpy enforces strict 4-space indentation (unlike Python which is more permissive). This prevents subtle bugs from inconsistent indentation. See `docs/language_specification/indentation.md` for details.

**Side Effects:**
- Advances `_position` past the whitespace
- Updates `_column` to reflect the new position

**Algorithm:**
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

### 3.4 `ReadString()`

**Location**: `src/Sharpy.Compiler/Lexer/Lexer.cs:519`

**Purpose**: Lex a regular string literal (`"hello"` or `'world'`).

**Algorithm:**
1. Remember the opening quote character
2. Check if it's a triple-quoted string (`"""` or `'''`)
   - If yes, delegate to `ReadTripleQuotedString()`
3. For single-line strings, read until:
   - Closing quote → success
   - Newline → error (unterminated string)
   - Backslash → call `ProcessEscapeSequence()`
   - EOF → error

**Escape Sequences**: Supports `\n`, `\t`, `\r`, `\\`, `\'`, `\"`, `\xhh`, `\uhhhh`, `\Uhhhhhhhh`, and octal escapes.

**Why split triple-quoted**: Triple-quoted strings can span multiple lines, requiring different handling for line tracking.

### 3.5 `ReadFStringStart()`

**Location**: `src/Sharpy.Compiler/Lexer/Lexer.cs:634`

**Purpose**: Begin lexing an f-string.

**Algorithm:**
1. Skip the `f` prefix
2. Read the quote character (`'` or `"`)
3. Check for triple-quoted f-strings
4. Push a new `FStringContext` onto the stack
5. Return `FStringStart` token

**Why use a stack**: F-strings can be nested inside each other's interpolation expressions, requiring a stack to track each level's context.

**Example token stream:**
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

### 3.6 `NextFStringToken()` - F-String State Machine

**Location**: `src/Sharpy.Compiler/Lexer/Lexer.cs:673`

This is a mini state machine for lexing inside f-strings. It's called automatically by `NextToken()` when `_fstringStack.Count > 0`.

**Two Modes:**

1. **Inside Expression** (`BraceDepth > 0`):
   - Tokenize normally (identifiers, operators, etc.)
   - `{` → Increase depth (nested dict/set)
   - `}` → Decrease depth; if zero, emit `FStringExprEnd`
   - `:` at depth 1 → Begin format spec reading

2. **Outside Expression** (`BraceDepth == 0`):
   - Read literal text until:
     - `{` → Start interpolation (emit `FStringExprStart`)
     - `{{` → Escaped brace (emit literal `{`)
     - `}}` → Escaped brace (emit literal `}`)
     - Quote match → End f-string (emit `FStringEnd`)
     - Backslash → Process escape sequence

**Format Spec Handling** (lines 758-822):

When we encounter `:` at brace depth 1, we enter "format spec mode". We read everything until the closing `}` as a single `FStringFormatSpec` token. This handles cases like:

```python
f"{value:>10.2f}"  # Format spec is ">10.2f"
```

Nested braces inside format specs are tracked separately to handle edge cases.

**Important implementation detail**: The format spec reader has its own nested brace tracking to handle cases where the format spec itself contains braces.

### 3.7 `ReadNumber()`

**Location**: `src/Sharpy.Compiler/Lexer/Lexer.cs:1174`

**Purpose**: Lex numeric literals (integers and floats).

**Features:**
- **Prefixes**: `0x` (hex), `0b` (binary), `0o` (octal)
- **Underscores**: Allows `1_000_000` for readability
  - Validates: no consecutive underscores, no trailing underscore
- **Decimal points**: `3.14` (must have digit after `.`)
- **Scientific notation**: `1e10`, `2.5e-3`
- **Suffixes**: `f`/`F`, `d`/`D`, `m`/`M`, `l`/`L`, `u`/`U`, `ul`/`UL`

**Token Type Decision:**
- Returns `TokenType.Float` if:
  - Has decimal point, OR
  - Has exponent (e/E), OR
  - Has float suffix (f, d, m)
- Otherwise returns `TokenType.Integer`

**Delegated Methods:**
- `ReadHexNumber()`: Handles `0xDEADBEEF`
- `ReadBinaryNumber()`: Handles `0b1010`
- `ReadOctalNumber()`: Handles `0o755`

**Validation Examples:**
```csharp
// Valid:
1_000_000  // Underscores for readability
0xFF_AA_BB // Hex with underscores
3.14e-10   // Scientific notation

// Invalid:
1__000     // Consecutive underscores - ERROR
1000_      // Trailing underscore - ERROR
```

### 3.8 `ReadIdentifierOrKeyword()`

**Location**: `src/Sharpy.Compiler/Lexer/Lexer.cs:1481`

**Purpose**: Lex identifiers and keywords.

**Algorithm:**
1. Read characters while `IsLetterOrDigit` or `_`
2. Look up the result in the `Keywords` dictionary
3. If found, return the keyword token type
4. Otherwise, return `TokenType.Identifier`

**Why simple**: Python/Sharpy identifiers follow simple rules compared to some languages. No need for complex Unicode handling in v0.1.

See `docs/language_specification/identifiers.md` for identifier naming rules.

### 3.9 `ReadLiteralName()`

**Location**: `src/Sharpy.Compiler/Lexer/Lexer.cs:1505`

**Purpose**: Lex backtick-delimited identifiers (`` `class` ``).

**Use Case**: Allows using reserved keywords as identifiers when escaped:
```python
`class` = "my-class"  # 'class' is a keyword, but `class` is an identifier
```

**Algorithm:**
1. Skip opening `` ` ``
2. Read until closing `` ` `` or newline
3. If newline/EOF before closing, throw error
4. Return `TokenType.Identifier` with the inner text

### 3.10 `ReadOperatorOrDelimiter()`

**Location**: `src/Sharpy.Compiler/Lexer/Lexer.cs:1541`

**Purpose**: Lex operators and delimiters.

**Strategy**: Greedy longest-match
1. Try 3-character operators first (`...`, `<<=`, `>>=`, `**=`, `//=`, `??=`)
2. Try 2-character operators (`==`, `!=`, `<=`, `>=`, `<<`, `>>`, `**`, `//`, `->`, `?.`, `??`, `+=`, `-=`, etc.)
3. Fall back to single-character operators (`+`, `-`, `*`, `/`, `=`, `<`, `>`, `(`, `)`, `[`, `]`, `{`, `}`, `,`, `:`, `.`, etc.)

**Bracket Depth Tracking** (lines 1714-1724):
- Increments `_bracketDepth` for `(`, `[`, `{`
- Decrements `_bracketDepth` for `)`, `]`, `}`
- Prevents negative depth (unmatched closing brackets set to 0)

**Special Case**: Rejects float literals starting with `.` (e.g., `.5`):
```csharp
if (c == '.' && _position + 1 < _source.Length && char.IsDigit(_source[_position + 1]))
    throw new LexerError("Float literals must have at least one digit before the decimal point...");
```

This is a v0.1 restriction for simplicity.

**Important for implicit line continuation**: The bracket depth tracking is essential for allowing multi-line expressions inside parentheses, brackets, and braces without explicit line continuation characters.

### 3.11 `ProcessEscapeSequence()`

**Location**: `src/Sharpy.Compiler/Lexer/Lexer.cs:1068`

**Purpose**: Convert escape sequences to their character equivalents.

**Supported Escapes:**
- Simple: `\n`, `\r`, `\t`, `\b`, `\f`, `\0`, `\\`, `\'`, `\"`, `\/`, `\a`, `\v`
- Hex: `\xhh` (2 hex digits)
- Unicode: `\uhhhh` (4 hex digits), `\Uhhhhhhhh` (8 hex digits)
- Octal: `\ooo` (1-3 octal digits, max 377 = 255)

**Error Handling**: Throws `LexerError` for invalid escape sequences.

**Why return char**: Escape sequences are processed during lexing and the resulting character is added to the string value. The token value contains the processed string, not the raw source.

### 3.12 `ReadComment()`

**Location**: `src/Sharpy.Compiler/Lexer/Lexer.cs:498`

**Purpose**: Skip comments from `#` to end of line.

**Behavior**: Reads until newline, then recursively calls `NextToken()` to get the next real token. Comments are not returned to the parser.

**Note**: The `TokenType.Comment` exists for potential documentation tool support, but currently comments are skipped.

### 3.13 `SkipWhitespace()`

**Location**: `src/Sharpy.Compiler/Lexer/Lexer.cs:475`

**Purpose**: Advance past spaces and tabs (but not newlines).

**Why not newlines**: Newlines are significant in Python/Sharpy syntax. They're only skipped when inside brackets (handled elsewhere).

### 3.14 `Peek(int offset = 1)`

**Location**: `src/Sharpy.Compiler/Lexer/Lexer.cs:492`

**Purpose**: Look ahead at future characters without advancing position.

**Returns**: The character at `_position + offset`, or `'\0'` if out of bounds.

**Use Cases**: Checking for multi-character operators, escape sequences, triple-quotes, etc.

### 3.15 `CreateToken()`

**Location**: `src/Sharpy.Compiler/Lexer/Lexer.cs:406`

**Purpose**: Factory method for creating tokens with position tracking.

**Why static**: It's a pure helper that doesn't need instance state.

### 3.16 `LogAndReturn()`

**Location**: `src/Sharpy.Compiler/Lexer/Lexer.cs:397`

**Purpose**: Log token creation (if logger enabled) and return the token.

**Why exists**: Keeps logging code out of the main tokenization logic, making it cleaner and easier to disable.

### 3.17 `TokenizeAll()`

**Location**: `src/Sharpy.Compiler/Lexer/Lexer.cs:123`

**Purpose**: Convenience method to lex the entire source into a list.

**Algorithm**: Repeatedly call `NextToken()` until `TokenType.Eof` is encountered.

**Use Case**: Useful for testing and for compiler phases that want all tokens upfront (vs. streaming).

## 4. Dependencies

### Internal Dependencies

- **`Token` class** (`Token.cs`): Represents a single token with type, value, and position
- **`TokenType` enum** (`Token.cs`): Defines all token types (~160 values)
- **`LexerError` class** (`LexerError.cs`): Exception type for lexer errors
- **`ICompilerLogger`** (`Sharpy.Compiler.Logging`): Logging interface for diagnostics

### External Dependencies

- `System.Text.StringBuilder`: Used for efficient string building during tokenization
- `System.Collections.Generic.Stack<T>`: For indentation and f-string context stacks
- `System.Collections.Generic.Queue<T>`: For pending token buffer
- `System.Collections.Generic.Dictionary<TKey, TValue>`: For keyword lookup

## 5. Patterns and Design Decisions

### 5.1 State Machine Design

The lexer is implemented as a **hand-written finite state machine** rather than using a lexer generator (like Lex/Flex). This provides:
- Full control over error messages
- Easy debugging
- No external tool dependencies
- Better performance (no indirection through generated tables)

### 5.2 Indentation Stack Pattern

The indentation stack is a classic solution for Python-style indentation:

```
Source:           Stack:           Tokens:
def foo():        [0]
    x = 1         [0, 4]           INDENT
    if y:         [0, 4]
        z = 2     [0, 4, 8]        INDENT
    return        [0, 4]           DEDENT
# EOF             [0]              DEDENT
```

**Key Insight**: DEDENT tokens can "cascade" when dedenting multiple levels. The pending tokens queue handles this elegantly.

### 5.3 Bracket Depth for Line Continuation

Python allows implicit line continuation inside brackets:

```python
result = (1 +
          2 +
          3)
```

The lexer tracks this via `_bracketDepth` and skips NEWLINEs when depth > 0. This is simpler than explicit line continuation with `\`.

### 5.4 F-String Context Stack

F-strings are complex because:
1. They can be nested: `f"outer {f'inner {x}'} more"`
2. Expressions can contain braces: `f"{{'key': value}}"`
3. Format specs exist: `f"{x:>10.2f}"`

The stack-based approach naturally handles nesting, and brace depth tracking handles nested braces.

### 5.5 Error Recovery

The lexer uses **exceptions for error handling** rather than error recovery. When a lexical error is encountered (unterminated string, invalid character), it throws `LexerError` immediately.

**Rationale**: For v0.1, simplicity is preferred. A production compiler might accumulate errors and continue lexing, but that adds complexity.

### 5.6 Token Value Semantics

Token values contain **processed content**, not raw source:
- String tokens contain the string with escape sequences resolved
- Number tokens contain the normalized number representation
- Keywords contain the keyword text

**Exception**: F-string tokens contain the raw delimiters (e.g., `f"`, `{`, `}`).

### 5.7 Position Tracking

Every token tracks:
- `Line`: 1-indexed line number
- `Column`: 1-indexed column number
- `Position`: 0-indexed byte offset in source

This enables high-quality error messages pointing to exact locations.

## 6. Debugging Tips

### 6.1 Enable Logging

Pass an `ICompilerLogger` to the constructor to see detailed token generation:

```csharp
var logger = new ConsoleLogger();
var lexer = new Lexer(source, logger);
```

This will log every token read with its position and value.

### 6.2 Use `TokenizeAll()` for Testing

For unit tests, call `TokenizeAll()` and inspect the resulting list:

```csharp
var tokens = lexer.TokenizeAll();
Assert.Equal(TokenType.Def, tokens[0].Type);
Assert.Equal("foo", tokens[1].Value);
```

### 6.3 Inspect Indentation Stack

Add debug breakpoints in `MeasureIndentation()` and examine `_indentStack.ToArray()` to understand indentation issues.

### 6.4 F-String Debugging

F-string bugs are common. Add breakpoints in `NextFStringToken()` and inspect:
- `_fstringStack.Peek().BraceDepth`
- `_fstringStack.Peek().InFormatSpec`
- `_fstringStack.Count` (nesting level)

### 6.5 Bracket Depth Issues

If newlines are incorrectly skipped/emitted, check `_bracketDepth`:
- Should be 0 at top level
- Incremented by `(`, `[`, `{`
- Decremented by `)`, `]`, `}`

Watch for unmatched brackets causing incorrect depth.

### 6.6 Common Error Messages

| Error | Cause | Fix |
|-------|-------|-----|
| "Tabs are not allowed for indentation" | Tab character in indentation | Use 4 spaces |
| "Indentation must be multiple of 4" | Wrong indent (e.g., 2 or 6 spaces) | Use 4, 8, 12, etc. |
| "Indentation mismatch" | Dedented to invalid level | Match a previous indent level |
| "Unterminated string literal" | Missing closing quote or newline in string | Close string or use triple-quotes for multiline |
| "Unterminated f-string" | Missing closing quote in f-string | Close f-string properly |
| "Unmatched '}' in f-string" | Extra `}` without matching `{` | Use `}}` for literal `}` |
| "Invalid character: '...'" | Character not recognized by lexer | Remove or escape the character |

### 6.7 Testing Strategy

**Unit Tests**: Test individual methods with known inputs:
- `ReadString()` with various escape sequences
- `ReadNumber()` with hex, binary, octal, floats
- `MeasureIndentation()` with valid/invalid indents

**Integration Tests**: Test `TokenizeAll()` with complete source files to ensure the state machine transitions work correctly.

**File-Based Tests**: Use the file-based test fixtures in `src/Sharpy.Compiler.Tests/Integration/TestFixtures/` (`.spy` + `.expected` pairs) to test real-world code.

## 7. Contribution Guidelines

### 7.1 When to Modify This File

Modify `Lexer.cs` when:
- **Adding new token types**: Update `Keywords` dictionary and add to `TokenType` enum
- **Adding new operators**: Update `ReadOperatorOrDelimiter()` with new cases
- **Changing indentation rules**: Modify `MeasureIndentation()` validation
- **Fixing lexer bugs**: Correct logic in appropriate methods
- **Adding new literal types**: Add new `ReadXxx()` methods and dispatch in `NextToken()`

### 7.2 When NOT to Modify

Do NOT modify `Lexer.cs` for:
- **Syntax changes**: Those belong in the Parser
- **Semantic rules**: Those belong in Semantic Analysis
- **Code generation**: That's in RoslynEmitter
- **Runtime behavior**: That's in Sharpy.Core

### 7.3 Testing Requirements

Any change to `Lexer.cs` MUST include:
1. **Unit tests**: Test the specific method changed
2. **Integration tests**: Test full tokenization of source snippets
3. **Error tests**: Ensure invalid input throws appropriate `LexerError`

Run tests with:
```bash
dotnet test --filter "FullyQualifiedName~Lexer"
```

### 7.4 Code Style

Follow existing patterns:
- Use `_camelCase` for private fields
- Use `PascalCase` for public methods
- Keep methods focused (Single Responsibility)
- Document complex logic with comments
- Use descriptive variable names (`startLine` not `sl`)

### 7.5 Performance Considerations

The lexer is performance-critical (runs on every compilation). Consider:
- **Avoid allocations**: Reuse `StringBuilder` when possible
- **Minimize string operations**: Use character comparisons over string comparisons
- **Avoid redundant work**: Don't re-parse the same content
- **Profile before optimizing**: Use benchmarks to identify bottlenecks

### 7.6 Backward Compatibility

When adding features:
- **Don't break existing tokens**: Ensure old code still lexes correctly
- **Add, don't change**: Prefer adding new token types over changing existing ones
- **Document breaking changes**: If unavoidable, document in release notes

### 7.7 Common Modifications

**Adding a Keyword:**
1. Add to `TokenType` enum in `Token.cs`
2. Add to `Keywords` dictionary (line 34)
3. Add tests for the new keyword
4. Update `docs/language_specification/keywords.md`

**Adding an Operator:**
1. Add to `TokenType` enum in `Token.cs`
2. Add case in `ReadOperatorOrDelimiter()` (appropriate 1/2/3 char section)
3. Add tests for the operator
4. Update language specification docs

**Changing Indentation Rules:**
1. Modify `MeasureIndentation()` validation
2. Update error messages
3. Add tests for new rules
4. Update `docs/language_specification/indentation.md`

## 8. Cross-References

### Related Files

- **`Token.cs`** (`src/Sharpy.Compiler/Lexer/Token.cs`): Token and TokenType definitions
- **`LexerError.cs`** (`src/Sharpy.Compiler/Lexer/LexerError.cs`): Lexer exception type
- **Parser** (`src/Sharpy.Compiler/Parser/`): Consumes tokens from lexer
- **Semantic Analysis** (`src/Sharpy.Compiler/Semantic/`): Works with AST, not tokens

### Related Documentation

- **`docs/language_specification/lexer_implementation.md`**: Lexer specification and state machine details
- **`docs/language_specification/indentation.md`**: Indentation rules and INDENT/DEDENT semantics
- **`docs/language_specification/keywords.md`**: Complete keyword list
- **`docs/language_specification/identifiers.md`**: Identifier naming rules

### Test Files

- **`src/Sharpy.Compiler.Tests/Lexer/`**: Unit tests for lexer
- **`src/Sharpy.Compiler.Tests/Integration/TestFixtures/`**: File-based integration tests

### Usage Example

```csharp
// Basic usage
var source = @"
def greet(name: str) -> str:
    return f""Hello, {name}!""
";

var lexer = new Lexer(source);
var tokens = lexer.TokenizeAll();

// First few tokens:
// [0] = NEWLINE (blank line)
// [1] = Def ("def")
// [2] = Identifier ("greet")
// [3] = LeftParen ("(")
// [4] = Identifier ("name")
// [5] = Colon (":")
// [6] = Identifier ("str")
// [7] = RightParen (")")
// [8] = Arrow ("->")
// [9] = Identifier ("str")
// [10] = Colon (":")
// [11] = NEWLINE
// [12] = INDENT
// [13] = Return ("return")
// [14] = FStringStart ("f\"")
// [15] = FStringText ("Hello, ")
// [16] = FStringExprStart ("{")
// [17] = Identifier ("name")
// [18] = FStringExprEnd ("}")
// [19] = FStringText ("!")
// [20] = FStringEnd ("\"")
// [21] = NEWLINE
// [22] = DEDENT
// [23] = EOF
```

---

**Key Takeaway**: The Lexer is a carefully crafted state machine that handles Python's complex indentation rules, f-string interpolation, and various literal formats. Understanding its state (indentation stack, bracket depth, f-string stack) is crucial for debugging and extending it.
