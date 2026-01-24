# Walkthrough: TypeInferenceService.cs

**Source File**: `src/Sharpy.Compiler/Semantic/TypeInferenceService.cs`

---

## Overview

`TypeInferenceService` is a focused utility service that determines what type results from various operations in Sharpy code. It's the "type calculator" of the semantic analysis phase - given an operation and its operand types, it tells you what type you'll get back.

**Key Design Philosophy**: This service performs **pure type inference only** - it does NOT validate operations or report errors. That separation of concerns means validators can use this service to figure out result types, then decide separately whether the operation is valid.

**Position in Pipeline**:
- **Upstream**: Used by `TypeChecker`, validators (especially `OperatorValidator`)
- **Downstream**: Results feed into `SemanticInfo` type annotations
- **Parallel Services**: Works alongside `SymbolTable`, `ClrMemberCache`

## Class Structure

### Main Class: `TypeInferenceService`

```csharp
public class TypeInferenceService
{
    private readonly SymbolTable _symbolTable;
    private readonly ClrMemberCache _clrMemberCache;
    
    // Performance caches (not thread-safe)
    private readonly Dictionary<(SemanticType, BinaryOperator, SemanticType), SemanticType?> _binaryOpCache;
    private readonly Dictionary<(UnaryOperator, SemanticType), SemanticType?> _unaryOpCache;
}
```

**Dependencies**:
- `SymbolTable`: Provides access to user-defined types and their operator methods
- `ClrMemberCache`: Looks up operator overloads from imported .NET types

**Design Note**: The caches use tuple keys for efficient lookup. Operations like `int + int` are highly repetitive during type checking, so caching provides significant performance gains.

## Key Methods by Category

### 1. Binary Operations

#### `InferBinaryOpType(BinaryOperator op, SemanticType left, SemanticType right)`

**Purpose**: Determines the result type of expressions like `a + b`, `x == y`, `lst1 + lst2`.

**Algorithm**:
1. **Check cache first** - Most operations repeat many times
2. **Handle special operators** - `and`, `or`, `is`, `in` have fixed semantics
3. **Try builtin types** - Numeric ops, string ops, list concatenation
4. **Try user-defined operators** - Look for `__add__`, `__eq__`, etc. in classes
5. **Try CLR operators** - Check for .NET operator overloads
6. **Return null** - If no valid operation found

**Example Flow** for `int + int`:
```csharp
// User writes: x + y where both are ints
InferBinaryOpType(BinaryOperator.Add, SemanticType.Int, SemanticType.Int)
  → TryInferBuiltinBinaryOp()  // Both are numeric
    → InferNumericResultType(Int, Int)  // Type promotion rules
      → Returns SemanticType.Int
```

**Important Semantics**:
- **Division** (`/`) always returns `float64` (Python semantics)
- **Power** (`**`) always returns `float64` (Math.Pow is double)
- **Null coalesce** (`??`): `T? ?? T` → `T`, `T? ?? T?` → `T?`

#### `InferNullCoalesceType(SemanticType left, SemanticType right)`

**Purpose**: Handles the `??` operator with null-safety rules.

**Rules**:
- Left side MUST be nullable (`T?`)
- Right side must be assignable to the non-nullable underlying type
- If right is nullable, result is nullable; otherwise non-nullable

```csharp
x: int? = None
y: int = 42
result = x ?? y  // Type is 'int' (non-nullable)

z: int? = 10
result2 = x ?? z  // Type is 'int?' (nullable)
```

#### `TryInferBuiltinBinaryOp(BinaryOperator op, SemanticType left, SemanticType right)`

**Purpose**: Handles built-in type operations without looking at user code or CLR.

**Coverage**:
- **Bitwise ops** (`&`, `|`, `^`, `<<`, `>>`) on integers → promoted integer type
- **Arithmetic** (`+`, `-`, `*`, `//`, `%`) on numerics → promoted numeric type
- **Division/Power** (`/`, `**`) → always `float64`
- **Comparisons** (`==`, `!=`, `<`, `<=`, `>`, `>=`) → `bool`
- **String concatenation** (`str + str`) → `str`
- **String repetition** (`str * int`) → `str`
- **List concatenation** (`list[T] + list[T]`) → `list[T]`

