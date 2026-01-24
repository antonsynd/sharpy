# Walkthrough: Parser.Expressions.cs

**Source File**: `src/Sharpy.Compiler/Parser/Parser.Expressions.cs`

---

## Overview

`Parser.Expressions.cs` is a partial class file implementing the **expression parsing** logic for the Sharpy compiler. This file uses **recursive descent parsing with operator precedence climbing** to transform a token stream from the Lexer into a structured Abstract Syntax Tree (AST) of expression nodes.

**Role in Pipeline:**
```
Source (.spy) → Lexer (tokens) → [Parser.Expressions] → AST → Semantic Analysis
```

This file handles all expression parsing, including:
- Binary operators (arithmetic, logical, bitwise, comparison)
- Unary operators (+, -, not, ~)
- Postfix operations (member access, indexing, function calls, type casts)
- Special expressions (walrus `:=`, try/maybe, conditional ternary)
- Comparison chains (`a < b < c`)
- Operator precedence enforcement

---

## Class/Type Structure

### Partial Class: `Parser`

This file is part of the `Parser` partial class, which is split across multiple files:
- **Parser.cs**: Core parser infrastructure and statement parsing entry
- **Parser.Expressions.cs**: Expression parsing (this file)
- **Parser.Primaries.cs**: Primary expressions (literals, identifiers, comprehensions)
- **Parser.Statements.cs**: Statement parsing (if, while, for, etc.)
- **Parser.Definitions.cs**: Definition parsing (functions, classes, structs)
- **Parser.Types.cs**: Type annotation parsing and utility methods

The parser maintains three key fields (from `Parser.cs`):
```csharp
private readonly List<Token> _tokens;  // Input token stream
private int _position;                 // Current position in token stream
private readonly ICompilerLogger _logger;
```

---

## Key Functions/Methods

Expression parsing follows the **precedence hierarchy** defined in `docs/language_specification/operator_precedence.md`. Each parsing method handles operators at a specific precedence level and delegates to the next higher precedence level for its operands.

### Entry Point

#### `ParseExpression()` (Line 14)
```csharp
private Expression ParseExpression() => ParseWalrusExpression();
```
The main entry point for expression parsing. Delegates to the lowest-precedence operator (walrus).

---

### Operator Precedence Ladder (Lowest to Highest)

The parsing methods are organized by precedence, from lowest (binds weakest) to highest (binds strongest):

#### 1. **ParseWalrusExpression()** (Lines 16-43) — Precedence 20
**Operator**: `:=` (walrus/assignment expression)
- **Associativity**: Right-to-left
- **Pattern**: `identifier := value`
- **AST Node**: `WalrusExpression`

**Key Logic:**
```csharp
if (Current.Type == TokenType.Identifier && Peek().Type == TokenType.ColonAssign)
{
    var name = Current.Value;
    Advance();  // Skip identifier
    Advance();  // Skip :=
    var value = ParseWalrusExpression();  // Right-associative recursion!
    return new WalrusExpression { Target = name, Value = value, ... };
}
```

**Important**: Uses right-associative recursion (calls itself for the right operand) to support chains like `a := b := 5`.

---

#### 2. **ParseTryMaybeExpression()** (Lines 45-104) — Precedence 17
**Operators**: `try`, `maybe` (Result/Optional wrapping)
- **Associativity**: Prefix operators (right-to-left)
- **AST Nodes**: `TryExpression`, `MaybeExpression`

**Try Expression:**
```python
try some_func(4) + 5           # Wraps entire expression in Result[T, E]
try[ValueError] parse_int(s)   # Specifies exception type
```

**Maybe Expression:**
```python
maybe get_value() + default    # Wraps nullable expression in Optional[T]
```

**Key Implementation Detail:**
Both `try` and `maybe` call `ParseConditionalOperand()` instead of continuing down the precedence chain. This is intentional — they capture everything **except** conditional expressions (`x if c else y`):

