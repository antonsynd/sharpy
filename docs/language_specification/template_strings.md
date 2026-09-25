# Template Strings (T-Strings)

Template strings (t-strings) provide structured string interpolation, producing a `Template` object instead of a plain string. Inspired by Python PEP 750.

## Syntax

T-strings use the `t` prefix, analogous to f-strings:

```python
name = "Alice"
greeting = t"Hello {name}"
print(greeting)  # Hello Alice
```

T-strings support the same interpolation syntax as f-strings — any expression can appear inside `{}`:

```python
x = 10
result = t"Value: {x * 2}"
```

## Format Specs and Nested Fields

A t-string hole may carry a format spec (`{expr:spec}`), including nested replacement fields inside
the spec (`{expr:>{width}}`), exactly like an f-string. Each `Interpolation` keeps its evaluated
value and its spec; rendering the `Template` applies the spec through the same one Python-format
engine (`Sharpy.PyFormat.Apply`) that f-strings, `str.format` and `format()` use — a t-string is not
a fourth format authority. A literal spec in a t-string hole gets the f-string's static check too
(SPY0609 with CPython's wording, see [Invalid Format Specs](fstrings.md#invalid-format-specs)). Holes
and their nested spec fields evaluate left to right, in source order:

```python
def main() -> None:
    pi: float = 3.14159
    print(t"Pi is {pi:.2f}")

    name: str = "hi"
    width: int = 8
    print(t"[{name:>{width}}]")
```

```
Pi is 3.14
[      hi]
```

## Conversions and Self-Documenting Fields

A t-string hole honours the conversion flags `!r`, `!s` and `!a` and the self-documenting `=`
exactly as an f-string does. Each `Interpolation` records its conversion (`"r"`, `"s"`, `"a"`, or
none — PEP 750's `conversion` field), and rendering applies it (`repr`, `str` or `ascii` of the
value) before the format spec. A self-documenting `{x=}` keeps its `x=` text in the template's
string segments and implies `!r` when no spec is given; with a spec (`{x=:>4}`) the value is
formatted, not repr'd — the same rule as f-strings (PEP 750: `t"{x=}"` has strings `("x=", "")`
and conversion `"r"`).

```python
def main() -> None:
    s: str = "ab"
    x: int = 1
    e: str = "é"
    print(t"{s!r:>6}|{s!a}|{x=}|{s=:>4}|{e!a}")
```

```
  'ab'|'ab'|x=1|s=  ab|'\xe9'
```

"Rendering" here is Sharpy's rendering of a `Template` (what `print` and `str()` produce), which
agrees byte-for-byte with the f-string on the same fields. Python's `str()` of a `Template` does not
render it; the rendered form is a Sharpy convenience, and the structured fields are the PEP 750
surface. The same holds for a single `Interpolation`: `print(i)` / `str(i)` renders its converted,
formatted value, where Python prints its repr (`Interpolation(1, 'x', None, '')`). `repr()` of
either is PEP 750's spelling (see [repr()](#repr)).

## Template Type

T-strings produce a value of type `Template`. You can annotate variables explicitly:

```python
name = "Alice"
greeting: Template = t"Hi {name}"
print(greeting)
```

`Template` is the runtime class `Sharpy.Template`; its members, iteration and `+` are checked
against that class, so an unknown member is a compile-time error (SPY0203), exactly as for any
other class.

## Template and Interpolation Attributes

A `Template` exposes PEP 750's structural fields:

| Member | Type | Meaning |
|--------|------|---------|
| `strings` | `array[str]` | The literal segments; always one more than the interpolations (empty segments included) |
| `interpolations` | `array[Interpolation]` | One per hole, in source order |
| `values` | `array[object]` | Each interpolation's evaluated value |

and each `Interpolation`:

| Member | Type | Meaning |
|--------|------|---------|
| `value` | `object` | The hole's evaluated value |
| `expression` | `str` | The hole's source text, from just after `{` to the `}`, `=`, `!` or `:` that ends it — leading whitespace kept, `#` comments removed, then trailing whitespace stripped (`t"{ x }"` → `' x'`, `t"{d['k']}"` → `"d['k']"`) |
| `conversion` | `str \| None` | `"r"`, `"s"`, `"a"`, or `None` |
| `format_spec` | `str` | The spec, with nested fields already evaluated (`t"{x:{w}}"` with `w = 5` → `'5'`); `''` when there is none |