**Type Promotion** for numeric operations (from `InferNumericResultType`):
```
Precedence: double > float32 > long > int
Mixed integer/float → float
Examples:
  int + int → int
  int + long → long
  int + float32 → float32
  int + double → double
  long + float32 → float32
```

#### `TryInferUserDefinedBinaryOp(BinaryOperator op, SemanticType left, SemanticType right)`

**Purpose**: Look up dunder methods like `__add__`, `__eq__` in user-defined classes.

**Algorithm**:
1. Convert operator to dunder name: `BinaryOperator.Add` → `"__add__"`
2. Check if left operand type has that operator method
3. Find best overload matching right operand type
4. Return the method's return type
5. **Special case**: Synthesize `__eq__` from `__ne__` and vice versa

**Example**:
```python
class Vec2:
    def __add__(self, other: Vec2) -> Vec2:
        return Vec2(self.x + other.x, self.y + other.y)

v1 + v2  # Infers return type as Vec2
```

**Equality Complement Synthesis**: If a class defines `__ne__` but not `__eq__`, the compiler can synthesize `==` by inverting `!=`. This matches Python's behavior.

#### `TryInferClrBinaryOp(BinaryOperator op, SemanticType left, SemanticType right)`

**Purpose**: Handle operator overloads from imported .NET types.

**Example**: If you import a .NET type like `System.Numerics.BigInteger`:
```csharp
// BigInteger has: public static BigInteger operator +(BigInteger left, BigInteger right)
// This method maps BinaryOperator.Add → "op_Addition" and looks it up
```

**CLR Operator Name Mapping**:
- `+` → `"op_Addition"`
- `-` → `"op_Subtraction"`
- `*` → `"op_Multiply"`
- `==` → `"op_Equality"`
- etc.

### 2. Unary Operations

#### `InferUnaryOpType(UnaryOperator op, SemanticType operand)`

**Purpose**: Type inference for unary expressions like `-x`, `~flags`, `not condition`.

**Special Cases**:
- `not` ALWAYS returns `bool` (regardless of operand type)
- Numeric `+` and `-` return the same type as operand
- Bitwise `~` on integers returns the same integer type

**Algorithm** (similar to binary ops):
1. Check cache
2. Handle `not` → always `bool`
3. Try builtin unary ops
4. Try user-defined dunder methods (`__neg__`, `__pos__`, `__invert__`)
5. Try CLR unary operators

**Example**:
```python
x: int = 5
-x       # InferUnaryOpType(Minus, Int) → Int
not x    # InferUnaryOpType(Not, Int) → Bool

class CustomNumber:
    def __neg__(self) -> CustomNumber: ...

num = CustomNumber()
-num     # Looks up __neg__, returns CustomNumber
```

### 3. Augmented Assignment

#### `InferAugmentedAssignmentType(AssignmentOperator op, SemanticType target, SemanticType value)`

**Purpose**: Handle compound assignments like `+=`, `*=`, `&=`.

**Python Semantics**: Prioritizes in-place operators over binary operators:
1. **Try in-place dunder** first: `__iadd__`, `__imul__`, etc.
2. **Fall back to binary operator**: If no in-place version, use `__add__` + assignment

**Example**:
```python
# For lists, += uses __iadd__ (mutates in place)
lst: list[int] = [1, 2]
lst += [3, 4]  # Calls __iadd__, returns list[int]

# For numbers, no __iadd__, so falls back to __add__
x: int = 5
x += 3  # Uses int.__add__, result is int
```

**Special Cases**:
- `=` (simple assignment) → just returns value type
- `??=` (null coalesce assign) → uses `InferNullCoalesceType` logic

### 4. Protocol Type Inference

These methods handle Python protocols (iteration, indexing, membership testing).

#### `InferIterableElementType(SemanticType iterableType)`

**Purpose**: Determine what type you get when iterating over a container.

**Rules**:
- `list[T]` → yields `T`
- `dict[K, V]` → yields `K` (keys, not values!)
- `tuple[T, U, V]` → yields first element type `T` (simplified)
- `str` → yields `str` (characters as strings)
- `Iterator<T>` → yields `T`

**Example**:
```python
numbers: list[int] = [1, 2, 3]
for n in numbers:  # n inferred as 'int'
    print(n)

mapping: dict[str, int] = {"a": 1}
for key in mapping:  # key inferred as 'str'
    print(key)
```

