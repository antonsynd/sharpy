# Walkthrough: Expression.Future.cs

**Source File**: `src/Sharpy.Compiler/Parser/Ast/Expression.Future.cs`

---

## Overview

`Expression.Future.cs` contains **placeholder AST node definitions** for upcoming language features planned for Sharpy v0.2.x and beyond. This file follows the "future features" pattern used in the Sharpy compiler, where AST nodes are defined early to establish the immutable data structure contracts before full implementation.

### Role in the Compiler Pipeline

**Position**: AST Node Definitions (Parser stage)  
**Status**: Placeholder definitions — parser logic **not yet implemented**  
**Target Version**: v0.2.x+

```
Source (.spy) → Lexer → Parser (creates these AST nodes) → Semantic → CodeGen → C#
                              ↑ NOT YET IMPLEMENTED
```

This file currently contains:
1. **AwaitExpression** - For async/await support
2. **MatchExpression** - For pattern matching expressions (returns a value)
3. **MatchArm** - Supporting type for match expression cases

These definitions exist to:
- Establish the immutable record-based contract early
- Enable downstream components (semantic analysis, codegen) to be designed in parallel
- Serve as documentation for the planned feature shape
- Allow compile-time references before runtime implementation

---

## File Organization

The file is organized with clear region markers indicating feature groupings and target versions:

```csharp
#region Async/Await (v0.2.x+)
// AwaitExpression
#endregion

#region Pattern Matching (v0.2.x)
// MatchExpression, MatchArm
#endregion
```

This structure parallels the companion file `Statement.Future.cs` which contains future statement nodes.

---

## Class/Type Structure

### Base Type: `Expression`

All nodes in this file inherit from `Expression`, which itself inherits from `Node`:

```
Node (abstract record)
 ├─ Expression (abstract record)
 │   ├─ AwaitExpression
 │   ├─ MatchExpression
 │   └─ ... (other expression types in Expression.cs)
 └─ Statement, Pattern, etc.
```

**Key inheritance properties from `Node`**:
- `LineStart`, `ColumnStart`, `LineEnd`, `ColumnEnd` - Source location tracking
- `Span: TextSpan?` - Optional character-offset span for precise error reporting

### 1. AwaitExpression

**Purpose**: Represents the `await` keyword applied to an async operation.

```csharp
public record AwaitExpression : Expression
{
    public Expression Operand { get; init; } = null!;
}
```

**Properties**:
- `Operand`: The expression being awaited (must return a `Task<T>` or `ValueTask<T>`)

**Python Syntax Example**:
```python
async def fetch_data(url: str) -> str:
    result = await http_get(url)  # AwaitExpression
    return result
```

**Design Notes**:
- Follows Python's `await` syntax (prefix operator)
- Maps to C#'s `await` keyword in codegen
- Will validate that `Operand` type implements awaitable pattern during semantic analysis
- Can only appear inside `async` functions (validated in semantic phase)

### 2. MatchExpression

**Purpose**: Represents a `match` expression that returns a value based on pattern matching.

```csharp
public record MatchExpression : Expression
{
    public Expression Scrutinee { get; init; } = null!;
    public ImmutableArray<MatchArm> Arms { get; init; } = ImmutableArray<MatchArm>.Empty;
}
```

