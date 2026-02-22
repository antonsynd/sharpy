# Issue Report: execution_failed

**Timestamp:** 2026-02-21T01:43:14.867039
**Type:** execution_failed
**Feature Focus:** builtin_conversions
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Test builtin conversions with type operations and validation
class Converter:
    values: list[str]
    
    def __init__(self):
        self.values = ["42", "3.14", "True", "0", "hello"]
    
    def convert_and_sum(self) -> float:
        total: float = 0.0
        for item in self.values:
            # Try to convert each string to float
            converted: float = float(item)
            total = total + converted
        return total

def main():
    c = Converter()
    
    # Test str() conversion with numbers
    num: int = 42
    pi: float = 3.14159
    flag: bool = True
    print(f"int as str: {str(num)}")
    print(f"float as str: {str(pi)}")
    print(f"bool as str: {str(flag)}")
    
    # Test bool() conversion
    zero: int = 0
    empty: str = ""
    non_empty: str = "hello"
    print(f"0 is bool: {str(bool(zero))}")
    print(f"empty string is bool: {str(bool(empty))}")
    print(f"non-empty is bool: {str(bool(non_empty))}")
    
    # Test int() and float() from string
    int_str: str = "100"
    float_str: str = "99.5"
    result: int = int(int_str)
    result_float: float = float(float_str)
    print(f"int from str: {str(result)}")
    print(f"float from str: {str(result_float)}")
    
    # Test the converter
    total: float = c.convert_and_sum()
    print(f"Converted sum: {str(total)}")

# EXPECTED OUTPUT:
# int as str: 42
# float as str: 3.14159
# bool as str: True
# 0 is bool: False
# empty string is bool: False
# non-empty is bool: True
# int from str: 100
# float from str: 99.5
# Converted sum: 145.54
```

## Error

```
Unhandled exception. Sharpy.ValueError: could not convert string to float: 'True'
   at Sharpy.Builtins.Double(String s)
   at Sharpy.Builtins.Float(String s)
   at DogfoodTest.Converter.ConvertAndSum() in /tmp/tmpmyf1j4rx/dogfood_test.spy:line 12
   at DogfoodTest.Main() in /tmp/tmpmyf1j4rx/dogfood_test.spy:line 44

```

## Compiler Output

```
int as str: 42
float as str: 3.14159
bool as str: True
0 is bool: False
empty string is bool: False
non-empty is bool: True
int from str: 100
float from str: 99.5

```

## Generated C#

```csharp
Generated C# code written to: /tmp/tmpmyf1j4rx/dogfood_test.cs

```

## Timing

- Generation: 833.53s
- Execution: 6.12s
