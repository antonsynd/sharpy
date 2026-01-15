# Implementation Plan: Task 0.1.6.4 - `__init__` Code Generation with Overloading and Chaining

## Executive Summary

**Task ID:** 0.1.6.4
**Priority:** Critical
**Status:** Planning

**Current State:** Single `__init__` constructor generation is **fully implemented** in `RoslynEmitter.cs`. The system correctly generates C# constructors from Python `__init__` methods with proper `self` handling, field mapping, and parameter generation.

**What's Missing:**
1. **Constructor Overloading** - Multiple `__init__` methods in the same class are not supported
2. **Constructor Chaining** - No `:this()` syntax support for calling other constructors
3. **Overload Validation** - No semantic check for duplicate constructor signatures

---

## Step-by-Step Implementation Approach

### Step 1: Add Constructor Tracking to TypeSymbol (Semantic)

**File:** `src/Sharpy.Compiler/Semantic/Symbol.cs`

**Change:** Add a `Constructors` dictionary similar to `OperatorMethods` and `ProtocolMethods`.

```csharp
// After line 67 (ProtocolMethods)
// Constructors - tracks all __init__ overloads
public List<FunctionSymbol> Constructors { get; init; } = new();
```

**Rationale:** This provides a dedicated place to track all `__init__` methods without mixing them with regular methods or protocol methods.

**Risk Level:** Low - Adding a new property to a record

---

### Step 2: Register `__init__` Methods in NameResolver

**File:** `src/Sharpy.Compiler/Semantic/NameResolver.cs`

**Location:** `ResolveMethodDeclaration()` method, around line 335

**Current Behavior:** All methods (including `__init__`) are added to `owningType.Methods` list. `__init__` is registered as a protocol method via `ProtocolSignatureValidator`.

**Change:** After registering the method, also add `__init__` methods to the new `Constructors` list.

```csharp
// After line 335: owningType.Methods.Add(funcSymbol);
// Add to Constructors list if this is __init__
if (method.Name == "__init__")
{
    owningType.Constructors.Add(funcSymbol);
    _logger.LogDebug($"Registered constructor overload: {owningType.Name}.__init__ (params: {method.Parameters.Count})");
}
```

**Risk Level:** Low - Adding tracking without changing behavior

---

### Step 3: Add Constructor Signature Validation in TypeChecker

**File:** `src/Sharpy.Compiler/Semantic/TypeChecker.cs`

**New Method:** `ValidateConstructorOverloads(TypeSymbol type)`

This validates that no two `__init__` methods have the same parameter signature (excluding `self`).

```csharp
private void ValidateConstructorOverloads(TypeSymbol type)
{
    var constructors = type.Constructors;
    if (constructors.Count <= 1)
        return;  // No overload conflict possible

    var signatures = new HashSet<string>();
    foreach (var ctor in constructors)
    {
        // Build signature string from parameter types (excluding self)
        var paramTypes = ctor.Parameters
            .Where(p => p.Name != "self")
            .Select(p => p.Type.ToString())
            .ToList();
        var signature = string.Join(",", paramTypes);

        if (!signatures.Add(signature))
        {
            AddError(
                $"Duplicate constructor signature in '{type.Name}': __init__({signature})",
                ctor.DeclarationLine,
                ctor.DeclarationColumn);
        }
    }
}
```

**Call Site:** In `CheckClassDef()` after processing all methods, call `ValidateConstructorOverloads(_currentClass)`.

**Risk Level:** Medium - Adds new validation that could reject previously-accepted code

---

### Step 4: Modify GenerateClassMembers to Collect All `__init__` Methods

**File:** `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

**Location:** `GenerateClassMembers()` method (lines 836-935)

**Current Behavior (lines 878-882):**
```csharp
if (funcDef.Name == "__init__")
{
    members.Add(GenerateConstructor(funcDef, className, fieldMapping));
}
```

This generates the constructor inline as each `__init__` is encountered.

**Changed Approach:**
1. Collect all `__init__` methods first
2. Generate all constructors together

```csharp
// At start of method, after fieldMapping setup (line 843):
var initMethods = new List<FunctionDef>();

// In the foreach loop (replace lines 878-882):
if (funcDef.Name == "__init__")
{
    initMethods.Add(funcDef);
    continue;  // Don't generate here, collect first
}

// After the foreach loop (before return):
// Generate all constructors (supports overloading)
foreach (var initMethod in initMethods)
{
    members.Add(GenerateConstructor(initMethod, className, fieldMapping));
}
```

**Risk Level:** Low - Same behavior for single constructor, enables multiple

---

### Step 5: (Optional) Support Constructor Chaining with `@chain` Decorator

**File:** `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

