# Issue Report: output_mismatch

**Timestamp:** 2026-02-24T05:09:29.192986
**Type:** output_mismatch
**Feature Focus:** try_expression
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
class DataParser:
    base: int
    
    def __init__(self, base: int):
        self.base = base
    
    def parse_positive(self, s: str) -> int:
        n = int(s)
        if n <= 0:
            raise ValueError("Number must be positive")
        return n * self.base

@abstract
class ValueTransformer:
    @abstract
    def transform(self, value: int) -> int: ...

class SafeTransformer(ValueTransformer):
    divisor: int
    
    def __init__(self, divisor: int):
        self.divisor = divisor
    
    @override
    def transform(self, value: int) -> int:
        if value % 2 == 0:
            raise RuntimeError("Even values not allowed")
        return value // self.divisor

class PipelineConfig:
    threshold: int
    fallback: int
    
    def __init__(self, threshold: int, fallback: int):
        self.threshold = threshold
        self.fallback = fallback
    
    def check_limit(self, value: int) -> int:
        if value > self.threshold:
            raise OverflowError(f"Value {value} exceeds threshold {self.threshold}")
        return value

def process_data(parser: DataParser, transformer: SafeTransformer, config: PipelineConfig, inputs: list[str]) -> list[int]:
    results: list[int] = []
    for raw in inputs:
        try:
            parsed = parser.parse_positive(raw)
            try:
                transformed = transformer.transform(parsed)
                try:
                    final = config.check_limit(transformed)
                    results.append(final)
                    print(f"Processed: {parsed} -> {final}")
                except OverflowError:
                    print(f"Limit exceeded, using fallback: {config.fallback}")
                    results.append(config.fallback)
            except RuntimeError:
                print(f"Transform failed for: {parsed}")
        except ValueError:
            print(f"Parse failed for: {raw}")
    return results

def compute_stats(values: list[int]) -> tuple[int, int, float]:
    total: int = sum(values)
    n: int = len(values)
    avg_val: float = 0.0
    if n > 0:
        avg_val = total / n
    return (total, n, avg_val)

def main():
    parser: DataParser = DataParser(10)
    transformer: SafeTransformer = SafeTransformer(3)
    config: PipelineConfig = PipelineConfig(500, 999)
    test_data: list[str] = ["10", "-5", "25", "0", "150", "abc", "30"]
    
    print("Starting pipeline processing")
    processed: list[int] = process_data(parser, transformer, config, test_data)
    print(f"Successful results: {len(processed)}")
    stats: tuple[int, int, float] = compute_stats(processed)
    print(f"Total: {stats[0]}")
    print(f"Count: {stats[1]}")
    print(f"Average: {stats[2]}")
    
    try:
        config.check_limit(1000)
        print("Unexpected success")
    except OverflowError:
        print("Final limit check failed as expected")

# EXPECTED OUTPUT:
# Processed: 250 -> 83
# Processed: 300 -> 100
# Total: 1182
# Count: 5
# Average: 236.4
```

## Error

```
AI verification backend failure
```

## Output Comparison

### Expected
```
Starting pipeline processing
Processed: 250 -> 83
Processed: 300 -> 100
Limit exceeded, using fallback: 999
Transform failed for: 1000
Parse failed for: -5
Parse failed for: 0
Parse failed for: abc
Successful results: 5
Total: 1182
Count: 5
Average: 236.4
Final limit check failed as expected

```

### Actual
```
Starting pipeline processing
Transform failed for: 100
Parse failed for: -5
Transform failed for: 250
Parse failed for: 0
Transform failed for: 1500
Parse failed for: abc
Transform failed for: 300
Successful results: 0
Total: 0
Count: 0
Average: 0.0
Final limit check failed as expected
```

## Timing

- Generation: 275.14s
- Execution: 4.71s
