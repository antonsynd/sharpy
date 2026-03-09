# Issue Report: output_mismatch

**Timestamp:** 2026-03-08T17:53:49.546281
**Type:** output_mismatch
**Feature Focus:** function_calling_function
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Function calling function with conditional pipeline
def preprocess(value: int) -> int:
    if value % 2 == 0:
        return value // 2
    return value * 3 + 1

def compute_step(input_val: int) -> int:
    intermediate: int = preprocess(input_val)
    return intermediate + 5

def finalize_result(raw: int) -> int:
    processed: int = compute_step(raw)
    if processed > 20:
        return processed - 10
    return processed + 10

def main():
    print(7)
    result: int = finalize_result(7)
    print(result)
    print(finalize_result(10))
    print(finalize_result(4))

```

## Error

```
AI explicitly reported mismatch
```

## Output Comparison

### Expected
```
7
29
15
13

```

### Actual
```
7
17
20
17
```

## Timing

- Generation: 46.52s
- Execution: 5.12s
