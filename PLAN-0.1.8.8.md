# Implementation Plan: Task 0.1.8.8 - Enum Usage Code Generation

## Overview

Generate code for enum member access (`Color.RED`) and the `.value` property for accessing the underlying value of an enum member.

## Current State Analysis

Based on codebase exploration:

1. **Enum definitions are fully implemented** (`RoslynEmitter.cs:748-893`):
   - Integer enums → C# `enum` declarations
   - String enums → `sealed class` with `public static readonly string` fields

2. **Member access already works via `GenerateMemberAccess`** (`RoslynEmitter.cs:2840-2862`):
   - Uses `NameMangler.ToPascalCase()` for member names
   - CAPS_SNAKE_CASE names like `RED`, `PENDING` are preserved (no underscores to transform)

3. **Type tracking for enums**:
   - `TypeSymbol` with `TypeKind.Enum` in symbol table
   - `IsEnumMemberAccess()` pattern exists in `DefaultParameterValidator.cs:207-220`
   - RoslynEmitter tracks class names in `_classNames` HashSet but not enum names

## Key Question: What needs to be implemented?

### Finding 1: Basic enum member access already works

```python
# Python/Sharpy
favorite = Color.RED
```

The existing `GenerateMemberAccess` method handles this:
- `Color` → identifier (type name)
- `RED` → `NameMangler.ToPascalCase("RED")` → `RED` (unchanged)
- Result: `Color.RED` ✓

### Finding 2: `.value` property needs special handling

```python
# Python/Sharpy
value = favorite.value  # Returns underlying int or string value
```

For **integer enums**, this should generate:
```csharp
var value = (int)favorite;  // Cast to underlying type
```

For **string enums**, the value IS the string (no `.value` property needed since string enum members are already `string` typed).

## Implementation Steps

### Step 1: Track Enum Definitions

Modify `RoslynEmitter` to track which types are enums and whether they're string or integer enums.

**File:** `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

Add fields:
```csharp
private readonly HashSet<string> _enumNames = new();          // All enum names
private readonly HashSet<string> _integerEnumNames = new();   // Integer enums only
private readonly HashSet<string> _stringEnumNames = new();    // String enums only
```

Update `GenerateEnumDeclaration` to register enums:
```csharp
private SyntaxNode GenerateEnumDeclaration(EnumDef enumDef)
{
    // Register the enum name
    _enumNames.Add(enumDef.Name);

    bool isStringEnum = IsStringEnum(enumDef);
    if (isStringEnum)
    {
        _stringEnumNames.Add(enumDef.Name);
        return GenerateStringEnumClass(enumDef);
    }
    else
    {
        _integerEnumNames.Add(enumDef.Name);
        return GenerateIntegerEnum(enumDef);
    }
}
```

### Step 2: Handle `.value` Property Access

Modify `GenerateMemberAccess` to detect `.value` on enum variables and generate appropriate code.

**Challenge:** We need to know the type of the object being accessed to detect if it's an enum. Options:

**Option A: Pattern-based detection (simpler, less accurate)**
- Check if member is `value` and object is a variable (not type name)
- Limitation: Can't distinguish enum variables from other variables with `.value` property

**Option B: Semantic info integration (more accurate, more complex)**
- Requires SemanticInfo to be passed to RoslynEmitter
- Would need significant refactoring

**Recommended: Option A with symbol table lookup**

In `GenerateMemberAccess`, add special handling:
```csharp
private ExpressionSyntax GenerateMemberAccess(MemberAccess memberAccess)
{
    var obj = GenerateExpression(memberAccess.Object);

    // Check for .value property on potential enum variable
    if (memberAccess.Member.Equals("value", StringComparison.OrdinalIgnoreCase))
    {
        // If the object is an identifier, check if it might be an enum variable
        // For integer enums: (int)variable
        // For string enums: no transformation needed (already string)

        // Since we can't easily determine the type at this point,
        // we rely on semantic analysis having validated the usage
        // For now, generate a cast for integer enums

        // Actually, we need type information to do this correctly.
        // This might need to be handled in semantic analysis + AST annotation
    }

    // ... existing code ...
}
```

### Step 3: Alternative Approach - AST Annotation

**Better approach:** Handle `.value` during semantic analysis by annotating the AST, then use that annotation during code generation.

1. **SemanticAnalysis Phase:**
   - Detect `enumVar.value` patterns
   - Annotate the MemberAccess expression with the enum type info

2. **Code Generation Phase:**
   - Check for annotation
   - Generate `(int)enumVar` for integer enums
   - Generate `enumVar` directly for string enums (no-op)

### Step 4: Track Enum Names in Class Names (Quick Fix)

The simplest immediate fix: Add enum names to the class tracking so type references work correctly.

Modify `GetMangledVariableName`:
```csharp
private string GetMangledVariableName(string name, bool isNewDeclaration)
{
    // ... existing const check ...

    // Check if this is a reference to an enum name - preserve PascalCase
    if (_enumNames.Contains(name))
    {
        return NameMangler.ToPascalCase(name);
    }

    // Check if this is a reference to a class name - preserve PascalCase
    if (_classNames.Contains(name))
    {
        return NameMangler.ToPascalCase(name);
    }

    // ... rest of method ...
}
```

## Detailed Implementation Plan

### Phase 1: Basic Enum Member Access (Verification + Minor Fix)

1. **Add enum name tracking** to RoslynEmitter:
   - Add `_enumNames` HashSet
   - Register enums in `GenerateEnumDeclaration`
   - Update `GetMangledVariableName` to handle enum type references

2. **Write unit tests** for enum member access:
   - Test `Color.RED` generates `Color.RED`
   - Test `Status.PENDING` generates `Status.PENDING`

### Phase 2: `.value` Property Support

**Option 1: Cast-based approach (simpler)**
- Modify `GenerateMemberAccess` to detect `.value`
- Generate cast expression: `(int)enumVar`
- Limitation: Only works if we can determine the object is an integer enum

**Option 2: Symbol table integration**
- Pass symbol table info to code generation
- Look up variable type to determine if it's an enum
- Generate appropriate code based on enum type

**Recommended approach for Option 2:**

1. **Extend CodeGenContext** to include a method that can look up variable types
2. **In GenerateMemberAccess**, when member is `value`:
   - Check if the object's type is an integer enum
   - If yes, generate cast: `(int)obj`
   - If string enum or not an enum, preserve standard member access

## Files to Modify

1. **`src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`**
   - Add enum tracking fields
   - Update `GenerateEnumDeclaration` to register enums
   - Update `GetMangledVariableName` to handle enum names
   - Add `.value` property handling in `GenerateMemberAccess`

2. **`src/Sharpy.Compiler/CodeGen/CodeGenContext.cs`** (maybe)
   - Add method to look up variable types if needed for `.value` support

## Tests to Add

**File:** `src/Sharpy.Compiler.Tests/CodeGen/RoslynEmitterExpressionTests.cs`

```csharp
#region Enum Member Access Tests

