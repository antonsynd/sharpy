# Implementation Plan: R-0.1.5.3 - Enhance Default Parameter Validation for Enum Values

## Summary

This task enhances the `DefaultParameterValidator` to accept enum member access expressions (e.g., `Color.RED`) as compile-time constants for default parameter values, as specified in the language specification.

## Current State

### What Works
- `DefaultParameterValidator.IsCompileTimeConstant()` accepts: literals, tuples, unary ops, binary ops, conditional expressions, parenthesized expressions
- Enum types are defined in NameResolver but **without member storage**

### What Doesn't Work
- `MemberAccess => false` - all member access is rejected (line 170)
- Enum members are parsed (`EnumMember` in AST) but not stored in `TypeSymbol`
- `CheckMemberAccess` in TypeChecker doesn't handle `TypeKind.Enum`

### Spec Requirements (from `function_default_parameters.md`)
```
| Enum values | `Color.RED`, `HttpMethod.GET` |
| Constant references | `MAX_SIZE`, `DEFAULT_NAME` | Must reference a `const` declaration |
```

## Implementation Approach

### Step 1: Store Enum Members in TypeSymbol (NameResolver.cs)

**File**: `src/Sharpy.Compiler/Semantic/NameResolver.cs`

Modify `ResolveEnumDeclaration()` (line 234-256) to populate `TypeSymbol.Fields` with enum members:

```csharp
private void ResolveEnumDeclaration(EnumDef enumDef)
{
    // ... existing validation ...

    // Create VariableSymbols for each enum member
    var fields = enumDef.Members.Select((member, index) => new VariableSymbol
    {
        Name = member.Name,
        Kind = SymbolKind.Variable,
        Type = new UserDefinedType { Name = enumDef.Name },  // Type is the enum itself
        IsConstant = true,  // Enum members are constants
        DeclarationLine = member.LineStart,
        DeclarationColumn = member.ColumnStart
    }).ToList();

    var typeSymbol = new TypeSymbol
    {
        Name = enumDef.Name,
        Kind = SymbolKind.Type,
        TypeKind = TypeKind.Enum,
        AccessLevel = AccessLevel.Public,
        Fields = fields,  // ADD THIS
        DeclarationLine = enumDef.LineStart,
        DeclarationColumn = enumDef.ColumnStart
    };

    _symbolTable.Define(typeSymbol);
}
```

### Step 2: Handle Enum Member Access in TypeChecker (TypeChecker.cs)

**File**: `src/Sharpy.Compiler/Semantic/TypeChecker.cs`

Modify `CheckMemberAccess()` (line 1343-1377) to handle enum types:

```csharp
private SemanticType CheckMemberAccess(MemberAccess memberAccess)
{
    var objectType = CheckExpression(memberAccess.Object);

    if (objectType is UserDefinedType udt && udt.Symbol != null)
    {
        // NEW: Check if this is an enum type
        if (udt.Symbol.TypeKind == TypeKind.Enum)
        {
            var member = udt.Symbol.Fields.FirstOrDefault(f => f.Name == memberAccess.Member);
            if (member != null)
            {
                return member.Type;
            }
            AddError($"Enum '{udt.Symbol.Name}' has no member '{memberAccess.Member}'",
                memberAccess.LineStart, memberAccess.ColumnStart);
            return SemanticType.Unknown;
        }

        // ... existing field/method lookup code ...
    }

    return SemanticType.Unknown;
}
```

**Note**: Also need to handle case where `memberAccess.Object` is an `Identifier` referring to an enum **type** (not an instance). Currently `CheckIdentifier` for a type name may return a special type or error.

**Alternative approach**: Check if `memberAccess.Object` is an `Identifier` and look it up directly:

```csharp
private SemanticType CheckMemberAccess(MemberAccess memberAccess)
{
    // Special case: Check if object is an identifier referring to an enum type
    if (memberAccess.Object is Identifier id)
    {
        var symbol = _symbolTable.Lookup(id.Name);
        if (symbol is TypeSymbol typeSymbol && typeSymbol.TypeKind == TypeKind.Enum)
        {
            var member = typeSymbol.Fields.FirstOrDefault(f => f.Name == memberAccess.Member);
            if (member != null)
            {
                return member.Type;
            }
            AddError($"Enum '{typeSymbol.Name}' has no member '{memberAccess.Member}'",
                memberAccess.LineStart, memberAccess.ColumnStart);
            return SemanticType.Unknown;
        }
    }

    // ... rest of existing logic ...
}
```

### Step 3: Update IsCompileTimeConstant (DefaultParameterValidator.cs)

**File**: `src/Sharpy.Compiler/Semantic/DefaultParameterValidator.cs`

The validator needs access to the symbol table to check if a MemberAccess refers to an enum member. Two approaches:

**Option A**: Make `IsCompileTimeConstant` an instance method (requires symbol table access)

```csharp
private bool IsCompileTimeConstant(Expression expr)
{
    return expr switch
    {
        // ... existing cases ...

        // NEW: Enum member access is a compile-time constant
        MemberAccess memberAccess => IsEnumMemberAccess(memberAccess),

        // ... rest ...
    };
}

private bool IsEnumMemberAccess(MemberAccess memberAccess)
{
    // Check if object is an identifier referring to an enum type
    if (memberAccess.Object is not Identifier id)
        return false;

    var symbol = _symbolTable.Lookup(id.Name);
    if (symbol is not TypeSymbol typeSymbol || typeSymbol.TypeKind != TypeKind.Enum)
        return false;

    // Check if the member exists in the enum
    return typeSymbol.Fields.Any(f => f.Name == memberAccess.Member);
}
```

**Option B**: Keep it static but pass symbol table (more invasive)

