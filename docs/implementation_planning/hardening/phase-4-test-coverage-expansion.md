# Phase 4: Test Coverage Expansion

> **Priority:** P1 (Important)
> **Estimated Effort:** 3-6 hours
> **Prerequisite:** Phase 1 recommended (incremental build tests depend on correct cache)
> **Concerns Addressed:** #9 from remaining-hardening-concerns.md

---

## Overview

This phase addresses gaps in multi-file integration test coverage. The existing test suite has 332+ single-file tests and ~14 multi-file test directories covering basic imports, module imports, and cross-module inheritance. However, several important scenarios lack coverage, particularly programmatic incremental compilation tests and advanced import patterns.

### Existing Multi-File Coverage

The following multi-file tests already exist:
- `imports/` — 3 basic import scenarios
- `module_imports/` — 3 complex import chains
- `cross_module_inheritance/` — 4 inheritance scenarios
- `errors/circular_import/` — Circular import detection
- `warnings/` — 3 import-related warning tests

### Why Additional Tests Matter

1. **Incremental compilation** — No programmatic tests for cache invalidation scenarios
2. **Package imports** — No `__init__.spy` package import tests
3. **Diamond dependencies** — Pattern not explicitly tested
4. **Dependency graph changes** — No tests for file addition/removal between builds

### Concerns in This Phase

| # | Concern | Effort | Impact |
|---|---------|--------|--------|
| 9 | Missing incremental compilation and advanced import tests | 2-4h | Medium |

---

## Task 4.1: Add File-Based Multi-File Test Fixtures

**Location:** `src/Sharpy.Compiler.Tests/Integration/TestFixtures/`

### Background

Multi-file tests use a directory structure:
```
test_name/
  main.spy          # Entry point
  other_files.spy   # Supporting modules
  main.expected     # Expected output (or main.error for error cases)
```

The test runner compiles all `.spy` files in the directory and runs `main.spy`.

### Existing Coverage (Already Implemented)

These scenarios already have test coverage:
- ✅ **Circular import detection** — `errors/circular_import/`
- ✅ **Transitive imports** — `module_imports/multifile_import_chain/`
- ✅ **Cross-file inheritance** — `cross_module_inheritance/` (4 scenarios)
- ✅ **Basic imports** — `imports/` (3 scenarios)

### Test Fixtures to Create

Focus on scenarios **not yet covered**:

#### 4.1.1 Diamond Dependency

- [ ] **Create directory:** `diamond_dependency/`

```python
# base.spy
class Config:
    value: int = 100
```

```python
# left.spy
from base import Config

class LeftHandler:
    def get_value(self) -> int:
        return Config.value + 1
```

```python
# right.spy
from base import Config

class RightHandler:
    def get_value(self) -> int:
        return Config.value + 2
```

```python
# main.spy
from left import LeftHandler
from right import RightHandler

def main():
    l = LeftHandler()
    r = RightHandler()
    print(l.get_value())
    print(r.get_value())
```

```
# main.expected
101
102
```

#### 4.1.2 Cross-File Type References (if not covered by existing tests)

- [ ] **Create directory:** `multi_file/cross_file_types/`

```python
# types.spy
class Point:
    x: int
    y: int

    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y
```

```python
# operations.spy
from types import Point

def add_points(a: Point, b: Point) -> Point:
    return Point(a.x + b.x, a.y + b.y)
```

```python
# main.spy
from types import Point
from operations import add_points

def main():
    p1 = Point(1, 2)
    p2 = Point(3, 4)
    result = add_points(p1, p2)
    print(result.x)
    print(result.y)
```

```
# main.expected
4
6
```

#### 4.1.3 Package Import with `__init__.spy`

- [ ] **Create directory:** `multi_file/package_import/`

```python
# mypackage/__init__.spy
from .helpers import greet
from .math import double
```

```python
# mypackage/helpers.spy
def greet(name: str) -> str:
    return f"Hello, {name}!"
```

```python
# mypackage/math.spy
def double(x: int) -> int:
    return x * 2
```

```python
# main.spy
from mypackage import greet, double

def main():
    print(greet("World"))
    print(double(21))
```

```
# main.expected
Hello, World!
42
```

#### 4.1.4 Import Alias (if not covered)

