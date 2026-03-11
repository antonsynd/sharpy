# Issue Report: compilation_failed

**Timestamp:** 2026-03-10T07:43:55.416776
**Type:** compilation_failed
**Feature Focus:** raise_exception
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Complex exception hierarchy with re-raise and multiple exception types
# Features: Custom exceptions, inheritance, try/except/else/finally, re-raise

class ValidationError(Exception):
    code: int
    def __init__(self, message: str, code: int):
        super().__init__(message)
        self.code = code

class ValueTooSmallError(ValidationError):
    min_required: int
    def __init__(self, value: int, minimum: int):
        super().__init__(f"Value {value} below minimum {minimum}", 1001)
        self.min_required = minimum

class ValueTooLargeError(ValidationError):
    max_allowed: int
    def __init__(self, value: int, maximum: int):
        super().__init__(f"Value {value} exceeds maximum {maximum}", 1002)
        self.max_allowed = maximum

class RangeValidator:
    min_val: int
    max_val: int
    def __init__(self, minimum: int, maximum: int):
        self.min_val = minimum
        self.max_val = maximum
    
    def validate(self, value: int) -> ValidationError?:
        if value < self.min_val:
            return ValueTooSmallError(value, self.min_val)
        if value > self.max_val:
            return ValueTooLargeError(value, self.max_val)
        return None()

def process_value(validator: RangeValidator, value: int) -> str?:
    result: int = 0
    processed: bool = False
    try:
        error = validator.validate(value)
        if error is not None:
            # After type narrowing, error is ValidationError (not Optional)
            # So we just raise it directly - no .unwrap() needed
            raise error
        result = value * 2
        processed = True
    except ValueTooSmallError as e:
        print(f"TooSmallError: {e.min_required}")
        raise
    except ValueTooLargeError as e:
        print(f"TooLargeError: {e.max_allowed}")
        return None()
    else:
        print(f"Valid: {value}")
        return f"processed_{result}"
    finally:
        print("Cleanup")

def main():
    validator = RangeValidator(10, 100)
    
    # Test 1: Valid value
    result1 = process_value(validator, 50)
    if result1 is not None:
        print(result1.unwrap())
    
    # Test 2: Too small - will be caught and re-raised
    try:
        process_value(validator, 5)
    except ValidationError as e:
        print(f"Caught: {e.code}")
    
    # Test 3: Too large - handled internally
    result2 = process_value(validator, 200)
    print(result2.unwrap() if result2 is not None else "handled")
    
    # Test 4: Bare raise in nested context
    try:
        try:
            raise ValueTooSmallError(3, 10)
        except Exception:
            print("Inner caught")
            raise
    except ValueTooSmallError as outer:
        print(f"Outer: {outer.min_required}")
    
    print("done")

```

## Error

```
Assembly compilation failed:

error[CS1929]: 'string' does not contain a definition for 'Unwrap' and the best extension method overload 'TaskExtensions.Unwrap(Task<Task>)' requires a receiver of type 'System.Threading.Tasks.Task<System.Threading.Tasks.Task>'
  --> /tmp/tmpt8x2ftmu/dogfood_test.spy:65:43
    |
 65 |         print(result1.unwrap())
    |                                ^
    |


```

## Compiler Output

```
warning[SPY0450]: Unreachable code detected
  --> /tmp/tmpt8x2ftmu/dogfood_test.spy:57:9
    |
 57 |         print("Cleanup")
    |         ^^^^^^^^^^^^^^^^
    |

warning[SPY0451]: Local variable 'processed' is assigned but never used
  --> /tmp/tmpt8x2ftmu/dogfood_test.spy:46:9
    |
 46 |         processed = True
    |         ^^^^^^^^^^^^^^^^
    |


```

## Generated C#

```csharp
warning[SPY0450]: Unreachable code detected
  --> /tmp/tmpt8x2ftmu/dogfood_test.spy:57:9
    |
 57 |         print("Cleanup")
    |         ^^^^^^^^^^^^^^^^
    |

warning[SPY0451]: Local variable 'processed' is assigned but never used
  --> /tmp/tmpt8x2ftmu/dogfood_test.spy:46:9
    |
 46 |         processed = True
    |         ^^^^^^^^^^^^^^^^
    |

Generated C# code written to: /tmp/tmpt8x2ftmu/dogfood_test.cs

```

## Timing

- Generation: 551.25s
- Execution: 5.10s
