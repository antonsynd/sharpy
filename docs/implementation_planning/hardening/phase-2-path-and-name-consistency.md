# Phase 2: Path and Name Consistency

> **Priority:** P1 (Important)
> **Estimated Effort:** 3-5 hours
> **Prerequisite:** None (can run in parallel with Phase 1)
> **Concerns Addressed:** #4, #5, #12 from remaining-hardening-concerns.md

---

## Overview

This phase addresses consistency issues that cause subtle bugs and user confusion. These are low-effort, high-value fixes that improve reliability and user experience.

### Why These Go Together

1. **All are quick wins** — Each task is 1-2 hours
2. **No dependencies** — Can be worked in any order
3. **Improve debuggability** — Easier to trace issues when paths are consistent and errors aren't duplicated

### Concerns in This Phase

| # | Concern | Effort | Impact |
|---|---------|--------|--------|
| 4 | Path normalization inconsistent | 1-2h | Medium |
| 5 | Name collision in variable versioning | 1-2h | Medium |
| 12 | Diagnostic deduplication uses strings | 1h | Low |

---

## Task 2.1: Consolidate Path Normalization

**Files:**
- Create: `src/Sharpy.Compiler/Utilities/PathNormalizer.cs`
- Update: `src/Sharpy.Compiler/Project/IncrementalCompilationCache.cs`
- Update: `src/Sharpy.Compiler/Project/ProjectCompiler.cs`
- Update: `src/Sharpy.Compiler/Project/SymbolSerializer.cs`
- Update: `src/Sharpy.Compiler/Project/DependencyGraph.cs`
- Update: `src/Sharpy.Compiler/Project/DependencyGraphBuilder.cs`
- Update: `src/Sharpy.Compiler/Model/ProjectModel.cs`

### Background

Six different `NormalizePath()` implementations exist across the codebase with subtle differences:

| Location | Uses GetFullPath? | Issue |
|----------|------------------|-------|
| IncrementalCompilationCache.cs:435 | Yes | Correct |
| ProjectCompiler.cs:1175 | **No** | Bug: relative paths not normalized |
| SymbolSerializer.cs:627 | Yes | Correct |
| DependencyGraph.cs:450 | Yes | Correct but duplicated |
| DependencyGraphBuilder.cs:217 | Yes | Correct but duplicated |
| ProjectModel.cs:327 | Yes | Correct but duplicated |

When `ProjectCompiler` uses a relative path but other components use absolute paths, the same file gets different cache keys. Additionally, having 6 implementations creates maintenance burden and risk of inconsistency.

### Implementation Checklist

- [ ] **2.1.1** Create the utility class
  ```csharp
  // src/Sharpy.Compiler/Utilities/PathNormalizer.cs
  namespace Sharpy.Compiler.Utilities;

  /// <summary>
  /// Provides consistent path normalization across the compiler.
  /// Used for cache keys, symbol storage, and cross-platform file comparison.
  /// </summary>
  public static class PathNormalizer
  {
      /// <summary>
      /// Normalizes a file path for use as cache keys and cross-platform comparison.
      /// - Resolves to absolute path
      /// - Converts backslashes to forward slashes
      /// - Lowercases on case-insensitive filesystems (Windows, macOS)
      /// </summary>
      /// <param name="path">The path to normalize. Can be relative or absolute.</param>
      /// <returns>A normalized, absolute path suitable for comparison and storage.</returns>
      public static string Normalize(string path)
      {
          if (string.IsNullOrEmpty(path))
              return path;

          // Always resolve to absolute path first
          var normalized = Path.GetFullPath(path).Replace('\\', '/');

          // Case-insensitive on Windows and macOS, case-sensitive on Linux
          if (!OperatingSystem.IsLinux())
          {
              normalized = normalized.ToLowerInvariant();
          }

          return normalized;
      }

      /// <summary>
      /// Gets a relative path from one path to another, normalized.
      /// </summary>
      public static string GetRelative(string relativeTo, string path)
      {
          var relative = Path.GetRelativePath(relativeTo, path).Replace('\\', '/');
          return relative;
      }
  }
  ```