**Properties**:
- `Scrutinee`: The value being matched against (the thing you're switching on)
- `Arms`: Collection of pattern/result pairs (the cases)

**Python Syntax Example**:
```python
result = match value:
    case 0: "zero"
    case 1: "one"
    case int() as n if n > 0: "positive"
    case _: "other"
```

**Design Notes**:
- This is the **expression form** of match (returns a value)
- Contrast with **MatchStatement** (in `Statement.Future.cs`) which executes statements
- Uses `ImmutableArray<MatchArm>` - immutability is critical for AST nodes
- Must be exhaustive (all cases covered) - validated during semantic analysis
- Maps to C# switch expressions (`value switch { 0 => "zero", ... }`)

**Key Terminology**:
- **Scrutinee**: The value being examined (common compiler term from ML languages)
- **Arms**: The cases being matched (each is pattern → result)

### 3. MatchArm

**Purpose**: A single case in a match expression (pattern → result mapping).

```csharp
public record MatchArm
{
    public Pattern Pattern { get; init; } = null!;
    public Expression? Guard { get; init; }
    public Expression Result { get; init; } = null!;
    
    // Source location properties
    public int LineStart { get; init; }
    public int ColumnStart { get; init; }
    public int LineEnd { get; init; }
    public int ColumnEnd { get; init; }
    public Text.TextSpan? Span { get; init; }
}
```

**Properties**:
- `Pattern`: The pattern to match against (see `Pattern.cs` for pattern types)
- `Guard`: Optional `when`/`if` clause for additional conditions
- `Result`: The expression to evaluate if the pattern matches

**Note**: `MatchArm` is **not** a `Node` subclass, but it implements `ILocatable` manually by having the same location properties. This is because it's a supporting structure, not a standalone AST node.

**Python Syntax Example**:
```python
case int() as n if n > 0: n * 2
# Pattern: int() as n
# Guard: n > 0
# Result: n * 2
```

**Design Notes**:
- Guard is nullable - most patterns don't need additional conditions
- Result must be an expression (single value), not a statement block
- Source location tracking enables precise error messages during pattern matching failures

---

## Key Patterns and Design Decisions

### 1. Immutable Record Pattern

All AST nodes use C# 9.0+ `record` types with `init` properties:

```csharp
public record AwaitExpression : Expression
{
    public Expression Operand { get; init; } = null!;
}
```

**Why?**
- **Immutability**: AST nodes are never modified after construction (functional programming principle)
- **Value semantics**: Records provide structural equality
- **Ease of cloning**: `with` expressions allow non-destructive updates
- **Thread safety**: Immutable data structures are inherently thread-safe

**The `= null!` pattern**:
- Suppresses C# nullable warnings for required properties
- Properties will be initialized during parsing
- `null!` is a "trust me, I'll set this" marker for the type system

### 2. Future Features Pattern

This file demonstrates Sharpy's **progressive implementation strategy**:

```csharp
/// <remarks>
/// PLACEHOLDER: Parser support not yet implemented.
/// Target version: v0.2.x+
/// </remarks>
```

**Benefits**:
1. **Early API design**: Downstream teams can design semantic/codegen against stable interfaces
2. **Documentation**: Serves as living specification for planned features
3. **Gradual implementation**: Can implement lexer → parser → semantic → codegen incrementally
4. **Type safety**: Compiler won't allow partial implementations to be used incorrectly

### 3. Separation of Expression vs Statement Forms

Sharpy distinguishes between:
- **MatchExpression** (this file): Returns a value, used in expression contexts
- **MatchStatement** (`Statement.Future.cs`): Executes statements, used at statement level

This mirrors Python and C#'s distinction between switch expressions and switch statements.

### 4. ImmutableArray Usage

```csharp
public ImmutableArray<MatchArm> Arms { get; init; } = ImmutableArray<MatchArm>.Empty;
```

**Why ImmutableArray?**
- Performance: Stack-allocated, no heap allocation for empty arrays
- Immutability: Prevents accidental modification
- Initialization: `.Empty` provides a safe default (never null)

**Important**: Always use `.Empty`, never `default(ImmutableArray<T>)` which creates a "null" array that throws on access.

---

## Dependencies

### Direct Dependencies

1. **Node.cs** (`Parser/Ast/Node.cs`)
   - Base class providing `ILocatable` implementation
   - Source location tracking infrastructure

2. **Expression.cs** (`Parser/Ast/Expression.cs`)
   - Base class for all expression nodes
   - Provides common expression infrastructure

3. **Pattern.cs** (`Parser/Ast/Pattern.cs`)
   - Defines pattern types used in `MatchArm.Pattern`
   - Contains: `WildcardPattern`, `BindingPattern`, `LiteralPattern`, etc.

4. **System.Collections.Immutable**
   - Provides `ImmutableArray<T>` for collections

### Related Files

- **Statement.Future.cs**: Contains `MatchStatement` (statement form of match)
- **Expression.cs**: Contains currently-implemented expression types
- **TypeAnnotation** (in `Types.cs`): Used in patterns for type constraints

### Downstream Consumers (Future)

When implemented, these nodes will be consumed by:

1. **Parser** (`Parser/Parser.cs`)
   - Will construct these AST nodes during parsing
   - Methods like `ParseAwaitExpression()`, `ParseMatchExpression()`

2. **Semantic Analysis** (`Semantic/TypeChecker.cs`)
   - Will validate type correctness
   - Check that await only appears in async functions
   - Verify match exhaustiveness

3. **Code Generation** (`CodeGen/RoslynEmitter.cs`)
   - Will emit corresponding C# code
   - `AwaitExpression` → C# `await` operator
   - `MatchExpression` → C# switch expression

---

## Connection to Language Specifications

These AST nodes correspond to specifications in `docs/language_specification/`:

### AwaitExpression

**Spec**: `docs/language_specification/async_programming.md`

Key points from spec:
- `await` can only appear in `async` functions
- The awaited expression must return `Task<T>` or `ValueTask<T>`
- No async comprehensions (different from Python 3.6+)
- Maps directly to C# `await` keyword

**Not supported** (per spec):
```python
# ❌ await inside comprehension
results = [await fetch(url) for url in urls]
```

**Supported**:
```python
# ✅ await in async function
async def fetch_all():
    result = await fetch("url")
    return result
```

### MatchExpression

**Spec**: `docs/language_specification/match_statement.md`

Key points from spec:
- Expression form produces a value (contrast with statement form)
- Must be exhaustive (all cases covered)
- Each case is a single expression, not a statement block
- Maps to C# switch expressions (C# 8+)

**Expression context example**:
```python
# Used in assignment
result = match value:
    case 0: "zero"
    case _: "other"

# Used in return
def categorize(n: int) -> str:
    return match n:
        case 0: "zero"
        case _: "other"
```

---

## How Pattern Matching Works (Future Implementation)

When fully implemented, the flow will be:

### 1. Parser Phase
```
Source: "match x: case 1: 'one'"
   ↓
Lexer: [MATCH, IDENTIFIER('x'), COLON, CASE, INT(1), COLON, STRING('one')]
   ↓
Parser: Creates MatchExpression {
    Scrutinee = IdentifierExpression("x"),
    Arms = [
        MatchArm {
            Pattern = LiteralPattern(IntegerLiteral("1")),
            Guard = null,
            Result = StringLiteral("one")
        }
    ]
}
```

### 2. Semantic Phase
```
TypeChecker validates:
- Scrutinee has a compatible type
- Pattern types match scrutinee type
- Guards are boolean expressions
- All arms produce compatible result types
- Match is exhaustive (all values covered)
```

### 3. Code Generation Phase
```
RoslynEmitter converts to C#:
MatchExpression → switch expression

Generated C#:
x switch {
    1 => "one",
    _ => throw new MatchError("non-exhaustive pattern")
}
```

---

## Debugging Tips

### When Adding Parser Support

1. **Start with lexer tokens**: Ensure `AWAIT`, `MATCH`, `CASE` tokens exist in `Lexer/Token.cs`

2. **Test incrementally**:
   ```bash
   dotnet run --project src/Sharpy.Cli -- emit tokens test.spy
   dotnet run --project src/Sharpy.Cli -- emit ast test.spy
   ```

3. **Use file-based integration tests**: Add `.spy` + `.expected` pairs to `Integration/TestFixtures/`
   ```
   TestFixtures/match_expressions/
   ├── simple_match.spy
   └── simple_match.expected
   ```

4. **Common pitfalls**:
   - Forgetting to handle expression vs statement contexts
   - Not validating that match arms are exhaustive
   - Incorrect precedence in expression parsing

### When Adding Semantic Support

1. **Type checking order**:
   - Check scrutinee type first
   - Validate each pattern against scrutinee type
   - Check guards are boolean
   - Verify result types are compatible

2. **Exhaustiveness checking**:
   - Track covered values during pattern analysis
   - Wildcard (`_`) covers all remaining cases
   - Report error if no wildcard and not all values covered

3. **Type narrowing**:
   - Pattern matching narrows types (e.g., `case int() as n` narrows `object` to `int`)
   - Store narrowed types in `SemanticInfo._narrowedTypes`

### When Adding Codegen Support

1. **Use SyntaxFactory exclusively**:
   ```csharp
   // ✅ Correct
   var switchExpr = SwitchExpression(scrutinee)
       .WithArms(SeparatedList(arms));
   
   // ❌ Wrong - never use string templating
   var code = $"switch ({scrutinee}) {{ ... }}";
   ```

2. **Map patterns to C# patterns**:
   - `WildcardPattern` → discard pattern `_`
   - `LiteralPattern` → constant pattern `1`, `"text"`
   - `BindingPattern` → declaration pattern `int n`

3. **Test generated C#**:
   ```bash
   dotnet run --project src/Sharpy.Cli -- emit csharp test.spy
   ```

### Debugging Integration Tests

When tests fail:

1. **Check parser output**:
   ```bash
   dotnet run --project src/Sharpy.Cli -- emit ast test.spy
   ```

2. **Verify semantic info**:
   - Add logging in `TypeChecker.cs` to see resolved types
   - Check `SemanticInfo` has correct type annotations

3. **Inspect generated C#**:
   ```bash
   dotnet run --project src/Sharpy.Cli -- emit csharp test.spy > output.cs
   cat output.cs
   ```

4. **Run specific test**:
   ```bash
   dotnet test --filter "FullyQualifiedName~MatchExpressionTests"
   ```

---

## Contribution Guidelines

### Adding New Future Expression Nodes

If adding a new planned expression type (e.g., `TryExpression`, `ComprehensionExpression`):

1. **Add to this file** with proper region markers:
   ```csharp
   #region Try Expressions (v0.3.x)
   
   /// <summary>
   /// Try expression (try expr except Type: fallback).
   /// </summary>
   /// <remarks>
   /// PLACEHOLDER: Parser support not yet implemented.
   /// Target version: v0.3.x
   /// </remarks>
   public record TryExpression : Expression
   {
       public Expression Body { get; init; } = null!;
       public ImmutableArray<ExceptClause> Handlers { get; init; } = ImmutableArray<ExceptClause>.Empty;
   }
   
   #endregion
   ```

2. **Follow the pattern**:
   - Use `record` types
   - All properties use `init`
   - Required properties default to `null!`
   - Collections default to `.Empty`
   - Include XML doc comments
   - Add `PLACEHOLDER` remark with target version

3. **Document in spec**: Create or update corresponding spec in `docs/language_specification/`

4. **Add to tracking**: Update `docs/language_specification/deferred_features.md`

### Implementing Placeholder Features

When implementing a placeholder definition:

1. **Remove PLACEHOLDER comment** from the AST node

2. **Add lexer support** (`Lexer/Lexer.cs`, `Lexer/Token.cs`):
   - Define token type
   - Add tokenization logic

3. **Add parser support** (`Parser/Parser.cs`):
   - Create parsing method (e.g., `ParseAwaitExpression()`)
   - Wire into expression parsing precedence

4. **Add semantic support** (`Semantic/TypeChecker.cs`):
   - Add `Visit` method for the expression type
   - Implement type checking logic
   - Add validation as needed

5. **Add codegen support** (`CodeGen/RoslynEmitter.cs`):
   - Add emission logic using `SyntaxFactory`
   - Test generated C# compiles

6. **Add tests** at each layer:
   - Lexer tests: `Sharpy.Compiler.Tests/Lexer/`
   - Parser tests: `Sharpy.Compiler.Tests/Parser/`
   - Semantic tests: `Sharpy.Compiler.Tests/Semantic/`
   - Integration tests: `Integration/TestFixtures/`

7. **Update documentation**:
   - Mark feature as implemented in spec
   - Add examples to samples
   - Update README if public-facing feature

### Testing Strategy

**For placeholder definitions (current state)**:
- No tests needed (definitions are not yet used)
- Parser should reject these constructs if encountered

**When implementing**:
- Unit tests at each layer (lexer, parser, semantic, codegen)
- Integration tests for end-to-end verification
- File-based tests for common use cases
- Edge case tests (empty matches, nested awaits, etc.)

### Code Style

- Follow existing AST conventions (see `Expression.cs`)
- Use XML doc comments for public types
- Include `<remarks>` with implementation status
- Keep properties alphabetically ordered (except location properties at end)
- Use consistent formatting with existing code

---

## Related Documentation

### AST Node Files
- **[Expression.cs](./Expression.md)** - Currently implemented expression types
- **[Statement.Future.cs](../../Statement.Future.md)** - Placeholder statement definitions (includes MatchStatement)
- **[Pattern.cs](./Pattern.md)** - Pattern types for match expressions
- **[Node.cs](./Node.md)** - Base AST node class

### Parser Documentation
- **[Parser.md](../../Parser.md)** - Main parser implementation
- **[Parser.Expressions.md](../../Parser.Expressions.md)** - Expression parsing logic

### Language Specifications
- **async_programming.md** - Async/await specification
- **match_statement.md** - Pattern matching specification
- **expressions.md** - General expression syntax
- **operator_precedence.md** - Expression precedence rules

### Implementation Guides
- **Main README** - Repository root documentation
- **Sharpy.Compiler/HOW_TO_CONTRIBUTE.md** - Compiler contribution guide
- **Sharpy.Compiler.Tests/HOW_TO_CONTRIBUTE.md** - Testing guide

---

## Cross-References

This file is part of the **AST module** split across multiple files:

### Same Directory (Parser/Ast/)
- `Expression.cs` - Implemented expression types (literals, operators, calls, etc.)
- `Expression.Future.cs` - **[THIS FILE]** - Placeholder expression types
- `Statement.cs` - Implemented statement types
- `Statement.Future.cs` - Placeholder statement types (includes `MatchStatement`)
- `Pattern.cs` - Pattern types for match expressions
- `Types.cs` - Type annotation AST nodes
- `Node.cs` - Base AST node class

### Used By
- **Parser** (`Parser/Parser.cs`) - Will construct these nodes when implemented
- **Semantic Analyzer** (`Semantic/TypeChecker.cs`) - Will validate these nodes
- **Code Generator** (`CodeGen/RoslynEmitter.cs`) - Will emit C# for these nodes

### Dependencies
- **System.Collections.Immutable** - For `ImmutableArray<T>`
- **Sharpy.Compiler.Text** - For `TextSpan` (source location tracking)

---

## Summary

`Expression.Future.cs` is a **forward-looking design document in code form**. It establishes the shape of upcoming features without implementing them, allowing the team to:

1. **Design the API early** - Downstream components can be designed against stable interfaces
2. **Document intent** - Clear communication of planned features and their shape
3. **Enable parallel work** - Different team members can work on lexer, parser, semantic, and codegen simultaneously
4. **Maintain consistency** - All future features follow the same immutable record pattern

When implementing these features, developers should:
- Follow the established pattern (immutable records with `init` properties)
- Use `ImmutableArray` for collections
- Add comprehensive tests at each compiler stage
- Update specifications as implementation progresses
- Remove PLACEHOLDER comments when features are complete

The immutable AST design is central to Sharpy's architecture—it enables clean separation of concerns, thread-safety, and functional programming patterns throughout the compiler pipeline.
