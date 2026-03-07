# Issue Report: output_mismatch

**Timestamp:** 2026-03-07T01:03:15.832661
**Type:** output_mismatch
**Feature Focus:** module_utils
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - demonstrates shape utilities and module imports
from shapes import Rectangle, Circle
from utils import scale_rectangle, compare_areas, sum_areas, ShapeStats

def main():
    stats: ShapeStats = ShapeStats()
    
    # Create some shapes
    rect1: Rectangle = Rectangle(4.0, 5.0)
    rect2: Rectangle = Rectangle(3.0, 3.0)
    circle1: Circle = Circle(2.0)
    circle2: Circle = Circle(5.0)
    
    # Test rectangle calculations
    print("Rectangle 1:")
    print(rect1.area())
    print(rect1.perimeter())
    stats.record_calculation()
    
    print("Rectangle 2:")
    print(rect2.area())
    print(rect2.perimeter())
    stats.record_calculation()
    
    # Test circle calculations
    print("\nCircle 1:")
    print(circle1.get_radius())
    print(circle1.area())
    print(circle1.circumference())
    stats.record_calculation()
    
    print("Circle 2:")
    print(circle2.area())
    stats.record_calculation()
    
    # Test utility functions
    print("\nComparison:")
    cmp_result: int = compare_areas(rect1.area(), rect2.area())
    print(cmp_result)
    
    # Test area summing
    areas_list: list[float] = [rect1.area(), rect2.area(), circle1.area()]
    total_area: float = sum_areas(areas_list)
    print(total_area)
    
    # Test stats
    print("\nStats:")
    print(stats.get_count())

```

## Error

```
AI explicitly reported mismatch
```

## Output Comparison

### Expected
```
Rectangle 1:
20.0
18.0
Rectangle 2:
9.0
12.0

Circle 1:
2.0
12.56636
12.56636
Circle 2:
78.53975

Comparison:
1

54.56636

Stats:
4

```

### Actual
```
Rectangle 1:
20.0
18.0
Rectangle 2:
9.0
12.0

Circle 1:
2.0
12.56636
12.56636
Circle 2:
78.53975

Comparison:
1
41.56636

Stats:
4
```

## Timing

- Generation: 143.15s
- Execution: 4.77s
