# Walkthrough: ITypeInfo.cs

**Source File**: `src/Sharpy.Compiler/Semantic/ITypeInfo.cs`

---

## Overview

`ITypeInfo` is a **common interface** that provides a unified abstraction for all type representations in the Sharpy compiler. It serves as the foundation for the type system, allowing different type implementations (built-in primitives, user-defined classes, generic instantiations, type parameters) to be handled polymorphically throughout the semantic analysis and code generation phases.

**Role in the Pipeline:**
- **Upstream**: Used by Parser (AST) to represent type annotations
- **Current**: Core abstraction in Semantic Analysis (type checking, resolution, inference)
- **Downstream**: Referenced by CodeGen (RoslynEmitter) for C# type mapping

**Key Design Philosophy**: This is explicitly called out as a "TWO-WAY DOOR" in the source comments, meaning it's a non-breaking abstraction layer that doesn't change existing behavior—it simply provides a common interface for future extensibility.

---

## Interface Definition

```csharp
public interface ITypeInfo
{
    string DisplayName { get; }
    bool IsNullable { get; }
    bool IsValueType { get; }
    Type? ClrType { get; }
    TypeSymbol? DeclaringSymbol { get; }
    
    bool IsAssignableTo(ITypeInfo other);
    ITypeInfo MakeNullable();
    ITypeInfo UnwrapNullable();
}
```

---

## Properties

### 1. `string DisplayName`

**Purpose**: Provides a human-readable representation of the type for diagnostics, error messages, and debugging output.

**Examples**:
- `"int"` - built-in primitive
- `"list[str]"` - generic type with type argument
- `"MyClass"` - user-defined class
- `"T"` - type parameter
- `"<?>"` - unknown type (error recovery)

**Usage Context**: 
- Compiler error messages: `"Cannot assign 'str' to 'int'"`
- Debug output when inspecting AST
- IDE tooltips and IntelliSense suggestions (future LSP support)

---

### 2. `bool IsNullable`

**Purpose**: Indicates whether this type can hold `None` (null) values. Critical for Sharpy's **Axiom 3: Static and Null-Safe Typing**.

**Behavior**:
- `true` for nullable types (e.g., `int?`, `str?`)
- `true` for reference types (classes)
- `false` for value types (primitives, structs) unless explicitly nullable

**Type Safety Implications**:
```python
# Sharpy code
x: int = None        # ❌ Compile error: int is not nullable
y: int? = None       # ✅ OK: int? is nullable
z: str? = "hello"    # ✅ OK: can assign non-null to nullable
```

**Related Specification**: See `docs/language_specification/nullable_types.md` and `type_narrowing.md`

---

### 3. `bool IsValueType`

**Purpose**: Distinguishes between value types (structs, primitives) and reference types (classes) for proper C# code generation.

**Why It Matters**:
- **Boxing/Unboxing**: Value types need boxing when assigned to `object`
- **Null handling**: Value types require `Nullable<T>` wrapper in C#
- **Memory layout**: Affects stack vs heap allocation in generated IL
- **Default values**: Value types have defined defaults (`0`, `false`), references default to `null`

**Examples**:
```csharp
// In C# codegen:
int x;           // IsValueType = true, stack allocated
MyClass y;       // IsValueType = false, heap allocated (reference)
int? z;          // IsValueType = true (Nullable<int> is a value type)
```

---

### 4. `Type? ClrType`

**Purpose**: Direct mapping to a .NET Common Language Runtime (CLR) type when available. Enables **Axiom 1: .NET Runtime Compatibility**.

**When Available**:
- ✅ Built-in primitives: `int` → `System.Int32`
- ✅ Built-in collections: `str` → `System.String`
- ✅ Compiled user-defined types (after code generation)
- ❌ Type parameters (`T`, `U`) - not yet resolved
- ❌ User-defined types during semantic analysis (before compilation)

**Usage**:
```csharp
// TypeMapper.cs uses ClrType for direct mappings:
if (typeInfo.ClrType != null)
{
    return SyntaxFactory.ParseTypeName(typeInfo.ClrType.FullName);
}
```

---

### 5. `TypeSymbol? DeclaringSymbol`

