# Skipped Dogfood Run

**Timestamp:** 2026-02-25T07:43:49.179955
**Skip Reason:** Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0239]: Cannot unpack non-tuple type '<?>' in for loop
  --> /tmp/tmp7lv3qicg/dogfood_test.spy:42:13
    |
 42 |         for k, v in base.items():
    |             ^^^^
    |

error[SPY0200]: Undefined identifier 'v'
  --> /tmp/tmp7lv3qicg/dogfood_test.spy:43:30
    |
 43 |             adjusted: int = (v - self.threshold) * 2
    |                              ^
    |

error[SPY0222]: Type 'int' does not support operator '>=' with operand of type 'AlertLevel'
  --> /tmp/tmp7lv3qicg/dogfood_test.spy:44:16
    |
 44 |             if adjusted >= AlertLevel.LOW:
    |                ^^^^^^^^^^^^^^^^^^^^^^^^^^
    |

error[SPY0200]: Undefined identifier 'k'
  --> /tmp/tmp7lv3qicg/dogfood_test.spy:45:26
    |
 45 |                 filtered[k] = adjusted
    |                          ^
    |

error[SPY0239]: Cannot unpack non-tuple type '<?>' in for loop
  --> /tmp/tmp7lv3qicg/dogfood_test.spy:61:13
    |
 61 |         for k, v in temp.items():
    |             ^^^^
    |

error[SPY0200]: Undefined identifier 'v'
  --> /tmp/tmp7lv3qicg/dogfood_test.spy:62:16
    |
 62 |             if v > 105:
    |                ^
    |

error[SPY0200]: Undefined identifier 'k'
  --> /tmp/tmp7lv3qicg/dogfood_test.spy:63:42
    |
 63 |                 key: str = "slot_" + str(k)
    |                                          ^
    |

error[SPY0200]: Undefined identifier 'v'
  --> /tmp/tmp7lv3qicg/dogfood_test.spy:64:31
    |
 64 |                 result[key] = v
    |                               ^
    |


**Feature Focus:** dict_comprehension
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
type SensorReading = tuple[str, int]

enum AlertLevel:
    LOW = 10
    HIGH = 50

interface IMonitor:
    def get_alerts(self) -> int: ...

@abstract
class BaseProcessor:
    threshold: int

    def __init__(self, t: int):
        self.threshold = t

    @abstract
    def extract(self, data: list[SensorReading]) -> dict[str, int]: ...

class AlertProcessor(BaseProcessor, IMonitor):
    alert_count: int

    def __init__(self):
        super().__init__(20)
        self.alert_count = 0

    def get_alerts(self) -> int:
        return self.alert_count

    def _count_and_filter(self, data: list[SensorReading]) -> dict[str, int]:
        result: dict[str, int] = {}
        for n, v in data:
            if v > self.threshold:
                result[n] = v
                self.alert_count = self.alert_count + 1
        return result

    @override
    def extract(self, data: list[SensorReading]) -> dict[str, int]:
        base: dict[str, int] = self._count_and_filter(data)
        filtered: dict[str, int] = {}
        for k, v in base.items():
            adjusted: int = (v - self.threshold) * 2
            if adjusted >= AlertLevel.LOW:
                filtered[k] = adjusted
        return filtered

class StatsProcessor(BaseProcessor):
    def __init__(self):
        super().__init__(5)

    @override
    def extract(self, data: list[SensorReading]) -> dict[str, int]:
        temp: dict[int, int] = {}
        i: int = 0
        for n, v in data:
            if v > self.threshold:
                temp[i] = v + 100
            i = i + 1
        result: dict[str, int] = {}
        for k, v in temp.items():
            if v > 105:
                key: str = "slot_" + str(k)
                result[key] = v
        return result

def main():
    readings: list[SensorReading] = [("temp", 45), ("pressure", 15), ("humidity", 80), ("flow", 8)]
    
    lookup: dict[str, int] = {}
    for k, v in readings:
        lookup[k] = v
    
    alert_proc: AlertProcessor = AlertProcessor()
    alerts: dict[str, int] = alert_proc.extract(readings)
    
    stats_proc: StatsProcessor = StatsProcessor()
    stats: dict[str, int] = stats_proc.extract(readings)
    
    indexed: dict[int, str] = {}
    i: int = 0
    for n, v in readings:
        if i < 3 and v > 10:
            indexed[i] = n
        i = i + 1
    
    print(len(lookup))
    print(lookup["pressure"])
    print(alert_proc.get_alerts())
    print(len(alerts))
    print(alerts["humidity"])
    print(len(stats))
    print(stats["slot_0"])
    print(len(indexed))

# EXPECTED OUTPUT:
# 4
# 15
# 2
# 2
# 120
# 4
# 145
# 3
```

## Timing

- Generation: 753.62s

## Notes

This iteration was skipped because the generated code didn't pass validation.
This is typically due to the AI generating code with unsupported features
or syntax that doesn't match the Sharpy spec (phases 0.1.0-0.1.18).

This output is saved for inspection to help improve prompting.