#### `InferIndexAccessType(SemanticType container, SemanticType index)`

**Purpose**: What type do you get from `container[index]`?

**Rules**:
- `list[T]` → `T`
- `dict[K, V]` → `V` (values, not keys!)
- `tuple[T, U, V]` → first element type `T` (simplified)
- `str` → `str`

**Example**:
```python
items: list[str] = ["a", "b", "c"]
first = items[0]  # first inferred as 'str'

lookup: dict[str, int] = {"age": 30}
age = lookup["age"]  # age inferred as 'int'
```

#### `InferMembershipType(SemanticType container, SemanticType element)`

**Purpose**: Result type of `element in container` or `element not in container`.

**Simple Rule**: Always returns `bool` (validation of whether membership is supported happens elsewhere).

#### `InferLenType(SemanticType target)`

**Purpose**: Result type of `len(container)`.

**Simple Rule**: Always returns `int` (validation happens elsewhere).

## Helper Methods

### Type Classification

#### `IsNumericType(SemanticType type)` and `IsIntegerType(SemanticType type)`

Quick checks for numeric operations:
- **Numeric**: `int`, `long`, `float`, `float32`, `double`
- **Integer**: `int`, `long` (subset of numeric)

Used throughout to determine if arithmetic/bitwise operations are valid.

### Type Conversion

#### `GetClrType(SemanticType type)`

Extracts the underlying .NET `System.Type` from a SemanticType:
- `BuiltinType` → `ClrType` property
- `UserDefinedType` → `Symbol?.ClrType`
- `GenericType` → `GenericDefinition?.ClrType`

#### `MapClrTypeToSemanticType(Type clrType)`

Reverse mapping from .NET types back to Sharpy types:
```csharp
typeof(int)    → SemanticType.Int
typeof(string) → SemanticType.Str
typeof(double) → SemanticType.Double
// etc.
```

### Operator Mapping

These methods translate between Sharpy operators and implementation details:

- `BinaryOperatorToDunder()`: `Add` → `"__add__"`, `Equal` → `"__eq__"`
- `UnaryOperatorToDunder()`: `Minus` → `"__neg__"`, `BitwiseNot` → `"__invert__"`
- `BinaryOperatorToClrMethod()`: `Add` → `"op_Addition"`, `Equal` → `"op_Equality"`
- `UnaryOperatorToClrMethod()`: `Minus` → `"op_UnaryNegation"`
- `AssignmentOperatorToInPlaceDunder()`: `PlusAssign` → `"__iadd__"`
- `AssignmentOperatorToBinaryOperator()`: `PlusAssign` → `BinaryOperator.Add`

### Overload Resolution

#### `FindBestOverload(List<FunctionSymbol> candidates, SemanticType argumentType)`

**Purpose**: When a type has multiple operator overloads, pick the right one.

**Strategy** (simplified - full resolution is in validators):
1. **Exact match**: Parameter type equals argument type exactly
2. **Assignable match**: Argument type can be assigned to parameter type
3. **Return null**: If no match found

**Example**:
```python
class Number:
    def __add__(self, other: int) -> Number: ...
    def __add__(self, other: float) -> Number: ...

n: Number
n + 5      # Finds exact match with int overload
n + 5.0    # Finds exact match with float overload
```

## Dependencies

### Internal Dependencies

**Critical imports from Sharpy codebase**:
- `Sharpy.Compiler.Parser.Ast`: Gets `BinaryOperator`, `UnaryOperator`, `AssignmentOperator` enums
- `Sharpy.Compiler.Semantic.SemanticType`: All the type representations
- `Sharpy.Compiler.Semantic.SymbolTable`: Access to symbols and type definitions
- `Sharpy.Compiler.Semantic.ClrMemberCache`: Reflection cache for .NET operators

### External Dependencies

**From .NET BCL**:
- `System.Type`: CLR type reflection
- `System.Reflection.MethodInfo`: Operator method introspection

## Patterns and Design Decisions

### 1. **Separation of Concerns: Inference vs Validation**

The most important design decision - this service ONLY infers types, never validates or reports errors.

**Benefits**:
- Validators can reuse inference logic without coupling to error reporting
- Same inference logic works for successful operations and error messages
- Easier to test (just check returned type, no error context needed)