**Purpose**: Links type usage back to its declaration in the symbol table. Only applicable for user-defined types (classes, structs, interfaces).

**Null Cases**:
- Built-in primitives (`int`, `str`, `bool`) - no declaring symbol
- Type parameters (`T`) - no declaring symbol, just a placeholder
- Generic instantiations reference their generic definition's symbol

**Use Cases**:
- Looking up methods/properties on a class
- Resolving inheritance hierarchy
- Checking access modifiers (public/private)
- Generic constraint validation

**Example Flow**:
```python
# Sharpy code
class MyClass:
    def method(self) -> int:
        return 42

x: MyClass = MyClass()  # TypeChecker needs to look up MyClass symbol
result = x.method()     # Needs DeclaringSymbol to find 'method'
```

---

## Methods

### 1. `bool IsAssignableTo(ITypeInfo other)`

**Purpose**: Core type compatibility check - determines if a value of this type can be assigned to a variable of another type.

**Assignment Rules** (in order of precedence):
1. **Identity**: Any type is assignable to itself
2. **Object assignability**: All types are assignable to `object`
3. **Subtyping**: Derived classes assignable to base classes
4. **Interface implementation**: Classes assignable to interfaces they implement
5. **Implicit conversions**: Numeric widening (`int` → `long`, `float` → `double`)
6. **Nullable assignment**: Non-nullable `T` assignable to `T?`
7. **None assignment**: `None` assignable to any nullable type

**Examples**:
```python
# Valid assignments
x: object = 42           # int → object ✅
y: int? = 5              # int → int? ✅
z: long = 100            # int → long ✅ (widening)
w: MyInterface = MyClass()  # MyClass → MyInterface ✅ (if implements)

# Invalid assignments
a: int = 3.14            # float → int ❌ (narrowing, data loss)
b: str = 42              # int → str ❌ (unrelated types)
c: MyClass = None        # None → MyClass ❌ (not nullable)
```

**Implementation Notes**:
- Most concrete implementation is in `SemanticType.IsAssignableTo(SemanticType other)`
- Uses `PrimitiveCatalog` for implicit numeric conversions
- Respects inheritance hierarchy via `TypeSymbol` lookups

**Related Files**:
- `SemanticType.cs` - concrete implementation
- `PrimitiveCatalog.cs` - numeric conversion rules
- `TypeChecker.cs` - uses this extensively for assignment validation

---

### 2. `ITypeInfo MakeNullable()`

**Purpose**: Creates a nullable version of the type (adds `?` suffix conceptually, wraps in `NullableType` internally).

**Behavior**:
- **Idempotent**: Calling on already-nullable type returns itself
- **Immutable**: Creates a new type instance, doesn't modify existing

**Implementation**:
```csharp
public virtual ITypeInfo MakeNullable()
{
    if (this is NullableType)
        return this;  // Already nullable
    return new NullableType { UnderlyingType = this };
}
```

**Usage Scenarios**:
1. **Type inference with `None`**:
   ```python
   x = None  # Infer as object? (nullable object)
   ```

2. **Optional parameters**:
   ```python
   def func(x: int = None):  # Converts int to int?
   ```

3. **Union with None** (future):
   ```python
   x: int | None  # Conceptually int?
   ```

**Codegen Impact**: In generated C#, `int?` becomes `System.Nullable<int>`, while `str?` stays `string` (already nullable reference type).

---

### 3. `ITypeInfo UnwrapNullable()`

**Purpose**: Extracts the underlying type from a nullable type. If not nullable, returns the type itself.

**Behavior**:
- `int?` → `int`
- `str?` → `str`
- `int` → `int` (no-op, already non-nullable)

**Critical Use Case: Type Narrowing**

Type narrowing is a key feature where the compiler tracks that a nullable type cannot be null in certain code paths:

```python
def process(x: int?) -> None:
    if x is not None:
        # Inside this block, TypeChecker narrows x from int? to int
        # UnwrapNullable() is used to get the non-nullable type
        y: int = x  # ✅ Safe: x is known to be non-null here
```

