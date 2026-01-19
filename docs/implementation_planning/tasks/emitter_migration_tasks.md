# Task List: Emitter Migration & Type System Unification

**Status:** Part A Partially Complete, Part B Complete
**Estimated Total Effort:** 3-5 days
**Prerequisites:** All tests passing (3591+ tests)
**Branch Name:** `feature/emitter-migration-type-unification`

---

## Implementation Summary (2026-01-19)

### Part A: Emitter Migration

**Status:** Partially Complete - CodeGenInfo integration done, legacy field removal blocked

**Completed:**
- ✅ A.1: Verified baseline (3591 tests passing, UsePrecomputedCodeGenInfo = true)
- ✅ A.2: Audited legacy field usage
- ✅ A.3: Created `TryGetCSharpNameFromCodeGenInfo()` and integrated into `GetMangledVariableName()`
- ✅ A.4: Updated `GenerateAssignment()` to check `CodeGenInfo.IsModuleLevel`
- ✅ A.5: From-import handling already covered by A.3 (CodeGenInfo used for symbol resolution)
- ✅ A.6: Added 12 migration tests in `EmitterMigrationTests.cs`
- ⚠️ A.7-A.9: Marked legacy code for removal but CANNOT fully remove yet

**Blocker Discovered:**
`CodeGenInfoComputer` has a limitation that prevents full legacy field removal:
- When an Assignment (`x = 5`) precedes a VariableDeclaration (`x: auto = 10`), the pre-scan in `GenerateModuleClass` correctly identifies execution order issues
- However, `CodeGenInfoComputer.ProcessModuleLevelVariable()` marks the variable as `IsModuleLevel = true` without checking for prior assignments
- Result: 5 tests fail when enabling CodeGenInfo for all integration tests

**Required to Complete Part A:**
1. Enhance `CodeGenInfoComputer` to track assignments before declarations (like `GenerateModuleClass` pre-scan does)
2. Update `CodeGenInfoComputer.ProcessModuleLevelVariable()` to NOT set `IsModuleLevel = true` when variable has execution order issues
3. Enable `computeCodeGenInfo: true` in `IntegrationTestBase`
4. Remove `[LEGACY FALLBACK]` marked code paths

**Commits Made:**
1. `feat(codegen): integrate CodeGenInfo into GetMangledVariableName with fallback`
2. `feat(codegen): use CodeGenInfo for module-level variable checks with fallback`
3. `test(codegen): add migration tests for CodeGenInfo-based emission`
4. `docs(codegen): mark legacy fallback code for future removal`

### Part B: Type System Unification

**Status:** Complete

**Completed:**
- ✅ B.1: Created `type_system_analysis.md` documenting type relationships
- ✅ B.2: Added `ITypeInfo` interface and implemented on `SemanticType`
  - Base class implements common methods: `MakeNullable()`, `UnwrapNullable()`
  - Subclasses override: `IsNullable`, `IsValueType`, `ClrType`, `DeclaringSymbol`
- ✅ B.3: Created `TypeRegistry` class for centralized type lookup
- ✅ B.4: Created `type_system_design.md` documenting invariants and design
- ✅ B.5: Added placeholder types for v0.2.x features:
  - `UnionType` - for tagged unions / ADTs
  - `TaskType` - for async functions returning Task<T>
- ✅ B.6: Created `TypeUtils` class with utility methods:
  - `IsNumeric`, `IsInteger`, `IsFloatingPoint`, `IsString`, `IsBool`
  - `IsCollection`, `IsList`, `IsDict`, `IsSet`, `IsTuple`
  - `GetElementType`, `GetKeyType`, `UnwrapAllNullable`
  - `AreEquivalent`, `GetCommonType`
- ✅ B.7: Added 43 tests in `TypeUtilsTests.cs`

**Files Created:**
- `src/Sharpy.Compiler/Semantic/ITypeInfo.cs`
- `src/Sharpy.Compiler/Semantic/TypeRegistry.cs`
- `src/Sharpy.Compiler/Semantic/TypeUtils.cs`
- `src/Sharpy.Compiler.Tests/Semantic/TypeUtilsTests.cs`
- `docs/implementation_planning/type_system_analysis.md`
- `docs/implementation_planning/type_system_design.md`

**Files Modified:**
- `src/Sharpy.Compiler/Semantic/SemanticType.cs` - Added `ITypeInfo` implementation and placeholder types

**Test Count:** 3646 tests passing (up from 3591 baseline)

---

## Overview

This document provides step-by-step instructions for completing two architectural improvements:

1. **Part A: Emitter Migration (Deferred Phase 5)** — Migrate `RoslynEmitter` to use `CodeGenInfo`-aware helper methods instead of legacy tracking fields.

2. **Part B: Type System Unification** — Consolidate the overlapping `TypeAnnotation` → `SemanticType` → `TypeSymbol` hierarchy into a clearer model.

### Design Principles

| Principle | Description |
|-----------|-------------|
| **Two-Way Doors** | All changes should be reversible. We add new functionality alongside existing code, verify it works, then remove the old code. |
| **Incremental Testing** | After each task, run the full test suite. Never proceed with failing tests. |
| **Commit Often** | Create commits at each checkpoint (marked with 🏁). This enables easy bisection and rollback. |
| **Future-Proof** | Consider tagged unions, async/await, LSP, and other v0.2.x features when making decisions. |

### How to Use This Document

1. Work through tasks in order within each part.
2. Each task has a checkbox `[ ]` — check it when complete.
3. Run `dotnet test` after each task.
4. Create git commits at 🏁 checkpoints.
5. If tests fail, debug before proceeding.

