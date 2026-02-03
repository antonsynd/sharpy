# Phase 1: Incremental Build Correctness

> **Priority:** P0 (Critical)
> **Estimated Effort:** 11-15 hours
> **Prerequisite:** None
> **Concerns Addressed:** #1, #2, #3 from remaining-hardening-concerns.md

---

## Overview

This phase addresses the most critical correctness issues in the incremental compilation system. These bugs can cause **silent correctness failures** where incremental builds produce different results than clean builds—the worst kind of bug because users don't know they're affected.

### Why This Phase Comes First

1. **Production readiness blocker** — Cannot ship v1.0 with incremental build correctness issues
2. **Hard to diagnose** — "Works after clean build" bugs waste hours of debugging
3. **CI/CD impact** — Build caches in CI can reuse stale artifacts across compiler upgrades

### Concerns in This Phase

| # | Concern | Effort | Impact |
|---|---------|--------|--------|
| 1 | Cache lacks compiler version | 2-3h | HIGH |
| 2 | SymbolSerializer format not versioned | 3-4h | HIGH |
| 3 | Restored symbols lack transitive validation | 6-8h | HIGH |

---

## Task 1.1: Add Compiler Version to Incremental Cache

**File:** `src/Sharpy.Compiler/Project/IncrementalCompilationCache.cs`

### Background

The incremental compilation cache stores file content hashes and generated C# code. When a file's content hash matches the cache, its compilation is skipped. However, the cache doesn't track which compiler version produced it.

**Problem scenario:**
1. Build project with compiler v1.0.0
2. Upgrade compiler to v1.0.1 (bug fix in operator emission)
3. Incremental build reuses cached C# from v1.0.0
4. Bug persists despite compiler upgrade

### Implementation Checklist

- [ ] **1.1.1** Read the current cache implementation
  ```bash
  # Understand the current structure
  dotnet run --project src/Sharpy.Cli -- emit csharp snippets/hello_world.spy
  ```

- [ ] **1.1.2** Create `CacheMetadata` record to wrap cache data
  ```csharp
  // Add near the top of IncrementalCompilationCache.cs
  private record CacheMetadata(string CompilerVersion, Dictionary<string, string> FileHashes);
  ```

- [ ] **1.1.3** Implement `GetCompilerVersion()` method
  ```csharp
  private static string GetCompilerVersion()
  {
      var assembly = typeof(IncrementalCompilationCache).Assembly;
      var version = assembly.GetName().Version?.ToString() ?? "0.0.0";

      // Include assembly content hash for debug builds where version doesn't change
      // This ensures cache invalidation during development
      var assemblyPath = assembly.Location;
      if (!string.IsNullOrEmpty(assemblyPath) && File.Exists(assemblyPath))
      {
          var bytes = File.ReadAllBytes(assemblyPath);
          var hash = Convert.ToHexStringLower(SHA256.HashData(bytes)[..8]);
          return $"{version}-{hash}";
      }

      return version;
  }
  ```

- [ ] **1.1.4** Update `LoadHashCache()` to validate version
  ```csharp
  private Dictionary<string, string> LoadHashCache()
  {
      if (!File.Exists(_cacheFilePath))
          return new();

      try
      {
          var json = File.ReadAllText(_cacheFilePath);
          var metadata = JsonSerializer.Deserialize<CacheMetadata>(json, s_jsonOptions);

          if (metadata?.CompilerVersion != GetCompilerVersion())
          {
              _logger?.LogInfo($"Compiler version changed ({metadata?.CompilerVersion} -> {GetCompilerVersion()}); invalidating cache");
              return new();  // Return empty to force full rebuild
          }

          return metadata.FileHashes ?? new();
      }
      catch (JsonException)
      {
          _logger?.LogInfo("Cache file corrupted; invalidating");
          return new();
      }
  }
  ```

- [ ] **1.1.5** Update `SaveHashCache()` to include version
  ```csharp
  private void SaveHashCache()
  {
      var metadata = new CacheMetadata(GetCompilerVersion(), _fileHashes);
      var json = JsonSerializer.Serialize(metadata, s_jsonOptions);

      var directory = Path.GetDirectoryName(_cacheFilePath);
      if (!string.IsNullOrEmpty(directory))
          Directory.CreateDirectory(directory);

      File.WriteAllText(_cacheFilePath, json);
  }
  ```

