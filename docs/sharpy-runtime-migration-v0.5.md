# Sharpy.Runtime Migration Guide for v0.5

## Overview

This document provides a comprehensive migration guide for aligning the code in `Sharpy.Runtime` with the specifications defined in [language_reference.md](language_reference.md) and [type_system.md](type_system.md), specifically targeting **v0.5 features only**.

The migration focuses on:
1. **Built-in type mappings** - Mapping Python-like Sharpy types to .NET types
2. **Dunder method to .NET operator mappings** - Bidirectional mapping of Python magic methods to C# operators
3. **Extension methods for Pythonic API** - Providing Python-like method names while using .NET types underneath
4. **v0.5 feature scope** - Only implementing features marked as [v0.5] in the specifications

## Assumptions

1. **Native .NET types as foundation**: Built-in types like `str`, `int`, `list`, etc., map to native .NET types (`System.String`, `System.Int32`, `System.Collections.Generic.List<T>`, etc.)
2. **Extension methods for API**: Where the Pythonic API differs from .NET, we create extension methods to fill the gap
3. **Dunder methods as interfaces**: Dunder methods are implemented as interface methods that map to static operators
4. **Wrapper types where needed**: Some types (like `Str`, `List<T>`, `Dict<K,V>`) are lightweight wrappers around .NET types to provide the Pythonic API

## Type Mappings

### Numeric Types

| Sharpy Type | Python Equiv | .NET Type | Wrapper Needed | Notes |
|-------------|--------------|-----------|----------------|-------|
| `int` | `int` | `System.Int32` | No | Default integer type, 32-bit signed |
| `long` | `int` | `System.Int64` | No | 64-bit signed integer |
| `short` | `int` | `System.Int16` | No | 16-bit signed integer |
| `byte` | `int` | `System.Byte` | No | 8-bit unsigned integer |
| `sbyte` | `int` | `System.SByte` | No | 8-bit signed integer |
| `uint` | `int` | `System.UInt32` | No | 32-bit unsigned integer |
| `ulong` | `int` | `System.UInt64` | No | 64-bit unsigned integer |
| `ushort` | `int` | `System.UInt16` | No | 16-bit unsigned integer |
| `float` | `float` | `System.Single` | No | 32-bit floating point |
| `double` | `float` | `System.Double` | No | 64-bit floating point (default for float literals) |
| `decimal` | `float` | `System.Decimal` | No | 128-bit decimal for precise arithmetic |
| `bool` | `bool` | `System.Boolean` | No | Boolean value |
| `char` | `int` | `System.Char` | No | Single UTF-16 character (Python treats chars as integer unicode code points) |

**Migration Status**: Most numeric types can use .NET types directly. No wrapper needed.

**Action Items**:
- Ensure literal suffix handling (`42L` for long, `3.14f` for float, etc.)
- Implement dunder methods for numeric operations (see Dunder Methods section)

### String Type

| Sharpy Type | Python Equiv | .NET Type | Wrapper | Current Implementation |
|-------------|--------------|-----------|---------|------------------------|
| `str` | `str` | `System.String` | `Sharpy.Str` | `Partial.Str/Str.cs` |

**Wrapper Details**: `Sharpy.Str` is a `readonly partial struct` wrapping `System.String`.

**Migration Tasks**:

1. **Retain existing wrapper structure** - Already correctly implemented
2. **Implement required dunder methods**:
   - ✅ `__str__()` → implicit via wrapper
   - ✅ `__len__()` → Already implemented as `__Len__()`
   - ✅ `__getitem__(int)` → Already implemented
   - ✅ `__getitem__(Slice)` → Already implemented
   - ⚠️ `__eq__()`, `__ne__()`, `__lt__()`, `__le__()`, `__gt__()`, `__ge__()` → Need interface implementations
   - ⚠️ `__add__(str)` → String concatenation via operator
   - ⚠️ `__mul__(int)` → String replication via operator
   - ⚠️ `__contains__(str)` → Substring test
   - ⚠️ `__hash__()` → For use in dict/set
   - ✅ `__iter__()` → Return character iterator

3. **Pythonic methods** (as extension or instance methods):
   ```csharp
   public readonly partial struct Str
   {
       // Case conversion
       public Str Upper() => new Str(_s.ToUpper());
       public Str Lower() => new Str(_s.ToLower());
       public Str Capitalize();
       public Str Title();
       
       // Searching
       public int Find(Str sub, int start = 0, int? end = null);
       public int Index(Str sub, int start = 0, int? end = null); // Throws if not found
       public bool StartsWith(Str prefix, int start = 0, int? end = null);
       public bool EndsWith(Str suffix, int start = 0, int? end = null);
       
       // Splitting/Joining
       public List<Str> Split(Str? sep = null, int maxsplit = -1);
       public Str Join(IEnumerable<Str> items);
       
       // Trimming
       public Str Strip(Str? chars = null);
       public Str LStrip(Str? chars = null);
       public Str RStrip(Str? chars = null);
       
       // Replacement
       public Str Replace(Str old, Str @new, int count = -1);
       
       // Type checks
       public bool IsAlpha();
       public bool IsDigit();
       public bool IsAlnum();
       public bool IsSpace();
   }
   ```

