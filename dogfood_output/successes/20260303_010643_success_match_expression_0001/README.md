# Successful Dogfood Run

**Timestamp:** 2026-03-03T01:02:50.682994
**Feature Focus:** match_expression
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Test match expression returning enum values
enum Status:
    CLEAR = 1
    WARNING = 2
    DANGER = 3

def classify_score(n: int) -> Status:
    return match n:
        case x if x < 40: Status.CLEAR
        case x if x < 80: Status.WARNING
        case _: Status.DANGER

def get_priority(s: Status) -> int:
    return match s:
        case Status.CLEAR: 1
        case Status.WARNING: 2
        case Status.DANGER: 3

def main():
    threshold: int = 45
    result: Status = classify_score(threshold)
    print(result.name)
    priority: int = get_priority(result)
    print(priority)
    print(classify_score(25).name)
    print(get_priority(Status.DANGER))

```

## Output

```
Warning
2
Clear
3
```

## Timing

- Generation: 221.42s
- Execution: 5.21s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
