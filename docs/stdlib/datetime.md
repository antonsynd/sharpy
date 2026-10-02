# datetime

Classes for working with dates and times.

```python
import datetime
```

## Properties

| Name | Type | Description |
|------|------|-------------|
| `date_type` | `Type` | The Date type. |
| `time_type` | `Type` | The Time type. |
| `date_time_type` | `Type` | The DateTime type. |
| `timedelta_type` | `Type` | The Timedelta type. |
| `timezone_type` | `Type` | The Timezone type. |

## date

Represents a date (year, month, day).

### Properties

| Name | Type | Description |
|------|------|-------------|
| `year` | `int` | The year component. |
| `month` | `int` | The month component (1-12). |
| `day` | `int` | The day component (1-31). |

### `__str__() -> str`

`repr()` uses the same method. Return the ISO 8601 string representation (yyyy-MM-dd).

### `today() -> date`

Return the current local date.

### `weekday() -> int`

Return the day of the week (0=Monday through 6=Sunday).

### `isoweekday() -> int`

Return the ISO day of the week (1=Monday through 7=Sunday).

### `isoformat() -> str`

Return the ISO 8601 formatted string.

### `replace(year: int | None = None, month: int | None = None, day: int | None = None) -> date`

Return a new Date with replaced components.

### `toordinal() -> int`

Return the proleptic Gregorian ordinal of the date.

### `fromordinal(ordinal: int) -> date`

Create a Date from a proleptic Gregorian ordinal.

### `fromisoformat(date_string: str) -> date`

Parse a date from ISO 8601 format string.

### `strftime(format: str) -> str`

Format the date using Python strftime format codes.

## time

Represents a time (hour, minute, second, microsecond).

### Properties

| Name | Type | Description |
|------|------|-------------|
| `hour` | `int` | The hour component (0-23). |
| `minute` | `int` | The minute component (0-59). |
| `second` | `int` | The second component (0-59). |
| `microsecond` | `int` | The microsecond component (0-999999). |

### `__str__() -> str`

`repr()` uses the same method. Python's `str(time)`, its `isoformat()`: `HH:MM:SS`, with `.ffffff` only when
the microseconds are non-zero (#2043).

### `isoformat() -> str`

Return the ISO 8601 formatted string.

### `strftime(format: str) -> str`

Format the time using Python strftime format codes.

## datetime

A combination of a date and a time.

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
| `tzinfo` | `ITzinfo \| None` | The timezone info, or null if naive. |
| `date_component` | `date` | The date component of this datetime. |
| `time_component` | `time` | The time component of this datetime. |

### `__str__() -> str`

`repr()` uses the same method. Python's `str(datetime)`, its `isoformat(" ")`: the `.ffffff` part only when the
microseconds are non-zero (#2043).

### `now() -> datetime`

Return the current local datetime.

### `utcnow() -> datetime`

Return the current UTC datetime.

### `combine(date: date, time: time) -> datetime`

Combine a date and a time to create a datetime.

### `weekday() -> int`

Return the day of the week (0=Monday through 6=Sunday).

### `isoweekday() -> int`

Return the ISO day of the week (1=Monday through 7=Sunday).

### `isoformat(sep: str | None = None) -> str`

Return the ISO 8601 formatted string.

### `replace(year: int | None = None, month: int | None = None, day: int | None = None, hour: int | None = None, minute: int | None = None, second: int | None = None) -> datetime`

Return a new DateTime with replaced components.

### `timestamp() -> float`

Return the Unix timestamp as a double.

### `fromisoformat(date_string: str) -> datetime`

Parse a datetime from ISO 8601 format string.

### `strftime(format: str) -> str`

Format the datetime using Python strftime format codes.

### `strptime(date_string: str, format: str) -> datetime`

Parse a datetime from a string using Python strftime format codes.

### `astimezone(tz: ITzinfo) -> datetime`

Convert to a different timezone.

## timedelta

Represents the difference between two dates or times.

### Properties

| Name | Type | Description |
|------|------|-------------|
| `days` | `int` | The days component of the time interval. |
| `seconds` | `int` | Gets the remaining seconds after extracting days (0-86399). This matches Python's \`timedelta.seconds\` property. For the total number of seconds, use \`total_seconds\`. |
| `microseconds` | `int` | The microseconds component of the time interval. |
| `total_seconds` | `float` | The total number of seconds represented by this timedelta. |

### `__str__() -> str`

`repr()` uses the same method. Python's `str(timedelta)`: `[D day[s], ]H:MM:SS[.ffffff]` over the normalized
(days, seconds, microseconds) triple — days floored, so `-1 day, 23:00:00` is minus an
hour (#2043).

### `abs() -> timedelta`

Return the absolute value of the timedelta.

## timezone

Represents a fixed-offset timezone.

### Constants

| Name | Type | Description |
|------|------|-------------|
| `utc` | `timezone` | The UTC timezone. |

### `utcoffset(dt: datetime | None = None) -> timedelta`

Return the UTC offset (dt parameter ignored for fixed-offset zones).

### `tzname(dt: datetime | None = None) -> str`

Return the timezone name (dt parameter ignored for fixed-offset zones): the given name, else
python's `UTC` for a zero offset and `UTC±HH:MM[:SS[.ffffff]]` otherwise.

### `dst(dt: datetime | None = None) -> timedelta`

Return DST offset (always zero for fixed-offset zones).

### `__str__() -> str`

`repr()` uses the same method. python's `str(timezone)`: its `tzname(None)`.
