# Walkthrough: RoslynEmitter.CompilationUnit.cs

**Source File**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.CompilationUnit.cs`

---

## Overview

This file is a partial class of `RoslynEmitter`, the final phase in the Sharpy compiler pipeline. It handles the **top-level orchestration** of C# code generation: creating the compilation unit structure, organizing namespaces, generating `using` directives, and managing imports.

**Key Responsibility**: Transform a Sharpy module (AST) into a complete, well-structured C# compilation unit with proper namespacing, imports, and nullable reference type directives.

**Position in Pipeline**:
```
Parser (AST) → Semantic Analysis → [THIS FILE] → C# Source Code → .NET IL
```

This file doesn't generate individual statements or expressions (those are in other partial files like `RoslynEmitter.Statements.cs` and `RoslynEmitter.Expressions.cs`). Instead, it creates the "shell" that wraps all generated code—the top-level structure that makes the C# file valid and compilable.

---

## Class/Type Structure

### `RoslynEmitter` (partial class)

This is one of several partial class files that together form the complete `RoslynEmitter`. The class uses the **Roslyn API** exclusively (via `SyntaxFactory`) to construct C# syntax trees—no string templating allowed.

**Partial Class Files**:
- **RoslynEmitter.cs** - Main class, fields, and helper methods
- **RoslynEmitter.CompilationUnit.cs** - *(This file)* Top-level structure
- **RoslynEmitter.ModuleClass.cs** - Module class generation
- **RoslynEmitter.Statements.cs** - Statement generation
- **RoslynEmitter.Expressions.cs** - Expression generation
- **RoslynEmitter.TypeDeclarations.cs** - Type definitions
- **RoslynEmitter.ClassMembers.cs** - Class member generation
- **RoslynEmitter.Operators.cs** - Operator handling

**Key Fields from Main `RoslynEmitter.cs`**:
- `_context: CodeGenContext` — Contains symbol table, project namespace, source file path, entry point flag
- `_typeMapper: TypeMapper` — Maps Sharpy types to C# types
- Various tracking fields for local scopes, variables, and type definitions

---

## Key Functions/Methods

### 1. `GenerateCompilationUnit(Module module)` → `CompilationUnitSyntax`

**Purpose**: Main entry point that orchestrates the entire compilation unit generation.

**Process**:
1. **Generate using directives** from import statements
2. **Filter out imports** to get non-import statements
3. **Collect from-import re-exports** (only for non-entry-point files)
4. **Generate module members** (module class + namespace-level types)
5. **Create namespace declaration** (block-scoped for C# 9.0 compatibility)
6. **Build compilation unit** with usings and namespace
7. **Add `#nullable enable` directive** at the top

**Key Decision**: Entry point files don't re-export symbols from imports (line 34). Only library modules generate delegating members for re-exported symbols.

**Output Structure**:
```csharp
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;
// ... more usings ...

namespace ProjectName.ModuleName
{
    public static class Exports
    {
        // Module-level functions, variables, etc.
    }

    // Namespace-level type declarations (siblings to Exports)
    public class MyClass { }
    public struct MyStruct { }
}
```

**Important Details**:

- **Re-export Logic (lines 30-40)**: Non-entry-point files (library modules) re-export symbols from `from ... import` statements. This allows multi-file projects where `package/__init__.py` re-exports symbols from submodules. Entry point files don't re-export because they're executable programs, not libraries meant to be imported by others.

- **Namespace-Level Types (lines 43-51)**: Types (classes, structs, interfaces, enums) are placed at namespace level as **siblings** to the module class, not nested inside it. This gives cleaner qualified names like `MyProject.Geometry.Point` instead of `MyProject.Geometry.Exports.Point`, matching C# conventions.

- **`#nullable enable` Pragma (lines 65-70)**: Added **after** `NormalizeWhitespace()` to preserve leading position. This aligns with Sharpy's "null-safe by default" design (Axiom 3: Type Safety). The pragma must be in leading trivia, so it can't be normalized away.

