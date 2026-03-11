# Successful Dogfood Run

**Timestamp:** 2026-03-10T19:38:07.834644
**Feature Focus:** match_relational_pattern
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Match expressions with guard clauses, classes, and enums
# Demonstrates range classification using guard clauses in match expressions

enum Priority:
    P0 = 0
    P1 = 1
    P2 = 2
    P3 = 3

type Threshold = tuple[low: int, high: int]

class DataAnalyzer:
    _values: list[int]

    def __init__(self):
        self._values = []

    def add(self, v: int) -> None:
        self._values.append(v)

    def get_max(self) -> int:
        if len(self._values) == 0:
            return 0
        max_val = self._values[0]
        for v in self._values:
            if v > max_val:
                max_val = v
        return max_val

def priority_from_value(val: int) -> Priority:
    # Using match expression with guard clauses for range classification
    return match val:
        case n if n < 0: Priority.P0
        case n if n < 50: Priority.P1
        case n if n <= 100: Priority.P2
        case _: Priority.P3

def status_for_threshold(t: Threshold, current: int) -> str:
    # Guard clauses for bound checking against tuple fields
    return match current:
        case n if n > t.high: "over"
        case n if n < t.low: "under"
        case _: "ok"

def main():
    analyzer = DataAnalyzer()
    
    # Test various values with guard clause classification
    test_values: list[int] = [25, 75, 150, -5, 100]
    for v in test_values:
        analyzer.add(v)
        p = priority_from_value(v)
        print(f"{v}:{p.value}")
    
    # Test with aggregated max value
    t1: Threshold = (20, 80)
    max_val = analyzer.get_max()
    print(max_val)
    status = status_for_threshold(t1, max_val)
    print(status)
    
    # Boundary condition tests
    p1 = priority_from_value(49)
    p2 = priority_from_value(50)
    p3 = priority_from_value(100)
    p4 = priority_from_value(101)
    print(p1.name)
    print(p2.name)
    print(p3.name)
    print(p4.name)

```

## Output

```
25:1
75:2
150:3
-5:0
100:2
150
over
P1
P2
P2
P3
```

## Timing

- Generation: 502.80s
- Execution: 5.22s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
