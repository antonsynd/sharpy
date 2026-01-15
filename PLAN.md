# Implementation Plan: Task 0.1.7.12 - Phase 0.1.7 Integration Tests

## Overview

Create comprehensive integration tests for Phase 0.1.7 OOP features in `src/Sharpy.Compiler.Tests/Integration/Phase017IntegrationTests.cs`.

## Phase 0.1.7 Feature Coverage

Based on the task commits (0.1.7.1 - 0.1.7.11), these features need integration testing:

1. **Inheritance** (Tasks 0.1.7.1-0.1.7.4)
   - Single class inheritance
   - Field inheritance
   - Method inheritance and override
   - Constructor chaining with `super().__init__(...)`

2. **Decorators** (Tasks 0.1.7.5-0.1.7.6)
   - `@static` decorator
   - `@abstract` / `@abstractmethod` decorators
   - `@virtual` decorator
   - `@override` decorator

3. **Abstract Classes** (Task 0.1.7.7)
   - Abstract class definition with `@abstract` decorator
   - Abstract method declaration with `@abstractmethod`
   - Concrete subclass implementation
   - Cannot instantiate abstract classes (error case)

4. **Interfaces** (Tasks 0.1.7.8-0.1.7.10)
   - Interface definition syntax
   - Single interface implementation
   - Multiple interface implementation
   - Interface method implementation

5. **Dunder Method Override Rules** (Task 0.1.7.11)
   - `__str__` override with `@override` decorator
   - `__repr__` override
   - `__eq__`, `__ne__`, `__lt__`, `__le__`, `__gt__`, `__ge__` comparison overrides
   - `__hash__` override
   - `super()` calls in dunder methods

---

## Step-by-Step Implementation

### Step 1: Create Test File Structure

Create `src/Sharpy.Compiler.Tests/Integration/Phase017IntegrationTests.cs` with:

```csharp
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// Integration tests for Phase 0.1.7: Inheritance, Interfaces, Abstract Classes, and Decorators.
/// These tests verify the full compilation pipeline for advanced OOP features.
/// </summary>
public class Phase017IntegrationTests : IntegrationTestBase
{
    public Phase017IntegrationTests(ITestOutputHelper output) : base(output)
    {
    }

    // Test regions to follow...
}
```

### Step 2: Implement Inheritance Tests

**Region: `#region Simple Inheritance Tests`**

| Test Name | Description |
|-----------|-------------|
| `SingleInheritance_CompilesAndRuns` | Dog extends Animal, inherit field and override speak() |
| `InheritedField_AccessibleInSubclass` | Subclass can access parent class fields |
| `InheritedMethod_CallableOnSubclass` | Subclass can call inherited methods |
| `ConstructorChaining_SuperInit_CompilesAndRuns` | `super().__init__()` calls parent constructor |
| `MultiLevelInheritance_ThreeLevels_CompilesAndRuns` | C extends B extends A |
| `MethodOverride_WithOverrideDecorator_CompilesAndRuns` | `@override` on method overriding parent |

**Example test:**
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
        return ""Some sound""

class Dog(Animal):
    breed: str

    def __init__(self, name: str, breed: str):
        super().__init__(name)
        self.breed = breed

    @override
    def speak(self) -> str:
        return ""Woof!""

dog = Dog(""Rex"", ""German Shepherd"")
print(dog.name)
print(dog.breed)
print(dog.speak())
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
    Assert.Equal("Rex\nGerman Shepherd\nWoof!\n", result.StandardOutput);
}
```

### Step 3: Implement Decorator Tests

**Region: `#region Decorator Tests`**

| Test Name | Description |
|-----------|-------------|
| `StaticDecorator_OnMethod_CompilesAndRuns` | `@static` decorator makes method static |
| `VirtualDecorator_AllowsOverride_CompilesAndRuns` | `@virtual` allows subclass override |
| `OverrideDecorator_OnInheritedMethod_CompilesAndRuns` | `@override` on overriding method |
| `MultipleDecorators_OnSameMethod_CompilesAndRuns` | Multiple decorators stack correctly |

### Step 4: Implement Abstract Class Tests

