# Walkthrough: Parser.Types.cs

**Source File**: `src/Sharpy.Compiler/Parser/Parser.Types.cs`

---

## Overview

`Parser.Types.cs` is a partial class file implementing **type annotation parsing** and various **parser utility methods** for the Sharpy compiler. This file transforms type syntax from the token stream into structured `TypeAnnotation` AST nodes, supporting both traditional syntax (`list[str]`) and Python-inspired shorthand forms (`[str]`, `{K: V}`, `(T, U)`).

**Role in Pipeline:**
```
Source (.spy) → Lexer (tokens) → [Parser.Types] → TypeAnnotation nodes → Semantic Analysis
```

This file handles:
- Standard type annotations: `int`, `str`, `list[T]`, `dict[K, V]`
- Python-style shorthand: `[T]` for lists, `{T}` for sets, `{K: V}` for dicts
- Tuple and function types: `(T, U)` for tuples, `(T) -> U` for functions
- Array syntax: `T[]` for arrays
- Nullable types: `T?` for optional values
- Special keywords: `auto` (type inference), `None` (void return type)
- F-string parsing (segmented approach with interpolated expressions)
- Utility methods for token navigation, span tracking, and validation

---

## Class/Type Structure

### Partial Class: `Parser`

This file is part of the `Parser` partial class, which is split across multiple files:
- **Parser.cs**: Core parser infrastructure and module/statement entry points
- **Parser.Expressions.cs**: Expression parsing with operator precedence
- **Parser.Primaries.cs**: Primary expressions (literals, identifiers, comprehensions)
- **Parser.Statements.cs**: Statement parsing (if, while, for, etc.)
- **Parser.Definitions.cs**: Definition parsing (functions, classes, structs)
- **Parser.Types.cs**: Type annotation parsing and utility methods (this file)

The parser maintains state from `Parser.cs`:
```csharp
private readonly List<Token> _tokens;  // Input token stream
private int _position;                 // Current position in token stream
private readonly ICompilerLogger _logger;
```

Helper properties for navigation:
```csharp
private Token Current;    // Current token being examined
private Token Previous;   // Last consumed token
private Token Peek(int);  // Look ahead in the token stream
private bool IsAtEnd;     // Check if at EOF
```

---

## Key Functions/Methods

### Type Annotation Parsing

#### `ParseTypeAnnotation()` (Lines 14-85)

**Purpose**: Main entry point for parsing type annotations. Orchestrates parsing of base types, array suffixes, and nullable modifiers.

**Algorithm:**
1. Determine which shorthand form or standard type to parse based on lookahead
2. Parse the base type (delegates to specialized methods)
3. Apply array suffix `[]` as a wrapper type (can be chained: `T[][]`)
4. Apply nullable suffix `?` if present

**Key Logic:**
```csharp
// Dispatch based on leading token
if (Current.Type == TokenType.LeftBracket)
    baseType = ParseListTypeShorthand(...);        // [T] → list[T]
else if (Current.Type == TokenType.LeftBrace)
    baseType = ParseSetOrDictTypeShorthand(...);   // {T} or {K: V}
else if (Current.Type == TokenType.LeftParen)
    baseType = ParseTupleOrFunctionTypeShorthand(...); // (T, U) or (T) -> U
else
    baseType = ParseStandardTypeAnnotation(...);   // int, list[T], etc.

// Handle T[] (can be chained: T[][] becomes array[array[T]])
while (Current == '[' && Peek() == ']') {
    baseType = new TypeAnnotation { Name = "array", TypeArguments = [baseType] };
}

// Handle T?
if (Current.Type == TokenType.Question) {
    baseType = baseType with { IsNullable = true };
}
```

**Return Value**: A `TypeAnnotation` AST node with:
- `Name`: Type name (`"int"`, `"list"`, `"dict"`, etc.)
- `TypeArguments`: Generic type parameters
- `IsNullable`: Whether `?` suffix was present
- Position tracking (LineStart/End, ColumnStart/End, Span)

**Design Insight**: The method uses the **factory pattern** for dispatch—examining the current token to determine which specialized parser to invoke. This keeps each parser focused on a single syntactic form.

---

### Standard Type Parsing

#### `ParseStandardTypeAnnotation()` (Lines 91-144)

**Purpose**: Parses traditional type syntax: `int`, `str`, `list[T]`, `dict[K, V]`, `auto`, `None`.