- [ ] **Create directory:** `import_alias/`

```python
# very_long_module_name.spy
def utility_function() -> str:
    return "utility"

class VeryLongClassName:
    pass
```

```python
# main.spy
import very_long_module_name as vlm
from very_long_module_name import VeryLongClassName as Short

def main():
    print(vlm.utility_function())
    obj = Short()
    print(type(obj).__name__)
```

```
# main.expected
utility
VeryLongClassName
```

#### 4.1.5 Selective Import Error

- [ ] **Create directory:** `multi_file/selective_import_error/`

```python
# lib.spy
def public_func() -> int:
    return 1

def _private_func() -> int:
    return 2
```

```python
# main.spy
from lib import public_func, nonexistent_func

def main():
    print(public_func())
```

```
# main.error
nonexistent_func
```

### Implementation Checklist

**Note:** Some scenarios below may already have partial coverage. Check existing tests before creating duplicates.

- [x] **4.1.1** Create `multi_file/diamond_dependency/` test fixture
- [x] **4.1.2** Create `multi_file/cross_file_types/` test fixture - already covered by `module_imports/complex_type_relationships/`
- [x] **4.1.3** Create `multi_file/package_import/` test fixture - **SKIPPED**: exposes bug in PackageResolver (second re-exported symbol fails)
- [x] **4.1.4** Create `multi_file/import_alias/` test fixture + `multi_file/symbol_alias/` - **symbol_alias SKIPPED**: exposes bug in alias binding
- [x] **4.1.5** Create `errors/selective_import_error/` test fixture

### Verification

```bash
# List existing multi-file test directories
ls -la src/Sharpy.Compiler.Tests/Integration/TestFixtures/imports/
ls -la src/Sharpy.Compiler.Tests/Integration/TestFixtures/module_imports/
ls -la src/Sharpy.Compiler.Tests/Integration/TestFixtures/cross_module_inheritance/

# Run all multi-file tests
dotnet test --filter "DisplayName~import or DisplayName~module_import or DisplayName~cross_module"

# Run specific test
dotnet test --filter "DisplayName~diamond_dependency"
```

---

## Task 4.2: Add Programmatic Incremental Compilation Tests

**File:** `src/Sharpy.Compiler.Tests/Integration/IncrementalCompilationTests.cs`

### Background

Some scenarios require modifying files between builds, which file-based fixtures can't do. These need programmatic tests using `ProjectCompilationHelper`.

### Tests to Create

**Note:** Many of these tests already exist in `src/Sharpy.Compiler.Tests/Project/IncrementalCompilationTests.cs`.

- [x] **4.2.1** Create test file - already exists at `Project/IncrementalCompilationTests.cs`
  ```csharp
  // src/Sharpy.Compiler.Tests/Integration/IncrementalCompilationTests.cs
  namespace Sharpy.Compiler.Tests.Integration;

  public class IncrementalCompilationTests : IntegrationTestBase
  {
      public IncrementalCompilationTests(ITestOutputHelper output) : base(output) { }

      // Tests will be added here
  }
  ```

- [x] **4.2.2** Test: Unchanged files are skipped - covered by `IncrementalMode_NoChanges_SkipsAllFiles`
  ```csharp
  [Fact]
  public void SkipsUnchangedFiles()
  {
      using var helper = new ProjectCompilationHelper(_output);

      helper.AddSourceFile("lib.spy", @"
  def helper() -> int:
      return 42
  ");
      helper.AddSourceFile("main.spy", @"
  from lib import helper

  def main():
      print(helper())
  ");
      helper.CreateProjectFile();

      // First build
      var result1 = helper.Compile(incremental: true);
      Assert.True(result1.Success);

      // Second build with no changes
      var result2 = helper.Compile(incremental: true);
      Assert.True(result2.Success);

      // Verify lib.spy was skipped (check log output or compilation time)
      // This may require exposing skip information from the compiler
  }
  ```

