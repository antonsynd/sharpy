# Walkthrough: Parser.Types.cs

**Source File**: `src/Sharpy.Compiler/Parser/Parser.Types.cs`

---

## Overview

`Parser.Types.cs` is a partial class file that implements **type annotation parsing** for the Sharpy compiler. This file handles the conversion of type syntax in Sharpy source code (like `int`, `list[str]`, `{str: int}`, `(int, str) -> bool`) into AST nodes (`TypeAnnotation` records).

**Role in Compiler Pipeline:**
- **Input**: Token stream from Lexer containing type-related tokens (identifiers, brackets, keywords)
- **Output**: `TypeAnnotation` AST nodes that represent type information
- **Position**: Part of the Parser's syntactic analysis phase, specifically handling type annotations in variable declarations, function signatures, class fields, etc.

This file is responsible for understanding Sharpy's rich type syntax, including:
- Standard types with generics: `list[int]`, `dict[str, float]`
- Shorthand syntax: `[int]` for lists, `{str}` for sets, `{str: int}` for dicts
- Nullable types: `int?`
- Array types: `int[]`
- Tuple types: `(int, str, bool)`
- Function types: `(int, str) -> bool`
- Type inference: `auto`

---

## Class/Type Structure

This file extends the `partial class Parser` defined in `Parser.cs`. The `Parser` class maintains:

**Key Fields (from Parser.cs):**
- `_tokens`: List of tokens from the Lexer
- `_position`: Current position in token stream
- `_logger`: Compiler logger for diagnostics

**Key Properties:**
- `Current`: Token at current position
- `Peek(offset)`: Look-ahead at tokens without advancing
- `IsAtEnd`: Check if we've reached EOF

**Output Type:**
- `TypeAnnotation` (from `Ast/Types.cs`): A record with:
  - `Name`: Type name (e.g., "int", "list", "dict", "tuple", "function")
  - `TypeArguments`: Generic type parameters
  - `IsNullable`: Whether type ends with `?`
  - Location info: `LineStart`, `ColumnStart`, `LineEnd`, `ColumnEnd`

---

## Key Functions/Methods

### 1. `ParseTypeAnnotation()` (Main Entry Point)

**Purpose**: Top-level method for parsing any type annotation. Dispatches to specialized parsers based on the leading token.

**Algorithm:**
```
1. Check for shorthand forms based on leading token:
   - '[' → List shorthand: [T]
   - '{' → Set/Dict shorthand: {T} or {K: V}
   - '(' → Tuple/Function: (T, U) or (T) -> U
   - Otherwise → Standard type: identifier with optional generics

2. Parse array suffix: T[] (can be chained: T[][])
   - Creates wrapper TypeAnnotation with name="array"

3. Parse nullable suffix: T?
   - Sets IsNullable=true on the type
```

**Key Design Decision**: The method handles **suffixes iteratively** after parsing the base type. This allows combinations like `list[int][]?` (nullable array of int lists).

**Example Flow:**
- Input: `list[str]?`
- Base: `ParseStandardTypeAnnotation` → `{Name="list", TypeArgs=[{Name="str"}]}`
- Suffix: `?` → Set `IsNullable=true`
- Output: `{Name="list", TypeArgs=[{Name="str"}], IsNullable=true}`

---

### 2. `ParseStandardTypeAnnotation(startLine, startColumn)`

**Purpose**: Parses standard type notation with optional generic arguments.

**Handles:**
- Keywords: `auto` (type inference), `None` (for `-> None` return types)
- Identifiers: `int`, `str`, `MyClass`
- Generics: `list[T]`, `dict[K, V]`

**Algorithm:**
```csharp
1. Parse type name (identifier, 'auto', or 'None')
2. If followed by '[' and not '[]':
   - Parse comma-separated type arguments recursively
   - Each arg is a full ParseTypeAnnotation() call (allows nesting)
3. Return TypeAnnotation with name and type arguments
```

**Important Note**: Generic brackets `[T]` are distinguished from array brackets `[]` by checking if the closing bracket is immediate.

