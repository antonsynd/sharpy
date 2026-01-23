````instructions
# Sharpy.Core

Standard library with Pythonic APIs for .NET. Location: `src/Sharpy.Core/`

## Key Components

| Directory/File | Purpose |
|---------------|---------|
| `Partial.List/` | `list[T]` wrapping `List<T>` with Python methods |
| `Partial.Set/` | `set[T]` wrapping `HashSet<T>` |
| `Partial.Str/` | String methods (`lower()`, `split()`, etc.) |
| `Dict.cs` | `dict[K,V]` wrapping `Dictionary<K,V>` |
| `Range.cs`, `Enumerate.cs`, `Zip.cs` | Iteration builtins |
| `Filter.cs`, `Map.cs`, `Sorted.cs` | Collection operations |
| `I*.cs` | Operator protocols (`IAddable`, `IEquatable`, etc.) |

## Design Principles

1. **Wrap .NET internally, expose Python API** — `list.append()` not `list.Add()`
2. **Match Python semantics** — Negative indices, slicing, same exceptions
3. **Prefer .NET when zero-cost abstraction impossible**

## Adding a Builtin Function

1. **Add to `partial class Exports`** (split across files):
   ```csharp
   // NewFunction.cs
   namespace Sharpy.Core;

   public static partial class Exports
   {
       public static TResult NewFunction<T, TResult>(T input) => ...;
   }
   ```
2. **Verify expected behavior against Python:**
   ```bash
   python3 -c "print(new_function(...))"
   ```
3. **Add tests** in `Sharpy.Core.Tests/NewFunctionTests.cs`
4. **Test against Python** to verify behavior matches
5. **Document** in language reference if needed

## Python-style Indexing Pattern

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

## Partial Class Pattern

Types split across `Partial.{Type}/` directories for organization:
```
Partial.List/
├── List.cs              # Main class + constructor
├── List.ISequence.cs    # ISequence implementation
├── List.IEnumerable.cs  # IEnumerable implementation
└── List.IBoolConvertible.cs
```

## Testing

```bash
dotnet test --filter "FullyQualifiedName~ListTests"
dotnet test --filter "FullyQualifiedName~DictTests"
```

**CRITICAL:** Always verify behavior against `python3 -c "..."` first. Fix bugs, don't change test expectations.

## Adding a New Collection Method

1. **Add method to partial class** (e.g., in `Partial.List/`)
2. **Follow Python semantics** — check Python documentation
3. **Add comprehensive tests**
4. **Test edge cases** (empty, single-element, etc.)
5. **Update documentation** if it's a public API

## Implementing an Operator Protocol

1. **Define interface** (e.g., `INewOperator.cs`)
2. **Implement on relevant types**
3. **Add operator overload** if appropriate
4. **Test with multiple types**
5. **Document protocol** in specs

## Fixing a Bug

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

- **Minimize allocations** — Reuse collections where possible
- **Use struct for small value types** — `Index`, `Slice`
- **Lazy evaluation** — Use iterators instead of materializing lists
- **Leverage .NET BCL** — Don't reinvent optimized data structures

## Related Documentation

- **Main README:** `README.md` (repository root)
- **Core Tests Guide:** `.github/instructions/Sharpy.Core.Tests/HOW_TO_CONTRIBUTE.instructions.md`
- **Language Specification:** `docs/language_specification/`

````
