# Walkthrough: Token.cs

**Source File**: `src/Sharpy.Compiler/Lexer/Token.cs`

---

## Overview

`Token.cs` is the foundational data structure file for the Sharpy compiler's lexical analysis phase. It defines two essential types:

1. **`TokenType`** - An enumeration of all possible token types in the Sharpy language
2. **`Token`** - A record that represents a single lexical token with its type, value, and source location

This file serves as the contract between the **Lexer** (which produces tokens) and the **Parser** (which consumes them). Every piece of Sharpy source code is broken down into a sequence of these tokens during the first phase of compilation.

**Role in the Compiler Pipeline:**
```
Source Code → [Lexer] → Tokens → [Parser] → AST → [Semantic] → [CodeGen] → C#
                         ↑
                    Token.cs defines these
```

## Class/Type Structure

### 1. `TokenType` Enum

The `TokenType` enum is a comprehensive catalog of every distinct syntactic element that can appear in Sharpy source code. It contains **129 different token types** organized into logical categories.

#### Categories Breakdown:

**Literals** (7 types)
- Primitive values: `Integer`, `Float`, `String`, `RawString`, `FString`
- Boolean/None: `True`, `False`, `None`

```csharp
// Examples of literal tokens:
42         → TokenType.Integer
3.14       → TokenType.Float
"hello"    → TokenType.String
r"\n\t"    → TokenType.RawString
f"{name}"  → TokenType.FString
True       → TokenType.True
None       → TokenType.None
```

**Keywords** (42 types)
- **Control Flow**: `Def`, `Class`, `Struct`, `Interface`, `Enum`, `If`, `Else`, `Elif`, `While`, `For`, `In`, `Return`, `Break`, `Continue`, `Pass`, `Try`, `Except`, `Finally`, `Raise`, `Assert`
- **Import System**: `Import`, `From`, `As`
- **Type/Value Modifiers**: `Auto`, `Const`, `Lambda`
- **Boolean Operators**: `And`, `Or`, `Not`, `Is`

```csharp
// Keywords are language-reserved identifiers
def greet(name: str):  // Def, Identifier, Identifier, Colon, Identifier
    return f"Hi {name}" // Return, FString
```

