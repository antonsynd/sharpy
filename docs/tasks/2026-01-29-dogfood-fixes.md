# Dogfood Issues Fix Task List

**Created:** 2026-01-29  
**Priority:** P0-P3  
**Estimated Total Effort:** 1-2 days  

This task list addresses issues discovered during dogfood testing on 2026-01-29.
Each section is a separate commit. Complete tasks in order.

---

## Phase 1: Cross-Module Inheritance Resolution (P0 - Critical)

**Goal:** Fix the bug where imported types don't have their `BaseType` resolved, causing "has no member" errors for inherited methods.

**Files to modify:**
- `src/Sharpy.Compiler/Semantic/Symbol.cs`
- `src/Sharpy.Compiler/Semantic/ImportResolver.cs`
- `src/Sharpy.Compiler/Compiler.cs`
- `src/Sharpy.Compiler/Project/ProjectCompiler.cs`

---

### Task 1.1: Add UnresolvedBaseName to TypeSymbol

**File:** `src/Sharpy.Compiler/Semantic/Symbol.cs`

- [ ] Find the `TypeSymbol` class definition
- [ ] Add the following properties after the existing `BaseType` property:

```csharp
/// <summary>
/// Unresolved base class name from AST, used for deferred inheritance resolution
/// when types are imported from other modules. The actual BaseType is resolved
/// after all imports are registered in the symbol table.
/// </summary>
public string? UnresolvedBaseName { get; set; }

/// <summary>
/// Unresolved interface names from AST, used for deferred inheritance resolution.
/// </summary>
public List<string> UnresolvedInterfaceNames { get; set; } = new();
```

- [ ] Verify the file compiles: `dotnet build src/Sharpy.Compiler/`

**Commit:** `feat(semantic): add UnresolvedBaseName to TypeSymbol for deferred inheritance`

---

### Task 1.2: Store Unresolved Base Names During Import

**File:** `src/Sharpy.Compiler/Semantic/ImportResolver.cs`

- [ ] Find the `ExtractFullClassSymbol` method (search for `private TypeSymbol ExtractFullClassSymbol`)
- [ ] After the `TypeSymbol classSymbol = new TypeSymbol { ... }` block but before the field/method extraction loop, add:

```csharp
// Store unresolved base class names for deferred resolution
// The actual BaseType will be resolved after all types are registered
if (classDef.BaseClasses.Length > 0)
{
    foreach (var baseAnnot in classDef.BaseClasses)
    {
        // First non-interface becomes base class, rest are interfaces
        // We store all as unresolved - resolution pass will sort them out
        if (classSymbol.UnresolvedBaseName == null)
        {
            classSymbol.UnresolvedBaseName = baseAnnot.Name;
        }
        else
        {
            classSymbol.UnresolvedInterfaceNames.Add(baseAnnot.Name);
        }
    }
    _logger.LogDebug($"[ImportResolver] Stored unresolved base for {classDef.Name}: {classSymbol.UnresolvedBaseName}");
}
```

- [ ] Find the `ExtractFullStructSymbol` method
- [ ] After the struct symbol creation, add:

```csharp
// Store unresolved interface names for deferred resolution
foreach (var baseAnnot in structDef.BaseClasses)
{
    structSymbol.UnresolvedInterfaceNames.Add(baseAnnot.Name);
}
```

- [ ] Find the `ExtractFullInterfaceSymbol` method
- [ ] After the interface symbol creation, add:

```csharp
// Store unresolved base interface names for deferred resolution
foreach (var baseAnnot in interfaceDef.BaseInterfaces)
{
    interfaceSymbol.UnresolvedInterfaceNames.Add(baseAnnot.Name);
}
```

- [ ] Verify the file compiles: `dotnet build src/Sharpy.Compiler/`

**Commit:** `feat(import): store unresolved base class names during import extraction`

---

### Task 1.3: Add Inheritance Resolution Method to Compiler

**File:** `src/Sharpy.Compiler/Compiler.cs`

- [ ] Add the following private method to the `Compiler` class (add before the `ToPascalCase` method):

