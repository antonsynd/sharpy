# Phase 0.1.6: Classes - Comprehensive Task List (v2)

**Goal:** Basic class definitions with fields, constructors (`__init__`), instance methods, static methods, and constructor overloading/chaining.

**Prerequisites:** Phases 0.1.0–0.1.5 must be complete (lexer, parser, code generation, variables, control flow, functions).

**Exit Criteria:**
- Classes compile to C# classes
- Fields declared and accessible with proper name mangling
- `__init__` compiles to C# constructor(s)
- Constructor overloading works (multiple `__init__` methods)
- Constructor chaining (`self.__init__(...)` → `: this(...)`) works
- Instance methods have correct `this` binding
- Static methods (no `self` parameter) work without instance
- Name mangling: `snake_case` → `PascalCase` for public, `_snake_case` → `_camelCase` for private
- `__init__` cannot be called directly by users (except for chaining)
- `self` parameter cannot have type annotation
- All fields must be declared at class level

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

📁 **Files**: `src/Sharpy.Compiler/Parser/Ast/Statement.cs`, `src/Sharpy.Compiler/Parser/Ast/Node.cs`

### Objective
Verify that `ClassDef` AST node exists and captures all required information.

### Actions

1. **Verify `ClassDef` record exists** with these properties:
   - `Name` (string) — class name
   - `BaseClasses` (List<TypeAnnotation> or similar) — inheritance (can be empty for now)
   - `Body` (List<Statement>) — class body statements
   - `Decorators` (List<Decorator>) — for `@abstract`, `@final`, etc.
   - Source location (LineStart, ColumnStart, LineEnd, ColumnEnd)

2. **Check if field declarations are captured:**
   - Fields should be `VariableDeclaration` nodes within the class body
   - Field type annotations must be preserved

3. **Verify `__init__` is parsed as a `FunctionDef`** within the class body

### Expected AST Structure
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

📁 **Files**: `src/Sharpy.Compiler/Parser/Parser.cs`

### Objective
Ensure the parser correctly handles class definitions with fields, methods, and multiple `__init__` overloads.

### Actions

1. **Verify basic class parsing:**
   ```python
   class Point:
       x: int
       y: int
   ```
   - Parser recognizes `class` keyword
   - Class name is captured
   - Body is parsed as indented block
   - Field declarations (type-annotated variables) are captured

2. **Verify class with constructor:**
   ```python
   class Point:
       x: int
       y: int
       
       def __init__(self, x: int, y: int):
           self.x = x
           self.y = y
   ```
   - `__init__` parsed as `FunctionDef` within class body
   - `self` parameter recognized (no type annotation allowed!)
   - `self.x` assignments parsed correctly

3. **Verify multiple `__init__` methods (constructor overloading):**
   ```python
   class Point:
       x: float
       y: float

       def __init__(self):
           self.x = 0.0
           self.y = 0.0

       def __init__(self, x: float, y: float):
           self.x = x
           self.y = y

       def __init__(self, other: Point):
           self.x = other.x
           self.y = other.y
   ```
   - All three `__init__` methods are captured in class body
   - Each has distinct parameter lists

4. **Verify `__init__` return type handling:**
   ```python
   # Both forms are valid and equivalent:
   def __init__(self, name: str):           # Implicit None return
       self.name = name

   def __init__(self, name: str) -> None:   # Explicit None return
       self.name = name
   ```
   - Parser accepts `__init__` with or without `-> None` return type

5. **Verify static methods (no `self`):**
   ```python
   class Math:
       def square(x: int) -> int:
           return x * x
   ```
   - Method without `self` is parsed correctly

### Verification Commands
```bash
# Run existing parser tests for classes
dotnet test --filter "Class" src/Sharpy.Compiler.Tests/

# Check for ParseClass method
grep -n "ParseClass\|ParseClassDef" src/Sharpy.Compiler/Parser/Parser.cs
```

---

## Task 0.1.6.3: Implement/Verify `self` Handling in Semantic Analysis

**Type:** ⚠️ Likely Implementation Needed  
**Priority:** Critical  
**Estimated Time:** 3-4 hours

