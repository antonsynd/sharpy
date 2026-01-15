# Phase 0.1.10: Module System - Detailed Task List

**Goal:** Import statements and multi-file compilation.

**Prerequisites:** Phases 0.1.6-0.1.9 (Classes through Type System) must be complete.

**Exit Criteria (from spec):**
- `import` loads module symbols
- `from ... import` selectively imports
- Module aliases work
- Multi-file projects compile
- Circular type imports don't crash

---

## Pre-Implementation Checklist

Before starting any task, check what already exists:

```bash
# Check for import-related parsing
grep -rn "Import\|import" src/Sharpy.Compiler/Parser/

# Check for module resolution
grep -rn "ModuleResol\|Module" src/Sharpy.Compiler/

# Check for multi-file compilation
grep -rn "CompileProject\|SourceFile" src/Sharpy.Compiler/

# Check for existing import tests
dotnet test --list-tests | grep -i "import\|module"
```

---

## Task 0.1.10.1: Audit/Verify Import Statement Parsing

**Type:** 🔍 Status Check  
**Priority:** Critical  
**Estimated Time:** 1 hour

### Objective
Verify that import statements are parsed correctly.

### Files to Check
- `src/Sharpy.Compiler/Parser/Ast/Statement.cs`
- `src/Sharpy.Compiler/Parser/Parser.cs`

### Sharpy Import Syntax

```python
# Simple import
import math
import collections.utils

# Import with alias
import numpy as np
import mymodule as mm

# From import
from math import sqrt, pow
from collections import list, dict

# From import with alias
from mymodule import MyClass as MC

# Import all (use sparingly)
from utils import *
```

### Actions

1. **Verify `ImportStatement` AST node:**
   ```csharp
   public record ImportStatement : Statement
   {
       public List<ImportAlias> Names { get; init; } = new();
   }
   
   public record ImportAlias
   {
       public string Name { get; init; } = "";      // module.submodule
       public string? AsName { get; init; }         // Optional alias
   }
   ```

2. **Verify `FromImportStatement` AST node:**
   ```csharp
   public record FromImportStatement : Statement
   {
       public string Module { get; init; } = "";    // math, collections.utils
       public List<ImportAlias> Names { get; init; } = new();
       public bool ImportAll { get; init; }         // from x import *
   }
   ```

3. **Test parsing:**
   ```python
   import math
   import numpy as np
   from math import sqrt, pow
   from utils import helper as h
   ```

### Verification Commands
```bash
# Check ImportStatement definition
grep -A 15 "record ImportStatement" src/Sharpy.Compiler/Parser/Ast/Statement.cs
grep -A 15 "record FromImportStatement" src/Sharpy.Compiler/Parser/Ast/Statement.cs

# Run import parsing tests
dotnet test --filter "Import" src/Sharpy.Compiler.Tests/
```

---

## Task 0.1.10.2: Implement/Verify Module Resolution

**Type:** ⚠️ Likely Implementation Needed  
**Priority:** Critical  
**Estimated Time:** 3-4 hours

### Objective
Implement module path resolution to find source files.

### Files to Create/Modify
- `src/Sharpy.Compiler/Module/ModuleResolver.cs` (may need to create)
- `src/Sharpy.Compiler/Compiler.cs`

### Resolution Rules

1. **Search current directory first:**
   ```python
   import utils  # Looks for ./utils.spy or ./utils/__init__.spy
   ```

2. **Search project directories:**
   ```python
   # If compiling from project root
   import mypackage.helpers  # Looks for ./mypackage/helpers.spy
   ```

3. **Standard library paths (future):**
   - For .NET interop, see Phase 0.1.12

### Module Path Mapping

| Import | File Path Options |
|--------|-------------------|
| `import math` | `./math.spy`, `./math/__init__.spy` |
| `import utils.helpers` | `./utils/helpers.spy` |
| `from math import sqrt` | `./math.spy` (then find `sqrt` in module) |

### Implementation Hints

