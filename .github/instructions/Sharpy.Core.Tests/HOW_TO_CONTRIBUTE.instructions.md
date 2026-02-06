# Sharpy.Core.Tests

Standard library tests. Location: `src/Sharpy.Core.Tests/`

## Directory Structure

Tests mirror `Sharpy.Core` layout:
```
Sharpy.Core.Tests/
├── Partial.ListTests/     # List tests split by facet
├── Partial.SetTests/      # Set tests
├── AllTests.cs            # all() builtin tests
├── AnyTests.cs            # any() builtin tests
├── DictTests.cs           # Dictionary tests
├── DictViewsTests.cs      # Dict views tests
├── EnumerateTests.cs      # Enumerate tests
├── FilterTests.cs         # filter() tests
├── FormatTests.cs         # format() tests
├── FrozenSetTests.cs      # frozenset tests
├── IsinstanceTests.cs     # isinstance() tests
├── IssubclassTests.cs     # issubclass() tests
├── MapTests.cs            # map() tests
├── MaxTests.cs            # max() tests
├── MinTests.cs            # min() tests
├── OptionalTests.cs       # Optional type tests
├── PowTests.cs            # pow() tests
├── PrintTests.cs          # print() builtin tests
├── RangeTests.cs          # Range tests
├── RoundTests.cs          # round() tests
├── SortedTests.cs         # sorted() tests
├── StrTests.cs            # String type tests
├── StrMethodsTests.cs     # String method tests
├── TypeTests.cs           # type() tests
├── ZipTests.cs            # zip() tests
└── *Tests.cs              # Other builtin/type tests
```

## Running Tests

```bash
dotnet test --filter "FullyQualifiedName~ListTests"
dotnet test --filter "FullyQualifiedName~DictTests"
dotnet test --filter "FullyQualifiedName~RangeTests"
dotnet test --filter "FullyQualifiedName~Core.Tests"
```

## Testing Workflow

**Always verify against Python first:**
```bash
python3 -c "lst = [1, 2, 3]; print(lst.pop())"  # 3
python3 -c "lst = [1, 2, 3]; print(lst[-1])"    # 3
python3 -c "print(list(range(5)))"              # [0, 1, 2, 3, 4]
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

## Required Edge Cases

Always test these for collection operations:
- **Empty collections:** `[]`
- **Single element:** `[1]`
- **Negative indices:** `lst[-1]`, `lst[-2]`
- **Out of range:** `lst[100]` → `IndexError`
- **Slicing boundaries:** `lst[0:0]`, `lst[100:200]`

```csharp
[Fact]
public void TestListPop_EmptyList_ThrowsIndexError()
{
    var lst = new List<int>();
    Assert.Throws<IndexError>(() => lst.Pop());
}

[Fact]
public void TestListIndex_NegativeIndex_ReturnsFromEnd()
{
    var lst = new List<int> { 1, 2, 3 };
    Assert.Equal(3, lst[-1]);
    Assert.Equal(2, lst[-2]);
}
```

## Critical Rules

1. **Match Python semantics** — Run `python3 -c "..."` first
2. **Never change tests to match bugs** — Fix `src/Sharpy.Core/`
3. **Skip with reason if feature missing:**
   ```csharp
   [Fact(Skip = "TODO: Implement dict.fromkeys()")]
   ```

## Test Categories

| Category | What to Test |
|----------|--------------|
| Core functionality | Basic operations work correctly |
| Edge cases | Empty, single, boundary conditions |
| Negative indexing | Python-style `[-1]`, `[-2]` |
| Errors | Correct exception types (`IndexError`, `KeyError`) |
