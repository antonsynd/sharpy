# Contributing to Sharpy.Core

## Overview

**Sharpy.Core** is the standard library for the Sharpy programming language. It provides Pythonic APIs for .NET collections and builtin functions, allowing Sharpy code to use familiar Python-like syntax while running on .NET.

**Location:** `src/Sharpy.Core/`

## What's in This Directory

### Core Collections

**List Implementation:**
- `Partial.List/` - Pythonic `list[T]` wrapper around .NET `List<T>`
- `Partial.ListIterator/` - Iterator support
- `Partial.ListReverseIterator/` - Reverse iteration
- Methods: `append()`, `extend()`, `insert()`, `remove()`, `pop()`, `clear()`, `index()`, `count()`, `sort()`, `reverse()`, `copy()`

**Dictionary Implementation:**
- `Dict.cs` - Pythonic `dict[K,V]` wrapper around .NET `Dictionary<K,V>`
- `DictKeyView.cs`, `DictValuesView.cs`, `DictItemsView.cs` - View objects
- Methods: `keys()`, `values()`, `items()`, `get()`, `pop()`, `popitem()`, `clear()`, `update()`, `setdefault()`

**Set Implementation:**
- `Partial.Set/` - Pythonic `set[T]` wrapper around .NET `HashSet<T>`
- `Partial.SetIterator/` - Iterator support
- Methods: `add()`, `remove()`, `discard()`, `pop()`, `clear()`, `union()`, `intersection()`, `difference()`, `symmetric_difference()`

**String Implementation:**
- `Partial.Str/` - Pythonic string wrapper
- Methods: `lower()`, `upper()`, `strip()`, `split()`, `join()`, `startswith()`, `endswith()`, `replace()`, `find()`, `count()`

### Builtin Functions

