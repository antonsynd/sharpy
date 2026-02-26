# Successful Dogfood Run

**Timestamp:** 2026-02-25T10:42:42.241421
**Feature Focus:** module_utils
**Complexity:** medium
**Backend:** klaude
**Test Type:** Multi-file (2 files)

## Source Files

### number_utils.spy

```python
# Module providing number utility functions and statistics tracking

def is_prime(n: int) -> bool:
    """Check if a number is prime."""
    if n < 2:
        return False
    if n == 2:
        return True
    if n % 2 == 0:
        return False
    i: int = 3
    while i * i <= n:
        if n % i == 0:
            return False
        i += 2
    return True

def factorial(n: int) -> int:
    """Calculate factorial of n."""
    if n < 0:
        return 0
    if n <= 1:
        return 1
    result: int = 1
    i: int = 2
    while i <= n:
        result = result * i
        i += 1
    return result

class NumberStats:
    # Use regular instance fields instead of properties
    count: int
    total: int
    
    def __init__(self):
        self.count = 0
        self.total = 0
    
    def add(self, n: int) -> None:
        """Add a number to the statistics."""
        self.count = self.count + 1
        self.total = self.total + n
    
    def reset(self) -> None:
        """Reset all statistics."""
        self.count = 0
        self.total = 0
    
    # Use regular method instead of property for computed values
    def average(self) -> float:
        """Calculate average of all added numbers."""
        if self.count == 0:
            return 0.0
        return float(self.total) / float(self.count)
```

### main.spy

```python
# Entry point importing number_utils module
from number_utils import is_prime, factorial, NumberStats

def main():
    # Test utility functions from module
    print(is_prime(17))
    print(factorial(5))
    
    # Test NumberStats class from module
    stats = NumberStats()
    stats.add(10)
    stats.add(20)
    stats.add(30)
    
    # Verify statistics
    print(stats.count)
    print(stats.total)
    print(stats.average())

# EXPECTED OUTPUT:
# True
# 120
# 3
# 60
# 20.0
```

## Timing

- Generation: 242.96s
- Execution: 4.43s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
