# Implementation Plan: Task 0.1.8.7 - Enum Semantic Validation

## Overview

Implement semantic validation for enum definitions to enforce type consistency and explicit value requirements.

## Validation Rules to Implement

Based on the task description (truncated but inferred from code generation context):

1. **All enum values must be explicit** - Every enum member must have an explicit value
2. **All values must be of the same type** - Either all int or all string (homogeneous types)
3. **Duplicate member names** - Not allowed within same enum
4. **Duplicate member values** - Not allowed within same enum

## Step-by-Step Implementation Approach

### Step 1: Update TypeChecker.CheckStatement for EnumDef

**File:** `src/Sharpy.Compiler/Semantic/TypeChecker.cs` (line 113-115)

Replace the current no-op:
```csharp
case EnumDef enumDef:
    // Enums don't need type checking
    break;
```

With:
```csharp
case EnumDef enumDef:
    CheckEnum(enumDef);
    break;
```

### Step 2: Implement CheckEnum Method

Add a new method following the `CheckStruct` pattern:

```csharp
private void CheckEnum(EnumDef enumDef)
{
    _logger.LogDebug($"Checking enum: {enumDef.Name}");

    // Lookup the enum symbol
    var enumSymbol = _symbolTable.LookupType(enumDef.Name);
    if (enumSymbol == null)
    {
        AddError($"Enum '{enumDef.Name}' not found in symbol table",
            enumDef.LineStart, enumDef.ColumnStart);
        return;
    }

    // Validate enum-specific rules
    ValidateEnumRules(enumSymbol, enumDef);
}
```

### Step 3: Implement ValidateEnumRules Method

Add after `ValidateStructRules` (around line 2417):

```csharp
/// <summary>
/// Validate enum-specific rules
/// </summary>
private void ValidateEnumRules(TypeSymbol enumSymbol, EnumDef enumDef)
{
    _logger.LogDebug($"Validating enum-specific rules for '{enumSymbol.Name}'");

    // Rule 1: Check for duplicate member names
    ValidateEnumMemberNamesUnique(enumDef);

    // Rule 2: Check all values are explicit
    ValidateEnumValuesExplicit(enumDef);

    // Rule 3: Check all values are same type (int or string)
    ValidateEnumValueTypesConsistent(enumDef);

    // Rule 4: Check for duplicate values
    ValidateEnumValuesUnique(enumDef);
}
```

### Step 4: Implement Individual Validation Methods

#### 4a. ValidateEnumMemberNamesUnique

```csharp
private void ValidateEnumMemberNamesUnique(EnumDef enumDef)
{
    var seenNames = new HashSet<string>();
    foreach (var member in enumDef.Members)
    {
        if (!seenNames.Add(member.Name))
        {
            AddError($"Duplicate enum member name '{member.Name}' in enum '{enumDef.Name}'",
                member.LineStart, member.ColumnStart);
        }
    }
}
```

#### 4b. ValidateEnumValuesExplicit

```csharp
private void ValidateEnumValuesExplicit(EnumDef enumDef)
{
    foreach (var member in enumDef.Members)
    {
        if (member.Value == null)
        {
            AddError($"Enum member '{member.Name}' requires an explicit value. All enum members must have explicit constant values.",
                member.LineStart, member.ColumnStart);
        }
    }
}
```

#### 4c. ValidateEnumValueTypesConsistent

```csharp
private void ValidateEnumValueTypesConsistent(EnumDef enumDef)
{
    // Skip if no members with values
    var membersWithValues = enumDef.Members.Where(m => m.Value != null).ToList();
    if (membersWithValues.Count == 0)
    {
        return;
    }

    // Determine expected type from first member with value
    var firstValue = membersWithValues[0].Value;
    bool expectString = firstValue is StringLiteral;

    foreach (var member in membersWithValues.Skip(1))
    {
        bool isString = member.Value is StringLiteral;
        if (isString != expectString)
        {
            var expectedType = expectString ? "string" : "integer";
            var actualType = isString ? "string" : "integer";
            AddError($"Enum member '{member.Name}' has {actualType} value but enum '{enumDef.Name}' expects {expectedType} values. All enum values must be of the same type.",
                member.LineStart, member.ColumnStart);
        }
    }
}
```

#### 4d. ValidateEnumValuesUnique

