# Implementation Plan: Task 0.1.10.CG4 - Handle Nested Module Access

**Task ID:** 0.1.10.CG4
**Title:** Handle Nested Module Access
**Date:** 2026-01-16
**Status:** Planning

## Objective

Handle `lib.math.add(5, 3)` where `lib.math` is a nested module path. Ensure proper code generation that emits `Lib.Math.Add(5, 3)` in C#.

## Current State Analysis

### What's Already Working

Based on codebase exploration:

1. **AST Structure**: Nested member access like `lib.math.add(5, 3)` is correctly parsed as recursive `MemberAccess` nodes:
   - `FunctionCall { Function = MemberAccess { Object = MemberAccess { Object = Identifier("lib"), Member = "math" }, Member = "add" } }`

2. **Symbol Table Structure**: The `ProjectCompiler.cs` correctly builds nested module structures (lines 257-295):
   - For `import lib.math`: creates `lib (ModuleSymbol) → Exports["math"] → math (ModuleSymbol) → Exports["add"] → FunctionSymbol`

3. **Type Checking Logic**: `TypeChecker.CheckMemberAccess()` (lines 1517-1614) correctly:
   - Returns `ModuleType` for intermediate module access (`lib.math`)
   - Looks up exports in the nested module symbol

4. **Code Generation Structure**: `RoslynEmitter.GenerateMemberAccess()` (lines 3146-3200):
   - Recursively generates member access expressions
   - Applies PascalCase name mangling

### Current Test Status

The integration test `BasicImport_ImportFromSubdirectory_Works` is **failing** with:
```
Function expects 0 arguments but got 2
```

This indicates the type checker is finding the function symbol, but it has incorrect parameter information. This is likely a **pre-existing bug** in how exports are populated, not a code generation issue.

## Problem Decomposition

### Issue 1: Export Symbol Population (Bug)

The error "Function expects 0 arguments but got 2" suggests that when `lib.math.add` is looked up:
1. The `lib` module is found ✓
2. The `math` nested module is found in `lib.Exports["math"]` ✓
3. The `add` function is found in `math.Exports["add"]` ✓
4. But the `FunctionSymbol` has **empty parameters** ✗

This is likely because the exported symbols from the parsed module aren't being properly propagated with their full type information.

### Issue 2: Code Generation for Nested Modules

Once the type checking is fixed, the code generation path needs verification:

For `lib.math.add(5, 3)`:
1. `GenerateCall` handles `FunctionCall` with `MemberAccess` as function
2. `GenerateExpression(memberAccess.Object)` is called for `lib.math`
3. This recursively calls `GenerateMemberAccess` for `lib.math`
4. Which calls `GenerateExpression(Identifier("lib"))` → `IdentifierName("lib")`
5. Then builds `MemberAccess("lib", "Math")`
6. Final result: `Lib.Math.Add(5, 3)`

**Current concern**: When generating the identifier `lib`, `GetMangledVariableName()` is called which uses `NameMangler.ToCamelCase()`, producing `lib` not `Lib`.

## Step-by-Step Implementation Approach

### Step 1: Investigate Export Symbol Population

**Location:** `ProjectCompiler.cs` around lines 255-295

**Action:** Trace how `moduleInfo.ExportedSymbols` is populated to ensure `FunctionSymbol` parameters are preserved.

Check:
- `ImportResolver.ResolveImport()` - how it collects exports
- `ModuleInfo.ExportedSymbols` - where parameters are copied from

### Step 2: Fix Module Identifier Code Generation

**Location:** `RoslynEmitter.cs`, `GenerateExpression()` method, line 2496

**Problem:** When an identifier represents a module (like `lib`), it goes through `GetMangledVariableName()` which uses camelCase, resulting in `lib` instead of `Lib`.

**Solution:** Check if the identifier references a `ModuleSymbol` and use PascalCase for modules.

**Proposed Change:**
```csharp
Identifier name => GenerateIdentifier(name),

// New method:
private ExpressionSyntax GenerateIdentifier(Identifier id)
{
    // Check if this identifier refers to a module
    var symbol = _context.LookupSymbol(id.Name);
    if (symbol is ModuleSymbol)
    {
        // Module references use PascalCase (namespace-like)
        return IdentifierName(NameMangler.ToPascalCase(id.Name));
    }

    // Regular variable
    return IdentifierName(GetMangledVariableName(id.Name, isNewDeclaration: false));
}
```

