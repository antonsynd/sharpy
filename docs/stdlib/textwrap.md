# textwrap

Text wrapping and filling, matching Python's textwrap module.

```python
import textwrap
```

## Functions

### `textwrap.wrap(text: str, width: int = 70) -> list[str]`

Wraps a single paragraph of text and returns a list of wrapped lines.
Whitespace in the input is collapsed, and words longer than
*width* are broken to fit.

**Parameters:**

- `text` (str) -- The text to wrap.
- `width` (int) -- The maximum line width (default 70).

**Returns:** A list of wrapped lines without trailing newlines.

### `textwrap.fill(text: str, width: int = 70) -> str`

Wraps a single paragraph of text and returns a single string
containing the wrapped paragraph. This is shorthand for
"\n".join(wrap(text, width)).

**Parameters:**

- `text` (str) -- The text to fill.
- `width` (int) -- The maximum line width (default 70).

**Returns:** A single string with line breaks inserted.

### `textwrap.dedent(text: str) -> str`

Remove any common leading whitespace from all lines in *text*.
Lines that consist solely of whitespace are treated as if they have
no indentation (they don't affect the common prefix calculation) but
their leading whitespace is still stripped.

**Parameters:**

- `text` (str) -- The text to dedent.

**Returns:** The dedented text.

### `textwrap.indent(text: str, prefix: str) -> str`

Add *prefix* to the beginning of selected lines in
*text*. By default, the prefix is added to all lines
that do not consist solely of whitespace (including any line endings).

**Parameters:**

- `text` (str) -- The text to indent.
- `prefix` (str) -- The prefix to add.

**Returns:** The indented text.

### `textwrap.shorten(text: str, width: int) -> str`

Collapse and truncate the given text to fit in the given width.
Whitespace is first collapsed, then the text is truncated to fit
with " [...]" appended as a placeholder.

**Parameters:**

- `text` (str) -- The text to shorten.
- `width` (int) -- The maximum width.

**Returns:** The shortened text.

**Raises:**

- `ValueError` -- Thrown if the placeholder is too large for the width.
