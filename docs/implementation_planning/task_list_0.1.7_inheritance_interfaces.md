# Phase 0.1.7: Inheritance & Interfaces - Detailed Task List

**Goal:** Class inheritance, abstract classes, and interfaces.

**Prerequisites:** Phase 0.1.6 (Classes) must be complete.

**Exit Criteria (from spec):**
- Single inheritance works
- `super()` calls parent constructor/methods (only in allowed contexts)
- `super()` errors in regular methods and free functions
- Abstract classes cannot be instantiated
- Abstract methods must be overridden
- Interfaces define contracts
- Interface implementations don't require `@override`
- Multiple interfaces supported
- Decorator modifiers apply correctly

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

### Objective
Verify that class inheritance (`class Dog(Animal):`) is parsed correctly.

### Files to Check
- `src/Sharpy.Compiler/Parser/Ast/Statement.cs` (ClassDef)
- `src/Sharpy.Compiler/Parser/Parser.cs`

### Actions

1. **Verify `ClassDef.BaseClasses` property exists:**
   - [ ] Should be `List<TypeAnnotation>` or similar
   - [ ] Captures base class for single inheritance
   - [ ] Can hold multiple entries (for interfaces)

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
   - [ ] `Dog`'s BaseClasses contains `Animal`
   - [ ] `super()` parsed as special expression

3. **Verify multiple base parsing (for interfaces):**
   ```python
   class MyClass(BaseClass, IInterface1, IInterface2):
       pass
   ```
   - [ ] All three entries captured in BaseClasses

### Verification Commands
```bash
# Check ClassDef structure
grep -A 20 "record ClassDef" src/Sharpy.Compiler/Parser/Ast/Statement.cs

# Check for base class parsing
grep -n "BaseClass\|bases\|inherit" src/Sharpy.Compiler/Parser/Parser.cs
```

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

### Objective
Parse `super()` calls and represent them in the AST.

### Spec Requirements

`super()` can ONLY be called in:
- `__init__` to call `super().__init__(...)`
- Dunder methods to call `super().__dunder__(...)`
- `@override` methods to call `super().method()`

Calling `super()` in regular methods or free functions is a **compile error**.

### Valid Forms (Grammar)
```ebnf
super_call ::= 'super' '(' ')' '.' identifier '(' [ arguments ] ')'
```

### Files to Create/Modify
- `src/Sharpy.Compiler/Parser/Ast/Expression.cs` — Add `SuperCall` AST node
- `src/Sharpy.Compiler/Parser/Parser.cs` — Parse `super()` expressions

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
   - [ ] Recognize `super` keyword
   - [ ] Expect `()` immediately after
   - [ ] Expect `.` method access
   - [ ] Parse method name and arguments

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
**Estimated Time:** 2-3 hours

### Objective
Validate that `super()` is only used in allowed contexts.

### Files to Modify
- `src/Sharpy.Compiler/Semantic/SemanticAnalyzer.cs`

### Validation Rules

| Context | Allowed `super()` Calls |
|---------|------------------------|
| `__init__` | `super().__init__(...)` only |
| `@override` method | `super().same_method_name(...)` |
| Dunder method | `super().__same_dunder__(...)` |
| Regular method | ❌ ERROR |
| Free function | ❌ ERROR |
| Module-level code | ❌ ERROR |

### Actions

1. **Track context during analysis:**
   - [ ] `isInInit` flag
   - [ ] `isInOverrideMethod` flag
   - [ ] `currentMethodName` for dunder check

2. **Validate `super()` usage:**
   ```csharp
   private void ValidateSuperCall(SuperCall superCall)
   {
       if (_currentClass == null)
       {
           Error("super() can only be used inside a class");
           return;
       }
       
       if (_currentMethod == null)
       {
           Error("super() can only be used inside a method");
           return;
       }
       
       if (_currentMethod.Name == "__init__")
       {
           if (superCall.MethodName != "__init__")
               Error("In __init__, super() can only call __init__");
       }
       else if (IsDunder(_currentMethod.Name))
       {
           if (superCall.MethodName != _currentMethod.Name)
               Error($"In {_currentMethod.Name}, super() can only call {_currentMethod.Name}");
       }
       else if (HasOverrideDecorator(_currentMethod))
       {
           // OK - can call parent method
       }
       else
       {
           Error("super() can only be used in __init__, dunder methods, or @override methods");
       }
   }
   ```