**TypeChecker Integration**:
```csharp
// In TypeChecker, when checking 'if x is not None:' branch
if (IsNotNoneCheck(condition))
{
    var narrowedType = originalType.UnwrapNullable();
    _narrowedTypes[variableName] = narrowedType;
}
```

**Related Specification**: `docs/language_specification/type_narrowing.md`

---

## Type System Architecture

### Relationship to `SemanticType`

`ITypeInfo` is the **interface**, `SemanticType` is the **abstract base class** that implements it:

```csharp
public abstract record SemanticType : ITypeInfo
{
    // Singleton instances for common types
    public static readonly SemanticType Int = new BuiltinType { ... };
    public static readonly SemanticType Str = new BuiltinType { ... };
    // ... etc
}
```

**Why Both?**
- `ITypeInfo`: Minimal contract for type operations (interface segregation principle)
- `SemanticType`: Rich base with common functionality and type-specific subclasses

### Concrete Type Implementations

All these inherit from `SemanticType` (which implements `ITypeInfo`):

| Type Class | Purpose | Example |
|------------|---------|---------|
| `BuiltinType` | Primitives | `int`, `str`, `bool`, `float` |
| `UserDefinedType` | Classes/structs | `MyClass`, `MyStruct` |
| `GenericType` | Generic instantiations | `list[int]`, `dict[str, int]` |
| `TypeParameterType` | Generic parameters | `T`, `U` in `class List[T]` |
| `NullableType` | Optional types | `int?`, `str?` |
| `UnknownType` | Error recovery | `<?>` when type resolution fails |
| `VoidType` | No return value | `None` in return type |

**Immutability**: All `SemanticType` subclasses are `record` types - immutable once created. Type information is never mutated, only new instances created.

---

## Dependencies

### Direct Dependencies

1. **`SemanticType.cs`**: Primary implementation of the interface
   - Defines all concrete type classes
   - Provides singleton instances for built-ins
   - Implements default `IsAssignableTo` logic

2. **`TypeSymbol` (in Symbol.cs)**: Represents type declarations
   - Classes, structs, interfaces, enums
   - Generic type definitions
   - Linked via `DeclaringSymbol` property

3. **`Type` (System.Type)**: .NET CLR type reflection
   - Linked via `ClrType` property
   - Used for interop with .NET assemblies

### Downstream Consumers

1. **`TypeChecker.cs`**: Uses `ITypeInfo` extensively
   - Assignment compatibility checking
   - Type narrowing in conditionals
   - Method overload resolution

2. **`TypeResolver.cs`**: Converts AST type annotations to `ITypeInfo`
   - `list[int]` annotation → `GenericType` instance
   - `MyClass` annotation → `UserDefinedType` instance

3. **`TypeMapper.cs` (in CodeGen)**: Maps Sharpy types to C# types
   - Uses `ClrType` for direct mappings
   - Uses `DisplayName` for diagnostics

4. **`NameMangler.cs` (in CodeGen)**: Generates C# identifiers
   - Uses `IsValueType` for nullable handling

---

## Design Patterns & Principles

### 1. **Interface Segregation Principle**

`ITypeInfo` provides a minimal, focused contract. It doesn't expose internals, only what consumers need:
- Properties for queries (read-only)
- Methods for transformations (immutable)

### 2. **Two-Way Door Decision**

Explicitly documented in source: this interface is **additive only**. It doesn't break existing code, just provides an abstraction layer. Future changes can add new methods with default implementations without breaking implementers.

### 3. **Immutability**

All types are immutable after creation:
- `MakeNullable()` returns a **new** instance
- Properties are read-only
- Backed by `record` types in `SemanticType`

**Benefits**:
- Thread-safe (important for incremental compilation)
- Easier to reason about (no hidden state changes)
- Cacheable (same inputs always produce same type instance)

### 4. **Null Object Pattern** (for error recovery)

`UnknownType` represents a failed type resolution but allows compilation to continue:
```csharp
public override bool IsAssignableTo(SemanticType other) => true;  // Allow anything
```

This prevents cascading errors - one type error doesn't cause 50 follow-up errors.

### 5. **Visitor Pattern** (implicit)

