# Issue Report: compilation_failed

**Timestamp:** 2026-01-13T16:17:26.495073
**Type:** compilation_failed
**Feature Focus:** nested_control_flow
**Complexity:** complex
**Backend:** claude

## Generated Sharpy Code

```python
# Complex nested control flow test for Sharpy compiler
# Tests deeply nested loops, conditionals, and function interactions

def is_prime(n: int) -> bool:
    """Check if a number is prime using trial division"""
    if n < 2:
        return False
    if n == 2:
        return True
    if n % 2 == 0:
        return False
    i: int = 3
    while i * i <= n:
        if n % i == 0:
            return False
        i += 2
    return True

def classify_number(n: int) -> str:
    """Classify a number based on multiple criteria"""
    result: str = ""
    if n < 0:
        result = "negative"
        if n % 2 == 0:
            result = result + "_even"
        else:
            result = result + "_odd"
    elif n == 0:
        result = "zero"
    else:
        if is_prime(n):
            result = "prime"
        else:
            result = "composite"
        if n % 3 == 0:
            if n % 5 == 0:
                result = result + "_fizzbuzz"
            else:
                result = result + "_fizz"
        elif n % 5 == 0:
            result = result + "_buzz"
    return result

def find_pattern_sum(limit: int, divisor1: int, divisor2: int) -> int:
    """Sum numbers matching specific divisibility patterns"""
    total: int = 0
    for i in range(1, limit):
        if i % divisor1 == 0:
            if i % divisor2 == 0:
                total += i * 2
                print(f"  Both: {i} contributes {i * 2}")
            else:
                total += i
                print(f"  First only: {i} contributes {i}")
        elif i % divisor2 == 0:
            total += i
            print(f"  Second only: {i} contributes {i}")
    return total

def nested_loop_matrix(size: int) -> int:
    """Generate a pattern matrix and compute diagonal sum"""
    diagonal_sum: int = 0
    print(f"Matrix {size}x{size}:")
    for row in range(size):
        line: str = ""
        for col in range(size):
            value: int = 0
            if row == col:
                value = row + col + 1
                diagonal_sum += value
            elif row < col:
                if (row + col) % 2 == 0:
                    value = 1
                else:
                    value = 2
            else:
                if row % 2 == 0:
                    if col % 2 == 0:
                        value = 3
                    else:
                        value = 4
                else:
                    value = 5
            if col > 0:
                line = line + " "
            line = line + f"{value}"
        print(f"  {line}")
    return diagonal_sum

def collatz_steps(n: int, max_steps: int) -> int:
    """Count Collatz sequence steps with early termination"""
    steps: int = 0
    current: int = n
    print(f"Collatz({n}):")
    while current != 1:
        if steps >= max_steps:
            print(f"  Exceeded {max_steps} steps, stopping")
            break
        print(f"  Step {steps}: {current}")
        if current % 2 == 0:
            current = current // 2
        else:
            current = current * 3 + 1
        steps += 1
    if current == 1:
        print(f"  Step {steps}: {current} (done)")
    return steps

def process_range_with_breaks(start: int, end: int) -> int:
    """Process a range with conditional breaks and continues"""
    processed: int = 0
    skipped: int = 0
    for i in range(start, end):
        if i % 7 == 0:
            print(f"  Breaking at {i} (divisible by 7)")
            break
        if i % 3 == 0:
            if i % 2 == 0:
                skipped += 1
                print(f"  Skipping {i} (divisible by 6)")
                continue
        processed += i
        print(f"  Processed {i}, running total: {processed}")
    print(f"  Skipped count: {skipped}")
    return processed

def main():
    print("=== Sharpy Nested Control Flow Test ===")
    print("")
    
    # Test 1: Number classification
    print("--- Test 1: Number Classification ---")
    test_numbers: list[int] = [-4, -3, 0, 1, 2, 7, 15, 30]
    for num in test_numbers:
        classification: str = classify_number(num)
        print(f"classify({num}) = {classification}")
    print("")
    
    # Test 2: Pattern sum with nested conditions
    print("--- Test 2: Pattern Sum (limit=16, div1=3, div2=4) ---")
    pattern_result: int = find_pattern_sum(16, 3, 4)
    print(f"Pattern sum result: {pattern_result}")
    print("")
    
    # Test 3: Matrix generation with nested loops
    print("--- Test 3: Nested Loop Matrix ---")
    diag_sum: int = nested_loop_matrix(4)
    print(f"Diagonal sum: {diag_sum}")
    print("")
    
    # Test 4: Collatz sequence with while and conditionals
    print("--- Test 4: Collatz Sequences ---")
    c1: int = collatz_steps(6, 20)
    print(f"Steps for 6: {c1}")
    c2: int = collatz_steps(27, 10)
    print(f"Steps for 27 (max 10): {c2}")
    print("")
    
    # Test 5: Break and continue in nested context
    print("--- Test 5: Range Processing with Breaks ---")
    proc_result: int = process_range_with_breaks(1, 20)
    print(f"Final processed sum: {proc_result}")
    print("")
    
    # Test 6: Deeply nested prime finder
    print("--- Test 6: Prime Pairs in Range ---")
    pair_count: int = 0
    for i in range(2, 20):
        if is_prime(i):
            for j in range(i + 1, 20):
                if is_prime(j):
                    if j - i == 2:
                        print(f"  Twin primes: ({i}, {j})")
                        pair_count += 1
    print(f"Twin prime pairs found: {pair_count}")
    print("")
    
    print("=== All Tests Complete ===")

main()

# EXPECTED OUTPUT:
# === Sharpy Nested Control Flow Test ===
# 
# --- Test 1: Number Classification ---
# classify(-4) = negative_even
# classify(-3) = negative_odd
# classify(0) = zero
# classify(1) = composite
# classify(2) = prime
# classify(7) = prime
# classify(15) = composite_fizzbuzz
# classify(30) = composite_fizzbuzz
# 
# --- Test 2: Pattern Sum (limit=16, div1=3, div2=4) ---
#   First only: 3 contributes 3
#   Second only: 4 contributes 4
#   First only: 6 contributes 6
#   Second only: 8 contributes 8
#   First only: 9 contributes 9
#   Both: 12 contributes 24
#   First only: 15 contributes 15
# Pattern sum result: 69
# 
# --- Test 3: Nested Loop Matrix ---
# Matrix 4x4:
#   1 1 2 1
#   5 3 1 2
#   3 4 5 1
#   5 3 4 7
# Diagonal sum: 16
# 
# --- Test 4: Collatz Sequences ---
# Collatz(6):
#   Step 0: 6
#   Step 1: 3
#   Step 2: 10
#   Step 3: 5
#   Step 4: 16
#   Step 5: 8
#   Step 6: 4
#   Step 7: 2
#   Step 8: 1 (done)
# Steps for 6: 8
# Collatz(27):
#   Step 0: 27
#   Step 1: 82
#   Step 2: 41
#   Step 3: 124
#   Step 4: 62
#   Step 5: 31
#   Step 6: 94
#   Step 7: 47
#   Step 8: 142
#   Step 9: 71
#   Exceeded 10 steps, stopping
# Steps for 27 (max 10): 10
# 
# --- Test 5: Range Processing with Breaks ---
#   Processed 1, running total: 1
#   Processed 2, running total: 3
#   Processed 3, running total: 6
#   Processed 4, running total: 10
#   Processed 5, running total: 15
#   Skipping 6 (divisible by 6)
#   Breaking at 7 (divisible by 7)
#   Skipped count: 1
# Final processed sum: 15
# 
# --- Test 6: Prime Pairs in Range ---
#   Twin primes: (3, 5)
#   Twin primes: (5, 7)
#   Twin primes: (11, 13)
#   Twin primes: (17, 19)
# Twin prime pairs found: 4
# 
# === All Tests Complete ===
```

## Error

```
Assembly compilation failed:
  dogfood_test.cs(6,79): error CS0116: A namespace cannot directly contain members such as fields, methods or statements
  dogfood_test.cs(6,23): error CS1514: { expected
  dogfood_test.cs(6,91): error CS1022: Type or namespace definition, or end-of-file expected
  dogfood_test.cs(264,57): error CS0029: Cannot implicitly convert type 'Sharpy.Core.List<object>' to 'Sharpy.Core.List<int>'

```

## Compiler Output

```
Warning: 1 module-level statement(s) ignored because a 'main' function is defined

```

## Timing

- Generation: 29.93s
- Execution: 1.30s