3. **Test error cases:**
   ```python
   class Bad:
       def regular_method(self):
           super().something()  # ERROR
   
   def free_function():
       super().__init__()  # ERROR
   ```

### Verification Tests
```python
# Should pass
class Parent:
    @virtual
    def speak(self) -> str:
        return "Parent"

class Child(Parent):
    @override
    def speak(self) -> str:
        base = super().speak()
        return f"Child + {base}"

# Should error
class BadClass:
    def not_override(self):
        super().something()  # ERROR: not in override context
```

---

## Task 0.1.7.4: Implement Inheritance Code Generation

**Type:** ⚠️ Likely Implementation Needed  
**Priority:** Critical  
**Estimated Time:** 2-3 hours

### Objective
Generate C# classes with proper inheritance syntax.

### Files to Modify
- `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

### Actions

1. **Generate base class specification:**
   ```python
   class Dog(Animal):
       pass
   ```
   Generates:
   ```csharp
   public class Dog : Animal
   {
   }
   ```

2. **Generate `base()` calls from `super()`:**
   ```python
   def __init__(self, name: str, breed: str):
       super().__init__(name)
       self.breed = breed
   ```
   Generates:
   ```csharp
   public Dog(string name, string breed) : base(name)
   {
       Breed = breed;
   }
   ```
   
   **Note:** When `super().__init__()` is the first statement in `__init__`, it should become a constructor initializer (`: base(...)`), not an in-body call.

3. **Generate `base.Method()` calls:**
   ```python
   @override
   def speak(self) -> str:
       return super().speak() + " bark"
   ```
   Generates:
   ```csharp
   public override string Speak()
   {
       return base.Speak() + " bark";
   }
   ```

### Implementation Hints

```csharp
private ClassDeclarationSyntax GenerateClass(ClassDef classDef)
{
    var classDecl = ClassDeclaration(
        Identifier(NameMangler.ToPascalCase(classDef.Name)));
    
    // Add base class if present
    if (classDef.BaseClasses.Count > 0)
    {
        var baseTypes = classDef.BaseClasses
            .Select(b => SimpleBaseType(IdentifierName(
                NameMangler.ToPascalCase(b.Name))));
        classDecl = classDecl.WithBaseList(
            BaseList(SeparatedList<BaseTypeSyntax>(baseTypes)));
    }
    
    // Add members...
    return classDecl;
}

private ExpressionSyntax GenerateSuperCall(SuperCall superCall)
{
    // base.MethodName(args)
    return InvocationExpression(
        MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            BaseExpression(),
            IdentifierName(NameMangler.ToPascalCase(superCall.MethodName))))
        .WithArgumentList(ArgumentList(
            SeparatedList(superCall.Arguments.Select(a => 
                Argument(GenerateExpression(a))))));
}
```

---

## Task 0.1.7.5: Implement Decorator Parsing and AST

**Type:** 🔍 Status Check / Implementation  
**Priority:** Critical  
**Estimated Time:** 1-2 hours

### Objective
Ensure decorators (`@virtual`, `@override`, `@abstract`, `@final`) are parsed.

### Files to Check/Modify
- `src/Sharpy.Compiler/Parser/Ast/Statement.cs` (Decorator node)
- `src/Sharpy.Compiler/Parser/Parser.cs`

### Required Decorators for Phase 0.1.7

| Decorator | Purpose | C# Equivalent |
|-----------|---------|---------------|
| `@virtual` | Method can be overridden | `virtual` |
| `@override` | Overrides base method | `override` |
| `@abstract` | Must be overridden (on class or method) | `abstract` |
| `@final` | Cannot be overridden/inherited | `sealed` |
| `@private` | Private access | `private` |
| `@protected` | Protected access | `protected` |
| `@internal` | Internal access | `internal` |

### Actions

1. **Verify `Decorator` AST node:**
   ```csharp
   public record Decorator : Node
   {
       public string Name { get; init; } = "";
       // No arguments for built-in decorators
   }
   ```

2. **Test decorator parsing:**
   ```python
   @abstract
   class Shape:
       @abstract
       def area(self) -> float:
           ...
   
   class Circle(Shape):
       @override
       def area(self) -> float:
           return 3.14159 * self.radius ** 2
   ```

3. **Verify decorators are captured:**
   - [ ] On class definitions
   - [ ] On method definitions

### Verification Commands
```bash
# Check decorator parsing
grep -rn "ParseDecorator\|Decorator" src/Sharpy.Compiler/Parser/

