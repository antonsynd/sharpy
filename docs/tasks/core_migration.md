# Sharpy.Object Migration Task List

## Overview

This task list migrates `Sharpy.Core` away from the `Sharpy.Object` base class and Sharpy-specific interfaces, aligning with the dunder-as-alias design where dunders map directly to .NET methods, properties, and operators.

**Prerequisites:**
- All existing tests pass before starting
- Working directory: `/Users/anton/Documents/github/sharpy`
- Run tests with: `dotnet test src/Sharpy.Core.Tests`

**Guiding Principles:**
- Each commit should leave all tests passing
- Update tests alongside the code they test
- Delete tests for removed functionality
- Keep commits small and focused

---

## Phase 0: Preparation

### Step 0.1: Create a migration branch and verify baseline
- [ ] Create branch: `git checkout -b migration/remove-sharpy-object`
- [ ] Run all tests: `dotnet test src/Sharpy.Core.Tests`
- [ ] Verify all tests pass
- [ ] **Commit:** `git commit --allow-empty -m "chore: start Sharpy.Object migration"`

### Step 0.2: Document current interface usage
- [ ] Create file `docs/migration/sharpy_object_removal.md`
- [ ] List all files that reference `Sharpy.Object` as a base class
- [ ] List all files that reference Sharpy-specific interfaces (`ISized`, `IContainer`, etc.)
- [ ] **Commit:** `git add docs && git commit -m "docs: document Sharpy.Object migration plan"`

---

## Phase 1: Update `List<T>` (Most Complex Collection)

### Step 1.1: Change `__Len__` return type from `uint` to `int`
- [ ] Edit `src/Sharpy.Core/Partial.List/List.ISized.cs`
  - Change `public uint __Len__()` to `public int __Len__()`
  - Change return from `(uint)_list.Count` to `_list.Count`
- [ ] Edit `src/Sharpy.Core/Collections/Interfaces/ISized.cs`
  - Change `uint __Len__()` to `int __Len__()`
  - Update `Length` and `Count` default implementations to remove casts
- [ ] Edit `src/Sharpy.Core/Len.cs`
  - Change return type from `uint` to `int`
  - Update `Len(string s)` similarly
- [ ] Update tests in `src/Sharpy.Core.Tests/Partial.ListTests/ListTests.Len.cs`
  - Change any `uint` assertions to `int`
- [ ] Run tests: `dotnet test src/Sharpy.Core.Tests`
- [ ] **Commit:** `git commit -am "refactor(List): change __Len__ return type from uint to int"`

### Step 1.2: Add .NET interface implementations to `List<T>` (additive)
- [ ] Edit `src/Sharpy.Core/Partial.List/List.cs`
  - Add `IList<T>` and `IReadOnlyList<T>` to the inheritance list (keep existing interfaces for now)
- [ ] Create `src/Sharpy.Core/Partial.List/List.IList.cs` with explicit interface implementations that delegate to existing methods:
  ```csharp
  public partial class List<T>
  {
      // ICollection<T>.Count - delegate to existing
      int ICollection<T>.Count => __Len__();
      int IReadOnlyCollection<T>.Count => __Len__();

      // IList<T>.IndexOf
      int IList<T>.IndexOf(T item) => (int)Index(item);

      // Already have: Add, Clear, Contains, CopyTo, Remove, Insert, RemoveAt, GetEnumerator, indexer
  }
  ```
- [ ] Run tests: `dotnet test src/Sharpy.Core.Tests`
- [ ] **Commit:** `git commit -am "feat(List): add IList<T> and IReadOnlyList<T> interface implementations"`

### Step 1.3: Replace `__Len__` with `Count` property
- [ ] Edit `src/Sharpy.Core/Partial.List/List.ISized.cs`
  - Rename `__Len__()` method to be a `Count` property: `public int Count => _list.Count;`
  - Remove explicit interface implementations from Step 1.2 that now conflict
- [ ] Edit `src/Sharpy.Core/Partial.List/List.IList.cs`
  - Remove `ICollection<T>.Count` and `IReadOnlyCollection<T>.Count` (now satisfied by public `Count`)
- [ ] Update any internal references from `__Len__()` to `Count` in:
  - [ ] `List.IMutableSequence.cs` (search for `__Len__`)
  - [ ] `List.ISequence.cs` (if exists)
  - [ ] `List.IBoolConvertible.cs`
- [ ] Run tests: `dotnet test src/Sharpy.Core.Tests`
- [ ] **Commit:** `git commit -am "refactor(List): replace __Len__() with Count property"`

### Step 1.4: Replace `__Eq__` with `Equals` override
- [ ] Edit `src/Sharpy.Core/Partial.List/List.IEquatable.cs`
  - Rename `public bool __Eq__(List<T> other)` to `public bool Equals(List<T>? other)`
  - Change `public override bool __Eq__(Object obj)` to `public override bool Equals(object? obj)`
  - Update internal logic to not call `Operator.Exports.Eq` - use `Equals()` or `EqualityComparer<T>.Default.Equals()`
- [ ] Edit `src/Sharpy.Core/Partial.List/List.DotNet.IEquatable.cs`
  - This file may become redundant - merge or delete as needed
- [ ] Update tests in `src/Sharpy.Core.Tests/Partial.ListTests/ListTests.Equality.cs`
  - Change any `__Eq__` calls to `Equals`
