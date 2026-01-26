# Issue Report: compilation_failed

**Timestamp:** 2026-01-25T23:13:49.131659
**Type:** compilation_failed
**Feature Focus:** dotnet_import
**Complexity:** medium
**Backend:** claude

## Generated Sharpy Code

```python
# Test: .NET interop - importing and using System.Math for calculations
# Tests: from system import X, calling .NET static methods, .NET type compatibility

from system import Math

class CircleCalculator:
    radius: float

    def __init__(self, r: float):
        self.radius = r

    def get_area(self) -> float:
        # Use Math.PI and Math.Pow from .NET
        return Math.PI * Math.Pow(self.radius, 2.0)

    def get_circumference(self) -> float:
        return 2.0 * Math.PI * self.radius

def calculate_square_root(value: float) -> float:
    return Math.Sqrt(value)

def main():
    print(f"Math.PI: {Math.PI:.5f}")
    
    circle = CircleCalculator(5.0)
    area: float = circle.get_area()
    circumference: float = circle.get_circumference()
    
    print(f"Circle radius: {circle.radius:.1f}")
    print(f"Circle area: {area:.2f}")
    print(f"Circle circumference: {circumference:.2f}")
    
    sqrt_result: float = calculate_square_root(16.0)
    print(f"Square root of 16: {sqrt_result:.1f}")

# EXPECTED OUTPUT:
# Math.PI: 3.14159
# Circle radius: 5.0
# Circle area: 78.54
# Circle circumference: 31.42
# Square root of 16: 4.0
```

## Error

```
Assembly compilation failed:
  dogfood_test.cs(36,20): error CS0234: The type or namespace name 'PI' does not exist in the namespace 'Sharpy.Math' (are you missing an assembly reference?)
  dogfood_test.cs(36,30): error CS0234: The type or namespace name 'Pow' does not exist in the namespace 'Sharpy.Math' (are you missing an assembly reference?)
  dogfood_test.cs(14,20): error CS0234: The type or namespace name 'Sqrt' does not exist in the namespace 'Sharpy.Math' (are you missing an assembly reference?)
  dogfood_test.cs(41,24): error CS0234: The type or namespace name 'PI' does not exist in the namespace 'Sharpy.Math' (are you missing an assembly reference?)
  dogfood_test.cs(19,59): error CS0234: The type or namespace name 'PI' does not exist in the namespace 'Sharpy.Math' (are you missing an assembly reference?)

```

## Timing

- Generation: 7.60s
- Execution: 1.58s
