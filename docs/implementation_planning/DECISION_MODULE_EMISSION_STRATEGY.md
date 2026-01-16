# DECISION: C# Emission Strategy for Module Access

**Date:** 2026-01-16
**Decision:** ✅ APPROVED - Option A: Fully Qualified Names
**Status:** Already Implemented

---

## Quick Reference

### Sharpy Code
```python
# config.spy
MAX_SIZE: int = 100

# main.spy
import config
x = config.MAX_SIZE
```

### Generated C#
```csharp
// config.cs
namespace MyProject.Config {
    public static class Exports {
        public static int MAX_SIZE = 100;
    }
}

// main.cs
using config = MyProject.Config.Exports;

namespace MyProject {
    public static class Exports {
        public static void Main() {
            var x = config.MAX_SIZE;
        }
    }
}
```

---

## Decision Matrix

| Import Type | Sharpy Syntax | C# Using Directive | Access Pattern |
|-------------|---------------|-------------------|----------------|
| **Module** | `import config` | `using config = Config.Exports;` | `config.MAX_SIZE` |
| **Nested** | `import lib.math` | `using lib_math = Lib.Math.Exports;` | `lib_math.Add(5, 3)` |
| **Alias** | `import config as cfg` | `using cfg = Config.Exports;` | `cfg.MAX_SIZE` |
| **From** | `from config import MAX_SIZE` | `using static Config.Exports;` | `MAX_SIZE` |
| **From All** | `from config import *` | `using static Config.Exports;` | `MAX_SIZE` |

---

## Why Option A?

1. **Prevents namespace collisions** - Multiple modules can have same member names
2. **Matches Python semantics** - `import module` requires `module.member` prefix
3. **Explicit origin** - Always clear where symbols come from
4. **Handles nested modules** - `lib.math.add` → `lib_math.Add` preserves hierarchy
5. **.NET interop clarity** - Distinguishes Sharpy modules from .NET namespaces

---

## Implementation Location

- **File:** `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`
- **Lines:** 3166-3327
- **Key Methods:**
  - `TryExtractModulePath()` - Validates module access chains
  - `BuildModuleAccessExpression()` - Converts to C# syntax
  - `GenerateMemberAccess()` - Main entry point

---

## Test Results

✅ **37/37 unit tests passing** (`RoslynEmitterModuleTests`)
✅ Code generation is correct and matches specification
⚠️ Integration test failing due to semantic analysis issue (not code generation)

---

## Next Steps

The emission strategy is finalized and implemented. The failing integration test (`BasicImport_ImportFromSubdirectory_Works`) is a **semantic analysis problem**, not related to code generation strategy.

See full details in: `c_sharp_emission_strategy_module_access.md`
