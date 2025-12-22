# Dunder Methods

Sharpy inherits the syntax of Python's dunder methods, however the semantics
are, in most cases, different both at compile time and runtime.

## Constructor Method

The `__init__` dunder method maps directly to C#'s constructor methods. Like
C# constructor methods, the `__init__` dunder method is overloadable. Unlike
Python, `__init__` cannot be called by the user directly, it must be invoked
via the constructor syntax:

```python
class Foobar:
    def __init__(self):
        pass

a = Foobar()  # OK: Allowed in both Sharpy and Python, both implicitly invoke `__init__`
a.__init__()  # ERROR: Not allowed in Sharpy, but allowed in Python
```

There is one exception to this rule and that is within the `__init__` dunder
itself when invoking a constructor method overload in the same class as
dispatch, or when invoking the superclass's constructor method:

```python
class Bar:
    x: int

    def __init__(self, x: int):
        self.x = x

class Foo(Bar):
    def __init__(self, x: int):
        super().__init__(x)  # Invokes superclass's constructor

    def __init__(self):
        self.__init__(0)     # Invokes same-class overload
```

Note that the above example is purely for example, as the intent above could
be easily represented with a default parameter value for `x` in the derived
class's constructor method.

## Arithmetic Operators

Arithmetic dunder methods translate directly to C# static operators. They do
not exist as callable methods outside of cross-operator synthesis.

```python
struct NonZeroInt:
    value: int

    def __init__(self, value: int):
        if value == 0:
            raise ValueError("value cannot be 0")

        self.value = value

    def __add__(self, other: NonZeroInt) -> NonZeroInt:
        return NonZeroInt(self.value + other.value)

    def __add__(self, other: int) -> NonZeroInt:
        return self.__add__(NonZeroInt(other))
```

Generates C# with `self` becoming the left-hand side operand for regular
operators:

```csharp
struct NonZeroInt {
  int value;

  NonZeroInt(int value) {
    this.value = value;
  }

  public static NonZeroInt operator+(NonZeroInt lhs, NonZeroInt rhs) {
    return new NonZeroInt(lhs.value + rhs.value);
  }

  public static NonZeroInt operator+(NonZeroInt lhs, int rhs) {
    return lhs + new NonZeroInt(rhs);
  }
}
```

Reflected operators (e.g. `__radd__`) cause `self` to become the right-hand side
operand:

```python
   def __radd__(self, other: int) -> NonZeroInt:
      return NonZeroInt(other) + self
```

Generates C#:

```csharp
  public static NonZeroInt operator+(int lhs, NonZeroInt rhs) {
    return new NonZeroInt(lhs) + rhs;
  }
```

In-place operators (e.g. `__iadd__`) do not exist in Sharpy yet as C# 9 does
not support defining them. When Sharpy is updated to support C# 14, then
in-place operators will be available to define in Sharpy.

Assuming `T` is the class defining the dunder method, and `U` (if present)
could be any type, including `T` itself, and `V` (if present) could be any type,
including `T` or `U` (if present), then:

**Binary arithmetic operators**

| Dunder | C# Output |
|--------|-----------|
| `__add__(self, other: U) -> V` | `public static V operator +(T lhs, U rhs)` |
| `__floordiv__(self, other: U) -> V` | |
| `__mod__(self, other: U) -> V` | `public static V operator %(T lhs, U rhs)` |
| `__mul__(self, other: U) -> V` | `public static V operator -(T lhs, U rhs)` |
| `__pow__(self, other: U) -> V` | |
| `__sub__(self, other: U) -> V` | `public static V operator -(T lhs, U rhs)` |
| `__truediv__(self, other: U) -> V` | `public static V operator /(T lhs, U rhs)` |

**Reflected binary arithmetic operators**

| Dunder | C# Output |
|--------|-----------|
| `__radd__(self, other: U) -> V` | `public static V operator +(U lhs, T rhs)` |
| `__rfloordiv__(self, other: U) -> V` | |
| `__rmod__(self, other: U) -> V` | `public static V operator %(U lhs, T rhs)` |
| `__rmul__(self, other: U) -> V` | `public static V operator *(U lhs, T rhs)` |
| `__rpow__(self, other: U) -> V` | |
| `__rsub__(self, other: U) -> V` | `public static V operator -(U lhs, T rhs)` |
| `__rtruediv__(self, other: U) -> V` | `public static V operator /(U lhs, T rhs)` |

**Unary sign operators**
| `__neg__(self) -> T` | `public static T operator -(T self)` |
| `__pos__(self) -> T` | `public static T operator +(T self)` |

*Rules*
- *Arithmetic operators are always public and static.*
- *Arithmetic operators are applied based on the static declared type of the operands, with lookup considering implicit conversions (including casting up the class inheritance chain).*

**Comparison Operators:**

| Dunder | C# Output |
|--------|----------------------|
| `__eq__(self, other: U) -> bool` | |
| `__ne__(self, other: U) -> bool` | |
| `__ne__(self, other: U)` | |
| `__lt__(self, other: U)` | |
| `__le__(self, other: U)` | |
| `__gt__(self, other: U)` | |
| `__ge__(self, other: U)` | |

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
__delitem__
__invert__
__next__
__not__
__index__
__enter__
__exit__
__float__
__bytes__
__complex__
__format__
__reversed__
__call__
__matmul__
__and__
__or__
__xor__
__rshift__
__lshift__
__divmod__
__rdivmod__
__abs__
__round__
__trunc__
__floor__
__ceil__

__aenter__
__aexit__
__aiter__
__anext__
__await__

__del__
__copy__
__deepcopy__
__replace__