4. **Operator mapping**:
   ```csharp
   // Already partially implemented, complete as needed:
   public static Str operator +(Str left, Str right);
   public static Str operator *(Str left, int right);
   public static Str operator *(int left, Str right);
   public static bool operator ==(Str left, Str right);
   public static bool operator !=(Str left, Str right);
   public static bool operator <(Str left, Str right);  // Lexicographical
   public static bool operator <=(Str left, Str right);
   public static bool operator >(Str left, Str right);
   public static bool operator >=(Str left, Str right);
   ```

### Collection Types

#### list[T]

| Sharpy Type | Python Equiv | .NET Type | Wrapper | Current Implementation |
|-------------|--------------|-----------|---------|------------------------|
| `list[T]` | `list[T]` | `List<T>` | `Sharpy.List<T>` | `Partial.List/List.cs` |

**Wrapper Details**: `Sharpy.List<T>` wraps `System.Collections.Generic.List<T>`.

**Migration Tasks**:

1. **Retain existing wrapper** - Already correctly structured
2. **Implement required dunder methods**:
   - ✅ `__len__()` → Already via `IMutableSequence<>`
   - ✅ `__getitem__(int)` → Already implemented
   - ✅ `__getitem__(Slice)` → Already implemented
   - ✅ `__setitem__(int, T)` → Already implemented
   - ⚠️ `__setitem__(Slice, IEnumerable<T>)` → Need to verify
   - ⚠️ `__delitem__(int)` → [v1.0] - Skip for v0.5
   - ⚠️ `__delitem__(Slice)` → [v1.0] - Skip for v0.5
   - ⚠️ `__contains__(T)` → Need to verify
   - ⚠️ `__iter__()` → Need to verify returns proper Iterator
   - ✅ `__add__(List<T>)` → List concatenation
   - ✅ `__mul__(int)` → List replication
   - ⚠️ `__eq__()`, `__ne__()`, `__lt__()`, etc. → Comparison operators

3. **Pythonic methods**:
   ```csharp
   public sealed partial class List<T>
   {
       // Mutating operations
       public void Append(T item);
       public void Extend(IEnumerable<T> items);
       public void Insert(int index, T item);
       public T Pop(int index = -1);
       public void Remove(T item);
       public void Clear();
       
       // Non-mutating
       public int Index(T item, int start = 0, int? stop = null);
       public int Count(T item);
       public List<T> Copy();
       
       // Sorting
       public void Sort(bool reverse = false);
       public void Sort<TKey>(Func<T, TKey> key, bool reverse = false);
       public void Reverse();
   }
   ```

4. **Operator mapping**:
   ```csharp
   public static List<T> operator +(List<T> left, List<T> right);
   public static List<T> operator *(List<T> left, int right);
   public static List<T> operator *(int left, List<T> right);
   public static bool operator ==(List<T> left, List<T> right);
   public static bool operator !=(List<T> left, List<T> right);
   ```

#### dict[K, V]

| Sharpy Type | Python Equiv | .NET Type | Wrapper | Current Implementation |
|-------------|--------------|-----------|---------|------------------------|
| `dict[K, V]` | `dict[K, V]` | `Dictionary<K, V>` | `Sharpy.Dict<K, V>` | `Dict.cs` |

**Wrapper Details**: `Sharpy.Dict<K, V>` wraps `System.Collections.Generic.Dictionary<K, V>`.

**Note**: Python 3.7+ dicts are insertion-ordered. As per [type_system.md](type_system.md) line 58, the wrapper wraps `OrderedDictionary<K,V>`. However, .NET's `Dictionary<K,V>` is already insertion-ordered as of .NET Core 3.0+, so verify your target .NET version and implementation details.

**Migration Tasks**:

1. **Retain existing wrapper**
2. **Implement required dunder methods**:
   - ⚠️ `__len__()` → Need to verify
   - ⚠️ `__getitem__(K)` → Access by key
   - ⚠️ `__setitem__(K, V)` → Set by key
   - ⚠️ `__delitem__(K)` → [v1.0] - Skip for v0.5
   - ⚠️ `__contains__(K)` → Key membership test
   - ⚠️ `__iter__()` → Iterate over keys
   - ⚠️ `__eq__()`, `__ne__()` → Comparison

3. **Pythonic methods**:
   ```csharp
   public sealed partial class Dict<K, V> where K : notnull
   {
       // Access operations
       public V Get(K key, V? default = default);
       public V Pop(K key);
       public V Pop(K key, V? default);
       public (K, V) PopItem(); // Removes and returns arbitrary item
       public V SetDefault(K key, V? default = default);
       
       // View operations
       public IKeysView<K> Keys();
       public IValuesView<V> Values();
       public IItemsView<K, V> Items();
       
       // Bulk operations
       public void Update(Dict<K, V> other);
       public void Update(IEnumerable<(K, V)> items);
       public void Clear();
       public Dict<K, V> Copy();
       
       // Factory methods
       public static Dict<K, V> FromKeys(IEnumerable<K> keys, V? value = default);
   }
   ```

