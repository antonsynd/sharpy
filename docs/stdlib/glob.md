# glob

Unix shell-style pathname pattern expansion.

```python
import glob
```

## Functions

### `glob.glob(pattern: str) -> list[str]`

Return a sorted list of pathnames matching a pathname pattern.
Similar to Python's `glob.glob()`.

**Parameters:**

- `pattern` (str) -- A glob pattern (e.g., "*.txt", "**/*.cs").

**Returns:** A sorted list of matching paths. Empty list if no matches.

```python
glob.glob("*.txt")          # ["a.txt", "b.txt"]
glob.glob("**/*.cs")        # recursive search for .cs files
glob.glob("src/[ab]*.py")   # files starting with a or b
```

### `glob.iglob(pattern: str) -> Iterable[str]`

Return an iterator which yields the same values as `Glob`
without actually storing them all simultaneously.
Similar to Python's `glob.iglob()`.

**Parameters:**

- `pattern` (str) -- A glob pattern (e.g., "*.txt", "**/*.cs").

**Returns:** An enumerable of matching paths.

### `glob.escape(pathname: str) -> str`

Escape all special characters in a pathname.
Similar to Python's `glob.escape()`.
Special characters `*`, `?`, and `[` are escaped
by wrapping them in brackets.

**Parameters:**

- `pathname` (str) -- The pathname to escape.

**Returns:** The escaped pathname.

```python
glob.escape("file[1].txt")    # "file[[]1].txt"
glob.escape("*.py")           # "[*].py"
```
