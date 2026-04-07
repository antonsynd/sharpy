# csv

Reads CSV data and maps each row to a dictionary keyed by field names,
similar to Python's `csv.DictReader`.

```python
import csv
```

## Constants

| Name | Type | Description |
|------|------|-------------|
| `quote_all` | `int` | Quote all fields. |
| `quote_minimal` | `int` | Quote only fields that contain special characters. |
| `quote_none` | `int` | Do not quote fields. |
| `quote_nonnumeric` | `int` | Quote all non-numeric fields. |

## Functions

### `csv.reader(lines: Iterable[str]) -> CsvReader`

Create a CSV reader from an enumerable of lines.

**Parameters:**

- `lines` (Iterable[str]) -- An enumerable of CSV lines to parse.

**Returns:** A `CsvReader` that iterates over parsed rows.

### `csv.writer(output: System.IO.TextWriter) -> CsvWriter`

Create a CSV writer that writes to a TextWriter.

**Parameters:**

- `output` (System.IO.TextWriter) -- The output writer to write CSV data to.

**Returns:** A `CsvWriter` for writing CSV rows.

### `csv.dict_reader(lines: Iterable[str], fieldnames: list[str]? = null) -> CsvDictReader`

Create a CSV DictReader from an enumerable of lines.

**Parameters:**

- `lines` (Iterable[str]) -- An enumerable of CSV lines to parse.
- `fieldnames` (list[str]?) -- Optional field names. If null, the first row is used as field names.

**Returns:** A `CsvDictReader` that iterates over parsed rows as dictionaries.

### `csv.dict_writer(output: System.IO.TextWriter, fieldnames: list[str]) -> CsvDictWriter`

Create a CSV DictWriter that writes to a TextWriter.

**Parameters:**

- `output` (System.IO.TextWriter) -- The output writer to write CSV data to.
- `fieldnames` (list[str]) -- The field names determining column order.

**Returns:** A `CsvDictWriter` for writing CSV rows as dictionaries.

## CsvDictReader

Reads CSV data and maps each row to a dictionary keyed by field names,
similar to Python's `csv.DictReader`.

## CsvDictWriter

Writes CSV data from dictionaries keyed by field names,
similar to Python's `csv.DictWriter`.

### `writeheader()`

Write the field names as a header row.

### `writerow(row: dict[str, str])`

Write a single row from a dictionary. Values are written in field name order.
Missing keys produce empty strings.

**Parameters:**

- `row` (dict[str, str]) -- A dictionary mapping field names to values.

### `writerows(rows: Iterable[dict[str, str]])`

Write multiple rows from dictionaries.

**Parameters:**

- `rows` (Iterable[dict[str, str]]) -- An enumerable of dictionaries to write.

## CsvReader

Reads CSV data from an enumerable of lines, parsing each line into a list of fields.
Handles quoted fields, escaped quotes, and commas within quoted fields.

## CsvWriter

Writes CSV data to a TextWriter.

### `writerow(row: Iterable[str])`

Write a single row of fields to the CSV output.

### `writerows(rows: Iterable[Iterable[str]])`

Write multiple rows of fields to the CSV output.
