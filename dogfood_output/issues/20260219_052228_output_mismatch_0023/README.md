# Issue Report: output_mismatch

**Timestamp:** 2026-02-19T05:18:04.645890
**Type:** output_mismatch
**Feature Focus:** class_with_init
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
class Temperature:
    _celsius: float
    _conversions: list[str]

    def __init__(self, celsius: float):
        self._celsius = celsius
        self._conversions = [f"Init: {celsius}C"]

    def to_fahrenheit(self) -> float:
        result: float = self._celsius * 9.0 / 5.0 + 32.0
        self._conversions.append(f"F: {result:.1f}")
        return result

    def to_kelvin(self) -> float:
        result: float = self._celsius + 273.15
        self._conversions.append(f"K: {result:.2f}")
        return result

    @virtual
    def display_log(self) -> None:
        for entry in self._conversions:
            print(entry)

class WeatherStation(Temperature):
    location: str

    def __init__(self, celsius: float, location: str):
        super().__init__(celsius)
        self.location = location

    @override
    def display_log(self) -> None:
        print(f"Station: {self.location}")
        super().display_log()

def main():
    station: WeatherStation = WeatherStation(25.0, "Beach")
    f: float = station.to_fahrenheit()
    k: float = station.to_kelvin()
    station.display_log()

# EXPECTED OUTPUT:
# Station: Beach
# Init: 25.0C
# F: 77.0
# K: 298.15
```

## Error

```
AI explicitly reported mismatch
```

## Output Comparison

### Expected
```
Station: Beach
Init: 25.0C
F: 77.0
K: 298.15

```

### Actual
```
Station: Beach
Init: 25C
F: 77.0
K: 298.15
```

## Timing

- Generation: 218.87s
- Execution: 4.53s