4. **Operator mapping**:
   ```csharp
   // Indexer already provides __getitem__/__setitem__
   public V this[K key] { get; set; }
   
   // Equality
   public static bool operator ==(Dict<K, V> left, Dict<K, V> right);
   public static bool operator !=(Dict<K, V> left, Dict<K, V> right);
   ```

#### set[T]

| Sharpy Type | Python Equiv | .NET Type | Wrapper | Current Implementation |
|-------------|--------------|-----------|---------|------------------------|
| `set[T]` | `set[T]` | `HashSet<T>` | `Sharpy.Set<T>` | `Partial.Set/Set.cs` |

**Wrapper Details**: `Sharpy.Set<T>` wraps `System.Collections.Generic.HashSet<T>`.

**Migration Tasks**:

1. **Create/verify wrapper**
2. **Implement required dunder methods**:
   - ⚠️ `__len__()` → Set size
   - ⚠️ `__contains__(T)` → Membership test
   - ⚠️ `__iter__()` → Iterate over elements
   - ⚠️ `__and__(Set<T>)` → Intersection via `&`
   - ⚠️ `__or__(Set<T>)` → Union via `|`
   - ⚠️ `__sub__(Set<T>)` → Difference via `-`
   - ⚠️ `__xor__(Set<T>)` → Symmetric difference via `^`
   - ⚠️ `__eq__()`, `__ne__()` → Comparison
   - ⚠️ `__le__()`, `__lt__()`, `__ge__()`, `__gt__()` → Subset/superset

3. **Pythonic methods**:
   ```csharp
   public sealed partial class Set<T>
   {
       // Mutating operations
       public void Add(T item);
       public void Remove(T item); // Throws if not present
       public void Discard(T item); // No-op if not present
       public T Pop(); // Remove and return arbitrary element
       public void Clear();
       
       // Set operations (mutating)
       public void Update(Set<T> other);
       public void IntersectionUpdate(Set<T> other);
       public void DifferenceUpdate(Set<T> other);
       public void SymmetricDifferenceUpdate(Set<T> other);
       
       // Set operations (non-mutating)
       public Set<T> Union(Set<T> other);
       public Set<T> Intersection(Set<T> other);
       public Set<T> Difference(Set<T> other);
       public Set<T> SymmetricDifference(Set<T> other);
       
       // Tests
       public bool IsDisjoint(Set<T> other);
       public bool IsSubset(Set<T> other);
       public bool IsSuperset(Set<T> other);
       
       // Other
       public Set<T> Copy();
   }
   ```

4. **Operator mapping**:
   ```csharp
   public static Set<T> operator &(Set<T> left, Set<T> right); // Intersection
   public static Set<T> operator |(Set<T> left, Set<T> right); // Union
   public static Set<T> operator -(Set<T> left, Set<T> right); // Difference
   public static Set<T> operator ^(Set<T> left, Set<T> right); // Symmetric difference
   public static bool operator ==(Set<T> left, Set<T> right);
   public static bool operator !=(Set<T> left, Set<T> right);
   public static bool operator <=(Set<T> left, Set<T> right); // Subset
   public static bool operator <(Set<T> left, Set<T> right);  // Proper subset
   public static bool operator >=(Set<T> left, Set<T> right); // Superset
   public static bool operator >(Set<T> left, Set<T> right);  // Proper superset
   ```

#### tuple[T1, T2, ...]

| Sharpy Type | Python Equiv | .NET Type | Wrapper | Current Implementation |
|-------------|--------------|-----------|---------|------------------------|
| `tuple[T1, T2, ...]` | `tuple` | `ValueTuple<...>` | `Sharpy.Tuple<...>` | To be created |

**Wrapper Details**: `Sharpy.Tuple<T1, T2, ...>` wraps `System.ValueTuple<T1, T2, ...>`.

**Migration Tasks**:

1. **Create wrapper structs** for common tuple arities (2-8 elements):
   ```csharp
   public readonly struct Tuple<T1, T2> : ITuple, IEnumerable<object?>
   {
       private readonly (T1, T2) _value;
       
       public Tuple(T1 item1, T2 item2) => _value = (item1, item2);
       
       public T1 Item1 => _value.Item1;
       public T2 Item2 => _value.Item2;
       
       public int Length => 2;
       
       // Dynamic indexing (returns object?)
       public object? this[int index] => index switch
       {
           0 => Item1,
           1 => Item2,
           _ => throw new IndexError($"tuple index out of range: {index}") // IndexError exists in Sharpy.Runtime
       };
       
       // Implicit conversions
       public static implicit operator (T1, T2)(Tuple<T1, T2> tuple) => tuple._value;
       public static implicit operator Tuple<T1, T2>((T1, T2) tuple) => new(tuple.Item1, tuple.Item2);
       
       // IEnumerable for iteration
       public IEnumerator<object?> GetEnumerator()
       {
           yield return Item1;
           yield return Item2;
       }
   }
   ```

2. **Implement required dunder methods**:
   - ⚠️ `__len__()` → Tuple size
   - ⚠️ `__getitem__(int)` → Access by index (dynamic)
   - ⚠️ `__iter__()` → Iterate over elements
   - ⚠️ `__eq__()`, `__ne__()` → Comparison
   - ⚠️ `__lt__()`, `__le__()`, `__gt__()`, `__ge__()` → Lexicographical comparison
   - ⚠️ `__hash__()` → Hash for dict/set use
   - ⚠️ `__add__(tuple)` → Tuple concatenation
   - ⚠️ `__mul__(int)` → Tuple replication

