## Operator Overloading **[v0.1.4]**

Classes can define dunder methods (double-underscore methods like `__add__`, `__eq__`) to customize how operators and built-in functions behave with their instances. **Dunder methods are a definition mechanism only**—they specify *how* a type behaves, but users invoke that behavior through operators and built-in functions, not by calling dunders directly.

### Dunder Invocation Rules **[v0.1.0]**

#### Dunders Are Definition-Only

Dunder methods exist to **define** how a type behaves with operators and built-in functions. **Explicit dunder invocation by user code is a compile error:**

```python
x = 5
x.__eq__(3)         # ERROR: Cannot invoke dunder methods directly
x.__repr__()        # ERROR: Cannot invoke dunder methods directly

my_list = [1, 2, 3]
my_list.__len__()   # ERROR: Cannot invoke dunder methods directly

obj = MyClass()
obj.__str__()       # ERROR: Cannot invoke dunder methods directly
```

#### Correct Usage

Use operators for operator dunders:

```python
x == y              # ✅ Correct — compiler uses __eq__ internally
x + y               # ✅ Correct — compiler uses __add__ internally
-x                  # ✅ Correct — compiler uses __neg__ internally
x < y               # ✅ Correct — compiler uses __lt__ internally
x[0]                # ✅ Correct — compiler uses __getitem__ internally
```

Use built-in functions for protocol dunders:

```python
repr(x)             # ✅ Correct — uses __repr__ internally
len(x)              # ✅ Correct — uses __len__ internally
hash(x)             # ✅ Correct — uses __hash__ internally
str(x)              # ✅ Correct — uses __str__ internally
```

#### Rationale

- **Uniform syntax**: `repr(x)` and `x == y` work on any type, whether primitive or Sharpy-defined
- **.NET interop**: Primitives from .NET (`int`, `str`, `bool`) don't have dunder methods—the compiler handles dispatch
- **Zero overhead**: No wrapper types or boxing required for polymorphic dispatch
- **Consistency**: Same syntax works whether the type defines a dunder or uses native behavior

> **Summary: Dunder Call Permissions**
>
> | Context | Allowed? |
> |---------|----------|
> | User code calling `x.__dunder__()` | ❌ Compile error |
> | Inside dunder method, calling `self.__other_dunder__()` | ✅ Allowed |
> | Inside dunder method, calling `super().__dunder__()` | ✅ Allowed |
> | Inside dunder method, calling `other_obj.__dunder__()` | ❌ Use operator/built-in |
> | Inside regular method, calling `self.__dunder__()` | ❌ Use built-in function |

*Implementation: The compiler emits different code based on static type:*
- *For primitives: direct C# operator or method call*
- *For Sharpy types with dunder: call to the generated method*
- *For built-in functions: type-appropriate dispatch (e.g., `len()` calls `.Count` or `__len__`)*

### Dunder Method Signatures

Dunder methods have compiler-enforced return types. The compiler validates that dunder method signatures match the expected protocol:

**Arithmetic Operators:**

| Dunder | Required Return Type | Notes |
|--------|----------------------|-------|
| `__add__(self, other: T)` | Same type as `self` or compatible | Binary `+` |
| `__sub__(self, other: T)` | Same type as `self` or compatible | Binary `-` |
| `__mul__(self, other: T)` | Same type as `self` or compatible | Binary `*` |
| `__truediv__(self, other: T)` | Same type as `self` or compatible | Binary `/` |
| `__floordiv__(self, other: T)` | `long` or float type | Binary `//` |
| `__mod__(self, other: T)` | Same type as `self` or compatible | Binary `%` |
| `__pow__(self, other: T)` | Same type as `self` or compatible | Binary `**` |
| `__neg__(self)` | Same type as `self` | Unary `-` |
| `__pos__(self)` | Same type as `self` | Unary `+` |

**Comparison Operators:**

