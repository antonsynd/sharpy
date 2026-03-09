# Issue Report: compilation_failed

**Timestamp:** 2026-03-08T20:58:45.541883
**Type:** compilation_failed
**Feature Focus:** optional_unwrap
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Complex optional unwrapping in a data processing pipeline
# Tests: Optional.unwrap(), unwrap_or(), map(), type narrowing with is not None

type DataId = int
type SensorReading = float

@abstract
class DataSource:
    @abstract
    def fetch(self) -> SensorReading?: ...

class SensorData(DataSource):
    _cached: SensorReading?
    _backups: list[SensorReading?]

    def __init__(self):
        self._cached = None()
        self._backups = []

    @override
    def fetch(self) -> SensorReading?:
        if self._cached is not None:
            return self._cached
        return None()

    def update(self, value: SensorReading?) -> None:
        self._cached = value
        self._backups.append(value)

    def get_average(self) -> float:
        total = 0.0
        count = 0
        for reading in self._backups:
            if reading is not None:
                total += reading
                count += 1
        if count > 0:
            return total / count
        return 0.0

    def get_backups(self) -> list[SensorReading?]:
        return self._backups

class DataProcessor:
    _transform: (float) -> float

    def __init__(self, transform: (float) -> float):
        self._transform = transform

    def process(self, value: float) -> float?:
        if value > 0.0:
            return Some(self._transform(value))
        return None()

def get_data_or_default(id: DataId, sensor: SensorData) -> SensorReading:
    raw = sensor.fetch()
    if raw is not None:
        return raw
    return 0.0

def main():
    sensor = SensorData()
    processor = DataProcessor(lambda x: x * 2.0)

    # Test unwrap_or with unpopulated data
    val = sensor.fetch()
    print(val.unwrap_or(-1.0))

    # Add data and test unwrap
    sensor.update(Some(10.5))
    result = sensor.fetch()
    if result is not None:
        print(result.unwrap())

    # Test map on optional
    mapped = processor.process(5.0)
    if mapped is not None:
        print(mapped.unwrap_or(0.0))

    # Add more readings
    sensor.update(Some(20.0))
    sensor.update(None())
    sensor.update(Some(30.5))
    print(sensor.get_average())

    # Test unwrap with multiple values
    for reading in sensor.get_backups():
        unwrapped = reading.unwrap_or(99.9)
        print(unwrapped)

```

## Error

```
Assembly compilation failed:

error[CS1929]: 'double' does not contain a definition for 'Unwrap' and the best extension method overload 'TaskExtensions.Unwrap(Task<Task>)' requires a receiver of type 'System.Threading.Tasks.Task<System.Threading.Tasks.Task>'
  --> /tmp/tmpcqonvqwg/dogfood_test.spy:73:43
    |
 73 |         print(result.unwrap())
    |                               ^
    |

error[CS1061]: 'double' does not contain a definition for 'UnwrapOr' and no accessible extension method 'UnwrapOr' accepting a first argument of type 'double' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpcqonvqwg/dogfood_test.spy:78:59
    |
 78 |         print(mapped.unwrap_or(0.0))
    |                                     ^
    |


```

## Generated C#

```csharp
Generated C# code written to: /tmp/tmpcqonvqwg/dogfood_test.cs

```

## Timing

- Generation: 324.35s
- Execution: 5.04s
