# Walkthrough: RoslynEmitter.CompilationUnit.cs

**Source File**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.CompilationUnit.cs`

---

## Overview

This file is part of the `RoslynEmitter` partial class and handles the **top-level structure** of C# code generation: compilation units, namespaces, and import directives. It's the **entry point** for transforming a Sharpy AST into the Roslyn C# syntax tree structure.

**Key Responsibilities:**
- Generate the `CompilationUnitSyntax` (root of the C# syntax tree)
- Convert Sharpy `import` and `from ... import` statements to C# `using` directives
- Generate namespace declarations from file paths
- Add the `#nullable enable` pragma for null-safety alignment
- Handle special cases: .NET framework imports vs. Sharpy module imports

**Position in Pipeline:**
```
Semantic Analysis (typed AST) → RoslynEmitter.CompilationUnit → CompilationUnitSyntax → Other RoslynEmitter files → C# source
```

---

## Class/Type Structure

This file defines methods on the `public partial class RoslynEmitter`. The class is split across multiple files:

- **RoslynEmitter.cs** - Main class, fields, and helper methods
- **RoslynEmitter.CompilationUnit.cs** - *(This file)* Top-level structure
- **RoslynEmitter.ModuleClass.cs** - Module class generation
- **RoslynEmitter.Statements.cs** - Statement generation
- **RoslynEmitter.Expressions.cs** - Expression generation
- **RoslynEmitter.TypeDeclarations.cs** - Type definitions
- **RoslynEmitter.ClassMembers.cs** - Class members
- **RoslynEmitter.Operators.cs** - Operator handling

**Fields Used (from RoslynEmitter.cs):**
- `_context: CodeGenContext` - Compilation context with project settings, symbol table, etc.
- `_typeMapper: TypeMapper` - Maps Sharpy types to C# types

---

## Key Methods

### 1. `GenerateCompilationUnit(Module module)` → `CompilationUnitSyntax`

**Purpose:** The main entry point that generates the complete C# compilation unit from a Sharpy module.

**Flow:**
```
1. Generate using directives (from imports)
2. Separate import statements from code statements
3. Collect from-imports for re-export (if not entry point)
4. Generate module class wrapper (delegates to GenerateModuleClass)
5. Generate namespace declaration
6. Build CompilationUnit with usings + namespace + module class
7. Add #nullable enable pragma
```

**Important Details:**

- **Re-export Logic (lines 30-40):** Non-entry-point files (library modules) re-export symbols from `from ... import` statements. This allows multi-file projects where `package/__init__.py` re-exports symbols from submodules. Entry point files don't re-export because they're executable, not libraries.

- **`#nullable enable` Pragma (lines 57-62):** Added *after* `NormalizeWhitespace()` to preserve leading position. This aligns with Sharpy's "null-safe by default" design (Axiom 3: Type Safety).

**Example Output Structure:**
```csharp
#nullable enable

using System;
using System.Collections.Generic;
using global::Sharpy.Core;

namespace MyProject.MyModule
{
    public static class Exports
    {
        // Module contents...
    }
}
```

---

### 2. `GenerateNamespaceName()` → `NameSyntax`

**Purpose:** Generate the C# namespace name from source file path and project settings.

**Logic Tree:**
```
IF ProjectNamespace + ProjectRootPath + SourceFilePath all set:
  → GenerateProjectNamespace() (multi-file project mode)

ELSE IF ProjectNamespace only:
  → ProjectNamespace.FileName (single-file with explicit namespace)

ELSE (fallback):
  → Sharpy.FileName (single-file, derived from filename only)
  → SharpyGenerated (if no filename available)
```

**Why Multiple Paths?**
- **Multi-file projects** need relative namespaces (e.g., `MyProject.Utils.Helpers` from `src/utils/helpers.spy`)
- **Single-file compilation** with explicit namespace (e.g., `dotnet run --namespace MyApp`)
- **Fallback** for REPL or in-memory compilation

**Edge Case Handling (lines 91-108):** Single-file compilation avoids numeric directories and special characters in paths by only using the filename, preventing issues like `Sharpy.123.MyModule`.

---

### 3. `GenerateProjectNamespace()` → `NameSyntax`

**Purpose:** Generate namespace for multi-file projects with relative path structure.

**Algorithm:**
```
1. Start with ProjectNamespace (e.g., "MyProject")
2. Get relative path from ProjectRootPath to SourceFilePath
3. Extract directory parts, convert to PascalCase
4. Add filename to namespace (EXCEPT __init__.spy)
5. Join with dots
```