📁 **Files**: `src/Sharpy.Compiler/Semantic/SemanticAnalyzer.cs`, `src/Sharpy.Compiler/Semantic/SymbolTable.cs`, `src/Sharpy.Compiler/Semantic/TypeChecker.cs`

### Objective
Ensure `self` is properly typed, `self.x` resolves to field access, and semantic validation rules are enforced.

### Actions

1. **When entering a class scope:**
   - Register the class type in the symbol table
   - Track "current class" context for `self` resolution

2. **When analyzing methods with `self` parameter:**
   - Type `self` as the enclosing class type
   - Mark the method as an instance method (not static)

3. **⚠️ NEW: Validate `self` cannot have type annotation:**
   ```python
   class Person:
       def greet(self: Person) -> str:  # ERROR: self cannot be annotated
           return "Hello"
   ```
   - If `self` parameter has type annotation, produce error
   - Error message: "`self` parameter cannot have a type annotation"

4. **When analyzing methods WITHOUT `self` parameter:**
   - Mark the method as static
   - Ensure no `self.x` access in static methods (semantic error)

5. **When analyzing `self.field` expressions:**
   - Resolve `field` in the class's member table
   - Return the field's type
   - Produce error if field doesn't exist

6. **⚠️ NEW: Validate all fields are declared at class level:**
   ```python
   class Bad:
       def __init__(self):
           self.undeclared = 5  # ERROR: 'undeclared' not declared at class level

   class Good:
       undeclared: int  # Declaration at class level
       
       def __init__(self):
           self.undeclared = 5  # OK
   ```
   - Error message: "Field 'undeclared' must be declared at class level before use"

7. **⚠️ NEW: Validate `__init__` cannot be called directly:**
   ```python
   a = Foobar()  # OK: Allowed, implicitly invokes __init__
   a.__init__()  # ERROR: Not allowed in Sharpy (but allowed in Python)
   ```
   - Exception: Within `__init__` for constructor chaining (`self.__init__(...)` or `super().__init__(...)`)
   - Error message: "Constructor `__init__` cannot be called directly; use class instantiation syntax"

### Implementation Hints

```csharp
private TypeSymbol? _currentClass;

private void AnalyzeClass(ClassDef classDef)
{
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
    bool hasSelf = method.Parameters.Count > 0 && 
                   method.Parameters[0].Name == "self";
    
    // Validate self has no type annotation
    if (hasSelf && method.Parameters[0].TypeAnnotation != null)
    {
        ReportError(method.Parameters[0], "`self` parameter cannot have a type annotation");
    }
    
    if (hasSelf)
    {
        _symbolTable.EnterScope();
        _symbolTable.DefineVariable("self", classSymbol);
        // Analyze body...
        _symbolTable.ExitScope();
    }
    else
    {
        // Static method - no self binding
    }
}
```

---

## Task 0.1.6.4: Implement/Verify `__init__` Code Generation with Overloading and Chaining

**Type:** ⚠️ Likely Implementation Needed  
**Priority:** Critical  
**Estimated Time:** 3-4 hours

📁 **Files**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

### Objective
Generate C# constructors from `__init__` methods, supporting overloading and chaining.

### Actions

1. **Identify `__init__` methods** within class body:
   - Check if method name is `__init__`
   - Generate C# constructor instead of regular method

2. **⚠️ NEW: Support constructor overloading (multiple `__init__` methods):**
   ```python
   class Point:
       x: float
       y: float

       def __init__(self):
           self.x = 0.0
           self.y = 0.0

       def __init__(self, x: float, y: float):
           self.x = x
           self.y = y
   ```
   
   **Generated C#:**
   ```csharp
   public class Point
   {
       public double X;
       public double Y;
       
       public Point()
       {
           X = 0.0;
           Y = 0.0;
       }
       
       public Point(double x, double y)
       {
           X = x;
           Y = y;
       }
   }
   ```

