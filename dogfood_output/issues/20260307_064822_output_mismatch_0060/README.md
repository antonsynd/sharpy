# Issue Report: output_mismatch

**Timestamp:** 2026-03-07T06:43:50.358545
**Type:** output_mismatch
**Feature Focus:** star_unpacking
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Star unpacking demonstration with proper Sharpy semantics
# Tests: spread operators, rest patterns, and collection merging

def sum_variadic(base: int, extras: list[int]) -> int:
    total: int = base
    for val in extras:
        total += val
    return total

def merge_lists(lists: list[list[int]]) -> list[int]:
    result: list[int] = []
    for lst in lists:
        result.extend(lst)
    return result

def process_with_defaults(items: list[int], defaults: list[int]) -> int:
    if len(items) > 0:
        return items[0]
    elif len(defaults) > 0:
        # Get first default without rest unpacking
        first: int = defaults[0]
        return first
    raise ValueError("empty")

class SafeList:
    items: list[int]
    
    def __init__(self, initial: list[int]):
        self.items = initial
    
    def first(self, defaults: list[int]) -> int:
        return process_with_defaults(self.items, defaults)
    
    def merge(self, others: list[list[int]]) -> list[int]:
        all_lists: list[list[int]] = [self.items]
        all_lists.extend(others)
        return merge_lists(all_lists)

def main():
    # Test rest patterns with fixed-size lists
    all_items: list[int] = [10, 20, 30, 40, 50]
    first_elem: int = all_items[0]
    # Use slice for middle elements instead of rest pattern
    middle: list[int] = all_items[1:4]
    last_elem: int = all_items[4]
    print(first_elem)
    for item in middle:
        print(item)
    print(last_elem)
    
    # Spread list into another list
    nums1: list[int] = [1, 2, 3]
    nums2: list[int] = [4, 5, 6]
    combined: list[int] = [*nums1, *nums2]
    for n in combined:
        print(n)
    
    # Test spread with variadic-style function using list
    extras: list[int] = [4, 5, 6]
    result: int = sum_variadic(10, extras)
    print(result)
    
    # Rest pattern with slice instead of starred unpacking
    left: int = all_items[0]
    rest: list[int] = all_items[1:]
    print(left)
    for r in rest:
        print(r)
    
    # Test method with list unpacking
    nums: SafeList = SafeList([100, 200, 300])
    defaults: list[int] = [999, 888]
    result2: int = nums.first(defaults)
    print(result2)

```

## Error

```
AI explicitly reported mismatch
```

## Output Comparison

### Expected
```
10
20
30
40
50
1
2
3
4
5
6
25
10
20
30
40
50
100
999
999

```

### Actual
```
10
20
30
40
50
1
2
3
4
5
6
25
10
20
30
40
50
100
```

## Timing

- Generation: 223.31s
- Execution: 4.93s