```csharp
/// <summary>
/// Resolve inheritance relationships for imported types.
/// This is called after all imports are registered but before type checking.
/// Imported types have their base class/interface names stored as strings;
/// this method resolves them to actual TypeSymbol references.
/// </summary>
private void ResolveImportedTypeInheritance(SymbolTable symbolTable)
{
    _logger.LogDebug("Resolving inheritance for imported types...");
    
    // Get all type symbols from global scope
    var allTypes = symbolTable.GlobalScope.GetAllSymbols()
        .OfType<TypeSymbol>()
        .ToList();
    
    foreach (var type in allTypes)
    {
        // Resolve base class
        if (type.BaseType == null && !string.IsNullOrEmpty(type.UnresolvedBaseName))
        {
            var baseType = symbolTable.LookupType(type.UnresolvedBaseName);
            if (baseType != null)
            {
                if (baseType.TypeKind == TypeKind.Interface)
                {
                    // It's actually an interface, not a base class
                    if (!type.Interfaces.Contains(baseType))
                    {
                        type.Interfaces.Add(baseType);
                    }
                }
                else
                {
                    type.BaseType = baseType;
                }
                _logger.LogDebug($"Resolved inheritance: {type.Name} : {baseType.Name}");
            }
            else
            {
                _logger.LogWarning($"Could not resolve base type '{type.UnresolvedBaseName}' for {type.Name}", 0, 0);
            }
        }
        
        // Resolve interfaces
        foreach (var ifaceName in type.UnresolvedInterfaceNames)
        {
            var ifaceType = symbolTable.LookupType(ifaceName);
            if (ifaceType != null && !type.Interfaces.Contains(ifaceType))
            {
                type.Interfaces.Add(ifaceType);
                _logger.LogDebug($"Resolved interface: {type.Name} : {ifaceType.Name}");
            }
            else if (ifaceType == null)
            {
                _logger.LogWarning($"Could not resolve interface '{ifaceName}' for {type.Name}", 0, 0);
            }
        }
    }
}
```

- [ ] Verify the file compiles: `dotnet build src/Sharpy.Compiler/`

**Commit:** `feat(compiler): add ResolveImportedTypeInheritance method`

---

### Task 1.4: Call Inheritance Resolution After Import Registration

**File:** `src/Sharpy.Compiler/Compiler.cs`

- [ ] Find the `Compile` method
- [ ] Locate the line `metrics.EndPhase();` that ends the "Import Resolution" phase (after the foreach loop processing imports)
- [ ] Add the following call BEFORE that `metrics.EndPhase();`:

```csharp
// Resolve inheritance for imported types now that all symbols are registered
ResolveImportedTypeInheritance(symbolTable);
```

- [ ] Verify the file compiles: `dotnet build src/Sharpy.Compiler/`

**Commit:** `feat(compiler): call inheritance resolution after import registration`

---

### Task 1.5: Add GetAllSymbols to Scope

**File:** `src/Sharpy.Compiler/Semantic/Scope.cs`

- [ ] Check if `GetAllSymbols()` method exists in `Scope` class
- [ ] If it doesn't exist, add it:

```csharp
/// <summary>
/// Get all symbols defined in this scope (does not include parent scopes).
/// </summary>
public IEnumerable<Symbol> GetAllSymbols()
{
    return _symbols.Values;
}
```

- [ ] Verify the file compiles: `dotnet build src/Sharpy.Compiler/`

**Commit:** `feat(scope): add GetAllSymbols method`

---

### Task 1.6: Apply Same Fix to ProjectCompiler

**File:** `src/Sharpy.Compiler/Project/ProjectCompiler.cs`

- [ ] Search for where imports are processed in `ProjectCompiler`
- [ ] Add the same `ResolveImportedTypeInheritance` call after imports are registered
- [ ] If the method isn't accessible, either:
  - Make the method `internal` instead of `private` in `Compiler.cs`, OR
  - Copy the method to `ProjectCompiler.cs`

- [ ] Verify the file compiles: `dotnet build src/Sharpy.Compiler/`

**Commit:** `feat(project): apply inheritance resolution fix to ProjectCompiler`

---

### Task 1.7: Write Integration Test

**File:** `tests/Sharpy.Compiler.Tests/Integration/CrossModuleInheritanceTests.cs` (create new)

- [ ] Create the test file with the following content:

