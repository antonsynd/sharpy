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
# Phase 0.1.7: Inheritance & Interfaces - Comprehensive Task List (v2)

**Goal:** Class inheritance, abstract classes, interfaces, and decorator modifiers (`@virtual`, `@override`, `@abstract`, `@final`).

**Prerequisites:** Phase 0.1.6 (Classes) must be complete.

**Exit Criteria:**
- Single inheritance works (`class Dog(Animal):`)
- `super()` calls parent constructor/methods (only in allowed contexts)
- `super()` errors in regular methods and free functions
- `super()` cannot be chained (`super().super()` is ERROR)
- Abstract classes cannot be instantiated
- Abstract methods must be overridden
- Interfaces define contracts (methods with `...` body)
- Interface implementations don't require `@override`
- Multiple interfaces supported
- Decorator modifiers apply correctly (`@virtual`, `@override`, `@abstract`, `@final`)
- `@final` on methods requires the method to override a virtual/abstract base method
- `@override` requires method to exist in base class and be `@virtual` or `@abstract`
- `super().__init__()` must be first statement unconditionally (not inside control flow)
- NO `@public` decorator (public is default)

---

## Pre-Implementation Checklist

Before starting any task, check what already exists:

```bash
# Check for inheritance-related parsing
grep -rn "BaseClass\|inherit\|super" src/Sharpy.Compiler/Parser/

# Check for decorator handling
grep -rn "Decorator\|virtual\|override\|abstract" src/Sharpy.Compiler/

# Check for interface parsing
grep -rn "Interface\|interface" src/Sharpy.Compiler/Parser/

# Check for existing tests
find src -name "*.cs" -exec grep -l "Inherit\|Interface\|super\|override" {} \;
```

---

## Task 0.1.7.1: Verify Inheritance AST and Parsing

**Type:** 🔍 Status Check  
**Priority:** Critical  
**Estimated Time:** 1 hour

📁 **Files**: `src/Sharpy.Compiler/Parser/Ast/Statement.cs`, `src/Sharpy.Compiler/Parser/Parser.cs`

### Objective
Verify that class inheritance (`class Dog(Animal):`) is parsed correctly.

### Actions

1. **Verify `ClassDef.BaseClasses` property exists:**
   - Should be `List<TypeAnnotation>` or similar
   - Captures base class for single inheritance
   - Can hold multiple entries (for interfaces)

2. **Test parsing of inherited class:**
   ```python
   class Animal:
       name: str
       
       def __init__(self, name: str):
           self.name = name
   
   class Dog(Animal):
       breed: str
       
       def __init__(self, name: str, breed: str):
           super().__init__(name)
           self.breed = breed
   ```
   - `Dog`'s BaseClasses contains `Animal`
   - `super()` parsed as special expression

3. **Verify multiple base parsing (for interfaces):**
   ```python
   class MyClass(BaseClass, IInterface1, IInterface2):
       pass
   ```
   - All three entries captured in BaseClasses

### Expected AST Structure
```csharp
// For: class Dog(Animal):
ClassDef {
    Name = "Dog",
    BaseClasses = [ TypeAnnotation { Name = "Animal" } ],
    Body = [...],
    Decorators = []
}
```

---

## Task 0.1.7.2: Implement/Verify `super()` Parsing and AST

**Type:** ⚠️ Likely Implementation Needed  
**Priority:** Critical  
**Estimated Time:** 2 hours

📁 **Files**: `src/Sharpy.Compiler/Parser/Ast/Expression.cs`, `src/Sharpy.Compiler/Parser/Parser.cs`

### Objective
Parse `super()` calls and represent them in the AST.

### Spec Requirements

`super()` can ONLY be called in:
- `__init__` to call `super().__init__(...)`
- Dunder methods to call `super().__any_dunder__(...)`
- `@override` methods to call `super().method()`

Calling `super()` in regular methods or free functions is a **compile error**.

### Valid Forms (Grammar)
```ebnf
super_call ::= 'super' '(' ')' '.' identifier '(' [ arguments ] ')'
```

### Actions

1. **Create `SuperCall` AST node:**
   ```csharp
   public record SuperCall : Expression
   {
       public string MethodName { get; init; } = "";  // e.g., "__init__" or "speak"
       public List<Expression> Arguments { get; init; } = new();
   }
   ```

2. **Parse `super()` expressions:**
   - Recognize `super` keyword
   - Expect `()` immediately after
   - Expect `.` method access
   - Parse method name and arguments

3. **Test valid patterns:**
   ```python
   super().__init__(name)          # Constructor call
   super().speak()                 # Override method call
   super().__eq__(other)           # Dunder method call
   ```

4. **Test invalid patterns (should error at parse or semantic):**
   ```python
   super()                         # ERROR: standalone super()
   super().field                   # ERROR: field access
   x = super()                     # ERROR: assignment
   super().method                  # ERROR: method reference without call
   ```

### Implementation Hints

```csharp
private Expression ParseSuperCall()
{
    Expect(TokenType.Super);
    Expect(TokenType.LeftParen);
    Expect(TokenType.RightParen);
    Expect(TokenType.Dot);
    
    var methodName = ExpectIdentifier();
    
    Expect(TokenType.LeftParen);
    var args = ParseArgumentList();
    Expect(TokenType.RightParen);
    
    return new SuperCall
    {
        MethodName = methodName,
        Arguments = args
    };
}
```

---

## Task 0.1.7.3: Implement `super()` Semantic Validation

**Type:** 🆕 New Implementation  
**Priority:** Critical  
**Estimated Time:** 3-4 hours

📁 **Files**: `src/Sharpy.Compiler/Semantic/SemanticAnalyzer.cs`

### Objective
Validate that `super()` is only used in allowed contexts with correct rules.

### Updated Validation Table (Per Amendments)

| Context | Allowed `super()` Calls |
|---------|------------------------|
| `__init__` | `super().__init__(...)` only |
| `@override` method | `super().same_method_name(...)` |
| Dunder method | `super().__any_dunder__(...)` (same or cross-dunder) |
| Regular method | ❌ ERROR |
| Free function | ❌ ERROR |

### Actions

1. **Track method context during analysis:**
   - Is current method `__init__`?
   - Is current method decorated with `@override`?
   - Is current method a dunder method?
   - Is this a free function (outside class)?

2. **⚠️ NEW: Validate `super()` cannot be chained:**
   ```python
   class C(B):
       @override
       def process(self) -> str:
           b_result = super().process()         # OK: immediate parent (B)
           a_result = super().super().process() # ERROR: Cannot chain super()
   ```
   - Error message: "`super()` cannot be chained to access ancestor classes further up the inheritance hierarchy"

3. **⚠️ NEW: Validate `super().__init__()` must be first statement unconditionally:**
   ```python
   def __init__(self, name: str, breed: str):
       if condition:
           super().__init__(name)  # ERROR: Must be first statement unconditionally
       # ...
   ```
   - Error message: "`super().__init__()` must be the first statement in the constructor, not inside control flow"

4. **Validate dunder cross-super calls are allowed:**
   ```python
   class Child(Parent):
       @override
       def __le__(self, other: Child) -> bool:
           # Both of these are valid:
           return self.__lt__(other) or self.__eq__(other)   # OK: cross-dunder on self
           return super().__lt__(other) or super().__eq__(other)  # OK: cross-dunder via super
   ```

### Implementation Hints

```csharp
private MethodContext _currentMethodContext;

private enum MethodContextType { Init, Override, Dunder, Regular, FreeFunction }

private struct MethodContext
{
    public MethodContextType Type;
    public string MethodName;
    public bool InControlFlow;  // Track if we're inside if/try/etc.
}

private void AnalyzeSuperCall(SuperCall superCall)
{
    // Check chaining - super() result cannot call super() again
    // (This is checked at parse time or by AST structure)
    
    switch (_currentMethodContext.Type)
    {
        case MethodContextType.Init:
            if (superCall.MethodName != "__init__")
                ReportError("super() in __init__ can only call super().__init__()");
            if (_currentMethodContext.InControlFlow)
                ReportError("super().__init__() must be first statement, not inside control flow");
            break;
            
        case MethodContextType.Override:
            // Must call same method name OR if it's a dunder, can call other dunders
            if (superCall.MethodName != _currentMethodContext.MethodName)
            {
                if (!IsDunderMethod(_currentMethodContext.MethodName) || !IsDunderMethod(superCall.MethodName))
                    ReportError($"super() in @override method must call super().{_currentMethodContext.MethodName}()");
            }
            break;
            
        case MethodContextType.Dunder:
            // Can call any dunder method via super()
            if (!IsDunderMethod(superCall.MethodName))
                ReportError("super() in dunder method must call another dunder method");
            break;
            
        case MethodContextType.Regular:
            ReportError("super() cannot be used in regular methods (only in __init__, @override, or dunder methods)");
            break;
            
        case MethodContextType.FreeFunction:
            ReportError("super() cannot be used outside of a class");
            break;
    }
}

private bool IsDunderMethod(string name) => name.StartsWith("__") && name.EndsWith("__");
```

---

## Task 0.1.7.4: Implement Inheritance Code Generation

**Type:** 🆕 New Implementation  
**Priority:** Critical  
**Estimated Time:** 2-3 hours

📁 **Files**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

### Objective
Generate C# inheritance syntax from Sharpy class definitions.

### Actions

1. **Generate base class in class declaration:**
   ```python
   class Dog(Animal):
       pass
   ```
   
   **Generated C#:**
   ```csharp
   public class Dog : Animal
   {
   }
   ```

2. **Generate `super().__init__()` as constructor initializer (`: base(...)`):**
   ```python
   def __init__(self, name: str, breed: str):
       super().__init__(name)
       self.breed = breed
   ```
   
   **Generated C#:**
   ```csharp
   public Dog(string name, string breed) : base(name)
   {
       Breed = breed;
   }
   ```
   
   - `super().__init__(...)` as **first statement** becomes `: base(...)`
   - NOT an in-body call; must be in constructor declaration syntax

3. **Generate `super().method()` as `base.Method()` calls:**
   ```python
   @override
   def speak(self) -> str:
       return super().speak() + "!"
   ```
   
   **Generated C#:**
   ```csharp
   public override string Speak()
   {
       return base.Speak() + "!";
   }
   ```

### Implementation Hints

```csharp
private ClassDeclarationSyntax GenerateClass(ClassDef classDef)
{
    var classDecl = ClassDeclaration(NameMangler.ToPascalCase(classDef.Name))
        .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)));
    
    // Add base types (first is base class, rest are interfaces)
    if (classDef.BaseClasses.Any())
    {
        var baseTypes = classDef.BaseClasses
            .Select(b => SimpleBaseType(IdentifierName(NameMangler.ToPascalCase(b.Name))));
        
        classDecl = classDecl.WithBaseList(BaseList(SeparatedList<BaseTypeSyntax>(baseTypes)));
    }
    
    // Generate members...
    return classDecl;
}

private ExpressionSyntax GenerateSuperCall(SuperCall superCall)
{
    // NOTE: For super().__init__(), this is handled specially in constructor generation
    // For other super() calls, generate: base.MethodName(args)
    
    var methodName = NameMangler.ToPascalCase(superCall.MethodName);
    var args = superCall.Arguments.Select(GenerateExpression);
    
    return InvocationExpression(
        MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            BaseExpression(),
            IdentifierName(methodName)))
        .WithArgumentList(ArgumentList(SeparatedList(args.Select(Argument))));
}
```

---

## Task 0.1.7.5: Implement Decorator Parsing

**Type:** 🔍 Status Check / Implementation  
**Priority:** High  
**Estimated Time:** 1-2 hours

📁 **Files**: `src/Sharpy.Compiler/Parser/Parser.cs`, `src/Sharpy.Compiler/Parser/Ast/Decorator.cs`

### Objective
Parse decorator syntax and store in AST.

### Supported Decorators

| Decorator | C# Equivalent | Applies To |
|-----------|--------------|------------|
| `@virtual` | `virtual` | Methods |
| `@override` | `override` | Methods overriding base |
| `@abstract` | `abstract` | Classes, Methods |
| `@final` | `sealed` (class) or `sealed override` (method) | Classes, Methods |
| `@private` | `private` | Members |
| `@protected` | `protected` | Members |
| `@internal` | `internal` | Members |
| `@static` | `static` | Methods (optional, absence of `self` is primary mechanism) |

### ⚠️ NEW: NO `@public` Decorator

- Public is the default when no access modifier is present
- `@public` is NOT a recognized decorator
- If a user writes `@public`, treat it as unknown decorator (warning or error)

### Actions

1. **Parse decorator syntax:**
   ```python
   @decorator_name
   @decorator_name(args)
   ```

2. **Store decorators in AST node:**
   ```csharp
   public record Decorator
   {
       public string Name { get; init; } = "";
       public List<Expression>? Arguments { get; init; }
   }
   ```

3. **Validate decorator is recognized:**
   - Known decorators: `virtual`, `override`, `abstract`, `final`, `private`, `protected`, `internal`, `static`
   - Unknown decorators: Warning or error

---

## Task 0.1.7.6: Implement Decorator Code Generation

**Type:** 🆕 New Implementation  
**Priority:** High  
**Estimated Time:** 2-3 hours

📁 **Files**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

### Objective
Generate C# modifiers from Sharpy decorators.

### Actions

