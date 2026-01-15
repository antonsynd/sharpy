# Implementation Plan: Task 0.1.8.3 - Implement Struct Semantic Validation

## Summary

Implement struct-specific semantic validation rules in `TypeChecker.cs` to ensure structs follow Sharpy language rules. The primary validation is ensuring constructors initialize all fields.

---

## 1. Key Files to Modify

| File | Action | Description |
|------|--------|-------------|
| `src/Sharpy.Compiler/Semantic/TypeChecker.cs` | **Primary** | Expand `CheckStruct` method with full validation |
| `src/Sharpy.Compiler.Tests/Semantic/TypeCheckerTests.cs` | **Add tests** | Struct-specific semantic validation tests |

---

## 2. Current State Analysis

### Existing `CheckStruct` Method (TypeChecker.cs:445-458)

```csharp
private void CheckStruct(StructDef structDef)
{
    _logger.LogDebug($"Type checking struct: {structDef.Name}");

    // Enter struct scope
    _symbolTable.EnterScope($"struct:{structDef.Name}");

    foreach (var statement in structDef.Body)
    {
        CheckStatement(statement);
    }

    _symbolTable.ExitScope();
}
```

**Current limitations:**
- No field type resolution (unlike `CheckClass` which resolves field types)
- No constructor validation
- No field initialization tracking
- No struct-specific rule enforcement (e.g., no virtual/abstract methods)

### Class Pattern to Adapt (TypeChecker.cs:390-443)

The `CheckClass` method provides a template with:
1. Symbol lookup and field type resolution
2. Current class context tracking (`_currentClass`)
3. Access validator integration
4. Constructor overload validation

---

## 3. Step-by-Step Implementation

### Step 1: Add Struct Tracking State

Add a field to track the current struct being checked (similar to `_currentClass`):

```csharp
private TypeSymbol? _currentStruct;
```

**Location:** Near line 45 in TypeChecker.cs, alongside other state fields.

### Step 2: Rewrite `CheckStruct` Method

Replace the minimal implementation with comprehensive validation:

```csharp
private void CheckStruct(StructDef structDef)
{
    _logger.LogDebug($"Type checking struct: {structDef.Name}");

    // Look up the struct symbol
    var structSymbol = _symbolTable.Lookup(structDef.Name) as TypeSymbol;
    if (structSymbol == null)
    {
        AddError($"Struct symbol for '{structDef.Name}' not found",
            structDef.LineStart, structDef.ColumnStart);
        return;
    }

    // Enter struct scope
    _symbolTable.EnterScope($"struct:{structDef.Name}");

    // Resolve field types first (before checking methods that might reference them)
    for (int i = 0; i < structSymbol.Fields.Count; i++)
    {
        var fieldSymbol = structSymbol.Fields[i];
        if (fieldSymbol.Type == SemanticType.Unknown)
        {
            var fieldDecl = structDef.Body
                .OfType<VariableDeclaration>()
                .FirstOrDefault(v => v.Name == fieldSymbol.Name);

            if (fieldDecl != null)
            {
                var resolvedType = _typeResolver.ResolveTypeAnnotation(fieldDecl.Type);
                structSymbol.Fields[i] = fieldSymbol with { Type = resolvedType };
            }
        }
    }

    // Set current struct context for method type checking
    var previousStruct = _currentStruct;
    _currentStruct = structSymbol;
    _accessValidator.EnterClass(structSymbol);  // Reuse access validator

    // Check all members
    foreach (var statement in structDef.Body)
    {
        CheckStatement(statement);
    }

    // Validate constructor overloads (no duplicates)
    ValidateConstructorOverloads(structSymbol);

    // Validate that each constructor initializes all fields
    ValidateStructFieldInitialization(structDef, structSymbol);

    // Restore previous struct context
    _currentStruct = previousStruct;
    _accessValidator.ExitClass();

    _symbolTable.ExitScope();
}
```

### Step 3: Implement Field Initialization Validation

