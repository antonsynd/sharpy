# Walkthrough: OperatorValidator.cs

**Source File**: `src/Sharpy.Compiler/Semantic/OperatorValidator.cs`

---

## 1. Overview

`OperatorValidator` is a critical component of Sharpy's semantic analysis phase that validates operator usage and determines result types for operator expressions. It sits at the intersection of Sharpy's Python-like operator semantics and .NET's CLR operator overloads, enabling seamless interoperability between Sharpy code and .NET libraries.

**Core Responsibilities:**
- Validate binary operators (`+`, `-`, `*`, `==`, `and`, `in`, etc.)
- Validate unary operators (`+`, `-`, `~`, `not`)
- Validate augmented assignment operators (`+=`, `-=`, `*=`, etc.)
- Resolve operator overloads through multiple strategies:
  - Sharpy dunder methods (`__add__`, `__eq__`, etc.)
  - Sharpy builtin type operations (int, float, str, list, etc.)
  - CLR operator overloads via reflection (`op_Addition`, `op_Equality`, etc.)
- Cache results for performance optimization

**Why it exists:** Python's flexible operator overloading via dunder methods needs to coexist with .NET's static operator overloading. This class provides the unified resolution logic that makes both work seamlessly in Sharpy.

---

## 2. Class Structure

### Main Class: `OperatorValidator`

```csharp
public class OperatorValidator
{
    private readonly SymbolTable _symbolTable;
    private readonly ICompilerLogger _logger;
    private readonly List<SemanticError> _errors;
    private readonly ProtocolValidator? _protocolValidator;
    private readonly ClrMemberCache _clrMemberCache;
    
    // Performance caches (NOT thread-safe!)
    private readonly Dictionary<(SemanticType, BinaryOperator, SemanticType), SemanticType?> _binaryOpCache;
    private readonly Dictionary<(UnaryOperator, SemanticType), SemanticType?> _unaryOpCache;
}
```

**Key Design Decisions:**

1. **Not Thread-Safe by Design**: The class explicitly documents that it's not thread-safe due to internal caches. This is a deliberate performance optimization—operator validation happens frequently during type checking, and avoiding lock overhead significantly improves compilation speed.

2. **Optional Dependencies**: `ProtocolValidator` and `ClrMemberCache` are optional, with null checks throughout. This allows flexible composition and testing.

3. **Error Collection**: Errors are accumulated in `_errors` list and also logged immediately via `_logger`. This dual approach supports both batch error reporting and real-time logging.

4. **Caching Strategy**: Uses tuple keys `(SemanticType, BinaryOperator, SemanticType)` for efficient cache lookups. The cache stores nullable `SemanticType?` to distinguish between "not yet computed" (missing from cache) and "failed to resolve" (cached as `null`).

---

## 3. Key Methods

### 3.1 Public API Methods

#### `ValidateBinaryOp`

```csharp
public SemanticType ValidateBinaryOp(
    BinaryOperator op,
    SemanticType left,
    SemanticType right,
    int line,
    int column)
```

**Purpose**: The main entry point for validating binary operations. Called by `TypeChecker` when encountering binary expressions like `x + y` or `a == b`.

**Implementation Strategy**:
1. **Check cache first** - Fast path for repeated operations
2. **Handle special cases** - Logical, identity, and membership operators that don't use traditional overloading
3. **Resolve operator overload** - For arithmetic, comparison, and bitwise operators
4. **Cache and return** - Store result for future calls

**Special Cases Handled Directly**:

```csharp
BinaryOperator.And, BinaryOperator.Or
    → Always return SemanticType.Bool (short-circuit evaluation)

BinaryOperator.In, BinaryOperator.NotIn
    → Delegate to ProtocolValidator to check __contains__ protocol
    → Return bool

BinaryOperator.Is, BinaryOperator.IsNot
    → Always return SemanticType.Bool (identity comparison)

BinaryOperator.NullCoalesce
    → Currently unimplemented (returns Unknown + error)
```

**Why special cases?** These operators have fixed semantics in Python/Sharpy and don't participate in operator overloading. For example, `and`/`or` always return bool regardless of operand types, and `is`/`is not` always perform identity checks.

#### `ValidateUnaryOp`

