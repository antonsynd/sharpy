# Successful Dogfood Run

**Timestamp:** 2026-01-29T22:06:16.478617
**Feature Focus:** collection_methods
**Complexity:** simple
**Backend:** claude

## Generated Sharpy Code

```python
# Test: Collection methods (list, dict, set operations)
# Feature: collection_methods
# Complexity: simple

def main():
    # List operations - using literals and indexing
    nums: list[int] = [1, 2, 3, 4]
    print(len(nums))
    print(nums[0])
    print(nums[3])
    
    # Dict operations - using literals and indexing
    scores: dict[str, int] = {"alice": 100, "bob": 85}
    print(len(scores))
    print(scores["bob"])
    
    # Set operations - using literals
    unique: set[int] = {1, 2, 3}
    print(len(unique))

# EXPECTED OUTPUT:
# 4
# 1
# 4
# 2
# 85
# 3
```

## Output

```
4
1
4
2
85
3
```

## Timing

- Generation: 17.99s
- Execution: 1.59s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
