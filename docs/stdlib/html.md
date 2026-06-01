# html

HTML processing module.

```python
import html
```

## Functions

### `html.escape(s: str, quote: bool = True) -> str`

Replace special characters "&", "<", ">" with HTML-safe sequences.
When *quote* is True (default), characters '"' and '\'' are also translated.

**Parameters:**

- `s` (str) -- The string to escape.
- `quote` (bool) -- Whether to escape quote characters.

**Returns:** The escaped string.

### `html.unescape(s: str) -> str`

Convert all named and numeric character references (e.g. &gt;, &#62;,
&#x3e;) in the string to the corresponding Unicode characters.

**Parameters:**

- `s` (str) -- The string to unescape.

**Returns:** The unescaped string.

## HTMLParser

A simple HTML parser, similar to Python's html.parser.HTMLParser.
Users subclass this and override the Handle* methods to receive parse events.

### `feed(data: str)`

Feed some text to the parser. It is processed insofar as it consists
of complete elements; incomplete data is buffered until more data is
fed or `Close` is called.

### `close()`

Force processing of all buffered data. This method may be called when
the end of input is reached. Any remaining data is treated as text data.

### `reset()`

Reset the parser instance. Loses all unprocessed data.

### `get_starttag_text() -> str | None`

Return the text of the most recently opened start tag.

### `handle_starttag(tag: str, attrs: list[tuple[str, str | None]])`

Called when an opening tag is encountered.

**Parameters:**

- `tag` (str) -- The tag name, lowercased.
- `attrs` (list[tuple[str, str | None]]) -- List of (name, value) tuples. Value is None for valueless attributes.

### `handle_endtag(tag: str)`

Called when a closing tag is encountered.

**Parameters:**

- `tag` (str) -- The tag name, lowercased.

### `handle_startendtag(tag: str, attrs: list[tuple[str, str | None]])`

Called when a self-closing tag like  is encountered.
The default implementation calls `HandleStarttag` then `HandleEndtag`.

**Parameters:**

- `tag` (str) -- The tag name, lowercased.
- `attrs` (list[tuple[str, str | None]]) -- List of (name, value) tuples.

### `handle_data(data: str)`

Called when character data is encountered.

**Parameters:**

- `data` (str) -- The character data.

### `handle_comment(data: str)`

Called when an HTML comment <!-- ... --> is encountered.

**Parameters:**

- `data` (str) -- The comment text (without delimiters).

### `handle_entityref(name: str)`

Called when a named character reference like &amp; is encountered
(only when convertCharrefs is False).

**Parameters:**

- `name` (str) -- The entity name (e.g., "amp").

### `handle_charref(name: str)`

Called when a numeric character reference like &#60; or &#x3c; is encountered
(only when convertCharrefs is False).

**Parameters:**

- `name` (str) -- The numeric reference (e.g., "60" or "x3c").

### `handle_decl(decl: str)`

Called when a DOCTYPE declaration is encountered.

**Parameters:**

- `decl` (str) -- The declaration text (e.g., "DOCTYPE html").

### `handle_pi(data: str)`

Called when a processing instruction like <?...?> is encountered.

**Parameters:**

- `data` (str) -- The processing instruction data.
