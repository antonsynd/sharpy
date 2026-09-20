# csv

CSV file reading and writing.

```python
import csv
```

## Functions

### `csv.writerow(row: list[str])`

Write a single row of fields to the CSV output.

### `csv.writerows(rows: list[list[str]])`

Write multiple rows of fields to the CSV output.

### `csv.writeheader()`

Write the field names as a header row.

### `csv.writerow(row: dict[str, str])`

Write a single row from a dictionary in field name order.

### `csv.writerows(rows: list[dict[str, str]])`

Write multiple rows from dictionaries.

### `csv.reader(lines: list[str]) -> CsvReader`

Create a CSV reader from a list of lines.

### `csv.writer(output: TextWriter) -> CsvWriter`

Create a CSV writer that writes to a TextWriter.

### `csv.dict_reader(lines: list[str], fieldnames: Optional[list[str]] = default) -> CsvDictReader`

Create a CSV DictReader from a list of lines.

### `csv.dict_writer(output: TextWriter, fieldnames: list[str]) -> CsvDictWriter`

Create a CSV DictWriter that writes to a TextWriter.
