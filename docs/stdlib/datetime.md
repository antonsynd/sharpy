# datetime

Represents a date (year, month, day).

```python
import datetime
```

## date

Represents a date (year, month, day).

### Properties

| Name | Type | Description |
|------|------|-------------|
| `year` | `int` | The year component. |
| `month` | `int` | The month component (1-12). |
| `day` | `int` | The day component (1-31). |

### `today() -> Date`

Return the current local date.

### `weekday() -> int`

Return the day of the week (0=Monday through 6=Sunday).

### `isoweekday() -> int`

Return the ISO day of the week (1=Monday through 7=Sunday).

### `isoformat() -> str`

Return the ISO 8601 formatted string.

### `replace(year: int? = null, month: int? = null, day: int? = null) -> Date`

Return a new Date with replaced components.

### `toordinal() -> int`

Return the proleptic Gregorian ordinal of the date.

### `fromordinal(ordinal: int) -> Date`

Create a Date from a proleptic Gregorian ordinal.

### `fromisoformat(date_string: str) -> Date`

Parse a date from ISO 8601 format string.

### `strftime(format: str) -> str`

Format the date using Python strftime format codes.

## time

Represents a date (year, month, day).

### Properties

| Name | Type | Description |
|------|------|-------------|
| `hour` | `int` | The hour component (0-23). |
| `minute` | `int` | The minute component (0-59). |
| `second` | `int` | The second component (0-59). |

### `isoformat() -> str`

Return the ISO 8601 formatted string.

### `strftime(format: str) -> str`

Format the time using Python strftime format codes.

## datetime

Represents a date (year, month, day).

### Properties

| Name | Type | Description |
|------|------|-------------|
| `year` | `int` | The year component. |
| `month` | `int` | The month component (1-12). |
| `day` | `int` | The day component (1-31). |
| `hour` | `int` | The hour component (0-23). |
| `minute` | `int` | The minute component (0-59). |
| `second` | `int` | The second component (0-59). |
| `tzinfo` | `Timezone?` | The timezone info, or null if naive. |

### `now() -> DateTime`

Return the current local datetime.

### `utcnow() -> DateTime`

Return the current UTC datetime.

### `combine(date: Date, time: Time) -> DateTime`

Combine a date and a time to create a datetime.

### `weekday() -> int`

Return the day of the week (0=Monday through 6=Sunday).

### `isoweekday() -> int`

Return the ISO day of the week (1=Monday through 7=Sunday).

### `isoformat(sep: str? = null) -> str`

Return the ISO 8601 formatted string.

### `replace(year: int? = null, month: int? = null, day: int? = null, hour: int? = null, minute: int? = null, second: int? = null) -> DateTime`

Return a new DateTime with replaced components.

### `timestamp() -> float`

Return the Unix timestamp as a double.

### `fromisoformat(date_string: str) -> DateTime`

Parse a datetime from ISO 8601 format string.

### `strftime(format: str) -> str`

Format the datetime using Python strftime format codes.

### `strptime(date_string: str, format: str) -> DateTime`

Parse a datetime from a string using Python strftime format codes.

### `astimezone(tz: Timezone) -> DateTime`

Convert to a different timezone.

## timedelta

Represents a date (year, month, day).

### Properties

| Name | Type | Description |
|------|------|-------------|
| `days` | `int` | The days component of the time interval. |
| `total_seconds` | `float` | The total number of seconds represented by this timedelta. |

### `abs() -> Timedelta`

Return the absolute value of the timedelta.

## timezone

Represents a date (year, month, day).

### Constants

| Name | Type | Description |
|------|------|-------------|
| `utc` | `Timezone` | The UTC timezone. |

### `utcoffset() -> Timedelta`

Return the UTC offset.

### `tzname() -> str`

Return the timezone name.
