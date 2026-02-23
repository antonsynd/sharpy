---
applyTo: "src/Sharpy.Core.Tests/**"
---
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
├── CollectionsModuleTests.cs # collections module tests
├── DatetimeTests.cs       # datetime module tests
├── DictTests.cs           # Dictionary tests
├── DictViewsTests.cs      # Dict views tests
├── DivModTests.cs         # divmod() tests
├── DoubleConversionTests.cs # float conversion tests
├── EnumerateTests.cs      # Enumerate tests
├── FilterTests.cs         # filter() tests
├── FormatTests.cs         # format() tests
├── FrozenSetTests.cs      # frozenset tests
├── FrozenSetConversionTests.cs # frozenset conversion tests
├── IntConversionTests.cs  # int conversion tests
├── IsinstanceTests.cs     # isinstance() tests
├── IssubclassTests.cs     # issubclass() tests
├── IteratorInteropTests.cs # Iterator interop tests
├── ItertoolsTests.cs      # itertools module tests
├── ListConversionTests.cs # list conversion tests
├── MapTests.cs            # map() tests
├── MaxTests.cs            # max() tests
├── MinTests.cs            # min() tests
├── ModuleIntegrationTests.cs # Module integration tests
├── OperatorModuleTests.cs # operator module tests
├── OptionalTests.cs       # Optional type tests
├── PowTests.cs            # pow() tests
├── PrintTests.cs          # print() builtin tests
├── RandomTests.cs         # random module tests
├── RangeTests.cs          # Range tests
├── ResultTests.cs         # Result type tests
├── RoundTests.cs          # round() tests
├── SetConversionTests.cs  # set conversion tests
├── SortedTests.cs         # sorted() tests
├── StringExtensionsTests.cs # String extension tests
├── StrTests.cs            # String type tests
├── SysModuleTests.cs      # sys module tests
├── TupleConversionTests.cs # tuple conversion tests
├── TypeTests.cs           # type() tests
├── WrapperTests.cs        # Wrapper tests
└── ZipTests.cs            # zip() tests
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
