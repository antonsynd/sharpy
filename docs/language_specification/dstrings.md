# D-Strings (Dedented String Literals)

D-strings apply automatic indentation stripping to triple-quoted string literals. The `d` prefix is a lexer-time transformation — by the time the parser sees the token, it is an ordinary string with the leading whitespace already removed.

## Syntax

```python
d"""
    hello
    world
    """

d'''
    line one
    line two
    '''

dr"""
    raw\nstring
    no escapes
    """

df"""
    Hello, {name}!
    Today is {day}.
    """
```

### Supported prefix combinations

| Prefix | Effect |
|--------|--------|
| `d"..."` / `d'...'` | Single-line: equivalent to `"..."` (no-op) |
| `d"""..."""` | Dedented multiline string |
| `dr"""..."""` | Dedented multiline raw string (no escape processing) |
| `df"""..."""` | Dedented multiline f-string (interpolation + dedent) |

Note: `db"""..."""` (byte strings with dedent) is not supported.

## Dedentation Algorithm

The amount of whitespace to strip is determined by the **indentation of the closing delimiter line** — the line that contains the closing `"""` or `'''`.

Steps:

1. Collect the raw triple-quoted string content (everything between opening and closing delimiters).
2. Split the content by `\n` into lines.
3. Inspect the **last segment** (after the final `\n`). It must consist entirely of whitespace; this is the closing-delimiter indentation line. Its length `N` is the strip amount.
4. Remove the last segment (it is not content).
5. If the first line (immediately after the opening `"""`) is empty or consists only of whitespace, remove it.
6. For each remaining line:
   - If the line is empty or consists only of whitespace: leave it as a blank line.
   - Otherwise: the line must begin with at least `N` whitespace characters. Strip exactly the first `N` characters. If it does not, a compile error is emitted (see Errors).
7. Join the remaining lines with `\n`.

### Example

```python
x = d"""
    hello
    world
    """
```

Raw content after triple-quote scan: `"\n    hello\n    world\n    "`

Processing:
- Split: `["", "    hello", "    world", "    "]`
- Last segment `"    "` → N = 4
- Remove last segment: `["", "    hello", "    world"]`
- First segment is empty → remove: `["    hello", "    world"]`
- Strip 4 from each: `["hello", "world"]`
- Join: `"hello\nworld"`

`print(x)` outputs:
```
hello
world
```

### Blank lines and trailing newlines

Blank lines within the content are preserved. A blank line before the closing delimiter becomes a trailing newline:

```python
x = d"""
    hello

    world

    """
# x == "hello\n\nworld\n"
```

### Tabs

Tabs and spaces are treated as distinct characters (they are not interchangeable). A tab counts as one character toward the strip amount. Mixing tabs and spaces in the leading whitespace of the closing delimiter line and content lines is allowed but may produce unexpected results if they do not align exactly.

### Single-line d-strings

A single-line d-string is valid but the `d` prefix has no effect:

```python
x = d"hello"    # equivalent to "hello"
x = d'world'    # equivalent to 'world'
```

## Combination Prefixes

### `dr"""..."""` — Dedented Raw String

Backslash sequences are not processed (same as `r"""..."""`). Dedentation is still applied to the raw content:

```python
path = dr"""
    C:\Users\alice
    C:\Users\bob
    """
# path == "C:\\Users\\alice\nC:\\Users\\bob"
```

Note: Because escape sequences are not processed, the line content is the literal source text minus the stripped indentation.

### `df"""..."""` — Dedented F-String

Interpolation expressions are evaluated at runtime. Dedentation is applied to the literal text portions between interpolations:

```python
name = "Alice"
msg = df"""
    Hello, {name}!
    Welcome.
    """
# msg == "Hello, Alice!\nWelcome."
```

The closing-delimiter indentation is determined at compile time from the source. The strip amount is fixed and applies to each line of literal text in the f-string.

## Errors

### SPY0029 — Dedented string indentation error

Emitted when a content line has less leading whitespace than the closing delimiter:

```python
# Error: second line has only 2 spaces but closing delimiter has 4
x = d"""
    hello
  world
    """
```

```
error SPY0029: d-string content line has insufficient indentation (expected 4 spaces, found 2)
```

Also emitted if the closing delimiter line contains non-whitespace characters.

## Design Notes

- Dedentation happens entirely at lex time — the AST receives a plain `String` (or `RawString`) token.
- This is equivalent to calling `textwrap.dedent()` and stripping the initial newline, but the strip amount is always determined by the closing-delimiter indentation rather than the common indent across all lines.
- The closing-delimiter rule gives the programmer explicit control: to strip 4 spaces, place `"""` with 4-space indentation.

## Related

- [F-Strings](fstrings.md) — interpolated string literals
- [String Literals](string_literals.md) — all string forms
- [String Type](string_type.md) — `str` type methods
