# Task: Support `list[T]`, `dict[K,V]`, `set[T]` Type Annotations

**Assignee:** Junior Engineer / Claude Sonnet  
**Estimated Time:** 2-4 hours  
**Prerequisites:** Familiarity with C#, ability to run `dotnet test`

## Overview

Currently, collection type annotations like `list[int]` or `dict[str, int]` map to `Sharpy.Core` wrapper types. Per the language specification (phases.md), v0.1.x should use .NET types directly:
- `list[T]` → `System.Collections.Generic.List<T>`
- `dict[K,V]` → `System.Collections.Generic.Dictionary<K,V>`
- `set[T]` → `System.Collections.Generic.HashSet<T>`

## Pre-Implementation Setup

- [x] **1.1** Navigate to the project root:
  ```bash
  cd /Users/anton/Documents/github/sharpy
  ```

- [x] **1.2** Ensure tests pass before making changes:
  ```bash
  dotnet test src/Sharpy.Compiler.Tests
  ```
  
- [x] **1.3** Create a new branch:
  ```bash
  git checkout -b feature/dotnet-collection-types
  ```

---

## Part 1: Update BuiltinRegistry.cs

**File:** `src/Sharpy.Compiler/Semantic/BuiltinRegistry.cs`

- [x] **2.1** Open the file and locate the collection type registrations (around lines 42-47):
  ```csharp
  // Look for these lines:
  RegisterType("list", typeof(Sharpy.Core.List<>), TypeKind.Class, isGeneric: true, typeParamCount: 1);
  RegisterType("dict", typeof(Sharpy.Core.Dict<,>), TypeKind.Class, isGeneric: true, typeParamCount: 2);
  RegisterType("set", typeof(Sharpy.Core.Set<>), TypeKind.Class, isGeneric: true, typeParamCount: 1);
  ```

- [x] **2.2** Update the comment above these lines:
  ```csharp
  // Collections (generic) - v0.1.x uses .NET types directly per phases.md
  // Sharpy.Core wrapper types will be introduced in v0.2.x+
  ```

- [x] **2.3** Change `list` registration:
  ```csharp
  RegisterType("list", typeof(System.Collections.Generic.List<>), TypeKind.Class, isGeneric: true, typeParamCount: 1);
  ```

- [x] **2.4** Change `dict` registration:
  ```csharp
  RegisterType("dict", typeof(System.Collections.Generic.Dictionary<,>), TypeKind.Class, isGeneric: true, typeParamCount: 2);
  ```

- [x] **2.5** Change `set` registration:
  ```csharp
  RegisterType("set", typeof(System.Collections.Generic.HashSet<>), TypeKind.Class, isGeneric: true, typeParamCount: 1);
  ```

- [x] **2.6** Verify the file compiles:
  ```bash
  dotnet build src/Sharpy.Compiler
  ```

- [x] **2.7** Run tests to check for regressions:
  ```bash
  dotnet test src/Sharpy.Compiler.Tests
  ```

- [x] **2.8** Commit this change:
  ```bash
  git add src/Sharpy.Compiler/Semantic/BuiltinRegistry.cs
  git commit -m "feat: map list/dict/set to .NET collection types in BuiltinRegistry

  Per phases.md, v0.1.x uses System.Collections.Generic types directly.
  Sharpy.Core wrappers will be introduced in v0.2.x+.

  - list -> System.Collections.Generic.List<>
  - dict -> System.Collections.Generic.Dictionary<,>
  - set -> System.Collections.Generic.HashSet<>"
  ```

---

## Part 2: Update TypeMapper.cs - Type Name Mappings

**File:** `src/Sharpy.Compiler/CodeGen/TypeMapper.cs`

- [x] **3.1** Open the file and locate the static constructor (around lines 15-30). Find these lines:
  ```csharp
  // Add non-primitive type mappings (collections, etc.)
  // These are Sharpy runtime types (use global:: to avoid conflicts when output namespace contains "Sharpy")
  _builtinTypeMap["list"] = "global::Sharpy.Core.List";
  _builtinTypeMap["dict"] = "global::Sharpy.Core.Dict";
  _builtinTypeMap["set"] = "global::Sharpy.Core.Set";
  ```

