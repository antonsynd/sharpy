# Phase 0.1.6: Classes - Detailed Task List

**Goal:** Basic class definitions with fields, constructors (`__init__`), and methods.

**Prerequisites:** Phases 0.1.0–0.1.5 must be complete (lexer, parser, code generation, variables, control flow, functions).

**Exit Criteria (from spec):**
- Classes compile to C# classes
- Fields declared and accessible
- `__init__` compiles to constructor
- Instance methods have correct `this` binding
- Static methods work without instance
- Name mangling: `snake_case` → `PascalCase`

---

## Pre-Implementation Checklist

Before starting any task, check what already exists:

```bash
# Check for existing class-related AST nodes
grep -rn "ClassDef\|class.*record" src/Sharpy.Compiler/Parser/Ast/

# Check for existing class parsing
grep -rn "ParseClass\|class\s*def" src/Sharpy.Compiler/Parser/Parser.cs

# Check for existing class code generation
grep -rn "GenerateClass\|ClassDeclaration" src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs

# Check for existing class tests
find src -name "*.cs" -exec grep -l "class.*Test\|ClassDef" {} \;
```

---

## Task 0.1.6.1: Audit/Verify Class Definition AST

**Type:** 🔍 Status Check  
**Priority:** Critical  
**Estimated Time:** 30 minutes

### Objective
Verify that `ClassDef` AST node exists and captures all required information.

### Files to Check
- `src/Sharpy.Compiler/Parser/Ast/Statement.cs`
- `src/Sharpy.Compiler/Parser/Ast/Node.cs`

### Actions

1. **Verify `ClassDef` record exists** with these properties:
   - [ ] `Name` (string) — class name
   - [ ] `BaseClasses` (List<TypeAnnotation> or similar) — inheritance (can be empty for now)
   - [ ] `Body` (List<Statement>) — class body statements
   - [ ] `Decorators` (List<Decorator>) — for `@abstract`, `@final`, etc.
   - [ ] Source location (LineStart, ColumnStart, LineEnd, ColumnEnd)

2. **Check if field declarations are captured:**
   - [ ] Fields should be `VariableDeclaration` nodes within the class body
   - [ ] Field type annotations must be preserved

3. **Verify `__init__` is parsed as a `FunctionDef`** within the class body

### Verification Commands
```bash
# Find ClassDef definition
grep -A 30 "record ClassDef" src/Sharpy.Compiler/Parser/Ast/Statement.cs

# Check what properties it has
grep -B 2 -A 10 "ClassDef" src/Sharpy.Compiler/Parser/Ast/Statement.cs | head -50
```

### Expected Outcome
```csharp
public record ClassDef : Statement
{
    public string Name { get; init; } = "";
    public List<TypeAnnotation> BaseClasses { get; init; } = new();
    public List<Statement> Body { get; init; } = new();
    public List<Decorator> Decorators { get; init; } = new();
    // ... source location properties
}
```

### If Missing or Incomplete
Create or modify `ClassDef` to include all required properties. Reference the grammar:
```ebnf
class_def ::= decorator* 'class' identifier type_params? class_bases? ':' class_body
```

---

## Task 0.1.6.2: Verify Class Definition Parsing

**Type:** 🔍 Status Check  
**Priority:** Critical  
**Estimated Time:** 1 hour

### Objective
Ensure the parser correctly handles class definitions with fields and methods.

### Files to Check
- `src/Sharpy.Compiler/Parser/Parser.cs`

### Actions

1. **Verify basic class parsing:**
   ```python
   class Point:
       x: int
       y: int
   ```
   - [ ] Parser recognizes `class` keyword
   - [ ] Class name is captured
   - [ ] Body is parsed as indented block
   - [ ] Field declarations (type-annotated variables) are captured

2. **Verify class with constructor:**
   ```python
   class Point:
       x: int
       y: int
       
       def __init__(self, x: int, y: int):
           self.x = x
           self.y = y
   ```
   - [ ] `__init__` parsed as `FunctionDef` within class body
   - [ ] `self` parameter recognized
   - [ ] `self.x` assignments parsed correctly