- [ ] Run tests: `dotnet test src/Sharpy.Core.Tests`
- [ ] **Commit:** `git commit -am "refactor(List): replace __Eq__ with Equals override"`

### Step 1.5: Replace `__Hash__` with `GetHashCode` override
- [ ] Edit `src/Sharpy.Core/Partial.List/List.IHashable.cs`
  - Rename method to `public override int GetHashCode()`
  - Keep the implementation logic
- [ ] Run tests: `dotnet test src/Sharpy.Core.Tests`
- [ ] **Commit:** `git commit -am "refactor(List): replace __Hash__ with GetHashCode override"`

### Step 1.6: Replace `__Repr__` and `__Str__` with `ToString` override
- [ ] Edit `src/Sharpy.Core/Partial.List/List.IRepresentable.cs`
  - Rename `__Repr__()` to a private helper or inline it
  - Create `public override string ToString()` that does the repr logic
- [ ] Delete or merge `List.IStrConvertible.cs` if it exists (or was inherited from Object)
- [ ] Update tests in `src/Sharpy.Core.Tests/Partial.ListTests/ListTests.Repr.cs` and `ListTests.Str.cs`
- [ ] Run tests: `dotnet test src/Sharpy.Core.Tests`
- [ ] **Commit:** `git commit -am "refactor(List): replace __Repr__/__Str__ with ToString override"`

### Step 1.7: Replace `__Bool__` with `operator true/false`
- [ ] Edit `src/Sharpy.Core/Partial.List/List.IBoolConvertible.cs`
  - Remove `__Bool__()` method
  - Add/update:
    ```csharp
    public static bool operator true(List<T>? list) => list is not null && list.Count > 0;
    public static bool operator false(List<T>? list) => list is null || list.Count == 0;
    ```
- [ ] Update tests in `src/Sharpy.Core.Tests/Partial.ListTests/ListTests.Bool.cs`
  - Change `__Bool__()` calls to use the operators or cast to bool in if statements
- [ ] Run tests: `dotnet test src/Sharpy.Core.Tests`
- [ ] **Commit:** `git commit -am "refactor(List): replace __Bool__ with operator true/false"`

### Step 1.8: Replace `__Contains__` with `Contains` method
- [ ] Edit `src/Sharpy.Core/Partial.List/List.IContainer.cs`
  - Rename `__Contains__` to `Contains` (may already exist for ICollection<T>)
  - Ensure it's the public method, not a dunder
- [ ] Update tests in `src/Sharpy.Core.Tests/Partial.ListTests/ListTests.Contains.cs`
- [ ] Run tests: `dotnet test src/Sharpy.Core.Tests`
- [ ] **Commit:** `git commit -am "refactor(List): replace __Contains__ with Contains method"`

### Step 1.9: Replace `__Iter__` with `GetEnumerator`
- [ ] Edit `src/Sharpy.Core/Partial.List/List.IIterable.cs`
  - Ensure `GetEnumerator()` is the public method
  - Remove `__Iter__()` if separate
- [ ] Edit `src/Sharpy.Core/Partial.List/List.IEnumerable.cs`
  - Consolidate enumeration logic here
- [ ] Update tests in `src/Sharpy.Core.Tests/Partial.ListTests/ListTests.Iteration.cs`
- [ ] Run tests: `dotnet test src/Sharpy.Core.Tests`
- [ ] **Commit:** `git commit -am "refactor(List): replace __Iter__ with GetEnumerator"`

### Step 1.10: Replace `__GetItem__`/`__SetItem__` with indexer
- [ ] Edit `src/Sharpy.Core/Partial.List/List.IMutableSequence.cs`
  - Rename `__GetItem__(int index)` logic to be the indexer getter
  - Rename `__SetItem__(int index, T value)` logic to be the indexer setter
  - Keep slice overloads as separate methods (e.g., `GetSlice`, `SetSlice`) called by the multi-param indexers
- [ ] Update tests in `src/Sharpy.Core.Tests/Partial.ListTests/ListTests.GetItem.cs` and `ListTests.SetItem.cs`
- [ ] Run tests: `dotnet test src/Sharpy.Core.Tests`
- [ ] **Commit:** `git commit -am "refactor(List): replace __GetItem__/__SetItem__ with indexer"`

### Step 1.11: Replace operator dunders with C# operators
- [ ] Edit `src/Sharpy.Core/Partial.List/List.IAddable.cs`
  - Rename `__Add__` to be `operator +`
- [ ] Edit `src/Sharpy.Core/Partial.List/List.IRightAddable.cs`
  - Merge into operator definitions or delete
