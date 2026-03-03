# Issue Report: output_mismatch

**Timestamp:** 2026-03-03T08:57:03.869225
**Type:** output_mismatch
**Feature Focus:** result_type
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Complex Result type test with class hierarchy and error handling patterns
# Tests: Result[T,E], unwrap_or, map, map_err, try expression

type ValidationError = str

@abstract
class Validator:
    @abstract
    def validate(self, value: int) -> bool ! ValidationError: ...

class RangeValidator(Validator):
    min_val: int
    max_val: int

    def __init__(self, min_val: int, max_val: int):
        self.min_val = min_val
        self.max_val = max_val

    @override
    def validate(self, value: int) -> bool ! ValidationError:
        if value < self.min_val:
            return Err(f"Value {value} below minimum {self.min_val}")
        if value > self.max_val:
            return Err(f"Value {value} above maximum {self.max_val}")
        return Ok(True)

class DivisorValidator(Validator):
    divisor: int

    def __init__(self, divisor: int):
        self.divisor = divisor

    @override
    def validate(self, value: int) -> bool ! ValidationError:
        if value % self.divisor != 0:
            return Err(f"Value {value} not divisible by {self.divisor}")
        return Ok(True)

def process_results(results: list[bool !str], default: bool) -> list[bool]:
    processed: list[bool] = []
    for r in results:
        processed.append(r.unwrap_or(default))
    return processed

def main():
    range_v = RangeValidator(10, 100)
    div_v = DivisorValidator(5)

    # Test validation chain with Result types
    test_values: list[int] = [15, 5, 105, 20, 7]
    for val in test_values:
        # Chain validate through multiple validators
        range_result = range_v.validate(val)

        # Use map_err to transform success, map_err for error context
        result = range_result.map_err(lambda e: f"Range check failed: {e}")

        # Unwrap with fallback
        is_valid = result.unwrap_or(False)
        print(is_valid)

    # Test list of Results by building list via append (not direct literals)
    results: list[bool !str] = []
    results.append(Ok(True))
    results.append(Err("bad"))
    results.append(Ok(True))
    results.append(Err("fail"))

    processed = process_results(results, False)
    print(processed[0])
    print(processed[1])
    print(processed[2])

    # Test try expression producing Result
    risky_add = try 100 // 10
    risky_div = try 50 // 0
    print(risky_add.unwrap_or(-1))
    print(risky_div.unwrap_or(-2))

```

## Error

```
AI explicitly reported mismatch
```

## Output Comparison

### Expected
```
True
True
False
True
True
True
False
True
10
-2

```

### Actual
```
True
False
False
True
False
True
False
True
10
2147483647
```

## Timing

- Generation: 275.02s
- Execution: 5.18s