# Run decorator tests
dotnet test --filter "Decorator" src/Sharpy.Compiler.Tests/
```

---

## Task 0.1.7.6: Implement Decorator Code Generation

**Type:** ⚠️ Likely Implementation Needed  
**Priority:** High  
**Estimated Time:** 2 hours

### Objective
Translate Sharpy decorators to C# modifiers.

### Files to Modify
- `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

### Actions

1. **Map decorators to C# modifiers:**

   ```csharp
   private SyntaxTokenList GetModifiersFromDecorators(List<Decorator> decorators, bool isClass = false)
   {
       var tokens = new List<SyntaxToken>();
       bool hasAccessModifier = false;
       
       foreach (var dec in decorators)
       {
           switch (dec.Name)
           {
               case "public":
                   tokens.Add(Token(SyntaxKind.PublicKeyword));
                   hasAccessModifier = true;
                   break;
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
               case "virtual":
                   tokens.Add(Token(SyntaxKind.VirtualKeyword));
                   break;
               case "override":
                   tokens.Add(Token(SyntaxKind.OverrideKeyword));
                   break;
               case "abstract":
                   tokens.Add(Token(SyntaxKind.AbstractKeyword));
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
           }
       }
       
       if (!hasAccessModifier)
           tokens.Insert(0, Token(SyntaxKind.PublicKeyword));
       
       return TokenList(tokens);
   }
   ```

2. **Generate abstract class:**
   ```python
   @abstract
   class Shape:
       @abstract
       def area(self) -> float:
           ...
   ```
   Generates:
   ```csharp
   public abstract class Shape
   {
       public abstract double Area();
   }
   ```

3. **Generate virtual/override methods:**
   ```python
   class Animal:
       @virtual
       def speak(self) -> str:
           return "..."
   
   class Dog(Animal):
       @override
       def speak(self) -> str:
           return "Woof!"
   ```
   Generates:
   ```csharp
   public class Animal
   {
       public virtual string Speak() => "...";
   }
   
   public class Dog : Animal
   {
       public override string Speak() => "Woof!";
   }
   ```

---

## Task 0.1.7.7: Implement Abstract Class/Method Validation

**Type:** 🆕 New Implementation  
**Priority:** High  
**Estimated Time:** 2 hours

### Objective
Validate abstract class rules during semantic analysis.

### Files to Modify
- `src/Sharpy.Compiler/Semantic/SemanticAnalyzer.cs`

### Validation Rules

1. **Abstract classes cannot be instantiated:**
   ```python
   @abstract
   class Shape:
       pass
   
   s = Shape()  # ERROR: Cannot instantiate abstract class
   ```

2. **Abstract methods must be in abstract classes:**
   ```python
   class NotAbstract:  # ERROR: Class must be @abstract
       @abstract
       def foo(self):
           ...
   ```

3. **Abstract methods must be overridden:**
   ```python
   @abstract
   class Shape:
       @abstract
       def area(self) -> float:
           ...
   
   class Circle(Shape):  # ERROR: Does not implement 'area'
       pass
   ```

4. **Abstract methods have no body (use `...`):**
   ```python
   @abstract
   def area(self) -> float:
       ...  # OK
   
   @abstract
   def area(self) -> float:
       return 0  # ERROR: Abstract method cannot have body
   ```

### Implementation Hints

```csharp
private void ValidateClass(ClassDef classDef)
{
    bool isAbstract = classDef.Decorators.Any(d => d.Name == "abstract");
    var abstractMethods = new List<string>();
    
    foreach (var member in classDef.Body)
    {
        if (member is FunctionDef method)
        {
            bool methodIsAbstract = method.Decorators.Any(d => d.Name == "abstract");
            
            if (methodIsAbstract)
            {
                if (!isAbstract)
                    Error($"Abstract method '{method.Name}' must be in an abstract class");
                
                abstractMethods.Add(method.Name);
                
                // Check body is just '...'
                if (!IsEllipsisBody(method.Body))
                    Error($"Abstract method '{method.Name}' cannot have a body");
            }
        }
    }
    
    // Check base class abstract methods are implemented
    if (!isAbstract && classDef.BaseClasses.Count > 0)
    {
        var baseClass = ResolveType(classDef.BaseClasses[0]);
        foreach (var abstractMethod in GetAbstractMethods(baseClass))
        {
            if (!HasImplementation(classDef, abstractMethod))
                Error($"Class '{classDef.Name}' does not implement abstract method '{abstractMethod}'");
        }
    }
}
```

