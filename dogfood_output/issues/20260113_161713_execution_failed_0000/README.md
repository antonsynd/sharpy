# Issue Report: execution_failed

**Timestamp:** 2026-01-13T16:16:46.029268
**Type:** execution_failed
**Feature Focus:** functions
**Complexity:** medium
**Backend:** claude

## Generated Sharpy Code

```python
# Function to check if a number is even
def is_even(n: int) -> bool:
    return n % 2 == 0

# Function to calculate the sum of even numbers in a range
def sum_even_in_range(start: int, end: int) -> int:
    total: int = 0
    for i in range(start, end):
        if is_even(i):
            total += i
    return total

# Function to find and print statistics about numbers
def analyze_range(start: int, end: int):
    print("Analyzing range from", start, "to", end)
    
    even_sum: int = sum_even_in_range(start, end)
    print("Sum of even numbers:", even_sum)
    
    # Count evens and odds
    even_count: int = 0
    odd_count: int = 0
    
    for i in range(start, end):
        if is_even(i):
            even_count += 1
        else:
            odd_count += 1
    
    print("Even count:", even_count)
    print("Odd count:", odd_count)
    
    # Find largest even using while loop
    current: int = end - 1
    largest_even: int = -1
    
    while current >= start:
        if is_even(current):
            largest_even = current
            break
        current -= 1
    
    if largest_even >= 0:
        print("Largest even:", largest_even)
    else:
        print("No even numbers found")

# Main execution
print("=== Number Analysis Program ===")

analyze_range(1, 11)

print("---")

analyze_range(5, 8)

print("=== Done ===")

# EXPECTED OUTPUT:
# === Number Analysis Program ===
# Analyzing range from 1 to 11
# Sum of even numbers: 30
# Even count: 5
# Odd count: 5
# Largest even: 10
# ---
# Analyzing range from 5 to 8
# Sum of even numbers: 6
# Even count: 1
# Odd count: 2
# Largest even: 6
# === Done ===
```

## Error

```
Compilation failed:
  Semantic error at line 15, column 5: Function 'print' expects 1 or 1-5 arguments but got 4
  Semantic error at line 18, column 5: Function 'print' expects 1 or 1-5 arguments but got 2
  Semantic error at line 30, column 5: Function 'print' expects 1 or 1-5 arguments but got 2
  Semantic error at line 31, column 5: Function 'print' expects 1 or 1-5 arguments but got 2
  Semantic error at line 44, column 9: Function 'print' expects 1 or 1-5 arguments but got 2

```

## Timing

- Generation: 8.89s
- Execution: 0.90s