```csharp
public class ModuleResolver
{
    private readonly List<string> _searchPaths;
    private readonly Dictionary<string, ModuleInfo> _moduleCache = new();
    
    public ModuleResolver(string projectRoot)
    {
        _searchPaths = new List<string>
        {
            projectRoot,
            Path.Combine(projectRoot, "src"),
            // Standard library path (future)
        };
    }
    
    public ModuleInfo? ResolveModule(string moduleName, string currentFile)
    {
        // Check cache
        if (_moduleCache.TryGetValue(moduleName, out var cached))
            return cached;
        
        // Convert module name to path
        // math -> math.spy
        // utils.helpers -> utils/helpers.spy
        var relativePath = moduleName.Replace('.', Path.DirectorySeparatorChar) + ".spy";
        
        foreach (var searchPath in _searchPaths)
        {
            var fullPath = Path.Combine(searchPath, relativePath);
            
            // Try direct file
            if (File.Exists(fullPath))
            {
                return LoadModule(fullPath);
            }
            
            // Try package (__init__.spy)
            var packagePath = Path.Combine(
                searchPath, 
                moduleName.Replace('.', Path.DirectorySeparatorChar),
                "__init__.spy");
            
            if (File.Exists(packagePath))
            {
                return LoadModule(packagePath);
            }
        }
        
        return null; // Module not found
    }
    
    private ModuleInfo LoadModule(string path)
    {
        var source = File.ReadAllText(path);
        var lexer = new Lexer(source, _logger);
        var tokens = lexer.TokenizeAll();
        var parser = new Parser(tokens, _logger);
        var module = parser.ParseModule();
        
        var info = new ModuleInfo
        {
            Path = path,
            Ast = module,
            ExportedSymbols = ExtractExports(module)
        };
        
        _moduleCache[path] = info;
        return info;
    }
}
```

---

## Task 0.1.10.3: Implement Import Symbol Resolution

**Type:** 🆕 New Implementation  
**Priority:** Critical  
**Estimated Time:** 2-3 hours

### Objective
Resolve imported symbols and add them to the current module's scope.

### Files to Modify
- `src/Sharpy.Compiler/Semantic/SemanticAnalyzer.cs`
- `src/Sharpy.Compiler/Semantic/SymbolTable.cs`

### Actions

1. **Handle `import module`:**
   ```python
   import math
   # math.sqrt(...) accessible
   ```
   - Load module
   - Add module namespace to scope
   - Members accessed via qualified name

2. **Handle `import module as alias`:**
   ```python
   import numpy as np
   # np.array(...) accessible
   ```
   - Load module
   - Register under alias name

3. **Handle `from module import name`:**
   ```python
   from math import sqrt
   # sqrt(...) directly accessible
   ```
   - Load module
   - Extract specific symbol
   - Add to current scope directly

4. **Handle `from module import name as alias`:**
   ```python
   from math import sqrt as square_root
   # square_root(...) accessible
   ```
   - Load module
   - Extract specific symbol
   - Add to current scope under alias

5. **Handle `from module import *`:**
   ```python
   from utils import *
   # All public symbols from utils accessible
   ```
   - Load module
   - Add all public exports to current scope

### Implementation Hints

```csharp
private void ResolveImports(Module module)
{
    foreach (var stmt in module.Body)
    {
        switch (stmt)
        {
            case ImportStatement import:
                ResolveImportStatement(import);
                break;
            case FromImportStatement fromImport:
                ResolveFromImportStatement(fromImport);
                break;
        }
    }
}

private void ResolveImportStatement(ImportStatement import)
{
    foreach (var alias in import.Names)
    {
        var moduleInfo = _moduleResolver.ResolveModule(alias.Name, _currentFile);
        if (moduleInfo == null)
        {
            Error($"Module not found: {alias.Name}");
            continue;
        }
        
        var name = alias.AsName ?? alias.Name.Split('.').Last();
        _symbolTable.DefineModule(name, moduleInfo);
    }
}

private void ResolveFromImportStatement(FromImportStatement fromImport)
{
    var moduleInfo = _moduleResolver.ResolveModule(fromImport.Module, _currentFile);
    if (moduleInfo == null)
    {
        Error($"Module not found: {fromImport.Module}");
        return;
    }
    
    if (fromImport.ImportAll)
    {
        foreach (var (name, symbol) in moduleInfo.ExportedSymbols)
        {
            if (!name.StartsWith("_")) // Skip private symbols
            {
                _symbolTable.DefineFromImport(name, symbol);
            }
        }
    }
    else
    {
        foreach (var alias in fromImport.Names)
        {
            if (!moduleInfo.ExportedSymbols.TryGetValue(alias.Name, out var symbol))
            {
                Error($"Symbol '{alias.Name}' not found in module '{fromImport.Module}'");
                continue;
            }
            
            var name = alias.AsName ?? alias.Name;
            _symbolTable.DefineFromImport(name, symbol);
        }
    }
}
```