**Region: `#region Abstract Class Tests`**

| Test Name | Description |
|-----------|-------------|
| `AbstractClass_ConcreteSubclass_CompilesAndRuns` | Abstract class with concrete implementation |
| `AbstractMethod_MustBeImplemented_CompilesAndRuns` | Abstract method implemented in subclass |
| `AbstractClass_MultipleAbstractMethods_CompilesAndRuns` | Multiple abstract methods |
| `AbstractClass_MixedAbstractAndConcrete_CompilesAndRuns` | Mix of abstract and concrete methods |

**Example test:**
```csharp
[Fact]
public void AbstractClass_ConcreteSubclass_CompilesAndRuns()
{
    var source = @"
@abstract
class Shape:
    name: str

    def __init__(self, name: str):
        self.name = name

    @abstractmethod
    def area(self) -> int:
        ...

class Rectangle(Shape):
    width: int
    height: int

    def __init__(self, width: int, height: int):
        super().__init__(""Rectangle"")
        self.width = width
        self.height = height

    @override
    def area(self) -> int:
        return self.width * self.height

rect = Rectangle(10, 5)
print(rect.name)
print(rect.area())
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
    Assert.Equal("Rectangle\n50\n", result.StandardOutput);
}
```

### Step 5: Implement Interface Tests

**Region: `#region Interface Tests`**

| Test Name | Description |
|-----------|-------------|
| `InterfaceDefinition_SimpleInterface_CompilesAndRuns` | Define and implement simple interface |
| `InterfaceImplementation_SingleMethod_CompilesAndRuns` | Class implements interface method |
| `MultipleInterfaces_ImplementsBoth_CompilesAndRuns` | Class implements multiple interfaces |
| `InterfaceInheritance_InterfaceExtendsInterface_CompilesAndRuns` | Interface extends another interface |
| `InterfaceWithBaseClass_ClassExtendsAndImplements_CompilesAndRuns` | Class extends and implements |

**Example test:**
```csharp
[Fact]
public void InterfaceDefinition_SimpleInterface_CompilesAndRuns()
{
    var source = @"
interface IDrawable:
    def draw(self) -> str:
        ...

class Circle(IDrawable):
    radius: int

    def __init__(self, radius: int):
        self.radius = radius

    def draw(self) -> str:
        return f""Circle with radius {self.radius}""

c = Circle(5)
print(c.draw())
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
    Assert.Equal("Circle with radius 5\n", result.StandardOutput);
}
```

### Step 6: Implement Dunder Method Override Tests

**Region: `#region Dunder Method Override Tests`**

| Test Name | Description |
|-----------|-------------|
| `DunderStr_Override_CompilesAndRuns` | `@override def __str__(self)` |
| `DunderRepr_Override_CompilesAndRuns` | `@override def __repr__(self)` |
| `DunderEq_Override_CompilesAndRuns` | `@override def __eq__(self, other)` |
| `DunderHash_Override_CompilesAndRuns` | `@override def __hash__(self)` |
| `DunderLt_Override_CompilesAndRuns` | `@override def __lt__(self, other)` |
| `DunderComparison_AllOperators_CompilesAndRuns` | All comparison dunders |
| `SuperInDunder_CallsParent_CompilesAndRuns` | `super().__str__()` calls |

**Example test:**
```csharp
[Fact]
public void DunderStr_Override_CompilesAndRuns()
{
    var source = @"
class Person:
    name: str
    age: int

    def __init__(self, name: str, age: int):
        self.name = name
        self.age = age

    @override
    def __str__(self) -> str:
        return f""Person({self.name}, {self.age})""

p = Person(""Alice"", 30)
print(str(p))
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
    Assert.Equal("Person(Alice, 30)\n", result.StandardOutput);
}
```

### Step 7: Implement Comprehensive Integration Tests

**Region: `#region Comprehensive Integration Tests`**

| Test Name | Description |
|-----------|-------------|
| `CompleteHierarchy_AnimalKingdom_CompilesAndRuns` | Animal -> Mammal -> Dog/Cat hierarchy |
| `AbstractInterfaceCombination_CompilesAndRuns` | Abstract class implementing interface |
| `DiamondPattern_MultipleInheritance_CompilesAndRuns` | Multiple interfaces with same method |
| `PolymorphicBehavior_DifferentSubclasses_CompilesAndRuns` | Polymorphic method calls |