- [ ] **1.1.6** Add unit tests
  ```csharp
  // In src/Sharpy.Compiler.Tests/Project/IncrementalCompilationCacheTests.cs

  [Fact]
  public void InvalidatesOnVersionChange()
  {
      // Create cache with fake old version
      var cacheDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
      Directory.CreateDirectory(cacheDir);
      var cacheFile = Path.Combine(cacheDir, ".sharpy-cache");

      // Write cache with different version
      var oldMetadata = new { CompilerVersion = "0.0.0-fake", FileHashes = new Dictionary<string, string> { ["test.spy"] = "abc123" } };
      File.WriteAllText(cacheFile, JsonSerializer.Serialize(oldMetadata));

      // Load cache - should be empty due to version mismatch
      var cache = new IncrementalCompilationCache(cacheDir, null);

      Assert.False(cache.HasValidCache("test.spy"));

      Directory.Delete(cacheDir, recursive: true);
  }

  [Fact]
  public void PreservesOnSameVersion()
  {
      // Test that cache is preserved when version matches
      // This requires mocking or accessing the version method
  }
  ```

### Verification

```bash
# Build and run tests
dotnet test --filter "FullyQualifiedName~IncrementalCompilationCache"

# Manual verification:
# 1. Build a project with --incremental
# 2. Modify the compiler (any source change)
# 3. Rebuild the same project with --incremental
# 4. Verify cache was invalidated (check log output)
```

### Decision Point

**Q: Should we also hash the Sharpy.Core assembly?**

If `Sharpy.Core` changes (e.g., a bug fix in `List<T>.append()`), cached symbols that reference Sharpy.Core types might be stale.

**Recommendation:** For now, only track the compiler assembly. Sharpy.Core changes are rare and typically require source code changes that would invalidate the cache anyway. Revisit if this becomes a problem.

---

## Task 1.2: Add Schema Version to SymbolSerializer

**Files:**
- `src/Sharpy.Compiler/Project/SymbolSerializer.cs`
- `src/Sharpy.Compiler/Project/SymbolCache.cs`

### Background

`SymbolSerializer` persists compiled symbols and generated C# to disk for incremental compilation. The serialization format uses `System.Text.Json` but has no schema version. If we add/remove/rename fields, old caches silently produce incorrect data.

**Problem scenario:**
1. Cache contains `FileCacheEntry` with fields `{A, B}`
2. New compiler version adds field `C` with important default
3. Deserialization succeeds but `C` has wrong default value
4. Silent semantic errors

### Implementation Checklist

- [ ] **1.2.1** Read current serialization code
  ```bash
  # Understand the structure
  grep -n "FileCacheEntry" src/Sharpy.Compiler/Project/SymbolSerializer.cs
  ```

- [ ] **1.2.2** Define schema version constant
  ```csharp
  // At the top of SymbolSerializer.cs
  private const int CurrentSchemaVersion = 1;
  ```

- [ ] **1.2.3** Create envelope record for versioned serialization
  ```csharp
  private record SymbolCacheEnvelope(
      int SchemaVersion,
      Dictionary<string, FileCacheEntry> Files
  );
  ```

- [ ] **1.2.4** Update serialization to wrap in envelope
  ```csharp
  public void Save(SymbolCache cache)
  {
      var envelope = new SymbolCacheEnvelope(
          CurrentSchemaVersion,
          cache.Entries
      );

      var json = JsonSerializer.Serialize(envelope, s_options);
      // ... write to file
  }
  ```

- [ ] **1.2.5** Update deserialization to validate version
  ```csharp
  public SymbolCache? Load()
  {
      if (!File.Exists(_symbolCachePath))
          return null;

      try
      {
          var json = File.ReadAllText(_symbolCachePath);
          var envelope = JsonSerializer.Deserialize<SymbolCacheEnvelope>(json, s_options);

          if (envelope == null)
          {
              _logger?.LogInfo("Symbol cache empty; rebuilding");
              return null;
          }

          if (envelope.SchemaVersion != CurrentSchemaVersion)
          {
              _logger?.LogInfo($"Symbol cache schema version {envelope.SchemaVersion} != {CurrentSchemaVersion}; rebuilding");
              return null;
          }

          return new SymbolCache(envelope.Files);
      }
      catch (JsonException ex)
      {
          _logger?.LogInfo($"Symbol cache corrupted: {ex.Message}; rebuilding");
          return null;
      }
  }
  ```

- [ ] **1.2.6** Add strict JSON options to catch unexpected fields
  ```csharp
  private static readonly JsonSerializerOptions s_strictOptions = new()
  {
      PropertyNameCaseInsensitive = true,
      WriteIndented = false,  // Smaller cache files
      // Note: UnmappedMemberHandling requires .NET 8+
      // If targeting earlier, skip this or use a custom converter
  };
  ```

