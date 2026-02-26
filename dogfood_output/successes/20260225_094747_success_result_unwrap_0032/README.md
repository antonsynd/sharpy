# Successful Dogfood Run

**Timestamp:** 2026-02-25T09:46:35.672013
**Feature Focus:** result_unwrap
**Complexity:** simple
**Backend:** klaude

## Generated Sharpy Code

```python
def main():
    result: int !str = Ok(7)
    
    doubled: int !str = result.map(lambda x: x * 3)
    print(doubled.unwrap())
    
    failed: int !str = Err("computation failed")
    print(failed.unwrap_or(42))
    
    print(result.unwrap_or(99))

# EXPECTED OUTPUT:
# 21
# 42
# 7
```

## Output

```
21
42
7
```

## Timing

- Generation: 61.69s
- Execution: 4.49s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
