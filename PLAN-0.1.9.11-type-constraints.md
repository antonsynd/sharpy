# Implementation Plan: Task 0.1.9.11 - Type Constraints

## Overview

Add support for parsing and generating type constraints on generic type parameters in Sharpy.

**Syntax Examples:**
```python
def find_max[T: IComparable](a: T, b: T) -> T:    # Interface constraint
    ...

class Container[T: class](value: T):              # Reference type constraint
    ...

struct Wrapper[T: struct](value: T):              # Value type constraint
    ...

def create[T: new()](count: int) -> list[T]:      # Constructor constraint
    ...

def process[T: IFoo & IBar](item: T):             # Multiple constraints
    ...
```

**C# Output:**
```csharp
public static T FindMax<T>(T a, T b) where T : IComparable { ... }
public class Container<T> where T : class { ... }
public struct Wrapper<T> where T : struct { ... }
public static List<T> Create<T>(int count) where T : new() { ... }
public static void Process<T>(T item) where T : IFoo, IBar { ... }
```

---

## Step 1: Add AST Nodes for Type Constraints

**File:** `src/Sharpy.Compiler/Parser/Ast/Statement.cs`

### 1.1 Add New Record Types

Add after the existing type definition records (~line 230):

```csharp
/// <summary>
/// Represents a single type parameter with its constraints (e.g., "T: IComparable")
/// </summary>
public record TypeParameterDef
{
    public string Name { get; init; } = "";
    public List<ConstraintClause> Constraints { get; init; } = new();
}

/// <summary>
/// Base type for constraint clauses
/// </summary>
public abstract record ConstraintClause;

/// <summary>
/// Interface/type constraint: T: IComparable
/// </summary>
public record TypeConstraint : ConstraintClause
{
    public TypeAnnotation Type { get; init; } = null!;
}

/// <summary>
/// Reference type constraint: T: class
/// </summary>
public record ClassConstraint : ConstraintClause;

/// <summary>
/// Value type constraint: T: struct
/// </summary>
public record StructConstraint : ConstraintClause;

/// <summary>
/// Constructor constraint: T: new()
/// </summary>
public record NewConstraint : ConstraintClause;
```

### 1.2 Update Existing Definition Records

Modify `FunctionDef`, `ClassDef`, `StructDef`, `InterfaceDef` to replace:
```csharp
public List<string> TypeParameters { get; init; } = new();
```
With:
```csharp
public List<TypeParameterDef> TypeParameters { get; init; } = new();
```

---

## Step 2: Update Parser for Constraint Syntax

**File:** `src/Sharpy.Compiler/Parser/Parser.cs`

### 2.1 Add Helper Method for Parsing Type Parameters with Constraints

Create new method (around line 600):

```csharp
private List<TypeParameterDef> ParseTypeParameterList()
{
    var typeParams = new List<TypeParameterDef>();

    Expect(TokenType.LeftBracket);

    do
    {
        var paramName = ExpectIdentifier();
        var constraints = new List<ConstraintClause>();

        // Check for constraint: T: IComparable
        if (Current.Type == TokenType.Colon)
        {
            Advance(); // consume ':'
            constraints = ParseConstraints();
        }

        typeParams.Add(new TypeParameterDef
        {
            Name = paramName,
            Constraints = constraints
        });

        if (Current.Type == TokenType.Comma)
            Advance();
        else
            break;
    } while (true);

    Expect(TokenType.RightBracket);

    return typeParams;
}

private List<ConstraintClause> ParseConstraints()
{
    var constraints = new List<ConstraintClause>();

    do
    {
        constraints.Add(ParseSingleConstraint());

        if (Current.Type == TokenType.Ampersand)
            Advance(); // consume '&'
        else
            break;
    } while (true);

    return constraints;
}

private ConstraintClause ParseSingleConstraint()
{
    // class constraint
    if (Current.Type == TokenType.Class)
    {
        Advance();
        return new ClassConstraint();
    }

    // struct constraint
    if (Current.Type == TokenType.Struct)
    {
        Advance();
        return new StructConstraint();
    }

    // new() constraint
    if (Current.Type == TokenType.Identifier && Current.Value == "new")
    {
        Advance();
        Expect(TokenType.LeftParen);
        Expect(TokenType.RightParen);
        return new NewConstraint();
    }

    // Type constraint (interface or base type)
    var type = ParseTypeAnnotation();
    return new TypeConstraint { Type = type };
}
```

### 2.2 Update Type Definition Parsing Methods

Update `ParseFunctionDef()` (~line 315-340):
- Replace simple string list parsing with `ParseTypeParameterList()`

Update `ParseClassDef()` (~line 391-404):
- Replace simple string list parsing with `ParseTypeParameterList()`

Update `ParseStructDef()` (~line 455-468):
- Replace simple string list parsing with `ParseTypeParameterList()`

Update `ParseInterfaceDef()` (~line 530-543):
- Replace simple string list parsing with `ParseTypeParameterList()`

---

## Step 3: Update Semantic Analysis

**File:** `src/Sharpy.Compiler/Semantic/Symbol.cs`

### 3.1 Update TypeSymbol

