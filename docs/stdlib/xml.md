# xml

XML processing module (ElementTree API).

```python
import xml
```

## Functions

### `xml.create_element(tag: str, attrib: dict[str, str] | None = None) -> Element`

Create a new Element with the given tag and optional attributes.

**Parameters:**

- `tag` (str) -- The element tag name.
- `attrib` (dict[str, str] | None) -- Optional dictionary of attributes.

**Returns:** A new `Element`.

### `xml.sub_element(parent: Element, tag: str, attrib: dict[str, str] | None = None) -> Element`

Create a child element and append it to the parent.

**Parameters:**

- `parent` (Element) -- The parent element.
- `tag` (str) -- The child element tag name.
- `attrib` (dict[str, str] | None) -- Optional dictionary of attributes.

**Returns:** The newly created child element.

### `xml.parse(source: str) -> ElementTree`

Parse an XML file and return an ElementTree.

**Parameters:**

- `source` (str) -- The file path to parse.

**Returns:** An `ElementTree` representing the parsed document.

**Raises:**

- `ParseError` -- If the XML is malformed.

### `xml.fromstring(text: str) -> Element`

Parse an XML string and return the root Element.

**Parameters:**

- `text` (str) -- The XML string to parse.

**Returns:** The root `Element`.

**Raises:**

- `ParseError` -- If the XML is malformed.

### `xml.tostring(element: Element, encoding: str = "unicode", method: str = "xml") -> str`

Serialize an Element to an XML string.

**Parameters:**

- `element` (Element) -- The element to serialize.
- `encoding` (str) -- The encoding. Use "unicode" for a string result.
- `method` (str) -- The serialization method ("xml", "html", or "text").

**Returns:** The XML string representation.

### `xml.register_namespace(prefix: str, uri: str)`

Register a namespace prefix for serialization.
When serializing, registered prefixes will be used instead of auto-generated ones.

**Parameters:**

- `prefix` (str) -- The namespace prefix (e.g., "ns").
- `uri` (str) -- The namespace URI.

### `xml.comment(text: str) -> Element`

Create a comment element with the given text.
Serialized as `&lt;!-- text --&gt;` by `Tostring`.

**Parameters:**

- `text` (str) -- The comment text.

**Returns:** A new comment element.

### `xml.processing_instruction(target: str, text: str | None = None) -> Element`

Create a processing instruction element.
Serialized as `&lt;?target text?&gt;` by `Tostring`.

**Parameters:**

- `target` (str) -- The PI target.
- `text` (str | None) -- Optional PI text.

**Returns:** A new PI element.

### `xml.iselement(obj: object | None) -> bool`

Check if an object is an Element.

**Parameters:**

- `obj` (object | None) -- The object to check.

**Returns:** True if the object is an Element.

### `xml.indent(element: Element, space: str = " ", level: int = 0)`

Add indentation to an element tree for pretty-printing.
Modifies the tree in-place by adding whitespace text.

**Parameters:**

- `element` (Element) -- The root element to indent.
- `space` (str) -- The indentation string per level (default: two spaces).
- `level` (int) -- The starting indentation level (default: 0).

### `xml.indent_tree(tree: ElementTree, space: str = " ", level: int = 0)`

Add indentation to an element tree for pretty-printing.

**Parameters:**

- `tree` (ElementTree) -- The ElementTree to indent.
- `space` (str) -- The indentation string per level (default: two spaces).
- `level` (int) -- The starting indentation level (default: 0).

## Element

Represents an XML element.
Mirrors Python's xml.etree.ElementTree.Element.
Wraps `XElement` and provides a Python-compatible API.

### `get(key: str, @default: str | None = None) -> str | None`

Get the value of an attribute, or a default value if not found.

**Parameters:**

- `key` (str) -- The attribute name.
- `@default` (str | None)

**Returns:** The attribute value, or *default*.

### `set(key: str, value: str)`

Set the value of an attribute.

**Parameters:**

- `key` (str) -- The attribute name.
- `value` (str) -- The attribute value.

### `keys() -> list[str]`

Return a list of attribute names.

### `len() -> int`

Return the number of direct child elements.

### `append(element: Element)`

Append a child element.

### `extend(elements: Iterable[Element])`

Append all elements from the iterable.

### `insert(index: int, element: Element)`

Insert a child element at the given position.

**Parameters:**

- `index` (int) -- The position to insert at.
- `element` (Element) -- The element to insert.

### `remove(element: Element)`

Remove a child element.

**Parameters:**

- `element` (Element) -- The child element to remove.

**Raises:**