---

## Task 0.1.10.4: Implement Multi-File Compilation

**Type:** ⚠️ Likely Implementation Needed  
**Priority:** Critical  
**Estimated Time:** 3-4 hours

### Objective
Support compiling multiple `.spy` files into a single assembly.

### Files to Modify
- `src/Sharpy.Compiler/Compiler.cs`
- `src/Sharpy.Compiler/Project/ProjectCompiler.cs` (may need to create)

### Project Structure

```
project/
    main.spy              # Entry point
    utils/
        __init__.spy      # Package initialization
        helpers.spy
        math/
            __init__.spy
            vectors.spy
```

### Actions

1. **Define project configuration:**
   ```csharp
   public class ProjectConfig
   {
       public string Name { get; set; } = "";
       public string RootDirectory { get; set; } = "";
       public string EntryPoint { get; set; } = "";  // main.spy
       public List<string> SourceFiles { get; set; } = new();
   }
   ```

2. **Implement multi-file compilation:**
   ```csharp
   public CompilationResult CompileProject(ProjectConfig config)
   {
       // Phase 1: Parse all source files
       var parsedModules = new Dictionary<string, Module>();
       foreach (var sourceFile in config.SourceFiles)
       {
           var source = File.ReadAllText(sourceFile);
           var module = ParseFile(source, sourceFile);
           parsedModules[sourceFile] = module;
       }
       
       // Phase 2: Resolve imports across all modules
       var sharedSymbolTable = new SymbolTable();
       foreach (var (path, module) in parsedModules)
       {
           ResolveImports(module, sharedSymbolTable);
       }
       
       // Phase 3: Type check all modules
       foreach (var (path, module) in parsedModules)
       {
           TypeCheck(module, sharedSymbolTable);
       }
       
       // Phase 4: Generate code for all modules
       var compilationUnits = new List<CompilationUnitSyntax>();
       foreach (var (path, module) in parsedModules)
       {
           var unit = GenerateCode(module, path);
           compilationUnits.Add(unit);
       }
       
       // Phase 5: Compile to single assembly
       return CompileToAssembly(compilationUnits, config.Name);
   }
   ```

3. **Handle entry point:**
   - Only one file should have top-level statements that become `Main()`
   - Or use explicit entry point configuration

---

## Task 0.1.10.5: Implement Namespace Generation from Module Path

**Type:** 🆕 New Implementation  
**Priority:** High  
**Estimated Time:** 2 hours

### Objective
Generate appropriate C# namespaces based on module structure.

### Files to Modify
- `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

### Namespace Rules

| Sharpy Module Path | C# Namespace |
|-------------------|--------------|
| `main.spy` (root) | `ProjectName` |
| `utils/helpers.spy` | `ProjectName.Utils` |
| `utils/math/vectors.spy` | `ProjectName.Utils.Math` |

### Name Mangling

Module/namespace names follow standard mangling:
- `snake_case` → `PascalCase`
- `io`, `ui`, `xml`, `http`, `api`, `sql` → Uppercase acronyms

### Implementation Hints

```csharp
private string GetNamespaceForFile(string projectName, string sourceFile, string projectRoot)
{
    var relativePath = Path.GetRelativePath(projectRoot, sourceFile);
    var directory = Path.GetDirectoryName(relativePath) ?? "";
    
    if (string.IsNullOrEmpty(directory))
    {
        return NameMangler.ToPascalCase(projectName);
    }
    
    var parts = directory
        .Split(Path.DirectorySeparatorChar)
        .Select(NameMangler.ToPascalCase);
    
    return $"{NameMangler.ToPascalCase(projectName)}.{string.Join(".", parts)}";
}
```

---

## Task 0.1.10.6: Implement Circular Import Detection and Handling

**Type:** 🆕 New Implementation  
**Priority:** High  
**Estimated Time:** 2-3 hours

### Objective
Handle circular imports gracefully (allowed for type annotations only).

### Spec Rules

Circular imports are resolved through forward references in type annotations:

```python
# module_a.spy
from module_b import ClassB  # Forward reference for type annotation

