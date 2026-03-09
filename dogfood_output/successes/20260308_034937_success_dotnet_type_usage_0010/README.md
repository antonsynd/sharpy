# Successful Dogfood Run

**Timestamp:** 2026-03-08T03:47:48.640308
**Feature Focus:** dotnet_type_usage
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
from system import Math

def distance(x1: float, y1: float, x2: float, y2: float) -> float:
    dx = x2 - x1
    dy = y2 - y1
    return Math.sqrt(dx * dx + dy * dy)

def main():
    # Using .NET Math in a calculation
    d = distance(0.0, 0.0, 3.0, 4.0)
    print(d)
    
    # Integer absolute value
    print(Math.abs(-42))
    
    # Power calculation
    print(Math.pow(2.0, 10.0))
    
    # Floor
    print(Math.floor(7.9))
    
    # Trigonometric identity: sin²(0) + cos²(0) should equal 1
    result = Math.sin(0.0) * Math.sin(0.0) + Math.cos(0.0) * Math.cos(0.0)
    print(result)
    
    # Min/Max
    print(Math.max(100, 50))
    print(Math.min(3.5, 2.5))

```

## Output

```
5.0
42
1024.0
7.0
1.0
100
2.5
```

## Timing

- Generation: 96.67s
- Execution: 5.46s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
