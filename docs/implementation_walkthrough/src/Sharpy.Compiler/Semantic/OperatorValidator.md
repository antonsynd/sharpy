# Walkthrough: OperatorValidator.cs

**Source File**: `src/Sharpy.Compiler/Semantic/OperatorValidator.cs`

---

## Overview

The `OperatorValidator` is a critical component in the **Semantic Analysis** phase of the Sharpy compiler pipeline. Its primary responsibility is to validate operator usage in Sharpy source code and determine the result types of operator expressions.

**Key Responsibilities**:
- Validate binary operators (arithmetic, comparison, bitwise, logical)
- Validate unary operators (negation, bitwise not, logical not)
- Validate augmented assignment operators (`+=`, `-=`, etc.)
- Support both **Sharpy dunder methods** (e.g., `__add__`, `__mul__`) and **CLR operator overloads** for .NET interop
- Implement overload resolution when multiple operator implementations exist
- Perform type checking and inference for operator expressions

**Pipeline Position**:
- **Input**: AST nodes from the Parser representing operator expressions
- **Output**: Validated operator types that flow to RoslynEmitter for code generation
- **Works with**: SymbolTable (for type lookups), ProtocolValidator (for membership operators)

**Thread Safety**: This class is NOT thread-safe. Instances should not be shared across threads due to internal caching without locks.

---

## Class Structure

### Main Class: `OperatorValidator`

```csharp
public class OperatorValidator
{
    private readonly SymbolTable _symbolTable;
    private readonly ICompilerLogger _logger;
    private readonly List<SemanticError> _errors;
    private readonly ProtocolValidator? _protocolValidator;
    private readonly ClrMemberCache _clrMemberCache;

    // Performance caches (not thread-safe)
    private readonly Dictionary<(SemanticType, BinaryOperator, SemanticType), SemanticType?> _binaryOpCache;
    private readonly Dictionary<(UnaryOperator, SemanticType), SemanticType?> _unaryOpCache;
}
```

**Key Fields**:
- `_symbolTable`: Provides access to symbol information for type lookups
- `_logger`: Used for logging errors during validation
- `_errors`: Accumulates semantic errors found during validation
- `_protocolValidator`: Optional validator for protocol-based operations (e.g., `__contains__` for `in` operator)
- `_clrMemberCache`: Caches CLR operator methods for performance
- `_binaryOpCache` / `_unaryOpCache`: Performance optimization to avoid re-validating the same operator combinations

---

## Key Methods and Validation Flow

### 1. Binary Operator Validation

#### `ValidateBinaryOp()`
**Location**: Lines 51-132

The main entry point for validating binary operations like `a + b`, `x == y`, `foo in bar`, etc.

```csharp
public SemanticType ValidateBinaryOp(
    BinaryOperator op,
    SemanticType left,
    SemanticType right,
    int line,
    int column)
```

**Flow**:
1. **Check cache**: If this exact combination of `(left type, operator, right type)` has been validated before, return the cached result
2. **Handle special cases**:
   - Logical operators (`and`, `or`): Always return `bool`
   - Null coalescing (`??`): Validate left is nullable, right is assignable to non-nullable version
   - Membership operators (`in`, `not in`): Delegate to `ProtocolValidator` to check for `__contains__`
   - Identity operators (`is`, `is not`): Always return `bool`
3. **Resolve overloaded operators**: For all other operators, call `ResolveOperatorOverload()`
4. **Cache the result** for future lookups
5. Return the result type

**Important Design Decision**: Special operators are handled first to avoid unnecessary overload resolution. This improves performance and provides clearer error messages.

---

### 2. Operator Overload Resolution

#### `ResolveOperatorOverload()`
**Location**: Lines 269-290

This method attempts to find a valid operator implementation through multiple resolution strategies.

**Resolution Order** (implemented in `TryResolveOperatorOverloadWithoutLogging`, lines 296-348):

1. **User-defined operator methods** (Sharpy dunder methods)
   - Check if the left operand is a `UserDefinedType` with operator methods
   - Look up the appropriate dunder method (e.g., `__add__` for `+`)
   - Perform overload resolution if multiple implementations exist

2. **Equality complement synthesis**
   - Special logic: if only `__eq__` is defined, synthesize `__ne__` (and vice versa)
   - This matches the behavior in `RoslynEmitter` where missing equality operators are auto-generated

3. **Sharpy builtin type operators**
   - Handle operators on primitive types (`int`, `float`, `str`, `list`, etc.)
   - Implement Python-like semantics (e.g., `/` always returns `float`, `+` on lists concatenates)

4. **CLR operator methods** (.NET interop)
   - Use reflection to find CLR operator overloads (e.g., `op_Addition`)
   - Enables Sharpy code to work with .NET types that define operators