Create a new method to validate that each constructor initializes all struct fields:

```csharp
/// <summary>
/// Validates that each constructor in a struct initializes all fields.
/// C# requires all struct fields to be definitely assigned before the constructor returns.
/// </summary>
private void ValidateStructFieldInitialization(StructDef structDef, TypeSymbol structSymbol)
{
    // Get all __init__ methods from the struct body
    var constructors = structDef.Body.OfType<FunctionDef>()
        .Where(f => f.Name == "__init__")
        .ToList();

    // If no constructors, struct uses implicit parameterless constructor (zero-initialized)
    if (constructors.Count == 0)
        return;

    var allFieldNames = structSymbol.Fields.Select(f => f.Name).ToHashSet();

    foreach (var ctor in constructors)
    {
        // Track which fields are initialized in this constructor
        var initializedFields = new HashSet<string>();

        // Analyze constructor body for self.field = value assignments
        CollectFieldAssignments(ctor.Body, initializedFields);

        // Check for uninitialized fields
        var uninitializedFields = allFieldNames.Except(initializedFields).ToList();

        if (uninitializedFields.Count > 0)
        {
            var fieldList = string.Join(", ", uninitializedFields.Select(f => $"'{f}'"));
            var plural = uninitializedFields.Count > 1 ? "s" : "";
            AddError(
                $"Struct '{structSymbol.Name}' constructor does not initialize field{plural}: {fieldList}",
                ctor.LineStart,
                ctor.ColumnStart);
        }
    }
}

/// <summary>
/// Recursively collects field assignments (self.field = value) from statement list.
/// </summary>
private void CollectFieldAssignments(List<Statement> statements, HashSet<string> initializedFields)
{
    foreach (var statement in statements)
    {
        switch (statement)
        {
            case Assignment assignment:
                // Check for self.field = value pattern
                if (assignment.Target is MemberAccess ma &&
                    ma.Object is Identifier selfId &&
                    selfId.Name == "self")
                {
                    initializedFields.Add(ma.Member);
                }
                break;

            case If ifStmt:
                // For if statements, we need both branches to initialize for it to count
                // For simplicity (and safety), we only count unconditional assignments
                // A more sophisticated analysis would track both branches
                break;

            case While whileStmt:
                // Loop body is conditional, so we don't count it
                break;

            case For forStmt:
                // Loop body is conditional, so we don't count it
                break;

            case ExpressionStatement exprStmt:
                // Handle augmented assignment via function call if needed
                break;
        }
    }
}
```

### Step 4: Update CheckFunction for Struct Context

Update `CheckFunction` (around line 178) to handle struct context similar to class context:

```csharp
// In CheckFunction, add struct context awareness:
if (functionDef.Name == "__init__" && _currentStruct != null)
{
    // Find the matching constructor by declaration line number
    functionSymbol = _currentStruct.Constructors
        .FirstOrDefault(c => c.DeclarationLine == functionDef.LineStart);
}
```

This already exists for `_currentClass`; add parallel logic for `_currentStruct`.

### Step 5: Validate Struct-Specific Rules in CheckFunction

Add validation that structs cannot have abstract or virtual methods:

```csharp
// In CheckFunction, after checking @abstract decorator (around line 264):
if (_currentStruct != null)
{
    if (isAbstract)
    {
        AddError($"Struct methods cannot be abstract: '{functionDef.Name}'",
            functionDef.LineStart, functionDef.ColumnStart);
    }

    bool isVirtual = functionDef.Decorators.Any(d => d.Name == "virtual");
    if (isVirtual)
    {
        AddError($"Struct methods cannot be virtual: '{functionDef.Name}'",
            functionDef.LineStart, functionDef.ColumnStart);
    }
}
```

---

## 4. Tests to Implement

### Test File: `src/Sharpy.Compiler.Tests/Semantic/TypeCheckerTests.cs`

Add a new region `#region Struct Validation Tests`:

