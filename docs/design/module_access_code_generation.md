# Module Access Code Generation Strategy

**Task:** 0.1.10.CG2 - Define C# Emission Strategy for Module Access
**Date:** 2026-01-16
**Status:** Design Complete

## Executive Summary

This document defines how Sharpy module imports and member access should be emitted to C# code. The strategy is based on analysis of the existing implementation and follows Sharpy's philosophy of ".NET first, Pythonic second."

**Key Decision:** Use **fully-qualified names with `using` aliases** for module access, leveraging C#'s type system while maintaining Python-like import semantics.

## Background

### Current Implementation

Based on analysis of `RoslynEmitter.cs` (lines 245-339, 3017-3071):

1. **Import Statement Handling:**
   - `.NET framework imports`: `import system.io` → `using System.IO;`
   - **Sharpy module imports**: `import utils.helpers` → `using utils_helpers = Utils.Helpers.Exports;`
   - **Aliased imports**: `import module as alias` → `using alias = Module.Exports;`

2. **From-Import Handling:**
   - `.NET framework**: `from system.io import File` → `using System.IO;`
   - **Sharpy modules**: `from utils.helpers import format_text` → `using static Utils.Helpers.Exports;`

3. **Member Access:**
   - Applies name mangling: `obj.member` → `obj.Member` (PascalCase)
   - Handles enum member access specially
   - Supports null-conditional: `obj?.member` → `obj?.Member`

### Language Specification

From `docs/language_specification/`:

- **import_statements.md**: Imports map to C# `using` directives
- **module_resolution.md**: Module names convert `snake_case` → `PascalCase`
- **name_mangling.md**: Members use PascalCase in generated C#
- **module_system.md**: Modules are directories with optional `__init__.spy`

## Design Decision: Fully Qualified Names with Exports Class

### Option A: Fully Qualified Names (SELECTED)

This is the **current implementation** and the **recommended approach**.

#### Example 1: Basic Import

**Sharpy Code:**
```python
# config.spy
MAX_SIZE: int = 100
MIN_SIZE: int = 10

# main.spy
import config
x = config.MAX_SIZE
```

**Generated C#:**
```csharp
// Config.cs
namespace MyProject
{
    public static class Config
    {
        public static class Exports
        {
            public static int MaxSize = 100;
            public static int MinSize = 10;
        }
    }
}

// Main.cs
using config = MyProject.Config.Exports;

namespace MyProject
{
    public static class Main
    {
        public static class Exports
        {
            public static void __init__()
            {
                var x = config.MaxSize;
            }
        }
    }
}
```

#### Example 2: Aliased Import

**Sharpy Code:**
```python
import config as cfg
x = cfg.MAX_SIZE
```

**Generated C#:**
```csharp
using cfg = MyProject.Config.Exports;

namespace MyProject
{
    public static class Main
    {
        public static class Exports
        {
            public static void __init__()
            {
                var x = cfg.MaxSize;
            }
        }
    }
}
```

#### Example 3: From-Import

**Sharpy Code:**
```python
from config import MAX_SIZE, MIN_SIZE
x = MAX_SIZE
```

**Generated C#:**
```csharp
using static MyProject.Config.Exports;

namespace MyProject
{
    public static class Main
    {
        public static class Exports
        {
            public static void __init__()
            {
                var x = MaxSize;  // Direct access via using static
            }
        }
    }
}
```

#### Example 4: Nested Module Import

**Sharpy Code:**
```python
import lib.math
result = lib.math.add(5, 3)
```

**Generated C#:**
```csharp
using lib_math = MyProject.Lib.Math.Exports;

namespace MyProject
{
    public static class Main
    {
        public static class Exports
        {
            public static void __init__()
            {
                var result = lib_math.Add(5, 3);
            }
        }
    }
}
```

#### Example 5: Module with Functions and Variables

**Sharpy Code:**
```python
# utils.spy
VERSION: str = "1.0.0"

def get_version() -> str:
    return VERSION

# main.spy
import utils
print(utils.VERSION)
print(utils.get_version())
```

**Generated C#:**
```csharp
// Utils.cs
namespace MyProject
{
    public static class Utils
    {
        public static class Exports
        {
            public static string Version = "1.0.0";

            public static string GetVersion()
            {
                return Version;
            }
        }
    }
}

// Main.cs
using utils = MyProject.Utils.Exports;