```csharp
using Xunit;
using Sharpy.Compiler;
using Sharpy.Compiler.Logging;

namespace Sharpy.Compiler.Tests.Integration;

public class CrossModuleInheritanceTests : IDisposable
{
    private readonly string _tempDir;
    
    public CrossModuleInheritanceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"sharpy_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }
    
    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }
    
    [Fact]
    public void ImportedClass_CanAccessInheritedMethods()
    {
        // Arrange: Create base module
        var shapeSpy = @"
class Shape:
    name: str
    
    def __init__(self, name: str):
        self.name = name
    
    @virtual
    def describe(self) -> str:
        return f""Shape: {self.name}""
";
        File.WriteAllText(Path.Combine(_tempDir, "shape.spy"), shapeSpy);
        
        // Arrange: Create derived module
        var rectangleSpy = @"
from shape import Shape

class Rectangle(Shape):
    width: float
    height: float
    
    def __init__(self, w: float, h: float):
        super().__init__(""Rectangle"")
        self.width = w
        self.height = h
";
        File.WriteAllText(Path.Combine(_tempDir, "rectangle.spy"), rectangleSpy);
        
        // Arrange: Create main module
        var mainSpy = @"
from rectangle import Rectangle

def main():
    r: Rectangle = Rectangle(5.0, 3.0)
    print(r.describe())  # Should access inherited method
";
        var mainPath = Path.Combine(_tempDir, "main.spy");
        File.WriteAllText(mainPath, mainSpy);
        
        // Act
        var options = new CompilerOptions { ModulePaths = new[] { _tempDir } };
        var compiler = new Compiler(options, new NullLogger());
        var result = compiler.Compile(File.ReadAllText(mainPath), mainPath);
        
        // Assert
        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.Errors)}");
    }
    
    [Fact]
    public void ImportedClass_CanBePassedToBaseTypeParameter()
    {
        // Arrange: Create shape module
        var shapeSpy = @"
class Shape:
    pass

def process_shape(s: Shape) -> str:
    return ""processed""
";
        File.WriteAllText(Path.Combine(_tempDir, "shape.spy"), shapeSpy);
        
        // Arrange: Create circle module
        var circleSpy = @"
from shape import Shape

class Circle(Shape):
    radius: float
    
    def __init__(self, r: float):
        self.radius = r
";
        File.WriteAllText(Path.Combine(_tempDir, "circle.spy"), circleSpy);
        
        // Arrange: Create main module
        var mainSpy = @"
from shape import process_shape
from circle import Circle

def main():
    c: Circle = Circle(5.0)
    result: str = process_shape(c)  # Circle should be assignable to Shape
    print(result)
";
        var mainPath = Path.Combine(_tempDir, "main.spy");
        File.WriteAllText(mainPath, mainSpy);
        
        // Act
        var options = new CompilerOptions { ModulePaths = new[] { _tempDir } };
        var compiler = new Compiler(options, new NullLogger());
        var result = compiler.Compile(File.ReadAllText(mainPath), mainPath);
        
        // Assert
        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.Errors)}");
    }
}
```

- [ ] Run the tests: `dotnet test tests/Sharpy.Compiler.Tests/ --filter "CrossModuleInheritanceTests"`
- [ ] Verify both tests pass

**Commit:** `test: add cross-module inheritance integration tests`

---

## Phase 2: Fix Pre-Validator False Positive (P2)

**Goal:** Fix the dogfood pre-validator incorrectly flagging keyword arguments as "tuple unpacking".

**Files to modify:**
- `tools/dogfood/validator.py` (or wherever the pre-validator lives)

---

### Task 2.1: Locate Pre-Validator Logic

- [ ] Find the dogfood validator file (likely in `tools/dogfood/` or `scripts/`)
- [ ] Search for "tuple unpacking" or the pattern that triggers the skip
- [ ] Document the file path here: `_______________________`

---

### Task 2.2: Fix the Tuple Unpacking Detection

- [ ] Find the regex or heuristic that detects tuple unpacking
- [ ] Modify it to NOT match keyword argument patterns like `func(name=value)`
- [ ] The pattern should match: `a, b = func()` or `x, y, z = something`
- [ ] The pattern should NOT match: `func(arg=value)` or `x = func(a=1, b=2)`

Example fix (adapt to actual code):
```python
# Before (too broad):
if re.search(r'=.*,.*=', line):
    return "tuple unpacking"

# After (more precise):
# Only match tuple unpacking on the LEFT side of assignment
if re.search(r'^[^=]*,\s*\w+\s*=\s*', line):
    return "tuple unpacking"
```

- [ ] Test with these cases:
  - `a, b = func()` → SHOULD be flagged
  - `score = calculate(base=50, bonus=20)` → should NOT be flagged
  - `x: int = func(a=1)` → should NOT be flagged

**Commit:** `fix(dogfood): don't flag keyword arguments as tuple unpacking`

---

## Phase 3: Add Python Syntax Pre-Check (P2)

**Goal:** Add a Python syntax check before running generated code to catch syntax errors early.

**Files to modify:**
- `tools/dogfood/runner.py` (or equivalent)

---

### Task 3.1: Add Python Syntax Validation Function

- [ ] Find the dogfood runner file
- [ ] Add a function to validate Python/Sharpy syntax:

```python
import ast
import subprocess
import sys

def validate_syntax(code: str, filepath: str) -> tuple[bool, str | None]:
    """
    Validate that code has valid Python syntax.
    Returns (is_valid, error_message).
    """
    try:
        # Try Python's AST parser first (fast)
        ast.parse(code)
        return True, None
    except SyntaxError as e:
        return False, f"Syntax error at line {e.lineno}: {e.msg}"
```

**Commit:** `feat(dogfood): add Python syntax validation function`

---

### Task 3.2: Integrate Syntax Check Into Pipeline

