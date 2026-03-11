# Successful Dogfood Run

**Timestamp:** 2026-03-10T13:34:45.337135
**Feature Focus:** custom_decorator_simple
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Custom decorators on functions and structs

@system.serializable
struct Config:
    setting: int

    def __init__(self, val: int):
        self.setting = val

@system.obsolete("Use enhanced_compute instead")
def compute(x: int) -> int:
    return x + 1

def enhanced_compute(x: int) -> int:
    return x * 2 + 1

def main():
    cfg = Config(5)
    print(cfg.setting)

    old_result = compute(3)
    print(old_result)

    new_result = enhanced_compute(3)
    print(new_result)

```

## Output

```
5
4
7
```

## Timing

- Generation: 690.65s
- Execution: 4.99s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
