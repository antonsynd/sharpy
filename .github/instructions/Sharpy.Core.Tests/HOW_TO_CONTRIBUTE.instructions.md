# Contributing to Sharpy.Core.Tests

## Overview

**Sharpy.Core.Tests** contains comprehensive tests for the Sharpy standard library, ensuring that all Pythonic APIs behave correctly and match Python semantics where applicable.

**Location:** `src/Sharpy.Core.Tests/`

**Test Coverage:** 735 tests, 100% pass rate ✅

## What's in This Directory

### Test Organization

```
Sharpy.Core.Tests/
├── Partial.ListTests/         # List tests
│   ├── ListTests.Core.cs      # Basic list operations
│   ├── ListTests.Mutation.cs  # append, insert, remove, etc.
│   ├── ListTests.Iteration.cs # Iteration and iteration protocols
│   ├── ListTests.Slicing.cs   # Slicing operations
│   └── ListTests.Equality.cs  # Equality and comparison
├── Partial.SetTests/          # Set tests
│   ├── SetTests.Core.cs       # Basic set operations
│   ├── SetTests.Operations.cs # union, intersection, difference
│   └── SetTests.Iteration.cs  # Set iteration
├── DictTests.cs               # Dictionary tests
├── DictViewsTests.cs          # keys(), values(), items()
├── StrTests.cs                # String wrapper tests
├── StrMethodsTests.cs         # String method tests
├── RangeTests.cs              # range() tests
├── EnumerateTests.cs          # enumerate() tests
├── ZipTests.cs                # zip() tests
├── FilterTests.cs             # filter() tests
├── MapTests.cs                # map() tests
├── SortedTests.cs             # sorted() tests
├── AllTests.cs                # all() tests
├── AnyTests.cs                # any() tests
├── MaxTests.cs                # max() tests
├── MinTests.cs                # min() tests
├── PowTests.cs                # pow() tests
├── RoundTests.cs              # round() tests
├── DivModTests.cs             # divmod() tests
├── IntConversionTests.cs      # int() tests
├── DoubleConversionTests.cs   # float() tests
├── FormatTests.cs             # String formatting
├── PrintTests.cs              # print() tests
├── IsinstanceTests.cs         # isinstance() tests
├── IssubclassTests.cs         # issubclass() tests
├── TypeTests.cs               # type() tests
├── ListConversionTests.cs     # list() conversion
├── SetConversionTests.cs      # set() conversion
├── TupleConversionTests.cs    # tuple() conversion
└── ModuleIntegrationTests.cs  # Integration tests
```

## How to Build

```bash
# From repository root
dotnet build src/Sharpy.Core.Tests/Sharpy.Core.Tests.csproj

# From Sharpy.Core.Tests directory
cd src/Sharpy.Core.Tests
dotnet build
```

## How to Run Tests

### Run All Tests
```bash
# From repository root
dotnet test src/Sharpy.Core.Tests

# Expected: 735 passed, 0 failed, 0 skipped
```

### Run Tests by Component

```bash
# List tests
dotnet test --filter "FullyQualifiedName~ListTests"

# Set tests
dotnet test --filter "FullyQualifiedName~SetTests"

# Dict tests
dotnet test --filter "FullyQualifiedName~DictTests"
dotnet test --filter "FullyQualifiedName~DictViewsTests"

# String tests
dotnet test --filter "FullyQualifiedName~StrTests"
dotnet test --filter "FullyQualifiedName~StrMethodsTests"

# Builtin function tests
dotnet test --filter "FullyQualifiedName~RangeTests"
dotnet test --filter "FullyQualifiedName~EnumerateTests"
dotnet test --filter "FullyQualifiedName~FilterTests"
dotnet test --filter "FullyQualifiedName~MapTests"
dotnet test --filter "FullyQualifiedName~SortedTests"

# Type conversion tests
dotnet test --filter "FullyQualifiedName~IntConversionTests"
dotnet test --filter "FullyQualifiedName~DoubleConversionTests"
dotnet test --filter "FullyQualifiedName~ListConversionTests"

# Type checking tests
dotnet test --filter "FullyQualifiedName~IsinstanceTests"
dotnet test --filter "FullyQualifiedName~TypeTests"
```

### Run Specific Tests

```bash
# Run a single test
dotnet test --filter "FullyQualifiedName~TestListAppend"
dotnet test --filter "FullyQualifiedName~TestDictGet"

# Run tests matching a pattern
dotnet test --filter "DisplayName~Append"
dotnet test --filter "DisplayName~Negative"
```

## Important Things to Note

### Testing Philosophy

**Match Python behavior exactly:**
- Run equivalent code in Python REPL to verify expected behavior
- Test edge cases that Python handles
- Document any intentional differences from Python

