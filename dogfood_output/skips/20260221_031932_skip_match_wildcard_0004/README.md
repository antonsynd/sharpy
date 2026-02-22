# Skipped Dogfood Run

**Timestamp:** 2026-02-21T03:12:13.377260
**Skip Reason:** Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0266]: Function 'parse_command' must return a value of type 'str' in all code paths
  --> /tmp/tmppe52sb8n/dogfood_test.spy:7:1
    |
  7 | def parse_command(cmd: object) -> str:
    | ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
    |


**Feature Focus:** match_wildcard
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
def main():
    commands: list[object] = ["start", "stop", 42, "status", None]
    for cmd in commands:
        result: str = parse_command(cmd)
        print(result)

def parse_command(cmd: object) -> str:
    match cmd:
        case "start":
            return "starting system"
        case "begin":
            return "starting system"
        case "stop":
            return "stopping system"
        case "end":
            return "stopping system"
        case "status":
            return "system ok"
        case "halt":
            return "stopping system"
        case 42:
            return "status code: 42"
        case -1:
            return "error code: -1"
        case _:
            return "unknown command"

# EXPECTED OUTPUT:
# starting system
# stopping system
# status code: 42
# system ok
# unknown command
```

## Timing

- Generation: 422.77s

## Notes

This iteration was skipped because the generated code didn't pass validation.
This is typically due to the AI generating code with unsupported features
or syntax that doesn't match the Sharpy spec (phases 0.1.0-0.1.18).

This output is saved for inspection to help improve prompting.
