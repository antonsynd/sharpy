# Successful Dogfood Run

**Timestamp:** 2026-02-21T00:55:41.467738
**Feature Focus:** while_loop
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
def simulate_guesses(target: int) -> int:
    low: int = 1
    high: int = 100
    attempts: int = 0
    
    while low <= high:
        mid: int = (low + high) // 2
        attempts = attempts + 1
        
        if mid < target:
            low = mid + 1
        elif mid > target:
            high = mid - 1
        else:
            break
    
    return attempts

def main():
    # Test different target values
    targets: list[int] = [1, 50, 99, 73]
    
    for t in targets:
        count = simulate_guesses(t)
        print(f"Found {t} in {count} attempts")

# EXPECTED OUTPUT:
# Found 1 in 6 attempts
# Found 50 in 1 attempts
# Found 99 in 6 attempts
# Found 73 in 6 attempts

```

## Output

```
Found 1 in 6 attempts
Found 50 in 1 attempts
Found 99 in 6 attempts
Found 73 in 6 attempts
```

## Timing

- Generation: 48.52s
- Execution: 5.09s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
