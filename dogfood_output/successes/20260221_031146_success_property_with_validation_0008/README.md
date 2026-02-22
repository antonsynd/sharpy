# Successful Dogfood Run

**Timestamp:** 2026-02-21T03:10:57.620287
**Feature Focus:** property_with_validation
**Complexity:** simple
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Property with validation to ensure age stays within valid range
class Person:
    _age: int
    
    def __init__(self, age: int):
        self._age = age
    
    property get age(self) -> int:
        return self._age
    
    property set age(self, value: int):
        if value < 0:
            self._age = 0
        elif value > 150:
            self._age = 150
        else:
            self._age = value

def main():
    p = Person(25)
    print(p.age)
    p.age = -5
    print(p.age)
    p.age = 200
    print(p.age)
    p.age = 50
    print(p.age)
    # EXPECTED OUTPUT:
    # 25
    # 0
    # 150
    # 50
```

## Output

```
25
0
150
50
```

## Timing

- Generation: 37.88s
- Execution: 4.78s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
