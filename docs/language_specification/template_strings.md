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
a fourth format authority. Holes and their nested spec fields evaluate left to right, in source
order:

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
surface.

## Template Type

T-strings produce a value of type `Template`. You can annotate variables explicitly:

```python
name = "Alice"
greeting: Template = t"Hi {name}"
print(greeting)
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