```csharp
public SemanticType ValidateUnaryOp(
    UnaryOperator op,
    SemanticType operand,
    int line,
    int column)
```

**Purpose**: Validates unary operations like `-x`, `+x`, `~x`, `not x`.

**Simpler than binary ops** because:
- Only one operand to consider
- Fewer special cases (only `not` returns bool unconditionally)
- No complement synthesis logic needed

#### `ValidateAugmentedAssignment`

```csharp
public SemanticType ValidateAugmentedAssignment(
    AssignmentOperator op,
    SemanticType targetType,
    SemanticType valueType,
    int line,
    int column)
```

**Purpose**: Validates augmented assignments like `x += y`, `a *= b`.

**Two-Phase Resolution**:
1. **Try in-place operators first**: `__iadd__`, `__isub__`, etc.
   - These modify the object in-place (Python optimization)
   - Must return a type assignable to the target
2. **Fall back to binary operators**: `__add__`, `__sub__`, etc.
   - Creates a new object with the result
   - Also must be assignable to target

**Critical Validation**: The result type must be assignable to the target type. This catches errors like:
```python
x: int = 5
x += 2.5  # Error: float not assignable to int
```

### 3.2 Operator Resolution Methods

#### `ResolveOperatorOverload` (Binary)

**Resolution Strategy** (in order):
1. **User-defined types** via dunder methods
   - Check `Symbol.OperatorMethods` dictionary
   - Use `ResolveBestOverload` for overload resolution
   - Try equality complement synthesis if applicable
2. **Sharpy builtin types** via `TryResolveBuiltinOperator`
   - Hardcoded rules for int, float, str, list, etc.
3. **CLR types** via reflection using `ClrMemberCache`
   - Look up `op_Addition`, `op_Equality`, etc.

**Why this order?** User-defined operators take precedence (explicit overrides), then builtin types (language semantics), then CLR operators (interop fallback).

#### `ResolveBestOverload`

**Sophisticated overload resolution algorithm**:

```csharp
private FunctionSymbol? ResolveBestOverload(
    List<FunctionSymbol> candidates,
    SemanticType argumentType,
    string operatorSymbol,
    string leftTypeName,
    int line,
    int column)
```

**Resolution Steps**:
1. **Exact match**: Parameter type equals argument type exactly
2. **Assignable matches**: Argument type is assignable to parameter type
3. **Most specific match**: Among assignable matches, find the most derived/specific
   - Uses assignability hierarchy to determine specificity
   - Type A is more specific than B if A is assignable to B
4. **Ambiguity detection**: Reports error if multiple equally specific matches exist

**Example**:
```python
class Animal: pass
class Dog(Animal): pass
class Labrador(Dog): pass

class Handler:
    def __add__(self, other: Animal) -> Handler: ...
    def __add__(self, other: Dog) -> Handler: ...

h = Handler()
l = Labrador()
h + l  # Chooses Dog overload (most specific match)
```

#### `TryResolveEqualityComplement`

**Equality Complement Synthesis**: A clever optimization that matches Python semantics and the code generator behavior.

**The Problem**: In Python, you often define only `__eq__` and get `__ne__` for free (or vice versa).

**The Solution**:
- If only `__eq__` is defined, synthesize `__ne__` by negating `__eq__`
- If only `__ne__` is defined, synthesize `__eq__` by negating `__ne__`
- Both return `SemanticType.Bool`

**Why it matters**: This allows Sharpy classes to follow the Python convention of defining minimal operators. The `RoslynEmitter` performs the same synthesis during code generation, so semantic analysis must match.

```python
class Point:
    def __eq__(self, other: Point) -> bool:
        # Only define equality
        pass

# Sharpy automatically synthesizes:
# def __ne__(self, other: Point) -> bool:
#     return not self.__eq__(other)
```

### 3.3 Builtin Type Resolution

#### `TryResolveBuiltinOperator`

**Handles Sharpy's built-in types with hardcoded semantics**:

**Numeric Types** (int, long, float, double, decimal, etc.):
```csharp
BinaryOperator.Add | Subtract | Multiply | Divide | ... 
    → InferNumericResultType(left, right)
    → Uses PrimitiveCatalog.GetPromotedType() for type promotion
    → Example: int + float → float
```

