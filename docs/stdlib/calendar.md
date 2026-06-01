# calendar

```python
import calendar
```

## Constants

| Name | Type | Description |
|------|------|-------------|
| `monday` | `int` |  |
| `tuesday` | `int` |  |
| `wednesday` | `int` |  |
| `thursday` | `int` |  |
| `friday` | `int` |  |
| `saturday` | `int` |  |
| `sunday` | `int` |  |
| `day_name` | `list[str]` |  |
| `day_abbr` | `list[str]` |  |
| `month_name` | `list[str]` |  |
| `month_abbr` | `list[str]` |  |

## Functions

### `calendar.isleap(year: int) -> bool`

### `calendar.leapdays(y1: int, y2: int) -> int`

### `calendar.weekday(year: int, month: int, day: int) -> int`

### `calendar.monthcalendar(year: int, month: int) -> list[list[int]]`

### `calendar.month(year: int, month: int, w: int = 2, l: int = 1) -> str`

### `calendar.calendar_text(year: int, w: int = 2, l: int = 1, c: int = 6, m: int = 3) -> str`

### `calendar.setfirstweekday(weekday: int)`

### `calendar.prmonth(year: int, month: int, w: int = 2, l: int = 1)`

### `calendar.prcal(year: int, w: int = 2, l: int = 1, c: int = 6, m: int = 3)`

### `calendar.timegm(year: int, month: int, day: int, hour: int, minute: int, second: int) -> long`

## Calendar

### Properties

| Name | Type | Description |
|------|------|-------------|
| `firstweekday` | `int` |  |

### `itermonthdays(year: int, month: int) -> Iterable[int]`

### `monthdayscalendar(year: int, month: int) -> list[list[int]]`

## HTMLCalendar

### `formatmonth(year: int, month: int, withyear: bool = True) -> str`

### `formatyear(year: int, width: int = 3) -> str`

### `formatyearpage(year: int, width: int = 3, css: str | None = None, encoding: str | None = None) -> str`

## TextCalendar

### `formatmonth(year: int, month: int, w: int = 2, l: int = 1) -> str`

### `prmonth(year: int, month: int, w: int = 2, l: int = 1)`

### `formatyear(year: int, w: int = 2, l: int = 1, c: int = 6, m: int = 3) -> str`

### `pryear(year: int, w: int = 2, l: int = 1, c: int = 6, m: int = 3)`
