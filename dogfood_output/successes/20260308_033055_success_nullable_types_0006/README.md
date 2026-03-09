# Successful Dogfood Run

**Timestamp:** 2026-03-08T03:27:51.312941
**Feature Focus:** nullable_types
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Test nullable types with Some/None constructors and operations

def main():
    # Basic nullable int
    x: int? = Some(42)
    print(x.unwrap())
    
    # Nullable with None
    y: int? = None()
    print(y.unwrap_or(0))
    
    # Nullable string
    name: str? = Some("hello")
    print(name.unwrap())
    
    # Type narrowing
    val: int? = Some(100)
    if val is not None:
        print(val)  # Already unwrapped by narrowing
    
    # Map operation
    num: int? = Some(5)
    doubled: int? = num.map(lambda n: n * 2)
    print(doubled.unwrap())
    
    # Chain of nullables
    a: str? = None()
    result: str? = a.unwrap_or("default")
    print(result)
    
    # Nullable bool
    flag: bool? = Some(True)
    print(flag.unwrap())
    
    # Nullable float
    price: float? = Some(19.99)
    print(price.unwrap())
    
    # Unwrap_or with same type default
    absent: int? = None()
    print(absent.unwrap_or(-1))

```

## Output

```
42
0
hello
100
10
default
True
19.99
-1
```

## Timing

- Generation: 173.04s
- Execution: 5.29s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