3. **Pythonic methods**:
   ```csharp
   public readonly partial struct Tuple<T1, T2>
   {
       public int Count(object? value);
       public int Index(object? value, int start = 0, int? stop = null);
   }
   ```

4. **Operator mapping**:
   ```csharp
   // Note: These are challenging due to variable arity
   // May need to use a common ITuple interface
   public static bool operator ==(Tuple<T1, T2> left, Tuple<T1, T2> right);
   public static bool operator !=(Tuple<T1, T2> left, Tuple<T1, T2> right);
   ```

### Special Literals

| Sharpy | Python | .NET Implementation | Notes |
|--------|--------|---------------------|-------|
| `None` | `None` | `null` | Null reference |
| `True` | `True` | `true` | Boolean literal |
| `False` | `False` | `false` | Boolean literal |
| `...` | `...` | `Sharpy.Ellipsis` (singleton) | Placeholder in v0.5 |
| `{/}` | N/A | Empty set literal | Sharpy-specific |

**Migration Tasks**:
- ✅ `None`/`null` - Already handled by .NET
- ✅ `True`/`False` - Already handled by .NET
- ⚠️ `Ellipsis` - Create singleton class if not exists
- ⚠️ `{/}` empty set - Ensure parser/compiler handles

## Dunder Methods to .NET Operator Mappings

This section provides the **bidirectional mapping** between Python dunder methods and C# static operators, as required for v0.5 features.

### Arithmetic Operators

#### Binary Arithmetic

| Sharpy Operator | Dunder Method | C# Operator | Interface/Signature |
|-----------------|---------------|-------------|---------------------|
| `+` (addition) | `__add__(self, other)` | `operator +(T left, U right)` | `IAddable<T, U, TResult>` |
| `-` (subtraction) | `__sub__(self, other)` | `operator -(T left, U right)` | `ISubtractable<T, U, TResult>` |
| `*` (multiplication) | `__mul__(self, other)` | `operator *(T left, U right)` | `IMultipliable<T, U, TResult>` |
| `/` (true division) | `__truediv__(self, other)` | `operator /(T left, U right)` | `IDivisible<T, U, TResult>` |
| `//` (floor division) | `__floordiv__(self, other)` | Custom method | `IFloorDivisible<T, U, TResult>` |
| `%` (modulo) | `__mod__(self, other)` | `operator %(T left, U right)` | `IModulable<T, U, TResult>` |
| `**` (power) | `__pow__(self, other)` | Custom method | `IPowerable<T, U, TResult>` |

**Example Implementation**:
```csharp
public interface IAddable<TLeft, TRight, TResult>
    where TLeft : IAddable<TLeft, TRight, TResult>
{
    TResult __Add__(TRight other);
    
    static virtual TResult operator +(TLeft left, TRight right)
    {
        if (left is null || right is null)
            throw TypeError.OpNotSupported("+", "NoneType");
        // The generic constraint ensures left implements IAddable
        return left.__Add__(right);
    }
}

// For same-type operations
public interface IAddable<T> : IAddable<T, T, T>
    where T : IAddable<T>
{
}
```

#### Reflected (Reversed) Operators

When the left operand doesn't support the operation with the right operand, try the reflected version on the right operand.

| Sharpy Operator | Reflected Dunder | C# Implementation | Notes |
|-----------------|------------------|-------------------|-------|
| `+` | `__radd__(self, other)` | Implement on right type | Called when left lacks `__add__` |
| `-` | `__rsub__(self, other)` | Implement on right type | Called when left lacks `__sub__` |
| `*` | `__rmul__(self, other)` | Implement on right type | Already used in runtime |
| `/` | `__rtruediv__(self, other)` | Implement on right type | Called when left lacks `__truediv__` |
| `//` | `__rfloordiv__(self, other)` | Implement on right type | Called when left lacks `__floordiv__` |
| `%` | `__rmod__(self, other)` | Implement on right type | Called when left lacks `__mod__` |
| `**` | `__rpow__(self, other)` | Implement on right type | Called when left lacks `__pow__` |

**Example**:
```csharp
public interface IRightAddable<TLeft, TRight, TResult>
    where TRight : IRightAddable<TLeft, TRight, TResult>
{
    TResult __RAdd__(TLeft other);
    
    static virtual TResult operator +(TLeft left, TRight right)
    {
        if (left is null || right is null)
            throw TypeError.OpNotSupported("+", "NoneType");
        // The generic constraint ensures right implements IRightAddable
        return right.__RAdd__(left);
    }
}
```

#### In-place Operators

**Note**: In-place operators (`+=`, `-=`, `*=`, `/=`, `//=`, `%=`, `**=`) and their corresponding dunder methods (`__iadd__`, `__isub__`, `__imul__`, `__itruediv__`, `__ifloordiv__`, `__imod__`, `__ipow__`) are **not available in v0.5** because C# does not support overloading compound assignment operators. These operators cannot be defined as custom operator overloads in C#. The compiler will automatically translate compound assignments to their equivalent binary operations (e.g., `a += b` becomes `a = a + b`).

