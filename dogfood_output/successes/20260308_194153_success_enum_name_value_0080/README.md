# Successful Dogfood Run

**Timestamp:** 2026-03-08T19:40:12.528278
**Feature Focus:** enum_name_value
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
enum PriorityLevel:
    LOW = 3
    MEDIUM = 5
    HIGH = 8
    CRITICAL = 13

def display_priority(p: PriorityLevel) -> str:
    name = p.name
    value = p.value
    return f"{name}={value}"

def main():
    total: int = 0
    
    for level in PriorityLevel:
        n = level.name
        v = level.value
        print(n)
        print(v)
        total += v
    
    print(total)
    
    p: PriorityLevel = PriorityLevel.HIGH
    display = display_priority(p)
    print(display)

```

## Output

```
Low
3
Medium
5
High
8
Critical
13
29
High=8
```

## Timing

- Generation: 90.51s
- Execution: 4.98s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