```csharp
private void ValidateEnumValuesUnique(EnumDef enumDef)
{
    var seenIntValues = new Dictionary<long, string>();
    var seenStringValues = new Dictionary<string, string>();

    foreach (var member in enumDef.Members)
    {
        if (member.Value is IntegerLiteral intLit)
        {
            if (seenIntValues.TryGetValue(intLit.Value, out var existingMember))
            {
                AddError($"Duplicate enum value {intLit.Value} in enum '{enumDef.Name}'. Value already used by member '{existingMember}'.",
                    member.LineStart, member.ColumnStart);
            }
            else
            {
                seenIntValues[intLit.Value] = member.Name;
            }
        }
        else if (member.Value is StringLiteral strLit)
        {
            if (seenStringValues.TryGetValue(strLit.Value, out var existingMember))
            {
                AddError($"Duplicate enum value \"{strLit.Value}\" in enum '{enumDef.Name}'. Value already used by member '{existingMember}'.",
                    member.LineStart, member.ColumnStart);
            }
            else
            {
                seenStringValues[strLit.Value] = member.Name;
            }
        }
    }
}
```

## Key Files to Modify

| File | Changes |
|------|---------|
| `src/Sharpy.Compiler/Semantic/TypeChecker.cs` | Add `CheckEnum`, `ValidateEnumRules`, and 4 validation helper methods |

## Tests to Verify

Create new test file: `src/Sharpy.Compiler.Tests/Semantic/EnumValidationTests.cs`

### Test Cases

1. **Explicit Value Tests**
   - `EnumMember_WithoutExplicitValue_ReportsError` - member without `= value`
   - `EnumMember_AllWithExplicitValues_NoError` - all members have values
   - `EnumMember_MultipleWithoutExplicitValues_ReportsMultipleErrors`

2. **Type Consistency Tests**
   - `EnumMembers_AllInteger_NoError`
   - `EnumMembers_AllString_NoError`
   - `EnumMembers_MixedIntAndString_ReportsError`

3. **Duplicate Name Tests**
   - `EnumMembers_DuplicateNames_ReportsError`
   - `EnumMembers_UniqueNames_NoError`

4. **Duplicate Value Tests**
   - `EnumMembers_DuplicateIntegerValues_ReportsError`
   - `EnumMembers_DuplicateStringValues_ReportsError`
   - `EnumMembers_UniqueValues_NoError`

5. **Edge Cases**
   - `EmptyEnum_NoError` (empty enums allowed)
   - `SingleMemberEnum_WithExplicitValue_NoError`

### Sample Test Code

```csharp
[Fact]
public void EnumMember_WithoutExplicitValue_ReportsError()
{
    var source = @"
enum Status:
    ACTIVE
    INACTIVE = 1
";
    var (module, symbolTable, semanticInfo, typeChecker) = CompileAndCheck(source);
    typeChecker.CheckModule(module);

    typeChecker.Errors.Should().ContainSingle(e =>
        e.Message.Contains("Enum member 'ACTIVE' requires an explicit value"));
}

[Fact]
public void EnumMembers_MixedIntAndString_ReportsError()
{
    var source = @"
enum Bad:
    A = 1
    B = ""hello""
";
    var (module, symbolTable, semanticInfo, typeChecker) = CompileAndCheck(source);
    typeChecker.CheckModule(module);

    typeChecker.Errors.Should().ContainSingle(e =>
        e.Message.Contains("expects integer values"));
}
```

## Potential Risks and Questions

### Risks

1. **Expression evaluation complexity** - Currently only checking for `IntegerLiteral` and `StringLiteral`. If enums support constant expressions (e.g., `A = 1 + 2`), need to evaluate or check differently.

2. **Negative integer values** - Need to verify `IntegerLiteral.Value` handles negative numbers for duplicate checking.

3. **Unary minus expressions** - Negative enum values like `A = -1` might be parsed as `UnaryOp` with `-` and `IntegerLiteral(1)`, not `IntegerLiteral(-1)`.

### Questions to Clarify

1. **Are implicit/auto-incrementing values allowed?** - The task description suggests NO (all must be explicit). Confirm this is the intended behavior.

2. **Are constant expressions allowed?** - Can members have computed values like `B = A + 1`? Current implementation assumes only literals.

3. **Empty enums** - Should an enum with no members be an error? Current implementation allows it.

4. **Should duplicate value detection cross-check int and string?** - E.g., is `A = 1` and `B = "1"` a conflict? Current implementation: NO (type mismatch would be caught first).

## Implementation Order

1. Add `CheckEnum` call in `CheckStatement` switch
2. Implement `ValidateEnumRules` orchestrator
3. Implement `ValidateEnumMemberNamesUnique`
4. Implement `ValidateEnumValuesExplicit`
5. Implement `ValidateEnumValueTypesConsistent`
6. Implement `ValidateEnumValuesUnique`
7. Write unit tests
8. Run tests and fix any issues

## Dependencies

- Requires `EnumDef` and `EnumMember` AST types (already exist)
- Requires `TypeSymbol` with `TypeKind.Enum` (already exist)
- No new dependencies needed