If no valid operator is found, an error is added to the errors collection.

---

### 3. Overload Resolution with "Most Specific" Semantics

#### `ResolveBestOverload()`
**Location**: Lines 406-505

When a type defines multiple operator overloads (e.g., `__add__(self, int)` and `__add__(self, object)`), this method determines which one to use.

**Resolution Strategy**:
1. **Exact match first**: If the argument type exactly matches a parameter type, use that overload
2. **Assignable matches**: Find all overloads where the argument is assignable to the parameter
3. **Most specific selection**: If multiple assignable matches exist, choose the most specific (most derived) parameter type

**Example**:
```python
class Calculator:
    def __add__(self, other: object) -> Calculator: ...
    def __add__(self, other: int) -> Calculator: ...

calc = Calculator()
result = calc + 5  # Chooses __add__(self, int) because int is more specific than object
```

**Ambiguity Detection**: If multiple "most specific" overloads are found, an error is reported with candidate types.

---

### 4. Builtin Type Operators

#### `TryResolveBuiltinOperator()`
**Location**: Lines 577-685

Implements operator semantics for Sharpy's builtin types following Python conventions.

**Key Behaviors**:

**Numeric Operations**:
- Arithmetic operators (`+`, `-`, `*`, `//`, `%`): Return the promoted numeric type (e.g., `int + float` → `float`)
- Division (`/`) and power (`**`): Always return `float64` (Python semantics)
- Bitwise operators (`&`, `|`, `^`, `<<`, `>>`): Only work on integer types

**String Operations**:
- `str + str` → `str` (concatenation)
- Comparison operators on strings → `bool`

**List Operations**:
- `list[T] + list[T]` → `list[T]` (concatenation with same element type)
- Mixed typed/untyped lists: Return the typed version
- Incompatible element types: Return `null` (error will be reported)

**Default Equality**:
- `==` and `!=` work for any two types if they're identical
- Ensures you can always compare values of the same type

**Delegation to PrimitiveCatalog**:
The actual numeric type checking and promotion logic is delegated to `PrimitiveCatalog` (lines 822-841), which centralizes knowledge about all numeric primitive types.

---

### 5. CLR Interop

#### `TryResolveClrOperator()`
**Location**: Lines 714-749

Enables Sharpy to work with .NET types that define operator overloads using CLR conventions.

**How It Works**:
1. Map Sharpy operator to CLR method name (e.g., `+` → `"op_Addition"`)
2. Get CLR types for both operands
3. Use `ClrMemberCache` to retrieve operator methods via reflection
4. Find the overload where parameter types match exactly
5. Map the CLR return type back to a `SemanticType`

**Example**:
```python
# Sharpy code using .NET's TimeSpan
from System import TimeSpan
duration1 = TimeSpan.FromHours(1)
duration2 = TimeSpan.FromMinutes(30)
total = duration1 + duration2  # Calls TimeSpan.op_Addition
```

**Mapping Tables**:
- `BinaryOperatorToClrMethod()` (lines 224-249): Maps operators like `+` → `"op_Addition"`
- `UnaryOperatorToClrMethod()` (lines 254-264): Maps unary operators like `-` → `"op_UnaryNegation"`

---

### 6. Unary Operator Validation

#### `ValidateUnaryOp()`
**Location**: Lines 137-160

Validates unary operators like `-x`, `~x`, `not x`.

**Flow**:
1. Check cache for previous validation
2. Special case: `not` always returns `bool`
3. For other operators, resolve via `ResolveUnaryOperatorOverload()`
4. Cache and return result

#### `ResolveUnaryOperatorOverload()`
**Location**: Lines 353-394

Similar to binary operator resolution but simpler (no overload selection needed):
1. Try user-defined dunder method (e.g., `__neg__`)
2. Try builtin type operators
3. Try CLR operator methods

---

### 7. Augmented Assignment Validation

#### `ValidateAugmentedAssignment()`
**Location**: Lines 1001-1074

Validates compound assignment operators like `x += 5`, `flags |= FLAG_ENABLED`.

**Resolution Strategy**:
1. Try **in-place operator** first (e.g., `__iadd__` for `+=`)
   - These allow types to optimize in-place modifications
   - Example: `list.__iadd__()` can modify the list directly rather than creating a new one
2. Fall back to **binary operator** (e.g., `__add__` for `+=`)
   - This is the common case for types that don't define in-place operators
3. Validate result type is assignable to target type
   - Important: `x += y` requires that the result of `x + y` can be assigned back to `x`

**Special Case for `??=`**:
The null coalescing assignment `x ??= y` returns the target type (which remains nullable), not the non-nullable version.

**Mapping Tables**:
- `AssignmentOperatorToInPlaceDunder()` (lines 919-938): Maps `+=` → `"__iadd__"`
- `AssignmentOperatorToBinaryOperator()` (lines 944-964): Maps `+=` → `BinaryOperator.Add`

