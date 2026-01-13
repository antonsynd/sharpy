# Issue Report: compilation_failed

**Timestamp:** 2026-01-13T16:18:34.044220
**Type:** compilation_failed
**Feature Focus:** nested_control_flow
**Complexity:** medium
**Backend:** claude

## Generated Sharpy Code

```python
# Nested control flow test - number classification and processing

def classify_number(n: int) -> str:
    if n < 0:
        return "negative"
    elif n == 0:
        return "zero"
    elif n % 2 == 0:
        return "positive_even"
    else:
        return "positive_odd"

def process_range(start: int, end: int) -> int:
    total: int = 0
    even_count: int = 0
    
    for i in range(start, end):
        classification: str = classify_number(i)
        print(classification)
        
        if classification == "positive_even":
            even_count += 1
            total += i
        elif classification == "positive_odd":
            total += i * 2
        elif classification == "negative":
            total -= 1
        else:
            pass
    
    print(even_count)
    return total

def find_threshold(target: int) -> int:
    sum: int = 0
    counter: int = 1
    
    while sum < target:
        if counter % 3 == 0:
            print(counter)
            sum += counter
        elif counter % 5 == 0:
            print(counter)
            sum += counter * 2
        else:
            sum += 1
        
        counter += 1
        
        if counter > 20:
            break
    
    return sum

result1: int = process_range(-2, 6)
print(result1)

result2: int = find_threshold(25)
print(result2)

# EXPECTED OUTPUT:
# negative
# negative
# zero
# positive_odd
# positive_even
# positive_odd
# positive_even
# positive_odd
# 2
# 24
# 3
# 5
# 6
# 9
# 10
# 12
# 15
# 44
```

## Error

```
Assembly compilation failed:
  dogfood_test.cs(6,81): error CS0116: A namespace cannot directly contain members such as fields, methods or statements
  dogfood_test.cs(6,23): error CS1514: { expected
  dogfood_test.cs(6,93): error CS1022: Type or namespace definition, or end-of-file expected

```

## Timing

- Generation: 8.49s
- Execution: 1.22s