While not explicitly using Visitor, the interface enables polymorphic dispatch:
```csharp
void ProcessType(ITypeInfo type)
{
    if (type.IsNullable) { /* handle nullable */ }
    if (type.IsValueType) { /* handle value type */ }
    // Behavior based on interface, not concrete class
}
```

---

## Debugging Tips

### 1. **Inspecting Types in Debugger**

Always look at `DisplayName` first - it's the human-readable representation:
```csharp
// In debugger watch window:
typeInfo.DisplayName  // "list[int]" or "MyClass" or "<?>"
```

### 2. **Assignment Failures**

When debugging "type X is not assignable to Y" errors:
```csharp
// Set breakpoint in IsAssignableTo()
bool result = sourceType.IsAssignableTo(targetType);

// Check these properties:
// 1. Are they the same type?
Console.WriteLine($"Same: {sourceType.Equals(targetType)}");

// 2. Is target more general?
Console.WriteLine($"Source: {sourceType.DisplayName}, Target: {targetType.DisplayName}");

// 3. Check nullability mismatch
Console.WriteLine($"Source nullable: {sourceType.IsNullable}, Target: {targetType.IsNullable}");
```

### 3. **Nullable Type Confusion**

If you see unexpected null-safety errors:
```csharp
// Check if type is wrapped in NullableType
var unwrapped = type.UnwrapNullable();
Console.WriteLine($"Original: {type.DisplayName}, Unwrapped: {unwrapped.DisplayName}");

// Check if wrapping worked
var madeNullable = type.MakeNullable();
Console.WriteLine($"Made nullable: {madeNullable.IsNullable}");  // Should be true
```

### 4. **Type Resolution Failures**

If you see `UnknownType` (`<?>`):
- Check `TypeResolver.cs` for resolution logic
- Look at `_errors` list in `TypeChecker` for underlying error
- Verify symbol is in `SymbolTable`

### 5. **CLR Type Mismatches**

When codegen produces wrong C# type:
```csharp
// Check if CLR type is set
if (type.ClrType == null)
{
    Console.WriteLine($"No CLR type for {type.DisplayName}");
    // Should use TypeMapper fallback logic
}
else
{
    Console.WriteLine($"CLR type: {type.ClrType.FullName}");
}
```

---

## Common Scenarios

### Scenario 1: Adding a New Type Kind

If you need to add a new type (e.g., `UnionType` for ADTs):

1. Create new record in `SemanticType.cs`:
   ```csharp
   public record UnionType : SemanticType
   {
       public List<SemanticType> Variants { get; init; } = new();
       public override string GetDisplayName() => 
           string.Join(" | ", Variants.Select(v => v.GetDisplayName()));
   }
   ```

2. Override relevant `ITypeInfo` properties:
   ```csharp
   public override bool IsAssignableTo(SemanticType other)
   {
       // A union is assignable if ANY variant is assignable
       return Variants.Any(v => v.IsAssignableTo(other));
   }
   ```

3. Update `TypeResolver.cs` to create instances
4. Update `TypeMapper.cs` for C# codegen
5. Add tests in `Sharpy.Compiler.Tests/Semantic/`

### Scenario 2: Extending Type Compatibility

To add new implicit conversion (e.g., `int` → `float`):

1. Update `PrimitiveCatalog.cs` with conversion rule
2. `IsAssignableTo` in `BuiltinType` already consults `PrimitiveCatalog`
3. No changes to `ITypeInfo` needed (extension point works!)

### Scenario 3: Type Narrowing in New Context

To add type narrowing for new patterns (e.g., `match` statement):

1. In `TypeChecker.cs`, track narrowed types:
   ```csharp
   private Dictionary<string, SemanticType> _narrowedTypes = new();
   ```

2. When entering narrowing context:
   ```csharp
   var narrowed = originalType.UnwrapNullable();
   _narrowedTypes[variableName] = narrowed;
   ```

3. When exiting context, restore original type

---

## Contribution Guidelines

### When to Modify This File

**DON'T modify** unless:
- Adding a fundamental type system capability needed by ALL types
- Fixing a bug in the interface contract itself

**Most changes belong elsewhere:**
- New type kinds → `SemanticType.cs`
- Type resolution → `TypeResolver.cs`
- Type checking rules → `TypeChecker.cs`
- Codegen → `TypeMapper.cs`