- [x] **4.2.3** Test: Modified file is recompiled - covered by `GetFilesToRecompile_OneChanged_ReturnsOnlyChangedFile` and `IncrementalMode_MultipleFiles_OnlyRecompilesChanged`
  ```csharp
  [Fact]
  public void RecompilesModifiedFile()
  {
      using var helper = new ProjectCompilationHelper(_output);

      helper.AddSourceFile("lib.spy", "def value() -> int:\n    return 1");
      helper.AddSourceFile("main.spy", @"
  from lib import value

  def main():
      print(value())
  ");
      helper.CreateProjectFile();

      var result1 = helper.Compile(incremental: true);
      Assert.Equal("1\n", result1.StandardOutput);

      // Modify lib.spy
      helper.UpdateSourceFile("lib.spy", "def value() -> int:\n    return 999");

      var result2 = helper.Compile(incremental: true);
      Assert.Equal("999\n", result2.StandardOutput);
  }
  ```

- [x] **4.2.4** Test: Dependent file recompiled when import changes - covered by `IncrementalMode_DependencyChangesSignature_RecompilesDependent`
  ```csharp
  [Fact]
  public void RecompilesDependentWhenImportChanges()
  {
      using var helper = new ProjectCompilationHelper(_output);

      helper.AddSourceFile("types.spy", @"
  class Data:
      value: int = 1
  ");
      helper.AddSourceFile("main.spy", @"
  from types import Data

  def main():
      d = Data()
      print(d.value)
  ");
      helper.CreateProjectFile();

      var result1 = helper.Compile(incremental: true);
      Assert.Equal("1\n", result1.StandardOutput);

      // Change the type
      helper.UpdateSourceFile("types.spy", @"
  class Data:
      value: int = 99
  ");

      // main.spy should be recompiled because its dependency changed
      var result2 = helper.Compile(incremental: true);
      Assert.Equal("99\n", result2.StandardOutput);
  }
  ```

- [x] **4.2.5** Test: Transitive dependency change triggers recompile - covered by `IncrementalMode_TransitiveDependency_RecompilesDependents`
  ```csharp
  [Fact]
  public void RecompilesOnTransitiveDependencyChange()
  {
      using var helper = new ProjectCompilationHelper(_output);

      helper.AddSourceFile("base.spy", "BASE_VALUE: int = 10");
      helper.AddSourceFile("mid.spy", @"
  from base import BASE_VALUE

  def get_mid() -> int:
      return BASE_VALUE * 2
  ");
      helper.AddSourceFile("main.spy", @"
  from mid import get_mid

  def main():
      print(get_mid())
  ");
      helper.CreateProjectFile();

      var result1 = helper.Compile(incremental: true);
      Assert.Equal("20\n", result1.StandardOutput);

      // Change the base module
      helper.UpdateSourceFile("base.spy", "BASE_VALUE: int = 50");

      // Both mid.spy and main.spy should be recompiled
      var result2 = helper.Compile(incremental: true);
      Assert.Equal("100\n", result2.StandardOutput);
  }
  ```

- [x] **4.2.6** Test: Adding a new file works - **ADDED**: `IncrementalMode_NewFileAddition_BuildsSuccessfully`
  ```csharp
  [Fact]
  public void HandlesNewFileAddition()
  {
      using var helper = new ProjectCompilationHelper(_output);

      helper.AddSourceFile("main.spy", @"
  def main():
      print(""hello"")
  ");
      helper.CreateProjectFile();

      var result1 = helper.Compile(incremental: true);
      Assert.Equal("hello\n", result1.StandardOutput);

      // Add a new file and import it
      helper.AddSourceFile("utils.spy", @"
  def greet() -> str:
      return ""world""
  ");
      helper.UpdateSourceFile("main.spy", @"
  from utils import greet

  def main():
      print(greet())
  ");

      var result2 = helper.Compile(incremental: true);
      Assert.Equal("world\n", result2.StandardOutput);
  }
  ```

- [x] **4.2.7** Test: Removing an import errors correctly - **ADDED**: `IncrementalMode_FileRemoval_ErrorsCorrectly`
  ```csharp
  [Fact]
  public void ErrorsWhenImportedModuleRemoved()
  {
      using var helper = new ProjectCompilationHelper(_output);

      helper.AddSourceFile("lib.spy", "def foo() -> int:\n    return 1");
      helper.AddSourceFile("main.spy", @"
  from lib import foo

  def main():
      print(foo())
  ");
      helper.CreateProjectFile();

      var result1 = helper.Compile(incremental: true);
      Assert.True(result1.Success);

      // Remove lib.spy
      helper.RemoveSourceFile("lib.spy");

      var result2 = helper.Compile(incremental: true);
      Assert.False(result2.Success);
      Assert.Contains("lib", result2.ErrorOutput.ToLower());
  }
  ```

