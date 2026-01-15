# Phase 0.1.10: Module System - Comprehensive Task List (v2)

**Goal:** Import/export system, multi-file compilation, namespace generation, and package support.

**Prerequisites:** Phase 0.1.9 (Type System Enhancements) must be complete.

**Exit Criteria:**
- `import module` works
- `import module as alias` works
- `from module import name` works
- `from module import *` works (with warning)
- Private symbols (`_name`) not exported by `import *`
- Private symbols (`__name`) cannot be imported at all
- Multi-file compilation produces single assembly
- Namespaces generated from file paths
- `__init__.spy` marks packages and defines re-exports
- Circular import detection with clear error messages
- Import statements must be at top of file
- Symbol shadowing/collision detection

---

## Pre-Implementation Checklist

Before starting any task, check what already exists:

```bash
# Check for import-related code
grep -rn "import\|Import\|module" src/Sharpy.Compiler/

# Check for namespace handling
grep -rn "namespace\|Namespace" src/Sharpy.Compiler/

# Check for multi-file compilation
grep -rn "CompileProject\|MultiFile" src/Sharpy.Compiler/

# Check existing tests
find src -name "*.cs" -exec grep -l "Import\|Module\|Namespace" {} \;
```

---

## Task 0.1.10.1: Implement Import Statement Parsing

**Type:** 🆕 New Implementation  
**Priority:** Critical  
**Estimated Time:** 2-3 hours

📁 **Files**: `src/Sharpy.Compiler/Parser/Parser.cs`, `src/Sharpy.Compiler/Parser/Ast/Statement.cs`

### Objective
Parse all import statement forms.

### Import Syntax Forms

```python
import utils.helpers                    # Full module import
import utils.helpers as h               # Aliased module import
from utils.helpers import format_text   # Specific symbol import
from utils.helpers import func1, func2  # Multiple symbol import
from utils.helpers import *             # Wildcard import
```

### Grammar
```ebnf
import_stmt ::= 'import' module_path [ 'as' identifier ]
              | 'from' module_path 'import' import_targets

module_path ::= identifier { '.' identifier }
import_targets ::= '*'
                 | identifier { ',' identifier } [ ',' ]
```

### Actions

1. **Create Import AST nodes:**
   ```csharp
   public record ImportStatement : Statement
   {
       public List<string> ModulePath { get; init; } = new();  // e.g., ["utils", "helpers"]
       public string? Alias { get; init; }  // For "as alias"
   }

   public record FromImport : Statement
   {
       public List<string> ModulePath { get; init; } = new();
       public List<string> Names { get; init; } = new();  // Empty for "*"
       public bool IsWildcard { get; init; }
   }
   ```

2. **⚠️ Validate import position:**
   - All imports must be at the beginning of the file
   - Imports cannot appear after other statements
   
   ```python
   def func():
       pass

   import utils  # ERROR: Imports must be at top of file
   ```
   
   - Error message: "Import statements must appear at the beginning of the file, before any other statements"

### Scope Decision

- **Phase 0.1.10:** Absolute imports only
- **Deferred:** Relative imports (`.`, `..`)

---

## Task 0.1.10.2: Implement Module Resolution

**Type:** 🆕 New Implementation  
**Priority:** Critical  
**Estimated Time:** 3-4 hours

📁 **Files**: `src/Sharpy.Compiler/Semantic/ModuleResolver.cs`

### Objective
Resolve module paths to actual source files.

### Resolution Algorithm

1. **Module path to file path:**
   - `utils.helpers` → `utils/helpers.spy`
   - `mypackage.submodule` → `mypackage/submodule.spy`

2. **Search paths:**
   - Current project directory
   - Standard library paths (future)
   - External package paths (future)

3. **Handle packages:**
   - If `mypackage/` exists AND `mypackage/__init__.spy` exists → package
   - If `mypackage.spy` exists → module file

### Implementation Hints

```csharp
public class ModuleResolver
{
    private readonly List<string> _searchPaths;
    
    public string? ResolveModule(List<string> modulePath)
    {
        var relativePath = string.Join(Path.DirectorySeparatorChar.ToString(), modulePath) + ".spy";
        
        foreach (var searchPath in _searchPaths)
        {
            var fullPath = Path.Combine(searchPath, relativePath);
            if (File.Exists(fullPath))
                return fullPath;
            
            // Check for package
            var packagePath = Path.Combine(searchPath, 
                string.Join(Path.DirectorySeparatorChar.ToString(), modulePath),
                "__init__.spy");
            if (File.Exists(packagePath))
                return packagePath;
        }
        
        return null;
    }
}
```