class ClassA:
    other: ClassB  # OK - used only as type annotation
```

```python
# module_b.spy
from module_a import ClassA

class ClassB:
    other: ClassA  # OK - used only as type annotation
```

### Allowed vs Disallowed

| Usage | Allowed |
|-------|---------|
| Type annotations | ✅ Yes |
| Base class inheritance | ❌ No |
| Function body usage | ✅ Yes (runtime) |

### Implementation Strategy: Two-Phase Resolution

1. **Phase 1: Type Declaration**
   - Register all type names (classes, structs, interfaces, enums)
   - Don't resolve member types yet

2. **Phase 2: Type Resolution**
   - Resolve all type annotations
   - Now circular references work because all names are registered

### Implementation Hints

```csharp
public void ResolveImportsWithCircularHandling(List<(string Path, Module Module)> modules)
{
    // Track loading state for circular detection
    var loading = new HashSet<string>();
    var loaded = new HashSet<string>();
    
    // Phase 1: Register all type declarations (no resolution)
    foreach (var (path, module) in modules)
    {
        RegisterTypeDeclarations(path, module);
    }
    
    // Phase 2: Resolve imports and type members
    foreach (var (path, module) in modules)
    {
        ResolveModuleImports(path, module, loading, loaded);
    }
}

private void ResolveModuleImports(string path, Module module, 
    HashSet<string> loading, HashSet<string> loaded)
{
    if (loaded.Contains(path))
        return;
    
    if (loading.Contains(path))
    {
        // Circular import detected - OK if just for type annotations
        return;
    }
    
    loading.Add(path);
    
    foreach (var stmt in module.Body)
    {
        if (stmt is ImportStatement import)
        {
            // Recursively load imported module
            var importedPath = ResolveModulePath(import);
            if (importedPath != null && !loaded.Contains(importedPath))
            {
                var importedModule = LoadModule(importedPath);
                ResolveModuleImports(importedPath, importedModule, loading, loaded);
            }
        }
    }
    
    loading.Remove(path);
    loaded.Add(path);
}
```

---

## Task 0.1.10.7: Implement Import Code Generation

**Type:** 🆕 New Implementation  
**Priority:** High  
**Estimated Time:** 2 hours

### Objective
Generate appropriate C# using statements or qualified names for imports.

### Files to Modify
- `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

### Code Generation Rules

1. **Module import → Namespace using:**
   ```python
   import utils.helpers
   ```
   Generates (at file top):
   ```csharp
   using ProjectName.Utils;
   ```

2. **From import → Direct usage:**
   ```python
   from utils.helpers import format_string
   # Use format_string() directly
   ```
   Generates:
   ```csharp
   using ProjectName.Utils;
   // FormatString() used directly (from Helpers class)
   ```

3. **Aliased import → Namespace alias:**
   ```python
   import utils.helpers as h
   # h.format_string()
   ```
   Generates:
   ```csharp
   using H = ProjectName.Utils.Helpers;
   // H.FormatString()
   ```

### Implementation Hints

```csharp
private List<UsingDirectiveSyntax> GenerateUsings(Module module, string projectName)
{
    var usings = new List<UsingDirectiveSyntax>();
    
    foreach (var stmt in module.Body)
    {
        if (stmt is ImportStatement import)
        {
            foreach (var alias in import.Names)
            {
                var ns = GetNamespaceForModule(alias.Name, projectName);
                
                if (alias.AsName != null)
                {
                    // Aliased using
                    usings.Add(UsingDirective(NameEquals(
                        IdentifierName(alias.AsName)), ParseName(ns)));
                }
                else
                {
                    // Regular using
                    usings.Add(UsingDirective(ParseName(ns)));
                }
            }
        }
        else if (stmt is FromImportStatement fromImport)
        {
            var ns = GetNamespaceForModule(fromImport.Module, projectName);
            usings.Add(UsingDirective(ParseName(ns)));
        }
    }
    
    return usings;
}
```

