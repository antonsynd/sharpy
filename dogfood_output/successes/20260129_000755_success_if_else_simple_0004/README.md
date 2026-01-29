# Successful Dogfood Run

**Timestamp:** 2026-01-29T00:07:39.303434
**Feature Focus:** if_else_simple
**Complexity:** simple
**Backend:** claude

## Generated Sharpy Code

```python
# Test simple if-else with integer comparison

def main():
    temperature: int = 25
    
    if temperature > 30:
        print(1)
    else:
        print(0)
    
    threshold: int = 20
    
    if threshold < 25:
        print(100)
    else:
        print(200)

# EXPECTED OUTPUT:
# 0
# 100
```

## Output

```
0
100
```

## Timing

- Generation: 8.44s
- Execution: 1.57s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