---

## Dunder Method Mapping

Sharpy uses Python-style "dunder" (double underscore) methods for operator overloading:

### Binary Operators
```
+   → __add__
-   → __sub__
*   → __mul__
/   → __truediv__
//  → __floordiv__
%   → __mod__
**  → __pow__
&   → __and__
|   → __or__
^   → __xor__
<<  → __lshift__
>>  → __rshift__
==  → __eq__
!=  → __ne__
<   → __lt__
<=  → __le__
>   → __gt__
>=  → __ge__
```

### Unary Operators
```
+x  → __pos__
-x  → __neg__
~x  → __invert__
```

### In-Place Assignment Operators
```
+=  → __iadd__
-=  → __isub__
*=  → __imul__
/=  → __itruediv__
//= → __ifloordiv__
%=  → __imod__
**= → __ipow__
&=  → __iand__
|=  → __ior__
^=  → __ixor__
<<= → __ilshift__
>>= → __irshift__
```

**See**: `BinaryOperatorToDunder()` (lines 165-204), `UnaryOperatorToDunder()` (lines 209-219)

---

## Dependencies

### Internal Dependencies

1. **SymbolTable** (`_symbolTable`)
   - Provides access to type symbols and their operator methods
   - Used to look up `UserDefinedType` symbols and their `OperatorMethods` dictionary

2. **ProtocolValidator** (`_protocolValidator`)
   - Used for validating membership operators (`in`, `not in`)
   - Checks if a type implements the `__contains__` protocol

3. **ClrMemberCache** (`_clrMemberCache`)
   - Caches CLR operator methods discovered via reflection
   - Improves performance by avoiding repeated reflection calls

4. **PrimitiveCatalog** (static dependency)
   - Centralized knowledge about numeric primitive types
   - Methods: `IsNumeric()`, `IsInteger()`, `GetPromotedType()`
   - See lines 822-841

### External Dependencies

- `Sharpy.Compiler.Parser.Ast`: Provides operator enums (`BinaryOperator`, `UnaryOperator`, `AssignmentOperator`, `ComparisonOperator`)
- `Sharpy.Compiler.Logging`: Error logging infrastructure
- `System.Reflection`: Used for CLR operator discovery

---

## Patterns and Design Decisions

### 1. **Caching for Performance**
The validator maintains caches for binary and unary operator results. This is crucial because:
- Operator validation can be expensive (symbol lookups, reflection, overload resolution)
- The same operator combinations are often validated multiple times in a codebase
- Trade-off: Caches are not thread-safe, so instances cannot be shared across threads

### 2. **Multi-Strategy Resolution**
The operator resolution follows a clear priority order:
1. User-defined operators (Sharpy dunder methods)
2. Builtin type operators (with special semantics)
3. CLR operators (.NET interop)

This ensures Sharpy types can override builtin behavior and .NET types work seamlessly.

### 3. **Equality Complement Synthesis**
If a type defines only `__eq__` or only `__ne__`, the validator synthesizes the complement operator. This matches the code generation behavior in `RoslynEmitter` and provides a better developer experience (you only need to define one).

See `TryResolveEqualityComplement()` at lines 511-572.