- `ValueError` -- If the element is not a direct child.

### `clear()`

Remove all child elements and text.

### `find(path: str, namespaces: dict[str, str] | None = None) -> Element | None`

Find the first matching child element by path.

**Parameters:**

- `path` (str) -- An XPath-like path expression.
- `namespaces` (dict[str, str] | None) -- Optional namespace prefix mapping.

**Returns:** The first matching element, or None if not found.

### `find_all(path: str, namespaces: dict[str, str] | None = None) -> list[Element]`

Find all matching child elements by path.

**Parameters:**

- `path` (str) -- An XPath-like path expression.
- `namespaces` (dict[str, str] | None) -- Optional namespace prefix mapping.

**Returns:** A list of matching elements.

### `find_text(path: str, @default: str | None = None, namespaces: dict[str, str] | None = None) -> str | None`

Find the text content of the first matching child element.

**Parameters:**

- `path` (str) -- An XPath-like path expression.
- `@default` (str | None)
- `namespaces` (dict[str, str] | None) -- Optional namespace prefix mapping.

**Returns:** The text content of the matching element, or *default*.

### `iter(tag: str | None = None) -> Iterable[Element]`

Iterate over all descendant elements (and optionally the element itself)
that match the given tag.

**Parameters:**

- `tag` (str | None) -- Optional tag filter. Null or "*" matches all elements.

**Returns:** An enumerable of matching elements.

### `iter_find(path: str, namespaces: dict[str, str] | None = None) -> Iterable[Element]`

Find all matching elements using an XPath-like expression,
including descendants.

**Parameters:**

- `path` (str) -- An XPath-like path expression.
- `namespaces` (dict[str, str] | None) -- Optional namespace prefix mapping.

**Returns:** An enumerable of matching elements.

### `iter_text() -> Iterable[str]`

Iterate over all text content in this element and its descendants.

**Returns:** An enumerable of text strings.

## ElementTree

Represents an XML document as an element tree.
Mirrors Python's xml.etree.ElementTree.ElementTree.

### `parse(source: str) -> ElementTree`

Parse an XML file and return an ElementTree.

**Parameters:**

- `source` (str) -- The file path to parse.

**Returns:** An ElementTree representing the parsed document.

**Raises:**

- `ParseError` -- If the XML is malformed.

### `parse_string(text: str) -> ElementTree`

Parse an XML string and return an ElementTree.

**Parameters:**

- `text` (str) -- The XML string to parse.

**Returns:** An ElementTree representing the parsed document.

**Raises:**

- `ParseError` -- If the XML is malformed.

### `getroot() -> Element | None`

Get the root element of the tree.

**Returns:** The root element, or None if the tree is empty.

### `find(path: str, namespaces: dict[str, str] | None = None) -> Element | None`

Find the first matching element by path from the root.

**Parameters:**

- `path` (str) -- An XPath-like path expression.
- `namespaces` (dict[str, str] | None) -- Optional namespace prefix mapping.

**Returns:** The first matching element, or None if not found.

### `find_all(path: str, namespaces: dict[str, str] | None = None) -> list[Element]`

Find all matching elements by path from the root.

**Parameters:**

- `path` (str) -- An XPath-like path expression.
- `namespaces` (dict[str, str] | None) -- Optional namespace prefix mapping.

**Returns:** A list of matching elements.

### `iter(tag: str | None = None) -> Iterable[Element]`

Iterate over all elements matching the given tag.

**Parameters:**

- `tag` (str | None) -- Optional tag filter. Null or "*" matches all.

**Returns:** An enumerable of matching elements.

### `iter_find(path: str, namespaces: dict[str, str] | None = None) -> Iterable[Element]`

Find all matching elements using an XPath-like expression from the root.

**Parameters:**

- `path` (str) -- An XPath-like path expression.
- `namespaces` (dict[str, str] | None) -- Optional namespace prefix mapping.

**Returns:** An enumerable of matching elements.

### `write(file_path: str, xml_declaration: bool = True, encoding: str = "utf-8")`

Write the XML tree to a file.

**Parameters:**

- `file_path` (str)
- `xml_declaration` (bool)
- `encoding` (str) -- The encoding name (default: "utf-8").

## ParseError

Exception raised when XML parsing fails.
Mirrors Python's xml.etree.ElementTree.ParseError.

### Properties

| Name | Type | Description |
|------|------|-------------|
| `position` | `int` | The character position where parsing failed. |
| `line` | `int` | The 1-based line number where the error occurred. |
| `column` | `int` | The 1-based column number where the error occurred. |