- [x] **3.2** Update the comment:
  ```csharp
  // Add non-primitive type mappings (collections, etc.)
  // v0.1.x uses .NET types directly per phases.md (Sharpy.Core wrappers in v0.2.x+)
  ```

- [x] **3.3** Change `list` mapping:
  ```csharp
  _builtinTypeMap["list"] = "System.Collections.Generic.List";
  ```

- [x] **3.4** Change `dict` mapping:
  ```csharp
  _builtinTypeMap["dict"] = "System.Collections.Generic.Dictionary";
  ```

- [x] **3.5** Change `set` mapping:
  ```csharp
  _builtinTypeMap["set"] = "System.Collections.Generic.HashSet";
  ```

- [x] **3.6** Verify the file compiles:
  ```bash
  dotnet build src/Sharpy.Compiler
  ```

- [x] **3.7** Run tests:
  ```bash
  dotnet test src/Sharpy.Compiler.Tests
  ```

- [x] **3.8** Commit this change:
  ```bash
  git add src/Sharpy.Compiler/CodeGen/TypeMapper.cs
  git commit -m "feat: update TypeMapper to emit .NET collection type names

  Changed type name mappings in static constructor:
  - list -> System.Collections.Generic.List
  - dict -> System.Collections.Generic.Dictionary
  - set -> System.Collections.Generic.HashSet"
  ```

---

## Part 3: Update TypeMapper.cs - CreateDictType Method

**File:** `src/Sharpy.Compiler/CodeGen/TypeMapper.cs`

- [x] **4.1** Locate the `CreateDictType` method (around line 200-210):
  ```csharp
  public TypeSyntax CreateDictType(TypeSyntax keyType, TypeSyntax valueType)
  {
      return GenericName("global::Sharpy.Core.Dict")
          .WithTypeArgumentList(
              TypeArgumentList(SeparatedList(new[] { keyType, valueType })));
  }
  ```

- [x] **4.2** Update it to use the .NET type:
  ```csharp
  public TypeSyntax CreateDictType(TypeSyntax keyType, TypeSyntax valueType)
  {
      return GenericName("System.Collections.Generic.Dictionary")
          .WithTypeArgumentList(
              TypeArgumentList(SeparatedList(new[] { keyType, valueType })));
  }
  ```

- [x] **4.3** Verify the file compiles:
  ```bash
  dotnet build src/Sharpy.Compiler
  ```

- [x] **4.4** Run tests:
  ```bash
  dotnet test src/Sharpy.Compiler.Tests
  ```

- [x] **4.5** Commit this change:
  ```bash
  git add src/Sharpy.Compiler/CodeGen/TypeMapper.cs
  git commit -m "feat: update CreateDictType to use System.Collections.Generic.Dictionary"
  ```

---

## Part 4: Add Integration Test for Collection Type Annotations

**Directory:** `src/Sharpy.Compiler.Tests/Integration/TestFixtures/`

- [x] **5.1** Create directory if it doesn't exist:
  ```bash
  mkdir -p src/Sharpy.Compiler.Tests/Integration/TestFixtures/collections
  ```

- [x] **5.2** Create test file `list_type_parameter.spy`:
  ```bash
  cat > src/Sharpy.Compiler.Tests/Integration/TestFixtures/collections/list_type_parameter.spy << 'EOF'
  # Test: list[T] type annotation with .NET List<T>

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
  EOF
  ```

- [x] **5.3** Create test file `dict_type_parameter.spy`:
  ```bash
  cat > src/Sharpy.Compiler.Tests/Integration/TestFixtures/collections/dict_type_parameter.spy << 'EOF'
  # Test: dict[K,V] type annotation with .NET Dictionary<K,V>

  def main():
      scores: dict[str, int] = {"alice": 100, "bob": 85}
      print(scores["alice"])
      print(scores["bob"])

  # EXPECTED OUTPUT:
  # 100
  # 85
  EOF
  ```

- [x] **5.4** Run the full test suite to verify the new fixtures:
  ```bash
  dotnet test src/Sharpy.Compiler.Tests
  ```

