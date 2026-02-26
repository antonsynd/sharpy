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

In general, Sharpy dunder methods are compiler aliases to C# methods/properties or compiler-intrinsic synthesis of inherited code patterns from Python (e.g. `__iter__()` and `__next__()`, or `__enter__()` and `__exit__()`). With the exception of cross-dunder synthesis (e.g. `__le__()` possibly invoking `__lt__()` and `__eq__()`, or `__init__()` invoking the super class's, `super().__init__()`, or `__init__()` calling another constructor overload in the same class as dispatch, etc.), dunders are only a compile-time construct and do not exist with their dunder name at runtime.

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

Reverse operators (e.g. `__radd__`) do not exist in Sharpy.

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
| `__mul__(self, other: U) -> V` | `public static V operator *(T lhs, U rhs)` |
| `__sub__(self, other: U) -> V` | `public static V operator -(T lhs, U rhs)` |

**Unary sign operators**

| Dunder | C# Output |
|--------|-----------|
| `__neg__(self) -> U` | `public static U operator -(T self)` |
| `__pos__(self) -> U` | `public static U operator +(T self)` |

## Bitwise Operators

Bitwise dunder methods translate directly to C# static operators. They do
not exist as callable methods outside of cross-operator synthesis, similar
to the arithmetic ones.

**Binary bitwise operators**

| Dunder | C# Output |
|--------|-----------|
| `__and__(self, other: U) -> V` | `public static V operator &(T lhs, U rhs)` |
| `__lshift__(self, other: U) -> V` | `public static V operator <<(T lhs, U rhs)` |
| `__or__(self, other: U) -> V` | `public static V operator \|(T lhs, U rhs)` |
| `__rshift__(self, other: U) -> V` | `public static V operator >>(T lhs, U rhs)` |
| `__xor__(self, other: U) -> V` | `public static V operator ^(T lhs, U rhs)` |

**Unary bitwise operators**

| Dunder | C# Output |
|--------|-----------|
| `__invert__(self) -> U` | `public static U operator ~(T self)` |

## Comparison Operators

| Dunder | C# Output | Notes |
|--------|-----------|-------|
| `__eq__(self, other: U) -> bool` | `public static bool operator ==(T lhs, U rhs)` and `public bool Equals(U rhs)` | 1:1 mapping. `override` only when `U` is `object`. |
| `__ne__(self, other: U) -> bool` | `public static bool operator !=(T lhs, U rhs)` | If not defined, is synthesized by the compiler as `!(lhs == rhs)` |
| `__lt__(self, other: U) -> bool` | `public static bool operator <(T lhs, U rhs)` | |
| `__le__(self, other: U) -> bool` | `public static bool operator <=(T lhs, U rhs)` | |
| `__gt__(self, other: U) -> bool` | `public static bool operator >(T lhs, U rhs)` | |
| `__ge__(self, other: U) -> bool` | `public static bool operator >=(T lhs, U rhs)` | |

Each `__eq__` overload generates a corresponding `Equals` overload with matching parameter type.
Only `__eq__(self, other: object)` generates `override bool Equals(object)` (overrides `System.Object`).
`@override` is implicit for the `object` overload (per the implicit override rule for Object methods).

**Warning SPY0454**: If any `__eq__` overload exists but none has parameter type `object`, the compiler
warns that collections (`set`, `dict`) will use reference equality.

Note that if a Sharpy user type has no `__eq__(self, other: object)` user override,
the one inherited from its base type is used. Additionally, defining an override of
`__eq__(self, other: object)` (specifically that override of that overload) without
an override `__hash__(self)` is a compile-time error. C# warns when types override
`Equals()` but not `GetHashCode()` and vice versa, but Sharpy treats this as an error.

Similarly, the opposite case of overriding `__hash__(self)` without an override
of `__eq__(self, other: object)` is also a compile-time error.

## Conversion Methods

Conversion dunder methods map to C# explicit or implicit conversion operators:

| Dunder | C# Output | Notes |
|--------|-----------|-------|
| `__bool__(self) -> bool` | `public static bool operator true(T self)`, and `public static bool operator false(T self)` | The latter invokes the former and returns the negated value |
| `__str__(self) -> str` | `public static explicit operator string(T self)` and `public override string ToString()` | The former invokes the latter. `@override` is optional (implicit override of `System.Object.ToString`). |