**Code Reference**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.CompilationUnit.cs:16`

---

### 2. `GenerateNamespaceName()` → `NameSyntax`

**Purpose**: Determine the C# namespace based on project structure, file path, and configuration.

**Decision Tree**:

1. **Project with root path** (multi-file project): Use `GenerateProjectNamespace()`
   - Requires: `ProjectNamespace`, `ProjectRootPath`, and `SourceFilePath` all set
2. **Project namespace only** (single-file with explicit namespace): Use namespace + filename
   - Requires: `ProjectNamespace` set, but not project root
3. **No project context**: Fallback to simple file-based namespace
   - Uses just the filename to avoid path issues

**Fallback Behavior**:
- If no source file path: `"SharpyGenerated"`
- If file path available: `"Sharpy.{FileName}"`

**Edge Case Handling (lines 99-116)**: Single-file compilation avoids numeric directories and special characters in paths by only using the filename, preventing issues like `Sharpy.123.MyModule` or problems with temporary directories.

**Why Multiple Paths?**
- **Multi-file projects** need relative namespaces (e.g., `MyProject.Utils.Helpers` from `src/utils/helpers.spy`)
- **Single-file compilation** with explicit namespace (e.g., `sharpyc --namespace MyApp file.spy`)
- **Fallback** for REPL, in-memory compilation, or ad-hoc scripts

**Code Reference**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.CompilationUnit.cs:73`

---

### 3. `GenerateProjectNamespace()` → `NameSyntax`

**Purpose**: Generate hierarchical namespace from project structure and file location.

**Algorithm**:
1. Start with `ProjectNamespace` (e.g., `"MyProject"`)
2. Calculate relative path from project root to source file
3. Extract directory path (without filename)
4. Split directory path into parts and convert to PascalCase (e.g., `"utils/helpers"` → `"Utils.Helpers"`)
5. Add filename as final component **except for `__init__.spy`** (which represents the package itself)
6. Join all parts with dots

**Example Transformations**:
```
Project: MyProject
File: src/lib/math/operations.spy
Result: MyProject.Lib.Math.Operations

Project: MyApp
File: src/utils/__init__.spy
Result: MyApp.Utils  (no __init__ suffix)

Project: Game
File: src/player.spy
Result: Game.Player
```

**Python Package Convention (lines 140-144)**: `__init__.spy` files don't add their filename to the namespace, as they represent the package directory itself, matching Python's `__init__.py` semantics.

**Name Mangling**: Uses `SimpleToPascalCase()` to convert snake_case → PascalCase:
- `my_utils` → `MyUtils`
- `data_loader` → `DataLoader`
- `http_client` → `HttpClient`

**Code Reference**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.CompilationUnit.cs:119`

---

### 4. `GenerateUsingDirectives(Module module)` → `List<UsingDirectiveSyntax>`

**Purpose**: Collect all C# `using` directives needed for the compilation unit.

**Default Usings** (always included):
- `System`
- `System.Collections.Generic`
- `System.Linq`
- `global::Sharpy.Core` (using `global::` to avoid conflicts when output namespace contains "Sharpy")

**Process**:
1. Add default usings
2. Process `import` statements → `GenerateImportUsings()`
3. Process `from ... import` statements → `GenerateFromImportUsings()`
4. **Deduplicate** by normalized string representation

**Why `global::Sharpy.Core`?** (line 159)
If the generated namespace is `Sharpy.MyModule`, a plain `using Sharpy.Core;` would be ambiguous—it could refer to a nested namespace `Sharpy.MyModule.Sharpy.Core` or the global `Sharpy.Core`. The `global::` prefix ensures we refer to the root `Sharpy.Core` namespace.

**Deduplication Logic (lines 174-184)**: Uses normalized whitespace string comparison. This is crucial because different import styles can produce identical using directives:
```python
import system.io
from system.io import File
# Both generate: using System.IO;
```

**Code Reference**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.CompilationUnit.cs:149`

---

### 5. `GenerateImportUsings(ImportStatement import)` → `IEnumerable<UsingDirectiveSyntax>`

**Purpose**: Convert Python-style `import` statements to C# using directives.

**Handles Two Categories**:

#### A. .NET Framework Imports
Detected by `IsNetFrameworkNamespace()` checking for prefixes like `system`, `microsoft`, etc.

**Without alias**:
```python
import system.io
# → using System.IO;
```

**With alias**:
```python
import system.io as io
# → using io = System.IO;
```

#### B. Sharpy Module Imports
User modules that expose members via the `Exports` class pattern.

