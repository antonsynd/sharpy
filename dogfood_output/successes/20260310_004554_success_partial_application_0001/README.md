# Successful Dogfood Run

**Timestamp:** 2026-03-10T00:34:08.145487
**Feature Focus:** partial_application
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Test partial application with data transformation pipeline
# Demonstrates: partial application with _ and operator sections

class DataPoint:
    value: float
    
    def __init__(self, v: float):
        self.value = v
    
    def scale(self, factor: float) -> float:
        return self.value * factor

def get_scaled_value(dp: DataPoint, factor: float) -> float:
    return dp.scale(factor)

def apply_transform(items: list[DataPoint], transform: (DataPoint) -> float) -> list[float]:
    result: list[float] = []
    for item in items:
        result.append(transform(item))
    return result

def combine(a: float, b: float, c: float) -> float:
    return a + b * c

def clamp(value: float, minimum: float, maximum: float) -> float:
    if value < minimum:
        return minimum
    if value > maximum:
        return maximum
    return value

def main():
    data: list[DataPoint] = [DataPoint(2.0), DataPoint(5.0), DataPoint(8.0)]
    
    # Partial applications - all with regular functions
    double_value = get_scaled_value(_, 2.0)
    is_large = (_ > 10.0)
    mul_and_add = combine(_, 5.0, 3.0)
    limit_0_100 = clamp(_, 0.0, 100.0)
    
    scaled: list[float] = apply_transform(data, double_value)
    print(scaled[0])
    print(scaled[1])
    print(scaled[2])
    
    test_val: float = 15.0
    print(is_large(test_val))
    
    result: float = mul_and_add(10.0)
    print(result)
    
    overshoot: float = 150.0
    print(limit_0_100(overshoot))

```

## Output

```
4.0
10.0
16.0
True
25.0
100.0
```

## Timing

- Generation: 688.75s
- Execution: 5.67s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
