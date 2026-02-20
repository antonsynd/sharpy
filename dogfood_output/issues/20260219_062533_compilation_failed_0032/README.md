# Issue Report: compilation_failed

**Timestamp:** 2026-02-19T06:20:11.008079
**Type:** compilation_failed
**Feature Focus:** dotnet_type_usage
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Using multiple .NET System types together
# Tests: Import and usage of Math constants and functions from .NET
from system import Math

class ProgressTracker:
    current: float
    target: float

    def __init__(self, target_value: float):
        self.current = 0.0
        self.target = target_value

    def advance(self, amount: float) -> None:
        self.current = self.current + amount
        if self.current > self.target:
            self.current = self.target

    def percentage(self) -> float:
        if self.target == 0.0:
            return 0.0
        return (self.current / self.target) * 100.0

    def is_complete(self) -> bool:
        return self.current >= self.target

def floor_progress(tracker: ProgressTracker) -> float:
    pct: float = tracker.percentage()
    return Math.Floor(pct)

def ceiling_progress(tracker: ProgressTracker) -> float:
    pct: float = tracker.percentage()
    return Math.Ceiling(pct)

def calculate_circle_area(radius: float) -> float:
    return Math.PI * radius * radius

def main():
    # Create a progress tracker for target of 100 units
    tracker = ProgressTracker(100.0)

    # Simulate progress in increments
    for i in range(10):
        tracker.advance(12.5)
        pct: float = tracker.percentage()
        print(Math.Round(pct, 1))

    # Test floor and ceiling functions
    print("")

    # Reset tracker for floor/ceiling tests
    tracker2 = ProgressTracker(3.0)
    tracker2.advance(7.3)  # 7.3/3.0 * 100 = 243.33...

    # Test Floor
    floored: float = floor_progress(tracker2)
    print(floored)

    # Test Ceiling
    ceiled: float = ceiling_progress(tracker2)
    print(ceiled)

    # Demonstrate Math constants
    print("")

    # Calculate areas using PI
    radius1: float = 2.0
    radius2: float = 3.0
    radius3: float = 5.0

    area1: float = calculate_circle_area(radius1)
    area2: float = calculate_circle_area(radius2)
    area3: float = calculate_circle_area(radius3)

    print(Math.Round(area1, 2))
    print(Math.Round(area2, 2))
    print(Math.Round(area3, 2))

    # Demonstrate rounding
    print("")

    value: float = Math.PI
    print(Math.Floor(value))
    print(Math.Ceiling(value))
    print(Math.Round(value, 3))

    # Test with negative numbers
    print("")

    neg: float = -7.3
    print(Math.Floor(neg))
    print(Math.Ceiling(neg))
    print(Math.Round(neg))

# EXPECTED OUTPUT:
# 12.5
# 25.0
# 37.5
# 50.0
# 62.5
# 75.0
# 87.5
# 100.0
# 100.0
# 100.0
#
# 243.0
# 244.0
#
# 12.57
# 28.27
# 78.54
#
# 3.0
# 4.0
# 3.142
#
# -8.0
# -7.0
# -7.0
```

## Error

```
Assembly compilation failed:

error[CS0117]: 'Math' does not contain a definition for 'Pi'
  --> /tmp/tmp8tazrsg9/dogfood_test.spy:35:21
    |
 35 |     return Math.PI * radius * radius
    |                     ^
    |

error[CS0117]: 'Math' does not contain a definition for 'Pi'
  --> /tmp/tmp8tazrsg9/dogfood_test.spy:81:29
    |
 81 |     value: float = Math.PI
    |                           ^
    |


```

## Generated C#

```csharp
Generated C# code written to: /tmp/tmp8tazrsg9/dogfood_test.cs

```

## Timing

- Generation: 303.48s
- Execution: 4.42s
