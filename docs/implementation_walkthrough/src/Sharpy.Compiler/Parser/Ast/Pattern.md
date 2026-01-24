# Walkthrough: Pattern.cs

**Source File**: `src/Sharpy.Compiler/Parser/Ast/Pattern.cs`

---

## 1. Overview

`Pattern.cs` defines the AST node types for **pattern matching** in Sharpy—a powerful feature planned for v0.2.x that enables destructuring, conditional matching, and elegant control flow.

**Key Responsibilities:**
- Defines the abstract `Pattern` base class and all pattern node types
- Supports both basic patterns (wildcards, literals, bindings) and compound patterns (tuples, lists, unions)
- Provides the structure for `match` expressions and statements (defined in `Expression.Future.cs` and `Statement.Future.cs`)

**Current Status:** 🚧 **PLACEHOLDER** 🚧
- Node definitions are complete and follow Sharpy's immutable AST pattern
- Parser support is **not yet implemented**
- Target version: v0.2.x

**Role in the Compiler Pipeline:**
```
Source Code → Lexer → Parser (AST) → Semantic Analysis → Code Generation
                         ↑
                    Pattern.cs defines nodes used here
                    (when match support is added)
```

Pattern nodes will be used in two main contexts:
1. **Match Expressions** (`MatchExpression` in `Expression.Future.cs`): Pattern matching that returns a value
2. **Match Statements** (`MatchStatement` in `Statement.Future.cs`): Pattern matching for control flow

**Python Influence:**
Sharpy patterns are inspired by Python 3.10+ structural pattern matching, with adaptations for static typing:
```python
# Python example
match point:
    case (0, 0):
        print("Origin")
    case (x, 0):
        print(f"On X-axis at {x}")
    case (0, y):
        print(f"On Y-axis at {y}")
    case (x, y):
        print(f"Point at {x}, {y}")
```

---

## 2. Class/Type Structure

### 2.1 Base Type: `Pattern` (Abstract Record)

```csharp
public abstract record Pattern : Node;
```

**Purpose**: Universal base class for all pattern matching constructs.

**Design Decision - Why Inherit from `Node`?**
- Patterns have source locations (needed for error messages)
- Consistent with the rest of the AST hierarchy
- Enables uniform traversal and debugging (via `AstDumper`)

**Why a Record?**
- **Immutability**: Patterns, like all AST nodes, shouldn't change after creation
- **Structural Equality**: Two identical patterns should compare equal in tests
- **Pattern Matching**: C# records work well with C#'s own pattern matching features

**Architecture Note**: The `Pattern` hierarchy is organized into two categories:
1. **Basic Patterns** (lines 19-68): Simple, atomic matching constructs
2. **Compound Patterns** (lines 71-178): Composite patterns that contain other patterns

---

## 3. Basic Patterns

### 3.1 `WildcardPattern` - The "I Don't Care" Pattern

```csharp
public record WildcardPattern : Pattern;
```

**Python Syntax**: `_`

**Purpose**: Matches anything and discards the value.

**Use Cases:**
```python
match point:
    case (_, 0):        # Matches any x-coordinate when y is 0
        print("On X-axis")
    
    case (1, 2, _):     # Ignore third element
        print("First two are 1 and 2")
```

**Why No Properties?**
The wildcard is a singleton concept—it has no data to store. Its type alone conveys all information.

**Codegen Consideration**: When generating C# for match expressions, wildcards map to C#'s discard pattern (`_`) or an unused variable.

---

### 3.2 `BindingPattern` - Capturing Values

```csharp
public record BindingPattern : Pattern
{
    public string Name { get; init; } = "";
    public TypeAnnotation? Type { get; init; }
}
```

**Python Syntax**: `x` or `x: int` (with type annotation)

**Purpose**: Matches anything and binds the value to a variable.

**Properties:**
- `Name`: The variable name to bind to (e.g., `"x"`, `"result"`)
- `Type`: Optional type constraint (e.g., `int`, `list[str]`)

