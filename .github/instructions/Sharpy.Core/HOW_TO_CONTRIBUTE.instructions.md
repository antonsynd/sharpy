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

1. **Wrap .NET internally, expose Python API** - `list.append()` not `list.Add()`
2. **Match Python semantics** - Negative indices, slicing, same exceptions
3. **Prefer .NET when zero-cost abstraction impossible**

## Adding a Builtin Function

```csharp
// NewFunction.cs
namespace Sharpy.Core;

public static partial class Exports
{
    // C# implementation should have C#-style PascalCase name
    public static TResult NewFunction<T, TResult>(T input) => ...;
}
```

Test it:
```bash
# Automatic name mangling maps C# NewFunction to Sharpy's snake_case new_function
python3 -c "print(new_function(...))"  # Verify expected behavior
dotnet test --filter "FullyQualifiedName~NewFunctionTests"
```

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

## Testing

```bash
dotnet test --filter "FullyQualifiedName~ListTests"
dotnet test --filter "FullyQualifiedName~DictTests"
```

**CRITICAL:** Always verify behavior against `python3 -c "..."` first. Fix bugs, don't change test expectations.
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
public void InsertAll(int index, IEnumerable<T> items)
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
    list.InsertAll(2, new[] { 3, 4 });
    Assert.Equal(new[] { 1, 2, 3, 4, 5, 6 }, list);
}

[Fact]
public void TestInsertAll_NegativeIndex()
{
    var list = new List<int> { 1, 4, 5 };
    list.InsertAll(-2, new[] { 2, 3 });
    Assert.Equal(new[] { 1, 2, 3, 4, 5 }, list);
}

[Fact]
public void TestInsertAll_Empty()
{
    var list = new List<int> { 1, 2 };
    list.InsertAll(1, Array.Empty<int>());
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
