# Issue Report: output_mismatch

**Timestamp:** 2026-03-10T03:24:28.044913
**Type:** output_mismatch
**Feature Focus:** spread_with_comprehension
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
def main():
    # Create lists using loops instead of comprehensions
    doubles: list[int] = []
    for x in range(5):
        doubles.append(x * 2)
    
    triples: list[int] = []
    for x in range(4):
        triples.append(x * 3)
    
    # Spread lists into a new list
    combined: list[int] = [*doubles, 100, *triples]
    print(combined)
    
    # Create dict using loops instead of comprehensions
    squares: dict[str, int] = {}
    for x in range(4):
        squares[str(x)] = x * x
    
    cubes: dict[str, int] = {}
    for x in range(3):
        cubes[str(x)] = x * x * x
    
    # Spread dicts together
    merged: dict[str, int] = {**squares, **cubes}
    print(merged)
    
    # Spread range into list - FIX: build list from range using loop
    ranged: list[int] = []
    for x in range(3):
        ranged.append(x)
    print(ranged)
    
    # Spread with filtered values - build filtered list with loop
    filtered: list[int] = []
    for x in combined:
        if x > 5:
            filtered.append(x)
    result: list[int] = [*filtered, 999]
    print(result)

```

## Error

```
AI explicitly reported mismatch
```

## Output Comparison

### Expected
```
[0, 2, 4, 6, 8, 100, 0, 3, 6, 9]
{"0": 0, "1": 1, "2": 8, "3": 9}
[0, 1, 2]
[6, 8, 9, 999]

```

### Actual
```
[0, 2, 4, 6, 8, 100, 0, 3, 6, 9]
{0: 0, 1: 1, 2: 8, 3: 9}
[0, 1, 2]
[6, 8, 100, 6, 9, 999]
```

## Timing

- Generation: 551.30s
- Execution: 5.29s
