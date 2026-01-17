# Successful Dogfood Run

**Timestamp:** 2026-01-17T00:46:12.158929
**Feature Focus:** class_with_init
**Complexity:** medium
**Backend:** claude

## Generated Sharpy Code

```python
# Test: class_with_init - A temperature converter class with initialization and methods

class TemperatureConverter:
    celsius: float
    name: str

    def __init__(self, initial_celsius: float, scale_name: str):
        self.celsius = initial_celsius
        self.name = scale_name
        print(self.name)

    def to_fahrenheit(self) -> float:
        return self.celsius * 9.0 / 5.0 + 32.0

    def to_kelvin(self) -> float:
        return self.celsius + 273.15

    def adjust(self, delta: float) -> None:
        self.celsius += delta

    def get_celsius(self) -> float:
        return self.celsius


converter = TemperatureConverter(0.0, "Water freezing point")
print(converter.get_celsius())
print(converter.to_fahrenheit())
print(converter.to_kelvin())

converter.adjust(100.0)
print(converter.get_celsius())
print(converter.to_fahrenheit())

second = TemperatureConverter(25.0, "Room temperature")
print(second.get_celsius())
print(second.to_kelvin())

# EXPECTED OUTPUT:
# Water freezing point
# 0
# 32
# 273.15
# 100
# 212
# Room temperature
# 25
# 298.15
```

## Output

```
Successfully compiled to: /var/folders/6r/tsfytt4x6s1cl4_t14c961040000gn/T/dogfood_test_aef4df7008044e48a2b29f44c60a39d1.exe

=== Running Program ===

Water freezing point
0
32
273.15
100
212
Room temperature
25
298.15
```

## Timing

- Generation: 7.28s
- Execution: 1.42s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
