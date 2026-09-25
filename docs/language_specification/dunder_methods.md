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

<!-- spec-sweep: error SPY0427 -->
```python
class Foobar:
    def __init__(self):
        pass

def main() -> None:
    a = Foobar()  # OK: Allowed in both Sharpy and Python, both implicitly invoke `__init__`
    a.__init__()  # ERROR (SPY0427): Not allowed in Sharpy, but allowed in Python
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
| `__ne__(self, other: U) -> bool` | `public static bool operator !=(T lhs, U rhs)` | If not defined, is synthesized by the compiler as `!(lhs == rhs)`; conversely a type declaring only `__ne__` gets `operator ==` synthesized as `!(lhs != rhs)` on the same `U` |
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

### Operand typing and dispatch

The right operand of a comparison against a user type is checked against the **selected
overload's parameter** under the same store rules as a call argument: an in-range constant
converts into an `int8` parameter, `Some(7)` into `int8?`, a literal into `LiteralString`, and a
mismatch is refused as the operator's own error (SPY0222), naming the parameter the overload
takes. An operand with no type of its own — `None()` — selects the unique `__eq__` overload
whose parameter is an Optional; with none (or several) the comparison is refused by name with
the spelling that would work:

```python
class D:
    def __eq__(self, other: int?) -> bool:
        return other is None

def main():
    print(D() == None())   # True — selects __eq__(int?)
    print(D() == Some(1))  # False
    # D() == 1             # SPY0604: 'int32' is not an Optional[int32]; construct it with Some(...)
```

A **bare `None`** operand dispatches to an `__eq__` whose parameter admits `None` — `T | None`
or `object` — exactly as CPython calls `__eq__(None)`. Any other `__eq__` keeps the reference
null check of `x == None` (`False`, no call; on a struct SPY0222 — write `is None`):

```python
class N:
    def __eq__(self, other: int | None) -> bool:
        return other is None

def main():
    print(N() == None)   # True — __eq__ is called with None
    print(None == N())   # True
