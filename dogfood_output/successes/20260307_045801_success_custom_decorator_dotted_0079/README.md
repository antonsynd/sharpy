# Successful Dogfood Run

**Timestamp:** 2026-03-07T04:57:23.036566
**Feature Focus:** custom_decorator_dotted
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Test custom decorators with dotted names mapping to .NET attributes
# Tests: @system.obsolete, @system.serializable, @system.dll_import

@system.obsolete("Use NewCalculator instead")
class OldCalculator:
    def __init__(self):
        pass

    def add(self, x: int, y: int) -> int:
        return x + y

@system.serializable
class Config:
    value: int

    def __init__(self, val: int):
        self.value = val

def main():
    # Create instances - this should compile with warnings
    old = OldCalculator()
    result = old.add(5, 3)
    print(result)

    cfg = Config(42)
    print(cfg.value)

    # Use a helper function to verify values
    counter: int = 0
    while counter < 2:
        counter += 1
        print(counter)

```

## Output

```
8
42
1
2
```

## Timing

- Generation: 28.14s
- Execution: 4.55s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
