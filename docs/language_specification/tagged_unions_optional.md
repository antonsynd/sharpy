# Optional Type

> **`T?` is syntactic sugar for `Optional[T]`.** The `T?` shorthand is the preferred way to express optional values in Sharpy-native code.

The `Optional[T]` type is a special tagged union provided by the Sharpy standard library for representing values that may or may not be present. This is similar to Rust's `Option` type.

`Optional[T]` is a **struct** — no heap allocation for returning optional values, just a bool + value (like `Nullable<T>` but with tagged union semantics).

## Definition

<!-- spec-sweep: fragment -->
```python
union Optional[T]:
    case Some(value: T)
    case None()
```

The `Optional` type is part of the standard library and provides special syntax and operators for ergonomic optional value handling.

## Creating Optional Values

`Optional[T]` is constructed exclusively through `Some(value)` and `None()`:

```python
value: int? = Some(42)
empty: int? = None()
```

`Some(…)` and `None()` are the only spellings that work. Every spelling of the qualified form —
`Optional.Some(…)`, the indexed `Optional[int].Some(…)`, and an aliased or module-qualified
`Optional` — is **refused by name** with SPY0608 and a steer to the bare form (#1758, #1856), in call
and in value position alike; none is a second way to write the same thing:

```python
v: int? = Some(42)                    # the spelling
# w: int? = Optional.Some(42)         # SPY0608 — use the bare form `Some(42)`
# x: int? = Optional[int].Some(42)    # SPY0608 — the indexed receiver is refused too
# e: int? = Optional.None()           # SPY0608 — use the bare form `None()`
```

The four builtin case names `Some`, `None`, `Ok` and `Err` are RESERVED for the same reason: a
declaration that takes one would shadow the constructor, so it is refused with SPY0212 rather than
warned — `def Some(x: int) -> int`, and a user union whose own case is named `Some`, both draw
*"'Some' is a builtin tagged-union constructor; rebinding this name would shadow the builtin form"*.
The steer offers the backtick-escaped spelling (`` def `Some` ``, `` case `Some` ``) for a declaration
that really wants the name.

A bare value or bare `None` is **not** accepted for `T?` — use `Some(…)` or `None()`:

```python
# x: int? = 1       # SPY0604 — use Some(1)
# y: int? = None     # SPY0604 — use None(), or declare y: int | None
```

Bare `None` belongs to `T | None` (C# nullable interop) — see [Nullable Types](nullable_types.md). The `maybe` expression bridges a `T` value into `T?`: `z: int? = maybe x` wraps `x` in `Some` when non-null.

The one place a bare payload value is accepted is **inside a narrowing** of the Optional name: the
store is classified against the payload first, re-wraps, and the narrowing survives (the payload
rule — see [Stores Use the Declared Type](type_narrowing.md#stores-use-the-declared-type)):

```python
def main() -> None:
    x: int? = Some(10)
    if x is not None:
        x += 5          # payload store — x re-wraps as Some(15) and stays narrowed
        n: int = x      # the narrowing survives the store
        print(n)
        x = 7           # payload store — Some(7)
        print(x)
    print(x)
```

```
15
7
7
```

Outside a narrowing (`if True:`, a `while` body, a nested `def`) the declared slot decides and a bare
payload is SPY0604 as above.

## Pattern Matching

Use pattern matching to handle both Some and None cases:

```python
class User:
    name: str

    def __init__(self, name: str):
        self.name = name

def find_user(user_id: int, database: dict[int, User]) -> User?:
    if user_id in database:
        return Some(database[user_id])
    return None()

def main():
    database: dict[int, User] = {123: User("ada")}
    match find_user(123, database):
        case Some(user):
            print(f"Found user: {user.name}")     # Found user: ada
        case None:
            print("User not found")
    match find_user(999, database):
        case Some(user):
            print(f"Found user: {user.name}")
        case None:
            print("User not found")               # User not found
```

Both returns construct the Optional: `Some(user)` for the present case and `None()` for the empty
one. A bare `return None` here is **SPY0604** — `None` belongs to `User | None`, not to `User?`
(see [Creating Optional Values](#creating-optional-values)). `case None:` in the match is a
*pattern*, not the `None` value, and is unaffected.

### Matching the payload by type

A bare **type pattern** naming the payload (`case str():` over a `str?`) is **refused** with
SPY0498: `Optional` is a tagged union and matches through its constructor cases, exactly as
`Result` does, so the payload pattern would be a second spelling of `case Some(v):` (ruled on
#1510). The diagnostic steers to the constructor cases:

<!-- spec-sweep: error SPY0498 -->
```python
def describe(x: str?) -> str:
    match x:
        case str():          # SPY0498 — match through 'case Some(v):' / 'case None():'
            return "a string"
        case None():
            return "nothing"
```

Write the cases instead. A check on the payload's type — a subtype of the declared payload —
goes *inside* `Some(...)`, and `case None():` (or a wildcard) takes absence:

```python
class Animal:
    pass


class Dog(Animal):
    def bark(self) -> str:
        return "woof"


def describe(x: str?) -> str:
    match x:
        case Some(s):
            return "a string: " + s
        case None():
            return "nothing"


def speak(a: Animal?) -> str:
    match a:
        case Some(Dog() as d):
            return d.bark()
        case _:
            return "not a dog"


def main() -> None:
    print(describe(Some("ab")), describe(None()))
    print(speak(Some(Dog())), speak(Some(Animal())), speak(None()))
```

```
a string: ab nothing
woof not a dog not a dog
```

The scrutinee is **not** unwrapped to make this work: `unwrap()` throws on an empty Optional, so an
unwrapped subject could never reach a `None()` arm. The constructor cases destructure the Optional in
place. (After `if x is not None:` the name is the payload type itself, so a plain type pattern on it
is an ordinary type pattern.)

## Common Methods

The builtin `Optional[T]` type provides properties and methods for inspecting and
extracting the contained value:

| Member | Type | Description |
|--------|------|-------------|
| `is_some` | `bool` (property) | `True` when the Optional contains a value |
| `is_none` | `bool` (property) | `True` when the Optional is empty |
| `unwrap()` | `T` | Returns the value; raises on empty |
| `unwrap_or(default)` | `T` | Returns the value or `default` |
| `unwrap_or_else(f)` | `T` | Returns the value or calls `f()` |
| `map(f)` | `U?` | Transforms the contained value if present |

```python
o: int? = Some(42)
print(o.is_some)        # True
print(o.is_none)        # False
print(o.unwrap())       # 42
print(o.unwrap_or(0))   # 42

empty: int? = None()
print(empty.is_some)        # False
print(empty.is_none)        # True
print(empty.unwrap_or(99))  # 99
```

## `str` and `repr`

`str()` of an Optional is **transparent** — the value's own `str`, or `None` for an empty one — so
`print`, a plain f-string hole and `str.format` show the value. `repr()` is the **constructor
spelling** — `Some(<repr of the value>)` or `None()` — so `!r`, `repr()` and every container element
say which case they hold (#2005):

```python
def main() -> None:
    a: int? = Some(1)
    b: str? = Some("ab")
    c: int? = None()
    print(a, b, c)
    print(repr(a), repr(b), repr(c))
    print([a, c], {"k": b})
```

```
1 ab None
Some(1) Some('ab') None()
[Some(1), None()] {'k': Some('ab')}
```

## Constructor Shorthand

When the expected type is known, `Some(value)` and `None()` infer the full
`Optional<T>` type from context — no qualification needed:

```python
# With type annotation
x: int? = Some(42)
y: int? = None()

# Function return
def get_value() -> int?:
    return Some(42)

# Default parameter
def foo(x: int? = None()) -> None:
    pass

# Without type context - error (type cannot be inferred)
x = Some(42)   # Error: Cannot infer type for 'Some()'
```

The shorthand is equivalent to calling `Optional<T>.Some(value)` or
`Optional<T>.None()`.

## Protocol Operations and Member Access

`T?` (`Optional[T]`) is **strict**: its safety guarantee is the whole point of the
type, so the underlying value is never reached implicitly. Protocol operations
(`len()`, the `in` membership test, indexing `x[i]`, and iteration
`for v in x`) and direct member/method access on the underlying type are
**compile errors** on a `T?` receiver:

<!-- spec-sweep: error SPY0326 -->
```python
def main() -> None:
    s: str? = Some("hello")
    print(len(s))    # SPY0326: Optional type 'str?' does not support len() directly
    print(s[0])      # SPY0326: ... does not support indexing directly
    print(s.upper()) # SPY0326: ... does not support member access ('upper') directly
```

Reach the underlying value explicitly — narrow it, use `?.`, pattern-match, or
unwrap it:

```python
def main() -> None:
    s: str? = Some("hello")

    # Narrow with `is not None` (refines T? to T in the branch)
    if s is not None:
        print(len(s))
        print(s.upper())

    # Null-conditional access (?.)
    upper: str? = s?.upper()

    # Pattern matching through the constructor cases
    match s:
        case Some(v):
            print(len(v))
        case None():
            print("empty")

    # Unwrap (throws on None)
    print(len(s.unwrap()))
    print(s.unwrap_or("default"))
```

The only members callable directly on a `T?` are `Optional`'s own API
(`unwrap`, `unwrap_or`, `unwrap_or_else`, `map`, `is_some`, `is_none`).

### `==` and `!=` are native on `T?`

Two `T?` values compare with `==`/`!=` (`Optional` defines both); ordering (`<`, `>`, …) is
refused. Membership in a `list[int?]` or `set[int?]` and a `dict[int?, V]` key — written or
read — use the same equality, so `d[None()]` reads the entry stored under `None()`:

```python
def main():
    a: int? = Some(1)
    b: int? = Some(1)
    c: int? = None()
    print(a == b, a != c, c == c)   # True True True
    d: dict[int?, str] = {Some(1): "a", None(): "b"}
    print(d[None()])                # b
```

Against a user type, `x == None()` selects the `__eq__` overload whose parameter is an Optional
(see [Dunder Methods › Operand typing and dispatch](dunder_methods.md#operand-typing-and-dispatch)).

### Passing a `T?` is the same rule

Handing a `T?` to something that expects a `T` is a use of the underlying value,
so it is refused for the same reason `len(s)` is — at argument binding, at an
operator's operand, and at any other position typed `T`. The reverse direction
takes the constructor: a plain value goes into a `T?` parameter as `Some(value)` (a
bare `T` is SPY0604, as for any store — see [Creating Optional Values](#creating-optional-values)).

<!-- spec-sweep: error SPY0220 -->
```python
def total(xs: list[int]) -> int:
    return len(xs)


def describe(v: int?) -> str:
    return "ok"


def main() -> None:
    ys: list[int]? = Some([1, 2])

    total(ys)          # error: Cannot pass argument of type 'list[int]?' to parameter
                       # of type 'list[int]' — the argument is Optional[list[int]];
                       # narrow it ('if x is not None:') or unwrap it first
    [1, 2] + ys        # error: Type 'list[int]' does not support operator '+' with
                       # operand of type 'list[int]?'

    if ys is not None: # narrowed to list[int] in the branch
        total(ys)      # OK
    total(ys.unwrap_or([]))  # OK
    describe(Some(7))  # OK — the constructor makes the T?
```

A `T?` is not a `T | None` either, even though both spell as `T?` at a use site:
they are different types, and neither converts to the other implicitly. Use
`maybe` to go from `T | None` to `T?`, and narrow or unwrap to go the other way.

### `T | None` is loose

By contrast, `T | None` (the C# nullable interop type — see
[Nullable Types](nullable_types.md)) is **loose**: protocol operations and
member access work directly on the underlying type, and a `None`/`null`
receiver fails at runtime (a `NullReferenceException`, mirroring Python's
`TypeError` / `AttributeError` on `None`). This matches .NET interop semantics,
where nullable references flow into ordinary member access:

```python
def main() -> None:
    s: str | None = "hello"
    print(len(s))      # works; throws at runtime if s is None
    print(s.upper())   # works; throws at runtime if s is None
    print(s[0])        # works — indexing is a protocol route like any other
    print(s[0:1])      # works — so is slicing
    for ch in s:       # works — and iteration
        print(ch)
    print([ch for ch in s])   # works — including the comprehension spelling
```

"Every protocol route" means every one, for every payload — including payloads that emit as .NET
value types, where the loose wrapper is a `Nullable<T>`:

```python
def main() -> None:
    b: bytes | None = b"ab"
    print(b[0])        # works
    print(b[0:1])      # works
    print(b.decode())  # works

    t: tuple[int, int] | None = (1, 2)
    print(t[0])        # works
    print(2 in t)      # works
    for x in t:        # works
        print(x)
```

By contrast the strict `T?` refuses all of them with SPY0326 and tells you to narrow or unwrap:

```python
def main() -> None:
    raw: str | None = "hello"
    o: str? = maybe raw
    # print(o[0])      # SPY0326 — narrow it first (if o is not None:) or unwrap it (o.unwrap())
    print(o.unwrap()[0])
```

Choose `T?` when you want the compiler to force you to handle absence; choose
`T | None` at .NET interop boundaries where Python-parity runtime semantics are
acceptable.

## Comparison: `T?` (Optional) vs `T | None` (C# Nullable)

| Feature | `T?` / `Optional[T]` | `T \| None` (C# Nullable) |
|---------|----------------------|---------------------------|
| Meaning | Safe tagged union | C# nullable reference/value |
| Has value | `Some(value)` | `value` |
| No value | `None()` | `None` |
| `str(x)` / `repr(x)` | the value's `str` / `Some(<repr>)`; `None` / `None()` | the value's `str` / `repr`; `None` / `None` |
| Type safety | Works with any `T` | Only reference types and `Nullable<T>` |
| Pattern matching | `case Some(v):` | `if x is not None:` |
| Protocol ops (`len`, `in`, `[i]`, iteration) | Compile error — narrow or `unwrap()` first | Allowed — throws at runtime on `None` |
| Member access (`x.method()`) | Compile error — only the Optional API (`unwrap`, `map`, …) | Allowed — throws at runtime on `None` |
| Heap allocation | **No** (struct) | No |
| Use case | Sharpy-native optionals | .NET interop boundaries |
| Interop | May need conversion | Direct .NET interop |

### When to Use `T?` (Optional)

- You're writing Sharpy-native code
- You want explicit, type-safe optional semantics
- You're working with value types that need to be optional
- You prefer functional programming patterns (map, flatMap, etc.)
- You want to make optionality more explicit in the type system

### When to Use `T | None` (C# Nullable)

- You're interfacing with .NET APIs that use null
- You're at a .NET interop boundary
- You want direct C# interop without conversions

See [Nullable Types](nullable_types.md) for details on `T | None`.

## Examples

### Safe Dictionary Access

```python
def get_config_value(config: dict[str, str], key: str) -> str?:
    if key in config:
        return Some(config[key])
    return None()


def main() -> None:
    config: dict[str, str] = {"timeout": "5"}
    value = get_config_value(config, "timeout")
    timeout: int = 30  # default
    match value:
        case Some(v):
            timeout = int(v)
        case None():
            pass
    print(timeout)
```

### Chaining Optional Operations

```python
class Address:
    city: str

    def __init__(self, city: str):
        self.city = city


class User:
    address: Address?

    def __init__(self, address: Address?):
        self.address = address

    def get_address(self) -> Address?:
        return self.address


def find_user(user_id: int) -> User?:
    if user_id == 1:
        return Some(User(Some(Address("Paris"))))
    return None()


def get_user_city(user_id: int) -> str?:
    user = find_user(user_id)
    if user.is_none:
        return None()

    address = user.unwrap().get_address()
    if address.is_none:
        return None()

    return Some(address.unwrap().city)


def main() -> None:
    print(get_user_city(1), get_user_city(2))
```

### Transforming Optional Values

```python
# Using map to transform the value if present
opt_number: int? = Some(42)
opt_string = opt_number.map(lambda x: f"The answer is {x}")
# Result: Some("The answer is 42")

opt_nothing: int? = None()
opt_result = opt_nothing.map(lambda x: x * 2)
# Result: None
```

## Converting Between Optional and C# Nullable

Use `maybe` to convert from `T | None` (C# nullable) to `T?` (Optional):

```python
def optional_to_nullable(opt: str?) -> str | None:
    match opt:
        case Some(value):
            return value
        case None():
            return None


def main() -> None:
    # C# nullable to Optional (use maybe)
    raw: str | None = "hello"
    safe: str? = maybe raw              # Convert to Optional[str]
    # Optional to C# nullable
    print(optional_to_nullable(safe), optional_to_nullable(None()))
```

See [Maybe Expressions](maybe_expressions.md) for details on the `maybe` keyword.

*Implementation*
- *✅ Implemented — `Optional[T]` is a struct-based tagged union in Sharpy.Core. Pattern matching with `Some`/`None`, the [`?` early-return operator](question_mark_operator.md), and `maybe` expressions are all supported.*

## Implementation Details

`Optional[T]` is implemented as a C# `readonly struct` in `Sharpy.Core`:

```csharp
public readonly struct Optional<T>
{
    // Two fields: the value and a hasValue flag
    // Zero heap allocation
}
```

The static helpers `Some(value)` and `None()` are available at module scope
for convenient construction.

## Must-Use Warning (SPY0480)

An `Optional[T]` (`T?`) produced as a bare expression statement and thrown away triggers the **must-use** warning `SPY0480` — discarding an `Optional` usually means an absent value is being silently ignored:

```python
def find(key: str) -> int?:
    ...

def main() -> None:
    find("x")          # ⚠ SPY0480: result of type 'int?' is silently discarded
```

Bind the value, propagate it with `?`, or discard it explicitly with `_ = find("x")`. Note this applies to the strict `Optional[T]` (`T?`) carrier — the loose `T | None` [nullable type](nullable_types.md) used for .NET interop is *not* flagged. The warning is scoped-suppressible with [`@suppress("SPY0480")`](diagnostic_suppression.md).

## See Also

- [Tagged Unions](tagged_unions.md) - General tagged union syntax and implementation
- [Result Type](tagged_unions_result.md) - The Result type for error handling
- [Question-Mark Operator](question_mark_operator.md) - The `?` early-return operator for propagating `None`
- [Maybe Expressions](maybe_expressions.md) - Converting `T | None` to `T?`
- [Nullable Types](nullable_types.md) - `T | None` syntax for .NET interop
- [Null Coalescing Operator](null_coalescing_operator.md) - The `??` operator
- [Pattern Matching](match_statement.md) - Pattern matching syntax