**Without alias**:
```python
import config
# → using config = MyProject.Config.Exports;

import utils.helpers
# → using utils_helpers = MyProject.Utils.Helpers.Exports;
```

**With alias**:
```python
import utils.helpers as helpers
# → using helpers = MyProject.Utils.Helpers.Exports;
```

**Key Insights**:

1. **Exports Class Convention (lines 209-227)**: Sharpy modules expose their members via a static class named `Exports`, while .NET namespaces don't have this wrapper. The method uses `IsNetFrameworkNamespace()` to distinguish between the two (line 195).

2. **Name Sanitization (lines 238-264)**: For Sharpy modules without an alias, the module name is sanitized:
   - Replace dots with underscores: `"utils.helpers"` → `"utils_helpers"`
   - Escape C# keywords: `"base"` → `"@base"`

3. **Project Namespace Prefixing (lines 215-223)**: If a project namespace is configured, it's prepended to the module path. Falls back to just the module namespace for single-file compilation.

**Code Reference**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.CompilationUnit.cs:188`

---

### 6. `GenerateFromImportUsings(FromImportStatement fromImport)` → `IEnumerable<UsingDirectiveSyntax>`

**Purpose**: Convert Python `from ... import` statements to C# using directives.

**Handles Two Categories**:

#### A. .NET Framework Imports
```python
from system.io import File
# → using System.IO;
```

Just imports the namespace; individual symbols are available without qualification.

#### B. Sharpy Module Imports
```python
from config import MAX_SIZE
# → using static MyProject.Config.Exports;

from utils.math import calculate
# → using static MyProject.Utils.Math.Exports;
```

**Why `using static`?** (lines 283-316)
This enables direct access to static members without qualification:
```csharp
using static MyProject.Config.Exports;

var size = MAX_SIZE;  // Direct access, no prefix needed
var result = Calculate(42);  // Functions also directly accessible
```

**Relative Import Resolution (line 294)**: Uses `GetResolvedModulePath()` to handle relative imports:
```python
# In mypackage/submodule.py:
from .helpers import utility
# .helpers is resolved to mypackage.helpers during semantic analysis
# → using static MyProject.Mypackage.Helpers.Exports;

from ..parent import function
# ..parent is resolved to parent_package
# → using static MyProject.ParentPackage.Exports;
```

The `ResolvedModulePath` was computed during semantic analysis by the import resolver, which walks up the directory tree to resolve relative imports based on the package structure.

**Exports Class Pattern (lines 286-313)**: The module class is always named `Exports` (not the module name). For nested modules like `lib.math.operations`:
- Namespace: `TestProject.Lib.Math.Operations`
- Class: `Exports`
- Full path: `TestProject.Lib.Math.Operations.Exports`

**Code Reference**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.CompilationUnit.cs:270`

---

### 7. `IsNetFrameworkNamespace(string moduleName)` → `bool`

**Purpose**: Detect if a module name refers to a .NET framework namespace.

**Detection Strategy**: Check if the first part (before first dot) matches known .NET prefixes:
```csharp
var netPrefixes = new[] {
    "system", "microsoft", "windows", "xamarin", "mono", "netstandard"
};
```

**Why This Matters**: .NET namespaces are imported differently from Sharpy modules:
- **.NET**: `using System.IO;` (standard namespace import)
- **Sharpy**: `using static MyProject.Module.Exports;` (static class import)

**Examples**:
```python
import system.io           # .NET → true
import microsoft.extensions # .NET → true
import config              # Sharpy → false
import system_utils        # Sharpy → false (no dot before "system")
```

**Limitations**: A user module named `system.something` would incorrectly be treated as .NET. This is acceptable because:
1. It's bad practice to name modules after .NET namespaces
2. Such conflicts would fail at C# compilation time anyway
3. The alternative (maintaining a complete .NET namespace list) is impractical

