# Successful Dogfood Run

**Timestamp:** 2026-03-06T16:00:43.268522
**Feature Focus:** delegate_declaration
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Test delegate declarations - simple and generic delegates
# Delegates are named function types that can be used as parameters or variables

from system import Console

# Simple delegate - predicate function
delegate Predicate(value: int) -> bool

# Generic delegate - transform function
delegate Transform[T](value: T) -> T

# Delegate with multiple parameters
delegate BinaryOp(left: int, right: int) -> int

# Function that takes a delegate as parameter
def filter_numbers(numbers: list[int], pred: Predicate) -> list[int]:
    result: list[int] = []
    for n in numbers:
        if pred(n):
            result.append(n)
    return result

# Function that returns a delegate
def make_multiplier(factor: int) -> BinaryOp:
    return lambda x, y: (x * y) * factor

def process_data(transform: Transform[str]) -> str:
    return transform("hello")

def main():
    # Test 1: Using delegate as function parameter type
    numbers: list[int] = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10]
    
    # Pass lambda where delegate is expected
    evens: list[int] = filter_numbers(numbers, lambda x: x % 2 == 0)
    print("Evens:")
    for n in evens:
        print(n)
    
    # Test 2: Using generic delegate
    upper: Transform[str] = lambda s: s.upper()
    result: str = process_data(upper)
    print("Upper:")
    print(result)
    
    # Test 3: Delegate variable assignment and invocation
    is_positive: Predicate = lambda x: x > 0
    print("Is 5 positive:")
    print(is_positive(5))
    print("Is -3 positive:")
    print(is_positive(-3))
    
    # Test 4: Delegate that returns delegate and invocation
    doubler: BinaryOp = make_multiplier(2)
    tripler: BinaryOp = make_multiplier(3)
    print("Double 3*4:")
    print(doubler(3, 4))
    print("Triple 2*5:")
    print(tripler(2, 5))
    
    # Test 5: Pass lambda directly without intermediate variable
    odds: list[int] = filter_numbers(numbers, lambda x: x % 2 != 0)
    print("Odds count:")
    print(len(odds))

```

## Output

```
Evens:
2
4
6
8
10
Upper:
HELLO
Is 5 positive:
True
Is -3 positive:
False
Double 3*4:
24
Triple 2*5:
30
Odds count:
5
```

## Timing

- Generation: 269.22s
- Execution: 5.19s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