**Example:**
- `dict[str, list[int]]` → `{Name="dict", TypeArgs=[{Name="str"}, {Name="list", TypeArgs=[{Name="int"}]}]}`

---

### 3. `ParseListTypeShorthand(startLine, startColumn)`

**Purpose**: Parses `[T]` syntax as syntactic sugar for `list[T]`.

**Algorithm:**
```
1. Consume '['
2. Check for empty brackets → Error ("List type shorthand requires an element type")
3. ParseTypeAnnotation() for element type (recursive, allows [list[int]])
4. Expect ']'
5. Return TypeAnnotation with Name="list"
```

**Example:**
- `[str]` → Equivalent to `list[str]`
- `[[int]]` → Equivalent to `list[list[int]]`

---

### 4. `ParseSetOrDictTypeShorthand(startLine, startColumn)`

**Purpose**: Parses `{T}` (set) or `{K: V}` (dict) shorthand syntax.

**Disambiguation Logic:**
```
1. Consume '{'
2. Parse first type
3. Check next token:
   - ':' → Dict: Parse value type, return {Name="dict", TypeArgs=[K, V]}
   - '}' → Set: Return {Name="set", TypeArgs=[T]}
```

**Key Insight**: The presence of `:` after the first type determines whether it's a set or dict. This is a simple but effective grammar design.

**Examples:**
- `{str}` → `{Name="set", TypeArgs=[{Name="str"}]}`
- `{str: int}` → `{Name="dict", TypeArgs=[{Name="str"}, {Name="int"}]}`

---

### 5. `ParseTupleOrFunctionTypeShorthand(startLine, startColumn)`

**Purpose**: Parses `()`, `(T)`, `(T, U)` (tuples) or `(T, U) -> R` (function types).

**Disambiguation Logic:**
```
1. Consume '('
2. Parse comma-separated type list (may be empty)
3. Consume ')'
4. Check next token:
   - '->' → Function type: Parse return type, return {Name="function", TypeArgs=[params..., returnType]}
   - Otherwise → Tuple: return {Name="tuple", TypeArgs=[types...]}
```

**Function Type Encoding**: For function types, the last element in `TypeArguments` is the return type, and all preceding elements are parameter types.

**Examples:**
- `()` → `{Name="tuple", TypeArgs=[]}`
- `(int, str)` → `{Name="tuple", TypeArgs=[{Name="int"}, {Name="str"}]}`
- `(int, str) -> bool` → `{Name="function", TypeArgs=[{Name="int"}, {Name="str"}, {Name="bool"}]}`

**Trailing Comma Handling**: The code tracks `hasTrailingComma` but doesn't currently use it for tuple vs. single-element distinction (both `(T)` and `(T,)` are treated as tuples).

---

### 6. `ParseSegmentedFString(startLine, startColumn)`

**Purpose**: Parses f-string literals with embedded expressions.

**Token Flow:**
```
FStringStart → (FStringText | FStringExprStart Expression [FormatSpec] FStringExprEnd)* → FStringEnd
```

**Algorithm:**
```
1. Consume FStringStart
2. Loop until FStringEnd:
   - FStringText → Add text part
   - FStringExprStart → Parse expression, optional format spec, expect FStringExprEnd
3. Consume FStringEnd
4. Return FStringLiteral with parts list
```

**Design Note**: This method seems somewhat out of place in a file focused on type annotations. It handles expression parsing (f-strings) rather than type parsing. It's likely here for organizational reasons or because it shares utility methods with type parsing.

---

## Helper/Utility Methods

### Token Navigation
- `Advance()`: Move to next token (`_position++`)
- `Peek(offset)`: Look ahead without consuming
- `Current`: Property for current token
- `IsAtEnd`: Check for EOF

### Expectation Helpers
- `Expect(TokenType)`: Consume expected token or throw error
- `ExpectIdentifier()`: Consume identifier token, return its value
- `ExpectIdentifierOrKeyword()`: Accept identifiers OR keywords (for member access like `obj.type`)
- `ExpectNewline()`: Consume newline or check for EOF
- `ExpectStatementEnd()`: Handle statement terminators (newline, dedent, EOF)

