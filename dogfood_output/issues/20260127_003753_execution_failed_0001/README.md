# Issue Report: execution_failed

**Timestamp:** 2026-01-27T00:36:59.990630
**Type:** execution_failed
**Feature Focus:** module_imports
**Complexity:** complex
**Backend:** claude

## Generated Sharpy Code

```python
# Main entry point: Demonstrates cross-module imports and inheritance
from data_models import Record
from processors import DatabaseSource, FileSource, RecordProcessor

def main():
    # Create data sources using cross-module inheritance
    db_source = DatabaseSource("DB001", "server=localhost;db=test")
    file_source = FileSource("FILE001", "/data/records.csv")

    # Test source status methods (inherited from DataSource)
    print(db_source.get_status())
    print(file_source.get_status())

    # Create records
    record1 = Record("temperature", 72, "2026-01-27T10:00")
    record2 = Record("humidity", 65, "2026-01-27T10:05")
    record3 = Record("pressure", 1013, "2026-01-27T10:10")

    # Process records with database source
    db_processor = RecordProcessor(db_source)
    print(db_processor.process(record1))
    print(db_processor.process(record2))

    # Process record with file source
    file_processor = RecordProcessor(file_source)
    print(file_processor.process(record3))

    # Show final processing counts
    print(f"DB processor handled: {db_processor.processed_count} records")
    print(f"File processor handled: {file_processor.processed_count} records")

# EXPECTED OUTPUT:
# Source DB001 active: True
# Source FILE001 active: True
# [1] Data from DB: server=localhost;db=test | temperature=72 at 2026-01-27T10:00
# [2] Data from DB: server=localhost;db=test | humidity=65 at 2026-01-27T10:05
# [1] Data from file: /data/records.csv | pressure=1013 at 2026-01-27T10:10
# DB processor handled: 2 records
# File processor handled: 1 records
```

## Error

```
Compilation failed:
  Semantic error at line 11, column 11: Type 'DatabaseSource' has no member 'get_status'
  Semantic error at line 12, column 11: Type 'FileSource' has no member 'get_status'

```

## Timing

- Generation: 20.90s
- Execution: 0.90s
