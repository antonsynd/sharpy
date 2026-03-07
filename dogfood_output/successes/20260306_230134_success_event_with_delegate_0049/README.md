# Successful Dogfood Run

**Timestamp:** 2026-03-06T22:58:57.635368
**Feature Focus:** event_with_delegate
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Temperature monitoring system with events and custom delegates
# Tests: events with delegates, inheritance, type aliases, lambdas

type SensorId = str

delegate TemperatureCallback(sensor: SensorId, celsius: float) -> None

@abstract
class AlertHandler:
    name: str
    
    def __init__(self, handler_name: str):
        self.name = handler_name
    
    @virtual
    def on_temperature(self, sensor: SensorId, celsius: float) -> None:
        print(f"Alert from {sensor}: {celsius}C")

class SeverityAlertHandler(AlertHandler):
    warning_level: float
    critical_level: float
    
    def __init__(self, name: str, warn: float, crit: float):
        super().__init__(name)
        self.warning_level = warn
        self.critical_level = crit
    
    @override
    def on_temperature(self, sensor: SensorId, celsius: float) -> None:
        if celsius >= self.critical_level:
            print(f"CRITICAL at {sensor}")
        elif celsius >= self.warning_level:
            print(f"WARNING at {sensor}")
        else:
            super().on_temperature(sensor, celsius)

class TemperatureMonitor:
    sensor_name: SensorId
    current: float
    event on_change: TemperatureCallback
    
    def __init__(self, name: SensorId, initial: float):
        self.sensor_name = name
        self.current = initial
    
    def update(self, new_temp: float) -> None:
        delta = new_temp - self.current
        if delta < 0:
            delta = -delta
        self.current = new_temp
        if delta > 3.0:
            self.on_change?.invoke(self.sensor_name, new_temp)

def log_temperature(sensor: SensorId, celsius: float) -> None:
    print(f"Logged {celsius}")

def main():
    # Create monitors and handler
    kitchen = TemperatureMonitor("Kitchen", 20.0)
    living = TemperatureMonitor("Living", 22.0)
    handler = SeverityAlertHandler("HVAC", 25.0, 30.0)
    
    # Subscribe handlers
    kitchen.on_change += log_temperature
    kitchen.on_change += handler.on_temperature
    living.on_change += lambda s, t: print(f"Lambda sees {s}")
    
    print("Phase 1")
    kitchen.update(21.5)  # Small change, no event
    kitchen.update(26.0)  # Large change, WARNING
    
    print("Phase 2")
    living.update(35.0)  # Lambda triggered
    
    print("Phase 3")
    kitchen.on_change -= log_temperature
    kitchen.update(32.0)  # CRITICAL, no log
    
    print("Complete")

```

## Output

```
Phase 1
Logged 26.0
WARNING at Kitchen
Phase 2
Lambda sees Living
Phase 3
CRITICAL at Kitchen
Complete
```

## Timing

- Generation: 144.67s
- Execution: 5.53s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
