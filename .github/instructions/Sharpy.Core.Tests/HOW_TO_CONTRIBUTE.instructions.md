# Sharpy.Core.Tests

Standard library tests. Location: `src/Sharpy.Core.Tests/`

## Test Organization

Tests for each class facet go in `Partial.*Tests/` directories:
```
Sharpy.Core.Tests/
├── Partial.ListTests/     # List tests (Core, Mutation, Slicing, etc.)
├── Partial.SetTests/      # Set tests
├── DictTests.cs           # Dictionary tests
└── ...
```

## Running Tests

```bash
dotnet test --filter "FullyQualifiedName~ListTests"
dotnet test --filter "FullyQualifiedName~DictTests"
dotnet test --filter "FullyQualifiedName~RangeTests"
```

## Testing Workflow

**Always verify against Python first:**
```bash
$ python3
>>> lst = [1, 2, 3]
>>> lst.pop()
3
```

Then write the test:
```csharp
[Fact]
public void TestListPop_RemovesLastElement()
{
    var lst = new List<int> { 1, 2, 3 };
    Assert.Equal(3, lst.Pop());
    Assert.Equal(new[] { 1, 2 }, lst);
}
```

## Test Edge Cases

Always test:
- Empty collections: `[]`
- Single element: `[1]`
- Negative indices: `lst[-1]`
- Out of range: `lst[100]` → `IndexError`

```csharp
[Fact]
public void TestListPop_EmptyList_ThrowsIndexError()
{
    var lst = new List<int>();
    Assert.Throws<IndexError>(() => lst.Pop());
}
```

## CRITICAL Rules

1. **Match Python semantics** - Run `python3 -c "..."` to verify
2. **Never change tests to match bugs** - Fix `src/Sharpy.Core/` instead
3. **Skip with reason if feature missing:**
   ```csharp
   [Fact(Skip = "TODO: Implement dict.fromkeys()")]
   ```
{
    var lst = new List<int> { 1, 2, 3 };
    Assert.Equal(2, lst.Pop(-2));  // Second to last
    Assert.Equal(new[] { 1, 3 }, lst);
}
```

### Common Testing Patterns

**Collection Tests:**
```csharp
[Fact]
public void TestListAppend()
{
    var lst = new List<int> { 1, 2 };
    lst.Append(3);
    Assert.Equal(new[] { 1, 2, 3 }, lst);
}

[Fact]
public void TestListExtend()
{
    var lst = new List<int> { 1, 2 };
    lst.Extend(new[] { 3, 4 });
    Assert.Equal(new[] { 1, 2, 3, 4 }, lst);
}
```

**Negative Indexing:**
```csharp
[Fact]
public void TestListNegativeIndex()
{
    var lst = new List<int> { 1, 2, 3 };
    Assert.Equal(3, lst[-1]);  // Last element
    Assert.Equal(2, lst[-2]);  // Second to last
    Assert.Equal(1, lst[-3]);  // Third to last
}
```

**Slicing:**
```csharp
[Fact]
public void TestListSlice()
{
    var lst = new List<int> { 0, 1, 2, 3, 4, 5 };
    Assert.Equal(new[] { 1, 2, 3 }, lst[1..4]);
    Assert.Equal(new[] { 0, 2, 4 }, lst[0..6..2]);  // Step
}
```

**Builtin Functions:**
```csharp
[Fact]
public void TestRange()
{
    var r = Range(5);
    Assert.Equal(new[] { 0, 1, 2, 3, 4 }, r);
}

[Fact]
public void TestEnumerate()
{
    var items = new List<string> { "a", "b", "c" };
    var enumerated = Enumerate(items).ToList();

    Assert.Equal(0, enumerated[0].Index);
    Assert.Equal("a", enumerated[0].Item);
    Assert.Equal(1, enumerated[1].Index);
    Assert.Equal("b", enumerated[1].Item);
}
```

**Exception Tests:**
```csharp
[Fact]
public void TestListIndex_NotFound_ThrowsValueError()
{
    var lst = new List<int> { 1, 2, 3 };
    Assert.Throws<ValueError>(() => lst.Index(99));
}

