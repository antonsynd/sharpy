# Edge Cases: Multi-File/Module Compilation and Cross-Module Inheritance

This document contains edge case test scenarios for the Sharpy compiler. Each test case should be compiled and executed to verify correct behavior. Test failures indicate bugs in the compiler that need to be fixed.

## Instructions for Engineers

1. **Setup**: Create a test directory for each test case
2. **Compile**: Use `sharpyc` to compile the project
3. **Execute**: Run the compiled executable
4. **Verify**: Compare output with expected output
5. **Report**: If output differs, report the bug with the test case name

### Prerequisites

Before running these tests, ensure that the following compiler fixes are implemented:
- **Task 1**: Fix NameResolver Instance Isolation Bug (required for all cross-module tests)
- **Task 2**: Extract Full Type Information from Imported Modules (required for member access tests)
- **Task 4**: Propagate Inherited Interface Methods (required for interface inheritance tests)
- **Task 5**: Interface Implementation Validation (required for error case tests)

See `cross_module_inheritance_tasks.md` for implementation details.

---

## Category 1: Multi-Level Import Chains

### Test 1.1: Three-Level Import Chain

Tests transitive imports where A imports B, B imports C.

**Directory Structure:**
```
test_1_1/
├── main.spy
├── middleware.spy
└── foundation.spy
```

**foundation.spy:**
```python
# Foundation module with base constants and functions

MAGIC_NUMBER: int = 42
PI: float = 3.14159

def add(a: int, b: int) -> int:
    return a + b

def multiply(a: float, b: float) -> float:
    return a * b
```

**middleware.spy:**
```python
# Middleware module that uses foundation
from foundation import MAGIC_NUMBER, add, multiply, PI

DOUBLE_MAGIC: int = MAGIC_NUMBER * 2

def process_numbers(x: int, y: int) -> int:
    return add(x, y) + MAGIC_NUMBER

def circle_area(radius: float) -> float:
    return multiply(PI, multiply(radius, radius))
```

**main.spy:**
```python
# Main entry point using middleware
from middleware import DOUBLE_MAGIC, process_numbers, circle_area

def main():
    print(DOUBLE_MAGIC)
    print(process_numbers(10, 5))
    result: int = circle_area(2.0) to int
    print(result)
```

**Expected Output:**
```
84
57
12
```

---

### Test 1.2: Import Aliases at Multiple Levels

Tests import aliasing through multiple module levels.

**Directory Structure:**
```
test_1_2/
├── main.spy
├── utils.spy
└── core.spy
```

**core.spy:**
```python
def format_string(s: str) -> str:
    return "[" + s + "]"

def pad_number(n: int) -> str:
    return "000" + (n to str)
```

**utils.spy:**
```python
from core import format_string as fmt, pad_number as pad

def fancy_print(value: str) -> None:
    print(fmt(value))

def format_id(id: int) -> str:
    return fmt(pad(id))
```

**main.spy:**
```python
from utils import fancy_print as fp, format_id

def main():
    fp("Hello")
    print(format_id(42))
```

**Expected Output:**
```
[Hello]
[00042]
```

---

### Test 1.3: Circular Import Detection

Tests that circular imports produce a helpful error message.

**Directory Structure:**
```
test_1_3/
├── module_a.spy
└── module_b.spy
```

**module_a.spy:**
```python
from module_b import func_b

def func_a() -> int:
    return func_b() + 1
```

**module_b.spy:**
```python
from module_a import func_a

def func_b() -> int:
    return func_a() + 1
```

**Expected Behavior:**
Compiler should emit an error like:
```
error: Circular import detected:
  -> module_a.spy
  -> module_b.spy
  -> module_a.spy (cycle)
```

---

## Category 2: Multi-Level Class Inheritance

### Test 2.1: Three-Level Class Inheritance (Same File)

Tests basic three-level inheritance within a single file.

**single_file_inheritance.spy:**
```python
class Animal:
    name: str
    
    def __init__(self, name: str):
        self.name = name
    
    @virtual
    def speak(self) -> str:
        return "..."

class Mammal(Animal):
    warm_blooded: bool
    
    def __init__(self, name: str):
        super().__init__(name)
        self.warm_blooded = True
    
    @override
    def speak(self) -> str:
        return "mammal sound"

class Dog(Mammal):
    breed: str
    
    def __init__(self, name: str, breed: str):
        super().__init__(name)
        self.breed = breed
    
    @override
    def speak(self) -> str:
        return "woof"

def main():
    animal: Animal = Animal("generic")
    mammal: Mammal = Mammal("mammal")
    dog: Dog = Dog("Rex", "Shepherd")
    
    print(animal.speak())
    print(mammal.speak())
    print(dog.speak())
    print(dog.name)
    print(dog.warm_blooded)
```

