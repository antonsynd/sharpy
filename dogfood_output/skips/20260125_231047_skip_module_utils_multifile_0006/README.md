# Skipped Dogfood Run

**Timestamp:** 2026-01-25T23:10:31.409920
**Skip Reason:** Unsupported feature in data_structures.spy: Line 47: tuple unpacking (not fully supported) - 'return f"{n} is prime, factorial={fact}"...'
**Feature Focus:** module_utils
**Complexity:** complex
**Backend:** claude
**Test Type:** Multi-file (3 files)

## Source Files

### utils.spy

```python
# Utility module providing math and validation helpers

class MathUtils:
    """Static utility class for mathematical operations"""
    
    @staticmethod
    def factorial(n: int) -> int:
        if n <= 1:
            return 1
        result: int = 1
        i: int = 2
        while i <= n:
            result *= i
            i += 1
        return result
    
    @staticmethod
    def is_prime(n: int) -> bool:
        if n < 2:
            return False
        i: int = 2
        while i * i <= n:
            if n % i == 0:
                return False
            i += 1
        return True

class Validator:
    """Validation utilities for data processing"""
    
    min_value: int
    max_value: int
    
    def __init__(self, min_val: int, max_val: int):
        self.min_value = min_val
        self.max_value = max_val
    
    def is_in_range(self, value: int) -> bool:
        return self.min_value <= value and value <= self.max_value
    
    def clamp(self, value: int) -> int:
        if value < self.min_value:
            return self.min_value
        elif value > self.max_value:
            return self.max_value
        else:
            return value
```

### data_structures.spy

```python
# Data structures module providing specialized collection wrappers

from utils import Validator

class RangedList:
    """List wrapper that only accepts values within a specified range"""
    
    items: list[int]
    validator: Validator
    
    def __init__(self, min_val: int, max_val: int):
        self.items = []
        self.validator = Validator(min_val, max_val)
    
    def add(self, value: int) -> bool:
        if self.validator.is_in_range(value):
            self.items.append(value)
            return True
        else:
            clamped: int = self.validator.clamp(value)
            self.items.append(clamped)
            return False
    
    def get_sum(self) -> int:
        total: int = 0
        for item in self.items:
            total += item
        return total
    
    def get_count(self) -> int:
        count: int = 0
        for item in self.items:
            count += 1
        return count

class DataProcessor:
    """Processes data using mathematical utilities"""
    
    @staticmethod
    def process_number(n: int) -> str:
        from utils import MathUtils
        
        fact: int = MathUtils.factorial(n)
        is_prime: bool = MathUtils.is_prime(n)
        
        if is_prime:
            return f"{n} is prime, factorial={fact}"
        else:
            return f"{n} is composite, factorial={fact}"
```

### main.spy

```python
# Main entry point demonstrating cross-module utilities

from utils import MathUtils, Validator
from data_structures import RangedList, DataProcessor

def main():
    # Test MathUtils from utils module
    print(MathUtils.factorial(5))
    
    # Test Validator from utils module
    validator = Validator(10, 50)
    print(validator.is_in_range(25))
    print(validator.is_in_range(100))
    
    # Test RangedList from data_structures (which uses utils.Validator)
    ranged_list = RangedList(1, 10)
    ranged_list.add(5)
    ranged_list.add(15)
    ranged_list.add(3)
    print(ranged_list.get_sum())
    print(ranged_list.get_count())
    
    # Test DataProcessor from data_structures (which uses utils.MathUtils)
    print(DataProcessor.process_number(7))
    print(DataProcessor.process_number(6))

# EXPECTED OUTPUT:
# 120
# True
# False
# 18
# 3
# 7 is prime, factorial=5040
# 6 is composite, factorial=720
```

## Timing

- Generation: 15.88s

## Notes

This iteration was skipped because the generated code didn't pass validation.
This is typically due to the AI generating code with unsupported features
or syntax that doesn't match the Sharpy spec (phases 0.1.0-0.1.10).

This output is saved for inspection to help improve prompting.
