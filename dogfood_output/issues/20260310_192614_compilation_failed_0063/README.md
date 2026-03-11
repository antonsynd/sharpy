# Issue Report: compilation_failed

**Timestamp:** 2026-03-10T19:15:44.482483
**Type:** compilation_failed
**Feature Focus:** event_with_inheritance
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Event inheritance with property setters and multiple event types
# Tests: virtual events, property setter overrides triggering events,
# multiple event types in hierarchy, event handler chaining
# Focus: Events declared at different inheritance levels with actual behavior

delegate ChangeHandler(old_val: int, new_val: int)
delegate ThresholdHandler(name: str, limit: int)

@abstract
class ValueMonitor:
    _value: int = 0

    @virtual
    event on_update: ChangeHandler

    @abstract
    property set tracked_value(self, v: int): ...

class ThresholdMonitor(ValueMonitor):
    monitor_name: str
    upper_limit: int
    _breached: bool = False

    event on_threshold_crossed: ThresholdHandler

    def __init__(self, name: str, limit: int):
        self.monitor_name = name
        self.upper_limit = limit

    @override
    property set tracked_value(self, v: int):
        prev = self._value
        self._value = v
        # Notify change first
        self.on_update?.invoke(prev, v)
        # Then check threshold
        if v >= self.upper_limit and not self._breached:
            self._breached = True
            self.on_threshold_crossed?.invoke(self.monitor_name, self.upper_limit)

class ResettableMonitor(ThresholdMonitor):
    crossed_count: int = 0

    def __init__(self, name: str, limit: int):
        super().__init__(name, limit)

    @override
    property set tracked_value(self, v: int):
        prev = self._value
        self._value = v
        # Notify change first
        self.on_update?.invoke(prev, v)
        # Threshold check (different behavior - counts all crossings, not just first)
        if v >= self.upper_limit:
            self.crossed_count += 1
            self.on_threshold_crossed?.invoke(self.monitor_name, self.upper_limit)

    def reset(self) -> None:
        self._breached = False
        self._value = 0

class EventTracker:
    changes: int = 0
    crossovers: int = 0

    def track_change(self, o: int, n: int) -> None:
        self.changes += 1

    def on_cross(self, name: str, lim: int) -> None:
        self.crossovers += 1

def main():
    # Create instance of most derived class
    rm: ResettableMonitor = ResettableMonitor("Sensor-A", 50)
    tracker: EventTracker = EventTracker()

    # Subscribe to events at different inheritance levels
    rm.on_update += tracker.track_change
    rm.on_threshold_crossed += tracker.on_cross

    # First: below threshold - only change event fires
    rm.tracked_value = 25

    # Second: crosses threshold - both events fire
    rm.tracked_value = 55

    # Third: already crossed - both fire again
    rm.tracked_value = 60

    # Reset and test again
    rm.reset()
    rm.tracked_value = 40

    # Cross again
    rm.tracked_value = 75

    print(tracker.changes)
    print(tracker.crossovers)
    print(rm.crossed_count)

```

## Error

```
Assembly compilation failed:

error[CS0103]: The name 'v' does not exist in the current context
  --> /tmp/tmpvl90n8if/dogfood_test.spy:33:31
    |
 33 |         self._value = v
    |                        ^
    |

error[CS0070]: The event 'DogfoodTest.ValueMonitor.OnUpdate' can only appear on the left hand side of += or -= (except when used from within the type 'DogfoodTest.ValueMonitor')
  --> /tmp/tmpvl90n8if/dogfood_test.spy:35:22
    |
 35 |         self.on_update?.invoke(prev, v)
    |                      ^
    |

error[CS0103]: The name 'v' does not exist in the current context
  --> /tmp/tmpvl90n8if/dogfood_test.spy:35:45
    |
 35 |         self.on_update?.invoke(prev, v)
    |                                        ^
    |

error[CS0103]: The name 'v' does not exist in the current context
  --> /tmp/tmpvl90n8if/dogfood_test.spy:37:21
    |
 37 |         if v >= self.upper_limit and not self._breached:
    |                     ^
    |

error[CS0103]: The name 'v' does not exist in the current context
  --> /tmp/tmpvl90n8if/dogfood_test.spy:50:31
    |
 50 |         self._value = v
    |                        ^
    |

error[CS0070]: The event 'DogfoodTest.ValueMonitor.OnUpdate' can only appear on the left hand side of += or -= (except when used from within the type 'DogfoodTest.ValueMonitor')
  --> /tmp/tmpvl90n8if/dogfood_test.spy:52:22
    |
 52 |         self.on_update?.invoke(prev, v)
    |                      ^
    |

error[CS0103]: The name 'v' does not exist in the current context
  --> /tmp/tmpvl90n8if/dogfood_test.spy:52:45
    |
 52 |         self.on_update?.invoke(prev, v)
    |                                        ^
    |

error[CS0103]: The name 'v' does not exist in the current context
  --> /tmp/tmpvl90n8if/dogfood_test.spy:54:21
    |
 54 |         if v >= self.upper_limit:
    |                     ^
    |

error[CS0070]: The event 'DogfoodTest.ThresholdMonitor.OnThresholdCrossed' can only appear on the left hand side of += or -= (except when used from within the type 'DogfoodTest.ThresholdMonitor')
  --> /tmp/tmpvl90n8if/dogfood_test.spy:56:26
    |
 56 |             self.on_threshold_crossed?.invoke(self.monitor_name, self.upper_limit)
    |                          ^
    |


```

## Generated C#

```csharp
Generated C# code written to: /tmp/tmpvl90n8if/dogfood_test.cs

```

## Timing

- Generation: 603.26s
- Execution: 5.02s
