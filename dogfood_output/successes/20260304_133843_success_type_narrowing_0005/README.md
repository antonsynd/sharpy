# Successful Dogfood Run

**Timestamp:** 2026-03-04T13:32:39.235922
**Feature Focus:** type_narrowing
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Type narrowing with optional types in a class
class OptionalProcessor:
    total: int
    valid_count: int
    
    def __init__(self):
        self.total = 0
        self.valid_count = 0
    
    def process_value(self, v: int?) -> None:
        # Outside if block, v is int?
        if v is not None:
            # Inside if block, v is narrowed to int - no .unwrap() needed
            self.total += v
            self.valid_count += 1
        # Back outside, v is int? again
    
    def average(self) -> float:
        if self.valid_count == 0:
            return 0.0
        return float(self.total) / float(self.valid_count)

def main():
    processor = OptionalProcessor()
    
    # Create values separately and process them one by one
    val1: int? = 10
    val2: int? = None()
    val3: int? = 25
    val4: int? = None()
    val5: int? = 15
    
    processor.process_value(val1)
    processor.process_value(val2)
    processor.process_value(val3)
    processor.process_value(val4)
    processor.process_value(val5)
    
    print(processor.valid_count)
    print(processor.total)
    print(processor.average())

```

## Output

```
3
50
16.666666666666668
```

## Timing

- Generation: 347.63s
- Execution: 4.79s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