Change from:
```csharp
public List<string> TypeParameters { get; init; } = new();
```
To:
```csharp
public List<TypeParameterDef> TypeParameters { get; init; } = new();
```

### 3.2 Update FunctionSymbol

Add type parameter constraints support.

**File:** `src/Sharpy.Compiler/Semantic/NameResolver.cs`

### 3.3 Update Symbol Creation

Update `ResolveClassDeclaration()`, `ResolveStructDeclaration()`, `ResolveInterfaceDeclaration()`, and function resolution to pass the full `TypeParameterDef` list instead of just names.

**File:** `src/Sharpy.Compiler/Semantic/TypeResolver.cs`

### 3.4 Add Constraint Validation (Optional for this phase)

Add method to validate that type arguments satisfy constraints when a generic type is instantiated. This could be deferred to a follow-up task if time is limited.

---

## Step 4: Update Code Generation

**File:** `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

### 4.1 Add Constraint Generation Helper

Add new method (~line 800):

```csharp
private SyntaxList<TypeParameterConstraintClauseSyntax> GenerateConstraintClauses(
    List<TypeParameterDef> typeParameters)
{
    var clauses = new List<TypeParameterConstraintClauseSyntax>();

    foreach (var typeParam in typeParameters)
    {
        if (typeParam.Constraints.Count == 0)
            continue;

        var constraintSyntaxes = new List<TypeParameterConstraintSyntax>();

        // Order: class/struct first, then types, then new()
        var ordered = typeParam.Constraints
            .OrderBy(c => c switch
            {
                ClassConstraint => 0,
                StructConstraint => 0,
                TypeConstraint => 1,
                NewConstraint => 2,
                _ => 3
            });

        foreach (var constraint in ordered)
        {
            constraintSyntaxes.Add(constraint switch
            {
                ClassConstraint => ClassOrStructConstraint(
                    SyntaxKind.ClassConstraint),
                StructConstraint => ClassOrStructConstraint(
                    SyntaxKind.StructConstraint),
                TypeConstraint tc => TypeConstraint(
                    MapType(tc.Type)),
                NewConstraint => ConstructorConstraint(),
                _ => throw new InvalidOperationException()
            });
        }

        clauses.Add(TypeParameterConstraintClause(typeParam.Name)
            .WithConstraints(SeparatedList(constraintSyntaxes)));
    }

    return List(clauses);
}
```

### 4.2 Update Method Generation

Update function emission (~line 502-508):
```csharp
if (func.TypeParameters.Count > 0)
{
    var typeParams = func.TypeParameters
        .Select(tp => TypeParameter(tp.Name))  // Changed from tp to tp.Name
        .ToArray();
    method = method
        .WithTypeParameterList(TypeParameterList(SeparatedList(typeParams)))
        .WithConstraintClauses(GenerateConstraintClauses(func.TypeParameters));
}
```

### 4.3 Update Class/Struct/Interface Generation

Apply similar changes to:
- `GenerateClassDeclaration()` (~line 669)
- `GenerateStructDeclaration()` (~line 716)
- `GenerateInterfaceDeclaration()` (~line 760)

---

## Step 5: Update Tests

### 5.1 Parser Tests

**File:** `src/Sharpy.Compiler.Tests/Parser/ParserTests.cs`

Add in `#region Generic Type Tests`:

```csharp
[Fact]
public void ParseFunctionWithInterfaceConstraint()
{
    var source = "def find_max[T: IComparable](a: T, b: T) -> T:\n    return a\n";
    var module = Parse(source);
    var func = module.Body[0].Should().BeOfType<FunctionDef>().Subject;
    func.TypeParameters.Should().HaveCount(1);
    func.TypeParameters[0].Name.Should().Be("T");
    func.TypeParameters[0].Constraints.Should().HaveCount(1);
    func.TypeParameters[0].Constraints[0].Should().BeOfType<TypeConstraint>();
}

[Fact]
public void ParseFunctionWithClassConstraint()
{
    var source = "def process[T: class](item: T):\n    pass\n";
    var module = Parse(source);
    var func = module.Body[0].Should().BeOfType<FunctionDef>().Subject;
    func.TypeParameters[0].Constraints[0].Should().BeOfType<ClassConstraint>();
}

[Fact]
public void ParseFunctionWithStructConstraint()
{
    var source = "def process[T: struct](item: T):\n    pass\n";
    var module = Parse(source);
    var func = module.Body[0].Should().BeOfType<FunctionDef>().Subject;
    func.TypeParameters[0].Constraints[0].Should().BeOfType<StructConstraint>();
}

[Fact]
public void ParseFunctionWithNewConstraint()
{
    var source = "def create[T: new()]() -> T:\n    pass\n";
    var module = Parse(source);
    var func = module.Body[0].Should().BeOfType<FunctionDef>().Subject;
    func.TypeParameters[0].Constraints[0].Should().BeOfType<NewConstraint>();
}

[Fact]
public void ParseFunctionWithMultipleConstraints()
{
    var source = "def process[T: IFoo & IBar](item: T):\n    pass\n";
    var module = Parse(source);
    var func = module.Body[0].Should().BeOfType<FunctionDef>().Subject;
    func.TypeParameters[0].Constraints.Should().HaveCount(2);
}

[Fact]
public void ParseClassWithConstraint()
{
    var source = "class Container[T: class]:\n    pass\n";
    var module = Parse(source);
    var classDef = module.Body[0].Should().BeOfType<ClassDef>().Subject;
    classDef.TypeParameters[0].Constraints[0].Should().BeOfType<ClassConstraint>();
}

[Fact]
public void ParseMultipleTypeParamsWithConstraints()
{
    var source = "def convert[T: IInput, U: class & IOutput](val: T) -> U:\n    pass\n";
    var module = Parse(source);
    var func = module.Body[0].Should().BeOfType<FunctionDef>().Subject;
    func.TypeParameters.Should().HaveCount(2);
    func.TypeParameters[0].Name.Should().Be("T");
    func.TypeParameters[1].Name.Should().Be("U");
    func.TypeParameters[1].Constraints.Should().HaveCount(2);
}
```