```csharp
var operand = ParseConditionalOperand();  // Skips conditionals!
```

This matches the spec: `try foo() if cond else bar()` is parsed as `(try foo()) if cond else bar()`, not `try (foo() if cond else bar())`.

---

#### 3. **ParseConditionalExpression()** (Lines 118-144) — Precedence 18
**Operator**: `if ... else` (ternary conditional)
- **Syntax**: `value if test else other`
- **Associativity**: Right-to-left
- **AST Node**: `ConditionalExpression`

**Unique Syntax**: Python's conditional expression has an unusual order — the "then" value comes **before** the test:
```python
x if condition else y  # Not "if condition then x else y"
```

**Implementation:**
```csharp
var expr = ParseNullCoalesce();  // Parse the "then" value first
if (Current.Type == TokenType.If)
{
    Advance();
    var test = ParseNullCoalesce();
    Expect(TokenType.Else);
    var elseValue = ParseConditionalExpression();  // Right-associative!
    return new ConditionalExpression { Test = test, ThenValue = expr, ElseValue = elseValue };
}
```

Right-associativity enables chaining: `a if x else b if y else c` → `a if x else (b if y else c)`

---

#### 4. **ParseNullCoalesce()** (Lines 146-169) — Precedence 16
**Operator**: `??` (null coalescing)
- **Behavior**: Returns left operand if non-null, otherwise right
- **Associativity**: Left-to-right

Standard left-associative binary operator pattern using a `while` loop:
```csharp
var left = ParseLogicalOr();
while (Current.Type == TokenType.NullCoalesce)
{
    Advance();
    var right = ParseLogicalOr();
    left = new BinaryOp { Operator = BinaryOperator.NullCoalesce, Left = left, Right = right };
}
```

---

#### 5-7. **Logical Operators** (Lines 171-244) — Precedence 13-15

These follow the standard pattern, implementing short-circuit evaluation via AST structure:

- **ParseLogicalOr()** (Precedence 15): `or` operator
- **ParseLogicalAnd()** (Precedence 14): `and` operator
- **ParseLogicalNot()** (Precedence 13): `not` operator (unary prefix)

**Note**: `not` is right-associative as a unary operator, allowing `not not x`.

---

#### 8. **ParseComparison()** (Lines 246-337) — Precedence 12
**Operators**: `==`, `!=`, `<`, `<=`, `>`, `>=`, `in`, `not in`, `is`, `is not`
- **Associativity**: **Chained** (neither left nor right)
- **AST Nodes**: `BinaryOp` (single comparison) or `ComparisonChain` (multiple)

**Special Feature**: Python-style comparison chaining:
```python
a < b < c  # Equivalent to: (a < b) and (b < c)
```

**Implementation Strategy:**

1. **Type Check Special Case** (Lines 251-277):
   ```csharp
   if (Current.Type == TokenType.Is && Peek(1).Type == TokenType.Identifier)
   {
       if (IsTypeName(nextTokenValue))
       {
           // Parse as TypeCheck: value is Type
           var typeAnnotation = ParseTypeAnnotation();
           return new TypeCheck { Value = left, CheckType = typeAnnotation };
       }
   }
   ```
   Disambiguates `x is Dog` (type check) from `x is y` (identity comparison).

2. **Comparison Chain Parsing** (Lines 279-336):
   ```csharp
   var operators = new List<ComparisonOperator>();
   var operands = new List<Expression> { left };

   while (IsComparisonOperator(Current.Type))
   {
       // Handle multi-token operators: "is not" and "not in"
       if (op == TokenType.Is && Current.Type == TokenType.Not) { ... }
       else if (op == TokenType.Not && Current.Type == TokenType.In) { ... }

       operators.Add(...);
       operands.Add(ParsePipe());
   }

   if (operators.Count == 1)
       return new BinaryOp { ... };  // Single comparison
   else
       return new ComparisonChain { Operands = ..., Operators = ... };
   ```