**String Operations**:
```csharp
Str + Str → Str          // Concatenation
Str == Str → Bool        // Comparison
Str < Str → Bool         // Lexicographic comparison
```

**List Operations**:
```csharp
list[T] + list[T] → list[T]   // Concatenation (same element type)
list[T] + list[U] → null      // Error (incompatible element types)
list[?] + list[T] → list[T]   // Untyped + typed → typed
```

**Default Equality**:
```csharp
T == T → Bool  // For any identical types
```

**Why hardcoded?** These are fundamental language semantics that shouldn't require symbol table lookups. Performance-critical operations benefit from direct implementation.

### 3.4 CLR Interop Methods

#### `TryResolveClrOperator`

**Enables .NET operator overload interop**:

1. **Map Sharpy operator to CLR method name**:
   - `BinaryOperator.Add` → `"op_Addition"`
   - `BinaryOperator.Equal` → `"op_Equality"`
   - `UnaryOperator.Minus` → `"op_UnaryNegation"`

2. **Get CLR types** from both operands using `GetClrType()`

3. **Query ClrMemberCache** for operator methods on the left operand's type

4. **Match overload** by comparing parameter types exactly

5. **Map return type** back to `SemanticType`

**Example: Using System.Numerics.Complex**:
```python
from System.Numerics import Complex

c1 = Complex(1.0, 2.0)
c2 = Complex(3.0, 4.0)
result = c1 + c2  # Resolves to Complex.op_Addition(Complex, Complex)
```

**Cache Benefits**: `ClrMemberCache` uses reflection only once per CLR type, then caches all operator methods. This makes repeated validation essentially free.

### 3.5 Operator Mapping Methods

These utility methods translate between Sharpy's operator representations and Python/CLR conventions:

#### `BinaryOperatorToDunder`
Maps `BinaryOperator` enum → Python dunder method name:
- `BinaryOperator.Add` → `"__add__"`
- `BinaryOperator.Multiply` → `"__mul__"`
- `BinaryOperator.Equal` → `"__eq__"`
- Returns `null` for operators without dunder methods (like `and`, `or`, `is`)

#### `BinaryOperatorToClrMethod`
Maps `BinaryOperator` enum → CLR operator method name:
- `BinaryOperator.Add` → `"op_Addition"`
- `BinaryOperator.Equal` → `"op_Equality"`
- `BinaryOperator.LeftShift` → `"op_LeftShift"`

#### `GetOperatorSymbol` / `GetUnaryOperatorSymbol`
Provides human-readable symbols for error messages:
- `BinaryOperator.Power` → `"**"`
- `BinaryOperator.FloorDivide` → `"//"`
- `UnaryOperator.BitwiseNot` → `"~"`

---

## 4. Dependencies

### Internal Dependencies

**SymbolTable** (`_symbolTable`):
- Stores user-defined types and their operator methods
- Accessed via `udt.Symbol.OperatorMethods` dictionary
- Essential for resolving custom operator overloads

**ProtocolValidator** (`_protocolValidator`, optional):
- Validates protocol implementations like `__contains__` for membership testing
- Used for `in` and `not in` operators
- Falls back to `Bool` if not provided (backwards compatibility)

**ClrMemberCache** (`_clrMemberCache`):
- Caches CLR type operator methods discovered via reflection
- Prevents expensive repeated reflection calls
- Automatically created if not provided

**PrimitiveCatalog** (static utility):
- Defines numeric type hierarchy and promotion rules
- Methods: `IsNumeric()`, `IsInteger()`, `GetPromotedType()`
- Centralized primitive type knowledge

### External Dependencies

**Parser.Ast** namespace:
- `BinaryOperator`, `UnaryOperator`, `AssignmentOperator` enums
- Defines the operator types being validated

**Logging** namespace:
- `ICompilerLogger` interface for error/warning output
- `SemanticError` for structured error reporting

**System.Reflection**:
- Used by CLR operator resolution
- `Type`, `MethodInfo`, `ParameterInfo` for reflection

### Data Flow

```
TypeChecker
    ↓ (calls ValidateBinaryOp/ValidateUnaryOp)
OperatorValidator
    ↓ (queries)
SymbolTable → UserDefinedType.OperatorMethods
    ↓ (fallback)
PrimitiveCatalog → Builtin type rules
    ↓ (fallback)
ClrMemberCache → Reflection-based CLR operators
    ↓ (on error)
ICompilerLogger + SemanticError collection
```

