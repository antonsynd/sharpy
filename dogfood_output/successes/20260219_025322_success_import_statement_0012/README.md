# Successful Dogfood Run

**Timestamp:** 2026-02-19T02:52:13.102497
**Feature Focus:** import_statement
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Import from system namespace with class-based computation
# Tests: from system import, class instantiation, method calls, arithmetic

from system import Console

class FactorialEngine:
    name: str
    
    def __init__(self, name: str):
        self.name = name
    
    def compute(self, n: int) -> int:
        if n <= 1:
            return 1
        result: int = 1
        i: int = 2
        while i <= n:
            result = result * i
            i += 1
        return result

def main():
    engine = FactorialEngine("Calculator")
    
    print(engine.name)
    print(engine.compute(5))
    print(engine.compute(7))
    print(engine.compute(10))

# EXPECTED OUTPUT:
# Calculator
# 120
# 5040
# 3628800
```

## Output

```
Calculator
120
5040
3628800
```

## Timing

- Generation: 58.73s
- Execution: 4.59s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