3. **Verify class with methods:**
   ```python
   class Point:
       x: int
       y: int
       
       def distance_from_origin(self) -> float:
           return (self.x ** 2 + self.y ** 2) ** 0.5
   ```
   - [ ] Instance methods parsed with `self` parameter
   - [ ] Return type annotation captured

4. **Verify static methods (no `self`):**
   ```python
   class Math:
       def square(x: int) -> int:
           return x * x
   ```
   - [ ] Method without `self` is parsed correctly
   - [ ] Compiler should later detect this as static

### Verification Commands
```bash
# Run existing parser tests for classes
dotnet test --filter "Class" src/Sharpy.Compiler.Tests/

# Check for ParseClass method
grep -n "ParseClass\|ParseClassDef" src/Sharpy.Compiler/Parser/Parser.cs
```

### Test Code
Create a test file `test_class_parsing.spy`:
```python
class Simple:
    x: int

class WithInit:
    x: int
    y: int
    
    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y

class WithMethods:
    value: int
    
    def __init__(self, v: int):
        self.value = v
    
    def get_value(self) -> int:
        return self.value
    
    def double() -> int:  # Static - no self
        return 42
```

### Expected Behavior
Parser should create `ClassDef` nodes with:
- Correct class names
- Field declarations in body
- Methods as `FunctionDef` nodes in body

---

## Task 0.1.6.3: Implement/Verify `self` Handling in Semantic Analysis

**Type:** ⚠️ Likely Implementation Needed  
**Priority:** Critical  
**Estimated Time:** 2-3 hours

### Objective
Ensure `self` is properly typed and `self.x` resolves to field access.

### Files to Modify/Check
- `src/Sharpy.Compiler/Semantic/SemanticAnalyzer.cs`
- `src/Sharpy.Compiler/Semantic/SymbolTable.cs`
- `src/Sharpy.Compiler/Semantic/TypeChecker.cs`

### Actions

1. **When entering a class scope:**
   - [ ] Register the class type in the symbol table
   - [ ] Track "current class" context for `self` resolution

2. **When analyzing methods with `self` parameter:**
   - [ ] Type `self` as the enclosing class type
   - [ ] Mark the method as an instance method (not static)

3. **When analyzing methods WITHOUT `self` parameter:**
   - [ ] Mark the method as static
   - [ ] Ensure no `self.x` access in static methods (semantic error)

4. **When analyzing `self.field` expressions:**
   - [ ] Resolve `field` in the class's member table
   - [ ] Return the field's type
   - [ ] Produce error if field doesn't exist

5. **When analyzing `self.method()` calls:**
   - [ ] Resolve method in class's member table
   - [ ] Verify argument types match

### Implementation Hints

In `SemanticAnalyzer`:
```csharp
private TypeSymbol? _currentClass;

private void AnalyzeClass(ClassDef classDef)
{
    // Register class type
    var classSymbol = new TypeSymbol(classDef.Name, ...);
    _symbolTable.DefineType(classDef.Name, classSymbol);
    
    var previousClass = _currentClass;
    _currentClass = classSymbol;
    
    try
    {
        // First pass: collect fields
        foreach (var member in classDef.Body)
        {
            if (member is VariableDeclaration field)
            {
                classSymbol.AddField(field.Name, ResolveType(field.Type));
            }
        }
        
        // Second pass: analyze methods
        foreach (var member in classDef.Body)
        {
            if (member is FunctionDef method)
            {
                AnalyzeMethod(method, classSymbol);
            }
        }
    }
    finally
    {
        _currentClass = previousClass;
    }
}

private void AnalyzeMethod(FunctionDef method, TypeSymbol classSymbol)
{
    bool hasself = method.Parameters.Count > 0 && 
                    method.Parameters[0].Name == "self";
    
    if (hasself)
    {
        // Instance method - type 'self' as the class type
        _symbolTable.EnterScope();
        _symbolTable.DefineVariable("self", classSymbol);
        // Analyze body...
        _symbolTable.ExitScope();
    }
    else
    {
        // Static method - no self binding
        // Analyze body without self...
    }
}
```

