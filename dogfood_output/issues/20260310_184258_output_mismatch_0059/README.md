# Issue Report: output_mismatch

**Timestamp:** 2026-03-10T18:40:52.386636
**Type:** output_mismatch
**Feature Focus:** match_wildcard
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
def categorize_value(value: object) -> str:
    # Use isinstance checks before match to categorize
    if isinstance(value, int):
        n: int = value
        if n > 100:
            return "large int"
        else:
            return "small int"
    elif isinstance(value, str):
        return "some string"
    else:
        return "something else"

def main():
    print(categorize_value(150))
    print(categorize_value(42))
    print(categorize_value("hello"))
    print(categorize_value(3.14))
    print(categorize_value(True))

```

## Error

```
AI explicitly reported mismatch
```

## Output Comparison

### Expected
```
large int
small int
some string
something else
small int

```

### Actual
```
large int
small int
some string
something else
something else
```

## Timing

- Generation: 93.59s
- Execution: 5.13s
