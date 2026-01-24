# Walkthrough: RoslynEmitter.Operators.cs

**Source File**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.Operators.cs`

---

## Overview

This partial class file contains operator-related code generation logic for the Sharpy compiler's code generation phase. It's responsible for:

1. **Operator Overloading**: Translating Python dunder methods (`__add__`, `__eq__`, etc.) into C# operator overloads (`operator +`, `operator ==`, etc.)
2. **Special Expression Forms**: Generating code for Sharpy-specific constructs like `try` expressions and `maybe` expressions
3. **Utility Methods**: Helper functions for loop transformations, dependency analysis, and type checking

This file bridges the gap between Python's operator protocol (dunder methods) and C#'s static operator overloading system, ensuring that Sharpy classes can use natural operator syntax while maintaining Python semantics.

### Role in the Compiler Pipeline

**Position**: Final code generation phase
**Input**: Typed AST nodes from semantic analysis
**Output**: Roslyn `OperatorDeclarationSyntax` and related C# constructs
**Upstream**: Receives validated AST with type information from `SemanticAnalyzer`
**Downstream**: Produces C# syntax trees that get compiled to .NET IL

---

## Class Structure

This is a **partial class** extending `RoslynEmitter`. The main `RoslynEmitter` class is defined in `RoslynEmitter.cs`, with functionality split across multiple files:

- `RoslynEmitter.cs` - Core class definition, constructor, shared state
- `RoslynEmitter.Operators.cs` - **This file** - Operator overloads and special expressions
- `RoslynEmitter.Expressions.cs` - General expression generation
- `RoslynEmitter.Statements.cs` - Statement generation
- `RoslynEmitter.ClassMembers.cs` - Class member generation
- `RoslynEmitter.TypeDeclarations.cs` - Type declaration generation
- And others...

### Key Dependencies

```csharp
private readonly CodeGenContext _context;      // Semantic information lookup
private readonly TypeMapper _typeMapper;       // Python → C# type mapping
private int _tempVarCounter;                    // Unique temp variable generation
```

These fields are defined in the main `RoslynEmitter.cs` file and shared across all partial class files.

---

## Key Functions and Methods

### 1. Dunder Method Detection

#### `ShouldGenerateDunderMethod(string dunderName)` (lines 21-30)

**Purpose**: Determines whether a Python dunder method should generate a corresponding C# method.

**Key Logic**:
- `__init__` always generates a method (becomes a constructor elsewhere)
- Protocol dunders registered in `ProtocolRegistry` generate methods (e.g., `__str__`, `__len__`, `__iter__`)
- Most other dunders do NOT generate methods to avoid conflicts with user-defined methods

**Why This Matters**: Python allows both `def __add__(self, other)` as a method AND operator usage `a + b`. In C#, operator overloads are separate from methods. This function decides which dunders become methods vs. operators.

```csharp
// Example:
// __add__ → Generates operator+ (TryGenerateOperatorOverload)
// __str__ → Generates ToString() method (ProtocolRegistry mapping)
// __custom__ → No generation (user-defined dunder)
```

---

### 2. Operator Overload Generation

#### `TryGenerateOperatorOverload(FunctionDef funcDef, string className)` (lines 35-73)

**Purpose**: Main dispatcher that routes dunder methods to their corresponding C# operator overload generators.

**Supported Operators**:

| Python Dunder | C# Operator | Generator Method |
|---------------|-------------|------------------|
| `__add__` | `operator +` | `GenerateBinaryOperator` |
| `__sub__` | `operator -` | `GenerateBinaryOperator` |
| `__mul__` | `operator *` | `GenerateBinaryOperator` |
| `__truediv__` | `operator /` | `GenerateBinaryOperator` |
| `__mod__` | `operator %` | `GenerateBinaryOperator` |
| `__and__` | `operator &` | `GenerateBinaryOperator` |
| `__or__` | `operator \|` | `GenerateBinaryOperator` |
| `__xor__` | `operator ^` | `GenerateBinaryOperator` |
| `__lshift__` | `operator <<` | `GenerateBinaryOperator` |
| `__rshift__` | `operator >>` | `GenerateBinaryOperator` |
| `__eq__` | `operator ==` | `GenerateComparisonOperator` |
| `__ne__` | `operator !=` | `GenerateComparisonOperator` |
| `__lt__` | `operator <` | `GenerateComparisonOperator` |
| `__le__` | `operator <=` | `GenerateComparisonOperator` |
| `__gt__` | `operator >` | `GenerateComparisonOperator` |
| `__ge__` | `operator >=` | `GenerateComparisonOperator` |
| `__neg__` | `operator -` (unary) | `GenerateUnaryOperator` |
| `__pos__` | `operator +` (unary) | `GenerateUnaryOperator` |
| `__invert__` | `operator ~` | `GenerateUnaryOperator` |

**Not Supported as Operators**:
- `__pow__` - No `**` operator in C#, use `Math.Pow()` method instead
- `__getitem__` / `__setitem__` - Require indexer syntax, not operator syntax

**Return Value**: `MemberDeclarationSyntax?` - Returns `null` if the dunder cannot be represented as an operator.

---

#### `GenerateBinaryOperator(...)` (lines 78-123)

**Purpose**: Generates C# operator overloads for binary operators (`+`, `-`, `*`, `/`, etc.)

**Generated Pattern**:
```csharp
// From Python:
// class Vector:
//     def __add__(self, other: Vector) -> Vector:
//         # ... implementation

