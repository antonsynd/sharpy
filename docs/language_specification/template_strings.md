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
