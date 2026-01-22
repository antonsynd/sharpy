# Task List: RoslynEmitter Migration to CodeGenInfo

**Goal:** Complete the migration of RoslynEmitter to use CodeGenInfo computed during semantic analysis, removing legacy tracking sets.

**Priority:** Low - The infrastructure is done; this is incremental cleanup.

**Prerequisites:** 
- CodeGenInfo and CodeGenInfoComputer implemented (✅ Done)
- TypeChecker.CheckModule with `computeCodeGenInfo: true` working (✅ Done)

**Estimated Total Effort:** 2-3 days

**Related Documents:**
- `architecture_review_and_recommendations.md` - Recommendation 4

---

## Problem Summary

RoslynEmitter currently maintains legacy tracking sets that duplicate what CodeGenInfo computes:

```csharp
// RoslynEmitter.cs - Legacy tracking (can eventually be removed)
private readonly HashSet<string> _declaredVariables = new();
private readonly Dictionary<string, int> _variableVersions = new();
private readonly HashSet<string> _constVariables = new();
private readonly HashSet<string> _moduleFieldNames = new();
// ... more sets
```

CodeGenInfo is already computed and attached to symbols, but the emission code hasn't been fully migrated to use it.

---

## Current State

### What's Done
- `CodeGenInfo` record with all needed properties
- `CodeGenInfoComputer` that populates `Symbol.CodeGenInfo`
- Helper methods in RoslynEmitter: `GetCSharpNameForSymbol()`, `IsModuleLevelConstant()`, etc.
- TypeChecker calls `CodeGenInfoComputer.ComputeForModule()` when `computeCodeGenInfo: true`

### What's Remaining
- Emission code still uses legacy tracking for local variables
- Legacy sets still maintained "just in case"
- Not all emission paths use the helper methods

---

## Design Decisions

### Two-Way Door Decisions (Reversible)
1. **Incremental migration**: Migrate one partial file at a time
2. **Keep legacy fallbacks temporarily**: Helper methods fall back to legacy logic if CodeGenInfo is null

### One-Way Door Decisions (Commit Now)
1. **CodeGenInfo is source of truth**: For module-level symbols, always prefer CodeGenInfo
2. **Local variables remain runtime-tracked**: Local variable redeclarations are inherently emission-time

---

## Phase 1: Audit Current Usage (1-2 hours)

### Task 1.1: Map Legacy Tracking Set Usage
**File:** `src/Sharpy.Compiler/CodeGen/RoslynEmitter*.cs`
**Description:** Document where each legacy tracking set is used.

| Tracking Set | Usage Location | Can Use CodeGenInfo? | Notes |
|--------------|----------------|---------------------|-------|
| `_declaredVariables` | Local var emission | Partial | Local redecls need runtime |
| `_variableVersions` | `GetMangledVariableName` | No | Local variable versioning |
| `_constVariables` | Const detection | Yes | Use `CodeGenInfo.IsConstant` |
| `_moduleFieldNames` | Duplicate prevention | Yes | Use `CodeGenInfo.CSharpName` |
| `_classNames` | Type detection | Yes | Symbol lookup |
| `_structNames` | Type detection | Yes | Symbol lookup |
| `_stringEnumNames` | Enum detection | Yes | TypeSymbol properties |

**Verification:**
- [ ] All usages documented
- [ ] Migration feasibility assessed

---

### Task 1.2: Identify Emission Methods Using Legacy Logic
**Files:** All `RoslynEmitter.*.cs` partials
**Description:** Find methods that should use CodeGenInfo but don't.

```bash
cd /Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/CodeGen
grep -n "_declaredVariables\|_constVariables\|_moduleFieldNames" RoslynEmitter*.cs
```

**Verification:**
- [ ] Migration targets identified

---

## Phase 2: Migrate Module-Level Symbol Handling (2-4 hours)

### Task 2.1: Migrate Module-Level Variable Emission
**File:** `src/Sharpy.Compiler/CodeGen/RoslynEmitter.ModuleClass.cs`
**Description:** Use CodeGenInfo for module-level variable field generation.

