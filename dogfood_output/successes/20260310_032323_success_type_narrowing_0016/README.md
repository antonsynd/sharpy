# Successful Dogfood Run

**Timestamp:** 2026-03-10T03:21:44.331544
**Feature Focus:** type_narrowing
**Complexity:** simple
**Backend:** klaude

## Generated Sharpy Code

```python
# Test type narrowing: after `is not None`, optional becomes the underlying type
def main():
    score: int? = Some(100)
    
    # In the else branch, score is narrowed from int? to int
    if score is None:
        print(0)
    else:
        # Can use score directly without .unwrap() after narrowing
        result = score + 50
        print(result)

```

## Output

```
150
```

## Timing

- Generation: 87.91s
- Execution: 4.85s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