**Syntax Examples:**
- `int` → `TypeAnnotation { Name = "int" }`
- `list[str]` → `TypeAnnotation { Name = "list", TypeArguments = [str] }`
- `dict[str, int]` → `TypeAnnotation { Name = "dict", TypeArguments = [str, int] }`
- `auto` → `TypeAnnotation { Name = "auto" }` (type inference)
- `None` → `TypeAnnotation { Name = "None" }` (used in `def foo() -> None:`)

**Algorithm:**
1. Parse the base type name (identifier, `auto`, or `None`)
2. Check for generic arguments `[T, U, ...]`
3. Recursively parse each type argument (allows nested generics like `list[dict[str, int]]`)
4. Build `TypeAnnotation` with all components

**Special Handling:**
- `auto` keyword: Enables type inference (e.g., `x: auto = 42` infers `int`)
- `None` keyword: Represents void/no return type in function signatures

**Generic Argument Parsing (Lines 114-127):**
```csharp
if (Current.Type == TokenType.LeftBracket && Peek().Type != TokenType.RightBracket)
{
    Advance();
    do
    {
        typeArgs.Add(ParseTypeAnnotation());  // Recursive call!
        if (Current.Type == TokenType.Comma)
            Advance();
        else
            break;
    } while (true);
    Expect(TokenType.RightBracket);
}
```

**Important Distinction**: The check `Peek().Type != TokenType.RightBracket` distinguishes:
- `list[T]` (generic arguments) from `list[]` (which would be a syntax error here, but is valid as an array suffix)

---

### Python Shorthand Forms

#### `ParseListTypeShorthand()` (Lines 149-177)

**Purpose**: Parses `[T]` syntax as syntactic sugar for `list[T]`.

**Syntax**: `[str]` → `TypeAnnotation { Name = "list", TypeArguments = [str] }`

**Error Handling:**
- `[]` (empty brackets) throws `ParserError`: "List type shorthand requires an element type: [T]"

**Implementation Note**: The shorthand produces **identical AST** to `list[T]`—it's purely syntactic sugar resolved at parse time.

**Example Flow:**
```
Input:  [dict[str, int]]
Tokens: LeftBracket, Identifier("dict"), LeftBracket, Identifier("str"), Comma, ...
Step 1: Consume '[' (line 151)
Step 2: Call ParseTypeAnnotation() → recursively parses dict[str, int]
Step 3: Expect ']' (line 160)
Result: TypeAnnotation { Name = "list", TypeArguments = [dict[str, int]] }
```

---

#### `ParseSetOrDictTypeShorthand()` (Lines 183-237)

**Purpose**: Parses `{T}` for sets or `{K: V}` for dicts. Disambiguates by checking for `:` after first type.

**Disambiguation Logic:**
```csharp
var firstType = ParseTypeAnnotation();
if (Current.Type == TokenType.Colon) {
    // {K: V} → dict[K, V]
    Advance();  // consume ':'
    var valueType = ParseTypeAnnotation();
    return new TypeAnnotation { Name = "dict", TypeArguments = [firstType, valueType] };
} else {
    // {T} → set[T]
    return new TypeAnnotation { Name = "set", TypeArguments = [firstType] };
}
```

**Syntax Examples:**
- `{str}` → `set[str]`
- `{str: int}` → `dict[str, int]`
- `{}` → Error: "Set/dict type shorthand requires type arguments: {T} for set or {K: V} for dict"

**Error Handling:**
- Empty `{}` throws `ParserError`: Must provide type information

**Grammar Design**: This is an elegant single-lookahead disambiguation. The grammar is:
```
SetOrDict ::= '{' Type ( ':' Type )? '}'
```

---

#### `ParseTupleOrFunctionTypeShorthand()` (Lines 244-317)

**Purpose**: Parses parenthesized type syntax. Disambiguates between tuples and function types by checking for `->`.

**Disambiguation Logic:**
```csharp
// Parse types inside parentheses
var types = ParseCommaSeparatedTypes();

if (Current.Type == TokenType.Arrow) {
    // (T, U) -> R is a function type
    Advance();  // consume '->'
    var returnType = ParseTypeAnnotation();
    return new TypeAnnotation {
        Name = "function",
        TypeArguments = [...types, returnType]  // Last arg is return type
    };
} else {
    // (T, U) is a tuple
    return new TypeAnnotation { Name = "tuple", TypeArguments = types };
}
```