1. **Map decorators to C# modifiers:**

```csharp
private SyntaxTokenList GenerateModifiers(List<Decorator> decorators, bool isClass)
{
    var tokens = new List<SyntaxToken>();
    bool hasAccessModifier = false;
    
    foreach (var decorator in decorators)
    {
        switch (decorator.Name)
        {
            case "private":
                tokens.Add(Token(SyntaxKind.PrivateKeyword));
                hasAccessModifier = true;
                break;
            case "protected":
                tokens.Add(Token(SyntaxKind.ProtectedKeyword));
                hasAccessModifier = true;
                break;
            case "internal":
                tokens.Add(Token(SyntaxKind.InternalKeyword));
                hasAccessModifier = true;
                break;
            // NO "public" case - it's the default
                
            case "abstract":
                tokens.Add(Token(SyntaxKind.AbstractKeyword));
                break;
            case "virtual":
                tokens.Add(Token(SyntaxKind.VirtualKeyword));
                break;
            case "override":
                tokens.Add(Token(SyntaxKind.OverrideKeyword));
                break;
            case "final":
                if (isClass)
                    tokens.Add(Token(SyntaxKind.SealedKeyword));
                else
                    tokens.AddRange(new[] {
                        Token(SyntaxKind.SealedKeyword),
                        Token(SyntaxKind.OverrideKeyword)
                    });
                break;
            case "static":
                tokens.Add(Token(SyntaxKind.StaticKeyword));
                break;
        }
    }
    
    // Add public if no access modifier specified
    if (!hasAccessModifier)
    {
        tokens.Insert(0, Token(SyntaxKind.PublicKeyword));
    }
    
    return TokenList(tokens);
}
```

2. **⚠️ NEW: Validate `@final` on method requires override context:**
   - `@final` on a method → `sealed override` (method must be overriding something)
   - If `@final` on method but method doesn't override anything → Error
   - Error message: "`@final` on a method requires the method to override a virtual or abstract base method"

3. **⚠️ NEW: Validate `@override` requires virtual/abstract in base:**
   - Method with `@override` must have a corresponding `@virtual` or `@abstract` method in base class
   - Overriding a non-virtual method → Error
   - Error message: "Method 'not_virtual' cannot be overridden because it is not marked @virtual or @abstract in the base class"

### Validation Examples

```python
# VALID: @final on override method
class Parent:
    @virtual
    def speak(self) -> str:
        return "Parent"

class Child(Parent):
    @final
    @override
    def speak(self) -> str:  # OK: overrides virtual method
        return "Child"

# INVALID: @final on non-override method
class Bad:
    @final
    def speak(self) -> str:  # ERROR: @final requires override context
        return "Bad"

# INVALID: @override without virtual base
class Parent:
    def not_virtual(self) -> str:  # No @virtual
        return "Parent"

class Child(Parent):
    @override
    def not_virtual(self) -> str:  # ERROR: Cannot override non-virtual method
        return "Child"
```

---

## Task 0.1.7.7: Implement Abstract Class/Method Validation and Code Generation

**Type:** 🆕 New Implementation  
**Priority:** High  
**Estimated Time:** 2 hours

📁 **Files**: `src/Sharpy.Compiler/Semantic/SemanticAnalyzer.cs`, `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

### Objective
Handle abstract classes and methods.

### Actions

1. **Parse `@abstract` decorator:**
   ```python
   @abstract
   class Shape:
       @abstract
       def area(self) -> float:
           ...
   ```

2. **Validate abstract rules:**
   - Abstract methods must have `...` body (no implementation)
   - Class with abstract methods must be marked `@abstract`
   - Abstract classes cannot be instantiated

3. **Generate abstract C#:**
   ```csharp
   public abstract class Shape
   {
       public abstract double Area();
   }
   ```

4. **Validate override requirements:**
   - All abstract methods must be overridden in non-abstract derived classes
   - Overriding method must have matching signature

### Error Messages

- "Class 'Shape' contains abstract method 'area' but is not marked @abstract"
- "Cannot create an instance of abstract class 'Shape'"
- "Non-abstract class 'Circle' must implement abstract method 'area' from 'Shape'"

---

## Task 0.1.7.8: Implement Interface Definition Parsing

**Type:** 🆕 New Implementation  
**Priority:** High  
**Estimated Time:** 2 hours

📁 **Files**: `src/Sharpy.Compiler/Parser/Parser.cs`, `src/Sharpy.Compiler/Parser/Ast/Statement.cs`

### Objective
Parse interface definitions.

### Syntax
```python
interface IDrawable:
    def draw(self) -> None:
        ...
    
    def get_bounds(self) -> Rect:
        ...
```

### ⚠️ Key Rules (Per Amendments)

1. **Interface methods MUST have `...` body (no implementation)**
2. **Interface methods do NOT require/allow `@abstract` decorator** (they're abstract by definition)
3. **Interface methods with actual body → Error**

### Actions

1. **Create `InterfaceDef` AST node:**
   ```csharp
   public record InterfaceDef : Statement
   {
       public string Name { get; init; } = "";
       public List<FunctionDef> Methods { get; init; } = new();
       public List<PropertyDef> Properties { get; init; } = new();
   }
   ```

2. **Parse interface syntax:**
   - Recognize `interface` keyword
   - Parse methods (must have `...` body)
   - Parse properties (abstract, see Amendment 7)

3. **Validate interface method bodies:**
   ```python
   interface IDrawable:
       def draw(self) -> None:
           ...  # OK: abstract (implicit)
       
       def invalid(self) -> None:
           print("test")  # ERROR: Interface methods cannot have implementation
   ```

### Scope Decisions

- **Phase 0.1.7:** Abstract interface methods only (`...` body)
- **Deferred:** Default interface implementations (C# 8+ feature)
- **Deferred:** Explicit interface implementation (`IFace.method_name`)

### Interface Property Support

```python
interface IIdentifiable:
    property get id: int       # Abstract - implementer must provide getter
```

Include abstract interface properties in Phase 0.1.7. Defer properties with default values to later phase.

---

## Task 0.1.7.9: Implement Interface Code Generation

**Type:** 🆕 New Implementation  
**Priority:** High  
**Estimated Time:** 1-2 hours

📁 **Files**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

### Objective
Generate C# interfaces from Sharpy interface definitions.

### Actions

1. **Generate interface declaration:**
   ```python
   interface IDrawable:
       def draw(self) -> None:
           ...
   ```
   
   **Generated C#:**
   ```csharp
   public interface IDrawable
   {
       void Draw();
   }
   ```

2. **Handle interface properties:**
   ```python
   interface IIdentifiable:
       property get id: int
   ```
   
   **Generated C#:**
   ```csharp
   public interface IIdentifiable
   {
       int Id { get; }
   }
   ```

### Implementation Hints

```csharp
private InterfaceDeclarationSyntax GenerateInterface(InterfaceDef interfaceDef)
{
    var members = new List<MemberDeclarationSyntax>();
    
    foreach (var method in interfaceDef.Methods)
    {
        // Skip 'self' parameter
        var parameters = method.Parameters
            .Where(p => p.Name != "self")
            .Select(p => Parameter(Identifier(NameMangler.ToCamelCase(p.Name)))
                .WithType(_typeMapper.MapType(p.Type)));
        
        var returnType = _typeMapper.MapType(method.ReturnType);
        
        members.Add(MethodDeclaration(returnType, NameMangler.ToPascalCase(method.Name))
            .WithParameterList(ParameterList(SeparatedList(parameters)))
            .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));
    }
    
    return InterfaceDeclaration(interfaceDef.Name)
        .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
        .WithMembers(List(members));
}
```

---

## Task 0.1.7.10: Implement Interface Implementation Code Generation

**Type:** 🆕 New Implementation  
**Priority:** High  
**Estimated Time:** 1-2 hours

📁 **Files**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

### Objective
Generate C# code for classes implementing interfaces.

### Syntax
```python
class Circle(IDrawable):
    def draw(self) -> None:
        print("Drawing circle")
```

**Generated C#:**
```csharp
public class Circle : IDrawable
{
    public void Draw()
    {
        Console.WriteLine("Drawing circle");
    }
}
```

### Actions

1. **Detect interface in base list:**
   - Interface names start with `I` by convention
   - OR check symbol table for interface type

2. **Generate interface implementation:**
   - Class implements all interface methods
   - Methods don't need `@override` decorator for interface implementation

3. **Validate all interface methods are implemented:**
   - If class doesn't implement an interface method → Error
   - Error message: "Class 'Circle' does not implement interface method 'IDrawable.draw'"

### Multiple Interfaces

```python
class Shape(IDrawable, ISerializable):
    def draw(self) -> None:
        ...
    
    def serialize(self) -> str:
        ...
```

**Generated C#:**
```csharp
public class Shape : IDrawable, ISerializable
{
    public void Draw() { ... }
    public string Serialize() { ... }
}
```

---

## Task 0.1.7.11: Implement Dunder Method Override Rules

**Type:** ⚠️ Clarification / Implementation  
**Priority:** Medium  
**Estimated Time:** 1 hour

📁 **Files**: `src/Sharpy.Compiler/Semantic/SemanticAnalyzer.cs`, `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

### Objective
Handle dunder method override semantics correctly.

### Key Rules

1. **Dunder methods overriding Object methods need `@override`:**
   - `__str__` → overrides `Object.ToString()`
   - `__eq__` → overrides `Object.Equals()`
   - `__hash__` → overrides `Object.GetHashCode()`

2. **`super()` in dunder methods can call ANY dunder:**
   ```python
   class Child(Parent):
       @override
       def __le__(self, other: Child) -> bool:
           return super().__lt__(other) or super().__eq__(other)  # OK: cross-dunder
   ```

### Example

```python
class Calculator:
    @override
    def __str__(self) -> str:
        return "Calculator"

class ScientificCalculator(Calculator):
    @final
    @override
    def __str__(self) -> str:
        return "ScientificCalculator"
```

**Generated C#:**
```csharp
public class Calculator
{
    public override string ToString()
    {
        return "Calculator";
    }
}

public class ScientificCalculator : Calculator
{
    public sealed override string ToString()
    {
        return "ScientificCalculator";
    }
}
```

---

## Task 0.1.7.12: Create Phase 0.1.7 Integration Tests

**Type:** 🆕 New Implementation  
**Priority:** Critical  
**Estimated Time:** 3-4 hours

📁 **Files**: `src/Sharpy.Compiler.Tests/Integration/Phase017IntegrationTests.cs`

### Test Cases

```csharp
[Fact]
public void SingleInheritance_CompilesAndRuns()
{
    var source = @"
class Animal:
    name: str
    
    def __init__(self, name: str):
        self.name = name
    
    def speak(self) -> str:
        return 'Some sound'

class Dog(Animal):
    breed: str
    
    def __init__(self, name: str, breed: str):
        super().__init__(name)
        self.breed = breed

d = Dog('Rex', 'German Shepherd')
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

[Fact]
public void VirtualOverride_Works()
{
    var source = @"
class Animal:
    @virtual
    def speak(self) -> str:
        return '...'

class Dog(Animal):
    @override
    def speak(self) -> str:
        return 'Woof!'

d = Dog()
result = d.speak()
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

[Fact]
public void SuperCallInOverride_Works()
{
    var source = @"
class Animal:
    @virtual
    def speak(self) -> str:
        return 'Animal'

class Dog(Animal):
    @override
    def speak(self) -> str:
        return super().speak() + ' says Woof!'

d = Dog()
result = d.speak()
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

[Fact]
public void SuperChaining_ProducesError()
{
    var source = @"
class A:
    @virtual
    def foo(self) -> str:
        return 'A'

class B(A):
    @override
    def foo(self) -> str:
        return 'B'

class C(B):
    @override
    def foo(self) -> str:
        return super().super().foo()
";
    var result = Compile(source);
    Assert.False(result.Success);
    Assert.Contains("cannot be chained", result.Error);
}

[Fact]
public void SuperInitInControlFlow_ProducesError()
{
    var source = @"
class Parent:
    def __init__(self):
        pass

class Child(Parent):
    def __init__(self, flag: bool):
        if flag:
            super().__init__()
";
    var result = Compile(source);
    Assert.False(result.Success);
    Assert.Contains("must be the first statement", result.Error);
}

[Fact]
public void SuperInRegularMethod_ProducesError()
{
    var source = @"
class Parent:
    def helper(self) -> str:
        return 'help'

class Child(Parent):
    def my_method(self) -> str:
        return super().helper()
";
    var result = Compile(source);
    Assert.False(result.Success);
    Assert.Contains("cannot be used in regular methods", result.Error);
}

[Fact]
public void AbstractClass_CannotInstantiate()
{
    var source = @"
@abstract
class Shape:
    @abstract
    def area(self) -> float:
        ...

s = Shape()
";
    var result = Compile(source);
    Assert.False(result.Success);
    Assert.Contains("cannot create an instance", result.Error.ToLower());
}

[Fact]
public void Interface_CompilesAndImplements()
{
    var source = @"
interface IDrawable:
    def draw(self) -> None:
        ...

class Circle(IDrawable):
    def draw(self) -> None:
        pass

c = Circle()
c.draw()
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

[Fact]
public void InterfaceWithBody_ProducesError()
{
    var source = @"
interface IBad:
    def method(self) -> None:
        print('has body')
";
    var result = Compile(source);
    Assert.False(result.Success);
    Assert.Contains("cannot have implementation", result.Error);
}

[Fact]
public void MultipleInterfaces_Work()
{
    var source = @"
interface IDrawable:
    def draw(self) -> None:
        ...

interface IMovable:
    def move(self, x: int, y: int) -> None:
        ...

class Sprite(IDrawable, IMovable):
    def draw(self) -> None:
        pass
    
    def move(self, x: int, y: int) -> None:
        pass

s = Sprite()
s.draw()
s.move(10, 20)
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

[Fact]
public void FinalMethod_RequiresOverride()
{
    var source = @"
class Bad:
    @final
    def method(self) -> str:
        return 'bad'
";
    var result = Compile(source);
    Assert.False(result.Success);
    Assert.Contains("requires", result.Error);
}

[Fact]
public void OverrideNonVirtual_ProducesError()
{
    var source = @"
class Parent:
    def method(self) -> str:
        return 'parent'

class Child(Parent):
    @override
    def method(self) -> str:
        return 'child'
";
    var result = Compile(source);
    Assert.False(result.Success);
    Assert.Contains("not marked @virtual", result.Error);
}

[Fact]
public void PublicDecorator_ProducesError()
{
    var source = @"
class Foo:
    @public
    def method(self) -> str:
        return 'foo'
";
    var result = Compile(source);
    Assert.False(result.Success);
    Assert.Contains("unknown decorator", result.Error.ToLower());
}

[Fact]
public void DunderOverride_Works()
{
    var source = @"
class MyClass:
    @override
    def __str__(self) -> str:
        return 'MyClass instance'

m = MyClass()
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}
```

