# Implementation Plan: Task 0.1.7.9 - Implement Interface Code Generation

## Executive Summary

**Finding:** Interface code generation is **already fully implemented** in the codebase.

After thorough analysis of the Sharpy compiler, I found that all core interface code generation functionality is complete:
- AST representation (`InterfaceDef` record) ✅
- Parser support for interface declarations ✅
- Roslyn code generator for C# output ✅
- Test coverage for common scenarios ✅

## Current Implementation Status

### 1. AST Definition (`src/Sharpy.Compiler/Parser/Ast/Statement.cs:224-231`)

```csharp
public record InterfaceDef : Statement
{
    public string Name { get; init; } = "";
    public List<string> TypeParameters { get; init; } = new();
    public List<TypeAnnotation> BaseInterfaces { get; init; } = new();
    public List<Statement> Body { get; init; } = new();
    public string? DocString { get; init; }
}
```

### 2. Code Generation (`src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs:704-746`)

The `GenerateInterfaceDeclaration` method handles:
- ✅ Interface name transformation (preserves I prefix)
- ✅ Public modifier (always public)
- ✅ Generic type parameters (`IRepository<T>`)
- ✅ Base interface inheritance (`IShape : IDrawable`)
- ✅ Method signature generation (via `GenerateInterfaceMembers`)
- ✅ XML documentation from docstrings

### 3. Interface Method Generation (`src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs:1311-1337`)

The `GenerateInterfaceMethod` method:
- ✅ Transforms method names to PascalCase
- ✅ Maps return types (defaults to void)
- ✅ Filters out `self` parameter
- ✅ Generates parameter list
- ✅ Adds semicolon (no body)
- ✅ Adds XML documentation

### 4. Existing Test Coverage (`src/Sharpy.Compiler.Tests/CodeGen/RoslynEmitterDefinitionTests.cs:919-1018`)

| Test | Status |
|------|--------|
| Simple interface generation | ✅ |
| Interface with method signatures | ✅ |
| Base interface inheritance | ✅ |
| Generic interface parameters | ✅ |

---

## What May Be Missing (Based on Task Description)

The task description mentions "Handle interface properties" but was truncated. Let me analyze:

### Interface Properties - NOT YET IMPLEMENTED

The current implementation only handles methods. Properties would be a new feature:

**Sharpy syntax (hypothetical):**
```python
interface IDrawable:
    color: str  # Property definition

    def draw(self) -> None:
        ...
```

**Expected C# output:**
```csharp
public interface IDrawable
{
    string Color { get; set; }
    void Draw();
}
```

---

## Step-by-Step Implementation Approach

### If Properties Need to be Added:

#### Step 1: Update AST (Optional - may use existing `TypedField`)
No changes needed if we use existing field syntax within interfaces.

#### Step 2: Update `GenerateInterfaceMembers` (`RoslynEmitter.cs:1281-1309`)

Add handling for property definitions:

```csharp
private List<MemberDeclarationSyntax> GenerateInterfaceMembers(List<Statement> body)
{
    var members = new List<MemberDeclarationSyntax>();

    foreach (var stmt in body)
    {
        switch (stmt)
        {
            case FunctionDef funcDef:
                members.Add(GenerateInterfaceMethod(funcDef));
                break;

            // ADD: Handle interface properties (field annotations)
            case TypedField field:
                members.Add(GenerateInterfaceProperty(field));
                break;

            case PassStatement:
            case ExpressionStatement { Expression: EllipsisLiteral }:
                break;
        }
    }
    return members;
}
```

#### Step 3: Add `GenerateInterfaceProperty` Method

```csharp
private PropertyDeclarationSyntax GenerateInterfaceProperty(TypedField field)
{
    var propertyName = NameMangler.Transform(field.Name, NameContext.Method); // PascalCase
    var propertyType = _typeMapper.MapType(field.Type);

    var property = PropertyDeclaration(propertyType, propertyName)
        .WithAccessorList(AccessorList(List(new[]
        {
            AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)),
            AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken))
        })));

    return property;
}
```

#### Step 4: Add Tests for Interface Properties

```csharp
[Fact]
public void GenerateInterfaceDeclaration_WithProperty_GeneratesPropertySignature()
{
    var interfaceDef = new InterfaceDef
    {
        Name = "IDrawable",
        Body = new List<Statement>
        {
            new TypedField
            {
                Name = "color",
                Type = new TypeAnnotation { Name = "str" }
            }
        }
    };

    var module = new Module { Body = new List<Statement> { interfaceDef } };
    var compilationUnit = _emitter.GenerateCompilationUnit(module);
    var code = compilationUnit.NormalizeWhitespace().ToFullString();

    Assert.Contains("string Color { get; set; }", code);
}
```

---

## Key Files to Modify

| File | Changes Needed |
|------|---------------|
| `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs` | Add `GenerateInterfaceProperty`, update `GenerateInterfaceMembers` |
| `src/Sharpy.Compiler.Tests/CodeGen/RoslynEmitterDefinitionTests.cs` | Add property test cases |

---

## Tests to Verify

### Existing Tests (Should Continue to Pass)
1. `GenerateInterfaceDeclaration_SimpleInterface_GeneratesPublicInterface`
2. `GenerateInterfaceDeclaration_WithMethod_GeneratesMethodSignature`
3. `GenerateInterfaceDeclaration_WithBaseInterface_GeneratesInheritance`
4. `GenerateInterfaceDeclaration_WithGenericTypeParameter_GeneratesGenericInterface`

### New Tests (If Properties Added)
1. `GenerateInterfaceDeclaration_WithProperty_GeneratesPropertySignature`
2. `GenerateInterfaceDeclaration_WithReadonlyProperty_GeneratesGetterOnly`
3. `GenerateInterfaceDeclaration_WithMethodsAndProperties_GeneratesBoth`

---

## Potential Risks or Questions

### Questions to Clarify

1. **Property syntax in Sharpy interfaces:** How are properties defined?
   - Is it `color: str` (field annotation)?
   - Or `@property def color(self) -> str: ...` (Python-style)?

2. **Read-only vs read-write properties:** Should interfaces support:
   - `{ get; }` only?
   - `{ get; set; }` only?
   - User-controlled via decorator?

3. **Is this task actually needed?** The core interface generation is complete. The task description was truncated - need to confirm if properties are in scope.

### Risks

1. **Parser support:** If properties use a new syntax, the parser may need updates.

2. **TypedField in interfaces:** Need to verify the parser allows `TypedField` statements inside interface bodies.

3. **Backward compatibility:** Adding new AST handling shouldn't break existing tests, but verify with test run.

---

## Recommendation

**Before implementation, clarify:**
1. Is the task truly "not done" or is this a verification task?
2. What specific interface property syntax is expected?
3. Are there edge cases (readonly, writeonly, indexed properties)?

**If no properties needed:** Mark task as complete after running existing tests.

**If properties needed:** Follow the step-by-step approach above (~30 minutes of implementation).
