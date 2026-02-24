# Issue Report: compilation_failed

**Timestamp:** 2026-02-24T02:56:23.059430
**Type:** compilation_failed
**Feature Focus:** raise_exception
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Complex exception raising patterns with class hierarchies and cleanup
# Tests: raise statement, custom exception hierarchy, exception propagation,
# finally block cleanup

class ValidationError:
    message: str
    field: str
    
    def __init__(self, field: str, message: str):
        self.field = field
        self.message = message
    
    def __str__(self) -> str:
        return f"ValidationError({self.field}): {self.message}"

class RangeError(ValidationError):
    min_val: int
    max_val: int
    
    def __init__(self, field: str, value: int, min_val: int, max_val: int):
        super().__init__(field, f"value {value} not in range [{min_val}, {max_val}]")
        self.min_val = min_val
        self.max_val = max_val

class ConfigValidator:
    config: dict[str, int]
    
    def __init__(self):
        self.config = {}
    
    def set_port(self, value: int) -> None:
        if value < 1024:
            raise RangeError("port", value, 1024, 65535)
        self.config["port"] = value
    
    def set_timeout(self, seconds: int) -> None:
        if seconds <= 0:
            raise ValidationError("timeout", "must be positive")
        if seconds > 300:
            raise RangeError("timeout", seconds, 1, 300)
        self.config["timeout"] = seconds
    
    def validate_or_raise(self) -> None:
        if "port" not in self.config:
            raise ValidationError("config", "port is required")
        if "timeout" not in self.config:
            self.set_timeout(30)

def attempt_configuration(port_val: int, timeout_val: int) -> str:
    validator = ConfigValidator()
    result: str = ""
    backup_port: int? = None()
    
    try:
        try:
            backup_port = Some(8080)
            validator.set_port(port_val)
            validator.set_timeout(timeout_val)
            validator.validate_or_raise()
        except RangeError:
            print("Caught range error")
            if backup_port is not None:
                print("Falling back to port 8080")
                validator.set_port(8080)
                return "fallback_success"
            raise
    except ValidationError:
        print("Validation failed")
        return "validation_failed"
    finally:
        print("Cleanup completed")
    
    return "success"

def main():
    result1: str = attempt_configuration(80, 60)
    print(f"Result 1: {result1}")
    result2: str = attempt_configuration(8080, -5)
    print(f"Result 2: {result2}")
    result3: str = attempt_configuration(9000, 120)
    print(f"Result 3: {result3}")

# EXPECTED OUTPUT:
# Result 1: fallback_success
# Result 2: validation_failed
# Result 3: success
```

## Error

```
Assembly compilation failed:

error[CS0029]: Cannot implicitly convert type 'DogfoodTest.RangeError' to 'System.Exception'
  --> /tmp/tmp3p3t2ov_/dogfood_test.spy:33:23
    |
 33 |             raise RangeError("port", value, 1024, 65535)
    |                       ^
    |

error[CS0155]: The type caught or thrown must be derived from System.Exception
  --> /tmp/tmp3p3t2ov_/dogfood_test.spy:61:20
    |
 61 |             print("Caught range error")
    |                    ^
    |

error[CS0155]: The type caught or thrown must be derived from System.Exception
  --> /tmp/tmp3p3t2ov_/dogfood_test.spy:69:16
    |
 69 |         return "validation_failed"
    |                ^
    |

error[CS0029]: Cannot implicitly convert type 'DogfoodTest.ValidationError' to 'System.Exception'
  --> /tmp/tmp3p3t2ov_/dogfood_test.spy:38:23
    |
 38 |             raise ValidationError("timeout", "must be positive")
    |                       ^
    |

error[CS0029]: Cannot implicitly convert type 'DogfoodTest.RangeError' to 'System.Exception'
  --> /tmp/tmp3p3t2ov_/dogfood_test.spy:40:23
    |
 40 |             raise RangeError("timeout", seconds, 1, 300)
    |                       ^
    |

error[CS0029]: Cannot implicitly convert type 'DogfoodTest.ValidationError' to 'System.Exception'
  --> /tmp/tmp3p3t2ov_/dogfood_test.spy:45:23
    |
 45 |             raise ValidationError("config", "port is required")
    |                       ^
    |


```

## Compiler Output

```
warning[SPY0451]: Local variable 'result' is assigned but never used
  --> /tmp/tmp3p3t2ov_/dogfood_test.spy:51:5
    |
 51 |     result: str = ""
    |     ^^^^^^^^^^^^^^
    |


```

## Generated C#

```csharp
warning[SPY0451]: Local variable 'result' is assigned but never used
  --> /tmp/tmp3p3t2ov_/dogfood_test.spy:51:5
    |
 51 |     result: str = ""
    |     ^^^^^^^^^^^^^^
    |

Generated C# code written to: /tmp/tmp3p3t2ov_/dogfood_test.cs

```

## Timing

- Generation: 268.18s
- Execution: 4.35s