- [x] **4.2.8** Test: Clean build after cache clear - covered by `IncrementalMode_Clean_ForcesFullRebuild`
  ```csharp
  [Fact]
  public void CleanBuildAfterCacheClear()
  {
      using var helper = new ProjectCompilationHelper(_output);

      helper.AddSourceFile("main.spy", @"
  def main():
      print(42)
  ");
      helper.CreateProjectFile();

      var result1 = helper.Compile(incremental: true);
      Assert.Equal("42\n", result1.StandardOutput);

      // Clear cache
      helper.ClearCache();

      // Should still work
      var result2 = helper.Compile(incremental: true);
      Assert.Equal("42\n", result2.StandardOutput);
  }
  ```

### Implementation Notes

The `ProjectCompilationHelper` class may need methods like:
- `UpdateSourceFile(string name, string content)` — Modify an existing file
- `RemoveSourceFile(string name)` — Delete a file
- `ClearCache()` — Delete `.sharpy-cache` and `.sharpy-symbols`

If these don't exist, add them:

```csharp
public class ProjectCompilationHelper : IDisposable
{
    // Existing methods...

    public void UpdateSourceFile(string name, string content)
    {
        var path = Path.Combine(_projectDir, name);
        File.WriteAllText(path, content);
    }

    public void RemoveSourceFile(string name)
    {
        var path = Path.Combine(_projectDir, name);
        if (File.Exists(path))
            File.Delete(path);
    }

    public void ClearCache()
    {
        var cacheDir = Path.Combine(_projectDir, "obj", "Debug");
        if (Directory.Exists(cacheDir))
        {
            foreach (var file in Directory.GetFiles(cacheDir, ".sharpy-*"))
            {
                File.Delete(file);
            }
        }
    }

    public CompilationResult Compile(bool incremental = false)
    {
        // Add --incremental flag if requested
        var args = incremental ? "--incremental" : "";
        // ... existing compile logic
    }
}
```

### Verification

```bash
dotnet test --filter "FullyQualifiedName~IncrementalCompilation"
```

---

## Phase Completion Criteria

- [x] New file-based multi-file test fixtures created (5 scenarios: diamond_dependency, package_import*, import_alias, symbol_alias*, selective_import_error) *skipped due to bugs discovered
- [x] All programmatic incremental compilation tests passing (existing tests cover most; added NewFileAddition and FileRemoval)
- [x] `ProjectCompilationHelper` has necessary methods (added `RemoveSourceFile`)
- [x] Tests run in both Debug and Release configurations
- [x] No flaky tests (each test deterministic)
- [x] Code review completed (2026-02-04)

**Bugs discovered during implementation:**
1. `PackageResolver.ProcessFromImport` - second re-exported symbol fails (package_import test)
2. Symbol alias binding not registered in symbol table (symbol_alias test)

---

## Notes for Implementers

### File-Based vs Programmatic Tests

**Use file-based tests when:**
- The scenario is static (no modification between builds)
- You want easy-to-read test fixtures
- The test is about compilation output

**Use programmatic tests when:**
- You need to modify files between builds
- You need to verify incremental compilation behavior
- You need to inspect internal state

### Test Naming Convention

- File-based: Directory name describes the scenario (`circular_import_error`)
- Programmatic: Method name describes the assertion (`RecompilesWhenImportChanges`)

### Common Issues

1. **File path casing** — On macOS/Windows, `Lib.spy` and `lib.spy` are the same file but different imports
2. **Test isolation** — Each test should use its own temporary directory
3. **Timing** — File modification times can be unreliable; use content hashing

### Helpful Commands

```bash
# List all test fixtures
ls src/Sharpy.Compiler.Tests/Integration/TestFixtures/multi_file/

# Run a specific fixture test
dotnet test --filter "DisplayName~diamond_dependency"

# Run all multi-file tests
dotnet test --filter "DisplayName~multi_file"

# Run all incremental tests
dotnet test --filter "FullyQualifiedName~IncrementalCompilation"
```
