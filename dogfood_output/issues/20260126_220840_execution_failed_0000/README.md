# Issue Report: execution_failed

**Timestamp:** 2026-01-26T22:08:17.625792
**Type:** execution_failed
**Feature Focus:** generic_function
**Complexity:** medium
**Backend:** claude

## Generated Sharpy Code

```python
# Generic function test: Data transformation pipeline with multiple type parameters
# Tests: Generic functions with constraints, chaining transformations

interface IComparable:
    def compare_to(self, other: IComparable) -> int:
        ...

interface IFormattable:
    def format(self) -> str:
        ...

class Score(IComparable, IFormattable):
    value: int
    
    def __init__(self, v: int):
        self.value = v
    
    def compare_to(self, other: IComparable) -> int:
        other_score = other
        if self.value > other_score.value:
            return 1
        if self.value < other_score.value:
            return -1
        return 0
    
    def format(self) -> str:
        return f"Score: {self.value}"

def transform[T: IFormattable](item: T) -> str:
    return item.format()

def find_max[T: IComparable](a: T, b: T) -> T:
    if a.compare_to(b) > 0:
        return a
    return b

def process_pair[T: IComparable, IFormattable](first: T, second: T) -> str:
    winner = find_max(first, second)
    return transform(winner)

def main():
    score1 = Score(85)
    score2 = Score(92)
    score3 = Score(78)
    
    print(transform(score1))
    print(transform(score2))
    
    result1 = process_pair(score1, score2)
    print(result1)
    
    result2 = process_pair(score2, score3)
    print(result2)

# EXPECTED OUTPUT:
# Score: 85
# Score: 92
# Score: 92
# Score: 92
```

## Error

```
Compilation failed:
  Semantic error at line 20, column 25: Type 'IComparable' has no member 'value'
  Semantic error at line 22, column 25: Type 'IComparable' has no member 'value'
  Semantic error at line 38, column 14: Inferred type 'T' does not satisfy constraint 'IComparable' for type parameter 'T'
  Semantic error at line 39, column 12: Inferred type '<?>' does not satisfy constraint 'IFormattable' for type parameter 'T'
  Semantic error at line 49, column 15: Type parameter 'IFormattable' cannot be inferred; no arguments provide type information. Use explicit syntax: process_pair[T, IFormattable](...)
  Semantic error at line 52, column 15: Type parameter 'IFormattable' cannot be inferred; no arguments provide type information. Use explicit syntax: process_pair[T, IFormattable](...)

```

## Timing

- Generation: 9.47s
- Execution: 1.73s