**Example Usage**:
```csharp
// In OperatorValidator
var resultType = _inferenceService.InferBinaryOpType(op, leftType, rightType);
if (resultType == null)
{
    // NOW report error - inference service doesn't do this
    ReportError($"Operator {op} not supported for types {leftType} and {rightType}");
}
```

### 2. **Null Return Convention**

All public inference methods return `SemanticType?` where `null` means "cannot infer a valid type."

**This is NOT an error** - it's just information. The caller decides what to do:
- Validators → report error
- Type checker with nullable context → might infer `None` type
- Recovery logic → might use `Unknown` type

### 3. **Caching Strategy**

Performance optimization using dictionary caches:
```csharp
var cacheKey = (left, op, right);
if (_binaryOpCache.TryGetValue(cacheKey, out var cached))
    return cached;
```

**Why cache?**:
- Type checking visits every expression node
- Common operations like `int + int` happen thousands of times
- Type objects are immutable, making them safe cache keys

**Trade-off**: Not thread-safe (noted in comments). Could add locking if needed for parallel compilation.

### 4. **Fallback Chain Pattern**

Most inference methods use a priority chain:
```
Special cases → Builtins → User-defined → CLR → null
```

This matches Python's method resolution order and ensures predictable behavior.

### 5. **Python Semantic Fidelity**

Critical decisions match Python:
- Division (`/`) returns float, not int
- Null coalesce properly handles nullable unwrapping
- `in` operator always returns bool
- Dict iteration yields keys, not values

### 6. **Type Promotion Rules**

Numeric operations follow clear precedence:
```
double > float32 > long > int
```

This ensures operations like `int + float` properly widen to float without information loss.

## Debugging Tips

### 1. **Cache Invalidation**

If you suspect a cached result is wrong:
- Caches are instance-scoped (new service = fresh caches)
- No explicit invalidation needed during normal compilation

### 2. **Tracing Inference Decisions**

Add logging at key decision points:
```csharp
// At start of InferBinaryOpTypeUncached
_logger?.LogDebug($"Inferring: {left} {op} {right}");

// Before each return
_logger?.LogDebug($"Result: {resultType}");
```

### 3. **Testing Specific Operations**

Write focused unit tests:
```csharp
[Fact]
public void TestIntPlusInt()
{
    var service = new TypeInferenceService(symbolTable);
    var result = service.InferBinaryOpType(
        BinaryOperator.Add, 
        SemanticType.Int, 
        SemanticType.Int
    );
    Assert.Equal(SemanticType.Int, result);
}
```

### 4. **Common Pitfalls**

**Null doesn't mean error**: 
```csharp
// WRONG: Throwing when null returned
var type = InferBinaryOpType(...);
if (type == null) throw new Exception("Invalid!");

// RIGHT: Let validator handle it
var type = InferBinaryOpType(...);
if (type == null) return ReportError(...);
```

**Cache key construction**:
The tuples use value equality, so `(Int, Add, Int)` will match every time. But custom types must implement proper `Equals()`.

**Overload ambiguity**:
`FindBestOverload` does simplified resolution. Full overload resolution (with generics, variance, etc.) happens in validators.

### 5. **Verifying Against Python**

When debugging unexpected types, always verify Python's behavior:
```bash
$ python3
>>> type(5 / 2)
<class 'float'>        # Not int!

>>> type([1,2] + [3,4])
<class 'list'>

>>> type("hello" * 3)
<class 'str'>
```

## Contribution Guidelines

### When to Modify This File

**ADD new inference methods when**:
- Implementing new operators (e.g., matrix multiply `@`)
- Adding new protocols (e.g., async iteration)
- Supporting new builtin operations

**MODIFY existing methods when**:
- Fixing type promotion bugs
- Improving overload resolution
- Adding support for new semantic types

**DO NOT modify when**:
- Adding validation logic (belongs in validators)
- Reporting errors (service is inference-only)
- Changing code generation (downstream concern)

### Adding a New Operator

**Example**: Adding matrix multiply `@`

1. **Add to parser**: `BinaryOperator.MatrixMultiply`

2. **Add case in `InferBinaryOpTypeUncached`**:
```csharp
case BinaryOperator.MatrixMultiply:
    return TryInferMatrixMultiply(left, right);
```

