# Walkthrough: Expression.cs

**Source File**: `src/Sharpy.Compiler/Parser/Ast/Expression.cs`

---

## Overview

`Expression.cs` defines the complete hierarchy of expression node types used throughout the Sharpy compiler. These immutable record types represent every kind of expression that can appear in Sharpy source code—from simple literals like `42` and `"hello"` to complex constructs like comprehensions, lambda expressions, and operator chains.

**Role in Compiler Pipeline:**
- **Created by**: Parser (`Parser.Expressions.cs`) converts tokens into these AST nodes
- **Consumed by**:
  - Semantic analyzer (`TypeChecker.Expressions.cs`) for type inference and validation
  - Code generator (`RoslynEmitter.Expressions.cs`) to produce C# Roslyn syntax trees
  - Pattern matcher (future) for destructuring and matching

**Key Philosophy**: All expression nodes are **immutable records** that inherit from the `Expression` base class, which in turn inherits from `Node` (providing source location tracking).

---

## Class/Type Structure

### Base Class Hierarchy

```csharp
Node (abstract record, from Node.cs)
  ├─ Expression (abstract record) ← defined here
  └─ Statement (abstract record, from Statement.cs)
```

Every expression node:
1. Inherits from `Expression : Node`
2. Is an immutable `record` type (C# 9.0+)
3. Has source location info (LineStart, ColumnStart, LineEnd, ColumnEnd, Span)
4. Uses `init`-only properties for immutability
5. Uses `ImmutableArray<T>` for collections

### Organization (7 major categories)

The 430-line file is organized into regions representing different expression categories:

1. **Literals** (lines 10-72) — Primitive values
2. **Collections** (lines 74-114) — Container literals
3. **Comprehensions** (lines 116-168) — List/set/dict comprehensions
4. **Primary Expressions** (lines 170-232) — Identifiers, member access, calls
5. **Operators** (lines 234-328) — Unary, binary, comparison operations
6. **Advanced Expressions** (lines 331-429) — Lambdas, type operations, special forms

---

## Key Functions/Methods

### Expression Types by Category

#### 1. Literals Region (lines 10-72)

**Purpose**: Represent constant values directly written in source code.

**IntegerLiteral**
```csharp
public record IntegerLiteral : Expression
{
    public string Value { get; init; } = "";
    public string? Suffix { get; init; }  // L, U, UL, etc.
}
```

- `Value`: String representation (e.g., `"42"`, `"1_000_000"`) to preserve exact formatting
- `Suffix`: Optional type suffix for explicit typing (`42L` → `"L"`)
- **Why string for Value?** Avoids parsing errors and preserves source fidelity during AST construction

**FloatLiteral**
Similar to IntegerLiteral but for floating-point values:
- `Suffix` can be `f`, `F`, `d`, `D`, `m`, `M` (for float, double, decimal)

**StringLiteral**
```csharp
public record StringLiteral : Expression
{
    public string Value { get; init; } = "";
    public bool IsRaw { get; init; }
}
```

- `Value`: The actual string content (escape sequences already processed by lexer)
- `IsRaw`: True for raw strings (`r"C:\path"`) where backslashes are literal

**FStringLiteral** (lines 42-52)
Represents Python-style f-strings: `f"Hello {name}, you are {age} years old"`

```csharp
public record FStringLiteral : Expression
{
    public ImmutableArray<FStringPart> Parts { get; init; }
}

public record FStringPart
{
    public string? Text { get; init; }
    public Expression? Expression { get; init; }
    public string? FormatSpec { get; init; }  // e.g., ".2f", ">10"
}
```

**Key insight**: F-strings are parsed into alternating text/expression parts:
- `f"Hello {name}!"` → Parts: [Text="Hello ", Expr=Identifier("name"), Text="!"]
- `FormatSpec` captures Python format specifications: `{price:.2f}` → `".2f"`

**BooleanLiteral, NoneLiteral, EllipsisLiteral**
- `BooleanLiteral`: `True` or `False` (note: Python-style capitalization)
- `NoneLiteral`: Represents Python's `None` (maps to C# `null`)
- `EllipsisLiteral`: Python's `...` (used in type hints, slicing)

#### 2. Collections Region (lines 74-114)

Represent Python's built-in collection literals.

**ListLiteral**
```csharp
public record ListLiteral : Expression
{
    public ImmutableArray<Expression> Elements { get; init; } = ImmutableArray<Expression>.Empty;
}
```
Represents `[1, 2, 3]` or `["a", "b", "c"]`

**DictLiteral**
```csharp
public record DictLiteral : Expression
{
    public ImmutableArray<DictEntry> Entries { get; init; } = ImmutableArray<DictEntry>.Empty;
}

public record DictEntry
{
    public Expression Key { get; init; } = null!;
    public Expression Value { get; init; } = null!;
}
```
Represents `{"a": 1, "b": 2}` — note that both keys and values are arbitrary expressions

**SetLiteral**
```csharp
public record SetLiteral : Expression
{
    public ImmutableArray<Expression> Elements { get; init; } = ImmutableArray<Expression>.Empty;
}
```
Represents `{1, 2, 3}` — **Parser disambiguation**: `{}` is an empty dict, `{1}` is a set

**TupleLiteral**
```csharp
public record TupleLiteral : Expression
{
    public ImmutableArray<Expression> Elements { get; init; } = ImmutableArray<Expression>.Empty;
}
```
Represents `(1, 2, 3)` or `(1,)` (single-element tuple requires trailing comma)

**Design note**: All collection literals use `ImmutableArray<Expression>` to maintain immutability throughout the AST.

#### 3. Comprehensions Region (lines 116-168)

Represent Python's powerful comprehension syntax.

**ListComprehension**
```csharp
public record ListComprehension : Expression
{
    public Expression Element { get; init; } = null!;
    public ImmutableArray<ComprehensionClause> Clauses { get; init; } = ImmutableArray<ComprehensionClause>.Empty;
}
```

Represents: `[expr for x in iterable if condition]`
- `Element`: The output expression (e.g., `x * 2`)
- `Clauses`: Sequence of `ForClause` and `IfClause` nodes

**ComprehensionClause Hierarchy**
```csharp
public abstract record ComprehensionClause : Node;

public record ForClause : ComprehensionClause
{
    public Expression Target { get; init; } = null!;    // Loop variable
    public Expression Iterator { get; init; } = null!;  // Iterable expression
}

public record IfClause : ComprehensionClause
{
    public Expression Condition { get; init; } = null!;
}
```

**Example comprehension parsing:**
```python
[x * 2 for x in numbers if x > 0]
```
→
```
ListComprehension {
  Element = BinaryOp(x * 2),
  Clauses = [
    ForClause(Target=Identifier("x"), Iterator=Identifier("numbers")),
    IfClause(Condition=BinaryOp(x > 0))
  ]
}
```

**Multiple for clauses supported:**
```python
[(x, y) for x in range(3) for y in range(3) if x != y]
```

**SetComprehension, DictComprehension**
Similar structure, but:
- `SetComprehension`: Single `Element` expression
- `DictComprehension`: Both `Key` and `Value` expressions

#### 4. Primary Expressions Region (lines 170-232)

Fundamental building blocks for accessing values and calling functions.

**Identifier**
```csharp
public record Identifier : Expression
{
    public string Name { get; init; } = "";
}
```
Represents variable/function names: `x`, `my_function`, `_private`

**MemberAccess**
```csharp
public record MemberAccess : Expression
{
    public Expression Object { get; init; } = null!;
    public string Member { get; init; } = "";
    public bool IsNullConditional { get; init; }  // obj?.member
}
```

Represents:
- `obj.member` (IsNullConditional = false)
- `obj?.member` (IsNullConditional = true) — Sharpy extension for null-safety

**Chain example**: `a.b.c` is parsed as nested MemberAccess nodes:
```
MemberAccess {
  Object = MemberAccess { Object = Identifier("a"), Member = "b" },
  Member = "c"
}
```

**IndexAccess**
```csharp
public record IndexAccess : Expression
{
    public Expression Object { get; init; } = null!;
    public Expression Index { get; init; } = null!;
}
```

Represents: `list[0]`, `dict["key"]`, `matrix[i]`

**Important distinction**: Single index only. Multi-dimensional indexing like `arr[i, j]` uses a TupleLiteral as the Index.

**SliceAccess**
```csharp
public record SliceAccess : Expression
{
    public Expression Object { get; init; } = null!;
    public Expression? Start { get; init; }
    public Expression? Stop { get; init; }
    public Expression? Step { get; init; }
}
```

Represents Python slicing: `list[1:5]`, `list[::2]`, `list[::-1]`
- All bounds are optional: `list[:]` → all null
- `Step` enables stride: `list[::2]` → every second element

**FunctionCall**
```csharp
public record FunctionCall : Expression
{
    public Expression Function { get; init; } = null!;
    public ImmutableArray<Expression> Arguments { get; init; } = ImmutableArray<Expression>.Empty;
    public ImmutableArray<KeywordArgument> KeywordArguments { get; init; } = ImmutableArray<KeywordArgument>.Empty;
}

public record KeywordArgument
{
    public string Name { get; init; } = "";
    public Expression Value { get; init; } = null!;
    // + source location fields
}
```

Represents: `func(1, 2, key=3)`
- `Function`: Any expression (usually Identifier or MemberAccess)
- `Arguments`: Positional args
- `KeywordArguments`: Named args (Python-style)

**Note**: `KeywordArgument` has its own source location tracking (not a Node subclass).

#### 5. Operators Region (lines 234-328)

**UnaryOp**
```csharp
public record UnaryOp : Expression
{
    public UnaryOperator Operator { get; init; }
    public Expression Operand { get; init; } = null!;
}

public enum UnaryOperator
{
    Plus,      // +x
    Minus,     // -x
    Not,       // not x
    BitwiseNot // ~x
}
```

Represents prefix operators: `-5`, `not flag`, `~bitmask`

**BinaryOp**
```csharp
public record BinaryOp : Expression
{
    public BinaryOperator Operator { get; init; }
    public Expression Left { get; init; } = null!;
    public Expression Right { get; init; } = null!;
}
```

The `BinaryOperator` enum (lines 263-304) covers:

**Arithmetic**: Add, Subtract, Multiply, Divide, FloorDivide, Modulo, Power
- `FloorDivide`: Python's `//` operator (integer division)
- `Power`: Python's `**` operator

**Comparison**: Equal, NotEqual, LessThan, LessThanOrEqual, GreaterThan, GreaterThanOrEqual

**Logical**: And, Or (short-circuiting)

**Bitwise**: BitwiseAnd, BitwiseOr, BitwiseXor, LeftShift, RightShift

**Membership/Identity**: In, NotIn, Is, IsNot
- `In`/`NotIn`: Python's membership testing (`x in list`)
- `Is`/`IsNot`: Python's identity comparison (`x is None`)

**Sharpy Extensions**:
- `NullCoalesce`: `??` operator (from C#)
- `PipeForward`: `|>` operator (functional programming style)

**ComparisonChain**
```csharp
public record ComparisonChain : Expression
{
    public ImmutableArray<Expression> Operands { get; init; } = ImmutableArray<Expression>.Empty;
    public ImmutableArray<ComparisonOperator> Operators { get; init; } = ImmutableArray<ComparisonOperator>.Empty;
}
```

Represents Python's chained comparisons: `a < b <= c`
- `Operands`: [a, b, c]
- `Operators`: [LessThan, LessThanOrEqual]

**Semantic**: `a < b <= c` means `(a < b) and (b <= c)` — but `b` is evaluated only once!

**Why separate from BinaryOp?** Chaining requires special evaluation semantics (no duplicate evaluation of shared operands).

#### 6. Advanced Expressions Region (lines 331-429)

Complex expression forms that combine multiple concepts.

**ConditionalExpression (Ternary)**
```csharp
public record ConditionalExpression : Expression
{
    public Expression Test { get; init; } = null!;
    public Expression ThenValue { get; init; } = null!;
    public Expression ElseValue { get; init; } = null!;
}
```

Represents Python's ternary: `value if test else other`
- **Note**: Python's syntax is "middle-out" (value first), unlike C's `test ? then : else`

**LambdaExpression**
```csharp
public record LambdaExpression : Expression
{
    public ImmutableArray<Parameter> Parameters { get; init; } = ImmutableArray<Parameter>.Empty;
    public Expression Body { get; init; } = null!;
}
```

Represents: `lambda x, y: x + y`
- `Parameters`: Same `Parameter` type used in function definitions (from Statement.cs:357)
- `Body`: Single expression (not a block)
- Maps to C# lambda expressions or Func<> delegates

**Type Operations** (lines 353-379)

Three distinct type-related operations:

**TypeCast** (`as` operator):
```csharp
public record TypeCast : Expression
{
    public Expression Value { get; init; } = null!;
    public TypeAnnotation TargetType { get; init; } = null!;
}
```
- Represents: `value as Type`
- Semantics: Returns None if cast fails (safe cast)

**TypeCoercion** (`to` operator):
```csharp
public record TypeCoercion : Expression
{
    public Expression Value { get; init; } = null!;
    public TypeAnnotation TargetType { get; init; } = null!;
}
```
- Represents: `value to Type` or `value to Type?`
- Semantics: Throws `InvalidCastException` on failure (for non-nullable types), returns None for nullable types
- **Critical distinction**: Unlike `as`, `to` enforces successful conversion

**TypeCheck** (`is` operator):
```csharp
public record TypeCheck : Expression
{
    public Expression Value { get; init; } = null!;
    public TypeAnnotation CheckType { get; init; } = null!;
}
```
- Represents: `value is Type` (type checking, not identity)
- Returns boolean, doesn't cast

**Parenthesized**
```csharp
public record Parenthesized : Expression
{
    public Expression Expression { get; init; } = null!;
}
```

Preserves parentheses in the AST for:
1. Precedence control: `(a + b) * c`
2. Tuple disambiguation: `(x,)` vs `x`
3. Formatting preservation

**SuperExpression**
```csharp
public record SuperExpression : Expression;
```

Represents Python's `super()` for accessing parent class methods.

**Usage restrictions** (enforced in semantic analysis):
- Only in `__init__` methods: `super().__init__(...)`
- Only in dunder methods: `super().__add__(...)`
- Only in `@override` methods: `super().method(...)`

**WalrusExpression (Assignment Expression)**
```csharp
public record WalrusExpression : Expression
{
    public string Target { get; init; } = "";
    public Expression Value { get; init; } = null!;
}
```

Represents Python 3.8+ walrus operator: `name := value`
- Assigns `value` to `name` AND returns the value
- Useful in conditions: `if (n := len(items)) > 10:`

**Note**: Target is `string`, not `Expression`, limiting to simple identifiers (matches Python's restriction).

**TryExpression**
```csharp
public record TryExpression : Expression
{
    public Expression Operand { get; init; } = null!;
    public TypeAnnotation? ExceptionType { get; init; }
}
```

**Sharpy extension** for functional error handling:
- `try expr` → wraps in `Result<T, Exception>`
- `try[ValueError] expr` → wraps in `Result<T, ValueError>`
- Returns `Ok(value)` on success, `Err(exception)` on failure

**Why?** Enables railway-oriented programming without try/catch blocks.

**MaybeExpression**
```csharp
public record MaybeExpression : Expression
{
    public Expression Operand { get; init; } = null!;
}
```

**Sharpy extension** for optional handling:
- `maybe expr` → wraps nullable expression in `Optional<T>`
- Returns `Some(value)` if non-null, `None()` if null

**Relationship to TryExpression**: `maybe` is for null safety, `try` is for exception safety.

---

## Dependencies

### Internal Dependencies
1. **Node.cs** (`src/Sharpy.Compiler/Parser/Ast/Node.cs`)
   - Base class providing ILocatable interface
   - Source location tracking (line/column)
   - TextSpan for offset-based positions

2. **Types.cs** (`src/Sharpy.Compiler/Parser/Ast/Types.cs`)
   - `TypeAnnotation`: Used in TypeCast, TypeCoercion, TypeCheck, TryExpression
   - Referenced in lambda parameters, type annotations

3. **Statement.cs** (`src/Sharpy.Compiler/Parser/Ast/Statement.cs`)
   - `Parameter` (line 357): Used in LambdaExpression

4. **Text/TextSpan** (`src/Sharpy.Compiler/Text/TextSpan.cs`)
   - Optional offset-based position tracking

### External Dependencies
- `System.Collections.Immutable`: For ImmutableArray collections
- No other external dependencies (pure data structures)

---

## Patterns and Design Decisions

### 1. Immutable Records Pattern
All expression types use C# 9.0 records with `init`-only properties:

```csharp
public record BinaryOp : Expression
{
    public BinaryOperator Operator { get; init; }
    public Expression Left { get; init; } = null!;
    public Expression Right { get; init; } = null!;
}
```

**Benefits**:
- Thread-safe AST traversal
- Enables structural equality (`==` compares values)
- Supports `with` expressions for creating modified copies
- Compiler pipeline can safely cache and share AST nodes

**Null-forgiving operator (`= null!`)**: Indicates "will be initialized by parser, trust me" — avoids nullable warnings while maintaining non-nullable guarantees.

### 2. ImmutableArray for Collections
All child node collections use `ImmutableArray<T>`:

```csharp
public ImmutableArray<Expression> Elements { get; init; } = ImmutableArray<Expression>.Empty;
```

**Why not List<T>?**
- Immutability guarantee
- Better performance for read-heavy scenarios (no defensive copying)
- Empty default prevents null reference issues

### 3. Separation of Concerns
- **AST nodes** (this file): Pure data, no behavior
- **Semantic info**: Stored separately in `SemanticInfo` dictionary (not in nodes)
- **Validation**: Handled by ValidationPipeline, not in constructors

This keeps AST nodes simple and allows multiple analysis passes without modifying the tree.

### 4. Enums for Operators
Instead of separate classes for each operator (e.g., `AddOp`, `SubtractOp`), uses enums:

```csharp
public enum BinaryOperator
{
    Add, Subtract, Multiply, // ...
}
```

**Trade-offs**:
- ✅ Simpler pattern matching
- ✅ Less type proliferation
- ❌ Can't add operator-specific data (mitigated: rare need)

### 5. Distinguishing Similar Constructs
- **TypeCast vs TypeCoercion**: `as` (safe) vs `to` (strict)
- **BinaryOp vs ComparisonChain**: Simple binary vs chained comparisons
- **SetLiteral vs DictLiteral**: Both use `{...}`, parser disambiguates
- **TupleLiteral vs Parenthesized**: Trailing comma signals tuple intent

### 6. Future-Proofing
Separate file `Expression.Future.cs` contains placeholder nodes:
- `AwaitExpression` (for async/await)
- `MatchExpression` (for pattern matching)

Keeps main file focused on currently implemented features.

---

## Debugging Tips

### 1. Visualizing Expression Trees
Use the CLI to inspect parsed AST:
```bash
dotnet run --project src/Sharpy.Cli -- emit ast myfile.spy
```

This shows the full expression tree structure.

### 2. Common Parsing Issues

**Problem**: Parser creates nested Parenthesized nodes unnecessarily
- **Check**: Look for excessive `Parenthesized(Parenthesized(...))` wrapping
- **Fix**: Parser.Primaries.cs should strip redundant parens

**Problem**: Operator precedence violations
- **Check**: BinaryOp tree structure matches expected precedence
- **Expected**: `a + b * c` → `BinaryOp(+, a, BinaryOp(*, b, c))`
- **Wrong**: `BinaryOp(*, BinaryOp(+, a, b), c)`
- **Reference**: docs/language_specification/operator_precedence.md

**Problem**: Comprehension clauses in wrong order
- **Check**: ForClause before IfClause in same nesting level
- **Example**: `[x for x in nums if x > 0]` → ForClause first, then IfClause

### 3. Type Annotation Confusion
Remember: `TypeAnnotation` (from Types.cs) is NOT an Expression!

```csharp
// ❌ Wrong - mixing types
Expression expr = new TypeAnnotation { Name = "int" };

// ✅ Correct - type operations hold TypeAnnotation separately
Expression expr = new TypeCast {
    Value = someExpr,
    TargetType = new TypeAnnotation { Name = "int" }
};
```

### 4. Null vs None
- **NoneLiteral**: Python's `None` keyword (an expression)
- **null**: C# null for uninitialized optional fields (not an expression)

### 5. Tracking Expression Origins
Use source location fields to trace back to original code:

```csharp
var expr = /* some expression from parser */;
Console.WriteLine($"Expression at {expr.LineStart}:{expr.ColumnStart}");
```

All expressions inherit location tracking from `Node`.

---

## Contribution Guidelines

### When to Add New Expression Types

**Add a new expression node when**:
1. Python has a dedicated syntax for the construct
2. The construct produces a value (not a statement)
3. It can't be represented by combining existing nodes

**Don't add when**:
- Can be desugared to existing nodes (do it in parser instead)
- It's a statement, not an expression (goes in Statement.cs)
- It's purely a type annotation (goes in Types.cs)

### Adding a New Expression Type

1. **Define the record** in the appropriate region:
```csharp
/// <summary>
/// Brief description of what this represents
/// </summary>
public record MyExpression : Expression
{
    public Expression Child { get; init; } = null!;
    public string SomeProperty { get; init; } = "";
}
```

2. **Update Parser.Expressions.cs** to parse it
3. **Update TypeChecker.Expressions.cs** to handle type inference
4. **Update RoslynEmitter.Expressions.cs** to generate C# code
5. **Add tests** in Sharpy.Compiler.Tests

### Adding New Operators

**For unary operators**:
1. Add enum value to `UnaryOperator`
2. Update Parser to recognize token
3. Update TypeChecker for operator validation
4. Update RoslynEmitter for code generation

**For binary operators**:
1. Add enum value to `BinaryOperator`
2. Update operator precedence table in Parser
3. Update TypeChecker operator validation
4. Update RoslynEmitter operator mapping

### Modifying Existing Expressions

**CRITICAL**: AST nodes are immutable by design!

**Don't**:
- Add mutable properties
- Add behavior methods (use extension methods or visitors instead)
- Store semantic information in AST nodes (use SemanticInfo)

**Do**:
- Add properties via `with` expressions for new features
- Update documentation comments
- Maintain backward compatibility where possible

### Style Guidelines

1. **Region organization**: Keep logical groupings
2. **XML comments**: Required for all public types
3. **Null-forgiving**: Use `= null!` for required properties initialized by parser
4. **Defaults**: Provide sensible defaults (`= ""`, `= ImmutableArray<T>.Empty`)
5. **Naming**: Use descriptive names matching Python terminology where applicable

---

## Cross-References

### Related AST Files
- [Node.md](Node.md) — Base Node class and Module definition
- [Statement.md](Statement.md) — Statement node types (including Parameter)
- [Types.md](Types.md) — TypeAnnotation and type-related definitions
- [Expression.Future.md](Expression.Future.md) — Future expression nodes (AwaitExpression, MatchExpression)
- [Pattern.md](Pattern.md) — Pattern matching constructs (used in MatchExpression)

### Parser Files
- `Parser.Expressions.cs` — Parses tokens into these expression nodes
- `Parser.Primaries.cs` — Handles primary expressions (identifiers, literals, calls)

### Semantic Analysis Files
- `TypeChecker.Expressions.cs` — Infers types for all expression nodes
- `ValidationPipeline.cs` — Validates expression semantics

### Code Generation Files
- `RoslynEmitter.Expressions.cs` — Converts expressions to C# Roslyn syntax trees
- `RoslynEmitter.Operators.cs` — Handles operator code generation

### Specification Documents
- `docs/language_specification/expressions.md` — Authoritative expression syntax
- `docs/language_specification/operator_precedence.md` — Operator precedence rules

---

## Key Takeaways

1. **Immutability is fundamental** — all nodes are immutable records
2. **Separation of data and behavior** — AST nodes are pure data structures
3. **Comprehensive coverage** — every Python expression form has a representation
4. **Sharpy extensions** — NullCoalesce, PipeForward, TryExpression, MaybeExpression
5. **Type safety** — Distinction between TypeCast (safe) and TypeCoercion (strict)
6. **Location tracking** — All expressions inherit source position from Node
7. **Future-ready** — Expression.Future.cs contains placeholders for upcoming features

This file is the foundation of Sharpy's expression semantics. Understanding these node types is essential for working on any part of the compiler pipeline that deals with expressions.
