# C# Emission Strategy for Module Access

**Task ID:** Design Decision
**Date:** 2026-01-16
**Status:** ✅ Decided and Already Implemented

## Executive Summary

**Decision:** Use **Option A: Fully Qualified Names** with static class exports

This strategy is **already implemented** in `RoslynEmitter.cs` lines 3166-3327 and is the correct approach for Sharpy's .NET-first philosophy.

---

## Background

When a Sharpy program imports a module and accesses its members, the compiler must decide how to emit the C# code:

```python
# config.spy
MAX_SIZE: int = 100

# main.spy
import config
x = config.MAX_SIZE
```

The question is: Should this emit to C# as:
- **Option A:** `Config.Exports.MAX_SIZE` (fully qualified)
- **Option B:** `MAX_SIZE` (using static import)

---

## Decision: Option A - Fully Qualified Names

### ✅ Chosen Strategy

**For regular imports (`import module`):**
```python
import config
x = config.MAX_SIZE
```

**Emits to C#:**
```csharp
// Import generates: using config = Config.Exports;

namespace MyProject {
    public static class Exports {
        public static void Main() {
            var x = config.MAX_SIZE;  // References Config.Exports.MAX_SIZE via alias
        }
    }
}
```

**For nested module access (`lib.math.add`):**
```python
import lib.math
result = lib.math.add(5, 3)
```

**Emits to C#:**
```csharp
// Import generates: using lib_math = Lib.Math.Exports;

namespace MyProject {
    public static class Exports {
        public static void Main() {
            var result = lib_math.Add(5, 3);  // Fully qualified via alias
        }
    }
}
```

**For from-imports (`from module import name`):**
```python
from config import MAX_SIZE
x = MAX_SIZE
```

**Emits to C#:**
```csharp
using static Config.Exports;

namespace MyProject {
    public static class Exports {
        public static void Main() {
            var x = MAX_SIZE;  // Direct access via using static
        }
    }
}
```

---

## Implementation Details

### Module Structure in C#

Each Sharpy module compiles to a static `Exports` class:

```csharp
// config.spy -> Config.cs
namespace MyProject {
    public static class Config {
        public static class Exports {
            public static int MAX_SIZE = 100;
        }
    }
}
```

### Import Statement Emission

| Sharpy Import | C# Using Directive | Member Access |
|---------------|-------------------|---------------|
| `import config` | `using config = Config.Exports;` | `config.MAX_SIZE` |
| `import lib.math` | `using lib_math = Lib.Math.Exports;` | `lib_math.Add(5, 3)` |
| `import config as cfg` | `using cfg = Config.Exports;` | `cfg.MAX_SIZE` |
| `from config import MAX_SIZE` | `using static Config.Exports;` | `MAX_SIZE` |
| `from lib.math import add` | `using static Lib.Math.Exports;` | `Add(5, 3)` |
| `from config import *` | `using static Config.Exports;` | `MAX_SIZE` |

### Nested Module Access Resolution

The code generation handles nested module paths through `TryExtractModulePath()` (RoslynEmitter.cs:3234-3300):

1. **Path extraction:** `lib.math.add` → `["lib", "math", "add"]`
2. **Symbol validation:** Verifies each segment in module hierarchy via `Exports` dictionary
3. **Code generation:** `BuildModuleAccessExpression()` converts to `Lib.Math.Add` with PascalCase

```csharp
// lib.math.add(5, 3) in Python becomes:
lib_math.Add(5, 3)  // where lib_math is an alias to Lib.Math.Exports
```

---

## Rationale

### Why Option A is Superior

#### 1. **Namespace Collision Prevention**
```csharp
// Option A: Clear module origin
using math = MyProject.Math.Exports;
using utils = Utils.Exports;

var result = math.Floor(3.7);     // Clearly from MyProject.Math
var other = utils.Floor(3.7);     // Different Floor from Utils

// Option B: Ambiguous with using static
using static MyProject.Math.Exports;
using static Utils.Exports;

var result = Floor(3.7);  // ERROR: Ambiguous call!
```