| Dunder | Required Return Type |
|--------|----------------------|
| `__eq__(self, other: object)` | `bool` |
| `__eq__(self, other: T)` | `bool` |
| `__ne__(self, other: object)` | `bool` |
| `__ne__(self, other: T)` | `bool` |
| `__lt__(self, other: T)` | `bool` |
| `__le__(self, other: T)` | `bool` |
| `__gt__(self, other: T)` | `bool` |
| `__ge__(self, other: T)` | `bool` |

**Special Methods:**

| Dunder | Required Return Type | Notes |
|--------|----------------------|-------|
| `__str__(self)` | `str` | Human-readable string |
| `__repr__(self)` | `str` | Debug representation |
| `__hash__(self)` | `int` | Hash code |
| `__len__(self)` | `int` | Length/count |
| `__bool__(self)` | `bool` | Truthiness (for `if`, `while`, `and`, `or`, `not`) |
| `__true__()` | N/A | C# `operator true` (advanced, rarely needed) |
| `__false__()` | N/A | C# `operator false` (advanced, rarely needed) |
| `__contains__(self, item: T)` | `bool` | Membership test |
| `__iter__(self)` | `Iterator[T]` | Iteration |
| `__getitem__(self, key: K)` | `V` | Index access |
| `__setitem__(self, key: K, value: V)` | `None` | Index assignment |

**Compiler Enforcement:**

```python
class MyNumber:
    value: int

    def __init__(self, value: int):
        self.value = value

    # ✅ Correct return type
    def __eq__(self, other: object) -> bool:
        if not isinstance(other, MyNumber):
            return False
        return self.value == other.value

    # ❌ ERROR: __eq__ must return bool
    def __eq__(self, other: object) -> int:
        return self.value

    # ✅ Correct return type
    def __str__(self) -> str:
        return f"MyNumber({self.value})"

    # ❌ ERROR: __str__ must return str
    def __str__(self) -> int:
        return self.value

    # ✅ Correct return type
    def __hash__(self) -> int:
        return hash(self.value)

    # ❌ ERROR: __hash__ must return int
    def __hash__(self) -> str:
        return str(self.value)
```

**Parameter Types:**

While return types are strictly enforced, parameter types for `other` in binary operations can vary based on what operations the type supports:

```python
class Vector:
    x: double
    y: double

    # Vector + Vector
    def __add__(self, other: Vector) -> Vector:
        return Vector(self.x + other.x, self.y + other.y)

    # Vector * scalar (different parameter type)
    def __mul__(self, other: double) -> Vector:
        return Vector(self.x * other, self.y * other)
```

This also applies to comparison operators like `__lt__()`. For `__eq__()` and `__ne__()` specifically, at least one overload must accept `object` (`System.Object`) as its argument. Additional overloads can be made for other types. This is actually satisfied by default for Sharpy reference types in Sharpy because they all derive from `Sharpy.Core.Object` which implements these dunder methods.

### Dunder Inheritance and Internal Calls **[v0.1.0]**

While user code cannot call dunders directly, there are specific contexts where dunder calls are permitted.

#### Dunder Inheritance

Dunder methods are inherited like any other method:

```python
class Animal:
    name: str

    def __init__(self, name: str):
        self.name = name

    def __repr__(self) -> str:
        return f"Animal({self.name})"

class Dog(Animal):
    def __init__(self, name: str):
        super().__init__(name)

    # Inherits __repr__ from Animal

dog = Dog("Buddy")
print(repr(dog))  # Output: Animal(Buddy)
```

#### Overriding Dunders

Dunder methods can be overridden using `@override`:

```python
class Dog(Animal):
    def __init__(self, name: str):
        super().__init__(name)

    @override
    def __repr__(self) -> str:
        return f"Dog({self.name})"

dog = Dog("Buddy")
print(repr(dog))  # Output: Dog(Buddy)
```

**Note:** The `@override` decorator is **required** when overriding inherited dunder methods, just like any other virtual method. All inheritable dunder methods from base classes are implicitly `@virtual`.

**Overriding Dunders from `Sharpy.Core.Object`:**

Since all Sharpy classes inherit from `Sharpy.Core.Object`, which provides default implementations for `__str__`, `__repr__`, `__eq__`, `__ne__`, and `__hash__`, overriding these dunders requires `@override`:

```python
class MyClass:
    value: int

    def __init__(self, value: int):
        self.value = value

    # Must use @override since __str__ is inherited from Sharpy.Core.Object
    @override
    def __str__(self) -> str:
        return f"MyClass({self.value})"

    # Same for __eq__, __hash__, etc.
    @override
    def __eq__(self, other: object) -> bool:
        if not isinstance(other, MyClass):
            return False
        return self.value == other.value

    @override
    def __hash__(self) -> int:
        return hash(self.value)
```

This matches C# where overriding `ToString()`, `Equals()`, and `GetHashCode()` from `System.Object` requires the `override` keyword.

#### Base Class Dunder Calls

Within a dunder method, you may call the base class implementation via `super()`:

```python
class Child(Parent):
    @override
    def __repr__(self) -> str:
        return super().__repr__() + " (child)"  # ✅ OK

    @override
    def __eq__(self, other: object) -> bool:
        if not super().__eq__(other):           # ✅ OK
            return False
        # Additional checks...
        return True
```

#### Cross-Dunder Calls for Synthesis

Within a dunder method, you may call other dunders on `self` for synthesizing related operations:

```python
class Ordered:
    value: int

    def __eq__(self, other: object) -> bool:
        if not isinstance(other, Ordered):
            return False
        return self.value == other.value

    def __lt__(self, other: Ordered) -> bool:
        return self.value < other.value

    def __le__(self, other: Ordered) -> bool:
        return self.__lt__(other) or self.__eq__(other)  # ✅ OK

    def __ge__(self, other: Ordered) -> bool:
        return not self.__lt__(other)                    # ✅ OK

    def __ne__(self, other: object) -> bool:
        return not self.__eq__(other)                    # ✅ OK

    def __gt__(self, other: Ordered) -> bool:
        return not self.__le__(other)                    # ✅ OK
```

#### Restrictions

Dunder calls on `self` or `super()` are **only** permitted:
- Within a dunder method body
- As immediate call expressions (cannot be captured or passed)

```python
class Example:
    def __repr__(self) -> str:
        func = self.__eq__              # ❌ ERROR: Cannot capture dunder
        return str(self.__hash__())     # ✅ OK: Immediate call, cross-dunder

    def regular_method(self):
        self.__repr__()                 # ❌ ERROR: Not inside a dunder
        print(repr(self))               # ✅ OK: Use built-in function

    def __eq__(self, other: object) -> bool:
        return other.__eq__(self)       # ❌ ERROR: Not self or super()
```

#### Child Objects Use Built-in Functions

For calling dunder-like behavior on other objects (including fields), use operators or built-in functions:

```python
class Node:
    left: Node?
    right: Node?
    value: int

    def __repr__(self) -> str:
        left_repr = repr(self.left) if self.left is not None else "None"
        right_repr = repr(self.right) if self.right is not None else "None"
        return f"Node({self.value}, {left_repr}, {right_repr})"
        # NOT: self.left.__repr__()  # ❌ Would be error anyway

    def __eq__(self, other: object) -> bool:
        if not isinstance(other, Node):
            return False
        return self.value == other.value  # ✅ Use == operator
        # NOT: self.value.__eq__(other.value)  # ❌ Error
```

#### Summary Table

| Call Site | `self.__dunder__()` | `super().__dunder__()` | `other.__dunder__()` |
|-----------|--------------------|-----------------------|---------------------|
| Inside dunder method | ✅ Immediate only | ✅ Immediate only | ❌ Use operator/built-in |
| Outside dunder method | ❌ Error | ❌ Error | ❌ Use operator/built-in |

### Arithmetic Operators

```python
class Vector:
    x: double
    y: double

    def __init__(self, x: double, y: double):
        self.x = x
        self.y = y

    def __add__(self, other: Vector) -> Vector:
        return Vector(self.x + other.x, self.y + other.y)

    def __sub__(self, other: Vector) -> Vector:
        return Vector(self.x - other.x, self.y - other.y)

    def __mul__(self, scalar: double) -> Vector:
        return Vector(self.x * scalar, self.y * scalar)

    def __neg__(self) -> Vector:
        return Vector(-self.x, -self.y)
```

