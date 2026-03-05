# Successful Dogfood Run

**Timestamp:** 2026-03-04T17:02:26.799366
**Feature Focus:** higher_order_function
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Higher-order function composition with closures
def compose(f: (int) -> int, g: (int) -> int) -> (int) -> int:
    return lambda x: f(g(x))

def apply_twice(fn: (int) -> int, x: int) -> int:
    return fn(fn(x))

def make_adder(n: int) -> (int) -> int:
    return lambda x: x + n

def apply_if_positive(fn: (int) -> int, x: int) -> int:
    if x > 0:
        return fn(x)
    return x

def main():
    # Compose transforms: g applied first, then f
    add_then_mul = compose(lambda x: x * 3, lambda x: x + 2)
    print(add_then_mul(4))
    
    # Closure capturing n=5, applied twice
    add_five_fn = make_adder(5)
    print(apply_twice(add_five_fn, 10))
    
    # Composed closures: add_three then add_five
    add_three_fn = make_adder(3)
    pipeline = compose(add_five_fn, add_three_fn)
    print(pipeline(7))
    
    # Nested composition: pipeline then double
    double = lambda x: x * 2
    double_pipeline = compose(double, pipeline)
    print(double_pipeline(5))
    
    # Conditional higher-order application
    print(apply_if_positive(lambda n: n * n, 5))

```

## Output

```
18
20
15
26
25
```

## Timing

- Generation: 550.03s
- Execution: 4.99s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