#### Unary Operators

| Sharpy Operator | Dunder Method | C# Operator | Interface |
|-----------------|---------------|-------------|-----------|
| `+x` (positive) | `__pos__(self)` | `operator +(T value)` | `IUnaryPlusable<T>` |
| `-x` (negative) | `__neg__(self)` | `operator -(T value)` | `INegatable<T>` |
| `~x` (bitwise NOT) | `__invert__(self)` | `operator ~(T value)` | `IInvertible<T>` |

**Example**:
```csharp
public interface INegatable<T>
    where T : INegatable<T>
{
    T __Neg__();
    
    static virtual T operator -(T value)
    {
        if (value is null)
            throw TypeError.OpNotSupported("-", "NoneType");
        // The generic constraint ensures value implements INegatable
        return value.__Neg__();
    }
}
```

### Comparison Operators

| Sharpy Operator | Dunder Method | C# Operator | Interface |
|-----------------|---------------|-------------|-----------|
| `==` (equality) | `__eq__(self, other)` | `operator ==(T left, T right)` | `IEquatable<T>` (exists) |
| `!=` (inequality) | `__ne__(self, other)` | `operator !=(T left, T right)` | `IInequatable<T>` (exists) |
| `<` (less than) | `__lt__(self, other)` | `operator <(T left, T right)` | `ILessThanComparable<T>` (exists) |
| `<=` (less than or equal) | `__le__(self, other)` | `operator <=(T left, T right)` | `ILessThanOrEquatable<T>` (exists) |
| `>` (greater than) | `__gt__(self, other)` | `operator >(T left, T right)` | `IGreaterThanComparable<T>` (exists) |
| `>=` (greater than or equal) | `__ge__(self, other)` | `operator >=(T left, T right)` | `IGreaterThanOrEquatable<T>` (exists) |

**Notes**:
- Many of these interfaces already exist in the codebase
- Comparison operators should return `bool`
- For types that don't support ordering (e.g., complex numbers), implement only `__eq__` and `__ne__`

### Bitwise Operators

| Sharpy Operator | Dunder Method | C# Operator | Interface |
|-----------------|---------------|-------------|-----------|
| `&` (bitwise AND) | `__and__(self, other)` | `operator &(T left, T right)` | `IBitwiseAndable<T>` |
| `\|` (bitwise OR) | `__or__(self, other)` | `operator \|(T left, T right)` | `IBitwiseOrable<T>` |
| `^` (bitwise XOR) | `__xor__(self, other)` | `operator ^(T left, T right)` | `IBitwiseXorable<T>` |
| `<<` (left shift) | `__lshift__(self, other)` | `operator <<(T left, int right)` | `ILeftShiftable<T>` |
| `>>` (right shift) | `__rshift__(self, other)` | `operator >>(T left, int right)` | `IRightShiftable<T>` |

**In-place variants**: In-place bitwise operators (`&=`, `|=`, `^=`, `<<=`, `>>=`) and their corresponding dunder methods (`__iand__`, `__ior__`, `__ixor__`, `__ilshift__`, `__irshift__`) are **not available in v0.5** because C# does not support overloading compound assignment operators.

### Collection/Sequence Operators

#### Indexing and Slicing

| Sharpy Syntax | Dunder Method | C# Implementation | Notes |
|---------------|---------------|-------------------|-------|
| `obj[index]` (get) | `__getitem__(self, index)` | `T this[int index] { get; }` | Single element access |
| `obj[index] = value` (set) | `__setitem__(self, index, value)` | `T this[int index] { set; }` | Single element assignment |
| `obj[start:stop:step]` (get) | `__getitem__(self, slice)` | `T this[Slice s] { get; }` | Slice access |
| `obj[start:stop:step] = values` (set) | `__setitem__(self, slice, values)` | `T this[Slice s] { set; }` | Slice assignment |
| `del obj[index]` | `__delitem__(self, index)` | Custom method | **[v1.0]** - Not in v0.5 |
| `del obj[slice]` | `__delitem__(self, slice)` | Custom method | **[v1.0]** - Not in v0.5 |

**Example**:
```csharp
public sealed partial class List<T>
{
    // Single index
    public T __GetItem__(int index) { /* ... */ }
    public void __SetItem__(int index, T value) { /* ... */ }
    
    // Indexer delegates to dunder methods
    public T this[int index]
    {
        get => __GetItem__(index);
        set => __SetItem__(index, value);
    }
    
    // Slice
    public List<T> __GetItem__(Slice slice) { /* ... */ }
    public void __SetItem__(Slice slice, IEnumerable<T> values) { /* ... */ }
    
    public List<T> this[Slice slice]
    {
        get => __GetItem__(slice);
        set => __SetItem__(slice, value);
    }
}
```

#### Membership and Iteration

| Sharpy Syntax | Dunder Method | C# Implementation | Interface |
|---------------|---------------|-------------------|-----------|
| `item in container` | `__contains__(self, item)` | Custom method | `IContainer<T>` |
| `len(obj)` | `__len__(self)` | Custom method | `ISized` |
| `iter(obj)` | `__iter__(self)` | `IEnumerator<T> GetEnumerator()` | `IIterable<T>` |
| `next(iterator)` | `__next__(self)` | `bool MoveNext()` + `Current` | `IIterator<T>` |

