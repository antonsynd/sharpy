# Successful Dogfood Run

**Timestamp:** 2026-03-10T05:35:50.921322
**Feature Focus:** try_except_else
**Complexity:** simple
**Backend:** klaude

## Generated Sharpy Code

```python
def main():
    results: list[int] = []
    
    # Test where no exception is raised - else should execute
    result: int = 0
    try:
        x: int = 10
        y: int = 2
        result = x // y
    except ZeroDivisionError:
        results.append(-1)
    except ValueError:
        results.append(-2)
    else:
        results.append(result)
    
    # Test where exception IS raised - else should NOT execute
    value: int = 0
    try:
        a: int = 5
        b: int = 0
        value = a // b
    except ZeroDivisionError:
        results.append(0)
    else:
        results.append(999)
    
    # Verify results: first try succeeded (else ran), second failed (except ran)
    for r in results:
        print(r)

```

## Output

```
5
0
```

## Timing

- Generation: 144.51s
- Execution: 5.11s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
