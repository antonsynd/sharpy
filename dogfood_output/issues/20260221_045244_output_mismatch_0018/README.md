# Issue Report: output_mismatch

**Timestamp:** 2026-02-21T04:51:16.760609
**Type:** output_mismatch
**Feature Focus:** module_utils
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - imports from utils and validators

from utils import StringUtils, MathUtils, format_number
from validators import Validator

def main():
    # Test static methods from StringUtils
    reversed_text: str = StringUtils.reverse("hello")
    print(reversed_text)
    
    capitalized: str = StringUtils.capitalize_words("hello world")
    print(capitalized)
    
    # Test MathUtils class
    math_utils: MathUtils = MathUtils(5)
    scaled: int = math_utils.scale(10)
    print(format_number(scaled))
    
    # Test Validator from validators module
    validator: Validator = Validator(0, 100)
    value: int = 42
    print(validator.is_valid(value))
    print(validator.is_valid(150))

# EXPECTED OUTPUT:
# olleh
# Hello World
# Number: 50
# True
# False
```

## Error

```
AI explicitly reported mismatch
```

## Output Comparison

### Expected
```
olleh
Hello World
Number: 50
True
False

```

### Actual
```
Hello World
Number: 50
True
False
```

## Timing

- Generation: 42.34s
- Execution: 4.95s
