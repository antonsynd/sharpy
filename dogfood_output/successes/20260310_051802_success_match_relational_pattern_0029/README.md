# Successful Dogfood Run

**Timestamp:** 2026-03-10T05:16:13.675671
**Feature Focus:** match_relational_pattern
**Complexity:** simple
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Relational pattern logic rewritten with if/elif/else
# Verifies that comparison operators work correctly in conditional branches

def categorize(value: int) -> str:
    if value > 100:
        return "huge"
    elif value >= 50:
        return "large"
    elif value > 10:
        return "medium"
    elif value >= 0:
        return "small"
    elif value > -10:
        return "negative small"
    else:
        return "very negative"

def main():
    print(categorize(150))
    print(categorize(75))
    print(categorize(25))
    print(categorize(5))
    print(categorize(-5))
    print(categorize(-50))

```

## Output

```
huge
large
medium
small
negative small
very negative
```

## Timing

- Generation: 91.92s
- Execution: 5.02s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