- [ ] **1.2.7** Add migration documentation comment
  ```csharp
  /// <summary>
  /// Schema version history:
  ///   v1 (2026-02): Initial versioned format
  ///
  /// When making breaking changes:
  ///   1. Increment CurrentSchemaVersion
  ///   2. Add migration logic if data can be upgraded
  ///   3. Document the change here
  /// </summary>
  private const int CurrentSchemaVersion = 1;
  ```

- [ ] **1.2.8** Add unit tests
  ```csharp
  [Fact]
  public void RejectsOldSchemaVersion()
  {
      var cacheDir = CreateTempDirectory();
      var cachePath = Path.Combine(cacheDir, ".sharpy-symbols");

      // Write cache with old schema version
      var oldEnvelope = new { SchemaVersion = 0, Files = new Dictionary<string, object>() };
      File.WriteAllText(cachePath, JsonSerializer.Serialize(oldEnvelope));

      var serializer = new SymbolSerializer(cachePath, null);
      var result = serializer.Load();

      Assert.Null(result);  // Should reject old version
  }

  [Fact]
  public void AcceptsCurrentSchemaVersion()
  {
      // Write and read cache with current version
      // Verify round-trip works
  }
  ```

### Verification

```bash
dotnet test --filter "FullyQualifiedName~SymbolSerializer"
```

### Future Consideration: Migration Support

Currently, schema version mismatch causes full cache invalidation. For large projects, this could be slow. Future enhancement:

```csharp
private SymbolCache? TryMigrate(SymbolCacheEnvelope envelope)
{
    return envelope.SchemaVersion switch
    {
        // Add migration logic when needed
        // 0 => MigrateV0ToV1(envelope),
        _ => null  // Unknown version, force rebuild
    };
}
```

**Recommendation:** Don't implement migration now. Full rebuild on schema change is acceptable for v1.0. Add migration if cache invalidation becomes a pain point.

---

## Task 1.3: Validate Restored Symbols Against Current SymbolTable

**File:** `src/Sharpy.Compiler/Project/ProjectCompiler.cs`

### Background

When incremental compilation skips an unchanged file, it restores cached symbols without verifying they're still valid. If a dependency changed (e.g., a type was renamed or a method signature changed), the restored symbols have stale references.

**Problem scenarios:**

**Scenario 1 — Type Renamed:**
1. `A.spy` imports `B.MyClass`
2. First build: both compiled, cached
3. `B.spy` changes: `MyClass` → `MyNewClass`
4. Second build: `A.spy` unchanged, restored from cache
5. **Bug:** `A`'s symbol still references non-existent `MyClass`

**Scenario 2 — Signature Changed:**
1. `A.spy`: `def foo() -> int: return 1`
2. `B.spy`: `from A import foo; x = foo()`
3. First build: `B` knows `foo()` returns `int`
4. `A.spy` changes: `def foo() -> str: return "x"`
5. Second build: `A` recompiles, `B` skips (unchanged)
6. **Bug:** `B`'s cached info says `foo()` returns `int`

### Implementation Checklist

- [ ] **1.3.1** Understand current restoration flow
  ```bash
  # Find RestoreCachedSymbols method
  grep -n "RestoreCachedSymbols" src/Sharpy.Compiler/Project/ProjectCompiler.cs
  ```

- [ ] **1.3.2** Create helper method to validate symbol references
  ```csharp
  /// <summary>
  /// Validates that a restored symbol's type references resolve in the current SymbolTable.
  /// Returns false if any reference is stale (type renamed, removed, etc.).
  /// </summary>
  private bool ValidateRestoredSymbol(Symbol symbol)
  {
      return symbol switch
      {
          VariableSymbol v => ValidateType(v.Type),
          FunctionSymbol f => ValidateFunctionSymbol(f),
          TypeSymbol t => ValidateTypeSymbol(t),
          _ => true  // Other symbol types (Module, etc.) don't have type refs
      };
  }

  private bool ValidateType(SemanticType? type)
  {
      if (type == null)
          return true;

      return type switch
      {
          UserDefinedType udt => _symbolTable.Lookup(udt.Name) != null,
          GenericType gt => gt.TypeArguments.All(ValidateType),
          NullableType nt => ValidateType(nt.UnderlyingType),
          OptionalType ot => ValidateType(ot.UnderlyingType),
          FunctionType ft => ValidateType(ft.ReturnType) && ft.ParameterTypes.All(ValidateType),
          TupleType tt => tt.ElementTypes.All(ValidateType),
          // Builtin types always valid
          BuiltinType => true,
          VoidType => true,
          UnknownType => true,
          _ => true
      };
  }
  ```