### Utility Methods
- `SkipNewlines()`: Consume consecutive newlines
- `IsKeywordToken(TokenType)`: Check if token is a keyword that can be used as identifier
- `IsTypeName(string)`: Heuristic to determine if a name looks like a type (primitive or starts with uppercase)

---

## Dependencies

**Internal Dependencies:**
- `Sharpy.Compiler.Lexer`: Token and TokenType definitions
- `Sharpy.Compiler.Parser.Ast`: TypeAnnotation, FStringLiteral, FStringPart AST nodes
- `Sharpy.Compiler.Logging`: ICompilerLogger for diagnostics

**Upstream Dependency:**
- Lexer must tokenize type syntax correctly, including:
  - Generic brackets `[`, `]`
  - Braces `{`, `}`
  - Parentheses `(`, `)`
  - Arrow `->` for function types
  - Question mark `?` for nullable types

**Downstream Usage:**
- `Parser.Definitions.cs`: Parses function parameters, class fields (uses `ParseTypeAnnotation()`)
- `Parser.Statements.cs`: Parses variable declarations with type hints
- Type annotations flow into semantic analysis for type checking

---

## Patterns and Design Decisions

### 1. **Recursive Descent Parsing**
All type parsing methods follow the recursive descent pattern:
- Each method handles one syntactic construct
- Recursive calls for nested structures (e.g., `list[dict[str, int]]`)
- Clear base cases (primitives) and recursive cases (generics)

### 2. **Lookahead for Disambiguation**
The parser uses single-token lookahead to distinguish:
- `[T]` (list) vs `[]` (array suffix)
- `{T}` (set) vs `{K: V}` (dict)
- `(T, U)` (tuple) vs `(T) -> U` (function)

This makes the grammar LL(1)-friendly and efficient.

### 3. **Normalization to Canonical Form**
Shorthand syntax is normalized to standard form in the AST:
- `[T]` → `TypeAnnotation{Name="list", TypeArgs=[T]}`
- `{T}` → `TypeAnnotation{Name="set", TypeArgs=[T]}`
- `T[]` → `TypeAnnotation{Name="array", TypeArgs=[T]}`

This simplifies downstream processing—semantic analysis doesn't need to know about shorthand syntax.

### 4. **Suffix Processing via Iteration**
Array and nullable suffixes are handled in a loop after base type parsing:
```csharp
while (Current.Type == TokenType.LeftBracket && Peek().Type == TokenType.RightBracket)
    // Wrap in array type
if (Current.Type == TokenType.Question)
    // Mark nullable
```

This enables chaining: `int[][]?` → nullable array of arrays of int.

### 5. **Position Tracking**
Every `TypeAnnotation` includes source location (line/column start/end). This enables:
- Precise error messages
- IDE features (go-to-definition, hover tooltips)
- Source maps for debugging

### 6. **Error Handling**
Errors are thrown immediately via `ParserError` with location info:
```csharp
throw new ParserError("List type shorthand requires an element type: [T]", Current.Line, Current.Column);
```

No error recovery—parsing stops at first error (fail-fast approach).

---

## Debugging Tips

### 1. **Token Stream Inspection**
If type parsing fails unexpectedly:
- Log `Current.Type` and `Current.Value` before each parsing decision
- Check if Lexer is correctly tokenizing type syntax (especially `->`, `?`, `[]`)

### 2. **Lookahead Confusion**
Common bug: Confusing `Peek()` with `Peek(1)`:
- `Peek()` defaults to offset=1 (next token)
- `Peek(0)` is same as `Current`
- `Peek(-1)` is same as `Previous`

### 3. **Recursion Depth**
Deeply nested types (e.g., `list[dict[str, set[tuple[int, str]]]]`) can cause stack overflow if recursive limit isn't set. Currently, there's no depth limit—consider adding one.

### 4. **Array vs. Generic Bracket Ambiguity**
The check `Current.Type == TokenType.LeftBracket && Peek().Type == TokenType.RightBracket` distinguishes `[]` (array) from `[T]` (generic).

If this fails, check:
- Is the Lexer emitting `RightBracket` immediately after `LeftBracket`?
- Are there spurious tokens (whitespace, comments) between `[` and `]`?

