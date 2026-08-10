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

The `frozendict` results are read by key rather than printed whole because a `frozendict` does not
carry `dict`'s insertion order — see [#1392](https://github.com/antonsynd/sharpy/issues/1392).

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

---

*Implementation*
- *🔄 Lowered - Sharpy collections are aliases to the corresponding .NET collections.*