### Test 1: Constructor Must Initialize All Fields (Negative)

```csharp
[Fact]
public void StructConstructor_MissingFieldInitialization_ProducesError()
{
    var source = @"
struct Point:
    x: int
    y: int
    z: int

    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y
        # Missing: self.z = ...
";
    var (module, _, _, _, typeChecker) = CompileAndCheck(source);
    typeChecker.CheckModule(module);

    typeChecker.Errors.Should().ContainSingle();
    typeChecker.Errors[0].Message.Should().Contain("does not initialize field");
    typeChecker.Errors[0].Message.Should().Contain("'z'");
}
```

### Test 2: Constructor Initializes All Fields (Positive)

```csharp
[Fact]
public void StructConstructor_AllFieldsInitialized_NoError()
{
    var source = @"
struct Point:
    x: int
    y: int

    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y
";
    var (module, _, _, _, typeChecker) = CompileAndCheck(source);
    typeChecker.CheckModule(module);

    typeChecker.Errors.Should().BeEmpty();
}
```

### Test 3: Struct Without Constructor (Implicit Zero-Init)

```csharp
[Fact]
public void StructWithoutConstructor_UsesImplicitZeroInit_NoError()
{
    var source = @"
struct Point:
    x: int
    y: int
";
    var (module, _, _, _, typeChecker) = CompileAndCheck(source);
    typeChecker.CheckModule(module);

    typeChecker.Errors.Should().BeEmpty();
}
```

### Test 4: Multiple Constructors - Each Must Initialize All Fields

```csharp
[Fact]
public void StructMultipleConstructors_EachMustInitializeAll_ErrorOnIncomplete()
{
    var source = @"
struct Vector:
    x: float
    y: float
    z: float

    def __init__(self):
        self.x = 0.0
        self.y = 0.0
        # Missing self.z

    def __init__(self, x: float, y: float, z: float):
        self.x = x
        self.y = y
        self.z = z
";
    var (module, _, _, _, typeChecker) = CompileAndCheck(source);
    typeChecker.CheckModule(module);

    typeChecker.Errors.Should().ContainSingle();
    typeChecker.Errors[0].Message.Should().Contain("'z'");
}
```

### Test 5: Struct Cannot Have Abstract Methods

```csharp
[Fact]
public void StructWithAbstractMethod_ProducesError()
{
    var source = @"
struct Point:
    x: int
    y: int

    @abstract
    def distance(self) -> float:
        ...
";
    var (module, _, _, _, typeChecker) = CompileAndCheck(source);
    typeChecker.CheckModule(module);

    typeChecker.Errors.Should().Contain(e =>
        e.Message.Contains("Struct methods cannot be abstract"));
}
```

### Test 6: Struct Cannot Have Virtual Methods

```csharp
[Fact]
public void StructWithVirtualMethod_ProducesError()
{
    var source = @"
struct Point:
    x: int
    y: int

    @virtual
    def magnitude(self) -> float:
        return 0.0
";
    var (module, _, _, _, typeChecker) = CompileAndCheck(source);
    typeChecker.CheckModule(module);

    typeChecker.Errors.Should().Contain(e =>
        e.Message.Contains("Struct methods cannot be virtual"));
}
```

### Test 7: Struct Field Type Resolution

```csharp
[Fact]
public void StructFieldTypes_AreResolved()
{
    var source = @"
struct Point:
    x: float
    y: float

    def magnitude(self) -> float:
        return (self.x ** 2 + self.y ** 2) ** 0.5
";
    var (module, symbolTable, _, _, typeChecker) = CompileAndCheck(source);
    typeChecker.CheckModule(module);

    typeChecker.Errors.Should().BeEmpty();

    var pointType = symbolTable.LookupType("Point");
    pointType.Fields[0].Type.GetDisplayName().Should().Be("float");
    pointType.Fields[1].Type.GetDisplayName().Should().Be("float");
}
```

### Test 8: Struct Constructor Overload Validation

