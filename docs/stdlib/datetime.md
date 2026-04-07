# datetime

Represents a date (year, month, day).

```python
import datetime
```

## Properties

| Name | Type | Description |
|------|------|-------------|
| `year` | `int` | The year component. |
| `month` | `int` | The month component (1-12). |
| `day` | `int` | The day component (1-31). |
| `hour` | `int` | The hour component (0-23). |
| `minute` | `int` | The minute component (0-59). |
| `second` | `int` | The second component (0-59). |
| `days` | `int` | The days component of the time interval. |
| `seconds` | `int` | Gets the seconds component (0-59) of the time interval, not the total number of seconds. This matches the behavior of Python's \`timedelta.seconds\` property. For the total number of seconds, use \`TotalSeconds\`. |
| `total_seconds` | `float` | The total number of seconds represented by this timedelta. |

## Functions

### `datetime.today() -> Date`

Return the current local date.

**Returns:** A `Date` representing today.

```python
d = date.today()
print(d)    # "2024-01-15"
```

### `datetime.now() -> DateTime`

Return the current local datetime.

**Returns:** A `DateTime` representing the current local date and time.

```python
dt = datetime.now()
print(dt.year, dt.month, dt.day)
```

### `datetime.utcnow() -> DateTime`

Return the current UTC datetime.

### `datetime.combine(date: Date, time: Time) -> DateTime`

Combine a date and a time to create a datetime.

**Parameters:**

- `date` (Date) -- The date component.
- `time` (Time) -- The time component.

**Returns:** A new `DateTime` combining the date and time.

```python
d = date(2024, 1, 15)
t = time(14, 30)
dt = datetime.combine(d, t)    # 2024-01-15 14:30:00.000000
```
