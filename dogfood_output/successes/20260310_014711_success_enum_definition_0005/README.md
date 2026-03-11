# Successful Dogfood Run

**Timestamp:** 2026-03-10T01:46:11.242267
**Feature Focus:** enum_definition
**Complexity:** simple
**Backend:** klaude

## Generated Sharpy Code

```python
# Test enum definition with function parameter and match
enum Priority:
    LOW = 10
    MEDIUM = 20
    HIGH = 30

def priority_name(p: Priority) -> str:
    match p:
        case Priority.LOW:
            return "low"
        case Priority.MEDIUM:
            return "medium"
        case Priority.HIGH:
            return "high"
        case _:
            return "unknown"

def main():
    task_priority = Priority.MEDIUM
    print(task_priority.value)
    print(priority_name(task_priority))
    
    urgent = Priority.HIGH
    print(urgent.value)
    print(priority_name(urgent))

```

## Output

```
20
medium
30
high
```

## Timing

- Generation: 48.84s
- Execution: 5.15s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