---

## Task 0.1.10.3: Implement Import Symbol Resolution

**Type:** 🆕 New Implementation  
**Priority:** Critical  
**Estimated Time:** 3-4 hours

📁 **Files**: `src/Sharpy.Compiler/Semantic/SemanticAnalyzer.cs`, `src/Sharpy.Compiler/Semantic/SymbolTable.cs`

### Objective
Resolve imported symbols and enforce visibility rules.

### ⚠️ Export Rules

| Symbol Pattern | Exported by `import *` | Directly Importable |
|---------------|------------------------|---------------------|
| `public_func` | ✅ Yes | ✅ Yes |
| `_protected_func` | ❌ No | ✅ Yes |
| `__private_func` | ❌ No | ❌ No |

### Actions

1. **`import module`:**
   - Add module name to symbol table
   - Access via `module.symbol`

2. **`import module as alias`:**
   - Add alias to symbol table
   - Access via `alias.symbol`

3. **`from module import name`:**
   - Add `name` directly to current scope
   - Validate `name` exists in module
   - Validate `name` is not private (`__name`)

4. **`from module import *`:**
   - Import all public symbols (not starting with `_`)
   - ⚠️ Emit warning: "Wildcard import 'from utils import *' may pollute namespace. Consider importing specific symbols."

5. **⚠️ Validate private import:**
   ```python
   from utils import __private_func  # ERROR: Cannot import private symbol
   ```
   - Error message: "Cannot import private symbol '__private_func' from module 'utils'"

### Symbol Shadowing Detection

1. **Local shadows import → Error:**
   ```python
   from utils import helper
   
   def helper():  # ERROR: Shadows imported 'helper'
       pass
   ```

2. **Later import shadows earlier → Error:**
   ```python
   from module_a import func
   from module_b import func  # ERROR: 'func' already imported
   ```

3. **Import shadows builtin → Warning:**
   ```python
   from mymodule import print  # WARNING: Shadows builtin 'print'
   ```

---

## Task 0.1.10.4: Implement Multi-File Compilation

**Type:** 🆕 New Implementation  
**Priority:** Critical  
**Estimated Time:** 4-5 hours

📁 **Files**: `src/Sharpy.Compiler/Compiler.cs`, `src/Sharpy.Compiler/Project/ProjectCompiler.cs`

### Objective
Compile multiple source files into a single assembly.

### Compilation Pipeline

```
1. Discover all .spy files in project
       ↓
2. Parse all files (AST generation)
       ↓
3. Phase 1: Collect type declarations (classes, structs, enums, interfaces)
       ↓
4. Phase 2: Resolve imports and build symbol tables
       ↓
5. Phase 3: Type check all files
       ↓
6. Phase 4: Generate C# for all files
       ↓
7. Compile generated C# to assembly
```

### Actions

1. **Discover source files:**
   - Find all `.spy` files in project directory
   - Respect `.spyproj` configuration if present

2. **Parse all files:**
   - Create AST for each file
   - Track file paths for error reporting

3. **Build unified symbol table:**
   - Register all types from all files
   - Handle cross-file references

4. **Generate namespaces:**
   - File path determines namespace (see Task 0.1.10.5)

---

## Task 0.1.10.5: Implement Namespace Generation from Module Path

**Type:** 🆕 New Implementation  
**Priority:** High  
**Estimated Time:** 2-3 hours

📁 **Files**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

### Objective
Generate C# namespaces from file paths.

### ⚠️ Detailed Algorithm

1. **Project name** from `.spyproj` becomes root namespace
2. **Directory path** segments become namespace segments
3. **File name** (without `.spy`) becomes final namespace segment OR class container
4. **Apply PascalCase** transformation to each segment

### Example

```
Project: MyApp.spyproj (defines RootNamespace = "MyApp")
File: src/models/user.spy

Possible namespace structures:
Option A: MyApp.Src.Models.User
Option B: MyApp.Models (if src/ is excluded)
          - Classes inside file in this namespace
```

### Recommended Approach

```python
# File: myproject/utils/helpers.spy

def format_text(s: str) -> str:
    return s.upper()

class TextProcessor:
    pass
```

