# Implementation Plan: Task 0.1.10.CG4 - Handle Nested Module Access

**Task ID:** 0.1.10.CG4
**Title:** Handle Nested Module Access
**Priority:** High
**Target File:** `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

---

## Summary

Handle code generation for `lib.math.add(5, 3)` where `lib.math` is a nested module path. The goal is to recognize chained module access and generate proper C# code: `Lib.Math.Add(5, 3)`.

---

## Current Implementation Status

**Status: IMPLEMENTED** (commit `011a546`)

The core infrastructure is already in place in `RoslynEmitter.cs:3166-3327`:

### Implemented Methods

1. **`GenerateMemberAccess()`** (lines 3166-3227)
   - First checks for nested module access via `TryExtractModulePath()`
   - If module path detected, calls `BuildModuleAccessExpression()`
   - Falls back to enum handling or standard member access otherwise

2. **`TryExtractModulePath()`** (lines 3234-3300)
   - Traverses `MemberAccess` chain: `lib.math.add` → `["lib", "math", "add"]`
   - Validates base is an `Identifier`
   - Checks if base symbol is a `ModuleSymbol` via `_context.LookupSymbol()`
   - Verifies path exists in module hierarchy via `Exports` dictionary
   - Returns `true` only if entire path is valid module access

3. **`BuildModuleAccessExpression()`** (lines 3306-3327)
   - Converts validated path to C# syntax
   - Applies `NameMangler.ToPascalCase()` to each segment
   - Chains `MemberAccessExpression` nodes: `["lib", "math", "add"]` → `Lib.Math.Add`

---

## Step-by-Step Implementation Approach

### Step 1: Verify Symbol Table Registration

**Purpose:** Ensure nested modules are correctly registered in the symbol table.

**Files to examine:**
- `src/Sharpy.Compiler/Project/ProjectCompiler.cs:257-295` - Module hierarchy building
- `src/Sharpy.Compiler/Semantic/Symbol.cs:107-111` - ModuleSymbol definition

**Expected behavior:**
For `import lib.math`:
1. Create leaf `ModuleSymbol` with actual exports from the parsed module
2. Build nested structure by wrapping in parent modules
3. Register root module in symbol table: `lib → Exports["math"] → math → Exports["add"]`

### Step 2: Verify AST Structure for Function Calls

**Purpose:** Ensure `lib.math.add(5, 3)` parses correctly.

**Expected AST:**
```
FunctionCall {
  Function = MemberAccess {
    Object = MemberAccess {
      Object = Identifier("lib"),
      Member = "math"
    },
    Member = "add"
  },
  Arguments = [Literal(5), Literal(3)]
}
```

**Files to examine:**
- `src/Sharpy.Compiler/Parser/Ast.cs` - AST node definitions
- `src/Sharpy.Compiler/Parser/Parser.cs` - Parsing logic

### Step 3: Verify Call Expression Handling

**Purpose:** Ensure function calls through nested modules work correctly.

**Code path:**
1. `GenerateCallExpression()` processes the `FunctionCall` node
2. `GenerateExpression(call.Function)` is called for the callee
3. If callee is `MemberAccess`, `GenerateMemberAccess()` is invoked
4. `TryExtractModulePath()` detects module path
5. `BuildModuleAccessExpression()` generates `Lib.Math.Add`
6. Arguments are processed and appended

**File:** `RoslynEmitter.cs` - `GenerateCallExpression()` method

### Step 4: Add/Update Unit Tests

**File:** `src/Sharpy.Compiler.Tests/CodeGen/RoslynEmitterModuleTests.cs`

```csharp
[Fact]
public void GenerateMemberAccess_NestedModulePath_GeneratesPascalCaseChain()
{
    // lib.math.add -> Lib.Math.Add
}

[Fact]
public void GenerateMemberAccess_DeeplyNestedModule_GeneratesFullPath()
{
    // level1.level2.level3.func -> Level1.Level2.Level3.Func
}

[Fact]
public void GenerateCallExpression_NestedModuleFunction_GeneratesCorrectInvocation()
{
    // lib.math.add(5, 3) -> Lib.Math.Add(5, 3)
}

[Fact]
public void GenerateMemberAccess_NestedModuleVariable_GeneratesStaticAccess()
{
    // lib.config.MAX_SIZE -> Lib.Config.MaxSize
}