### Step 8: Implement Error Cases

**Region: `#region Error Cases`**

| Test Name | Description |
|-----------|-------------|
| `Error_AbstractClassInstantiation_ReportsError` | Cannot instantiate abstract class |
| `Error_AbstractMethodNotImplemented_ReportsError` | Concrete class missing abstract method |
| `Error_InterfaceMethodNotImplemented_ReportsError` | Class missing interface method |
| `Error_OverrideWithoutBaseMethod_ReportsError` | `@override` without matching parent method |
| `Error_MissingOverrideDecorator_ReportsError` | Override without `@override` decorator |
| `Error_CircularInheritance_ReportsError` | Class inherits from itself |
| `Error_InheritFromNonClass_ReportsError` | Inherit from non-existent class |
| `Error_DunderOverrideWithoutDecorator_ReportsError` | Dunder override missing `@override` |

### Step 9: Implement Edge Cases

**Region: `#region Edge Cases`**

| Test Name | Description |
|-----------|-------------|
| `EdgeCase_EmptyInterface_CompilesAndRuns` | Interface with no methods |
| `EdgeCase_EmptyAbstractClass_CompilesAndRuns` | Abstract class with no abstract methods |
| `EdgeCase_DeepInheritanceChain_CompilesAndRuns` | 5+ levels of inheritance |
| `EdgeCase_ManyInterfaces_CompilesAndRuns` | Class implementing 5+ interfaces |
| `EdgeCase_InterfaceWithProperties_CompilesAndRuns` | Interface with property signatures |

---

## Files to Modify

| File | Action |
|------|--------|
| `src/Sharpy.Compiler.Tests/Integration/Phase017IntegrationTests.cs` | **CREATE** - Main test file |

---

## Tests to Verify

After implementation, run:
```bash
dotnet test src/Sharpy.Compiler.Tests --filter "FullyQualifiedName~Phase017"
```

All tests should pass. Expected test count: ~40-50 tests.

---

## Potential Risks and Questions

### Risks

1. **Syntax variations**: The Sharpy syntax for inheritance, interfaces, and decorators may have nuances not captured in the examples. Need to verify against actual parser/semantic analysis.

2. **super() semantics**: The `super().__init__()` pattern and `super()` in dunder methods has specific rules. Tests should verify these work correctly.

3. **Name mangling**: Methods like `__str__` map to `ToString()` in C#. Need to verify the code generation handles this correctly.

4. **Interface vs Abstract class syntax**: Need to verify the exact Sharpy syntax for interfaces (`interface IName:` vs Python's ABC approach).

5. **Decorator stacking**: Multiple decorators on a single method need to work together correctly.

### Questions to Clarify

1. **Interface prefix convention**: Does Sharpy require `I` prefix for interfaces (like C#) or follow Python naming?
   - Based on code review: `NameMangler.Transform` with `NameContext.Interface` preserves `I` prefix.

2. **Multiple inheritance**: Does Sharpy support multiple class inheritance or only single inheritance + multiple interfaces?
   - Based on `ClassDef.BaseClasses` being a list: Multiple base types supported (likely class + interfaces pattern).

3. **Property syntax in interfaces**: Are properties allowed in interface definitions?
   - Need to test: May need `pass` or `...` as placeholder.

4. **@abstractmethod vs @abstract**: Both seem supported based on RoslynEmitter code. Should test both.

5. **Required @override for dunders**: Based on TypeChecker code, `@override` is required for dunders that override System.Object methods.

---

## Implementation Notes

- Follow the test patterns from `Phase016IntegrationTests.cs`
- Use `#region` comments to organize test sections
- Include both positive and negative (error) test cases
- Use descriptive test names: `[Feature]_[Scenario]_[ExpectedBehavior]()`
- Assert both `result.Success` and `result.StandardOutput` for positive tests
- Assert `result.Success == false` and `result.CompilationErrors` not empty for error tests