**Operators** (47 types)
- **Arithmetic**: `Plus` (+), `Minus` (-), `Star` (*), `Slash` (/), `DoubleSlash` (//), `Percent` (%), `DoubleStar` (**)
- **Comparison**: `Equal` (==), `NotEqual` (!=), `Less` (<), `Greater` (>), `LessEqual` (<=), `GreaterEqual` (>=)
- **Bitwise**: `Ampersand` (&), `Pipe` (|), `Caret` (^), `Tilde` (~), `LeftShift` (<<), `RightShift` (>>)
- **Assignment**: `Assign` (=) plus compound assignments (`+=`, `-=`, `*=`, `/=`, `//=`, `%=`, `**=`, `&=`, `|=`, `^=`, `<<=`, `>>=`)
- **Special**: `Question` (?), `NullConditional` (?.),`NullCoalesce` (??), `Ellipsis` (...)

```csharp
// Operator precedence and associativity are handled by the Parser
x = 2 ** 3 + 4  // Assign, Integer, DoubleStar, Integer, Plus, Integer
y?.method()     // Identifier, NullConditional, Identifier, LeftParen, RightParen
```

**Delimiters** (14 types)
- Brackets: `LeftParen` ((), `RightParen` ()), `LeftBracket` ([), `RightBracket` (]), `LeftBrace` ({), `RightBrace` (})
- Punctuation: `Comma` (,), `Colon` (:), `Semicolon` (;), `Dot` (.)
- Special: `Arrow` (->), `At` (@), `Backslash` (\), `Backtick` (`)

```csharp
// Delimiters structure the code
def func(a: int, b: int) -> int:  // Arrow for return type annotation
    return a + b                   // Colon ends function signature

@decorator  // At symbol for decorators
class MyClass:
    pass
```

**Special** (6 types)
- Indentation: `Indent`, `Dedent` - Critical for Python-like significant whitespace
- Flow control: `Newline`, `Eof` (End of File)
- Comments: `Comment` (usually filtered out but available for tooling)
- Literals: `Backtick` (for escaping reserved names)

```csharp
// Sharpy uses significant whitespace like Python
if condition:     // Newline, then Indent
    do_something() // Indented block
    do_more()      // Still indented
                   // Dedent at end of block
```

**Design Note**: The enum values have **no explicit numeric values** assigned. This is intentional - the ordinal values don't matter, only the symbolic names. This makes it easy to add new token types without worrying about numbering conflicts.

### 2. `Token` Record

The `Token` record is an **immutable data structure** (C# record type) that represents a single lexical token discovered in the source code.

```csharp
public record Token
{
    public TokenType Type { get; init; }      // What kind of token (keyword, operator, etc.)
    public string Value { get; init; }        // The actual text from source code
    public int Line { get; init; }            // Line number (1-based)
    public int Column { get; init; }          // Column number (1-based)
    
    public Token(TokenType type, string value, int line, int column)
    {
        Type = type;
        Value = value;
        Line = line;
        Column = column;
    }
}
```

#### Properties Explained:

**`Type` (TokenType)**
- The category/classification of this token
- Tells the parser how to interpret the token
- Examples: `TokenType.Identifier`, `TokenType.Plus`, `TokenType.If`

**`Value` (string)**
- The original source text that produced this token
- Defaults to empty string if not provided
- Examples:
  - For `TokenType.Identifier`: `"myVariable"`
  - For `TokenType.Integer`: `"42"`
  - For `TokenType.Plus`: `"+"`
  - For `TokenType.If`: `"if"`

**`Line` (int)**
- The line number where this token appears (1-based indexing)
- Used for error messages and debugging
- Example: "Syntax error at line 42, column 15"

**`Column` (int)**
- The column/character position within the line (1-based indexing)
- Used for precise error location and IDE integration
- Helps show the exact position with a caret (^) in error messages

#### Why a Record?

The `Token` type uses C#'s `record` feature for several important reasons:

1. **Immutability**: Once created, a token cannot be modified. This prevents bugs from accidental mutation during compilation.
2. **Value Semantics**: Two tokens with the same data are considered equal, which is useful for testing.
3. **Built-in ToString()**: Records automatically generate readable string representations for debugging.
4. **Init-only Properties**: Properties can only be set during construction or object initialization.

```csharp
// Creating tokens
var token1 = new Token(TokenType.Integer, "42", 1, 5);

// With init syntax
var token2 = new Token(TokenType.Identifier, "x", 2, 1);

// Records provide value equality
var token3 = new Token(TokenType.Integer, "42", 1, 5);
bool equal = token1 == token3;  // True! Same type, value, line, column

// Records are immutable - this won't compile:
// token1.Value = "43";  ❌ Error: init-only property
```

## Key Functions/Methods

### Constructor

```csharp
public Token(TokenType type, string value, int line, int column)
```

**Purpose**: Creates a new token instance with all required information.

**Parameters**:
- `type`: The classification of the token (from `TokenType` enum)
- `value`: The raw source text that created this token
- `line`: Source line number (1-indexed)
- `column`: Column position in the line (1-indexed)

**Usage Example**:
```csharp
// In the Lexer, when we encounter "def" at line 5, column 1:
var defToken = new Token(TokenType.Def, "def", 5, 1);

// When we find the number "123" at line 10, column 15:
var numberToken = new Token(TokenType.Integer, "123", 10, 15);

// When we hit end-of-file at line 100:
var eofToken = new Token(TokenType.Eof, "", 100, 1);
```

**Design Decision**: All parameters are required (no optional parameters). This ensures every token has complete location information for error reporting.

### Implicit Methods (from `record`)

Because `Token` is a record, it automatically gets several useful methods:

**`ToString()`** - Returns a string representation
```csharp
var token = new Token(TokenType.Plus, "+", 5, 10);
Console.WriteLine(token);
// Output: Token { Type = Plus, Value = +, Line = 5, Column = 10 }
```

**`Equals()` and `GetHashCode()`** - Value-based equality
```csharp
var t1 = new Token(TokenType.Integer, "42", 1, 1);
var t2 = new Token(TokenType.Integer, "42", 1, 1);
var t3 = new Token(TokenType.Integer, "99", 1, 1);

t1.Equals(t2);  // True - same values
t1 == t2;       // True
t1.Equals(t3);  // False - different Value
```

**`with` Expression** - Non-destructive mutation
```csharp
var original = new Token(TokenType.Identifier, "x", 5, 10);

// Create a new token with updated line number
var updated = original with { Line = 6 };
// original is unchanged, updated is a new token with Line=6
```

## Dependencies

### Namespace
```csharp
namespace Sharpy.Compiler.Lexer;
```

The `Token` and `TokenType` types are in the `Sharpy.Compiler.Lexer` namespace, placing them logically with other lexer-related code.

### Direct Dependencies

**No external dependencies!** This file is completely self-contained with no imports or references. It only uses:
- Built-in C# types (`string`, `int`)
- Built-in C# features (`enum`, `record`)

### Reverse Dependencies (What Uses This File)

This file is **heavily referenced** throughout the compiler:

1. **`Lexer.cs`** - Creates `Token` instances and assigns `TokenType` values
2. **`Parser.cs`** - Consumes tokens, checks `TokenType`, uses `Value` for literals
3. **`AstDumper.cs`** - May display token information in AST dumps
4. **Test files** - Verify correct tokenization by checking `TokenType` and `Value`
5. **Error reporting** - Uses `Line` and `Column` for diagnostic messages

```csharp
// In Lexer.cs
var tokens = new List<Token>();
tokens.Add(new Token(TokenType.Def, "def", currentLine, currentColumn));

// In Parser.cs
Token current = Peek();
if (current.Type == TokenType.Identifier)
{
    string name = current.Value;
    // ...
}

// In error reporting
throw new ParseException(
    $"Unexpected token '{token.Value}' at line {token.Line}, column {token.Column}");
```

## Patterns and Design Decisions

### 1. Comprehensive Token Set

**Decision**: Include ALL possible tokens upfront, even if not all are implemented yet.

**Rationale**:
- Makes the language specification explicit
- Allows parser to provide better error messages ("not yet implemented" vs "syntax error")
- Easier to add features incrementally without changing the token enum

**Evidence**: Comments like `// @ (decorators)` show planned features.

### 2. Immutability via Records

**Decision**: Use C# 9.0+ `record` for `Token` instead of `class`.

**Benefits**:
- **Thread-safe**: Tokens can be shared across threads safely
- **Debugging**: Easier to track token flow without worrying about mutation
- **Testing**: Value equality makes test assertions cleaner
- **Performance**: Records can be stack-allocated in some scenarios

**Trade-off**: Slightly more memory allocations when "modifying" tokens (creates new instances), but this is rarely needed.

### 3. Separate Concerns

**Decision**: Keep token type and token data in separate but related types.

**Pattern**:
```csharp
enum TokenType { ... }      // The "what" - classification
record Token { ... }        // The "where" and "how" - data + location
```

**Benefit**: Parser can check types without caring about values, and can extract values without repeated type checks.

```csharp
// Clean separation enables clean code
switch (token.Type)
{
    case TokenType.Identifier:
        string name = token.Value;  // Safe - we know it's an identifier
        break;
    case TokenType.Integer:
        int value = int.Parse(token.Value);  // Safe - we know it's a number
        break;
}
```

### 4. Token Categorization

**Decision**: Group related tokens with comments in `TokenType` enum.

**Evidence**:
```csharp
// Literals
Integer,
Float,
String,

// Keywords - Control Flow
Def,
Class,
...
```

**Benefit**: Makes the enum easier to navigate and understand. New contributors can quickly find where to add new token types.

### 5. Python-Inspired Indentation Tokens

**Decision**: Include `Indent`, `Dedent`, `Newline` as first-class tokens.

**Rationale**: Sharpy uses significant whitespace like Python. The lexer handles the complexity of tracking indentation levels and emits these special tokens.

**Impact on Parser**: The parser treats `Indent` like an opening brace `{` and `Dedent` like a closing brace `}`.

```csharp
// Source:
if x > 0:      // If, Identifier, Greater, Integer, Colon, Newline
    print(x)   // Indent, Identifier, LeftParen, Identifier, RightParen, Newline
               // Dedent (implicit)
```

### 6. Location Tracking

**Decision**: Every token stores its source location (`Line`, `Column`).

**Benefit**: Enables precise error messages:
```
Error at line 42, column 15: Unexpected token '}'
   |
42 | def func():
   |               ^
```

**Cost**: 8 extra bytes per token (two int32 fields). This is acceptable given the value for debugging.

### 7. Default Value for `Value` Property

**Decision**: `Value` defaults to `string.Empty` instead of being nullable.

```csharp
public string Value { get; init; } = string.Empty;
```

**Rationale**: Most tokens have a value, and empty string is safer than null (avoids null reference exceptions). Tokens like `Eof`, `Indent`, `Dedent` naturally have empty values.

**Alternative Considered**: Making `Value` nullable (`string?`) but rejected because it adds null-checking burden on consumers.

## Debugging Tips

### 1. Inspecting Token Streams

When debugging lexer issues, print the entire token stream:

```csharp
var lexer = new Lexer(sourceCode);
var tokens = lexer.Tokenize();

foreach (var token in tokens)
{
    Console.WriteLine($"{token.Line}:{token.Column} {token.Type,-20} '{token.Value}'");
}
```

**Output Example**:
```
1:1  Def                  'def'
1:5  Identifier           'greet'
1:10 LeftParen            '('
1:11 Identifier           'name'
1:15 Colon                ':'
1:17 Identifier           'str'
1:20 RightParen           ')'
1:21 Arrow                '->'
1:24 None                 'None'
...
```

### 2. Finding Token Type Issues

If the parser is complaining about unexpected tokens:

1. **Check the lexer output**: Verify the `TokenType` is what you expect
2. **Look for off-by-one**: Are `Line` and `Column` correct?
3. **Check keyword vs identifier**: Is "true" being lexed as `True` (keyword) or `Identifier`?

```csharp
// Add a breakpoint in Parser.cs and inspect:
var currentToken = Peek();
// Examine: Type, Value, Line, Column
```

### 3. Common Token Type Confusion

**Keywords vs. Identifiers**:
```csharp
// "class" should be TokenType.Class, not TokenType.Identifier
// "Class" (capitalized) might be TokenType.Identifier depending on lexer rules
```

**Operators vs. Delimiters**:
```csharp
// "->" is TokenType.Arrow (one token), not TokenType.Minus + TokenType.Greater
```

**String Types**:
```csharp
"hello"    // TokenType.String
r"hello"   // TokenType.RawString
f"hello"   // TokenType.FString
```

### 4. Testing Token Equality

When writing tests, use record equality:

```csharp
// Test helper
void AssertToken(Token actual, TokenType expectedType, string expectedValue, int line, int col)
{
    Assert.Equal(expectedType, actual.Type);
    Assert.Equal(expectedValue, actual.Value);
    Assert.Equal(line, actual.Line);
    Assert.Equal(col, actual.Column);
}

// Or use record equality directly:
var expected = new Token(TokenType.Plus, "+", 1, 5);
var actual = lexer.NextToken();
Assert.Equal(expected, actual);  // Uses record value equality
```

### 5. Debugging Indentation Issues

Indent/Dedent tokens are tricky. To debug:

```csharp
// Filter for indentation tokens
var indentTokens = tokens.Where(t => 
    t.Type == TokenType.Indent || 
    t.Type == TokenType.Dedent || 
    t.Type == TokenType.Newline);

foreach (var token in indentTokens)
{
    Console.WriteLine($"{token.Line}:{token.Column} {token.Type}");
}
```

**Expected Pattern**:
```
After ":" → Newline → Indent (if indenting)
End of block → Dedent (one or more)
```

## Contribution Guidelines

### When to Modify This File

You should **only** modify `Token.cs` when:

1. **Adding a new operator or keyword to Sharpy**
   - Add the new `TokenType` to the appropriate category
   - Update comments to document the symbol

2. **Adding new token metadata**
   - Example: Adding a `Span` or `Length` property to `Token`
   - Ensure it's immutable (`init` or readonly)

3. **Refactoring token categories**
   - Breaking `TokenType` into multiple enums (major change)
   - This would require updating Lexer and Parser extensively

### What NOT to Change

**Do NOT**:
- ❌ Remove existing `TokenType` values (breaks backward compatibility)
- ❌ Rename `TokenType` values (breaks existing code)
- ❌ Make `Token` properties mutable (breaks immutability contract)
- ❌ Change from `record` to `class` (breaks value semantics)
- ❌ Add methods to `Token` record (keep it a pure data structure)

### Adding a New Token Type

**Step-by-step**:

1. **Identify the category** (Literal, Keyword, Operator, Delimiter, Special)

2. **Add to `TokenType` enum** in the appropriate section:
```csharp
// If adding a new operator "??"
public enum TokenType
{
    // ... existing tokens ...
    
    // Operators - Special
    Question,          // ?
    NullCoalesce,      // ?? ← Add here with comment
    
    // ... rest of tokens ...
}
```

3. **Update the Lexer** to recognize the new token:
```csharp
// In Lexer.cs
case '?':
    if (PeekChar() == '?')
    {
        Advance();
        AddToken(TokenType.NullCoalesce, "??");
    }
    else
    {
        AddToken(TokenType.Question, "?");
    }
    break;
```

4. **Update the Parser** to handle the new token:
```csharp
// In Parser.cs
case TokenType.NullCoalesce:
    return ParseNullCoalesce();
```

5. **Write tests**:
```csharp
[Fact]
public void Lexer_Tokenizes_NullCoalesce()
{
    var source = "x ?? y";
    var tokens = new Lexer(source).Tokenize();
    
    Assert.Equal(4, tokens.Count);  // x, ??, y, EOF
    Assert.Equal(TokenType.Identifier, tokens[0].Type);
    Assert.Equal(TokenType.NullCoalesce, tokens[1].Type);
    Assert.Equal("??", tokens[1].Value);
}
```

### Best Practices

1. **Group related tokens** with clear comments
2. **Use descriptive names** (e.g., `DoubleSlash` instead of `SlashSlash` to match `DoubleEquals`)
3. **Keep alphabetical order within categories** when possible
4. **Document unusual tokens** with comments explaining their purpose
5. **Verify token type uniqueness** - each symbol should map to exactly one token type

### Testing Checklist

When adding or modifying token types, test:

- ✅ Lexer correctly identifies the new token
- ✅ Token value is captured correctly
- ✅ Line and column numbers are accurate
- ✅ Parser can consume the token
- ✅ Error messages include the new token when relevant
- ✅ Token appears in dumps/debug output correctly

### Documentation Updates

After modifying `Token.cs`:

1. Update `docs/specs/language_reference.md` if adding new syntax
2. Update lexer tests in `Sharpy.Compiler.Tests/Lexer/`
3. Update this walkthrough if the structure changes significantly
4. Consider updating `docs/manual/` if user-facing

## Summary

`Token.cs` is a small but critical file that defines the vocabulary of the Sharpy language. It's designed for:

- **Simplicity**: Pure data structures, no logic
- **Immutability**: Safe to share, easy to reason about
- **Completeness**: All possible tokens defined upfront
- **Clarity**: Well-organized and documented

As a newcomer, you'll interact with this file primarily through:
- Reading `TokenType` to understand what syntactic elements exist
- Creating `Token` instances in the Lexer
- Checking `Token.Type` in the Parser
- Using `Token` location info in error messages

The design is stable and changes are infrequent. Most new features involve adding new `TokenType` values rather than changing the `Token` structure itself.