- [ ] **1.3.3** Add function signature validation
  ```csharp
  private bool ValidateFunctionSymbol(FunctionSymbol cached)
  {
      // Validate return type exists
      if (!ValidateType(cached.ReturnType))
          return false;

      // Validate parameter types exist
      foreach (var param in cached.Parameters)
      {
          if (!ValidateType(param.Type))
              return false;
      }

      // If this function is imported, verify the current version has matching signature
      var current = _symbolTable.Lookup(cached.FullyQualifiedName ?? cached.Name);
      if (current is FunctionSymbol currentFunc)
      {
          if (!SignaturesMatch(cached, currentFunc))
          {
              _logger?.LogInfo($"Function signature changed: {cached.Name}");
              return false;
          }
      }

      return true;
  }

  private bool SignaturesMatch(FunctionSymbol cached, FunctionSymbol current)
  {
      // Return type must match
      if (!TypesEqual(cached.ReturnType, current.ReturnType))
          return false;

      // Parameter count must match
      if (cached.Parameters.Count != current.Parameters.Count)
          return false;

      // Each parameter type must match
      for (int i = 0; i < cached.Parameters.Count; i++)
      {
          if (!TypesEqual(cached.Parameters[i].Type, current.Parameters[i].Type))
              return false;
      }

      return true;
  }

  private bool TypesEqual(SemanticType? a, SemanticType? b)
  {
      if (a == null && b == null) return true;
      if (a == null || b == null) return false;

      // SemanticType records use structural equality
      return a.Equals(b);
  }
  ```

- [ ] **1.3.4** Add type symbol validation (for class inheritance)
  ```csharp
  private bool ValidateTypeSymbol(TypeSymbol cached)
  {
      // Validate base type still exists with same name
      if (cached.BaseType != null)
      {
          var currentBase = _symbolTable.Lookup(cached.BaseType.Name);
          if (currentBase == null)
          {
              _logger?.LogInfo($"Base type no longer exists: {cached.BaseType.Name}");
              return false;
          }
      }

      // Validate interface implementations still exist
      foreach (var iface in cached.Interfaces)
      {
          if (_symbolTable.Lookup(iface.Name) == null)
          {
              _logger?.LogInfo($"Interface no longer exists: {iface.Name}");
              return false;
          }
      }

      // Validate field types
      foreach (var field in cached.Fields)
      {
          if (!ValidateType(field.Type))
              return false;
      }

      // Validate method signatures
      foreach (var method in cached.Methods)
      {
          if (!ValidateFunctionSymbol(method))
              return false;
      }

      return true;
  }
  ```

- [ ] **1.3.5** Integrate validation into `RestoreCachedSymbols()`
  ```csharp
  private void RestoreCachedSymbols()
  {
      // ... existing restoration logic ...

      // After restoring, validate all symbols
      var invalidFiles = new HashSet<string>();

      foreach (var (symbolName, symbol) in _restoredSymbols)
      {
          if (!ValidateRestoredSymbol(symbol))
          {
              var filePath = symbol.FilePath ?? symbol.Location?.FilePath;
              if (filePath != null)
              {
                  invalidFiles.Add(PathNormalizer.Normalize(filePath));
              }
          }
      }

      // Re-add invalid files to recompile set
      foreach (var file in invalidFiles)
      {
          _logger?.LogInfo($"Restored symbol validation failed; recompiling: {file}");
          _filesToSkip.Remove(file);

          // Remove invalidated symbols from restored set
          var symbolsToRemove = _restoredSymbols
              .Where(kvp => PathNormalizer.Normalize(kvp.Value.FilePath ?? "") == file)
              .Select(kvp => kvp.Key)
              .ToList();

          foreach (var key in symbolsToRemove)
          {
              _restoredSymbols.Remove(key);
          }
      }
  }
  ```

