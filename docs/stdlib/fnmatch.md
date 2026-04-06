# fnmatch

Unix filename pattern matching, matching Python's fnmatch module.

```python
import fnmatch
```

## Functions

### `fnmatch.fn_match(name: str, pat: str) -> bool`

Test whether  matches .
The pattern uses Unix shell-style wildcards:
* matches everything, ? matches any single character,
[seq] matches any character in seq, [!seq] matches any
character not in seq.
On Windows, the comparison is case-insensitive. On Unix, it is
case-sensitive.

**Parameters:**

- `name` (str) -- The filename to test.
- `pat` (str) -- The pattern to match against.

**Returns:** true if  matches .

### `fnmatch.fn_match_case(name: str, pat: str) -> bool`

Test whether  matches ,
using a case-sensitive comparison regardless of the platform.

**Parameters:**

- `name` (str) -- The filename to test.
- `pat` (str) -- The pattern to match against.

**Returns:** true if  matches .

### `fnmatch.filter(names: list[str], pat: str) -> list[str]`

Return the subset of the list of  that match
. Same as
[n for n in names if fnmatch(n, pat)] but more efficient.

**Parameters:**

- `names` (list[str]) -- The list of filenames to filter.
- `pat` (str) -- The pattern to match against.

**Returns:** A new list of matching filenames.

### `fnmatch.translate(pat: str) -> str`

Translate a shell-style  to a regular expression.
The resulting string will be a regex pattern suitable for use with
.

**Parameters:**

- `pat` (str) -- The fnmatch pattern to translate.

**Returns:** A regex string equivalent to the pattern.
