# Implementation Plan: Task 0.1.10.CG4 - Handle Nested Module Access

**Task ID:** 0.1.10.CG4
**Title:** Handle Nested Module Access
**Priority:** High
**Target File:** `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

---

## Summary

This task handles code generation for nested module access patterns like `lib.math.add(5, 3)`. The goal is to correctly recognize chained module access and generate proper C# code (`Lib.Math.Add(5, 3)`).

---

## Current State Analysis

### ✅ Already Implemented (from recent commits)

Based on analysis of `RoslynEmitter.cs:3166-3327`, the core infrastructure is **already in place**:

1. **`TryExtractModulePath()`** (lines 3234-3300):
   - Traverses `MemberAccess` chain to extract path: `lib.math.add` → `["lib", "math", "add"]`
   - Validates base is an `Identifier`
   - Checks if base symbol is a `ModuleSymbol`
   - Verifies path exists in module hierarchy via `Exports` dictionary
   - Returns `true` only if entire path is valid module access

2. **`BuildModuleAccessExpression()`** (lines 3306-3327):
   - Converts validated path to C# syntax
   - Applies PascalCase conversion to each segment
   - Chains `MemberAccessExpression` nodes: `Lib.Math.Add`

3. **`GenerateMemberAccess()`** (lines 3166-3227):
   - Calls `TryExtractModulePath()` first
   - Falls back to enum handling or standard member access

### ⚠️ Potential Issues to Verify

1. **Symbol table registration** - Does `NameResolver` properly register nested modules?
2. **Exports population** - Are nested module exports correctly populated?
3. **Method invocation handling** - Does `lib.math.add(5, 3)` correctly parse as `CallExpression` with `MemberAccess` callee?

---

## Step-by-Step Implementation Approach

### Step 1: Verify Existing Tests Pass

Run the existing nested module tests to understand current behavior:

```bash
dotnet test --filter "FullyQualifiedName~Phase0110IntegrationTests.BasicImport_ImportFromSubdirectory_Works"
dotnet test --filter "FullyQualifiedName~RoslynEmitterModuleTests.GenerateCompilationUnit_WithFromImportNestedModule"
```

**Expected outcome:** Determine if tests pass or fail with current implementation.

### Step 2: Debug Symbol Table Registration (if tests fail)

If nested module access fails, the issue is likely in symbol registration. Check:

1. **NameResolver.cs** - `ResolveImport()` method (currently has TODO comments)
2. **ProjectCompiler.cs** - `ResolveImports()` method that builds module hierarchy

**Files to examine:**
- `src/Sharpy.Compiler/Semantic/NameResolver.cs`
- `src/Sharpy.Compiler/Project/ProjectCompiler.cs`

### Step 3: Verify CallExpression Handling for Module Methods

When `lib.math.add(5, 3)` is parsed:
- The AST should be: `CallExpression { Callee = MemberAccess { Object = MemberAccess { Object = Identifier("lib"), Member = "math" }, Member = "add" }, Arguments = [...] }`
- The `GenerateCallExpression()` method processes this
- Check that `GenerateExpression(call.Callee)` correctly delegates to `GenerateMemberAccess()`

**Location:** `RoslynEmitter.cs` - `GenerateCallExpression()` method

### Step 4: Add Comprehensive Unit Tests

Create specific tests for nested module access patterns:

```csharp
// In RoslynEmitterModuleTests.cs

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
    // lib.config.MAX_SIZE -> Lib.Config.MAX_SIZE
}
```

### Step 5: Handle Edge Cases

Verify these scenarios work correctly:

1. **Snake_case path conversion:**
   - `my_lib.math_utils.add_numbers` → `MyLib.MathUtils.AddNumbers`

2. **Mixed module/member access:**
   - `lib.math.Vector.x` where `Vector` is a class, not a module
   - Should generate: `Lib.Math.Vector.X`

3. **Module access with parenthesized expression:**
   - `(lib.math).add(5, 3)` (if supported)

4. **Import alias with nested module:**
   - `import lib.math as m` then `m.add(5, 3)` → `Math.Add(5, 3)`

---

## Key Files to Modify

| File | Purpose | Changes Needed |
|------|---------|----------------|
| `RoslynEmitter.cs:3166-3327` | Module access code gen | Likely minimal - verify existing code works |
| `NameResolver.cs` | Symbol registration | May need to implement `ResolveImport()` TODO |
| `ProjectCompiler.cs` | Module hierarchy | May need to verify nested module registration |

---

## Tests to Verify

### Integration Tests
```
Phase0110IntegrationTests.BasicImport_ImportFromSubdirectory_Works
Phase0110IntegrationTests.BasicImport_MultipleImports_Works
```

### Unit Tests
```
RoslynEmitterModuleTests.GenerateCompilationUnit_WithFromImportNestedModule_GeneratesPascalCasePath
```

### New Tests to Add
1. Nested module function call with arguments
2. Deeply nested module access (3+ levels)
3. Snake_case module path conversion
4. Module variable access (not just functions)

---

## Potential Risks & Questions

### Risks

1. **Symbol table may not be fully populated**
   - The `NameResolver.ResolveImport()` method has TODO stubs
   - If module symbols aren't registered, `TryExtractModulePath()` will return false
   - Mitigation: Check if `ProjectCompiler.ResolveImports()` handles this instead

2. **Nested module exports may be incomplete**
   - Each `ModuleSymbol` needs its `Exports` dictionary populated
   - If `lib.Exports["math"]` is a `ModuleSymbol`, it needs its own `Exports`
   - Mitigation: Trace through `ImportResolver` to verify hierarchy

3. **PascalCase conversion edge cases**
   - Names starting with numbers
   - Reserved C# keywords
   - Unicode identifiers
   - Mitigation: Review `NameMangler.ToPascalCase()` implementation

### Questions for Clarification

1. **Should `import lib.math` make both `lib` and `lib.math` accessible?**
   - Python: Only `lib.math` is accessible (must use full path)
   - Current impl: Registers `lib` as module with `math` in exports
   - Clarify expected behavior

2. **How should `import lib.math as m` affect code generation?**
   - Does `m.add()` become `Math.Add()` or `Lib.Math.Add()`?
   - Current tests suggest: `using static Math.Exports` (just the alias)

3. **What happens with conflicting names?**
   - `lib.math.List` (custom) vs `List` (builtin)
   - Should fully-qualified names be generated to avoid conflicts?

---

## Implementation Order

1. **Run existing tests** → Identify actual failures
2. **Trace symbol registration** → Verify module hierarchy is correct
3. **Debug `TryExtractModulePath()`** → Add logging to understand why it might fail
4. **Add unit tests** → Cover edge cases
5. **Fix any issues found** → Likely in symbol registration, not code gen
6. **Run full test suite** → Ensure no regressions

---

## Success Criteria

- [ ] `lib.math.add(5, 3)` generates `Lib.Math.Add(5, 3)`
- [ ] `level1.level2.level3.func()` generates correct nested path
- [ ] Snake_case paths convert correctly to PascalCase
- [ ] Module variable access works: `lib.config.MAX_SIZE` → `Lib.Config.MAX_SIZE`
- [ ] All Phase 0.1.10 integration tests pass
- [ ] No regressions in existing module tests
