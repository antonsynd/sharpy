# Implementation Plan: Task 0.1.5.7 - Function Code Generation

## Status Assessment

**✅ ALREADY IMPLEMENTED** - Function code generation is fully implemented and well-tested.

Based on analysis of the codebase, all four verification items in this task are already complete:

| Item | Status | Location |
|------|--------|----------|
| Functions generate as C# static methods | ✅ Done | `RoslynEmitter.cs:379-426`, `450-518` |
| Name mangling (snake_case → PascalCase) | ✅ Done | `NameMangler.cs:84-128` |
| Parameter generation with types | ✅ Done | `RoslynEmitter.cs:428-448` |
| Default parameter values | ✅ Done | `RoslynEmitter.cs:441-445` |

---

## Step-by-Step Verification Approach

### Step 1: Verify Functions Generate as C# Static Methods

**Location**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs:379-426`

**Current Implementation**:
```csharp
private MethodDeclarationSyntax GenerateFunctionDeclaration(FunctionDef func)
{
    var mangledName = NameMangler.Transform(func.Name, NameContext.Method);
    // ...
    var modifiers = GenerateModifiersFromDecorators(func.Decorators);
    // ...
}
```

**Modifier Generation** (`RoslynEmitter.cs:450-518`):
- Default: `public static` if no decorators specified
- Line 508-513 explicitly adds `static` for module-level functions:
```csharp
if (!tokens.Any(t => t.IsKind(SyntaxKind.StaticKeyword) || ...))
{
    tokens.Add(Token(SyntaxKind.StaticKeyword));
}
```

**Verification**: ✅ Module-level functions automatically become `public static` methods.

---

### Step 2: Verify Name Mangling (hello_world → HelloWorld)

**Location**: `src/Sharpy.Compiler/CodeGen/NameMangler.cs:84-128`

**Current Implementation**:
```csharp
public static string ToPascalCase(string name)
{
    // Handles:
    // - snake_case → PascalCase (my_function → MyFunction)
    // - Dunder methods (__init__ → Constructor, __str__ → ToString)
    // - Private prefix preservation (_private → _Private)
    // - Literal names (`ExactName` → ExactName)
    var parts = cleanName.Split('_');
    var result = string.Join("", parts.Select(Capitalize));
    return EscapeKeywordIfNeeded(result);
}
```

**Examples**:
| Input | Output |
|-------|--------|
| `hello_world` | `HelloWorld` |
| `calculate_total` | `CalculateTotal` |
| `_private_method` | `_PrivateMethod` |
| `__init__` | `Constructor` |
| `__add__` | `__Add__` |

**Verification**: ✅ Name mangling is correctly implemented.

---

### Step 3: Verify Parameter Generation with Types

**Location**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs:428-448`

**Current Implementation**:
```csharp
private ParameterSyntax GenerateParameter(Parameter param)
{
    var paramName = NameMangler.Transform(param.Name, NameContext.Parameter);

    TypeSyntax paramType = param.Type != null
        ? _typeMapper.MapType(param.Type)
        : PredefinedType(Token(SyntaxKind.ObjectKeyword));

    var parameter = Parameter(Identifier(paramName))
        .WithType(paramType);
    // ...
}
```

**Type Mapping**: Uses `TypeMapper` to convert Python types to C#:
- `int` → `int`
- `str`/`string` → `string`
- `bool` → `bool`
- `float` → `double`
- No annotation → `object`

**Verification**: ✅ Parameters are generated with proper types.

---

### Step 4: Verify Default Parameter Values in C# Signature

**Location**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs:441-445`

**Current Implementation**:
```csharp
if (param.DefaultValue != null)
{
    var defaultExpr = GenerateExpression(param.DefaultValue);
    parameter = parameter.WithDefault(EqualsValueClause(defaultExpr));
}
```

**Validation** (`DefaultParameterValidator.cs`):
- Ensures default values are compile-time constants
- Supported: integers, floats, strings, booleans, None, tuples, unary/binary ops
- Rejects: mutable defaults (lists, dicts, sets)

**Example**:
```python
def greet(name: str = "World") -> str:
    return f"Hello, {name}"