namespace MyProject
{
    public static class Main
    {
        public static class Exports
        {
            public static void __init__()
            {
                Console.WriteLine(utils.Version);
                Console.WriteLine(utils.GetVersion());
            }
        }
    }
}
```

### Why This Approach?

**Advantages:**

1. ✅ **Already Implemented**: This is the current code generation strategy
2. ✅ **Clear Separation**: `Exports` class clearly delineates public API
3. ✅ **Type Safety**: Full compile-time resolution of module members
4. ✅ **IDE Support**: IntelliSense works perfectly with static classes
5. ✅ **No Ambiguity**: Module access is always `moduleName.member`
6. ✅ **Namespace Organization**: Each module gets its own namespace
7. ✅ **Compatible with .NET**: Interops well with .NET assemblies
8. ✅ **Handles Nesting**: `lib.math` → `Lib.Math.Exports` is straightforward

**Trade-offs:**

- ⚠️ Requires `Exports` nested class (minor verbosity in generated code)
- ⚠️ Module alias uses underscore when dotted: `lib.math` → `lib_math`

### Alternative: Direct Namespace Access (REJECTED)

**Why Rejected:**

```python
import config
x = config.MAX_SIZE
```

Would generate:
```csharp
using config = MyProject.Config;  // No .Exports
var x = config.MaxSize;
```

**Problems:**
- Conflicts with C# namespace system (can't alias a namespace to access static members)
- Doesn't support module-level variables cleanly
- Mixing namespace and type concepts

## Implementation Details

### Module Structure

Each Sharpy module file generates:

```csharp
namespace <ProjectNamespace>.<ModulePath>
{
    public static class <ModuleName>
    {
        public static class Exports
        {
            // All public module-level declarations
        }
    }
}
```

### Import Translation Rules

| Sharpy Import | C# Using Directive | Access Pattern |
|---------------|-------------------|----------------|
| `import module` | `using module = Namespace.Module.Exports;` | `module.Member` |
| `import module as alias` | `using alias = Namespace.Module.Exports;` | `alias.Member` |
| `import a.b.c` | `using a_b_c = Namespace.A.B.C.Exports;` | `a_b_c.Member` |
| `from module import X` | `using static Namespace.Module.Exports;` | `X` (direct) |
| `from module import X as Y` | `using Y = Namespace.Module.Exports.X;` | `Y` (type alias) |

### Name Mangling

All member access follows standard Sharpy name mangling:

- Module names: `snake_case` → `PascalCase`
- Member names: `snake_case` → `PascalCase`
- Constants: `CAPS_SNAKE` → `PascalCase`

Examples:
- `config.MAX_SIZE` → `config.MaxSize`
- `lib.math.calculate_sum` → `lib_math.CalculateSum`
- `utils.helpers.format_text` → `utils_helpers.FormatText`

### Special Cases

#### .NET Framework Imports

Framework namespaces are detected and handled differently:

```python
import system.io
from system.io import File
```

```csharp
using System.IO;  // Direct namespace, no .Exports
```

Framework detection (see `RoslynEmitter.cs:309-324`):
- `system.*`, `microsoft.*`, `windows.*`, `xamarin.*`, `mono.*`, `netstandard.*`

#### Package Imports with `__init__.spy`

```
mypackage/
    __init__.spy
    module_a.spy
    module_b.spy
```

```python
# __init__.spy
from mypackage.module_a import func_a
from mypackage.module_b import func_b
```

```python
# main.spy
import mypackage
mypackage.func_a()  # Re-exported from module_a
```

Generated:
```csharp
// Mypackage/__init__.cs
using static MyProject.Mypackage.ModuleA.Exports;
using static MyProject.Mypackage.ModuleB.Exports;

