# Successful Dogfood Run

**Timestamp:** 2026-03-06T18:22:43.173685
**Feature Focus:** union_with_generics
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
union Either[L, R]:
    case Left(value: L)
    case Right(value: R)

def classify(n: int) -> Either[str, int]:
    if n < 0:
        return Either.Left("negative")
    elif n == 0:
        return Either.Left("zero")
    return Either.Right(n * 2)

def main():
    values: list[int] = [-7, 0, 3, 5]
    count: int = 0
    for n in values:
        result = classify(n)
        match result:
            case Left(msg):
                print(msg)
            case Right(val):
                print(val)
        count += 1
    print(count)

```

## Output

```
negative
zero
6
10
4
```

## Timing

- Generation: 570.52s
- Execution: 4.68s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