**Example**:
```csharp
public interface ISized
{
    uint __Len__();
}

public interface IContainer<T>
{
    bool __Contains__(T item);
}

public interface IIterable<T>
{
    Iterator<T> __Iter__();
}

public abstract class Iterator<T>
{
    public abstract T __Next__(); // Throws StopIteration when exhausted (StopIteration exists in Sharpy.Runtime)
}
```

### Other Important Dunders

#### String Representation

| Dunder Method | Purpose | C# Equivalent | Notes |
|---------------|---------|---------------|-------|
| `__str__(self)` | User-friendly string | `override string ToString()` | Called by `str()` |
| `__repr__(self)` | Developer string | Custom method | Called by `repr()` |
| `__hash__(self)` | Hash value | `override int GetHashCode()` | For dict/set |

**Example**:
```csharp
public interface IStrConvertible
{
    string __Str__();
}

public interface IRepresentable
{
    string __Repr__();
}

public interface IHashable
{
    int __Hash__();
}
```

#### Type Conversion

| Dunder Method | Purpose | C# Pattern | Notes |
|---------------|---------|------------|-------|
| `__int__(self)` | Convert to int | Explicit cast | `explicit operator int(T value)` |
| `__float__(self)` | Convert to float | Explicit cast | `explicit operator double(T value)` |
| `__bool__(self)` | Convert to bool | Explicit cast | `explicit operator bool(T value)` |

**Example**:
```csharp
public readonly partial struct Str
{
    public static explicit operator int(Str value) => int.Parse(value._s);
    public static explicit operator double(Str value) => double.Parse(value._s);
    public static explicit operator bool(Str value) => value._s.Length > 0;
}
```

#### Constructor

| Dunder Method | Purpose | C# Equivalent | Notes |
|---------------|---------|---------------|-------|
| `__init__(self, ...)` | Constructor | `public ClassName(...)` | Can have overloads |

**Example**:
```csharp
public class Person
{
    private string _name;
    private int _age;
    
    // __init__(self, name: str, age: int)
    public Person(string name, int age)
    {
        _name = name;
        _age = age;
    }
    
    // Overload: __init__(self, name: str)
    public Person(string name) : this(name, 0) { }
}
```

### Context Managers (v1.0+)

**NOT in v0.5** - Skip these for now:
- `__enter__(self)` → Part of `IDisposable` pattern
- `__exit__(self, exc_type, exc_val, exc_tb)` → Part of `IDisposable` pattern

## Interface Hierarchy

To support the dunder method mappings, we need a comprehensive set of interfaces:

```
IEquatable (base for all equality comparisons)
├── IEquatable<T>
│   ├── IInequatable<T>
│   └── IHashable
│
IComparable (for ordering)
├── ILessThanComparable<T>
├── ILessThanOrEquatable<T>
├── IGreaterThanComparable<T>
└── IGreaterThanOrEquatable<T>

IArithmetic (for numeric operations)
├── IAddable<TLeft, TRight, TResult>
│   ├── IAddable<T> (same-type shorthand)
│   └── IRightAddable<TLeft, TRight, TResult>
├── IInplaceAddable<T>
├── ISubtractable<TLeft, TRight, TResult>
├── IMultipliable<TLeft, TRight, TResult>
│   └── IRightMultipliable<TLeft, TRight, TResult>
├── IInplaceMultipliable<T>
├── IDivisible<TLeft, TRight, TResult>
├── IFloorDivisible<TLeft, TRight, TResult>
├── IModulable<TLeft, TRight, TResult>
└── IPowerable<TLeft, TRight, TResult>

IUnary (for unary operations)
├── INegatable<T>
├── IUnaryPlusable<T>
└── IInvertible<T>

IBitwise (for bitwise operations)
├── IBitwiseAndable<T>
├── IBitwiseOrable<T>
├── IBitwiseXorable<T>
├── ILeftShiftable<T>
└── IRightShiftable<T>

ICollection (for collections)
├── ISized (__len__)
├── IContainer<T> (__contains__)
├── IIterable<T> (__iter__)
└── IIterator<T> (__next__)
    
ISequence<T> : ISized, IContainer<T>, IIterable<T>
├── IMutableSequence<T> (supports __setitem__, __delitem__)
└── IMapping<K, V> (for dict-like)
    └── IMutableMapping<K, V>
```

**Migration Action**: Review existing interfaces in runtime and ensure they match this hierarchy. Fill in gaps.

## Migration Steps

### Phase 1: Interface Audit and Completion

1. **Inventory existing interfaces**:
   ```bash
   grep -r "^public interface I" src/Sharpy.Runtime --include="*.cs" > interfaces.txt
   ```

2. **Compare with required interfaces** from the Interface Hierarchy section above

3. **Create missing interfaces**:
   - `ISubtractable<T, U, TResult>` and variants
   - `IDivisible<T, U, TResult>` and variants
   - `IFloorDivisible<T, U, TResult>` and variants
   - `IModulable<T, U, TResult>` and variants
   - `IPowerable<T, U, TResult>` and variants
   - `INegatable<T>`, `IUnaryPlusable<T>`, `IInvertible<T>`
   - Bitwise interfaces if missing
   - `IContainer<T>`, `IIterable<T>`, etc. if missing