---

## Task 0.1.7.13: Document Phase 0.1.7 Exit Criteria Verification

**Type:** 📝 Documentation  
**Priority:** High  
**Estimated Time:** 30 minutes

📁 **Files**: `docs/implementation/phase_0_1_7_complete.md`

### Exit Criteria Checklist

| Criterion | Test | Status |
|-----------|------|--------|
| Single inheritance works | `SingleInheritance_CompilesAndRuns` | [ ] |
| `super()` calls parent constructor | `SingleInheritance_CompilesAndRuns` | [ ] |
| `super()` in override calls parent | `SuperCallInOverride_Works` | [ ] |
| `super()` chaining blocked | `SuperChaining_ProducesError` | [ ] |
| `super().__init__()` must be first | `SuperInitInControlFlow_ProducesError` | [ ] |
| `super()` in regular method blocked | `SuperInRegularMethod_ProducesError` | [ ] |
| Abstract classes work | `AbstractClass_CannotInstantiate` | [ ] |
| Interfaces compile | `Interface_CompilesAndImplements` | [ ] |
| Interface body validation | `InterfaceWithBody_ProducesError` | [ ] |
| Multiple interfaces | `MultipleInterfaces_Work` | [ ] |
| `@final` requires override | `FinalMethod_RequiresOverride` | [ ] |
| `@override` requires virtual | `OverrideNonVirtual_ProducesError` | [ ] |
| No `@public` decorator | `PublicDecorator_ProducesError` | [ ] |
| Dunder override works | `DunderOverride_Works` | [ ] |

---

## Summary: Task Dependencies

```
0.1.7.1 (Verify Inheritance AST) ──────────────────────┐
                                                       │
0.1.7.2 (super() Parsing) ────────────────────────────┼──► 0.1.7.3 (super() Validation)
                                                       │           │
0.1.7.5 (Decorator Parsing) ──────────────────────────┤           │
                                                       │           ▼
                                                       │    0.1.7.4 (Inheritance CodeGen)
                                                       │    0.1.7.6 (Decorator CodeGen)
                                                       │    0.1.7.7 (Abstract Validation)
                                                       │           │
0.1.7.8 (Interface Parsing) ──────────────────────────┤           │
                                                       │           ▼
                                                       │    0.1.7.9 (Interface CodeGen)
                                                       │    0.1.7.10 (Interface Impl)
                                                       │    0.1.7.11 (Dunder Override)
                                                       │           │
                                                       ▼           ▼
                                                0.1.7.12 (Integration Tests)
                                                       │
                                                       ▼
                                                0.1.7.13 (Exit Criteria Doc)
```

## Estimated Total Time
- **Audit/Verification tasks:** 2-3 hours
- **Implementation tasks:** 16-22 hours
- **Testing and documentation:** 4-5 hours
- **Total:** 22-30 hours

## Notes for Agent/Engineer

1. **`super()` is special** — It has strict context requirements and cannot be chained.

2. **`super().__init__()` must be first** — This transforms to `: base()` constructor initializer.

3. **NO `@public` decorator** — Public is the default; `@public` should produce an error.

4. **`@final` on methods requires override** — It generates `sealed override`, not just `sealed`.

5. **Interfaces are abstract by default** — Methods with `...` body, no `@abstract` decorator needed.

6. **Dunder methods follow Object override rules** — `__str__` → `ToString()`, etc.
# Phase 0.1.8: Structs & Enums - Comprehensive Task List (v2)

**Goal:** Value types (structs) and enumeration types with explicit values.

**Prerequisites:** Phase 0.1.7 (Inheritance & Interfaces) must be complete.

**Exit Criteria:**
- Structs compile to C# structs with value semantics
- Struct fields initialized correctly
- Structs can implement interfaces (with boxing awareness)
- Enums compile to C# enums (integer-based)
- String enums lower to static classes with const strings
- All enum values must be explicit (no auto-numbering)
- Enum `.name` and `.value` properties work
- Enum members use PascalCase in C# (`RED` → `Red`)
- Structs cannot have `@virtual` or `@abstract` methods
- Simple enums cannot have methods

---

## Pre-Implementation Checklist

Before starting any task, check what already exists:

```bash
# Check for struct-related code
grep -rn "struct\|StructDef" src/Sharpy.Compiler/

# Check for enum-related code
grep -rn "enum\|EnumDef" src/Sharpy.Compiler/

# Check for existing tests
find src -name "*.cs" -exec grep -l "Struct\|Enum" {} \;
```

---

## Task 0.1.8.1: Implement Struct Definition AST and Parsing

**Type:** 🆕 New Implementation  
**Priority:** Critical  
**Estimated Time:** 2 hours

📁 **Files**: `src/Sharpy.Compiler/Parser/Ast/Statement.cs`, `src/Sharpy.Compiler/Parser/Parser.cs`

### Objective
Parse struct definitions and create AST nodes.

### Syntax
```python
struct Point:
    x: int
    y: int

struct Vector2:
    x: float
    y: float
    
    def __init__(self, x: float, y: float):
        self.x = x
        self.y = y
    
    def length(self) -> float:
        return (self.x ** 2 + self.y ** 2) ** 0.5
```

### Grammar
```ebnf
struct_def ::= 'struct' identifier [ type_params ] [ '(' interface_list ')' ] ':' struct_body
struct_body ::= NEWLINE INDENT { struct_member } DEDENT
struct_member ::= field_decl | method_def
```

### Actions

1. **Create `StructDef` AST node:**
   ```csharp
   public record StructDef : Statement
   {
       public string Name { get; init; } = "";
       public List<TypeAnnotation> Interfaces { get; init; } = new();  // Not base classes!
       public List<Statement> Body { get; init; } = new();
       public List<Decorator> Decorators { get; init; } = new();
   }
   ```

2. **Parse struct syntax:**
   - Recognize `struct` keyword
   - Parse optional interface list in parentheses
   - Parse struct body (fields and methods)

3. **Key difference from classes:**
   - Structs can ONLY implement interfaces (no inheritance)
   - First item in parentheses must be an interface, not a class

### Test Cases

```python
# Basic struct
struct Point:
    x: int
    y: int

# Struct with constructor
struct Vector2:
    x: float
    y: float
    
    def __init__(self, x: float, y: float):
        self.x = x
        self.y = y

# Struct implementing interface
struct Point(IDescribable):
    x: int
    y: int
    
    def describe(self) -> str:
        return 'Point'
```

---

## Task 0.1.8.2: Implement Struct Code Generation

**Type:** 🆕 New Implementation  
**Priority:** Critical  
**Estimated Time:** 2-3 hours

📁 **Files**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

### Objective
Generate C# structs from Sharpy struct definitions.

### Type Mapping Note

Sharpy `float` → C# `double` (64-bit, like Python)
Sharpy `float32` → C# `float` (32-bit)

### Code Generation Example

**Input:**
```python
struct Vector2:
    x: float
    y: float
    
    def __init__(self, x: float, y: float):
        self.x = x
        self.y = y
    
    def length(self) -> float:
        return (self.x ** 2 + self.y ** 2) ** 0.5
```

**Output:**
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
    
    public double Length()
    {
        return Math.Sqrt(X * X + Y * Y);
    }
}
```

### Interface Implementation

**Input:**
```python
struct Point(IDescribable):
    x: int
    y: int
    
    def describe(self) -> str:
        return 'Point'
```

**Output:**
```csharp
public struct Point : IDescribable
{
    public int X;
    public int Y;
    
    public string Describe()
    {
        return "Point";
    }
}
```

### Implementation Hints

```csharp
private StructDeclarationSyntax GenerateStruct(StructDef structDef)
{
    var structDecl = StructDeclaration(NameMangler.ToPascalCase(structDef.Name))
        .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)));
    
    // Add interfaces (structs can only implement interfaces, not inherit)
    if (structDef.Interfaces.Any())
    {
        var baseTypes = structDef.Interfaces
            .Select(i => SimpleBaseType(IdentifierName(i.Name)));
        
        structDecl = structDecl.WithBaseList(BaseList(SeparatedList<BaseTypeSyntax>(baseTypes)));
    }
    
    // Generate fields and methods
    var members = new List<MemberDeclarationSyntax>();
    
    foreach (var member in structDef.Body)
    {
        if (member is VariableDeclaration field)
            members.Add(GenerateStructField(field));
        else if (member is FunctionDef method)
            members.Add(GenerateStructMethod(method, structDef.Name));
    }
    
    return structDecl.WithMembers(List(members));
}
```

---

## Task 0.1.8.3: Implement Struct Semantic Validation

**Type:** 🆕 New Implementation  
**Priority:** High  
**Estimated Time:** 2-3 hours

📁 **Files**: `src/Sharpy.Compiler/Semantic/SemanticAnalyzer.cs`

### Objective
Validate struct-specific rules.

### Validation Rules

1. **⚠️ Constructor must initialize ALL fields:**
   ```python
   struct Point:
       x: int
       y: int
       z: int
       
       def __init__(self, x: int, y: int):
           self.x = x
           self.y = y
           # ERROR: Field 'z' is not initialized in constructor
   ```
   - Error message: "Struct constructor must initialize all fields. Field 'z' is not assigned before the constructor returns."

2. **⚠️ Structs cannot have `@virtual` or `@abstract` methods:**
   ```python
   struct Bad:
       @virtual
       def method(self) -> int:  # ERROR: Struct methods cannot be virtual
           return 0
       
       @abstract
       def other(self) -> int:   # ERROR: Struct methods cannot be abstract
           ...
   ```
   - Error messages:
     - "Struct methods cannot be marked @virtual"
     - "Struct methods cannot be marked @abstract"
     - "Structs cannot be marked @abstract"

3. **Structs can only implement interfaces (no inheritance):**
   ```python
   struct BadStruct(SomeClass):  # ERROR: Structs cannot inherit from classes
       pass
   ```

4. **⚠️ Optional: Boxing warning when assigning to interface:**
   ```python
   struct Point(IDescribable):
       x: int
       y: int

   p = Point(10, 20)
   d: IDescribable = p  # WARNING: Assigning struct to interface causes boxing
   ```
   - Warning message: "Assigning struct 'Point' to interface 'IDescribable' causes boxing (heap allocation). Consider using direct struct method calls for performance-critical code."

### Default Struct Behavior (C# 9.0)

- Structs **always** have an implicit parameterless constructor that zero-initializes all fields
- Even if you define constructors, the parameterless one still exists
- `Point()` → all fields initialized to default values (0 for int, etc.)

```python
struct Point:
    x: int
    y: int

p1 = Point()  # x = 0, y = 0 (implicit parameterless constructor)

struct Vector:
    x: float
    y: float

    def __init__(self, x: float, y: float):
        self.x = x
        self.y = y

v1 = Vector(1.0, 2.0)  # x = 1.0, y = 2.0
v2 = Vector()          # x = 0.0, y = 0.0 (implicit parameterless still exists)
```

---

## Task 0.1.8.4: Document Struct Value Semantics

**Type:** 📝 Documentation / Awareness  
**Priority:** Medium  
**Estimated Time:** 30 minutes

📁 **Files**: `docs/language_specification/structs.md`

### Objective
Document value type semantics for engineers/users.

### Key Points

1. **Structs are value types:**
   - Copied on assignment
   - Passed by value to functions
   - Stored inline (no heap allocation for the struct itself)

2. **For large structs, use parameter modifiers to avoid copies:**
   - `in[T]` — pass by reference, read-only (no copy, no mutation)
   - `ref[T]` — pass by reference, allows mutation

   ```python
   def process(data: in[LargeStruct]) -> double:  # No copy, read-only
       return data.value

   def modify(data: ref[LargeStruct]):  # No copy, allows mutation
       data.value = 100
   ```

   Note: This is covered by parameter modifiers (separate feature), not required for Phase 0.1.8.

3. **Boxing occurs when:**
   - Struct assigned to interface variable
   - Struct passed to interface-typed parameter
   - Struct used with `object` type

---

## Task 0.1.8.5: Implement Enum Definition AST and Parsing

**Type:** 🆕 New Implementation  
**Priority:** Critical  
**Estimated Time:** 2 hours

📁 **Files**: `src/Sharpy.Compiler/Parser/Ast/Statement.cs`, `src/Sharpy.Compiler/Parser/Parser.cs`

### Objective
Parse enum definitions and create AST nodes.

### Syntax
```python
enum Color:
    RED = 1
    GREEN = 2
    BLUE = 3