**Code Reference**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.CompilationUnit.cs:324`

---

### 8. `IsConstantCaseName(string name)` → `bool`

**Purpose**: Determine if a name follows CONSTANT_CASE convention (ALL_CAPS with underscores).

**Rules**:
- Must contain at least one letter
- All letters must be uppercase
- Only uppercase letters, digits, and underscores allowed

**Algorithm**:
```csharp
foreach (char c in name)
{
    if (char.IsLetter(c))
    {
        if (!char.IsUpper(c))
            return false;  // Found lowercase letter
        hasLetter = true;
    }
    else if (!char.IsDigit(c) && c != '_')
    {
        return false;  // Invalid character
    }
}
return hasLetter;  // At least one letter required
```

**Examples**:
- `MAX_SIZE` → true
- `API_KEY` → true
- `HTTP_200_OK` → true
- `max_size` → false (lowercase)
- `MaxSize` → false (mixed case)
- `123_456` → false (no letters)

**Usage Context**: Used elsewhere in code generation to preserve casing for constants. Python constants are conventionally CONSTANT_CASE, and this should be preserved in C# rather than converting to PascalCase:
```python
# Python
MAX_SIZE = 1024

# Generated C# (correct)
public static readonly int MAX_SIZE = 1024;

# NOT this
public static readonly int MaxSize = 1024;
```

**Code Reference**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.CompilationUnit.cs:345`

---

### 9. `EscapeCSharpKeyword(string name)` → `string`

**Purpose**: Prefix C# keywords with `@` to make them valid identifiers.

**Examples**:
```python
import base  # "base" is a C# keyword
# → using @base = MyProject.Base.Exports;

import class  # "class" is a C# keyword
# → using @class = MyProject.Class.Exports;

import my_module  # Not a keyword
# → using my_module = MyProject.MyModule.Exports;
```

**Complete Keyword Set**: Includes all 85 C# keywords (lines 372-386):
- Flow control: `if`, `else`, `for`, `foreach`, `while`, `do`, `switch`, `case`, `break`, `continue`, `return`, `goto`
- Types: `bool`, `byte`, `char`, `decimal`, `double`, `float`, `int`, `long`, `object`, `string`, `void`, etc.
- Modifiers: `public`, `private`, `protected`, `internal`, `static`, `readonly`, `const`, `virtual`, `override`, etc.
- Special: `this`, `base`, `null`, `true`, `false`, `new`, `typeof`, `sizeof`, etc.

**Case Insensitive**: Uses `StringComparer.OrdinalIgnoreCase` so `Base`, `BASE`, and `base` are all escaped.

**Code Reference**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.CompilationUnit.cs:391`

---

## Dependencies

### External Dependencies
- **Microsoft.CodeAnalysis.CSharp**: Roslyn API for syntax tree construction
  - `SyntaxFactory`: Factory methods for creating syntax nodes
  - `CompilationUnitSyntax`, `NamespaceDeclaration`, `UsingDirective`, etc.
  - `SyntaxKind`: Enumeration of syntax node types (e.g., `StaticKeyword`)

### Internal Dependencies
- **`Sharpy.Compiler.Parser.Ast`**: AST node definitions
  - `Module`: Root node representing a Sharpy source file
  - `ImportStatement`: `import module [as alias]`
  - `FromImportStatement`: `from module import symbols`
  - `Statement`: Base class for all statements

- **`Sharpy.Compiler.Semantic`**: Semantic analysis results
  - `CodeGenContext`: Symbol table, project configuration, entry point flag
  - `SemanticBinding`: Import resolution data (resolved paths, re-exported symbols)
  - `SymbolTable`: Lookup table for symbols and types

### Other RoslynEmitter Partials
- **`RoslynEmitter.ModuleClass.cs`**: Provides `GenerateModuleMembers()` method (called at line 46)
  - Returns tuple of `(ClassDeclarationSyntax moduleClass, List<MemberDeclarationSyntax> namespaceTypes)`
  - Handles separation of module class from namespace-level types
  - Generates delegating members for re-exported symbols

### Helper Methods (from other partials)
- `SimpleToPascalCase(string)` — Converts snake_case to PascalCase
- `ConvertModuleNameToNamespace(string)` — Converts Python module path to C# namespace
- `GetResolvedModulePath(FromImportStatement)` — Gets resolved path for relative imports
- `HasReExportedSymbols(FromImportStatement)` — Checks if import has re-exports

---

## Patterns and Design Decisions

### 1. Partial Class Architecture
The `RoslynEmitter` is split across multiple files by responsibility:
- `CompilationUnit.cs` — Top-level structure (this file)
- `ModuleClass.cs` — Module class generation
- `Statements.cs` — Statement generation
- `Expressions.cs` — Expression generation
- `TypeDeclarations.cs` — Type definitions
- `Operators.cs` — Operator handling
- `ClassMembers.cs` — Class member generation

**Benefit**: Each file stays focused on a specific aspect of code generation, making the codebase more navigable. A developer working on expression generation doesn't need to understand namespace generation.

**Trade-off**: Must carefully manage shared state (fields in main `RoslynEmitter.cs`) and ensure methods are in the right file.

### 2. Roslyn SyntaxFactory Exclusively
All C# code is generated using Roslyn's `SyntaxFactory` methods (static import at line 7):
```csharp
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
```

**Never**: String concatenation or templates
```csharp
// ❌ FORBIDDEN - DO NOT DO THIS
var code = $"namespace {namespaceName} {{ ... }}";
```

**Always**: Type-safe syntax tree construction
```csharp
// ✅ CORRECT - Always use SyntaxFactory
var ns = NamespaceDeclaration(ParseName(namespaceName))
    .WithMembers(List(members));
