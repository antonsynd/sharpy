# shlex

Simple lexical analysis of shell-style syntaxes.

```python
import shlex
```

## Functions

### `shlex.split(s: str, comments: bool = False, posix: bool = True) -> list[str]`

Split a shell-like string into tokens using POSIX shlex rules.

### `shlex.quote(s: str) -> str`

Return a shell-escaped version of a string.

### `shlex.join(split_command: list[str]) -> str`

Join shell tokens into a command string using shell escaping.
