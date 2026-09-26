# csv

CSV file reading and writing.

```python
import csv
```

## Functions

### `csv.reader(lines: list[str]) -> CsvReader`

Create a CSV reader from a list of lines.

### `csv.writer(output: TextWriter) -> CsvWriter`

Create a CSV writer that writes to a TextWriter.

### `csv.dict_reader(lines: list[str], fieldnames: Optional[list[str]] = None) -> CsvDictReader`

Create a CSV DictReader from a list of lines.

### `csv.dict_writer(output: TextWriter, fieldnames: list[str]) -> CsvDictWriter`

Create a CSV DictWriter that writes to a TextWriter.

## CsvReader

Reads CSV data from a list of lines, parsing each line into a list of fields.

## CsvWriter

Writes CSV data to a TextWriter.

### `writerow(row: list[str])`

Write a single row of fields to the CSV output.

### `writerows(rows: list[list[str]])`

Write multiple rows of fields to the CSV output.

## CsvDictReader

Reads CSV data and maps each row to a dictionary keyed by field names.

## CsvDictWriter

Writes CSV data from dictionaries keyed by field names.

### `writeheader()`

Write the field names as a header row.

### `writerow(row: dict[str, str])`

Write a single row from a dictionary in field name order.

### `writerows(rows: list[dict[str, str]])`

Write multiple rows from dictionaries.