**Examples:**
```python
match response:
    case status:                    # BindingPattern(Name="status", Type=null)
        print(f"Status: {status}")
    
    case value: int:                # BindingPattern(Name="value", Type="int")
        print(f"Integer: {value}")
```

**Semantic Analysis Note**: When type checking match expressions:
1. If `Type` is provided, the semantic analyzer must verify the scrutinee (matched value) can be that type
2. The variable `Name` is introduced into the scope of the match arm's body
3. Type narrowing applies: if matched against `Option[T]`, binding to `Some(x)` narrows `x` to `T`

**Codegen Strategy**: 
- Without type: C# wildcard with variable assignment
- With type: C# type pattern: `case int x:`

---

### 3.3 `LiteralPattern` - Exact Value Matching

```csharp
public record LiteralPattern : Pattern
{
    public Expression Literal { get; init; } = null!;
}
```

**Python Syntax**: `42`, `"hello"`, `True`, `None`

**Purpose**: Matches only when the value equals a specific constant.

**Properties:**
- `Literal`: The constant expression to match (typically `IntegerLiteral`, `StringLiteral`, `BooleanLiteral`, `NoneLiteral`)

**Examples:**
```python
match status_code:
    case 200:           # LiteralPattern(Literal=IntegerLiteral("200"))
        print("OK")
    case 404:           # LiteralPattern(Literal=IntegerLiteral("404"))
        print("Not Found")
    case 500:
        print("Server Error")
```

**Why Store an `Expression` Instead of a Raw Value?**
- Reuses existing literal node types (`IntegerLiteral`, `StringLiteral`, etc.)
- Preserves source location information from the literal
- Maintains consistency with the rest of the AST design

**Type Safety**: The semantic analyzer must verify:
1. The literal type is compatible with the scrutinee type
2. Multiple case arms don't have duplicate literals (unreachable code warning)

**Codegen**: Maps to C# constant pattern: `case 200:`

---

### 3.4 `TypePattern` - Type Checking and Casting

```csharp
public record TypePattern : Pattern
{
    public TypeAnnotation Type { get; init; } = null!;
    public string? BindingName { get; init; }
}
```

**Python Syntax**: `is Type` or `is Type as name`

**Purpose**: Matches if the value is of a specific type, optionally binding it.

**Properties:**
- `Type`: The type to check against (e.g., `"int"`, `"list[str]"`)
- `BindingName`: Optional variable to bind the casted value to

**Examples:**
```python
match value:
    case is int:                    # TypePattern(Type="int", BindingName=null)
        print("It's an integer")
    
    case is str as text:            # TypePattern(Type="str", BindingName="text")
        print(f"String: {text}")
    
    case is list[int] as numbers:   # TypePattern with generic type
        print(f"List of integers: {numbers}")
```

**Semantic Analysis:**
- Verify `Type` is a valid type in the current scope
- If `BindingName` is provided, introduce a new variable with type `Type` into the match arm scope
- Check if the type test is always true/false (emit warnings for unreachable patterns)

**Type Narrowing**: This is the primary pattern for type narrowing in Sharpy:
```python
def process(value: int | str) -> None:
    match value:
        case is int as n:
            # n is narrowed to 'int' here
            print(n + 1)
        case is str as s:
            # s is narrowed to 'str' here
            print(s.upper())
```

**Codegen**: Maps to C# type pattern: `case int n:`

---

## 4. Compound Patterns

Compound patterns are recursive structures that contain other patterns, enabling sophisticated destructuring.

### 4.1 `UnionCasePattern` - Discriminated Union Matching

```csharp
public record UnionCasePattern : Pattern
{
    public TypeAnnotation? UnionType { get; init; }
    public string CaseName { get; init; } = "";
    public ImmutableArray<Pattern> FieldPatterns { get; init; } = ImmutableArray<Pattern>.Empty;
}
```

**Purpose**: Matches a specific case of a discriminated union (tagged union / algebraic data type).

