# Name Mangling System Refactor

## Context

The `NameMangler` class has several edge cases that cause silent name collisions (`foo__bar` = `foo_bar`), destroy casing (`httpClient` → `Httpclient`), overzealously escape keywords (`String` → `@String`), strip the `__` private prefix, and deviate from the spec on constants. Additionally, dunder method mapping is a codegen concern wrongly living in a naming utility, and `TransformEnumMemberName` is duplicated outside NameMangler.

## Changes Overview

1. **Name form detection** — only mangle well-formed `snake_case` / `SCREAMING_SNAKE_CASE`; pass through `PascalCase`, `camelCase`, and unrecognized forms
2. **Preserve `__` private prefix** — `__private_field` → `__PrivateField`
3. **Case-sensitive keyword escaping** — only escape exact lowercase C# keywords
4. **Constants → PascalCase** — `MAX_SIZE` → `MaxSize` (match spec and C# convention)
5. **Extract dunder mapping** from NameMangler into codegen-owned `DunderMapping` class
6. **Move `TransformEnumMemberName`** into NameMangler as `ToEnumMemberName`
7. **Add `NamingConventionValidator`** — warn (SPY0453) about consecutive underscores inside names

## Phase 1: New Infrastructure (additive only, no behavior changes)

### 1a. Create `NameFormDetector` — name classification utility

**New file**: `src/Sharpy.Compiler/CodeGen/NameFormDetector.cs`

```csharp
internal enum NameForm
{
    SnakeCase,            // get_user_name (all lowercase + digits + single underscores)
    PascalCase,           // HttpClient (starts uppercase, no underscores)
    CamelCase,            // httpClient (starts lowercase, no underscores, has uppercase)
    ScreamingSnakeCase,   // MAX_SIZE (all uppercase + digits + single underscores)
    SingleWordLower,      // hello (all lowercase, no underscores)
    SingleWordUpper,      // HTTP (all uppercase, no underscores)
    Dunder,               // __init__ (double underscore bookends)
    Literal,              // `backtick_escaped`
    Unrecognized          // foo__bar, Foo_bar, mixed patterns
}
```

- `Detect(string nameBody)` — classifies the body (after prefix/suffix stripping)
- `HasConsecutiveUnderscores(string nameBody)` — for validator use
- Consolidates the duplicated `IsConstantCaseName` from 3 locations into one public method

### 1b. Create `DunderMapping` — codegen-owned dunder→C# mapping

**New file**: `src/Sharpy.Compiler/CodeGen/DunderMapping.cs`

Move the 11-entry `_dunderMethodMap` from NameMangler here:
- `GetCSharpName(string dunderName)` → `"ToString"` for `__str__`, etc.
- `HasMapping(string dunderName)` → bool
- `TransformUnknownDunder(string name)` → `__add__` → `__Add__` (capitalize middle)

### 1c. Add diagnostic code SPY0453

**File**: `src/Sharpy.Compiler/Diagnostics/DiagnosticCodes.cs` — add `NamingConventionWarning = "SPY0453"` in Validation class

**File**: `src/Sharpy.Compiler/Diagnostics/DiagnosticExplanations.cs` — add explanation entry

### 1d. Add `ToEnumMemberName` stub to NameMangler

**File**: `src/Sharpy.Compiler/CodeGen/NameMangler.cs` — add `EnumMember` to `NameContext`, add `ToEnumMemberName()` method (logic from `TransformEnumMemberName` in RoslynEmitter)

### 1e. Tests for new infrastructure

**New files**:
- `src/Sharpy.Compiler.Tests/CodeGen/NameFormDetectorTests.cs`
- `src/Sharpy.Compiler.Tests/CodeGen/DunderMappingTests.cs`

## Phase 2: Keyword Escaping Fix (small, isolated)

### 2a. Make `EscapeKeywordIfNeeded` case-sensitive

**File**: `src/Sharpy.Compiler/CodeGen/NameMangler.cs` (line 293)
- Change `_csharpKeywords.Contains(name.ToLowerInvariant())` → `_csharpKeywords.Contains(name)`
- Since all keywords are lowercase and `ToPascalCase("class")` → `"Class"`, the result `"Class"` won't match → no `@` prefix. Correct, because `Class` is a valid C# identifier.

**File**: `src/Sharpy.Compiler/CodeGen/NameResolutionService.cs` (line ~197)
- Same fix in `EscapeCSharpKeyword()`

### 2b. Update tests

**File**: `src/Sharpy.Compiler.Tests/CodeGen/NameManglerTests.cs`
- PascalCase keyword tests: `"class"` → `"Class"` (not `"@Class"`)
- CamelCase keyword tests unchanged: `"class"` → `"@class"` (still correct)

Run full test suite, fix any `.expected`/`.expected.cs` snapshots where `@String`, `@Class`, etc. appeared.

## Phase 3: Core Mangling Refactor (largest change)

### 3a. Refactor `ToPascalCase`

**File**: `src/Sharpy.Compiler/CodeGen/NameMangler.cs`

New algorithm:
1. Handle null/empty, backtick literals (unchanged)
2. **Dunders**: Route to `DunderMapping.GetCSharpName()` ?? `DunderMapping.TransformUnknownDunder()` (temporary bridge — Phase 5 removes this)
3. **`__` prefix**: Recognize `__foo` (starts `__`, does NOT end `__`) as double-private prefix. Strip, mangle body, re-attach.
4. **`_` prefix**: Recognize `_foo` (starts `_`, not `__`) as single-private prefix. Strip, mangle body, re-attach. (unchanged behavior)
5. Strip trailing underscores (unchanged)
6. **Detect name form** via `NameFormDetector.Detect(cleanName)`:
   - `SnakeCase` / `SingleWordLower` → split on `_`, capitalize each segment (preserving: `char.ToUpper(first) + rest`), join
   - `ScreamingSnakeCase` → split on `_`, title-case each segment (normalizing: `char.ToUpper(first) + rest.ToLower()`), join. `MAX_SIZE` → `MaxSize`
   - `PascalCase` / `SingleWordUpper` → pass through as-is
   - `CamelCase` → pass through as-is (no warning from NameMangler)
   - `Unrecognized` → pass through as-is
   - `Dunder` → already handled in step 2
7. Restore prefix + trailing underscores
8. Case-sensitive keyword escaping

Key difference from current code: the `Capitalize` helper is split into two variants:
- `CapitalizePreserving(word)`: `char.ToUpper(first) + rest` — for snake_case segments where rest is already lowercase
- `CapitalizeNormalizing(word)`: `char.ToUpper(first) + rest.ToLower()` — for SCREAMING_SNAKE_CASE segments

### 3b. Refactor `ToCamelCase`

Same form detection approach:
- `SnakeCase` / `SingleWordLower` → first segment lowercase, rest capitalized (preserving)
- `ScreamingSnakeCase` → first segment fully lowered, rest title-cased. `MAX_SIZE` → `maxSize`
- `PascalCase` → first char to lower, rest preserved. `HttpClient` → `httpClient`
- `CamelCase` → pass through
- `SingleWordUpper` → fully lowercase. `HTTP` → `http`
- `Unrecognized` → pass through

### 3c. Refactor `ToConstantCase`

Now converts SCREAMING_SNAKE_CASE to PascalCase (matching spec):
- `ScreamingSnakeCase` → PascalCase via normalizing capitalize. `MAX_SIZE` → `MaxSize`
- `SingleWordUpper` → title-case. `HTTP` → `Http`
- `SnakeCase` → PascalCase (same as `ToPascalCase` for snake_case)
- `PascalCase` / `CamelCase` → pass through
- `Unrecognized` → pass through

Difference from `ToPascalCase`: `SingleWordUpper` becomes `Http` here (constants normalize) vs staying `HTTP` there (types preserve).

### 3d. Simplify `IsConstantCaseName` call sites

Since `ToPascalCase` now handles `SCREAMING_SNAKE_CASE` → PascalCase too, the branching pattern `IsConstantCaseName ? ToConstantCase : ToPascalCase` can be simplified in most places. The distinction only matters for single-word all-caps (`HTTP`):

**Files with `IsConstantCaseName` branches to simplify**:
- `src/Sharpy.Compiler/CodeGen/RoslynEmitter.Expressions.cs` (lines 835, 994, 1015)
- `src/Sharpy.Compiler/CodeGen/RoslynEmitter.Statements.cs` (line 515)
- `src/Sharpy.Compiler/CodeGen/RoslynEmitter.ModuleClass.cs` (lines 164, 484, 488)
- `src/Sharpy.Compiler/Semantic/CodeGenInfoComputer.cs` (line 182)
- `src/Sharpy.Compiler/Semantic/ExecutionOrderAnalyzer.cs` (line 90)
- `src/Sharpy.Compiler/CodeGen/RoslynEmitter.CompilationUnit.cs` (line 372 — method can be removed or delegated to `NameFormDetector`)

Approach: Replace the ternary with `NameMangler.ToPascalCase(name)` for contexts where the name is known to be a method/function/type. For constant contexts, use `NameMangler.ToConstantCase(name)` or `NameMangler.Transform(name, NameContext.Constant)`. Remove duplicate `IsConstantCaseName` private methods and use `NameFormDetector` where distinction is still needed.

### 3e. Update tests

**File**: `src/Sharpy.Compiler.Tests/CodeGen/NameManglerTests.cs` — Major updates:
- `ToPascalCase("httpClient")` → `"httpClient"` (camelCase passthrough, was `"Httpclient"`)
- `ToConstantCase("MAX_SIZE")` → `"MaxSize"` (was `"MAX_SIZE"`)
- `ToPascalCase("foo__bar")` → `"foo__bar"` (Unrecognized passthrough)
- `ToPascalCase("__private_field")` → `"__PrivateField"` (new `__` prefix test)
- Add new test cases for all name forms

Run full test suite. Update `.expected` and `.expected.cs` snapshots where SCREAMING_SNAKE_CASE constant output changes (e.g., `MAX_SIZE` → `MaxSize`). Regenerate snapshots with `UPDATE_SNAPSHOTS=true dotnet test --filter "FullyQualifiedName~FileBasedIntegrationTests"`.

## Phase 4: Move TransformEnumMemberName into NameMangler

### 4a. Wire up `ToEnumMemberName` in call sites

**File**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.TypeDeclarations.cs`
- Line 701: `TransformEnumMemberName(member.Name)` → `NameMangler.ToEnumMemberName(member.Name)`
- Lines 715-731: Delete the private `TransformEnumMemberName` method

**File**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.Expressions.cs`
- Line 805: `TransformEnumMemberName(memberAccess.Member)` → `NameMangler.ToEnumMemberName(memberAccess.Member)`

### 4b. Add tests

Add `ToEnumMemberName` tests to NameManglerTests: `RED` → `Red`, `DARK_BLUE` → `DarkBlue`, etc.

## Phase 5: Extract Dunder Mapping from NameMangler (most delicate)

### 5a. Remove dunder handling from `ToPascalCase`

**File**: `src/Sharpy.Compiler/CodeGen/NameMangler.cs`
- Remove the dunder block (bridge from Phase 3) — dunders now return as-is: `__str__` → `__str__`
- Remove `_dunderMethodMap` dictionary
- Remove `#if DEBUG` static constructor
- Remove `GetDunderMethodMapping` method
- `IsDunderMethod` delegates to `DunderMapping.IsDunderMethod` (or remove and have callers use `DunderMapping` directly)

### 5b. Wire `DunderMapping` into `CodeGenInfoComputer`

**File**: `src/Sharpy.Compiler/Semantic/CodeGenInfoComputer.cs` (line 301)

```csharp
// Before: CSharpName = NameMangler.ToPascalCase(funcDef.Name)
// After:
CSharpName = DunderMapping.GetCSharpName(funcDef.Name)
    ?? DunderMapping.TransformUnknownDunder(funcDef.Name)  // for operator dunders like __add__ → __Add__
    ?? NameMangler.ToPascalCase(funcDef.Name),              // for non-dunder methods
```

Wait — the fallback chain needs care. `TransformUnknownDunder` should only be called for dunders. Better:

```csharp
CSharpName = DunderMapping.IsDunderMethod(funcDef.Name)
    ? (DunderMapping.GetCSharpName(funcDef.Name) ?? DunderMapping.TransformUnknownDunder(funcDef.Name))
    : NameMangler.ToPascalCase(funcDef.Name),
```

Same pattern for `ProcessFunctionDef` (line 315).

### 5c. Wire `DunderMapping` into RoslynEmitter

**File**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.ClassMembers.cs`
- Line 60: `NameMangler.IsDunderMethod(fd.Name)` → `DunderMapping.IsDunderMethod(fd.Name)`
- Line 78: Same
- Line 331: The `GenerateClassMethod` call uses `NameMangler.Transform(func.Name, NameContext.Method)`. After Phase 5, this returns `__str__` for dunders. Fix:
  ```csharp
  var mangledName = DunderMapping.IsDunderMethod(func.Name)
      ? (DunderMapping.GetCSharpName(func.Name) ?? DunderMapping.TransformUnknownDunder(func.Name))
      : NameMangler.Transform(func.Name, NameContext.Method);
  ```
- Line 604 (`GenerateInterfaceMethod`): Same pattern

**File**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.Operators.cs`
- Lines 109, 155, 188: Operator bodies call the instance method by mangled name. Change:
  ```csharp
  var methodName = DunderMapping.GetCSharpName(funcDef.Name)
      ?? DunderMapping.TransformUnknownDunder(funcDef.Name);
  ```

### 5d. Update tests

**File**: `src/Sharpy.Compiler.Tests/CodeGen/RegistryConsistencyTests.cs`
- Rewrite to test `DunderMapping` instead of `NameMangler.Transform` for dunder→C# mappings

**File**: `src/Sharpy.Compiler.Tests/CodeGen/NameManglerTests.cs`
- Remove dunder mapping tests (moved to `DunderMappingTests.cs`)
- `ToPascalCase("__str__")` now returns `"__str__"` (passthrough)

Run full test suite. The dunder method C# names should still flow correctly via `CodeGenInfo.CSharpName`.

## Phase 6: Add NamingConventionValidator

### 6a. Create validator

**New file**: `src/Sharpy.Compiler/Semantic/Validation/NamingConventionValidator.cs`

- Extends `SemanticValidatorBase`, Order = 55 (after ModuleLevelValidator, before DecoratorValidator)
- Walks module body checking names on: `FunctionDef`, `ClassDef`, `StructDef`, `InterfaceDef`, `EnumDef` (+ members), `VariableDeclaration`, parameters
- For each name: strip `_`/`__` prefix, strip dunder bookends, strip trailing underscores, check body with `NameFormDetector.HasConsecutiveUnderscores()`
- Emit `SPY0453` warning: `"Identifier 'foo__bar' contains consecutive underscores, which may cause name mangling collisions. Use backtick escaping or rename."`
- Skip backtick-escaped names

### 6b. Register in pipeline

**File**: `src/Sharpy.Compiler/Semantic/Validation/ValidationPipelineFactory.cs`
- Add `.AddValidator(new NamingConventionValidator())` at Order 55

### 6c. Tests

**New file**: `src/Sharpy.Compiler.Tests/Semantic/Validation/NamingConventionValidatorTests.cs`
- `foo__bar` variable → warns
- `foo_bar` variable → no warning
- `__init__` dunder → no warning
- `_private_var` → no warning
- backtick name → no warning

**New integration fixture**: `naming_convention_warning.spy` + `.warning`

## Phase 7: Update Spec

**File**: `docs/language_specification/name_mangling.md`
- Update constant row: `CAPS_SNAKE_CASE constants` → `PascalCase` (matches implementation now)
- Document camelCase passthrough behavior
- Document `__` private prefix preservation
- Document consecutive underscore warning
- Update the complete examples table

## File Summary

| Phase | Modified | Created |
|-------|----------|---------|
| 1 | NameMangler.cs, DiagnosticCodes.cs, DiagnosticExplanations.cs | NameFormDetector.cs, DunderMapping.cs, NameFormDetectorTests.cs, DunderMappingTests.cs |
| 2 | NameMangler.cs, NameResolutionService.cs, NameManglerTests.cs | — |
| 3 | NameMangler.cs, CodeGenInfoComputer.cs, RoslynEmitter.{Expressions,Statements,ModuleClass,CompilationUnit}.cs, ExecutionOrderAnalyzer.cs, NameManglerTests.cs, ~N snapshots | — |
| 4 | RoslynEmitter.TypeDeclarations.cs, RoslynEmitter.Expressions.cs, NameManglerTests.cs | — |
| 5 | NameMangler.cs, CodeGenInfoComputer.cs, RoslynEmitter.{ClassMembers,Operators}.cs, RegistryConsistencyTests.cs, NameManglerTests.cs | — |
| 6 | ValidationPipelineFactory.cs | NamingConventionValidator.cs, NamingConventionValidatorTests.cs, fixture files |
| 7 | name_mangling.md | — |

## Verification

After each phase:
1. `dotnet build sharpy.sln` — must compile
2. `dotnet test` — all tests pass (or expected failures are fixed)
3. `dotnet run --project src/Sharpy.Cli -- emit csharp snippets/hello.spy` — sanity check

After all phases:
- Run `UPDATE_SNAPSHOTS=true dotnet test --filter "FullyQualifiedName~FileBasedIntegrationTests"` to regenerate any remaining stale C# snapshots
- Run `dotnet format whitespace` before committing
- Manually verify a few `.spy` files that use constants, dunders, private members, and camelCase names