- [ ] Find where generated code is saved/executed
- [ ] Call `validate_syntax()` before attempting compilation
- [ ] If syntax is invalid, either:
  - Retry generation with feedback about the error, OR
  - Skip with a clear "syntax error" reason (not "tuple unpacking")

```python
# Before running Sharpy compiler:
is_valid, error = validate_syntax(generated_code, filepath)
if not is_valid:
    # Option A: Retry with feedback
    return retry_generation(prompt, error_feedback=error)
    
    # Option B: Skip with accurate reason
    return skip_iteration(reason=f"Generated code has syntax error: {error}")
```

- [ ] Test by manually introducing a syntax error and verifying it's caught

**Commit:** `feat(dogfood): integrate syntax validation into pipeline`

---

## Phase 4: Update Dogfood Generator Prompts (P2)

**Goal:** Fix the AI generator to produce valid Sharpy code by updating prompts.

**Files to modify:**
- `tools/dogfood/prompts/` or wherever prompts are stored

---

### Task 4.1: Add @virtual Requirement to Prompts

- [ ] Find the prompt template(s) for generating Sharpy code
- [ ] Add explicit instruction about `@virtual`:

```
IMPORTANT: In Sharpy, methods that will be overridden in subclasses MUST be marked with @virtual in the base class. Unlike Python, this is required.

Example:
```python
class Animal:
    @virtual
    def speak(self) -> str:
        return "..."

class Dog(Animal):
    @override
    def speak(self) -> str:
        return "Woof!"
```
```

- [ ] Add to any examples that show inheritance

**Commit:** `docs(dogfood): add @virtual requirement to generation prompts`

---

### Task 4.2: Add Function Type Annotation Requirement

- [ ] Find prompts that might generate higher-order functions
- [ ] Add explicit instruction:

```
IMPORTANT: Sharpy requires explicit type annotations for function parameters, including callable/function parameters.

WRONG:
def apply(func):  # Missing type!
    return func(42)

CORRECT:
def apply(func: (int) -> int) -> int:
    return func(42)

Function type syntax: (ParamType1, ParamType2) -> ReturnType
- () -> int           # No params, returns int
- (int) -> str        # One int param, returns str
- (int, str) -> bool  # Two params, returns bool
- (int) -> None       # Returns nothing
```

**Commit:** `docs(dogfood): add function type annotation requirement to prompts`

---

### Task 4.3: Add Examples of Common Patterns

- [ ] Create or update an examples section in prompts with correct Sharpy patterns:

```python
# Higher-order function with proper types
def filter_list(items: list[int], predicate: (int) -> bool) -> list[int]:
    return [x for x in items if predicate(x)]

# Class with virtual method
class Shape:
    @virtual
    def area(self) -> float:
        return 0.0

class Circle(Shape):
    radius: float
    
    def __init__(self, r: float):
        self.radius = r
    
    @override
    def area(self) -> float:
        return 3.14159 * self.radius ** 2

# Optional types
def find_first(items: list[int]) -> int?:
    if len(items) > 0:
        return Some(items[0])
    return Nothing
```

**Commit:** `docs(dogfood): add correct Sharpy pattern examples to prompts`

---

## Phase 5: Verification

### Task 5.1: Run Full Test Suite

- [ ] Run all compiler tests: `dotnet test`
- [ ] Verify no regressions

**Commit:** None (verification only)

---

### Task 5.2: Re-run Dogfood

- [ ] Run the dogfood suite again
- [ ] Compare results to the 2026-01-29 baseline (50% success)
- [ ] Document new success rate: `_______% (was 50%)`
- [ ] If any new failures, create follow-up tasks

**Commit:** None (verification only)

---

## Cleanup

- [ ] Delete the temporary sharpydocs directory if it was created:
  ```bash
  rm -rf /Users/anton/Documents/github/sharpydocs
  ```

---

## Summary Checklist

| Phase | Task | Status |
|-------|------|--------|
| 1.1 | Add UnresolvedBaseName to TypeSymbol | ☐ |
| 1.2 | Store unresolved names in ImportResolver | ☐ |
| 1.3 | Add ResolveImportedTypeInheritance method | ☐ |
| 1.4 | Call inheritance resolution in Compiler | ☐ |
| 1.5 | Add GetAllSymbols to Scope | ☐ |
| 1.6 | Apply fix to ProjectCompiler | ☐ |
| 1.7 | Write integration tests | ☐ |
| 2.1 | Locate pre-validator | ☐ |
| 2.2 | Fix tuple unpacking detection | ☐ |
| 3.1 | Add syntax validation function | ☐ |
| 3.2 | Integrate syntax check | ☐ |
| 4.1 | Add @virtual to prompts | ☐ |
| 4.2 | Add function type requirements | ☐ |
| 4.3 | Add pattern examples | ☐ |
| 5.1 | Run full test suite | ☐ |
| 5.2 | Re-run dogfood | ☐ |
