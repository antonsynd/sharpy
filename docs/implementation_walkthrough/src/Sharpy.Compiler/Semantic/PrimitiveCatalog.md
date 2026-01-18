# Walkthrough: PrimitiveCatalog.cs

**Source File**: `src/Sharpy.Compiler/Semantic/PrimitiveCatalog.cs`

---

## Overview

`PrimitiveCatalog` is a static registry that serves as the **authoritative source** for all primitive type information in the Sharpy compiler. It acts as a bridge between Sharpy's type syntax (like `int32`, `str`, `float`) and the underlying .NET CLR types (like `System.Int32`, `System.String`, `System.Double`).

**Role in the Compiler Pipeline:**
- **Used by**: Parser (type resolution), Semantic Analysis (type checking, promotion), CodeGen (CLR type emission)
- **Purpose**:
  - Map Sharpy type names to CLR types
  - Provide numeric promotion rules for arithmetic operations
  - Determine implicit/explicit conversion compatibility
  - Support both Sharpy-style (`int32`, `str`) and C#-style (`int`, `string`) type aliases

This is a **read-only, immutable catalog** initialized at startup using `FrozenDictionary` for optimal lookup performance.

---

## Class/Type Structure

### `PrimitiveCatalog` (Static Class)

The main class containing all registry data and query methods.

### `NumericKind` (Enum)

Categorizes primitives by their numeric characteristics:

```csharp
public enum NumericKind
{
    None,           // Not numeric: void, bool, string, char
    SignedInteger,  // sbyte, short, int, long
    UnsignedInteger,// byte, ushort, uint, ulong
    FloatingPoint,  // float, double
    Decimal         // decimal (128-bit precision)
}
```

**Why this matters**: The `NumericKind` drives critical type system behavior:
- **Promotion rules**: Only numeric types can be promoted (combined in arithmetic)
- **Mixing restrictions**: `Decimal` cannot mix with `FloatingPoint` types
- **Conversion rules**: Different rules apply for integer widening vs float conversions

### `PrimitiveInfo` (Record)

Immutable descriptor for each primitive type:

```csharp
public record PrimitiveInfo(
    string SharpyName,   // "int32", "str", "float"
    string CSharpName,   // "int", "string", "double"
    Type ClrType,        // typeof(int), typeof(string)
    NumericKind Kind,    // Classification
    int SizeInBits,      // 8, 16, 32, 64, 128 (0 for non-numeric)
    bool IsSigned        // true for signed integers/floats
);
```

**Key Design Decision**: Using a `record` provides:
- Immutability (thread-safe, no accidental mutation)
- Value semantics (equality by content)
- Concise syntax with positional parameters

### Two-Way Lookup Dictionaries

```csharp
private static readonly FrozenDictionary<string, PrimitiveInfo> _bySharpyName;
private static readonly FrozenDictionary<Type, PrimitiveInfo> _byClrType;
```

**Why `FrozenDictionary`?**
- Optimized for read-heavy workloads (no writes after initialization)
- Lower memory overhead than `Dictionary`
- Faster lookups than regular dictionaries
- Perfect for static, immutable registries

**Dual indexing** allows fast lookups in both directions:
- **Sharpy → CLR**: Parser resolves `int32` to `typeof(int)`
- **CLR → Sharpy**: CodeGen translates `typeof(int)` back to `"int"` for emission

---

## Key Functions/Methods

### Registration and Initialization

#### Static Constructor (lines 44-51)

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

**Pattern**: Build in mutable dictionaries, freeze for production use. This is executed once when the type is first accessed.

#### `RegisterAll()` Method (lines 59-101)

Registers all primitives with **both Sharpy-style and C#-style aliases**:

```csharp
// Sharpy-style (primary per language spec)
Register(..., new PrimitiveInfo("int32", "int", typeof(int), ...));

// C#-style alias (for familiarity)
Register(..., new PrimitiveInfo("int", "int", typeof(int), ...));
```

**Critical Categories**:

1. **Signed Integers** (lines 62-70):
   - Sharpy names: `int8`, `int16`, `int32`, `int64`
   - C# aliases: `sbyte`, `short`, `int`, `long`
   - Comment `1.2.1` references language spec section

2. **Unsigned Integers** (lines 73-81):
   - Sharpy names: `uint8`, `uint16`, `uint32`, `uint64`
   - C# aliases: `byte`, `ushort`, `uint`, `ulong`
   - Comment `1.2.2` references language spec section

3. **Floating-Point** (lines 84-89):
   - `float32` → C# `float` (32-bit)
   - `float` / `float64` / `double` → C# `double` (64-bit)
   - `decimal` → C# `decimal` (128-bit, for financial precision)
   - **Important**: Sharpy's default `float` is 64-bit (C# `double`), unlike C# where `float` is 32-bit