**Important**: Multi-token operators (`is not`, `not in`) require special handling to consume both tokens.

---

#### 9-13. **Arithmetic and Bitwise Operators** (Lines 376-500)

Standard left-associative binary operators, each following the pattern:

- **ParsePipe()** (Precedence 10): `|>` pipe forward operator
- **ParseBitwiseOr()** (Precedence 9): `|` bitwise OR
- **ParseBitwiseXor()** (Precedence 8): `^` bitwise XOR
- **ParseBitwiseAnd()** (Precedence 7): `&` bitwise AND
- **ParseShift()** (Precedence 6): `<<`, `>>` bit shifts
- **ParseAdditive()** (Precedence 5): `+`, `-`
- **ParseMultiplicative()** (Precedence 4): `*`, `/`, `//`, `%`

**Pattern:**
```csharp
private Expression ParseX()
{
    var left = ParseY();  // Higher precedence
    while (Current.Type == TokenType.XOperator)
    {
        Advance();
        var right = ParseY();
        left = new BinaryOp { Operator = ..., Left = left, Right = right };
    }
    return left;
}
```

---

#### 14. **ParseUnary()** (Lines 562-592) — Precedence 3
**Operators**: `+x`, `-x`, `~x` (unary plus, minus, bitwise NOT)
- **Associativity**: Right-to-left (unary)
- **AST Node**: `UnaryOp`

**Right-associativity via recursion:**
```csharp
if (Current.Type == TokenType.Plus || Current.Type == TokenType.Minus || Current.Type == TokenType.Tilde)
{
    var op = Current.Type switch { ... };
    Advance();
    var operand = ParseUnary();  // Recursive call enables --x, ~~x
    return new UnaryOp { Operator = op, Operand = operand };
}
```

---

#### 15. **ParsePower()** (Lines 594-617) — Precedence 2
**Operator**: `**` (exponentiation)
- **Associativity**: Right-to-left
- **Important**: Unlike other binary operators, uses `ParseUnary()` for right operand!

```csharp
if (Current.Type == TokenType.DoubleStar)
{
    Advance();
    var right = ParseUnary();  // Right-associative: 2**3**2 = 2**(3**2)
    return new BinaryOp { Operator = BinaryOperator.Power, Left = left, Right = right };
}
```

**Why right-associative?** Mathematical convention: `2**3**2` = 2^(3^2) = 2^9 = 512, not (2^3)^2 = 8^2 = 64.

---

#### 16. **ParsePostfix()** (Lines 619-788) — Precedence 1 (Highest)
**Operators**: `.`, `?.`, `[]`, `()`, `as`, `to`
- **Associativity**: Left-to-right
- **AST Nodes**: `MemberAccess`, `IndexAccess`, `SliceAccess`, `FunctionCall`, `TypeCast`, `TypeCoercion`

This is the **most complex** parsing method because it handles multiple postfix operations in a loop:

```csharp
private Expression ParsePostfix()
{
    var expr = ParsePrimary();  // Start with highest precedence (literals, identifiers, etc.)

    while (true)
    {
        if (Current.Type == TokenType.Dot || Current.Type == TokenType.NullConditional)
            expr = /* MemberAccess */;
        else if (Current.Type == TokenType.LeftBracket)
            expr = /* IndexAccess or SliceAccess */;
        else if (Current.Type == TokenType.LeftParen)
            expr = /* FunctionCall */;
        else if (Current.Type == TokenType.As)
            expr = /* TypeCast */;
        else if (Current.Type == TokenType.To)
            expr = /* TypeCoercion */;
        else
            break;
    }
    return expr;
}
```

**Key Operations:**

1. **Member Access** (Lines 625-644):
   ```csharp
   var isNullConditional = Current.Type == TokenType.NullConditional;
   Advance();
   var member = ExpectIdentifierOrKeyword();  // Allows keywords as members!
   expr = new MemberAccess { Object = expr, Member = member, IsNullConditional = ... };
   ```
   Supports both `obj.member` and `obj?.member` (null-conditional).

