# time

Represents a time value as a named tuple of components, similar to Python's
`time.struct_time`.

```python
import time
```

## Functions

### `time.time() -> float`

Return the time in seconds since the epoch (1970-01-01T00:00:00Z) as a
floating point number.

**Returns:** Seconds since the Unix epoch.

```python
t = time.time()    # e.g. 1700000000.123
```

### `time.time_ns() -> long`

Return the time in nanoseconds since the epoch (1970-01-01T00:00:00Z).

**Returns:** Nanoseconds since the Unix epoch (millisecond precision).

!!! note
    On netstandard2.x, precision is limited to milliseconds. The value is
    derived from `DateTimeOffset.ToUnixTimeMilliseconds` multiplied
    by 1,000,000.

### `time.sleep(secs: float)`

Suspend execution of the calling thread for the given number of seconds.

**Parameters:**

- `secs` (float) -- Number of seconds to sleep. Fractional values are accepted.

```python
time.sleep(0.5)    # sleep for 500 milliseconds
```

### `time.perf_counter() -> float`

Return the value (in fractional seconds) of a performance counter,
i.e. a clock with the highest available resolution to measure a short
duration.

**Returns:** A monotonic time value in seconds.

### `time.perf_counter_ns() -> long`

Return the value (in nanoseconds) of a performance counter.

**Returns:** A monotonic time value in nanoseconds.

### `time.monotonic() -> float`

Return the value (in fractional seconds) of a monotonic clock,
i.e. a clock that cannot go backwards.

**Returns:** A monotonic time value in seconds.

### `time.monotonic_ns() -> long`

Return the value (in nanoseconds) of a monotonic clock.

**Returns:** A monotonic time value in nanoseconds.

### `time.strftime(format: str) -> str`

Convert a time value to a string according to a format specification.
Uses the current local time.

**Parameters:**

- `format` (str) -- A format string using Python-style format codes
(e.g. `%Y-%m-%d %H:%M:%S`).

**Returns:** The formatted time string.

```python
time.strftime("%Y-%m-%d")    # e.g. "2024-01-15"
```

### `time.gmtime() -> StructTime`

Convert current UTC time to a `StructTime` (similar to Python's
`time.gmtime()`).

**Returns:** A `StructTime` representing the current UTC time.

```python
t = time.gmtime()
print(t.tm_year)    # e.g. 2024
```

### `time.gmtime(seconds: float) -> StructTime`

Convert a Unix timestamp (seconds since the epoch) to a `StructTime`
in UTC (similar to Python's `time.gmtime(secs)`).

**Parameters:**

- `seconds` (float) -- Seconds since the Unix epoch (1970-01-01T00:00:00Z).

**Returns:** A `StructTime` representing the specified UTC time.

```python
t = time.gmtime(0)
print(t.tm_year)    # 1970
```

### `time.localtime() -> StructTime`

Convert current local time to a `StructTime` (similar to Python's
`time.localtime()`).

**Returns:** A `StructTime` representing the current local time.

```python
t = time.localtime()
print(t.tm_hour)    # current local hour
```

### `time.localtime(seconds: float) -> StructTime`

Convert a Unix timestamp (seconds since the epoch) to a `StructTime`
in local time (similar to Python's `time.localtime(secs)`).

**Parameters:**

- `seconds` (float) -- Seconds since the Unix epoch (1970-01-01T00:00:00Z).

**Returns:** A `StructTime` representing the specified local time.

```python
t = time.localtime(86400)
print(t.tm_mday)    # depends on local timezone
```

## StructTime

Represents a time value as a named tuple of components, similar to Python's
`time.struct_time`.