// Generates C#:
public static Vector operator +(Vector left, Vector right)
{
    return left.Add(right);
}
```

**Key Details**:
1. **Parameter Handling**: Filters out `self` parameter, uses the second parameter for `right` type
2. **Name Mangling**: Transforms `__add__` → `Add` using `NameMangler.Transform(funcDef.Name, NameContext.Method)`
3. **Type Resolution**:
   - Return type defaults to class type if not specified
   - Right operand type comes from parameter annotation or defaults to class type
4. **Delegation Pattern**: The operator overload **delegates** to the actual method implementation (`left.Add(right)`)

**Why Delegation?** This keeps the actual implementation logic in the method, making it accessible both via operators and explicit method calls if needed.

---

#### `GenerateComparisonOperator(...)` (lines 128-169)

**Purpose**: Generates comparison operators (`==`, `!=`, `<`, `<=`, `>`, `>=`)

**Key Difference from Binary Operators**: Always returns `bool`, regardless of what the Python method's return type annotation says.

```csharp
// From Python:
// def __eq__(self, other: Point) -> bool:
//     return self.x == other.x and self.y == other.y

// Generates C#:
public static bool operator ==(Point left, Point right)
{
    return left.Eq(right);
}
```

**Important**: C# requires comparison operators to return `bool`, so this overrides any other return type annotation.

---

#### `GenerateUnaryOperator(...)` (lines 174-202)

**Purpose**: Generates unary operators (`-`, `+`, `~`)

**Generated Pattern**:
```csharp
// From Python:
// def __neg__(self) -> Vector:
//     return Vector(-self.x, -self.y)

// Generates C#:
public static Vector operator -(Vector value)
{
    return value.Neg();
}
```

**Parameter Handling**: Only has `self`, which becomes the `value` parameter in C#.

---

### 3. Complementary Equality Operators

C# has a strict requirement: if you define `operator ==`, you **must** also define `operator !=`, and vice versa. The compiler will issue a warning (CS0660/CS0661) otherwise.

#### `GenerateComplementaryEqualsOperator(string className)` (lines 207-229)

Generates `operator ==` when only `__ne__` is defined in Python.

**Implementation**: `operator == → !(left != right)`

#### `GenerateComplementaryNotEqualsOperator(string className)` (lines 234-256)

Generates `operator !=` when only `__eq__` is defined in Python.

**Implementation**: `operator != → !(left == right)`

**Usage Context**: These methods are called from `RoslynEmitter.ClassMembers.cs` when processing class definitions to ensure C# requirements are met.

---

### 4. Special Sharpy Expression Forms

#### `GenerateTryExpression(TryExpression tryExpr)` (lines 262-300)

**Purpose**: Generates code for Sharpy's `try` expression syntax, which wraps risky operations in a `Result<T, E>` type.

**Sharpy Syntax**:
```python
# Simple try expression (catches all exceptions)
result = try risky_operation()

