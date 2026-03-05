# Skipped Dogfood Run

**Timestamp:** 2026-03-04T14:14:17.305505
**Skip Reason:** Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0103]: Expected end of statement, got Def
  --> /tmp/tmpux8sdng4/dogfood_test.spy:33:8
    |
 33 | extern def beep(frequency: int, duration: int) -> int:
    |        ^^^
    |


**Feature Focus:** custom_decorator_simple
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: custom_decorator_simple - complex version with multiple attributes
# Demonstrates various custom decorator patterns including:
# - Simple PascalCase mangling (@obsolete -> Obsolete)
# - Keyword arguments (@dll_import with entry_point)
# - Boolean arguments (@conditional with DEBUG)
# - Float arguments (@range with min/max)
# - Type arguments via type(X)
#
# Expected: Compile-time attributes are applied to the code

@system.serializable
class Point:
    x: float = 0.0
    y: float = 0.0

@obsolete("Use Calculator.add instead")
def old_add(x: int, y: int) -> int:
    return x + y

@obsolete("This will be removed in v2.0", error=True)
def deprecated_error_func() -> str:
    return "this is deprecated"

@conditional(DEBUG)
def debug_log(msg: str) -> None:
    print(f"[DEBUG] {msg}")

@range(min=0.0, max=100.0)
def percent_value() -> float:
    return 50.0

@dll_import("kernel32.dll", entry_point="Beep", set_last_error=True)
extern def beep(frequency: int, duration: int) -> int:
    pass

def main():
    # Test that deprecated function still works (produces warning)
    result: int = old_add(3, 4)
    print(result)
    
    # Test with class
    p: Point = Point()
    p.x = 1.5
    p.y = 2.5
    print(p.x)
    print(p.y)
    
    # Call remaining functions
    print(percent_value())
    
    # Debug function (conditionally compiled)
    debug_log("Application started")

```

## Timing

- Generation: 317.55s

## Notes

This iteration was skipped because the generated code didn't pass validation.
This is typically due to the AI generating code with unsupported features
or syntax that doesn't match the Sharpy spec (phases 0.1.0-0.2.6).

This output is saved for inspection to help improve prompting.
