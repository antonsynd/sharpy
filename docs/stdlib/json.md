# json

JSON encoder and decoder.

```python
import json
```

## Properties

| Name | Type | Description |
|------|------|-------------|
| `msg` | `str` | The unformatted error message. |
| `doc` | `str` | The JSON document being parsed. |
| `pos` | `int` | The index in doc where parsing failed. |

## Functions

### `json.dumps(obj: object | None) -> str`

Serialize obj to a JSON formatted string.

**Parameters:**

- `obj` (object | None) -- The object to serialize.

**Returns:** A JSON string representation of *obj*.

```python
json.dumps({"key": "value"})    # '{"key": "value"}'
json.dumps([1, 2, 3])           # '[1, 2, 3]'
```

### `json.dumps(obj: object | None, indent: int = -1, sort_keys: bool = False, ensure_ascii: bool = True, separators: tuple[str, str] | None = None, @default: (object) -> object | None | None = None, cls: JSONEncoder | None = None) -> str`

Serialize obj to a JSON formatted string with formatting options.

**Parameters:**

- `obj` (object | None) -- The object to serialize.
- `indent` (int) -- Number of spaces for indentation. Use -1 for compact output.
- `sort_keys` (bool)
- `ensure_ascii` (bool)
- `separators` (tuple[str, str] | None) -- A tuple of `(itemSeparator, keySeparator)` overriding the
defaults. When `None`, defaults to `(", ", ": ")` in compact mode and
`(",", ": ")`-style behavior in pretty mode (newlines drive item separation).
- `@default` ((object) -> object | None | None)
- `cls` (JSONEncoder | None) -- Optional `JSONEncoder` instance. When provided,
delegates serialization to `cls.Encode(obj)`.

**Returns:** A JSON string representation of *obj*.

### `json.loads(s: str, cls: JSONDecoder | None = None, object_hook: (dict[str, object | None]) -> object | None | None = None) -> object | None`

Deserialize a JSON string to a Python-like object.
Returns Dict for objects, List for arrays,
string, int/long/double, bool, or None.

**Parameters:**

- `s` (str) -- The JSON string to deserialize.
- `cls` (JSONDecoder | None) -- Optional `JSONDecoder` instance for custom decoding.
- `object_hook` ((dict[str, object | None]) -> object | None | None)

**Returns:** The deserialized object.

```python
json.loads('{"a": 1}')    # {"a": 1}
json.loads('[1, 2]')      # [1, 2]
```

### `json.dump(obj: object | None, fp: TextFile)`

Serialize obj as a JSON formatted stream to a file.

**Parameters:**

- `obj` (object | None) -- The object to serialize.
- `fp` (TextFile) -- The file to write to.

```python
f = open("data.json", "w")
json.dump({"key": "value"}, f)
f.close()
```

### `json.dump(obj: object | None, fp: TextFile, indent: int = -1, sort_keys: bool = False, ensure_ascii: bool = True, separators: tuple[str, str] | None = None, @default: (object) -> object | None | None = None, cls: JSONEncoder | None = None)`

Serialize obj as a JSON formatted stream to a file with formatting options.

**Parameters:**

- `obj` (object | None) -- The object to serialize.
- `fp` (TextFile) -- The file to write to.
- `indent` (int) -- Number of spaces for indentation. Use -1 for compact output.
- `sort_keys` (bool)
- `ensure_ascii` (bool)
- `separators` (tuple[str, str] | None) -- A tuple of `(itemSeparator, keySeparator)` overriding the
defaults. See `Dumps(object?, int, bool, bool, ValueTuple{string, string}?, Func{object, object?}?)`.
- `@default` ((object) -> object | None | None)
- `cls` (JSONEncoder | None) -- Optional `JSONEncoder` instance for custom encoding.

### `json.load(fp: TextFile, cls: JSONDecoder | None = None, object_hook: (dict[str, object | None]) -> object | None | None = None) -> object | None`

Deserialize a JSON document read from a file.

**Parameters:**

- `fp` (TextFile) -- The file to read from.
- `cls` (JSONDecoder | None) -- Optional `JSONDecoder` instance for custom decoding.
- `object_hook` ((dict[str, object | None]) -> object | None | None)

**Returns:** The deserialized object.

### `json.loads(s: str) -> Result[T, JSONDecodeError]`

Deserialize a JSON string to a strongly-typed object using `System.Text.Json`.

**Parameters:**

- `s` (str) -- The JSON string to deserialize.

**Returns:** A `Result{T,E}` containing the deserialized value on success,
or a `JSONDecodeError` on failure.

### `json.load(fp: TextFile) -> Result[T, JSONDecodeError]`

Deserialize a JSON document read from a file to a strongly-typed object.

**Parameters:**

- `fp` (TextFile) -- The file to read from.

**Returns:** A `Result{T,E}` containing the deserialized value on success,
or a `JSONDecodeError` on failure.

## JSONDecoder

Simple JSON decoder. Subclass this to customize JSON decoding behavior.

### `decode(s: str) -> object | None`

Deserialize a JSON string to a Python-like object.

**Parameters:**

- `s` (str) -- The JSON string to deserialize.

**Returns:** The deserialized object.

## JSONEncoder

Extensible JSON encoder. Subclass this to customize JSON encoding behavior.

### `default(obj: object) -> object | None`

Called for objects that cannot otherwise be serialized. Override to provide custom serialization.

**Parameters:**

- `obj` (object) -- The object that is not JSON serializable.

**Returns:** A JSON-serializable replacement object.

### `encode(obj: object | None) -> str`

Serialize an object to a JSON formatted string.

**Parameters:**

- `obj` (object | None) -- The object to serialize.

**Returns:** A JSON string representation of the object.
