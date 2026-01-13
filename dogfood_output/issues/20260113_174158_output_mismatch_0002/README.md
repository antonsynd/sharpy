# Issue Report: output_mismatch

**Timestamp:** 2026-01-13T17:41:26.524018
**Type:** output_mismatch
**Feature Focus:** bool_variables
**Complexity:** medium
**Backend:** copilot

## Generated Sharpy Code

```python
# Test: Boolean variables and logical operations
def check_conditions(temperature: int, is_raining: bool) -> bool:
    is_cold: bool = temperature < 15
    is_hot: bool = temperature > 30
    is_comfortable: bool = not is_cold and not is_hot
    
    print(is_cold)
    print(is_hot)
    print(is_comfortable)
    
    needs_umbrella: bool = is_raining
    can_go_outside: bool = is_comfortable and not is_raining
    
    print(needs_umbrella)
    print(can_go_outside)
    
    return can_go_outside

def main():
    sunny_day: bool = False
    rainy_day: bool = True
    
    print(sunny_day)
    print(rainy_day)
    
    temp: int = 22
    result: bool = check_conditions(temp, sunny_day)
    print(result)
    
    cold_temp: int = 5
    result2: bool = check_conditions(cold_temp, rainy_day)
    print(result2)

main()

# EXPECTED OUTPUT:
# False
# True
# False
# False
# True
# False
# True
# True
# False
# False
# False
# False
```

## Output Comparison

### Expected
```
False
True
False
False
True
False
True
True
False
False
False
False
```

### Actual
```
Warning: 1 module-level statement(s) ignored because a 'main' function is defined
Successfully compiled to: /var/folders/6r/tsfytt4x6s1cl4_t14c961040000gn/T/dogfood_test_93ee2d2fc8ef4a2b904754dced604edd.exe

=== Running Program ===

False
True
False
False
True
False
True
True
True
False
False
True
False
False
```

## Timing

- Generation: 9.31s
- Execution: 1.29s
