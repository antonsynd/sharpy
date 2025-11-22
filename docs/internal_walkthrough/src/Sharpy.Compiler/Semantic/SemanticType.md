# Walkthrough: SemanticType.cs

**Source File**: `src/Sharpy.Compiler/Semantic/SemanticType.cs`

---

## 1. Overview

`SemanticType.cs` is the **type system foundation** for the Sharpy compiler's semantic analysis phase. It defines the type hierarchy that the compiler uses to represent, reason about, and validate types during compilation.

**Think of it as:** The "blueprint" for all types that exist in Sharpy programs - from simple primitives like `int` and `str` to complex generic types like `list[int]` and nullable types like `str?`.

**Core responsibilities:**
- Represent all possible types in the Sharpy type system
- Provide type comparison and assignability checking
- Support type-safe operations (e.g., "Can I assign an `int` to a `long`?")
- Generate human-readable type names for error messages

**Why this matters:** Every expression, variable, parameter, and return value in your Sharpy code gets a `SemanticType` during semantic analysis. This file determines whether your code type-checks correctly!

---

## 2. Class/Type Structure

The file uses a **discriminated union pattern** via C# records to represent different kinds of types:

```
SemanticType (abstract base)
├── UnknownType       - For error recovery (<?> in error messages)
├── VoidType          - Functions returning None
├── BuiltinType       - Primitives: int, long, float, double, bool, str
├── GenericType       - Parameterized types: list[int], dict[str, int]
├── UserDefinedType   - Classes, structs, interfaces defined by user
├── NullableType      - Optional types: int?, str?
├── FunctionType      - Lambda/function signatures: (int, str) -> bool
└── TupleType         - Tuple types: tuple[int, str, bool]
```

### Why Records?

Records provide:
- **Immutability** - Types don't change once created (safer, easier to reason about)
- **Value equality** - Two `int` types are equal by their content, not reference
- **Pattern matching** - Easy to check type kinds (e.g., `if (type is NullableType nullable)`)

---

## 3. Key Methods and Concepts

### 3.1 Singleton Instances (Lines 8-17)

```csharp
public static readonly SemanticType Int = new BuiltinType { Name = "int", ClrType = typeof(int) };
public static readonly SemanticType Str = new BuiltinType { Name = "str", ClrType = typeof(string) };
// ... etc
```

**Purpose:** Reuse the same instance for common types instead of creating new objects.

**Benefits:**
- **Performance** - No repeated allocations for `int`, `bool`, etc.
- **Reference equality** - Can use `==` to check if two types are both `Int`
- **Convenience** - Easy to reference: `SemanticType.Int` instead of `new BuiltinType { ... }`

**Example usage in compiler:**
```csharp
// When parsing "x: int = 5"
var varType = SemanticType.Int;  // Just grab the singleton
```

### 3.2 IsAssignableTo() - The Heart of Type Checking

This is **the most important method** in the file. It answers: "Can a value of this type be assigned to a variable of another type?"

#### Base Implementation (Lines 22-29)

```csharp
public virtual bool IsAssignableTo(SemanticType other)
{
    // All types are assignable to object
    if (other is UserDefinedType { Name: "object" })
        return true;

    return this.Equals(other);
}
```

