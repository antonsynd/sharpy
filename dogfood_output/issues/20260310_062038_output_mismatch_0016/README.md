# Issue Report: output_mismatch

**Timestamp:** 2026-03-10T06:18:48.775740
**Type:** output_mismatch
**Feature Focus:** event_subscribe_unsubscribe
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Event subscribe and unsubscribe with temperature monitoring
# Tests: event declaration, += subscribe, -= unsubscribe, ?.invoke(), multiple handlers

delegate ThresholdHandler(current: float, threshold: float) -> None

class TemperatureMonitor:
    high_threshold: float
    low_threshold: float
    
    def __init__(self, high: float, low: float):
        self.high_threshold = high
        self.low_threshold = low
    
    event on_high_temp: ThresholdHandler
    event on_low_temp: ThresholdHandler
    
    def check_temperature(self, temp: float) -> None:
        if temp > self.high_threshold:
            self.on_high_temp?.invoke(temp, self.high_threshold)
        elif temp < self.low_threshold:
            self.on_low_temp?.invoke(temp, self.low_threshold)

def main():
    monitor = TemperatureMonitor(30.0, 10.0)
    
    # Subscribe handlers
    monitor.on_high_temp += lambda t, th: print(f"Hot: {t} > {th}")
    monitor.on_low_temp += lambda t, th: print(f"Cold: {t} < {th}")
    
    # Test high threshold
    monitor.check_temperature(35.0)
    
    # Unsubscribe low handler
    monitor.on_low_temp -= lambda t, th: print(f"Cold: {t} < {th}")
    
    # Test low threshold (should not print - handler unsubscribed)
    monitor.check_temperature(5.0)
    
    # Re-subscribe with different handler
    monitor.on_low_temp += lambda t, th: print(f"Freezing alert: {t}")
    
    # Test low again
    monitor.check_temperature(5.0)

```

## Error

```
AI explicitly reported mismatch
```

## Output Comparison

### Expected
```
Hot: 35.0 > 30.0
Freezing alert: 5.0

```

### Actual
```
Hot: 35 > 30
Cold: 5 < 10
Cold: 5 < 10
Freezing alert: 5
```

## Timing

- Generation: 75.30s
- Execution: 4.96s
