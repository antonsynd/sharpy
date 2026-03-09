# Successful Dogfood Run

**Timestamp:** 2026-03-08T13:38:23.817939
**Feature Focus:** try_except_finally
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Complete try/except/else/finally with resource tracking
class DataProcessor:
    _items: list[int]
    processed_count: int
    cleanup_count: int
    
    def __init__(self):
        self._items = [10, 20, 30]
        self.processed_count = 0
        self.cleanup_count = 0
    
    def process_at(self, index: int) -> None:
        # Full try/except/else/finally pattern
        # Declare value before try since it's used in else
        value: int = 0
        try:
            # This might raise IndexError
            value = self._items[index]
        except IndexError as e:
            print(f"caught_error: index {index} out of range")
        else:
            # Only runs when NO exception occurs
            print(f"success: got value {value}")
            self.processed_count += 1
        finally:
            # ALWAYS runs regardless of exception
            self.cleanup_count += 1
    
    def get_stats(self) -> str:
        return f"stats: {self.processed_count} processed, {self.cleanup_count} cleanups"

def main():
    processor = DataProcessor()
    
    # Test valid index - uses else branch
    processor.process_at(1)
    
    # Test invalid index - uses except branch
    processor.process_at(5)
    
    # Another valid index - uses else branch again
    processor.process_at(0)
    
    print(processor.get_stats())

```

## Output

```
success: got value 20
caught_error: index 5 out of range
success: got value 10
stats: 2 processed, 3 cleanups
```

## Timing

- Generation: 192.58s
- Execution: 5.09s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