**Type Conversions:**
- `Int.cs` - `int()` conversion
- `Bool.cs` - `bool()` conversion
- `Double.cs` - `float()` conversion (maps to C# double)
- `Str.cs` - String operations
- `Bytes.cs` - Bytes handling

**Collection Functions:**
- `Len.cs` - `len()` - Get length of collections
- `Range.cs` - `range()` - Generate sequences
- `Enumerate.cs` - `enumerate()` - Index-value pairs
- `Zip.cs` - `zip()` - Combine iterables
- `Sorted.cs` - `sorted()` - Sort collections
- `Reversed.cs` - `reversed()` - Reverse iteration

**Filtering/Mapping:**
- `Filter.cs` - `filter()` - Filter elements
- `Map.cs` - `map()` - Transform elements
- `All.cs` - `all()` - Check if all elements are truthy
- `Any.cs` - `any()` - Check if any element is truthy

**Mathematical:**
- `Sum.cs` - `sum()` - Sum numeric values
- `Min.cs` - `min()` - Find minimum
- `Max.cs` - `max()` - Find maximum
- `Pow.cs` - `pow()` - Power operation
- `Round.cs` - `round()` - Round numbers
- `DivMod.cs` - `divmod()` - Division and modulo
- `Math/` - Math functions (abs, floor, ceil, etc.)

**Type Checking:**
- `Isinstance.cs` - `isinstance()` - Runtime type checking
- `Issubclass.cs` - `issubclass()` - Class hierarchy checking
- `Type.cs` - `type()` - Get type of object

**I/O:**
- `Print.cs` - `print()` - Console output
- `Input.cs` - `input()` - Console input (basic)

**Iteration:**
- `Iter.cs` - `iter()` - Get iterator
- `Next.cs` - `next()` - Get next item from iterator
- `StopIteration.cs` - StopIteration exception

**Formatting:**
- `Format.cs` - String formatting utilities
- `Repr.cs` - `repr()` - String representation

### Operator Protocols

Sharpy supports operator overloading through protocol interfaces:

**Arithmetic:**
- `IAddable.cs` - `__add__` (a + b)
- `ISubtractable.cs` - `__sub__` (a - b)
- `IMultipliable.cs` - `__mul__` (a * b)
- `IDivisible.cs` - `__truediv__` (a / b)
- `IFloorDivisible.cs` - `__floordiv__` (a // b)
- `IModulable.cs` - `__mod__` (a % b)
- `IPowerable.cs` - `__pow__` (a ** b)

**Comparison:**
- `IEquatable.cs` - `__eq__` (a == b)
- `IInequatable.cs` - `__ne__` (a != b)
- `ILessThanComparable.cs` - `__lt__` (a < b)
- `ILessThanOrEquatable.cs` - `__le__` (a <= b)
- `IGreaterThanComparable.cs` - `__gt__` (a > b)
- `IGreaterThanOrEquatable.cs` - `__ge__` (a >= b)

**Unary:**
- `INegatable.cs` - `__neg__` (-a)
- `IUnaryPlusable.cs` - `__pos__` (+a)
- `IInvertible.cs` - `__invert__` (~a)
- `IAbsoluteValue.cs` - `__abs__` (abs(a))

**Bitwise:**
- `IBitwiseAndable.cs` - `__and__` (a & b)
- `IBitwiseOrable.cs` - `__or__` (a | b)
- `IBitwiseXorable.cs` - `__xor__` (a ^ b)
- `ILeftShiftable.cs` - `__lshift__` (a << b)
- `IRightShiftable.cs` - `__rshift__` (a >> b)

**Other:**
- `IHashable.cs` - `__hash__` - Hashing support
- `IStrConvertible.cs` - `__str__` - String conversion
- `IRepresentable.cs` - `__repr__` - Representation
- `IBoolConvertible.cs` - `__bool__` - Boolean conversion

### Utilities

- `Index.cs`, `IndexExtensions.cs` - Python-style indexing (negative indices)
- `Slice.cs`, `Slice.Sized.cs` - Slicing support
- `EnumerableExtensions.cs`, `IterableLinqExtensions.cs` - LINQ-style extensions
- `KeyComparer.cs` - Custom comparison for sorting
- `IdentityAdapterFactory.cs` - Identity-based comparisons

### Error Types

- `IndexError.cs` - Index out of range
- `KeyError.cs` - Key not found in dictionary
- `StopIteration.cs` - Iterator exhausted
- `UnicodeEncodeError.cs` - Unicode encoding errors

## How to Build

```bash
# From repository root
dotnet build src/Sharpy.Core/Sharpy.Core.csproj

# From Sharpy.Core directory
cd src/Sharpy.Core
dotnet build
```

## How to Test

```bash
# Run all Sharpy.Core tests
dotnet test src/Sharpy.Core.Tests

# Run specific test files
dotnet test --filter "FullyQualifiedName~ListTests"
dotnet test --filter "FullyQualifiedName~DictTests"
dotnet test --filter "FullyQualifiedName~SetTests"
dotnet test --filter "FullyQualifiedName~StrTests"

# Run builtin function tests
dotnet test --filter "FullyQualifiedName~RangeTests"
dotnet test --filter "FullyQualifiedName~EnumerateTests"
dotnet test --filter "FullyQualifiedName~FilterTests"
```

## Important Things to Note

### Design Philosophy

**Sharpy.Core follows three principles:**

1. **Sharpy is a .NET language** - Use .NET types internally, expose Pythonic APIs
2. **Sharpy is Pythonic** - Match Python semantics and behavior where possible
3. **Pragmatic compatibility** - When conflicts arise, prefer .NET unless zero-cost abstractions are possible

**Examples:**
- `list[T]` wraps `List<T>` - .NET performance, Python API
- `dict[K,V]` wraps `Dictionary<K,V>` - .NET hash tables, Python API
- String methods return new strings (immutable like both Python and C#)

### Pythonic Behavior

**What we match from Python:**
- Method names (`append()`, `extend()`, `pop()`, `keys()`, etc.)
- Negative indexing (`list[-1]` is last element)
- Slicing (`list[1:3]`, `list[::2]`)
- Iterator protocol (`iter()`, `next()`, `StopIteration`)
- Exception types (`IndexError`, `KeyError`)
- Boolean semantics (empty collections are falsy)

**What differs from Python:**
- Static typing (everything has a known type at compile time)
- No dynamic attribute access
- .NET null semantics (nullable types)
- Performance characteristics match .NET collections

### Testing Best Practices

**CRITICAL: When writing tests for Sharpy.Core:**

1. **Test against Python behavior:**
   - Run equivalent code in Python
   - Match the behavior exactly
   - Document intentional differences

2. **NEVER artificially make tests pass:**
   ```csharp
   // ❌ WRONG: Changing test to match bug
   [Fact]
   public void TestListPop()
   {
       var list = new List<int> { 1, 2, 3 };
       Assert.Equal(1, list.pop());  // Bug: should be 3 (last element)
   }

   // ✅ CORRECT: Fix the implementation
   [Fact]
   public void TestListPop()
   {
       var list = new List<int> { 1, 2, 3 };
       Assert.Equal(3, list.pop());  // Correct Python behavior
   }
   ```

3. **Test edge cases:**
   - Empty collections
   - Single-element collections
   - Null values (where applicable)
   - Out-of-range indices
   - Type mismatches (compile-time in Sharpy)

4. **Mark skipped tests appropriately:**
   ```csharp
   [Fact(Skip = "TODO: Implement dict.fromkeys() - Sharpy 1.x")]
   public void TestDictFromKeys()
   {
       // Feature not yet implemented
   }
   ```

### Common Patterns

**Wrapping .NET Collections:**
```csharp
public partial class List<T> : IEnumerable<T>
{
    private readonly System.Collections.Generic.List<T> _inner;

    public List(IEnumerable<T> items)
    {
        _inner = new System.Collections.Generic.List<T>(items);
    }

    public void append(T item) => _inner.Add(item);
    public T pop() => pop(-1);  // Default to last element
    public int __len__() => _inner.Count;
}
```

**Implementing Operators:**
```csharp
public static List<T> operator +(List<T> left, List<T> right)
{
    var result = new List<T>(left);
    result.extend(right);
    return result;
}
```

**Python-style Indexing:**
```csharp
public T this[int index]
{
    get
    {
        var actualIndex = index < 0 ? _inner.Count + index : index;
        if (actualIndex < 0 || actualIndex >= _inner.Count)
            throw new IndexError($"list index out of range: {index}");
        return _inner[actualIndex];
    }
}
```

## Common Development Tasks

### Adding a New Builtin Function

1. **Create the implementation file** (e.g., `NewFunction.cs`)
2. **Implement the function:**
   ```csharp
   namespace Sharpy.Core;

   public static class NewFunction
   {
       public static TResult new_function<T, TResult>(T input)
       {
           // Implementation
       }
   }
   ```
3. **Add tests** in `Sharpy.Core.Tests/NewFunctionTests.cs`
4. **Test against Python** to verify behavior matches
5. **Document** in language reference if needed

### Adding a New Collection Method

1. **Add method to partial class** (e.g., in `Partial.List/`)
2. **Follow Python semantics** - check Python documentation
3. **Add comprehensive tests**
4. **Test edge cases** (empty, single-element, etc.)
5. **Update documentation** if it's a public API

### Implementing an Operator Protocol

1. **Define interface** (e.g., `INewOperator.cs`)
2. **Implement on relevant types**
3. **Add operator overload** if appropriate
4. **Test with multiple types**
5. **Document protocol** in specs

### Fixing a Bug

1. **Write a test that reproduces the bug:**
   ```csharp
   [Fact]
   public void TestBugReproduction()
   {
       // This test should fail initially
       var result = BuggyFunction();
       Assert.Equal(expected, result);
   }
   ```
2. **Debug the implementation**
3. **Fix the root cause**
4. **Verify test passes**
5. **Add regression tests** for edge cases
6. **Do NOT** change the test to match the bug!

## Performance Considerations

- **Minimize allocations** - Reuse collections where possible
- **Use struct for small value types** - `Index`, `Slice`
- **Lazy evaluation** - Use iterators instead of materializing lists
- **Leverage .NET BCL** - Don't reinvent optimized data structures

## Dependencies

- **.NET 9.0/10.0** - BCL and runtime
- **System.Collections.Generic** - Underlying collection types
- **System.Linq** - Query operations

## Related Documentation

- **Main README:** `README.md` (repository root)
- **Core Tests Guide:** `.github/instructions/Sharpy.Core.Tests/HOW_TO_CONTRIBUTE.instructions.md`
- **Type System Spec:** `docs/specs/type_system.md`
- **Builtins Reference:** `docs/specs/builtins.md`

## Example: Adding a New List Method

Let's add `list.insert_all(index, items)` to insert multiple items at once:

### 1. Implementation
```csharp
// In Partial.List/List.Mutation.cs
public void insert_all(int index, IEnumerable<T> items)
{
    var actualIndex = index < 0 ? _inner.Count + index : index;
    if (actualIndex < 0) actualIndex = 0;
    if (actualIndex > _inner.Count) actualIndex = _inner.Count;

    _inner.InsertRange(actualIndex, items);
}
```

### 2. Tests
```csharp
// In Sharpy.Core.Tests/Partial.ListTests/ListTests.Mutation.cs
[Fact]
public void TestInsertAll_InMiddle()
{
    var list = new List<int> { 1, 2, 5, 6 };
    list.insert_all(2, new[] { 3, 4 });
    Assert.Equal(new[] { 1, 2, 3, 4, 5, 6 }, list);
}

[Fact]
public void TestInsertAll_NegativeIndex()
{
    var list = new List<int> { 1, 4, 5 };
    list.insert_all(-2, new[] { 2, 3 });
    Assert.Equal(new[] { 1, 2, 3, 4, 5 }, list);
}

[Fact]
public void TestInsertAll_Empty()
{
    var list = new List<int> { 1, 2 };
    list.insert_all(1, Array.Empty<int>());
    Assert.Equal(new[] { 1, 2 }, list);
}
```

### 3. Verify
```bash
dotnet test --filter "FullyQualifiedName~TestInsertAll"
```

## Getting Help

- Check Python documentation for expected behavior
- Review existing implementations for patterns
- Run tests frequently to catch issues early
- Consult type system documentation for type-related questions