**Syntax Examples:**
- `()` → Empty tuple: `tuple[]`
- `(int)` → Single-element tuple: `tuple[int]` (note: trailing comma optional)
- `(int, str)` → Tuple: `tuple[int, str]`
- `(int, str) -> bool` → Function: `function[int, str, bool]` (last type is return)

**Function Type Encoding**: The return type is stored as the **last element** in `TypeArguments`. For `(T, U) -> R`:
- TypeArguments = `[T, U, R]`
- Name = `"function"`
- The semantic analyzer later separates parameters from return type

**Trailing Comma Handling (Lines 249-268)**: The code tracks `hasTrailingComma` for potential future use (e.g., distinguishing `(T)` from `(T,)` like Python), but currently both produce `tuple[T]`.

**Grammar:**
```
TupleOrFunction ::= '(' (Type (',' Type)* ','?)? ')' ('->' Type)?
```

---

### Utility Methods

#### `GetSpanFromToken()` and `GetSpanFromTokens()` (Lines 325-343)

**Purpose**: Create `TextSpan` objects for source location tracking, used for error reporting and IDE integration.

**Usage Pattern:**
```csharp
var span = GetSpanFromTokens(startToken, endToken);  // Inclusive range
node.Span = span;  // Attach to AST node for diagnostics
```

**Return Value**: `TextSpan?` (nullable) — returns `null` if token lacks position data

**Implementation Detail (Lines 334-342):**
```csharp
private static Text.TextSpan? GetSpanFromTokens(Token start, Token end)
{
    var startSpan = start.GetSpan();
    var endSpan = end.GetSpan();

    if (startSpan == null || endSpan == null)
        return null;

    return Text.TextSpan.FromBounds(startSpan.Value.Start, endSpan.Value.End);
}
```

Uses `TextSpan.FromBounds()` to create a span covering the entire type annotation from first to last token.

---

#### `CombineSpans()` (Lines 349-354)

**Purpose**: Merges two optional spans into a single span covering both.

**Use Case**: When building composite AST nodes from multiple sub-nodes, this combines their source locations.

**Null Handling**: Returns `null` if either input span is `null` (defensive programming for incomplete position data).

---

### Token Navigation & Validation

#### `Expect()` (Lines 356-361)

**Purpose**: Consume a token if it matches the expected type, otherwise throw `ParserError`.

**Implementation:**
```csharp
private void Expect(TokenType type)
{
    if (Current.Type != type)
        throw new ParserError($"Expected {type}, got {Current.Type}", Current.Line, Current.Column);
    Advance();
}
```

**Usage Pattern:**
```csharp
Expect(TokenType.RightBracket);  // "Expected RightBracket, got Identifier at line 10, column 5"
```

**Error Handling**: Throws `ParserError` with current token's line/column for precise error location.

---

#### `ExpectIdentifier()` (Lines 363-370)

**Purpose**: Consume an identifier token and return its string value.

**Return Value**: The identifier name as a `string`.

**Error Example**: `"Expected identifier, got LeftParen at line 5, column 12"`

---

#### `ExpectIdentifierOrKeyword()` (Lines 376-385)

**Purpose**: Allow keywords to be used as identifiers in member access contexts.

**Use Case**: Enables syntax like `obj.type`, `obj.class`, `obj.property` where `type`, `class`, `property` are reserved keywords but valid as attribute names (Python behavior).

**Implementation:**
```csharp
private string ExpectIdentifierOrKeyword()
{
    if (Current.Type == TokenType.Identifier || IsKeywordToken(Current.Type))
    {
        var value = Current.Value;
        Advance();
        return value;
    }
    throw new ParserError($"Expected identifier, got {Current.Type}", Current.Line, Current.Column);
}
```

**Rationale**: Python allows `obj.class` even though `class` is a keyword. Sharpy maintains this flexibility for attribute access.

---

#### `IsKeywordToken()` (Lines 390-422)

**Purpose**: Determines if a token type represents a keyword that can be used as an identifier in member access.

**Covered Keywords** (46 total):
- Control flow: `def`, `class`, `struct`, `if`, `else`, `elif`, `for`, `while`, `return`, `break`, `continue`, `pass`, `try`, `except`, `finally`, `raise`, `assert`, `with`
- Type keywords: `auto`, `const`, `lambda`, `type`, `interface`, `enum`
- Pattern matching: `match`, `case`
- Async: `async`, `await`, `yield`
- Member keywords: `property`, `event`
- Import keywords: `import`, `from`, `as`
- Other: `del`, `to`, `maybe`, `super`, `defer`, `do`
- Boolean operators: `and`, `or`, `not`, `is`
- Literals: `True`, `False`, `None`