**Concept:** Allow Sharpy code to specify constructor chaining via a decorator:

```python
class Person:
    name: str
    age: int

    def __init__(self, name: str, age: int):
        self.name = name
        self.age = age

    @chain("", 0)  # Chains to __init__(name, 0)
    def __init__(self, name: str):
        pass  # Body is empty, initialization delegated
```

**Generated C#:**
```csharp
public Person(string name, int age) {
    Name = name;
    Age = age;
}

public Person(string name) : this(name, 0) { }
```

**Implementation:**
1. Check decorators for `@chain(args...)` in `GenerateConstructor()`
2. If found, generate `: this(args)` initializer
3. Optionally skip body generation if chained

**Risk Level:** Medium - New feature, requires decorator parsing

**Recommendation:** Mark as optional/future for initial implementation. The core overloading support is more important.

---

### Step 6: Add Comprehensive Tests

**File:** `src/Sharpy.Compiler.Tests/CodeGen/RoslynEmitterDefinitionTests.cs`

#### Test 1: Multiple Constructors with Different Signatures

```csharp
[Fact]
public void GenerateClassDeclaration_WithMultipleConstructors_GeneratesOverloads()
{
    var classDef = new ClassDef
    {
        Name = "Person",
        Body = new List<Statement>
        {
            new VariableDeclaration { Name = "name", Type = new TypeAnnotation { Name = "string" } },
            new VariableDeclaration { Name = "age", Type = new TypeAnnotation { Name = "int" } },
            // Constructor 1: No params (default values)
            new FunctionDef
            {
                Name = "__init__",
                Parameters = new List<Parameter>
                {
                    new Parameter { Name = "self" }
                },
                Body = new List<Statement>
                {
                    // self.name = ""
                    new Assignment { Target = new MemberAccess { Object = new Identifier { Name = "self" }, Member = "name" }, Value = new StringLiteral { Value = "" } },
                    // self.age = 0
                    new Assignment { Target = new MemberAccess { Object = new Identifier { Name = "self" }, Member = "age" }, Value = new IntLiteral { Value = 0 } }
                }
            },
            // Constructor 2: name only
            new FunctionDef
            {
                Name = "__init__",
                Parameters = new List<Parameter>
                {
                    new Parameter { Name = "self" },
                    new Parameter { Name = "name", Type = new TypeAnnotation { Name = "string" } }
                },
                Body = new List<Statement>
                {
                    new Assignment { Target = new MemberAccess { Object = new Identifier { Name = "self" }, Member = "name" }, Value = new Identifier { Name = "name" } },
                    new Assignment { Target = new MemberAccess { Object = new Identifier { Name = "self" }, Member = "age" }, Value = new IntLiteral { Value = 0 } }
                }
            },
            // Constructor 3: name and age
            new FunctionDef
            {
                Name = "__init__",
                Parameters = new List<Parameter>
                {
                    new Parameter { Name = "self" },
                    new Parameter { Name = "name", Type = new TypeAnnotation { Name = "string" } },
                    new Parameter { Name = "age", Type = new TypeAnnotation { Name = "int" } }
                },
                Body = new List<Statement>
                {
                    new Assignment { Target = new MemberAccess { Object = new Identifier { Name = "self" }, Member = "name" }, Value = new Identifier { Name = "name" } },
                    new Assignment { Target = new MemberAccess { Object = new Identifier { Name = "self" }, Member = "age" }, Value = new Identifier { Name = "age" } }
                }
            }
        }
    };

    var module = new Module { Body = new List<Statement> { classDef } };
    var compilationUnit = _emitter.GenerateCompilationUnit(module);
    var code = compilationUnit.NormalizeWhitespace().ToFullString();

    // Assert all three constructors are generated
    Assert.Contains("public Person()", code);
    Assert.Contains("public Person(string name)", code);
    Assert.Contains("public Person(string name, int age)", code);
}
```

#### Test 2: Duplicate Constructor Signature Error (Semantic)

**File:** `src/Sharpy.Compiler.Tests/Semantic/TypeCheckerTests.cs`

```csharp
[Fact]
public void DuplicateConstructorSignature_ProducesError()
{
    var source = @"
class Person:
    name: str

    def __init__(self, name: str):
        self.name = name

    def __init__(self, name: str):  # Duplicate!
        self.name = name
";
    var errors = RunSemanticAnalysis(source);
    Assert.Single(errors);
    Assert.Contains("Duplicate constructor signature", errors[0].Message);
}
```

#### Test 3: Single Constructor (Regression)

```csharp
[Fact]
public void GenerateClassDeclaration_WithSingleConstructor_StillWorks()
{
    // Existing test case - ensure no regression
}
```

---