**Expected Output:**
```
...
mammal sound
woof
Rex
True
```

---

### Test 2.2: Three-Level Class Inheritance (Across Files)

Tests three-level inheritance where each class is in a different file.

**Directory Structure:**
```
test_2_2/
├── main.spy
├── animal.spy
├── mammal.spy
└── dog.spy
```

**animal.spy:**
```python
class Animal:
    name: str
    
    def __init__(self, name: str):
        self.name = name
    
    @virtual
    def speak(self) -> str:
        return "..."
    
    def get_name(self) -> str:
        return self.name
```

**mammal.spy:**
```python
from animal import Animal

class Mammal(Animal):
    warm_blooded: bool
    
    def __init__(self, name: str):
        super().__init__(name)
        self.warm_blooded = True
    
    @override
    def speak(self) -> str:
        return "mammal sound"
    
    def is_warm_blooded(self) -> bool:
        return self.warm_blooded
```

**dog.spy:**
```python
from mammal import Mammal

class Dog(Mammal):
    breed: str
    
    def __init__(self, name: str, breed: str):
        super().__init__(name)
        self.breed = breed
    
    @override
    def speak(self) -> str:
        return "woof"
    
    def get_breed(self) -> str:
        return self.breed
```

**main.spy:**
```python
from dog import Dog
from mammal import Mammal
from animal import Animal

def main():
    dog: Dog = Dog("Rex", "Shepherd")
    
    # Test inherited methods from all levels
    print(dog.speak())
    print(dog.get_name())
    print(dog.is_warm_blooded())
    print(dog.get_breed())
    
    # Test polymorphism
    animal: Animal = dog
    print(animal.speak())
```

**Expected Output:**
```
woof
Rex
True
Shepherd
woof
```

---

### Test 2.3: Super Call to Grandparent Method

Tests that super() correctly chains through inheritance hierarchy.

**Directory Structure:**
```
test_2_3/
├── main.spy
├── base.spy
├── middle.spy
└── derived.spy
```

**base.spy:**
```python
class Base:
    value: int
    
    def __init__(self, value: int):
        self.value = value
    
    @virtual
    def describe(self) -> str:
        return "Base:" + (self.value to str)
```

**middle.spy:**
```python
from base import Base

class Middle(Base):
    multiplier: int
    
    def __init__(self, value: int, mult: int):
        super().__init__(value)
        self.multiplier = mult
    
    @override
    def describe(self) -> str:
        base_desc: str = super().describe()
        return base_desc + ",Middle:" + (self.multiplier to str)
```

**derived.spy:**
```python
from middle import Middle

class Derived(Middle):
    suffix: str
    
    def __init__(self, value: int, mult: int, suffix: str):
        super().__init__(value, mult)
        self.suffix = suffix
    
    @override
    def describe(self) -> str:
        middle_desc: str = super().describe()
        return middle_desc + ",Derived:" + self.suffix
```

**main.spy:**
```python
from derived import Derived
from middle import Middle
from base import Base

def main():
    b: Base = Base(10)
    m: Middle = Middle(20, 2)
    d: Derived = Derived(30, 3, "end")
    
    print(b.describe())
    print(m.describe())
    print(d.describe())
```

**Expected Output:**
```
Base:10
Base:20,Middle:2
Base:30,Middle:3,Derived:end
```

---

## Category 3: Multi-Level Interface Implementation

### Test 3.1: Interface Inheritance Chain

Tests interfaces extending other interfaces.

**interface_chain.spy:**
```python
interface IBasic:
    def get_id(self) -> int: ...

interface IExtended(IBasic):
    def get_name(self) -> str: ...

interface IFull(IExtended):
    def get_description(self) -> str: ...

class FullImplementation(IFull):
    id: int
    name: str
    
    def __init__(self, id: int, name: str):
        self.id = id
        self.name = name
    
    def get_id(self) -> int:
        return self.id
    
    def get_name(self) -> str:
        return self.name
    
    def get_description(self) -> str:
        return (self.id to str) + ":" + self.name

def main():
    obj: FullImplementation = FullImplementation(42, "Test")
    print(obj.get_id())
    print(obj.get_name())
    print(obj.get_description())
    
    # Test interface assignment
    basic: IBasic = obj
    print(basic.get_id())
```

**Expected Output:**
```
42
Test
42:Test
42
```

