# Phase 0.1.10: Module Import Code Generation Fix

**Goal:** Complete the module import system by implementing proper C# code generation for module member access patterns.

**Prerequisites:** Phase 0.1.10 semantic analysis must be working (completed in this PR).

**Current Status:**
- ✅ Semantic analysis for `import module` working
- ✅ `ModuleSymbol` and `ModuleType` types created
- ✅ `TypeChecker.CheckMemberAccess` handles module types
- ✅ `ProjectCompiler.ResolveImports` creates proper module symbol hierarchy
- ❌ Code generation for module access patterns (`config.MAX_SIZE`) fails
- ❌ Generated C# references non-existent types/namespaces

**Root Cause:**
When `config.MAX_SIZE` is type-checked, it correctly identifies `config` as a module and `MAX_SIZE` as an exported variable. However, the code generator (`RoslynEmitter`) doesn't know how to emit this pattern because it tries to generate `Config.Max_Size` as if `Config` were a C# class.

---

## Task 0.1.10.CG1: Analyze Current Code Generation for Member Access

**Type:** 🔍 Investigation
**Priority:** Critical
**Estimated Time:** 1 hour

📁 **Files**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

### Objective
Understand how `MemberAccess` expressions are currently being emitted to C# code.

### Actions

1. **Find the MemberAccess emission code:**
   ```bash
   grep -n "MemberAccess" src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs
   ```

2. **Document the current behavior:**
   - What does `obj.member` get emitted as?
   - How are module-level variables/functions accessed in generated C#?

3. **Identify the gap:**
   - Module imports create `ModuleSymbol` with exports
   - But generated C# doesn't create corresponding namespaces/classes

---

## Task 0.1.10.CG2: Define C# Emission Strategy for Module Access

**Type:** 📐 Design
**Priority:** Critical
**Estimated Time:** 2 hours

### Objective
Decide how `import config` + `config.MAX_SIZE` should emit to C#.

### Option A: Fully Qualified Names (Recommended)

```python
# config.spy
MAX_SIZE: int = 100

# main.spy
import config
x = config.MAX_SIZE
```

**Generated C#:**
```csharp
// config.cs
namespace MyProject {
    public static class Config {
        public static int MAX_SIZE = 100;
    }
}

// main.cs
namespace MyProject {
    public static class Main {
        public static int X = Config.MAX_SIZE;  // Direct static class access
    }
}
```

### Option B: Using Static

```csharp
using static MyProject.Config;
// Then directly access MAX_SIZE
```

### Decision Criteria
- Option A is cleaner for module namespacing semantics
- Requires generating static classes for each module with its exports
- Module-level variables become static fields
- Module-level functions become static methods

---

## Task 0.1.10.CG3: Update Module Code Generation

**Type:** 🛠️ Implementation
**Priority:** Critical
**Estimated Time:** 3-4 hours

📁 **Files**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

### Objective
Generate a static class for each Sharpy module file containing its exports.

### Current Behavior
- Module-level functions generate fine (already in static class)
- Module-level variables may not be generating correctly as static fields

### Required Changes

1. **Ensure module generates as static class:**
   - Module name → PascalCase class name
   - File `config.spy` → `public static class Config`

2. **Module-level variables → static fields:**
   ```python
   MAX_SIZE: int = 100
   ```
   Becomes:
   ```csharp
   public static int MAX_SIZE = 100;
   ```

3. **Track semantic info for member access:**
   - When emitting `config.MAX_SIZE`, look up that this is a module member access
   - Emit as `Config.MAX_SIZE` (static class member access)

### Implementation Hints

```csharp
// In RoslynEmitter, when handling MemberAccess:
private ExpressionSyntax EmitMemberAccess(MemberAccess memberAccess)
{
    // Check if the object is a module identifier
    if (IsModuleReference(memberAccess.Object))
    {
        var moduleName = GetModuleName(memberAccess.Object);
        var pascalModuleName = ToPascalCase(moduleName);
        var memberName = ToPascalCase(memberAccess.Member);

        // Generate: ModuleName.MemberName
        return SyntaxFactory.MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            SyntaxFactory.IdentifierName(pascalModuleName),
            SyntaxFactory.IdentifierName(memberName));
    }

    // Default behavior for other member access
    ...
}
```

---

## Task 0.1.10.CG4: Handle Nested Module Access

**Type:** 🛠️ Implementation
**Priority:** High
**Estimated Time:** 2-3 hours

