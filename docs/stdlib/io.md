# io

In-memory text stream using a string buffer, similar to Python's io.StringIO.
Extends TextWriter so it can be used anywhere a TextWriter is expected (e.g., csv module).

```python
import io
```

## StringIO

In-memory text stream using a string buffer, similar to Python's io.StringIO.
Extends TextWriter so it can be used anywhere a TextWriter is expected (e.g., csv module).

### `write(value: char)`

Write a single character to the buffer at the current position.
This override satisfies the TextWriter contract.

### `write(s: str) -> int`

Write a string to the buffer at the current position.
Returns the number of characters written (Python semantics).
Hides the base TextWriter.Write(string?) which returns void.
When called through a TextWriter reference, the base Write(string?) delegates
to Write(char) which correctly updates the buffer, but the return value
(character count) is silently discarded. Direct StringIO callers get the count;
TextWriter callers do not.

**Parameters:**

- `s` (str) -- The string to write.

**Returns:** The number of characters written.

**Raises:**

- `ValueError` -- Thrown if the stream is closed.

### `read(n: int = -1) -> str`

Read from the buffer starting at the current position.

**Parameters:**

- `n` (int) -- Number of characters to read. -1 reads all remaining.

**Returns:** The string read from the buffer.

**Raises:**

- `ValueError` -- Thrown if the stream is closed.

### `readline() -> str`

Read a single line from the current position (up to and including the newline).

**Returns:** The line read, including the trailing newline if present.

**Raises:**

- `ValueError` -- Thrown if the stream is closed.

### `seek(pos: int) -> int`

Set the stream position.

**Parameters:**

- `pos` (int) -- The new position.

**Returns:** The new absolute position.

**Raises:**

- `ValueError` -- Thrown if the stream is closed or position is negative.

### `tell() -> int`

Return the current stream position.

**Returns:** The current position in the stream.

**Raises:**

- `ValueError` -- Thrown if the stream is closed.

### `getvalue() -> str`

Return the entire contents of the buffer regardless of position.

**Returns:** The complete buffer content.

**Raises:**

- `ValueError` -- Thrown if the stream is closed.

### `truncate(size: int = -1) -> int`

Truncate the buffer at the given size, or at the current position if size is -1.

**Parameters:**

- `size` (int) -- The size to truncate to, or -1 for current position.

**Returns:** The new size of the buffer.

**Raises:**

- `ValueError` -- Thrown if the stream is closed.

### `close()`

Mark the stream as closed. Further operations will raise ValueError.