```

A comparison whose **left** operand has no dunder for it dispatches to the right operand's
reflected dunder, as CPython does: `1 == d` is `d.__eq__(1)`, `1 < d` is `d.__gt__(1)` (refused
by name when the mirror is not declared). An **inherited** dunder decides exactly as an own one:
with `__eq__(self, other: str)` declared on `B`, `D(B)() == "a"` calls it.

## Conversion Methods

Conversion dunder methods map to C# explicit or implicit conversion operators:

| Dunder | C# Output | Notes |
|--------|-----------|-------|
| `__bool__(self) -> bool` | `public static bool operator true(T self)`, and `public static bool operator false(T self)` | The latter invokes the former and returns the negated value |
| `__str__(self) -> str` | `public static explicit operator string(T self)` and `public override string ToString()` | The former invokes the latter. `@override` is optional (implicit override of `System.Object.ToString`). |
| `@static __implicit__(val: S) -> T` | `public static implicit operator T(S val)` | Must be `@static`, exactly 1 param, one type must be enclosing |
| `@static __explicit__(val: S) -> T` | `public static explicit operator T(S val)` | Must be `@static`, exactly 1 param, one type must be enclosing |

## Special Methods

| Dunder | C# Output | Notes |
|--------|------------|-------|
| `__contains__(self, item: T) -> bool` | `bool Contains(T item)` method | Membership test (`in` operator) |
| `__format__(self, spec: str) -> str` | `public virtual string ToString(string? spec, IFormatProvider? formatProvider = null)`, and the type implements `System.IFormattable` | The body moves into IFormattable's `ToString`, opening with `spec ??= ""`; every format route (f-string hole, `format()`, `str.format`, t-string) hands it the spec, the empty one included. `super().__format__(s)` calls the base's. Synthesis is announced by SPY1001. Declaring the explicit `IFormattable` spelling (`to_string(fmt, provider)`) beside it is SPY0522. See [Types Without `__format__`](fstrings.md#types-without-__format__) |
| `__hash__(self) -> int` | `int GetHashCode()` override | Hash code. `@override` is optional (implicit override of `System.Object.GetHashCode`). |
| `__getitem__(self, key: K) -> V` | `this[K key] { get; }` indexer | Index access |
| `__iter__(self) -> T` | `IEnumerator<T> IEnumerable<T>.GetEnumerator()` | Iteration. Generator body: annotate with element type T. Non-generator body: annotate with the producer (`Iterator[T]`/`IEnumerator[T]`/`IEnumerable[T]`), unpeeled to T. See [Producer Annotations](#producer-annotations-for-__iter__-and-__reversed__) |
| `__len__(self) -> int` | `int Count` property | Length/count |
| `__next__(self) -> T` | `void IEnumerator<T>.MoveNext()` + `T Current` | Iterator protocol |
| `__reversed__(self) -> T` | Custom method `IEnumerator<T> GetReverseEnumerator()` | Reverse iteration. Generator body: annotate with element type T. Non-generator body: annotate with the producer (`Iterator[T]`/`IEnumerator[T]`/`IEnumerable[T]`), unpeeled to T. See [Producer Annotations](#producer-annotations-for-__iter__-and-__reversed__) |
| `__setitem__(self, key: K, value: V) -> None` | `this[K key] { set; }` indexer | Index assignment |

### Producer Annotations for `__iter__` and `__reversed__`

`__iter__` and `__reversed__` are **producer dunders**: their return annotation decides the element
type `T` of the interface the compiler synthesizes — `IEnumerable[T]` for `__iter__`,
`IReverseEnumerable[T]` for `__reversed__`. What the annotation *means* depends on the body:

| Body | The annotation names | Synthesized element |
|------|----------------------|---------------------|
| **generator** (contains `yield`) | the **element** | the annotation itself |
| generator, unannotated | — | `object` |
| **non-generator**, annotation is `Iterator[T]`, `IEnumerator[T]` or `IEnumerable[T]` | the **producer** | `T`, unpeeled from the annotation |
| **non-generator**, any other annotation | nothing — a plain method | **no interface is synthesized** |

A non-generator body returns a producer *object*, so its annotation names that producer and is
unpeeled to its sole type argument. Exactly three producer spellings are recognized: Sharpy's own
`Iterator[T]`, and the CLR names `IEnumerator[T]` and `IEnumerable[T]`. A name outside that set, or
one of those names with zero or more than one type argument, is not a producer.

**A non-producer annotation synthesizes nothing, and the class is simply not iterable.** The dunder
stays an ordinary method; every consumer refuses **by name**, not with an internal error:

| Consumer | Refusal |
|----------|---------|
| `list(c)` / `reversed(r)` | SPY0320 `Type 'C' is not iterable (missing '__iter__' method)`, SPY0203 `No overload of 'reversed' matches the argument types (R)` |
| a declared `IEnumerable[T]` / `IReverseEnumerable[T]` slot | SPY0220 `Cannot assign type 'C' to variable of type 'IEnumerable[int32]'` |

A **generator** annotated with a producer wrapper (`-> Iterator[int]` on a body that `yield`s) is
refused at the declaration itself with SPY0220 — the two spellings are not interchangeable, and the
message steers to the element spelling.

```spy
class Countdown:
    items: list[int]

    def __init__(self) -> None:
        self.items = [3, 2, 1]

    def __iter__(self) -> int:          # generator: the annotation IS the element
        for x in self.items:
            yield x

def main() -> None:
    c = Countdown()
    for x in c:
        print(x)
    print(list(c))
```

Output:

```
3
2
1
[3, 2, 1]
```

```spy
class Bag:
    items: list[int]

    def __init__(self) -> None:
        self.items = [3, 2, 1]

    def __iter__(self) -> Iterator[int]:        # producer: unpeeled to int
        return iter(self.items)

    def __reversed__(self) -> Iterator[int]:    # producer: unpeeled to int
        return reversed(self.items)

def main() -> None:
    b = Bag()
    e: IEnumerable[int] = b
    print(list(e))
    print(list(reversed(b)))
```

Output:

```
[3, 2, 1]
[1, 2, 3]
```

```spy
class Shelf:
    items: list[int]

    def __init__(self) -> None:
        self.items = [3, 2, 1]

    def __iter__(self) -> IEnumerable[int]:     # producer: unpeeled to int
        return iter(self.items)

def main() -> None:
    s = Shelf()
    for x in s:
        print(x)
    print(list(s))