```csharp
[Fact]
public void StructDuplicateConstructorSignature_ProducesError()
{
    var source = @"
struct Point:
    x: int
    y: int

    def __init__(self, x: int):
        self.x = x
        self.y = 0

    def __init__(self, y: int):  # Same signature as above!
        self.x = 0
        self.y = y
";
    var (module, _, _, _, typeChecker) = CompileAndCheck(source);
    typeChecker.CheckModule(module);

    typeChecker.Errors.Should().Contain(e =>
        e.Message.Contains("Duplicate constructor signature"));
}
```

---

## 5. Implementation Order

1. **Add `_currentStruct` field** to TypeChecker
2. **Update `CheckStruct` method** with full validation
3. **Implement `ValidateStructFieldInitialization`** method
4. **Implement `CollectFieldAssignments`** helper
5. **Update `CheckFunction`** for struct context awareness
6. **Add struct method restrictions** (no abstract/virtual)
7. **Write tests** - all 8 tests above
8. **Run tests** and fix any issues

---

## 6. Potential Risks and Edge Cases

### Risks

1. **Conditional field initialization**
   - Risk: `if condition: self.x = 1` - field only initialized sometimes
   - Mitigation: Conservative approach - only count unconditional direct assignments
   - Future: Could add dataflow analysis for more precise checking

2. **Nested assignments**
   - Risk: `self.x = self.y = 0` (chained assignment)
   - Mitigation: Handle `Assignment` with `MemberAccess` target pattern

3. **Field initialization in helper methods**
   - Risk: `self._init_defaults()` called from `__init__`
   - Mitigation: Only track direct `self.field = value` in constructor body
   - Note: This matches C# compiler behavior (constructor must directly assign)

4. **Property vs Field confusion**
   - Risk: `self.X` vs `self._x` naming conventions
   - Mitigation: Use exact field names from `TypeSymbol.Fields`

### Edge Cases to Handle

1. **Empty struct** (`struct Unit: pass`) - Valid, no fields to initialize
2. **Struct with only methods** - Valid, no field initialization needed
3. **Inherited interface fields** - N/A for structs (no field inheritance)
4. **Generic structs** - Field types may contain type parameters

---

## 7. Questions for Clarification

1. **Conditional initialization strictness**
   - Option A: Any path that doesn't initialize is an error (C# behavior)
   - Option B: Allow conditional if at least one path initializes
   - **Recommendation:** Option A (matches C# compiler)

2. **Default value assignments**
   - Should `x: int = 0` count as "initialized"?
   - **Recommendation:** No - C# doesn't allow instance field initializers in structs prior to C# 10. The constructor must explicitly assign.

3. **Should we validate struct inheritance restrictions?**
   - Already done in NameResolver (`ResolveStructInheritance`)
   - No duplicate validation needed in TypeChecker

---

## 8. Verification Checklist

- [ ] `_currentStruct` field added
- [ ] `CheckStruct` method fully implements class-like validation
- [ ] `ValidateStructFieldInitialization` tracks all constructor field assignments
- [ ] `CollectFieldAssignments` handles direct `self.field = value` pattern
- [ ] `CheckFunction` recognizes struct constructor context
- [ ] Abstract method restriction enforced for structs
- [ ] Virtual method restriction enforced for structs
- [ ] All 8 tests pass
- [ ] Run `dotnet test --filter "Struct"` - all pass
- [ ] Manual test with example from task description

---

## 9. Conclusion

This implementation adds struct-specific semantic validation to match C# struct requirements. The key validation is ensuring constructors initialize all fields. The implementation:

1. Reuses existing patterns from `CheckClass`
2. Adds struct-specific restrictions (no abstract/virtual)
3. Implements field initialization tracking for constructors
4. Maintains consistency with existing error reporting

The conservative approach to conditional initialization (only counting unconditional direct assignments) matches C# compiler behavior and avoids false negatives that could lead to C# compilation errors.