---

## Task 0.1.10.8: Implement Package `__init__.spy` Handling

**Type:** 🆕 New Implementation  
**Priority:** Medium  
**Estimated Time:** 1-2 hours

### Objective
Support `__init__.spy` files for package initialization and re-exports.

### Package Structure
```
utils/
    __init__.spy      # Re-exports from submodules
    helpers.spy
    math/
        __init__.spy
        vectors.spy
```

### `__init__.spy` Content
```python
# utils/__init__.spy
from utils.helpers import format_string, parse_input
from utils.math.vectors import Vector2, Vector3
```

### Behavior

When importing a package:
```python
import utils
# Symbols from __init__.spy are available via utils.*
```

```python
from utils import format_string
# Direct access to re-exported symbol
```

### Actions

1. **Detect package vs module:**
   ```csharp
   private bool IsPackage(string modulePath)
   {
       var initPath = Path.Combine(modulePath, "__init__.spy");
       return Directory.Exists(modulePath) && File.Exists(initPath);
   }
   ```

2. **Load `__init__.spy` for packages:**
   ```csharp
   private ModuleInfo LoadPackage(string packagePath)
   {
       var initPath = Path.Combine(packagePath, "__init__.spy");
       return LoadModule(initPath);
   }
   ```

3. **Process re-exports:**
   - Parse `from ... import ...` statements in `__init__.spy`
   - Add re-exported symbols to package's exported symbols

---

## Task 0.1.10.9: Implement `.spyproj` Project File Support

**Type:** 🆕 New Implementation  
**Priority:** Medium  
**Estimated Time:** 2-3 hours

### Objective
Support project files for multi-file compilation configuration.

### Project File Format (`.spyproj`)

```json
{
  "name": "MyProject",
  "version": "1.0.0",
  "outputType": "exe",
  "entryPoint": "src/main.spy",
  "sourceFiles": [
    "src/**/*.spy"
  ],
  "references": [
    { "project": "../shared/shared.spyproj" }
  ],
  "targetFramework": "net8.0"
}
```

### Actions

1. **Define project file schema:**
   ```csharp
   public class SpyProject
   {
       public string Name { get; set; } = "";
       public string Version { get; set; } = "1.0.0";
       public string OutputType { get; set; } = "exe";  // exe, library
       public string? EntryPoint { get; set; }
       public List<string> SourceFiles { get; set; } = new();
       public List<ProjectReference> References { get; set; } = new();
       public string TargetFramework { get; set; } = "net8.0";
   }
   ```

2. **Implement project file loading:**
   ```csharp
   public SpyProject LoadProject(string projectPath)
   {
       var json = File.ReadAllText(projectPath);
       var project = JsonSerializer.Deserialize<SpyProject>(json);
       
       // Expand glob patterns
       project.SourceFiles = ExpandGlobs(
           project.SourceFiles, 
           Path.GetDirectoryName(projectPath));
       
       return project;
   }
   ```

3. **Support glob patterns:**
   ```csharp
   private List<string> ExpandGlobs(List<string> patterns, string basePath)
   {
       var files = new List<string>();
       foreach (var pattern in patterns)
       {
           var matcher = new Matcher();
           matcher.AddInclude(pattern);
           var result = matcher.GetResultsInFullPath(basePath);
           files.AddRange(result);
       }
       return files;
   }
   ```

---

## Task 0.1.10.10: Create Phase 0.1.10 Integration Tests

**Type:** 🆕 New Implementation  
**Priority:** Critical  
**Estimated Time:** 3-4 hours

### Objective
Create comprehensive end-to-end tests for module system.

### File to Create
`src/Sharpy.Compiler.Tests/Integration/Phase0110IntegrationTests.cs`

### Test Setup

Create test fixtures with multiple files:

```csharp
public class Phase0110IntegrationTests : IDisposable
{
    private readonly string _tempDir;
    
    public Phase0110IntegrationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
    }
    
    public void Dispose()
    {
        Directory.Delete(_tempDir, recursive: true);
    }
    
    private void WriteFile(string relativePath, string content)
    {
        var fullPath = Path.Combine(_tempDir, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
    }
}
```