2. **Indexing/Slicing** (Lines 645-673):
   ```csharp
   Advance();  // Skip '['
   var index = ParseSliceOrIndex();  // Returns IndexAccess or SliceAccess
   Expect(TokenType.RightBracket);

   // Update the Object property with current expression
   if (index is IndexAccess ia)
       expr = ia with { Object = expr, ... };
   else if (index is SliceAccess sa)
       expr = sa with { Object = expr, ... };
   ```
   Uses C# 9.0 `with` expressions to update record properties.

3. **Function Calls** (Lines 674-743):
   ```csharp
   var args = new List<Expression>();
   var kwargs = new List<KeywordArgument>();
   var seenKeywordArg = false;

   // Parse arguments
   do {
       if (Current.Type == TokenType.Identifier && Peek().Type == TokenType.Assign)
       {
           // Keyword argument: name=value
           seenKeywordArg = true;
           kwargs.Add(new KeywordArgument { Name = ..., Value = ParseExpression() });
       }
       else
       {
           if (seenKeywordArg)
               throw new ParserError("Positional argument cannot follow keyword argument", ...);
           args.Add(ParseExpression());
       }
   } while (...);
   ```
   **Important**: Enforces Python's rule that keyword arguments must come after positional arguments.

4. **Type Cast** (Lines 744-761):
   ```python
   value as Type  # Safe cast, returns None on failure
   ```

5. **Type Coercion** (Lines 762-780):
   ```python
   value to Type   # Throws InvalidCastException on failure
   value to Type?  # Returns None on failure for nullable types
   ```

---

#### 17. **ParseSliceOrIndex()** (Lines 790-879)
**Helper method** for `[]` operations. Determines whether it's an index, slice, or tuple (for generics):

- **Simple Index**: `arr[5]` → `IndexAccess`
- **Slice**: `arr[1:10:2]` → `SliceAccess` (start:stop:step)
- **Tuple (Generic Type Args)**: `Dict[int, str]` → `IndexAccess` with `TupleLiteral`

**Implementation Logic:**
```csharp
if (Current.Type == TokenType.Comma)
{
    // Multiple type arguments: Dict[int, str, bool]
    var elements = new List<Expression> { start! };
    while (Current.Type == TokenType.Comma) {
        Advance();
        if (Current.Type == TokenType.RightBracket) break;  // Trailing comma
        elements.Add(ParseExpression());
    }
    return new IndexAccess { Index = new TupleLiteral { Elements = ... } };
}

if (Current.Type == TokenType.Colon)
{
    // Slice: [start:stop:step]
    isSlice = true;
    // Parse stop and optional step...
    return new SliceAccess { Start = start, Stop = stop, Step = step };
}

// Simple index
return new IndexAccess { Index = start! };
```

**Note**: The returned `IndexAccess`/`SliceAccess` has `Object = null!` — the caller fills this in.

---

## Dependencies

### Internal Sharpy Dependencies

**From `Sharpy.Compiler.Lexer`:**
- `Token`: Token type with `Type`, `Value`, `Line`, `Column`
- `TokenType` enum: All token types (operators, keywords, literals)

**From `Sharpy.Compiler.Parser.Ast`:**
- Expression types: `BinaryOp`, `UnaryOp`, `ConditionalExpression`, `WalrusExpression`, etc.
- Operator enums: `BinaryOperator`, `UnaryOperator`, `ComparisonOperator`

