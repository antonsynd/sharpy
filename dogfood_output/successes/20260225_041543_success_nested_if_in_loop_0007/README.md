# Successful Dogfood Run

**Timestamp:** 2026-02-25T04:09:45.518198
**Feature Focus:** nested_if_in_loop
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
def main():
    tier1: int = 0  # divisible by 2, 3, and 4
    tier2: int = 0  # divisible by 2 and 3 only
    tier3: int = 0  # divisible by 2 only
    other: int = 0
    
    for n in range(2, 25):
        if n % 2 == 0:
            if n % 3 == 0:
                if n % 4 == 0:
                    tier1 += 1
                else:
                    tier2 += 1
            else:
                tier3 += 1
        else:
            other += 1
    
    print(tier1)
    print(tier2)
    print(tier3)
    print(other)
# EXPECTED OUTPUT:
# 2
# 2
# 8
# 11
```

## Output

```
2
2
8
11
```

## Timing

- Generation: 348.27s
- Execution: 4.39s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