**Properties:**
- `UnionType`: Optional qualified type (e.g., `Result` in `Result.Ok`)
- `CaseName`: The case variant to match (e.g., `"Ok"`, `"Err"`, `"Some"`, `"None"`)
- `FieldPatterns`: Patterns to match against the case's fields

**Example (Planned ADT Syntax):**
```python
# Union definition (from Statement.Future.cs)
union Result[T, E]:
    case Ok(value: T)
    case Err(error: E)

# Pattern matching with UnionCasePattern
match result:
    case Ok(value):                    # UnionCasePattern(CaseName="Ok", FieldPatterns=[BindingPattern("value")])
        print(f"Success: {value}")
    case Err(error):                   # UnionCasePattern(CaseName="Err", FieldPatterns=[BindingPattern("error")])
        print(f"Error: {error}")

# Nested destructuring
match result:
    case Ok(Ok(nested)):               # Nested UnionCasePatterns
        print("Doubly wrapped success")
    case Ok(Err(e)):
        print("Inner error")
```

**Singleton Cases** (no fields):
```python
union Option[T]:
    case Some(value: T)
    case None

match opt:
    case None:                         # UnionCasePattern(CaseName="None", FieldPatterns=[])
        print("Empty")
    case Some(_):                      # UnionCasePattern(CaseName="Some", FieldPatterns=[WildcardPattern])
        print("Has value")
```

**Semantic Analysis Checklist:**
1. Verify the `UnionType` (if provided) exists and is actually a union type
2. Verify `CaseName` is a valid case of that union
3. Check that the number of `FieldPatterns` matches the case's field count
4. Type check each field pattern against the corresponding field type
5. Ensure all cases are covered or there's a wildcard (exhaustiveness checking)

**Codegen Challenge**: Unions don't exist in C# natively, so codegen will likely:
- Generate a class hierarchy (base class + derived classes per case)
- Use C# pattern matching: `case Result<int, string>.Ok { Value: var v }:`

---

### 4.2 `TuplePattern` - Destructuring Tuples

```csharp
public record TuplePattern : Pattern
{
    public ImmutableArray<Pattern> Elements { get; init; } = ImmutableArray<Pattern>.Empty;
}
```

**Python Syntax**: `(pattern1, pattern2, ...)`

**Purpose**: Matches tuple structure and destructures elements.

**Properties:**
- `Elements`: Patterns for each tuple position (must match tuple arity)

**Examples:**
```python
match point:
    case (0, 0):                       # TuplePattern([LiteralPattern(0), LiteralPattern(0)])
        print("Origin")
    
    case (x, y):                       # TuplePattern([BindingPattern("x"), BindingPattern("y")])
        print(f"Point: {x}, {y}")
    
    case (1, _, 3):                    # TuplePattern with wildcard
        print("Middle element ignored")

# Nested tuples
match nested:
    case ((a, b), (c, d)):             # TuplePattern([TuplePattern(...), TuplePattern(...)])
        print(f"Nested: {a}, {b}, {c}, {d}")
```

**Semantic Analysis:**
- Verify scrutinee is actually a tuple type
- Check arity matches: `tuple[int, str]` requires exactly 2 patterns
- Type check each pattern against corresponding tuple element type

**Type Safety Example:**
```python
def process(point: tuple[int, int]) -> None:
    match point:
        case (x, y):            # OK: 2 patterns for 2-tuple
            print(x + y)
        case (x, y, z):         # ERROR: arity mismatch
            print(x + y + z)
```

**Codegen**: Maps to C# tuple pattern: `case (var x, var y):`

---

### 4.3 `ListPattern` - List Destructuring with Rest Patterns

```csharp
public record ListPattern : Pattern
{
    public ImmutableArray<Pattern> Elements { get; init; } = ImmutableArray<Pattern>.Empty;
    public Pattern? RestPattern { get; init; }
}
```

**Python Syntax**: `[pattern1, pattern2, ...]` or `[head, ...tail]`