4. **Ensure operator overloads** in each interface following the pattern:
   ```csharp
   static virtual TResult operator OP(TLeft left, TRight right)
   {
       // Null checks
       // Call dunder method
   }
   ```

### Phase 2: Built-in Type Implementations

For each built-in type wrapper (`Str`, `List<T>`, `Dict<K,V>`, `Set<T>`, `Tuple<...>`):

1. **Implement required interfaces** based on the type's operators
2. **Implement dunder methods** (e.g., `__Add__`, `__Eq__`, etc.)
3. **Implement Pythonic methods** (e.g., `Upper()`, `Append()`, etc.)
4. **Add operator overloads** that delegate to dunder methods
5. **Add implicit/explicit conversions** where appropriate
6. **Write unit tests** for each dunder method and operator

**Order of implementation**:
1. `Str` - Most commonly used, already partially complete
2. `List<T>` - Already well-developed
3. `Dict<K,V>` - Core collection type
4. `Set<T>` - Set operations
5. `Tuple<...>` - Create from scratch

### Phase 3: Numeric Type Extensions

For numeric types (`int`, `long`, `double`, etc.):

1. **Create extension methods** for Pythonic APIs if needed
2. **Ensure dunder methods work** via interfaces
3. **Test arithmetic operators** with mixed types

### Phase 4: Integration Testing

1. **Create comprehensive integration tests** that exercise:
   - Mixed-type arithmetic (e.g., `int + double`)
   - Reflected operators (e.g., `5 * list`)
   - In-place operators
   - Collection operations
   - Comparison chains

2. **Test interop scenarios**:
   - Sharpy types with .NET types
   - Implicit conversions
   - Explicit casts

### Phase 5: Documentation

1. **Document each dunder method** in XML comments:
   ```csharp
   /// <summary>
   /// Implements the __add__ dunder method for string concatenation.
   /// Maps to the + operator in Sharpy code.
   /// </summary>
   /// <param name="other">The string to concatenate.</param>
   /// <returns>A new Str with the concatenated result.</returns>
   public Str __Add__(Str other) { /* ... */ }
   ```

2. **Create mapping reference** in docs showing Sharpy → C# translations

3. **Add examples** for each type showing both Sharpy and C# usage

## Testing Strategy

### Unit Tests

For each type, create tests for:
- Constructor variants
- Dunder methods individually
- Operators
- Pythonic methods
- Edge cases (empty, null, etc.)

**Example test structure**:
```csharp
public class StrTests
{
    [Fact]
    public void Add_ConcatenatesTwoStrings()
    {
        var s1 = new Str("Hello");
        var s2 = new Str(" World");
        var result = s1.__Add__(s2);
        Assert.Equal("Hello World", result);
    }
    
    [Fact]
    public void OperatorPlus_DelegatesToAdd()
    {
        var s1 = new Str("Hello");
        var s2 = new Str(" World");
        var result = s1 + s2;
        Assert.Equal("Hello World", result);
    }
    
    [Fact]
    public void Mul_ReplicatesString()
    {
        var s = new Str("Ha");
        var result = s.__Mul__(3);
        Assert.Equal("HaHaHa", result);
    }
    
    // ... more tests
}
```

### Integration Tests

Test combinations of types and operations:
```csharp
[Fact]
public void MixedOperations_WorkCorrectly()
{
    var list = new List<int> { 1, 2, 3 };
    var repeated = list * 2; // [1, 2, 3, 1, 2, 3]
    var combined = repeated + new List<int> { 4, 5 };
    Assert.Equal(8, combined.__Len__());
    Assert.True(combined.__Contains__(4));
}
```

### Compatibility Tests

Ensure Sharpy wrappers work seamlessly with .NET:
```csharp
[Fact]
public void StrConvertsToSystemString()
{
    var str = new Str("Hello");
    string sysStr = str; // Implicit conversion
    Assert.Equal("Hello", sysStr);
}

[Fact]
public void ListWorksWithLinq()
{
    var list = new List<int> { 1, 2, 3, 4, 5 };
    var evens = list.Where(x => x % 2 == 0).ToList();
    Assert.Equal(2, evens.Count);
}
```

## V0.5 Scope Limitations

**Features NOT in v0.5** (skip during migration):

1. **Context managers** (`with` statement, `__enter__`/`__exit__`)
2. **Del statement** (`del`, `__delitem__`)
3. **Properties** (explicit getter/setter, auto-properties)
4. **Match/case statements** (pattern matching)
5. **Comprehensions** (list/dict/set/generator comprehensions)
6. **Async/await**
7. **Generators** (`yield`, `__next__` for generators)
8. **Walrus operator** (`:=`)
9. **Type aliases** (`type` keyword)
10. **Advanced features**: decorators with args, `@property`, events, etc.

**Features IN v0.5** (must implement):