Find code like:
```csharp
// OLD
var fieldName = NameMangler.ToPascalCase(varDecl.Name);
if (!_moduleFieldNames.Contains(fieldName))
{
    // emit field
    _moduleFieldNames.Add(fieldName);
}
```

Replace with:
```csharp
// NEW
var symbol = _context.LookupSymbol(varDecl.Name);
if (symbol?.CodeGenInfo != null && symbol.CodeGenInfo.IsModuleLevel)
{
    var fieldName = symbol.CodeGenInfo.CSharpName;
    // emit field using fieldName
}
```

**Verification:**
- [ ] Module-level variables emit correctly
- [ ] Tests pass

**Commit:** `refactor(codegen): Use CodeGenInfo for module-level variable emission`

---

### Task 2.2: Migrate Module-Level Constant Emission
**File:** `src/Sharpy.Compiler/CodeGen/RoslynEmitter.ModuleClass.cs`
**Description:** Use CodeGenInfo for constant field generation.

```csharp
// NEW approach
var symbol = _context.LookupSymbol(constDecl.Name);
if (symbol?.CodeGenInfo != null && symbol.CodeGenInfo.IsConstant)
{
    var constName = symbol.CodeGenInfo.CSharpName; // Already in CONSTANT_CASE
    // emit const field
}
```

**Verification:**
- [ ] Constants emit correctly
- [ ] CONSTANT_CASE naming preserved

**Commit:** `refactor(codegen): Use CodeGenInfo for constant emission`

---

### Task 2.3: Migrate From-Import Symbol Handling
**File:** `src/Sharpy.Compiler/CodeGen/RoslynEmitter.CompilationUnit.cs`
**Description:** Use CodeGenInfo for imported symbol name resolution.

```csharp
// When emitting code that references an imported symbol
var symbol = _context.LookupSymbol(name);
if (symbol?.CodeGenInfo != null && IsFromImportSymbol(symbol))
{
    var csharpName = symbol.CodeGenInfo.CSharpName;
    var originalName = symbol.CodeGenInfo.OriginalImportName;
    // Use csharpName in generated code
}
```

**Verification:**
- [ ] Imported symbols resolve correctly
- [ ] Aliased imports work

**Commit:** `refactor(codegen): Use CodeGenInfo for from-import symbols`

---

## Phase 3: Migrate Type and Method Emission (2-4 hours)

### Task 3.1: Migrate Class Name Resolution
**File:** `src/Sharpy.Compiler/CodeGen/RoslynEmitter.TypeDeclarations.cs`
**Description:** Use CodeGenInfo for class name generation instead of `_classNames` set.

```csharp
// When emitting a class
var typeSymbol = _context.LookupSymbol(classDef.Name) as TypeSymbol;
var csharpClassName = typeSymbol?.CodeGenInfo?.CSharpName 
    ?? NameMangler.ToPascalCase(classDef.Name);
```

**Verification:**
- [ ] Class names correct
- [ ] Tests pass

**Commit:** `refactor(codegen): Use CodeGenInfo for class name resolution`

---

### Task 3.2: Migrate Struct Name Resolution
**File:** `src/Sharpy.Compiler/CodeGen/RoslynEmitter.TypeDeclarations.cs`
**Description:** Use CodeGenInfo for struct name generation.

**Verification:**
- [ ] Struct names correct

**Commit:** `refactor(codegen): Use CodeGenInfo for struct name resolution`

---

### Task 3.3: Migrate Method Name Resolution
**File:** `src/Sharpy.Compiler/CodeGen/RoslynEmitter.ClassMembers.cs`
**Description:** Use CodeGenInfo for method name generation.

```csharp
// When emitting a method
var methodSymbol = typeSymbol?.Methods.FirstOrDefault(m => m.Name == funcDef.Name);
var csharpMethodName = methodSymbol?.CodeGenInfo?.CSharpName 
    ?? NameMangler.ToPascalCase(funcDef.Name);
```

