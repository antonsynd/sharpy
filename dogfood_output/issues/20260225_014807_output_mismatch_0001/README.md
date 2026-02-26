# Issue Report: output_mismatch

**Timestamp:** 2026-02-25T01:33:36.141066
**Type:** output_mismatch
**Feature Focus:** optional_unwrap
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
type ConfigValue = str

@abstract
class ConfigLoader:
    @abstract
    def get_value(self, key: str) -> ConfigValue?: ...

class IntConfigLoader(ConfigLoader):
    _fallback: int

    def __init__(self, fallback: int):
        self._fallback = fallback

    @override
    def get_value(self, key: str) -> ConfigValue?:
        if key == "port":
            return Some("8080")
        return None()

class DataTransformer:
    def transform(self, val: str) -> float?:
        return Some(float(val))

def main():
    loader = IntConfigLoader(3000)
    transformer = DataTransformer()

    raw = loader.get_value("port")
    if raw is not None:
        print(f"raw: {raw}")
        parsed = transformer.transform(raw).unwrap_or(0.0)
        print(f"parsed: {parsed}")
    else:
        print("missing")

    direct = loader.get_value("missing")
    default_str = direct.unwrap_or("fallback")
    print(default_str)

    num_opt: int? = Some(25)
    doubled = num_opt.map(lambda x: x * 2)
    print(doubled.unwrap())

# EXPECTED OUTPUT:
# raw: 8080
# parsed: 8080.0
# missing
# fallback
# 50
```

## Error

```
AI explicitly reported mismatch
```

## Output Comparison

### Expected
```
raw: 8080
parsed: 8080.0
missing
fallback
50

```

### Actual
```
raw: 8080
parsed: 8080
fallback
50
```

## Timing

- Generation: 754.91s
- Execution: 4.57s