### Test Cases

```csharp
#region Simple Import

[Fact]
public void ImportStatement_LoadsModule()
{
    WriteFile("math_utils.spy", @"
def square(x: int) -> int:
    return x * x
");
    
    WriteFile("main.spy", @"
import math_utils

result = math_utils.square(5)
");
    
    var result = CompileProject(_tempDir, "main.spy");
    Assert.True(result.Success);
}

[Fact]
public void ImportWithAlias_Works()
{
    WriteFile("utils.spy", @"
def helper() -> str:
    return 'helper'
");
    
    WriteFile("main.spy", @"
import utils as u

result = u.helper()
");
    
    var result = CompileProject(_tempDir, "main.spy");
    Assert.True(result.Success);
}

#endregion

#region From Import

[Fact]
public void FromImport_SelectiveImport()
{
    WriteFile("math_utils.spy", @"
def square(x: int) -> int:
    return x * x

def cube(x: int) -> int:
    return x * x * x
");
    
    WriteFile("main.spy", @"
from math_utils import square

result = square(5)
");
    
    var result = CompileProject(_tempDir, "main.spy");
    Assert.True(result.Success);
}

[Fact]
public void FromImportWithAlias_Works()
{
    WriteFile("utils.spy", @"
def format_string(s: str) -> str:
    return s.upper()
");
    
    WriteFile("main.spy", @"
from utils import format_string as fmt

result = fmt('hello')
");
    
    var result = CompileProject(_tempDir, "main.spy");
    Assert.True(result.Success);
}

[Fact]
public void FromImportStar_ImportsAll()
{
    WriteFile("utils.spy", @"
def func1() -> int:
    return 1

def func2() -> int:
    return 2

_private_func = 3  # Should not be imported
");
    
    WriteFile("main.spy", @"
from utils import *

a = func1()
b = func2()
");
    
    var result = CompileProject(_tempDir, "main.spy");
    Assert.True(result.Success);
}

#endregion

#region Package Structure

[Fact]
public void Package_WithInitSpy()
{
    WriteFile("utils/__init__.spy", @"
from utils.helpers import format_text
");
    
    WriteFile("utils/helpers.spy", @"
def format_text(s: str) -> str:
    return s.upper()
");
    
    WriteFile("main.spy", @"
from utils import format_text

result = format_text('hello')
");
    
    var result = CompileProject(_tempDir, "main.spy");
    Assert.True(result.Success);
}

[Fact]
public void NestedPackage_Works()
{
    WriteFile("utils/__init__.spy", "");
    WriteFile("utils/math/__init__.spy", @"
from utils.math.vectors import Vector2
");
    WriteFile("utils/math/vectors.spy", @"
class Vector2:
    x: float
    y: float
    
    def __init__(self, x: float, y: float):
        self.x = x
        self.y = y
");
    
    WriteFile("main.spy", @"
from utils.math import Vector2

v = Vector2(1.0, 2.0)
");
    
    var result = CompileProject(_tempDir, "main.spy");
    Assert.True(result.Success);
}

#endregion

#region Circular Imports

[Fact]
public void CircularImport_TypeAnnotationsOnly_Works()
{
    WriteFile("module_a.spy", @"
from module_b import ClassB

class ClassA:
    other: ClassB?
    
    def __init__(self):
        self.other = None
");
    
    WriteFile("module_b.spy", @"
from module_a import ClassA

class ClassB:
    other: ClassA?
    
    def __init__(self):
        self.other = None
");
    
    WriteFile("main.spy", @"
from module_a import ClassA
from module_b import ClassB

a = ClassA()
b = ClassB()
a.other = b
b.other = a
");
    
    var result = CompileProject(_tempDir, "main.spy");
    Assert.True(result.Success);
}

[Fact]
public void CircularImport_BaseClass_Error()
{
    WriteFile("module_a.spy", @"
from module_b import ClassB

class ClassA(ClassB):  # Circular base class
    pass
");
    
    WriteFile("module_b.spy", @"
from module_a import ClassA

class ClassB(ClassA):  # Circular base class
    pass
");
    
    var result = CompileProject(_tempDir, "module_a.spy");
    Assert.False(result.Success);
    Assert.Contains("circular", result.Errors.First().ToLower());
}

#endregion

#region Multi-File Compilation

[Fact]
public void MultiFile_SingleAssembly()
{
    WriteFile("models.spy", @"
class User:
    name: str
    
    def __init__(self, name: str):
        self.name = name
");
    
    WriteFile("services.spy", @"
from models import User

def create_user(name: str) -> User:
    return User(name)
");
    
    WriteFile("main.spy", @"
from services import create_user

user = create_user('Alice')
");
    
    var result = CompileProject(_tempDir, entryPoint: "main.spy", 
        sourceFiles: new[] { "models.spy", "services.spy", "main.spy" });
    Assert.True(result.Success);
}

#endregion

#region Error Cases

[Fact]
public void ImportNonExistentModule_Error()
{
    WriteFile("main.spy", @"
import nonexistent_module
");
    
    var result = CompileProject(_tempDir, "main.spy");
    Assert.False(result.Success);
    Assert.Contains("not found", result.Errors.First().ToLower());
}

[Fact]
public void ImportNonExistentSymbol_Error()
{
    WriteFile("utils.spy", @"
def existing_func() -> int:
    return 1
");
    
    WriteFile("main.spy", @"
from utils import nonexistent_func
");
    
    var result = CompileProject(_tempDir, "main.spy");
    Assert.False(result.Success);
    Assert.Contains("not found", result.Errors.First().ToLower());
}

#endregion
```

