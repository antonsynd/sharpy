# Issue Report: execution_failed

**Timestamp:** 2026-01-29T00:07:02.766617
**Type:** execution_failed
**Feature Focus:** higher_order_function
**Complexity:** complex
**Backend:** claude

## Generated Sharpy Code

```python
# Test: Higher-order functions with filtering, mapping, and custom predicates
# Tests: lambda expressions, function parameters, list comprehensions, optional types, generics

class NumberProcessor:
    numbers: list[int]
    
    def __init__(self, nums: list[int]):
        self.numbers = nums
    
    def apply_transform(self, transform_fn) -> list[int]:
        return [transform_fn(n) for n in self.numbers]
    
    def filter_by(self, predicate_fn) -> list[int]:
        return [n for n in self.numbers if predicate_fn(n)]
    
    def find_first(self, predicate_fn) -> int?:
        for n in self.numbers:
            if predicate_fn(n):
                return Some(n)
        return Nothing

class Validator:
    threshold: int
    
    def __init__(self, threshold: int):
        self.threshold = threshold
    
    def is_above_threshold(self, value: int) -> bool:
        return value > self.threshold
    
    def is_even(self, value: int) -> bool:
        return value % 2 == 0

def compose(f, g):
    return lambda x: f(g(x))

def main():
    processor: NumberProcessor = NumberProcessor([1, 2, 3, 4, 5, 6, 7, 8, 9, 10])
    validator: Validator = Validator(5)
    
    # Test basic transformation
    doubled: list[int] = processor.apply_transform(lambda x: x * 2)
    print(doubled[0])
    print(doubled[4])
    
    # Test filtering with method reference pattern
    above_threshold: list[int] = processor.filter_by(validator.is_above_threshold)
    print(above_threshold[0])
    print(len(above_threshold))
    
    # Test composition
    square_then_add_ten = compose(lambda x: x + 10, lambda x: x * x)
    print(square_then_add_ten(3))
    
    # Test find_first with optional return
    first_even: int? = processor.find_first(validator.is_even)
    print(first_even.unwrap())
    
    # Test chaining transformations
    squared: list[int] = processor.apply_transform(lambda x: x * x)
    even_squares: list[int] = NumberProcessor(squared).filter_by(validator.is_even)
    print(even_squares[0])
    print(len(even_squares))

# EXPECTED OUTPUT:
# 2
# 10
# 6
# 5
# 19
# 2
# 4
# 5
```

## Error

```
Compilation failed:
  Semantic error at line 10, column 31: Parameter 'transform_fn' requires a type annotation
  Semantic error at line 11, column 17: 'transform_fn' is not callable (type: <?>)
  Semantic error at line 13, column 25: Parameter 'predicate_fn' requires a type annotation
  Semantic error at line 14, column 44: 'predicate_fn' is not callable (type: <?>)
  Semantic error at line 16, column 26: Parameter 'predicate_fn' requires a type annotation
  Semantic error at line 18, column 16: 'predicate_fn' is not callable (type: <?>)
  Semantic error at line 34, column 13: Parameter 'f' requires a type annotation
  Semantic error at line 34, column 16: Parameter 'g' requires a type annotation
  Semantic error at line 35, column 24: 'g' is not callable (type: <?>)
  Semantic error at line 35, column 22: 'f' is not callable (type: <?>)
  Semantic error at line 35, column 5: Cannot return type '(<?>) -> <?>' from function expecting 'None'
  Semantic error at line 53, column 11: 'square_then_add_ten' is not callable (type: None)

```

## Timing

- Generation: 19.42s
- Execution: 1.00s
