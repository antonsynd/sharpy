# yaml

Holds the comments associated with a single key (in a mapping) or item (in a
sequence) for YAML roundtrip preservation, mirroring ruamel.yaml's comment model.

```python
import yaml
```

## Properties

| Name | Type | Description |
|------|------|-------------|
| `before_comment` | `str?` | Comment appearing on the line(s) before the associated node. |
| `inline_comment` | `str?` | Comment trailing the associated node on the same line. |
| `after_comment` | `str?` | Comment appearing on the line(s) after the associated node. |
| `has_comments` | `bool` | Gets a value indicating whether this instance carries any comment text. |

## Functions

### `yaml.safe_load(text: str) -> object?`

Parse the first YAML document in *text* and return the
corresponding Sharpy value (Dict, List, or scalar).

**Parameters:**

- `text` (str) -- The YAML text to parse.

**Returns:** The parsed value, or `null` for an empty document.

**Raises:**

- `YAMLParseError` -- Thrown when the input cannot be parsed.

### `yaml.safe_dump(data: object?, default_flow_style: bool = false, indent: int = 2, sort_keys: bool = true, allow_unicode: bool = true, width: int = 80) -> str`

Serialize *data* to a YAML formatted string.

**Parameters:**

- `data` (object?) -- The Sharpy value to serialize.
- `default_flow_style` (bool)
- `indent` (int) -- Number of spaces per indentation level (1-9).
- `sort_keys` (bool)
- `allow_unicode` (bool)
- `width` (int) -- Preferred maximum line width before wrapping.

**Returns:** The YAML string representation of *data*.

### `yaml.safe_load_file(fp: TextFile) -> object?`

Parse the first YAML document read from a file and return the corresponding Sharpy value.

**Parameters:**

- `fp` (TextFile) -- The file to read from.

**Returns:** The parsed value, or `null` for an empty document.

**Raises:**

- `YAMLParseError` -- Thrown when the input cannot be parsed.

### `yaml.safe_dump_file(data: object?, fp: TextFile, default_flow_style: bool = false, indent: int = 2, sort_keys: bool = true, allow_unicode: bool = true, width: int = 80)`

Serialize *data* to a file as a YAML formatted document.

**Parameters:**

- `data` (object?) -- The Sharpy value to serialize.
- `fp` (TextFile) -- The file to write to.
- `default_flow_style` (bool)
- `indent` (int) -- Number of spaces per indentation level (1-9).
- `sort_keys` (bool)
- `allow_unicode` (bool)
- `width` (int) -- Preferred maximum line width before wrapping.

### `yaml.safe_load_all(text: str) -> list[object?]`

Parse all YAML documents in a multi-document stream (separated by `---`).

**Parameters:**

- `text` (str) -- The YAML text to parse.

**Returns:** A list with one entry per parsed document.

**Raises:**

- `YAMLParseError` -- Thrown when the input cannot be parsed.

### `yaml.safe_dump_all(documents: list[object?], default_flow_style: bool = false, indent: int = 2, sort_keys: bool = true, allow_unicode: bool = true, width: int = 80) -> str`

Serialize a sequence of documents into a single multi-document YAML string,
separating documents with `---`.

**Parameters:**

- `documents` (list[object?]) -- The documents to serialize.
- `default_flow_style` (bool)
- `indent` (int) -- Number of spaces per indentation level (1-9).
- `sort_keys` (bool)
- `allow_unicode` (bool)
- `width` (int) -- Preferred maximum line width before wrapping.

**Returns:** The multi-document YAML string.

### `yaml.roundtrip_load(text: str) -> object?`

Parse a YAML document preserving comments, key order, and formatting.
Mappings become `CommentedMap`, sequences become
`CommentedSeq`, and scalars are converted to their natural types.

**Parameters:**

- `text` (str) -- The YAML text to parse.

**Returns:** The parsed value with comments preserved.

**Raises:**

- `YAMLParseError` -- Thrown when the input cannot be parsed.

### `yaml.roundtrip_dump(data: object?, indent: int = 2) -> str`

