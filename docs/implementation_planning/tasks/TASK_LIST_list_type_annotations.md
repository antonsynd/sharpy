# Task List: Support `list[T]` Type Annotations and Multi-File Imports

**Assignee:** Junior Engineer / Claude Sonnet  
**Estimated Time:** 2-4 hours  
**Related Issues:** `skip_module_imports_multifile_0003`, `skip_module_imports_multifile_0006`

---

## Prerequisites

- [ ] Ensure you have the Sharpy repository cloned at `/Users/anton/Documents/github/sharpy`
- [ ] Run `dotnet build` to verify the project builds successfully
- [ ] Run `dotnet test src/Sharpy.Compiler.Tests` to verify all existing tests pass
- [ ] Create a new branch: `git checkout -b feature/list-type-annotations`

---

## Part 1: Update BuiltinRegistry to Use .NET Collection Types

### Task 1.1: Modify BuiltinRegistry.cs

**File:** `src/Sharpy.Compiler/Semantic/BuiltinRegistry.cs`

- [ ] Open the file and locate the `LoadBuiltins()` method (around line 40)
- [ ] Find the collection type registrations (lines 42-44):
  ```csharp
  RegisterType("list", typeof(Sharpy.Core.List<>), TypeKind.Class, isGeneric: true, typeParamCount: 1);
  RegisterType("dict", typeof(Sharpy.Core.Dict<,>), TypeKind.Class, isGeneric: true, typeParamCount: 2);
  RegisterType("set", typeof(Sharpy.Core.Set<>), TypeKind.Class, isGeneric: true, typeParamCount: 1);
  ```
- [ ] Replace with .NET collection types:
  ```csharp
  // Collections (generic) - v0.1.x uses .NET types directly per phases.md
  // Sharpy.Core wrapper types will be introduced in v0.2.x+
  RegisterType("list", typeof(System.Collections.Generic.List<>), TypeKind.Class, isGeneric: true, typeParamCount: 1);
  RegisterType("dict", typeof(System.Collections.Generic.Dictionary<,>), TypeKind.Class, isGeneric: true, typeParamCount: 2);
  RegisterType("set", typeof(System.Collections.Generic.HashSet<>), TypeKind.Class, isGeneric: true, typeParamCount: 1);
  ```
- [ ] Update the comment above these lines to explain the v0.1.x decision
- [ ] Save the file

### Task 1.2: Verify Build

- [ ] Run `dotnet build src/Sharpy.Compiler` - should succeed
- [ ] Run `dotnet test src/Sharpy.Compiler.Tests` - note any failures (some may be expected)

### Task 1.3: Commit

```bash
git add src/Sharpy.Compiler/Semantic/BuiltinRegistry.cs
git commit -m "feat: map list/dict/set to .NET collection types in BuiltinRegistry

Per phases.md, v0.1.x should use System.Collections.Generic types directly.
Sharpy.Core wrapper types will be introduced in v0.2.x+.

- list -> System.Collections.Generic.List<>
- dict -> System.Collections.Generic.Dictionary<,>
- set -> System.Collections.Generic.HashSet<>"
```

---

## Part 2: Update TypeMapper Code Generation

### Task 2.1: Update Static Type Mappings in TypeMapper.cs

**File:** `src/Sharpy.Compiler/CodeGen/TypeMapper.cs`

- [ ] Open the file and locate the static constructor (around line 15-30)
- [ ] Find the collection type string mappings:
  ```csharp
  _builtinTypeMap["list"] = "global::Sharpy.Core.List";
  _builtinTypeMap["dict"] = "global::Sharpy.Core.Dict";
  _builtinTypeMap["set"] = "global::Sharpy.Core.Set";
  ```
- [ ] Replace with .NET type strings:
  ```csharp
  // v0.1.x uses .NET types directly per phases.md (Sharpy.Core wrappers in v0.2.x+)
  _builtinTypeMap["list"] = "System.Collections.Generic.List";
  _builtinTypeMap["dict"] = "System.Collections.Generic.Dictionary";
  _builtinTypeMap["set"] = "System.Collections.Generic.HashSet";
  ```
- [ ] Save the file

### Task 2.2: Update CreateDictType Method

**File:** `src/Sharpy.Compiler/CodeGen/TypeMapper.cs`

- [ ] Locate the `CreateDictType` method (around line 200-210)
- [ ] Find the current implementation:
  ```csharp
  public TypeSyntax CreateDictType(TypeSyntax keyType, TypeSyntax valueType)
  {
      return GenericName("global::Sharpy.Core.Dict")
          .WithTypeArgumentList(
              TypeArgumentList(SeparatedList(new[] { keyType, valueType })));
  }
  ```
- [ ] Replace with:
  ```csharp
  public TypeSyntax CreateDictType(TypeSyntax keyType, TypeSyntax valueType)
  {
      return GenericName("System.Collections.Generic.Dictionary")
          .WithTypeArgumentList(
              TypeArgumentList(SeparatedList(new[] { keyType, valueType })));
  }
  ```
