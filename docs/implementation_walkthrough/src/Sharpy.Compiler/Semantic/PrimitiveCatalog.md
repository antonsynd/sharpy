# Walkthrough: PrimitiveCatalog.cs

**Source File**: `src/Sharpy.Compiler/Semantic/PrimitiveCatalog.cs`

---

## 1. Overview

`PrimitiveCatalog` is the **single source of truth** for all primitive types in the Sharpy compiler. Think of it as a registry and rules engine that:

- **Defines** what types count as primitives (int, float, str, bool, etc.)
- **Maps** between Sharpy names (`str`), C# names (`string`), and .NET runtime types
- **Categorizes** primitives by their numeric characteristics (signed int, unsigned int, float, etc.)
- **Implements** type promotion rules (e.g., `int + float` → `float`)
- **Validates** implicit and explicit conversions between types

This is used throughout the semantic analysis phase whenever the compiler needs to reason about primitive types, perform type checking, or determine whether operations are valid.

### Role in the Compiler Pipeline

```
Parser (AST) → Semantic Analysis → Code Generation
                      ↑
                PrimitiveCatalog
                (consulted during type checking)
```

When the `TypeChecker` encounters operations like `x + y`, it uses `PrimitiveCatalog` to determine if the types are compatible and what the result type should be.

---

## 2. Class/Type Structure

### 2.1 `NumericKind` Enum

```csharp
public enum NumericKind
{
    None,            // Non-numeric: void, bool, string, char
    SignedInteger,   // sbyte, short, int, long
    UnsignedInteger, // byte, ushort, uint, ulong
    FloatingPoint,   // float, double
    Decimal          // decimal (128-bit)
}
```

**Purpose**: Groups primitives by their numeric nature. This drives conversion rules:
- Floats and decimals **cannot mix** (would lose precision guarantees)
- Signed/unsigned integers of the same size need special promotion logic
- Non-numeric types follow different rules

### 2.2 `PrimitiveInfo` Record

```csharp
public record PrimitiveInfo(
    string SharpyName,    // "int", "str" (what users write)
    string CSharpName,    // "int", "string" (what gets emitted)
    Type ClrType,         // typeof(int), typeof(string)
    NumericKind Kind,     // SignedInteger, FloatingPoint, etc.
    int SizeInBits,       // 8, 16, 32, 64, 128 (0 for non-numeric)
    bool IsSigned         // true for signed types
);
```

**Purpose**: Immutable descriptor for each primitive. Contains all metadata needed for type operations.

**Key Design Decision**: Using a `record` makes instances immutable and value-comparable by default, which is perfect for compiler metadata.

### 2.3 Static Dictionaries

```csharp
private static readonly FrozenDictionary<string, PrimitiveInfo> _bySharpyName;
private static readonly FrozenDictionary<Type, PrimitiveInfo> _byClrType;
```

**Why `FrozenDictionary`?**
- Read-only after initialization (frozen in static constructor)
- Better performance than regular `Dictionary` for lookups
- Thread-safe by design (important for compiler parallelization)

**Two indices**: Allows O(1) lookup by either Sharpy name (`"int"`) or CLR type (`typeof(int)`).

---

## 3. Key Functions/Methods

### 3.1 Static Constructor & Registration

```csharp
static PrimitiveCatalog()
{
    var byName = new Dictionary<string, PrimitiveInfo>();
    var byClr = new Dictionary<Type, PrimitiveInfo>();
    RegisterAll(byName, byClr);
    _bySharpyName = byName.ToFrozenDictionary();
    _byClrType = byClr.ToFrozenDictionary();
}
```

**What it does**: Initializes both dictionaries at program startup (before any code runs).

**Pattern**: Build mutable dictionaries, populate them, then freeze them. This is a common pattern for static registries.

#### `RegisterAll()` Method

Registers all primitives in categorized sections:
1. **Signed integers** (sbyte → long)
2. **Unsigned integers** (byte → ulong)
3. **Floating-point** (float, double, decimal)
4. **Non-numeric** (bool, char, str, object)
5. **Void/None** (with `str`/`string` and `None`/`void` aliases)

