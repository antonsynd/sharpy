# Issue Report: output_mismatch

**Timestamp:** 2026-03-10T11:27:44.589117
**Type:** output_mismatch
**Feature Focus:** match_type_pattern
**Complexity:** simple
**Backend:** klaude

## Generated Sharpy Code

```python
def scale_value(item: object) -> int:
    if isinstance(item, int):
        n: int = item
        return n * 5
    elif isinstance(item, str):
        s: str = item
        return len(s) * 2
    else:
        return 0

def main():
    print(scale_value(7))
    print(scale_value("hi"))
    print(scale_value(True))

```

## Error

```
AI explicitly reported mismatch
```

## Output Comparison

### Expected
```
35
4
5
```

### Actual
```
35
4
0
```

## Timing

- Generation: 203.04s
- Execution: 4.93s
