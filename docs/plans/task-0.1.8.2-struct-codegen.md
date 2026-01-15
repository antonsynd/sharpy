# Implementation Plan: Task 0.1.8.2 - Implement Struct Code Generation

## Summary

**Status:** Implementation already exists - task requires verification and potential enhancement of tests.

The struct code generation (`GenerateStructDeclaration`) was implemented as part of Task 0.1.8.1 (struct definition AST and parsing). The current implementation in `RoslynEmitter.cs:660-702` correctly generates C# structs from Sharpy struct definitions.

---

## 1. Current Implementation Analysis

### Already Implemented (RoslynEmitter.cs:660-702)

```csharp
private StructDeclarationSyntax GenerateStructDeclaration(StructDef structDef)
{
    // 1. Transform struct name with proper naming conventions
    var structName = NameMangler.Transform(structDef.Name, NameContext.Type);

    // 2. Process decorators for access modifiers (@public, @private, etc.)
    var modifiers = GenerateTypeModifiersFromDecorators(structDef.Decorators);

    // 3. Create struct declaration
    var structDecl = StructDeclaration(structName).WithModifiers(modifiers);

    // 4. Handle generic type parameters (struct Pair[T1, T2])
    if (structDef.TypeParameters.Count > 0) { ... }

    // 5. Handle interface implementations (structs can only implement interfaces)
    if (structDef.BaseClasses.Count > 0) { ... }

    // 6. Generate members using shared GenerateClassMembers method
    var members = GenerateClassMembers(structDef.Body, structName);

    // 7. Add XML documentation from docstring
    if (!string.IsNullOrEmpty(structDef.DocString)) { ... }

    return structDecl;
}
```

### Type Mapping (Already Correct)

The `PrimitiveCatalog` and `TypeMapper` already handle the float type mapping:

| Sharpy Type | C# Type | Description |
|-------------|---------|-------------|
| `float` | `double` | 64-bit (Python-like default) |
| `float32` | `float` | 32-bit |
| `float64` | `double` | 64-bit explicit |
| `double` | `double` | 64-bit (alias) |

---

## 2. Step-by-Step Verification Plan

### Step 1: Verify Existing Tests Pass
Run the existing struct tests to confirm the implementation works:

```bash
dotnet test --filter "FullyQualifiedName~GenerateStructDeclaration"
```

**Existing tests (RoslynEmitterDefinitionTests.cs:1125-1198):**
- `GenerateStructDeclaration_SimpleStruct_GeneratesPublicStruct`
- `GenerateStructDeclaration_WithFields_GeneratesFieldDeclarations`
- `GenerateStructDeclaration_WithGenericTypeParameter_GeneratesGenericStruct`

### Step 2: Add Missing Test Coverage

The current tests are basic. Add comprehensive tests for:

1. **Struct with Constructor (`__init__`)**
   - Input: `struct Vector2` with `__init__(self, x: float, y: float)`
   - Expected: Constructor with `double x, double y` parameters

2. **Struct with Methods**
   - Input: Method like `def magnitude(self) -> float`
   - Expected: `public double Magnitude()` method

3. **Struct with Interface Implementation**
   - Input: `struct Point(IComparable)`
   - Expected: `: IComparable` in declaration

4. **Float Type Mapping Verification**
   - Input: `x: float` field
   - Expected: `public double X;` (not `float`)
   - Input: `x: float32` field
   - Expected: `public float X;`

5. **Struct with Docstring**
   - Input: Struct with docstring
   - Expected: XML documentation comment

6. **Struct with Decorators**
   - Input: `@internal struct Point`
   - Expected: `internal struct Point`

### Step 3: Integration Test (End-to-End)

Create an integration test that compiles a complete Vector2 struct:

**Input (Sharpy):**
```python
struct Vector2:
    x: float
    y: float

    def __init__(self, x: float, y: float):
        self.x = x
        self.y = y
```