---

### Test 3.2: Interface Inheritance Across Files

Tests interface inheritance where interfaces are in different files.

**Directory Structure:**
```
test_3_2/
├── main.spy
├── base_interface.spy
├── extended_interface.spy
└── implementation.spy
```

**base_interface.spy:**
```python
interface IIdentifiable:
    def get_id(self) -> int: ...
```

**extended_interface.spy:**
```python
from base_interface import IIdentifiable

interface IEntity(IIdentifiable):
    def get_name(self) -> str: ...
    def is_active(self) -> bool: ...
```

**implementation.spy:**
```python
from extended_interface import IEntity

class User(IEntity):
    id: int
    name: str
    active: bool
    
    def __init__(self, id: int, name: str):
        self.id = id
        self.name = name
        self.active = True
    
    def get_id(self) -> int:
        return self.id
    
    def get_name(self) -> str:
        return self.name
    
    def is_active(self) -> bool:
        return self.active
```

**main.spy:**
```python
from implementation import User
from base_interface import IIdentifiable

def main():
    user: User = User(1, "Alice")
    
    print(user.get_id())
    print(user.get_name())
    print(user.is_active())
    
    # Test base interface assignment
    identifiable: IIdentifiable = user
    print(identifiable.get_id())
```

**Expected Output:**
```
1
Alice
True
1
```

---

### Test 3.3: Multiple Interface Implementation with Diamond

Tests implementing multiple interfaces that share a common base.

**diamond_interface.spy:**
```python
interface IBase:
    def base_method(self) -> str: ...

interface ILeft(IBase):
    def left_method(self) -> str: ...

interface IRight(IBase):
    def right_method(self) -> str: ...

class DiamondImpl(ILeft, IRight):
    value: str
    
    def __init__(self, value: str):
        self.value = value
    
    def base_method(self) -> str:
        return "base:" + self.value
    
    def left_method(self) -> str:
        return "left:" + self.value
    
    def right_method(self) -> str:
        return "right:" + self.value

def main():
    obj: DiamondImpl = DiamondImpl("test")
    
    print(obj.base_method())
    print(obj.left_method())
    print(obj.right_method())
    
    # Test interface assignments
    left: ILeft = obj
    right: IRight = obj
    base: IBase = obj
    
    print(left.base_method())
    print(right.base_method())
    print(base.base_method())
```

**Expected Output:**
```
base:test
left:test
right:test
base:test
base:test
base:test
```

---

## Category 4: Mixed Inheritance and Interface Implementation

### Test 4.1: Class Inheriting Base + Implementing Interface (Same File)

**mixed_inheritance.spy:**
```python
interface ISerializable:
    def serialize(self) -> str: ...

class Entity:
    id: int
    
    def __init__(self, id: int):
        self.id = id
    
    def get_id(self) -> int:
        return self.id

class User(Entity, ISerializable):
    name: str
    
    def __init__(self, id: int, name: str):
        super().__init__(id)
        self.name = name
    
    def serialize(self) -> str:
        return "User:" + (self.id to str) + ":" + self.name

def main():
    user: User = User(1, "Alice")
    
    print(user.get_id())
    print(user.name)
    print(user.serialize())
    
    # Test interface assignment
    serializable: ISerializable = user
    print(serializable.serialize())
    
    # Test base class assignment
    entity: Entity = user
    print(entity.get_id())
```

**Expected Output:**
```
1
Alice
User:1:Alice
User:1:Alice
1
```

---

### Test 4.2: Multi-Level Mixed Inheritance Across Files

Tests complex inheritance: Base class in one file, derived class + interface in another.

**Directory Structure:**
```
test_4_2/
├── main.spy
├── contracts.spy
├── base_entity.spy
├── named_entity.spy
└── user.spy
```

**contracts.spy:**
```python
interface ISerializable:
    def to_string(self) -> str: ...

interface IComparable:
    def compare_to(self, other: int) -> int: ...
```

**base_entity.spy:**
```python
class Entity:
    id: int
    
    def __init__(self, id: int):
        self.id = id
    
    @virtual
    def get_type(self) -> str:
        return "Entity"
```

**named_entity.spy:**
```python
from base_entity import Entity
from contracts import ISerializable

class NamedEntity(Entity, ISerializable):
    name: str
    
    def __init__(self, id: int, name: str):
        super().__init__(id)
        self.name = name
    
    @override
    def get_type(self) -> str:
        return "NamedEntity"
    
    def to_string(self) -> str:
        return self.name + "(" + (self.id to str) + ")"
```