**From Other Parser Files:**
- `ParsePrimary()` (Parser.Primaries.cs): Parses literals, identifiers, parenthesized expressions
- `ParseTypeAnnotation()` (Parser.Types.cs): Parses type annotations for casts/checks
- Helper methods (Parser.Types.cs):
  - `Advance()`: Move to next token
  - `Expect(TokenType)`: Consume expected token or throw error
  - `ExpectIdentifier()`: Consume and return identifier value
  - `ExpectIdentifierOrKeyword()`: Consume identifier or keyword (for member access)
  - `IsTypeName(string)`: Check if identifier is a type name
  - `CombineSpans()`, `GetSpanFromToken()`: Source location tracking

### External Dependencies
- `System.Collections.Immutable`: For `ImmutableArray<T>` in AST nodes

---

## Patterns and Design Decisions

### 1. **Recursive Descent with Precedence Climbing**
Each method handles one precedence level. Lower precedence methods call higher precedence methods to build the tree bottom-up.

**Visualization:**
```
ParseExpression (lowest precedence)
  ↓
ParseWalrusExpression (:=)
  ↓
ParseTryMaybeExpression (try/maybe)
  ↓
ParseConditionalExpression (if/else)
  ↓
ParseNullCoalesce (??)
  ↓
... (logical, comparison, arithmetic) ...
  ↓
ParsePostfix (., [], ())
  ↓
ParsePrimary (highest precedence - literals/identifiers)
```

### 2. **Left vs. Right Associativity**

**Left-associative** (most operators): Use `while` loop, accumulate left:
```csharp
var left = ParseNext();
while (IsOperator) {
    var right = ParseNext();
    left = new BinaryOp { Left = left, Right = right };  // left becomes LHS
}
```

**Right-associative** (`:=`, `**`, `if/else`): Use recursion:
```csharp
var left = ParseNext();
if (IsOperator) {
    var right = ParseSelf();  // Recursive call!
    return new Op { Left = left, Right = right };
}
```

### 3. **Immutable AST with Records**
All AST nodes are C# 9.0 `record` types (immutable). The `with` expression is used to create modified copies:
```csharp
expr = ia with { Object = expr, LineStart = ..., ... };
```

### 4. **Source Location Tracking**
Every AST node includes:
- `LineStart`, `ColumnStart`, `LineEnd`, `ColumnEnd`: Position in source file
- `Span`: `TextSpan?` for IDE integration (go-to-definition, errors)

These are populated from token positions using helpers like `CombineSpans()`.

### 5. **Multi-Token Operators**
`is not` and `not in` are single logical operators but tokenized as two tokens. Special handling:
```csharp
if (op == TokenType.Is && Current.Type == TokenType.Not)
{
    Advance();  // Consume the second token
    operators.Add(ComparisonOperator.IsNot);
}
```

### 6. **Null-Conditional Operator** (`?.`)
Sharpy extends Python with C#-style null-conditional access:
```csharp
obj?.method()  # Returns None if obj is None
```

Represented by `IsNullConditional` flag on `MemberAccess`.

### 7. **Error Recovery**
Parser uses **fail-fast** approach: `ParserError` exception on any syntax error. No error recovery or synchronization.

---

## Debugging Tips

### 1. **Understanding Parse Errors**
When you get "Expected X, got Y", the error occurs in a precedence level. Trace the call stack:
```
ParseExpression → ... → ParsePostfix → ParsePrimary
```
The error location tells you which precedence level failed.

### 2. **Operator Precedence Issues**
If an expression parses incorrectly, check the precedence table (docs/language_specification/operator_precedence.md).

**Example:** If `a + b * c` parses as `(a + b) * c` instead of `a + (b * c)`, it means `ParseMultiplicative` should be called by `ParseAdditive`, not the reverse.

### 3. **Inspecting the AST**
Use the CLI to dump the parsed AST:
```bash
dotnet run --project src/Sharpy.Cli -- emit ast file.spy
```

### 4. **Comparison Chain Debugging**
For `a < b < c`, check:
- Are all operands parsed correctly?
- Are operators collected in the right order?
- Does `operators.Count` match `operands.Count - 1`?

