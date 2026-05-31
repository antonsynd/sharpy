# toml

TOML configuration file parser and encoder.

```python
import toml
```

## Functions

### `toml.loads(s: str) -> dict[str, object | None]`

Parse a TOML string into a dictionary.

### `toml.dumps(obj: object | None) -> str`

Serialize an object to a TOML string.

### `toml.dumps(obj: object | None, sort_keys: bool = False) -> str`

Serialize an object to a TOML string, optionally sorting keys.

### `toml.load(fp: TextFile) -> dict[str, object | None]`

Parse TOML content from an open text file.

### `toml.dump(obj: object | None, fp: TextFile)`

Write an object's TOML representation to a text file.

### `toml.dump(obj: object | None, fp: TextFile, sort_keys: bool = False)`

Write an object's TOML representation to a text file, optionally sorting keys.

### `toml.load_file(path: str) -> dict[str, object | None]`

Parse a TOML file from a path.

### `toml.dump_file(obj: object | None, path: str)`

Write an object's TOML representation to a file path.

### `toml.dump_file(obj: object | None, path: str, sort_keys: bool = False)`

Write an object's TOML representation to a file path, optionally sorting keys.

### `toml.loads(new(: string s) where T : class,) -> Result[T, TOMLDecodeError]`

Parse a TOML string into a typed model.

### `toml.load(new(: TextFile fp) where T : class,) -> Result[T, TOMLDecodeError]`

Parse TOML content from a text file into a typed model.

## TOMLDecodeError

Represents a TOML decoding error.

### Properties

| Name | Type | Description |
|------|------|-------------|
| `msg` | `str` | Get the original error message. |
| `doc` | `str` | Get the TOML document that failed to decode. |
| `pos` | `int` | Get the zero-based error position in the document. |
