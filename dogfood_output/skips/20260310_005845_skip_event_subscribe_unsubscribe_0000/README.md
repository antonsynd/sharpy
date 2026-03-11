# Skipped Dogfood Run

**Timestamp:** 2026-03-10T00:49:38.252697
**Skip Reason:** Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0104]: Expected Colon, got Newline
  --> /tmp/tmprdfcibkm/dogfood_test.spy:7:42
    |
  7 |     property get sensor_name(self) -> str
    |                                          ^
    |

error[SPY0104]: Expected Colon, got Newline
  --> /tmp/tmprdfcibkm/dogfood_test.spy:8:44
    |
  8 |     property get temperature(self) -> float
    |                                            ^
    |


**Feature Focus:** event_subscribe_unsubscribe
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Event subscription/unsubscription with temperature monitoring system
# Tests multiple handlers, dynamic subscribe/unsubscribe, and event lifecycle

from system import EventHandler, EventArgs

class TempEventArgs(EventArgs):
    property get sensor_name(self) -> str
    property get temperature(self) -> float

    def __init__(self, name: str, temp: float):
        self._sensor_name = name
        self._temperature = temp

@abstract
class BaseSensor:
    _name: str

    def __init__(self, name: str):
        self._name = name

    property get name(self) -> str:
        return self._name

class TemperatureSensor(BaseSensor):
    _current: float
    _threshold: float
    _alert_count: int
    event on_threshold_exceeded: EventHandler[TempEventArgs]

    def __init__(self, name: str, threshold: float):
        super().__init__(name)
        self._current = 20.0
        self._threshold = threshold
        self._alert_count = 0

    property get current_temp(self) -> float:
        return self._current

    def update(self, new_temp: float):
        self._current = new_temp
        if self._current > self._threshold:
            self._alert_count += 1
            args = TempEventArgs(self._name, self._current)
            self.on_threshold_exceeded?.invoke(self, args)

    def get_alert_count(self) -> int:
        return self._alert_count

class SensorLogger:
    _logs: list[str]
    _active: bool
    _logger_name: str

    def __init__(self, name: str):
        self._logs = []
        self._active = True
        self._logger_name = name

    def enable(self):
        self._active = True

    def disable(self):
        self._active = False

    def get_logs(self) -> list[str]:
        return self._logs

    def on_alert(self, sender: object, args: TempEventArgs):
        if self._active:
            msg = args.sensor_name + ":" + str(args.temperature)
            self._logs.append(msg)

def main():
    # Create sensor with 25.0 threshold
    sensor = TemperatureSensor("LivingRoom", 25.0)

    # Create two loggers with different behaviors
    logger_a = SensorLogger("A")
    logger_b = SensorLogger("B")

    # Subscribe both loggers
    sensor.on_threshold_exceeded += logger_a.on_alert
    sensor.on_threshold_exceeded += logger_b.on_alert

    # First alert - both should receive
    sensor.update(26.5)

    # Disable logger_b and update again
    logger_b.disable()
    sensor.update(27.0)

    # Unsubscribe logger_a and update
    sensor.on_threshold_exceeded -= logger_a.on_alert
    sensor.update(28.0)

    # Re-enable logger_b, subscribe again, update
    logger_b.enable()
    sensor.on_threshold_exceeded += logger_b.on_alert
    sensor.update(29.0)

    # Unsubscribe logger_b and final update (no one subscribed)
    sensor.on_threshold_exceeded -= logger_b.on_alert
    sensor.update(30.0)

    # Print results
    logs_a = logger_a.get_logs()
    logs_b = logger_b.get_logs()

    print(len(logs_a))
    if len(logs_a) > 0:
        print(logs_a[0])
    if len(logs_a) > 1:
        print(logs_a[1])

    print(len(logs_b))
    if len(logs_b) > 0:
        print(logs_b[0])
    if len(logs_b) > 1:
        print(logs_b[1])

    print(sensor.get_alert_count())

```

## Timing

- Generation: 535.77s

## Notes

This iteration was skipped because the generated code didn't pass validation.
This is typically due to the AI generating code with unsupported features
or syntax that doesn't match the Sharpy spec (phases 0.1.0-0.2.6).

This output is saved for inspection to help improve prompting.
