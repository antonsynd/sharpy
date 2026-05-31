# logging

Flexible event logging system for applications.

```python
import logging
```

## Constants

| Name | Type | Description |
|------|------|-------------|
| `debug` | `int` | Detailed information, typically of interest only when diagnosing problems. |
| `info` | `int` | Confirmation that things are working as expected. |
| `warning` | `int` | An indication that something unexpected happened. |
| `error` | `int` | Due to a more serious problem, the software has not been able to perform some function. |
| `critical` | `int` | A serious error, indicating that the program itself may be unable to continue running. |

## Functions

### `logging.get_logger(name: str = "root") -> Logger`

Return a logger with the specified name, creating it if necessary.

**Parameters:**

- `name` (str) -- The logger name. Defaults to "root".

**Returns:** A `Logger` instance with the given name.

### `logging.basic_config(level: int = WARNING)`

Do basic configuration for the logging system by setting the root logger level.

**Parameters:**

- `level` (int) -- The logging level threshold.

### `logging.debug(msg: str)`

Log a message with DEBUG level on the root logger.

**Parameters:**

- `msg` (str) -- The message to log.

### `logging.info(msg: str)`

Log a message with INFO level on the root logger.

**Parameters:**

- `msg` (str) -- The message to log.

### `logging.warning(msg: str)`

Log a message with WARNING level on the root logger.

**Parameters:**

- `msg` (str) -- The message to log.

### `logging.error(msg: str)`

Log a message with ERROR level on the root logger.

**Parameters:**

- `msg` (str) -- The message to log.

### `logging.critical(msg: str)`

Log a message with CRITICAL level on the root logger.

**Parameters:**

- `msg` (str) -- The message to log.

## Logger

A named logger that outputs messages at or above a configured level.
Output format: LEVEL:name:message (written to stderr).

### `set_level(level: int)`

Set the minimum logging level for this logger.

**Parameters:**

- `level` (int) -- The logging level threshold.

### `debug(msg: str)`

Log a message with DEBUG level.

**Parameters:**

- `msg` (str) -- The message to log.

### `info(msg: str)`

Log a message with INFO level.

**Parameters:**

- `msg` (str) -- The message to log.

### `warning(msg: str)`

Log a message with WARNING level.

**Parameters:**

- `msg` (str) -- The message to log.

### `error(msg: str)`

Log a message with ERROR level.

**Parameters:**

- `msg` (str) -- The message to log.

### `critical(msg: str)`

Log a message with CRITICAL level.

**Parameters:**

- `msg` (str) -- The message to log.
