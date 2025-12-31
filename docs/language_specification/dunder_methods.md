# Dunder Methods

Sharpy inherits the syntax of Python's dunder methods, however the semantics
are, in most cases, different both at compile time and runtime.

Note, in the tables below, the generic type `T` is the class defining the dunder method. `U` (if present)
could be any type, including `T` itself, and `V` (if present) could be any type,
including `T` or `U` (if present).

Also, unless stated otherwise:
- *Operators from dunder methods are always public and static.*
- *Operators from dunder methods are applied based on C# static resolution rules.*
  - *The chosen operator is based on the static declared type of the operands.*
  - *Lookup considers the availability of implicit conversions and/or casting up the class inheritance chain.*

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

**Binary arithmetic operators**

Note that Sharpy does not support `__pow__` or `__floordiv__` as these are not
overridable operators in C#, and as a result, `__truediv__` is renamed to
`__div__` to reflect the lack of a contrasting `__floordiv__`.

| Dunder | C# Output |
|--------|-----------|
| `__add__(self, other: U) -> V` | `public static V operator +(T lhs, U rhs)` |
| `__div__(self, other: U) -> V` | `public static V operator /(T lhs, U rhs)` |
| `__mod__(self, other: U) -> V` | `public static V operator %(T lhs, U rhs)` |
| `__mul__(self, other: U) -> V` | `public static V operator -(T lhs, U rhs)` |
| `__sub__(self, other: U) -> V` | `public static V operator -(T lhs, U rhs)` |

**Reflected binary arithmetic operators**

| Dunder | C# Output |
|--------|-----------|
| `__radd__(self, other: U) -> V` | `public static V operator +(U lhs, T rhs)` |
| `__rdiv__(self, other: U) -> V` | `public static V operator /(U lhs, T rhs)` |
| `__rmod__(self, other: U) -> V` | `public static V operator %(U lhs, T rhs)` |
| `__rmul__(self, other: U) -> V` | `public static V operator *(U lhs, T rhs)` |
| `__rsub__(self, other: U) -> V` | `public static V operator -(U lhs, T rhs)` |

**Unary sign operators**

| Dunder | C# Output |
|--------|-----------|
| `__neg__(self) -> U` | `public static U operator -(T self)` |
| `__pos__(self) -> U` | `public static U operator +(T self)` |

## Bitwise Operators

Bitwise dunder methods translate directly to C# static operators. They do
not exist as callable methods outside of cross-operator synthesis, similarly
to the arithmetic ones.

**Binary bitwise operators**

| Dunder | C# Output |
|--------|-----------|
| `__and__(self, other: U) -> V` | `public static V operator &(T lhs, U rhs)` |
| `__lshift__(self, other: U) -> V` | `public static V operator <<(T lhs, U rhs)` |
| `__or__(self, other: U) -> V` | `public static V operator \|(T lhs, U rhs)` |
| `__rshift__(self, other: U) -> V` | `public static V operator >>(T lhs, U rhs)` |
| `__xor__(self, other: U) -> V` | `public static V operator ^(T lhs, U rhs)` |

**Reflected binary bitwise operators**

| Dunder | C# Output |
|--------|-----------|
| `__rand__(self, other: U) -> V` | `public static V operator &(U lhs, T rhs)` |
| `__rlshift__(self, other: U) -> V` | `public static V operator <<(U lhs, T rhs)` |
| `__ror__(self, other: U) -> V` | `public static V operator \|(U lhs, T rhs)` |
| `__rrshift__(self, other: U) -> V` | `public static V operator >>(U lhs, T rhs)` |
| `__rxor__(self, other: U) -> V` | `public static V operator ^(U lhs, T rhs)` |

**Unary bitwise operators**

| Dunder | C# Output |
|--------|-----------|
| `__invert__(self) -> U` | `public static U operator ~(T self)` |

## Comparison Operators

| Dunder | C# Output | Notes |
|--------|-----------|-------|
| `__eq__(self, other: U) -> bool` | `public static bool operator ==(T lhs, U rhs)` and `public override bool Equals(U rhs)` | The former invokes the latter |
| `__ne__(self, other: U) -> bool` | `public static bool operator !=(T lhs, U rhs)` | |
| `__lt__(self, other: U) -> bool` | `public static bool operator <(T lhs, U rhs)` | |
| `__le__(self, other: U) -> bool` | `public static bool operator <=(T lhs, U rhs)` | |
| `__gt__(self, other: U) -> bool` | `public static bool operator >(T lhs, U rhs)` | |
| `__ge__(self, other: U) -> bool` | `public static bool operator >=(T lhs, U rhs)` | |

## Conversion Methods

Conversion dunder methods map to C# explicit or implicit conversion operators:

| Dunder | C# Output | Notes |
|--------|-----------|-------|
| `__bool__(self) -> bool` | `public static bool operator true(T self)` and `public static bool operator false(T self)` | The latter invokes the former and returns the negated value |
| `__float__(self) -> float` | `public static explicit operator float(T self)` | |
| `__int__(self) -> int` | `public static explicit operator int(T self)` | |
| `__str__(self) -> str` | `public override string ToString()` and `public static explicit operator string(T self)` | The latter invokes the former |

**Numeric conversion dunders (all explicit operators):**

Also includes numeric conversion methods from above for thoroughness.

| Dunder | C# Output |
|--------|-----------|
| `__byte__(self) -> byte` | `public static explicit operator byte(T self)` |
| `__decimal__(self) -> decimal` | `public static explicit operator decimal(T self)` |
| `__double__(self) -> double` | `public static explicit operator double(T self)` |
| `__float__(self) -> float` | `public static explicit operator float(T self)` |
| `__int__(self) -> int` | `public static explicit operator int(T self)` |
| `__long__(self) -> long` | `public static explicit operator long(T self)` |
| `__short__(self) -> short` | `public static explicit operator short(T self)` |
| `__sbyte__(self) -> sbyte` | `public static explicit operator sbyte(T self)` |
| `__uint__(self) -> uint` | `public static explicit operator uint(T self)` |
| `__ulong__(self) -> ulong` | `public static explicit operator ulong(T self)` |
| `__ushort__(self) -> ushort` | `public static explicit operator ushort(T self)` |

## Special Methods

| Dunder | Required Return Type | C# Mapping | Notes |
|--------|----------------------|------------|-------|
| `__contains__(self, item: T)` | `bool` | `Contains(T item)` method | Membership test (`in` operator) |
| `__hash__(self)` | `int` | `GetHashCode()` override | Hash code |
| `__format__(self, spec: str)` | `str` | `IFormattable.ToString(format, provider)` | Custom formatting |
| `__getitem__(self, key: K)` | `V` | `this[K key] { get; }` indexer | Index access |
| `__index__(self)` | `int` | Used for integer conversion in slice contexts | |
| `__iter__(self)` | `Iterator[T]` | `IEnumerable<T>.GetEnumerator()` | Iteration |
| `__len__(self)` | `int` | `Count` property | Length/count |
| `__next__(self)` | `T` | `IEnumerator<T>.MoveNext()` + `Current` | Iterator protocol |
| `__reversed__(self)` | `Iterator[T]` | `Reverse()` method or custom | Reverse iteration |
| `__setitem__(self, key: K, value: V)` | `None` | `this[K key] { set; }` indexer | Index assignment |

## Unsupported Dunders

| Dunder | Status | Rationale |
|--------|--------|-----------|
| `__abs__(self)` | Not supported | `Math.Abs()` doesn't dispatch to this |
| `__aenter__(self)` | Not supported yet | Complex feature |
| `__aexit__(self, exc_type, exc_val, exc_tb)` | Not supported yet | Complex feature |
| `__aiter__(self)` | Not supported yet | Complex feature |
| `__anext__(self)` | Not supported yet | Complex feature |
| `__await__(self)` | Not supported yet | Complex feature |
| `__call__` | Not supported | C# has no callable object protocol; use explicit `Invoke()` method |
| `__ceil__(self)` | Not supported | `Math.Ceiling()` doesn't dispatch to this |
| `__complex__` | Not supported | Use explicit conversion methods |
| `__copy__` | Not supported | Use `ICloneable.Clone()` or explicit copy methods |
| `__deepcopy__` | Not supported | Use serialization or explicit deep copy methods |
| `__del__` | Not supported | Use `IDisposable` instead. |
| `__delitem__(self, key: K)` | Not supported yet | Use `Remove(K key)` method directly |
| `__divmod__(self, other)` | Not supported | `Math.DivRem` doesn't dispatch to this |
| `__enter__(self)` | Not supported yet | Use `IDisposable` instead |
| `__exit__(self, exc_type, exc_val, exc_tb)` | Not supported yet | Use `IDisposable` instead |
| `__floor__(self)` | Not supported | `Math.Floor()` doesn't dispatch to this |
| `__floordiv__` | Not supported | Use `__div__` for `/` operator; `//` handled specially |
| `__matmul__` | Not supported | `@` operator not available in C# |
| `__round__(self, ndigits: int?)` | Not supported | `Math.Round()` doesn't dispatch to this |
| `__trunc__(self)` | Not supported | `Math.Truncate()` doesn't dispatch to this |
| `__pow__` | Not supported | `**` is not an overloadable operator in C# |
| `__repr__` | Not supported | No direct C# equivalent; use `__str__` for string representation |

## Dunder Method Invocation Rules

See [dunder_invocation_rules.md](dunder_invocation_rules.md).
