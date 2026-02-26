# Successful Dogfood Run

**Timestamp:** 2026-02-25T23:22:57.084476
**Feature Focus:** result_type
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Try expression with Result chaining for safe parsing
# Tests: try expression, Result[T !E], map, map_err, unwrap_or

class SafeParser:
    def parse_int(self, s: str) -> int !str:
        # Convert exception to Result using try expression
        result: int !Exception = try int(s)
        # Transform error to custom message using map_err
        return result.map_err(lambda e: f"invalid integer: '{s}'")

    def parse_float(self, s: str) -> float !str:
        result: float !Exception = try float(s)
        return result.map_err(lambda e: f"invalid float: '{s}'")

    def compute_scaled(self, s: str, factor: float) -> float !str:
        # Chain operations: parse then scale using map
        parsed: int !str = self.parse_int(s)
        return parsed.map(lambda n: float(n) * factor)

def main():
    parser = SafeParser()

    # Valid integer parsing
    r1: int !str = parser.parse_int("42")
    print(r1.unwrap_or(0))

    # Invalid integer - should return error
    r2: int !str = parser.parse_int("abc")
    print(r2.unwrap_or(-1))

    # Valid float parsing
    r3: float !str = parser.parse_float("3.14")
    print(r3.unwrap_or(0.0))

    # Chained computation
    r4: float !str = parser.compute_scaled("10", 2.5)
    print(r4.unwrap_or(0.0))

    # Failed computation chain
    r5: float !str = parser.compute_scaled("xyz", 2.0)
    print(r5.unwrap_or(-1.0))
```

## Output

```
42
-1
3.14
25.0
-1.0
```

## Timing

- Generation: 179.58s
- Execution: 4.54s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