| Operator | Dunder Method | C# Operator |
|----------|---------------|-------------|
| `+` | `__add__` | `operator +` |
| `-` | `__sub__` | `operator -` |
| `*` | `__mul__` | `operator *` |
| `/` | `__truediv__` | `operator /` |
| `//` | `__floordiv__` | (method call) |
| `%` | `__mod__` | `operator %` |
| `**` | `__pow__` | (method call) |
| `-x` | `__neg__` | `operator -` (unary) |
| `+x` | `__pos__` | `operator +` (unary) |

*Implementation: ✅ Native - Generates both dunder method and C# operator overload.*

### Comparison Operators

```python
class Point:
    x: int
    y: int

    def __eq__(self, other: object) -> bool:
        if not isinstance(other, Point):
            return False
        return self.x == other.x and self.y == other.y

    def __lt__(self, other: Point) -> bool:
        return (self.x ** 2 + self.y ** 2) < (other.x ** 2 + other.y ** 2)
```

| Operator | Dunder Method | C# Method |
|----------|---------------|-----------|
| `==` | `__eq__` | `operator ==` + `Equals()` |
| `!=` | `__ne__` | `operator !=` |
| `<` | `__lt__` | `operator <` |
| `<=` | `__le__` | `operator <=` |
| `>` | `__gt__` | `operator >` |
| `>=` | `__ge__` | `operator >=` |

### Special Methods

| Method | Purpose | C# Mapping | Invoked Via |
|--------|---------|------------|-------------|
| `__str__` | String representation | `ToString()` override | `str(x)` |
| `__repr__` | Debug representation | Custom method | `repr(x)` |
| `__hash__` | Hash value | `GetHashCode()` override | `hash(x)` |
| `__len__` | Length | `Count` property | `len(x)` |
| `__contains__` | Membership test | `Contains()` method | `x in collection` |
| `__iter__` | Iteration | `GetEnumerator()` | `for x in obj` |
| `__getitem__` | Index access | Indexer `this[...]` | `obj[key]` |
| `__setitem__` | Index assignment | Indexer `this[...]` | `obj[key] = value` |
| `__delitem__` | Index deletion | (method call) | `del obj[key]` |

**`__contains__` Return Type:**

The `__contains__` dunder method must return `bool`. The `in` operator's result type is always `bool`, regardless of the implementation:

```python
class MyContainer:
    items: list[int]

    # Must return bool
    def __contains__(self, item: int) -> bool:
        return item in self.items

    # ❌ ERROR: __contains__ must return bool
    # def __contains__(self, item: int) -> int:
    #     return self.items.index(item)

c = MyContainer()
result: bool = 5 in c  # Always bool
```

**Note:** Users invoke these behaviors through the "Invoked Via" syntax, not by calling the dunder methods directly. See [Dunder Invocation Rules](#dunder-invocation-rules-v01) for details.

### Hashable Objects

For objects to be used as dictionary keys or in sets, they must implement `__hash__` and `__eq__`:

```python
class Coordinate:
    x: int
    y: int

    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y

    def __eq__(self, other: object) -> bool:
        if not isinstance(other, Coordinate):
            return False
        return self.x == other.x and self.y == other.y

    def __hash__(self) -> int:
        return hash((self.x, self.y))

# Now usable as dict key or in sets
locations: dict[Coordinate, str] = {}
coord = Coordinate(10, 20)
locations[coord] = "Home"  # Works because __hash__ and __eq__ defined
```

**Rules for Hashable Objects:**
- If `__eq__` is defined, `__hash__` must also be defined, and vice versa
- If `a == b`, then `hash(a) == hash(b)` must be true
- Hash value should not change during object lifetime
- Mutable objects should not implement `__hash__`

*Implementation: ✅ Native or 🔄 Lowered depending on the method.*

---

