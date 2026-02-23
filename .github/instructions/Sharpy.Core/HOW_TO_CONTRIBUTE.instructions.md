---
applyTo: "src/Sharpy.Core/**"
---
# Sharpy.Core

Standard library with Pythonic APIs for .NET. Location: `src/Sharpy.Core/`

**Target:** `netstandard2.0;netstandard2.1` with C# 9.0 (LangVersion 9.0) — no file-scoped namespaces, global usings, or record structs.

## Directory Structure

```
Sharpy.Core/
├── Builtins/           # Builtin functions (Builtins.cs, Exceptions.cs)
├── Collections/        # Collection utilities
├── Datetime/           # datetime module
├── Itertools/          # itertools module
├── Math/               # math module
├── Operator/           # Operator protocols (IAdd, IMul, etc.)
├── Random/             # random module
├── Sys/                # sys module
├── Partial.Complex/    # complex type
├── Partial.Iterator/   # Iterator base
├── Partial.List/       # list[T] wrapping List<T> with Python methods
├── Partial.ListIterator/       # List iterator
├── Partial.ListReverseIterator/ # List reverse iterator
├── Partial.Set/        # set[T] wrapping HashSet<T>
├── Partial.SetIterator/ # Set iterator
├── Dict.cs             # dict[K,V] wrapping Dictionary<K,V>
├── Range.cs            # range() builtin
├── Enumerate.cs        # enumerate() builtin
├── Filter.cs, Map.cs   # Collection operations
├── IndexError.cs       # Python-style exceptions
├── KeyError.cs
└── *.cs (root)         # Builtins and utilities
```

## Design Principles

1. **Wrap .NET internally, expose Python API** — `list.append()` not `list.Add()`
2. **Match Python semantics** — Negative indices, slicing, same exceptions
3. **Prefer .NET when zero-cost abstraction impossible** — Axiom 1 wins
4. **Python exception names** — `IndexError`, `KeyError`, not `IndexOutOfRangeException`

## Partial Class Pattern

Types split across `Partial.{Type}/` directories by functionality:
```
Partial.List/
├── List.cs              # Main class + constructor
├── List.Methods.cs      # Python methods (append, pop, extend, etc.)
├── List.Slicing.cs      # Slicing operations
├── List.Interfaces.cs   # Interface implementations
└── List.operators.cs    # Operator overloads
```

## Adding a Builtin Function

Add to `partial class Builtins` (split across files):
```csharp
// NewFunction.cs
namespace Sharpy;

public static partial class Builtins
{
    public static TResult NewFunction<T, TResult>(T input) => ...;
}
```

## Python-style Indexing Pattern

Always support negative indices:
```csharp
public T this[int index]
{
    get
    {
        var actual = index < 0 ? _inner.Count + index : index;
        if (actual < 0 || actual >= _inner.Count)
            throw new IndexError($"list index out of range: {index}");
        return _inner[actual];
    }
}
```

## Workflow

1. **Verify Python behavior first:**
   ```bash
   python3 -c "print([1,2,3].pop())"     # Expected: 3
   python3 -c "print([1,2,3][-1])"       # Expected: 3
   python3 -c "print(list(range(5)))"    # Expected: [0, 1, 2, 3, 4]
   ```
2. **Implement matching behavior in C#**
3. **Add tests** in `Sharpy.Core.Tests/`
4. **Test edge cases:** empty, single-element, negative indices, out-of-range

## Testing

```bash
dotnet test --filter "FullyQualifiedName~ListTests"
dotnet test --filter "FullyQualifiedName~DictTests"
dotnet test --filter "FullyQualifiedName~Core.Tests"
```

**CRITICAL:** Verify behavior against `python3 -c "..."` first. Fix bugs, don't change test expectations.

## Python Method Names

Use Python naming, not .NET:
- `append()` not `Add()`
- `pop()` not `RemoveAt()`
- `extend()` not `AddRange()`
- `__len__` not `get_Count`

## Operator Protocols

Interfaces in `Operator/` define operator support:
- `IAdd` — `+` operator
- `IMul` — `*` operator

Implement on types that should support the operator.
