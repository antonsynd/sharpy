# subprocess

Subprocess management: spawn new processes, connect to their pipes, and obtain return codes.

```python
import subprocess
```

## Constants

| Name | Type | Description |
|------|------|-------------|
| `pipe` | `int` | Requests that a standard stream be redirected through a pipe. |
| `stdout` | `int` | Requests that stderr be merged into stdout. |
| `devnull` | `int` | Requests that a standard stream be redirected to the null device. |

## Functions

### `subprocess.run(args: list[str], capture_output: bool = False, text: bool = True, check: bool = False, timeout: float | None = None, input: str | None = None, cwd: str | None = None, env: dict[str, str] | None = None, shell: bool = False, stdin: int = 0, stdout: int = 0, stderr: int = 0) -> CompletedProcess`

Runs a command, waits for it to finish, and returns a CompletedProcess.

### `subprocess.check_output(args: list[str], text: bool = True, timeout: float | None = None, input: str | None = None, cwd: str | None = None, env: dict[str, str] | None = None, shell: bool = False, stderr: int = 0) -> str`

Runs a command and returns its standard output.

### `subprocess.check_call(args: list[str], timeout: float | None = None, cwd: str | None = None, env: dict[str, str] | None = None, shell: bool = False) -> int`

Runs a command and raises if it exits with a non-zero status.

## CompletedProcess

Represents the result of a finished subprocess.

### Properties

| Name | Type | Description |
|------|------|-------------|
| `args` | `list[str]` | Gets the command arguments used to launch the process. |
| `returncode` | `int` | Gets the process exit status. |
| `stdout` | `str | None` | Gets the captured standard output, if any. |
| `stderr` | `str | None` | Gets the captured standard error, if any. |

### `check_returncode()`

Raises CalledProcessError if the process exited with a non-zero status.

## SubprocessError

Base exception for subprocess-related failures.

## CalledProcessError

Base exception for subprocess-related failures.

### Properties

| Name | Type | Description |
|------|------|-------------|
| `returncode` | `int` | Gets the process exit status. |
| `cmd` | `list[str]` | Gets the command that was run. |
| `output` | `str | None` | Gets the captured standard output, if any. |
| `stderr` | `str | None` | Gets the captured standard error, if any. |

## TimeoutExpired

Base exception for subprocess-related failures.

### Properties

| Name | Type | Description |
|------|------|-------------|
| `cmd` | `list[str]` | Gets the command that timed out. |
| `timeout` | `float` | Gets the timeout value in seconds. |
| `output` | `str | None` | Gets the captured standard output, if any. |
| `stderr` | `str | None` | Gets the captured standard error, if any. |

## Popen

Starts and manages a child process like Python's subprocess.Popen.

### Properties

| Name | Type | Description |
|------|------|-------------|
| `pid` | `int` | Gets the operating system process identifier. |
| `args` | `list[str]` | Gets the command arguments used to start the process. |
| `stdin` | `StreamWriter | None` | Gets the redirected standard input writer, if available. |
| `stdout_stream` | `StreamReader | None` | Gets the redirected standard output reader, if available. |
| `stderr_stream` | `StreamReader | None` | Gets the redirected standard error reader, if available. |

### `wait(timeout: float | None = None) -> int`

Waits for the process to exit and returns its exit code.

### `poll() -> int | None`

Checks whether the process has exited without blocking.

### `kill()`

Forcefully terminates the child process.

### `terminate()`

Terminates the child process.

### `send_signal(signal: int)`

Sends a signal to the child process.
