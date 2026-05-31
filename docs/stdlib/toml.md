# toml

```python
import toml
```

## Functions

### `toml.loads(s: str) -> dict[str, object?]`

### `toml.dumps(obj: object?) -> str`

### `toml.dumps(obj: object?, sort_keys: bool = false) -> str`

### `toml.load(fp: TextFile) -> dict[str, object?]`

### `toml.dump(obj: object?, fp: TextFile)`

### `toml.dump(obj: object?, fp: TextFile, sort_keys: bool = false)`

### `toml.load_file(path: str) -> dict[str, object?]`

### `toml.dump_file(obj: object?, path: str)`

### `toml.dump_file(obj: object?, path: str, sort_keys: bool = false)`

### `toml.loads(s: str) -> Result[T, TOMLDecodeError]`

### `toml.load(fp: TextFile) -> Result[T, TOMLDecodeError]`

## TOMLDecodeError

### Properties

| Name | Type | Description |
|------|------|-------------|
| `msg` | `str` |  |
| `doc` | `str` |  |
| `pos` | `int` |  |
