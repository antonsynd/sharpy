# Successful Dogfood Run

**Timestamp:** 2026-02-25T06:47:38.636453
**Feature Focus:** match_wildcard
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
def classify_value(value: int) -> str:
    if value == 0:
        return "zero"
    elif value == 1 or value == 2 or value == 3:
        return "small positive"
    elif value < 0:
        return f"negative: {value}"
    elif value > 100:
        return f"large: {value}"
    else:
        return f"medium: {value}"

def handle_command(cmd: str) -> str:
    lower_cmd = cmd.lower()
    if lower_cmd == "quit" or lower_cmd == "exit":
        return "goodbye"
    elif lower_cmd == "help":
        return "available commands: help, status, quit"
    elif lower_cmd == "status":
        return "system operational"
    else:
        return f"unknown command '{cmd}'"

def main():
    values: list[int] = [0, -5, 2, 50, 150]
    commands: list[str] = ["help", "STATUS", "quit", "unknown"]
    for v in values:
        print(classify_value(v))
    for c in commands:
        print(handle_command(c))

# EXPECTED OUTPUT:
# zero
# negative: -5
# small positive
# medium: 50
# large: 150
# available commands: help, status, quit
# system operational
# goodbye
# unknown command 'unknown'
```

## Output

```
zero
negative: -5
small positive
medium: 50
large: 150
available commands: help, status, quit
system operational
goodbye
unknown command 'unknown'
```

## Timing

- Generation: 197.71s
- Execution: 4.54s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