- [ ] Save the file

### Task 2.3: Verify Build and Tests

- [ ] Run `dotnet build src/Sharpy.Compiler` - should succeed
- [ ] Run `dotnet test src/Sharpy.Compiler.Tests` - note any failures

### Task 2.4: Commit

```bash
git add src/Sharpy.Compiler/CodeGen/TypeMapper.cs
git commit -m "feat: update TypeMapper to generate .NET collection types

Update code generation to emit System.Collections.Generic types:
- List<T> instead of Sharpy.Core.List<T>
- Dictionary<K,V> instead of Sharpy.Core.Dict<K,V>
- HashSet<T> instead of Sharpy.Core.Set<T>"
```

---

## Part 3: Add Integration Tests for Collection Type Annotations

### Task 3.1: Create List Type Annotation Test

**File:** `src/Sharpy.Compiler.Tests/IntegrationTests/Fixtures/collections/list_type_parameter.spy` (create directory if needed)

- [ ] Create the `collections` directory if it doesn't exist:
  ```bash
  mkdir -p src/Sharpy.Compiler.Tests/IntegrationTests/Fixtures/collections
  ```
- [ ] Create the test file with this content:
  ```python
  # Test: list[T] type annotation in function parameter and return type
  
  def sum_numbers(numbers: list[int]) -> int:
      total: int = 0
      for n in numbers:
          total += n
      return total
  
  def main():
      nums: list[int] = [1, 2, 3, 4, 5]
      result: int = sum_numbers(nums)
      print(result)
  
  # EXPECTED OUTPUT:
  # 15
  ```

### Task 3.2: Create Dict Type Annotation Test

**File:** `src/Sharpy.Compiler.Tests/IntegrationTests/Fixtures/collections/dict_type_parameter.spy`

- [ ] Create the test file with this content:
  ```python
  # Test: dict[K, V] type annotation
  
  def get_value(data: dict[str, int], key: str) -> int:
      if data.contains_key(key):
          return data[key]
      return 0
  
  def main():
      scores: dict[str, int] = {"alice": 100, "bob": 85}
      alice_score: int = get_value(scores, "alice")
      unknown_score: int = get_value(scores, "charlie")
      print(alice_score)
      print(unknown_score)
  
  # EXPECTED OUTPUT:
  # 100
  # 0
  ```

### Task 3.3: Create Set Type Annotation Test

**File:** `src/Sharpy.Compiler.Tests/IntegrationTests/Fixtures/collections/set_type_parameter.spy`

- [ ] Create the test file with this content:
  ```python
  # Test: set[T] type annotation
  
  def count_unique(items: set[int]) -> int:
      count: int = 0
      for item in items:
          count += 1
      return count
  
  def main():
      unique_nums: set[int] = {1, 2, 2, 3, 3, 3}
      count: int = count_unique(unique_nums)
      print(count)
  
  # EXPECTED OUTPUT:
  # 3
  ```

### Task 3.4: Run Integration Tests

- [ ] Run the integration tests:
  ```bash
  dotnet test src/Sharpy.Compiler.Tests --filter "FullyQualifiedName~IntegrationTests"
  ```
- [ ] If tests fail, debug by examining the generated C# code:
  ```bash
  dotnet run --project src/Sharpy.Cli -- emit-cs src/Sharpy.Compiler.Tests/IntegrationTests/Fixtures/collections/list_type_parameter.spy
  ```
- [ ] Verify the generated C# uses `System.Collections.Generic.List<int>` not `Sharpy.Core.List<int>`

### Task 3.5: Commit

```bash
git add src/Sharpy.Compiler.Tests/IntegrationTests/Fixtures/collections/
git commit -m "test: add integration tests for collection type annotations

Add test fixtures for:
- list[T] type annotations in function parameters
- dict[K, V] type annotations
- set[T] type annotations

These tests verify that collection types generate correct .NET types."
```

---

## Part 4: Update Dogfood Validator (Optional but Recommended)

### Task 4.1: Remove Collection Type Blocks from Orchestrator

**File:** `build_tools/sharpy_dogfood/orchestrator.py`

- [ ] Open the file and locate the `_quick_prevalidate` method (around line 1230)
- [ ] Find the `forbidden_checks` list
- [ ] Comment out or remove these patterns:
  ```python
  # REMOVE OR COMMENT THESE LINES:
  (r":\s*list\[", "list type annotation (v0.1.11)"),
  (r":\s*dict\[", "dict type annotation (v0.1.11)"),
  (r":\s*set\[", "set type annotation (v0.1.11)"),
  ```