- [ ] Edit `src/Sharpy.Core/Partial.List/List.IInplaceAddable.cs`
  - `__IAdd__` becomes the implementation behind `+=` (C# handles this via `operator +` for classes)
  - May need to keep as `AddRange` or similar method
- [ ] Edit `src/Sharpy.Core/Partial.List/List.IMultipliable.cs` and related
  - Rename `__Mul__` to be `operator *`
- [ ] Edit `src/Sharpy.Core/Partial.List/List.operators.cs`
  - Consolidate all operators here
- [ ] Update tests in `src/Sharpy.Core.Tests/Partial.ListTests/ListTests.Addition.cs` and `ListTests.Multiplication.cs`
- [ ] Run tests: `dotnet test src/Sharpy.Core.Tests`
- [ ] **Commit:** `git commit -am "refactor(List): replace operator dunders with C# operators"`

### Step 1.12: Remove `Object` base class from `List<T>`
- [ ] Edit `src/Sharpy.Core/Partial.List/List.cs`
  - Change `public sealed partial class List<T> : Object, ...` to `public sealed partial class List<T> : IList<T>, IReadOnlyList<T>, IEquatable<List<T>>`
  - Remove all Sharpy-specific interfaces from inheritance list
- [ ] Delete files that are now empty or redundant:
  - [ ] `List.IContainer.cs` (if empty)
  - [ ] `List.IIterable.cs` (if empty)
  - [ ] `List.ISized.cs` (if only had `__Len__`)
  - [ ] Any other `List.I*.cs` files that only contained dunder methods
- [ ] Run tests: `dotnet test src/Sharpy.Core.Tests`
- [ ] **Commit:** `git commit -am "refactor(List): remove Object base class and Sharpy interfaces"`

### Step 1.13: Consolidate `List<T>` partial files
- [ ] Review remaining partial files in `src/Sharpy.Core/Partial.List/`
- [ ] Merge small files into logical groupings:
  - `List.cs` - core class, constructors, fields
  - `List.Interfaces.cs` - IList<T>, ICollection<T> implementation
  - `List.Operators.cs` - all operator overloads
  - `List.Methods.cs` - Python-style methods (Append, Extend, Pop, etc.)
  - `List.Slicing.cs` - slice-related logic
- [ ] Delete empty/redundant partial files
- [ ] Run tests: `dotnet test src/Sharpy.Core.Tests`
- [ ] **Commit:** `git commit -am "refactor(List): consolidate partial class files"`

---

## Phase 2: Update `Set<T>`

### Step 2.1: Change `__Len__` return type from `uint` to `int`
- [ ] Edit `src/Sharpy.Core/Partial.Set/Set.ISized.cs`
  - Change return type to `int`
- [ ] Update tests in `src/Sharpy.Core.Tests/Partial.SetTests/SetTests.Len.cs`
- [ ] Run tests: `dotnet test src/Sharpy.Core.Tests`
- [ ] **Commit:** `git commit -am "refactor(Set): change __Len__ return type from uint to int"`

### Step 2.2: Add .NET interface implementations to `Set<T>`
- [ ] Edit `src/Sharpy.Core/Partial.Set/Set.cs`
  - Add `ISet<T>` and `IReadOnlySet<T>` to inheritance (keep existing for now)
- [ ] Create `src/Sharpy.Core/Partial.Set/Set.ISet.cs` with any missing interface methods
- [ ] Run tests: `dotnet test src/Sharpy.Core.Tests`
- [ ] **Commit:** `git commit -am "feat(Set): add ISet<T> and IReadOnlySet<T> interface implementations"`

### Step 2.3: Replace `__Len__` with `Count` property
- [ ] Edit `src/Sharpy.Core/Partial.Set/Set.ISized.cs`
  - Replace method with property
- [ ] Update internal references
- [ ] Run tests: `dotnet test src/Sharpy.Core.Tests`
- [ ] **Commit:** `git commit -am "refactor(Set): replace __Len__() with Count property"`

### Step 2.4: Replace `__Eq__` with `Equals` override
- [ ] Edit `src/Sharpy.Core/Partial.Set/Set.IEquatable.cs`
- [ ] Update tests in `src/Sharpy.Core.Tests/Partial.SetTests/SetTests.Equality.cs`
- [ ] Run tests: `dotnet test src/Sharpy.Core.Tests`
- [ ] **Commit:** `git commit -am "refactor(Set): replace __Eq__ with Equals override"`

### Step 2.5: Replace `__Hash__` with `GetHashCode` override
- [ ] Edit `src/Sharpy.Core/Partial.Set/Set.IHashable.cs`
- [ ] Run tests: `dotnet test src/Sharpy.Core.Tests`
- [ ] **Commit:** `git commit -am "refactor(Set): replace __Hash__ with GetHashCode override"`

### Step 2.6: Replace `__Repr__` with `ToString` override
- [ ] Edit `src/Sharpy.Core/Partial.Set/Set.IRepresentable.cs`
- [ ] Update tests in `src/Sharpy.Core.Tests/Partial.SetTests/SetTests.Repr.cs`
- [ ] Run tests: `dotnet test src/Sharpy.Core.Tests`
- [ ] **Commit:** `git commit -am "refactor(Set): replace __Repr__ with ToString override"`

### Step 2.7: Replace `__Bool__` with `operator true/false`
- [ ] Edit `src/Sharpy.Core/Partial.Set/Set.IBoolConvertible.cs`
- [ ] Update tests in `src/Sharpy.Core.Tests/Partial.SetTests/SetTests.Bool.cs`
- [ ] Run tests: `dotnet test src/Sharpy.Core.Tests`
- [ ] **Commit:** `git commit -am "refactor(Set): replace __Bool__ with operator true/false"`

### Step 2.8: Replace `__Contains__` with `Contains` method
- [ ] Edit `src/Sharpy.Core/Partial.Set/Set.IContainer.cs`
- [ ] Update tests in `src/Sharpy.Core.Tests/Partial.SetTests/SetTests.Contains.cs`
- [ ] Run tests: `dotnet test src/Sharpy.Core.Tests`
- [ ] **Commit:** `git commit -am "refactor(Set): replace __Contains__ with Contains method"`

### Step 2.9: Replace `__Iter__` with `GetEnumerator`
- [ ] Edit `src/Sharpy.Core/Partial.Set/Set.IIterable.cs`
- [ ] Update tests in `src/Sharpy.Core.Tests/Partial.SetTests/SetTests.Iteration.cs`
- [ ] Run tests: `dotnet test src/Sharpy.Core.Tests`
- [ ] **Commit:** `git commit -am "refactor(Set): replace __Iter__ with GetEnumerator"`

### Step 2.10: Replace set operator dunders with C# operators
- [ ] Edit `src/Sharpy.Core/Partial.Set/Set.ISet.cs` and `Set.IMutableSet.cs`
  - `__Or__` → `operator |` (union)
  - `__And__` → `operator &` (intersection)
  - `__Sub__` → `operator -` (difference)
  - `__XOr__` → `operator ^` (symmetric difference)
- [ ] Edit `src/Sharpy.Core/Partial.Set/Set.operators.cs`
- [ ] Update tests in `src/Sharpy.Core.Tests/Partial.SetTests/SetTests.Union.cs`, `SetTests.Intersection.cs`, `SetTests.Difference.cs`, `SetTests.SymmetricDifference.cs`
- [ ] Run tests: `dotnet test src/Sharpy.Core.Tests`
- [ ] **Commit:** `git commit -am "refactor(Set): replace set operator dunders with C# operators"`

### Step 2.11: Replace comparison dunders with C# operators
- [ ] Edit `src/Sharpy.Core/Partial.Set/Set.ILessThanComparable.cs` and related
  - `__Lt__` → `operator <` (proper subset)
  - `__Le__` → `operator <=` (subset)
  - `__Gt__` → `operator >` (proper superset)
  - `__Ge__` → `operator >=` (superset)
- [ ] Update tests in `src/Sharpy.Core.Tests/Partial.SetTests/SetTests.Comparators.cs`, `SetTests.IsSubset.cs`, `SetTests.IsSuperset.cs`
- [ ] Run tests: `dotnet test src/Sharpy.Core.Tests`
- [ ] **Commit:** `git commit -am "refactor(Set): replace comparison dunders with C# operators"`

### Step 2.12: Remove `Object` base class from `Set<T>`
- [ ] Edit `src/Sharpy.Core/Partial.Set/Set.cs`
  - Remove `: Object` and Sharpy interfaces
  - Keep only `ISet<T>`, `IReadOnlySet<T>`, `IEquatable<Set<T>>`
- [ ] Delete empty/redundant partial files
- [ ] Run tests: `dotnet test src/Sharpy.Core.Tests`
- [ ] **Commit:** `git commit -am "refactor(Set): remove Object base class and Sharpy interfaces"`

### Step 2.13: Consolidate `Set<T>` partial files
- [ ] Merge into logical groupings
- [ ] Delete empty files
- [ ] Run tests: `dotnet test src/Sharpy.Core.Tests`
- [ ] **Commit:** `git commit -am "refactor(Set): consolidate partial class files"`

---

## Phase 3: Update `Dict<K,V>`

### Step 3.1: Change `__Len__` return type from `uint` to `int`
- [ ] Edit `src/Sharpy.Core/Dict.cs`
  - Change `__Len__()` return type to `int`
- [ ] Update tests in `src/Sharpy.Core.Tests/DictTests.cs`
- [ ] Run tests: `dotnet test src/Sharpy.Core.Tests`
- [ ] **Commit:** `git commit -am "refactor(Dict): change __Len__ return type from uint to int"`

### Step 3.2: Add .NET interface implementations to `Dict<K,V>`
- [ ] Edit `src/Sharpy.Core/Dict.cs`
  - Add `IDictionary<K,V>` and `IReadOnlyDictionary<K,V>` to inheritance
- [ ] Implement any missing interface members
- [ ] Run tests: `dotnet test src/Sharpy.Core.Tests`
- [ ] **Commit:** `git commit -am "feat(Dict): add IDictionary<K,V> and IReadOnlyDictionary<K,V> implementations"`

### Step 3.3: Replace `__Len__` with `Count` property
- [ ] Change method to property in `src/Sharpy.Core/Dict.cs`
- [ ] Run tests: `dotnet test src/Sharpy.Core.Tests`
- [ ] **Commit:** `git commit -am "refactor(Dict): replace __Len__() with Count property"`

### Step 3.4: Replace `__Eq__` with `Equals` override
- [ ] Update `src/Sharpy.Core/Dict.cs`
- [ ] Run tests: `dotnet test src/Sharpy.Core.Tests`
- [ ] **Commit:** `git commit -am "refactor(Dict): replace __Eq__ with Equals override"`

### Step 3.5: Replace `__Hash__` with `GetHashCode` override
- [ ] Update `src/Sharpy.Core/Dict.cs`
- [ ] Run tests: `dotnet test src/Sharpy.Core.Tests`
- [ ] **Commit:** `git commit -am "refactor(Dict): replace __Hash__ with GetHashCode override"`

### Step 3.6: Replace `__Repr__` with `ToString` override
- [ ] Update `src/Sharpy.Core/Dict.cs`
- [ ] Run tests: `dotnet test src/Sharpy.Core.Tests`
- [ ] **Commit:** `git commit -am "refactor(Dict): replace __Repr__ with ToString override"`

### Step 3.7: Replace `__Bool__` with `operator true/false`
- [ ] Update `src/Sharpy.Core/Dict.cs`
- [ ] Run tests: `dotnet test src/Sharpy.Core.Tests`
- [ ] **Commit:** `git commit -am "refactor(Dict): replace __Bool__ with operator true/false"`

### Step 3.8: Replace `__Contains__` with `ContainsKey` method
- [ ] Update `src/Sharpy.Core/Dict.cs`
- [ ] Run tests: `dotnet test src/Sharpy.Core.Tests`
- [ ] **Commit:** `git commit -am "refactor(Dict): replace __Contains__ with ContainsKey"`

### Step 3.9: Replace `__Iter__` with `GetEnumerator`
- [ ] Update `src/Sharpy.Core/Dict.cs`
- [ ] Run tests: `dotnet test src/Sharpy.Core.Tests`
- [ ] **Commit:** `git commit -am "refactor(Dict): replace __Iter__ with GetEnumerator"`

### Step 3.10: Replace `__GetItem__`/`__SetItem__`/`__DelItem__` with indexer and methods
- [ ] Update `src/Sharpy.Core/Dict.cs`
  - Indexer for get/set
  - `Remove(K key)` for delete
- [ ] Run tests: `dotnet test src/Sharpy.Core.Tests`
- [ ] **Commit:** `git commit -am "refactor(Dict): replace item dunders with indexer and Remove"`

### Step 3.11: Replace `__Or__`/`__IOr__` with `operator |`
- [ ] Update `src/Sharpy.Core/Dict.cs`
- [ ] Run tests: `dotnet test src/Sharpy.Core.Tests`
- [ ] **Commit:** `git commit -am "refactor(Dict): replace __Or__ with operator |"`

### Step 3.12: Remove `Object` base class from `Dict<K,V>`
- [ ] Edit `src/Sharpy.Core/Dict.cs`
  - Remove `: Object` and Sharpy interfaces
- [ ] Run tests: `dotnet test src/Sharpy.Core.Tests`
- [ ] **Commit:** `git commit -am "refactor(Dict): remove Object base class and Sharpy interfaces"`

---

## Phase 4: Update Dict Views

### Step 4.1: Update `DictKeyView<K,V>`
- [ ] Edit `src/Sharpy.Core/DictKeyView.cs`
  - Remove any Sharpy interface dependencies
  - Implement `IReadOnlyCollection<K>` or `IEnumerable<K>`
- [ ] Run tests: `dotnet test src/Sharpy.Core.Tests`
- [ ] **Commit:** `git commit -am "refactor(DictKeyView): use .NET interfaces"`

### Step 4.2: Update `DictValuesView<K,V>`
- [ ] Edit `src/Sharpy.Core/DictValuesView.cs`
- [ ] Run tests: `dotnet test src/Sharpy.Core.Tests`
- [ ] **Commit:** `git commit -am "refactor(DictValuesView): use .NET interfaces"`

### Step 4.3: Update `DictItemsView<K,V>`
- [ ] Edit `src/Sharpy.Core/DictItemsView.cs`
- [ ] Run tests: `dotnet test src/Sharpy.Core.Tests`
- [ ] **Commit:** `git commit -am "refactor(DictItemsView): use .NET interfaces"`

---

## Phase 5: Update Iterators

### Step 5.1: Update `Iterator<T>`
- [ ] Edit `src/Sharpy.Core/Partial.Iterator/Iterator.cs`
  - Remove Sharpy interface dependencies
  - Ensure it implements `IEnumerator<T>`
- [ ] Run tests: `dotnet test src/Sharpy.Core.Tests`
- [ ] **Commit:** `git commit -am "refactor(Iterator): use .NET interfaces"`

### Step 5.2: Update `ListIterator<T>`
- [ ] Edit `src/Sharpy.Core/Partial.ListIterator/`
- [ ] Run tests: `dotnet test src/Sharpy.Core.Tests`
- [ ] **Commit:** `git commit -am "refactor(ListIterator): use .NET interfaces"`

### Step 5.3: Update `ListReverseIterator<T>`
- [ ] Edit `src/Sharpy.Core/Partial.ListReverseIterator/`
- [ ] Run tests: `dotnet test src/Sharpy.Core.Tests`
- [ ] **Commit:** `git commit -am "refactor(ListReverseIterator): use .NET interfaces"`

### Step 5.4: Update `SetIterator<T>`
- [ ] Edit `src/Sharpy.Core/Partial.SetIterator/`
- [ ] Run tests: `dotnet test src/Sharpy.Core.Tests`
- [ ] **Commit:** `git commit -am "refactor(SetIterator): use .NET interfaces"`

### Step 5.5: Update `EnumeratorIterator<T>`
- [ ] Edit `src/Sharpy.Core/EnumeratorIterator.cs`
- [ ] Run tests: `dotnet test src/Sharpy.Core.Tests`
- [ ] **Commit:** `git commit -am "refactor(EnumeratorIterator): use .NET interfaces"`

---

## Phase 6: Update Builtins

### Step 6.1: Update `Len()` builtin
- [ ] Edit `src/Sharpy.Core/Len.cs`
  - Remove `Len(ISized sized)` overload
  - Add `Len<T>(ICollection<T> c)` and `Len<T>(IReadOnlyCollection<T> c)`
  - Keep `Len(string s)`
- [ ] Run tests: `dotnet test src/Sharpy.Core.Tests`
- [ ] **Commit:** `git commit -am "refactor(Len): use .NET ICollection instead of ISized"`

### Step 6.2: Update `Repr()` builtin
- [ ] Edit `src/Sharpy.Core/Repr.cs`
  - Remove any `IRepresentable` checks
  - Use `ToString()` as fallback
- [ ] Run tests: `dotnet test src/Sharpy.Core.Tests`
- [ ] **Commit:** `git commit -am "refactor(Repr): remove IRepresentable dependency"`

### Step 6.3: Update `Bool()` builtin (or create if missing)
- [ ] Create/edit `src/Sharpy.Core/Bool.cs`
  ```csharp
  public static bool Bool(object? obj) => obj switch
  {
      null => false,
      bool b => b,
      ICollection c => c.Count > 0,
      string s => s.Length > 0,
      _ => true
  };
  ```
- [ ] Add tests in `src/Sharpy.Core.Tests/BoolTests.cs`
- [ ] Run tests: `dotnet test src/Sharpy.Core.Tests`
- [ ] **Commit:** `git commit -am "refactor(Bool): use runtime dispatch for truthiness"`

### Step 6.4: Update `Iter()` builtin
- [ ] Edit `src/Sharpy.Core/Iter.cs`
  - Remove `IIterable` dependency
  - Use `IEnumerable<T>` instead
- [ ] Run tests: `dotnet test src/Sharpy.Core.Tests`
- [ ] **Commit:** `git commit -am "refactor(Iter): use IEnumerable instead of IIterable"`

### Step 6.5: Update `Reversed()` builtin
- [ ] Edit `src/Sharpy.Core/Reversed.cs`
  - Remove `IReversible` dependency
  - Check for `IList<T>` and iterate backwards, or use `Enumerable.Reverse()`
- [ ] Run tests: `dotnet test src/Sharpy.Core.Tests`
- [ ] **Commit:** `git commit -am "refactor(Reversed): remove IReversible dependency"`

### Step 6.6: Update `Sorted()` builtin
- [ ] Edit `src/Sharpy.Core/Sorted.cs`
  - Ensure it works with `IEnumerable<T>`
- [ ] Run tests: `dotnet test src/Sharpy.Core.Tests`
- [ ] **Commit:** `git commit -am "refactor(Sorted): ensure IEnumerable compatibility"`

### Step 6.7: Update remaining builtins that use Sharpy interfaces
- [ ] Review and update: `All.cs`, `Any.cs`, `Enumerate.cs`, `Filter.cs`, `Map.cs`, `Max.cs`, `Min.cs`, `Next.cs`, `Sum.cs`, `Zip.cs`
- [ ] Run tests after each file: `dotnet test src/Sharpy.Core.Tests`
- [ ] **Commit:** `git commit -am "refactor(builtins): remove Sharpy interface dependencies"`

---

## Phase 7: Update Other Types

### Step 7.1: Update `Str` type
- [ ] Edit `src/Sharpy.Core/Partial.Str/Str.cs`
  - Remove Sharpy interface dependencies if any
- [ ] Edit related partial files
- [ ] Run tests: `dotnet test src/Sharpy.Core.Tests`
- [ ] **Commit:** `git commit -am "refactor(Str): remove Sharpy interface dependencies"`

### Step 7.2: Update `Range` type
- [ ] Edit `src/Sharpy.Core/Range.cs`
- [ ] Run tests: `dotnet test src/Sharpy.Core.Tests`
- [ ] **Commit:** `git commit -am "refactor(Range): remove Sharpy interface dependencies"`

### Step 7.3: Update `Slice` type
- [ ] Edit `src/Sharpy.Core/Slice.cs`
- [ ] Run tests: `dotnet test src/Sharpy.Core.Tests`
- [ ] **Commit:** `git commit -am "refactor(Slice): remove Sharpy interface dependencies"`

### Step 7.4: Update `Bytes` type
- [ ] Edit `src/Sharpy.Core/Bytes.cs`
- [ ] Run tests: `dotnet test src/Sharpy.Core.Tests`
- [ ] **Commit:** `git commit -am "refactor(Bytes): remove Sharpy interface dependencies"`

### Step 7.5: Update `Complex` type
- [ ] Edit `src/Sharpy.Core/Partial.Complex/Complex.cs`
- [ ] Run tests: `dotnet test src/Sharpy.Core.Tests`
- [ ] **Commit:** `git commit -am "refactor(Complex): remove Sharpy interface dependencies"`

---

## Phase 8: Delete `Sharpy.Object` and Sharpy Interfaces

### Step 8.1: Delete `Sharpy.Object` class
- [ ] Delete entire directory: `src/Sharpy.Core/Partial.Object/`
  - `Object.cs`
  - `Object.object.cs`
  - `Object.operators.cs`
  - `Object.IEquatable.cs`
  - `Object.IHashable.cs`
  - `Object.IIdentifiable.cs`
  - `Object.IInequatable.cs`
  - `Object.IBoolConvertible.cs`
  - `Object.IRepresentable.cs`
  - `Object.IStrConvertible.cs`
  - `Object.DotNet.IEquatable.cs`
- [ ] Run tests: `dotnet test src/Sharpy.Core.Tests`
- [ ] **Commit:** `git commit -am "chore: delete Sharpy.Object class"`

### Step 8.2: Delete operator interfaces
- [ ] Delete files:
  - [ ] `IAbsoluteValue.cs`
  - [ ] `IAddable.cs`
  - [ ] `IBitwiseAndable.cs`
  - [ ] `IBitwiseOrable.cs`
  - [ ] `IBitwiseXorable.cs`
  - [ ] `IDivisible.cs`
  - [ ] `IFloorDivisible.cs`
  - [ ] `IInplaceAddable.cs`
  - [ ] `IInplaceMultipliable.cs`
  - [ ] `IInvertible.cs`
  - [ ] `ILeftShiftable.cs`
  - [ ] `IModulable.cs`
  - [ ] `IMultipliable.cs`
  - [ ] `INegatable.cs`
  - [ ] `IPowerable.cs`
  - [ ] `IRightAddable.cs`
  - [ ] `IRightMultipliable.cs`
  - [ ] `IRightShiftable.cs`
  - [ ] `ISubtractable.cs`
  - [ ] `IUnaryPlusable.cs`
- [ ] Run tests: `dotnet test src/Sharpy.Core.Tests`
- [ ] **Commit:** `git commit -am "chore: delete operator interfaces"`

### Step 8.3: Delete comparison interfaces
- [ ] Delete files:
  - [ ] `IEquatable.cs` (Sharpy's version)
  - [ ] `IInequatable.cs`
  - [ ] `IGreaterThanComparable.cs`
  - [ ] `IGreaterThanOrEquatable.cs`
  - [ ] `ILessThanComparable.cs`
  - [ ] `ILessThanOrEquatable.cs`
- [ ] Run tests: `dotnet test src/Sharpy.Core.Tests`
- [ ] **Commit:** `git commit -am "chore: delete comparison interfaces"`

### Step 8.4: Delete protocol interfaces
- [ ] Delete files:
  - [ ] `IBoolConvertible.cs`
  - [ ] `IHashable.cs`
  - [ ] `IIdentifiable.cs`
  - [ ] `IRepresentable.cs`
  - [ ] `IStrConvertible.cs`
- [ ] Run tests: `dotnet test src/Sharpy.Core.Tests`
- [ ] **Commit:** `git commit -am "chore: delete protocol interfaces"`

### Step 8.5: Delete collection interfaces
- [ ] Delete directory: `src/Sharpy.Core/Collections/Interfaces/`
  - [ ] `ISized.cs`
  - [ ] `IContainer.cs`
  - [ ] `IIterable.cs`
  - [ ] `ICollection.cs`
  - [ ] `IReversible.cs`
  - [ ] `ISequence.cs`
  - [ ] `IMutableSequence.cs`
  - [ ] `ISet.cs`
  - [ ] `IMutableSet.cs`
  - [ ] `IMapping.cs`
  - [ ] `IMutableMapping.cs`
  - [ ] `IMappingView.cs`
  - [ ] `IKeysView.cs`
  - [ ] `IValuesView.cs`
  - [ ] `IItemsView.cs`
- [ ] Delete `src/Sharpy.Core/Collections/Exports.cs` if empty
- [ ] Delete `src/Sharpy.Core/Collections/` directory if empty
- [ ] Run tests: `dotnet test src/Sharpy.Core.Tests`
- [ ] **Commit:** `git commit -am "chore: delete Sharpy collection interfaces"`

### Step 8.6: Delete helper classes that depended on deleted interfaces
- [ ] Review and delete if unused:
  - [ ] `IdentityAdapterFactory.cs`
  - [ ] `ComparerAdapter.cs`
  - [ ] `KeyComparer.cs`
- [ ] Run tests: `dotnet test src/Sharpy.Core.Tests`
- [ ] **Commit:** `git commit -am "chore: delete unused helper classes"`

---

## Phase 9: Add `FrozenSet<T>`

### Step 9.1: Create `FrozenSet<T>` class
- [ ] Create `src/Sharpy.Core/FrozenSet.cs`
  ```csharp
  public sealed class FrozenSet<T> : IReadOnlySet<T>, IEquatable<FrozenSet<T>>
  {
      private readonly HashSet<T> _set;  // or System.Collections.Frozen.FrozenSet<T> if targeting .NET 8+

      public FrozenSet() => _set = [];
      public FrozenSet(IEnumerable<T> items) => _set = [..items];

      public int Count => _set.Count;
      public bool Contains(T item) => _set.Contains(item);
      public IEnumerator<T> GetEnumerator() => _set.GetEnumerator();
      IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

      public override bool Equals(object? obj) => obj is FrozenSet<T> other && SetEquals(other);
      public bool Equals(FrozenSet<T>? other) => other is not null && _set.SetEquals(other._set);
      public override int GetHashCode() { /* XOR of element hashes */ }
      public override string ToString() => $"frozenset({{{string.Join(", ", _set.Select(Repr))}}})";

      public static bool operator true(FrozenSet<T>? s) => s is not null && s.Count > 0;
      public static bool operator false(FrozenSet<T>? s) => s is null || s.Count == 0;

      // Set operators
      public static FrozenSet<T> operator |(FrozenSet<T> a, FrozenSet<T> b) => new(a._set.Union(b._set));
      public static FrozenSet<T> operator &(FrozenSet<T> a, FrozenSet<T> b) => new(a._set.Intersect(b._set));
      public static FrozenSet<T> operator -(FrozenSet<T> a, FrozenSet<T> b) => new(a._set.Except(b._set));
      public static FrozenSet<T> operator ^(FrozenSet<T> a, FrozenSet<T> b) => new(a._set.SymmetricExceptWith...);

      // Comparison operators (subset/superset)
      public static bool operator <(FrozenSet<T> a, FrozenSet<T> b) => a._set.IsProperSubsetOf(b._set);
      public static bool operator <=(FrozenSet<T> a, FrozenSet<T> b) => a._set.IsSubsetOf(b._set);
      public static bool operator >(FrozenSet<T> a, FrozenSet<T> b) => a._set.IsProperSupersetOf(b._set);
      public static bool operator >=(FrozenSet<T> a, FrozenSet<T> b) => a._set.IsSupersetOf(b._set);
      public static bool operator ==(FrozenSet<T>? a, FrozenSet<T>? b) => a?.Equals(b) ?? b is null;
      public static bool operator !=(FrozenSet<T>? a, FrozenSet<T>? b) => !(a == b);

      // Python-style methods
      public FrozenSet<T> Copy() => new(_set);
      public FrozenSet<T> Union(FrozenSet<T> other) => this | other;
      public FrozenSet<T> Intersection(FrozenSet<T> other) => this & other;
      public FrozenSet<T> Difference(FrozenSet<T> other) => this - other;
      public FrozenSet<T> SymmetricDifference(FrozenSet<T> other) => this ^ other;
      public bool IsSubset(FrozenSet<T> other) => this <= other;
      public bool IsSuperset(FrozenSet<T> other) => this >= other;
      public bool IsDisjoint(FrozenSet<T> other) => !_set.Overlaps(other._set);
  }
  ```
- [ ] Run tests: `dotnet test src/Sharpy.Core.Tests`
- [ ] **Commit:** `git commit -am "feat: add FrozenSet<T> immutable set type"`

### Step 9.2: Add `FrozenSet<T>` tests
- [ ] Create `src/Sharpy.Core.Tests/FrozenSetTests.cs`
  - [ ] Constructor tests
  - [ ] Count/Contains tests
  - [ ] Equality tests (including hashability)
  - [ ] Set operator tests (|, &, -, ^)
  - [ ] Comparison operator tests (<, <=, >, >=)
  - [ ] Truthiness tests
  - [ ] Iteration tests
- [ ] Run tests: `dotnet test src/Sharpy.Core.Tests`
- [ ] **Commit:** `git commit -am "test: add FrozenSet<T> tests"`

### Step 9.3: Add `frozenset()` builtin
- [ ] Create `src/Sharpy.Core/FrozenSetConversion.cs`
  ```csharp
  public static FrozenSet<T> FrozenSet<T>(IEnumerable<T> items) => new(items);
  public static FrozenSet<T> FrozenSet<T>() => new();
  ```
- [ ] Add tests in `src/Sharpy.Core.Tests/FrozenSetConversionTests.cs`
- [ ] Run tests: `dotnet test src/Sharpy.Core.Tests`
- [ ] **Commit:** `git commit -am "feat: add frozenset() builtin"`

---

## Phase 10: Final Cleanup

### Step 10.1: Update `Exports.cs` files
- [ ] Edit `src/Sharpy.Core/Builtins/Exports.cs`
  - Remove exports for deleted types/methods
  - Add exports for new types/methods
- [ ] Run tests: `dotnet test src/Sharpy.Core.Tests`
- [ ] **Commit:** `git commit -am "chore: update Exports.cs files"`

### Step 10.2: Clean up `using` statements
- [ ] Run a tool or manually review all `.cs` files
- [ ] Remove unused `using Sharpy.Collections.Interfaces;` and similar
- [ ] Run tests: `dotnet test src/Sharpy.Core.Tests`
- [ ] **Commit:** `git commit -am "chore: clean up unused using statements"`

### Step 10.3: Update XML documentation
- [ ] Review and update XML docs on public APIs
- [ ] Ensure references to `__len__`, `__eq__`, etc. are updated to refer to `Count`, `Equals`, etc.
- [ ] Run tests: `dotnet test src/Sharpy.Core.Tests`
- [ ] **Commit:** `git commit -am "docs: update XML documentation for new API"`

### Step 10.4: Delete migration documentation
- [ ] Delete `docs/migration/sharpy_object_removal.md` (or move to archive)
- [ ] **Commit:** `git commit -am "chore: remove migration documentation"`

### Step 10.5: Final verification
- [ ] Run full test suite: `dotnet test src/Sharpy.Core.Tests`
- [ ] Run any integration tests
- [ ] Build in Release mode: `dotnet build -c Release src/Sharpy.Core`
- [ ] Verify no warnings
- [ ] **Commit:** `git commit -am "chore: final migration verification"`

### Step 10.6: Merge
- [ ] Create PR for review
- [ ] After approval: `git checkout main && git merge migration/remove-sharpy-object`
- [ ] Delete branch: `git branch -d migration/remove-sharpy-object`
- [ ] Tag release if appropriate: `git tag v0.2.0-alpha`

---

## Summary Statistics

| Phase | Commits | Estimated Files Changed |
|-------|---------|------------------------|
| Phase 0: Preparation | 2 | 1 |
| Phase 1: List<T> | 13 | ~25 |
| Phase 2: Set<T> | 13 | ~19 |
| Phase 3: Dict<K,V> | 12 | ~5 |
| Phase 4: Dict Views | 3 | 3 |
| Phase 5: Iterators | 5 | ~10 |
| Phase 6: Builtins | 7 | ~15 |
| Phase 7: Other Types | 5 | ~10 |
| Phase 8: Deletions | 6 | ~50 deleted |
| Phase 9: FrozenSet | 3 | 3 |
| Phase 10: Cleanup | 6 | ~10 |
| **Total** | **~75** | **~150** |