```
Generates:
```csharp
public static string Greet(string name = "World")
{
    return $"Hello, {name}";
}
```

**Verification**: ✅ Default parameter values are correctly generated.

---

## Key Files

| File | Purpose |
|------|---------|
| `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs` | Main code generation |
| `src/Sharpy.Compiler/CodeGen/NameMangler.cs` | Name transformation (snake_case → PascalCase) |
| `src/Sharpy.Compiler/CodeGen/TypeMapper.cs` | Python → C# type mapping |
| `src/Sharpy.Compiler/Semantic/DefaultParameterValidator.cs` | Default value validation |

---

## Existing Tests

| Test File | Coverage |
|-----------|----------|
| `RoslynEmitterDefinitionTests.cs` | Function declaration, parameters, defaults, decorators |
| `NameManglerTests.cs` | All name transformation cases |

**Specific Tests**:
1. `GenerateFunctionDeclaration_SimpleFunction_GeneratesPublicStaticMethod`
2. `GenerateFunctionDeclaration_WithParameters_GeneratesParameterList`
3. `GenerateFunctionDeclaration_WithDefaultParameter_GeneratesDefaultValue`
4. `GenerateFunctionDeclaration_WithDocstring_GeneratesXmlDoc`
5. `GenerateFunctionDeclaration_WithPrivateDecorator_GeneratesPrivateMethod`

---

## Example Transformation Verification

**Input** (from task description):
```python
def add(a: int, b: int = 1) -> int:
    return a * b
```

**Expected Output**:
```csharp
public static int Add(int a, int b = 1)
{
    return a * b;
}
```

**Verification Steps**:
1. ✅ `def add` → `public static ... Add` (static method, PascalCase)
2. ✅ `a: int` → `int a` (typed parameter)
3. ✅ `b: int = 1` → `int b = 1` (default value)
4. ✅ `-> int` → `int` return type
5. ✅ `return a * b` → `return a * b;` (body generation)

---

## Recommended Actions

Since all functionality is already implemented, the task should focus on **verification**:

### 1. Run Existing Tests
```bash
dotnet test --filter "FullyQualifiedName~RoslynEmitterDefinitionTests"
dotnet test --filter "FullyQualifiedName~NameManglerTests"
```

### 2. Add Integration Test (Optional)
Add a test for the exact example in the task description to `RoslynEmitterDefinitionTests.cs`:

```csharp
[Fact]
public void GenerateFunctionDeclaration_AddFunctionWithDefault_GeneratesCorrectSignature()
{
    var func = new FunctionDef
    {
        Name = "add",
        Parameters = new List<Parameter>
        {
            new Parameter { Name = "a", Type = new TypeAnnotation { Name = "int" } },
            new Parameter { Name = "b", Type = new TypeAnnotation { Name = "int" },
                           DefaultValue = new IntegerLiteral { Value = 1 } }
        },
        ReturnType = new TypeAnnotation { Name = "int" },
        Body = new List<Statement>
        {
            new ReturnStatement
            {
                Value = new BinaryOp
                {
                    Left = new Identifier { Name = "a" },
                    Operator = "*",
                    Right = new Identifier { Name = "b" }
                }
            }
        }
    };

    var module = new Module { Body = new List<Statement> { func } };
    var compilationUnit = _emitter.GenerateCompilationUnit(module);
    var code = compilationUnit.NormalizeWhitespace().ToFullString();

    Assert.Contains("public static int Add(int a, int b = 1)", code);
    Assert.Contains("return a * b;", code);
}
```

### 3. End-to-End Test (Optional)
Create a `.spy` file and verify compilation:

```python
# tests/e2e/function_generation.spy
def add(a: int, b: int = 1) -> int:
    return a * b

def hello_world() -> str:
    return "Hello, World!"

def greet(name: str = "World") -> str:
    return f"Hello, {name}!"
```

---

## Potential Risks

| Risk | Mitigation |
|------|------------|
| Edge case in name mangling | Covered by NameManglerTests |
| Complex default expressions | DefaultParameterValidator restricts to constants |
| Type inference without annotation | Falls back to `object` type |

---

## Questions for Clarification

1. **Keyword arguments**: Does the task include `*args` and `**kwargs` support, or just positional/default parameters?
   - Current: Only positional and default parameters supported

2. **Async functions**: Should `async def` be covered in this task?
   - Current: Not implemented (future task)

3. **Decorators beyond access modifiers**: Are custom decorators in scope?
   - Current: Only standard decorators (@staticmethod, @abstractmethod, access modifiers)

---

## Conclusion

**Task Status**: ✅ **VERIFICATION ONLY**

All four items in the task are already implemented:
1. ✅ Functions generate as `public static` methods
2. ✅ Name mangling: `hello_world` → `HelloWorld`
3. ✅ Parameters generated with types
4. ✅ Default parameter values in C# signature

**Recommended Action**: Run existing tests to verify, optionally add the specific example test for documentation purposes.