### Verification Tests
```python
# Should pass
class Good:
    x: int
    
    def __init__(self, x: int):
        self.x = x
    
    def get_x(self) -> int:
        return self.x

# Should error: accessing self.x in static method
class Bad:
    x: int
    
    def static_bad() -> int:
        return self.x  # ERROR: 'self' not defined in static method
```

### Verification Commands
```bash
# Run semantic tests
dotnet test --filter "Semantic" src/Sharpy.Compiler.Tests/

# Check for self-related code
grep -rn "self\|Self\|CurrentClass" src/Sharpy.Compiler/Semantic/
```

---

## Task 0.1.6.4: Implement/Verify `__init__` Code Generation

**Type:** ⚠️ Likely Implementation Needed  
**Priority:** Critical  
**Estimated Time:** 2-3 hours

### Objective
Generate C# constructors from `__init__` methods.

### Files to Modify/Check
- `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

### Actions

1. **Identify `__init__` methods** within class body:
   - [ ] Check if method name is `__init__`
   - [ ] Generate C# constructor instead of regular method

2. **Generate constructor signature:**
   - [ ] Skip the `self` parameter (implicit in C#)
   - [ ] Map other parameters with name mangling
   - [ ] No return type (constructors don't have one)

3. **Generate constructor body:**
   - [ ] Transform `self.x = y` to `this.X = y` (with name mangling)
   - [ ] Handle other initialization logic

### Code Generation Example

**Input (Sharpy):**
```python
class Point:
    x: int
    y: int
    
    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y
```

**Output (C#):**
```csharp
public class Point
{
    public int X;
    public int Y;
    
    public Point(int x, int y)
    {
        X = x;
        Y = y;
    }
}
```

### Implementation Hints

```csharp
private MemberDeclarationSyntax GenerateMethod(FunctionDef method, string className)
{
    if (method.Name == "__init__")
    {
        return GenerateConstructor(method, className);
    }
    else
    {
        return GenerateRegularMethod(method);
    }
}

private ConstructorDeclarationSyntax GenerateConstructor(FunctionDef init, string className)
{
    // Skip 'self' parameter
    var parameters = init.Parameters
        .Where(p => p.Name != "self")
        .Select(p => Parameter(Identifier(NameMangler.ToCamelCase(p.Name)))
            .WithType(_typeMapper.MapType(p.Type)))
        .ToArray();
    
    var body = init.Body.Select(stmt => GenerateStatement(stmt, isInClass: true));
    
    return ConstructorDeclaration(Identifier(NameMangler.ToPascalCase(className)))
        .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
        .WithParameterList(ParameterList(SeparatedList(parameters)))
        .WithBody(Block(body));
}
```

### Verification Tests

Create test that compiles and runs:
```python
class Counter:
    value: int
    
    def __init__(self, start: int = 0):
        self.value = start

c = Counter(10)
# c.value should be 10
```

### Verification Commands
```bash
# Run code generation tests
dotnet test --filter "CodeGen" src/Sharpy.Compiler.Tests/

# Check for __init__ handling
grep -rn "__init__\|Constructor" src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs
```

---

## Task 0.1.6.5: Implement/Verify Field Code Generation

**Type:** 🔍 Status Check / Implementation  
**Priority:** High  
**Estimated Time:** 1-2 hours

### Objective
Generate C# fields from class field declarations.

### Files to Check/Modify
- `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

### Actions

1. **Identify field declarations** in class body:
   - [ ] `VariableDeclaration` with type annotation at class level

2. **Generate C# fields:**
   - [ ] Apply name mangling: `snake_case` → `PascalCase`
   - [ ] Generate correct C# type
   - [ ] Default to `public` access modifier
   - [ ] Handle default values if present

### Code Generation Example

**Input:**
```python
class Example:
    count: int
    name: str = "default"
    active: bool = True
```

**Output:**
```csharp
public class Example
{
    public int Count;
    public string Name = "default";
    public bool Active = true;
}
```

### Verification Commands
```bash
# Check existing field generation
grep -rn "GenerateField\|FieldDeclaration" src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs

# Run field-related tests
dotnet test --filter "Field" src/Sharpy.Compiler.Tests/
```