---

## 5. Patterns and Design Decisions

### 5.1 Multi-Strategy Resolution Pattern

**Pattern**: Try multiple resolution strategies in priority order, returning on first success.

**Implementation**:
```csharp
// User-defined → Builtin → CLR
var userResult = TryResolveUserDefined(...);
if (userResult != null) return userResult;

var builtinResult = TryResolveBuiltinOperator(...);
if (builtinResult != null) return builtinResult;

var clrResult = TryResolveClrOperator(...);
if (clrResult != null) return clrResult;

// Only report error if all strategies fail
AddError(...);
return SemanticType.Unknown;
```

**Benefits**:
- Clear precedence: user code > language semantics > platform interop
- Extensible: easy to add new resolution strategies
- Fail-safe: always returns a type (Unknown if all else fails)

### 5.2 Caching with Tuple Keys

**Pattern**: Use value tuples as dictionary keys for multi-dimensional lookups.

```csharp
private readonly Dictionary<(SemanticType, BinaryOperator, SemanticType), SemanticType?> _binaryOpCache;

var cacheKey = (left, op, right);
if (_binaryOpCache.TryGetValue(cacheKey, out var cached))
    return cached ?? SemanticType.Unknown;
```

**Benefits**:
- Natural composite key representation
- Structural equality by default (perfect for types/enums)
- Excellent performance for compiler hot paths

**Trade-offs**:
- Not thread-safe (deliberate choice)
- Memory cost increases with code size
- Cache persists for validator lifetime (per-file compilation)

### 5.3 Separation of Concerns: Validation vs. Logging

**Pattern**: Methods come in pairs—one that validates and logs, one that only validates.

```csharp
// Public method: validates AND logs errors
private SemanticType ResolveOperatorOverload(...)
{
    var result = TryResolveOperatorOverloadWithoutLogging(...);
    if (result == null)
    {
        AddError(...);  // Only log at top level
        return SemanticType.Unknown;
    }
    return result;
}

// Private helper: only validates, no logging
private SemanticType? TryResolveOperatorOverloadWithoutLogging(...)
{
    // Try user-defined, builtin, CLR...
    return null;  // Indicate failure without side effects
}
```

**Benefits**:
- Prevents duplicate error messages when trying multiple strategies
- Allows internal retry logic (e.g., complement synthesis) without noise
- Testing-friendly: can test validation logic independently of logging

### 5.4 Null Handling Conventions

**Pattern**: Use nullable return types to distinguish success/failure.

```csharp
// null = no operator found (not an error yet)
private SemanticType? TryResolveBuiltinOperator(...)

// Unknown = operator not found AND error reported
public SemanticType ValidateBinaryOp(...)  // Never returns null
```

**Convention**:
- **Internal methods** (`Try*`) return `SemanticType?` where `null` means "not found"
- **Public API** returns `SemanticType` (never null), uses `Unknown` for errors
- **Cache** stores `SemanticType?` to cache negative results

### 5.5 The "Most Specific Match" Algorithm

**Pattern**: Resolve overload ambiguity using type hierarchy specificity.

```csharp
// Type A is more specific than Type B if:
// A.IsAssignableTo(B) but not B.IsAssignableTo(A)

foreach (var candidate in assignableMatches)
{
    bool isMostSpecific = true;
    foreach (var other in assignableMatches)
    {
        if (otherParamType.IsAssignableTo(candidateParamType))
        {
            isMostSpecific = false;  // Other is more specific
            break;
        }
    }
    // ...
}
```

**Why it matters**: Matches C# overload resolution semantics and user intuition—call the most derived/specific overload available.

### 5.6 Defensive Programming

**Pattern**: Multiple layers of null checks and early returns.

```csharp
// Check prerequisites before expensive operations
if (left is not UserDefinedType udt) return null;
if (udt.Symbol == null) return null;
if (!udt.Symbol.OperatorMethods.TryGetValue(dunderName, out var methods)) return null;

// Now safe to proceed with methods
var bestOverload = ResolveBestOverload(methods, ...);
```