**Recommendation**: Use Option A - the method is already in an instance class with `_symbolTable` access. Just remove the `static` modifier.

### Step 4: Handle Const References (Future/Optional)

The spec also mentions const references (`MAX_SIZE`, `DEFAULT_NAME`). This requires:

```csharp
// In IsCompileTimeConstant
Identifier id => IsConstReference(id),

private bool IsConstReference(Identifier id)
{
    var symbol = _symbolTable.Lookup(id.Name);
    return symbol is VariableSymbol varSymbol && varSymbol.IsConstant;
}
```

**Note**: This may be out of scope for this task. The task description mentions enum values specifically. Consider implementing in a follow-up task.

## Files to Modify

| File | Changes |
|------|---------|
| `src/Sharpy.Compiler/Semantic/NameResolver.cs` | Store enum members in `TypeSymbol.Fields` |
| `src/Sharpy.Compiler/Semantic/TypeChecker.cs` | Handle enum type member access in `CheckMemberAccess()` |
| `src/Sharpy.Compiler/Semantic/DefaultParameterValidator.cs` | Update `IsCompileTimeConstant()` for enum members |
| `src/Sharpy.Compiler/Semantic/ImportResolver.cs` | Also store enum members when creating enum symbols for imports |

## Tests to Add

**File**: `src/Sharpy.Compiler.Tests/Semantic/DefaultParameterValidatorTests.cs`

```csharp
#region Enum Default Parameters

[Fact]
public void AllowsEnumMemberDefault()
{
    var source = @"
enum Color:
    RED
    GREEN
    BLUE

def paint(c: Color = Color.RED):
    pass
";
    var (module, _, _, typeChecker) = CompileAndCheck(source);
    typeChecker.CheckModule(module);
    typeChecker.Errors.Should().BeEmpty();
}

[Fact]
public void AllowsEnumMemberDefaultWithExplicitValues()
{
    var source = @"
enum Priority:
    LOW = 1
    MEDIUM = 2
    HIGH = 3

def process(p: Priority = Priority.MEDIUM):
    pass
";
    var (module, _, _, typeChecker) = CompileAndCheck(source);
    typeChecker.CheckModule(module);
    typeChecker.Errors.Should().BeEmpty();
}

[Fact]
public void RejectsInvalidEnumMember()
{
    var source = @"
enum Color:
    RED
    GREEN
    BLUE

def paint(c: Color = Color.YELLOW):
    pass
";
    var (module, _, _, typeChecker) = CompileAndCheck(source);
    typeChecker.CheckModule(module);
    typeChecker.Errors.Should().NotBeEmpty();
    typeChecker.Errors.Should().Contain(e => e.Message.Contains("no member") && e.Message.Contains("YELLOW"));
}

[Fact]
public void RejectsNonEnumMemberAccess()
{
    var source = @"
class Config:
    VALUE: int = 42

def foo(x: int = Config.VALUE):
    pass
";
    var (module, _, _, typeChecker) = CompileAndCheck(source);
    typeChecker.CheckModule(module);
    typeChecker.Errors.Should().NotBeEmpty();
    typeChecker.Errors.Should().Contain(e => e.Message.Contains("compile-time constant"));
}

[Fact]
public void AllowsEnumDefaultInMethod()
{
    var source = @"
enum Mode:
    NORMAL
    FAST
    SLOW

class Processor:
    def run(self, mode: Mode = Mode.NORMAL):
        pass
";
    var (module, _, _, typeChecker) = CompileAndCheck(source);
    typeChecker.CheckModule(module);
    typeChecker.Errors.Should().BeEmpty();
}

#endregion
```

### Additional Tests for TypeChecker

Add tests to verify enum member access works correctly:

```csharp
[Fact]
public void EnumMemberAccessReturnsEnumType()
{
    var source = @"
enum Color:
    RED
    GREEN

x: Color = Color.RED
";
    // Should compile without errors
}
```

## Potential Risks and Questions

### Risks

1. **Type resolution order**: Ensure enum types are fully resolved before default parameter validation runs. The current pipeline (NameResolver → TypeResolver → TypeChecker) should handle this, but verify.

2. **Circular dependencies**: If an enum member's explicit value references another constant, this could cause issues. For v0.1.5.3, assume simple enum values only.

3. **ImportResolver consistency**: Changes to `NameResolver` should be mirrored in `ImportResolver` to ensure imported enums work correctly.

4. **Code generation**: Ensure `RoslynEmitter` can handle enum default values. Check if additional codegen changes are needed.

### Questions to Clarify

1. **Scope of const references**: Should this task also implement const references (`DEFAULT_VALUE`) or just enum members? The task title says "Enum Values" but the spec mentions both.

2. **Type compatibility**: Should we validate that the enum type matches the parameter type? E.g., `def foo(c: Color = Mode.NORMAL)` should be rejected.

3. **Nested access**: What about `Color.RED.value` or similar? Probably out of scope for this task.

## Implementation Order

1. **NameResolver.cs** - Store enum members in Fields (foundation for everything else)
2. **ImportResolver.cs** - Mirror the same change for imports
3. **TypeChecker.cs** - Handle enum member access (enables type checking of `Color.RED`)
4. **DefaultParameterValidator.cs** - Allow enum member access as compile-time constant
5. **Tests** - Add comprehensive test coverage
6. **Verify codegen** - Ensure RoslynEmitter handles the new cases

## Estimated Complexity

- **NameResolver change**: Simple (add field population)
- **TypeChecker change**: Moderate (special case for enum lookup)
- **DefaultParameterValidator change**: Simple (add check for enum member)
- **Tests**: Moderate (several test cases)

Total: ~150-200 lines of code changes + tests