- [ ] Update the comment to indicate these are now supported:
  ```python
  # Collections - NOW SUPPORTED in v0.1.11
  # (r":\s*list\[", "list type annotation"),  # Supported
  # (r":\s*dict\[", "dict type annotation"),  # Supported
  # (r":\s*set\[", "set type annotation"),    # Supported
  ```
- [ ] Save the file

### Task 4.2: Commit

```bash
git add build_tools/sharpy_dogfood/orchestrator.py
git commit -m "chore: enable list/dict/set type annotations in dogfood validator

Remove prevalidation blocks for collection type annotations now that
the compiler supports them."
```

---

## Part 5: Test the Dogfood Examples

### Task 5.1: Test the Multi-File Example (0006)

- [ ] Navigate to the dogfood skip directory:
  ```bash
  cd /Users/anton/Documents/github/sharpy/dogfood_output/skips/20260124_193258_skip_module_imports_multifile_0006
  ```
- [ ] Try compiling the multi-file project:
  ```bash
  dotnet run --project /Users/anton/Documents/github/sharpy/src/Sharpy.Cli -- run main.spy -m .
  ```
- [ ] If it fails, examine the error message and note it
- [ ] If it succeeds, verify the output matches `expected_output.txt`

### Task 5.2: Test the Multi-File Example (0003)

- [ ] Navigate to the dogfood skip directory:
  ```bash
  cd /Users/anton/Documents/github/sharpy/dogfood_output/skips/20260124_183629_skip_module_imports_multifile_0003
  ```
- [ ] Try compiling the multi-file project:
  ```bash
  dotnet run --project /Users/anton/Documents/github/sharpy/src/Sharpy.Cli -- run main.spy -m .
  ```
- [ ] Note whether it succeeds or fails (this one may have other issues beyond `list[T]`)

### Task 5.3: Document Results

- [ ] Create a test results file:
  ```bash
  # In the sharpy root directory
  echo "# Test Results for list[T] Implementation" > /tmp/test_results.md
  echo "" >> /tmp/test_results.md
  echo "## Test 0006 (list[Shape] type annotation):" >> /tmp/test_results.md
  echo "- Status: [PASS/FAIL]" >> /tmp/test_results.md
  echo "- Notes: [any notes]" >> /tmp/test_results.md
  echo "" >> /tmp/test_results.md
  echo "## Test 0003 (multi-file imports):" >> /tmp/test_results.md
  echo "- Status: [PASS/FAIL]" >> /tmp/test_results.md
  echo "- Notes: [any notes]" >> /tmp/test_results.md
  ```

---

## Part 6: Final Verification and PR

### Task 6.1: Run Full Test Suite

- [ ] Run all compiler tests:
  ```bash
  dotnet test src/Sharpy.Compiler.Tests
  ```
- [ ] Ensure no regressions (all previously passing tests still pass)
- [ ] Note the test count before and after changes

### Task 6.2: Run Full Build

- [ ] Build the entire solution:
  ```bash
  dotnet build sharpy.sln
  ```
- [ ] Ensure no build warnings related to the changes

### Task 6.3: Final Commit (if any uncommitted changes)

```bash
git status
# If there are any remaining changes:
git add -A
git commit -m "chore: final cleanup for list[T] support"
```

### Task 6.4: Push Branch

```bash
git push -u origin feature/list-type-annotations
```

### Task 6.5: Create PR Description

Create a PR with this description:
```markdown
## Summary
Adds support for `list[T]`, `dict[K, V]`, and `set[T]` type annotations per phases.md v0.1.11.

## Changes
- Updated `BuiltinRegistry.cs` to map collection types to .NET generics
- Updated `TypeMapper.cs` to generate correct .NET type names
- Added integration tests for collection type annotations
- Updated dogfood validator to allow collection type annotations

## Testing
- All existing tests pass
- New integration tests added for collections
- Manually tested dogfood examples 0003 and 0006

## Related Issues
- Fixes dogfood skip: `skip_module_imports_multifile_0006`
```

---

## Troubleshooting Guide

### If tests fail with "Type not found" errors:
- Verify `BuiltinRegistry.cs` changes are correct
- Check that the `typeof()` references are valid

### If generated C# still shows `Sharpy.Core.List`:
- Verify `TypeMapper.cs` static constructor changes
- Clear build cache: `dotnet clean && dotnet build`

### If multi-file tests fail with import errors:
- This may be a separate issue from `list[T]` support
- Document the error and create a follow-up task

### If dogfood tests still skip:
- Verify the regex patterns were removed from `orchestrator.py`
- Check if there are other blocking patterns in the code

---

## Checklist Summary

- [ ] Part 1: BuiltinRegistry changes (1 commit)
- [ ] Part 2: TypeMapper changes (1 commit)
- [ ] Part 3: Integration tests (1 commit)
- [ ] Part 4: Dogfood validator update (1 commit)
- [ ] Part 5: Manual testing of dogfood examples
- [ ] Part 6: Final verification and PR

**Total Expected Commits:** 4-5
