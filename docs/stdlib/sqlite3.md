# sqlite3

```python
import sqlite3
```

## Constants

| Name | Type | Description |
|------|------|-------------|
| `row` | `Func[Sqlite3Cursor, list[object?], object]` |  |

## Functions

### `sqlite3.connect(database: str) -> Sqlite3Connection`

## Connection

### Properties

| Name | Type | Description |
|------|------|-------------|
| `row_factory` | `Func[Sqlite3Cursor, list[object?], object]?` |  |

### `cursor() -> Sqlite3Cursor`

### `execute(sql: str, parameters: System.Collections.IEnumerable? = null) -> Sqlite3Cursor`

### `executemany(sql: str, seq_of_parameters: System.Collections.IEnumerable) -> Sqlite3Cursor`

### `executescript(sql_script: str) -> Sqlite3Cursor`

### `commit()`

### `rollback()`

### `close()`

## Cursor

### Properties

| Name | Type | Description |
|------|------|-------------|
| `arraysize` | `int` |  |
| `lastrowid` | `long` |  |
| `rowcount` | `int` |  |
| `description` | `System.Collections.Generic.List[list[object?]]?` |  |

### `execute(sql: str, parameters: System.Collections.IEnumerable? = null) -> Sqlite3Cursor`

### `executemany(sql: str, seq_of_parameters: System.Collections.IEnumerable) -> Sqlite3Cursor`

### `executescript(sql_script: str) -> Sqlite3Cursor`

### `fetchone() -> object?`

### `fetchmany(size: int = -1) -> System.Collections.Generic.List[object]`

### `fetchall() -> System.Collections.Generic.List[object]`

### `close()`

## Error

## DatabaseError

## OperationalError

## IntegrityError

## ProgrammingError

## InterfaceError

## Row

### `keys() -> System.Collections.Generic.List[str]`
