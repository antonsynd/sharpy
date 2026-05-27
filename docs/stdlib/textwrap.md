# textwrap

```python
import textwrap
```

## Functions

### `textwrap.wrap(text: str, width: int = 70) -> list[str]`

Wrap a single paragraph of text, returning a list of wrapped lines.

### `textwrap.fill(text: str, width: int = 70) -> str`

Wrap a single paragraph of text, and return a single string with newlines.

### `textwrap.dedent(text: str) -> str`

Remove any common leading whitespace from all lines in text.

### `textwrap.indent(text: str, prefix: str) -> str`

Add prefix to the beginning of selected lines in text.

### `textwrap.shorten(text: str, width: int) -> str`

Collapse and truncate the given text to fit in the given width.

### `textwrap._collapse_whitespace(text: str) -> str`

Collapse runs of whitespace into a single space and strip leading/trailing whitespace.

### `textwrap._is_whitespace_only(line: str) -> bool`

Return True if line contains only whitespace characters.

### `textwrap._get_leading_whitespace(line: str) -> str`

Return the leading whitespace of a line.

### `textwrap._common_prefix(a: str, b: str) -> str`

Return the longest common prefix of strings a and b.

### `textwrap._split_keep_ends(text: str) -> list[str]`

Split text into lines, preserving line endings.
