# Successful Dogfood Run

**Timestamp:** 2026-03-03T10:20:32.499499
**Feature Focus:** partial_application
**Complexity:** simple
**Backend:** klaude

## Generated Sharpy Code

```python
# Test partial application with lambdas
def add_offset(base: int, offset: int) -> int:
    return base + offset

def main():
    # Partial application using lambda: fix second argument to 1000
    add_thousand: (int) -> int = lambda x: add_offset(x, 1000)
    
    # Lambda for doubling - renamed to avoid shadowing double type
    doubler: (int) -> int = lambda x: x * 2
    
    x: int = 5
    print(add_thousand(x))
    print(doubler(x))
    print(add_thousand(doubler(3)))

```

## Output

```
1005
10
1006
```

## Timing

- Generation: 282.49s
- Execution: 4.80s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