- [ ] **1.3.6** Add integration tests

  **Test fixture: `TestFixtures/multi_file/incremental_type_rename/`**

  This requires programmatic tests since file-based fixtures don't support incremental testing.

  ```csharp
  // In src/Sharpy.Compiler.Tests/Integration/IncrementalCompilationTests.cs

  public class IncrementalCompilationTests : IntegrationTestBase
  {
      [Fact]
      public void RecompilesWhenImportedTypeRenamed()
      {
          using var helper = new ProjectCompilationHelper(_output);

          // Initial files
          helper.AddSourceFile("types.spy", @"
  class MyClass:
      x: int = 1
  ");
          helper.AddSourceFile("main.spy", @"
  from types import MyClass

  def main():
      obj = MyClass()
      print(obj.x)
  ");
          helper.CreateProjectFile();

          // First build
          var result1 = helper.Compile(incremental: true);
          Assert.True(result1.Success);
          Assert.Equal("1\n", result1.StandardOutput);

          // Rename type in types.spy
          helper.UpdateSourceFile("types.spy", @"
  class RenamedClass:
      x: int = 99
  ");

          // Second build - main.spy should fail because MyClass no longer exists
          var result2 = helper.Compile(incremental: true);
          Assert.False(result2.Success);
          Assert.Contains("MyClass", result2.ErrorOutput);  // Should mention missing type
      }

      [Fact]
      public void RecompilesWhenMethodSignatureChanges()
      {
          using var helper = new ProjectCompilationHelper(_output);

          helper.AddSourceFile("lib.spy", @"
  def get_value() -> int:
      return 42
  ");
          helper.AddSourceFile("main.spy", @"
  from lib import get_value

  def main():
      x: int = get_value()
      print(x)
  ");
          helper.CreateProjectFile();

          var result1 = helper.Compile(incremental: true);
          Assert.True(result1.Success);

          // Change return type
          helper.UpdateSourceFile("lib.spy", @"
  def get_value() -> str:
      return ""hello""
  ");

          // main.spy should be recompiled and fail type check
          var result2 = helper.Compile(incremental: true);
          Assert.False(result2.Success);
          Assert.Contains("type", result2.ErrorOutput.ToLower());
      }

      [Fact]
      public void RecompilesWhenBaseClassChanges()
      {
          using var helper = new ProjectCompilationHelper(_output);

          helper.AddSourceFile("base.spy", @"
  class Animal:
      def speak(self) -> str:
          return ""...""
  ");
          helper.AddSourceFile("derived.spy", @"
  from base import Animal

  class Dog(Animal):
      @override
      def speak(self) -> str:
          return ""woof""
  ");
          helper.AddSourceFile("main.spy", @"
  from derived import Dog

  def main():
      d = Dog()
      print(d.speak())
  ");
          helper.CreateProjectFile();

          var result1 = helper.Compile(incremental: true);
          Assert.True(result1.Success);
          Assert.Equal("woof\n", result1.StandardOutput);

          // Change base class signature
          helper.UpdateSourceFile("base.spy", @"
  class Animal:
      def speak(self) -> int:  # Changed return type!
          return 0
  ");

          // derived.spy should be invalidated and recompiled
          var result2 = helper.Compile(incremental: true);
          Assert.False(result2.Success);  // Override signature mismatch
      }
  }
  ```

- [ ] **1.3.7** Add logging for debugging cache validation
  ```csharp
  private bool ValidateRestoredSymbol(Symbol symbol)
  {
      _logger?.LogDebug($"Validating restored symbol: {symbol.Name} ({symbol.GetType().Name})");

      var valid = symbol switch { /* ... */ };

      if (!valid)
      {
          _logger?.LogInfo($"Symbol validation failed: {symbol.Name}");
      }

      return valid;
  }
  ```

### Verification

```bash
# Run all incremental compilation tests
dotnet test --filter "FullyQualifiedName~IncrementalCompilation"

# Manual verification:
# 1. Create a two-file project
# 2. Build with --incremental
# 3. Modify a type in the dependency
# 4. Build again with --incremental
# 5. Verify the dependent file was recompiled (check output)
```

### Performance Consideration

Symbol validation adds overhead to incremental builds. However:
- Validation only runs for skipped (cached) files
- Most validations are O(1) symbol table lookups
- The alternative (incorrect builds) is far worse

If validation becomes a bottleneck for very large projects, consider:
1. Caching validation results
2. Only validating symbols that have cross-file references
3. Using a dependency graph to scope validation

**Recommendation:** Implement straightforward validation first. Optimize only if profiling shows it's a bottleneck.

---

## Phase Completion Criteria

- [ ] All unit tests pass
- [ ] All integration tests pass
- [ ] Manual verification: compiler version change invalidates cache
- [ ] Manual verification: schema version change invalidates symbol cache
- [ ] Manual verification: type rename in dependency triggers recompilation
- [ ] Manual verification: signature change in dependency triggers recompilation
- [ ] Code review completed
- [ ] Documentation updated (CLAUDE.md incremental compilation section)

---

## Rollback Plan

If issues are discovered after deployment:

1. **Cache version** — Users can delete `obj/*/.sharpy-*` files
2. **Schema version** — Same as above
3. **Symbol validation** — Can be disabled by removing the validation call (not recommended long-term)

---

## Dependencies on Later Phases

Task 1.3 uses `PathNormalizer.Normalize()` which is implemented in Phase 2 (Task 2.1). During Phase 1, use the local `NormalizePath()` method that already exists in `ProjectCompiler`. Phase 2 will consolidate these.