```

**Why This Rule?**
1. **Type Safety**: Compiler catches errors instead of producing invalid C#
2. **Validation**: Roslyn validates syntax tree structure
3. **Formatting**: `NormalizeWhitespace()` produces consistent output
4. **Refactoring**: IDEs can refactor Roslyn trees, not string templates
5. **Maintainability**: Syntax errors caught at compile time, not runtime

### 3. Namespace-Level Types (Modern C# Convention)
Types are placed as **siblings** to the module class at namespace level (lines 43-51), not nested inside:

```csharp
namespace MyProject.Geometry
{
    public static class Exports { }  // Module class
    public class Point { }           // Type at namespace level ✅
    public struct Vector { }         // Type at namespace level ✅
}

// NOT this (old nested approach):
namespace MyProject.Geometry
{
    public static class Exports
    {
        public class Point { }  // ❌ Nested type
    }
}
```

**Benefits**:
- ✅ Cleaner qualified names: `MyProject.Geometry.Point`
- ❌ Avoids verbose names: `MyProject.Geometry.Exports.Point`
- ✅ Matches C# conventions and expectations
- ✅ Better IntelliSense and discoverability

**Implementation**: The `GenerateModuleMembers()` method (in `RoslynEmitter.ModuleClass.cs`) returns a tuple of `(moduleClass, namespaceTypes)` which are then combined as siblings.

### 4. C# 9.0 Compatibility (Unity Requirement)
Uses **block-scoped namespaces** (line 56) instead of file-scoped namespaces:

```csharp
// ✅ Used (C# 9.0 compatible)
namespace MyNamespace
{
    // members
}

// ❌ NOT used (requires C# 10+)
namespace MyNamespace;
// members
```

**Reason**: Unity 2021.2+ uses C# 9.0. Per CLAUDE.md: "C# 9.0 target — no global usings, file-scoped namespaces, or record structs."

**Other C# 9.0 Restrictions**:
- No `global using` directives
- No `record struct` (only `record class`)
- No natural-sized integers (`nint`, `nuint`)
- No lambda improvements (natural type, inferred return)

### 5. Null Safety by Default
The `#nullable enable` directive (line 68) enables C# nullable reference types for all generated code, aligning with Sharpy's "null-safe by default" design principle (Axiom 3).

**Why After NormalizeWhitespace?**
```csharp
// Build compilation unit first
var compilationUnit = CompilationUnit()
    .WithUsings(List(usingDirectives))
    .WithMembers(SingletonList<MemberDeclarationSyntax>(namespaceDecl))
    .NormalizeWhitespace();  // ← Normalize first

// Add #nullable enable directive to enable C# nullable reference types
// Must be added AFTER NormalizeWhitespace to preserve leading position
var nullablePragma = ParseLeadingTrivia("#nullable enable\n\n");

return compilationUnit.WithLeadingTrivia(nullablePragma);  // ← Add trivia after
```

If added before normalization, the trivia might be removed or reformatted incorrectly.

### 6. Entry Point Special Handling
Entry point files (files that generate a `Main` method) don't re-export symbols from imports (line 34):

```csharp
// Only generate re-export members for non-entry-point files
if (!_context.IsEntryPoint)
{
    fromImports = module.Body
        .OfType<FromImportStatement>()
        .Where(f => HasReExportedSymbols(f))
        .ToList();
}
```