**Important Details**:
- `str` and `string` both map to C# `string` (alias support)
- `None` and `void` both map to C# `void` (Pythonic vs. C# naming)
- Size is 0 for reference types (string, object) since they're not value types

### 3.2 Query Methods

#### `GetByName(string sharpyName)` and `GetByClrType(Type clrType)`

```csharp
public static PrimitiveInfo? GetByName(string sharpyName)
    => _bySharpyName.GetValueOrDefault(sharpyName);
```

**Returns**: `null` if not found (nullable return type).

**Usage**: Parser sees `int` → calls `GetByName("int")` → gets full metadata.

#### `GetPrimitiveInfo(SemanticType type)`

```csharp
public static PrimitiveInfo? GetPrimitiveInfo(SemanticType type)
{
    if (type is BuiltinType builtin)
    {
        // Try CLR type first (more reliable)
        if (builtin.ClrType != null && _byClrType.TryGetValue(builtin.ClrType, out var info))
            return info;
        // Fall back to name lookup
        return _bySharpyName.GetValueOrDefault(builtin.Name);
    }
    return null;
}
```

**Why CLR type first?**: The CLR type is canonical—names can have aliases, but `typeof(string)` is unique.

**Pattern**: Try the most reliable lookup first, then fall back to name matching.

#### Helper Predicates

```csharp
IsNumeric(type)      // Any numeric type
IsInteger(type)      // Signed or unsigned integer
IsFloatingPoint(type) // float or double
IsDecimal(type)      // decimal only
```

These are **semantic sugar** over `GetPrimitiveInfo()` + checking the `Kind`. Used extensively in `TypeChecker` for readable validation:

```csharp
if (PrimitiveCatalog.IsNumeric(leftType) && PrimitiveCatalog.IsNumeric(rightType))
{
    // Can perform arithmetic
}
```

### 3.3 Numeric Promotion Rules

#### `GetPromotionPriority(PrimitiveInfo info)`

**Purpose**: Assigns a numeric "weight" to each type for determining promotion.

**Algorithm**:
```
decimal:  100  (isolated, doesn't mix with floats)
double:    50
float:     40
ulong:     35
long:      34
uint:      33
int:       32
ushort:    31
short:     30
byte:      29
sbyte:     28
```

**Design Rationale**:
- Higher priority = wider type
- Decimal gets special treatment (value 100) to prevent mixing with floats
- Order follows .NET standard conversion rules

#### `GetPromotedType(PrimitiveInfo left, PrimitiveInfo right)`

**The Core Logic**: Determines result type for binary operations like `+`, `-`, `*`, `/`.

**Rules Implemented**:

1. **Non-numeric rejection**:
   ```csharp
   if (left.Kind == NumericKind.None || right.Kind == NumericKind.None)
       return null;
   ```
   Can't promote `bool + int`.