3. **⚠️ NEW: Support constructor chaining (`self.__init__()`):**
   ```python
   class Point:
       x: float
       y: float

       def __init__(self):
           self.__init__(0.0, 0.0)  # Chains to two-parameter constructor

       def __init__(self, x: float, y: float):
           self.x = x
           self.y = y
   ```
   
   **Generated C#:**
   ```csharp
   public Point() : this(0.0, 0.0) { }

   public Point(double x, double y)
   {
       X = x;
       Y = y;
   }
   ```
   
   - Detect `self.__init__(...)` as **first statement** in `__init__`
   - Transform to C# constructor initializer (`: this(...)`)
   - NOT an in-body call; must be in constructor declaration syntax
   - Only ONE `self.__init__()` call allowed per constructor

4. **Generate constructor signature:**
   - Skip the `self` parameter (implicit in C#)
   - Map other parameters with name mangling (camelCase for params)
   - No return type (constructors don't have one)
   - `-> None` return type annotation is ignored

5. **Generate constructor body:**
   - Transform `self.x = y` to `this.X = y` (with name mangling)
   - Handle other initialization logic

### Implementation Hints

```csharp
private ConstructorDeclarationSyntax GenerateConstructor(FunctionDef init, string className)
{
    // Skip 'self' parameter
    var parameters = init.Parameters
        .Where(p => p.Name != "self")
        .Select(p => Parameter(Identifier(NameMangler.ToCamelCase(p.Name)))
            .WithType(_typeMapper.MapType(p.Type)))
        .ToArray();
    
    // Check for constructor chaining: self.__init__(...) as first statement
    ConstructorInitializerSyntax? initializer = null;
    var bodyStatements = init.Body.ToList();
    
    if (bodyStatements.Count > 0 && IsConstructorChainCall(bodyStatements[0]))
    {
        var chainCall = (CallExpression)((ExpressionStatement)bodyStatements[0]).Expression;
        var chainArgs = chainCall.Arguments.Select(GenerateExpression);
        
        initializer = ConstructorInitializer(
            SyntaxKind.ThisConstructorInitializer,
            ArgumentList(SeparatedList(chainArgs.Select(Argument))));
        
        bodyStatements = bodyStatements.Skip(1).ToList();  // Remove chain call from body
    }
    
    var body = bodyStatements.Select(stmt => GenerateStatement(stmt, isInClass: true));
    
    var constructor = ConstructorDeclaration(Identifier(NameMangler.ToPascalCase(className)))
        .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
        .WithParameterList(ParameterList(SeparatedList(parameters)))
        .WithBody(Block(body));
    
    if (initializer != null)
    {
        constructor = constructor.WithInitializer(initializer);
    }
    
    return constructor;
}

private bool IsConstructorChainCall(Statement stmt)
{
    // Check if statement is: self.__init__(...)
    if (stmt is not ExpressionStatement exprStmt) return false;
    if (exprStmt.Expression is not CallExpression call) return false;
    if (call.Function is not MemberAccess ma) return false;
    if (ma.Object is not Identifier id || id.Name != "self") return false;
    return ma.Member == "__init__";
}
```

---

## Task 0.1.6.5: Implement/Verify Field Code Generation

**Type:** 🔍 Status Check / Implementation  
**Priority:** High  
**Estimated Time:** 1-2 hours

📁 **Files**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

### Objective
Generate C# fields from class field declarations with correct name mangling.

### Actions

1. **Identify field declarations** in class body:
   - `VariableDeclaration` with type annotation at class level

2. **Generate C# fields with correct name mangling:**
   - Public fields: `snake_case` → `PascalCase` (e.g., `user_count` → `UserCount`)
   - Private fields (single underscore): `_snake_case` → `_camelCase` (e.g., `_user_count` → `_userCount`)
   - Default to `public` access modifier unless private prefix

3. **Handle default values if present:**

### Code Generation Example

**Input:**
```python
class Example:
    count: int
    user_name: str = "default"
    _private_data: bool = True
```

**Output:**
```csharp
public class Example
{
    public int Count;
    public string UserName = "default";
    private bool _privateData = true;
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

📁 **Files**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

### Objective
Generate C# instance methods from Sharpy methods that have `self` parameter.

### Actions

1. **Detect instance methods:**
   - Method has `self` as first parameter

2. **Generate C# method:**
   - Skip `self` parameter
   - Method name: `snake_case` → `PascalCase`
   - Parameter names: `snake_case` → `camelCase`
   - Transform `self.x` to `this.X`

3. **Handle exponentiation correctly:**
   - `x ** 2` → `Math.Pow(x, 2)` OR `x * x` for integer 2
   - `x ** 0.5` → `Math.Sqrt(x)`

### Code Generation Example

**Input:**
```python
class Point:
    x: float
    y: float
    
    def distance_from_origin(self) -> float:
        return (self.x ** 2 + self.y ** 2) ** 0.5
```

**Output:**
```csharp
public class Point
{
    public double X;
    public double Y;
    
    public double DistanceFromOrigin()
    {
        return Math.Sqrt(Math.Pow(X, 2) + Math.Pow(Y, 2));
        // Or more efficiently:
        // return Math.Sqrt(X * X + Y * Y);
    }
}
```

### Implementation Hints

```csharp
private MethodDeclarationSyntax GenerateInstanceMethod(FunctionDef method)
{
    // Skip 'self' parameter
    var parameters = method.Parameters
        .Where(p => p.Name != "self")
        .Select(p => Parameter(Identifier(NameMangler.ToCamelCase(p.Name)))
            .WithType(_typeMapper.MapType(p.Type)))
        .ToArray();
    
    var returnType = _typeMapper.MapType(method.ReturnType);
    var body = method.Body.Select(stmt => GenerateStatement(stmt, isInClass: true));
    
    return MethodDeclaration(returnType, NameMangler.ToPascalCase(method.Name))
        .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
        .WithParameterList(ParameterList(SeparatedList(parameters)))
        .WithBody(Block(body));
}
```

---

## Task 0.1.6.7: Implement/Verify Static Method Detection and Code Generation

**Type:** ⚠️ Likely Implementation Needed  
**Priority:** High  
**Estimated Time:** 1-2 hours

📁 **Files**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`, `src/Sharpy.Compiler/Semantic/SemanticAnalyzer.cs`

### Objective
Detect and generate static methods (methods without `self` parameter).

### Key Rules (Per Specification)

1. **Primary mechanism:** Absence of `self` parameter makes method static (Pythonic)
2. **`@static` decorator:** Valid but OPTIONAL/redundant for methods
3. **Validation:** If method has both `self` AND `@static`, produce error

### Example

```python
class Math:
    # Both are static methods:
    def square(x: int) -> int:        # Static (no self)
        return x * x
    
    @static
    def cube(x: int) -> int:          # Also static (explicit)
        return x * x * x
    
    @static
    def broken(self, x: int) -> int:  # ERROR: Cannot have both @static and self
        return x
```

### Generated C#

```csharp
public class Math
{
    public static int Square(int x)
    {
        return x * x;
    }
    
    public static int Cube(int x)
    {
        return x * x * x;
    }
}
```

### Actions

1. **Detect static methods:**
   - Method without `self` parameter → automatically static
   - Method with `@static` decorator → explicitly static (same effect)

2. **Validation:**
   - If method has `@static` decorator AND `self` parameter → Error
   - Error message: "Static method cannot have `self` parameter"

3. **Generate `static` modifier in C#:**

### Implementation Hints

```csharp
private MemberDeclarationSyntax GenerateMethod(FunctionDef method)
{
    bool hasSelf = method.Parameters.Count > 0 && 
                   method.Parameters[0].Name == "self";
    bool hasStaticDecorator = method.Decorators.Any(d => d.Name == "static");
    
    // Validation: cannot have both
    if (hasSelf && hasStaticDecorator)
    {
        throw new SemanticException("Static method cannot have `self` parameter");
    }
    
    bool isStatic = !hasSelf || hasStaticDecorator;
    
    var modifiers = new List<SyntaxToken> { Token(SyntaxKind.PublicKeyword) };
    if (isStatic)
    {
        modifiers.Add(Token(SyntaxKind.StaticKeyword));
    }
    
    // ... rest of method generation
}
```

---

## Task 0.1.6.8: Verify Name Mangling Implementation

**Type:** 🔍 Status Check  
**Priority:** High  
**Estimated Time:** 1 hour

📁 **Files**: `src/Sharpy.Compiler/CodeGen/NameMangler.cs`, `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

### Objective
Ensure name mangling correctly transforms Sharpy identifiers to C# conventions.

### Name Mangling Rules

| Sharpy | C# | Example |
|--------|-----|---------|
| Public `snake_case` fields | `PascalCase` | `user_count` → `UserCount` |
| Private `_snake_case` fields | `_camelCase` | `_user_count` → `_userCount` |
| Method names | `PascalCase` | `get_user_name` → `GetUserName` |
| Parameter names | `camelCase` | `user_name` → `userName` |
| Class names | `PascalCase` | `user_profile` → `UserProfile` |

### Verification Tests

```python
class UserProfile:
    user_name: str
    _private_data: int
    account_balance: int
    
    def __init__(self, user_name: str, account_balance: int):
        self.user_name = user_name
        self.account_balance = account_balance
        self._private_data = 0
    
    def get_user_name(self) -> str:
        return self.user_name
    
    def _internal_method(self) -> int:
        return self._private_data
```

**Expected C#:**
```csharp
public class UserProfile
{
    public string UserName;
    private int _privateData;
    public int AccountBalance;
    
    public UserProfile(string userName, int accountBalance)
    {
        UserName = userName;
        AccountBalance = accountBalance;
        _privateData = 0;
    }
    
    public string GetUserName()
    {
        return UserName;
    }
    
    private int InternalMethod()  // Or _internalMethod depending on convention
    {
        return _privateData;
    }
}
```

---

## Task 0.1.6.9: Implement/Verify Class Instantiation Code Generation

**Type:** ⚠️ Likely Implementation Needed  
**Priority:** High  
**Estimated Time:** 1-2 hours

📁 **Files**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

### Objective
Generate correct C# `new` expressions for class instantiation.

### Actions

1. **Detect class instantiation:**
   - Call expression where callee is a class name
   - Example: `Point(3, 4)` where `Point` is a class

2. **Generate `new` expression:**
   - `Point(3, 4)` → `new Point(3, 4)`
   - Apply name mangling to class name if needed

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
**Estimated Time:** 3-4 hours

📁 **Files**: `src/Sharpy.Compiler.Tests/Integration/Phase016IntegrationTests.cs`

### Objective
Create comprehensive end-to-end tests for class functionality including all amendments.

### Test Cases

```csharp
[Fact]
public void SimpleClass_CompilesAndRuns()
{
    // Note: Classes without explicit __init__ get implicit parameterless constructor
    // that initializes fields to default values (0 for int) - matches C# behavior
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
public void ConstructorOverloading_Works()
{
    var source = @"
class Point:
    x: float
    y: float

    def __init__(self):
        self.x = 0.0
        self.y = 0.0

    def __init__(self, x: float, y: float):
        self.x = x
        self.y = y

p1 = Point()
p2 = Point(3.0, 4.0)
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

[Fact]
public void ConstructorChaining_Works()
{
    var source = @"
class Point:
    x: float
    y: float

    def __init__(self):
        self.__init__(0.0, 0.0)

    def __init__(self, x: float, y: float):
        self.x = x
        self.y = y

p = Point()
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
    // Generated C# should use `: this(0.0, 0.0)` syntax
}

[Fact]
public void InitDirectCall_ProducesError()
{
    var source = @"
class Foo:
    x: int
    
    def __init__(self, x: int):
        self.x = x

f = Foo(5)
f.__init__(10)
";
    var result = Compile(source);
    Assert.False(result.Success);
    Assert.Contains("cannot be called directly", result.Error);
}

[Fact]
public void SelfWithTypeAnnotation_ProducesError()
{
    var source = @"
class Person:
    def greet(self: Person) -> str:
        return 'Hello'
";
    var result = Compile(source);
    Assert.False(result.Success);
    Assert.Contains("cannot have a type annotation", result.Error);
}

[Fact]
public void UndeclaredField_ProducesError()
{
    var source = @"
class Bad:
    def __init__(self):
        self.undeclared = 5
";
    var result = Compile(source);
    Assert.False(result.Success);
    Assert.Contains("must be declared at class level", result.Error);
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
result = c.get()
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

result = Math.square(5)
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

[Fact]
public void StaticMethodWithSelf_ProducesError()
{
    var source = @"
class Math:
    @static
    def bad(self, x: int) -> int:
        return x
";
    var result = Compile(source);
    Assert.False(result.Success);
    Assert.Contains("cannot have `self`", result.Error);
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

[Fact]
public void PrivateField_NameMangling()
{
    var source = @"
class Secret:
    _hidden_value: int
    
    def __init__(self, v: int):
        self._hidden_value = v
    
    def get(self) -> int:
        return self._hidden_value

s = Secret(42)
result = s.get()
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
    // Generated C# should have `private int _hiddenValue;`
}

[Fact]
public void InitReturnTypeNone_Accepted()
{
    var source = @"
class Foo:
    x: int
    
    def __init__(self, x: int) -> None:
        self.x = x

f = Foo(5)
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}
```

---

## Task 0.1.6.11: Document Phase 0.1.6 Exit Criteria Verification

**Type:** 📝 Documentation  
**Priority:** High  
**Estimated Time:** 30 minutes

📁 **Files**: `docs/implementation/phase_0_1_6_complete.md`

### Objective
Verify and document that all exit criteria are met.

### Exit Criteria Checklist

| Criterion | Test | Status |
|-----------|------|--------|
| Classes compile to C# classes | `SimpleClass_CompilesAndRuns` | [ ] |
| Fields declared and accessible | `ClassWithConstructor_InitializesFields` | [ ] |
| `__init__` compiles to constructor | `ClassWithConstructor_InitializesFields` | [ ] |
| Constructor overloading works | `ConstructorOverloading_Works` | [ ] |
| Constructor chaining works | `ConstructorChaining_Works` | [ ] |
| Instance methods have correct `this` binding | `InstanceMethod_AccessesFields` | [ ] |
| Static methods work without instance | `StaticMethod_NoSelfParameter` | [ ] |
| Name mangling works correctly | `NameMangling_SnakeToPascal` | [ ] |
| `__init__` direct call blocked | `InitDirectCall_ProducesError` | [ ] |
| `self` type annotation blocked | `SelfWithTypeAnnotation_ProducesError` | [ ] |
| Undeclared field access blocked | `UndeclaredField_ProducesError` | [ ] |

### Verification Process

1. Run all Phase 0.1.6 tests:
   ```bash
   dotnet test --filter "Phase016" --logger "console;verbosity=detailed"
   ```

2. Verify generated C# looks correct:
   ```bash
   dotnet run --project src/Sharpy.Compiler -- compile test.spy --emit-csharp
   ```

---

## Summary: Task Dependencies

```
0.1.6.1 (Audit AST) ─────────────┐
                                 │
0.1.6.2 (Verify Parsing) ────────┼──► 0.1.6.3 (Self Handling + Validations)
                                 │           │
                                 │           ▼
                                 │    0.1.6.4 (__init__ + Overloading + Chaining)
                                 │    0.1.6.5 (Field CodeGen)
                                 │    0.1.6.6 (Instance Method CodeGen)
                                 │    0.1.6.7 (Static Method Detection)
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
- **Implementation tasks:** 14-18 hours
- **Testing and documentation:** 4-5 hours
- **Total:** 20-26 hours

## Notes for Agent/Engineer

1. **Check existing implementation first** — Many of these features may already be partially implemented.

2. **Incremental testing** — After each task, run relevant tests to catch issues early.

3. **Reference the spec** — The language specification in `docs/language_specification/` has authoritative definitions.

4. **Name mangling is critical** — Ensure `NameMangler` handles all edge cases correctly.

5. **Self vs this** — The transformation from `self.x` to `this.X` (with mangling) is a common source of bugs.

6. **Constructor chaining** — The `self.__init__()` → `: this()` transformation requires special handling as first statement.

7. **⚠️ NEW validations are important** — Direct `__init__` calls, `self` type annotations, and undeclared fields must produce errors.