- [x] **5.5** If tests fail, debug by checking generated C#:
  ```bash
  # Use the CLI to see generated code
  dotnet run --project src/Sharpy.Cli -- emit csharp src/Sharpy.Compiler.Tests/Integration/TestFixtures/collections/list_type_parameter.spy
  ```

- [x] **5.6** Commit the test fixtures:
  ```bash
  git add src/Sharpy.Compiler.Tests/IntegrationTests/Fixtures/collections/
  git commit -m "test: add integration tests for list[T] and dict[K,V] type annotations"
  ```

---

## Part 5: Update Dogfood Validator (Optional - If Running Dogfood Tests)

**File:** `build_tools/sharpy_dogfood/orchestrator.py`

- [x] **6.1** Open the file and locate `_quick_prevalidate` method (around line 1230)

- [x] **6.2** Find the `forbidden_checks` list and locate these patterns:
  ```python
  (r":\s*list\[", "list type annotation (v0.1.11)"),
  (r":\s*dict\[", "dict type annotation (v0.1.11)"),
  (r":\s*set\[", "set type annotation (v0.1.11)"),
  ```

- [x] **6.3** Comment them out or remove them:
  ```python
  # Collections - NOW SUPPORTED in v0.1.11
  # (r":\s*list\[", "list type annotation (v0.1.11)"),
  # (r":\s*dict\[", "dict type annotation (v0.1.11)"),
  # (r":\s*set\[", "set type annotation (v0.1.11)"),
  ```

- [x] **6.4** Commit the change:
  ```bash
  git add build_tools/sharpy_dogfood/orchestrator.py
  git commit -m "feat: enable list/dict/set type annotations in dogfood validator

  These features are now supported in the compiler."
  ```

---

## Part 6: Final Verification

- [x] **7.1** Run full test suite:
  ```bash
  dotnet test src/Sharpy.Compiler.Tests
  ```

- [x] **7.2** Test with a manual example:
  ```bash
  # Create a test file
  cat > /tmp/test_collections.spy << 'EOF'
  def process(items: list[int]) -> int:
      total: int = 0
      for item in items:
          total += item
      return total

  def main():
      numbers: list[int] = [10, 20, 30]
      result: int = process(numbers)
      print(result)
  EOF

  # Run it
  dotnet run --project src/Sharpy.Cli -- run /tmp/test_collections.spy
  ```

  **Expected output:** `60`

- [x] **7.3** Verify generated C# looks correct:
  ```bash
  dotnet run --project src/Sharpy.Cli -- emit csharp /tmp/test_collections.spy
  ```

  **Verify the output contains:**
  - `System.Collections.Generic.List<int>` (NOT `Sharpy.Core.List<int>`)

- [x] **7.4** Push the branch:
  ```bash
  git push -u origin feature/dotnet-collection-types
  ```

---

## Troubleshooting

### If tests fail after BuiltinRegistry changes:
- Check if any tests explicitly expect `Sharpy.Core.List` in generated code
- Those tests may need updating to expect `System.Collections.Generic.List`

### If compilation fails:
- Ensure `System.Collections.Generic` namespace is available (it's part of .NET base)
- Check for typos in type names (`Dictionary` not `Dict`)

### If runtime errors occur:
- Verify the method names are correct:
  - `List<T>.Add()` (same as before)
  - `Dictionary<K,V>.ContainsKey()` (was `Contains` in Sharpy.Core.Dict)
  - `HashSet<T>.Add()` (same as before)

---

## Completion Checklist

- [x] All compiler changes made (BuiltinRegistry.cs, TypeMapper.cs, RoslynEmitter.Expressions.cs)
- [x] All tests pass
- [x] New integration tests added and passing
- [x] Dogfood validator updated (if applicable)
- [x] Manual verification complete
- [x] All commits pushed to feature branch
- [x] Ready for code review

---

## References

- Language Specification: `docs/language_specification/`
- Implementation Phases: `docs/implementation_planning/phases.md` (Phase 0.1.11: Collections)
- Analysis Document: `docs/implementation_planning/tasks/list_type_annotations_and_multifile_imports.md`