**Rationale**:
- **Entry points** (programs) consume APIs but don't expose them
- **Library modules** need re-export delegation so `from package import X` works
- Reduces generated code size for entry points

**Example**:
```python
# main.spy (entry point) - NO re-exports generated
from config import MAX_SIZE
print(MAX_SIZE)

# __init__.spy (library) - re-exports generated
from .config import MAX_SIZE  # This symbol is re-exported
```

### 7. Path-Based Namespace Generation
Namespace hierarchy mirrors the physical directory structure:
```
src/utils/string_helpers.spy → MyProject.Utils.StringHelpers
src/__init__.spy → MyProject (package root)
src/lib/math/operations.spy → MyProject.Lib.Math.Operations
```

This follows Python's package structure conventions while producing idiomatic C# namespaces.

**Directory to Namespace Mapping**:
- Directory separators (`/` or `\`) → namespace dots (`.`)
- snake_case → PascalCase
- `__init__.spy` → omitted from namespace (represents package itself)

### 8. Exports Class Convention
Sharpy modules expose members via a static class named `Exports`:

**Sharpy code**:
```python
# config.spy
MAX_SIZE = 1024
def get_config(): ...
```

**Generated C#**:
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

The alternative would be to expose everything at namespace level, but C# doesn't allow top-level functions or variables outside of classes (until C# 9 top-level statements, which only work in entry points).

---

## Debugging Tips

### 1. Use the `emit` Command
To inspect what this file generates:
```bash
dotnet run --project src/Sharpy.Cli -- emit csharp myfile.spy
```

This shows the full C# output, including namespaces and using directives. You can pipe it to a file for easier inspection:
```bash
dotnet run --project src/Sharpy.Cli -- emit csharp myfile.spy > output.cs
```

### 2. Check Namespace Generation
If namespace names look wrong, trace through:
1. Is `_context.ProjectNamespace` set correctly?
2. Is `_context.ProjectRootPath` pointing to the right directory?
3. Is `_context.SourceFilePath` the absolute path to the `.spy` file?

Add breakpoints in:
- `GenerateNamespaceName()` (line 73)
- `GenerateProjectNamespace()` (line 119)

Inspect these values to understand which code path is being taken.

**Common Issues**:
- Missing `ProjectRootPath` → Falls back to simple namespace
- Wrong relative path calculation → Namespace has extra or missing components
- Path separator issues on Windows → Check line 134 handles both `/` and `\`

### 3. Import Issues
If imports aren't working:

**For .NET imports**:
- Check `IsNetFrameworkNamespace()` — is it correctly identifying .NET vs. Sharpy modules?
- Verify the namespace conversion is correct (e.g., `system.io` → `System.IO`)

**For Sharpy module imports**:
- Inspect `GenerateImportUsings()` output — are aliases being generated correctly?
- Is the `Exports` suffix being added?
- Is the project namespace being prepended?

**For relative imports**:
- Verify `GetResolvedModulePath()` is returning the right path for relative imports
- Check semantic analysis ran and populated `ResolvedModulePath`
- Look at `_context.SemanticBinding` — does it contain import resolution data?

### 4. Duplicate Using Directives
The deduplication logic (lines 174-184) normalizes whitespace before comparing. If you see duplicates in output:
- Check if `NormalizeWhitespace()` is producing consistent formatting
- Look for inconsistencies in `ParseName()` calls
- Verify the normalized string comparison is working

**Debug Strategy**:
```csharp
// Add logging before deduplication
foreach (var u in usings)
{
    var key = u.NormalizeWhitespace().ToFullString();
    Console.WriteLine($"Using: {key}");
}
```

### 5. Missing `#nullable enable`
If the nullable directive isn't appearing:
- Verify it's being added **after** `NormalizeWhitespace()` (line 68-70)
- Check that `ParseLeadingTrivia()` is creating the trivia correctly
- Inspect the final syntax tree in the debugger

### 6. Symbol Resolution Failures
If symbols from imports aren't resolving:
- Verify that semantic analysis has run before code generation
- Check `_context.SemanticBinding` — does it contain import resolution data?
- For Sharpy modules, ensure the `Exports` class suffix is being added
- For `from` imports, verify `using static` is being generated

### 7. Keyword Conflicts
If seeing C# compilation errors about keywords:
- Check if `EscapeCSharpKeyword()` is being called on aliases
- Verify the `CSharpKeywords` set includes the problematic keyword
- Look for places where names are used without escaping

---

## Contribution Guidelines

### When to Modify This File

**Add to this file when**:
1. Changing how namespaces are generated from file paths
2. Adding new default using directives (e.g., new runtime library namespaces)
3. Modifying import statement handling (new import syntax)
4. Changing compilation unit structure (new top-level directives)
5. Adding support for new .NET framework prefixes
6. Changing how entry points vs. library modules are handled

**Don't modify this file when**:
- Adding new statement types → use `RoslynEmitter.Statements.cs`
- Adding new expression types → use `RoslynEmitter.Expressions.cs`
- Changing how class members are generated → use `RoslynEmitter.ClassMembers.cs`
- Modifying type declarations → use `RoslynEmitter.TypeDeclarations.cs`
- Changing type mapping → use `TypeMapper.cs`

### Code Style Guidelines

1. **Always use SyntaxFactory**: No string templates or concatenation
   ```csharp
   // ✅ Good
   var ns = NamespaceDeclaration(ParseName(namespaceName));

   // ❌ Bad
   var code = $"namespace {namespaceName} {{ }}";
   ```

2. **Method naming**: Use `Generate*` prefix for methods that create syntax nodes
   ```csharp
   private NameSyntax GenerateNamespaceName() { ... }
   private List<UsingDirectiveSyntax> GenerateUsingDirectives() { ... }
   ```

3. **Comments**: Explain **why**, not just **what**
   ```csharp
   // ✅ Good: Explains rationale
   // Must be added AFTER NormalizeWhitespace to preserve leading position

   // ❌ Bad: Just restates code
   // Add nullable pragma
   ```

4. **Keep methods focused**: Each method should do one thing well
   - `GenerateCompilationUnit()` orchestrates
   - Helper methods handle specific concerns
   - Single Responsibility Principle

5. **Use descriptive variable names**:
   ```csharp
   // ✅ Good
   var namespaceName = GenerateNamespaceName();
   var usingDirectives = GenerateUsingDirectives(module);

   // ❌ Bad
   var ns = GenerateNamespaceName();
   var usings = GenerateUsingDirectives(module);
   ```

### Testing Changes

When modifying this file:

1. **Unit tests**: Add tests in `Sharpy.Compiler.Tests/CodeGen/` for specific behaviors
   ```bash
   dotnet test --filter "FullyQualifiedName~CodeGen"
   ```

2. **Integration tests**: Use file-based tests in `TestFixtures/`:
   ```
   import_behavior.spy
   import_behavior.expected
   ```

3. **Test different scenarios**:
   - Single-file compilation (no project)
   - Multi-file project compilation
   - Entry point vs. library modules
   - .NET framework imports vs. Sharpy module imports
   - Relative imports (`from .module import ...`)
   - Reserved keyword handling
   - `__init__.spy` files
   - Nested directory structures

4. **Verify output**:
   ```bash
   dotnet test --filter "FullyQualifiedName~CodeGen"
   dotnet run --project src/Sharpy.Cli -- emit csharp test_file.spy
   dotnet run --project src/Sharpy.Cli -- run test_file.spy
   ```

5. **Check edge cases**:
   - Empty files
   - Files with only imports
   - Circular imports (should be caught earlier, but verify)
   - Very long namespaces
   - Special characters in file paths
   - Windows vs. Unix path separators

### Common Pitfalls

1. **Don't break C# 9.0 compatibility**:
   - Avoid file-scoped namespaces (`namespace Foo;`)
   - Avoid global usings (`global using System;`)
   - Avoid record structs (`record struct Point { }`)
   - Test in Unity if changing generated C# patterns

2. **Preserve deduplication**: When adding using directives, ensure deduplication still works
   - Normalized strings must be compared
   - Order shouldn't affect deduplication

3. **Test edge cases**: Empty files, files with only imports, `__init__.spy` files
   - Don't assume files have content
   - Handle missing or null paths gracefully

4. **Keyword escaping**: Always run import aliases through `EscapeCSharpKeyword()`
   - Easy to forget when adding new code paths
   - C# compilation will fail if missed

5. **Path separators**: Remember Windows uses `\` and Unix uses `/`
   - Use `Path.GetDirectoryName()`, `Path.GetRelativePath()`, etc.
   - Don't hardcode separators

6. **Null checks**: Always validate `_context` properties before use
   - `ProjectNamespace`, `ProjectRootPath`, `SourceFilePath` can be null
   - Have sensible fallbacks

### Performance Considerations

- Using deduplication is O(n) on number of imports (acceptable for typical files)
- If adding expensive checks, cache results in `_context`
- `IsNetFrameworkNamespace()` string prefix check is fast, don't over-optimize
- Avoid LINQ in hot paths if generating thousands of files (but likely premature optimization)

### C# Target Version Compatibility

- **NEVER** use C# 10+ features (file-scoped namespaces, global usings)
- **NEVER** use C# 11+ features (required members, UTF-8 strings, etc.)
- Test in Unity if changing generated C# patterns
- Stick to Roslyn APIs available in .NET SDK 6.0+ (Sharpy's minimum)
- Check Unity's C# version support before using new C# features

### Code Generation Axioms

When making changes, respect the axiom hierarchy (from CLAUDE.md):

1. **.NET Compatibility** (highest) - Generated C# must compile and run on .NET/Unity
2. **Type Safety** - Maintain null-safety, strong typing
3. **Python Syntax Fidelity** (lowest) - Match Python semantics where possible

**Example**: If Python import semantics conflict with C# namespace rules, C# wins.

---

## Cross-References

### Related Partial Class Files

This file is part of `RoslynEmitter` partial class. Related files:

- **[RoslynEmitter.cs](./RoslynEmitter.md)** — Main class definition, fields, constructor, helper methods
- **[RoslynEmitter.ModuleClass.cs](./RoslynEmitter.ModuleClass.md)** — Generates the `Exports` class and separates namespace-level types (called at line 46)
- **[RoslynEmitter.Statements.cs](./RoslynEmitter.Statements.md)** — Statement code generation
- **[RoslynEmitter.Expressions.cs](./RoslynEmitter.Expressions.md)** — Expression code generation
- **[RoslynEmitter.TypeDeclarations.cs](./RoslynEmitter.TypeDeclarations.md)** — Class, struct, interface, enum generation
- **[RoslynEmitter.ClassMembers.cs](./RoslynEmitter.ClassMembers.md)** — Method, property, field generation
- **[RoslynEmitter.Operators.cs](./RoslynEmitter.Operators.md)** — Operator overload generation

### Related Types

- **[CodeGenContext.md](./CodeGenContext.md)** — Context object with configuration (`src/Sharpy.Compiler/CodeGen/CodeGenContext.cs`)
- **[TypeMapper.md](./TypeMapper.md)** — Sharpy type → C# type mapping (`src/Sharpy.Compiler/CodeGen/TypeMapper.cs`)
- **SemanticBinding** (`src/Sharpy.Compiler/Semantic/SemanticBinding.cs`) — Import resolution data
- **Module**, **ImportStatement**, **FromImportStatement** (`src/Sharpy.Compiler/Parser/Ast/`) — AST nodes

### Relevant Specifications

- **`docs/language_specification/dotnet_interop.md`** — How Sharpy interoperates with .NET
- **`docs/language_specification/modules_and_imports.md`** — Module system and import semantics (if exists)
- **`docs/language_specification/type_system.md`** — Type system and null safety (if exists)

---

## Summary

This file is the **foundation** of C# code generation, setting up the top-level structure that all other RoslynEmitter components build upon. It bridges Sharpy's Python-like module system with C#'s namespace and using directive system, handling the critical distinction between .NET framework imports and Sharpy user modules.

**Key Takeaways**:
- **Entry point**: `GenerateCompilationUnit()` produces the root `CompilationUnitSyntax`
- **Import handling**: Sharpy modules use `Exports` class convention with `using static`
- **Namespace generation**: Multi-mode support (project, single-file, fallback)
- **Null-safety**: Always includes `#nullable enable` pragma
- **Compatibility**: C# 9.0 compatible (Unity support)
- **Type placement**: Namespace-level types as siblings, not nested

**When debugging code generation issues, start here** — most problems with imports, namespaces, or overall structure trace back to this file.