**Verification:**
- [ ] Method names correct
- [ ] Dunder methods mapped correctly (`__init__` → constructor, `__str__` → `ToString`)

**Commit:** `refactor(codegen): Use CodeGenInfo for method name resolution`

---

## Phase 4: Remove Legacy Tracking Sets (1-2 hours)

### Task 4.1: Remove Unused Tracking Sets
**File:** `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`
**Description:** After migration, remove tracking sets that are no longer used.

**Sets that can be removed:**
- `_moduleFieldNames` - replaced by CodeGenInfo
- `_classNames` - replaced by symbol lookup
- `_structNames` - replaced by symbol lookup
- `_stringEnumNames` - replaced by TypeSymbol properties

**Sets that must be kept (local scope tracking):**
- `_declaredVariables` - tracks local variable declarations during emission
- `_variableVersions` - tracks local variable redeclarations
- `_constVariables` - tracks local const declarations

```csharp
// REMOVE these:
// private readonly HashSet<string> _moduleFieldNames = new();
// private readonly HashSet<string> _classNames = new();
// private readonly HashSet<string> _structNames = new();
// private readonly HashSet<string> _stringEnumNames = new();

// KEEP these (local scope tracking):
private readonly HashSet<string> _declaredVariables = new();
private readonly Dictionary<string, int> _variableVersions = new();
private readonly HashSet<string> _constVariables = new();
```

**Verification:**
- [ ] Removed sets have no usages
- [ ] Compilation succeeds
- [ ] All tests pass

**Commit:** `refactor(codegen): Remove unused legacy tracking sets`

---

### Task 4.2: Update Comments
**File:** `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`
**Description:** Update comments to reflect the new architecture.

```csharp
/// <summary>
/// Generates C# code using Roslyn syntax trees.
/// 
/// Name Resolution:
/// - Module-level symbols: Use Symbol.CodeGenInfo (computed during semantic analysis)
/// - Local variables: Use runtime tracking (_declaredVariables, _variableVersions)
/// </summary>
public partial class RoslynEmitter
{
    // ============================================================
    // LOCAL SCOPE TRACKING
    // These fields track local variable state during emission.
    // Module-level symbols use CodeGenInfo instead.
    // ============================================================
    
    private readonly HashSet<string> _declaredVariables = new();
    private readonly Dictionary<string, int> _variableVersions = new();
    private readonly HashSet<string> _constVariables = new();
}
```

**Verification:**
- [ ] Comments accurate

**Commit:** `docs(codegen): Update RoslynEmitter comments for CodeGenInfo architecture`

---

## Phase 5: Verification (30 minutes)

### Task 5.1: Run Full Test Suite
```bash
dotnet test Sharpy.Compiler.Tests --verbosity minimal
```

**Verification:**
- [ ] All tests pass

---

### Task 5.2: Run Integration Tests
```bash
dotnet test Sharpy.Compiler.Tests --filter "FullyQualifiedName~Integration" --verbosity normal
```

**Verification:**
- [ ] Integration tests pass
- [ ] Generated C# code is correct

---

### Task 5.3: Manual Verification
**Description:** Compile a few sample programs and inspect generated C#.

```bash
cd examples
dotnet run --project ../src/Sharpy.Cli -- compile sample.spy --output sample.cs
cat sample.cs  # Verify naming conventions
```

**Verification:**
- [ ] PascalCase for types and methods
- [ ] camelCase for local variables
- [ ] CONSTANT_CASE for constants
- [ ] Correct handling of imports

---

## Summary

After completing these tasks:

1. ✅ Module-level symbols use CodeGenInfo for name resolution
2. ✅ Legacy tracking sets removed (except local scope tracking)
3. ✅ Cleaner separation: semantic analysis computes names, emission just uses them
4. ✅ Local variable handling unchanged (still runtime-tracked)

Benefits:
- Simpler emission code
- Names computed once during semantic analysis
- Easier to test semantic analysis and code generation independently
- Foundation for future parallel compilation (CodeGenInfo is immutable)
