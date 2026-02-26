# Successful Dogfood Run

**Timestamp:** 2026-02-26T10:27:54.067282
**Feature Focus:** set_literal
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
def main():
    # Set literal with duplicates - automatically deduplicated
    values: set[int] = {5, 5, 5, 10, 10, 15}
    print(len(values))

    # Spread sets into new literal with computed values
    extended: set[int] = {*values, 20, 25}
    print(len(extended))

    # Create set with computed values
    base: int = 7
    prefs: set[int] = {base, base * 2, base + 10}
    print(len(prefs))

    # Combine multiple sets via spread
    combined: set[int] = {*values, *prefs}
    print(len(combined))

    # Membership checks
    has_five: bool = 5 in values
    print(has_five)

    has_twenty: bool = 20 in extended
    print(has_twenty)

    # Direct set literal in expression
    direct: set[int] = {1, 2, 3}
    print(len(direct))

    # Check set membership with computed value
    result: set[int] = {1, 4, 9}
    check: int = 2
    has_square: bool = (check * check) in result
    print(has_square)

    # Set with string values
    words: set[str] = {"hello", "world", "hello"}
    print(len(words))

    has_hello: bool = "hello" in words
    print(has_hello)
```

## Output

```
3
5
3
6
True
True
3
True
2
True
```

## Timing

- Generation: 316.75s
- Execution: 4.45s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
