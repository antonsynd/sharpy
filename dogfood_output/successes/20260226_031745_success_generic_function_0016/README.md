# Successful Dogfood Run

**Timestamp:** 2026-02-26T03:14:07.486379
**Feature Focus:** generic_function
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Generic function composition and type inference with higher-order functions
# Tests single-type-parameter generic functions with various concrete types
# and generic higher-order function passing

def identity[T](value: T) -> T:
    return value

def apply[T](fn: (T) -> T, value: T) -> T:
    return fn(value)

def double_int(x: int) -> int:
    return x * 2

def append_wow(s: str) -> str:
    return s + "wow"

def negate(b: bool) -> bool:
    return not b

def main():
    # Generic identity with different types
    print(identity(55))
    print(identity("hi"))
    print(identity(False))
    
    # Generic apply with int function
    print(apply(double_int, 6))
    
    # Generic apply with str function  
    print(apply(append_wow, "oh"))
    
    # Generic apply with bool function
    print(apply(negate, True))
```

## Output

```
55
hi
False
12
ohwow
False
```

## Timing

- Generation: 208.16s
- Execution: 4.53s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