**Python Package Convention (lines 132-136):**
```python
# File: mypackage/__init__.py
# Namespace: MyProject.Mypackage  (no __init__ suffix)

# File: mypackage/helpers.py
# Namespace: MyProject.Mypackage.Helpers
```

**Name Mangling:** Uses `SimpleToPascalCase()` to convert snake_case → PascalCase:
- `my_utils` → `MyUtils`
- `data_loader` → `DataLoader`

---

### 4. `GenerateUsingDirectives(Module module)` → `List<UsingDirectiveSyntax>`

**Purpose:** Convert Sharpy imports to C# using directives with deduplication.

**Process:**
```
1. Add default usings: System, System.Collections.Generic, System.Linq
2. Add Sharpy runtime: global::Sharpy.Core (global:: avoids conflicts)
3. Process import statements → GenerateImportUsings()
4. Process from-import statements → GenerateFromImportUsings()
5. Deduplicate by normalized string representation
```

**Why `global::Sharpy.Core`?** (line 151)
If the generated namespace is `Sharpy.MyModule`, a plain `using Sharpy.Core;` would be ambiguous. The `global::` prefix ensures we refer to the root `Sharpy.Core` namespace.

---

### 5. `GenerateImportUsings(ImportStatement import)` → `IEnumerable<UsingDirectiveSyntax>`

**Purpose:** Handle `import module` and `import module as alias` statements.

**Two Categories:**

**A) .NET Framework Imports** (detected by `IsNetFrameworkNamespace`):
```python
import system.io              # → using System.IO;
import system.io as io        # → using io = System.IO;
```

**B) Sharpy Module Imports** (user modules):
```python
import config                 # → using config = MyProject.Config.Exports;
import utils.helpers          # → using utils_helpers = MyProject.Utils.Helpers.Exports;
import utils.helpers as h     # → using h = MyProject.Utils.Helpers.Exports;
```

**Key Insight:** Sharpy modules expose their members via a static class named `Exports`, not the module name itself. This convention allows Python-style module access patterns.

**Keyword Escaping (line 234):** Module names that are C# keywords get `@` prefix:
```python
import base   # → using @base = MyProject.Base.Exports;
```

---

### 6. `GenerateFromImportUsings(FromImportStatement fromImport)` → `IEnumerable<UsingDirectiveSyntax>`

**Purpose:** Handle `from module import symbol` statements.

**Two Categories:**

**A) .NET Framework Imports:**
```python
from system.io import File    # → using System.IO;
```

**B) Sharpy Module Imports** (use `using static`):
```python
from config import MAX_SIZE   # → using static MyProject.Config.Exports;
```

**Why `using static`?** (lines 274-309)
This enables direct access to static members without qualification:
```csharp
using static MyProject.Config.Exports;

var size = MAX_SIZE;  // Direct access, no prefix needed
```

**Relative Import Resolution (line 286):** Uses `GetResolvedModulePath()` to convert relative imports:
```python
# In mypackage/submodule.py:
from .helpers import util_function   # → using static MyProject.Mypackage.Helpers.Exports;
```

The `ResolvedModulePath` was computed during semantic analysis by the import resolver.

---

### 7. `IsNetFrameworkNamespace(string moduleName)` → `bool`

**Purpose:** Distinguish .NET framework namespaces from Sharpy modules.

**Detection Logic:** Checks if first part of module name matches known prefixes:
- `system.*` → .NET framework
- `microsoft.*` → .NET framework
- `windows.*`, `xamarin.*`, `mono.*`, `netstandard.*` → .NET framework

**Why Important?**
- .NET namespaces: Use standard `using` directives
- Sharpy modules: Use `using static <Module>.Exports` pattern

**False Positives:** A user module named `system_utils` would incorrectly be treated as .NET. This is acceptable because naming user modules `system.*` is bad practice.

---

### 8. `IsConstantCaseName(string name)` → `bool`

**Purpose:** Detect if a symbol name is in CONSTANT_CASE (e.g., `MAX_SIZE`, `API_KEY`).

**Algorithm:**
- Check all characters are uppercase letters, digits, or underscores
- Must contain at least one letter
- Return true if all letters are uppercase

**Use Case:** Determines proper C# name casing for from-import symbols:
```python
from config import MAX_SIZE       # Constant → MAX_SIZE (preserve)
from config import max_size       # Variable → MaxSize (PascalCase for module-level)
```

---

### 9. `EscapeCSharpKeyword(string name)` → `string`

