# Skipped Dogfood Run

**Timestamp:** 2026-03-07T00:49:14.717665
**Skip Reason:** Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0102]: Expected newline, got Class
  --> /tmp/tmpjjzx2pkh/dogfood_test.spy:8:11
    |
  8 | @abstract class Task:
    |           ^^^^^
    |


**Feature Focus:** function_default_params
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Complex default params: inheritance, generics, nullable patterns

enum Priority:
    LOW = 1
    MEDIUM = 2
    HIGH = 3

@abstract class Task:
    priority: Priority

    def __init__(self, priority: Priority = Priority.MEDIUM):
        self.priority = priority

    @abstract
    def execute(self, retries: int = 3, delay: float = 1.0) -> bool:
        ...

    @virtual
    def describe(self, verbose: bool = False, prefix: str = "") -> str:
        if verbose:
            return f"{prefix}Task(priority={self.priority})"
        return f"{prefix}Task"

class RetryableTask(Task):
    operation: str
    max_attempts: int

    def __init__(self, operation: str = "unknown", max_attempts: int = 5):
        super().__init__(Priority.LOW)
        self.operation = operation
        self.max_attempts = max_attempts

    @override
    def execute(self, retries: int = 3, delay: float = 1.0) -> bool:
        attempts: int = 0
        while attempts < retries:
            if attempts > 0:
                print(f"retry-{attempts}")
            attempts += 1
        return attempts <= self.max_attempts

    @override
    def describe(self, verbose: bool = False, prefix: str = "") -> str:
        base: str = super().describe(verbose, prefix)
        if verbose:
            return f"{base}[op={self.operation},max={self.max_attempts}]"
        return f"{base}:{self.operation}"

def log_action(action: str, timestamp: int = 0, user: str? = None) -> str:
    if user is not None:
        return f"[{timestamp}@{user}] {action}"
    return f"[{timestamp}] {action}"

def configure_buffer(size: int = 1024, auto_flush: bool = True, name: str = "default") -> str:
    flags: list[str] = [f"size={size}"]
    if auto_flush:
        flags.append("auto")
    flags.append(f"name={name}")
    result: str = ""
    first: bool = True
    for flag in flags:
        if not first:
            result += ","
        result += flag
        first = False
    return result

def merge_configs(base: dict[str, int] = {}, overrides: dict[str, int] = {}) -> dict[str, int]:
    r: dict[str, int] = {}
    for k, v in base.items():
        r[k] = v
    for k, v in overrides.items():
        r[k] = v
    return r

def main():
    task: RetryableTask = RetryableTask("process", 10)
    task.execute()
    print(task.describe())
    detailed: str = task.describe(True, ">>")
    print(detailed)
    print(log_action("start"))
    print(log_action("login", 12345, "admin"))
    print(configure_buffer())
    cfg: dict[str, int] = merge_configs({"port": 8080, "timeout": 30}, {"port": 9090})
    print(cfg["port"])

```

## Timing

- Generation: 510.01s

## Notes

This iteration was skipped because the generated code didn't pass validation.
This is typically due to the AI generating code with unsupported features
or syntax that doesn't match the Sharpy spec (phases 0.1.0-0.2.6).

This output is saved for inspection to help improve prompting.