```

Output:

```
3
2
1
[3, 2, 1]
```

Whatever the producer spelling, the body must return a value that **is** an iterator: the
synthesized member is `IEnumerator<T> GetEnumerator()`. Returning a `list` from a body annotated
`Iterator[T]` or `IEnumerator[T]` is refused with SPY0260 (`Cannot return type 'list[int32]' from
function expecting 'Iterator[int32]'`); the `IEnumerable[T]` spelling does not yet enforce this and
reaches an internal error instead — [#1930](https://github.com/antonsynd/sharpy/issues/1930).

**`__next__` overrides all of this.** When a class declares `__next__`, the element comes from
`__next__`'s return annotation and `__iter__`'s own annotation is not consulted at all — which is
why the self-returning `def __iter__(self) -> Self: return self` pairing works even though `Self` is
not a producer name.

## Unsupported Dunders

| Dunder | Status | Rationale |
|--------|--------|-----------|
| `__abs__(self) -> T` | Not supported | `Math.Abs()` doesn't dispatch to this |
| `__aenter__(self)` | Supported | Async context manager enter; maps to `AenterAsync()`. See [Context Managers](context_managers.md) |
| `__aexit__(self, ...)` | Supported | Async context manager exit; 1-arg and 3-arg forms. See [Context Managers](context_managers.md) |
| `__aiter__(self)` | Not supported yet | Complex feature |
| `__anext__(self)` | Not supported yet | Complex feature |
| `__await__(self)` | Not supported yet | Complex feature |
| `__call__(self, ...) -> T` | Supported | Callable objects: `obj(args)` dispatches to `obj.Invoke(args)`. Explicit `obj.__call__()` is refused (SPY0427) |
| `__ceil__(self) -> float` | Not supported | `Math.Ceiling()` doesn't dispatch to this |
| `__complex__(self) -> complex` | Not supported | Use explicit conversion methods |
| `__copy__(self) -> T` | Not supported | Use `ICloneable.Clone()` or explicit copy methods |
| `__deepcopy__(self) -> T` | Not supported | Use serialization or explicit deep copy methods |
| `__del__(self) -> None` | Not supported | Use `IDisposable` instead. |
| `__delitem__(self, key: K) -> None` | Not supported yet | Use `Remove(K key)` method directly |
| `__divmod__(self, other: int) -> int` | Not supported | `Math.DivRem` doesn't dispatch to this |
| `__enter__(self) -> T` | Supported | Context manager enter; maps to `Enter()`. See [Context Managers](context_managers.md) |
| `__exit__(self, ...)` | Supported | Context manager exit; 1-arg and 3-arg forms. See [Context Managers](context_managers.md) |
| `__float__(self) -> float` | Not supported | Not yet designed |
| `__floor__(self) -> float` | Not supported | `Math.Floor()` doesn't dispatch to this |
| `__floordiv__` | Not supported | Use `__div__` for `/` operator; `//` handled specially |
| `__index__(self) -> int` | Not supported | Not yet designed, but should be used for integer conversion in slice contexts | |
| `__int__(self) -> int` | Not supported | Not yet designed |
| `__matmul__(self, other: T) -> U` | Experimental (behind `matmul` feature) | `@` matrix multiplication (PEP 465). C# has no `@` operator, so it lowers to a `MatMul(other)` instance method rather than a C# operator overload. Enable with `--enable-feature=matmul` or `<Features>matmul</Features>`; ungated use is rejected with SPY0331. `@=` augmented assignment is also supported. |
| `__round__(self, ndigits: int?) -> T` | Not supported | `Math.Round()` doesn't dispatch to this |
| `__trunc__(self) -> T` | Not supported | `Math.Truncate()` doesn't dispatch to this |
| `__pow__(self, exponent: int) -> float` | Not supported | `Math.Pow()` doesn't dispatch to this |
| `__repr__(self) -> str` | Supported | Maps to `ToString()` (same as `__str__`) on a user class, so a user class's `repr` is its `str`. Used by `@dataclass` synthesis for auto-generated string representation. A Core/Stdlib type whose python `repr` differs from its `str` spells `__repr__` as the interface `Sharpy.IRepr` (`string Repr()`), which `repr()`, `!r`, `ascii()` and every container element ask before `ToString()`: `Optional` (`Some(1)` / `None()`), `Template`/`Interpolation`, and the `datetime` types (`datetime.date(2020, 1, 2)`) |

## Dunder Method Invocation Rules

See [dunder_invocation_rules.md](dunder_invocation_rules.md).
