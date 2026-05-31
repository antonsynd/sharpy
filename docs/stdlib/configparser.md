# configparser

```python
import configparser
```

## Functions

### `configparser.read_string(content: str, source: str = "<string>")`

### `configparser.read(filename: str)`

### `configparser.read_dict(dictionary: SCG.Dictionary[str, SCG.Dictionary[str, str]])`

### `configparser.write(writer: TextWriter, space_around_delimiters: bool = true)`

### `configparser.write_to_file(filename: str, space_around_delimiters: bool = true)`

## ConfigParser

### `sections() -> SCG.List[str]`

### `add_section(section: str)`

### `has_section(section: str) -> bool`

### `remove_section(section: str) -> bool`

### `get(section: str, option: str, fallback: str? = null, raw: bool = false) -> str?`

### `get_int(section: str, option: str, fallback: int? = null) -> int`

### `get_float(section: str, option: str, fallback: float? = null) -> float`

### `get_boolean(section: str, option: str, fallback: bool? = null) -> bool`

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
| `source` | `str?` |  |
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

## BasicInterpolation

### `before_get(parser: ConfigParser, section: str, option: str, raw_value: str) -> str`

### `before_set(parser: ConfigParser, section: str, option: str, value: str) -> str`

## ExtendedInterpolation

### `before_get(parser: ConfigParser, section: str, option: str, raw_value: str) -> str`

### `before_set(parser: ConfigParser, section: str, option: str, value: str) -> str`

## SectionProxy

### `get(key: str, fallback: str? = null) -> str?`

### `keys() -> SCG.List[str]`

### `items() -> SCG.Dictionary[str, str]`
