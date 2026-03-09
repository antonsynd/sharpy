# Successful Dogfood Run

**Timestamp:** 2026-03-08T15:58:19.439652
**Feature Focus:** custom_decorator_simple
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Custom decorators on classes and methods with arithmetic processing
# Tests @obsolete class/function attribute and @system.serializable

@obsolete("Use DataProcessor instead")
class Calculator:
    multiplier: int
    
    def __init__(self, m: int):
        self.multiplier = m
    
    def compute(self, x: int) -> int:
        return x * self.multiplier

@system.serializable
class Config:
    threshold: int
    active: bool
    
    def __init__(self):
        self.threshold = 10
        self.active = True

def accumulate_until(items: list[int], cap: int) -> int:
    running = 0
    for item in items:
        if running + item > cap:
            break
        running += item
    return running

def main():
    calc = Calculator(3)
    print(calc.compute(7))
    
    cfg = Config()
    print(cfg.threshold)
    
    data: list[int] = [2, 4, 6, 8, 10]
    result = accumulate_until(data, 15)
    print(result)

```

## Output

```
21
10
12
```

## Timing

- Generation: 349.69s
- Execution: 5.25s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
