# textwrap

Text wrapping and filling.

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