---

## Task 0.1.6.6: Implement/Verify Instance Method Code Generation

**Type:** ⚠️ Likely Implementation Needed  
**Priority:** High  
**Estimated Time:** 2 hours

### Objective
Generate C# instance methods from methods with `self` parameter.

### Files to Modify
- `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

### Actions

1. **Detect instance methods:**
   - [ ] First parameter named `self`
   - [ ] NOT static

2. **Generate method signature:**
   - [ ] Skip `self` parameter
   - [ ] Apply name mangling to method name
   - [ ] Map parameter types
   - [ ] Map return type

3. **Transform `self.field` references:**
   - [ ] `self.x` → `this.X` (with name mangling)
   - [ ] `self.method()` → `this.Method()`

### Code Generation Example

**Input:**
```python
class Point:
    x: int
    y: int
    
    def distance_from_origin(self) -> float:
        return (self.x ** 2 + self.y ** 2) ** 0.5
    
    def move(self, dx: int, dy: int) -> None:
        self.x += dx
        self.y += dy
```

**Output:**
```csharp
public class Point
{
    public int X;
    public int Y;
    
    public double DistanceFromOrigin()
    {
        return Math.Pow(X * X + Y * Y, 0.5);
    }
    
    public void Move(int dx, int dy)
    {
        X += dx;
        Y += dy;
    }
}
```

### Implementation Hints

```csharp
private MethodDeclarationSyntax GenerateInstanceMethod(FunctionDef method)
{
    // Get all parameters except 'self'
    var parameters = method.Parameters
        .Where(p => p.Name != "self")
        .Select(MapParameter);
    
    var returnType = method.ReturnType != null
        ? _typeMapper.MapType(method.ReturnType)
        : PredefinedType(Token(SyntaxKind.VoidKeyword));
    
    // When generating body, transform 'self.X' to 'this.X'
    var body = method.Body.Select(stmt => 
        GenerateStatement(stmt, selfToThis: true));
    
    return MethodDeclaration(returnType, 
            Identifier(NameMangler.ToPascalCase(method.Name)))
        .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
        .WithParameterList(ParameterList(SeparatedList(parameters)))
        .WithBody(Block(body));
}
```

---

## Task 0.1.6.7: Implement/Verify Static Method Detection and Code Generation

**Type:** ⚠️ Implementation Needed  
**Priority:** High  
**Estimated Time:** 1-2 hours

### Objective
Detect methods without `self` and generate C# `static` methods.

### Key Rule from Spec
> Static methods have no `self` parameter. The compiler detects this and emits the C# `static` keyword automatically.

### Files to Modify
- `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`
- `src/Sharpy.Compiler/Semantic/SemanticAnalyzer.cs` (for validation)

### Actions

1. **Detection logic:**
   - [ ] Method has no `self` parameter
   - [ ] Method is inside a class body
   - [ ] Generate with `static` modifier

2. **Validation (semantic analysis):**
   - [ ] Error if static method references `self`
   - [ ] Error if static method accesses instance fields without qualifier

### Code Generation Example

**Input:**
```python
class Math:
    def square(x: int) -> int:
        return x * x
    
    def add(a: int, b: int) -> int:
        return a + b
```

**Output:**
```csharp
public class Math
{
    public static int Square(int x)
    {
        return x * x;
    }
    
    public static int Add(int a, int b)
    {
        return a + b;
    }
}
```

### Verification Tests

```python
# Should compile to static
class Utils:
    def helper(x: int) -> int:
        return x * 2

# Usage
result = Utils.helper(5)  # Called on class, not instance
```

---

## Task 0.1.6.8: Implement/Verify Name Mangling for Classes

**Type:** 🔍 Status Check  
**Priority:** High  
**Estimated Time:** 1 hour

### Objective
Ensure name mangling rules are correctly applied for class members.

### Rules from Spec

| Sharpy | C# |
|--------|-----|
| `snake_case` fields | `PascalCase` |
| `snake_case` methods | `PascalCase` |
| `snake_case` parameters | `camelCase` |
| `_private_field` | `_privateField` (private) |
| `__dunder__` methods | Special mapping (e.g., `__init__` → constructor) |

### Files to Check
- `src/Sharpy.Compiler/CodeGen/NameMangler.cs`
- `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

