# Issue Report: output_mismatch

**Timestamp:** 2026-03-03T08:20:04.435121
**Type:** output_mismatch
**Feature Focus:** generator_basic
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Generator with running cumulative sum - yields intermediate totals
def cumulative_sums(values: list[int]) -> int:
    total: int = 0
    for v in values:
        total += v
        yield total

# Generator that yields only positive running totals
def positive_accumulations(values: list[int]) -> int:
    total: int = 0
    for v in values:
        total += v
        if total > 0:
            yield total

def main():
    # Test cumulative sums generator
    nums: list[int] = [3, -1, 4, -1, 5]
    
    print("cumulative:")
    for running in cumulative_sums(nums):
        print(running)
    
    print("positive:")
    for positive in positive_accumulations([5, -3, -1, 4, 2]):
        print(positive)
    
    # Generator with early termination via return
    print("limited:")
    for partial in cumulative_sums([10, 20, 30, 40]):
        if partial > 25:
            break
        print(partial)

```

## Error

```
AI verification backend failure
```

## Output Comparison

### Expected
```
cumulative:
3
2
6
5
10
positive:
5
9
11
limited:
10
20

```

### Actual
```
cumulative:
3
2
6
5
10
positive:
5
2
1
5
7
limited:
10
```

## Timing

- Generation: 66.42s
- Execution: 5.18s