**Implementation Pattern (Lines 392-421):**
```csharp
return type switch
{
    TokenType.Def or TokenType.Class or TokenType.Struct or ... => true,
    _ => false
};
```

Uses C# 9.0 pattern matching with `or` combinators for concise readability.

---

#### `ExpectNewline()`, `ExpectStatementEnd()`, `SkipNewlines()` (Lines 424-448)

**Purpose**: Manage indentation-sensitive syntax (Python-style whitespace significance).

- **`ExpectNewline()`** (Lines 424-430): Require a newline token (used after simple statements)
  ```csharp
  if (Current.Type == TokenType.Newline)
      Advance();
  else if (!IsAtEnd)
      throw new ParserError($"Expected newline, got {Current.Type}", ...);
  ```

- **`ExpectStatementEnd()`** (Lines 432-442): Accept newline, dedent, or EOF (flexible for block endings)
  ```csharp
  if (Current.Type == TokenType.Newline)
      Advance();
  else if (Current.Type != TokenType.Dedent && !IsAtEnd)
      throw new ParserError($"Expected end of statement, got {Current.Type}", ...);
  ```

- **`SkipNewlines()`** (Lines 444-448): Consume consecutive newline tokens (used after block headers)
  ```csharp
  while (Current.Type == TokenType.Newline)
      Advance();
  ```

**Error Cases:**
- `ExpectNewline()`: "Expected newline, got LeftBrace"
- `ExpectStatementEnd()`: "Expected end of statement, got Identifier"

---

#### `IsTypeName()` (Lines 450-461)

**Purpose**: Heuristic check to determine if an identifier looks like a type name (used for disambiguation in some contexts).

**Logic:**
1. Check if it's a built-in type: `int`, `float`, `str`, `bool`, `list`, `dict`, `set`, `tuple`, `object`, `any`
2. Check if it starts with an uppercase letter (convention for user-defined types)

**Implementation:**
```csharp
private bool IsTypeName(string name)
{
    // Primitive types
    if (name is "int" or "float" or "str" or "bool" or "list" or "dict" or
               "set" or "tuple" or "object" or "any")
        return true;

    // User-defined types typically start with uppercase letter
    if (name.Length > 0 && char.IsUpper(name[0]))
        return true;

    return false;
}
```

**Use Case**: Helps distinguish between `x: list[int]` (type annotation) and `x = list[1, 2]` (expression) in ambiguous parsing contexts.

**Limitation**: This is a heuristic—it's not used in type annotation parsing itself (which has unambiguous syntax), but may be used in other parser files for statement-level disambiguation.

---

### F-String Parsing

#### `ParseSegmentedFString()` (Lines 467-525)

**Purpose**: Parse f-string literals with embedded expressions using the lexer's segmented token approach.

**Token Sequence:**
```
FStringStart → (FStringText | FStringExprStart Expression [FormatSpec] FStringExprEnd)* → FStringEnd
```

**Example**: `f"Hello {name:>10}!"` produces:
- FStringStart
- FStringText: `"Hello "`
- FStringExprStart
- [expression tokens for `name`]
- FStringFormatSpec: `">10"`
- FStringExprEnd
- FStringText: `"!"`
- FStringEnd

**Algorithm:**
1. Consume `FStringStart`
2. Loop until `FStringEnd`:
   - If `FStringText`: Add text part to AST
   - If `FStringExprStart`: Parse expression, check for optional format spec, add expression part
3. Build `FStringLiteral` node with array of `FStringPart` objects

**Implementation (Lines 474-507):**
```csharp
while (Current.Type != TokenType.FStringEnd && Current.Type != TokenType.Eof)
{
    if (Current.Type == TokenType.FStringText)
    {
        // Text segment
        parts.Add(new FStringPart { Text = Current.Value, Expression = null });
        Advance();
    }
    else if (Current.Type == TokenType.FStringExprStart)
    {
        // Expression segment
        Advance(); // Skip FStringExprStart

        var expr = ParseExpression();  // Recursive call to expression parser

        // Check for optional format spec
        string? formatSpec = null;
        if (Current.Type == TokenType.FStringFormatSpec)
        {
            formatSpec = Current.Value;
            Advance();
        }

        parts.Add(new FStringPart { Text = null, Expression = expr, FormatSpec = formatSpec });
        Expect(TokenType.FStringExprEnd);
    }
    else
    {
        throw new ParserError($"Unexpected token in f-string: {Current.Type}", ...);
    }
}
```

