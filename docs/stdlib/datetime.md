# datetime

Classes for working with dates and times.

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

### `replace(year: int | None = None, month: int | None = None, day: int | None = None) -> Date`

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
| `microsecond` | `int` | The microsecond component (0-999999). |

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
| `microsecond` | `int` | The microsecond component (0-999999). |
| `tzinfo` | `ITzinfo | None` | The timezone info, or null if naive. |
| `date_component` | `Date` | The date component of this datetime. |
| `time_component` | `Time` | The time component of this datetime. |

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

### `isoformat(sep: str | None = None) -> str`

Return the ISO 8601 formatted string.

### `replace(year: int | None = None, month: int | None = None, day: int | None = None, hour: int | None = None, minute: int | None = None, second: int | None = None) -> DateTime`

Return a new DateTime with replaced components.

### `timestamp() -> float`

Return the Unix timestamp as a double.

### `fromisoformat(date_string: str) -> DateTime`

Parse a datetime from ISO 8601 format string.

### `strftime(format: str) -> str`

Format the datetime using Python strftime format codes.

### `strptime(date_string: str, format: str) -> DateTime`

Parse a datetime from a string using Python strftime format codes.

### `astimezone(tz: ITzinfo) -> DateTime`

Convert to a different timezone.

## timedelta

Represents a date (year, month, day).

### Properties

| Name | Type | Description |
|------|------|-------------|
| `days` | `int` | The days component of the time interval. |
| `seconds` | `int` | Gets the remaining seconds after extracting days (0-86399). This matches Python's \`timedelta.seconds\` property. For the total number of seconds, use \`TotalSeconds\`. |
| `microseconds` | `int` | The microseconds component of the time interval. |
| `total_seconds` | `float` | The total number of seconds represented by this timedelta. |

### `abs() -> Timedelta`

Return the absolute value of the timedelta.

## timezone

Represents a date (year, month, day).

### Constants

| Name | Type | Description |
|------|------|-------------|
| `utc` | `Timezone` | The UTC timezone. |

### Properties

| Name | Type | Description |
|------|------|-------------|
| `date_type` | `Type` | The Date type. |
| `time_type` | `Type` | The Time type. |
| `date_time_type` | `Type` | The DateTime type. |
| `timedelta_type` | `Type` | The Timedelta type. |
| `timezone_type` | `Type` | The Timezone type. |

### `utcoffset(dt: DateTime | None = None) -> Timedelta`

Return the UTC offset (dt parameter ignored for fixed-offset zones).

### `tzname(dt: DateTime | None = None) -> str`

Return the timezone name (dt parameter ignored for fixed-offset zones).

### `dst(dt: DateTime | None = None) -> Timedelta`

Return DST offset (always zero for fixed-offset zones).