```python
def main() -> None:
    x: int = 1
    s: str = "ab"
    tp = t"a{x}b{ s !r:>6}"
    print(len(tp.strings), repr(tp.strings[0]), repr(tp.strings[2]))
    print(len(tp.interpolations), len(tp.values))
    i = tp.interpolations[1]
    print(repr(i.value), repr(i.expression), repr(i.conversion), repr(i.format_spec))
    print(repr(tp.interpolations[0].conversion), repr(tp.interpolations[0].format_spec))
```

```
3 'a' ''
2 2
'ab' ' s' 'r' '>6'
None ''
```

The three arrays are .NET arrays: index them, take `len()`, or iterate them. Printing one directly
does not yet match Python: `print(tp.strings)` spreads the array into `print`'s arguments and prints
the segments space-separated, where Python prints the tuple `('a', 'b', '')` (#2011).

A hole's expression may span lines and carry `#` comments (PEP 701). `expression` is the hole's
source with its comments removed, then trailing whitespace stripped; the `=` form's text keeps its
newlines:

```python
def main() -> None:
    x: int = 1
    tp = t"""{x # the count
 + 1 # plus one
}"""
    print(repr(tp.interpolations[0].expression), tp.interpolations[0].value)
    doc = t"""{x # c
=}"""
    print(repr(doc.strings[0]))
```

```
'x \n + 1' 2
'x \n='
```

## Iteration

Iterating a `Template` yields its non-empty string segments and its `Interpolation`s, interleaved in
source order (PEP 750); the element type is `object`. An empty t-string yields nothing.

```python
def main() -> None:
    x: int = 1
    tp = t"a{x}b"
    for part in tp:
        print(repr(part))
    print(list(tp))
    print(list(t""))
```

```
'a'
Interpolation(1, 'x', None, '')
'b'
['a', Interpolation(1, 'x', None, ''), 'b']
[]
```

## repr()

`repr()` of a `Template` or an `Interpolation` is PEP 750's spelling: a `Template` shows its strings
and interpolations as tuples, and an `Interpolation` always shows four positions — value (as
`repr`), expression, conversion (`None` when absent) and format spec. `!r`, `ascii()` and `repr()`
of a list containing them use the same spelling.

```python
def main() -> None:
    x: int = 1
    s: str = "ab"
    print(repr(t""))
    print(repr(t"plain"))
    print(repr(t"{x}"))
    print(repr(t"a{x}b{s!r:>6}"))
    print(repr(t"{x=}"))
```

```
Template(strings=('',), interpolations=())
Template(strings=('plain',), interpolations=())
Template(strings=('', ''), interpolations=(Interpolation(1, 'x', None, ''),))
Template(strings=('a', 'b', ''), interpolations=(Interpolation(1, 'x', None, ''), Interpolation('ab', 's', 'r', '>6')))
Template(strings=('x=', ''), interpolations=(Interpolation(1, 'x', 'r', ''),))
```

## Triple-Quoted T-Strings

Multi-line t-strings use triple quotes:

```python
name = "World"
msg = t"""
Hello {name}
Welcome!
"""
```

## T-String Concatenation

T-strings can be concatenated with the `+` operator:

```python
first = t"Hello "
second = t"World"
combined = first + second
```

## Relationship to F-Strings

| Feature | F-String (`f"..."`) | T-String (`t"..."`) |
|---------|---------------------|---------------------|
| Prefix | `f` | `t` |
| Result type | `str` | `Template` |
| Interpolation | `{expr}` | `{expr}` |
| Conversions, `=` | `!r` `!s` `!a`, `{x=}` | same, recorded per `Interpolation` |
| Use case | String formatting | Structured interpolation |

Both f-strings and t-strings share the same interpolation part structure internally (`FStringPart`), but t-strings preserve interpolation structure in the resulting `Template` object rather than eagerly producing a string.

## Generated C#

T-strings are lowered similarly to f-strings, producing a `Template` value:

```python
name = "world"
result = t"Hello {name}"
```

*Implementation*
- *✅ Implemented — `TStringLiteral` AST node, lexer support for `t"..."` / `t'...'` / `t"""..."""` prefixes*
- *Produces `Template` type (not `str`)*
- *Shares interpolation infrastructure with f-strings*