📁 **Files**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

### Objective
Handle `lib.math.add(5, 3)` where `lib.math` is a nested module path.

### Approach

For `import lib.math`:
- Register `lib` → `math` → exports in symbol table (done)
- When emitting `lib.math.add()`:
  - Recognize this is a chained module access
  - Generate: `Lib.Math.Add(5, 3)` (using PascalCase conversion)

### Namespace Generation

```python
# lib/math.spy
def add(x: int, y: int) -> int:
    return x + y
```

**Generated C#:**
```csharp
namespace MyProject.Lib {
    public static class Math {
        public static int Add(int x, int y) {
            return x + y;
        }
    }
}
```

---

## Task 0.1.10.CG5: Handle From-Import Code Generation

**Type:** 🛠️ Implementation
**Priority:** High
**Estimated Time:** 2 hours

📁 **Files**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

### Objective
Handle `from module import symbol` by emitting direct references.

### Example

```python
from config import MAX_SIZE
x = MAX_SIZE  # Direct use without prefix
```

**Generated C#:**
```csharp
// Option A: using static
using static MyProject.Config;
// Then: int x = MAX_SIZE;

// Option B: Fully qualified
int x = MyProject.Config.MAX_SIZE;
```

### Implementation Notes
- `from X import Y` imports `Y` directly into current scope
- Code gen can either:
  1. Generate `using static` directives
  2. Emit fully qualified names at each usage site
- Option 2 is simpler and more explicit

---

## Task 0.1.10.CG6: Fix Multiple Entry Points Issue

**Type:** 🐛 Bug Fix
**Priority:** High
**Estimated Time:** 1 hour

📁 **Files**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`, `src/Sharpy.Compiler/Project/ProjectCompiler.cs`

### Problem
When compiling multiple files, each generates a `Main` method, causing C# compilation error.

### Solution
- Only the designated entry point file should generate a `Main` method
- Other files should only generate their module classes
- Check `ProjectConfig.EntryPoint` when generating code

### Implementation
```csharp
// In code generation, check if this is the entry point file
if (codeGenContext.IsEntryPoint)
{
    // Generate Main method
}
else
{
    // Only generate module-level static class
}
```

---

## Task 0.1.10.CG7: Update Integration Tests

**Type:** 🧪 Testing
**Priority:** High
**Estimated Time:** 2 hours

📁 **Files**: `src/Sharpy.Compiler.Tests/Integration/Phase0110IntegrationTests.cs`

### Objective
Verify all Phase 0.1.10 tests pass after code generation fixes.

### Test Categories
1. Basic import (`import module`)
2. Nested imports (`import lib.math`)
3. Aliased imports (`import config as cfg`)
4. From-imports (`from module import symbol`)
5. Package imports with `__init__.spy`
6. Multi-file compilation
7. Deep nesting (`level1.level2.level3`)

---

## Summary: Task Dependencies

```
0.1.10.CG1 (Investigate) ──► 0.1.10.CG2 (Design)
                                    │
                                    ▼
                             0.1.10.CG3 (Module CodeGen)
                                    │
                                    ├──► 0.1.10.CG4 (Nested Modules)
                                    │
                                    ├──► 0.1.10.CG5 (From-Import)
                                    │
                                    └──► 0.1.10.CG6 (Entry Point Fix)
                                              │
                                              ▼
                                       0.1.10.CG7 (Tests)
```

## Estimated Total Time
- Investigation: 1 hour
- Design: 2 hours
- Implementation: 8-10 hours
- Testing: 2 hours
- **Total:** 13-15 hours

## Notes for Agent/Engineer

1. **Semantic analysis is working** — The `ModuleSymbol` and `ModuleType` system correctly tracks module imports and validates member access. No changes needed there.

2. **Code generation is the bottleneck** — The emitter doesn't know how to translate module access patterns to C#.

3. **Static classes are the target** — Each `.spy` module should generate a static class with its exports as static members.

4. **Namespace from path** — Use file path to determine C# namespace (`lib/math.spy` → `namespace Lib { class Math }` or `namespace Lib.Math`).

5. **Entry point handling** — Only one file should generate `Main()`. Use `ProjectConfig.EntryPoint` to determine which.

6. **PascalCase conversion** — Python-style names (`max_size`) should become C#-style (`MaxSize`).

7. **The fix to `ImportResolver.ExtractExportedSymbol`** — Now exports all module-level variables, not just constants.
