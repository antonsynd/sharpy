# Issue Report: compilation_failed

**Timestamp:** 2026-01-13T16:13:16.592696
**Type:** compilation_failed
**Feature Focus:** function_calls
**Complexity:** complex
**Backend:** claude

## Generated Sharpy Code

```python
# Complex function calls test - testing various parameter combinations,
# nested calls, default parameters, and keyword arguments

# Basic function with multiple parameters
def add(a: int, b: int) -> int:
    print("add called")
    return a + b

def subtract(a: int, b: int) -> int:
    print("subtract called")
    return a - b

def multiply(a: int, b: int) -> int:
    print("multiply called")
    return a * b

# Function with default parameters
def power(base: int, exponent: int = 2) -> int:
    print("power called")
    result: int = 1
    for i in range(exponent):
        result = result * base
    return result

# Function with multiple default parameters
def format_value(value: int, prefix: str = "[", suffix: str = "]") -> str:
    print("format_value called")
    return f"{prefix}{value}{suffix}"

# Function that calls other functions
def calculate_expression(x: int, y: int, z: int) -> int:
    print("calculate_expression called")
    sum_xy: int = add(x, y)
    product: int = multiply(sum_xy, z)
    squared: int = power(product)
    return squared

# Recursive function
def factorial(n: int) -> int:
    print(f"factorial({n}) called")
    if n <= 1:
        return 1
    return n * factorial(n - 1)

# Function with complex control flow
def classify_and_compute(value: int) -> int:
    print(f"classify_and_compute({value}) called")
    if value < 0:
        print("  negative branch")
        return multiply(value, -1)
    elif value == 0:
        print("  zero branch")
        return 0
    elif value < 10:
        print("  small positive branch")
        return power(value, 3)
    else:
        print("  large positive branch")
        return add(value, power(value, 2))

# Function returning result of nested function calls
def nested_operations(a: int, b: int) -> int:
    print("nested_operations called")
    return add(multiply(a, b), subtract(power(a), power(b)))

# Function demonstrating keyword arguments
def build_message(code: int, level: str = "INFO", active: bool = True) -> str:
    print("build_message called")
    status: str = "ON" if active else "OFF"
    return f"[{level}] Code: {code}, Status: {status}"

# Main test sequence
print("=== Basic Function Calls ===")
result1: int = add(5, 3)
print(f"add(5, 3) = {result1}")

result2: int = subtract(10, 4)
print(f"subtract(10, 4) = {result2}")

result3: int = multiply(6, 7)
print(f"multiply(6, 7) = {result3}")

print("")
print("=== Default Parameters ===")
result4: int = power(3)
print(f"power(3) = {result4}")

result5: int = power(2, 5)
print(f"power(2, 5) = {result5}")

result6: str = format_value(42)
print(f"format_value(42) = {result6}")

result7: str = format_value(99, "<<", ">>")
print(f"format_value(99, '<<', '>>') = {result7}")

print("")
print("=== Keyword Arguments ===")
result8: str = format_value(100, suffix=" END")
print(f"format_value(100, suffix=' END') = {result8}")

result9: str = build_message(200)
print(f"build_message(200) = {result9}")

result10: str = build_message(404, level="ERROR")
print(f"build_message(404, level='ERROR') = {result10}")

result11: str = build_message(500, active=False, level="WARN")
print(f"build_message(500, active=False, level='WARN') = {result11}")

print("")
print("=== Nested Function Calls ===")
result12: int = add(multiply(2, 3), subtract(10, 5))
print(f"add(multiply(2, 3), subtract(10, 5)) = {result12}")

result13: int = power(add(1, 2), subtract(5, 2))
print(f"power(add(1, 2), subtract(5, 2)) = {result13}")

result14: int = nested_operations(3, 4)
print(f"nested_operations(3, 4) = {result14}")

print("")
print("=== Function Calling Functions ===")
result15: int = calculate_expression(2, 3, 2)
print(f"calculate_expression(2, 3, 2) = {result15}")

print("")
print("=== Recursive Function ===")
result16: int = factorial(5)
print(f"factorial(5) = {result16}")

print("")
print("=== Complex Control Flow in Functions ===")
result17: int = classify_and_compute(-7)
print(f"classify_and_compute(-7) = {result17}")

result18: int = classify_and_compute(0)
print(f"classify_and_compute(0) = {result18}")

result19: int = classify_and_compute(5)
print(f"classify_and_compute(5) = {result19}")

result20: int = classify_and_compute(15)
print(f"classify_and_compute(15) = {result20}")

print("")
print("=== Chained Operations in Loop ===")
total: int = 0
for i in range(1, 6):
    computed: int = add(power(i), multiply(i, 2))
    print(f"  i={i}: power({i}) + multiply({i}, 2) = {computed}")
    total = add(total, computed)
print(f"Total sum = {total}")

print("")
print("=== Function Results as Conditions ===")
threshold: int = 50
for val in range(0, 101, 25):
    comparison_result: int = subtract(val, threshold)
    if comparison_result < 0:
        print(f"  {val} is below threshold by {multiply(comparison_result, -1)}")
    elif comparison_result == 0:
        print(f"  {val} equals threshold")
    else:
        print(f"  {val} exceeds threshold by {comparison_result}")

print("")
print("=== All tests completed ===")

# EXPECTED OUTPUT:
# === Basic Function Calls ===
# add called
# add(5, 3) = 8
# subtract called
# subtract(10, 4) = 6
# multiply called
# multiply(6, 7) = 42
# 
# === Default Parameters ===
# power called
# power(3) = 9
# power called
# power(2, 5) = 32
# format_value called
# format_value(42) = [42]
# format_value called
# format_value(99, '<<', '>>') = <<99>>
# 
# === Keyword Arguments ===
# format_value called
# format_value(100, suffix=' END') = [100 END
# build_message called
# build_message(200) = [INFO] Code: 200, Status: ON
# build_message called
# build_message(404, level='ERROR') = [ERROR] Code: 404, Status: ON
# build_message called
# build_message(500, active=False, level='WARN') = [WARN] Code: 500, Status: OFF
# 
# === Nested Function Calls ===
# multiply called
# subtract called
# add called
# add(multiply(2, 3), subtract(10, 5)) = 11
# add called
# subtract called
# power called
# power(add(1, 2), subtract(5, 2)) = 27
# nested_operations called
# multiply called
# power called
# power called
# subtract called
# add called
# nested_operations(3, 4) = 5
# 
# === Function Calling Functions ===
# calculate_expression called
# add called
# multiply called
# power called
# calculate_expression(2, 3, 2) = 100
# 
# === Recursive Function ===
# factorial(5) called
# factorial(4) called
# factorial(3) called
# factorial(2) called
# factorial(1) called
# factorial(5) = 120
# 
# === Complex Control Flow in Functions ===
# classify_and_compute(-7) called
#   negative branch
# multiply called
# classify_and_compute(-7) = 7
# classify_and_compute(0) called
#   zero branch
# classify_and_compute(0) = 0
# classify_and_compute(5) called
#   small positive branch
# power called
# classify_and_compute(5) = 125
# classify_and_compute(15) called
#   large positive branch
# power called
# add called
# classify_and_compute(15) = 240
# 
# === Chained Operations in Loop ===
# power called
# multiply called
# add called
#   i=1: power(1) + multiply(1, 2) = 3
# add called
# power called
# multiply called
# add called
#   i=2: power(2) + multiply(2, 2) = 8
# add called
# power called
# multiply called
# add called
#   i=3: power(3) + multiply(3, 2) = 15
# add called
# power called
# multiply called
# add called
#   i=4: power(4) + multiply(4, 2) = 24
# add called
# power called
# multiply called
# add called
#   i=5: power(5) + multiply(5, 2) = 35
# add called
# Total sum = 85
# 
# === Function Results as Conditions ===
# subtract called
# multiply called
#   0 is below threshold by 50
# subtract called
# multiply called
#   25 is below threshold by 25
# subtract called
#   50 equals threshold
# subtract called
#   75 exceeds threshold by 25
# subtract called
#   100 exceeds threshold by 50
# 
# === All tests completed ===
```

## Error

```
Assembly compilation failed:
  dogfood_test.cs(6,81): error CS0116: A namespace cannot directly contain members such as fields, methods or statements
  dogfood_test.cs(6,23): error CS1514: { expected
  dogfood_test.cs(6,93): error CS1022: Type or namespace definition, or end-of-file expected

```

## Timing

- Generation: 32.61s
- Execution: 1.34s
