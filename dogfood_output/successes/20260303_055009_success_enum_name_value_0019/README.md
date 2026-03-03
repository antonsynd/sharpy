# Successful Dogfood Run

**Timestamp:** 2026-03-03T05:49:25.520717
**Feature Focus:** enum_name_value
**Complexity:** simple
**Backend:** klaude

## Generated Sharpy Code

```python
enum Priority:
    LOW = 10
    MEDIUM = 20
    HIGH = 30

def main():
    level = Priority.HIGH
    print(level.name)
    print(level.value)
    print(Priority.LOW.name)
    print(Priority.MEDIUM.value)

```

## Output

```
High
30
Low
20
```

## Timing

- Generation: 33.36s
- Execution: 4.62s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
