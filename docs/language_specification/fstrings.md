# F-Strings (Formatted String Literals)

An f-string interpolates expressions into a string. Each replacement field is
`{expr[=][!conversion][:format_spec]}`: the expression is evaluated, an optional `=` self-documents
it, an optional `!r`/`!s`/`!a` conversion is applied, and an optional format spec after `:` controls
padding, sign, precision and presentation. Sharpy follows Python 3.12 (PEP 701) semantics.

```python
def main() -> None:
    name: str = "Alice"
    age: int = 30
    print(f"My name is {name} and I'm {age} years old")

    # Expressions in f-strings
    print(f"Result: {age * 2}")

    # Format specifiers
    pi: float = 3.14159
    print(f"Pi: {pi:.2f}")
```

```
My name is Alice and I'm 30 years old
Result: 60
Pi: 3.14
```

## One Format Engine

Every hole is rendered by a single Python-format engine, `Sharpy.PyFormat.Apply`, which `str.format`,
the `format()` builtin and every f-string / t-string hole share. There is no separate emitter-side
translator, so the format spec behaves exactly as it does in Python (the same engine, the same
rounding, the same padding).

| Hole shape | Renders as | Example → output |
|------------|-----------|------------------|
| `{v}` (plain) | `str(v)` | `f"{None}"` → `None` |
| `{v!r}` | `repr(v)` | `f"{'ab'!r}"` → `'ab'` |
| `{v!s}` | `str(v)` | `f"{None!s}"` → `None` |
| `{v!a}` | `ascii(v)` | `f"{'café'!a}"` → `'caf\xe9'` |
| `{v=}` | `"v=" + repr(v)` | `f"{age=}"` → `age=30` |
| `{v:spec}` | `PyFormat.Apply(v, spec)` | `f"{3.14159:.2f}"` → `3.14` |
| `{v=:spec}` | `"v=" + PyFormat.Apply(v, spec)` | `f"{pi=:.2f}"` → `pi=3.14` |

### None Renders `None`

A plain hole calls `str(value)`, so a `None` value renders `None` — not the empty string. This holds
for a bare `None`, a `T | None` variable, an `object` holding null, and an `int?` that is `None()`:

```python
def main() -> None:
    n: int | None = None
    print(f"value: {n}")
    print(f"[{None}]")
```

```
value: None
[None]
```

## Format Spec Grammar

The format spec is `[[fill]align][sign][z][#][0][width][grouping][.precision][type]`. Each component
has the same meaning as in Python, and is applied by the one engine:

| Component | Python meaning | Sharpy | Example → output |
|-----------|----------------|--------|------------------|
| `align` `<^>=` | left / center / right / pad-after-sign | same engine | `f"{5:<4}"` → `5   ` |
| `fill` + align | pad with a fill char | same engine | `f"{5:*^7}"` → `***5***` |
| `sign` `+`/`-`/space | force / default / leading-space sign | same engine | `f"{5:+}"` → `+5` |
| `z` | coerce negative zero (PEP 682) | same engine | `f"{-0.0:z.1f}"` → `0.0` |
| `#` | alternate form | same engine | `f"{5:#x}"` → `0x5` |
| `0` + width | zero-pad | same engine | `f"{5:05}"` → `00005` |
| `width` | minimum field width | same engine | `f"{5:5}"` → `    5` |
| grouping `,` / `_` | thousands separator | same engine | `f"{1234567:,}"` → `1,234,567` |
| `.precision` | digits / significant figures | same engine | `f"{3.14159:.2f}"` → `3.14` |
| type `d f e g x X o b c n % s` | presentation | same engine | `f"{255:X}"` → `FF` |

A bare width with no alignment (`{5:5}`) pads to the field width — a case the old emitter-side
translator dropped:

```python
def main() -> None:
    print(f"[{5:5}]")
    print(f"[{5:<5}]")
    print(f"[{1234567:,}]")
    print(f"[{255:X}]")
    print(f"[{3.14159:.2f}]")
```

```
[    5]
[5    ]
[1,234,567]
[FF]
[3.14]
```

Because the engine is value-kind aware, the same spec formats differently by type (this is Python's
behavior, not a Sharpy quirk): `{x:4}` right-aligns an int, `{s:4}` left-aligns a str, and a `bool`
under a non-empty spec formats as its integer value:

```python
def main() -> None:
    x: int = 5
    s: str = "ab"
    b: bool = True
    print(f"[{x:4}]")
    print(f"[{s:4}]")
    print(f"[{b:6}]")
```

```
[   5]
[ab  ]
[     1]
```

### The Alternate Form and `=` Padding Are One Rule Each

