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

In the tables below, assuming `T` is the class defining the dunder method, and `U` (if present)
could be any type, including `T` itself, and `V` (if present) could be any type,
including `T` or `U` (if present), then:

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

*Rules*
- *Arithmetic operators are always public and static.*
- *Arithmetic operators are applied based on C# static resolution rules.*
  - *The chosen operator is based on the static declared type of the operands.*
  - *Lookup considers the availability of implicit conversions and/or casting up the class inheritance chain.*

## Bitwise Operators

Bitwise dunder methods translate directly to C# static operators. They do
not exist as callable methods outside of cross-operator synthesis, similarly
to the arithmetic ones.

In the tables below, assuming `T` is the class defining the dunder method, and `U` (if present)
could be any type, including `T` itself, and `V` (if present) could be any type,
including `T` or `U` (if present), then:

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

**Comparison Operators:**

| Dunder | C# Output |
|--------|----------------------|
| `__eq__(self, other: U) -> bool` | `public static bool operator ==(T lhs, U rhs)` and `public override bool Equals(U rhs)` |
| `__ne__(self, other: U) -> bool` | `public static bool operator !=(T lhs, U rhs)` |
| `__lt__(self, other: U) -> bool` | `public static bool operator <(T lhs, U rhs)` |
| `__le__(self, other: U) -> bool` | `public static bool operator <=(T lhs, U rhs)` |
| `__gt__(self, other: U) -> bool` | `public static bool operator >(T lhs, U rhs)` |
| `__ge__(self, other: U) -> bool` | `public static bool operator >=(T lhs, U rhs)` |

## Conversion Methods

Conversion dunder methods map to C# explicit or implicit conversion operators:

| Dunder | C# Output | Notes |
|--------|-----------|-------|
| `__str__(self) -> str` | `public override string ToString()` | Also provides explicit `operator string` |
| `__int__(self) -> int` | `public static explicit operator int(T self)` | |
| `__float__(self) -> double` | `public static explicit operator double(T self)` | |
| `__bool__(self) -> bool` | `public static bool operator true(T self)` + `operator false` | |

**Numeric conversion dunders (all explicit operators):**

| Dunder | C# Output |
|--------|-----------|
| `__int__` | `explicit operator int` |
| `__float__` | `explicit operator double` |
| `__double__` | `explicit operator double` |
| `__decimal__` | `explicit operator decimal` |
| `__long__` | `explicit operator long` |
| `__short__` | `explicit operator short` |
| `__byte__` | `explicit operator byte` |
| `__sbyte__` | `explicit operator sbyte` |
| `__uint__` | `explicit operator uint` |
| `__ulong__` | `explicit operator ulong` |
| `__ushort__` | `explicit operator ushort` |

## Special Methods

| Dunder | Required Return Type | C# Mapping | Notes |
|--------|----------------------|------------|-------|
| `__str__(self)` | `str` | `ToString()` override | Human-readable string |
| `__repr__(self)` | `str` | Custom method or `ToString()` | Debug representation |
| `__hash__(self)` | `int` | `GetHashCode()` override | Hash code |
| `__len__(self)` | `int` | `Count` property | Length/count |
| `__bool__(self)` | `bool` | `operator true`/`operator false` | Truthiness |
| `__contains__(self, item: T)` | `bool` | `Contains(T item)` method | Membership test (`in` operator) |
| `__iter__(self)` | `Iterator[T]` | `IEnumerable<T>.GetEnumerator()` | Iteration |
| `__next__(self)` | `T` | `IEnumerator<T>.MoveNext()` + `Current` | Iterator protocol |
| `__getitem__(self, key: K)` | `V` | `this[K key] { get; }` indexer | Index access |
| `__setitem__(self, key: K, value: V)` | `None` | `this[K key] { set; }` indexer | Index assignment |
| `__delitem__(self, key: K)` | `None` | `Remove(K key)` method | Index deletion |
| `__call__(self, ...)` | varies | `Invoke(...)` method | Callable objects |
| `__index__(self)` | `int` | Used for integer conversion in slice contexts | |
| `__format__(self, spec: str)` | `str` | `IFormattable.ToString(format, provider)` | Custom formatting |
| `__reversed__(self)` | `Iterator[T]` | `Reverse()` method or custom | Reverse iteration |

## Context Manager Methods

| Dunder | C# Mapping | Notes |
|--------|------------|-------|
| `__enter__(self)` | `IDisposable` pattern / resource acquisition | Returns resource |
| `__exit__(self, exc_type, exc_val, exc_tb)` | `Dispose()` or exception handling | Cleanup |

## Async Context Manager Methods

| Dunder | C# Mapping | Notes |
|--------|------------|-------|
| `__aenter__(self)` | `IAsyncDisposable` pattern | Async resource acquisition |
| `__aexit__(self, exc_type, exc_val, exc_tb)` | `DisposeAsync()` | Async cleanup |
| `__aiter__(self)` | `IAsyncEnumerable<T>` | Async iteration |
| `__anext__(self)` | `IAsyncEnumerator<T>` | Async iterator protocol |
| `__await__(self)` | Custom awaiter | Awaitable objects |

## Math Methods

| Dunder | C# Mapping | Notes |
|--------|------------|-------|
| `__abs__(self)` | Custom `Abs()` method | `Math.Abs()` doesn't dispatch to this |
| `__round__(self, ndigits: int?)` | Custom `Round()` method | `Math.Round()` doesn't dispatch |
| `__floor__(self)` | Custom `Floor()` method | `Math.Floor()` doesn't dispatch |
| `__ceil__(self)` | Custom `Ceil()` method | `Math.Ceiling()` doesn't dispatch |
| `__trunc__(self)` | Custom `Trunc()` method | Truncation toward zero |
| `__divmod__(self, other)` | Returns `(quotient, remainder)` tuple | |

## Unsupported/Discouraged Dunders

| Dunder | Status | Rationale |
|--------|--------|-----------|
| `__del__` | Discouraged | Maps to `~Finalizer()` but non-deterministic in .NET. Use `IDisposable` instead. |
| `__copy__` | Not supported | Use `ICloneable.Clone()` or explicit copy methods |
| `__deepcopy__` | Not supported | Use serialization or explicit deep copy methods |
| `__pow__` | Not supported | `**` is not an overloadable operator in C# |
| `__floordiv__` | Not supported | Use `__div__` for `/` operator; `//` handled specially |
| `__matmul__` | Not supported | `@` operator not available in C# |
| `__complex__` | Not supported | Use explicit conversion methods |

## Dunder Method Invocation Rules

Dunder methods in Sharpy are **not directly callable** by user code (unlike Python), except in specific contexts:

**Allowed direct calls:**
```python
class Foo:
    def __init__(self, x: int):
        self.x = x

    def __init__(self):
        self.__init__(0)  # ✅ OK: calling overload from __init__

    def __eq__(self, other: Foo) -> bool:
        if not super().__eq__(other):  # ✅ OK: super() in dunder
            return False
        return self.x == other.x
```

**Disallowed direct calls:**
```python
obj = Foo()
obj.__init__(5)     # ❌ ERROR: cannot call __init__ directly
obj.__str__()       # ❌ ERROR: use str(obj) instead
obj.__len__()       # ❌ ERROR: use len(obj) instead
```

*Implementation*
- *🔄 Lowered - Dunder methods are transformed to their C# equivalents during code generation*
- *Direct dunder calls are blocked during semantic analysis (except in allowed contexts)*