**FStringPart Structure:**
```csharp
public record FStringPart {
    public string? Text;           // Non-null for text segments
    public Expression? Expression; // Non-null for expression segments
    public string? FormatSpec;     // Optional format spec like ".2f" or ">10"
}
```

**Format Spec Examples:**
- `{value:.2f}` → FormatSpec = `".2f"` (2 decimal places)
- `{name:>10}` → FormatSpec = `">10"` (right-align in 10 chars)
- `{count}` → FormatSpec = `null` (no formatting)

**Error Handling**: Throws `ParserError` if unexpected token appears inside f-string (should not happen with correct lexer implementation).

**Design Note**: This method is somewhat out of place in a file focused on type annotations. It's here likely for organizational reasons or because it shares utility methods with other parsing logic.

---

#### `Advance()` (Line 319)

**Purpose**: Move to the next token in the stream.

**Implementation**: Simply increments `_position` counter.

```csharp
private void Advance() => _position++;
```

**Note**: This is defined in `Parser.Types.cs` even though it's used throughout all partial class files. This is a quirk of C# partial classes—the method can be defined in any file and is accessible from all parts.

---

## Dependencies

### Internal Dependencies

**From Sharpy.Compiler.Lexer:**
- `Token`: Represents lexical tokens with type, value, and position
- `TokenType`: Enum of all token types (Keywords, Operators, Literals, etc.)

**From Sharpy.Compiler.Parser.Ast:**
- `TypeAnnotation`: AST node for type annotations (record type with immutable properties)
- `FStringLiteral`: AST node for f-string expressions
- `FStringPart`: Part of an f-string (text or expression)
- `Expression`: Base type for all expression AST nodes

**From Sharpy.Compiler.Logging:**
- `ICompilerLogger`: Interface for diagnostic logging

**From Sharpy.Compiler.Text:**
- `TextSpan`: Represents source code location (start/end positions)

### External Dependencies

- `System.Collections.Immutable`: All AST nodes use `ImmutableArray` for child collections (enforces immutability)
- `System.Text`: Used for `StringBuilder` in some contexts (though not in this specific file)

---

## Patterns and Design Decisions

### 1. **Recursive Descent Parsing**

Type annotations are parsed recursively, allowing unlimited nesting:
- `list[dict[str, list[int]]]` works because `ParseTypeAnnotation()` calls itself for generic arguments

**Example Recursion Tree:**
```
ParseTypeAnnotation()
  ├─ ParseStandardTypeAnnotation("list")
  │   └─ ParseTypeAnnotation()  [for generic arg]
  │       └─ ParseStandardTypeAnnotation("dict")
  │           ├─ ParseTypeAnnotation()  [for key type]
  │           │   └─ ParseStandardTypeAnnotation("str")
  │           └─ ParseTypeAnnotation()  [for value type]
  │               └─ ParseListTypeShorthand()
  │                   └─ ParseTypeAnnotation()
  │                       └─ ParseStandardTypeAnnotation("int")
```

### 2. **Immutable AST Nodes**

All `TypeAnnotation` nodes use C# `record` types with `init`-only properties:
```csharp
public record TypeAnnotation {
    public string Name { get; init; } = "";
    public ImmutableArray<TypeAnnotation> TypeArguments { get; init; } = ...;
    public bool IsNullable { get; init; }
}
```

**Rationale**: Immutability prevents accidental AST mutations. Semantic analysis adds annotations in a separate `SemanticInfo` structure, never by modifying the AST.

**C# `with` Expression**: Used to create modified copies (Line 75-81):
```csharp
baseType = baseType with {
    IsNullable = true,
    LineEnd = endLine,
    ColumnEnd = endColumn,
    Span = GetSpanFromTokens(startToken, endToken)
};
```

### 3. **Syntactic Sugar Resolution**

Python shorthands are **desugared at parse time** into standard forms:
- `[str]` → `TypeAnnotation { Name = "list", TypeArguments = [str] }`
- `{int}` → `TypeAnnotation { Name = "set", TypeArguments = [int] }`
- `{str: int}` → `TypeAnnotation { Name = "dict", TypeArguments = [str, int] }`