**Benefits**:
- Prevents null reference exceptions
- Clear, flat code structure (avoid deep nesting)
- Fail-fast on invalid preconditions

---

## 6. Debugging Tips

### 6.1 Tracing Operator Resolution

**Add logging to understand resolution path**:

```csharp
// Temporarily add debug output
private SemanticType? TryResolveOperatorOverloadWithoutLogging(...)
{
    Console.WriteLine($"Resolving {op} for {left.GetDisplayName()} and {right.GetDisplayName()}");
    
    if (left is UserDefinedType udt && ...)
    {
        Console.WriteLine($"  → Trying user-defined operators on {udt.Name}");
        // ...
    }
    
    var builtinResult = TryResolveBuiltinOperator(op, left, right);
    if (builtinResult != null)
    {
        Console.WriteLine($"  → Resolved via builtin: {builtinResult.GetDisplayName()}");
        return builtinResult;
    }
    
    Console.WriteLine($"  → Failed to resolve");
    return null;
}
```

**Better approach**: Enable compiler verbose logging if available, or add conditional compilation flags for debug builds.

### 6.2 Cache Investigation

**Problem**: Cached wrong result causing cascading type errors.

**Solution**: Clear or bypass cache during debugging:

```csharp
// Option 1: Disable caching temporarily
public SemanticType ValidateBinaryOp(...)
{
    // Comment out cache check
    // if (_binaryOpCache.TryGetValue(cacheKey, out var cached)) ...
    
    // Always compute fresh result
    SemanticType result = /* ... */;
    
    // Don't cache
    // _binaryOpCache[cacheKey] = result;
    return result;
}

// Option 2: Add cache inspection helper
public void DumpCache()
{
    foreach (var kvp in _binaryOpCache)
    {
        Console.WriteLine($"{kvp.Key} → {kvp.Value?.GetDisplayName() ?? "null"}");
    }
}
```

### 6.3 Overload Resolution Ambiguity

**Symptom**: Error says "Ambiguous overload" but you only see one definition.

**Likely causes**:
1. Inherited overloads from base classes
2. Extension methods (if supported)
3. Multiple overloads with compatible parameter types

**Debug approach**:
```csharp
private FunctionSymbol? ResolveBestOverload(...)
{
    // Add at the start
    Console.WriteLine($"Resolving overload for {operatorSymbol} on {leftTypeName}");
    Console.WriteLine($"  Candidates: {candidates.Count}");
    foreach (var c in candidates)
    {
        Console.WriteLine($"    - {c.Name}({string.Join(", ", c.Parameters.Select(p => p.Type.GetDisplayName()))})");
    }
    
    // Continue with normal logic...
}
```

### 6.4 CLR Operator Not Found

**Symptom**: CLR type has operator overload but Sharpy doesn't find it.

**Checklist**:
1. Verify operator is static: `public static T operator+(T a, T b)`
2. Check parameter types match exactly (no implicit conversions)
3. Inspect `ClrMemberCache` contents
4. Confirm CLR method name mapping is correct

**Debug helper**:
```csharp
private SemanticType? TryResolveClrOperator(...)
{
    var operators = _clrMemberCache.GetOperatorMethods(leftClrType);
    Console.WriteLine($"CLR operators on {leftClrType.Name}:");
    foreach (var op in operators)
    {
        Console.WriteLine($"  {op.Key}: {op.Value.Count} overloads");
    }
    // ...
}
```

### 6.5 Type Promotion Confusion

**Symptom**: `int + float` gives unexpected result type.

**Solution**: Check `PrimitiveCatalog.GetPromotedType()` behavior:

```csharp
private static SemanticType InferNumericResultType(SemanticType left, SemanticType right)
{
    var promoted = PrimitiveCatalog.GetPromotedType(left, right);
    
    // Debug output
    Console.WriteLine($"Promoting {left.GetDisplayName()} + {right.GetDisplayName()} → {promoted?.GetDisplayName() ?? "Unknown"}");
    
    return promoted ?? SemanticType.Unknown;
}
```

**Common gotchas**:
- `decimal` + `float` → `Unknown` (no standard promotion)
- `long` + `ulong` → `Unknown` (ambiguous without overflow checking)
- `int` + `double` → `double` (standard widening)

### 6.6 Error Location Tracking