**Expected Output (C#):**
```csharp
public struct Vector2
{
    public double X;
    public double Y;

    public Vector2(double x, double y)
    {
        X = x;
        Y = y;
    }
}
```

---

## 3. Files to Modify

| File | Action | Description |
|------|--------|-------------|
| `src/Sharpy.Compiler.Tests/CodeGen/RoslynEmitterDefinitionTests.cs` | **Add tests** | Add comprehensive struct code generation tests |
| `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs` | **Review only** | Verify implementation is complete (likely no changes needed) |

---

## 4. Test Implementation Details

### Test: Struct with Constructor

```csharp
[Fact]
public void GenerateStructDeclaration_WithConstructor_GeneratesConstructor()
{
    // Arrange
    var structDef = new StructDef
    {
        Name = "Vector2",
        Body = new List<Statement>
        {
            new VariableDeclaration { Name = "x", Type = new TypeAnnotation { Name = "float" } },
            new VariableDeclaration { Name = "y", Type = new TypeAnnotation { Name = "float" } },
            new FunctionDef
            {
                Name = "__init__",
                Parameters = new List<Parameter>
                {
                    new Parameter { Name = "self" },
                    new Parameter { Name = "x", Type = new TypeAnnotation { Name = "float" } },
                    new Parameter { Name = "y", Type = new TypeAnnotation { Name = "float" } }
                },
                Body = new List<Statement>
                {
                    new AssignmentStatement { /* self.x = x */ },
                    new AssignmentStatement { /* self.y = y */ }
                }
            }
        }
    };

    // Act
    var module = new Module { Body = new List<Statement> { structDef } };
    var compilationUnit = _emitter.GenerateCompilationUnit(module);
    var code = compilationUnit.NormalizeWhitespace().ToFullString();

    // Assert
    Assert.Contains("public struct Vector2", code);
    Assert.Contains("public double X;", code);
    Assert.Contains("public double Y;", code);
    Assert.Contains("public Vector2(double x, double y)", code);
}
```

### Test: Float vs Float32 Type Mapping

```csharp
[Fact]
public void GenerateStructDeclaration_FloatField_MapsToDouble()
{
    var structDef = new StructDef
    {
        Name = "Test",
        Body = new List<Statement>
        {
            new VariableDeclaration { Name = "value", Type = new TypeAnnotation { Name = "float" } }
        }
    };

    var code = GenerateCode(structDef);

    Assert.Contains("public double Value;", code);  // float -> double
}

[Fact]
public void GenerateStructDeclaration_Float32Field_MapsToFloat()
{
    var structDef = new StructDef
    {
        Name = "Test",
        Body = new List<Statement>
        {
            new VariableDeclaration { Name = "value", Type = new TypeAnnotation { Name = "float32" } }
        }
    };

    var code = GenerateCode(structDef);

    Assert.Contains("public float Value;", code);  // float32 -> float
}
```

### Test: Struct with Interface

```csharp
[Fact]
public void GenerateStructDeclaration_WithInterface_GeneratesBaseList()
{
    var structDef = new StructDef
    {
        Name = "Point",
        BaseClasses = new List<TypeAnnotation>
        {
            new TypeAnnotation { Name = "IEquatable", TypeArguments = new List<TypeAnnotation>
                { new TypeAnnotation { Name = "Point" } } }
        },
        Body = new List<Statement> { new PassStatement() }
    };

    var code = GenerateCode(structDef);

    Assert.Contains("public struct Point : IEquatable<Point>", code);
}
```

---

## 5. Potential Risks and Questions

### Risks

1. **Constructor self-assignment generation**
   - Need to verify `self.x = x` generates `X = x` correctly
   - The `GenerateClassMembers` method should handle this via field name mapping

2. **Struct default values**
   - C# structs cannot have instance field initializers prior to C# 10
   - Verify we don't generate `public double X = 0.0;` (should be just `public double X;`)

3. **Parameterless constructor**
   - C# structs have implicit parameterless constructors
   - If Sharpy has explicit `__init__(self)`, verify it generates correctly

### Questions for Clarification

1. **Should structs be `readonly`?**
   - Current: Generates mutable structs
   - Option: Add `@readonly` decorator support for `readonly struct`

2. **Should fields be properties?**
   - Current: Generates fields (`public double X;`)
   - Option: Generate auto-properties (`public double X { get; set; }`)
   - Recommendation: Keep fields for structs (better performance)

3. **Interface validation**
   - Current: `BaseClasses` can contain any type annotation
   - Question: Should we validate that only interfaces are listed?
   - Recommendation: Add validation in semantic analysis, not code generation

---

## 6. Verification Checklist

- [ ] Run existing tests: `dotnet test --filter "GenerateStructDeclaration"`
- [ ] Add test for struct with `__init__` constructor
- [ ] Add test for struct with methods
- [ ] Add test for float → double type mapping
- [ ] Add test for float32 → float type mapping
- [ ] Add test for struct with interface implementation
- [ ] Add test for struct with docstring (XML comments)
- [ ] Add test for struct with decorators (@internal, @public)
- [ ] Integration test: Full Vector2 struct compilation
- [ ] Verify generated code compiles with C# compiler

---

## 7. Conclusion

**The core implementation is complete.** Task 0.1.8.2 primarily requires:

1. **Verification** that existing implementation handles all cases
2. **Test enhancement** to ensure comprehensive coverage
3. **No code changes** expected in `RoslynEmitter.cs` unless tests reveal issues

The struct code generation correctly reuses `GenerateClassMembers` for consistency with class generation, handles type parameters and interfaces, and leverages the existing type mapping that already implements `float` → `double` and `float32` → `float`.