4. **Non-Numeric** (lines 92-96):
   - `bool`, `char`, `str` (Sharpy-style) / `string` (C# alias), `object`
   - Size is 0 for reference types

5. **Void/None** (lines 99-100):
   - `None` (Pythonic) / `void` (C#-style) both map to `typeof(void)`
   - Represents absence of a value in function returns

**Design Note**: The comments reference spec section `1.2.x`, showing tight coupling to language design docs.

---

### Query Methods (Section 1.3, lines 103-166)

#### `GetByName(string sharpyName)` → `PrimitiveInfo?` (lines 106-107)

```csharp
public static PrimitiveInfo? GetByName(string sharpyName)
    => _bySharpyName.GetValueOrDefault(sharpyName);
```

**Used by**: Parser when resolving type annotations like `x: int32`

**Returns**: `null` if not a primitive (e.g., custom class names)

**Connects to Downstream**: CodeGen uses the returned `CSharpName` to emit correct C# syntax

#### `GetByClrType(Type clrType)` → `PrimitiveInfo?` (lines 110-111)

Reverse lookup: given a .NET `Type`, find the primitive info. Used during semantic analysis when working with resolved types.

#### `GetPrimitiveInfo(SemanticType type)` → `PrimitiveInfo?` (lines 121-132)

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

**Why CLR-first lookup?** CLR types are canonical and unambiguous (no alias conflicts). Names like `str`/`string` both map to `typeof(string)`.

**Used by**: Semantic analysis when checking if a resolved type is primitive.

**Connects to Upstream**: Receives `SemanticType` instances created by the Parser

#### Type Classification Helpers (lines 134-161)

```csharp
IsNumeric(SemanticType)       // Any numeric type?
IsInteger(SemanticType)       // Signed or unsigned integer?
IsFloatingPoint(SemanticType) // float or double?
IsDecimal(SemanticType)       // decimal?
IsPrimitive(string)           // Is name a registered primitive?
```

**Pattern**: All delegate to `GetPrimitiveInfo()` then check `Kind`. Clean separation of concerns.

**Usage in Semantic Analysis**: Used extensively in type checking for readable validation:

```csharp
if (PrimitiveCatalog.IsNumeric(leftType) && PrimitiveCatalog.IsNumeric(rightType))
{
    // Can perform arithmetic
}
```

#### `GetAllPrimitives()` (lines 164-165)

Returns all registered primitives for iteration—useful for debugging and tooling.

---

### Numeric Promotion Rules (Section 1.4, lines 167-272)

#### `GetPromotionPriority(PrimitiveInfo)` → `int` (lines 171-195)

**Purpose**: Assigns numeric "rank" to determine which type wins in mixed arithmetic.

```csharp
private static int GetPromotionPriority(PrimitiveInfo info)
{
    return info.ClrType switch
    {
        Type when info.Kind == NumericKind.Decimal => 100,  // Highest
        Type t when t == typeof(double) => 50,
        Type t when t == typeof(float) => 40,
        Type t when t == typeof(ulong) => 35,
        Type t when t == typeof(long) => 34,
        // ... rest in descending order ...
    };
}
```

**Key Insight**: Higher priority = wider type. The hierarchy:
1. `decimal` (100) - special case, doesn't mix with floats
2. `double` (50) > `float` (40)
3. Integers by size: `ulong` (35) > `long` (34) > ... > `sbyte` (28)

**Design Rationale**:
- Follows .NET standard conversion rules
- Decimal gets special treatment (value 100) to prevent mixing with floats
- Void gets priority 0 (special case, line 174)

#### `GetPromotedType(PrimitiveInfo, PrimitiveInfo)` → `PrimitiveInfo?` (lines 202-236)

**Core logic for binary operator type checking** (e.g., `int + float → float`).

```csharp
public static PrimitiveInfo? GetPromotedType(PrimitiveInfo left, PrimitiveInfo right)
{
    // 1. Both must be numeric
    if (left.Kind == NumericKind.None || right.Kind == NumericKind.None)
        return null;

    // 2. Decimal doesn't mix with float/double
    if ((left.Kind == NumericKind.Decimal) != (right.Kind == NumericKind.Decimal))
        return null;

    // 3. Special case: signed + unsigned of same size → promote to larger signed
    if (left.SizeInBits == right.SizeInBits && /* mixing signed/unsigned */)
    {
        return left.SizeInBits switch
        {
            8 => GetByName("short"),   // sbyte + byte → short
            16 => GetByName("int"),    // short + ushort → int
            32 => GetByName("long"),   // int + uint → long
            64 => null                 // long + ulong: ERROR (no safe promotion)
        };
    }

    // 4. Otherwise, return higher priority type
    return leftPriority >= rightPriority ? left : right;
}
```

**Critical Cases**:
- `int32 + float` → `float` (float has higher priority)
- `int32 + uint32` → `int64` (safe promotion to avoid overflow, line 225)
- `int64 + uint64` → `null` (ERROR: no safe 64-bit signed+unsigned mix, line 226)
- `decimal + float` → `null` (ERROR: cannot mix decimal with float, lines 209-210)

**Algorithm**:
1. **Non-numeric rejection** (lines 205-206): Can't promote `bool + int`
2. **Decimal isolation** (lines 209-210): Prevents silent precision loss
3. **Mixed signedness handling** (lines 214-229): Special safety promotion for same-size signed/unsigned mixing
4. **Standard promotion** (lines 232-235): Pick the wider type based on priority

**Connects to Downstream**: Result used by CodeGen to emit correct cast operations

#### `GetPromotedType(SemanticType, SemanticType)` → `SemanticType?` (lines 239-272)

**Overload for semantic analysis** - wraps the `PrimitiveInfo` version and returns semantic types.

**Key Optimization** (lines 252-271): Returns singleton instances for common types:

```csharp
return promoted.ClrType switch
{
    Type t when t == typeof(int) => SemanticType.Int,      // Singleton
    Type t when t == typeof(long) => SemanticType.Long,    // Singleton
    Type t when t == typeof(float) => SemanticType.Float32,
    Type t when t == typeof(double) => SemanticType.Double,
    // Less common types: create new BuiltinType instances
    Type t when t == typeof(sbyte) => new BuiltinType { Name = "sbyte", ... },
    ...
};
```

**Why singletons?** Common types like `int` are used heavily; reusing instances reduces allocations during compilation.

**Connects to Upstream**: Receives `SemanticType` from Parser AST nodes
**Connects to Downstream**: Returns `SemanticType` used throughout semantic analysis

---

### Conversion Checking (Section 1.5, lines 274-348)

#### `CanImplicitlyConvert(PrimitiveInfo from, PrimitiveInfo to)` → `bool` (lines 279-324)

**Determines if conversion is safe without casts** (no data loss).

**Rules Implemented**:

1. **Void cannot convert** (lines 282-283):
   ```csharp
   if (from.ClrType == typeof(void) || to.ClrType == typeof(void))
       return false;
   ```

2. **Same type** (lines 285-286): Always allowed

3. **Non-numeric only convert to themselves** (lines 289-290)

4. **Decimal accepts integers only** (lines 293-294):
   ```csharp
   if (to.Kind == NumericKind.Decimal)
       return from.Kind == NumericKind.SignedInteger || from.Kind == NumericKind.UnsignedInteger;
   ```
   Can't implicitly convert `float` → `decimal` (precision semantics differ).

5. **Decimal converts to nothing** (lines 297-298): Must explicitly cast `decimal` to anything else

6. **Integer → Float/Double** (lines 301-303): Allowed (matches C# spec, though precision may be lost for large `long` values)

7. **Float → Double** (lines 306-307): Allowed (widening)

8. **Integer widening** (lines 310-321):
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
- `int8 → int32`: ✅ (widening, same signedness)
- `uint32 → int64`: ✅ (unsigned to larger signed)
- `int32 → uint32`: ❌ (signed to unsigned not implicit)
- `int64 → float`: ✅ (per C# spec, though precision may be lost)
- `float → int32`: ❌ (would lose fractional part)

**Connects to Downstream**: Used by semantic analyzer to validate assignments and parameter passing

#### `CanExplicitlyConvert(PrimitiveInfo from, PrimitiveInfo to)` → `bool` (lines 329-348)

**Explicit casts** allow potentially lossy conversions:

```csharp
public static bool CanExplicitlyConvert(PrimitiveInfo from, PrimitiveInfo to)
{
    // Void cannot convert
    if (from.ClrType == typeof(void) || to.ClrType == typeof(void))
        return false;

    // Any numeric → any numeric: allowed
    if (from.Kind != NumericKind.None && to.Kind != NumericKind.None)
        return true;

    // char ↔ integer: allowed
    if (/* char to int OR int to char */)
        return true;

    return CanImplicitlyConvert(from, to); // Implicit also explicit
}
```

**Examples**:
- `int64 → int8`: ✅ (explicit, may truncate)
- `float → int32`: ✅ (explicit, loses fractional)
- `double → decimal`: ✅ (explicit)
- `char → int`: ✅ (get Unicode code point, lines 340-345)

**Important Note** (line 347): All implicit conversions are also explicit (they fall through to the final return).

---

## Dependencies

### Internal Dependencies
- **`SemanticType`**: Base class for type representations (used throughout methods)
- **`BuiltinType`**: Specific `SemanticType` subclass for primitives (checked in `GetPrimitiveInfo`)
- **Semantic Analyzer**: Calls promotion/conversion methods during type checking
- **`TypeMapper`**: Uses CLR type mappings during code generation (in CodeGen phase)

### External Dependencies
- **`System.Collections.Frozen`**: .NET 8+ optimized frozen collections (line 1)
- **`System.Type`**: CLR reflection system for type metadata

### Cross-References
- **[BuiltinRegistry.md](BuiltinRegistry.md)**: Manages built-in functions like `len()`, `print()`
- **[TypeMapper.md](TypeMapper.md)**: Translates semantic types to Roslyn syntax (depends on `PrimitiveCatalog`)
- **[OperatorValidator.md](OperatorValidator.md)**: Uses promotion rules to validate operators
- **[NameResolver.md](NameResolver.md)**: Uses primitive lookups during name resolution

---

## Patterns and Design Decisions

### 1. **Static Catalog Pattern**
- Entire class is `static` with no instance state
- All data initialized once in static constructor
- Thread-safe by design (immutable after init)

### 2. **Frozen Collections for Performance**
- `FrozenDictionary` chosen for read-heavy workloads
- Trade slightly higher startup cost for faster runtime lookups
- Critical since type lookups happen thousands of times per compilation

### 3. **Dual Aliasing Strategy**
- Supports both Sharpy-style (`int32`) and C#-style (`int`) names
- Makes language approachable for C# developers
- Spec comments (`1.2.1`, `1.2.2`) tie to formal language design

### 4. **Explicit Promotion Priority Hierarchy**
- `GetPromotionPriority()` encodes .NET numeric type hierarchy
- Matches C# language spec for consistency with target platform
- Special handling for `decimal` (doesn't mix with floats)

### 5. **Null-Safe Query Methods**
- All query methods return nullable types (`PrimitiveInfo?`, `SemanticType?`)
- Forces callers to handle "not a primitive" cases explicitly
- Prevents NullReferenceExceptions downstream

### 6. **Separation of Concerns**
- Registration logic (`RegisterAll`) separate from query logic
- Promotion rules separate from conversion rules
- Each method has single, clear responsibility

### 7. **Alignment with C# Semantics**
- The promotion and conversion rules **mirror C# exactly**
- Ensures generated C# code behaves predictably
- Avoids surprises when interoperating with .NET libraries

---

## Debugging Tips

### Common Issues and How to Diagnose

#### 1. **Type Not Found Errors**
**Symptom**: `GetByName()` returns `null` for a valid-looking type name.

**Check**:
- Is the type name using the correct case? (`int` vs `Int`)
- Is it a Sharpy primitive or a user-defined type?
- Call `GetAllPrimitives()` to dump registered types:
  ```csharp
  foreach (var (name, info) in PrimitiveCatalog.GetAllPrimitives())
      Console.WriteLine($"{name} -> {info.CSharpName}");
  ```

#### 2. **Promotion Returning Null**
**Symptom**: `GetPromotedType()` returns `null` for seemingly compatible types.

**Check**:
- Are both types numeric? (Call `IsNumeric()` on each)
- Are you mixing `decimal` with `float`/`double`? (Not allowed, lines 209-210)
- Are you mixing `long` + `ulong`? (No safe promotion exists, line 226)
- Add logging:
  ```csharp
  var leftInfo = GetPrimitiveInfo(left);
  var rightInfo = GetPrimitiveInfo(right);
  Console.WriteLine($"Left: {leftInfo?.Kind}, Right: {rightInfo?.Kind}");
  ```

#### 3. **Conversion Failures**
**Symptom**: Semantic analyzer rejects a conversion you expect to work.

**Check**:
- Is it implicit or explicit? (`int → float` implicit, `float → int` explicit only)
- Use the REPL to test:
  ```csharp
  var from = PrimitiveCatalog.GetByName("float");
  var to = PrimitiveCatalog.GetByName("int");
  Console.WriteLine($"Implicit: {CanImplicitlyConvert(from, to)}");
  Console.WriteLine($"Explicit: {CanExplicitlyConvert(from, to)}");
  ```

#### 4. **Signed/Unsigned Confusion**
**Symptom**: Unexpected promotion results with mixed signedness.

**Debug Strategy**:
- Check `IsSigned` and `SizeInBits` for both operands
- Remember: `uint32 + int32 → int64` (promotes to larger signed, line 225)
- Add assertions in tests:
  ```csharp
  var result = GetPromotedType(uint32Info, int32Info);
  Assert.Equal("long", result?.SharpyName);
  ```

### Useful Debug Queries

```csharp
// List all integer types
foreach (var (name, info) in PrimitiveCatalog.GetAllPrimitives())
{
    if (info.Kind is NumericKind.SignedInteger or NumericKind.UnsignedInteger)
        Console.WriteLine($"{name}: {info.SizeInBits} bits, signed={info.IsSigned}");
}

// Check what types can promote to each other
var int32 = PrimitiveCatalog.GetByName("int32");
var float32 = PrimitiveCatalog.GetByName("float32");
var promoted = PrimitiveCatalog.GetPromotedType(int32, float32);
Console.WriteLine($"int32 + float32 = {promoted?.SharpyName}");
```

---

## Contribution Guidelines

### When to Modify This File

#### ✅ **Do modify when**:
1. **Adding new primitive types** to the language spec
   - Add entries in `RegisterAll()`
   - Update `NumericKind` if new category needed
   - Add tests for promotion/conversion rules

2. **Changing promotion rules**
   - Update `GetPromotionPriority()` hierarchy
   - Update `GetPromotedType()` special cases
   - Add test cases for new edge cases

3. **Adding type aliases**
   - Register new alias in `RegisterAll()`
   - Document in comments (reference spec section)

4. **Fixing conversion bugs**
   - Update `CanImplicitlyConvert()` or `CanExplicitlyConvert()`
   - Add regression tests

#### ❌ **Don't modify for**:
1. **User-defined types** (classes, structs, enums)
   - Use `TypeMapper` or `BuiltinRegistry` instead
2. **Generic types** (List<T>, Dictionary<K,V>)
   - Handled by separate generic type system
3. **Type inference logic**
   - Belongs in semantic analyzer, not the catalog

### Testing Guidelines

When modifying, ensure you have tests for:

```csharp
// 1. Registration
[Fact] void CanLookupBySharpyName() { ... }
[Fact] void CanLookupByClrType() { ... }

// 2. Promotion
[Theory]
[InlineData("int32", "float", "float")]  // int + float → float
[InlineData("int32", "uint32", "long")]  // int + uint → long
[InlineData("int64", "uint64", null)]    // long + ulong → ERROR
void TestPromotion(string left, string right, string expected) { ... }

// 3. Conversion
[Theory]
[InlineData("int8", "int32", true, true)]   // implicit and explicit
[InlineData("int32", "int8", false, true)]  // explicit only
[InlineData("int32", "uint32", false, false)] // signed → unsigned not allowed
void TestConversion(string from, string to, bool implicit, bool explicit) { ... }
```

### Code Style Notes

- Use `record` for immutable data types
- Prefer `switch` expressions over `if-else` chains (see line 177 for example)
- Use `GetValueOrDefault()` instead of `TryGetValue()` for cleaner code (see line 107)
- Document spec references in comments (e.g., `// 1.2.1 Signed integer types`, line 61)
- Keep methods small and focused (single responsibility)

### Performance Considerations

- **Avoid allocations in hot paths**: Use singletons for common `SemanticType` instances (lines 252-271)
- **FrozenDictionary lookups are O(1)**: Don't cache results unnecessarily
- **Promotion priority calculation is cheap**: No need to memoize

---

## Summary

`PrimitiveCatalog` is the **single source of truth** for primitive types in Sharpy. It:
- Maps between Sharpy syntax, C# keywords, and CLR types
- Enforces numeric promotion rules for arithmetic operations
- Determines implicit/explicit conversion compatibility
- Uses frozen collections for optimal read performance
- Supports dual aliasing (Sharpy-style + C#-style names)

**Mental Model**: Think of it as a **reference book** that other compiler phases consult when they need to answer questions about primitive types. It's declarative (data-driven) rather than procedural, making it easy to extend and test.

**Key Characteristics**:
- **350 lines** of well-documented code
- **Zero runtime mutations** (immutable after startup)
- **Dual indexing** (by name and by CLR type)
- **C# semantics alignment** (matches .NET behavior exactly)

**Next Steps**:
- See **[TypeMapper.md](TypeMapper.md)** for how these primitives map to Roslyn syntax nodes
- See **[OperatorValidator.md](OperatorValidator.md)** for how promotion rules validate binary operators
- See **[BuiltinRegistry.md](BuiltinRegistry.md)** for built-in function signatures that use these primitives
