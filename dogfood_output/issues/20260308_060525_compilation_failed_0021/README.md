# Issue Report: compilation_failed

**Timestamp:** 2026-03-08T06:00:55.660329
**Type:** compilation_failed
**Feature Focus:** lambda_basic
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Lambda composition with generics, type aliases, and class-based function pipelines
# Tests: typed lambdas, generic constraints, lambda as class properties, higher-order functions

type IntTransformer = (int) -> int
type Predicate[T] = (T) -> bool

class LambdaPipeline[T]:
    """A pipeline that composes multiple transformation lambdas"""
    _transformers: list[IntTransformer]
    _final_check: Predicate[int]?
    
    def __init__(self, check: Predicate[int]?):
        self._transformers = []
        self._final_check = check
    
    def add_transform(self, fn: IntTransformer) -> None:
        self._transformers.append(fn)
    
    def compose(self, fn1: IntTransformer, fn2: IntTransformer) -> IntTransformer:
        """Returns a new lambda that is the composition of two lambdas"""
        return lambda x: fn2(fn1(x))
    
    def execute(self, value: int) -> int?:
        """Apply all transforms, then check predicate if exists"""
        result: int = value
        for transform in self._transformers:
            result = transform(result)
        
        if self._final_check is not None:
            if self._final_check(result):
                return Some(result)
            return None()
        return Some(result)

class NumberProcessor:
    """Uses composed lambdas to process number ranges"""
    _pipeline: LambdaPipeline[int]
    
    def __init__(self):
        self._pipeline = LambdaPipeline[int](lambda x: x > 0)
        
        # Add composed transformation: (x * 2) + 1
        double: IntTransformer = lambda x: x * 2
        add_one: IntTransformer = lambda x: x + 1
        composed: IntTransformer = self._pipeline.compose(double, add_one)
        self._pipeline.add_transform(composed)
        
        # Add another transform: square the value
        square: IntTransformer = lambda n: n * n
        self._pipeline.add_transform(square)
    
    def process(self, inputs: list[int]) -> list[int]:
        results: list[int] = []
        for n in inputs:
            opt = self._pipeline.execute(n)
            if opt is not None:
                results.append(opt)
        return results

def apply_twice(fn: IntTransformer, x: int) -> int:
    """Apply a lambda function twice to the same value"""
    return fn(fn(x))

def conditional_transform(pred: Predicate[int], yes_fn: IntTransformer, no_fn: IntTransformer) -> IntTransformer:
    """Returns a lambda that chooses transform based on predicate"""
    return lambda x: yes_fn(x) if pred(x) else no_fn(x)

def main():
    processor = NumberProcessor()
    
    # Test data: values that will go through ((x*2)+1)^2 transform
    # x=1: (1*2+1)^2 = 9 (passes >0 check)
    # x=3: (3*2+1)^2 = 49 (passes >0 check)  
    # x=-1: ((-1)*2+1)^2 = 1 (passes >0 check after square)
    inputs: list[int] = [1, 3, -1]
    processed = processor.process(inputs)
    print(f"Processed count: {len(processed)}")
    
    # Test apply_twice with lambda
    triple: IntTransformer = lambda x: x * 3
    result = apply_twice(triple, 2)  # (2*3)*3 = 18
    print(f"Applied twice: {result}")
    
    # Test conditional_transform with lambdas
    is_even: Predicate[int] = lambda n: n % 2 == 0
    double_it: IntTransformer = lambda x: x * 2
    triple_it: IntTransformer = lambda x: x * 3
    conditional = conditional_transform(is_even, double_it, triple_it)
    
    print(f"Conditional(4): {conditional(4)}")   # even, doubled: 8
    print(f"Conditional(5): {conditional(5)}") # odd, tripled: 15
    
    # Direct lambda composition test
    add_five: IntTransformer = lambda x: x + 5
    sub_three: IntTransformer = lambda x: x - 3
    chained: IntTransformer = lambda x: sub_three(add_five(x))  # x + 5 - 3 = x + 2
    print(f"Chained(10): {chained(10)}")
    
    # Processed values test
    print(f"First processed: {processed[0]}")  # ((1*2+1)^2) = 9
    print(f"Second processed: {processed[1]}") # ((3*2+1)^2) = 49

```

## Error

```
Assembly compilation failed:

error[CS1503]: Argument 1: cannot convert from 'Sharpy.Optional<int>' to 'int'
  --> /tmp/tmpuj257e83/dogfood_test.spy:57:36
    |
 57 |                 results.append(opt)
    |                                    ^
    |

error[CS1660]: Cannot convert lambda expression to type 'Optional<Optional<Func<int, bool>>>' because it is not a delegate type
  --> /tmp/tmpuj257e83/dogfood_test.spy:40:56
    |
 40 |         self._pipeline = LambdaPipeline[int](lambda x: x > 0)
    |                                                        ^
    |

error[CS1955]: Non-invocable member 'DogfoodTest.LambdaPipeline<T>._FinalCheck' cannot be used like a method.
  --> /tmp/tmpuj257e83/dogfood_test.spy:30:26
    |
 30 |             if self._final_check(result):
    |                          ^
    |


```

## Generated C#

```csharp
Generated C# code written to: /tmp/tmpuj257e83/dogfood_test.cs

```

## Timing

- Generation: 255.21s
- Execution: 5.00s