**user.spy:**
```python
from named_entity import NamedEntity
from contracts import IComparable

class User(NamedEntity, IComparable):
    email: str
    
    def __init__(self, id: int, name: str, email: str):
        super().__init__(id, name)
        self.email = email
    
    @override
    def get_type(self) -> str:
        return "User"
    
    @override
    def to_string(self) -> str:
        return super().to_string() + "<" + self.email + ">"
    
    def compare_to(self, other: int) -> int:
        if self.id < other:
            return -1
        if self.id > other:
            return 1
        return 0
```

**main.spy:**
```python
from user import User
from contracts import ISerializable, IComparable
from base_entity import Entity

def main():
    user: User = User(42, "Alice", "alice@example.com")
    
    # Test direct methods
    print(user.get_type())
    print(user.to_string())
    print(user.compare_to(50))
    print(user.compare_to(42))
    print(user.compare_to(10))
    
    # Test inherited field access
    print(user.id)
    print(user.name)
    print(user.email)
    
    # Test interface assignments
    serializable: ISerializable = user
    print(serializable.to_string())
    
    comparable: IComparable = user
    print(comparable.compare_to(100))
    
    # Test base class assignment and polymorphism
    entity: Entity = user
    print(entity.get_type())
```

**Expected Output:**
```
User
Alice(42)<alice@example.com>
-1
0
1
42
Alice
alice@example.com
Alice(42)<alice@example.com>
-1
User
```

---

## Category 5: .NET Type Inheritance

### Test 5.1: Implementing System.IComparable

Tests implementing a .NET interface.

**net_interface_impl.spy:**
```python
from system import IComparable

class SortableItem(IComparable):
    value: int
    
    def __init__(self, value: int):
        self.value = value
    
    def compare_to(self, obj: object) -> int:
        other: SortableItem = obj as SortableItem
        if other == None:
            return 1
        if self.value < other.value:
            return -1
        if self.value > other.value:
            return 1
        return 0

def main():
    a: SortableItem = SortableItem(10)
    b: SortableItem = SortableItem(20)
    c: SortableItem = SortableItem(10)
    
    print(a.compare_to(b))
    print(b.compare_to(a))
    print(a.compare_to(c))
```

**Expected Output:**
```
-1
1
0
```

---

### Test 5.2: Extending System.Exception

Tests inheriting from a .NET class.

**custom_exception.spy:**
```python
from system import Exception

class ValidationError(Exception):
    field_name: str
    
    def __init__(self, message: str, field: str):
        super().__init__(message)
        self.field_name = field
    
    def get_field(self) -> str:
        return self.field_name

def validate(value: int) -> None:
    if value < 0:
        raise ValidationError("Value cannot be negative", "value")

def main():
    try:
        validate(-5)
    except ValidationError as e:
        print(e.field_name)
        print(e.message)
```

**Expected Output:**
```
value
Value cannot be negative
```

---

## Category 6: Abstract Classes Across Modules

### Test 6.1: Abstract Class with Concrete Derived Class

**Directory Structure:**
```
test_6_1/
├── main.spy
├── abstract_shape.spy
└── concrete_shapes.spy
```

**abstract_shape.spy:**
```python
@abstract
class Shape:
    name: str
    
    def __init__(self, name: str):
        self.name = name
    
    @abstract
    def area(self) -> float:
        ...
    
    @abstract
    def perimeter(self) -> float:
        ...
    
    def describe(self) -> str:
        return self.name + " with area " + (self.area() to str)
```

**concrete_shapes.spy:**
```python
from abstract_shape import Shape

class Rectangle(Shape):
    width: float
    height: float
    
    def __init__(self, width: float, height: float):
        super().__init__("Rectangle")
        self.width = width
        self.height = height
    
    @override
    def area(self) -> float:
        return self.width * self.height
    
    @override
    def perimeter(self) -> float:
        return 2.0 * (self.width + self.height)

class Circle(Shape):
    radius: float
    
    def __init__(self, radius: float):
        super().__init__("Circle")
        self.radius = radius
    
    @override
    def area(self) -> float:
        return 3.14159 * self.radius * self.radius
    
    @override
    def perimeter(self) -> float:
        return 2.0 * 3.14159 * self.radius
```

**main.spy:**
```python
from concrete_shapes import Rectangle, Circle
from abstract_shape import Shape

def print_shape_info(shape: Shape) -> None:
    print(shape.describe())
    print(shape.perimeter() to int)

def main():
    rect: Rectangle = Rectangle(4.0, 3.0)
    circle: Circle = Circle(2.0)
    
    print_shape_info(rect)
    print_shape_info(circle)
```

