# Collection Types

| Sharpy Type | Shorthand | .NET Type | Notes |
|-------------|-----------|-----------|-------|
| `list[T]` | `[T]` | `Sharpy.List<T>` | Mutable list |
| `dict[K, V]` | `{K: V}` | `Sharpy.Dict<K, V>` | Hash map |
| `set[T]` | `{T}` | `Sharpy.Set<T>` | Unique elements; see [set operators](#set-and-frozenset-operators) |
| `frozenset[T]` | — | `Sharpy.FrozenSet<T>` | Immutable, hashable set; no shorthand |
| `tuple[T1, T2, ...]` | `(T1, T2, ...)` | `System.ValueTuple<T1, T2, ...>` | Fixed-size tuple; supports [positional access](#tuple-positional-access) |

With the exception of `tuple[...]`, Sharpy collection types use custom Pythonic wrappers around the corresponding .NET collection types.

## Shorthand Syntax

All collection types support [shorthand syntax](type_annotation_shorthand.md) for more concise type annotations:

```python
# These pairs are equivalent:
items: [int]           # items: list[int]
scores: {str: int}     # scores: dict[str, int]
unique: {int}          # unique: set[int]
point: (int, int)      # point: tuple[int, int]
```

## Optional and Error Handling Conventions

Collection access follows these conventions for optionality and error handling:

| Operation | Return Type | Behavior |
|-----------|------------|----------|
| `dict.get(key: K)` | `V?` | Returns `Some(value)` or `None()` |
| `dict[key]` | `V` | Throws `KeyError` if missing |
| `list[i]` | `T` | Throws `IndexError` if out of bounds |
| `list.get(index: int)` | `T?` | Returns `Some(value)` or `None()` |

```python
d: dict[str, int] = {"x": 1, "y": 2}

# Safe access - returns Optional
val: int? = d.get("x")      # Some(1)
val: int? = d.get("z")      # None()

# Direct access - throws on missing key
val: int = d["x"]           # 1
val: int = d["z"]           # KeyError

# List safe access
items: list[str] = ["a", "b", "c"]
item: str? = items.get(0)   # Some("a")
item: str? = items.get(99)  # None()

# List direct access - throws on out of bounds
item: str = items[0]        # "a"
item: str = items[99]       # IndexError
```

## Set and Frozenset Operators

`set[T]` and `frozenset[T]` support the same four set operations and the same four subset/superset
comparisons, and every one of them accepts the *other* type as its right operand. Every value in
this section was executed against the compiler.

### The left operand decides the result type

A mixed operation follows CPython's **left-operand rule**: the type of the left operand is the type
of the result. `set | frozenset` is a `set`; `frozenset | set` is a `frozenset`. The rule is about
the operand types alone — it does not depend on which side is larger, or on the annotation of the
variable being assigned.

In the table below, `∘` stands for whichever operator the row names:

| Operator | Meaning | `set ∘ set` | `set ∘ frozenset` | `frozenset ∘ set` | `frozenset ∘ frozenset` |
|----------|---------|-------------|-------------------|-------------------|-------------------------|
| `\|` | union | `set[T]` | `set[T]` | `frozenset[T]` | `frozenset[T]` |
| `&` | intersection | `set[T]` | `set[T]` | `frozenset[T]` | `frozenset[T]` |
| `-` | difference | `set[T]` | `set[T]` | `frozenset[T]` | `frozenset[T]` |
| `^` | symmetric difference | `set[T]` | `set[T]` | `frozenset[T]` | `frozenset[T]` |
| `<` `<=` `>` `>=` | subset/superset | `bool` | `bool` | `bool` | `bool` |
| `==` `!=` | equality | `bool` | **refused (SPY0222)** | **refused (SPY0222)** | `bool` |

The comparisons answer a question about the two operands rather than building a collection, so they
return `bool` in every cell and the left-operand rule has nothing to decide.

### Neither operand is mutated

A set operation builds a new collection; it never writes through either operand, whichever side the
mutable one is on. With a `set` on the left, the result is a `set`:

```python
def main() -> None:
    s: set[int] = {1, 2}
    f: frozenset[int] = frozenset([2, 3])

    print(s | f)      # {1, 2, 3}
    print(s & f)      # {2}
    print(s - f)      # {1}
    print(s ^ f)      # {1, 3}
    print(s <= f)     # False

    print(s)          # {1, 2}             — unchanged
    print(f)          # frozenset({2, 3})  — unchanged
```

The same operands the other way round produce the same elements in a `frozenset`:

```python
def main() -> None:
    s: set[int] = {1, 2}
    f: frozenset[int] = frozenset([2, 3])

    print(f | s)      # frozenset({1, 2, 3})
    print(f & s)      # frozenset({2})
    print(f - s)      # frozenset({3})
    print(f ^ s)      # frozenset({1, 3})
    print(f >= s)     # False
```

`s - f` is `{1}` and `f - s` is `frozenset({3})` because difference is not symmetric; the elements
differ for the usual reason, and only the *type* of each result comes from the left-operand rule.

### Augmented assignment rebinds

`|=`, `&=`, `-=` and `^=` are defined by the binary operator plus a rebinding of the left-hand name.
Nothing is mutated in place, so the left operand may be a `frozenset` — the name is simply bound to
the new frozenset the operator returned:

```python
def main() -> None:
    s: set[int] = {1, 2}
    f: frozenset[int] = frozenset([2, 3])

    s |= f
    print(s)          # {1, 2, 3}

    g: frozenset[int] = frozenset([1, 2])
    g |= {2, 3}
    print(g)          # frozenset({1, 2, 3})
```

Because the result type follows the left operand, an augmented assignment always rebinds the name to
its own type: `s` stays a `set[int]` and `g` stays a `frozenset[int]`.

### Mixed `==` and `!=` are refused

CPython compares a set and a frozenset by their elements, so `{1} == frozenset([1])` is `True`.
Sharpy refuses the comparison in both directions:

```python
def main() -> None:
    s: set[int] = {1}
    f: frozenset[int] = frozenset([1])

    # ERROR SPY0222: Type 'set[int]' does not support operator '=='
    #                with operand of type 'frozenset[int]'
    print(s == f)
    # ERROR SPY0222: Type 'frozenset[int]' does not support operator '=='
    #                with operand of type 'set[int]'
    print(f == s)
    # ERROR SPY0222: Type 'set[int]' does not support operator '!='
    #                with operand of type 'frozenset[int]'
    print(s != f)
```

This is the one gap in the mixed matrix, and it is a deliberate trade rather than an oversight:
equality operators take nullable operands, so a mixed `==` overload would make the ordinary
`someSet == None` ambiguous between the two candidates. Converting one side states which comparison
is meant, and both spellings hold:

```python
def main() -> None:
    s: set[int] = {1}
    f: frozenset[int] = frozenset([1])

    print(s == set(f))          # True
    print(frozenset(s) == f)    # True
```

Catalogued as a deviation (`docs/deviations.yaml`, `mixed-set-frozenset-equality`), which carries
the full rationale.

### Dict and frozendict

`dict[K, V]` and `frozendict[K, V]` share exactly one operator, `|`, and it obeys the same
left-operand rule. Keys on the **right** win in every cell, which is CPython's PEP 584 rule for
`dict | dict`:

| Operator | Meaning | `dict ∘ dict` | `dict ∘ frozendict` | `frozendict ∘ dict` | `frozendict ∘ frozendict` |
|----------|---------|---------------|---------------------|---------------------|---------------------------|
| `\|` | merge | `dict[K, V]` | `dict[K, V]` | `frozendict[K, V]` | `frozendict[K, V]` |

Neither operand is mutated, exactly as for sets — `\|` builds a new mapping even when the mutable
`dict` is on the left:

```python
def main() -> None:
    d: dict[str, int] = {"a": 1, "b": 2}
    fd: frozendict[str, int] = frozendict({"b": 20, "c": 30})

    mixed: dict[str, int] = d | fd
    print(mixed)                    # {'a': 1, 'b': 20, 'c': 30}

    frozen_first: frozendict[str, int] = fd | d
    print(len(frozen_first))        # 3
    print(frozen_first["b"])        # 2 — the right operand wins, so d's value survives

    print(d)                        # {'a': 1, 'b': 2}  — unchanged
```

A `frozendict` carries `dict`'s insertion order, so these results can equally be printed whole; the
examples above read by key only because that is what they are illustrating. A repeated key keeps its
first position and takes the last value, and under `|` a key present on both sides keeps its
left-hand position and takes the right-hand value — the same rules `dict` follows
([#1392](https://github.com/antonsynd/sharpy/issues/1392)).

Order is an iteration and rendering property only: `==` and hashing ignore it, so two `frozendict`s
built from the same pairs in different orders are equal, hash alike, and collapse to one element in
a `set` — while their `repr`s differ. That is the same split `dict` has, and it is what keeps a
`frozendict` usable as a `dict` key.

Both mixed directions were unreachable until [#1361](https://github.com/antonsynd/sharpy/issues/1361):
`dict` was the one builtin collection still registered against `System.Collections.Generic.Dictionary<,>`
rather than its `Sharpy.Dict<K, V>` wrapper, and the registered CLR type is what operator resolution
reflects over. `Dictionary` declares no operators, so `dict | dict` resolved only through the
shortcut that applies to a lone candidate, and the mixed overload took that shortcut away.

*Implementation*
- *✅ Native - each mixed pairing is a C# operator overload declared on the LEFT operand's type
  (`Sharpy.Set<T>` for `set ∘ frozenset`, `Sharpy.FrozenSet<T>` for `frozenset ∘ set`,
  `Sharpy.Dict<K, V>` for `dict | frozendict`, `Sharpy.FrozenDict<K, V>` for `frozendict | dict`),
  which is what makes the left operand decide the result type.*

## Tuple Positional Access

Tuple elements can be accessed by position using integer literal subscript syntax:

```python
point: tuple[int, int, int] = (10, 20, 30)
x = point[0]   # 10
y = point[1]   # 20
z = point[2]   # 30
```

Named tuples also support positional access alongside named access:

```python
type Point = tuple[x: float, y: float]
p: Point = (x=3.0, y=4.0)
print(p[0])    # 3.0 (same as p.x)
print(p[1])    # 4.0 (same as p.y)
```

### Restrictions

| Rule | Behavior |
|------|----------|
| Indices must be integer literals | Variable indices are not supported (e.g., `t[i]` where `i` is a variable) |
| No negative indices | `t[-1]` produces a compile-time error (Python divergence) |
| Compile-time bounds checking | `t[3]` on a 3-element tuple produces a compile-time error |

### Python Divergence

Unlike Python, Sharpy does not support negative tuple indices. This is because tuple positional access is resolved at compile time (lowered to `.Item1`, `.Item2`, etc.), and negative indexing would require runtime tuple length information.

*Implementation*
- *🔄 Lowered - `tuple[i]` is lowered to `.Item{i+1}` (e.g., `tuple[0]` → `.Item1`, `tuple[1]` → `.Item2`).*

## Tuple Slicing

Tuples support slicing with **constant non-negative integer bounds** only. The result type is a
narrower tuple containing the selected element types:

```python
t: tuple[int, str, float, bool] = (1, "hello", 3.14, True)
a: tuple[str, float, bool] = t[1:]     # ('hello', 3.14, True)
b: tuple[int, str] = t[:2]             # (1, 'hello')
c: tuple[str, float] = t[1:3]          # ('hello', 3.14)
```

Constant references (`const N: int = 1; t[N:]`) are resolved at compile time via constant folding.

### Restrictions

| Restriction | Reason |
|-------------|--------|
| Bounds must be constant non-negative integers | Tuple element types vary by position; runtime slicing cannot determine the result type |
| No negative indices | Same as tuple positional access — negative normalization needs runtime arity |
| No step (except `1`) | Reverse/strided tuple slicing is not supported in v1 |

Non-constant or negative bounds produce a compile-time error with a diagnostic steering to the
constant-bound requirement.
([#1609](https://github.com/antonsynd/sharpy/issues/1609))

*Implementation*
- *🔄 Lowered — `t[1:3]` on `tuple[int, str, float]` lowers to `ValueTuple.Create(t.Item2, t.Item3)`.*

## Slicing

Slicing extracts a contiguous (or strided) subsequence from a collection using `start:stop` or
`start:stop:step` syntax inside `[]`.

### Sliceable Types

| Type | Result Type | Notes |
|------|-------------|-------|
| `list[T]` | `list[T]` | New list; original unchanged |
| `str` | `str` | UTF-16 code-unit slice |
| `bytes` | `bytes` | Byte-level slice |
| `array[T]` | `list[T]` | Materializes a list from the slice |
| `ndarray` | per-axis | Multi-axis via `SliceSpec`; one spec per dimension |

Types that are NOT sliceable produce a compile-time error:

- `dict[K, V]` — CPython raises `KeyError` at runtime (slice object is a missing key); Sharpy
  refuses at compile time.
- `set[T]` / `frozenset[T]` — unordered; slicing is meaningless.
- `tuple[T1, T2, ...]` — constant-bound slicing only (see [Tuple Slicing](#tuple-slicing) below).

A subscript that mixes a comma with a slice (`a[i:j, k]`, `a[:, k]`, `a[i, ::2]`) is a
**multi-axis subscript**, supported only for `ndarray`; every other receiver refuses with
SPY0602. A comma with no slice (`a[i, j]`) is not multi-axis: it is an ordinary subscript whose
index is the tuple `(i, j)`, so it is refused by the receiver's index-type rule (SPY0220) unless
the receiver accepts a tuple index (an `ndarray` does; `a[i, j]` reads one element).

```python
xs: list[int] = [1, 2, 3]
print(xs[0, 1])    # SPY0220: Index must be 'int', got 'tuple[int32, int32]'
print(xs[0:2, 1])  # SPY0602: Type 'list[int32]' does not support multi-axis subscripting
```

### Syntax

```python
xs[start:stop]       # elements from start up to (not including) stop
xs[start:stop:step]  # every step-th element
xs[:stop]            # from beginning
xs[start:]           # to end
xs[::step]           # every step-th from beginning to end
xs[::-1]             # reverse
```

All three bounds — `start`, `stop`, `step` — are optional. An omitted bound is `None`
(absence-of-bound), equivalent to writing `None` explicitly.

### Bound Types

Each slice bound must be assignable to `int?` (the loose nullable — `int` or `None`):

| Accepted | Example |
|----------|---------|
| `int` literal | `xs[1:4]` |
| `int` variable | `n: int = 2; xs[n:]` |
| `None` (absent bound) | `xs[:3]`, `xs[None:3]` |
| `int \| None` variable | `n: int \| None = 2; xs[n:]` |

| Refused | Diagnostic |
|---------|------------|
| `bool` | `Slice bound must be 'int' or 'None', got 'bool'` |
| `float` | `Slice bound must be 'int' or 'None', got 'float64'` |
| `str` | `Slice bound must be 'int' or 'None', got 'str'` |
| `bytes` | `Slice bound must be 'int' or 'None', got 'bytes'` |
| `int?` (`Optional[int]` ADT) | `Slice bound must be 'int' or 'None', got 'int32?'` |

The `Optional[int]` ADT is distinct from the loose nullable `int | None` — it does not
implicitly cross into a slice bound. Narrow or unwrap the `Optional` first.

```python
def main():
    xs: list[int] = [10, 20, 30, 40, 50]
    print(xs[1:4])       # [20, 30, 40]
    print(xs[:3])        # [10, 20, 30]
    print(xs[2:])        # [30, 40, 50]
    print(xs[None:3])    # [10, 20, 30]  — None is the absent bound
    print(xs[-2:])       # [40, 50]      — negative indexing
    print(xs[::2])       # [10, 30, 50]  — step
    print(xs[::-1])      # [50, 40, 30, 20, 10] — reverse
    print(xs[4:1:-1])    # [50, 40, 30]  — reverse with bounds

    n: int | None = 2
    print(xs[n:])        # [30, 40, 50]
    print(xs[:n])        # [10, 20]
    n = None
    print(xs[n:])        # [10, 20, 30, 40, 50]
```

String and bytes slicing follows the same rules:

```python
def main():
    s: str = "hello"
    print(s[1:4])     # ell
    print(s[:3])      # hel
    print(s[::2])     # hlo
    print(s[::-1])    # olleh

    b: bytes = b"abcdef"
    print(b[1:4])     # b'bcd'
    print(b[::-1])    # b'fedcba'
```

### Index Type Rule

Plain subscript access `x[i]` on int-indexed sequences (list, str, bytes, array) requires an
`int` index. Types that Python allows via implicit conversion are refused:

| Refused | Diagnostic | Python behavior |
|---------|------------|-----------------|
| `xs[True]` | `Index must be 'int', got 'bool'` | Treats as `xs[1]` (bool subclasses int) |
| `xs["a"]` | `Index must be 'int', got 'str'` | TypeError at runtime |

The bool refusal is a deliberate Type Safety deviation — Python permits `xs[True]` because
`bool` is a subclass of `int`, but Sharpy keeps `bool` and `int` distinct. The explicit spelling
is `xs[int(flag)]`.

### Dict Key Type Rule

Dict subscript access `d[k]`, store `d[k] = v` and augmented store `d[k] += v` validate the key
against the dict's key type parameter using assignability (not strict equality). This means
`dict[long, V]` accepts an `int` key (widening), and `dict[object, V]` accepts any key. A
`T | None` receiver is keyed like the dict it holds once narrowed; an `Optional[K]` key is not a
`K` key and must be unwrapped first.

| Expression | Key type | Outcome |
|------------|----------|---------|
| `d: dict[str, int]; d[1]` | `int` vs `str` | SPY0220 — `Dict key must be 'str', got 'int32'` |
| `d: dict[str, int]; d[True]` | `bool` vs `str` | SPY0220 + bool steer (`d[int(flag)]`) |
| `d: dict[int, str]; k: int? = Some(1); d[k]` | `int?` vs `int` | SPY0220 + `Unwrap the Optional key first` |
| `d: dict[long, str]; d[1]` | `int` vs `long` | Accepted (int assignable to long) |
| `o: dict[object, int]; o["k"]; o[2]` | any vs `object` | Accepted |

```python
def main():
    d: dict[long, str] = {}    # a `{1: "a"}` literal is a dict[int, str] — start empty
    d[1] = "a"                 # int key widens to long
    d[1] += "!"
    print(d[1])                # a!

    o: dict[object, int] = {}
    o["k"] = 1
    o[2] = 2
    print(o["k"] + o[2])       # 3
```

**User protocols follow the same rule, per position.** A read `x[k]` validates `k` against the
class's `__getitem__` overloads; a store `x[k] = v` validates `k` against its `__setitem__`
overloads and then checks `v` against the selected overload's value parameter; an augmented
store `x[k] op= v` is both. A key matching no overload is refused with SPY0220 naming the dunder
it was checked against and listing the overloads; a class that lacks the dunder the position
needs is refused once, by the protocol check (SPY0320), so a `__setitem__`-only class stores
freely and is refused only when read.

```python
class Box:
    def __setitem__(self, k: int, v: str) -> None:
        print(v)

def main():
    b: Box = Box()
    b[1] = "x"        # x — __setitem__ accepts (int, str)
    # b["a"] = "x"    # SPY0220: __setitem__ of 'Box' does not accept a key of type 'str' (overloads: (k: int32, v: str))
    # b[1] = 5        # SPY0220: Cannot assign type 'int32' to 'str'
    # print(b[1])     # SPY0320: Type 'Box' does not support indexing (missing '__getitem__' method).
```

### User Protocol

A user-defined class can support subscript access via `__getitem__`. Slice support requires
a `__getitem__` overload that takes a `slice` parameter:

```python
class MySeq:
    def __getitem__(self, s: slice) -> str:
        return f"{s.start}-{s.stop}-{s.step}"

seq: MySeq = MySeq()
print(seq[1:5:2])  # "1-5-2"
```

The `slice` type is a builtin with nullable `start`, `stop`, and `step` properties (each `int?`).
A `slice()` constructor is available with the same signatures as Python: `slice(stop)` and
`slice(start, stop[, step])`.
([#1610](https://github.com/antonsynd/sharpy/issues/1610))

### `__index__` Protocol

Sharpy does not adopt Python's `__index__` protocol. In CPython, `__index__` lets a value present
itself as an integer losslessly (distinct from `__int__`, which may truncate). Positions that
accept it: sequence indexing, slice bounds, `range()` arguments, and numeral formatters.

The two practical beneficiaries in real Python code — numpy integer scalars and `bool` — do not
create demand in Sharpy: numpy scalars already type as `int`, and `bool` in int positions is a
deliberate Type Safety deviation. Adopting the protocol without a concrete beneficiary would be
the "add X because Python has it" anti-pattern. Full rationale in
[`docs/design/index-protocol-proposal.md`](../design/index-protocol-proposal.md)
([#1611](https://github.com/antonsynd/sharpy/issues/1611)).

*Implementation*
- *🔄 Lowered — `x[start:stop:step]` lowers to `Sharpy.Slice.GetSlice(x, start, end, step)` for
  list, str, bytes, and array. ndarray slicing lowers to `x.Slice(new SliceSpec(...))`.*

---

*Implementation*
- *🔄 Lowered - Sharpy collections are aliases to the corresponding .NET collections.*