# Typed try expression (catches specific exception)
result = try[ValueError] parse_int(input)
```

**Generated C# Code**:
```csharp
// Simple form:
var result = global::Sharpy.Core.Result.Try(() => risky_operation());

// Typed form:
var result = global::Sharpy.Core.Result.Try<ValueError>(() => parse_int(input));
```

**Key Details**:
1. Wraps the operand in a parameterless lambda `() => expr`
2. Uses fully qualified `global::Sharpy.Core.Result` to avoid namespace conflicts
3. Exception type defaults to `Exception` if not specified
4. Returns a `Result<T, E>` that must be pattern-matched or unwrapped

**Design Philosophy**: This follows Sharpy's "railway-oriented programming" approach, making error handling explicit without try/catch boilerplate.

---

#### `GenerateMaybeExpression(MaybeExpression maybeExpr)` (lines 306-319)

**Purpose**: Converts nullable expressions into Sharpy's `Optional<T>` type.

**Sharpy Syntax**:
```python
# Convert nullable to Optional
maybe_value = maybe get_nullable_string()
```

**Generated C# Code**:
```csharp
var maybe_value = global::Sharpy.Core.Optional.From(get_nullable_string());
```

**Purpose**: Bridges between .NET's nullable reference types (`T?`) and Sharpy's explicit `Optional<T>` type for safer null handling.

---

### 5. Utility Methods

#### `GenerateTempVarName(string prefix)` (lines 324-327)

**Purpose**: Generates unique temporary variable names to avoid collisions.

**Pattern**: `__{prefix}_{counter++}`

**Example**: `__temp_0`, `__temp_1`, `__loopFlag_0`

---

#### Loop Else Support (lines 333-379)

Python's `for`/`while` loops support an `else` clause that executes only if the loop completes naturally (no `break`):

```python
for item in items:
    if item == target:
        break
else:
    print("Not found")  # Only runs if no break occurred
```

**Challenge**: C# has no equivalent construct.

**Solution**: Transform the loop body to track whether a `break` occurred using a flag.

##### `TransformLoopBodyForElse(...)` (lines 333-341)

Transforms all statements in a loop body to support else clause tracking.

##### `TransformStatementForLoopElse(Statement stmt, string flagName)` (lines 347-379)

**Transformation Strategy**:
1. **Break Statements**: Transform `break` → `{ flag = false; break; }`
2. **If Statements**: Recursively transform nested if/elif/else bodies
3. **Nested Loops**: Do NOT transform (nested loops have their own break semantics)
4. **Other Statements**: Pass through unchanged

**Generated Pattern**:
```csharp
bool __loopCompleted_0 = true;
while (condition)
{
    if (should_break)
    {
        __loopCompleted_0 = false;
        break;
    }
}
if (__loopCompleted_0)
{
    // else clause code
}
```

**Special AST Node**: Uses `BreakWithFlagStatement` (defined elsewhere) to represent the transformed break.

---

#### `IsFloatExpression(Expression expr)` (lines 385-407)

**Purpose**: Determines if an expression evaluates to a floating-point type at compile time.

**Why This Matters**: Floor division (`//`) has different semantics and return types depending on operand types:
- Integer operands: `(int)Math.Floor((double)a / b)` → returns `int32`
- Float operands: `Math.Floor(a / b)` → returns `double`

**Detection Logic**:
1. **Literal**: `FloatLiteral` → `true`
2. **Division/Power**: Always produces float (Python semantics)
3. **Floor Division**: Recursive check on operands
4. **Other Binary Ops**: Float if either operand is float
5. **Unary/Parenthesized**: Recursive check
6. **Default**: Assumes integer (conservative for static analysis)

**Limitation**: This is a static, syntactic analysis. It doesn't use full type inference, so complex expressions (variables, function calls) default to integer. The semantic analysis phase would have more accurate type information.

---

#### `GenerateFloorDivision(...)` (lines 417-444)

**Purpose**: Implements Python's floor division (`//`) with correct semantics.

**Python Floor Division Semantics**: Rounds toward **negative infinity**, not toward zero (truncation).

```python
# Python:
7 // 2   →  3
-7 // 2  → -4  (not -3!)
```