**Problem**: Error message points to wrong line/column.

**Root cause**: Line/column passed from `TypeChecker` may be from wrong AST node.

**Investigation**:
1. Verify caller passes correct `expression.Line` and `expression.Column`
2. Check if offset is applied (some AST nodes have start/end positions)
3. Look for off-by-one errors in lexer token positions

**Temporary fix**: Add assertion to catch invalid positions:
```csharp
private void AddError(string message, int line, int column)
{
    if (line <= 0 || column <= 0)
    {
        // Invalid position - log stack trace
        Console.WriteLine($"Invalid error position: {line}:{column}");
        Console.WriteLine(Environment.StackTrace);
    }
    
    _errors.Add(new SemanticError(message, line, column));
    _logger.LogError(message, line, column);
}
```

---

## 7. Contribution Guidelines

### 7.1 Adding Support for New Operators

**Example: Adding the matrix multiplication operator `@` (Python 3.5+)**

**Step 1**: Define operator in AST enum (`Parser/Ast/Operator.cs`):
```csharp
public enum BinaryOperator
{
    // ... existing operators
    MatrixMultiply
}
```

**Step 2**: Add dunder method mapping:
```csharp
private string? BinaryOperatorToDunder(BinaryOperator op)
{
    return op switch
    {
        // ... existing mappings
        BinaryOperator.MatrixMultiply => "__matmul__",
        _ => null
    };
}
```

**Step 3**: Add operator symbol for error messages:
```csharp
private string GetOperatorSymbol(BinaryOperator op)
{
    return op switch
    {
        // ... existing mappings
        BinaryOperator.MatrixMultiply => "@",
        _ => op.ToString()
    };
}
```

**Step 4**: Add CLR mapping if applicable (optional):
```csharp
private string? BinaryOperatorToClrMethod(BinaryOperator op)
{
    return op switch
    {
        // ... existing mappings
        // Note: .NET doesn't have standard matrix multiply operator
        BinaryOperator.MatrixMultiply => null,  
        _ => null
    };
}
```

**Step 5**: Add tests in `Sharpy.Compiler.Tests/Semantic/OperatorValidatorTests.cs`:
```csharp
[Fact]
public void TestMatrixMultiplication_ValidTypes()
{
    var validator = new OperatorValidator(symbolTable);
    var matrixType = new UserDefinedType { Name = "Matrix", Symbol = matrixSymbol };
    
    var result = validator.ValidateBinaryOp(
        BinaryOperator.MatrixMultiply,
        matrixType,
        matrixType,
        1, 1);
    
    Assert.Equal(matrixType, result);
}
```

### 7.2 Improving Builtin Type Support

**Example: Adding set operations (`set & set`, `set | set`)**

Modify `TryResolveBuiltinOperator`:

```csharp
// After list concatenation logic
if (left is GenericType { Name: "set" } leftSet &&
    right is GenericType { Name: "set" } rightSet)
{
    if (op == BinaryOperator.BitwiseAnd)  // Intersection: set & set
    {
        // Return set with common element type
        if (leftSet.TypeArguments.Count > 0 && rightSet.TypeArguments.Count > 0)
        {
            var leftElem = leftSet.TypeArguments[0];
            var rightElem = rightSet.TypeArguments[0];
            
            if (leftElem.Equals(rightElem))
                return leftSet;
                
            return null;  // Incompatible element types
        }
    }
    else if (op == BinaryOperator.BitwiseOr)  // Union: set | set
    {
        // Similar logic...
    }
}
```

**Testing strategy**:
1. Test typed sets: `set[int] & set[int]`
2. Test untyped sets: `set & set`
3. Test mixed: `set[int] & set`
4. Test incompatible: `set[int] & set[str]` (should error)

### 7.3 Enhancing Overload Resolution

**Potential improvements**:

**A. Consider parameter names** (Python keyword arguments):
```python
class Processor:
    def __add__(self, value: int) -> Processor: ...
    def __add__(self, items: list[int]) -> Processor: ...

p = Processor()
p + value=5      # Could select first overload by name
p + items=[1,2]  # Could select second overload by name
```

**B. Support optional parameters**:
```python
class Logger:
    def __add__(self, message: str, level: int = 1) -> Logger: ...

log = Logger()
log + "error"  # Should match with level defaulting to 1
```

