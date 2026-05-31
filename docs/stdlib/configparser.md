# configparser

INI-style configuration file parsing and writing, mirroring Python's `configparser`
module. Implemented as a hand-written parser (no .NET equivalent).

```python
import configparser
```

Keys are case-insensitive (lower-cased on storage, matching Python's default
`optionxform`); section names are case-sensitive. The special `DEFAULT` section
provides fallback values for every other section.

## ConfigParser

The primary class for reading, writing, and accessing configuration data.

### Constructor

`ConfigParser(interpolation: IInterpolation? = None, allow_no_value: bool = False)`

- `interpolation` -- value-substitution strategy. Defaults to `BasicInterpolation`.
- `allow_no_value` -- when `True`, keys may appear with no value (stored as `null`).

### Section management

| Method | Description |
|--------|-------------|
| `sections() -> list[str]` | All section names, excluding `DEFAULT`. |
| `add_section(section: str)` | Add an empty section. Raises `DuplicateSectionError` if it exists, `ValueError` if named `DEFAULT`. |
| `has_section(section: str) -> bool` | Whether the section exists (excludes `DEFAULT`). |
| `remove_section(section: str) -> bool` | Remove a section; returns whether it existed. |

### Option access

| Method | Description |
|--------|-------------|
| `get(section, option, fallback=None, raw=False) -> str` | Value with interpolation. Falls back to `DEFAULT`, then `fallback`, else raises `NoOptionError` / `NoSectionError`. Returns `None` for `allow_no_value` keys. |
| `get_int(section, option, fallback=None) -> int` | Parse as `int`; raises `ValueError` on failure. |
| `get_float(section, option, fallback=None) -> float` | Parse as `float`. |
| `get_boolean(section, option, fallback=None) -> bool` | `1/yes/true/on` → `True`, `0/no/false/off` → `False` (case-insensitive); else `ValueError`. |
| `set(section, option, value)` | Set a value; raises `NoSectionError` if the section is missing. |
| `has_option(section, option) -> bool` | Whether the option exists in the section or `DEFAULT`. |
| `remove_option(section, option) -> bool` | Remove an option; returns whether it existed. |
| `options(section) -> list[str]` | All option names (merged with `DEFAULT`). |
| `items(section) -> dict[str, str]` | All `(key, value)` pairs (merged with `DEFAULT`, interpolated). |
| `defaults() -> dict[str, str]` | The `DEFAULT` section's contents. |

Dict-like access is also supported: `config["section"]["key"]` returns a value via
a `SectionProxy`. Indexing a missing section raises `NoSectionError`.

### Reading and writing

| Method | Description |
|--------|-------------|
| `read_string(content, source="<string>")` | Parse INI text. |
| `read(filename)` | Read and parse a file (silently ignores a missing file, matching Python). |
| `read_dict(dictionary)` | Bulk-load from a nested dict. |
| `write(writer, space_around_delimiters=True)` | Write INI to a `TextWriter`. `DEFAULT` is written first. |
| `write_to_file(filename, space_around_delimiters=True)` | Convenience wrapper that opens a file and calls `write`. |

Parsing supports both `=` and `:` delimiters, `#` and `;` comments, multiline
continuation lines (indented), and whitespace **preservation** inside section
headers (`[ section ]` → section name `" section "`, matching Python).

## SectionProxy

A lightweight dict-like view of a single section, returned by `config[section]`.
Provides `this[key]` get/set, `contains_key(key)`, `keys()`, `items()`, and
`get(key, fallback=None)`.

## Interpolation

Value substitution is pluggable via the `IInterpolation` interface.

| Class | Syntax | Description |
|-------|--------|-------------|
| `BasicInterpolation` (default) | `%(key)s` | Substitutes from the same section or `DEFAULT`. |
| `ExtendedInterpolation` | `${section:key}` / `${key}` | Cross-section or same-section references. |

Both enforce a recursion depth limit of 10 and raise `InterpolationError` on a
cycle or on a reference to a missing key.

## Exceptions

| Class | Python name | Raised when |
|-------|-------------|-------------|
| `ConfigparserError` | `Error` | Base class for all configparser errors. |
| `NoSectionError` | `NoSectionError` | A requested section does not exist. |
| `NoOptionError` | `NoOptionError` | A requested option does not exist. |
| `DuplicateSectionError` | `DuplicateSectionError` | A section is added twice. |
| `DuplicateOptionError` | `DuplicateOptionError` | A key is duplicated within a section. |
| `ParsingError` | `ParsingError` | The INI source is malformed. |
| `MissingSectionHeaderError` | `MissingSectionHeaderError` | Data appears before the first section header. |
| `InterpolationError` | `InterpolationError` | Interpolation fails (cycle, depth limit, or missing reference). |

## Differences from Python

- `RawConfigParser` and `SafeConfigParser` are not provided. Use the `raw=True`
  argument to `get()` to skip interpolation.
- Python's finer-grained interpolation subclasses (`InterpolationDepthError`,
  `InterpolationMissingOptionError`, `InterpolationSyntaxError`) are collapsed into
  a single `InterpolationError`.