**Default rules:**
1. **Everything is assignable to `object`** (just like in Python/C#)
2. **Types are assignable to themselves** (via `Equals`)

#### UnknownType Override (Line 44)

```csharp
public override bool IsAssignableTo(SemanticType other) => true;
```

**Why always true?** When the compiler encounters an error and can't determine a type, it uses `UnknownType`. Allowing it to be assignable to anything prevents **cascading errors** - one type error doesn't cause 100 more errors downstream.

**Example:**
```python
x = undefined_variable  # Error: undefined_variable not found
                        # x gets type UnknownType
y: int = x              # Don't report another error here!
```

#### VoidType Override (Lines 54-61)

```csharp
public override bool IsAssignableTo(SemanticType other)
{
    // None can be assigned to any nullable type
    if (other is NullableType)
        return true;

    return base.IsAssignableTo(other);
}
```

**Python semantics:** `None` (void) can be assigned to any optional type.

**Example:**
```python
x: int? = None  # ✓ Valid - None is assignable to int?
y: int = None   # ✗ Error - None not assignable to int
```

#### BuiltinType Override (Lines 74-86)

```csharp
public override bool IsAssignableTo(SemanticType other)
{
    if (base.IsAssignableTo(other)) return true;

    // Handle numeric conversions
    if (this == Int && other == Long) return true;    // int -> long
    if (this == Int && other == Float) return true;   // int -> float
    if (this == Int && other == Double) return true;  // int -> double
    if (this == Float && other == Double) return true; // float -> double
    if (this == Long && other == Double) return true;  // long -> double

    return false;
}
```

**Widening conversions:** Allows safe implicit conversions where no precision is lost.

**Examples:**
```python
x: int = 5
y: long = x      # ✓ int -> long (safe)
z: float = x     # ✓ int -> float (safe)

a: float = 3.14
b: int = a       # ✗ float -> int (would lose precision)
```

**Note:** `long -> float` is intentionally **not** allowed because floats can lose precision for large longs.

#### GenericType Override (Lines 104-121)

```csharp
public override bool IsAssignableTo(SemanticType other)
{
    if (other is GenericType otherGeneric
        && Name == otherGeneric.Name
        && TypeArguments.Count == otherGeneric.TypeArguments.Count)
    {
        // For now, check if type arguments match exactly
        for (int i = 0; i < TypeArguments.Count; i++)
        {
            if (!TypeArguments[i].Equals(otherGeneric.TypeArguments[i]))
                return false;
        }
        return true;
    }
    
    return base.IsAssignableTo(other);
}
```

**Current behavior:** Invariant type parameters (exact match required).

**Examples:**
```python
list1: list[int] = [1, 2, 3]
list2: list[int] = list1      # ✓ Same type

list3: list[long] = list1     # ✗ list[int] != list[long]
```

**Future enhancement** (see comment on line 110): Add covariance/contravariance support.

```python
# Future possibility with covariance:
animals: list[Animal] = list[Dog]()  # If Dog extends Animal
```

#### UserDefinedType Override (Lines 134-158)

```csharp
public override bool IsAssignableTo(SemanticType other)
{
    if (base.IsAssignableTo(other)) return true;

    if (other is UserDefinedType otherUdt && Symbol != null)
    {
        // Same type
        if (Symbol == otherUdt.Symbol || Name == otherUdt.Name)
            return true;

        // Check inheritance chain
        var current = Symbol.BaseType;
        while (current != null)
        {
            if (current == otherUdt.Symbol || current.Name == otherUdt.Name)
                return true;
            current = current.BaseType;
        }

        // Check interfaces
        return Symbol.Interfaces.Any(i => i == otherUdt.Symbol || i.Name == otherUdt.Name);
    }

    return false;
}
```

**Inheritance support:** Checks the full inheritance hierarchy.

**Examples:**
```python
class Animal:
    pass

class Dog(Animal):
    pass

class IRunnable:
    pass

class Cat(Animal, IRunnable):
    pass

dog: Dog = Dog()
animal: Animal = dog         # ✓ Dog is assignable to Animal (inheritance)

runnable: IRunnable = Cat()  # ✓ Cat is assignable to IRunnable (interface)
runnable2: IRunnable = dog   # ✗ Dog doesn't implement IRunnable
```

**How it works:**
1. First check if types are the same (by symbol or name)
2. Walk up the base class chain looking for a match
3. Check all implemented interfaces

#### NullableType Override (Lines 170-181)

```csharp
public override bool IsAssignableTo(SemanticType other)
{
    // Nullable T is assignable to T (implicit unwrapping)
    if (UnderlyingType.IsAssignableTo(other))
        return true;

    // Nullable T is assignable to Nullable T
    if (other is NullableType otherNullable)
        return UnderlyingType.IsAssignableTo(otherNullable.UnderlyingType);

    return base.IsAssignableTo(other);
}
```

**Special behavior:** Nullable types can be implicitly unwrapped.

**Examples:**
```python
x: int? = 5
y: int = x      # ✓ int? -> int (implicit unwrap, may need null check at runtime)

a: int? = 10
b: long? = a    # ✓ int? -> long? (underlying int -> long conversion)
```

**Safety note:** The compiler assumes you've done appropriate null checking. Runtime null reference exceptions are still possible!

### 3.3 GetDisplayName() - Human-Readable Type Names

Each type provides a display name for error messages and debugging.

**Examples of output:**
- `Int` → `"int"`
- `NullableType(Int)` → `"int?"`
- `GenericType("list", [Int])` → `"list[int]"`
- `FunctionType([Int, Str], Bool)` → `"(int, str) -> bool"`
- `TupleType([Int, Str, Bool])` → `"tuple[int, str, bool]"`
- `UnknownType` → `"<?>"`

**Usage in error messages:**
```csharp
throw new TypeError($"Cannot assign {sourceType.GetDisplayName()} to {targetType.GetDisplayName()}");
// Output: "Cannot assign str to int"
```

---

## 4. Dependencies

### Internal Dependencies

**TypeSymbol** (referenced in `GenericType` and `UserDefinedType`):
- Represents the symbol table entry for a type
- Contains inheritance information (`BaseType`, `Interfaces`)
- Links semantic types back to their definitions

**SymbolTable** (used by):
- `SemanticAnalyzer` - Maps variable names to types
- `TypeChecker` - Uses `IsAssignableTo()` for validation

### External Dependencies

**System Types:**
- `System.Type` (CLR types) - Stored in `BuiltinType.ClrType` for .NET interop
- Used when generating C# code (e.g., mapping `SemanticType.Int` to `System.Int32`)

---

## 5. Patterns and Design Decisions

### 5.1 Discriminated Union via Records

**Pattern:**
```csharp
public abstract record SemanticType { }
public record BuiltinType : SemanticType { }
public record GenericType : SemanticType { }
```

**Benefits:**
- **Type safety** - Can't accidentally mix up type kinds
- **Exhaustive pattern matching** - Compiler warns if you forget a case
- **Immutability** - Types never change, easier to reason about

**Usage example:**
```csharp
string GetTypeCategory(SemanticType type) => type switch
{
    UnknownType => "error recovery",
    VoidType => "void",
    BuiltinType => "primitive",
    GenericType => "generic",
    UserDefinedType => "user-defined",
    NullableType => "nullable",
    FunctionType => "function",
    TupleType => "tuple",
    _ => throw new NotImplementedException()
};
```

### 5.2 Flyweight Pattern (Singletons)

**Pattern:** Reuse instances for common types (`SemanticType.Int`, etc.)

**Why:** Performance + convenience. The compiler creates millions of type references - reusing instances saves memory and allocation time.

### 5.3 Virtual Method Pattern for Extensibility

**Pattern:** `virtual IsAssignableTo()` allows subclasses to override behavior.

**Why:** Different type kinds have different assignability rules. Virtual methods provide the right extensibility point.

### 5.4 Progressive Enhancement

Notice the comment on line 110: **"Check covariance/contravariance rules here in future"**

**Design principle:** Start simple, add complexity when needed.
- Currently: Invariant generics (simple, safe)
- Future: Covariant/contravariant generics (complex, powerful)

This is a **intentional simplification** - the compiler works without it, but could be enhanced later.

---

## 6. Debugging Tips

### 6.1 Type Comparison Issues

**Problem:** "Why does the compiler think these types are different?"

**Debug approach:**
```csharp
// Add temporary logging
Console.WriteLine($"Comparing {type1.GetDisplayName()} vs {type2.GetDisplayName()}");
Console.WriteLine($"Type1: {type1}");  // Full record dump
Console.WriteLine($"Type2: {type2}");
Console.WriteLine($"Equals: {type1.Equals(type2)}");
Console.WriteLine($"Assignable: {type1.IsAssignableTo(type2)}");
```

**Common gotchas:**
- Generic type arguments must match exactly (no covariance yet)
- Check if you're comparing by reference vs. by value
- Nullable wrappers: `int` != `int?`

### 6.2 Inheritance Chain Issues

**Problem:** "Why isn't my derived type assignable to the base?"

**Debug approach:**
```csharp
if (type is UserDefinedType udt && udt.Symbol != null)
{
    Console.WriteLine($"Type: {udt.Name}");
    Console.WriteLine($"Base: {udt.Symbol.BaseType?.Name ?? "none"}");
    Console.WriteLine($"Interfaces: {string.Join(", ", udt.Symbol.Interfaces.Select(i => i.Name))}");
}
```

**Common issues:**
- `Symbol` is null (type not fully resolved)
- Circular inheritance (infinite loop in base type chain)
- Interface not in `Symbol.Interfaces` list

### 6.3 Unknown Type Proliferation

**Problem:** Too many `<?>` in error messages.

**Why:** Error recovery is working correctly, but the **root cause** error is hard to find.

**Debug strategy:**
1. Look for the **first** error message (cascading errors flow from it)
2. Check where `UnknownType` is created (usually in error paths)
3. Fix the root cause, and cascading errors disappear

### 6.4 Using the Debugger

**Set breakpoints at:**
- `IsAssignableTo()` override for the type kind you're debugging
- `GetDisplayName()` when error messages look wrong
- Record constructors to see when types are created

**Watch expressions:**
```csharp
type.GetType().Name          // Which subclass?
type.GetDisplayName()        // Human-readable
((GenericType)type).TypeArguments[0]  // Inspect type args
```

---

## 7. Contribution Guidelines

### 7.1 Adding a New Type Kind

**Example:** Adding `UnionType` for `int | str`

**Steps:**
1. **Define the record:**
   ```csharp
   public record UnionType : SemanticType
   {
       public List<SemanticType> Types { get; init; } = new();
       
       public override string GetDisplayName() => 
           string.Join(" | ", Types.Select(t => t.GetDisplayName()));
   }
   ```

2. **Override `IsAssignableTo()`:**
   ```csharp
   public override bool IsAssignableTo(SemanticType other)
   {
       // A union is assignable if ALL members are assignable
       return Types.All(t => t.IsAssignableTo(other));
   }
   ```

3. **Add tests** in `Sharpy.Compiler.Tests/Semantic/TypeCheckerTests.cs`

4. **Update dependent code:**
   - `TypeChecker.cs` - Handle union types in expressions
   - `RoslynEmitter.cs` - Generate C# code for unions
   - Parser AST nodes if new syntax is needed

### 7.2 Extending Type Compatibility

**Example:** Adding covariance for `list[T]`

**Modify `GenericType.IsAssignableTo()`:**
```csharp
public override bool IsAssignableTo(SemanticType other)
{
    if (other is GenericType otherGeneric && Name == otherGeneric.Name)
    {
        // Check variance annotations on GenericDefinition
        if (GenericDefinition != null)
        {
            for (int i = 0; i < TypeArguments.Count; i++)
            {
                var variance = GenericDefinition.TypeParameters[i].Variance;
                
                if (variance == TypeVariance.Covariant)
                {
                    if (!TypeArguments[i].IsAssignableTo(otherGeneric.TypeArguments[i]))
                        return false;
                }
                else if (variance == TypeVariance.Contravariant)
                {
                    if (!otherGeneric.TypeArguments[i].IsAssignableTo(TypeArguments[i]))
                        return false;
                }
                else // Invariant
                {
                    if (!TypeArguments[i].Equals(otherGeneric.TypeArguments[i]))
                        return false;
                }
            }
            return true;
        }
    }
    
    return base.IsAssignableTo(other);
}
```

**Don't forget:**
- Update `TypeSymbol` to track variance
- Add variance syntax to parser (e.g., `class Box[+T]` for covariance)
- Comprehensive tests for variance edge cases

### 7.3 Improving Error Messages

**Current:**
```
Cannot assign int to str
```

**Better:**
```csharp
public class TypeError : Exception
{
    public SemanticType From { get; init; }
    public SemanticType To { get; init; }
    
    public override string Message => 
        $"Cannot assign {From.GetDisplayName()} to {To.GetDisplayName()}\n" +
        $"Hint: {GetHint()}";
    
    private string GetHint() => (From, To) switch
    {
        (BuiltinType { Name: "int" }, BuiltinType { Name: "str" }) => 
            "Did you mean to convert using str()?",
        (NullableType, _) when To is not NullableType => 
            "Use null checking or the '??' operator to unwrap nullable values",
        _ => ""
    };
}
```

### 7.4 Testing Checklist

When modifying this file, ensure:

- [ ] **Assignability tests** - Test `IsAssignableTo()` in both directions
- [ ] **Reflexivity** - Type assignable to itself
- [ ] **Transitivity** - If A→B and B→C, then A→C
- [ ] **Null safety** - Handle nullable types correctly
- [ ] **Error recovery** - UnknownType doesn't break type checking
- [ ] **Display names** - `GetDisplayName()` produces readable output
- [ ] **Inheritance** - Base/derived and interface checks work
- [ ] **Generics** - Type arguments checked correctly
- [ ] **Edge cases** - Empty collections, null symbols, etc.

### 7.5 Performance Considerations

**Current performance is good because:**
- Singletons reduce allocations
- Records provide efficient equality checks
- Virtual dispatch is fast for small hierarchies

**If adding features, watch out for:**
- Deep inheritance chains (cache assignability results?)
- Complex generic variance checks (memoization?)
- Creating many temporary type objects (use object pooling?)

**Profiling:**
```csharp
// Add to SemanticAnalyzer
private static int IsAssignableToCallCount = 0;

// In IsAssignableTo
IsAssignableToCallCount++;

// After compilation
Console.WriteLine($"IsAssignableTo called {IsAssignableToCallCount} times");
```

---

## Quick Reference Card

```csharp
// Creating types
SemanticType intType = SemanticType.Int;
SemanticType nullable = new NullableType { UnderlyingType = SemanticType.Int };
SemanticType list = new GenericType 
{ 
    Name = "list", 
    TypeArguments = new List<SemanticType> { SemanticType.Int } 
};

// Checking assignability
if (sourceType.IsAssignableTo(targetType))
{
    // Assignment is valid
}

// Pattern matching on type kind
string description = type switch
{
    UnknownType => "error type",
    VoidType => "void type",
    BuiltinType bt => $"builtin: {bt.Name}",
    GenericType gt => $"generic: {gt.GetDisplayName()}",
    UserDefinedType udt => $"user type: {udt.Name}",
    NullableType nt => $"nullable: {nt.GetDisplayName()}",
    FunctionType ft => $"function: {ft.GetDisplayName()}",
    TupleType tt => $"tuple: {tt.GetDisplayName()}",
    _ => "unknown"
};

// Display names for errors
throw new TypeError($"Cannot assign {from.GetDisplayName()} to {to.GetDisplayName()}");
```

---

## Further Reading

- **`TypeChecker.cs`** - Uses these types to validate expressions
- **`SymbolTable.cs`** - Stores SemanticTypes for variables/functions
- **`TypeSymbol.cs`** - Symbol table entries for type definitions
- **`RoslynEmitter.cs`** - Maps SemanticTypes to C# types
- **Language spec:** `docs/specs/type_system.md` (if exists)

---

**Questions?** Look at the test files in `Sharpy.Compiler.Tests/Semantic/` for concrete usage examples!