### 5. **Postfix Operation Ordering**
For `obj.method()[0].field`:
1. Parse `obj` (Primary)
2. Apply `.method` → `MemberAccess`
3. Apply `()` → `FunctionCall` wrapping MemberAccess
4. Apply `[0]` → `IndexAccess` wrapping FunctionCall
5. Apply `.field` → `MemberAccess` wrapping IndexAccess

The loop builds left-to-right, each iteration wrapping the previous result.

### 6. **Right-Associative Recursion**
For `a := b := 5`:
- First call: `left = Identifier("a")`, sees `:=`, recurses
- Second call: `left = Identifier("b")`, sees `:=`, recurses
- Third call: `left = IntegerLiteral(5)`, no `:=`, returns
- Unwind: `b := 5`, then `a := (b := 5)`

### 7. **Null Span Warnings**
If you see `Span = expr.Span` with a comment "// TypeAnnotation doesn't have Span yet (A.12)":
- This is a known limitation tracked in the codebase
- TypeAnnotation nodes don't yet have proper span tracking
- Uses operand span as fallback

---

## Contribution Guidelines

### What Changes Might Be Made to This File

1. **Adding New Operators**
   - Determine precedence level (consult language spec)
   - Add token type to appropriate `ParseX()` method
   - Update operator enum in `Ast/Expression.cs`
   - Add tests in `Sharpy.Compiler.Tests`

2. **Fixing Precedence Issues**
   - Reorder method calls to match intended precedence
   - Update operator_precedence.md if spec changes

3. **Improving Error Messages**
   - Add context to `ParserError` exceptions
   - Consider adding "Did you mean...?" suggestions

4. **Performance Optimization**
   - This is a hot path — avoid allocations in tight loops
   - Consider using `Span<T>` for temporary lists

5. **Adding Source Location Tracking**
   - When TypeAnnotation gains `Span` support, update lines 274, 759, 778

### Testing Changes

After modifying this file:
```bash
# Run parser tests
dotnet test --filter "FullyQualifiedName~Parser"

# Run expression-specific tests
dotnet test --filter "DisplayName~Expression"

# Test specific operators
dotnet test --filter "DisplayName~Walrus"
dotnet test --filter "DisplayName~Comparison"
```

### Code Style

- **Maintain consistency**: Follow existing patterns for new operators
- **Use object initializers**: All AST nodes use initializer syntax
- **Keep methods short**: Each method handles one precedence level
- **Comment multi-token operators**: `is not`, `not in` are easy to miss
- **Document associativity**: Add comments for right-associative operators

### Common Pitfalls

1. **Don't forget `Advance()`**: Every token consumed must advance the position
2. **Check for null**: `ParseSliceOrIndex()` returns `Object = null!` — caller must fill it
3. **Validate argument order**: Keyword args must follow positional args
4. **Handle trailing commas**: Function calls and tuples allow trailing commas
5. **Test edge cases**: Empty argument lists, single-element tuples, etc.

---

## Cross-References

### Related Parser Files (Partial Classes)
- [Parser.cs](Parser.md) — Core infrastructure and entry points
- [Parser.Primaries.cs](Parser.Primaries.md) — Literal and primary expression parsing
- [Parser.Statements.cs](Parser.Statements.md) — Statement parsing
- [Parser.Definitions.cs](Parser.Definitions.md) — Function/class/struct definitions
- [Parser.Types.cs](Parser.Types.md) — Type annotation parsing and utility methods

### Related AST Definitions
- `src/Sharpy.Compiler/Parser/Ast/Expression.cs` — All expression node types and operator enums

### Specification Documents
- `docs/language_specification/expressions.md` — Expression semantics
- `docs/language_specification/operator_precedence.md` — **Critical reference** for understanding the parsing order

### Downstream Components
- `Sharpy.Compiler.Semantic` — Consumes AST for type checking and semantic analysis
- `Sharpy.Compiler.CodeGen.RoslynEmitter` — Converts AST to Roslyn C# syntax trees