**Purpose:** Escape C# reserved keywords with `@` prefix.

**Examples:**
- `base` → `@base`
- `class` → `@class`
- `string` → `@string`
- `my_func` → `my_func` (not a keyword, unchanged)

**Keyword List:** Uses `CSharpKeywords` HashSet (lines 364-378) with case-insensitive comparison.

**Where Used:**
- Module import aliases (line 234)
- Module symbol name resolution (line 179 in RoslynEmitter.cs)

---

## Dependencies

### Internal (Sharpy Compiler)
- **`Sharpy.Compiler.Parser.Ast`**: AST node types (`Module`, `ImportStatement`, `FromImportStatement`)
- **`Sharpy.Compiler.Semantic`**: Symbol resolution and semantic data
- **`CodeGenContext`**: Compilation settings, symbol table, project paths
- **`NameMangler`** (from RoslynEmitter.cs): snake_case → PascalCase conversion

### External (Microsoft)
- **`Microsoft.CodeAnalysis.CSharp`**: Roslyn C# syntax tree APIs
- **`Microsoft.CodeAnalysis.CSharp.SyntaxFactory`**: Factory methods for creating syntax nodes (imported as `static`)

### Related Files (Partial Class)
- **`RoslynEmitter.cs`** - Field definitions, helper methods
- **`RoslynEmitter.ModuleClass.cs`** - `GenerateModuleClass()` called at line 43
- **`CodeGenContext.cs`** - Context object with project settings

---

## Patterns and Design Decisions

### 1. **SyntaxFactory Pattern**
All syntax tree construction uses `SyntaxFactory` methods (imported statically at line 7):
```csharp
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

// Enables clean syntax:
var ns = NamespaceDeclaration(namespaceName)
    .WithMembers(SingletonList<MemberDeclarationSyntax>(moduleClass));
```

**Why?** The CLAUDE.md rules state: "RoslynEmitter uses SyntaxFactory exclusively — no string templating." This ensures type-safe, validated syntax trees.

### 2. **Immutable AST with Semantic Separation**
Import resolution data is stored in two places:
- **SemanticBinding** (preferred, new approach): `GetResolvedModulePath()`, `GetReExportedSymbols()`
- **AST properties** (legacy fallback): `fromImport.ResolvedModulePath`, `fromImport.ReExportedSymbols`

The methods at lines 287-315 abstract this dual storage, preferring SemanticBinding when available.

**Why Dual Storage?** Transitional architecture. Future versions will remove mutable AST properties entirely.

### 3. **Exports Class Convention**
Sharpy modules expose members via a static class named `Exports`:

**Sharpy code:**
```python
# config.spy
MAX_SIZE = 1024
def get_config(): ...
```

**Generated C#:**
```csharp
namespace MyProject.Config
{
    public static class Exports
    {
        public static readonly int MAX_SIZE = 1024;
        public static ConfigType GetConfig() { ... }
    }
}
```

**Why?** Allows Python-style module imports while maintaining static typing:
```python
import config
print(config.MAX_SIZE)  # → config.MAX_SIZE in C# (where config is alias for MyProject.Config.Exports)
```

### 4. **Namespace Sanitization**
Module paths are converted to valid C# namespaces:
- Dots in module names → Underscores in aliases (line 234)
- PascalCase for namespace segments (line 127)
- Reserved words → `@`-prefixed (line 234)

### 5. **C# 9.0 Compatibility (Unity)**
Uses **block-scoped namespaces** (lines 48-49), not file-scoped:
```csharp
namespace MyProject.MyModule   // C# 9.0 compatible
{
    public static class Exports { ... }
}

// NOT:
// namespace MyProject.MyModule;  // C# 10.0+ only
```

**Reason:** Unity uses older C# compiler versions. Per CLAUDE.md: "C# 9.0 target — no global usings, file-scoped namespaces."

---

## Debugging Tips