**C# Implementation Strategy**:
```csharp
// Integer operands:
(int)System.Math.Floor((double)(left / right))

// Float operands:
System.Math.Floor((double)(left / right))
```

**Important Details**:

1. **Double Cast**: Always casts to `double` to avoid `CS0121` ambiguity between `Math.Floor(double)` and `Math.Floor(decimal)`
2. **Fully Qualified Name**: Uses `System.Math` not `Math` to avoid conflicts with potential `Sharpy.Math` namespace
3. **Pragmatic Return Type**:
   - Integer operands → `int32` (spec says `int64`, but `int32` is more .NET-idiomatic)
   - Float operands → `double`
4. **Comments in Code**: Extensive comments explain the deviation from spec for pragmatic .NET compatibility

**Design Trade-off**: The spec calls for `int64` return type for integer floor division, but this implementation returns `int32` for better .NET interop (most variables are `int`, augmented assignment works naturally).

---

#### `IsEnumTypeExpression(Expression expr)` (lines 450-463)

**Purpose**: Determines if an expression refers to an enum type variable.

**Use Case**: When accessing `.value` on an enum, should translate to an `int` cast in C#:

```python
# Sharpy:
color = Color.RED
value = color.value  # Get underlying int value

# C#:
var color = Color.RED;
var value = (int)color;
```

**Implementation**:
1. Checks if expression is an `Identifier`
2. Looks up symbol in semantic context
3. Checks if symbol is a variable with `UserDefinedType` of `TypeKind.Enum`

**Limitation**: Only handles simple identifier expressions, not complex enum expressions.

---

#### `CollectReferencedIdentifiers(...)` (lines 470-585)

**Purpose**: Recursively walks an expression tree to collect all identifier names referenced.

**Use Case**: **Module-level variable dependency analysis**. Determines whether a variable declaration should be:
- A **module-level field** (if it references other module-level variables)
- A **local variable in Main** (if it only uses local values)

**Coverage**: Handles all expression types:
- Simple: `Identifier`, literals
- Compound: `BinaryOp`, `UnaryOp`, `FunctionCall`
- Collections: `ListLiteral`, `DictLiteral`, `SetLiteral`, `TupleLiteral`
- Advanced: Comprehensions, f-strings, lambda expressions, conditionals
- Access: `MemberAccess`, `IndexAccess`, `SliceAccess`

**Example**:
```python
# Module level:
PI = 3.14159
radius = 5
area = PI * radius * radius  # References PI and radius

# CollectReferencedIdentifiers(area's initializer) → {"PI", "radius"}
# Conclusion: area must be a field, not a local in Main
```

**Recursive Strategy**: Each expression type handler recursively calls itself on sub-expressions, building up a set of all referenced identifiers.

---

## Dependencies

### Internal Sharpy Dependencies

1. **Sharpy.Compiler.Parser.Ast**: All AST node types (`FunctionDef`, `Expression`, `Statement`, etc.)
2. **Sharpy.Compiler.Semantic**:
   - `CodeGenContext` - Symbol table lookup
   - `ProtocolRegistry` - Dunder method protocol mappings
   - `TypeKind`, `VariableSymbol`, `UserDefinedType` - Type information
3. **Sharpy.Compiler.CodeGen.NameMangler**: Transforms Python names to C# names (`__add__` → `Add`)
4. **Sharpy.Compiler.CodeGen.TypeMapper**: Maps Python type annotations to C# types

### External Dependencies

1. **Microsoft.CodeAnalysis.CSharp**: Roslyn API for building C# syntax trees
   - `SyntaxFactory` static methods for creating nodes
   - `OperatorDeclarationSyntax`, `ExpressionSyntax`, etc.
2. **System.Collections.Immutable**: `ImmutableArray` for immutable collections

---

## Patterns and Design Decisions

### 1. **SyntaxFactory Exclusively**

**Critical Rule**: RoslynEmitter uses `SyntaxFactory` methods exclusively, **never** string templating.

```csharp
// CORRECT:
var expr = BinaryExpression(
    SyntaxKind.AddExpression,
    IdentifierName("left"),
    IdentifierName("right"));

// WRONG (not used in Sharpy):
var code = $"{left} + {right}";
```

