# Implementation Plan: R-0.1.3.1 - Fix Const Name Mangling Inconsistency

## Problem Analysis

The issue is a naming mismatch between const declaration and const reference:

1. **Declaration** (line 1500 in `RoslynEmitter.cs`):
   ```csharp
   var varName = varDecl.IsConst
       ? NameMangler.ToConstantCase(varDecl.Name)  // BASE → BASE
       : GetMangledVariableName(varDecl.Name, isNewDeclaration: true);
   ```

2. **Reference** (line 1753 in `RoslynEmitter.cs`):
   ```csharp
   Identifier name => IdentifierName(GetMangledVariableName(name.Name, isNewDeclaration: false)),
   ```

   `GetMangledVariableName` internally calls `NameMangler.ToCamelCase(name)` which converts `BASE` → `base`.

**Result**: When `const BASE: int = 10` is declared, C# gets `const int BASE = 10`. When `x: int = BASE` is compiled, the reference becomes `base` (lowercase), causing C# compilation error.

## Solution Options

### Option A: Track const names and use constant case for references (Recommended)
- Track declared const names in a `HashSet<string>` or `Dictionary<string, string>`
- When generating identifier references, check if the name is a const and use `ToConstantCase`
- Pros: Preserves C# convention of UPPER_CASE for constants
- Cons: Requires tracking state

### Option B: Use camelCase for both declaration and reference
- Change declaration from `ToConstantCase` to `ToCamelCase`
- Simplest fix but loses UPPER_CASE const convention
- Pros: Simple, consistent
- Cons: Loses idiomatic C# naming for constants

### Option C: Use constant case for both (leverage existing tracking)
- Const declarations already track the mangled name via `_declaredVariables.Add(varName)` and `_variableVersions`
- Problem: The tracking uses `baseName = ToCamelCase(name)` as the key
- Would require separate tracking for const names

**Recommendation**: Option A - it's the cleanest and most idiomatic.

## Step-by-Step Implementation

### Step 1: Add const name tracking field
Location: `RoslynEmitter.cs` near other field declarations (around line 10-20)

Add a field to track which variable names are constants:
```csharp
private readonly HashSet<string> _constNames = new();
```

### Step 2: Track const names during declaration
Location: `GenerateVariableDeclaration` method (around line 1497-1538)

After computing `varName` for a const, add it to the tracking set:
```csharp
if (varDecl.IsConst)
{
    _constNames.Add(varDecl.Name);  // Track original name
}
```

### Step 3: Update identifier reference generation
Location: Line 1753 in the switch expression handling `Identifier`

Change from:
```csharp
Identifier name => IdentifierName(GetMangledVariableName(name.Name, isNewDeclaration: false)),
```

To:
```csharp
Identifier name => IdentifierName(GetMangledIdentifierName(name.Name)),
```

### Step 4: Add helper method for identifier name resolution
Add a new helper method that checks if the name is a const:
```csharp
private string GetMangledIdentifierName(string name)
{
    // Check if this is a reference to a const
    if (_constNames.Contains(name))
    {
        return NameMangler.ToConstantCase(name);
    }

    // Regular variable reference
    return GetMangledVariableName(name, isNewDeclaration: false);
}
```

### Step 5: Clear const tracking on scope reset (if needed)
Check if `_constNames` needs to be cleared when entering new scopes (functions, classes). Look at how `_variableVersions` and `_declaredVariables` are managed.

## Files to Modify

| File | Changes |
|------|---------|
| `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs` | Add `_constNames` field, modify `GenerateVariableDeclaration`, add helper method, update identifier handling |

## Tests to Verify

### Existing test to enable
Remove the `Skip` attribute from the existing test:
- `Phase013IntegrationTests.MixedDeclarations_WithOperations_CompilesAndRuns()` (line 640)

### Additional test cases to verify
1. **Basic const usage**: `const BASE: int = 10; x = BASE`
2. **Const in expressions**: `const BASE: int = 10; y = BASE + 5`
3. **Multiple consts**: `const A: int = 1; const B: int = 2; z = A + B`
4. **Mixed const and var**: `const C: int = 10; x: int = 5; y = C + x`
5. **Const with augmented assignment**: `const BASE: int = 10; x: int = 0; x += BASE`

## Potential Risks

1. **Scope handling**: If consts are declared in inner scopes (functions, blocks), the `_constNames` set might not be properly scoped. Need to verify scope management.

2. **Shadowing**: If a non-const variable shadows a const (or vice versa), the tracking might give incorrect results. This is likely a semantic error anyway.

3. **Function-level vs module-level consts**: Need to verify tracking works for both top-level and function-level const declarations.

4. **Reset on new compilation**: Ensure `_constNames` is properly initialized/cleared between compilations.

## Questions for Clarification

1. Should consts preserve UPPER_CASE naming (Option A) or is camelCase acceptable (Option B)?
   - **Default assumption**: Preserve UPPER_CASE as it's idiomatic C#

2. Are there nested scopes where const tracking could be problematic?
   - Need to check during implementation