[Fact]
public void TestDictKeyError()
{
    var d = new Dict<string, int> { ["a"] = 1 };
    Assert.Throws<KeyError>(() => { var x = d["b"]; });
}
```

## Common Development Tasks

### Adding Tests for a New Collection Method

1. **Check Python behavior:**
   ```python
   >>> lst = [1, 2, 3]
   >>> lst.clear()
   >>> lst
   []
   ```

2. **Write the test:**
   ```csharp
   [Fact]
   public void TestListClear()
   {
       var lst = new List<int> { 1, 2, 3 };
       lst.Clear();
       Assert.Empty(lst);
   }
   ```

3. **Run the test** (should fail if not implemented)
4. **Implement in Sharpy.Core**
5. **Verify test passes**

### Adding Tests for a New Builtin Function

1. **Test in Python:**
   ```python
   >>> all([True, True, True])
   True
   >>> all([True, False, True])
   False
   >>> all([])
   True
   ```

2. **Write comprehensive tests:**
   ```csharp
   [Fact]
   public void TestAll_AllTrue_ReturnsTrue()
   {
       Assert.True(All(new[] { true, true, true }));
   }

   [Fact]
   public void TestAll_OneFalse_ReturnsFalse()
   {
       Assert.False(All(new[] { true, false, true }));
   }

   [Fact]
   public void TestAll_Empty_ReturnsTrue()
   {
       Assert.True(All(Array.Empty<bool>()));
   }
   ```

### Debugging Test Failures

1. **Run the specific test:**
   ```bash
   dotnet test --filter "FullyQualifiedName~TestListPop"
   ```

2. **Check Python behavior:**
   ```python
   >>> lst = [1, 2, 3]
   >>> lst.pop()
   3
   ```

3. **Compare with test:**
   ```csharp
   var lst = new List<int> { 1, 2, 3 };
   Assert.Equal(3, lst.Pop());  // Should match Python where possible and appropriate
   ```

4. **Debug the implementation** in `src/Sharpy.Core/`
5. **Fix and verify**

### Adding Edge Case Tests

```csharp
// Test with empty collection
[Fact]
public void TestListReverse_Empty()
{
    var lst = new List<int>();
    lst.Reverse();
    Assert.Empty(lst);
}

// Test with single element
[Fact]
public void TestListReverse_Single()
{
    var lst = new List<int> { 42 };
    lst.Reverse();
    Assert.Equal(new[] { 42 }, lst);
}

// Test with multiple elements
[Fact]
public void TestListReverse_Multiple()
{
    var lst = new List<int> { 1, 2, 3, 4, 5 };
    lst.Reverse();
    Assert.Equal(new[] { 5, 4, 3, 2, 1 }, lst);
}
```

## Test Data Guidelines

### Use Clear Test Data
```csharp
// Good - clear intent
var numbers = new List<int> { 1, 2, 3, 4, 5 };
var names = new List<string> { "Alice", "Bob", "Charlie" };

// Avoid - magic numbers/unclear
var lst = new List<int> { 42, 7, 99, 13 };
```

### Test Multiple Scenarios
```csharp
[Theory]
[InlineData(new[] { 1, 2, 3 }, 3)]
[InlineData(new[] { 5 }, 5)]
[InlineData(new int[] { }, 0)]
public void TestMax(int[] values, int expected)
{
    Assert.Equal(expected, Max(values));
}
```

## Assertion Best Practices

**Use specific assertions:**
```csharp
// Good
Assert.Equal(expected, actual);
Assert.Empty(collection);
Assert.Single(collection);
Assert.Throws<IndexError>(() => lst[100]);

// Avoid
Assert.True(actual == expected);
Assert.True(collection.Count == 0);
```

**Provide helpful messages:**
```csharp
Assert.Equal(expected, actual, $"Failed for input: {input}");
```

## Dependencies

- **Sharpy.Core** - Standard library under test
- **xUnit** - Testing framework
- **Microsoft.NET.Test.Sdk** - Test SDK
- **.NET 9.0/10.0** - Runtime

## Related Documentation

- **Core Library Guide:** `.github/instructions/Sharpy.Core/HOW_TO_CONTRIBUTE.instructions.md`
- **Builtins Reference:** `docs/specs/builtins.md`
- **Python Documentation:** https://docs.python.org/3/ (for reference behavior)

## Getting Help

- Check Python documentation for expected behavior
- Run code in Python REPL to verify
- Look at existing similar tests for patterns
- Review implementation in `src/Sharpy.Core/` to understand behavior
