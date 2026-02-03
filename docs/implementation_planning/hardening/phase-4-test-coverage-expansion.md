# Phase 4: Test Coverage Expansion

> **Priority:** P1 (Important)
> **Estimated Effort:** 3-6 hours
> **Prerequisite:** Phase 1 recommended (incremental build tests depend on correct cache)
> **Concerns Addressed:** #9 from remaining-hardening-concerns.md

---

## Overview

This phase addresses the gap in multi-file integration test coverage. The existing test suite has 332+ single-file tests but only ~3 multi-file test fixtures. Many import-related bugs and incremental compilation issues can only be caught with multi-file tests.

### Why Multi-File Tests Matter

1. **Import resolution** — Single-file tests can't test cross-module imports
2. **Dependency graph** — Transitive imports, diamond dependencies need multiple files
3. **Incremental compilation** — Cache invalidation bugs only manifest with multiple files
4. **Real-world coverage** — Actual projects always have multiple files

### Concerns in This Phase

| # | Concern | Effort | Impact |
|---|---------|--------|--------|
| 9 | Missing multi-file integration tests | 3-6h | Medium |

---

## Task 4.1: Add File-Based Multi-File Test Fixtures

**Location:** `src/Sharpy.Compiler.Tests/Integration/TestFixtures/multi_file/`

### Background

Multi-file tests use a directory structure:
```
test_name/
  main.spy          # Entry point
  other_files.spy   # Supporting modules
  main.expected     # Expected output (or main.error for error cases)
```

The test runner compiles all `.spy` files in the directory and runs `main.spy`.

### Test Fixtures to Create

#### 4.1.1 Circular Import Detection

- [ ] **Create directory:** `circular_import_error/`

```python
# a.spy
from b import bar

def foo() -> int:
    return bar() + 1
```

```python
# b.spy
from a import foo

def bar() -> int:
    return foo() + 1
```

```python
# main.spy
from a import foo

def main():
    print(foo())
```

```
# main.error
circular import
```

#### 4.1.2 Transitive Imports

- [ ] **Create directory:** `transitive_imports/`

```python
# level3.spy
def deep_value() -> int:
    return 42
```

```python
# level2.spy
from level3 import deep_value

def mid_value() -> int:
    return deep_value() * 2
```

```python
# level1.spy
from level2 import mid_value

def top_value() -> int:
    return mid_value() + 1
```

```python
# main.spy
from level1 import top_value

def main():
    print(top_value())
```

```
# main.expected
85
```

#### 4.1.3 Diamond Dependency

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

#### 4.1.4 Cross-File Type References

- [ ] **Create directory:** `cross_file_types/`

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

#### 4.1.5 Package Import with `__init__.spy`

- [ ] **Create directory:** `package_import/`

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

#### 4.1.6 Inheritance Across Files

- [ ] **Create directory:** `cross_file_inheritance/`

```python
# animal.spy
@abstract
class Animal:
    name: str

    def __init__(self, name: str):
        self.name = name

    @abstract
    def speak(self) -> str:
        ...
```

```python
# dog.spy
from animal import Animal

class Dog(Animal):
    def __init__(self, name: str):
        super().__init__(name)

    @override
    def speak(self) -> str:
        return f"{self.name} says woof!"
```

```python
# cat.spy
from animal import Animal

class Cat(Animal):
    def __init__(self, name: str):
        super().__init__(name)

    @override
    def speak(self) -> str:
        return f"{self.name} says meow!"
```

```python
# main.spy
from dog import Dog
from cat import Cat

def main():
    d = Dog("Rex")
    c = Cat("Whiskers")
    print(d.speak())
    print(c.speak())
```

```
# main.expected
Rex says woof!
Whiskers says meow!
```

#### 4.1.7 Import Alias

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

#### 4.1.8 Selective Import Error

- [ ] **Create directory:** `selective_import_error/`

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

- [ ] **4.1.1** Create `circular_import_error/` test fixture
- [ ] **4.1.2** Create `transitive_imports/` test fixture
- [ ] **4.1.3** Create `diamond_dependency/` test fixture
- [ ] **4.1.4** Create `cross_file_types/` test fixture
- [ ] **4.1.5** Create `package_import/` test fixture
- [ ] **4.1.6** Create `cross_file_inheritance/` test fixture
- [ ] **4.1.7** Create `import_alias/` test fixture
- [ ] **4.1.8** Create `selective_import_error/` test fixture

### Verification

```bash
# Run all multi-file tests
dotnet test --filter "DisplayName~multi_file"

# Run specific test
dotnet test --filter "DisplayName~transitive_imports"
```

---

## Task 4.2: Add Programmatic Incremental Compilation Tests

**File:** `src/Sharpy.Compiler.Tests/Integration/IncrementalCompilationTests.cs`

### Background

Some scenarios require modifying files between builds, which file-based fixtures can't do. These need programmatic tests using `ProjectCompilationHelper`.

### Tests to Create

- [ ] **4.2.1** Create test file
  ```csharp
  // src/Sharpy.Compiler.Tests/Integration/IncrementalCompilationTests.cs
  namespace Sharpy.Compiler.Tests.Integration;

  public class IncrementalCompilationTests : IntegrationTestBase
  {
      public IncrementalCompilationTests(ITestOutputHelper output) : base(output) { }

      // Tests will be added here
  }
  ```

- [ ] **4.2.2** Test: Unchanged files are skipped
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

- [ ] **4.2.3** Test: Modified file is recompiled
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

- [ ] **4.2.4** Test: Dependent file recompiled when import changes
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

- [ ] **4.2.5** Test: Transitive dependency change triggers recompile
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

- [ ] **4.2.6** Test: Adding a new file works
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

- [ ] **4.2.7** Test: Removing an import errors correctly
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

- [ ] **4.2.8** Test: Clean build after cache clear
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

- [ ] All 8 file-based multi-file test fixtures created and passing
- [ ] All 8 programmatic incremental compilation tests passing
- [ ] `ProjectCompilationHelper` has necessary methods for test scenarios
- [ ] Tests run in both Debug and Release configurations
- [ ] No flaky tests (each test deterministic)
- [ ] Code review completed

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
