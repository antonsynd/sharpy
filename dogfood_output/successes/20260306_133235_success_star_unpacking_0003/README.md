# Successful Dogfood Run

**Timestamp:** 2026-03-06T13:31:28.968220
**Feature Focus:** star_unpacking
**Complexity:** simple
**Backend:** klaude

## Generated Sharpy Code

```python
# Test star unpacking with different positions and list sizes
def main():
    # Star in middle: first and last, rest in middle
    data: list[int] = [10, 20, 30, 40, 50]
    a, *middle, b = data
    print(a)
    print(len(middle))
    print(b)
    
    # Star at start: collect all but last element
    *rest, last = data
    print(len(rest))
    print(last)
    
    # Star at end: skip first, collect rest
    first, *others = data
    print(first)
    print(len(others))

```

## Output

```
10
3
50
4
50
10
4
```

## Timing

- Generation: 56.71s
- Execution: 4.55s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
