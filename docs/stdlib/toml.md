# toml

TOML configuration file parser and encoder.

```python
import toml
```

## Functions

### `toml.loads(s: str) -> dict[str, object | None]`

### `toml.dumps(obj: object | None) -> str`

### `toml.dumps(obj: object | None, sort_keys: bool = False) -> str`

### `toml.load(fp: TextFile) -> dict[str, object | None]`

### `toml.dump(obj: object | None, fp: TextFile)`

### `toml.dump(obj: object | None, fp: TextFile, sort_keys: bool = False)`

### `toml.load_file(path: str) -> dict[str, object | None]`

### `toml.dump_file(obj: object | None, path: str)`

### `toml.dump_file(obj: object | None, path: str, sort_keys: bool = False)`

### `toml.loads(new(: string s) where T : class,) -> Result[T, TOMLDecodeError]`

### `toml.load(new(: TextFile fp) where T : class,) -> Result[T, TOMLDecodeError]`

## TOMLDecodeError

### Properties

| Name | Type | Description |
|------|------|-------------|
| `msg` | `str` |  |
| `doc` | `str` |  |
| `pos` | `int` |  |
