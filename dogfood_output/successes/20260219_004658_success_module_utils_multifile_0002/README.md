# Successful Dogfood Run

**Timestamp:** 2026-02-19T00:39:34.335134
**Feature Focus:** module_utils
**Complexity:** medium
**Backend:** klaude
**Test Type:** Multi-file (3 files)

## Source Files

### formatter.spy

```python
# Text formatting interface and implementations

interface IFormatter:
    def format_header(self, text: str) -> str: ...

class DecoratedFormatter(IFormatter):
    prefix: str
    suffix: str
    
    def __init__(self, prefix: str, suffix: str):
        self.prefix = prefix
        self.suffix = suffix
    
    def format_header(self, text: str) -> str:
        return self.prefix + text + self.suffix
```

### data_utils.spy

```python
from formatter import IFormatter

class DataSet:
    values: list[int]
    name: str
    
    def __init__(self, name: str, values: list[int]):
        self.name = name
        self.values = values
    
    def sum(self) -> int:
        total: int = 0
        for v in self.values:
            total = total + v
        return total
    
    def average(self) -> int:
        if len(self.values) == 0:
            return 0
        return self.sum() // len(self.values)
    
    def format_report(self, formatter: IFormatter) -> str:
        return formatter.format_header("Report: " + self.name)
```

### main.spy

```python
from formatter import DecoratedFormatter
from data_utils import DataSet

def main():
    # Create formatter with decorative brackets
    formatter: DecoratedFormatter = DecoratedFormatter("=== ", " ===")
    
    # Create dataset with test values
    data: DataSet = DataSet("Sales", [100, 200, 300, 400, 500])
    
    # Print formatted report header using interface
    print(data.format_report(formatter))
    
    # Print computed statistics
    print("Sum: " + str(data.sum()))
    print("Count: " + str(len(data.values)))
    print("Average: " + str(data.average()))
    print("Done!")
    
# EXPECTED OUTPUT:
# === Report: Sales ===
# Sum: 1500
# Count: 5
# Average: 300
# Done!
```

## Timing

- Generation: 429.15s
- Execution: 4.45s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
