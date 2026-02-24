# Successful Dogfood Run

**Timestamp:** 2026-02-24T01:13:48.248861
**Feature Focus:** builtin_aggregation
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
def main():
    data: list[int] = [-5, 10, -3, 8, 0, 12]
    
    total: int = 0
    for x in data:
        total = total + x
    
    smallest: int = data[0]
    for x in data:
        if x < smallest:
            smallest = x
    
    largest: int = data[0]
    for x in data:
        if x > largest:
            largest = x
    
    range_val: int = largest - smallest
    
    all_non_zero: bool = False
    for x in data:
        if x == 0:
            all_non_zero = True
    
    any_negative: bool = False
    for x in data:
        if x < 0:
            any_negative = True
    
    print(total)
    print(range_val)
    print(smallest)
    print(largest)
    print(any_negative)
    print(all_non_zero)

# EXPECTED OUTPUT:
# 22
# 17
# -5
# 12
# True
# True
```

## Output

```
22
17
-5
12
True
True
```

## Timing

- Generation: 273.29s
- Execution: 4.59s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
