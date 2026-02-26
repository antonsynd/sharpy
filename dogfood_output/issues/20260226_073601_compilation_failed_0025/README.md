# Issue Report: compilation_failed

**Timestamp:** 2026-02-26T07:34:59.559235
**Type:** compilation_failed
**Feature Focus:** result_type
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Result type chaining with map and map_err
# Demonstrates a computation pipeline where operations can fail
# and errors are transformed through the chain

def safe_divide(numerator: float, denominator: float) -> float !str:
    if denominator == 0.0:
        return Err("division by zero")
    return Ok(numerator / denominator)

def safe_sqrt(value: float) -> float !str:
    if value < 0.0:
        return Err("negative input")
    return Ok(value ** 0.5)

def compute_pipeline(a: float, b: float, c: float) -> float !str:
    # Chain operations: first divide, then sqrt the result
    step1: float !str = safe_divide(a, b)
    
    # Use map to transform success value, map_err to transform error
    step2: float !str = step1.map(lambda x: x * 2.0)
    
    # Chain another operation that could fail
    if step2 is not None:
        result: float !str = safe_sqrt(step2.unwrap())
        return result.map_err(lambda e: f"sqrt failed: {e}")
    
    return step2.map_err(lambda e: f"pipeline error: {e}")

def main():
    # Test successful chain
    result1: float !str = compute_pipeline(18.0, 2.0, 0.0)
    print(result1.unwrap_or(-1.0))
    
    # Test division by zero error
    result2: float !str = compute_pipeline(10.0, 0.0, 0.0)
    print(result2.unwrap_or(-1.0))
    
    # Test negative sqrt error (via division producing negative)
    result3: float !str = compute_pipeline(-8.0, 2.0, 0.0)
    print(result3.unwrap_or(-1.0))
    
    # Test unwrap_or with custom default
    result4: float !str = Err("failed")
    print(result4.unwrap_or(99.9))
```

## Error

```
Unhandled exception. System.InvalidOperationException: Called Unwrap on Err: division by zero
   at Sharpy.Result`2.Unwrap()
   at DogfoodTest.ComputePipeline(Double a, Double b, Double c) in /tmp/tmpa3wp6yim/dogfood_test.spy:line 24
   at DogfoodTest.Main() in /tmp/tmpa3wp6yim/dogfood_test.spy:line 35

```

## Compiler Output

```
4.242640687119285

```

## Generated C#

```csharp
Generated C# code written to: /tmp/tmpa3wp6yim/dogfood_test.cs

```

## Timing

- Generation: 47.88s
- Execution: 5.86s
