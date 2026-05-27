# fnmatch

```python
import fnmatch
```

## Functions

### `fnmatch.fnmatch(name: str, pat: str) -> bool`

Test whether filename matches pattern.

### `fnmatch.fnmatchcase(name: str, pat: str) -> bool`

Test whether filename matches pattern, including case.

### `fnmatch.filter(names: list[str], pat: str) -> list[str]`

Construct a list from those elements of the names sequence that match pattern.

### `fnmatch.translate(pat: str) -> str`

Translate a shell pattern to a regular expression.