[Fact]
public void GenerateMemberAccess_SnakeCaseModulePath_ConvertsCorrectly()
{
    // my_lib.math_utils.add_numbers -> MyLib.MathUtils.AddNumbers
}
```

### Step 5: Handle Edge Cases

1. **Snake_case path conversion:**
   - `my_lib.math_utils.add_numbers` → `MyLib.MathUtils.AddNumbers`

2. **Mixed module/member access:**
   - `lib.math.Vector.x` where `Vector` is a class
   - Should generate: `Lib.Math.Vector.X`

3. **Module variable access (not just functions):**
   - `lib.config.MAX_SIZE` → `Lib.Config.MaxSize`

4. **Import alias with nested module:**
   - `import lib.math as m` then `m.add(5, 3)` → Uses alias correctly

### Step 6: Run Full Test Suite

```bash
dotnet test --filter "FullyQualifiedName~RoslynEmitterModuleTests"
dotnet test --filter "FullyQualifiedName~Phase0110IntegrationTests"
```

---

## Key Files to Modify

| File | Purpose | Changes Needed |
|------|---------|----------------|
| `RoslynEmitter.cs:3166-3327` | Module access code gen | **DONE** - Verify existing code works |
| `ProjectCompiler.cs:257-295` | Module hierarchy building | Verify nested module registration |
| `RoslynEmitterModuleTests.cs` | Unit tests | Add tests for nested module access |
| `Phase0110IntegrationTests.cs` | Integration tests | Add end-to-end tests |

---

## Tests to Verify

### Existing Tests to Run
```
RoslynEmitterModuleTests.GenerateCompilationUnit_WithFromImportNestedModule_GeneratesPascalCasePath
Phase0110IntegrationTests.BasicImport_ImportFromSubdirectory_Works
Phase0110IntegrationTests.PackageInit_NestedPackages_Works
Phase0110IntegrationTests.ComplexScenario_NestedPackagesWithImports_Works
Phase0110IntegrationTests.EdgeCase_DeepNesting_Works
```

### New Tests to Add
1. Nested module function call with arguments
2. Deeply nested module access (3+ levels)
3. Snake_case module path conversion
4. Module variable access (not just functions)
5. Mixed module/member access chains

---

## Potential Risks

### Risk 1: Symbol Table Population
**Issue:** If `ModuleSymbol.Exports` dictionary is not correctly populated for nested modules, `TryExtractModulePath()` will return false and fall through to standard member access.

**Mitigation:**
- Add debug logging in `TryExtractModulePath()` to trace path validation
- Verify `ProjectCompiler.ResolveImports()` correctly builds the hierarchy

### Risk 2: Name Mangling Edge Cases
**Issue:** PascalCase conversion may not handle all cases correctly.

**Edge cases:**
- Names starting with numbers: `3d_model` → ???
- Reserved C# keywords: `class`, `namespace`
- Unicode identifiers

**Mitigation:** Review `NameMangler.ToPascalCase()` implementation

### Risk 3: Exports Symbol Type Preservation
**Issue:** When symbols are copied to `ModuleSymbol.Exports`, they may lose type information (e.g., function parameter counts).

**Evidence:** Previous error "Function expects 0 arguments but got 2" suggests this could be an issue.

**Mitigation:** Trace symbol copying in `ImportResolver` to verify full symbol information is preserved.

---

## Questions for Clarification

1. **Should `import lib.math` make both `lib` and `lib.math` accessible?**
   - Python: Only `lib.math` is accessible (must use full path)
   - Current impl: Registers `lib` as module with `math` in exports
   - **Recommendation:** Follow current implementation (register full hierarchy)

2. **How should `import lib.math as m` affect code generation?**
   - Does `m.add()` become `Math.Add()` or `Lib.Math.Add()`?
   - **Expected:** `m.add()` → alias maps directly to the imported module

3. **What happens with conflicting names?**
   - `lib.math.List` vs builtin `List`
   - **Recommendation:** Module access takes precedence; users can disambiguate

---

## Implementation Order

1. ✅ **Core implementation done** (commit `011a546`)
2. ⬜ **Run existing tests** - Identify actual failures
3. ⬜ **Debug failing tests** - Trace through code path
4. ⬜ **Add missing unit tests** - Cover edge cases
5. ⬜ **Run full test suite** - Ensure no regressions

---

## Success Criteria

- [ ] `lib.math.add(5, 3)` generates `Lib.Math.Add(5, 3)`
- [ ] `level1.level2.level3.func()` generates correct nested path
- [ ] Snake_case paths convert correctly to PascalCase
- [ ] Module variable access works: `lib.config.MAX_SIZE` → `Lib.Config.MaxSize`
- [ ] All Phase 0.1.10 integration tests pass
- [ ] No regressions in existing module tests

---

## Code Flow Diagram

```
Sharpy: lib.math.add(5, 3)
           │
           ▼
    ┌──────────────┐
    │ Parser       │
    │ Creates AST  │
    └──────────────┘
           │
           ▼
    FunctionCall {
      Function: MemberAccess {
        Object: MemberAccess {
          Object: Identifier("lib"),
          Member: "math"
        },
        Member: "add"
      },
      Args: [5, 3]
    }
           │
           ▼
    ┌──────────────────────┐
    │ ProjectCompiler      │
    │ RegisterImports()    │
    └──────────────────────┘
           │
           ▼
    Symbol Table:
    lib (ModuleSymbol)
      └─ Exports["math"] → math (ModuleSymbol)
           └─ Exports["add"] → add (FunctionSymbol)
           │
           ▼
    ┌──────────────────────┐
    │ RoslynEmitter        │
    │ GenerateCallExpr()   │
    └──────────────────────┘
           │
           ▼
    ┌──────────────────────┐
    │ GenerateMemberAccess │
    │ TryExtractModulePath │
    └──────────────────────┘
           │
           ▼
    modulePath = ["lib", "math", "add"]
           │
           ▼
    ┌──────────────────────┐
    │ BuildModuleAccess    │
    │ Expression           │
    └──────────────────────┘
           │
           ▼
    C#: Lib.Math.Add(5, 3)
```

---

## References

- `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs:3166-3327` - Module access implementation
- `src/Sharpy.Compiler/Project/ProjectCompiler.cs:257-295` - Symbol table registration
- `src/Sharpy.Compiler/Semantic/Symbol.cs:107-111` - ModuleSymbol definition
- `docs/design/module_access_code_generation.md` - Design document
- Commit `011a546` - Original implementation