1. ✅ All arithmetic operators (`+`, `-`, `*`, `/`, `//`, `%`, `**`)
2. ✅ All comparison operators (`==`, `!=`, `<`, `<=`, `>`, `>=`)
3. ✅ All bitwise operators (`&`, `|`, `^`, `~`, `<<`, `>>`)
4. ✅ Membership operators (`in`, `not in`)
5. ✅ Indexing and slicing (`[]` get/set)
6. ✅ Identity operators (`is`, `is not`)
7. ✅ Logical operators (`and`, `or`, `not`)
8. ✅ Null-conditional (`?.`) and null-coalescing (`??`)
9. ✅ Basic control flow (`if`, `for`, `while`, `try/except/finally`)
10. ✅ Functions, classes, structs, interfaces, enums
11. ✅ Built-in functions (`len`, `str`, `int`, `print`, etc.)
12. ✅ Collections (list, dict, set, tuple)
13. ✅ Constructors (`__init__`)
14. ✅ String formatting (f-strings)
15. ✅ Lambdas
16. ✅ Exception handling

## Implementation Checklist

Use this checklist to track migration progress:

### Interfaces
- [ ] Audit existing interfaces
- [ ] Create missing arithmetic interfaces
- [ ] Create missing comparison interfaces
- [ ] Create missing bitwise interfaces
- [ ] Create missing collection interfaces
- [ ] Ensure all interfaces have static operators

### Str Type
- [ ] Complete dunder methods
- [ ] Add missing Pythonic methods
- [ ] Implement all operators
- [ ] Add comprehensive tests
- [ ] Update documentation

### List[T] Type
- [ ] Complete dunder methods
- [ ] Verify Pythonic methods
- [ ] Implement all operators
- [ ] Add comprehensive tests
- [ ] Update documentation

### Dict[K,V] Type
- [ ] Complete dunder methods
- [ ] Add missing Pythonic methods
- [ ] Implement all operators
- [ ] Add comprehensive tests
- [ ] Update documentation

### Set[T] Type
- [ ] Complete dunder methods
- [ ] Add missing Pythonic methods
- [ ] Implement all operators (incl. set ops)
- [ ] Add comprehensive tests
- [ ] Update documentation

### Tuple Types
- [ ] Create Tuple<T1, T2> through Tuple<T1..T8>
- [ ] Implement dunder methods
- [ ] Add Pythonic methods
- [ ] Implement operators
- [ ] Add comprehensive tests
- [ ] Update documentation

### Numeric Types
- [ ] Extension methods if needed
- [ ] Verify operators work
- [ ] Test mixed-type arithmetic
- [ ] Update documentation

### Built-in Functions
- [ ] Verify `len()` works with all collections
- [ ] Verify `str()` works with all types
- [ ] Verify `int()`, `float()`, etc. conversions
- [ ] Test `print()` with various types
- [ ] Test `range()`, `enumerate()`, `zip()`

### Integration
- [ ] Test Sharpy/C# interop
- [ ] Test operator precedence
- [ ] Test operator associativity
- [ ] Test error handling
- [ ] Performance benchmarks

### Documentation
- [ ] Update XML comments
- [ ] Create Sharpy→C# mapping guide
- [ ] Add usage examples
- [ ] Document limitations and v0.5 scope

## Reference: Complete Dunder Method List (v0.5)

For quick reference, here's the complete list of dunder methods needed for v0.5:

**Arithmetic**:
- `__add__`, `__radd__`
- `__sub__`, `__rsub__`
- `__mul__`, `__rmul__`
- `__truediv__`, `__rtruediv__`
- `__floordiv__`, `__rfloordiv__`
- `__mod__`, `__rmod__`
- `__pow__`, `__rpow__`

**Note**: In-place arithmetic operators (`__iadd__`, `__isub__`, `__imul__`, `__itruediv__`, `__ifloordiv__`, `__imod__`, `__ipow__`) are not available in v0.5 because C# does not support overloading compound assignment operators.

**Unary**:
- `__pos__`, `__neg__`, `__invert__`

**Comparison**:
- `__eq__`, `__ne__`, `__lt__`, `__le__`, `__gt__`, `__ge__`

**Bitwise**:
- `__and__`, `__rand__`
- `__or__`, `__ror__`
- `__xor__`, `__rxor__`
- `__lshift__`, `__rlshift__`
- `__rshift__`, `__rrshift__`

**Note**: In-place bitwise operators (`__iand__`, `__ior__`, `__ixor__`, `__ilshift__`, `__irshift__`) are not available in v0.5 for the same reason.

**Collection**:
- `__len__`, `__getitem__`, `__setitem__`, `__contains__`, `__iter__`, `__next__`

**Other**:
- `__str__`, `__repr__`, `__hash__`, `__bool__`, `__int__`, `__float__`, `__init__`

## Conclusion

This migration guide provides a comprehensive roadmap for aligning `Sharpy.Runtime` with the v0.5 language specification. The key principles are:

1. **Use native .NET types as the foundation** with lightweight wrappers where needed
2. **Map dunder methods to C# operators** bidirectionally via interfaces
3. **Provide Pythonic APIs** through instance and extension methods
4. **Stay within v0.5 scope** - defer v1.0+ features

By following this guide, the runtime will provide a seamless Pythonic experience while leveraging .NET's performance and ecosystem.
