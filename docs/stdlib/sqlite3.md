# sqlite3

Represents a connection to an SQLite database.

```python
import sqlite3
```

## Constants

| Name | Type | Description |
|------|------|-------------|
| `row` | `Func[Sqlite3Cursor, list[object?], object]` | A factory function that returns \`Sqlite3Row\` objects for query results. |

## Functions

### `sqlite3.connect(database: str) -> Sqlite3Connection`

Open a connection to an SQLite database.

**Parameters:**

- `database` (str) -- The path to the database file, or ":memory:" for an in-memory database.

**Returns:** A new `Sqlite3Connection` to the specified database.

## Connection

Represents a connection to an SQLite database.

### Properties

| Name | Type | Description |
|------|------|-------------|
| `row_factory` | `Func[Sqlite3Cursor, list[object?], object]?` | Gets or sets the row factory used to create row objects from query results. |

### `cursor() -> Sqlite3Cursor`

Create a new cursor object for this connection.

**Returns:** A new `Sqlite3Cursor`.

### `execute(sql: str, parameters: System.Collections.IEnumerable? = null) -> Sqlite3Cursor`

Create a cursor, execute a single SQL statement, and return the cursor.

**Parameters:**

- `sql` (str) -- The SQL statement to execute.
- `parameters` (System.Collections.IEnumerable?) -- Optional parameters to bind to placeholders in the SQL.

**Returns:** The cursor that executed the statement.

### `executemany(sql: str, seq_of_parameters: System.Collections.IEnumerable) -> Sqlite3Cursor`

Create a cursor and execute an SQL statement against all parameter sequences.

**Parameters:**

- `sql` (str) -- The SQL statement to execute.
- `seq_of_parameters` (System.Collections.IEnumerable)

**Returns:** The cursor that executed the statements.

### `executescript(sql_script: str) -> Sqlite3Cursor`

Create a cursor and execute a script of one or more SQL statements.

**Returns:** The cursor that executed the script.

### `commit()`

Commit the current transaction.

### `rollback()`

Roll back the current transaction.

### `close()`

Close the connection. Any pending transaction is rolled back.

## Cursor

Represents a database cursor used to execute SQL statements and fetch results.

### Properties

| Name | Type | Description |
|------|------|-------------|
| `arraysize` | `int` | Gets or sets the number of rows to fetch at a time with \`Fetchmany\`. Default is 1. |
| `lastrowid` | `long` | Gets the row ID of the last modified row, or -1 if no row was inserted. |
| `rowcount` | `int` | Gets the number of rows affected by the last DML statement, or -1 for queries. |
| `description` | `System.Collections.Generic.List[list[object?]]?` | Gets column descriptions for the last query, or null if no query has been executed. |

### `execute(sql: str, parameters: System.Collections.IEnumerable? = null) -> Sqlite3Cursor`

Execute a single SQL statement, optionally binding parameters.

**Parameters:**

- `sql` (str) -- The SQL statement to execute. Use `?` as a placeholder for positional parameters.
- `parameters` (System.Collections.IEnumerable?) -- Optional parameters to bind to placeholders in the SQL.

**Returns:** This cursor instance.

### `executemany(sql: str, seq_of_parameters: System.Collections.IEnumerable) -> Sqlite3Cursor`

Execute an SQL statement against all parameter sequences in the given iterable.

**Parameters:**

- `sql` (str) -- The SQL statement to execute.
- `seq_of_parameters` (System.Collections.IEnumerable)

**Returns:** This cursor instance.

### `executescript(sql_script: str) -> Sqlite3Cursor`

Execute a script of one or more SQL statements, committing any pending transaction first.

**Returns:** This cursor instance.

### `fetchone() -> object?`

Fetch the next row of a query result, returning null if no more data is available.

**Returns:** The next row as an array of values, or null.

### `fetchmany(size: int = -1) -> System.Collections.Generic.List[object]`

Fetch the next set of rows, returning a list. An empty list is returned when no more rows are available.

**Parameters:**

- `size` (int) -- The maximum number of rows to fetch. Defaults to `Arraysize`.

**Returns:** A list of row objects.

### `fetchall() -> System.Collections.Generic.List[object]`

Fetch all remaining rows of a query result, returning a list.

**Returns:** A list of all remaining row objects.

### `close()`

Close the cursor. The cursor will be unusable from this point forward.

## Error

Base exception for all sqlite3-related errors.

## DatabaseError

Base exception for all sqlite3-related errors.

## OperationalError

Base exception for all sqlite3-related errors.

## IntegrityError

Base exception for all sqlite3-related errors.

## ProgrammingError

Base exception for all sqlite3-related errors.

## InterfaceError

Base exception for all sqlite3-related errors.

## Row

Represents a row returned from a query when using `Sqlite3.Row` as the row factory.

### `keys() -> System.Collections.Generic.List[str]`

Return a list of column names.

**Returns:** A list of column name strings.
