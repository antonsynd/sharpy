# configparser

Configuration file parser similar to Python's configparser module.

```python
import configparser
```

## Functions

### `configparser.read_string(content: str, source: str = "<string>")`

Reads configuration data from a string.

### `configparser.read(filename: str)`

Reads configuration data from a file if it exists.

### `configparser.read_dict(dictionary: dict[str, dict[str, str]])`

Loads configuration values from nested dictionaries.

### `configparser.write(writer: TextWriter, space_around_delimiters: bool = True)`

Writes the current configuration to a text writer.

### `configparser.write_to_file(filename: str, space_around_delimiters: bool = True)`

Writes the current configuration to a file.

## ConfigParser

Parses and stores INI-style configuration data.

### `sections() -> list[str]`

Returns the non-default section names.

### `add_section(section: str)`

Adds a new section.

### `has_section(section: str) -> bool`

Determines whether a non-default section exists.

### `remove_section(section: str) -> bool`

Removes a non-default section.

### `get(section: str, option: str, fallback: str | None = None, raw: bool = False) -> str | None`

Gets an option value, optionally applying interpolation.

### `get_int(section: str, option: str, fallback: int | None = None) -> int`

Gets an option as an integer.

### `get_float(section: str, option: str, fallback: float | None = None) -> float`

Gets an option as a floating-point number.

### `get_boolean(section: str, option: str, fallback: bool | None = None) -> bool`

Gets an option as a boolean using configparser truth values.

### `set(section: str, option: str, value: str)`

Sets an option value in a section.

### `has_option(section: str, option: str) -> bool`

Determines whether a section or defaults contain an option.

### `remove_option(section: str, option: str) -> bool`

Removes an option from a section or from defaults.

### `options(section: str) -> list[str]`

Returns the option names available in a section.

### `items(section: str) -> dict[str, str]`

Returns the section items with defaults applied.

### `defaults() -> dict[str, str]`

Returns the default section values.

## Error

Represents the base exception for configparser errors.

## NoSectionError

Represents the base exception for configparser errors.

### Properties

| Name | Type | Description |
|------|------|-------------|
| `section` | `str` | Gets the missing section name. |

## NoOptionError

Represents the base exception for configparser errors.

### Properties

| Name | Type | Description |
|------|------|-------------|
| `option` | `str` | Gets the missing option name. |
| `section` | `str` | Gets the section containing the missing option. |

## DuplicateSectionError

Represents the base exception for configparser errors.

### Properties

| Name | Type | Description |
|------|------|-------------|
| `section` | `str` | Gets the duplicate section name. |

## DuplicateOptionError

Represents the base exception for configparser errors.

### Properties

| Name | Type | Description |
|------|------|-------------|
| `section` | `str` | Gets the section containing the duplicate option. |
| `option` | `str` | Gets the duplicate option name. |

## ParsingError

Represents the base exception for configparser errors.

### Properties

| Name | Type | Description |
|------|------|-------------|
| `source` | `str | None` | Gets the source being parsed, if available. |
| `line_number` | `int` | Gets the line number associated with the parse error. |

## MissingSectionHeaderError

Represents the base exception for configparser errors.

### Properties

| Name | Type | Description |
|------|------|-------------|
| `filename` | `str` | Gets the filename or source name. |
| `lineno` | `int` | Gets the line number with the missing header. |
| `line` | `str` | Gets the offending line text. |

## InterpolationError

Represents the base exception for configparser errors.

### Properties

| Name | Type | Description |
|------|------|-------------|
| `section` | `str` | Gets the section containing the interpolation error. |
| `option` | `str` | Gets the option containing the interpolation error. |
| `raw_value` | `str` | Gets the raw value that failed to interpolate. |

## InterpolationDepthError

Represents the base exception for configparser errors.

## InterpolationMissingOptionError

Represents the base exception for configparser errors.

### Properties

| Name | Type | Description |
|------|------|-------------|
| `reference` | `str` |  |

## InterpolationSyntaxError

Represents the base exception for configparser errors.

## BasicInterpolation

Implements configparser.BasicInterpolation using %(name)s substitutions.

### `before_get(parser: ConfigParser, section: str, option: str, raw_value: str) -> str`

Interpolates %(name)s references in an option value.

### `before_set(parser: ConfigParser, section: str, option: str, value: str) -> str`

Returns the value unchanged before storing it.

## ExtendedInterpolation

Implements configparser.BasicInterpolation using %(name)s substitutions.

### `before_get(parser: ConfigParser, section: str, option: str, raw_value: str) -> str`

Interpolates ${section:option} references in an option value.

### `before_set(parser: ConfigParser, section: str, option: str, value: str) -> str`

Returns the value unchanged before storing it.

## SectionProxy

Provides mapping-style access to a single configparser section.

### `get(key: str, fallback: str | None = None) -> str | None`

Gets an option from the section, returning the fallback when it is missing.

### `keys() -> list[str]`

Returns the option names available in the section.

### `items() -> dict[str, str]`

Returns the section items with defaults merged in.