namespace MyProject.Mypackage
{
    public static class Mypackage
    {
        public static class Exports
        {
            // Re-exported symbols appear here
            public static readonly Func<...> func_a = ModuleA.Exports.FuncA;
            public static readonly Func<...> func_b = ModuleB.Exports.FuncB;
        }
    }
}
```

## Code Generation Algorithm

### Step 1: Generate Using Directives

For each `ImportStatement`:
```csharp
if (IsNetFrameworkNamespace(moduleName))
{
    // using System.IO;
    Generate using directive for namespace
}
else
{
    // using alias = Namespace.Module.Exports;
    var alias = asName ?? moduleName.Replace(".", "_");
    var nsName = ConvertModuleNameToNamespace(moduleName);
    Generate using alias = nsName + ".Exports";
}
```

For each `FromImportStatement`:
```csharp
if (IsNetFrameworkNamespace(moduleName))
{
    // using System.IO;
    Generate using directive for namespace
}
else if (importAll || multipleSymbols)
{
    // using static Namespace.Module.Exports;
    var nsName = ConvertModuleNameToNamespace(moduleName);
    Generate using static nsName + ".Exports";
}
else
{
    // Individual type alias (if needed)
    // Usually using static is sufficient
}
```

### Step 2: Generate Module Class

```csharp
namespace <CalculatedNamespace>
{
    public static class <ModuleName>
    {
        public static class Exports
        {
            // Generate all module-level declarations:
            // - public static fields for module variables
            // - public static methods for module functions
            // - public classes/structs/enums for type definitions
        }
    }
}
```

### Step 3: Generate Member Access

In `GenerateMemberAccess()`:

1. Check if object is a module symbol
2. Apply name mangling to member name
3. Generate: `moduleAlias.MangledMember`

**Current Implementation** (RoslynEmitter.cs:3017-3071):
- Already applies `NameMangler.ToPascalCase()` to member names
- Handles special cases (enums, null-conditional)
- No module-specific logic needed (works via using aliases)

## Testing Strategy

### Unit Tests Required

1. **Import Code Generation** (`RoslynEmitterModuleTests.cs` - EXISTING)
   - ✅ Basic import → using alias
   - ✅ Aliased import → using with custom alias
   - ✅ Nested import → using with underscore alias
   - ✅ .NET framework import → using namespace

2. **From-Import Code Generation** (`RoslynEmitterModuleTests.cs` - EXISTING)
   - ✅ From-import single → using static
   - ✅ From-import multiple → using static
   - ✅ From-import * → using static

3. **Member Access Code Generation** (`RoslynEmitterExpressionTests.cs` - EXISTING)
   - ✅ `obj.member` → `obj.Member` (name mangling)
   - ✅ Null-conditional access

4. **Module Structure Generation** (NEW TESTS NEEDED)
   - Module generates static class with Exports
   - Module-level variables generate static fields
   - Module-level functions generate static methods

### Integration Tests Required

1. **Basic Import** (`Phase0110IntegrationTests.cs`)
   - Import module, access variable
   - Import module, call function

2. **Nested Import**
   - Import lib.math, access member

3. **From-Import**
   - From-import symbol, use directly

4. **Package Import**
   - Import package with __init__.spy

## Migration Path

### Current State (✅ ALREADY IMPLEMENTED)

The described strategy is **already implemented** in:
- `RoslynEmitter.cs:245-339` - Import to using directive conversion
- `RoslynEmitter.cs:3017-3071` - Member access with name mangling
- Tests exist in `RoslynEmitterModuleTests.cs`

### What Needs to be Verified

1. ✅ Module-level variables generate as static fields in Exports class
2. ✅ Module-level functions generate as static methods in Exports class
3. ✅ Member access through module aliases works correctly
4. ⚠️ Nested module access (`lib.math.func()`) - needs verification
5. ⚠️ Package imports with `__init__.spy` - needs implementation review

### What Needs to be Added/Fixed

Based on task 0.1.10.CG3-CG7:

1. **Verify Module Variable Generation** (CG3)
   - Ensure module-level variables emit as static fields in Exports

2. **Nested Module Access** (CG4)
   - Verify `lib.math.add()` works with alias `lib_math.Add()`

3. **From-Import Edge Cases** (CG5)
   - Symbol aliasing: `from module import X as Y`

4. **Multiple Entry Points Fix** (CG6)
   - Only entry point file should generate Main()

5. **Integration Tests** (CG7)
   - Complete test coverage for all scenarios

## Rationale Summary

**Why this approach is correct for Sharpy:**

1. **Aligns with .NET Conventions**: Uses static classes, which is idiomatic C#
2. **Type Safety**: Full compile-time resolution
3. **Already Working**: Existing implementation proves viability
4. **Clear API Boundary**: `Exports` class clearly marks public interface
5. **Tooling Support**: Works perfectly with C# IDE tools
6. **Extensible**: Easy to add features like module initialization, private members

**What makes this "Pythonic":**

1. Import syntax matches Python exactly
2. Module access feels like Python: `module.member`
3. From-imports enable direct name usage
4. Package structure mirrors Python conventions

**What makes this ".NET first":**

1. Generates idiomatic C# static classes
2. Uses C# namespace system properly
3. Leverages `using` directives for aliasing
4. Compatible with .NET type system and reflection

## Conclusion

**Decision: Adopt Option A - Fully Qualified Names with Exports Class**

This strategy:
- ✅ Is already implemented and working
- ✅ Follows Sharpy's ".NET first, Pythonic second" philosophy
- ✅ Provides type safety and IDE support
- ✅ Has a clear migration path (mostly verification)

**Next Steps:**

1. Document this decision ✅ (this document)
2. Verify current implementation completeness (Task CG3)
3. Fix any gaps in nested module access (Task CG4)
4. Complete integration tests (Task CG7)

---

**References:**

- `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs:245-339` - Import handling
- `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs:3017-3071` - Member access
- `docs/language_specification/import_statements.md`
- `docs/language_specification/module_system.md`
- `docs/language_specification/name_mangling.md`