enum HttpMethod:
    GET = "GET"
    POST = "POST"
    PUT = "PUT"
    DELETE = "DELETE"
```

### Grammar
```ebnf
enum_def ::= 'enum' identifier ':' NEWLINE INDENT { enum_member } DEDENT
enum_member ::= identifier '=' constant_expr NEWLINE
```

### Actions

1. **Create `EnumDef` AST node:**
   ```csharp
   public record EnumDef : Statement
   {
       public string Name { get; init; } = "";
       public List<EnumMember> Members { get; init; } = new();
   }

   public record EnumMember
   {
       public string Name { get; init; } = "";
       public Expression Value { get; init; } = null!;  // Must have explicit value
   }
   ```

2. **Parse enum syntax:**
   - Recognize `enum` keyword
   - Parse members with explicit values
   - Determine value type (int or string)

---

## Task 0.1.8.6: Implement Enum Code Generation

**Type:** 🆕 New Implementation  
**Priority:** Critical  
**Estimated Time:** 2-3 hours

📁 **Files**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

### Objective
Generate C# code for enums.

### ⚠️ Two Code Generation Strategies

1. **Integer enums → C# `enum`:**
   ```python
   enum Color:
       RED = 1
       GREEN = 2
       BLUE = 3
   ```
   
   **Generated C#:**
   ```csharp
   public enum Color
   {
       Red = 1,
       Green = 2,
       Blue = 3
   }
   ```

2. **⚠️ String enums → Static class with const strings:**
   ```python
   enum HttpMethod:
       GET = "GET"
       POST = "POST"
       PUT = "PUT"
       DELETE = "DELETE"
   ```
   
   **Generated C#:**
   ```csharp
   public static class HttpMethod
   {
       public const string Get = "GET";
       public const string Post = "POST";
       public const string Put = "PUT";
       public const string Delete = "DELETE";
   }
   ```
   
   C# `enum` only supports integral types, so string enums must be lowered to static classes.

### ⚠️ Enum Member Name Mangling

Enum members should be transformed to PascalCase:
- `RED` → `Red`
- `DARK_BLUE` → `DarkBlue`
- `HTTP_NOT_FOUND` → `HttpNotFound`

**Important:** Use `NameContext.EnumMember` or create a specific method for this, NOT `NameContext.Constant` (which preserves CAPS_SNAKE_CASE).

### Implementation Hints

```csharp
private MemberDeclarationSyntax GenerateEnum(EnumDef enumDef)
{
    // Determine if this is integer or string enum
    var firstValue = enumDef.Members.FirstOrDefault()?.Value;
    bool isStringEnum = firstValue is StringLiteral;
    
    if (isStringEnum)
    {
        return GenerateStringEnumAsStaticClass(enumDef);
    }
    else
    {
        return GenerateIntegerEnum(enumDef);
    }
}

private EnumDeclarationSyntax GenerateIntegerEnum(EnumDef enumDef)
{
    var members = enumDef.Members.Select(m =>
        EnumMemberDeclaration(NameMangler.EnumMemberToPascalCase(m.Name))
            .WithEqualsValue(EqualsValueClause(GenerateExpression(m.Value))));
    
    return EnumDeclaration(enumDef.Name)
        .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
        .WithMembers(SeparatedList(members));
}