---

## Part A: Emitter Migration (Phase 5 Completion)

**Goal:** Replace all usages of legacy tracking fields in `RoslynEmitter` with `CodeGenInfo`-aware helper methods.

**Current State:** Helper methods exist with fallback behavior. Legacy fields still populated and used.

**Target State:** Legacy fields removed; all emission uses `Symbol.CodeGenInfo`.

---

### A.1: Preparation

#### A.1.1: Create Feature Branch
```bash
git checkout -b feature/emitter-migration-type-unification
git push -u origin feature/emitter-migration-type-unification
```

#### A.1.2: Verify Baseline
- [ ] Run `dotnet test` — all tests must pass
- [ ] Record test count: `____` tests passing
- [ ] Verify `UsePrecomputedCodeGenInfo` is `true` in `ProjectConfig.cs`

---

### A.2: Audit Legacy Field Usage

Before migrating, understand exactly where legacy fields are used.

#### A.2.1: Document Current Usage
- [ ] Open `RoslynEmitter.cs` and list all legacy tracking fields:
  - `_variableVersions` — Used in: `GetMangledVariableName()`
  - `_constVariables` — Used in: `GetMangledVariableName()`, `GenerateVariableDeclaration()`
  - `_moduleConstVariables` — Used in: `GetMangledVariableName()`, `GenerateModuleClass()`
  - `_moduleVariables` — Used in: `GetMangledVariableName()`, `GenerateAssignment()`, `GenerateModuleClass()`
  - `_variablesWithExecutionOrderIssues` — Used in: `GenerateModuleLevelField()`, `GenerateModuleClass()`
  - `_fromImportSymbols` — Used in: `GetMangledVariableName()`
  - `_importAliasToOriginal` — Used in: `GetMangledVariableName()`

- [ ] Search for all usages in partial files:
  ```bash
  grep -rn "_moduleVariables\|_moduleConstVariables\|_constVariables\|_variableVersions\|_variablesWithExecutionOrderIssues\|_fromImportSymbols\|_importAliasToOriginal" src/Sharpy.Compiler/CodeGen/
  ```

- [ ] Create a migration checklist noting each file and line number

---

### A.3: Migrate GetMangledVariableName

This is the core method that needs migration. We'll create a new method that uses `CodeGenInfo` and gradually transition to it.

#### A.3.1: Create Symbol-Based Name Resolution
- [ ] In `RoslynEmitter.cs`, add a new method above `GetMangledVariableName()`:

```csharp
/// <summary>
/// Resolve the C# name for a variable using CodeGenInfo.
/// Returns null if CodeGenInfo is not available (falls back to legacy).
/// </summary>
private string? TryGetCSharpNameFromCodeGenInfo(string sharpyName, bool isNewDeclaration)
{
    var symbol = _context.LookupSymbol(sharpyName);
    if (symbol?.CodeGenInfo == null)
        return null;

    var info = symbol.CodeGenInfo;

    // For new declarations, track the version increment
    // Note: CodeGenInfo has the version pre-computed, but for redeclarations
    // within the same emission scope, we may need runtime tracking
    if (isNewDeclaration && !info.IsModuleLevel)
    {
        // Local variable redeclarations still need runtime tracking
        // because they happen during emission, not semantic analysis
        return null; // Fall back to legacy for local redeclarations
    }

    return info.GetVersionedCSharpName();
}
```

- [ ] Run tests — should still pass (method not yet called)

#### A.3.2: Integrate Into GetMangledVariableName
- [ ] Modify `GetMangledVariableName()` to try CodeGenInfo first:

```csharp
private string GetMangledVariableName(string name, bool isNewDeclaration)
{
    // Try CodeGenInfo-based resolution first
    var codeGenName = TryGetCSharpNameFromCodeGenInfo(name, isNewDeclaration);
    if (codeGenName != null)
        return codeGenName;

    // Fall back to existing logic...
    var baseName = NameMangler.ToCamelCase(name);
    // ... rest of existing code unchanged
```

- [ ] Run tests — all should pass

#### A.3.3: Verify CodeGenInfo Is Preferred
- [ ] Add a debug log or temporary counter to track how often fallback is used
- [ ] Run a few compilation tests manually
- [ ] Verify CodeGenInfo path is taken for module-level variables

🏁 **Checkpoint A.3: Commit**
```bash
git add -A
git commit -m "feat(codegen): integrate CodeGenInfo into GetMangledVariableName with fallback"
```

---

### A.4: Migrate Module-Level Variable Tracking

#### A.4.1: Update GenerateModuleClass Pre-Scan
The pre-scan in `GenerateModuleClass()` populates `_moduleConstVariables` and `_moduleVariables`. We need to keep this for now because it also detects execution order issues, but we can stop relying on it for name resolution.

- [ ] Verify `CodeGenInfoComputer.ComputeForModule()` already handles module-level detection
- [ ] Ensure `HasExecutionOrderIssues` is correctly computed in `CodeGenInfoComputer`

#### A.4.2: Update GenerateAssignment
- [ ] In `RoslynEmitter.Statements.cs`, find `GenerateAssignment()`:

```csharp
// Current code:
if (_variableVersions.ContainsKey(baseName) ||
    _moduleVariables.Contains(name.Name) ||
    _moduleConstVariables.Contains(name.Name))
```

- [ ] Update to also check CodeGenInfo:

```csharp
// Check if variable exists using CodeGenInfo or legacy tracking
var symbol = _context.LookupSymbol(name.Name);
var existsViaCodeGenInfo = symbol?.CodeGenInfo?.IsModuleLevel == true;
var existsViaLegacy = _variableVersions.ContainsKey(baseName) ||
    _moduleVariables.Contains(name.Name) ||
    _moduleConstVariables.Contains(name.Name);

if (existsViaCodeGenInfo || existsViaLegacy)
```

- [ ] Run tests — all should pass

#### A.4.3: Update GenerateModuleLevelField
- [ ] In `RoslynEmitter.Statements.cs`, find `GenerateModuleLevelField()`:
- [ ] Update the execution order check:

```csharp
// Current:
if (_variablesWithExecutionOrderIssues.Contains(varDecl.Name))
    return null;

// Updated (check both):
var symbol = _context.LookupSymbol(varDecl.Name);
var hasIssuesViaCodeGenInfo = symbol?.CodeGenInfo?.HasExecutionOrderIssues == true;
var hasIssuesViaLegacy = _variablesWithExecutionOrderIssues.Contains(varDecl.Name);
if (hasIssuesViaCodeGenInfo || hasIssuesViaLegacy)
    return null;
```

- [ ] Run tests — all should pass

🏁 **Checkpoint A.4: Commit**
```bash
git add -A
git commit -m "feat(codegen): use CodeGenInfo for module-level variable checks with fallback"
```

---

### A.5: Migrate Import Symbol Tracking

#### A.5.1: Update From-Import Handling
- [ ] In `GetMangledVariableName()`, the from-import check currently uses `_fromImportSymbols`:

```csharp
// Current:
if (_fromImportSymbols.Contains(name))
{
    var actualName = _importAliasToOriginal.TryGetValue(name, out var originalName)
        ? originalName
        : name;
    // ...
}
```

- [ ] Update to prefer CodeGenInfo:

```csharp
// Check via CodeGenInfo first
var symbol = _context.LookupSymbol(name);
if (symbol?.CodeGenInfo?.ImportKind == ImportKind.FromImport ||
    symbol?.CodeGenInfo?.ImportKind == ImportKind.FromImportWithAlias)
{
    var actualName = symbol.CodeGenInfo.OriginalImportName ?? name;
    if (IsConstantCaseName(actualName))
        return NameMangler.ToConstantCase(actualName);
    else
        return NameMangler.ToPascalCase(actualName);
}

// Fall back to legacy tracking
if (_fromImportSymbols.Contains(name))
{
    // ... existing code
}
```

- [ ] Run tests — all should pass

🏁 **Checkpoint A.5: Commit**
```bash
git add -A
git commit -m "feat(codegen): use CodeGenInfo for from-import symbol resolution"
```

---

### A.6: Add Migration Tests

Before removing legacy code, add tests that specifically verify CodeGenInfo-based resolution works.

#### A.6.1: Create Dedicated Migration Tests
- [ ] Create `src/Sharpy.Compiler.Tests/CodeGen/EmitterMigrationTests.cs`:

```csharp
using Xunit;
using Sharpy.Compiler.Semantic;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// Tests verifying CodeGenInfo-based emission works correctly
/// after migration from legacy tracking fields.
/// </summary>
public class EmitterMigrationTests
{
    [Fact]
    public void ModuleLevelVariable_UsesCodeGenInfo()
    {
        var code = @"
my_variable: int = 42
";
        var result = TestHelpers.CompileToString(code);

        // Should use PascalCase for module-level variable
        Assert.Contains("public static int MyVariable = 42;", result);
    }

    [Fact]
    public void ModuleLevelConstant_UsesCodeGenInfo()
    {
        var code = @"
const MAX_VALUE: int = 100
";
        var result = TestHelpers.CompileToString(code);

        // Should use CONSTANT_CASE
        Assert.Contains("MAX_VALUE", result);
    }

    [Fact]
    public void FromImport_UsesCodeGenInfo()
    {
        // This test may need multi-file setup
        // Verify imported symbols maintain correct casing
    }

    [Fact]
    public void ExecutionOrderIssues_DetectedViaCodeGenInfo()
    {
        var code = @"
x: int = y + 1
y: int = 10
";
        var result = TestHelpers.CompileToString(code);

        // Variables with execution order issues should be in Main
        Assert.Contains("var x = ", result.ToLower());
    }
}
```

- [ ] Implement the tests (adjust based on test infrastructure)
- [ ] Run tests — all should pass

🏁 **Checkpoint A.6: Commit**
```bash
git add -A
git commit -m "test(codegen): add migration tests for CodeGenInfo-based emission"
```

---

### A.7: Remove Legacy Fallback (Gradual)

Now that CodeGenInfo is the primary path, we can start removing fallback code.

#### A.7.1: Remove Fallback in GetMangledVariableName
- [ ] In `GetMangledVariableName()`, remove the `_moduleConstVariables.Contains()` check
- [ ] Run tests — if any fail, the test is using legacy path; investigate
- [ ] Remove the `_moduleVariables.Contains()` check
- [ ] Run tests
- [ ] Remove the `_fromImportSymbols.Contains()` check
- [ ] Run tests

#### A.7.2: Remove Fallback in Other Methods
- [ ] Update `GenerateAssignment()` to only use CodeGenInfo check
- [ ] Run tests
- [ ] Update `GenerateModuleLevelField()` to only use CodeGenInfo check
- [ ] Run tests

🏁 **Checkpoint A.7: Commit**
```bash
git add -A
git commit -m "refactor(codegen): remove legacy fallback in name resolution"
```

---

