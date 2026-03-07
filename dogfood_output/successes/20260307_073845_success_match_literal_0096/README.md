# Successful Dogfood Run

**Timestamp:** 2026-03-07T07:36:50.908045
**Feature Focus:** match_literal
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: literal pattern matching with integers, strings, and wildcard
# Categorizes status codes and commands using if-elif-else chains
def categorize_status(code: int) -> str:
    if code == 200:
        return "OK"
    elif code == 201:
        return "Created"
    elif code == 404:
        return "Not Found"
    elif code == 500 or code == 502 or code == 503:
        return "Server Error"
    else:
        return "Unknown"

def classify_command(cmd: str) -> str:
    if cmd == "start":
        return "Starting service"
    elif cmd == "stop":
        return "Stopping service"
    elif cmd == "restart":
        return "Restarting service"
    else:
        return f"Unknown command: {cmd}"

def main():
    codes: list[int] = [200, 404, 500, 999]
    for code in codes:
        result = categorize_status(code)
        print(f"{code}: {result}")
    
    commands: list[str] = ["start", "restart", "unknown"]
    for cmd in commands:
        print(classify_command(cmd))

```

## Output

```
200: OK
404: Not Found
500: Server Error
999: Unknown
Starting service
Restarting service
Unknown command: unknown
```

## Timing

- Generation: 99.76s
- Execution: 4.69s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