### Step 3: Fix GenerateMemberAccess for Module Paths

**Location:** `RoslynEmitter.cs`, `GenerateMemberAccess()` method, lines 3146-3200

The current implementation correctly applies PascalCase to the member name. But we need to ensure the full chain produces valid C# namespace/class paths.

For `lib.math`:
- `obj = GenerateExpression(Identifier("lib"))` → should be `Lib` (with Step 2 fix)
- `member = ToPascalCase("math")` → `Math`
- Result: `Lib.Math`

**Verification needed:** Confirm the generated C# compiles correctly with module class names.

### Step 4: Update Namespace Generation

**Location:** `RoslynEmitter.cs`, namespace/using directive generation

Ensure that for `import lib.math`:
- A using directive is generated: `using Lib.Math;` or similar
- The module class is accessible as `Lib.Math.Add()`

Check `GenerateUsingDirectives()` and related methods.

### Step 5: Add Unit Tests

**Location:** `RoslynEmitterModuleTests.cs`

```csharp
[Fact]
public void GenerateCompilationUnit_NestedModuleAccess_GeneratesCorrectPath()
{
    // Test that lib.math.add(5, 3) generates Lib.Math.Add(5, 3)
}

[Fact]
public void GenerateCompilationUnit_DeepNestedModuleAccess_GeneratesCorrectPath()
{
    // Test level1.level2.level3.func() generates Level1.Level2.Level3.Func()
}
```

## Key Files to Modify

| File | Changes |
|------|---------|
| `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs` | Add `GenerateIdentifier()` method, update `GenerateExpression()` switch |
| `src/Sharpy.Compiler.Tests/CodeGen/RoslynEmitterModuleTests.cs` | Add nested module access tests |

## Potential Issues (Investigation Required)

### Issue A: Export Symbol Population

The test error suggests exports may not have complete function signatures. Need to investigate:
- `ImportResolver.ResolveImport()`
- How `FunctionSymbol` is created for exported functions
- Whether type checking runs on imported modules before export collection

### Issue B: Namespace vs Class Access

In generated C#, there's a difference between:
- `using Lib.Math; ... Add(5, 3);` - namespace import with direct function call
- `Lib.Math.Add(5, 3);` - fully qualified call without using

Need to determine the intended pattern and ensure consistency.

### Issue C: Module Class Naming

Currently, CG3 introduced `GetModuleClassName()` which uses the file name for the class name. For `lib/math.spy`:
- File-based: `Math` class in `Lib` namespace
- But accessing as `lib.math.add()` needs to resolve to `Lib.Math.Add()`

## Tests to Verify

### Existing Integration Tests (should pass after fix)
- `BasicImport_ImportFromSubdirectory_Works` - The main target
- `PackageInit_NestedPackages_Works`
- `EdgeCase_DeepNesting_Works`

### New Unit Tests
1. `GenerateIdentifier_ModuleReference_UsesPascalCase`
2. `GenerateMemberAccess_NestedModule_GeneratesFullPath`
3. `GenerateCall_NestedModuleFunction_GeneratesCorrectInvocation`

## Risks

1. **Breaking existing single-level module access**: Changes to identifier handling could affect `utils.helper()` which currently works

2. **Namespace collision**: If both `lib` package and `lib.spy` file exist, naming could conflict

3. **Circular dependency**: If fix requires changes to type checking (for export population), scope grows significantly

## Recommended Investigation Order

1. **First:** Debug why test reports "Function expects 0 arguments" - this is the blocking issue
2. **Second:** Verify code generation path once type checking passes
3. **Third:** Add comprehensive unit tests for the code generation

## Conclusion

The task appears more complex than initially scoped. The core code generation logic for nested member access is largely in place, but there's a bug in how function symbols are being exported from imported modules. This needs to be fixed before code generation can be properly verified.

**Recommended approach:**
1. Fix export symbol population (may require changes in `ImportResolver` or `ProjectCompiler`)
2. Add module-aware identifier generation in `RoslynEmitter`
3. Verify full integration test passes
4. Add unit tests for code generation paths
