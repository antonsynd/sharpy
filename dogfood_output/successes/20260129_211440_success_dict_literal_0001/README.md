# Successful Dogfood Run

**Timestamp:** 2026-01-29T21:14:28.174996
**Feature Focus:** dict_literal
**Complexity:** simple
**Backend:** claude

## Generated Sharpy Code

```python
# Test: Dictionary literal with string keys and int values
def main():
    inventory: dict[str, int] = {"apples": 12, "bananas": 8, "oranges": 15}
    print(inventory["apples"])
    print(inventory["bananas"])
    print(inventory["oranges"])

# EXPECTED OUTPUT:
# 12
# 8
# 15
```

## Output

```
12
8
15
```

## Timing

- Generation: 4.50s
- Execution: 1.49s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
