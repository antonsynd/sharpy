# Skipped Dogfood Run

**Timestamp:** 2026-02-25T12:31:48.654488
**Skip Reason:** Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0220]: Cannot assign type 'float?' to variable of type 'float'
  --> /tmp/tmp0bakozxt/dogfood_test.spy:14:5
    |
 14 |     v: float = value.numeric_value
    |     ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
    |


**Feature Focus:** type_narrowing
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
struct ConfigValue:
    name: str
    numeric_value: float?
    category: str
    
    def __init__(self, name: str, numeric_value: float?, category: str):
        self.name = name
        self.numeric_value = numeric_value
        self.category = category

def validate_and_scale(value: ConfigValue) -> float:
    if value.numeric_value is None:
        return -1.0
    v: float = value.numeric_value
    if v > 1000.0:
        return v / 1000.0
    elif v > 0.0:
        return v
    else:
        return 0.0

def categorize(scaled: float) -> str:
    if scaled >= 10.0:
        return "large"
    elif scaled >= 1.0:
        return "medium"
    elif scaled >= 0.0:
        return "small"
    else:
        return "invalid"

def process_config(item: ConfigValue) -> str:
    scaled: float = validate_and_scale(item)
    size: str = categorize(scaled)
    return f"{item.name}:{size}:{scaled}"

def main():
    high: ConfigValue = ConfigValue("throughput", Some(5000.5), "perf")
    mid: ConfigValue = ConfigValue("latency", Some(150.0), "perf")
    low: ConfigValue = ConfigValue("accuracy", Some(0.85), "ml")
    none_val: ConfigValue = ConfigValue("disabled", None(), "debug")
    print(process_config(high))
    print(process_config(mid))
    print(process_config(low))
    print(process_config(none_val))

# EXPECTED OUTPUT:
# throughput:medium:5.0005
# latency:large:150.0
# accuracy:small:0.85
# disabled:invalid:-1.0
```

## Timing

- Generation: 1108.34s

## Notes

This iteration was skipped because the generated code didn't pass validation.
This is typically due to the AI generating code with unsupported features
or syntax that doesn't match the Sharpy spec (phases 0.1.0-0.1.18).

This output is saved for inspection to help improve prompting.
