# subprocess

Module exports for the subprocess module.

```python
import subprocess
```

## Constants

| Name | Type | Description |
|------|------|-------------|
| `pipe` | `int` |  |
| `stdout` | `int` |  |
| `devnull` | `int` |  |

## Functions

### `subprocess.run(args: list[str], capture_output: bool = False, text: bool = True, check: bool = False, timeout: float | None = None, input: str | None = None, cwd: str | None = None, env: dict[str, str] | None = None, shell: bool = False, stdin: int = 0, stdout: int = 0, stderr: int = 0) -> CompletedProcess`

### `subprocess.check_output(args: list[str], text: bool = True, timeout: float | None = None, input: str | None = None, cwd: str | None = None, env: dict[str, str] | None = None, shell: bool = False, stderr: int = 0) -> str`

### `subprocess.check_call(args: list[str], timeout: float | None = None, cwd: str | None = None, env: dict[str, str] | None = None, shell: bool = False) -> int`

## CompletedProcess

### Properties

| Name | Type | Description |
|------|------|-------------|
| `args` | `list[str]` |  |
| `returncode` | `int` |  |
| `stdout` | `str | None` |  |
| `stderr` | `str | None` |  |

### `check_returncode()`

## SubprocessError

## CalledProcessError

### Properties

| Name | Type | Description |
|------|------|-------------|
| `returncode` | `int` |  |
| `cmd` | `list[str]` |  |
| `output` | `str | None` |  |
| `stderr` | `str | None` |  |

## TimeoutExpired

### Properties

| Name | Type | Description |
|------|------|-------------|
| `cmd` | `list[str]` |  |
| `timeout` | `float` |  |
| `output` | `str | None` |  |
| `stderr` | `str | None` |  |

## Popen

### Properties

| Name | Type | Description |
|------|------|-------------|
| `pid` | `int` |  |
| `args` | `list[str]` |  |
| `stdin` | `StreamWriter | None` |  |
| `stdout_stream` | `StreamReader | None` |  |
| `stderr_stream` | `StreamReader | None` |  |

### `wait(timeout: float | None = None) -> int`

### `poll() -> int | None`

### `kill()`

### `terminate()`

### `send_signal(signal: int)`
