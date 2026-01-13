# Issue Report: compilation_failed

**Timestamp:** 2026-01-13T16:14:39.644546
**Type:** compilation_failed
**Feature Focus:** variables
**Complexity:** medium
**Backend:** claude

## Generated Sharpy Code

```python
# Variable declaration, shadowing, and type inference tests

def calculate_sum(a: int, b: int) -> int:
    result: int = a + b
    print(result)
    return result

def process_numbers(start: int, count: int) -> int:
    total: int = 0
    current: int = start
    
    for i in range(count):
        total += current
        print(current)
        current += 2
    
    print(total)
    return total

def main():
    # Explicit type annotation
    x: int = 10
    y: int = 20
    print(x)
    print(y)
    
    # Type inference
    z = x + y
    print(z)
    
    # Variable shadowing with different type
    value: int = 100
    print(value)
    value: str = "shadowed"
    print(value)
    value: auto = 3.14
    print(value)
    
    # Augmented assignment
    counter: int = 0
    counter += 5
    print(counter)
    counter *= 2
    print(counter)
    counter -= 3
    print(counter)
    
    # Control flow with variables
    flag: bool = True
    if flag:
        message: str = "flag is true"
        print(message)
    else:
        message: str = "flag is false"
        print(message)
    
    # Nested function calls
    sum_result: int = calculate_sum(15, 25)
    print(sum_result)
    
    # Loop with variable updates
    n: int = 5
    factorial: int = 1
    while n > 1:
        factorial *= n
        n -= 1
    print(factorial)
    
    # Process numbers function
    total: int = process_numbers(1, 4)
    print(total)
    
    # Final variable state check
    final: int = sum_result + factorial + total
    print(final)

main()

# EXPECTED OUTPUT:
# 10
# 20
# 30
# 100
# shadowed
# 3.14
# 5
# 10
# 7
# flag is true
# 40
# 40
# 120
# 1
# 3
# 5
# 7
# 16
# 16
# 176
```

## Error

```
Assembly compilation failed:
  dogfood_test.cs(6,81): error CS0116: A namespace cannot directly contain members such as fields, methods or statements
  dogfood_test.cs(6,23): error CS1514: { expected
  dogfood_test.cs(6,93): error CS1022: Type or namespace definition, or end-of-file expected

```

## Compiler Output

```
Warning: 1 module-level statement(s) ignored because a 'main' function is defined

```

## Timing

- Generation: 10.68s
- Execution: 1.35s