**Generated C#:**
```csharp
namespace MyProject.Utils  // Or MyProject.Utils.Helpers
{
    public static class Helpers  // Module-level container for functions
    {
        public static string FormatText(string s) => s.ToUpper();
    }
    
    public class TextProcessor { }
}
```

### Key Decisions

1. **Module-level functions** → Static class with file name
2. **Classes/structs/enums** → Directly in namespace
3. **`src/` directory** → Typically excluded from namespace (configurable)

---

## Task 0.1.10.6: Implement Circular Import Detection

**Type:** 🆕 New Implementation  
**Priority:** High  
**Estimated Time:** 3-4 hours

📁 **Files**: `src/Sharpy.Compiler/Semantic/ImportResolver.cs`

### Objective
Detect and report circular imports with clear error messages.

### Two-Phase Resolution Algorithm

**Phase 1: Type Collection**
- Parse all files
- Collect type declarations (classes, structs, enums, interfaces)
- Do NOT resolve type bodies yet
- This allows forward references

**Phase 2: Body Resolution**
- Resolve imports and type bodies
- Check for circular dependencies that require resolution

### Circular Import Categories

| Category | Example | Allowed |
|----------|---------|---------|
| Type annotation only | `field: OtherClass` | ✅ Yes |
| Default value | `field: OtherClass = OtherClass()` | ⚠️ Order dependent |
| Inheritance | `class A(B)` where B imports A | ❌ No |
| Constructor body | `super().__init__()` | ✅ Yes (runtime) |

### Detection Algorithm

```python
# module_a.spy
from module_b import ClassB
class ClassA(ClassB):  # Needs ClassB fully resolved
    pass

# module_b.spy
from module_a import ClassA
class ClassB(ClassA):  # Needs ClassA fully resolved → CYCLE
    pass
```

**Error message:** "Circular inheritance detected: ClassA inherits from ClassB, which inherits from ClassA"

### Implementation Hints

```csharp
public class ImportGraph
{
    private Dictionary<string, HashSet<string>> _dependencies = new();
    private HashSet<string> _visiting = new();
    private HashSet<string> _visited = new();
    
    public void AddDependency(string from, string to)
    {
        if (!_dependencies.ContainsKey(from))
            _dependencies[from] = new HashSet<string>();
        _dependencies[from].Add(to);
    }
    
    public List<string>? FindCycle()
    {
        foreach (var module in _dependencies.Keys)
        {
            var cycle = DFS(module, new List<string>());
            if (cycle != null)
                return cycle;
        }
        return null;
    }
    
    private List<string>? DFS(string module, List<string> path)
    {
        if (_visiting.Contains(module))
        {
            // Found cycle
            var cycleStart = path.IndexOf(module);
            return path.Skip(cycleStart).Append(module).ToList();
        }
        
        if (_visited.Contains(module))
            return null;
        
        _visiting.Add(module);
        path.Add(module);
        
        if (_dependencies.TryGetValue(module, out var deps))
        {
            foreach (var dep in deps)
            {
                var cycle = DFS(dep, path);
                if (cycle != null)
                    return cycle;
            }
        }
        
        path.RemoveAt(path.Count - 1);
        _visiting.Remove(module);
        _visited.Add(module);
        
        return null;
    }
}
```

---

## Task 0.1.10.7: Implement Import Code Generation

**Type:** 🆕 New Implementation  
**Priority:** High  
**Estimated Time:** 2-3 hours

📁 **Files**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

### Objective
Generate C# `using` statements from Sharpy imports.

### Import Mapping

1. **`import module`:**
   ```python
   import utils.helpers
   # Usage: utils.helpers.function()
   ```
   
   **C# Option A:** Fully qualified calls (no using)
   ```csharp
   MyProject.Utils.Helpers.Function();
   ```
   
   **C# Option B:** Using alias
   ```csharp
   using utils_helpers = MyProject.Utils.Helpers;
   // Usage: utils_helpers.Function();
   ```

2. **`import module as alias`:**
   ```python
   import utils.helpers as h
   # Usage: h.function()
   ```
   
   **C#:**
   ```csharp
   using h = MyProject.Utils.Helpers;
   // Usage: h.Function();
   ```

3. **`from module import name`:**
   ```python
   from utils.helpers import format_text
   # Usage: format_text()
   ```
   
   **C#:**
   ```csharp
   using static MyProject.Utils.Helpers;
   // Usage: FormatText();
   ```

