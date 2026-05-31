# configparser

Configuration file parser similar to Python's configparser module.

```python
import configparser
```

## Functions

### `configparser.read_string(content: str, source: str = "<string>")`

### `configparser.read(filename: str)`

### `configparser.read_dict(dictionary: SCG.Dictionary[str, SCG.Dictionary[str, str]])`

### `configparser.write(writer: TextWriter, space_around_delimiters: bool = True)`

### `configparser.write_to_file(filename: str, space_around_delimiters: bool = True)`

## ConfigParser

### `sections() -> SCG.List[str]`

### `add_section(section: str)`

### `has_section(section: str) -> bool`

### `remove_section(section: str) -> bool`

### `get(section: str, option: str, fallback: str | None = None, raw: bool = False) -> str | None`

### `get_int(section: str, option: str, fallback: int | None = None) -> int`

### `get_float(section: str, option: str, fallback: float | None = None) -> float`

### `get_boolean(section: str, option: str, fallback: bool | None = None) -> bool`

### `set(section: str, option: str, value: str)`

### `has_option(section: str, option: str) -> bool`

### `remove_option(section: str, option: str) -> bool`

### `options(section: str) -> SCG.List[str]`

### `items(section: str) -> SCG.Dictionary[str, str]`

### `defaults() -> SCG.Dictionary[str, str]`

## Error

## NoSectionError

### Properties

| Name | Type | Description |
|------|------|-------------|
| `section` | `str` |  |

## NoOptionError

### Properties

| Name | Type | Description |
|------|------|-------------|
| `option` | `str` |  |
| `section` | `str` |  |

## DuplicateSectionError

### Properties

| Name | Type | Description |
|------|------|-------------|
| `section` | `str` |  |

## DuplicateOptionError

### Properties

| Name | Type | Description |
|------|------|-------------|
| `section` | `str` |  |
| `option` | `str` |  |

## ParsingError

### Properties

| Name | Type | Description |
|------|------|-------------|
| `source` | `str | None` |  |
| `line_number` | `int` |  |

## MissingSectionHeaderError

### Properties

| Name | Type | Description |
|------|------|-------------|
| `filename` | `str` |  |
| `lineno` | `int` |  |
| `line` | `str` |  |

## InterpolationError

### Properties

| Name | Type | Description |
|------|------|-------------|
| `section` | `str` |  |
| `option` | `str` |  |
| `raw_value` | `str` |  |

## InterpolationDepthError

## InterpolationMissingOptionError

### Properties

| Name | Type | Description |
|------|------|-------------|
| `reference` | `str` |  |

## InterpolationSyntaxError

## BasicInterpolation

### `before_get(parser: ConfigParser, section: str, option: str, raw_value: str) -> str`

### `before_set(parser: ConfigParser, section: str, option: str, value: str) -> str`

## ExtendedInterpolation

### `before_get(parser: ConfigParser, section: str, option: str, raw_value: str) -> str`

### `before_set(parser: ConfigParser, section: str, option: str, value: str) -> str`

## SectionProxy

### `get(key: str, fallback: str | None = None) -> str | None`

### `keys() -> SCG.List[str]`

### `items() -> SCG.Dictionary[str, str]`