## Key Files to Modify

| File | Changes | Priority |
|------|---------|----------|
| `src/Sharpy.Compiler/Semantic/Symbol.cs` | Add `Constructors` list to `TypeSymbol` | High |
| `src/Sharpy.Compiler/Semantic/NameResolver.cs` | Register `__init__` in `Constructors` list | High |
| `src/Sharpy.Compiler/Semantic/TypeChecker.cs` | Add `ValidateConstructorOverloads()` | High |
| `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs` | Collect and generate all `__init__` methods | High |
| `src/Sharpy.Compiler.Tests/CodeGen/RoslynEmitterDefinitionTests.cs` | Add overloading tests | High |
| `src/Sharpy.Compiler.Tests/Semantic/TypeCheckerTests.cs` | Add duplicate signature tests | Medium |

---

## Tests to Verify

### Existing Tests (Must Pass)
```bash
dotnet test src/Sharpy.Compiler.Tests --filter "GenerateClassDeclaration_WithConstructor"
dotnet test src/Sharpy.Compiler.Tests --filter "FullyQualifiedName~RoslynEmitter"
```

### New Tests to Add
1. `GenerateClassDeclaration_WithMultipleConstructors_GeneratesOverloads`
2. `GenerateClassDeclaration_WithParameterlessConstructor_Works`
3. `DuplicateConstructorSignature_ProducesError`
4. `ConstructorOverloads_DifferentParamTypes_Allowed`
5. `ConstructorOverloads_DifferentParamCounts_Allowed`

### Full Test Suite
```bash
dotnet test src/Sharpy.Compiler.Tests
```

---

## Potential Risks and Questions

### Risk 1: Python vs C# Semantics
**Issue:** Python only allows one `__init__` per class (last one wins). Supporting multiple is a Sharpy extension.

**Mitigation:** Document this as a Sharpy feature that extends Python semantics for C# interop.

### Risk 2: Field Initialization Consistency
**Issue:** Different constructors might initialize different sets of fields.

**Mitigation:**
- Phase 1: Allow it (C# allows this)
- Future: Add analyzer to warn about uninitialized fields

### Risk 3: Constructor Ordering in Generated Code
**Issue:** Order of constructors in generated C# affects readability.

**Decision:** Maintain source order from AST (first `__init__` in source = first constructor in output).

### Question 1: Default Constructor Generation
**Question:** Should a default (parameterless) constructor be auto-generated if no `__init__` exists?

**Answer:** No - C# behavior is that no default constructor exists if any constructor is defined. Match C# semantics.

### Question 2: Constructor Chaining Priority
**Question:** Should constructor chaining (`@chain` decorator) be implemented in this task?

**Recommendation:** No - Mark as future enhancement. Focus on basic overloading first.

---

## Implementation Order

1. **Add `Constructors` to TypeSymbol** (5 min)
2. **Update NameResolver** to register constructors (10 min)
3. **Add validation in TypeChecker** (20 min)
4. **Modify GenerateClassMembers** to collect all `__init__` (15 min)
5. **Write overloading tests** (30 min)
6. **Write semantic validation tests** (20 min)
7. **Run full test suite** (10 min)
8. **Fix any issues** (variable)

---

## Success Criteria

1. Multiple `__init__` methods generate as overloaded C# constructors
2. Each constructor has correct parameter list (without `self`)
3. Each constructor properly handles `self.field` assignments
4. Duplicate constructor signatures produce clear semantic error
5. Single `__init__` (existing behavior) continues to work
6. All existing tests pass
7. New comprehensive tests pass
8. Generated C# code compiles successfully

---

## Dependencies

- **Task 0.1.6.3 (Self Handling):** Should be complete or near-complete. The `self` handling infrastructure is already robust based on analysis.
- **Existing Infrastructure:** Leverages existing `OperatorMethods`/`ProtocolMethods` pattern for tracking overloads.

---

## Code Snippets Reference

### Current Constructor Generation (RoslynEmitter.cs:937-1023)
The existing `GenerateConstructor()` method is already well-implemented:
- Skips `self` parameter
- Handles `self.field = value` → `this.Field = value`
- Uses field mapping for consistent naming
- Supports decorators for access modifiers

No changes needed to `GenerateConstructor()` itself - only the calling code in `GenerateClassMembers()` needs modification.

### Existing Overload Pattern (NameResolver.cs:351-356)
```csharp
if (!owningType.OperatorMethods.TryGetValue(method.Name, out var overloads))
{
    overloads = new List<FunctionSymbol>();
    owningType.OperatorMethods[method.Name] = overloads;
}
overloads.Add(funcSymbol);
```

This pattern can be simplified for constructors since we use a list, not a dictionary.
