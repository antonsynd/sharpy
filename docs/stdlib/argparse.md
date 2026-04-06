# argparse

Python-compatible command-line argument parser.

```python
import argparse
```

## Functions

### `argparse.get(name: str) -> T`

Get a parsed argument value with typed conversion.

### `argparse.contains(name: str) -> bool`

Check if a named argument exists.

## argparse

Python-compatible command-line argument parser.

### `add_argument(name: str, type: str = "str", help: str = "", default_value: object? = null, nargs: str? = null, choices: list[str]? = null)`

Add a positional argument.

**Parameters:**

- `name` (str) -- The argument name.
- `type` (str) -- Value type: "str", "int", or "float".
- `help` (str) -- Help text for this argument.
- `default_value` (object?)
- `nargs` (str?) -- Number of arguments: "*", "+", or "?".
- `choices` (list[str]?) -- Restrict values to this set.

```python
parser = ArgumentParser(description="My tool")
parser.add_argument("filename", help="input file")
```

### `add_optional_argument(long_name: str, short_name: str? = null, type: str = "str", help: str = "", default_value: object? = null, required: bool = false, action: str = "store", nargs: str? = null, choices: list[str]? = null, dest: str? = null)`

Add an optional argument with long name only (e.g., "--verbose").

### `parse_args(args: list[str]) -> Namespace`

Parse command-line arguments from the given string array.

### `parse_args() -> Namespace`

Parse command-line arguments from Environment.GetCommandLineArgs().

### `format_help() -> str`

Format and return the help text.

### `set_output(output: TextWriter)`

Set the output writer for help text (for testing).