### 5.2 Code Generation Tests

**File:** `src/Sharpy.Compiler.Tests/CodeGen/RoslynEmitterDefinitionTests.cs`

```csharp
[Fact]
public void EmitFunctionWithInterfaceConstraint()
{
    var code = Compile("def find_max[T: IComparable](a: T, b: T) -> T:\n    return a\n");
    code.Should().Contain("where T : IComparable");
}

[Fact]
public void EmitFunctionWithClassConstraint()
{
    var code = Compile("def process[T: class](item: T):\n    pass\n");
    code.Should().Contain("where T : class");
}

[Fact]
public void EmitFunctionWithStructConstraint()
{
    var code = Compile("def process[T: struct](item: T):\n    pass\n");
    code.Should().Contain("where T : struct");
}

[Fact]
public void EmitFunctionWithNewConstraint()
{
    var code = Compile("def create[T: new()]() -> T:\n    pass\n");
    code.Should().Contain("where T : new()");
}

[Fact]
public void EmitFunctionWithMultipleConstraints()
{
    var code = Compile("def process[T: class & IFoo](item: T):\n    pass\n");
    code.Should().Contain("where T : class, IFoo");
}

[Fact]
public void EmitClassWithConstraint()
{
    var code = Compile("class Container[T: ISerializable]:\n    pass\n");
    code.Should().Contain("where T : ISerializable");
}
```

---

## Step 6: Fix Existing Test Breakage

Since we're changing `List<string>` to `List<TypeParameterDef>`, existing tests that reference `.TypeParameters` will need updates:

- Update assertions from `func.TypeParameters.Should().Equal("T")` to `func.TypeParameters.Select(p => p.Name).Should().Equal("T")`
- Or add a helper property/extension for backward compatibility

---

## Files to Modify

| File | Changes |
|------|---------|
| `src/Sharpy.Compiler/Parser/Ast/Statement.cs` | Add constraint AST nodes, update type definition records |
| `src/Sharpy.Compiler/Parser/Parser.cs` | Add constraint parsing logic |
| `src/Sharpy.Compiler/Semantic/Symbol.cs` | Update TypeSymbol type parameter storage |
| `src/Sharpy.Compiler/Semantic/NameResolver.cs` | Update symbol creation to use new structure |
| `src/Sharpy.Compiler/Semantic/TypeResolver.cs` | Update generic type resolution (minimal changes) |
| `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs` | Add constraint clause generation |
| `src/Sharpy.Compiler.Tests/Parser/ParserTests.cs` | Add constraint parsing tests |
| `src/Sharpy.Compiler.Tests/CodeGen/RoslynEmitterDefinitionTests.cs` | Add constraint emission tests |

---

## Potential Risks & Questions

### Risks

1. **Breaking Change in AST**: Changing `List<string>` to `List<TypeParameterDef>` will break all code that accesses `.TypeParameters`. Need to update all usages across the codebase.

2. **Lexer Token Handling**: Need to verify `class`, `struct`, and `new` are recognized correctly in the constraint context (they might be reserved keywords).

3. **Ampersand Token**: Need to verify `&` (TokenType.Ampersand) exists and is properly lexed.

### Questions for Clarification

1. **Constraint Order**: Should we enforce C# ordering rules (class/struct first, then interfaces, then new()) at parse time or code generation time?
   - **Recommendation**: Handle at code gen time for better error messages

2. **Semantic Validation Scope**: Should we validate constraints during semantic analysis (e.g., ensure `T: IFoo` where IFoo is actually an interface)?
   - **Recommendation**: Defer full validation to a follow-up task; emit code and let C# compiler validate

3. **Generic Constraint Syntax in Constraints**: Should we support `T: IComparable[T]` (generic constraints)?
   - **Recommendation**: Support it since `ParseTypeAnnotation()` already handles generic types

---

## Implementation Order

1. **Step 1**: AST changes (foundation)
2. **Step 2**: Parser changes (enables parsing)
3. **Step 3**: Semantic changes (updates symbol system)
4. **Step 4**: Code generation (produces output)
5. **Step 5**: Tests (verifies correctness)
6. **Step 6**: Fix any broken existing tests