**Benefit**: Later compiler stages (semantic analysis, code generation) never need to handle shorthand forms—they only see normalized `TypeAnnotation` nodes.

**Trade-off**: Loses source-level information about which syntax was used (can't reconstruct original shorthand in error messages). This is acceptable because the shorthands are perfect equivalents.

### 4. **Lookahead for Disambiguation**

The parser uses `Peek()` to distinguish similar syntax:
- `[T]` (list type) vs `[]` (array suffix) — checks if `Peek()` is `RightBracket` immediately
- `{T}` (set type) vs `{T: V}` (dict type) — checks for `:` after first type
- `(T, U)` (tuple type) vs `(T) -> U` (function type) — checks for `->` after closing paren

**Technique**: This is a **predictive parser** that uses bounded lookahead (usually 1 token) to make parsing decisions without backtracking.

**LL(1) Grammar**: Most of the type grammar is LL(1), meaning one token of lookahead is sufficient for deterministic parsing.

### 5. **Position Tracking for Diagnostics**

Every `TypeAnnotation` node includes:
- `LineStart`, `ColumnStart`, `LineEnd`, `ColumnEnd`: Human-readable positions
- `Span`: `TextSpan?` for IDE integration (hover, go-to-definition, etc.)

**Example Error Message:**
```
Error at line 10, column 15: List type shorthand requires an element type: [T]
```

The position fields enable precise error reporting pointing to the exact source location.

**Usage in IDE**: The `Span` field is used by language services to:
- Highlight the entire type annotation on hover
- Provide "go to definition" for type names
- Show squiggly underlines for type errors

### 6. **Error Recovery Strategy**

This file uses **panic-mode error recovery**: when an error occurs, a `ParserError` exception is thrown with line/column information.

**No Recovery Implemented**: Unlike some parsers that try to synchronize and continue after errors, Sharpy's parser stops at the first error. This simplifies implementation but means only one error is reported per parse.

**Future Enhancement**: Could implement error recovery to report multiple syntax errors in one pass (similar to Roslyn's error recovery).

### 7. **Partial Class Organization**

The `Parser` class is split across 6 files by responsibility:
- **Types.cs** (this file): Type annotations + utilities
- **Expressions.cs**: Binary/unary operators, precedence climbing
- **Primaries.cs**: Literals, identifiers, comprehensions
- **Statements.cs**: Control flow statements
- **Definitions.cs**: Functions, classes, structs, enums
- **Parser.cs**: Core infrastructure

**Rationale**: Each file is ~300-600 lines, keeping them readable. Related functionality is co-located (e.g., all type parsing in one file).

**C# Partial Class Mechanics**: All methods and fields are shared across files. The compiler merges them into a single class at compile time.

---

## Debugging Tips

### 1. **Trace Token Consumption**

Add logging to see how the parser consumes tokens:
```csharp
// At the start of ParseTypeAnnotation()
_logger.LogDebug($"Parsing type at position {_position}, current token: {Current.Type} '{Current.Value}'");
```

### 2. **Check Token Lookahead**

When debugging disambiguation issues (e.g., tuple vs function type), print `Peek()` values:
```csharp
_logger.LogDebug($"Current: {Current.Type}, Peek(1): {Peek(1).Type}, Peek(2): {Peek(2).Type}");
```

### 3. **Visualize AST Nodes**

Use the CLI's `emit ast` command to inspect parsed type annotations:
```bash
dotnet run --project src/Sharpy.Cli -- emit ast file.spy
```

This shows the full AST structure, including type annotations in function signatures, variable declarations, etc.

### 4. **Test Shorthand Equivalence**

Verify that shorthands produce identical ASTs:
```python
# file1.spy
x: list[int]

# file2.spy
x: [int]
```

Both should produce: `TypeAnnotation { Name = "list", TypeArguments = [TypeAnnotation { Name = "int" }] }`

Compare outputs using:
```bash
dotnet run --project src/Sharpy.Cli -- emit ast file1.spy > out1.txt
dotnet run --project src/Sharpy.Cli -- emit ast file2.spy > out2.txt
diff out1.txt out2.txt  # Should be identical
```

### 5. **Parser Error Location**

When a `ParserError` is thrown, check:
- `Current.Line` and `Current.Column`: Where the error occurred
- `Current.Type` and `Current.Value`: What token caused the issue
- The error message: What was expected

**Example**: "Expected RightBracket, got Identifier at line 5, column 12"
→ Likely a missing `]` in a generic type annotation

### 6. **Null Span Debugging**

If `Span` is `null` in AST nodes, check that:
- Lexer is populating `Token.Position` and `Token.Length` fields
- `GetSpanFromTokens()` is being called correctly
- The token's `GetSpan()` method returns a valid `TextSpan`

Add diagnostics:
```csharp
var span = GetSpanFromTokens(startToken, endToken);
if (span == null)
    _logger.LogWarning($"Null span for type annotation at line {startLine}");
```

### 7. **F-String Segment Boundaries**

When debugging f-string parsing, verify the lexer's token sequence:
```csharp
// Before parsing f-string
for (int i = _position; i < _tokens.Count && i < _position + 20; i++) {
    _logger.LogDebug($"[{i}] {_tokens[i].Type}: '{_tokens[i].Value}'");
}
```

Ensure the lexer properly emits `FStringExprStart`/`FStringExprEnd` pairs around expressions.

### 8. **Recursion Depth Issues**

If you encounter stack overflow with deeply nested types:
```python
# Pathological case
x: list[list[list[list[list[list[list[list[list[int]]]]]]]]]
```

Consider adding a recursion depth counter:
```csharp
private int _typeDepth = 0;
private const int MaxTypeDepth = 100;

private TypeAnnotation ParseTypeAnnotation()
{
    if (_typeDepth++ > MaxTypeDepth)
        throw new ParserError("Type nesting too deep", Current.Line, Current.Column);
    try {
        // ... existing code ...
    } finally {
        _typeDepth--;
    }
}
```

---

## Contribution Guidelines

### When to Modify This File

1. **Adding New Type Syntax**: If the language adds new type annotation forms (e.g., union types `T | U`), add parsing logic here
2. **New Shorthand Forms**: Implement shorthand desugaring in new methods like `ParseUnionTypeShorthand()`
3. **Fixing Type Parsing Bugs**: Issues with generic arguments, nullable types, or shorthands
4. **Improving Error Messages**: Enhance `ParserError` messages for better developer experience
5. **Position Tracking Fixes**: If AST nodes are missing source location information

### When NOT to Modify This File

1. **Type Checking**: Semantic validation happens in `Sharpy.Compiler/Semantic/TypeChecker.cs`, not here
2. **Type Resolution**: Resolving type names to actual types is in `Sharpy.Compiler/Semantic/TypeResolver.cs`
3. **Expression Parsing**: Add to `Parser.Expressions.cs` or `Parser.Primaries.cs` instead
4. **Code Generation**: Type annotation → C# code happens in `RoslynEmitter`, not in the parser

### Code Quality Rules

1. **Never Modify AST Nodes**: Parser creates immutable AST nodes. Never add mutable state.
2. **Use Record Pattern**: If adding new AST node types, follow the record pattern used by `TypeAnnotation`
3. **Maintain Position Tracking**: Always set `LineStart`, `ColumnStart`, `LineEnd`, `ColumnEnd`, and `Span`
4. **Comprehensive Error Messages**: Include token type, expected type, and position in all errors
5. **Test Both Forms**: When adding shorthand syntax, verify it produces the same AST as the long form

### Testing New Type Syntax

1. **Unit Tests**: Add to `Sharpy.Compiler.Tests/Parser/ParserTests.cs`
2. **File-Based Tests**: Create `.spy` + `.expected` pairs in `src/Sharpy.Compiler.Tests/Integration/TestFixtures/`
3. **Error Cases**: Test invalid syntax with `.spy` + `.error` pairs
4. **Roundtrip Testing**: Verify `emit ast` → semantic analysis → `emit csharp` produces correct C# code

### Example: Adding Union Types (`T | U`)

If adding union type syntax:

1. **Update TokenType**: Add `TokenType.Pipe` to lexer (if not already present)

2. **Add Parsing Method**:
   ```csharp
   private TypeAnnotation ParseUnionType(int startLine, int startColumn, Token startToken)
   {
       var types = new List<TypeAnnotation> { ParsePrimaryType() };
       while (Current.Type == TokenType.Pipe) {
           Advance();
           types.Add(ParsePrimaryType());
       }
       if (types.Count == 1) return types[0];

       var endToken = Previous;
       return new TypeAnnotation {
           Name = "union",
           TypeArguments = types.ToImmutableArray(),
           IsNullable = false,
           LineStart = startLine,
           ColumnStart = startColumn,
           LineEnd = endToken.Line,
           ColumnEnd = endToken.Column + endToken.Value.Length,
           Span = GetSpanFromTokens(startToken, endToken)
       };
   }
   ```

3. **Integrate into `ParseTypeAnnotation()`**: Call before nullable check

4. **Update Semantic Analyzer**: Teach `TypeChecker` how to validate union types

5. **Update Code Generator**: Emit appropriate C# code (possibly `object` with runtime checks or C# discriminated unions)

6. **Add Tests**: Both unit tests and file-based integration tests
   ```python
   # test_union_types.spy
   def process(value: int | str) -> str:
       return str(value)
   ```

---

## Cross-References

### Related Parser Files (Partial Classes)

- **[Parser.cs](Parser.md)**: Core parser infrastructure, module parsing, token navigation properties
- **[Parser.Expressions.cs](Parser.Expressions.md)**: Expression parsing with operator precedence
- **[Parser.Primaries.cs](Parser.Primaries.md)**: Primary expressions (literals, identifiers, comprehensions, lambdas)
- **[Parser.Statements.cs](Parser.Statements.md)**: Statement parsing (if, while, for, try, with, etc.)
- **[Parser.Definitions.cs](Parser.Definitions.md)**: Definition parsing (functions, classes, structs, interfaces, enums)

### Related AST Files

- **Parser/Ast/Types.cs**: `TypeAnnotation` record definition
- **Parser/Ast/Expression.cs**: `FStringLiteral` and `FStringPart` records

### Semantic Analysis (Downstream)

- **Semantic/TypeResolver.cs**: Resolves type names to actual types using `ModuleRegistry`
- **Semantic/TypeChecker.cs**: Validates type annotations and performs type inference
- **Semantic/ValidationPipeline.cs**: Runs semantic validators on the AST

### Code Generation (Downstream)

- **CodeGen/RoslynEmitter.cs**: Converts type annotations to Roslyn `TypeSyntax` nodes
- **CodeGen/RoslynEmitter.Expressions.cs**: Emits f-string expressions as C# string interpolation or `string.Format()`

### Language Specification

- **docs/language_specification/type_annotations.md**: Authoritative spec for type annotation syntax
- **docs/language_specification/type_casting.md**: Type casting and conversion rules
- **docs/language_specification/type_hierarchy.md**: Type system hierarchy and relationships
- **docs/language_specification/type_narrowing.md**: Type narrowing in conditional contexts

### Testing

- **src/Sharpy.Compiler.Tests/Parser/ParserTests.cs**: Unit tests for parser, including type annotation tests
- **src/Sharpy.Compiler.Tests/Integration/TestFixtures/**: File-based integration tests with `.spy` files

---

## Summary

`Parser.Types.cs` is the **type annotation parsing** module of the Sharpy compiler. It:

1. **Parses all type annotation syntax**: Standard forms (`list[T]`), Python shorthands (`[T]`, `{K: V}`), and special types (`auto`, `None`)
2. **Handles complex type constructs**: Nested generics, function types, tuple types, array types, nullable types
3. **Desugars syntax at parse time**: Converts shorthands to normalized AST nodes for downstream processing
4. **Provides parser utilities**: Token navigation, validation, error handling, position tracking
5. **Parses f-strings**: Handles segmented f-string tokens from the lexer with embedded expressions and format specs

The file follows **immutable AST** design, uses **recursive descent parsing** for nesting, and maintains **precise source location tracking** for error reporting. It's one of six partial class files that together implement the full Sharpy parser.

**Key Insight**: By resolving all syntactic sugar at parse time (`[T]` → `list[T]`), the parser simplifies all downstream compiler stages, which only need to handle the canonical type annotation form. This is a classic compiler design pattern: **normalize early, process uniformly**.

For newcomers:
- Start by reading `ParseTypeAnnotation()` (line 14) to understand the dispatch logic
- Study one shorthand parser (`ParseListTypeShorthand`, line 149) to see the pattern
- Trace a complex type like `dict[str, list[int]]?` through the recursive calls
- Use `dotnet run --project src/Sharpy.Cli -- emit ast file.spy` to visualize parsed types

This file is a great example of recursive descent parsing done right—readable, maintainable, and efficient.
