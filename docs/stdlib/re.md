# re

Wraps a .NET `System.Text.RegularExpressions.Match` with Python-compatible API.

```python
import re
```

## Constants

| Name | Type | Description |
|------|------|-------------|
| `ignorecase` | `int` | Perform case-insensitive matching. |
| `i` | `int` | Shorthand for IGNORECASE. |
| `multiline` | `int` | Make ^ and $ match at line boundaries. |
| `m` | `int` | Shorthand for MULTILINE. |
| `dotall` | `int` | Make . match any character including newline. |
| `s` | `int` | Shorthand for DOTALL. |

## Properties

| Name | Type | Description |
|------|------|-------------|
| `string` | `str` | The input string. |
| `pattern` | `str` | The pattern string. |
| `pos` | `int` | The start position of the search. |
| `endpos` | `int` | The end position of the search. |
| `pattern_str` | `str` | The original pattern string. |
| `flags` | `int` | The flags used to compile this pattern. |

## Functions

### `re.group(n: int = 0) -> str?`

Return the string matched by group number. Group 0 is the entire match.
Returns null if the group didn't participate in the match.

### `re.group(name: str) -> str?`

Return the string matched by a named group.
Returns null if the group didn't participate in the match.

### `re.groups() -> list[str?]`

Return a list of all subgroups (groups 1..n).

### `re.groupdict() -> dict[str, str?]`

Return a Dict of all named subgroups.

### `re.start(group: int = 0) -> int`

Start index of the matched group.

### `re.end(group: int = 0) -> int`

End index of the matched group.

### `re.search(s: str, pos: int = 0, endpos: int = -1) -> ReMatch?`

Scan through string looking for the first location where the pattern produces a match.

### `re.match(s: str, pos: int = 0, endpos: int = -1) -> ReMatch?`

Try to apply the pattern at the start of the string.

### `re.fullmatch(s: str, pos: int = 0, endpos: int = -1) -> ReMatch?`

Try to apply the pattern to the entire string.

### `re.findall(s: str, pos: int = 0, endpos: int = -1) -> list[object?]`

Return all non-overlapping matches as a list of strings.
If the pattern has groups, returns the group(s) rather than the full match.

### `re.finditer(s: str, pos: int = 0, endpos: int = -1) -> list[ReMatch]`

Return an iterator yielding ReMatch objects over all non-overlapping matches.

### `re.sub(repl: str, s: str, count: int = 0) -> str`

Return the string obtained by replacing the leftmost non-overlapping occurrences of pattern.

### `re.split(s: str, maxsplit: int = 0) -> list[str]`

Split string by the occurrences of the pattern.

### `re.translate(pattern: str) -> str`

Translate Python-specific regex syntax to .NET-compatible syntax.
Handles:
  (?P...) → (?...)
  (?P=name) → \k

### `re.compile(pattern: str, flags: int = 0) -> RePattern`

Compile a pattern into a RePattern object.

### `re.search(pattern: str, s: str, flags: int = 0) -> ReMatch?`

Scan through string looking for the first location where the pattern produces a match.

**Parameters:**

- `pattern` (str) -- The regular expression pattern.
- `s` (str) -- The string to search.
- `flags` (int) -- Optional regex flags (e.g., re.IGNORECASE).

**Returns:** A match object, or `null` if no match is found.

```python
m = re.search(r"\d+", "abc123")
m.group()    # "123"
```

### `re.match(pattern: str, s: str, flags: int = 0) -> ReMatch?`

Try to apply the pattern at the start of the string.

**Parameters:**

- `pattern` (str) -- The regular expression pattern.
- `s` (str) -- The string to match against.
- `flags` (int) -- Optional regex flags.

**Returns:** A match object, or `null` if the pattern does not match at the start.

```python
m = re.match(r"\d+", "123abc")
m.group()    # "123"
```

### `re.fullmatch(pattern: str, s: str, flags: int = 0) -> ReMatch?`

Try to apply the pattern to the entire string.

### `re.findall(pattern: str, s: str, flags: int = 0) -> list[object?]`

Return all non-overlapping matches of pattern in string, as a list.

### `re.finditer(pattern: str, s: str, flags: int = 0) -> list[ReMatch]`

Return an iterator yielding match objects over all non-overlapping matches.

### `re.sub(pattern: str, repl: str, s: str, count: int = 0, flags: int = 0) -> str`

Return the string obtained by replacing the leftmost non-overlapping occurrences.

**Parameters:**

- `pattern` (str) -- The regular expression pattern.
- `repl` (str) -- The replacement string.
- `s` (str) -- The string to search and replace in.
- `count` (int) -- Maximum number of replacements (0 = all).
- `flags` (int) -- Optional regex flags.

**Returns:** The modified string.

```python
re.sub(r"\d+", "N", "abc123def456")    # "abcNdefN"
```

### `re.split(pattern: str, s: str, maxsplit: int = 0, flags: int = 0) -> list[str]`

Split string by the occurrences of the pattern.

### `re.escape(pattern: str) -> str`

Escape special characters in pattern.