## Special Methods

| Dunder | C# Output | Notes |
|--------|------------|-------|
| `__contains__(self, item: T) -> bool` | `bool Contains(T item)` method | Membership test (`in` operator) |
| `__hash__(self) -> int` | `int GetHashCode()` override | Hash code. `@override` is optional (implicit override of `System.Object.GetHashCode`). |
| `__getitem__(self, key: K) -> V` | `this[K key] { get; }` indexer | Index access |
| `__iter__(self) -> T` | `IEnumerator<T> IEnumerable<T>.GetEnumerator()` | Iteration (generator: annotate with element type T; compiler wraps to `IEnumerator<T>`) |
| `__len__(self) -> int` | `int Count` property | Length/count |
| `__next__(self) -> T` | `void IEnumerator<T>.MoveNext()` + `T Current` | Iterator protocol |
| `__reversed__(self) -> T` | Custom method `IEnumerator<T> GetReverseEnumerator()` | Reverse iteration (generator: annotate with element type T; compiler wraps to `IEnumerator<T>`) |
| `__setitem__(self, key: K, value: V) -> None` | `this[K key] { set; }` indexer | Index assignment |

## Unsupported Dunders

| Dunder | Status | Rationale |
|--------|--------|-----------|
| `__abs__(self) -> T` | Not supported | `Math.Abs()` doesn't dispatch to this |
| `__aenter__(self)` | Not supported yet | Complex feature |
| `__aexit__(self, exc_type, exc_val, exc_tb)` | Not supported yet | Complex feature |
| `__aiter__(self)` | Not supported yet | Complex feature |
| `__anext__(self)` | Not supported yet | Complex feature |
| `__await__(self)` | Not supported yet | Complex feature |
| `__call__` | Not supported | C# has no callable object protocol; use explicit `Invoke()` method |
| `__ceil__(self) -> float` | Not supported | `Math.Ceiling()` doesn't dispatch to this |
| `__complex__(self) -> complex` | Not supported | Use explicit conversion methods |
| `__copy__(self) -> T` | Not supported | Use `ICloneable.Clone()` or explicit copy methods |
| `__deepcopy__(self) -> T` | Not supported | Use serialization or explicit deep copy methods |
| `__del__(self) -> None` | Not supported | Use `IDisposable` instead. |
| `__delitem__(self, key: K) -> None` | Not supported yet | Use `Remove(K key)` method directly |
| `__divmod__(self, other: int) -> int` | Not supported | `Math.DivRem` doesn't dispatch to this |
| `__enter__(self) -> T` | Not supported yet | Use `IDisposable` instead |
| `__exit__(self, exc_type: System.Type, exc_val: Exception?, exc_tb: object)` | Not supported yet | Use `IDisposable` instead |
| `__float__(self) -> float` | Not supported | Not yet designed |
| `__floor__(self) -> float` | Not supported | `Math.Floor()` doesn't dispatch to this |
| `__floordiv__` | Not supported | Use `__div__` for `/` operator; `//` handled specially |
| `__format__(self, spec: str)` | Not supported | Not yet designed, but possibly synthesizes `IFormattable.ToString(format, provider)` |
| `__index__(self) -> int` | Not supported | Not yet designed, but should be used for integer conversion in slice contexts | |
| `__int__(self) -> int` | Not supported | Not yet designed |
| `__matmul__(self, other: T) -> U` | Not supported | `@` operator not available in C# |
| `__round__(self, ndigits: int?) -> T` | Not supported | `Math.Round()` doesn't dispatch to this |
| `__trunc__(self) -> T` | Not supported | `Math.Truncate()` doesn't dispatch to this |
| `__pow__(self, exponent: int) -> float` | Not supported | `Math.Pow()` doesn't dispatch to this |
| `__repr__(self) -> str` | Not supported | No direct C# equivalent; use `__str__` for string representation |

## Dunder Method Invocation Rules

See [dunder_invocation_rules.md](dunder_invocation_rules.md).
