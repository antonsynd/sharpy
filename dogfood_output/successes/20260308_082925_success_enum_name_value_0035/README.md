# Successful Dogfood Run

**Timestamp:** 2026-03-08T08:28:04.420169
**Feature Focus:** enum_name_value
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
enum Priority:
    LOW = 10
    MEDIUM = 20
    HIGH = 30
    CRITICAL = 100

def describe_priority(p: Priority) -> str:
    return f"{p.name} (score: {p.value})"

def should_alert(p: Priority) -> bool:
    return p.value >= 30

def main():
    p1: Priority = Priority.MEDIUM
    p2: Priority = Priority.HIGH
    
    print(describe_priority(p1))
    print(describe_priority(p2))
    
    for p in Priority:
        if should_alert(p):
            print(f"ALERT: {p.name}")
        else:
            print(f"OK: {p.name}")

```

## Output

```
Medium (score: 20)
High (score: 30)
OK: Low
OK: Medium
ALERT: High
ALERT: Critical
```

## Timing

- Generation: 70.43s
- Execution: 5.04s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