3. **Implement inference logic**:
```csharp
private SemanticType? TryInferMatrixMultiply(SemanticType left, SemanticType right)
{
    // Check for __matmul__ dunder method
    if (left is UserDefinedType udt && udt.Symbol != null)
    {
        if (udt.Symbol.OperatorMethods.TryGetValue("__matmul__", out var methods))
        {
            var bestOverload = FindBestOverload(methods, right);
            return bestOverload?.ReturnType;
        }
    }
    return null;
}
```

4. **Add to dunder mapping**:
```csharp
BinaryOperator.MatrixMultiply => "__matmul__",
```

5. **Add tests**:
```csharp
[Fact]
public void TestMatrixMultiply()
{
    // Test builtin types, user-defined types, error cases
}
```

### Testing Your Changes

**Unit tests** for each public method:
- Test all builtin type combinations
- Test user-defined types with operator methods
- Test CLR type operator overloads
- Test null returns for unsupported operations

**Integration tests** via type checker:
- Compile actual Sharpy code
- Verify inferred types in SemanticInfo
- Check that operations compile to correct C#

### Performance Considerations

**Cache effectiveness**:
- Monitor cache hit rates in large codebases
- Consider cache size limits if memory becomes an issue

**Reflection overhead**:
- CLR operator lookup uses reflection
- `ClrMemberCache` amortizes this cost
- Avoid repeated reflection in hot paths

### Code Style

**Consistency with existing patterns**:
- Use `?` suffix for nullable returns
- Group related operations in regions
- Static helper methods for pure logic
- Instance methods when using `_symbolTable` or `_clrMemberCache`

**Comments**:
- XML docs on public methods
- Inline comments for non-obvious logic
- Reference Python semantics where relevant

## Cross-References

### Related Files in Semantic Analysis

**Core Type System**:
- [`SemanticType.cs`](SemanticType.md) - Type representation hierarchy
- [`SymbolTable.cs`](SymbolTable.md) - Symbol storage and lookup
- [`Symbol.cs`](Symbol.md) - Symbol types (TypeSymbol, FunctionSymbol, etc.)

**Type Resolution**:
- [`TypeResolver.cs`](TypeResolver.md) - Resolves type annotations to SemanticTypes
- [`TypeChecker.cs`](TypeChecker.md) - Main type checking logic (uses this service)
- [`TypeChecker.Expressions.cs`](TypeChecker.Expressions.md) - Expression type checking

**Validation**:
- [`OperatorValidator.cs`](OperatorValidator.md) - Validates operator usage (uses this service)
- [`ProtocolValidator.cs`](ProtocolValidator.md) - Validates protocol implementations

**Supporting Infrastructure**:
- [`ClrMemberCache.cs`](ClrMemberCache.md) - Caches .NET reflection lookups
- [`BuiltinRegistry.cs`](BuiltinRegistry.md) - Registry of builtin types and functions

### Related Specification Documents

**Type System**:
- [`docs/language_specification/type_annotations.md`](../../../language_specification/type_annotations.md) - Syntax and semantics of type annotations
- [`docs/language_specification/type_hierarchy.md`](../../../language_specification/type_hierarchy.md) - Type relationships and assignability
- [`docs/language_specification/type_casting.md`](../../../language_specification/type_casting.md) - Explicit type conversions

**Operators**:
- [`docs/language_specification/operators.md`](../../../language_specification/operators.md) - Operator precedence and semantics
- [`docs/language_specification/operator_overloading.md`](../../../language_specification/operator_overloading.md) - Dunder methods

**Protocols**:
- [`docs/language_specification/protocols.md`](../../../language_specification/protocols.md) - Iteration, indexing, membership protocols

### Downstream Usage

**Code Generation**:
- `RoslynEmitter` uses inferred types to generate correct C# operator calls
- Generic specialization depends on accurate element type inference

**Error Messages**:
- Validators use inference results to generate helpful error messages
- "Cannot add `int` and `str`" comes from failed inference

## Summary

`TypeInferenceService` is the compiler's type calculator - a pure, stateless service that answers the question "what type results from this operation?" Its design prioritizes:

1. **Separation of concerns**: Inference ≠ validation
2. **Performance**: Aggressive caching for repetitive operations
3. **Python fidelity**: Matches Python's type semantics exactly
4. **Extensibility**: Easy to add new operators and protocols

When working with this file, remember: **return null means "cannot infer", not "error occurred"**. Let the validators decide what null means in their context.
