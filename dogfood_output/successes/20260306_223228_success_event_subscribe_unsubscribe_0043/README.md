# Successful Dogfood Run

**Timestamp:** 2026-03-06T22:30:24.948897
**Feature Focus:** event_subscribe_unsubscribe
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
delegate LogHandler(msg: str) -> None

class EventLogger:
    event on_log: LogHandler
    _name: str
    
    def __init__(self, name: str):
        self._name = name
    
    def log(self, msg: str) -> None:
        full = f"[{self._name}] {msg}"
        self.on_log?.invoke(full)

def to_console(msg: str) -> None:
    print(f"CONSOLE: {msg}")

def to_file(msg: str) -> None:
    print(f"FILE: {msg}")

def main():
    logger = EventLogger("App")
    
    # Subscribe console handler only
    logger.on_log += to_console
    logger.log("Starting")
    
    # Subscribe file handler (now both active)
    logger.on_log += to_file
    logger.log("Processing")
    
    # Unsubscribe console handler (only file remains)
    logger.on_log -= to_console
    logger.log("Warning")
    
    # Unsubscribe file handler (no handlers remain)
    logger.on_log -= to_file
    logger.log("Silent")

```

## Output

```
CONSOLE: [App] Starting
CONSOLE: [App] Processing
FILE: [App] Processing
FILE: [App] Warning
```

## Timing

- Generation: 111.35s
- Execution: 5.70s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