### Actions

1. **Verify field name mangling:**
   - [ ] `user_count` → `UserCount`
   - [ ] `_private_data` → `_privateData`

2. **Verify method name mangling:**
   - [ ] `get_user_name` → `GetUserName`
   - [ ] `calculate_sum` → `CalculateSum`

3. **Verify parameter name mangling:**
   - [ ] `user_id` → `userId`
   - [ ] `max_count` → `maxCount`

### Verification Commands
```bash
# Check name mangling implementation
grep -rn "ToPascalCase\|ToCamelCase\|Mangle" src/Sharpy.Compiler/CodeGen/NameMangler.cs

# Run name mangling tests
dotnet test --filter "NameMangl" src/Sharpy.Compiler.Tests/
```

---

## Task 0.1.6.9: Implement Class Instantiation Code Generation

**Type:** ⚠️ Likely Implementation Needed  
**Priority:** High  
**Estimated Time:** 1-2 hours

### Objective
Generate correct C# `new` expressions for class instantiation.

### Files to Modify
- `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

### Actions

1. **Detect class instantiation:**
   - [ ] Call expression where callee is a class name
   - [ ] Example: `Point(3, 4)` where `Point` is a class

2. **Generate `new` expression:**
   - [ ] `Point(3, 4)` → `new Point(3, 4)`
   - [ ] Apply name mangling to class name if needed

### Code Generation Example

**Input:**
```python
class Point:
    x: int
    y: int
    
    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y

p = Point(3, 4)
x_val = p.x
p.y = 10
```

**Output:**
```csharp
public class Point
{
    public int X;
    public int Y;
    
    public Point(int x, int y)
    {
        X = x;
        Y = y;
    }
}

