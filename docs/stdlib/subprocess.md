# subprocess

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

### `subprocess.run(args: list[str], capture_output: bool = false, text: bool = true, check: bool = false, timeout: float? = null, input: str? = null, cwd: str? = null, env: dict[str, str]? = null, shell: bool = false, stdin: int = 0, stdout: int = 0, stderr: int = 0) -> CompletedProcess`

### `subprocess.check_output(args: list[str], text: bool = true, timeout: float? = null, input: str? = null, cwd: str? = null, env: dict[str, str]? = null, shell: bool = false, stderr: int = 0) -> str`

### `subprocess.check_call(args: list[str], timeout: float? = null, cwd: str? = null, env: dict[str, str]? = null, shell: bool = false) -> int`

## CompletedProcess

### Properties

| Name | Type | Description |
|------|------|-------------|
| `args` | `list[str]` |  |
| `returncode` | `int` |  |
| `stdout` | `str?` |  |
| `stderr` | `str?` |  |

### `check_returncode()`

## SubprocessError

## CalledProcessError

### Properties

| Name | Type | Description |
|------|------|-------------|
| `returncode` | `int` |  |
| `cmd` | `list[str]` |  |
| `output` | `str?` |  |
| `stderr` | `str?` |  |

## TimeoutExpired

### Properties

| Name | Type | Description |
|------|------|-------------|
| `cmd` | `list[str]` |  |
| `timeout` | `float` |  |
| `output` | `str?` |  |
| `stderr` | `str?` |  |

## Popen

### Properties

| Name | Type | Description |
|------|------|-------------|
| `pid` | `int` |  |
| `args` | `list[str]` |  |
| `stdin` | `System.IO.StreamWriter?` |  |
| `stdout_stream` | `System.IO.StreamReader?` |  |
| `stderr_stream` | `System.IO.StreamReader?` |  |

### `wait(timeout: float? = null) -> int`

### `poll() -> int?`

### `kill()`

### `terminate()`

### `send_signal(signal: int)`