---

## Task 0.1.7.8: Implement Interface Definition Parsing and AST

**Type:** ⚠️ Likely Implementation Needed  
**Priority:** High  
**Estimated Time:** 2 hours

### Objective
Parse interface definitions and create AST nodes.

### Sharpy Interface Syntax
```python
interface IDrawable:
    def draw(self) -> None:
        ...

interface ISerializable:
    def serialize(self) -> str:
        ...
```

### Files to Create/Modify
- `src/Sharpy.Compiler/Parser/Ast/Statement.cs` — Add `InterfaceDef`
- `src/Sharpy.Compiler/Parser/Parser.cs` — Parse interfaces

### Actions

1. **Create `InterfaceDef` AST node:**
   ```csharp
   public record InterfaceDef : Statement
   {
       public string Name { get; init; } = "";
       public List<TypeAnnotation> BaseInterfaces { get; init; } = new();
       public List<Statement> Body { get; init; } = new();  // Methods only
       // Source location...
   }
   ```

2. **Implement interface parsing:**
   - [ ] Recognize `interface` keyword
   - [ ] Parse interface name (should start with `I` by convention)
   - [ ] Parse optional base interfaces
   - [ ] Parse method signatures (no implementation bodies)

3. **Validate interface structure:**
   - [ ] Only method signatures allowed
   - [ ] No fields (unless computed properties in future)
   - [ ] Methods have `...` body (abstract by default)

### Verification Tests
```python
interface IDrawable:
    def draw(self) -> None:
        ...

interface IResizable(IDrawable):
    def resize(self, width: int, height: int) -> None:
        ...
```

---

## Task 0.1.7.9: Implement Interface Code Generation

**Type:** 🆕 New Implementation  
**Priority:** High  
**Estimated Time:** 2 hours

### Objective
Generate C# interfaces from Sharpy interface definitions.

### Files to Modify
- `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

### Code Generation Example

**Input:**
```python
interface IDrawable:
    def draw(self) -> None:
        ...

interface IResizable:
    def resize(self, width: int, height: int) -> None:
        ...
```

**Output:**
```csharp
public interface IDrawable
{
    void Draw();
}

public interface IResizable
{
    void Resize(int width, int height);
}
```

### Actions

1. **Generate interface declaration:**
   ```csharp
   private InterfaceDeclarationSyntax GenerateInterface(InterfaceDef iface)
   {
       var members = iface.Body
           .OfType<FunctionDef>()
           .Select(GenerateInterfaceMethod);
       
       return InterfaceDeclaration(Identifier(iface.Name))
           .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
           .WithMembers(List<MemberDeclarationSyntax>(members));
   }
   
   private MethodDeclarationSyntax GenerateInterfaceMethod(FunctionDef method)
   {
       // Skip 'self' parameter
       var parameters = method.Parameters
           .Where(p => p.Name != "self")
           .Select(MapParameter);
       
       var returnType = method.ReturnType != null
           ? _typeMapper.MapType(method.ReturnType)
           : PredefinedType(Token(SyntaxKind.VoidKeyword));
       
       return MethodDeclaration(returnType, 
               Identifier(NameMangler.ToPascalCase(method.Name)))
           .WithParameterList(ParameterList(SeparatedList(parameters)))
           .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
   }
   ```

---

## Task 0.1.7.10: Implement Interface Implementation Code Generation

**Type:** 🆕 New Implementation  
**Priority:** High  
**Estimated Time:** 2 hours

### Objective
Generate correct C# for classes implementing interfaces.

### Key Rule from Spec
> Interface method implementations do NOT use `@override`. The `@override` decorator is only for overriding virtual/abstract methods from base classes.

### Files to Modify
- `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`
- `src/Sharpy.Compiler/Semantic/SemanticAnalyzer.cs`

### Code Generation Example

**Input:**
```python
interface IDrawable:
    def draw(self) -> None:
        ...

class Circle(IDrawable):
    def draw(self) -> None:
        pass  # drawing logic
```

**Output:**
```csharp
public interface IDrawable
{
    void Draw();
}

public class Circle : IDrawable
{
    public void Draw()
    {
        // drawing logic
    }
}
```

### Multiple Interface Implementation

**Input:**
```python
interface IDrawable:
    def draw(self) -> None:
        ...

interface ISerializable:
    def serialize(self) -> str:
        ...

class Data(IDrawable, ISerializable):
    def draw(self) -> None:
        pass
    
    def serialize(self) -> str:
        return "{}"
