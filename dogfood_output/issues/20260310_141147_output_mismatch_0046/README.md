# Issue Report: output_mismatch

**Timestamp:** 2026-03-10T14:04:48.762059
**Type:** output_mismatch
**Feature Focus:** nested_if_in_loop
**Complexity:** simple
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Nested if statements in loop for number categorization
# Simple numeric analysis with multiple decision layers
def analyze_numbers() -> None:
    nums: list[int] = [5, 12, 8, 15, 3, 20, 7]
    for n in nums:
        if n > 10:
            if n % 2 == 0:
                print("large-even")
            else:
                print("large-odd")
        else:
            if n < 5:
                print("small")
            else:
                print("medium")
        if n % 3 == 0:
            if n > 9:
                print("div3-big")
            else:
                print("div3-small")

def main():
    analyze_numbers()

```

## Error

```
AI explicitly reported mismatch
```

## Output Comparison

### Expected
```
medium
div3-small
large-even
div3-big
large-odd
medium
small
div3-small
large-even
div3-big
medium
div3-small
large-odd

```

### Actual
```
medium
large-even
div3-big
medium
large-odd
div3-big
small
div3-small
large-even
medium
```

## Timing

- Generation: 153.35s
- Execution: 5.14s