- [ ] **2.1.2** Add unit tests for edge cases
  ```csharp
  // src/Sharpy.Compiler.Tests/Utilities/PathNormalizerTests.cs
  namespace Sharpy.Compiler.Tests.Utilities;

  public class PathNormalizerTests
  {
      [Fact]
      public void NormalizesRelativeToAbsolute()
      {
          var normalized = PathNormalizer.Normalize("./foo/bar.spy");

          Assert.True(Path.IsPathRooted(normalized));
          Assert.Contains("foo/bar.spy", normalized);
      }

      [Fact]
      public void ConvertsBackslashesToForwardSlashes()
      {
          var normalized = PathNormalizer.Normalize(@"C:\foo\bar\test.spy");

          Assert.DoesNotContain("\\", normalized);
          Assert.Contains("/", normalized);
      }

      [Fact]
      public void SameFileGetsSameNormalization()
      {
          // Different ways to refer to the same file
          var abs = Path.GetFullPath("test.spy");
          var rel = "./test.spy";
          var dotdot = "../" + Path.GetFileName(Directory.GetCurrentDirectory()) + "/test.spy";

          var n1 = PathNormalizer.Normalize(abs);
          var n2 = PathNormalizer.Normalize(rel);
          var n3 = PathNormalizer.Normalize(dotdot);

          Assert.Equal(n1, n2);
          Assert.Equal(n2, n3);
      }

      [Fact]
      public void HandlesEmptyAndNull()
      {
          Assert.Equal("", PathNormalizer.Normalize(""));
          Assert.Null(PathNormalizer.Normalize(null!));
      }

      [Theory]
      [InlineData("/foo/bar/./baz", "/foo/bar/baz")]  // Resolves .
      [InlineData("/foo/bar/../baz", "/foo/baz")]      // Resolves ..
      public void ResolvesRelativeComponents(string input, string expectedSuffix)
      {
          if (!OperatingSystem.IsWindows())
          {
              var normalized = PathNormalizer.Normalize(input);
              Assert.EndsWith(expectedSuffix.ToLowerInvariant(), normalized.ToLowerInvariant());
          }
      }
  }
  ```

- [ ] **2.1.3** Update `IncrementalCompilationCache.cs`
  ```csharp
  // Replace local NormalizePath with:
  using Sharpy.Compiler.Utilities;

  // Change all calls from:
  NormalizePath(path)
  // To:
  PathNormalizer.Normalize(path)

  // Remove the local NormalizePath method
  ```

- [ ] **2.1.4** Update `ProjectCompiler.cs`
  ```csharp
  // Add using statement:
  using Sharpy.Compiler.Utilities;

  // Replace local NormalizePath method with PathNormalizer.Normalize calls

  // IMPORTANT: The local method was missing Path.GetFullPath()!
  // This fixes the bug.
  ```

- [ ] **2.1.5** Update `SymbolSerializer.cs`
  ```csharp
  // Same pattern - replace local method with PathNormalizer.Normalize
  ```

- [ ] **2.1.6** Update `DependencyGraph.cs`
  ```csharp
  // Replace local NormalizePath at line 450 with PathNormalizer.Normalize
  ```

- [ ] **2.1.7** Update `DependencyGraphBuilder.cs`
  ```csharp
  // Replace local NormalizePath at line 217 with PathNormalizer.Normalize
  ```

- [ ] **2.1.8** Update `ProjectModel.cs`
  ```csharp
  // Replace local NormalizePath at line 327 with PathNormalizer.Normalize
  ```

- [ ] **2.1.9** Verify no other NormalizePath usages remain
  ```bash
  grep -rn "private static string NormalizePath" src/Sharpy.Compiler/
  # Should output nothing after all updates
  ```

### Verification

```bash
dotnet test --filter "FullyQualifiedName~PathNormalizer"

# Manual test: incremental build with relative vs absolute paths
cd /path/to/project
dotnet run --project src/Sharpy.Cli -- project ./myproject.spyproj --incremental
dotnet run --project src/Sharpy.Cli -- project /full/path/to/myproject.spyproj --incremental
# Both should use the same cache
```

---

## Task 2.2: Fix Variable Name Collision in RoslynEmitter