Serialize data to YAML, re-emitting any comments stored in
`CommentedMap`/`CommentedSeq` nodes.

**Parameters:**

- `data` (object?) -- The data to serialize (may include commented nodes).
- `indent` (int) -- Number of spaces per indentation level.

**Returns:** The YAML string with comments preserved.

### `yaml.safe_load_typed(text: str) -> Result[T, YAMLError]`

Deserialize a YAML string into a strongly-typed object.

**Parameters:**

- `text` (str) -- The YAML text to parse.

**Returns:** A `Result{T,E}` containing the deserialized value on success,
or a `YAMLError` on failure.

## CommentedMap

An ordered, comment-aware mapping used for YAML roundtrip operations, analogous to
ruamel.yaml's `CommentedMap`. Wraps a `Dict{K, V}` internally
(composition — `Dict{K, V}` is sealed and cannot be inherited) and
tracks insertion order plus the comments associated with each key.

### Properties

| Name | Type | Description |
|------|------|-------------|
| `map` | `dict[str, object?]` | The underlying \`Dict{K, V}\` backing this mapping, exposed for serialization access. |
| `keys` | `IReadOnlyList[str]` | The keys of this mapping, in insertion order. |
| `count` | `int` | The number of key/value pairs in this mapping. |
| `comments` | `IReadOnlyDictionary[str, CommentInfo]` | The comments associated with this mapping's keys, keyed by key name. |

### `add(key: str, value: object?)`

Adds a key/value pair to the mapping, preserving insertion order.

### `remove(key: str) -> bool`

Removes the specified key (and any associated comment) from the mapping.

**Returns:** `true` if the key was present and removed; otherwise `false`.

### `set_comment(key: str, comment: CommentInfo)`

Associates the given comment information with a key, replacing any existing entry.

### `get_or_add_comment(key: str) -> CommentInfo`

Gets the comment associated with a key, or returns an existing/new mutable
`CommentInfo` so callers can attach comments incrementally.

### `get_comment(key: str) -> CommentInfo?`

Gets the comment associated with a key, or `null` if none exists.

## CommentedSeq

An ordered, comment-aware sequence used for YAML roundtrip operations, analogous to
ruamel.yaml's `CommentedSeq`. Wraps a `List{T}` internally
(composition — `List{T}` is sealed and cannot be inherited) and tracks
the comments associated with each item by index.

### Properties

| Name | Type | Description |
|------|------|-------------|
| `seq` | `list[object?]` | The underlying \`List{T}\` backing this sequence, exposed for serialization access. |
| `comments` | `IReadOnlyDictionary[int, CommentInfo]` | The comments associated with this sequence's items, keyed by item index. |

### `add(item: object?)`

Appends an item to the end of the sequence.

### `insert(index: int, item: object?)`

Inserts an item at the specified index, shifting comments after it.

### `remove_at(index: int)`

Removes the item at the specified index, shifting comments after it.

### `set_comment(index: int, comment: CommentInfo)`

Associates the given comment information with an item index, replacing any
existing entry.

### `get_or_add_comment(index: int) -> CommentInfo`

Gets the comment associated with an item index, or returns an existing/new
mutable `CommentInfo` so callers can attach comments incrementally.

### `get_comment(index: int) -> CommentInfo?`

Gets the comment associated with an item index, or `null` if none exists.

## YAMLError

Base exception for all yaml-related errors.
Mirrors Python's `yaml.YAMLError`, which subclasses `Exception`.

## YAMLParseError

Base exception for all yaml-related errors.
Mirrors Python's `yaml.YAMLError`, which subclasses `Exception`.

### Properties

| Name | Type | Description |
|------|------|-------------|
| `line` | `long` | The 1-based line number where parsing failed, or -1 if unknown. |
| `column` | `long` | The 1-based column number where parsing failed, or -1 if unknown. |
| `problem` | `str?` | A short description of the problem that caused the failure. |
| `context` | `str?` | Additional context describing where the problem occurred. |