**Purpose**: Matches list structure with optional rest/spread pattern.

**Properties:**
- `Elements`: Patterns for specific list positions
- `RestPattern`: Optional pattern for "the rest of the list" (Python's `*rest` syntax)

**Examples:**
```python
match items:
    case []:                           # ListPattern(Elements=[], RestPattern=null)
        print("Empty list")
    
    case [x]:                          # ListPattern(Elements=[BindingPattern("x")], RestPattern=null)
        print(f"Single element: {x}")
    
    case [first, second, third]:       # Exact length match (3 elements)
        print(f"Three: {first}, {second}, {third}")
    
    case [head, ...tail]:              # ListPattern(Elements=[BindingPattern("head")], RestPattern=BindingPattern("tail"))
        print(f"Head: {head}, Tail: {tail}")
    
    case [first, second, ...rest]:     # Match first two, capture rest
        print(f"First two: {first}, {second}")
```

**Semantic Analysis:**
- If `RestPattern` is null, this is an **exact length match** (list must have `Elements.Length` items)
- If `RestPattern` is present, this is a **minimum length match** (list must have at least `Elements.Length` items)
- Type check: If scrutinee is `list[T]`, each element pattern must be compatible with `T`

**Rest Pattern Semantics:**
```python
def process(items: list[int]) -> None:
    match items:
        case [head, ...tail]:
            # head: int
            # tail: list[int]
            print(f"First: {head}, Rest: {tail}")
```

**Python Alignment**: This follows Python 3.10 sequence patterns with `*` rest syntax:
```python
match [1, 2, 3, 4]:
    case [first, *rest]:    # first=1, rest=[2,3,4]
        ...
```

**Codegen Note**: C# 11+ has list patterns, but Sharpy targets C# 9.0, so codegen will likely use:
- Length check: `if (list.Count == expectedLength)`
- Indexing: `var head = list[0]; var tail = list[1..];`

---

### 4.4 `OrPattern` - Alternative Patterns

```csharp
public record OrPattern : Pattern
{
    public ImmutableArray<Pattern> Alternatives { get; init; } = ImmutableArray<Pattern>.Empty;
}
```

**Python Syntax**: `pattern1 | pattern2 | ...`

**Purpose**: Matches if **any** of the alternative patterns match.

**Properties:**
- `Alternatives`: List of alternative patterns (at least 2)

**Examples:**
```python
match value:
    case 0 | 1 | 2:                    # OrPattern([LiteralPattern(0), LiteralPattern(1), LiteralPattern(2)])
        print("Small number")
    
    case "yes" | "y" | "true":         # String alternatives
        return True
    
    case [] | None:                    # OrPattern([ListPattern([]), LiteralPattern(None)])
        print("Empty or none")

# With type patterns
match obj:
    case is int | is float:            # OrPattern([TypePattern("int"), TypePattern("float")])
        print("Numeric type")
```

**Semantic Analysis - Critical Rule:**
All alternatives must bind the **same variables** with compatible types:

```python
# ✅ Valid: both bind 'x'
match value:
    case (x, 0) | (0, x):
        print(x)

# ❌ Invalid: inconsistent bindings
match value:
    case (x, y) | (a, b):  # ERROR: different variable names
        ...
```

**Type Checking**: The result type is the **union** of alternative result types:
```python
match value:
    case is int as n | is float as n:
        # n has type: int | float
        print(n)
```

**Codegen**: Maps to multiple C# case labels:
```csharp
case 0:
case 1:
case 2:
    Console.WriteLine("Small number");
    break;
```

---

### 4.5 `AndPattern` - Conjunction Patterns

```csharp
public record AndPattern : Pattern
{
    public Pattern Left { get; init; } = null!;
    public Pattern Right { get; init; } = null!;
}
```

**Purpose**: Matches if **both** patterns match (also called "as pattern" in some languages).

**Properties:**
- `Left`: First pattern to match
- `Right`: Second pattern to match

**Example Use Case - Type Test + Binding:**
```python
match value:
    case (is int and x):               # AndPattern(Left=TypePattern("int"), Right=BindingPattern("x"))
        # Value must be int AND bind to x
        print(x + 1)
```

**Common Pattern - Multiple Bindings:**
```python
match point:
    case ((x, y) and p):               # Destructure into x, y AND bind whole tuple to p
        print(f"Point {p} has coordinates {x}, {y}")
```

**Semantic Analysis:**
- Both patterns must be compatible with the scrutinee type
- All variables from both patterns are introduced into scope
- Check for conflicting bindings (same variable name bound twice)

**Note**: Less common than `OrPattern`, but essential for advanced matching scenarios.

---

### 4.6 `GuardPattern` - Conditional Matching

```csharp
public record GuardPattern : Pattern
{
    public Pattern Inner { get; init; } = null!;
    public Expression Guard { get; init; } = null!;
}
```

**Python Syntax**: `pattern when condition`

**Purpose**: Adds a boolean condition that must be true for the pattern to match.

**Properties:**
- `Inner`: The pattern to match first
- `Guard`: Boolean expression evaluated after pattern matches

**Examples:**
```python
match value:
    case x when x > 0:                 # GuardPattern(Inner=BindingPattern("x"), Guard=BinaryOp(...))
        print("Positive")
    
    case x when x < 0:
        print("Negative")
    
    case _:
        print("Zero")

# With destructuring
match point:
    case (x, y) when x == y:           # Guard on tuple pattern
        print("On diagonal")
    
    case (x, y) when x > y:
        print("Above diagonal")
```

**Semantic Analysis:**
1. Type check `Inner` pattern first
2. Variables bound by `Inner` are in scope for `Guard` expression
3. `Guard` must have type `bool`
4. Guards can make otherwise overlapping patterns non-overlapping

**Exhaustiveness Checking Challenge:**
Guards make exhaustiveness checking undecidable:
```python
match x:
    case n when n > 0:
        ...
    case n when n <= 0:
        ...
    # Compiler can't prove these are exhaustive without SMT solver
```

**Codegen**: Maps to C# when clause:
```csharp
case var x when x > 0:
    Console.WriteLine("Positive");
    break;
```

---

## 5. Dependencies

### Internal Dependencies

1. **`Node.cs`**: Base class for all AST nodes
   - Pattern inherits location tracking from `Node`
   - See: `docs/implementation_walkthrough/src/Sharpy.Compiler/Parser/Ast/Node.md`

2. **`Expression.cs`**: For `LiteralPattern.Literal` and `GuardPattern.Guard`
   - Literal patterns reuse expression literal nodes
   - Guard expressions use the expression AST

3. **`Types.cs`**: For type annotations in `BindingPattern`, `TypePattern`, etc.
   - `TypeAnnotation` used extensively in type-constrained patterns

4. **`Expression.Future.cs`**: Defines `MatchExpression` and `MatchArm`
   - Consumers of pattern nodes
   - See related walkthrough: `docs/implementation_walkthrough/src/Sharpy.Compiler/Parser/Ast/Expression.Future.md`

5. **`Statement.Future.cs`**: Defines `MatchStatement` and `MatchCase`
   - Also consumers of pattern nodes
   - Union definitions (`UnionDef`) are the data types that `UnionCasePattern` matches against

### External Dependencies

1. **`System.Collections.Immutable`**: For `ImmutableArray<Pattern>`
   - Used in all compound patterns to store sub-patterns
   - Ensures immutability of pattern trees

2. **`Sharpy.Compiler.Text`**: For `TextSpan` location tracking (inherited from `Node`)

---

## 6. Patterns and Design Decisions

### 6.1 Immutable AST Design

**Decision**: All patterns are immutable records with `init`-only properties.

**Rationale:**
- **Thread Safety**: Multiple compilation threads can share AST nodes
- **Predictability**: Once created, a pattern cannot change
- **Easier Reasoning**: No hidden mutations to track

**Implication**: To "modify" a pattern, use C# `with` expressions:
```csharp
var newPattern = oldPattern with { CaseName = "NewCase" };
```

### 6.2 Recursive Pattern Structure

**Decision**: Compound patterns (like `TuplePattern`) contain `ImmutableArray<Pattern>`, enabling arbitrary nesting.

**Rationale:**
- Supports complex matching: `case [[a, b], [c, d]]:`
- Uniform traversal: recursive visitors work naturally
- Mirrors Python's nested pattern syntax

**Implementation Note**: When traversing patterns (e.g., in semantic analysis), use recursive descent or the visitor pattern.

### 6.3 Separation of Pattern Matching Components

**Decision**: Pattern definitions are in `Pattern.cs`, but match expressions/statements are in `*.Future.cs` files.

**Rationale:**
- **Modularity**: Pattern nodes can be developed/tested independently
- **Phased Implementation**: Patterns are ready; parser integration comes later
- **Clear Scope**: Each file has a single responsibility

**Navigation Guide:**
- **Pattern nodes** (this file): `Pattern.cs`
- **Match expressions**: `Expression.Future.cs` → `MatchExpression`, `MatchArm`
- **Match statements**: `Statement.Future.cs` → `MatchStatement`, `MatchCase`
- **Union types** (matched by patterns): `Statement.Future.cs` → `UnionDef`

### 6.4 Why `Expression` for `LiteralPattern.Literal`?

**Decision**: Store literals as `Expression` nodes rather than raw values.

**Alternatives Considered:**
1. Store raw `object?` values ❌
2. Create separate `PatternLiteral` hierarchy ❌
3. Reuse `Expression` literals ✅ (chosen)

**Rationale:**
- **Reuse**: Leverages existing well-tested literal nodes
- **Location Tracking**: Expression nodes already have source locations
- **Type Information**: `IntegerLiteral.Suffix` preserves type hints (L, UL, etc.)

### 6.5 Optional vs. Required Properties

**Design Pattern**: Properties are marked `= null!` (non-nullable but unchecked) or nullable (`?`):

```csharp
// Required: Compiler error if not set
public Pattern Inner { get; init; } = null!;

// Optional: Can be null
public Pattern? RestPattern { get; init; }
```

**Rationale:**
- Makes intent clear in type signatures
- Required properties failing to initialize is a programming error (parser bug)
- Optional properties have semantic meaning when null

---

## 7. Debugging Tips

### 7.1 Using `AstDumper` to Inspect Patterns

When implementing parser support for patterns, use `AstDumper` to visualize the pattern tree:

```csharp
var pattern = new TuplePattern {
    Elements = ImmutableArray.Create(
        new BindingPattern { Name = "x" },
        new WildcardPattern()
    )
};

var dumper = new AstDumper();
var dump = dumper.Dump(pattern);
Console.WriteLine(dump);
```

### 7.2 Common Pattern Matching Bugs

**Issue 1: Incorrect Arity**
```python
match (1, 2):
    case (x, y, z):  # Arity mismatch: 2-tuple vs 3 patterns
```
**Semantic Error Location**: `TypeChecker.cs` → tuple pattern checking

**Issue 2: Inconsistent Or Pattern Bindings**
```python
match value:
    case (x, 0) | (0, y):  # x vs y: different names
```
**Semantic Error Location**: `ValidationPipeline.cs` → or pattern validator (future)

**Issue 3: Missing Required Fields**
```python
match result:
    case Ok():  # Error: Ok requires 'value' field
```
**Semantic Error Location**: `TypeChecker.cs` → union case pattern checking

### 7.3 Testing Pattern Nodes

Since parser support isn't implemented yet, test pattern nodes directly:

```csharp
[Fact]
public void TestLiteralPattern_StoresCorrectValue()
{
    var literal = new IntegerLiteral { Value = "42" };
    var pattern = new LiteralPattern { Literal = literal };
    
    Assert.IsType<IntegerLiteral>(pattern.Literal);
    Assert.Equal("42", ((IntegerLiteral)pattern.Literal).Value);
}
```

### 7.4 Semantic Analysis Debug Strategy

When semantic analysis for patterns is implemented:

1. **Break on pattern type**: Set conditional breakpoints in `TypeChecker.Visit(Pattern p)`
2. **Trace pattern traversal**: Log each pattern node visited
3. **Check binding scope**: Verify variables are added to symbol tables correctly
4. **Validate type narrowing**: Print inferred types for each match arm

---

## 8. Contribution Guidelines

### 8.1 Adding a New Pattern Type

If you need to add a new pattern (unlikely, as the current set is comprehensive):

1. **Define the record** in the appropriate section (Basic or Compound)
2. **Add XML documentation** explaining purpose and examples
3. **Update `AstDumper`** to handle the new pattern type (in `Parser/AstDumper.cs`)
4. **Plan semantic analysis**: What type checks are needed?
5. **Plan codegen**: How does this map to C# patterns?
6. **Add tests**: Unit tests for the node structure

**Example Template:**
```csharp
/// <summary>
/// My new pattern - brief description.
/// </summary>
/// <example>
/// match value:
///     case my_pattern:
///         ...
/// </example>
public record MyNewPattern : Pattern
{
    public SomeType Property { get; init; } = null!;
}
```

### 8.2 Implementing Parser Support (v0.2.x Task)

When implementing the parser for pattern matching:

1. **Start with basic patterns** (wildcard, binding, literal)
2. **Add syntax rules** in `Parser.cs`:
   ```csharp
   private Pattern ParsePattern()
   {
       if (Match(TokenType.Underscore)) return new WildcardPattern { ... };
       if (Match(TokenType.Identifier)) return ParseBindingOrType();
       // ...
   }
   ```
3. **Handle precedence** for `|` (or) and `and` patterns
4. **Add tests** in `Sharpy.Compiler.Tests/Parser/PatternTests.cs`
5. **Update `AstDumper`** to pretty-print patterns

### 8.3 Implementing Semantic Analysis (v0.2.x Task)

Pattern type checking will likely live in `Semantic/TypeChecker.cs`:

1. **Add pattern visitor methods**:
   ```csharp
   private void CheckPattern(Pattern pattern, TypeInfo scrutineeType)
   {
       switch (pattern)
       {
           case WildcardPattern: return;  // Always matches
           case BindingPattern bp: CheckBindingPattern(bp, scrutineeType); break;
           case LiteralPattern lp: CheckLiteralPattern(lp, scrutineeType); break;
           // ...
       }
   }
   ```
2. **Implement exhaustiveness checking** (ensure all cases are covered)
3. **Track bindings** introduced by patterns (add to symbol table)
4. **Handle type narrowing** (crucial for `TypePattern`)

### 8.4 Code Style Guidelines

When working with Pattern.cs:

- **Keep patterns immutable**: Use `init` for all properties
- **Use `ImmutableArray`** for collections, never `List` or arrays
- **Preserve source locations**: All pattern nodes inherit from `Node`, so initialize location properties
- **Document with examples**: XML docs should include Python code examples
- **Follow naming conventions**: Pattern types end with `Pattern` (e.g., `TuplePattern`, not `Tuple`)

### 8.5 Common Modifications

**Likely Changes:**
- Adding default values to optional properties
- Updating XML documentation with more examples
- Adjusting property types based on semantic analysis needs

**Unlikely Changes:**
- Adding mutable state (violates immutable AST principle)
- Removing pattern types (all current patterns are planned features)
- Changing base class (must remain `Pattern : Node`)

---

## 9. Cross-References

### Related AST Files

- **[`Node.md`](Node.md)**: Base `Node` record documentation
- **[`Expression.md`](Expression.md)**: Expression nodes used in patterns
- **[`Expression.Future.md`](Expression.Future.md)**: `MatchExpression` consumer of patterns
- **[`Statement.Future.md`](Statement.md)**: `MatchStatement` consumer of patterns (coming soon)
- **[`Types.md`](Types.md)**: `TypeAnnotation` used in type patterns

### Related Compiler Components

- **Parser**: `docs/implementation_walkthrough/src/Sharpy.Compiler/Parser/Parser.md`
  - Where pattern parsing will be implemented
  
- **Semantic Analysis**: `docs/implementation_walkthrough/src/Sharpy.Compiler/Semantic/TypeChecker.md`
  - Where pattern type checking will be implemented
  
- **Code Generation**: `docs/implementation_walkthrough/src/Sharpy.Compiler/CodeGen/RoslynEmitter.md`
  - Where patterns will be translated to C# patterns

### Language Specification

- **Pattern Matching Spec**: `docs/language_specification/pattern_matching.md` (planned)
  - Formal specification of pattern semantics
  
- **Union Types Spec**: `docs/language_specification/union_types.md` (planned)
  - Specification for discriminated unions matched by `UnionCasePattern`

### Testing

- **Parser Tests**: `src/Sharpy.Compiler.Tests/Parser/PatternTests.cs` (future)
- **Semantic Tests**: `src/Sharpy.Compiler.Tests/Semantic/PatternTypeCheckingTests.cs` (future)
- **Integration Tests**: `src/Sharpy.Compiler.Tests/Integration/TestFixtures/pattern_matching/` (future)

---

## 10. Future Work (v0.2.x and Beyond)

### Planned Implementation Phases

**Phase 1: Basic Pattern Parsing (v0.2.0)**
- Implement parser for `WildcardPattern`, `BindingPattern`, `LiteralPattern`
- Add match expression parsing (`match x: case ...`)
- Basic semantic analysis (type checking, binding introduction)

**Phase 2: Compound Patterns (v0.2.1)**
- Parser support for `TuplePattern`, `ListPattern`
- Recursive pattern type checking
- Exhaustiveness checking for simple cases

**Phase 3: Advanced Patterns (v0.2.2)**
- `OrPattern`, `AndPattern`, `GuardPattern`
- Complex exhaustiveness checking
- Pattern optimization (eliminate redundant checks)

**Phase 4: Union Types and Union Patterns (v0.2.3)**
- `UnionDef` parsing and codegen
- `UnionCasePattern` semantic analysis
- Full exhaustiveness checking with union case coverage

### Known Challenges

1. **Exhaustiveness Checking**: Proving match arms cover all cases is complex
   - Heuristic approach vs. SMT solver integration
   - Guard patterns make full coverage undecidable

2. **C# Target Limitations**: C# 9.0 doesn't have all pattern features
   - List patterns added in C# 11
   - May need to emit explicit if/else chains

3. **Type Inference**: Patterns introduce variables without explicit types
   - Must infer from context: `case (x, y):` → infer from tuple element types

4. **Performance**: Pattern matching can generate complex branching code
   - Decision tree optimization for large match expressions

---

## Summary

`Pattern.cs` is a **forward-looking** AST module that defines the infrastructure for Sharpy's pattern matching feature. While parser support is pending, the node definitions are complete and ready for implementation.

**Key Takeaways:**
- ✅ 10 pattern types covering simple to complex matching scenarios
- ✅ Immutable record-based design consistent with Sharpy's AST philosophy
- ✅ Python-inspired syntax with static typing enhancements
- 🚧 Parser, semantic, and codegen implementation pending (v0.2.x)

**Next Steps for Contributors:**
1. Review `Expression.Future.cs` and `Statement.Future.cs` for pattern consumers
2. Study Python 3.10+ pattern matching for semantic reference
3. Check C# 9.0 pattern capabilities for codegen constraints
4. Plan parser implementation starting with basic patterns

For questions about pattern matching design, consult:
- Python PEP 634 (Structural Pattern Matching: Specification)
- Language design discussions in GitHub issues
- The `#patterns` tag in project documentation
