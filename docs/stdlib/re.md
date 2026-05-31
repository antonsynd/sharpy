# re

Regular expression operations.

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
| `verbose` | `int` | Allow verbose regex with comments and whitespace. |
| `x` | `int` | Shorthand for VERBOSE. |
| `unicode` | `int` | Make \\w, \\b, etc. match Unicode (default on .NET, accepted for compatibility). |
| `u` | `int` | Shorthand for UNICODE. |
| `ascii` | `int` | Make \\w, \\b, etc. match ASCII only (no-op on .NET, accepted for compatibility). |
| `a` | `int` | Shorthand for ASCII. |

## Functions

### `re.compile(pattern: str, flags: int = 0) -> RePattern`

Compile a pattern into a RePattern object.

### `re.search(pattern: str, s: str, flags: int = 0) -> ReMatch | None`

Scan through string looking for the first location where the pattern produces a match.

**Parameters:**

- `pattern` (str) -- The regular expression pattern.
- `s` (str) -- The string to search.
- `flags` (int) -- Optional regex flags (e.g., re.IGNORECASE).

**Returns:** A match object, or `None` if no match is found.

```python
m = re.search(r"\d+", "abc123")
m.group()    # "123"
```

### `re.match(pattern: str, s: str, flags: int = 0) -> ReMatch | None`

Try to apply the pattern at the start of the string.

**Parameters:**

- `pattern` (str) -- The regular expression pattern.
- `s` (str) -- The string to match against.
- `flags` (int) -- Optional regex flags.

**Returns:** A match object, or `None` if the pattern does not match at the start.

```python
m = re.match(r"\d+", "123abc")
m.group()    # "123"
```

### `re.fullmatch(pattern: str, s: str, flags: int = 0) -> ReMatch | None`

Try to apply the pattern to the entire string.

### `re.findall(pattern: str, s: str, flags: int = 0) -> list[object | None]`

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

### `re.sub(pattern: str, repl: (ReMatch) -> str, s: str, count: int = 0, flags: int = 0) -> str`

Return the string obtained by replacing occurrences using a callable.
The callable receives the match object and returns the replacement string.

### `re.purge()`

Clear the regular expression cache. No-op on .NET (no internal cache).

### `re.escape(pattern: str) -> str`

Escape special characters in pattern.

## Match

Wraps a .NET `Match` with Python-compatible API.

### Properties

| Name | Type | Description |
|------|------|-------------|
| `string` | `str` | The input string. |
| `pattern` | `str` | The pattern string. |
| `pos` | `int` | The start position of the search. |
| `endpos` | `int` | The end position of the search. |
| `re` | `RePattern | None` | The compiled pattern object that produced this match, or null. |

### `group(n: int = 0) -> str | None`

Return the string matched by group number. Group 0 is the entire match.
Returns None if the group didn't participate in the match.

### `group(name: str) -> str | None`

Return the string matched by a named group.
Returns None if the group didn't participate in the match.

### `groups() -> list[str | None]`

Return a list of all subgroups (groups 1..n).

### `groupdict() -> dict[str, str | None]`

Return a Dict of all named subgroups.

### `start(group: int = 0) -> int`

Start index of the matched group.

### `end(group: int = 0) -> int`

End index of the matched group.

### `expand(template: str) -> str`

Return the string obtained by doing backslash substitution on the template.
Supports \1, \2, ... and \g references.

## Pattern

Compiled regular expression pattern, wrapping .NET's Regex.

### Properties

| Name | Type | Description |
|------|------|-------------|
| `pattern_str` | `str` | The original pattern string. |
| `flags` | `int` | The flags used to compile this pattern. |
| `pattern` | `str` | The pattern string (Python-compatible alias for PatternStr). |

### `search(s: str, pos: int = 0, endpos: int = -1) -> ReMatch | None`

Scan through string looking for the first location where the pattern produces a match.

### `match(s: str, pos: int = 0, endpos: int = -1) -> ReMatch | None`

Try to apply the pattern at the start of the string.

### `fullmatch(s: str, pos: int = 0, endpos: int = -1) -> ReMatch | None`

Try to apply the pattern to the entire string.

### `findall(s: str, pos: int = 0, endpos: int = -1) -> list[object | None]`

Return all non-overlapping matches as a list of strings.
If the pattern has groups, returns the group(s) rather than the full match.

### `finditer(s: str, pos: int = 0, endpos: int = -1) -> list[ReMatch]`

Return an iterator yielding ReMatch objects over all non-overlapping matches.

### `sub(repl: str, s: str, count: int = 0) -> str`

Return the string obtained by replacing the leftmost non-overlapping occurrences of pattern.

### `sub(repl: (ReMatch) -> str, s: str, count: int = 0) -> str`

Return the string obtained by replacing occurrences using a callable.
The callable receives the match object and returns the replacement string.

### `split(s: str, maxsplit: int = 0) -> list[str]`

Split string by the occurrences of the pattern.

## error

Exception raised when a regex pattern is invalid.
Equivalent to Python's `re.error`.

### Properties

| Name | Type | Description |
|------|------|-------------|
| `msg` | `str` | The unformatted error message. |
| `pattern` | `str | None` | The regex pattern that caused the error, if available. |
| `pos` | `int | None` | The position in the pattern where the error occurred, if available. |
| `lineno` | `int | None` | The line number of the error position, if available. |
| `colno` | `int | None` | The column number of the error position, if available. |