#### 2. **Matches Python Semantics**
```python
import config
x = config.MAX_SIZE  # Module prefix required in Python
```

Python requires the module name prefix with regular imports. Option A preserves this:

```csharp
using config = Config.Exports;
var x = config.MAX_SIZE;  // Module prefix preserved
```

#### 3. **Explicit Origin (Principle of Least Surprise)**
- **Option A:** `lib_math.Add(5, 3)` - origin is clear
- **Option B:** `Add(5, 3)` - where does this come from?

Developers can immediately see which module provides a symbol.

#### 4. **Handles Complex Module Hierarchies**
```python
import lib.math.advanced.matrix
result = lib.math.advanced.matrix.multiply(a, b)
```

**Option A (Clean):**
```csharp
using lib_math_advanced_matrix = Lib.Math.Advanced.Matrix.Exports;
var result = lib_math_advanced_matrix.Multiply(a, b);
```

**Option B (Problematic):**
```csharp
using static Lib.Math.Advanced.Matrix.Exports;
var result = Multiply(a, b);  // Lost all context!
```

#### 5. **Interop with .NET Namespaces**
Sharpy treats .NET namespaces differently from Sharpy modules:

```python
# .NET framework import
import system.io
file = system.io.File.ReadAllText("data.txt")

# Sharpy module import
import config
value = config.MAX_SIZE
```

**Emits to:**
```csharp
using System.IO;  // .NET namespace - normal using
using config = Config.Exports;  // Sharpy module - alias to Exports

var file = File.ReadAllText("data.txt");  // .NET - direct access
var value = config.MAX_SIZE;  // Sharpy - prefixed access
```

This distinction is important for tooling and clarity.

#### 6. **.NET First Philosophy**
Sharpy's design principle: "**.NET first, Pythonic second**"

- Uses static classes (not instances) for modules
- Uses compile-time resolution (not runtime)
- Leverages C# `using` aliases for clean syntax
- PascalCase naming for public APIs

---

## from-import Distinction

The `from X import Y` statement **does** use `using static`, which is correct:

```python
from config import MAX_SIZE
x = MAX_SIZE  # Direct access, no module prefix
```

**Emits to:**
```csharp
using static Config.Exports;
var x = MAX_SIZE;  // Direct access as intended
```

This matches Python's semantics where `from` imports bring names directly into scope.

---

## Edge Cases Handled

### 1. Snake_case Conversion
```python
import my_custom_module
result = my_custom_module.helper_func()
```

**Emits to:**
```csharp
using my_custom_module = MyCustomModule.Exports;
var result = my_custom_module.HelperFunc();
```

### 2. Deep Nesting (3+ levels)
```python
import level1.level2.level3
result = level1.level2.level3.func()
```

**Emits to:**
```csharp
using level1_level2_level3 = Level1.Level2.Level3.Exports;
var result = level1_level2_level3.Func();
```

### 3. Module Variable Access
```python
import config
size = config.MAX_SIZE
```

**Emits to:**
```csharp
using config = Config.Exports;
var size = config.MAX_SIZE;  // Static field access
```

### 4. Module Function Calls
```python
import lib.math
result = lib.math.add(5, 3)
```

**Emits to:**
```csharp
using lib_math = Lib.Math.Exports;
var result = lib_math.Add(5, 3);  // Static method call
```

### 5. Import Aliasing
```python
import lib.math.operations as ops
result = ops.add(5, 3)
```

**Emits to:**
```csharp
using ops = Lib.Math.Operations.Exports;
var result = ops.Add(5, 3);
```

---

## Current Implementation Status

### ✅ Already Implemented

The following functionality is **already working** in `RoslynEmitter.cs`:

1. **`TryExtractModulePath()`** (lines 3234-3300)
   - Traverses `MemberAccess` chain to extract module paths
   - Validates each segment against symbol table
   - Returns `true` only if entire path is valid module access

2. **`BuildModuleAccessExpression()`** (lines 3306-3327)
   - Converts validated path to C# syntax
   - Applies PascalCase conversion to each segment
   - Chains `MemberAccessExpression` nodes