### A.8: Remove Legacy Field Population

#### A.8.1: Stop Populating Legacy Sets in GenerateModuleClass
- [ ] In `RoslynEmitter.ModuleClass.cs`, find `GenerateModuleClass()`:
- [ ] Comment out (don't delete yet) the population of:
  - `_moduleConstVariables.Add()`
  - `_moduleVariables.Add()`
  - `_variablesWithExecutionOrderIssues` computation
- [ ] Run tests — if any fail, there's still a dependency

#### A.8.2: Stop Populating Legacy Sets in GenerateVariableDeclaration
- [ ] In `RoslynEmitter.Statements.cs`, find `GenerateVariableDeclaration()`:
- [ ] Comment out `_constVariables.Add(varDecl.Name)`
- [ ] Run tests

#### A.8.3: Verify No More Usages
- [ ] Run grep to confirm no remaining usages:
```bash
grep -rn "_moduleVariables\|_moduleConstVariables\|_constVariables" src/Sharpy.Compiler/CodeGen/ | grep -v "^\s*//"
```

🏁 **Checkpoint A.8: Commit**
```bash
git add -A
git commit -m "refactor(codegen): stop populating legacy tracking sets"
```

---

### A.9: Remove Legacy Field Declarations

#### A.9.1: Remove Deprecated Fields
- [ ] In `RoslynEmitter.cs`, delete the following field declarations:
  - `_moduleConstVariables`
  - `_moduleVariables`
  - `_variablesWithExecutionOrderIssues`
  - `_constVariables` (if no longer needed for local const tracking)
  - `_fromImportSymbols`
  - `_importAliasToOriginal`

**Note:** Keep `_variableVersions` for now — it's still needed for local variable redeclarations within a function scope. This is a TWO-WAY DOOR: we can migrate local variable tracking to CodeGenInfo later.

- [ ] Run tests — all should pass

#### A.9.2: Update Deprecation Comments
- [ ] Remove the "LEGACY TRACKING FIELDS" comment block
- [ ] Add a note about `_variableVersions` still being used for local scope

🏁 **Checkpoint A.9: Commit**
```bash
git add -A
git commit -m "refactor(codegen): remove deprecated legacy tracking fields"
```

---

### A.10: Documentation & Cleanup

#### A.10.1: Update TASK_precompute_codegen_info_COMPLETED.md
- [ ] Mark Phase 5 as completed
- [ ] Note which fields were removed and which remain
- [ ] Document any edge cases discovered

#### A.10.2: Update Architecture Review Document
- [ ] In `docs/implementation_planning/architecture_review_and_recommendations.md`:
- [ ] Mark Recommendation #4 as fully complete

🏁 **Checkpoint A.10: Final Commit for Part A**
```bash
git add -A
git commit -m "docs: mark Phase 5 (Emitter Migration) as complete"
```

---

## Part B: Type System Unification

**Goal:** Create a clearer separation between syntax types (`TypeAnnotation`), semantic types (`SemanticType`), and declaration types (`TypeSymbol`).

**Current Problems:**
1. `TypeAnnotation` (AST) is just syntax with no semantics
2. `SemanticType` is resolved during type checking but references `TypeSymbol`
3. `TypeSymbol` contains members but also points to `ClrType`
4. No single source of truth for "what is this type"

**Design Decision (Two-Way Door):**
We will NOT replace `SemanticType` entirely. Instead, we will:
1. Clarify ownership boundaries
2. Add missing functionality to `SemanticType`
3. Reduce confusion by documenting invariants
4. Prepare for future features (tagged unions, async)

This is a two-way door because we're adding capabilities, not removing existing functionality.

---

### B.1: Analysis Phase

#### B.1.1: Document Current Type Relationships
- [ ] Create a diagram showing type relationships:

```
TypeAnnotation (AST - Parser)
    ├── SimpleTypeAnnotation ("int", "str", "MyClass")
    ├── GenericTypeAnnotation ("list[int]", "dict[K,V]")
    ├── NullableTypeAnnotation ("int?")
    └── FunctionTypeAnnotation ("(int) -> str")
           ↓ resolved by TypeResolver
SemanticType (Semantic Analysis)
    ├── BuiltinType (int, str, bool, float)
    │      └── ClrType: System.Type
    ├── UserDefinedType (classes, structs)
    │      └── Symbol: TypeSymbol
    ├── GenericType (list[int])
    │      ├── TypeArguments: List<SemanticType>
    │      └── GenericDefinition: TypeSymbol
    ├── NullableType (T?)
    │      └── UnderlyingType: SemanticType
    ├── FunctionType ((int) -> str)
    ├── TupleType ((int, str))
    ├── TypeParameterType (T in generic context)
    └── ... others
           ↓ backs declaration
TypeSymbol (Symbol Table)
    ├── Name, Kind (Class/Struct/Interface/Enum)
    ├── ClrType: System.Type (for interop)
    ├── Fields: List<VariableSymbol>
    ├── Methods: List<FunctionSymbol>
    ├── BaseType: TypeSymbol
    └── Interfaces: List<TypeSymbol>
```

- [ ] Save diagram to `docs/implementation_planning/type_system_analysis.md`

#### B.1.2: Identify Pain Points
- [ ] List places where type confusion occurs:
  1. `UserDefinedType.Symbol` vs `GenericType.GenericDefinition`
  2. `BuiltinType.ClrType` vs `TypeSymbol.ClrType`
  3. When to use `SemanticType` vs `TypeSymbol` for type checking
  4. `Symbol.Type` is `SemanticType` but types are represented by `TypeSymbol`

🏁 **Checkpoint B.1: Commit**
```bash
git add -A
git commit -m "docs: analyze current type system relationships"
```

---

### B.2: Add TypeInfo Interface

Create a unified interface that all type representations implement.

#### B.2.1: Define ITypeInfo Interface
- [ ] Create `src/Sharpy.Compiler/Semantic/ITypeInfo.cs`:

```csharp
namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Common interface for all type representations in the compiler.
/// Provides a unified view regardless of whether the type is:
/// - A built-in primitive (BuiltinType)
/// - A user-defined class/struct (UserDefinedType)
/// - A generic instantiation (GenericType)
/// - A type parameter (TypeParameterType)
///
/// This is a TWO-WAY DOOR: Adding this interface doesn't change existing behavior.
/// It provides a common abstraction for future features.
/// </summary>
public interface ITypeInfo
{
    /// <summary>
    /// Human-readable name for diagnostics and display.
    /// Examples: "int", "list[str]", "MyClass", "T"
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Whether this type can hold null values.
    /// True for nullable types (T?) and reference types.
    /// </summary>
    bool IsNullable { get; }

    /// <summary>
    /// Whether this is a value type (struct, primitive) or reference type (class).
    /// </summary>
    bool IsValueType { get; }

    /// <summary>
    /// The CLR type, if known. Null for type parameters and some user-defined types.
    /// </summary>
    Type? ClrType { get; }

    /// <summary>
    /// The declaring TypeSymbol, if this is a user-defined type.
    /// Null for built-in types and type parameters.
    /// </summary>
    TypeSymbol? DeclaringSymbol { get; }

    /// <summary>
    /// Check if this type is assignable to another type.
    /// Includes subtyping, interface implementation, and implicit conversions.
    /// </summary>
    bool IsAssignableTo(ITypeInfo other);

    /// <summary>
    /// Create a nullable version of this type (T?).
    /// </summary>
    ITypeInfo MakeNullable();

    /// <summary>
    /// If this is a nullable type, get the underlying type. Otherwise returns this.
    /// </summary>
    ITypeInfo UnwrapNullable();
}
```

- [ ] Run tests — should pass (interface only, no implementation changes yet)

#### B.2.2: Implement ITypeInfo on SemanticType
- [ ] Modify `SemanticType.cs` to implement `ITypeInfo`:

```csharp
public abstract record SemanticType : ITypeInfo
{
    // Existing abstract method
    public abstract string GetDisplayName();

    // ITypeInfo.DisplayName delegates to existing method
    string ITypeInfo.DisplayName => GetDisplayName();

    // Default implementations (override in subclasses as needed)
    public virtual bool IsNullable => false;
    public virtual bool IsValueType => false;
    public virtual Type? ClrType => null;
    public virtual TypeSymbol? DeclaringSymbol => null;

    public virtual bool IsAssignableTo(ITypeInfo other)
    {
        if (other is SemanticType semanticType)
            return this.IsAssignableTo(semanticType);
        return false;
    }

    public virtual ITypeInfo MakeNullable()
    {
        if (this is NullableType)
            return this;
        return new NullableType { UnderlyingType = this };
    }

    public virtual ITypeInfo UnwrapNullable()
    {
        if (this is NullableType nullable)
            return nullable.UnderlyingType;
        return this;
    }
}
```

- [ ] Run tests — should pass

#### B.2.3: Implement ITypeInfo Properties in Subclasses
- [ ] Update `BuiltinType`:
```csharp
public record BuiltinType : SemanticType
{
    // ... existing code ...

    public override bool IsValueType =>
        ClrType?.IsValueType ?? false;

    public override Type? ClrType { get; init; }
}
```

- [ ] Update `UserDefinedType`:
```csharp
public record UserDefinedType : SemanticType
{
    // ... existing code ...

    public override bool IsValueType =>
        Symbol?.TypeKind == TypeKind.Struct;

    public override TypeSymbol? DeclaringSymbol => Symbol;

    public override Type? ClrType => Symbol?.ClrType;
}
```

- [ ] Update `NullableType`:
```csharp
public record NullableType : SemanticType
{
    // ... existing code ...

    public override bool IsNullable => true;

    public override bool IsValueType =>
        UnderlyingType is SemanticType st && st.IsValueType;
}
```

- [ ] Run tests — should pass

🏁 **Checkpoint B.2: Commit**
```bash
git add -A
git commit -m "feat(types): add ITypeInfo interface to unify type representations"
```

---

### B.3: Add Type Registry

Create a central registry for looking up type information.

#### B.3.1: Create TypeRegistry Class
- [ ] Create `src/Sharpy.Compiler/Semantic/TypeRegistry.cs`:

```csharp
namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Central registry for type information.
/// Provides canonical access to types and caches resolved type info.
///
/// This addresses the problem of type information being scattered across
/// TypeAnnotation, SemanticType, and TypeSymbol.
/// </summary>
public class TypeRegistry
{
    private readonly Dictionary<string, SemanticType> _builtinTypes = new();
    private readonly Dictionary<string, TypeSymbol> _userTypes = new();
    private readonly SymbolTable _symbolTable;

    public TypeRegistry(SymbolTable symbolTable)
    {
        _symbolTable = symbolTable;
        InitializeBuiltinTypes();
    }

    private void InitializeBuiltinTypes()
    {
        // Register canonical builtin types
        _builtinTypes["int"] = SemanticType.Int;
        _builtinTypes["int32"] = SemanticType.Int;
        _builtinTypes["long"] = SemanticType.Long;
        _builtinTypes["int64"] = SemanticType.Long;
        _builtinTypes["float"] = SemanticType.Float;
        _builtinTypes["float64"] = SemanticType.Float;
        _builtinTypes["float32"] = SemanticType.Float32;
        _builtinTypes["double"] = SemanticType.Double;
        _builtinTypes["bool"] = SemanticType.Bool;
        _builtinTypes["str"] = SemanticType.Str;
        _builtinTypes["object"] = SemanticType.Object;
    }

    /// <summary>
    /// Get a type by name, checking builtins first, then user-defined.
    /// </summary>
    public SemanticType? GetType(string name)
    {
        if (_builtinTypes.TryGetValue(name, out var builtin))
            return builtin;

        var symbol = _symbolTable.Lookup(name);
        if (symbol is TypeSymbol typeSymbol)
        {
            return new UserDefinedType { Name = name, Symbol = typeSymbol };
        }

        return null;
    }

    /// <summary>
    /// Register a user-defined type.
    /// Called during NameResolver when type definitions are encountered.
    /// </summary>
    public void RegisterType(TypeSymbol typeSymbol)
    {
        _userTypes[typeSymbol.Name] = typeSymbol;
    }

    /// <summary>
    /// Check if a type name is a builtin type.
    /// </summary>
    public bool IsBuiltinType(string name) => _builtinTypes.ContainsKey(name);

    /// <summary>
    /// Get all registered user-defined types.
    /// </summary>
    public IEnumerable<TypeSymbol> GetUserDefinedTypes() => _userTypes.Values;
}
```

- [ ] Run tests — should pass (class not yet used)

#### B.3.2: Integrate TypeRegistry into TypeChecker
- [ ] In `TypeChecker.cs`, add a `TypeRegistry` field:
```csharp
private readonly TypeRegistry _typeRegistry;

public TypeChecker(SymbolTable symbolTable, /* other params */)
{
    _symbolTable = symbolTable;
    _typeRegistry = new TypeRegistry(symbolTable);
    // ... rest of constructor
}
```

- [ ] Run tests — should pass

🏁 **Checkpoint B.3: Commit**
```bash
git add -A
git commit -m "feat(types): add TypeRegistry for centralized type lookup"
```

---

### B.4: Document Type System Invariants

#### B.4.1: Add XML Documentation
- [ ] Update `SemanticType.cs` with clear documentation:

```csharp
/// <summary>
/// Represents a resolved type during semantic analysis.
///
/// <para><b>Design Invariants:</b></para>
/// <list type="bullet">
/// <item><description>
/// SemanticType is IMMUTABLE - once created, it never changes.
/// </description></item>
/// <item><description>
/// SemanticType represents TYPE USAGE, not TYPE DECLARATION.
/// For declarations, see TypeSymbol.
/// </description></item>
/// <item><description>
/// User-defined types (UserDefinedType) always reference their declaring TypeSymbol.
/// </description></item>
/// <item><description>
/// Generic types (GenericType) contain resolved type arguments, not parameters.
/// </description></item>
/// </list>
///
/// <para><b>Relationship to Other Types:</b></para>
/// <list type="bullet">
/// <item><description>
/// TypeAnnotation (AST) → resolved by TypeResolver → SemanticType
/// </description></item>
/// <item><description>
/// TypeSymbol (Symbol) → used by → UserDefinedType.Symbol
/// </description></item>
/// </list>
///
/// <para><b>Future Extensions (v0.2.x):</b></para>
/// <list type="bullet">
/// <item><description>
/// UnionType - for tagged unions / ADTs
/// </description></item>
/// <item><description>
/// TaskType - for async functions returning Task&lt;T&gt;
/// </description></item>
/// </list>
/// </summary>
public abstract record SemanticType : ITypeInfo
```

- [ ] Add similar documentation to `TypeSymbol`

#### B.4.2: Add Design Document
- [ ] Create `docs/implementation_planning/type_system_design.md`:

```markdown
# Sharpy Type System Design

## Overview

The Sharpy compiler uses three complementary type representations:

1. **TypeAnnotation** (AST) - Syntax-level type expressions
2. **SemanticType** - Resolved types for type checking
3. **TypeSymbol** - Type declarations with members

## When to Use Each

| Scenario | Use |
|----------|-----|
| Parsing type syntax | TypeAnnotation |
| Type checking expressions | SemanticType |
| Looking up type members | TypeSymbol |
| Checking assignability | SemanticType.IsAssignableTo() |
| Code generation | SemanticType + CodeGenInfo |

## Future Considerations

### Tagged Unions (v0.2.x)
- Will add `UnionType : SemanticType`
- `TypeSymbol.TypeKind.Union`
- Case types as nested TypeSymbols

### Async/Await (v0.2.x)
- `TaskType : SemanticType` for `Task<T>` wrapping
- Async functions return `TaskType`
```

🏁 **Checkpoint B.4: Commit**
```bash
git add -A
git commit -m "docs: document type system invariants and design"
```

---

### B.5: Add Future-Proof Type Extensions

Prepare for tagged unions and async by adding placeholder types.

#### B.5.1: Add UnionType (Placeholder)
- [ ] Add to `SemanticType.cs`:

```csharp
/// <summary>
/// Represents a tagged union type (v0.2.x feature).
/// Example: Result[T, E] with cases Ok(T) and Err(E)
///
/// This is a placeholder for future implementation.
/// </summary>
public record UnionType : SemanticType
{
    public string Name { get; init; } = string.Empty;
    public TypeSymbol? Symbol { get; init; }

    /// <summary>
    /// The union case types. Each case is itself a type.
    /// </summary>
    public List<SemanticType> CaseTypes { get; init; } = new();

    public override string GetDisplayName() => Name;

    public override bool IsAssignableTo(SemanticType other)
    {
        if (other is UnionType otherUnion && Name == otherUnion.Name)
            return true;
        return base.IsAssignableTo(other);
    }
}
```

#### B.5.2: Add TaskType (Placeholder)
- [ ] Add to `SemanticType.cs`:

```csharp
/// <summary>
/// Represents an async Task type (v0.2.x feature).
/// Wraps the return type of async functions.
///
/// This is a placeholder for future implementation.
/// </summary>
public record TaskType : SemanticType
{
    /// <summary>
    /// The result type (T in Task&lt;T&gt;). Null for Task (void return).
    /// </summary>
    public SemanticType? ResultType { get; init; }

    public override string GetDisplayName()
    {
        if (ResultType == null)
            return "Task";
        return $"Task[{ResultType.GetDisplayName()}]";
    }

    public override Type? ClrType =>
        ResultType == null
            ? typeof(System.Threading.Tasks.Task)
            : null; // Generic Task<T> needs runtime resolution
}
```

- [ ] Run tests — should pass (placeholders not used)

🏁 **Checkpoint B.5: Commit**
```bash
git add -A
git commit -m "feat(types): add placeholder types for tagged unions and async"
```

---

### B.6: Add Type Utility Methods

Add commonly-needed type operations to reduce code duplication.

#### B.6.1: Create TypeUtils Class
- [ ] Create `src/Sharpy.Compiler/Semantic/TypeUtils.cs`:

```csharp
namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Utility methods for working with types.
/// Centralizes common operations to avoid duplication.
/// </summary>
public static class TypeUtils
{
    /// <summary>
    /// Check if a type is numeric (int, long, float, double, decimal).
    /// </summary>
    public static bool IsNumeric(SemanticType type)
    {
        if (type is BuiltinType builtin)
        {
            return builtin.ClrType == typeof(int) ||
                   builtin.ClrType == typeof(long) ||
                   builtin.ClrType == typeof(float) ||
                   builtin.ClrType == typeof(double) ||
                   builtin.ClrType == typeof(decimal) ||
                   builtin.ClrType == typeof(short) ||
                   builtin.ClrType == typeof(byte) ||
                   builtin.ClrType == typeof(sbyte) ||
                   builtin.ClrType == typeof(ushort) ||
                   builtin.ClrType == typeof(uint) ||
                   builtin.ClrType == typeof(ulong);
        }
        return false;
    }

    /// <summary>
    /// Check if a type is a string type.
    /// </summary>
    public static bool IsString(SemanticType type)
    {
        return type is BuiltinType { Name: "str" };
    }

    /// <summary>
    /// Check if a type is a collection (list, dict, set).
    /// </summary>
    public static bool IsCollection(SemanticType type)
    {
        return type is GenericType generic &&
            (generic.Name == "list" || generic.Name == "dict" || generic.Name == "set");
    }

    /// <summary>
    /// Get the element type of a collection, if applicable.
    /// </summary>
    public static SemanticType? GetElementType(SemanticType type)
    {
        if (type is GenericType generic)
        {
            if (generic.Name == "list" || generic.Name == "set")
                return generic.TypeArguments.FirstOrDefault();
            if (generic.Name == "dict")
                return generic.TypeArguments.Skip(1).FirstOrDefault(); // Value type
        }
        return null;
    }

    /// <summary>
    /// Unwrap nullable types recursively.
    /// </summary>
    public static SemanticType UnwrapAllNullable(SemanticType type)
    {
        while (type is NullableType nullable)
            type = nullable.UnderlyingType;
        return type;
    }

    /// <summary>
    /// Check if two types are structurally equivalent.
    /// </summary>
    public static bool AreEquivalent(SemanticType a, SemanticType b)
    {
        // Unwrap nullables for comparison
        var unwrappedA = UnwrapAllNullable(a);
        var unwrappedB = UnwrapAllNullable(b);

        // Check nullable mismatch
        bool aNullable = a is NullableType;
        bool bNullable = b is NullableType;
        if (aNullable != bNullable)
            return false;

        return unwrappedA.Equals(unwrappedB);
    }
}
```

- [ ] Run tests — should pass

#### B.6.2: Add Tests for TypeUtils
- [ ] Create `src/Sharpy.Compiler.Tests/Semantic/TypeUtilsTests.cs`:

```csharp
using Xunit;
using Sharpy.Compiler.Semantic;

namespace Sharpy.Compiler.Tests.Semantic;

public class TypeUtilsTests
{
    [Theory]
    [InlineData("int", true)]
    [InlineData("float", true)]
    [InlineData("str", false)]
    [InlineData("bool", false)]
    public void IsNumeric_ReturnsCorrectly(string typeName, bool expected)
    {
        var type = typeName switch
        {
            "int" => SemanticType.Int,
            "float" => SemanticType.Float,
            "str" => SemanticType.Str,
            "bool" => SemanticType.Bool,
            _ => SemanticType.Unknown
        };

        Assert.Equal(expected, TypeUtils.IsNumeric(type));
    }

    [Fact]
    public void UnwrapAllNullable_RemovesAllLayers()
    {
        var doubleNullable = new NullableType
        {
            UnderlyingType = new NullableType
            {
                UnderlyingType = SemanticType.Int
            }
        };

        var unwrapped = TypeUtils.UnwrapAllNullable(doubleNullable);

        Assert.Equal(SemanticType.Int, unwrapped);
    }
}
```

- [ ] Run tests — should pass

🏁 **Checkpoint B.6: Commit**
```bash
git add -A
git commit -m "feat(types): add TypeUtils with common type operations"
```

---

### B.7: Integration Tests

#### B.7.1: Verify Type System Works End-to-End
- [ ] Create `src/Sharpy.Compiler.Tests/Semantic/TypeSystemIntegrationTests.cs`:

```csharp
using Xunit;
using Sharpy.Compiler.Semantic;

namespace Sharpy.Compiler.Tests.Semantic;

public class TypeSystemIntegrationTests
{
    [Fact]
    public void ITypeInfo_WorksWithSemanticTypes()
    {
        ITypeInfo intType = SemanticType.Int;
        ITypeInfo strType = SemanticType.Str;

        Assert.Equal("int", intType.DisplayName);
        Assert.True(intType.IsValueType);
        Assert.False(strType.IsValueType);
    }

    [Fact]
    public void TypeRegistry_FindsBuiltinTypes()
    {
        var registry = new TypeRegistry(new SymbolTable());

        Assert.NotNull(registry.GetType("int"));
        Assert.NotNull(registry.GetType("str"));
        Assert.True(registry.IsBuiltinType("bool"));
    }

    [Fact]
    public void NullableType_ImplementsITypeInfo()
    {
        var nullable = new NullableType { UnderlyingType = SemanticType.Int };
        ITypeInfo typeInfo = nullable;

        Assert.True(typeInfo.IsNullable);
        Assert.Equal("int?", typeInfo.DisplayName);

        var unwrapped = typeInfo.UnwrapNullable();
        Assert.Equal(SemanticType.Int, unwrapped);
    }
}
```

- [ ] Run tests — should pass

🏁 **Checkpoint B.7: Commit**
```bash
git add -A
git commit -m "test(types): add type system integration tests"
```

---

### B.8: Final Cleanup

#### B.8.1: Remove Any Dead Code
- [ ] Search for unused type-related code:
```bash
grep -rn "// TODO\|// HACK\|// FIXME" src/Sharpy.Compiler/Semantic/
```
- [ ] Address or document any findings

#### B.8.2: Update Architecture Review
- [ ] In `docs/implementation_planning/architecture_review_and_recommendations.md`:
- [ ] Update Recommendation #5 status:
  - Note that we chose a two-way door approach
  - Document what was added (ITypeInfo, TypeRegistry, TypeUtils)
  - Document what was NOT changed (SemanticType hierarchy preserved)
  - List future work for full unification (v0.2.x)

🏁 **Checkpoint B.8: Final Commit for Part B**
```bash
git add -A
git commit -m "docs: update architecture review with type system improvements"
```

---

## Final Steps

### Run Complete Test Suite
```bash
dotnet test --logger "console;verbosity=detailed"
```

- [ ] All tests pass
- [ ] Test count is same or higher than baseline

### Create Pull Request
```bash
git push
```

- [ ] Create PR with title: "feat: Complete Emitter Migration & Type System Improvements"
- [ ] Add description summarizing changes
- [ ] Request review if applicable

### Update Project Documentation
- [ ] Update `CLAUDE.md` if any workflow changes
- [ ] Update `README.md` if API changes

---

## Rollback Procedures

If issues are discovered after merging:

### Part A Rollback
The legacy fields can be re-added by reverting commits. CodeGenInfo helpers have fallback behavior, so re-adding fields and their population code will restore functionality.

### Part B Rollback
All Part B changes are additive (two-way door). To rollback:
1. Remove `ITypeInfo` implementations from `SemanticType`
2. Remove `TypeRegistry` class
3. Remove `TypeUtils` class
4. Remove placeholder types (`UnionType`, `TaskType`)

---

## Success Criteria

### Part A Complete When:
- [ ] No legacy tracking fields remain (except `_variableVersions` for local scope)
- [ ] All emission uses CodeGenInfo helpers
- [ ] All 3591+ tests pass
- [ ] Generated C# code is identical to before migration

### Part B Complete When:
- [ ] `ITypeInfo` interface exists and is implemented by `SemanticType`
- [ ] `TypeRegistry` provides centralized type lookup
- [ ] `TypeUtils` provides common operations
- [ ] Documentation explains type system design
- [ ] Placeholder types ready for v0.2.x features
- [ ] All tests pass

---

## Appendix: Files Modified

### Part A Files
- `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`
- `src/Sharpy.Compiler/CodeGen/RoslynEmitter.Statements.cs`
- `src/Sharpy.Compiler/CodeGen/RoslynEmitter.ModuleClass.cs`
- `src/Sharpy.Compiler.Tests/CodeGen/EmitterMigrationTests.cs` (new)

### Part B Files
- `src/Sharpy.Compiler/Semantic/ITypeInfo.cs` (new)
- `src/Sharpy.Compiler/Semantic/SemanticType.cs`
- `src/Sharpy.Compiler/Semantic/TypeRegistry.cs` (new)
- `src/Sharpy.Compiler/Semantic/TypeUtils.cs` (new)
- `src/Sharpy.Compiler.Tests/Semantic/TypeUtilsTests.cs` (new)
- `src/Sharpy.Compiler.Tests/Semantic/TypeSystemIntegrationTests.cs` (new)
- `docs/implementation_planning/type_system_analysis.md` (new)
- `docs/implementation_planning/type_system_design.md` (new)