**Example workflow:**
```bash
# 1. Test in Python
$ python3
>>> lst = [1, 2, 3]
>>> lst.pop()
3
>>> lst
[1, 2]

# 2. Write equivalent Sharpy.Core test
[Fact]
public void TestListPop_RemovesLastElement()
{
    var lst = new List<int> { 1, 2, 3 };
    Assert.Equal(3, lst.pop());
    Assert.Equal(new[] { 1, 2 }, lst);
}
```

### Testing Best Practices

**CRITICAL: Rules for Writing Tests**

1. **NEVER artificially make tests pass:**
   ```csharp
   // ❌ WRONG: Changing test to match incorrect behavior
   [Fact]
   public void TestDictGet_DefaultValue()
   {
       var d = new Dict<string, int> { ["a"] = 1 };
       Assert.Equal(1, d.get("b", 0));  // BUG: Should return 0 (default)
   }
   
   // ✅ CORRECT: Fix the implementation in Sharpy.Core
   [Fact]
   public void TestDictGet_DefaultValue()
   {
       var d = new Dict<string, int> { ["a"] = 1 };
       Assert.Equal(0, d.get("b", 0));  // Correct Python behavior
   }
   ```

2. **Test against Python behavior:**
   ```python
   # Python REPL
   >>> d = {"a": 1}
   >>> d.get("b", 0)
   0
   ```
   
   Then write the C# test to match.

3. **Fix the root cause in Sharpy.Core:**
   - Debug the test failure
   - Find the bug in `src/Sharpy.Core/`
   - Fix the implementation
   - Verify the test passes
   - Check for regressions

4. **Mark as skipped ONLY if feature not implemented:**
   ```csharp
   [Fact(Skip = "TODO: Implement dict.fromkeys() class method - Python 3.x feature")]
   public void TestDictFromKeys()
   {
       var result = Dict<string, int>.fromkeys(new[] { "a", "b" });
       Assert.Equal(2, result.Count);
   }
   ```

### Testing Edge Cases

**Always test:**
- **Empty collections:** `[]`, `{}`, `set()`
- **Single element:** `[1]`, `{"a": 1}`
- **Multiple elements:** `[1, 2, 3]`
- **Negative indices:** `lst[-1]`, `lst[-2]`
- **Out of range:** `lst[100]` should throw `IndexError`
- **Null/None:** Where applicable in .NET context
- **Type mismatches:** (Caught at compile-time in Sharpy, but test conversions)

**Example:**
```csharp
[Fact]
public void TestListPop_EmptyList_ThrowsIndexError()
{
    var lst = new List<int>();
    Assert.Throws<IndexError>(() => lst.pop());
}

[Fact]
public void TestListPop_NegativeIndex()
{
    var lst = new List<int> { 1, 2, 3 };
    Assert.Equal(2, lst.pop(-2));  // Second to last
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
    lst.append(3);
    Assert.Equal(new[] { 1, 2, 3 }, lst);
}

[Fact]
public void TestListExtend()
{
    var lst = new List<int> { 1, 2 };
    lst.extend(new[] { 3, 4 });
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
    var r = range(5);
    Assert.Equal(new[] { 0, 1, 2, 3, 4 }, r);
}

[Fact]
public void TestEnumerate()
{
    var items = new List<string> { "a", "b", "c" };
    var enumerated = enumerate(items).ToList();
    
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
    Assert.Throws<ValueError>(() => lst.index(99));
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
       lst.clear();
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
       Assert.True(all(new[] { true, true, true }));
   }
   
   [Fact]
   public void TestAll_OneFalse_ReturnsFalse()
   {
       Assert.False(all(new[] { true, false, true }));
   }
   
   [Fact]
   public void TestAll_Empty_ReturnsTrue()
   {
       Assert.True(all(Array.Empty<bool>()));
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
   Assert.Equal(3, lst.pop());  // Should match Python
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
    lst.reverse();
    Assert.Empty(lst);
}

// Test with single element
[Fact]
public void TestListReverse_Single()
{
    var lst = new List<int> { 42 };
    lst.reverse();
    Assert.Equal(new[] { 42 }, lst);
}

// Test with multiple elements
[Fact]
public void TestListReverse_Multiple()
{
    var lst = new List<int> { 1, 2, 3, 4, 5 };
    lst.reverse();
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
    Assert.Equal(expected, max(values));
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

## Current Test Statistics

- **Total Tests:** 735
- **Passing:** 735 (100%)
- **Failed:** 0
- **Skipped:** 0

**Coverage by Component:**
- List operations: ~200 tests
- Dict operations: ~150 tests
- Set operations: ~100 tests
- Builtin functions: ~200 tests
- Type conversions: ~50 tests
- Miscellaneous: ~35 tests

## Getting Help

- Check Python documentation for expected behavior
- Run code in Python REPL to verify
- Look at existing similar tests for patterns
- Review implementation in `src/Sharpy.Core/` to understand behavior