### Inspecting Generated Namespaces
If namespace generation seems wrong:
1. Check `_context.ProjectNamespace`, `_context.ProjectRootPath`, `_context.SourceFilePath`
2. Set breakpoint in `GenerateNamespaceName()` line 65
3. Verify path separator handling (Windows `\` vs. Unix `/`) at line 126

### Import Not Resolved
If `from module import symbol` isn't working:
1. Check if `IsNetFrameworkNamespace()` is misclassifying the module
2. Verify `GetResolvedModulePath()` returns correct path for relative imports
3. Check if `SemanticBinding` is set in `_context` (null means fallback to AST properties)

### Duplicate Using Directives
The deduplication logic (lines 166-177) normalizes and compares strings. If seeing duplicates:
- Check if normalized forms are actually different (e.g., `global::Sharpy.Core` vs. `Sharpy.Core`)
- Verify `NormalizeWhitespace()` produces consistent output

### Module Alias Conflicts
If seeing errors like "The type or namespace name 'X' could not be found":
- Check if module alias conflicts with C# keyword (should be `@`-escaped)
- Verify alias sanitization at line 234 handles dots correctly (`utils.helpers` → `utils_helpers`)

### Re-export Not Working
If symbols aren't re-exported from `__init__.py`:
1. Check `_context.IsEntryPoint` (should be `false` for library modules)
2. Verify `HasReExportedSymbols()` returns true (line 38)
3. Check semantic analysis populated `ReExportedSymbols` on the AST node

---

## Contribution Guidelines

### Adding New Import Types
If adding support for new import patterns:
1. Update `GenerateImportUsings()` or `GenerateFromImportUsings()` based on statement type
2. Consider .NET vs. Sharpy module distinction
3. Add tests in `src/Sharpy.Compiler.Tests/CodeGen/` for the new pattern
4. Update `IsNetFrameworkNamespace()` if adding new framework prefixes

### Modifying Namespace Generation
If changing namespace generation logic:
1. Ensure backward compatibility with existing projects
2. Test with: single-file, multi-file, __init__.py files, nested directories
3. Verify Windows and Unix path handling
4. Update tests in integration test fixtures (`src/Sharpy.Compiler.Tests/Integration/TestFixtures/`)

### Performance Considerations
- Using deduplication is O(n) on number of imports (acceptable for typical files)
- If adding expensive checks, cache results in `_context`
- `IsNetFrameworkNamespace()` string prefix check is fast, don't over-optimize

### C# Target Version Compatibility
- NEVER use C# 10+ features (file-scoped namespaces, global usings)
- Test in Unity if changing generated C# patterns
- Stick to Roslyn APIs available in .NET SDK 6.0+ (Sharpy's minimum)

### Code Generation Axioms
When making changes, respect the axiom hierarchy:
1. **.NET Compatibility** (highest) - Generated C# must compile and run on .NET/Unity
2. **Type Safety** - Maintain null-safety, strong typing
3. **Python Syntax Fidelity** (lowest) - Match Python semantics where possible

Example: If Python import semantics conflict with C# namespace rules, C# wins.

---

## Cross-References

### Related Partial Class Files
This file is part of `RoslynEmitter` partial class. For complete understanding, see:

- **[RoslynEmitter.cs](./RoslynEmitter.md)** - Main class definition, fields, name mangling
- **[RoslynEmitter.ModuleClass.cs](./RoslynEmitter.ModuleClass.md)** - Module wrapper class generation (called at line 43)
- **[CodeGenContext.md](./CodeGenContext.md)** - Compilation context and settings

### Semantic Analysis Dependencies
This file relies on data computed during semantic analysis:

- **`CodeGenInfo`** (src/Sharpy.Compiler/Semantic/CodeGenInfo.cs) - Symbol metadata for code generation
- **`SemanticBinding`** (src/Sharpy.Compiler/Semantic/SemanticBinding.cs) - Import resolution data
- **Import Resolution** - Relative import path resolution happens in semantic phase

### Specification References
- **`docs/language_specification/dotnet_interop.md`** - How Sharpy imports map to .NET namespaces
- **`docs/language_specification/modules.md`** *(if exists)* - Module system specification

### Testing
- Integration tests: `src/Sharpy.Compiler.Tests/Integration/TestFixtures/*.spy`
- Unit tests: Search for "Import" or "Namespace" test classes in `src/Sharpy.Compiler.Tests/`

---

## Summary

This file is the **foundation** of C# code generation, setting up the top-level structure that all other RoslynEmitter components build upon. It bridges Sharpy's Python-like module system with C#'s namespace and using directive system, handling the critical distinction between .NET framework imports and Sharpy user modules.

**Key Takeaways:**
- Entry point: `GenerateCompilationUnit()` produces the root `CompilationUnitSyntax`
- Import handling: Sharpy modules use `Exports` class convention with `using static`
- Namespace generation: Multi-mode support (project, single-file, fallback)
- Null-safety: Always includes `#nullable enable` pragma
- Compatibility: C# 9.0 compatible (Unity support)

**When debugging code generation issues, start here** - most problems with imports, namespaces, or overall structure trace back to this file.
