# json

Python-compatible json module.
Provides dumps/loads for string serialization and dump/load for file I/O.

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

### `json.dumps(obj: object?) -> str`

Serialize obj to a JSON formatted string.

**Parameters:**

- `obj` (object?) -- The object to serialize.

**Returns:** A JSON string representation of *obj*.

```python
json.dumps({"key": "value"})    # '{"key": "value"}'
json.dumps([1, 2, 3])           # '[1, 2, 3]'
```

### `json.dumps(obj: object?, indent: int = -1, sort_keys: bool = false, ensure_ascii: bool = true, (string, string) -> str`

Serialize obj to a JSON formatted string with formatting options.

**Parameters:**

- `obj` (object?) -- The object to serialize.
- `indent` (int) -- Number of spaces for indentation. Use -1 for compact output.
- `sort_keys` (bool)
- `ensure_ascii` (bool)
- `(string` ()
- `string` ()

**Returns:** A JSON string representation of *obj*.

### `json.loads(s: str, cls: JSONDecoder? = null, object_hook: Func[dict[str, object?], object?]? = null) -> object?`

Deserialize a JSON string to a Python-like object.
Returns Dict for objects, List for arrays,
string, int/long/double, bool, or null.

**Parameters:**

- `s` (str) -- The JSON string to deserialize.
- `cls` (JSONDecoder?) -- Optional `JSONDecoder` instance for custom decoding.
- `object_hook` (Func[dict[str, object?], object?]?)

**Returns:** The deserialized object.

```python
json.loads('{"a": 1}')    # {"a": 1}
json.loads('[1, 2]')      # [1, 2]
```

### `json.dump(obj: object?, fp: TextFile)`

Serialize obj as a JSON formatted stream to a file.

**Parameters:**

- `obj` (object?) -- The object to serialize.
- `fp` (TextFile) -- The file to write to.

```python
f = open("data.json", "w")
json.dump({"key": "value"}, f)
f.close()
```

### `json.dump(obj: object?, fp: TextFile, indent: int = -1, sort_keys: bool = false, ensure_ascii: bool = true, (string, string)`

Serialize obj as a JSON formatted stream to a file with formatting options.

**Parameters:**

- `obj` (object?) -- The object to serialize.
- `fp` (TextFile) -- The file to write to.
- `indent` (int) -- Number of spaces for indentation. Use -1 for compact output.
- `sort_keys` (bool)
- `ensure_ascii` (bool)
- `(string` ()
- `string` ()

### `json.load(fp: TextFile, cls: JSONDecoder? = null, object_hook: Func[dict[str, object?], object?]? = null) -> object?`

Deserialize a JSON document read from a file.

**Parameters:**

- `fp` (TextFile) -- The file to read from.
- `cls` (JSONDecoder?) -- Optional `JSONDecoder` instance for custom decoding.
- `object_hook` (Func[dict[str, object?], object?]?)

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

### `decode(s: str) -> object?`

## JSONEncoder

### `default(obj: object) -> object?`

### `encode(obj: object?) -> str`
