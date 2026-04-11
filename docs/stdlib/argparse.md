# argparse

A named group of arguments for organization in help text.
Arguments still belong to the parent parser; groups are for help formatting.

```python
import argparse
```

## ArgumentGroup

A named group of arguments for organization in help text.
Arguments still belong to the parent parser; groups are for help formatting.

### `add_argument(name: str, type: str = "str", help: str = "", default_value: object? = null, nargs: str? = null, choices: list[str]? = null)`

Add a positional argument to this group.

### `add_optional_argument(long_name: str, short_name: str? = null, type: str = "str", help: str = "", default_value: object? = null, required: bool = false, action: str = "store", nargs: str? = null, choices: list[str]? = null, dest: str? = null)`

Add an optional argument to this group.

## ArgumentParser

Python-compatible command-line argument parser.

### Properties

| Name | Type | Description |
|------|------|-------------|
| `prog` | `str` | Program name for help text. |

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

### `add_subparsers(title: str = "", dest: str = "") -> SubparsersAction`

Add subparsers to this parser, allowing subcommand dispatch.

### `add_argument_group(title: str) -> ArgumentGroup`

Add a named argument group for organizational purposes.
Arguments in the group are still parsed by this parser.

### `add_mutually_exclusive_group(required: bool = false) -> MutuallyExclusiveGroup`

Add a mutually exclusive group of optional arguments.
At most one option in the group may be provided.

### `parse_args(args: list[str]) -> Namespace`

Parse command-line arguments from a Sharpy list of strings.

### `parse_args() -> Namespace`

Parse command-line arguments from Environment.GetCommandLineArgs().

### `format_help() -> str`

Format and return the help text.

### `set_output(output: TextWriter)`

Set the output writer for help text (for testing).

## MutuallyExclusiveGroup

A group of mutually exclusive optional arguments.
At most one may be provided. If required, exactly one must be provided.

### `add_optional_argument(long_name: str, short_name: str? = null, type: str = "str", help: str = "", default_value: object? = null, action: str = "store", dest: str? = null)`

Add an optional argument to this mutually exclusive group.

## Namespace

Stores parsed arguments as named values.

### `get(name: str) -> T`

Get a parsed argument value with typed conversion.

### `contains(name: str) -> bool`

Check if a named argument exists.

## SubparsersAction

Manages subparser commands for ArgumentParser.

### Properties

| Name | Type | Description |
|------|------|-------------|
| `dest` | `str` | The destination attribute name for the subcommand. |

### `add_parser(name: str, help: str = "") -> ArgumentParser`

Add a subcommand parser.

### `has_parser(name: str) -> bool`

Check if a parser with the given name exists.

### `get_parser(name: str) -> ArgumentParser`

Get the parser for the given subcommand name.