4. **`from module import *`:**
   ```python
   from utils.helpers import *
   ```
   
   **C#:**
   ```csharp
   using static MyProject.Utils.Helpers;
   ```

### Decision (Per Axiom 1 - .NET Runtime)

- Use `using static` for `from ... import` of functions
- Use `using namespace` for class imports
- Use `using alias = ...` for aliased imports

---

## Task 0.1.10.8: Implement `__init__.spy` Support

**Type:** 🆕 New Implementation  
**Priority:** High  
**Estimated Time:** 3-4 hours

📁 **Files**: `src/Sharpy.Compiler/Semantic/PackageResolver.cs`

### Objective
Support package initialization and re-exports.

### `__init__.spy` Purpose

1. **Marks directory as a package**
2. **Defines package-level exports (re-exports)**
3. **Executes when package is imported**

### Example Structure

```
mypackage/
├── __init__.spy      # Package initialization
├── module_a.spy
└── module_b.spy
```

### `__init__.spy` Content

```python
# mypackage/__init__.spy
from mypackage.module_a import ClassA, func_a
from mypackage.module_b import ClassB

# Now these are available at package level:
# from mypackage import ClassA, ClassB, func_a
```

### Behavior

1. **When `import mypackage`:**
   - Load and execute `__init__.spy`
   - Symbols re-exported become package-level symbols

2. **Direct submodule access bypasses `__init__.spy`:**
   - `from mypackage.module_a import ...` works directly

3. **Module initialization order:**
   - Dependencies loaded first
   - Module-level statements execute in file order
   - `__init__.spy` executes when package imported

---

## Task 0.1.10.9: Implement Project File Support (`.spyproj`)

**Type:** 🆕 New Implementation  
**Priority:** Medium  
**Estimated Time:** 2-3 hours

📁 **Files**: `src/Sharpy.Compiler/Project/SpyProject.cs`

### Objective
Parse and use project configuration files.

### `.spyproj` Format

```xml
<Project>
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net6.0</TargetFramework>
    <RootNamespace>MyProject</RootNamespace>
    <EntryPoint>main.spy</EntryPoint>
  </PropertyGroup>
  <ItemGroup>
    <SourceFile Include="**/*.spy" />
    <Exclude Include="tests/**" />
  </ItemGroup>
</Project>
```

### Properties

- `OutputType` — `Exe` or `Library`
- `TargetFramework` — .NET framework version
- `RootNamespace` — Root namespace for all types
- `EntryPoint` — Main entry point file

### Scope Decision

- **Phase 0.1.10:** Basic properties (OutputType, RootNamespace, EntryPoint)
- **Deferred:** External assembly references (System imports → Phase 0.1.12)
- **Deferred:** Project references

---

## Task 0.1.10.10: Create Test Infrastructure for Multi-File Compilation

**Type:** 🆕 New Implementation  
**Priority:** High  
**Estimated Time:** 2-3 hours

📁 **Files**: `src/Sharpy.Compiler.Tests/TestHelpers/ProjectCompilationHelper.cs`

### Objective
Create test infrastructure for multi-file compilation tests.

### Required Test Helpers

```csharp
public class ProjectCompilationHelper
{
    private readonly string _tempDir;
    
    public ProjectCompilationHelper()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
    }
    
    public void WriteFile(string relativePath, string content)
    {
        var fullPath = Path.Combine(_tempDir, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
    }
    
    public CompileResult CompileProject(string entryPoint)
    {
        // 1. Find all .spy files in directory
        // 2. Parse all files
        // 3. Resolve imports across files
        // 4. Generate C# for all files
        // 5. Compile C# assembly
        return result;
    }
    
    public CompileResult CompileProject(string entryPoint, string[] sourceFiles)
    {
        // Explicit file list compilation
    }
    
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }
}
```

---

## Task 0.1.10.11: Create Phase 0.1.10 Integration Tests

**Type:** 🆕 New Implementation  
**Priority:** Critical  
**Estimated Time:** 4-5 hours

📁 **Files**: `src/Sharpy.Compiler.Tests/Integration/Phase0110IntegrationTests.cs`

### Test Cases