---

## Task 0.1.10.11: Document Phase 0.1.10 Exit Criteria Verification

**Type:** 📝 Documentation  
**Priority:** High  
**Estimated Time:** 30 minutes

### Exit Criteria Checklist

| Criterion | Test | Status |
|-----------|------|--------|
| `import` loads module symbols | `ImportStatement_LoadsModule` | [ ] |
| `from ... import` selectively imports | `FromImport_SelectiveImport` | [ ] |
| Module aliases work | `ImportWithAlias_Works` | [ ] |
| Multi-file projects compile | `MultiFile_SingleAssembly` | [ ] |
| Circular type imports don't crash | `CircularImport_TypeAnnotationsOnly_Works` | [ ] |

### Verification Process

```bash
# Run all Phase 0.1.10 tests
dotnet test --filter "Phase0110" --logger "console;verbosity=detailed"

# Test with a real multi-file project
dotnet run --project src/Sharpy.Compiler -- build samples/calculator_app/calculator.spyproj
```

---

## Summary: Task Dependencies

```
0.1.10.1 (Import Parsing) ───────┐
                                 │
0.1.10.2 (Module Resolution) ────┼──► 0.1.10.3 (Symbol Resolution)
                                 │           │
                                 │           ▼
                                 │    0.1.10.4 (Multi-File Compile)
                                 │           │
                                 │           ▼
                                 │    0.1.10.5 (Namespace Generation)
                                 │           │
0.1.10.6 (Circular Handling) ────┤           │
                                 │           ▼
0.1.10.7 (Import CodeGen) ───────┤    0.1.10.8 (__init__.spy)
                                 │           │
0.1.10.9 (.spyproj Support) ─────┤           │
                                 │           ▼
                                 ▼    0.1.10.10 (Integration Tests)
                                             │
                                             ▼
                                      0.1.10.11 (Exit Criteria Doc)
```

## Estimated Total Time
- **Audit/Verification tasks:** 2-3 hours
- **Implementation tasks:** 18-24 hours
- **Testing and documentation:** 4-5 hours
- **Total:** 24-32 hours

## Notes for Agent/Engineer

1. **Module resolution order matters:** Current directory → project directories → standard library.

2. **Circular imports limited:** Only type annotations, not base classes.

3. **Two-phase resolution:** Register types first, resolve references second.

4. **`__init__.spy` defines package exports:** Re-exports make symbols available at package level.

5. **Namespace generation from path:** `utils/helpers.spy` → `ProjectName.Utils.Helpers`.

6. **Private symbols not exported:** Names starting with `_` are not exported by `import *`.

7. **Assembly boundary:** Each `.spyproj` compiles to one assembly; `@internal` visibility respects this.