`#` means the same thing on every float presentation type (`e E f F g G n %` and no type): the
result always has a decimal point, and `g`/`G`/`n`/no-type keep their trailing zeros. On an integer
presentation (`b o x X`) it adds the radix prefix. `=` alignment pads after the sign **and** the
radix prefix, whatever the fill character — `0`, an explicit fill, or the default space:

```python
def main() -> None:
    print(f"[{42:#.0f}]")
    print(f"[{3.5:#g}]")
    print(f"[{42:#.0%}]")
    print(f"[{-255:*=#10x}]")
    print(f"[{255:=#10x}]")
    print(f"[{255:0=#10x}]")
```

```
[42.]
[3.50000]
[4200.%]
[-0x*****ff]
[0x      ff]
[0x000000ff]
```

### The Sign Applies to Every Numeric Presentation Type

A sign (`+`, `-`, or space) is applied to **every** numeric presentation type, including `%` and the
radix types — the sign is one rule, not a per-type special case:

```python
def main() -> None:
    print(f"[{1.5:+%}]")
    print(f"[{2:+%}]")
    print(f"[{42:+x}]")
    print(f"[{42:+o}]")
```

```
[+150.000000%]
[+200.000000%]
[+2a]
[+52]
```

The one exception is `c` (character): a sign with `c` is refused — statically as **SPY0609** on a
literal spec, and at runtime with the same wording through a dynamic spec:

<!-- spec-sweep: error SPY0609 -->
```python
def main() -> None:
    print(f"[{65:+c}]")   # SPY0609: Sign not allowed with integer format specifier 'c'
```

### String Operands

A string operand takes only fill/align, width and precision. The `0` flag zero-fills a string but
leaves it at the string default alignment `<`, so it fills on the **right** (Python's rule since 3.10 —
this is *not* `=`-style synthesis, which would fill on the left):

```python
def main() -> None:
    s: str = "ab"
    print(f"[{s:05}]")
    print(f"[{s:>05}]")
    print(f"[{s:<05}]")
```

```
[ab000]
[000ab]
[ab000]
```

A sign, the alternate form `#`, and `=` alignment are all refused on a string, in CPython's order
(sign → `z` → `#` → `=`) and with CPython's wording — statically as **SPY0609** on a literal spec:

<!-- spec-sweep: error SPY0609 -->
```python
def main() -> None:
    s: str = "ab"
    print(f"[{s:=5}]")   # SPY0609: '=' alignment not allowed in string format specifier
```

The same refusals fire at runtime (as a `ValueError`) for a dynamic spec, and — because a literal
spec passed through `format(v, spec)` or `"{:spec}".format(v)` is not seen by the static validator —
those two routes are refused only at runtime.

## Nested Replacement Fields

A format spec is itself a mini f-string: a `{...}` inside the spec is a nested replacement field —
an ordinary expression the AST owns, evaluated at runtime. This makes dynamic width and precision
work (the previous implementation dropped the nested field and never type-checked it):

```python
def main() -> None:
    name: str = "Sam"
    width: int = 10
    print(f"[{name:>{width}}]")

    precision: int = 3
    pi: float = 3.14159
    print(f"[{pi:.{precision}f}]")
```

```
[       Sam]
[3.142]
```

The same runtime engine splits `str.format`/`format_map` replacement fields, so `"{:{}}".format(1234, ">8")`
also resolves its nested spec field. The recursion limit is **per route**, matching CPython: a
`str.format` spec may nest one level (a field inside a field's spec raises
`ValueError: Max string recursion exceeded`), while an f-string spec may nest one level deeper.

## Evaluation Order

Holes — and the nested fields inside their specs — evaluate strictly left to right, in source order,
exactly once each. In `f"{a} {b}"`, `a` is evaluated before `b`; in `f"{x:{w}}"`, `x` is evaluated
before `w`. A hole with a side effect therefore observes the same order Python does:

```python
def main() -> None:
    xs: list[int] = [1, 2, 3]
    print(f"{xs.pop(0)} {len([v for v in xs])}")
```

```
1 2
```

`xs.pop(0)` runs first (returning `1` and shortening the list), so `len(...)` sees the two remaining
elements — `1 2`, not `1 3`.

## Invalid Format Specs

A **static** spec (no nested fields) whose type is known is validated at compile time and refused by
name as **SPY0609**, carrying CPython's exact `ValueError`/`TypeError` wording:

<!-- spec-sweep: error SPY0609 -->
```python
def main() -> None:
    x: str = "a"
    print(f"[{x:d}]")   # SPY0609: Unknown format code 'd' for object of type 'str'
```

A non-empty spec on a `None` literal is refused the same way
(`unsupported format string passed to NoneType.__format__`).