```csharp
[Fact]
public void BasicImport_Works()
{
    _helper.WriteFile("utils.spy", @"
def helper() -> str:
    return 'help'
");
    _helper.WriteFile("main.spy", @"
import utils

result = utils.helper()
");
    var result = _helper.CompileProject("main.spy");
    Assert.True(result.Success);
}

[Fact]
public void ImportAsAlias_Works()
{
    _helper.WriteFile("utils/helpers.spy", @"
def format(s: str) -> str:
    return s.upper()
");
    _helper.WriteFile("main.spy", @"
import utils.helpers as h

result = h.format('hello')
");
    var result = _helper.CompileProject("main.spy");
    Assert.True(result.Success);
}

[Fact]
public void FromImport_Works()
{
    _helper.WriteFile("math_utils.spy", @"
def square(x: int) -> int:
    return x * x

def cube(x: int) -> int:
    return x * x * x
");
    _helper.WriteFile("main.spy", @"
from math_utils import square, cube

a = square(5)
b = cube(3)
");
    var result = _helper.CompileProject("main.spy");
    Assert.True(result.Success);
}

[Fact]
public void WildcardImport_Works()
{
    _helper.WriteFile("utils.spy", @"
def public_func() -> str:
    return 'public'

def _protected_func() -> str:
    return 'protected'

def __private_func() -> str:
    return 'private'
");
    _helper.WriteFile("main.spy", @"
from utils import *

result = public_func()
");
    var result = _helper.CompileProject("main.spy");
    Assert.True(result.Success);
    Assert.Contains("warning", result.Warnings.ToLower());  // Should warn about wildcard
}

[Fact]
public void WildcardImport_ExcludesProtected()
{
    _helper.WriteFile("utils.spy", @"
def public_func() -> str:
    return 'public'

def _protected_func() -> str:
    return 'protected'
");
    _helper.WriteFile("main.spy", @"
from utils import *

result = _protected_func()
");
    var result = _helper.CompileProject("main.spy");
    Assert.False(result.Success);  // _protected_func not imported by *
}

[Fact]
public void PrivateSymbol_CannotBeImported()
{
    _helper.WriteFile("utils.spy", @"
def __private() -> str:
    return 'private'
");
    _helper.WriteFile("main.spy", @"
from utils import __private
");
    var result = _helper.CompileProject("main.spy");
    Assert.False(result.Success);
    Assert.Contains("private", result.Error.ToLower());
}

[Fact]
public void CircularImport_ProducesError()
{
    _helper.WriteFile("module_a.spy", @"
from module_b import ClassB

class ClassA(ClassB):
    pass
");
    _helper.WriteFile("module_b.spy", @"
from module_a import ClassA

class ClassB(ClassA):
    pass
");
    var result = _helper.CompileProject("module_a.spy");
    Assert.False(result.Success);
    Assert.Contains("circular", result.Error.ToLower());
}

[Fact]
public void ImportNotAtTop_ProducesError()
{
    _helper.WriteFile("main.spy", @"
def func() -> int:
    return 42

import utils
");
    var result = _helper.CompileProject("main.spy");
    Assert.False(result.Success);
    Assert.Contains("top", result.Error.ToLower());
}

[Fact]
public void SymbolShadowing_Import_ProducesError()
{
    _helper.WriteFile("utils.spy", @"
def helper() -> str:
    return 'help'
");
    _helper.WriteFile("main.spy", @"
from utils import helper

def helper() -> str:
    return 'local'
");
    var result = _helper.CompileProject("main.spy");
    Assert.False(result.Success);
    Assert.Contains("shadow", result.Error.ToLower());
}

[Fact]
public void SymbolShadowing_DoubleImport_ProducesError()
{
    _helper.WriteFile("module_a.spy", @"
def func() -> str:
    return 'a'
");
    _helper.WriteFile("module_b.spy", @"
def func() -> str:
    return 'b'
");
    _helper.WriteFile("main.spy", @"
from module_a import func
from module_b import func
");
    var result = _helper.CompileProject("main.spy");
    Assert.False(result.Success);
    Assert.Contains("already imported", result.Error.ToLower());
}

[Fact]
public void PackageWithInit_Works()
{
    _helper.WriteFile("mypackage/__init__.spy", @"
from mypackage.core import Helper
");
    _helper.WriteFile("mypackage/core.spy", @"
class Helper:
    def help(self) -> str:
        return 'help'
");
    _helper.WriteFile("main.spy", @"
from mypackage import Helper

h = Helper()
");
    var result = _helper.CompileProject("main.spy");
    Assert.True(result.Success);
}

[Fact]
public void NamespaceGeneration_CorrectPascalCase()
{
    _helper.WriteFile("utils/string_helpers.spy", @"
def to_upper(s: str) -> str:
    return s.upper()
");
    _helper.WriteFile("main.spy", @"
from utils.string_helpers import to_upper

result = to_upper('hello')
");
    var result = _helper.CompileProject("main.spy");
    Assert.True(result.Success);
    // Generated namespace should be Utils.StringHelpers or similar
}

[Fact]
public void MultipleFiles_CrossReference_Works()
{
    _helper.WriteFile("models.spy", @"
class User:
    name: str
    
    def __init__(self, name: str):
        self.name = name
");
    _helper.WriteFile("services.spy", @"
from models import User

def create_user(name: str) -> User:
    return User(name)
");
    _helper.WriteFile("main.spy", @"
from services import create_user

user = create_user('Alice')
");
    var result = _helper.CompileProject("main.spy");
    Assert.True(result.Success);
}
```