[Fact]
public void GenerateExpression_EnumMemberAccess_PreservesEnumMemberCase()
{
    // Color.RED -> Color.RED
}

[Fact]
public void GenerateExpression_EnumMemberAccess_WithSnakeCaseMember_Preserves()
{
    // Status.IN_PROGRESS -> Status.IN_PROGRESS (or Status.InProgress?)
}

#endregion
```

**File:** `src/Sharpy.Compiler.Tests/CodeGen/RoslynEmitterDefinitionTests.cs` or new file

```csharp
[Fact]
public void GenerateExpression_IntegerEnumValue_GeneratesCast()
{
    // favorite.value -> (int)favorite
}

[Fact]
public void GenerateExpression_StringEnumValue_NoTransformation()
{
    // status.value -> status (or no .value property on string enums)
}
```

**File:** `src/Sharpy.Compiler.Tests/Integration/` (integration tests)

```csharp
[Fact]
public void IntegerEnum_MemberAccess_Works()
{
    var source = @"
enum Color:
    RED = 0
    GREEN = 1
    BLUE = 2

favorite: Color = Color.RED
";
    // Verify compilation succeeds
}

[Fact]
public void IntegerEnum_ValueProperty_ReturnsUnderlyingValue()
{
    var source = @"
enum Color:
    RED = 0
    GREEN = 1

c: Color = Color.RED
v: int = c.value
print(v)
";
    var result = CompileAndExecute(source);
    result.Should().Contain("0");
}
```

## Potential Risks / Questions

### Question 1: Should string enums have `.value`?

In Python, `Enum.value` returns the associated value. For string enums:
```python
class Status(str, Enum):
    PENDING = "pending"

Status.PENDING.value  # Returns "pending"
```

But in our implementation, string enums are `sealed class` with `string` fields. The field value IS the string. So:
- `Status.PENDING` already returns `"pending"`
- `Status.PENDING.value` doesn't make sense (strings don't have `.value`)

**Decision needed:** Should we:
a) Prohibit `.value` on string enums (semantic error)
b) Make `.value` a no-op on string enums (just return the value itself)
c) Something else?

### Question 2: Type Resolution for `.value`

We need to know if a variable is an enum type to generate the cast. Options:
1. **Require type annotations** - if variable is annotated with enum type, we know
2. **Use semantic info** - look up the expression type from semantic analysis
3. **Pattern-based heuristic** - assume `.value` on identifier means enum (risky)

**Recommendation:** Use semantic info from type checking phase.

### Question 3: Enum Member Naming

Current approach: Members stored as CAPS_SNAKE_CASE (e.g., `RED`, `IN_PROGRESS`)
- `NameMangler.ToPascalCase("RED")` → `RED` (unchanged, good)
- `NameMangler.ToPascalCase("IN_PROGRESS")` → `InProgress` (transformed!)

**Is this intentional?** Enum members with underscores will be PascalCased.

### Risk: Two-Pass Processing

The current code generation might process statements in order. If an enum is used before it's defined, `_enumNames` won't contain it yet.

**Mitigation:** Consider a first pass to collect all type definitions before code generation, or rely on semantic validation to ensure forward references are caught.

## Summary

1. **Enum member access** (`Color.RED`) mostly works already; need to verify and potentially add enum name tracking.

2. **`.value` property** requires type information to generate correct code (cast for integer enums, no-op or error for string enums).

3. **Testing** should cover both unit tests for code generation and integration tests for end-to-end functionality.

4. **Key decisions needed:**
   - How to handle `.value` on string enums
   - How to get type information in code generation for proper `.value` handling