The static check is one rule shared by **every route** that carries a literal spec: an f-string or
t-string hole, the `format()` builtin (bare, `builtins.format`, or `format_spec=` by keyword), and
each field of a `str.format` call whose template is a string literal. The same spec is refused with
the same SPY0609 wording wherever it is written:

<!-- spec-sweep: error SPY0609 -->
```python
def main() -> None:
    s: str = "ab"
    print(f"{s:=5}")            # SPY0609: '=' alignment not allowed in string format specifier
    print(t"{s:=5}")            # SPY0609 (same wording)
    print(format(s, "=5"))      # SPY0609 (same wording)
    print("{:=5}".format(s))    # SPY0609 (same wording)
```

A `str.format` field with a nested spec (`"{:{w}}"`), an attribute or index field, a template held
in a variable, and `format_map` are not visible to the static check; they are validated by the
engine at runtime. `str.format` takes positional fields only — a keyword argument is refused with
SPY0234 and a steer to an f-string or `format_map`.

A **dynamic** spec (one with a nested field, or a hole whose type is not statically known) is
validated by the engine at runtime and raises the same `ValueError`/`TypeError` Python would. The
program below compiles, then raises `ValueError: Unknown format code 'q' for object of type 'int'`
when run:

<!-- spec-sweep: fragment -->
```python
def main() -> None:
    x: int = 5
    spec: str = "q"
    print(f"[{x:{spec}}]")   # runtime ValueError
```

## Implicit String Conversion

Non-string expressions in a plain hole are converted to strings via `str()` (which calls `__str__`
or `.ToString()`), matching both Python's f-string behavior and C#'s string interpolation:

<!-- spec-sweep: fragment -->
```python
f"Value: {x}"           # str(x)
f"Location: {point}"    # str(point) -> point.__str__() or point.ToString()
```

## Interpolation Hole Type

Each interpolation hole is typed as an `object` slot, so any expression is accepted — including a
conditional with unrelated branch types (both branches are admitted to `object`):

<!-- spec-sweep: fragment -->
```python
c = True
print(f"{Dog() if c else Cat()}")  # OK — both branches admitted to object
```

Because the hole (and any nested spec field) is a real expression the AST owns, a variable used only
inside a spec is a genuine use — an unused-variable warning is not raised for it.

## F-String Nesting Rules

Sharpy supports nested f-strings, matching Python 3.12+ behavior. The lexer uses a mode stack to
track nested interpolation contexts. Use alternating quote styles for each level:

```python
def main() -> None:
    name: str = "Alice"
    print(f"Hello, {f'dear {name}'}!")
```

```
Hello, dear Alice!
```

**Literal braces:** double a brace to include a literal `{` or `}`:

```python
def main() -> None:
    print(f"Set: {{{1}, {2}, {3}}}")
    print(f"Empty dict: {{}}")
```

```
Set: {1, 2, 3}
Empty dict: {}
```

**Dictionary literals in f-strings:** wrap a dict literal in parentheses so its `{` is not read as a
replacement field, or bind it to a variable first:

<!-- spec-sweep: fragment -->
```python
f"result: {({'key': value})}"   # OK: prints the dict
d = {'key': value}
f"result: {d}"                   # OK
```

## Hole Delimiter Rules

Inside a hole (`{…}`), a `:` at brace depth 1 starts a format spec. When the `:` is inside
parentheses or brackets, it is part of the expression — not a spec delimiter. This lets a
parenthesized expression that contains `:` appear in a hole:

```python
def f() -> str:
    return "hello"

def main() -> None:
    # Walrus — parenthesized to avoid format-spec parse
    print(f"{(s := f())} {s}")

    # Lambda — parenthesized call
    print(f"{(lambda: 42)()}")

    # Slice — parenthesized indexing
    xs: list[int] = [1, 2, 3, 4]
    print(f"{xs[0:2]}")
```

```
hello hello
42
[1, 2]
```

Without parentheses, `{s := f()}` would parse `:` as the format-spec start. The same applies to
`{lambda: 42}` (parsed as `lambda` with format spec `42}`) and bare slices. A bare `{x:=10}` (no
parentheses) is intentionally a **format specifier** — it formats `x` with the spec `=10`, matching
Python; only the parenthesized form is a walrus.

*Implementation*
- *Every hole lowers to `Builtins.Str`/`Repr`/`Ascii(v)` or `Sharpy.PyFormat.Apply(v, spec)` — one Python-format engine, no emitter-side translator.*
- *Holes and nested spec fields are generated through the ordered-operand helper (source order).*
- *Nested f-strings require the lexer mode stack; each hole pushes `StorePosition.FStringHole` with slot `object`.*