**Expected Output:**
```
Rectangle with area 12
14
Circle with area 12.56636
12
```

---

## Category 7: Edge Cases and Error Handling

### Test 7.1: Incomplete Interface Implementation (Error Case)

Tests that the compiler correctly errors when interface methods are missing.

**incomplete_impl.spy:**
```python
interface IComplete:
    def method_a(self) -> int: ...
    def method_b(self) -> str: ...

class Incomplete(IComplete):
    def method_a(self) -> int:
        return 42
    # Missing method_b!

def main():
    obj: Incomplete = Incomplete()
    print(obj.method_a())
```

**Expected Behavior:**
Compiler should emit an error like:
```
error: Class 'Incomplete' does not implement interface method 'IComplete.method_b'
```

---

### Test 7.2: Covariant Return Type Override

Tests that derived classes can return more specific types.

**covariant_return.spy:**
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

class AnimalFactory:
    @virtual
    def create(self) -> Animal:
        return Animal("generic")

class DogFactory(AnimalFactory):
    dog_breed: str
    
    def __init__(self, breed: str):
        self.dog_breed = breed
    
    @override
    def create(self) -> Dog:  # Covariant return
        return Dog("doggy", self.dog_breed)

def main():
    factory: AnimalFactory = DogFactory("Shepherd")
    animal: Animal = factory.create()
    print(animal.name)
```

**Expected Output:**
```
doggy
```

**Note:** If Sharpy doesn't support covariant return types, this test should produce a specific compiler error indicating that the return type must match.

---

### Test 7.3: Method Hiding vs Override

Tests the difference between hiding and overriding methods.

**method_hiding.spy:**
```python
class Parent:
    @virtual
    def virtual_method(self) -> str:
        return "Parent.virtual"
    
    def non_virtual_method(self) -> str:
        return "Parent.non_virtual"

class Child(Parent):
    @override
    def virtual_method(self) -> str:
        return "Child.virtual"
    
    # This hides the parent method (no @override)
    def non_virtual_method(self) -> str:
        return "Child.non_virtual"

def main():
    child: Child = Child()
    parent: Parent = child
    
    # Virtual method: Child's version called (polymorphism)
    print(parent.virtual_method())
    print(child.virtual_method())
    
    # Non-virtual method: depends on reference type
    print(parent.non_virtual_method())
    print(child.non_virtual_method())
```

**Expected Output:**
```
Child.virtual
Child.virtual
Parent.non_virtual
Child.non_virtual
```

---

## Summary Checklist

| Test | Category | Expected Behavior | Pass/Fail |
|------|----------|-------------------|-----------|
| 1.1 | Import Chain | Transitive imports work | |
| 1.2 | Import Aliases | Aliases preserved through levels | |
| 1.3 | Circular Import | Error detected | |
| 2.1 | Inheritance (Same File) | Three-level works | |
| 2.2 | Inheritance (Across Files) | Cross-module works | |
| 2.3 | Super to Grandparent | Chain works | |
| 3.1 | Interface Chain | Inherited methods work | |
| 3.2 | Interface Across Files | Cross-module works | |
| 3.3 | Diamond Interface | Single implementation | |
| 4.1 | Mixed Inheritance | Class + interface | |
| 4.2 | Multi-Level Mixed | Complex hierarchy | |
| 5.1 | .NET Interface | IComparable works | |
| 5.2 | .NET Class | Exception works | |
| 6.1 | Abstract Class | Cross-module | |
| 7.1 | Error: Incomplete | Proper error message | |
| 7.2 | Covariant Return | Type check | |
| 7.3 | Hiding vs Override | Correct dispatch | |

---

## Notes for Test Implementation

1. **File Creation**: Each multi-file test requires creating the directory structure exactly as specified.

2. **Project File**: For multi-file tests, you may need to create a `.spyproj` file listing all source files:
   ```json
   {
     "name": "TestProject",
     "entry_point": "main.spy",
     "source_files": ["main.spy", "other.spy"]
   }
   ```

3. **Compilation Command**: 
   ```bash
   sharpyc compile --project test_dir/ --output test_dir/bin/
   ```

4. **Execution Command**:
   ```bash
   dotnet test_dir/bin/TestProject.dll
   ```

5. **Error Tests**: For tests expecting compilation errors (like 1.3 and 7.1), verify that:
   - Compilation fails with exit code != 0
   - Error message matches expected pattern

6. **Known Limitations**: Document any tests that fail due to known unimplemented features vs actual bugs.
