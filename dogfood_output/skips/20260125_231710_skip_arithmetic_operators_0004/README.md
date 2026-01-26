# Skipped Dogfood Run

**Timestamp:** 2026-01-25T23:16:42.703777
**Skip Reason:** Invalid expected output after 3 attempts (Python says: )
**Feature Focus:** arithmetic_operators
**Complexity:** simple
**Backend:** claude

## Generated Sharpy Code

```python
# Test arithmetic operators with negative numbers and operator precedence
def main():
    x: int = -10
    y: int = 3
    
    # Addition
    print(x + y)
    
    # Subtraction
    print(x - y)
    
    # Multiplication
    print(x * y)
    
    # Power
    result: int = y ** 2
    print(result)
    
    # Combined with precedence
    combined: int = x + y * result
    print(combined)

# EXPECTED OUTPUT:
# -7
# -13
# -30
# 9
# 17
```

## Timing

- Generation: 27.37s

## Notes

This iteration was skipped because the generated code didn't pass validation.
This is typically due to the AI generating code with unsupported features
or syntax that doesn't match the Sharpy spec (phases 0.1.0-0.1.10).

This output is saved for inspection to help improve prompting.
