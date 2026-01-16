# Implementation Plan: Task 0.1.9.2 - Nullable Type Code Generation

## Overview

**Task ID:** 0.1.9.2
**Title:** Implement Nullable Type Code Generation
**Objective:** Generate correct C# nullable types with `#nullable enable` directive

---

## Current State Analysis

### What's Already Done (from Task 0.1.9.1)
- `TypeAnnotation.IsNullable` property exists in `src/Sharpy.Compiler/Parser/Ast/Types.cs` (line 10)
- `NullableType : SemanticType` record exists in `src/Sharpy.Compiler/Semantic/SemanticType.cs` (lines 199-220)
- `TypeMapper.MapType()` already handles nullable types via Roslyn's `NullableType()` (lines 67-77)
- TypeMapper tests for nullable types already pass in `src/Sharpy.Compiler.Tests/CodeGen/TypeMapperTests.cs`

### What Needs to Be Done
1. Add `#nullable enable` pragma to generated C# files
2. Verify nullable type generation works end-to-end
3. Add comprehensive tests

---

## Step-by-Step Implementation

### Step 1: Add `#nullable enable` Pragma to RoslynEmitter.cs

**File:** `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`
**Location:** `GenerateCompilationUnit()` method (around line 98-121)

**Change:** Add `#nullable enable` pragma as leading trivia on the CompilationUnit

**Implementation approach:**
```csharp
// Option 1: Using NullableDirectiveTrivia (structured approach)
var nullablePragma = Trivia(
    NullableDirectiveTrivia(Token(SyntaxKind.EnableKeyword), true));

// Option 2: Using ParseLeadingTrivia (simpler, string-based)
var nullablePragma = ParseLeadingTrivia("#nullable enable\n");

return CompilationUnit()
    .WithLeadingTrivia(nullablePragma)
    .WithUsings(List(usingDirectives))
    .WithMembers(SingletonList<MemberDeclarationSyntax>(namespaceDecl))
    .NormalizeWhitespace();
```

### Step 2: Verify TypeMapper Already Works

**File:** `src/Sharpy.Compiler/CodeGen/TypeMapper.cs`

The `MapType()` method already handles nullable types correctly:
- Lines 67-70: Nullable generic types (`list[int]?` → `List<int>?`)
- Lines 75-76: Nullable non-generic types (`int?` → `int?`, `str?` → `string?`)

**No changes needed** - already implemented.

---

## Test Plan

### Step 3: Add Unit Tests to RoslynEmitterModuleTests.cs

**File:** `src/Sharpy.Compiler.Tests/CodeGen/RoslynEmitterModuleTests.cs`

| Test Name | Description |
|-----------|-------------|
| `GenerateCompilationUnit_IncludesNullablePragma` | Verify `#nullable enable` appears in generated code |
| `GenerateCompilationUnit_NullablePragma_AppearsBeforeUsings` | Verify pragma comes before `using` statements |
| `GenerateCompilationUnit_NullableIntParameter_GeneratesIntQuestion` | Function with `int?` parameter |
| `GenerateCompilationUnit_NullableStringReturnType_GeneratesStringQuestion` | Function with `str?` return type |
| `GenerateCompilationUnit_NullableListType_GeneratesListQuestion` | `list[int]?` → `List<int>?` |
| `GenerateCompilationUnit_ListOfNullableType_GeneratesCorrectly` | `list[int?]` → `List<int?>` |
| `GenerateCompilationUnit_NestedNullableTypes_GeneratesCorrectly` | `list[int?]?` → `List<int?>?` |

### Step 4: Add Integration Tests

**File:** `src/Sharpy.Compiler.Tests/CodeGen/RoslynEmitterIntegrationTests.cs`

| Test Name | Description |
|-----------|-------------|
| `GeneratedCode_WithNullableTypes_CompilesSuccessfully` | Code with nullable types compiles |
| `GeneratedCode_NullableReferenceTypes_CompilesWithNullableEnabled` | `str?` parameter compiles correctly |

---

## Files to Modify

| File | Action | Description |
|------|--------|-------------|
| `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs` | **MODIFY** | Add `#nullable enable` in `GenerateCompilationUnit()` |
| `src/Sharpy.Compiler.Tests/CodeGen/RoslynEmitterModuleTests.cs` | **ADD TESTS** | 7 new tests for pragma and nullable type generation |
| `src/Sharpy.Compiler.Tests/CodeGen/RoslynEmitterIntegrationTests.cs` | **ADD TESTS** | 2 new integration tests |

---

## Expected Output

### Before (Current Generated Code)
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.MyApp
{
    public static class Exports
    {
        public static int? GetOptionalValue() { ... }
    }
}
```

### After (With `#nullable enable`)
```csharp
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.MyApp
{
    public static class Exports
    {
        public static int? GetOptionalValue() { ... }
    }
}
```

---

## Verification Commands

```bash
# Run TypeMapper tests (should pass - already working)
dotnet test src/Sharpy.Compiler.Tests --filter "TypeMapper"

# Run RoslynEmitter tests
dotnet test src/Sharpy.Compiler.Tests --filter "RoslynEmitter"

# Run all compiler tests
dotnet test src/Sharpy.Compiler.Tests
```

---

## Potential Risks

| Risk | Mitigation |
|------|-----------|
| Trivia placement affects formatting | Use `NormalizeWhitespace()` after adding trivia; test output format |
| Existing tests might fail | Run full test suite before/after changes |
| Pragma syntax incorrect | Test with actual C# compilation |
| NullableDirectiveTrivia API differs | Fall back to `ParseLeadingTrivia("#nullable enable\n")` if needed |

---

## Questions to Resolve

1. **Roslyn trivia approach:** Should we use `NullableDirectiveTrivia` or `ParseLeadingTrivia("#nullable enable\n")`?
   - The former is more structured but may require additional tokens
   - The latter is simpler and more predictable
   - **Recommendation:** Try `NullableDirectiveTrivia` first, fall back to string parsing if formatting issues arise

2. **Blank line after pragma:** Should there be a blank line between `#nullable enable` and `using` statements?
   - Standard C# convention includes a blank line
   - `NormalizeWhitespace()` may handle this automatically

---

## Summary

This task is relatively straightforward because the nullable type infrastructure already exists from Task 0.1.9.1:

1. **One code change:** Add `#nullable enable` pragma in `GenerateCompilationUnit()`
2. **~9 new tests:** 7 unit tests + 2 integration tests

The TypeMapper already correctly generates `int?`, `string?`, `List<int>?`, etc. The only missing piece is the file-level `#nullable enable` directive that enables C# compiler nullable reference type checking.
