# Issue Report: output_mismatch

**Timestamp:** 2026-02-25T04:50:59.205199
**Type:** output_mismatch
**Feature Focus:** result_type
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Test Result types with inheritance, enums, and method chaining

enum ParseKind:
    INTEGER = 1
    FLOAT = 2

enum ParseError:
    INVALID = 1
    RANGE = 2
    TYPE = 3

type IntResult = int !ParseError
type FloatResult = float !ParseError

@abstract
class Parser:
    @abstract
    def parse(self, raw: str) -> IntResult:
        ...

    def safe_divide(self, a: int, b: int) -> FloatResult:
        if b == 0:
            return Err(ParseError.RANGE)
        return Ok((a to float) / (b to float))

    def expect_positive(self, r: IntResult) -> IntResult:
        return r.map(lambda x: x if x > 0 else -x)

@abstract
class FloatParser(Parser):
    precision: int

    def __init__(self, prec: int):
        self.precision = prec

    @override
    def parse(self, raw: str) -> IntResult:
        if len(raw) == 0:
            return Err(ParseError.INVALID)
        return Ok(42)

class BoundedParser(FloatParser):
    max_limit: int

    def __init__(self, limit: int, prec: int):
        super().__init__(prec)
        self.max_limit = limit

    @override
    def parse(self, raw: str) -> IntResult:
        base: IntResult = super().parse(raw)
        checked: IntResult = base.map(lambda x: x if x <= self.max_limit else -1)
        temp: int = checked.unwrap_or(-1)
        if temp == -1:
            return Err(ParseError.RANGE)
        return Ok(temp)

    def format_result(self, r: FloatResult) -> str:
        return r.map(lambda f: f"{f:.{self.precision}f}").unwrap_or("error")

def main():
    p = BoundedParser(100, 2)

    # Test Ok path with map
    val: IntResult = p.parse("test")
    doubled: IntResult = val.map(lambda x: x * 2)
    print(doubled.unwrap_or(-1))
    print(val.unwrap_or(999))

    err_val: IntResult = Err(ParseError.INVALID)
    mapped: IntResult = err_val.map_err(lambda e: ParseError.RANGE if e == ParseError.INVALID else e)
    print(mapped.unwrap_or(-2))

    div_result: FloatResult = p.safe_divide(10, 2)
    print(div_result.unwrap_or(0.0))

    neg_input: IntResult = Ok(-5)
    pos: IntResult = p.expect_positive(neg_input)
    print(pos.unwrap_or(-1))

    ok_float: FloatResult = Ok(3.14159)
    print(p.format_result(ok_float))

    err_float: FloatResult = Err(ParseError.TYPE)
    print(p.format_result(err_float))

# EXPECTED OUTPUT:
# 84
# 42
# -2
# 5.0
# 5
# 3.14
# error
```

## Error

```
AI verification backend failure
```

## Output Comparison

### Expected
```
84
42
-2
5.0
5
3.14
error

```

### Actual
```
84
42
-2
5.0
5
3.14159
error
```

## Timing

- Generation: 1076.65s
- Execution: 4.65s