// In Main or module:
var p = new Point(3, 4);
var xVal = p.X;
p.Y = 10;
```

### Implementation Hints

```csharp
private ExpressionSyntax GenerateCall(CallExpression call)
{
    // Check if this is a class instantiation
    if (call.Function is Identifier id && IsClassName(id.Name))
    {
        var className = NameMangler.ToPascalCase(id.Name);
        var args = call.Arguments.Select(GenerateExpression);
        
        return ObjectCreationExpression(IdentifierName(className))
            .WithArgumentList(ArgumentList(SeparatedList(
                args.Select(Argument))));
    }
    
    // Regular function call
    return InvocationExpression(...);
}
```

---

## Task 0.1.6.10: Create Phase 0.1.6 Integration Tests

**Type:** 🆕 New Implementation  
**Priority:** Critical  
**Estimated Time:** 2-3 hours

### Objective
Create comprehensive end-to-end tests for class functionality.

### File to Create
`src/Sharpy.Compiler.Tests/Integration/Phase016IntegrationTests.cs`

### Test Cases

```csharp
[Fact]
public void SimpleClass_CompilesAndRuns()
{
    var source = @"
class Point:
    x: int
    y: int

p = Point()
p.x = 10
p.y = 20
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

[Fact]
public void ClassWithConstructor_InitializesFields()
{
    var source = @"
class Counter:
    value: int
    
    def __init__(self, start: int):
        self.value = start

c = Counter(42)
result = c.value
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
    // Verify result == 42
}

[Fact]
public void ClassWithDefaultParameter_Works()
{
    var source = @"
class Counter:
    value: int
    
    def __init__(self, start: int = 0):
        self.value = start

c1 = Counter()
c2 = Counter(10)
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

[Fact]
public void InstanceMethod_AccessesFields()
{
    var source = @"
class Counter:
    value: int
    
    def __init__(self, start: int = 0):
        self.value = start
    
    def increment(self) -> None:
        self.value += 1
    
    def get(self) -> int:
        return self.value

c = Counter(10)
c.increment()
result = c.get()  # Should be 11
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

[Fact]
public void StaticMethod_NoSelfParameter()
{
    var source = @"
class Math:
    def square(x: int) -> int:
        return x * x

result = Math.square(5)  # Should be 25
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

[Fact]
public void MethodChaining_Works()
{
    var source = @"
class Builder:
    value: str
    
    def __init__(self):
        self.value = ''
    
    def add(self, s: str) -> Builder:
        self.value += s
        return self
    
    def build(self) -> str:
        return self.value

b = Builder()
result = b.add('Hello').add(' ').add('World').build()
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

[Fact]
public void NameMangling_SnakeToPascal()
{
    var source = @"
class UserProfile:
    user_name: str
    account_balance: int
    
    def __init__(self, user_name: str, account_balance: int):
        self.user_name = user_name
        self.account_balance = account_balance
    
    def get_user_name(self) -> str:
        return self.user_name

profile = UserProfile('Alice', 100)
name = profile.get_user_name()
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}
```

### Verification Commands
```bash
# Run Phase 0.1.6 tests
dotnet test --filter "Phase016" src/Sharpy.Compiler.Tests/

# Run all class-related tests
dotnet test --filter "Class" src/Sharpy.Compiler.Tests/
```

---

## Task 0.1.6.11: Document Phase 0.1.6 Exit Criteria Verification

**Type:** 📝 Documentation  
**Priority:** High  
**Estimated Time:** 30 minutes

### Objective
Verify and document that all exit criteria are met.

### Exit Criteria Checklist

| Criterion | Test | Status |
|-----------|------|--------|
| Classes compile to C# classes | `SimpleClass_CompilesAndRuns` | [ ] |
| Fields declared and accessible | `ClassWithConstructor_InitializesFields` | [ ] |
| `__init__` compiles to constructor | `ClassWithConstructor_InitializesFields` | [ ] |
| Instance methods have correct `this` binding | `InstanceMethod_AccessesFields` | [ ] |
| Static methods work without instance | `StaticMethod_NoSelfParameter` | [ ] |
| Name mangling: `snake_case` → `PascalCase` | `NameMangling_SnakeToPascal` | [ ] |

### Verification Process

1. Run all Phase 0.1.6 tests:
   ```bash
   dotnet test --filter "Phase016" --logger "console;verbosity=detailed"
   ```

2. Verify generated C# looks correct:
   ```bash
   # Compile a test file and inspect output
   dotnet run --project src/Sharpy.Compiler -- compile test.spy --emit-csharp
   ```

3. Run the generated assembly:
   ```bash
   dotnet run --project src/Sharpy.Compiler -- run test.spy
   ```

---

## Summary: Task Dependencies

```
0.1.6.1 (Audit AST) ─────────────┐
                                 │
0.1.6.2 (Verify Parsing) ────────┼──► 0.1.6.3 (Self Handling)
                                 │           │
                                 │           ▼
                                 │    0.1.6.4 (__init__ CodeGen)
                                 │    0.1.6.5 (Field CodeGen)
                                 │    0.1.6.6 (Instance Method CodeGen)
                                 │    0.1.6.7 (Static Method CodeGen)
                                 │    0.1.6.8 (Name Mangling)
                                 │    0.1.6.9 (Instantiation CodeGen)
                                 │           │
                                 ▼           ▼
                          0.1.6.10 (Integration Tests)
                                 │
                                 ▼
                          0.1.6.11 (Exit Criteria Doc)
```

## Estimated Total Time
- **Audit/Verification tasks:** 2-3 hours
- **Implementation tasks:** 8-12 hours
- **Testing and documentation:** 3-4 hours
- **Total:** 13-19 hours

## Notes for Agent/Engineer

1. **Check existing implementation first** — Many of these features may already be partially implemented.

2. **Incremental testing** — After each task, run relevant tests to catch issues early.

3. **Reference the spec** — The language specification in `docs/language_specification/` has authoritative definitions.

4. **Name mangling is critical** — Ensure `NameMangler` handles all edge cases correctly.

5. **Self vs this** — The transformation from `self.x` to `this.X` (with mangling) is a common source of bugs.
