# re

Regular expression operations.

```python
import re
```

## Functions

### `re.compile(pattern: str, flags: int = 0) -> Pattern`

Compile a regular expression pattern into a Pattern object.

### `re.search(pattern: str, s: str, flags: int = 0) -> MatchResult | None`

Scan through string looking for the first match.

### `re.match(pattern: str, s: str, flags: int = 0) -> MatchResult | None`

Try to apply the pattern at the start of the string.

### `re.fullmatch(pattern: str, s: str, flags: int = 0) -> MatchResult | None`

Try to apply the pattern to the entire string.

### `re.findall(pattern: str, s: str, flags: int = 0) -> list[object]`

Return all non-overlapping matches of pattern in string.

### `re.finditer(pattern: str, s: str, flags: int = 0) -> list[MatchResult]`

Return a list of match objects over all non-overlapping matches.

### `re.sub(pattern: str, repl: str, s: str, count: int = 0, flags: int = 0) -> str`

Return the string obtained by replacing occurrences.

### `re.sub(pattern: str, repl: (MatchResult) -> str, s: str, count: int = 0, flags: int = 0) -> str`

Return the string obtained by replacing occurrences using a callable.

### `re.subn(pattern: str, repl: str, s: str, count: int = 0, flags: int = 0) -> tuple[str, int]`

Like sub(), but returns (new_string, number_of_subs_made).

### `re.subn(pattern: str, repl: (MatchResult) -> str, s: str, count: int = 0, flags: int = 0) -> tuple[str, int]`

Like sub() with callable, but returns (new_string, number_of_subs_made).

### `re.split(pattern: str, s: str, maxsplit: int = 0, flags: int = 0) -> list[str]`

Split string by the occurrences of pattern.

### `re.purge()`

Clear the regular expression cache (no-op on .NET).

### `re.escape(pattern: str) -> str`

Escape special characters in pattern.

## error

Exception raised when a regex pattern is invalid.

## Pattern

Compiled regular expression pattern, wrapping .NET Regex.

### `search(s: str, pos: int = 0, endpos: int | None = None) -> MatchResult | None`

Scan through string looking for the first match.

### `match(s: str, pos: int = 0, endpos: int | None = None) -> MatchResult | None`

Try to apply the pattern at the start of the string.

### `fullmatch(s: str, pos: int = 0, endpos: int | None = None) -> MatchResult | None`

Try to apply the pattern to the entire string.

### `findall(s: str, pos: int = 0, endpos: int | None = None) -> list[object]`

Return all non-overlapping matches as a list.

### `finditer(s: str, pos: int = 0, endpos: int | None = None) -> list[MatchResult]`

Return a list of MatchResult objects over all non-overlapping matches.

### `sub(repl: str, s: str, count: int = 0) -> str`

Return the string obtained by replacing occurrences using a string.

### `sub(repl: (MatchResult) -> str, s: str, count: int = 0) -> str`

Return the string obtained by replacing occurrences using a callable.

### `subn(repl: str, s: str, count: int = 0) -> tuple[str, int]`

Like sub(), but returns (new_string, number_of_subs_made).

### `subn(repl: (MatchResult) -> str, s: str, count: int = 0) -> tuple[str, int]`

Like sub() with callable, but returns (new_string, number_of_subs_made).

### `split(s: str, maxsplit: int = 0) -> list[str]`

Split string by the occurrences of the pattern.

### `__str__() -> str`

`repr()` uses the same method. Returns a string representation of the compiled pattern.

## MatchResult

Wraps a .NET Match with Python-compatible regex match API.

### `group(n: int = 0) -> str | None`

Return the string matched by group number.

### `group(name: str) -> str | None`

Return the string matched by a named group.

### `groups() -> list[str | None]`

Return a list of all subgroups (groups 1..n).

### `groupdict() -> dict[str, str | None]`

Return a dict of all named subgroups.

### `start(group_num: int = 0) -> int`

Start index of the matched group.

### `end(group_num: int = 0) -> int`

End index of the matched group.

### `span(group_num: int = 0) -> tuple[int, int]`

Returns (start, end) for the matched group.

### `expand(template: str) -> str`

Return the string obtained by doing backslash substitution on the template.

### `__str__() -> str`

`repr()` uses the same method. String representation of the match.
