# Successful Dogfood Run

**Timestamp:** 2026-03-10T20:15:49.669097
**Feature Focus:** event_function_style
**Complexity:** simple
**Backend:** klaude

## Generated Sharpy Code

```python
# Test function-style events with custom add/remove accessors
# Function-style events allow custom logic when handlers are added or removed
delegate LogHandler(message: str) -> None

class EventLogger:
    _handlers: list[LogHandler]
    add_count: int
    remove_count: int

    def __init__(self):
        self._handlers = []
        self.add_count = 0
        self.remove_count = 0

    event add on_log(self, handler: LogHandler):
        self._handlers.append(handler)
        self.add_count += 1

    event remove on_log(self, handler: LogHandler):
        self._handlers.remove(handler)
        self.remove_count += 1

    def log(self, msg: str) -> None:
        for h in self._handlers:
            h(msg)

def main():
    logger = EventLogger()
    
    # Subscribe two handlers
    handler1: LogHandler = lambda m: print(f"H1: {m}")
    handler2: LogHandler = lambda m: print(f"H2: {m}")
    
    logger.on_log += handler1
    logger.on_log += handler2
    print(f"Added: {logger.add_count}")
    
    # Fire event
    logger.log("hello")
    
    # Unsubscribe one handler
    logger.on_log -= handler1
    print(f"Removed: {logger.remove_count}")
    
    # Fire again
    logger.log("world")

```

## Output

```
Added: 2
H1: hello
H2: hello
Removed: 1
H2: world
```

## Timing

- Generation: 45.46s
- Execution: 5.43s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
