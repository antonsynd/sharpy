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