---

## Task 0.1.10.12: Document Phase 0.1.10 Exit Criteria Verification

**Type:** 📝 Documentation  
**Priority:** High  
**Estimated Time:** 30 minutes

📁 **Files**: `docs/implementation/phase_0_1_10_complete.md`

### Exit Criteria Checklist

| Criterion | Test | Status |
|-----------|------|--------|
| Basic import | `BasicImport_Works` | [ ] |
| Import as alias | `ImportAsAlias_Works` | [ ] |
| From import | `FromImport_Works` | [ ] |
| Wildcard import | `WildcardImport_Works` | [ ] |
| Protected exclusion | `WildcardImport_ExcludesProtected` | [ ] |
| Private import blocked | `PrivateSymbol_CannotBeImported` | [ ] |
| Circular import detection | `CircularImport_ProducesError` | [ ] |
| Import position validation | `ImportNotAtTop_ProducesError` | [ ] |
| Local shadows import | `SymbolShadowing_Import_ProducesError` | [ ] |
| Double import collision | `SymbolShadowing_DoubleImport_ProducesError` | [ ] |
| Package with __init__ | `PackageWithInit_Works` | [ ] |
| Namespace generation | `NamespaceGeneration_CorrectPascalCase` | [ ] |
| Multi-file cross-reference | `MultipleFiles_CrossReference_Works` | [ ] |

---

## Summary: Task Dependencies

```
0.1.10.1 (Import Parsing) ──────────────────────────────┐
                                                        │
0.1.10.2 (Module Resolution) ───────────────────────────┼──► 0.1.10.3 (Symbol Resolution)
                                                        │    0.1.10.4 (Multi-File Compilation)
                                                        │           │
                                                        │           ▼
                                                        │    0.1.10.5 (Namespace Generation)
                                                        │    0.1.10.6 (Circular Detection)
                                                        │    0.1.10.7 (Import CodeGen)
                                                        │    0.1.10.8 (__init__.spy)
                                                        │    0.1.10.9 (.spyproj)
                                                        │           │
                                                        ▼           ▼
                                                 0.1.10.10 (Test Infrastructure)
                                                        │
                                                        ▼
                                                 0.1.10.11 (Integration Tests)
                                                        │
                                                        ▼
                                                 0.1.10.12 (Exit Criteria Doc)
```

## Estimated Total Time
- **Parsing/Resolution tasks:** 8-12 hours
- **Multi-file compilation:** 8-10 hours
- **Code generation tasks:** 6-8 hours
- **Testing infrastructure:** 4-6 hours
- **Testing and documentation:** 5-6 hours
- **Total:** 31-42 hours

## Notes for Agent/Engineer

1. **Imports must be at file top** — Validate position strictly.

2. **`__name` is private, not importable** — Single underscore is protected (not in `*`), double underscore is private.

3. **Circular imports are complex** — Two-phase resolution allows type annotations but not inheritance cycles.

4. **Wildcard import emits warning** — Good practice to import specific symbols.

5. **Module-level functions → static class** — Named after file, contains module functions.

6. **`src/` typically excluded from namespace** — Configurable in project file.

7. **External assembly references deferred** — System imports in Phase 0.1.12.

8. **Relative imports deferred** — Only absolute imports in Phase 0.1.10.

9. **Module initialization order matters** — Dependencies first, then module-level statements.