### 5. **F-String Placement**
`ParseSegmentedFString()` is in this file but isn't type-related. If you're debugging f-strings, you're in the right place, but don't expect type-parsing logic.

---

## Contribution Guidelines

### Types of Changes You Might Make

1. **Adding New Type Syntax**
   - Example: Union types `int | str`, intersection types `A & B`
   - Add new shorthand forms or extend existing ones
   - Update `ParseTypeAnnotation()` to dispatch to new parsers
   - Add tests for new syntax

2. **Improving Error Messages**
   - Replace generic "Expected X" with context-specific hints
   - Example: "Did you mean 'list[int]' instead of '[int'?"

3. **Optimizations**
   - Cache frequently parsed types (e.g., `int`, `str`)
   - Reduce allocations in hot paths (e.g., `TypeArguments` list)

4. **Better Nullability Handling**
   - Currently `T?` sets `IsNullable=true`, but semantic analysis still needed
   - Consider nullable context flow (like C# nullable reference types)

5. **Type Alias Support**
   - If Sharpy adds type aliases (`type IntList = list[int]`), expand `IsTypeName()` to check alias table

### Testing Considerations

When modifying this file:
- Add tests for new type syntax in `test/Sharpy.Compiler.Tests/Parser/TypeParsingTests.cs` (or similar)
- Test edge cases: empty tuples `()`, nested generics `dict[str, list[int]]`, combined suffixes `int[]?`
- Ensure error messages are clear and actionable

### Consistency with Language Spec

Always cross-reference with:
- `docs/language_specification/type_annotations.md`: Official syntax definition
- `docs/language_specification/type_casting.md`: Interaction with casting
- `docs/language_specification/type_hierarchy.md`: Subtyping rules (affects nullable types)

---

## Cross-References

### Related Partial Class Files

This file is part of the `partial class Parser` split across multiple files:

- **[Parser.cs](./Parser.md)**: Main parser class, module parsing, statement dispatching
- **[Parser.Definitions.cs](./Parser.Definitions.md)**: Function, class, struct, enum definitions (uses `ParseTypeAnnotation()` for parameters and fields)
- **[Parser.Expressions.cs](./Parser.Expressions.md)**: Expression parsing (binary ops, calls, indexing)
- **Parser.Primaries.cs**: Primary expressions (literals, identifiers, parenthesized)
- **Parser.Statements.cs**: Statement parsing (if, while, for, return, etc.)

### AST Nodes Produced

- `TypeAnnotation` (defined in `src/Sharpy.Compiler/Parser/Ast/Types.cs`):
  - Used in function signatures, variable declarations, class fields, generic constraints
  - Consumed by semantic analysis for type checking

### Where Type Annotations Are Used

1. **Function Definitions** (`Parser.Definitions.cs`):
   ```python
   def add(x: int, y: int) -> int:
       return x + y
   ```

2. **Variable Declarations**:
   ```python
   name: str = "Alice"
   numbers: list[int] = [1, 2, 3]
   ```

3. **Class Fields**:
   ```python
   class User:
       id: int
       email: str?
   ```

4. **Generic Type Parameters**:
   ```python
   class Stack[T]:
       items: list[T]
   ```

---

## Summary

`Parser.Types.cs` is a **focused, well-structured module** that handles all type annotation parsing in Sharpy. It demonstrates:

- **Clean separation of concerns**: Type parsing isolated from statement/expression parsing
- **Elegant disambiguation**: Uses lookahead to distinguish similar syntax (`[T]` vs `[]`, `{T}` vs `{K: V}`)
- **Normalization**: Converts shorthand syntax to canonical AST form
- **Extensibility**: Easy to add new type syntax by following existing patterns

For newcomers:
- Start by reading `ParseTypeAnnotation()` to understand the dispatch logic
- Study one shorthand parser (`ParseListTypeShorthand`) to see the pattern
- Trace a complex type like `dict[str, list[int]]?` through the recursive calls

This file is a great example of recursive descent parsing done right—readable, maintainable, and efficient.