**File:** `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

### Background

`RoslynEmitter` generates versioned variable names for redeclarations (`x`, `x_1`, `x_2`). However, it doesn't check if generated names collide with user-declared variables.

**Bug scenario:**
```python
x = 1
x_1 = 2      # User-defined variable
x = 3        # Redeclaration — compiler generates x_1
# ERROR: C# sees two declarations of x_1!
```

**Result:** Cryptic Roslyn error: `error CS0128: A local variable named 'x_1' is already defined`

### Implementation Checklist

- [ ] **2.2.1** Find the current variable versioning code
  ```bash
  grep -n "_variableVersions" src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs
  ```

- [ ] **2.2.2** Add a HashSet to track all declared names
  ```csharp
  // Add field near _variableVersions:
  private readonly HashSet<string> _declaredNames = new();
  ```

- [ ] **2.2.3** Modify the name resolution method
  ```csharp
  /// <summary>
  /// Gets a unique C# variable name for a Sharpy variable.
  /// Handles redeclarations by versioning (x, x_1, x_2) while avoiding
  /// collisions with user-declared variables.
  /// </summary>
  private string GetUniqueVariableName(string baseName, bool isNewDeclaration)
  {
      // Case 1: Existing variable reference (not a new declaration)
      if (!isNewDeclaration && _variableVersions.TryGetValue(baseName, out var existingVersion))
      {
          return existingVersion == 0 ? baseName : $"{baseName}_{existingVersion}";
      }

      // Case 2: First declaration of this variable
      if (!_variableVersions.ContainsKey(baseName))
      {
          // Check if the base name itself is already used
          if (_declaredNames.Contains(baseName))
          {
              // Rare: base name is used by a versioned variable (e.g., user has 'x_1', we try to declare 'x_1')
              // Find an available name
              var version = 1;
              string candidateName;
              do
              {
                  candidateName = $"{baseName}_{version}";
                  version++;
              } while (_declaredNames.Contains(candidateName));

              _variableVersions[baseName] = version - 1;
              _declaredNames.Add(candidateName);
              return candidateName;
          }

          _variableVersions[baseName] = 0;
          _declaredNames.Add(baseName);
          return baseName;
      }

      // Case 3: Redeclaration — find next available version
      var currentVersion = _variableVersions[baseName];
      string newName;
      do
      {
          currentVersion++;
          newName = $"{baseName}_{currentVersion}";
      } while (_declaredNames.Contains(newName));

      _variableVersions[baseName] = currentVersion;
      _declaredNames.Add(newName);
      return newName;
  }
  ```

- [ ] **2.2.4** Register user-declared variables at scope entry
  ```csharp
  // When entering a function or block, pre-register all user-declared variable names
  // This ensures we know about x_1 before trying to generate x_1 from x redeclaration

  private void RegisterDeclaredVariables(IEnumerable<Statement> statements)
  {
      foreach (var stmt in statements)
      {
          if (stmt is AssignmentStatement assign && assign.IsDeclaration)
          {
              foreach (var target in assign.Targets)
              {
                  if (target is Identifier id)
                  {
                      // Don't add to _variableVersions, just mark the name as taken
                      _declaredNames.Add(id.Name);
                  }
              }
          }
          // Recursively check nested blocks
          if (stmt is IfStatement ifStmt)
          {
              RegisterDeclaredVariables(ifStmt.Body);
              if (ifStmt.ElseBody != null)
                  RegisterDeclaredVariables(ifStmt.ElseBody);
          }
          // ... other compound statements
      }
  }
  ```

- [ ] **2.2.5** Alternative simpler approach (recommended)

  Instead of pre-scanning, just check and skip collisions at generation time:

  ```csharp
  // Simpler: at the start of function emission, collect all variable names used in source
  private HashSet<string> CollectSourceVariableNames(FunctionDef func)
  {
      var names = new HashSet<string>();
      CollectNamesFromStatements(func.Body, names);
      return names;
  }

  private void CollectNamesFromStatements(IReadOnlyList<Statement> statements, HashSet<string> names)
  {
      foreach (var stmt in statements)
      {
          switch (stmt)
          {
              case AssignmentStatement assign:
                  foreach (var target in assign.Targets)
                  {
                      if (target is Identifier id)
                          names.Add(id.Name);
                  }
                  break;
              case ForStatement forStmt:
                  if (forStmt.Target is Identifier forId)
                      names.Add(forId.Name);
                  CollectNamesFromStatements(forStmt.Body, names);
                  break;
              // ... handle other statement types with nested blocks
          }
      }
  }
  ```

- [ ] **2.2.6** Add tests
  ```csharp
  // src/Sharpy.Compiler.Tests/CodeGen/RoslynEmitterVariableTests.cs

  public class RoslynEmitterVariableTests : IntegrationTestBase
  {
      [Fact]
      public void HandlesVariableNameCollision()
      {
          var source = @"
  def main():
      x = 1
      x_1 = 2       # User-defined
      x = 3         # Redeclaration - should NOT generate x_1
      print(x)
      print(x_1)
  ";
          var result = CompileAndExecute(source);
          Assert.True(result.Success, result.ErrorOutput);
          Assert.Equal("3\n2\n", result.StandardOutput);
      }

      [Fact]
      public void SkipsMultipleCollidingVersionNumbers()
      {
          var source = @"
  def main():
      x = 1
      x_1 = 10
      x_2 = 20
      x = 2         # Should generate x_3, skipping x_1 and x_2
      x = 3         # Should generate x_4
      print(x)      # Latest x (x_4)
      print(x_1)    # User's x_1
      print(x_2)    # User's x_2
  ";
          var result = CompileAndExecute(source);
          Assert.True(result.Success, result.ErrorOutput);
          Assert.Equal("3\n10\n20\n", result.StandardOutput);
      }

      [Fact]
      public void HandlesReversedDeclarationOrder()
      {
          // User declares x_1 AFTER first x declaration
          var source = @"
  def main():
      x = 1
      x = 2         # Generates x_1
      x_1 = 100     # User's x_1 - collision!
      print(x)
      print(x_1)
  ";
          // This tests that we pre-scan for all names
          var result = CompileAndExecute(source);
          Assert.True(result.Success, result.ErrorOutput);
      }
  }
  ```

- [ ] **2.2.7** Add file-based test fixture
  ```
  # TestFixtures/variable_name_collision/main.spy
  def main():
      x = 1
      x_1 = "user"
      x = 2
      print(x)
      print(x_1)
  ```
  ```
  # TestFixtures/variable_name_collision/main.expected
  2
  user
  ```

### Verification

```bash
dotnet test --filter "FullyQualifiedName~VariableTests"
dotnet test --filter "DisplayName~variable_name_collision"
```

### Design Decision

**Q: Should we warn users about underscore-number variable names?**

Some options:
1. **Warn** — "Variable name 'x_1' may conflict with compiler-generated names"
2. **Mangle differently** — Use `__sharpy_x_1` instead of `x_1`
3. **Do nothing** — Just handle collisions silently (current approach)

**Recommendation:** Option 3 (handle silently). The collision is rare, the fix is transparent, and warnings would be noisy for legitimate code (e.g., `matrix_2d`, `version_1`).

---

## Task 2.3: Add Diagnostic Deduplication

**File:** `src/Sharpy.Compiler/Diagnostics/DiagnosticBag.cs`

### Background

`DiagnosticBag` currently has **no deduplication logic**. The `Add()`, `AddRange()`, and `Merge()` methods simply add all diagnostics without checking for duplicates:

```csharp
// Current implementation - no deduplication
public void Merge(DiagnosticBag other)
{
    AddRange(other.GetAll());  // Simply adds all, no duplicate check
}
```

**Problems:**
1. Same error reported multiple times when multiple validators catch it
2. Noisy output confuses users
3. Hard to tell if there's one problem or many

**Solution:** Add deduplication by `(Code, Line, Column)` — semantic identity, not string comparison.

### Implementation Checklist

- [ ] **2.3.1** Add a deduplication set to DiagnosticBag
  ```csharp
  // Add field to DiagnosticBag class
  private readonly HashSet<(string?, int?, int?)> _seenDiagnostics = new();
  ```

- [ ] **2.3.2** Update the Add method to check for duplicates
  ```csharp
  public void Add(CompilerDiagnostic diagnostic)
  {
      // ... existing suppression and promotion logic ...

      // Deduplicate by code and location
      var key = (diagnostic.Code, diagnostic.Line, diagnostic.Column);
      lock (_lock)
      {
          if (!_seenDiagnostics.Add(key))
              return;  // Skip duplicate

          _diagnostics.Add(diagnostic);
      }
  }
  ```

- [ ] **2.3.3** Handle edge case: diagnostics without codes
  ```csharp
  // For diagnostics with null codes, include message in key to distinguish
  private (string?, int?, int?, string?) GetDeduplicationKey(CompilerDiagnostic d)
  {
      if (string.IsNullOrEmpty(d.Code))
      {
          // No code - use message as fallback for uniqueness
          return (null, d.Line, d.Column, d.Message);
      }
      return (d.Code, d.Line, d.Column, null);
  }
  ```

- [ ] **2.3.4** Update Clear method to reset deduplication set
  ```csharp
  public void Clear()
  {
      lock (_lock)
      {
          _diagnostics.Clear();
          _seenDiagnostics.Clear();
      }
  }
  ```

- [ ] **2.3.5** Add tests
  ```csharp
  // src/Sharpy.Compiler.Tests/Diagnostics/DiagnosticBagTests.cs

  [Fact]
  public void DeduplicatesByCodeAndLocation()
  {
      var bag = new DiagnosticBag();

      // Add same error twice with slightly different messages
      bag.AddError(10, 5, "SHP0001", "Cannot assign int to str");
      bag.AddError(10, 5, "SHP0001", "Cannot assign 'int' to 'str'");  // Variant message

      var errors = bag.GetAll().ToList();
      Assert.Single(errors);  // Should deduplicate
  }

  [Fact]
  public void AllowsSameCodeAtDifferentLocations()
  {
      var bag = new DiagnosticBag();

      bag.AddError(10, 5, "SHP0001", "Type mismatch");
      bag.AddError(15, 5, "SHP0001", "Type mismatch");  // Same code, different line

      var errors = bag.GetAll().ToList();
      Assert.Equal(2, errors.Count);  // Should NOT deduplicate
  }

  [Fact]
  public void AllowsDifferentCodesAtSameLocation()
  {
      var bag = new DiagnosticBag();

      bag.AddError(10, 5, "SHP0001", "Type mismatch");
      bag.AddError(10, 5, "SHP0002", "Missing return");  // Different code, same location

      var errors = bag.GetAll().ToList();
      Assert.Equal(2, errors.Count);  // Should NOT deduplicate
  }
  ```

### Verification

```bash
dotnet test --filter "FullyQualifiedName~DiagnosticBag"
```

### Impact on Existing Tests

Some tests might rely on duplicate diagnostics being emitted. After this change:
- Tests checking for specific error counts might need adjustment
- Tests checking for specific message text are unaffected (first instance is kept)

Run full test suite to identify any affected tests:
```bash
dotnet test 2>&1 | grep -i "fail"
```

---

## Phase Completion Criteria

- [ ] All unit tests pass
- [ ] All integration tests pass
- [ ] `PathNormalizer` is used consistently across the codebase
- [ ] Variable name collision test passes
- [ ] Diagnostic deduplication uses code-based comparison
- [ ] Code review completed
- [ ] No grep results for local `NormalizePath` methods

---

## Notes for Implementers

### Task Priority Order

If time is limited, prioritize:
1. **Task 2.1 (Path normalization)** — Fixes actual bug in incremental compilation
2. **Task 2.2 (Name collision)** — Prevents confusing user-facing errors
3. **Task 2.3 (Diagnostic dedup)** — Polish, lower impact

### Common Pitfalls

1. **Path normalization on Windows vs Linux** — Test on both if possible
2. **Variable versioning state reset** — Ensure `_declaredNames` is cleared when entering a new function
3. **Diagnostic code null handling** — Not all diagnostics have codes yet

### Testing Tips

```bash
# Run all Phase 2 tests
dotnet test --filter "FullyQualifiedName~PathNormalizer or FullyQualifiedName~Variable or FullyQualifiedName~DiagnosticBag"

# Quick smoke test
dotnet run --project src/Sharpy.Cli -- run snippets/hello_world.spy
```