**Implementation hint**: Modify `ResolveBestOverload` to:
1. Check exact parameter count match
2. Check parameter count with defaults
3. Match by parameter names if provided

**C. Support variadic parameters** (`*args`, `**kwargs`):
```python
class Builder:
    def __call__(self, *items: int) -> Builder: ...
```

This requires extending `FunctionSymbol` to track variadic parameters.

### 7.4 Performance Optimizations

**A. Implement cache warmup**:
```csharp
public void WarmupCache(IEnumerable<(BinaryOperator, SemanticType, SemanticType)> commonOps)
{
    foreach (var (op, left, right) in commonOps)
    {
        ValidateBinaryOp(op, left, right, 0, 0);  // Precompute and cache
    }
}
```

Call during initialization with common patterns like `int + int`, `str + str`.

**B. Use more efficient cache key**:
```csharp
// Instead of full SemanticType in key, use type ID (if available)
private readonly Dictionary<(int, BinaryOperator, int), SemanticType?> _binaryOpCache;

var leftId = GetTypeId(left);
var rightId = GetTypeId(right);
var cacheKey = (leftId, op, rightId);
```

This reduces tuple size and improves hashing performance.

**C. Implement cache eviction strategy**:
```csharp
private const int MaxCacheSize = 10000;

private void AddToCache<TKey>(Dictionary<TKey, SemanticType?> cache, TKey key, SemanticType? value)
{
    if (cache.Count >= MaxCacheSize)
    {
        // Simple strategy: clear half the cache
        var keysToRemove = cache.Keys.Take(MaxCacheSize / 2).ToList();
        foreach (var k in keysToRemove)
            cache.Remove(k);
    }
    
    cache[key] = value;
}
```

Prevents unbounded memory growth in pathological cases.

### 7.5 Testing Checklist

When contributing changes, ensure:

**Unit tests**:
- [ ] Test with user-defined types having custom operators
- [ ] Test with all builtin primitive types
- [ ] Test with CLR types (e.g., `System.DateTime`)
- [ ] Test overload resolution with inheritance hierarchy
- [ ] Test equality complement synthesis
- [ ] Test augmented assignments
- [ ] Test error messages are clear and actionable

**Integration tests**:
- [ ] Compile and run complete Sharpy programs using the operators
- [ ] Verify generated C# code is correct
- [ ] Test interop with real .NET libraries

**Edge cases**:
- [ ] Empty/null types
- [ ] Generic types with missing type arguments
- [ ] Recursive type definitions
- [ ] Operator overloads with 0 or >2 parameters (malformed)
- [ ] Ambiguous overloads

**Performance**:
- [ ] No regressions in compilation time
- [ ] Cache hit rate is reasonable (>80% in typical code)

### 7.6 Code Style Guidelines

**Follow existing patterns**:
- Use early returns to avoid deep nesting
- Separate validation logic from error reporting (Try* pattern)
- Document non-obvious algorithm choices
- Use descriptive variable names (avoid single letters except in tight loops)
- Null checks before dereferencing
- Prefer pattern matching over type casting chains

**Error messages**:
- Include operator symbol: `"operator '+'"` not just `"operator Add"`
- Include type names: `'int'` and `'str'` (with quotes)
- Suggest fixes when possible
- Reference line/column accurately

**Example good error message**:
```
Type 'str' does not support operator '+' with right operand of type 'int'
Consider converting the int to str: str(value)
```

**Example bad error message**:
```
Invalid operator usage
```

---

## Summary

`OperatorValidator` is a sophisticated type resolution system that bridges Python's duck-typed operator overloading with .NET's static type system. Its multi-strategy resolution (user-defined → builtin → CLR) and caching architecture make it both powerful and performant.

**Key takeaways for contributors**:
1. **Understand the resolution order**: user operators beat builtins beat CLR
2. **Use the Try* pattern**: validate without logging, then log once at the top level
3. **Cache aggressively**: operator validation is a hot path
4. **Test thoroughly**: operators interact with every part of the type system
5. **Error messages matter**: users see these frequently, make them helpful

When debugging, start by tracing which resolution strategy is being used and why others failed. Most issues stem from misunderstanding the precedence rules or missing null checks.
