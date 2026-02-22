# Successful Dogfood Run

**Timestamp:** 2026-02-21T05:30:14.050687
**Feature Focus:** enum_usage
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Enum usage in class field and method dispatch
# Demonstrates enum state machine with class encapsulation

enum LogLevel:
    DEBUG = 0
    INFO = 1
    WARNING = 2
    ERROR = 3

class Logger:
    level: LogLevel

    def __init__(self, level: LogLevel):
        self.level = level

    def should_log(self, msg_level: LogLevel) -> bool:
        return msg_level >= self.level

    def log(self, message: str, msg_level: LogLevel) -> None:
        if self.should_log(msg_level):
            print(message)

def get_level_name(level: LogLevel) -> str:
    if level == LogLevel.DEBUG:
        return "Debug"
    elif level == LogLevel.INFO:
        return "Info"
    elif level == LogLevel.WARNING:
        return "Warning"
    elif level == LogLevel.ERROR:
        return "Error"
    else:
        return "Unknown"

def main():
    logger: Logger = Logger(LogLevel.INFO)
    print(get_level_name(LogLevel.INFO))
    logger.log("Debug message", LogLevel.DEBUG)
    logger.log("Info message", LogLevel.INFO)
    logger.log("Error message", LogLevel.ERROR)
    logger.level = LogLevel.ERROR
    logger.log("Another info", LogLevel.INFO)

# EXPECTED OUTPUT:
# Info
# Info message
# Error message
```

## Output

```
Info
Info message
Error message
```

## Timing

- Generation: 85.18s
- Execution: 4.97s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