**Why**: SyntaxFactory produces properly structured, type-safe syntax trees that Roslyn can analyze and transform. String templating is brittle and error-prone.

---

### 2. **Delegation Pattern for Operators**

Generated operator overloads **delegate** to the actual method implementation:

```csharp
// Operator overload (generated):
public static Vector operator +(Vector left, Vector right)
{
    return left.Add(right);  // Delegates to method
}

// Method implementation (generated from __add__ body):
public Vector Add(Vector right)
{
    // Actual implementation here
}
```

**Benefits**:
- Keeps implementation in one place
- Allows explicit method calls: `v1.Add(v2)` if needed
- Follows C# best practices for operator overloading

---

### 3. **Immutable AST with Transformations**

**Axiom**: The AST is immutable. Annotations go in `SemanticInfo`, not AST nodes.

However, loop else support requires AST transformation. How is this reconciled?

**Solution**: Use `with` expressions (C# record syntax) to create modified copies:

```csharp
IfStatement ifStmt => ifStmt with
{
    ThenBody = TransformLoopBodyForElse(ifStmt.ThenBody, flagName),
    // ... creates a new IfStatement with modified bodies
}
```

This preserves immutability while allowing transformations during code generation.

---

### 4. **Pragmatic .NET Axiom Over Spec Purity**

**Example**: Floor division return type

- **Spec says**: Integer floor division should return `int64`
- **Implementation returns**: `int32`
- **Reason**: Most .NET code uses `int` (alias for `int32`), so returning `int32` enables smoother interop, augmented assignment, and variable declarations without constant type conflicts

**Axiom Hierarchy** (from CLAUDE.md):
1. .NET Compatibility (highest priority)
2. Type Safety
3. Python Syntax Fidelity (lowest priority)

When these conflict, .NET wins.

---

### 5. **Fully Qualified Names for Runtime Support**

Always uses `global::Sharpy.Core.Result` not just `Result`:

```csharp
IdentifierName("global::Sharpy.Core.Result")  // Safe
IdentifierName("Result")                       // Risky - could conflict with user types
```

**Why**: User code might define their own `Result` type. Fully qualified names avoid ambiguity.

---

### 6. **Temporary Variable Naming Convention**

Pattern: `__{purpose}_{counter}`

**Examples**: `__temp_0`, `__loopCompleted_1`, `__flagName_2`

**Why Double Underscore**: Python reserves single underscore prefixes for weak internal symbols. Double underscore is Sharpy's convention for compiler-generated identifiers, making them obviously non-user-defined.

---

## Debugging Tips

### 1. **Use the `/emit` Command**

When debugging operator generation issues:

```bash
dotnet run --project src/Sharpy.Cli -- emit csharp file.spy
```

This shows the generated C# code, letting you see exactly what operator overloads were created.

---

### 2. **Common Operator Issues**

**Problem**: Operator not being generated

**Checks**:
1. Is the dunder method name spelled correctly? (`__add__` not `__Add__`)
2. Is it in the `TryGenerateOperatorOverload` switch statement?
3. Does it have the right parameter count?
4. Check if it's excluded (like `__pow__`, `__getitem__`)

---

### 3. **Floor Division Confusion**

**Symptom**: Floor division produces unexpected results

**Debug Steps**:
1. Check `IsFloatExpression` result - is it detecting float/int correctly?
2. Verify the generated C# uses `System.Math.Floor`
3. Confirm the division is happening in `double` space before flooring
4. Test with negative numbers: `-7 // 2` should be `-4` not `-3`

---

### 4. **Loop Else Not Working**

**Symptom**: Else clause executes when it shouldn't (or vice versa)

**Debug Steps**:
1. Check that `TransformLoopBodyForElse` is being called
2. Verify `BreakWithFlagStatement` nodes are generated
3. Look for nested loops - are they being skipped correctly?
4. Check flag variable naming - are there collisions?

---

### 5. **Complementary Operators Missing**

**Symptom**: C# compiler warning CS0660/CS0661

**Issue**: Defined `operator ==` without `operator !=` (or vice versa)

**Fix**: Ensure `GenerateComplementaryEqualsOperator` or `GenerateComplementaryNotEqualsOperator` is called when processing class members.

---

### 6. **Setting Breakpoints**

Key breakpoints for debugging:

- `TryGenerateOperatorOverload:37` - Entry point for all operators
- `GenerateBinaryOperator:78` - Binary operator generation
- `GenerateFloorDivision:417` - Floor division logic
- `TransformStatementForLoopElse:347` - Loop else transformation
- `CollectReferencedIdentifiers:470` - Dependency analysis

---

## Contribution Guidelines

### What Changes Might Be Made to This File?

1. **Adding New Operator Support**
   - Add case to `TryGenerateOperatorOverload` switch
   - Call appropriate generator (`GenerateBinaryOperator`, etc.)
   - Update operator documentation

2. **Fixing Operator Semantics**
   - Modify generator methods to adjust parameter types, return types, or delegation patterns
   - Ensure delegation still calls the correct mangled method name

3. **Adding New Special Expression Forms**
   - Add new `GenerateXxxExpression` methods
   - Call from `GenerateExpression` in `RoslynEmitter.Expressions.cs`
   - Follow the pattern of wrapping in Sharpy.Core runtime types

4. **Improving Type Detection**
   - Enhance `IsFloatExpression` to use semantic type information instead of syntactic patterns
   - Similar improvements to `IsEnumTypeExpression`

5. **Optimizing Generated Code**
   - Example: Inline simple operator implementations instead of delegating
   - Trade-off: Complexity vs. performance

### Testing Your Changes

When modifying this file:

1. **Unit Tests**: Look in `src/Sharpy.Compiler.Tests/CodeGen/`
2. **Integration Tests**: File-based tests in `src/Sharpy.Compiler.Tests/Integration/TestFixtures/`
3. **Run Operator Tests**:
   ```bash
   dotnet test --filter "FullyQualifiedName~Operator"
   ```

4. **Check Generated C#**:
   ```bash
   dotnet run --project src/Sharpy.Cli -- emit csharp test.spy
   ```

5. **Verify Axiom Compliance**: Use `/project:check-axioms` for major changes

---

## Cross-References

### Related Partial Class Files

This file is part of the `RoslynEmitter` partial class. Related files:

- [RoslynEmitter.cs](RoslynEmitter.md) - Main class definition and shared state
- [RoslynEmitter.Expressions.md](RoslynEmitter.Expressions.md) - General expression generation (calls methods from this file)
- [RoslynEmitter.ClassMembers.md](RoslynEmitter.ClassMembers.md) - Processes dunder methods in classes, calls `TryGenerateOperatorOverload`
- [RoslynEmitter.Statements.md](RoslynEmitter.Statements.md) - Statement generation, uses loop transformation utilities

### Dependencies

- `NameMangler.cs` - Transforms `__add__` → `Add`, `snake_case` → `PascalCase`
- `TypeMapper.cs` - Maps Python type annotations to C# types
- `ProtocolRegistry.cs` - Defines which dunders map to .NET protocols (`__str__` → `ToString()`)
- `CodeGenContext.cs` - Provides symbol table access for semantic lookups

### Language Specifications

- `docs/language_specification/operator_overloading.md` - Operator overloading rules
- `docs/language_specification/arithmetic_operators.md` - Arithmetic operator semantics
- `docs/language_specification/operator_precedence.md` - Operator precedence rules
- `docs/language_specification/dotnet_interop.md` - .NET interop guidelines

---

## Summary

`RoslynEmitter.Operators.cs` is a critical bridge between Python's dynamic operator protocol and C#'s static operator overloading system. It:

- Translates dunder methods into C# operator overloads using a delegation pattern
- Handles special Sharpy constructs like `try` and `maybe` expressions
- Provides utilities for loop transformation (else clause support) and dependency analysis
- Makes pragmatic decisions favoring .NET compatibility over spec purity when necessary

Understanding this file is essential for:
- Adding new operator support to Sharpy
- Debugging operator-related code generation issues
- Understanding how Python operator protocols map to .NET
- Working with Sharpy's error handling and optional value features

The code demonstrates careful attention to C# requirements (complementary operators), Python semantics (floor division), and practical .NET compatibility (int32 over int64).