### Adding New Methods

If adding a new method to `ITypeInfo`:

1. **Consider default implementation** to avoid breaking existing code
2. **Document with examples** in XML comments
3. **Add to all implementations** in `SemanticType.cs`
4. **Update this walkthrough document**

Example:
```csharp
public interface ITypeInfo
{
    // New method with default implementation (C# 8.0+)
    bool IsNumeric() => this.ClrType?.IsValueType == true && 
                        (ClrType == typeof(int) || ClrType == typeof(float));
}
```

### Testing Changes

Any change to `ITypeInfo` requires tests in:
1. **Unit tests**: `Sharpy.Compiler.Tests/Semantic/SemanticTypeTests.cs`
2. **Integration tests**: `Sharpy.Compiler.Tests/Integration/TypeSystemTests.cs`
3. **File-based tests**: Add `.spy` + `.expected` pairs

Example test:
```csharp
[Fact]
public void ITypeInfo_MakeNullable_Idempotent()
{
    var intType = SemanticType.Int;
    var nullable1 = intType.MakeNullable();
    var nullable2 = nullable1.MakeNullable();
    
    Assert.True(nullable1.IsNullable);
    Assert.True(nullable2.IsNullable);
    Assert.Equal(nullable1, nullable2);  // Should be same instance
}
```

---

## Cross-References

### Related Implementation Files

- **[`SemanticType.md`](SemanticType.md)**: Primary implementation, all concrete type classes
- **[`TypeChecker.md`](TypeChecker.md)**: Main consumer, uses `IsAssignableTo` extensively
- **[`TypeResolver.md`](TypeResolver.md)**: Creates `ITypeInfo` instances from AST annotations
- **[`Symbol.md`](Symbol.md)**: Defines `TypeSymbol` referenced by `DeclaringSymbol`
- **[`PrimitiveCatalog.md`](PrimitiveCatalog.md)**: Numeric conversion rules for `IsAssignableTo`
- **[`../CodeGen/TypeMapper.md`](../CodeGen/TypeMapper.md)**: Maps to C# types for codegen

### Related Specification Documents

- **`docs/language_specification/type_annotations.md`**: How types are written in Sharpy
- **`docs/language_specification/type_hierarchy.md`**: Type system rules (`object` as universal base)
- **`docs/language_specification/type_narrowing.md`**: How `UnwrapNullable()` is used
- **`docs/language_specification/nullable_types.md`**: Semantics of `T?` types
- **`docs/language_specification/type_casting.md`**: Explicit vs implicit conversions

### Test Files

- **`src/Sharpy.Compiler.Tests/Semantic/SemanticTypeTests.cs`**: Unit tests for type system
- **`src/Sharpy.Compiler.Tests/Integration/TypeSystemTests.cs`**: End-to-end type checking tests

---

## Key Takeaways for Newcomers

1. **`ITypeInfo` is the foundation** of Sharpy's type system - understand this interface and you understand how types flow through the compiler

2. **Immutability is sacred** - types never change after creation, only new instances are made

3. **Assignment compatibility is complex** - `IsAssignableTo` is the heart of type checking, with many edge cases

4. **Nullable types are first-class** - `MakeNullable()` and `UnwrapNullable()` are critical for Sharpy's null-safety guarantees

5. **The interface is minimal by design** - it only exposes what's needed, keeping coupling low

6. **Look at `SemanticType.cs` next** - that's where the concrete implementations live

7. **Type narrowing is subtle** - understanding how `UnwrapNullable()` works with `TypeChecker._narrowedTypes` is key to advanced type system features

8. **Error recovery matters** - `UnknownType` allows compilation to continue after errors, preventing error avalanches

---

## Further Reading

- **Architecture**: `README.md` (root) - compiler pipeline overview
- **Semantic Analysis**: `.github/instructions/Sharpy.Compiler/HOW_TO_CONTRIBUTE.instructions.md`
- **Language Spec**: `docs/language_specification/README.md` - language semantics
- **Axioms**: `CLAUDE.md` - the three core principles guiding type system design