```

**Output:**
```csharp
public class Data : IDrawable, ISerializable
{
    public void Draw()
    {
    }
    
    public string Serialize()
    {
        return "{}";
    }
}
```

### Semantic Validation

1. **All interface methods must be implemented:**
   ```python
   class Incomplete(IDrawable):  # ERROR: Does not implement 'draw'
       pass
   ```

2. **Method signatures must match:**
   ```python
   class Wrong(IDrawable):
       def draw(self) -> str:  # ERROR: Return type mismatch
           return ""
   ```

---

## Task 0.1.7.11: Create Phase 0.1.7 Integration Tests

**Type:** 🆕 New Implementation  
**Priority:** Critical  
**Estimated Time:** 3-4 hours

### Objective
Create comprehensive end-to-end tests for inheritance and interfaces.

### File to Create
`src/Sharpy.Compiler.Tests/Integration/Phase017IntegrationTests.cs`

### Test Cases

```csharp
#region Basic Inheritance

[Fact]
public void SingleInheritance_CompilesAndRuns()
{
    var source = @"
class Animal:
    name: str
    
    def __init__(self, name: str):
        self.name = name

class Dog(Animal):
    breed: str
    
    def __init__(self, name: str, breed: str):
        super().__init__(name)
        self.breed = breed

d = Dog('Buddy', 'Golden Retriever')
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
result = d.speak()  # Should be 'Woof!'
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

[Fact]
public void SuperMethodCall_Works()
{
    var source = @"
class Parent:
    @virtual
    def greet(self) -> str:
        return 'Hello'

class Child(Parent):
    @override
    def greet(self) -> str:
        base_greeting = super().greet()
        return base_greeting + ' World'

c = Child()
result = c.greet()  # Should be 'Hello World'
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

#endregion

#region Abstract Classes

[Fact]
public void AbstractClass_CannotInstantiate()
{
    var source = @"
@abstract
class Shape:
    @abstract
    def area(self) -> float:
        ...

s = Shape()  # Should produce error
";
    var result = CompileAndExecute(source);
    Assert.False(result.Success);
    Assert.Contains("abstract", result.CompilationErrors.First().ToLower());
}

[Fact]
public void AbstractMethod_MustBeOverridden()
{
    var source = @"
@abstract
class Shape:
    @abstract
    def area(self) -> float:
        ...

class Circle(Shape):
    radius: float
    
    def __init__(self, radius: float):
        self.radius = radius
    
    @override
    def area(self) -> float:
        return 3.14159 * self.radius * self.radius

c = Circle(5.0)
a = c.area()  # Should be ~78.54
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

[Fact]
public void MissingAbstractImplementation_Error()
{
    var source = @"
@abstract
class Shape:
    @abstract
    def area(self) -> float:
        ...

class Circle(Shape):  # Missing area() implementation
    radius: float
";
    var result = CompileAndExecute(source);
    Assert.False(result.Success);
}

#endregion

#region Interfaces

[Fact]
public void Interface_SimpleImplementation()
{
    var source = @"
interface IGreeter:
    def greet(self) -> str:
        ...

class FriendlyGreeter(IGreeter):
    def greet(self) -> str:
        return 'Hello!'

g = FriendlyGreeter()
result = g.greet()
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

[Fact]
public void Interface_NoOverrideRequired()
{
    var source = @"
interface IDrawable:
    def draw(self) -> None:
        ...

class Circle(IDrawable):
    # Note: NO @override decorator
    def draw(self) -> None:
        pass

c = Circle()
c.draw()
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

[Fact]
public void MultipleInterfaces_Implementation()
{
    var source = @"
interface IDrawable:
    def draw(self) -> None:
        ...

interface ISerializable:
    def serialize(self) -> str:
        ...

class Data(IDrawable, ISerializable):
    def draw(self) -> None:
        pass
    
    def serialize(self) -> str:
        return '{}'

d = Data()
d.draw()
s = d.serialize()
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

[Fact]
public void ClassWithBaseAndInterface()
{
    var source = @"
interface IComparable:
    def compare_to(self, other: object) -> int:
        ...

class Entity:
    id: int
    
    def __init__(self, id: int):
        self.id = id

class User(Entity, IComparable):
    def __init__(self, id: int):
        super().__init__(id)
    
    def compare_to(self, other: object) -> int:
        return 0

u = User(1)
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

#endregion

#region super() Validation

[Fact]
public void SuperInRegularMethod_Error()
{
    var source = @"
class Parent:
    def foo(self) -> None:
        pass

class Child(Parent):
    def bar(self) -> None:
        super().foo()  # ERROR: not in override context
";
    var result = CompileAndExecute(source);
    Assert.False(result.Success);
}

[Fact]
public void SuperInFreeFunction_Error()
{
    var source = @"
def bad_function():
    super().__init__()  # ERROR: not in class
";
    var result = CompileAndExecute(source);
    Assert.False(result.Success);
}

[Fact]
public void SuperInInit_Valid()
{
    var source = @"
class Parent:
    x: int
    
    def __init__(self, x: int):
        self.x = x

class Child(Parent):
    y: int
    
    def __init__(self, x: int, y: int):
        super().__init__(x)
        self.y = y

c = Child(1, 2)
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}

#endregion

#region Final Classes and Methods

[Fact]
public void FinalClass_CannotInherit()
{
    var source = @"
@final
class Sealed:
    pass

class Derived(Sealed):  # ERROR: Cannot inherit from sealed class
    pass
";
    var result = CompileAndExecute(source);
    Assert.False(result.Success);
}

[Fact]
public void FinalMethod_CannotOverride()
{
    var source = @"
class Parent:
    @final
    @virtual
    def locked(self) -> None:
        pass

class Child(Parent):
    @override
    def locked(self) -> None:  # ERROR: Cannot override sealed method
        pass
";
    var result = CompileAndExecute(source);
    Assert.False(result.Success);
}

#endregion
```

---

## Task 0.1.7.12: Document Phase 0.1.7 Exit Criteria Verification

**Type:** 📝 Documentation  
**Priority:** High  
**Estimated Time:** 30 minutes

### Exit Criteria Checklist

| Criterion | Test | Status |
|-----------|------|--------|
| Single inheritance works | `SingleInheritance_CompilesAndRuns` | [ ] |
| `super()` calls parent constructor/methods | `SuperMethodCall_Works` | [ ] |
| `super()` errors in regular methods | `SuperInRegularMethod_Error` | [ ] |
| Abstract classes cannot be instantiated | `AbstractClass_CannotInstantiate` | [ ] |
| Abstract methods must be overridden | `AbstractMethod_MustBeOverridden` | [ ] |
| Interfaces define contracts | `Interface_SimpleImplementation` | [ ] |
| Interface implementations don't require `@override` | `Interface_NoOverrideRequired` | [ ] |
| Multiple interfaces supported | `MultipleInterfaces_Implementation` | [ ] |
| Decorator modifiers apply correctly | `VirtualOverride_Works` | [ ] |

### Verification Process

```bash
# Run all Phase 0.1.7 tests
dotnet test --filter "Phase017" --logger "console;verbosity=detailed"

# Verify test count
dotnet test --filter "Phase017" --list-tests
```

---

## Summary: Task Dependencies

```
0.1.7.1 (Inheritance AST) ──────┐
                                │
0.1.7.2 (super() Parsing) ──────┼──► 0.1.7.3 (super() Validation)
                                │           │
0.1.7.5 (Decorator Parsing) ────┤           ▼
                                │    0.1.7.4 (Inheritance CodeGen)
                                │    0.1.7.6 (Decorator CodeGen)
                                │    0.1.7.7 (Abstract Validation)
                                │           │
0.1.7.8 (Interface Parsing) ────┤           │
                                │           ▼
                                │    0.1.7.9 (Interface CodeGen)
                                │    0.1.7.10 (Interface Impl CodeGen)
                                │           │
                                ▼           ▼
                         0.1.7.11 (Integration Tests)
                                │
                                ▼
                         0.1.7.12 (Exit Criteria Doc)
```

## Estimated Total Time
- **Audit/Verification tasks:** 2-3 hours
- **Implementation tasks:** 14-18 hours
- **Testing and documentation:** 4-5 hours
- **Total:** 20-26 hours

## Notes for Agent/Engineer

1. **Interface vs Abstract Class:** Interfaces have no implementation; abstract classes can have partial implementation.

2. **super() is restricted:** Unlike Python, `super()` can only be used in specific contexts.

3. **No `@override` for interfaces:** Interface implementations are implicit.

4. **@final maps to sealed:** Both for classes and methods.

5. **Multiple inheritance:** Only one class base allowed, but multiple interfaces.

6. **Order matters in base list:** First entry is base class (if any), rest are interfaces.