### 4. **Error Collection Pattern**
Errors are collected in a list (`_errors`) and also logged immediately. This allows:
- Continued type checking after an error (don't stop at first error)
- Post-processing of all errors
- Immediate feedback via logging

### 5. **Separation of Concerns**
- Operator-to-dunder mapping is separated from resolution logic
- CLR mapping is isolated in dedicated methods
- Overload resolution is a reusable helper method
- This makes the code maintainable and easy to extend with new operators

---

## Debugging Tips

### 1. **Cache-Related Issues**
If you see inconsistent validation results:
- Check if the cache is being inappropriately shared across validation contexts
- Remember: Caches are keyed by `SemanticType` instances, so type identity matters
- Clear caches between test runs if needed

### 2. **Overload Resolution Failures**
When "ambiguous overload" errors occur:
- Look at the candidate parameter types in the error message
- Check if types have proper inheritance relationships defined
- Verify `IsAssignableTo()` works correctly for your types
- Add logging in `ResolveBestOverload()` to see which candidates are considered

### 3. **Missing Operators**
If an operator isn't working:
1. Check if dunder method is properly registered in `UserDefinedType.Symbol.OperatorMethods`
2. Verify the dunder name mapping is correct (check `BinaryOperatorToDunder()`)
3. For CLR types, verify the operator method exists via reflection
4. Add a breakpoint in the appropriate `TryResolve*` method to trace resolution

### 4. **Type Promotion Issues**
For numeric operators returning wrong types:
- Check `PrimitiveCatalog.GetPromotedType()` logic
- Verify both operand types are being recognized as numeric
- Look at `InferNumericResultType()` (lines 836-841)

### 5. **Tracking Validation Flow**
The validation flow is:
1. `ValidateBinaryOp()` or `ValidateUnaryOp()` (entry point)
2. `ResolveOperatorOverload()` or `ResolveUnaryOperatorOverload()`
3. `TryResolveOperatorOverloadWithoutLogging()` (tries all strategies)
4. Individual `TryResolve*` methods

Set breakpoints at each level to understand where resolution fails.

---

## Contribution Guidelines

### Adding a New Operator

To add support for a new operator:

1. **Add to AST enums** (in `Parser.Ast` namespace):
   - Add to `BinaryOperator`, `UnaryOperator`, or `AssignmentOperator` enum

2. **Add dunder mapping**:
   - Update `BinaryOperatorToDunder()` or `UnaryOperatorToDunder()`
   - Follow Python naming conventions

3. **Add CLR mapping** (if applicable):
   - Update `BinaryOperatorToClrMethod()` or `UnaryOperatorToClrMethod()`
   - Follow .NET operator naming (e.g., `op_*`)

4. **Add symbol mapping**:
   - Update `GetOperatorSymbol()` or `GetUnaryOperatorSymbol()` for error messages
   - If it's an assignment operator, update `GetAssignmentOperatorSymbol()`

5. **Add builtin semantics** (if needed):
   - Update `TryResolveBuiltinOperator()` or `TryResolveBuiltinUnaryOperator()`
   - Define behavior for primitive types

6. **Update special case handling** (if needed):
   - If the operator has special validation logic, add it to `ValidateBinaryOp()` switch

7. **Write tests**:
   - Test user-defined operator overloads
   - Test builtin type behavior
   - Test CLR interop (if applicable)
   - Test error cases (missing operators, type mismatches)

### Extending Overload Resolution

If you need to modify overload resolution logic:
- Consider backward compatibility (existing code relies on current behavior)
- Update `ResolveBestOverload()` carefully (it's used by multiple operators)
- Add comprehensive tests for ambiguous cases
- Document the resolution strategy in comments

### Performance Improvements

When optimizing:
- Profile first to identify actual bottlenecks
- Consider expanding caching (but be mindful of memory usage)
- Could pre-populate builtin operator cache at startup
- Could use more efficient data structures for overload storage

### CLR Interop Enhancements

To improve .NET interop:
- Consider supporting CLR generic type operators (currently not fully handled)
- Could implement reverse operator lookup (e.g., `int + MyType` where `MyType` defines the operator)
- Could add support for conversion operators (`op_Implicit`, `op_Explicit`)

---

## Cross-References

### Related Semantic Analysis Components

- **SymbolTable**: Stores type symbols and their operator methods; queried by this validator
- **ProtocolValidator**: Validates protocol-based operations (e.g., `__contains__` for `in` operator)
  - Documentation: `docs/implementation_walkthrough/src/Sharpy.Compiler/Semantic/ProtocolValidator.md` (if exists)
- **ClrMemberCache**: Performance optimization for caching CLR member lookups
- **PrimitiveCatalog**: Centralized numeric type knowledge (type checking and promotion)

### Upstream Components

- **Parser**: Produces AST nodes with operator expressions that need validation
  - See `docs/implementation_walkthrough/src/Sharpy.Compiler/Parser/Parser.Expressions.md`

### Downstream Components

- **RoslynEmitter**: Consumes validated operator types to generate C# code
  - Must handle equality complement synthesis (matches this validator's logic)
  - See `docs/implementation_walkthrough/src/Sharpy.Compiler/CodeGen/RoslynEmitter.Operators.md` (if exists)

### Specification Documents

- `docs/language_specification/arithmetic_operators.md`: Defines arithmetic operator semantics
- `docs/language_specification/operator_overloading.md`: Defines how operator overloading works in Sharpy
- `docs/language_specification/operator_precedence.md`: Defines operator precedence (handled by parser, not validator)

---

## Summary

`OperatorValidator` is the semantic analysis component responsible for ensuring operator expressions are type-safe and determining their result types. It bridges Sharpy's Python-like dunder methods with .NET's CLR operator overloads, providing a seamless experience for both Sharpy and .NET types.

**Key Takeaways**:
- Supports three operator systems: Sharpy dunder methods, builtin type operators, and CLR operators
- Implements sophisticated overload resolution with "most specific" parameter semantics
- Uses caching aggressively for performance (not thread-safe)
- Follows clear separation of concerns for maintainability
- Synthesizes complement equality operators to improve developer experience

When debugging operator issues, start by tracing through the resolution strategies in order: user-defined → builtin → CLR. Understanding this flow will help you quickly identify where validation fails.
