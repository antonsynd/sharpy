## Inheritance **[v0.1.2]**

### Single Class Inheritance

Sharpy supports single class inheritance only. A class can extend at most one base class but may implement multiple interfaces.

```python
class Employee(Person):
    employee_id: str

    def __init__(self, name: str, age: int, employee_id: str):
        super().__init__(name, age)
        self.employee_id = employee_id

    def greet(self) -> str:
        return f"Hello, I'm {self.name}, employee #{self.employee_id}"
```

**Multiple Class Inheritance is Not Supported:**

```python
class A:
    pass

class B:
    pass

# ❌ ERROR: Multiple class inheritance not allowed
class C(A, B):  # ERROR: A class can only extend one base class
    pass

# ✅ OK: Single class + multiple interfaces
class C(A, ISerializable, IComparable):
    pass
```

*Implementation: ✅ Native - `: BaseClass`; `super().__init__()` → `: base()` or `base.Method()`*

### Multiple Interface Implementation

```python
class JSONEmployee(Employee, ISerializable, IComparable):
    def serialize(self) -> str:
        # Implementation
        pass

    def compare_to(self, other: object) -> int:
        # Implementation
        pass
```

**Rules:**
- Single class inheritance allowed
- Multiple interface implementation allowed
- Base class (if present) must come first

*Implementation: ✅ Native - `: BaseClass, IInterface1, IInterface2`*

### The `super()` Function

`super()` provides access to methods from a parent class. It is only valid in specific contexts.

**Valid Contexts for `super()`:**

| Context | Example | Purpose |
|---------|---------|---------|
| Inside `__init__` | `super().__init__(args)` | Call parent constructor |
| Inside dunder methods | `super().__eq__(other)` | Call parent dunder implementation |
| Inside overriding methods | `super().method()` | Call parent method being overridden |

**Constructor Chaining:**

Use `super().__init__()` to call the parent class constructor:

```python
class Animal:
    name: str

    def __init__(self, name: str):
        self.name = name

class Dog(Animal):
    breed: str

    def __init__(self, name: str, breed: str):
        super().__init__(name)  # ✅ Call parent constructor
        self.breed = breed
```

**Calling Overridden Methods:**

When a method overrides a parent method, `super()` can call the parent implementation:

```python
class Shape:
    @virtual
    def describe(self) -> str:
        return "A shape"

class Circle(Shape):
    radius: double

    def __init__(self, radius: double):
        self.radius = radius

    @override
    def describe(self) -> str:
        base_description = super().describe()  # ✅ Calls Shape.describe()
        return f"{base_description}: circle with radius {self.radius}"
```

**Inside Dunder Methods:**

Within a dunder method, `super()` can call the parent's dunder implementation. This includes calling the same dunder on the parent, or other dunders for cross-dunder synthesis:

```python
class Child(Parent):
    @override
    def __repr__(self) -> str:
        return super().__repr__() + " (child)"  # ✅ Same dunder on parent

    @override
    def __eq__(self, other: object) -> bool:
        if not super().__eq__(other):           # ✅ Same dunder on parent
            return False
        # Additional comparison logic...
        return True

    def __le__(self, other: Child) -> bool:
        # Cross-dunder synthesis (also allowed on self, see Dunder Invocation Rules)
        return self.__lt__(other) or self.__eq__(other)  # ✅ OK
```

**No chained `super()` for Multi-Level Inheritance:**

In deep inheritance hierarchies, you cannot chain `super()` calls to access methods from ancestors further up the chain:

```python
class A:
    @virtual
    def process(self) -> str:
        return "A"

class B(A):
    @override
    def process(self) -> str:
        return "B+" + super().process()  # Returns "B+A"

class C(B):
    @override
    def process(self) -> str:
        # Access immediate parent (B)
        b_result = super().process()              # "B+A"

        # Access grandparent (A) by chaining
        a_result = super().super().process()      # ERROR: Not allowed

        return f"C({b_result}, {a_result})"
```

**Invalid Contexts:**

`super()` is a compile-time error in any other context:

```python
class Example:
    value: int

    def __init__(self, value: int):
        self.value = value

    def regular_method(self) -> None:
        super().something()     # ❌ ERROR: regular_method does not override a parent method

    def another_method(self) -> int:
        return super().value    # ❌ ERROR: cannot access parent fields via super()

class Standalone:
    def method(self) -> None:
        super().__init__()      # ❌ ERROR: Standalone has no parent class

def free_function():
    super().something()         # ❌ ERROR: super() not valid outside class methods
```

**Compiler Error Message:**

When `super()` is used in an invalid context, the compiler provides a helpful error:

```
error: `super()` is only valid in:
  - `__init__()` (to call parent constructor)
  - dunder methods (to call parent dunders)
  - methods decorated with @override (to call overridden methods up the inheritance chain)
```

**Rules Summary:**

1. `super()` requires the class to have a parent class (compile error otherwise)
2. In `__init__`: can call `super().__init__(...)`
3. In `@override` methods: can call `super().method_name(...)` for overridden methods up the chain
4. In dunder methods: can call `super().__dunder__(...)` for parent dunders, and `super().__other_dunder__(...)` for cross-dunder synthesis
5. `super()` cannot be chained to access ancestors further up the inheritance hierarchy
6. Cannot use `super()` to access parent fields
7. Cannot use `super()` in non-overriding regular methods
8. Cannot use `super()` in free functions or static methods

*Implementation: 🔄 Lowered - `super()` maps to `base.Method()`*

---