2. **Decimal isolation**:
   ```csharp
   if ((left.Kind == NumericKind.Decimal) != (right.Kind == NumericKind.Decimal))
       return null;
   ```
   `decimal + float` is **not allowed** (matches C# semantics—prevents silent precision loss).

3. **Mixed signedness with same size** (special case):
   ```csharp
   if (left is signed && right is unsigned && same size) {
       // Promote to next larger signed type
       8-bit  → short (16-bit)
       16-bit → int (32-bit)
       32-bit → long (64-bit)
       64-bit → null (ERROR: no safe promotion exists)
   }
   ```
   
   **Why?**: Mixing `int` and `uint` could overflow. C# promotes to `long` for safety. But `long + ulong` has nowhere to go—requires explicit cast.

4. **Standard promotion**:
   ```csharp
   return leftPriority >= rightPriority ? left : right;
   ```
   Just pick the wider type: `float + int` → `float`.

#### `GetPromotedType(SemanticType left, SemanticType right)` Overload

**Why two versions?** One works with `PrimitiveInfo` (metadata), one works with `SemanticType` (AST types).

**Key optimization**:
```csharp
return promoted.ClrType switch
{
    Type t when t == typeof(int) => SemanticType.Int,      // Singleton
    Type t when t == typeof(long) => SemanticType.Long,    // Singleton
    // ...
    _ => new BuiltinType { ... }  // Create instance for rare types
};
```

**Why?**: Common types (int, long, float, double) have **singleton instances** in `SemanticType` to reduce allocations. Rare types (sbyte, ushort) get new instances only when needed.

**Performance tip**: In a large codebase, creating millions of `BuiltinType` objects for `int` would be wasteful. Singletons solve this.

### 3.4 Conversion Checking

#### `CanImplicitlyConvert(PrimitiveInfo from, PrimitiveInfo to)`

**Purpose**: Determines if a type can be converted **without explicit cast** and **without data loss**.

**Key Rules**:

1. **Void cannot convert**:
   ```csharp
   if (from.ClrType == typeof(void) || to.ClrType == typeof(void))
       return false;
   ```

2. **Same type**:
   ```csharp
   if (from.ClrType == to.ClrType)
       return true;
   ```

3. **Decimal accepts integers only**:
   ```csharp
   if (to.Kind == NumericKind.Decimal)
       return from.Kind is SignedInteger or UnsignedInteger;
   ```
   Can't implicitly convert `float` → `decimal` (precision semantics differ).

4. **Decimal converts to nothing**:
   ```csharp
   if (from.Kind == NumericKind.Decimal)
       return false;
   ```
   Must explicitly cast `decimal` to anything else.

5. **Integer → Float/Double**: Allowed (matches C# spec, though precision may be lost for large `long` values).

6. **Float → Double**: Allowed (widening).

7. **Integer widening**:
   ```csharp
   // Unsigned → Signed requires extra bit
   if (!from.IsSigned && to.IsSigned)
       return to.SizeInBits > from.SizeInBits;
   
   // Signed → Unsigned: NOT implicit
   if (from.IsSigned && !to.IsSigned)
       return false;
   
   // Same signedness: size must be >=
   return to.SizeInBits >= from.SizeInBits;
   ```

   **Examples**:
   - `byte` (unsigned 8-bit) → `short` (signed 16-bit): ✅ (needs extra bit)
   - `byte` (unsigned 8-bit) → `sbyte` (signed 8-bit): ❌ (would overflow)
   - `int` (signed) → `uint` (unsigned): ❌ (negative values would corrupt)
   - `short` → `int`: ✅ (widening with same signedness)

#### `CanExplicitlyConvert(PrimitiveInfo from, PrimitiveInfo to)`

**Purpose**: Determines if explicit cast (potentially lossy) is allowed.

**Rules**:
- Void cannot convert (no exception)
- Any numeric → any numeric: ✅ (even if lossy, explicit cast allows it)
- `char` ↔ integer: ✅ (C# allows explicit cast between char and int)
- Falls back to implicit check for non-numeric types

**Example**: `long` → `int` loses upper 32 bits, but explicit cast is allowed.

---

## 4. Dependencies

### Internal Dependencies (within Sharpy.Compiler.Semantic)

- **`SemanticType` hierarchy**: `PrimitiveCatalog` works with `BuiltinType` records and returns `SemanticType` instances.
- **`TypeChecker`**: Calls `GetPromotedType()`, `CanImplicitlyConvert()` during type validation.
- **`TypeResolver`**: Uses `GetByName()` to resolve type annotations like `: int`.
- **`BuiltinRegistry`**: Coordinates with primitive definitions (though `PrimitiveCatalog` is the source of truth for primitives themselves).

### External Dependencies

- **`System.Collections.Frozen`**: For `FrozenDictionary` (read-only, optimized dictionaries).
- **`System.Type`**: Uses .NET reflection types as canonical identifiers.

### Dependents

Any code that needs to:
- Check if a type is numeric
- Determine result type of arithmetic operations
- Validate type conversions
- Emit correct C# primitive names

---

## 5. Patterns and Design Decisions

### 5.1 Static Registry Pattern

**Why static?** Primitives are fixed at compile-time. No need for instance state or dependency injection.

**Thread safety**: `FrozenDictionary` is immutable after initialization, so concurrent access is safe.

### 5.2 Exhaustive Documentation Comments

Each section has numbered comments (1.2.1, 1.2.2, etc.), suggesting this file serves as **documentation** as much as code. The structure mirrors a specification document.

### 5.3 Separation of Concerns

- **Metadata** (PrimitiveInfo) vs. **Semantic Type** (SemanticType): Keeps compiler infrastructure separate from AST representation.
- **Promotion** vs. **Conversion**: Distinct methods for "what's the result type?" vs. "can this assignment happen?".

### 5.4 Null-Safe API

All lookup methods return **nullable types** (`PrimitiveInfo?`, `SemanticType?`). Callers must handle `null` explicitly, preventing null reference exceptions.

### 5.5 Performance Optimizations

1. **FrozenDictionary**: Faster lookup than regular `Dictionary`.
2. **Singleton SemanticTypes**: Reuse instances for common types (int, float, etc.).
3. **Priority-based promotion**: O(1) switch statements instead of complex algorithms.

### 5.6 Alignment with C# Semantics

The promotion and conversion rules **mirror C# exactly**. This ensures generated C# code behaves predictably and avoids surprises when interoperating with .NET libraries.

---

## 6. Debugging Tips

### 6.1 Tracing Type Promotion Issues

If you see unexpected type errors like "cannot add int and float":

1. **Set a breakpoint** in `GetPromotedType()`.
2. **Inspect** `leftInfo` and `rightInfo` to verify they're being looked up correctly.
3. **Check** if one type is `null` (indicates lookup failure).
4. **Trace** the priority calculation—maybe a new type wasn't registered.

### 6.2 Adding Debug Logging

Add a method to dump all registered primitives:

```csharp
public static void DumpCatalog()
{
    foreach (var (name, info) in _bySharpyName)
    {
        Console.WriteLine($"{name} -> {info.CSharpName} " +
                          $"({info.Kind}, {info.SizeInBits}-bit)");
    }
}
```

Call from `Compiler.Main()` to see what's registered.

### 6.3 Testing Conversion Rules

Write unit tests that assert expected conversions:

```csharp
[Fact]
public void ByteCanImplicitlyConvertToShort()
{
    var from = PrimitiveCatalog.GetByName("byte");
    var to = PrimitiveCatalog.GetByName("short");
    Assert.True(PrimitiveCatalog.CanImplicitlyConvert(from, to));
}
```

### 6.4 Common Pitfalls

- **Forgetting to register a new primitive**: If you add a type, update `RegisterAll()`.
- **Mixing decimal and float**: Remember they **don't mix**—this is intentional per C# spec.
- **Assuming name uniqueness**: Use CLR type for reliable comparisons, not names (due to aliases).

---

## 7. Contribution Guidelines

### 7.1 Adding a New Primitive Type

**When?** Rarely needed—most primitives are already defined. But if Sharpy adds a new builtin (e.g., `complex` numbers):

1. **Add to `RegisterAll()`**:
   ```csharp
   Register(byName, byClr, new PrimitiveInfo(
       "complex", "System.Numerics.Complex", 
       typeof(System.Numerics.Complex), 
       NumericKind.None,  // Or create a new kind
       0, false
   ));
   ```

2. **Update promotion rules** if needed:
   - Add case in `GetPromotionPriority()`
   - Add logic in `GetPromotedType()` for how it mixes with other types

3. **Add conversion rules** in `CanImplicitlyConvert()` and `CanExplicitlyConvert()`.

4. **Write tests** in `Sharpy.Compiler.Tests/Semantic/PrimitiveCatalogTests.cs`.

### 7.2 Modifying Promotion Rules

**When?** If C# changes its spec, or if Sharpy needs to diverge for Pythonic behavior.

**Steps**:
1. Update the logic in `GetPromotedType()`.
2. Document **why** with a comment (e.g., "Deviates from C# to match Python's `int` promotion").
3. Add test cases covering the change.
4. Update `docs/specs/type_system.md` to reflect the new rules.

### 7.3 Performance Improvements

**Ideas**:
- Cache promotion results if profiling shows repeated lookups.
- Replace `switch` expressions with arrays indexed by enum for `GetPromotionPriority()`.

**Before optimizing**:
- **Profile first**: Use BenchmarkDotNet to measure actual impact.
- Don't optimize unless this shows up as a bottleneck (unlikely for a static registry).

### 7.4 Testing Additions

All changes to conversion or promotion logic **must** include tests:

```csharp
// In Sharpy.Compiler.Tests/Semantic/PrimitiveCatalogTests.cs
[Theory]
[InlineData("int", "float", true)]    // Can implicitly convert
[InlineData("long", "int", false)]   // Cannot (narrowing)
public void ImplicitConversionRules(string from, string to, bool expected)
{
    var fromInfo = PrimitiveCatalog.GetByName(from);
    var toInfo = PrimitiveCatalog.GetByName(to);
    Assert.Equal(expected, 
                 PrimitiveCatalog.CanImplicitlyConvert(fromInfo, toInfo));
}
```

### 7.5 Documentation Requirements

- **Inline comments**: Explain **why**, not **what**. The code is self-documenting for "what".
- **Architecture docs**: Update `docs/architecture/semantic-analyzer-architecture.md` if structure changes.
- **Spec docs**: Update `docs/specs/type_system.md` if promotion/conversion rules change.

### 7.6 Code Style

- Use **expression-bodied members** for simple methods (`=> _dict.GetValueOrDefault(key)`).
- Keep **pattern matching** readable—avoid deeply nested switches.
- Prefer **early returns** for validation (reduces nesting).

---

## 8. Related Files to Explore

After understanding `PrimitiveCatalog`, check out:

1. **`SemanticType.cs`**: See how `BuiltinType` and other semantic types are defined.
2. **`TypeChecker.cs`**: See how `PrimitiveCatalog` is used during type checking.
3. **`RoslynEmitter.cs`** (in CodeGen): See how `CSharpName` is used to emit correct C# code.
4. **`BuiltinRegistry.cs`**: See how builtin functions (`len()`, `print()`) work alongside primitives.
5. **Tests**: `Sharpy.Compiler.Tests/Semantic/PrimitiveCatalogTests.cs` (if it exists) or integration tests showing type promotion in action.

---

## 9. Quick Reference: Common Scenarios

### Scenario 1: Check if a type is numeric

```csharp
if (PrimitiveCatalog.IsNumeric(type))
{
    // Can perform arithmetic
}
```

### Scenario 2: Get promoted type for binary operation

```csharp
var resultType = PrimitiveCatalog.GetPromotedType(leftType, rightType);
if (resultType == null)
{
    // Error: incompatible types
}
```

### Scenario 3: Check if assignment is valid

```csharp
var fromInfo = PrimitiveCatalog.GetPrimitiveInfo(valueType);
var toInfo = PrimitiveCatalog.GetPrimitiveInfo(targetType);
if (fromInfo != null && toInfo != null && 
    !PrimitiveCatalog.CanImplicitlyConvert(fromInfo, toInfo))
{
    // Error: cannot implicitly convert
}
```

### Scenario 4: Lookup Sharpy name → C# name for codegen

```csharp
var info = PrimitiveCatalog.GetByName(sharpyTypeName);
var csharpName = info?.CSharpName;  // e.g., "str" → "string"
```

---

## 10. Summary

`PrimitiveCatalog` is a **foundational component** that encodes the rules of Sharpy's primitive type system. It's designed to be:

- **Authoritative**: Single source of truth
- **Fast**: Frozen dictionaries and singletons
- **Safe**: Null-safe APIs, immutable data
- **Maintainable**: Clear structure, well-documented
- **Aligned**: Matches C# semantics for interop

Understanding this file is key to working on type checking, code generation, or any compiler feature involving primitive types. It's also a great example of the **static registry pattern** used throughout the compiler for managing compile-time metadata.

---

**Next Steps**: Try adding a debug method to `PrimitiveCatalog.DumpCatalog()` and call it from `Compiler.cs` to see all registered primitives. Then trace through `TypeChecker.cs` to see promotion rules in action!
