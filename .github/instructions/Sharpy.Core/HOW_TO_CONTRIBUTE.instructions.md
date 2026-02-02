# Sharpy.Core

Standard library with Pythonic APIs for .NET. Location: `src/Sharpy.Core/`

## Directory Structure

```
Sharpy.Core/
├── Partial.List/       # list[T] wrapping List<T> with Python methods
├── Partial.Set/        # set[T] wrapping HashSet<T>
├── Partial.Str/        # String methods (lower(), split(), etc.)
├── Dict.cs             # dict[K,V] wrapping Dictionary<K,V>
├── Range.cs            # range() builtin
├── Enumerate.cs        # enumerate() builtin
├── Filter.cs, Map.cs   # Collection operations
├── I*.cs               # Operator protocols (IAddable, IEquatable, etc.)
└── *.cs (root)         # Builtins via partial class Exports
```

## Design Principles

1. **Wrap .NET internally, expose Python API** — `list.append()` not `list.Add()`
2. **Match Python semantics** — Negative indices, slicing, same exceptions
3. **Prefer .NET when zero-cost abstraction impossible** — Axiom 1 wins

## Partial Class Pattern

Types split across `Partial.{Type}/` directories by interface:
```
Partial.List/
├── List.cs              # Main class + constructor
├── List.ISequence.cs    # ISequence implementation
├── List.IEnumerable.cs  # IEnumerable implementation
└── List.IBoolConvertible.cs
```

## Adding a Builtin Function

Add to `partial class Exports` (split across files):
```csharp
// NewFunction.cs
namespace Sharpy.Core;

public static partial class Exports
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

Interfaces in `I*.cs` define operator support:
- `IAddable<T>` — `+` operator
- `ISubtractable<T>` — `-` operator
- `IMultiplicable<T>` — `*` operator
- `IEquatable<T>` — `==` operator

Implement on types that should support the operator.