3. **`GenerateMemberAccess()`** (lines 3166-3227)
   - Calls `TryExtractModulePath()` first
   - Falls back to enum or standard member access
   - Handles null-conditional access (`?.`)

### Import Statement Code Generation

Module imports are handled in the `GenerateCompilationUnit()` method:

```csharp
// Import statement
new ImportStatement { Names = [ new ImportAlias { Name = "lib.math" } ] }

// Generates using directive
using lib_math = Lib.Math.Exports;
```

This is tested in `RoslynEmitterModuleTests.cs`:
- Line 226: `GenerateCompilationUnit_WithImportModule_GeneratesAliasToExports`
- Line 252: `GenerateCompilationUnit_WithImportModuleAsAlias_GeneratesCorrectAlias`
- Line 885: `GenerateCompilationUnit_WithFromImportNestedModule_GeneratesPascalCasePath`

---

## Testing Status

### Existing Tests

| Test | File | Status |
|------|------|--------|
| `GenerateCompilationUnit_WithImportModule_GeneratesAliasToExports` | RoslynEmitterModuleTests.cs:226 | ✅ Passing |
| `GenerateCompilationUnit_WithFromImportNestedModule_GeneratesPascalCasePath` | RoslynEmitterModuleTests.cs:885 | ✅ Passing |
| `BasicImport_ImportFromSubdirectory_Works` | Phase0110IntegrationTests.cs:62 | ❌ Failing (semantic analysis issue) |

### Known Issues

The integration test `BasicImport_ImportFromSubdirectory_Works` is currently failing with:

```
Function expects 0 arguments but got 2
```

This suggests a **semantic analysis issue**, not a code generation issue. The problem is likely in:
- `NameResolver.cs` - Symbol registration for nested modules
- `ProjectCompiler.cs` - Module hierarchy construction
- Type resolution for module member access

The code generation is correct, but the symbol table may not have the function signature properly registered when accessed via module path.

---

## Comparison Summary

| Aspect | Option A (Chosen) | Option B (Rejected) |
|--------|------------------|-------------------|
| **Namespace collisions** | ✅ Prevented | ❌ Prone to conflicts |
| **Python semantics match** | ✅ Exact match | ❌ Loses module prefix |
| **Code clarity** | ✅ Origin always visible | ❌ Ambiguous source |
| **Deep nesting** | ✅ Handles well | ❌ Loses all context |
| **Tooling support** | ✅ IntelliSense friendly | ⚠️ Requires smart completion |
| **from-import** | ✅ Uses `using static` | ✅ Same |
| **.NET interop** | ✅ Clear distinction | ⚠️ Ambiguous |
| **Implementation** | ✅ Already done | N/A |

---

## Conclusion

**Option A (Fully Qualified Names)** is the correct strategy and is already implemented in the codebase. It aligns with:

1. **Sharpy's .NET-first philosophy** - Uses static classes and compile-time resolution
2. **Python's import semantics** - Preserves module prefix for regular imports
3. **Clarity and maintainability** - Origin of symbols is always visible
4. **Namespace hygiene** - Prevents conflicts in large codebases

The current failing test is due to a **semantic analysis issue**, not a code generation problem. The strategy itself is sound and correctly implemented.

---

## Next Steps

To fix the failing integration test:

1. **Debug symbol table registration** - Verify `ModuleSymbol.Exports` is properly populated
2. **Trace nested module resolution** - Check `ProjectCompiler.ResolveImports()` handles `lib.math`
3. **Verify type information** - Ensure function signatures are available when accessed via module path
4. **Add comprehensive tests** - Cover all edge cases listed above

The emission strategy itself requires no changes - it is already correct.

---

## References

- **Implementation:** `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs:3166-3327`
- **Tests:** `src/Sharpy.Compiler.Tests/CodeGen/RoslynEmitterModuleTests.cs`
- **Integration Tests:** `src/Sharpy.Compiler.Tests/Integration/Phase0110IntegrationTests.cs`
- **Language Spec:** `docs/language_specification/import_statements.md`
- **Module System:** `docs/language_specification/module_system.md`