private ClassDeclarationSyntax GenerateStringEnumAsStaticClass(EnumDef enumDef)
{
    var members = enumDef.Members.Select(m =>
        FieldDeclaration(
            VariableDeclaration(PredefinedType(Token(SyntaxKind.StringKeyword)))
                .WithVariables(SingletonSeparatedList(
                    VariableDeclarator(NameMangler.EnumMemberToPascalCase(m.Name))
                        .WithInitializer(EqualsValueClause(GenerateExpression(m.Value))))))
            .WithModifiers(TokenList(
                Token(SyntaxKind.PublicKeyword),
                Token(SyntaxKind.ConstKeyword))));
    
    return ClassDeclaration(enumDef.Name)
        .WithModifiers(TokenList(
            Token(SyntaxKind.PublicKeyword),
            Token(SyntaxKind.StaticKeyword)))
        .WithMembers(List<MemberDeclarationSyntax>(members));
}
```

---

## Task 0.1.8.7: Implement Enum Semantic Validation

**Type:** 🆕 New Implementation  
**Priority:** High  
**Estimated Time:** 1-2 hours

📁 **Files**: `src/Sharpy.Compiler/Semantic/SemanticAnalyzer.cs`

### Objective
Validate enum-specific rules.

### Validation Rules

1. **All enum values must be explicit:**
   ```python
   enum Bad:
       A        # ERROR: requires explicit value
       B = 1
   ```
   - Error message: "Enum member 'A' requires an explicit value. All enum members must have explicit constant values."

2. **All values must be of the same type:**
   ```python
   enum Mixed:
       A = 1
       B = "str"  # ERROR: mixed types
   ```
   - Error message: "Enum 'Mixed' has mixed value types. All enum members must have values of the same type (all integers or all strings)."

3. **⚠️ Simple enums cannot have methods:**
   ```python
   enum Color:
       RED = 1
       GREEN = 2
       
       def is_warm(self) -> bool:  # ERROR: Simple enums cannot have methods
           return self == Color.RED
   ```
   - Error message: "Simple enums cannot have methods. Use a tagged union for enums with associated methods."

4. **Enum values must be compile-time constants:**
   - Integer literals
   - String literals
   - Other constant expressions

---

## Task 0.1.8.8: Implement Enum Usage Code Generation

**Type:** 🆕 New Implementation  
**Priority:** High  
**Estimated Time:** 2-3 hours

📁 **Files**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

### Objective
Generate code for enum member access and properties.

### Actions

1. **Enum member access:**
   ```python
   favorite = Color.RED
   ```
   
   **Generated C#:**
   ```csharp
   var favorite = Color.Red;  // Note: PascalCase
   ```

2. **⚠️ `.value` property:**
   ```python
   value = favorite.value  # Returns underlying value
   ```
   
   **Generated C# (integer enum):**
   ```csharp
   var value = (int)favorite;
   ```
   
   **Generated C# (string enum):**
   ```csharp
   // String enums are already const strings, so .value just returns the string
   var value = HttpMethod.Get;  // Already "GET"
   ```

3. **⚠️ `.name` property:**
   ```python
   name = favorite.name  # Returns enum member name as string
   ```
   
   **Generated C#:**
   ```csharp
   var name = Enum.GetName(typeof(Color), favorite);
   // Or: var name = favorite.ToString();
   ```

### Phase Scope Decision

- Include `.value` access (simple cast for int enums)
- Include `.name` access (`Enum.GetName` or `ToString()`)
- **Defer** iteration support (`for x in EnumType:`) to later phase (requires collection semantics)

---

## Task 0.1.8.9: Implement Struct Instantiation and Assignment

**Type:** 🆕 New Implementation  
**Priority:** High  
**Estimated Time:** 1-2 hours

📁 **Files**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

### Objective
Generate correct code for struct instantiation and value semantics.

### Actions

1. **Struct instantiation:**
   ```python
   p = Point(10, 20)
   ```
   
   **Generated C#:**
   ```csharp
   var p = new Point(10, 20);
   ```

2. **Default instantiation:**
   ```python
   p = Point()  # Uses implicit parameterless constructor
   ```
   
   **Generated C#:**
   ```csharp
   var p = new Point();  // All fields zero-initialized
   // Or: var p = default(Point);
   ```

3. **Struct assignment (value copy):**
   ```python
   p1 = Point(1, 2)
   p2 = p1  # Value copy, not reference
   p2.x = 100  # Does not affect p1
   ```
   
   **Generated C#:**
   ```csharp
   var p1 = new Point(1, 2);
   var p2 = p1;  // Value copy
   p2.X = 100;   // Does not affect p1
   ```

---

## Task 0.1.8.10: Create Phase 0.1.8 Integration Tests

**Type:** 🆕 New Implementation  
**Priority:** Critical  
**Estimated Time:** 3-4 hours

📁 **Files**: `src/Sharpy.Compiler.Tests/Integration/Phase018IntegrationTests.cs`

### Test Cases

```csharp
[Fact]
public void BasicStruct_CompilesAndRuns()
{
    var source = @"
struct Point:
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
public void StructWithConstructor_Works()
{
    var source = @"
struct Vector2:
    x: float
    y: float
    
    def __init__(self, x: float, y: float):
        self.x = x
        self.y = y

v = Vector2(3.0, 4.0)
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

[Fact]
public void StructConstructorMustInitializeAllFields()
{
    var source = @"
struct Point:
    x: int
    y: int
    z: int
    
    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y
";
    var result = Compile(source);
    Assert.False(result.Success);
    Assert.Contains("z", result.Error);
    Assert.Contains("not initialized", result.Error.ToLower());
}

[Fact]
public void StructWithInterface_Works()
{
    var source = @"
interface IDescribable:
    def describe(self) -> str:
        ...

struct Point(IDescribable):
    x: int
    y: int
    
    def describe(self) -> str:
        return 'Point'

p = Point()
p.x = 10
p.y = 20
desc = p.describe()
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

[Fact]
public void StructVirtualMethod_ProducesError()
{
    var source = @"
struct Bad:
    @virtual
    def method(self) -> int:
        return 0
";
    var result = Compile(source);
    Assert.False(result.Success);
    Assert.Contains("virtual", result.Error.ToLower());
}

[Fact]
public void StructAbstractMethod_ProducesError()
{
    var source = @"
struct Bad:
    @abstract
    def method(self) -> int:
        ...
";
    var result = Compile(source);
    Assert.False(result.Success);
    Assert.Contains("abstract", result.Error.ToLower());
}

[Fact]
public void IntegerEnum_CompilesAndRuns()
{
    var source = @"
enum Color:
    RED = 1
    GREEN = 2
    BLUE = 3

favorite = Color.RED
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

[Fact]
public void StringEnum_CompilesAndRuns()
{
    var source = @"
enum HttpMethod:
    GET = 'GET'
    POST = 'POST'
    PUT = 'PUT'

method = HttpMethod.GET
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

[Fact]
public void EnumWithoutExplicitValue_ProducesError()
{
    var source = @"
enum Bad:
    A
    B = 1
";
    var result = Compile(source);
    Assert.False(result.Success);
    Assert.Contains("explicit value", result.Error.ToLower());
}

[Fact]
public void EnumMixedTypes_ProducesError()
{
    var source = @"
enum Mixed:
    A = 1
    B = 'string'
";
    var result = Compile(source);
    Assert.False(result.Success);
    Assert.Contains("mixed", result.Error.ToLower());
}

[Fact]
public void EnumWithMethods_ProducesError()
{
    var source = @"
enum Color:
    RED = 1
    GREEN = 2
    
    def is_warm(self) -> bool:
        return True
";
    var result = Compile(source);
    Assert.False(result.Success);
    Assert.Contains("cannot have methods", result.Error.ToLower());
}

[Fact]
public void EnumValue_Property()
{
    var source = @"
enum Color:
    RED = 1
    GREEN = 2
    BLUE = 3

favorite = Color.RED
value = favorite.value
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
    // value should be 1
}

[Fact]
public void EnumName_Property()
{
    var source = @"
enum Color:
    RED = 1
    GREEN = 2
    BLUE = 3

favorite = Color.RED
name = favorite.name
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
    // name should be 'Red' (PascalCase in C#)
}

[Fact]
public void EnumMemberNameMangling_CorrectPascalCase()
{
    var source = @"
enum Status:
    NOT_FOUND = 404
    INTERNAL_SERVER_ERROR = 500

s = Status.NOT_FOUND
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
    // Generated C# should have NotFound, InternalServerError
}

[Fact]
public void StructValueSemantics_CopyOnAssignment()
{
    var source = @"
struct Point:
    x: int
    y: int

p1 = Point()
p1.x = 10
p2 = p1
p2.x = 100
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
    // p1.x should still be 10 (value copy)
}
```

---

## Task 0.1.8.11: Document Phase 0.1.8 Exit Criteria Verification

**Type:** 📝 Documentation  
**Priority:** High  
**Estimated Time:** 30 minutes

📁 **Files**: `docs/implementation/phase_0_1_8_complete.md`

### Exit Criteria Checklist

| Criterion | Test | Status |
|-----------|------|--------|
| Basic struct compiles | `BasicStruct_CompilesAndRuns` | [ ] |
| Struct with constructor | `StructWithConstructor_Works` | [ ] |
| Struct field init validation | `StructConstructorMustInitializeAllFields` | [ ] |
| Struct implements interface | `StructWithInterface_Works` | [ ] |
| No @virtual in structs | `StructVirtualMethod_ProducesError` | [ ] |
| No @abstract in structs | `StructAbstractMethod_ProducesError` | [ ] |
| Integer enum compiles | `IntegerEnum_CompilesAndRuns` | [ ] |
| String enum compiles | `StringEnum_CompilesAndRuns` | [ ] |
| Enum requires explicit values | `EnumWithoutExplicitValue_ProducesError` | [ ] |
| Enum type consistency | `EnumMixedTypes_ProducesError` | [ ] |
| No methods in simple enums | `EnumWithMethods_ProducesError` | [ ] |
| Enum .value property | `EnumValue_Property` | [ ] |
| Enum .name property | `EnumName_Property` | [ ] |
| Enum member name mangling | `EnumMemberNameMangling_CorrectPascalCase` | [ ] |
| Struct value semantics | `StructValueSemantics_CopyOnAssignment` | [ ] |

---

## Summary: Task Dependencies

```
0.1.8.1 (Struct AST/Parsing) ───────────────────────────┐
                                                        │
0.1.8.5 (Enum AST/Parsing) ─────────────────────────────┼──► 0.1.8.2 (Struct CodeGen)
                                                        │    0.1.8.3 (Struct Validation)
                                                        │    0.1.8.4 (Value Semantics Doc)
                                                        │           │
                                                        │           ▼
                                                        │    0.1.8.6 (Enum CodeGen)
                                                        │    0.1.8.7 (Enum Validation)
                                                        │    0.1.8.8 (Enum Usage)
                                                        │    0.1.8.9 (Struct Instantiation)
                                                        │           │
                                                        ▼           ▼
                                                 0.1.8.10 (Integration Tests)
                                                        │
                                                        ▼
                                                 0.1.8.11 (Exit Criteria Doc)
```

## Estimated Total Time
- **Parsing/AST tasks:** 4-5 hours
- **Code generation tasks:** 6-8 hours
- **Validation tasks:** 3-5 hours
- **Testing and documentation:** 4-5 hours
- **Total:** 17-23 hours

## Notes for Agent/Engineer

1. **Structs are value types** — They're copied on assignment and passed by value.

2. **String enums → static classes** — C# `enum` only supports integral types.

3. **Enum member name mangling** — Use PascalCase (`RED` → `Red`), NOT constant case.

4. **All enum values explicit** — No auto-numbering like Python's `auto()`.

5. **Struct constructors must init all fields** — C# 9.0 requirement for value types.

6. **No @virtual/@abstract on struct methods** — Structs have no polymorphism.

7. **Simple enums have no methods** — Tagged unions (future feature) support methods.

8. **Type mapping:** `float` → `double`, `float32` → `float`.
# Phase 0.1.9: Type System Enhancements - Comprehensive Task List (v2)

**Goal:** Nullable types, generics, type aliases, type constraints, and null-handling operators.

**Prerequisites:** Phase 0.1.8 (Structs & Enums) must be complete.

**Exit Criteria:**
- Nullable types (`T?`) compile correctly for value and reference types
- `#nullable enable` in all generated C# (non-nullable by default)
- Null coalescing (`??`) works
- Null coalescing assignment (`??=`) works
- Null conditional (`?.`) works with correct result type inference
- Type narrowing in `if x is not None:` blocks
- Generic classes/methods with `[T]` syntax
- Type constraints (`: IComparable`, `: class`, `: struct`)
- Type aliases are compile-time expanded (not emitted to C#)
- Function type aliases map to `Func<>` / `Action<>`
- Nested nullable validation (`T??` is error)

---

## Pre-Implementation Checklist

Before starting any task, check what already exists:

```bash
# Check for nullable-related code
grep -rn "Nullable\|nullable\|\?" src/Sharpy.Compiler/

# Check for generic-related code
grep -rn "Generic\|TypeParam\|type_param" src/Sharpy.Compiler/

# Check for type alias handling
grep -rn "TypeAlias\|type.*=" src/Sharpy.Compiler/

# Check existing tests
find src -name "*.cs" -exec grep -l "Nullable\|Generic\|TypeAlias" {} \;
```

---

## Task 0.1.9.1: Implement Nullable Type Parsing

**Type:** 🆕 New Implementation  
**Priority:** Critical  
**Estimated Time:** 2 hours

📁 **Files**: `src/Sharpy.Compiler/Parser/Parser.cs`, `src/Sharpy.Compiler/Parser/Ast/Type.cs`

### Objective
Parse nullable type syntax (`T?`).

### Syntax
```python
x: int?             # Nullable int (value type)
name: str?          # Nullable string (reference type)
points: list[int?]  # List of nullable ints
data: list[int]?    # Nullable list of ints
both: list[int?]?   # Nullable list of nullable ints
```

### Grammar
```ebnf
type_annotation ::= base_type '?'?
                  | base_type '[' type_args ']' '?'?
```

### Actions

1. **Parse nullable marker:**
   - `?` suffix on type indicates nullable
   - Can apply to any type (value or reference)

2. **⚠️ Validate nested nullable:**
   ```python
   x: int??  # ERROR: Double nullable not meaningful
   ```
   - Error message: "Type 'int' cannot be made nullable twice"

3. **Handle nullable in generics:**
   ```python
   list[int?]    # List of nullable ints
   list[int]?    # Nullable list
   dict[str, int?]?  # Nullable dict with nullable int values
   ```

### AST Representation

```csharp
public record TypeAnnotation
{
    public string Name { get; init; } = "";
    public List<TypeAnnotation> TypeArguments { get; init; } = new();
    public bool IsNullable { get; init; }
}
```

---

## Task 0.1.9.2: Implement Nullable Type Code Generation

**Type:** 🆕 New Implementation  
**Priority:** Critical  
**Estimated Time:** 2-3 hours

📁 **Files**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

### Objective
Generate correct C# nullable types.

### ⚠️ Key Requirement: `#nullable enable`

All generated C# files should include `#nullable enable` at the top. This enables C# nullable reference types (C# 8+) and aligns with Sharpy's "null-safe by default" principle (Axiom 3).

```csharp
#nullable enable

namespace MyProject
{
    // ...
}
```

### Type Mapping

| Sharpy | C# | Notes |
|--------|-----|-------|
| `int?` | `int?` | `Nullable<int>` |
| `str?` | `string?` | Nullable reference |
| `list[int]?` | `List<int>?` | Nullable list |
| `list[int?]` | `List<int?>` | List of nullable |
| `MyClass?` | `MyClass?` | Nullable reference |
| `MyStruct?` | `MyStruct?` | `Nullable<MyStruct>` |

### Implementation Hints

```csharp
private TypeSyntax MapType(TypeAnnotation type)
{
    var baseType = MapBaseType(type.Name, type.TypeArguments);
    
    if (type.IsNullable)
    {
        return NullableType(baseType);
    }
    
    return baseType;
}

// Add to file generation:
private CompilationUnitSyntax GenerateCompilationUnit(...)
{
    var unit = CompilationUnit()
        .WithUsings(...)
        .WithMembers(...);
    
    // Add #nullable enable directive
    unit = unit.WithLeadingTrivia(
        Trivia(NullableDirectiveTrivia(Token(SyntaxKind.EnableKeyword), true)));
    
    return unit;
}
```

---

## Task 0.1.9.3: Implement Null Coalescing Operator (`??`)

**Type:** 🆕 New Implementation  
**Priority:** High  
**Estimated Time:** 1-2 hours

📁 **Files**: `src/Sharpy.Compiler/Parser/Parser.cs`, `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

### Objective
Parse and generate null coalescing operator.

### Syntax
```python
result = value ?? default_value
```

### Actions

1. **Parse `??` operator:**
   - Binary operator with left associativity
   - Lower precedence than comparison operators

2. **Generate C# `??`:**
   ```python
   name = user_name ?? "Guest"
   ```
   
   **Generated C#:**
   ```csharp
   var name = userName ?? "Guest";
   ```

3. **Type inference:**
   - If left is `T?` and right is `T`, result is `T`
   - If both are `T?`, result is `T?`

---

## Task 0.1.9.4: Implement Null Coalescing Assignment (`??=`)

**Type:** 🆕 New Implementation  
**Priority:** Medium  
**Estimated Time:** 1 hour

📁 **Files**: `src/Sharpy.Compiler/Parser/Parser.cs`, `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

### Objective
Parse and generate null coalescing assignment operator.

### Syntax
```python
name: str? = None
name ??= "default"  # Assign only if name is None
```

### Generated C#
```csharp
string? name = null;
name ??= "default";
```

### Actions

1. **Parse `??=` as compound assignment:**
   - Target must be assignable (variable, field, property)
   - Left side must be nullable type

2. **Generate C# `??=`:**
   - Direct mapping to C# operator

---

## Task 0.1.9.5: Implement Null Conditional Operator (`?.`)

**Type:** 🆕 New Implementation  
**Priority:** High  
**Estimated Time:** 2-3 hours

📁 **Files**: `src/Sharpy.Compiler/Parser/Parser.cs`, `src/Sharpy.Compiler/Semantic/SemanticAnalyzer.cs`, `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

### Objective
Parse and generate null conditional operator with correct type inference.

### Syntax
```python
name: str? = "hello"
upper = name?.upper()  # upper is str? (not str!)
length = name?.length  # length is int?
```

### ⚠️ Result Type Inference

The result type of a null conditional expression is **always nullable**, even if the member returns non-nullable:

```python
class Person:
    def get_name(self) -> str:  # Returns non-nullable str
        return self.name

p: Person? = None
n = p?.get_name()  # n is str? (nullable) because p might be None
```

### Type Inference Rules

- `T?.member` where `member` has type `U` → result type is `U?`
- `T?.method()` where `method()` returns `U` → result type is `U?`
- Chained: `T?.a?.b` → result type is `typeof(b)?`

### Implementation Hints

```csharp
private ExpressionSyntax GenerateNullConditional(NullConditionalExpression expr)
{
    var target = GenerateExpression(expr.Target);
    var member = expr.Member;
    
    // Generate: target?.Member
    return ConditionalAccessExpression(
        target,
        MemberBindingExpression(IdentifierName(NameMangler.ToPascalCase(member))));
}

// In type checker:
private TypeInfo InferNullConditionalType(NullConditionalExpression expr)
{
    var targetType = InferType(expr.Target);
    // Must be nullable
    if (!targetType.IsNullable)
    {
        ReportWarning("Null conditional on non-nullable type has no effect");
    }
    
    var memberType = ResolveMemberType(targetType.UnwrapNullable(), expr.Member);
    
    // Result is ALWAYS nullable
    return memberType.MakeNullable();
}
```

---

## Task 0.1.9.6: Implement Type Narrowing

**Type:** 🆕 New Implementation  
**Priority:** High  
**Estimated Time:** 2-3 hours

📁 **Files**: `src/Sharpy.Compiler/Semantic/SemanticAnalyzer.cs`, `src/Sharpy.Compiler/Semantic/TypeChecker.cs`

### Objective
Narrow types within conditional blocks.

### Type Narrowing Patterns

1. **`is not None` check:**
   ```python
   def process(value: str?) -> str:
       if value is not None:
           # value is narrowed from str? to str
           return value.upper()
       return "empty"
   ```

2. **`is None` check (inverse narrowing):**
   ```python
   def process(value: str?) -> str:
       if value is None:
           return "empty"
       # After return, value is narrowed to str
       return value.upper()
   ```

3. **`isinstance` check:**
   ```python
   def handle(obj: Animal) -> str:
       if isinstance(obj, Dog):
           # obj is narrowed from Animal to Dog
           return obj.bark()
       return "unknown"
   ```

### Scope Rules

- Narrowing applies ONLY within the narrowing block
- Narrowing does NOT persist after the block ends
- Narrowing in `elif` is independent of previous `if`

### Implementation Hints

```csharp
private class TypeNarrowingScope
{
    public Dictionary<string, TypeInfo> NarrowedTypes { get; } = new();
}

private Stack<TypeNarrowingScope> _narrowingScopes = new();

private void AnalyzeIfStatement(IfStatement ifStmt)
{
    // Check condition for narrowing patterns
    if (IsNotNoneCheck(ifStmt.Condition, out var variable))
    {
        _narrowingScopes.Push(new TypeNarrowingScope());
        var currentType = GetVariableType(variable);
        _narrowingScopes.Peek().NarrowedTypes[variable] = currentType.UnwrapNullable();
        
        AnalyzeBody(ifStmt.ThenBody);
        
        _narrowingScopes.Pop();
    }
    else
    {
        AnalyzeBody(ifStmt.ThenBody);
    }
    
    // Handle else branch with inverse narrowing
    if (ifStmt.ElseBody != null)
    {
        // In else, the variable is still nullable (original type)
        AnalyzeBody(ifStmt.ElseBody);
    }
}

private TypeInfo GetVariableType(string name)
{
    // Check narrowing scopes first
    foreach (var scope in _narrowingScopes)
    {
        if (scope.NarrowedTypes.TryGetValue(name, out var narrowed))
            return narrowed;
    }
    
    return _symbolTable.GetVariable(name).Type;
}
```

---

## Task 0.1.9.7: Implement Type Alias Parsing

**Type:** 🆕 New Implementation  
**Priority:** High  
**Estimated Time:** 1-2 hours

📁 **Files**: `src/Sharpy.Compiler/Parser/Parser.cs`, `src/Sharpy.Compiler/Parser/Ast/Statement.cs`

### Objective
Parse type alias declarations.

### Syntax
```python
type UserId = int
type StringList = list[str]
type Callback = (int, str) -> bool
type Effect = () -> None
```

### Grammar
```ebnf
type_alias ::= 'type' identifier '=' type_expression
type_expression ::= type_annotation
                  | function_type
function_type ::= '(' [ type_list ] ')' '->' type_annotation
```

### Actions

1. **Create `TypeAlias` AST node:**
   ```csharp
   public record TypeAlias : Statement
   {
       public string Name { get; init; } = "";
       public TypeExpression Type { get; init; } = null!;
   }
   ```

2. **Parse type alias syntax:**
   - `type` keyword
   - Identifier (alias name)
   - `=` 
   - Type expression (including function types)

---

## Task 0.1.9.8: Implement Type Alias Expansion

**Type:** 🆕 New Implementation  
**Priority:** High  
**Estimated Time:** 2-3 hours

📁 **Files**: `src/Sharpy.Compiler/Semantic/SemanticAnalyzer.cs`, `src/Sharpy.Compiler/Semantic/TypeResolver.cs`

### Objective
Expand type aliases at compile time (no C# output for aliases).

### Key Principle

Type aliases are **compile-time only**. They are expanded at every usage point, and NO C# declaration is generated for the alias itself.

### Example

```python
type UserId = int
type StringList = list[str]

def get_user(id: UserId) -> StringList:
    ...
```

**Generated C# (NO type alias declarations):**
```csharp
public List<string> GetUser(int id)
{
    ...
}
```

### Expansion Strategy

1. **First pass:** Register all type aliases in symbol table
2. **Type resolution:** Replace alias names with their expanded types
3. **Code generation:** Work only with expanded types

### Error Cases

```python
type Recursive = list[Recursive]  # ERROR: Recursive type alias
type Unknown = NonExistent        # ERROR: Undefined type in alias
```

### Function Type Alias Mapping

```python
type UnaryOp = (int) -> int              # Func<int, int>
type BinaryOp = (int, int) -> int        # Func<int, int, int>
type Consumer = (str) -> None            # Action<string>
type Supplier = () -> int                # Func<int>
type Effect = () -> None                 # Action
```

**Mapping Rules:**
- `(T1, T2, ...) -> R` where R is not None → `Func<T1, T2, ..., R>`
- `(T1, T2, ...) -> None` → `Action<T1, T2, ...>`
- `() -> R` where R is not None → `Func<R>`
- `() -> None` → `Action`

---

## Task 0.1.9.9: Implement Generic Type Parsing

**Type:** 🆕 New Implementation  
**Priority:** High  
**Estimated Time:** 2 hours

📁 **Files**: `src/Sharpy.Compiler/Parser/Parser.cs`, `src/Sharpy.Compiler/Parser/Ast/Statement.cs`

### Objective
Parse generic class/method definitions with `[T]` syntax.

### ⚠️ Key Syntax Difference from C#

Sharpy uses **square brackets** `[T]` for generics, not angle brackets `<T>`:

```python
class Box[T]:
    value: T

def find_max[T](a: T, b: T) -> T:
    ...

box = Box[int](42)  # Usage
```

### Grammar
```ebnf
type_params ::= '[' type_param { ',' type_param } [ ',' ] ']'
type_param ::= identifier [ ':' type_constraint ]
type_constraint ::= qualified_name [ '[' type_args ']' ]
                  | 'class'
                  | 'struct'
```

### Actions

1. **Parse type parameters:**
   - `[T]` → single type parameter
   - `[K, V]` → multiple type parameters
   - `[T: IComparable]` → type parameter with constraint

2. **Parse generic usage:**
   - `Box[int]` → generic instantiation
   - `list[str]` → built-in generic

---

## Task 0.1.9.10: Implement Generic Code Generation

**Type:** 🆕 New Implementation  
**Priority:** High  
**Estimated Time:** 2-3 hours

📁 **Files**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

### Objective
Transform Sharpy `[T]` syntax to C# `<T>` syntax.

### Code Generation Examples

**Input:**
```python
class Box[T]:
    value: T
    
    def __init__(self, value: T):
        self.value = value
    
    def get(self) -> T:
        return self.value

box = Box[int](42)
```

**Output:**
```csharp
public class Box<T>
{
    public T Value;
    
    public Box(T value)
    {
        Value = value;
    }
    
    public T Get()
    {
        return Value;
    }
}

var box = new Box<int>(42);
```

### Implementation Hints

```csharp
private ClassDeclarationSyntax GenerateGenericClass(ClassDef classDef)
{
    var classDecl = ClassDeclaration(NameMangler.ToPascalCase(classDef.Name))
        .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)));
    
    // Add type parameters
    if (classDef.TypeParameters.Any())
    {
        var typeParams = classDef.TypeParameters
            .Select(tp => TypeParameter(tp.Name));
        
        classDecl = classDecl.WithTypeParameterList(
            TypeParameterList(SeparatedList(typeParams)));
    }
    
    // ... generate members
    
    return classDecl;
}
```

---

## Task 0.1.9.11: Implement Type Constraints

**Type:** 🆕 New Implementation  
**Priority:** High  
**Estimated Time:** 2-3 hours

📁 **Files**: `src/Sharpy.Compiler/Parser/Parser.cs`, `src/Sharpy.Compiler/Semantic/SemanticAnalyzer.cs`, `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

### Objective
Parse and generate type constraints.

### Syntax
```python
def find_max[T: IComparable](a: T, b: T) -> T:  # Interface constraint
    ...

class Container[T: class](value: T):  # Reference type constraint
    ...

struct Wrapper[T: struct](value: T):  # Value type constraint
    ...
```

### Constraint Types

| Sharpy | C# |
|--------|-----|
| `T: IComparable` | `where T : IComparable` |
| `T: class` | `where T : class` |
| `T: struct` | `where T : struct` |
| `T: BaseClass` | `where T : BaseClass` |

### Generated C#

```csharp
public T FindMax<T>(T a, T b) where T : IComparable
{
    ...
}

public class Container<T> where T : class
{
    ...
}

public struct Wrapper<T> where T : struct
{
    ...
}
```

### ⚠️ Constraint Validation

1. **Constraint must exist:**
   ```python
   def foo[T: NonExistent](x: T):  # ERROR: NonExistent is not defined
   ```

2. **Constraint must be interface or class:**
   ```python
   def foo[T: int](x: T):  # ERROR: int is not a valid constraint
   ```

3. **`struct` and `class` are mutually exclusive:**
   ```python
   def foo[T: struct & class](x: T):  # ERROR: Cannot be both
   ```

4. **Type argument must satisfy constraint:**
   ```python
   def foo[T: IComparable](x: T):
       ...
   
   foo[int](42)     # OK: int implements IComparable
   foo[object](x)   # ERROR: object doesn't implement IComparable
   ```

### Scope Decision

- **Phase 0.1.9:** Single constraints only
- **Deferred:** Multiple constraints (`T: IFoo & IBar`)
- **Deferred:** Generic variance (covariance/contravariance with `out`/`in`)

---

## Task 0.1.9.12: Create Phase 0.1.9 Integration Tests

**Type:** 🆕 New Implementation  
**Priority:** Critical  
**Estimated Time:** 3-4 hours

📁 **Files**: `src/Sharpy.Compiler.Tests/Integration/Phase019IntegrationTests.cs`

### Test Cases

```csharp
[Fact]
public void NullableValueType_CompilesCorrectly()
{
    var source = @"
x: int? = None
y: int? = 42

if x is not None:
    z = x + 1
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

[Fact]
public void NullableReferenceType_CompilesCorrectly()
{
    var source = @"
name: str? = None
name = 'hello'
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

[Fact]
public void NullCoalescing_Works()
{
    var source = @"
value: str? = None
result = value ?? 'default'
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

[Fact]
public void NullCoalescingAssignment_Works()
{
    var source = @"
value: str? = None
value ??= 'default'
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

[Fact]
public void NullConditional_Works()
{
    var source = @"
class Person:
    name: str
    
    def __init__(self, name: str):
        self.name = name
    
    def get_name(self) -> str:
        return self.name

p: Person? = None
name = p?.get_name()
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

[Fact]
public void NullConditional_ResultTypeIsNullable()
{
    var source = @"
class Foo:
    def bar(self) -> int:
        return 42

f: Foo? = None
result = f?.bar()
result2: int? = result
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

[Fact]
public void TypeNarrowing_IsNotNone()
{
    var source = @"
def process(value: str?) -> str:
    if value is not None:
        return value.upper()
    return 'empty'

result = process('hello')
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

[Fact]
public void DoubleNullable_ProducesError()
{
    var source = @"
x: int?? = None
";
    var result = Compile(source);
    Assert.False(result.Success);
    Assert.Contains("nullable twice", result.Error.ToLower());
}

[Fact]
public void TypeAlias_ExpandsCorrectly()
{
    var source = @"
type UserId = int

def get_user(id: UserId) -> str:
    return 'user'

result = get_user(42)
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

[Fact]
public void FunctionTypeAlias_Works()
{
    var source = @"
type Predicate = (int) -> bool

def is_positive(x: int) -> bool:
    return x > 0

check: Predicate = is_positive
result = check(5)
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

[Fact]
public void GenericClass_CompilesAndRuns()
{
    var source = @"
class Box[T]:
    value: T
    
    def __init__(self, value: T):
        self.value = value
    
    def get(self) -> T:
        return self.value

box = Box[int](42)
result = box.get()
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

[Fact]
public void GenericMethod_CompilesAndRuns()
{
    var source = @"
def identity[T](value: T) -> T:
    return value

result = identity[int](42)
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

[Fact]
public void TypeConstraint_Interface()
{
    var source = @"
interface IComparable:
    def compare_to(self, other: object) -> int:
        ...

def find_max[T: IComparable](a: T, b: T) -> T:
    if a.compare_to(b) > 0:
        return a
    return b
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

[Fact]
public void TypeConstraint_Class()
{
    var source = @"
class RefContainer[T: class]:
    value: T?
    
    def __init__(self):
        self.value = None

c = RefContainer[str]()
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

[Fact]
public void TypeConstraint_Struct()
{
    var source = @"
struct ValueWrapper[T: struct]:
    value: T

w = ValueWrapper[int]()
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

[Fact]
public void NestedNullable_InGenerics()
{
    var source = @"
values: list[int?] = [1, None, 3]
data: list[int]? = None
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

[Fact]
public void RecursiveTypeAlias_ProducesError()
{
    var source = @"
type Recursive = list[Recursive]
";
    var result = Compile(source);
    Assert.False(result.Success);
    Assert.Contains("recursive", result.Error.ToLower());
}
```

---

## Task 0.1.9.13: Document Phase 0.1.9 Exit Criteria Verification

**Type:** 📝 Documentation  
**Priority:** High  
**Estimated Time:** 30 minutes

📁 **Files**: `docs/implementation/phase_0_1_9_complete.md`

### Exit Criteria Checklist

| Criterion | Test | Status |
|-----------|------|--------|
| Nullable value type | `NullableValueType_CompilesCorrectly` | [ ] |
| Nullable reference type | `NullableReferenceType_CompilesCorrectly` | [ ] |
| `??` operator | `NullCoalescing_Works` | [ ] |
| `??=` operator | `NullCoalescingAssignment_Works` | [ ] |
| `?.` operator | `NullConditional_Works` | [ ] |
| `?.` result type nullable | `NullConditional_ResultTypeIsNullable` | [ ] |
| Type narrowing | `TypeNarrowing_IsNotNone` | [ ] |
| Double nullable error | `DoubleNullable_ProducesError` | [ ] |
| Type alias expansion | `TypeAlias_ExpandsCorrectly` | [ ] |
| Function type alias | `FunctionTypeAlias_Works` | [ ] |
| Generic class | `GenericClass_CompilesAndRuns` | [ ] |
| Generic method | `GenericMethod_CompilesAndRuns` | [ ] |
| Interface constraint | `TypeConstraint_Interface` | [ ] |
| Class constraint | `TypeConstraint_Class` | [ ] |
| Struct constraint | `TypeConstraint_Struct` | [ ] |
| Nested nullable generics | `NestedNullable_InGenerics` | [ ] |
| Recursive alias error | `RecursiveTypeAlias_ProducesError` | [ ] |

---

## Summary: Task Dependencies

```
0.1.9.1 (Nullable Parsing) ─────────────────────────────┐
                                                        │
0.1.9.7 (Type Alias Parsing) ───────────────────────────┼──► 0.1.9.2 (Nullable CodeGen)
                                                        │    0.1.9.3 (?? Operator)
0.1.9.9 (Generic Parsing) ──────────────────────────────┤    0.1.9.4 (??= Operator)
                                                        │    0.1.9.5 (?. Operator)
                                                        │    0.1.9.6 (Type Narrowing)
                                                        │           │
                                                        │           ▼
                                                        │    0.1.9.8 (Alias Expansion)
                                                        │    0.1.9.10 (Generic CodeGen)
                                                        │    0.1.9.11 (Type Constraints)
                                                        │           │
                                                        ▼           ▼
                                                 0.1.9.12 (Integration Tests)
                                                        │
                                                        ▼
                                                 0.1.9.13 (Exit Criteria Doc)
```

## Estimated Total Time
- **Parsing tasks:** 6-8 hours
- **Code generation tasks:** 8-12 hours
- **Semantic analysis tasks:** 4-6 hours
- **Testing and documentation:** 4-5 hours
- **Total:** 22-31 hours

## Notes for Agent/Engineer

1. **`#nullable enable` is critical** — All generated C# must have this directive for proper null safety.

2. **`?.` result is ALWAYS nullable** — Even if the member returns non-nullable, the result is nullable.

3. **Type aliases are NOT emitted** — They expand at compile time; no C# declaration generated.

4. **Sharpy uses `[T]` not `<T>`** — Transform square brackets to angle brackets in code generation.

5. **Type narrowing is flow-sensitive** — Only applies within the narrowing block.

6. **`T??` is an error** — Double nullable is not meaningful.

7. **Function types → Func/Action** — `(T) -> R` → `Func<T, R>`, `(T) -> None` → `Action<T>`.

8. **Generic variance deferred** — No `out`/`in` modifiers in Phase 0.1.9.
# Phase 0.1.10: Module System - Comprehensive Task List (v2)

**Goal:** Import/export system, multi-file compilation, namespace generation, and package support.

**Prerequisites:** Phase 0.1.9 (Type System Enhancements) must be complete.

**Exit Criteria:**
- `import module` works
- `import module as alias` works
- `from module import name` works
- `from module import *` works (with warning)
- Private symbols (`_name`) not exported by `import *`
- Private symbols (`__name`) cannot be imported at all
- Multi-file compilation produces single assembly
- Namespaces generated from file paths
- `__init__.spy` marks packages and defines re-exports
- Circular import detection with clear error messages
- Import statements must be at top of file
- Symbol shadowing/collision detection

---

## Pre-Implementation Checklist

Before starting any task, check what already exists:

```bash
# Check for import-related code
grep -rn "import\|Import\|module" src/Sharpy.Compiler/

# Check for namespace handling
grep -rn "namespace\|Namespace" src/Sharpy.Compiler/

# Check for multi-file compilation
grep -rn "CompileProject\|MultiFile" src/Sharpy.Compiler/

# Check existing tests
find src -name "*.cs" -exec grep -l "Import\|Module\|Namespace" {} \;
```

---

## Task 0.1.10.1: Implement Import Statement Parsing

**Type:** 🆕 New Implementation  
**Priority:** Critical  
**Estimated Time:** 2-3 hours

📁 **Files**: `src/Sharpy.Compiler/Parser/Parser.cs`, `src/Sharpy.Compiler/Parser/Ast/Statement.cs`

### Objective
Parse all import statement forms.

### Import Syntax Forms

```python
import utils.helpers                    # Full module import
import utils.helpers as h               # Aliased module import
from utils.helpers import format_text   # Specific symbol import
from utils.helpers import func1, func2  # Multiple symbol import
from utils.helpers import *             # Wildcard import
```

### Grammar
```ebnf
import_stmt ::= 'import' module_path [ 'as' identifier ]
              | 'from' module_path 'import' import_targets

module_path ::= identifier { '.' identifier }
import_targets ::= '*'
                 | identifier { ',' identifier } [ ',' ]
```

### Actions

1. **Create Import AST nodes:**
   ```csharp
   public record ImportStatement : Statement
   {
       public List<string> ModulePath { get; init; } = new();  // e.g., ["utils", "helpers"]
       public string? Alias { get; init; }  // For "as alias"
   }

   public record FromImport : Statement
   {
       public List<string> ModulePath { get; init; } = new();
       public List<string> Names { get; init; } = new();  // Empty for "*"
       public bool IsWildcard { get; init; }
   }
   ```

2. **⚠️ Validate import position:**
   - All imports must be at the beginning of the file
   - Imports cannot appear after other statements
   
   ```python
   def func():
       pass

   import utils  # ERROR: Imports must be at top of file
   ```
   
   - Error message: "Import statements must appear at the beginning of the file, before any other statements"

### Scope Decision

- **Phase 0.1.10:** Absolute imports only
- **Deferred:** Relative imports (`.`, `..`)

---

## Task 0.1.10.2: Implement Module Resolution

**Type:** 🆕 New Implementation  
**Priority:** Critical  
**Estimated Time:** 3-4 hours

📁 **Files**: `src/Sharpy.Compiler/Semantic/ModuleResolver.cs`

### Objective
Resolve module paths to actual source files.

### Resolution Algorithm

1. **Module path to file path:**
   - `utils.helpers` → `utils/helpers.spy`
   - `mypackage.submodule` → `mypackage/submodule.spy`

2. **Search paths:**
   - Current project directory
   - Standard library paths (future)
   - External package paths (future)

3. **Handle packages:**
   - If `mypackage/` exists AND `mypackage/__init__.spy` exists → package
   - If `mypackage.spy` exists → module file

### Implementation Hints

```csharp
public class ModuleResolver
{
    private readonly List<string> _searchPaths;
    
    public string? ResolveModule(List<string> modulePath)
    {
        var relativePath = string.Join(Path.DirectorySeparatorChar.ToString(), modulePath) + ".spy";
        
        foreach (var searchPath in _searchPaths)
        {
            var fullPath = Path.Combine(searchPath, relativePath);
            if (File.Exists(fullPath))
                return fullPath;
            
            // Check for package
            var packagePath = Path.Combine(searchPath, 
                string.Join(Path.DirectorySeparatorChar.ToString(), modulePath),
                "__init__.spy");
            if (File.Exists(packagePath))
                return packagePath;
        }
        
        return null;
    }
}
```

---

## Task 0.1.10.3: Implement Import Symbol Resolution

**Type:** 🆕 New Implementation  
**Priority:** Critical  
**Estimated Time:** 3-4 hours

📁 **Files**: `src/Sharpy.Compiler/Semantic/SemanticAnalyzer.cs`, `src/Sharpy.Compiler/Semantic/SymbolTable.cs`

### Objective
Resolve imported symbols and enforce visibility rules.

### ⚠️ Export Rules

| Symbol Pattern | Exported by `import *` | Directly Importable |
|---------------|------------------------|---------------------|
| `public_func` | ✅ Yes | ✅ Yes |
| `_protected_func` | ❌ No | ✅ Yes |
| `__private_func` | ❌ No | ❌ No |

### Actions

1. **`import module`:**
   - Add module name to symbol table
   - Access via `module.symbol`

2. **`import module as alias`:**
   - Add alias to symbol table
   - Access via `alias.symbol`

3. **`from module import name`:**
   - Add `name` directly to current scope
   - Validate `name` exists in module
   - Validate `name` is not private (`__name`)

4. **`from module import *`:**
   - Import all public symbols (not starting with `_`)
   - ⚠️ Emit warning: "Wildcard import 'from utils import *' may pollute namespace. Consider importing specific symbols."

5. **⚠️ Validate private import:**
   ```python
   from utils import __private_func  # ERROR: Cannot import private symbol
   ```
   - Error message: "Cannot import private symbol '__private_func' from module 'utils'"

### Symbol Shadowing Detection

1. **Local shadows import → Error:**
   ```python
   from utils import helper
   
   def helper():  # ERROR: Shadows imported 'helper'
       pass
   ```

2. **Later import shadows earlier → Error:**
   ```python
   from module_a import func
   from module_b import func  # ERROR: 'func' already imported
   ```

3. **Import shadows builtin → Warning:**
   ```python
   from mymodule import print  # WARNING: Shadows builtin 'print'
   ```

---

## Task 0.1.10.4: Implement Multi-File Compilation

**Type:** 🆕 New Implementation  
**Priority:** Critical  
**Estimated Time:** 4-5 hours

📁 **Files**: `src/Sharpy.Compiler/Compiler.cs`, `src/Sharpy.Compiler/Project/ProjectCompiler.cs`

### Objective
Compile multiple source files into a single assembly.

### Compilation Pipeline

```
1. Discover all .spy files in project
       ↓
2. Parse all files (AST generation)
       ↓
3. Phase 1: Collect type declarations (classes, structs, enums, interfaces)
       ↓
4. Phase 2: Resolve imports and build symbol tables
       ↓
5. Phase 3: Type check all files
       ↓
6. Phase 4: Generate C# for all files
       ↓
7. Compile generated C# to assembly
```

### Actions

1. **Discover source files:**
   - Find all `.spy` files in project directory
   - Respect `.spyproj` configuration if present

2. **Parse all files:**
   - Create AST for each file
   - Track file paths for error reporting

3. **Build unified symbol table:**
   - Register all types from all files
   - Handle cross-file references

4. **Generate namespaces:**
   - File path determines namespace (see Task 0.1.10.5)

---

## Task 0.1.10.5: Implement Namespace Generation from Module Path

**Type:** 🆕 New Implementation  
**Priority:** High  
**Estimated Time:** 2-3 hours

📁 **Files**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

### Objective
Generate C# namespaces from file paths.

### ⚠️ Detailed Algorithm

1. **Project name** from `.spyproj` becomes root namespace
2. **Directory path** segments become namespace segments
3. **File name** (without `.spy`) becomes final namespace segment OR class container
4. **Apply PascalCase** transformation to each segment

### Example

```
Project: MyApp.spyproj (defines RootNamespace = "MyApp")
File: src/models/user.spy

Possible namespace structures:
Option A: MyApp.Src.Models.User
Option B: MyApp.Models (if src/ is excluded)
          - Classes inside file in this namespace
```

### Recommended Approach

```python
# File: myproject/utils/helpers.spy

def format_text(s: str) -> str:
    return s.upper()

class TextProcessor:
    pass
```

**Generated C#:**
```csharp
namespace MyProject.Utils  // Or MyProject.Utils.Helpers
{
    public static class Helpers  // Module-level container for functions
    {
        public static string FormatText(string s) => s.ToUpper();
    }
    
    public class TextProcessor { }
}
```

### Key Decisions

1. **Module-level functions** → Static class with file name
2. **Classes/structs/enums** → Directly in namespace
3. **`src/` directory** → Typically excluded from namespace (configurable)

---

## Task 0.1.10.6: Implement Circular Import Detection

**Type:** 🆕 New Implementation  
**Priority:** High  
**Estimated Time:** 3-4 hours

📁 **Files**: `src/Sharpy.Compiler/Semantic/ImportResolver.cs`

### Objective
Detect and report circular imports with clear error messages.

### Two-Phase Resolution Algorithm

**Phase 1: Type Collection**
- Parse all files
- Collect type declarations (classes, structs, enums, interfaces)
- Do NOT resolve type bodies yet
- This allows forward references

**Phase 2: Body Resolution**
- Resolve imports and type bodies
- Check for circular dependencies that require resolution

### Circular Import Categories

| Category | Example | Allowed |
|----------|---------|---------|
| Type annotation only | `field: OtherClass` | ✅ Yes |
| Default value | `field: OtherClass = OtherClass()` | ⚠️ Order dependent |
| Inheritance | `class A(B)` where B imports A | ❌ No |
| Constructor body | `super().__init__()` | ✅ Yes (runtime) |

### Detection Algorithm

```python
# module_a.spy
from module_b import ClassB
class ClassA(ClassB):  # Needs ClassB fully resolved
    pass

# module_b.spy
from module_a import ClassA
class ClassB(ClassA):  # Needs ClassA fully resolved → CYCLE
    pass
```

**Error message:** "Circular inheritance detected: ClassA inherits from ClassB, which inherits from ClassA"

### Implementation Hints

```csharp
public class ImportGraph
{
    private Dictionary<string, HashSet<string>> _dependencies = new();
    private HashSet<string> _visiting = new();
    private HashSet<string> _visited = new();
    
    public void AddDependency(string from, string to)
    {
        if (!_dependencies.ContainsKey(from))
            _dependencies[from] = new HashSet<string>();
        _dependencies[from].Add(to);
    }
    
    public List<string>? FindCycle()
    {
        foreach (var module in _dependencies.Keys)
        {
            var cycle = DFS(module, new List<string>());
            if (cycle != null)
                return cycle;
        }
        return null;
    }
    
    private List<string>? DFS(string module, List<string> path)
    {
        if (_visiting.Contains(module))
        {
            // Found cycle
            var cycleStart = path.IndexOf(module);
            return path.Skip(cycleStart).Append(module).ToList();
        }
        
        if (_visited.Contains(module))
            return null;
        
        _visiting.Add(module);
        path.Add(module);
        
        if (_dependencies.TryGetValue(module, out var deps))
        {
            foreach (var dep in deps)
            {
                var cycle = DFS(dep, path);
                if (cycle != null)
                    return cycle;
            }
        }
        
        path.RemoveAt(path.Count - 1);
        _visiting.Remove(module);
        _visited.Add(module);
        
        return null;
    }
}
```

---

## Task 0.1.10.7: Implement Import Code Generation

**Type:** 🆕 New Implementation  
**Priority:** High  
**Estimated Time:** 2-3 hours

📁 **Files**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

### Objective
Generate C# `using` statements from Sharpy imports.

### Import Mapping

1. **`import module`:**
   ```python
   import utils.helpers
   # Usage: utils.helpers.function()
   ```
   
   **C# Option A:** Fully qualified calls (no using)
   ```csharp
   MyProject.Utils.Helpers.Function();
   ```
   
   **C# Option B:** Using alias
   ```csharp
   using utils_helpers = MyProject.Utils.Helpers;
   // Usage: utils_helpers.Function();
   ```

2. **`import module as alias`:**
   ```python
   import utils.helpers as h
   # Usage: h.function()
   ```
   
   **C#:**
   ```csharp
   using h = MyProject.Utils.Helpers;
   // Usage: h.Function();
   ```

3. **`from module import name`:**
   ```python
   from utils.helpers import format_text
   # Usage: format_text()
   ```
   
   **C#:**
   ```csharp
   using static MyProject.Utils.Helpers;
   // Usage: FormatText();
   ```

4. **`from module import *`:**
   ```python
   from utils.helpers import *
   ```
   
   **C#:**
   ```csharp
   using static MyProject.Utils.Helpers;
   ```

### Decision (Per Axiom 1 - .NET Runtime)

- Use `using static` for `from ... import` of functions
- Use `using namespace` for class imports
- Use `using alias = ...` for aliased imports

---

## Task 0.1.10.8: Implement `__init__.spy` Support

**Type:** 🆕 New Implementation  
**Priority:** High  
**Estimated Time:** 3-4 hours

📁 **Files**: `src/Sharpy.Compiler/Semantic/PackageResolver.cs`

### Objective
Support package initialization and re-exports.

### `__init__.spy` Purpose

1. **Marks directory as a package**
2. **Defines package-level exports (re-exports)**
3. **Executes when package is imported**

### Example Structure

```
mypackage/
├── __init__.spy      # Package initialization
├── module_a.spy
└── module_b.spy
```

### `__init__.spy` Content

```python
# mypackage/__init__.spy
from mypackage.module_a import ClassA, func_a
from mypackage.module_b import ClassB

# Now these are available at package level:
# from mypackage import ClassA, ClassB, func_a
```

### Behavior

1. **When `import mypackage`:**
   - Load and execute `__init__.spy`
   - Symbols re-exported become package-level symbols

2. **Direct submodule access bypasses `__init__.spy`:**
   - `from mypackage.module_a import ...` works directly

3. **Module initialization order:**
   - Dependencies loaded first
   - Module-level statements execute in file order
   - `__init__.spy` executes when package imported

---

## Task 0.1.10.9: Implement Project File Support (`.spyproj`)

**Type:** 🆕 New Implementation  
**Priority:** Medium  
**Estimated Time:** 2-3 hours

📁 **Files**: `src/Sharpy.Compiler/Project/SpyProject.cs`

### Objective
Parse and use project configuration files.

### `.spyproj` Format

```xml
<Project>
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net6.0</TargetFramework>
    <RootNamespace>MyProject</RootNamespace>
    <EntryPoint>main.spy</EntryPoint>
  </PropertyGroup>
  <ItemGroup>
    <SourceFile Include="**/*.spy" />
    <Exclude Include="tests/**" />
  </ItemGroup>
</Project>
```

### Properties

- `OutputType` — `Exe` or `Library`
- `TargetFramework` — .NET framework version
- `RootNamespace` — Root namespace for all types
- `EntryPoint` — Main entry point file

### Scope Decision

- **Phase 0.1.10:** Basic properties (OutputType, RootNamespace, EntryPoint)
- **Deferred:** External assembly references (System imports → Phase 0.1.12)
- **Deferred:** Project references

---

## Task 0.1.10.10: Create Test Infrastructure for Multi-File Compilation

**Type:** 🆕 New Implementation  
**Priority:** High  
**Estimated Time:** 2-3 hours

📁 **Files**: `src/Sharpy.Compiler.Tests/TestHelpers/ProjectCompilationHelper.cs`

### Objective
Create test infrastructure for multi-file compilation tests.

### Required Test Helpers

```csharp
public class ProjectCompilationHelper
{
    private readonly string _tempDir;
    
    public ProjectCompilationHelper()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
    }
    
    public void WriteFile(string relativePath, string content)
    {
        var fullPath = Path.Combine(_tempDir, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
    }
    
    public CompileResult CompileProject(string entryPoint)
    {
        // 1. Find all .spy files in directory
        // 2. Parse all files
        // 3. Resolve imports across files
        // 4. Generate C# for all files
        // 5. Compile C# assembly
        return result;
    }
    
    public CompileResult CompileProject(string entryPoint, string[] sourceFiles)
    {
        // Explicit file list compilation
    }
    
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }
}
```

---

## Task 0.1.10.11: Create Phase 0.1.10 Integration Tests

**Type:** 🆕 New Implementation  
**Priority:** Critical  
**Estimated Time:** 4-5 hours

📁 **Files**: `src/Sharpy.Compiler.Tests/Integration/Phase0110IntegrationTests.cs`

### Test Cases

```csharp
[Fact]
public void BasicImport_Works()
{
    _helper.WriteFile("utils.spy", @"
def helper() -> str:
    return 'help'
");
    _helper.WriteFile("main.spy", @"
import utils

result = utils.helper()
");
    var result = _helper.CompileProject("main.spy");
    Assert.True(result.Success);
}

[Fact]
public void ImportAsAlias_Works()
{
    _helper.WriteFile("utils/helpers.spy", @"
def format(s: str) -> str:
    return s.upper()
");
    _helper.WriteFile("main.spy", @"
import utils.helpers as h

result = h.format('hello')
");
    var result = _helper.CompileProject("main.spy");
    Assert.True(result.Success);
}

[Fact]
public void FromImport_Works()
{
    _helper.WriteFile("math_utils.spy", @"
def square(x: int) -> int:
    return x * x

def cube(x: int) -> int:
    return x * x * x
");
    _helper.WriteFile("main.spy", @"
from math_utils import square, cube

a = square(5)
b = cube(3)
");
    var result = _helper.CompileProject("main.spy");
    Assert.True(result.Success);
}

[Fact]
public void WildcardImport_Works()
{
    _helper.WriteFile("utils.spy", @"
def public_func() -> str:
    return 'public'

def _protected_func() -> str:
    return 'protected'

def __private_func() -> str:
    return 'private'
");
    _helper.WriteFile("main.spy", @"
from utils import *

result = public_func()
");
    var result = _helper.CompileProject("main.spy");
    Assert.True(result.Success);
    Assert.Contains("warning", result.Warnings.ToLower());  // Should warn about wildcard
}

[Fact]
public void WildcardImport_ExcludesProtected()
{
    _helper.WriteFile("utils.spy", @"
def public_func() -> str:
    return 'public'

def _protected_func() -> str:
    return 'protected'
");
    _helper.WriteFile("main.spy", @"
from utils import *

result = _protected_func()
");
    var result = _helper.CompileProject("main.spy");
    Assert.False(result.Success);  // _protected_func not imported by *
}

[Fact]
public void PrivateSymbol_CannotBeImported()
{
    _helper.WriteFile("utils.spy", @"
def __private() -> str:
    return 'private'
");
    _helper.WriteFile("main.spy", @"
from utils import __private
");
    var result = _helper.CompileProject("main.spy");
    Assert.False(result.Success);
    Assert.Contains("private", result.Error.ToLower());
}

[Fact]
public void CircularImport_ProducesError()
{
    _helper.WriteFile("module_a.spy", @"
from module_b import ClassB

class ClassA(ClassB):
    pass
");
    _helper.WriteFile("module_b.spy", @"
from module_a import ClassA

class ClassB(ClassA):
    pass
");
    var result = _helper.CompileProject("module_a.spy");
    Assert.False(result.Success);
    Assert.Contains("circular", result.Error.ToLower());
}

[Fact]
public void ImportNotAtTop_ProducesError()
{
    _helper.WriteFile("main.spy", @"
def func() -> int:
    return 42

import utils
");
    var result = _helper.CompileProject("main.spy");
    Assert.False(result.Success);
    Assert.Contains("top", result.Error.ToLower());
}

[Fact]
public void SymbolShadowing_Import_ProducesError()
{
    _helper.WriteFile("utils.spy", @"
def helper() -> str:
    return 'help'
");
    _helper.WriteFile("main.spy", @"
from utils import helper

def helper() -> str:
    return 'local'
");
    var result = _helper.CompileProject("main.spy");
    Assert.False(result.Success);
    Assert.Contains("shadow", result.Error.ToLower());
}

[Fact]
public void SymbolShadowing_DoubleImport_ProducesError()
{
    _helper.WriteFile("module_a.spy", @"
def func() -> str:
    return 'a'
");
    _helper.WriteFile("module_b.spy", @"
def func() -> str:
    return 'b'
");
    _helper.WriteFile("main.spy", @"
from module_a import func
from module_b import func
");
    var result = _helper.CompileProject("main.spy");
    Assert.False(result.Success);
    Assert.Contains("already imported", result.Error.ToLower());
}

[Fact]
public void PackageWithInit_Works()
{
    _helper.WriteFile("mypackage/__init__.spy", @"
from mypackage.core import Helper
");
    _helper.WriteFile("mypackage/core.spy", @"
class Helper:
    def help(self) -> str:
        return 'help'
");
    _helper.WriteFile("main.spy", @"
from mypackage import Helper

h = Helper()
");
    var result = _helper.CompileProject("main.spy");
    Assert.True(result.Success);
}

[Fact]
public void NamespaceGeneration_CorrectPascalCase()
{
    _helper.WriteFile("utils/string_helpers.spy", @"
def to_upper(s: str) -> str:
    return s.upper()
");
    _helper.WriteFile("main.spy", @"
from utils.string_helpers import to_upper

result = to_upper('hello')
");
    var result = _helper.CompileProject("main.spy");
    Assert.True(result.Success);
    // Generated namespace should be Utils.StringHelpers or similar
}

[Fact]
public void MultipleFiles_CrossReference_Works()
{
    _helper.WriteFile("models.spy", @"
class User:
    name: str
    
    def __init__(self, name: str):
        self.name = name
");
    _helper.WriteFile("services.spy", @"
from models import User

def create_user(name: str) -> User:
    return User(name)
");
    _helper.WriteFile("main.spy", @"
from services import create_user

user = create_user('Alice')
");
    var result = _helper.CompileProject("main.spy");
    Assert.True(result.Success);
}
```

---

## Task 0.1.10.12: Document Phase 0.1.10 Exit Criteria Verification

**Type:** 📝 Documentation  
**Priority:** High  
**Estimated Time:** 30 minutes

📁 **Files**: `docs/implementation/phase_0_1_10_complete.md`

### Exit Criteria Checklist

| Criterion | Test | Status |
|-----------|------|--------|
| Basic import | `BasicImport_Works` | [ ] |
| Import as alias | `ImportAsAlias_Works` | [ ] |
| From import | `FromImport_Works` | [ ] |
| Wildcard import | `WildcardImport_Works` | [ ] |
| Protected exclusion | `WildcardImport_ExcludesProtected` | [ ] |
| Private import blocked | `PrivateSymbol_CannotBeImported` | [ ] |
| Circular import detection | `CircularImport_ProducesError` | [ ] |
| Import position validation | `ImportNotAtTop_ProducesError` | [ ] |
| Local shadows import | `SymbolShadowing_Import_ProducesError` | [ ] |
| Double import collision | `SymbolShadowing_DoubleImport_ProducesError` | [ ] |
| Package with __init__ | `PackageWithInit_Works` | [ ] |
| Namespace generation | `NamespaceGeneration_CorrectPascalCase` | [ ] |
| Multi-file cross-reference | `MultipleFiles_CrossReference_Works` | [ ] |

---

## Summary: Task Dependencies

```
0.1.10.1 (Import Parsing) ──────────────────────────────┐
                                                        │
0.1.10.2 (Module Resolution) ───────────────────────────┼──► 0.1.10.3 (Symbol Resolution)
                                                        │    0.1.10.4 (Multi-File Compilation)
                                                        │           │
                                                        │           ▼
                                                        │    0.1.10.5 (Namespace Generation)
                                                        │    0.1.10.6 (Circular Detection)
                                                        │    0.1.10.7 (Import CodeGen)
                                                        │    0.1.10.8 (__init__.spy)
                                                        │    0.1.10.9 (.spyproj)
                                                        │           │
                                                        ▼           ▼
                                                 0.1.10.10 (Test Infrastructure)
                                                        │
                                                        ▼
                                                 0.1.10.11 (Integration Tests)
                                                        │
                                                        ▼
                                                 0.1.10.12 (Exit Criteria Doc)
```

## Estimated Total Time
- **Parsing/Resolution tasks:** 8-12 hours
- **Multi-file compilation:** 8-10 hours
- **Code generation tasks:** 6-8 hours
- **Testing infrastructure:** 4-6 hours
- **Testing and documentation:** 5-6 hours
- **Total:** 31-42 hours

## Notes for Agent/Engineer

1. **Imports must be at file top** — Validate position strictly.

2. **`__name` is private, not importable** — Single underscore is protected (not in `*`), double underscore is private.

3. **Circular imports are complex** — Two-phase resolution allows type annotations but not inheritance cycles.

4. **Wildcard import emits warning** — Good practice to import specific symbols.

5. **Module-level functions → static class** — Named after file, contains module functions.

6. **`src/` typically excluded from namespace** — Configurable in project file.

7. **External assembly references deferred** — System imports in Phase 0.1.12.

8. **Relative imports deferred** — Only absolute imports in Phase 0.1.10.

9. **Module initialization order matters** — Dependencies first, then module-level statements.
