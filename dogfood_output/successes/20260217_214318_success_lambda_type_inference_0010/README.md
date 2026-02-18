# Successful Dogfood Run

**Timestamp:** 2026-02-17T21:38:51.832911
**Feature Focus:** lambda_type_inference
**Complexity:** simple
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Lambda type inference with list operations and higher-order functions
# Verifies that lambda parameter types are inferred from context in function chains

def apply_transform(items: list[int], threshold: int) -> list[int]:
    # Lambda type inferred from (int) -> int context
    doubler: (int) -> int = lambda x: x * 2
    
    # Lambda type inferred from (int) -> bool context
    is_above_threshold: (int) -> bool = lambda n: n > threshold
    
    # Apply transformations using list comprehensions
    doubled: list[int] = [doubler(x) for x in items]
    result: list[int] = [x for x in doubled if is_above_threshold(x)]
    return result

def main():
    numbers: list[int] = [1, 2, 3, 4, 5]
    filtered: list[int] = apply_transform(numbers, 6)
    for val in filtered:
        print(val)

# EXPECTED OUTPUT:
# 8
# 10
```

## Output

```
8
10
```

## Timing

- Generation: 251.66s
- Execution: 4.68s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
