# re

Regular expression operations.

```python
import re
```

## Functions

### `re.search(s: str, pos: int = 0, endpos: int | None = None) -> MatchResult | None`

Scan through string looking for the first match.

### `re.match(s: str, pos: int = 0, endpos: int | None = None) -> MatchResult | None`

Try to apply the pattern at the start of the string.

### `re.fullmatch(s: str, pos: int = 0, endpos: int | None = None) -> MatchResult | None`

Try to apply the pattern to the entire string.

### `re.findall(s: str, pos: int = 0, endpos: int | None = None) -> list[object]`

Return all non-overlapping matches as a list.

### `re.finditer(s: str, pos: int = 0, endpos: int | None = None) -> list[MatchResult]`

Return a list of MatchResult objects over all non-overlapping matches.

### `re.sub(repl: str, s: str, count: int = 0) -> str`

Return the string obtained by replacing occurrences using a string.

### `re.sub(repl: (MatchResult) -> str, s: str, count: int = 0) -> str`

Return the string obtained by replacing occurrences using a callable.

### `re.split(s: str, maxsplit: int = 0) -> list[str]`

Split string by the occurrences of the pattern.

### `re.group(n: int = 0) -> str | None`

Return the string matched by group number.

### `re.group(name: str) -> str | None`

Return the string matched by a named group.

### `re.groups() -> list[str | None]`

Return a list of all subgroups (groups 1..n).

### `re.groupdict() -> dict[str, str | None]`

Return a dict of all named subgroups.

### `re.start(group_num: int = 0) -> int`

Start index of the matched group.

### `re.end(group_num: int = 0) -> int`

End index of the matched group.

### `re.expand(template: str) -> str`

Return the string obtained by doing backslash substitution on the template.

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

### `re.split(pattern: str, s: str, maxsplit: int = 0, flags: int = 0) -> list[str]`

Split string by the occurrences of pattern.

### `re.purge()`

Clear the regular expression cache (no-op on .NET).

### `re.escape(pattern: str) -> str`

Escape special characters in pattern.
