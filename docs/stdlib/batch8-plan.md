<!-- Verified by /verify-plan on 2026-05-29 -->
<!-- Verification result: PASS WITH CORRECTIONS -->

# Stdlib Batch 8: pprint, calendar, zoneinfo, colorsys

## Context

Implement the four "utilities" stdlib modules from the [Tier 2 roadmap](roadmap.md) Batch 8. These are smaller modules that round out Sharpy's standard library: `pprint` for pretty-printing data structures, `calendar` and `zoneinfo` which pair with the existing `datetime` module, and `colorsys` for color space conversions.

**GitHub issues:**
- [#745](https://github.com/antonsynd/sharpy/issues/745) — pprint module (data structure pretty-printing)
- [#759](https://github.com/antonsynd/sharpy/issues/759) — calendar module (calendar operations)
- [#760](https://github.com/antonsynd/sharpy/issues/760) — zoneinfo module (IANA time zone support)
- [#758](https://github.com/antonsynd/sharpy/issues/758) — colorsys module (color space conversions)

## Current State

- **33+ stdlib modules** exist in `src/Sharpy.Stdlib/` (31 original + Toml + Yaml; earlier batches may add more by the time this plan executes)
- None of pprint, calendar, zoneinfo, or colorsys exist yet
- Module infrastructure is mature: `[SharpyModule]`/`[SharpyModuleType]` attributes, `ModuleRegistry` discovery, `.spy` source files, per-module `.csproj` files
- **Existing datetime module** (`src/Sharpy.Stdlib/Datetime/`) provides `Date`, `DateTime`, `Timedelta`, and `Timezone` (fixed-offset only). Calendar and zoneinfo integrate with these types.
- `Timezone` is a concrete class with fixed offset — ZoneInfo needs DST-aware behavior, requiring an `ITzinfo` interface extraction (see Design Decisions)
- No new NuGet dependencies needed — all four modules use BCL types or are pure custom code

## Design Decisions

1. **Implementation order: colorsys → pprint → calendar → zoneinfo.** Rationale: colorsys is the simplest (~100 lines, pure math, no dependencies). pprint is self-contained but larger. calendar depends on datetime module types. zoneinfo is last because it requires refactoring the datetime module's timezone API (ITzinfo interface extraction).

2. **All modules are hand-written C#** (not `.spy`-generated). Rationale: pprint has complex formatting logic with recursion and circular reference detection. calendar has text formatting tables. zoneinfo wraps BCL types. colorsys is pure math. All are better expressed in C# directly.

3. **colorsys: pure static functions, no classes.** All 6 functions are module-level. Values are `double` in [0.0, 1.0] range. Hue is [0.0, 1.0] (not degrees — matching Python exactly). Returns are `(double, double, double)` tuples. Verified: Python returns float tuples.

4. **pprint: PrettyPrinter class + module-level convenience functions.** Module-level functions (`pprint()`, `pformat()`, `isreadable()`, `isrecursive()`) delegate to a default `PrettyPrinter` instance. `PrettyPrinter` parameters: `indent` (default 1), `width` (default 80), `depth` (default null = unlimited), `compact` (default false), `sortDicts` (default true). `underscore_numbers` is skipped for v1 (low priority, Python 3.10+ feature).

5. **pprint: circular reference detection via identity tracking.** Use `HashSet<object>` with `ReferenceEqualityComparer` to track visited objects during formatting. When a circular reference is detected, emit `<Recursion on {typeName} with id={hashCode}>` (matching Python's format).

6. **pprint: Sharpy type support.** Support formatting of: `Dict<TK,TV>`, `List<T>`, `Set<T>`, `string`, `int`, `long`, `double`, `float`, `bool`, `null`, tuples (via `ITuple` or `ValueTuple`). Unsupported types fall back to `ToString()`. Depth limiting replaces deeper structures with `...`.

7. **calendar: module-level functions + TextCalendar/HTMLCalendar classes.** Module-level functions (`month()`, `calendar()`, `isleap()`, `leapdays()`, `weekday()`, `monthrange()`, `monthcalendar()`) delegate to a default `TextCalendar`. Day constants: `MONDAY=0` through `SUNDAY=6` (matching Python ISO convention). Verified: `weekday(2026,5,28)` = 3 (Thursday).

8. **calendar: `monthrange()` returns `(int, int)` tuple** — (weekday of first day, number of days in month). Python 3.12+ returns weekday as IntEnum constant, but Sharpy returns plain `int` since we don't have IntEnum. Verified: `monthrange(2026, 2)` = `(6, 28)` (Sunday, 28 days).

9. **calendar: `Calendar` base class with iteration methods.** `Calendar(firstweekday=0)` provides `itermonthdays()`, `itermonthdays2()`, `monthdatescalendar()`, `monthdays2calendar()`, `yeardatescalendar()`. `TextCalendar` and `HTMLCalendar` extend with formatting. The `firstweekday` defaults to Monday (0) matching Python.

10. **calendar: day/month name constants from hardcoded English arrays.** `day_name`, `day_abbr`, `month_name`, `month_abbr` as static read-only lists. Locale customization is deferred — v1 uses English names only (matching most Python usage). CultureInfo-based locale support can be added later.

11. **zoneinfo: `ZoneInfo` class wrapping `System.TimeZoneInfo`.** IANA zone names supported natively via `TimeZoneInfo.FindSystemTimeZoneById()` (.NET 6+). `ZoneInfo.Key` property returns the IANA zone ID. `available_timezones()` returns all known zone IDs.

12. **zoneinfo: ITzinfo interface extraction for datetime integration.** The existing `Timezone` class is a fixed-offset type. ZoneInfo is DST-aware (offset depends on the datetime). To integrate both with `DateTime.Astimezone()`:
    - Extract `ITzinfo` interface with `Utcoffset(DateTime? dt = null)`, `Tzname(DateTime? dt = null)`, `Dst(DateTime? dt = null)` methods
    - `Timezone` implements `ITzinfo` (ignores the `dt` parameter, returns fixed offset)
    - `ZoneInfo` implements `ITzinfo` (uses `dt` parameter to compute DST-aware offset)
    - Update `DateTime` class to accept `ITzinfo` instead of `Timezone`
    - This is a **source-compatible** change: existing code passing `Timezone` still works since `Timezone` implements `ITzinfo`

13. **zoneinfo: `ZoneInfoNotFoundError` for invalid zone names.** Matches Python's exception. Maps from `TimeZoneNotFoundException` thrown by .NET.

14. **zoneinfo: DST handling.** `ZoneInfo.Utcoffset(dt)` returns the total UTC offset including DST. `ZoneInfo.Dst(dt)` returns only the DST component (`TimeSpan.Zero` when not in DST). Uses `TimeZoneInfo.GetUtcOffset()` and `TimeZoneInfo.IsDaylightSavingTime()`.

15. **C# 9.0 compatibility** for `netstandard2.1` target. No file-scoped namespaces, no record structs, no global usings. Use `#if NET10_0_OR_GREATER` where needed.

16. **No new NuGet dependencies.** All four modules use BCL types or are pure custom code.

## Implementation

Module implementation order: colorsys (~100 lines) → pprint (~500 lines) → calendar (~600 lines) → zoneinfo (~300 lines + datetime refactoring). Each module follows the standard stdlib pattern.

### Phase 1: colorsys Module

**Goal:** Implement the complete colorsys module — the simplest of the four.

#### Tasks

1. **Create colorsys module and implement all functions** — `src/Sharpy.Stdlib/Colorsys/__Init__.cs`, `src/Sharpy.Stdlib/Colorsys/Colorsys.cs`
   - Create `Colorsys/` directory under `src/Sharpy.Stdlib/`
   - Add `__Init__.cs` with `[SharpyModule("colorsys")]` on `public static partial class ColorsysModule`
   - Add `Colorsys.cs` as partial class with 6 static methods:
     - `(double h, double s, double v) RgbToHsv(double r, double g, double b)`
     - `(double r, double g, double b) HsvToRgb(double h, double s, double v)`
     - `(double h, double l, double s) RgbToHls(double r, double g, double b)`
     - `(double r, double g, double b) HlsToRgb(double h, double l, double s)`
     - `(double y, double i, double q) RgbToYiq(double r, double g, double b)`
     - `(double r, double g, double b) YiqToRgb(double y, double i, double q)`
   - Return ValueTuples — these map to Sharpy tuples at the codegen layer
   - Formulas must match Python's CPython implementation exactly for bit-perfect compatibility:
     - HSV: standard max/min-based conversion. `h = 0.0` when `maxc == minc`
     - HLS: standard lightness = (max+min)/2. `s` computation depends on `l <= 0.5`
     - YIQ: `y = 0.30*r + 0.59*g + 0.11*b`, `i = 0.74*(r-y) - 0.27*(b-y)`, `q = 0.48*(r-y) + 0.41*(b-y)` (NTSC coefficients, matching Python)
   - Verified Python behavior:
     - `rgb_to_hsv(0.2, 0.4, 0.6)` → `(0.5833..., 0.6666..., 0.6)`
     - `rgb_to_hsv(0, 0, 0)` → `(0.0, 0.0, 0)` (black)
     - `rgb_to_hsv(1, 1, 1)` → `(0.0, 0.0, 1)` (white)
     - `rgb_to_hls(0.2, 0.4, 0.6)` → `(0.5833..., 0.4, 0.4999...)`
     - `rgb_to_yiq(0.2, 0.4, 0.6)` → `(0.362, -0.18414, 0.01982)`
   - Acceptance: all 6 functions compile, roundtrip correctly, and match Python output
   - Commit: `feat(stdlib): implement colorsys module with RGB/HSV/HLS/YIQ conversions`

2. **Create colorsys project file and spy stub** — `src/Sharpy.Stdlib/modules/Sharpy.Stdlib.Colorsys.csproj`, `src/Sharpy.Stdlib/spy/colorsys_module.spy`
   - Project file: copy pattern from `Sharpy.Stdlib.Hashlib.csproj`, set `<AssemblyName>Sharpy.Stdlib.Colorsys</AssemblyName>`, `<Compile Include="../Colorsys/**/*.cs" />`
   - Spy stub: define 6 module-level functions with correct signatures:
     ```python
     def rgb_to_hsv(r: float, g: float, b: float) -> tuple[float, float, float]: ...
     def hsv_to_rgb(h: float, s: float, v: float) -> tuple[float, float, float]: ...
     # etc.
     ```
   - Acceptance: `dotnet build src/Sharpy.Stdlib/modules/Sharpy.Stdlib.Colorsys.csproj` succeeds
   - Commit: `build(stdlib): add colorsys project file and spy stub`

3. **Add colorsys tests** — `src/Sharpy.Stdlib.Tests/ColorsysTests.cs`
   - Test RGB→HSV roundtrip: `rgb_to_hsv(r,g,b)` → `hsv_to_rgb(h,s,v)` equals original within tolerance
   - Test RGB→HLS roundtrip: same pattern
   - Test RGB→YIQ roundtrip: same pattern
   - Test edge cases: black `(0,0,0)`, white `(1,1,1)`, pure red `(1,0,0)`, pure green `(0,1,0)`, pure blue `(0,0,1)`
   - Test known values against Python output (use `Assert.Equal(expected, actual, precision: 10)`)
   - Test boundary values: all zeros, all ones
   - Acceptance: all tests pass
   - Commit: `test(stdlib): add colorsys module tests`

### Phase 2: pprint Module — PrettyPrinter Class

**Goal:** Implement the PrettyPrinter class with recursive formatting and circular reference detection.

#### Tasks

4. **Create pprint module and PrettyPrinter class** — `src/Sharpy.Stdlib/Pprint/__Init__.cs`, `src/Sharpy.Stdlib/Pprint/PrettyPrinter.cs`
   - Create `Pprint/` directory under `src/Sharpy.Stdlib/`
   - Add `__Init__.cs` with `[SharpyModule("pprint")]` on `public static partial class PprintModule`
   - Add `PrettyPrinter.cs` with `[SharpyModuleType("pprint", "PrettyPrinter")]` sealed class:
     - Constructor: `PrettyPrinter(int indent = 1, int width = 80, int? depth = null, bool compact = false, bool sortDicts = true)`
     - `void Pprint(object? obj)` — format and print to stdout
     - `string Pformat(object? obj)` — format and return string
     - `bool Isreadable(object? obj)` — true if the representation can be parsed back
     - `bool Isrecursive(object? obj)` — true if the object contains circular references
   - Internal formatting engine:
     - `string Format(object? obj, int currentIndent, int allowance, HashSet<object> context)` — recursive formatter
     - Circular reference tracking: `context` set using `ReferenceEqualityComparer` (same approach as `SemanticInfo`). Check if object already in set before recursing; if found, return `<Recursion on {type} with id={RuntimeHelpers.GetHashCode(obj)}>`.
     - Type dispatch:
       - `null` → `"None"`
       - `bool` → `"True"` or `"False"`
       - `string` → repr with quotes (escape special chars), break long strings across lines
       - `int`, `long`, `double`, `float` → `ToString()` with appropriate format
       - `Dict<TK,TV>` → `{key: value, ...}` with sorted keys (if `sortDicts`). One entry per line if exceeds width.
       - `List<T>` → `[item, ...]`. Compact mode: fit multiple items per line. Non-compact: one per line if exceeds width.
       - `Set<T>` → `{item, ...}` with sorted elements
       - Tuples (ValueTuple/ITuple) → `(item, ...)`
       - Other objects → `obj.ToString()`
     - Width management: track current column position. If formatted representation fits within `width - currentIndent - allowance`, emit on one line. Otherwise, break across lines with proper indentation.
     - Depth management: when `depth` is set and current depth exceeds it, emit `...` for nested collections
   - Acceptance: PrettyPrinter produces Python-matching output for all supported types
   - Commit: `feat(stdlib): implement pprint PrettyPrinter class`

5. **Add module-level convenience functions** — `src/Sharpy.Stdlib/Pprint/PprintFunctions.cs`
   - Add to `public static partial class PprintModule`:
     - `void Pprint(object? obj, int indent = 1, int width = 80, int? depth = null, bool compact = false, bool sortDicts = true)` — create temporary `PrettyPrinter` and call `Pprint(obj)`
     - `string Pformat(object? obj, int indent = 1, int width = 80, int? depth = null, bool compact = false, bool sortDicts = true)` — create temporary `PrettyPrinter` and call `Pformat(obj)`
     - `bool Isreadable(object? obj)` — create default `PrettyPrinter` and call `Isreadable(obj)`
     - `bool Isrecursive(object? obj)` — create default `PrettyPrinter` and call `Isrecursive(obj)`
   - Acceptance: module-level functions compile and delegate correctly
   - Commit: `feat(stdlib): add pprint module-level convenience functions`

6. **Create pprint project file and spy stub** — `src/Sharpy.Stdlib/modules/Sharpy.Stdlib.Pprint.csproj`, `src/Sharpy.Stdlib/spy/pprint_module.spy`
   - Project file: standard pattern, `<AssemblyName>Sharpy.Stdlib.Pprint</AssemblyName>`
   - Spy stub: define `PrettyPrinter` class and module-level functions
   - Acceptance: project builds successfully
   - Commit: `build(stdlib): add pprint project file and spy stub`

7. **Add pprint tests** — `src/Sharpy.Stdlib.Tests/PprintTests.cs`
   - Test basic formatting:
     - `pformat(42)` → `"42"`
     - `pformat("hello")` → `"'hello'"`
     - `pformat(None)` → `"None"`
     - `pformat(True)` → `"True"`
   - Test dict formatting:
     - Simple dict fits one line: `{'a': 1, 'b': 2}`
     - Dict exceeding width wraps to multiple lines with indentation
     - `sort_dicts=True` (default) sorts keys alphabetically
     - `sort_dicts=False` preserves insertion order
   - Test list formatting:
     - Short list fits one line: `[1, 2, 3]`
     - Long list wraps to one item per line
     - `compact=True` fits multiple items per line
     - `compact=False` (default) uses one per line when wrapping
   - Test nested structures:
     - Dict containing lists and other dicts — proper indentation
     - `depth=2` truncates deeper nesting with `...`
   - Test circular reference detection:
     - Self-referencing dict → `<Recursion on Dict...>`
     - `isrecursive()` returns true for circular structures
     - `isreadable()` returns false for circular structures
   - Test set formatting: `{1, 2, 3}`
   - Test tuple formatting: `(1, 2, [3, 4])`
   - Test PrettyPrinter class:
     - Custom `indent=4` and `width=40`
   - Acceptance: all tests pass
   - Commit: `test(stdlib): add pprint module tests`

### Phase 3: calendar Module

**Goal:** Implement the calendar module with text and HTML formatting.

#### Tasks

8. **Implement Calendar base class and constants** — `src/Sharpy.Stdlib/Calendar/__Init__.cs`, `src/Sharpy.Stdlib/Calendar/CalendarBase.cs`
   - Create `Calendar/` directory under `src/Sharpy.Stdlib/`
   - Add `__Init__.cs` with `[SharpyModule("calendar")]` on `public static partial class CalendarModule`
   - Add module-level constants (as static fields on `CalendarModule`):
     - `int MONDAY = 0`, `TUESDAY = 1`, ..., `SUNDAY = 6`
     - `List<string> DayName` — `["Monday", "Tuesday", ..., "Sunday"]`
     - `List<string> DayAbbr` — `["Mon", "Tue", ..., "Sun"]`
     - `List<string> MonthName` — `["", "January", ..., "December"]` (index 0 is empty, matching Python)
     - `List<string> MonthAbbr` — `["", "Jan", ..., "Dec"]`
   - Add `CalendarBase.cs` with `[SharpyModuleType("calendar", "Calendar")]` class:
     - Constructor: `Calendar(int firstweekday = 0)`
     - `int Firstweekday { get; set; }` — settable, matching Python
     - `IEnumerable<int> Itermonthdays(int year, int month)` — yield day numbers (0 for padding days outside month)
     - `IEnumerable<(int day, int weekday)> Itermonthdays2(int year, int month)` — yield (day, weekday) tuples
     - `List<List<int>> Monthdayscalendar(int year, int month)` — return weeks as lists of day numbers
     - `List<List<(int day, int weekday)>> Monthdays2calendar(int year, int month)` — return weeks as lists of (day, weekday) tuples
   - Implementation: use `System.DateTime` and `System.Globalization.GregorianCalendar` for date calculations. Map `DayOfWeek` (Sunday=0) to Python convention (Monday=0) via `((int)dow + 6) % 7`.
   - Acceptance: Calendar base class compiles with all iteration methods
   - Commit: `feat(stdlib): implement calendar Calendar base class and constants`

9. **Implement TextCalendar** — `src/Sharpy.Stdlib/Calendar/TextCalendar.cs`
   - Create `[SharpyModuleType("calendar", "TextCalendar")]` class extending `Calendar`:
     - Constructor: `TextCalendar(int firstweekday = 0)`
     - `string Formatmonth(int year, int month, int w = 2, int l = 1)` — format a month as text:
       - `w` = width of each day column (minimum 2)
       - `l` = number of blank lines between weeks (minimum 1)
       - Header: centered month name + year
       - Day abbreviations header (respecting `firstweekday`)
       - Day numbers right-justified in columns
       - Verified format: matches Python's output exactly (column widths, spacing, centering)
     - `void Prmonth(int year, int month, int w = 2, int l = 1)` — print `Formatmonth` to stdout
     - `string Formatyear(int year, int w = 2, int l = 1, int c = 6, int m = 3)` — format full year:
       - `c` = spacing between month columns
       - `m` = months per row (default 3)
       - Centered year header, months in `m`-column grid
     - `void Pryear(int year, int w = 2, int l = 1, int c = 6, int m = 3)` — print `Formatyear` to stdout
   - Acceptance: `Formatmonth(2026, 6)` matches Python's output exactly
   - Commit: `feat(stdlib): implement calendar TextCalendar`

10. **Implement HTMLCalendar** — `src/Sharpy.Stdlib/Calendar/HTMLCalendar.cs`
    - Create `[SharpyModuleType("calendar", "HTMLCalendar")]` class extending `Calendar`:
      - Constructor: `HTMLCalendar(int firstweekday = 0)`
      - `string Formatmonth(int year, int month, bool withyear = true)` — HTML table for one month:
        - `<table>` with CSS classes: `month`, day-of-week classes (`mon`, `tue`, ..., `sun`)
        - `<th>` for month/year header
        - `<td>` for each day (empty `<td>` for padding days)
        - Matching Python's class names and structure
      - `string Formatyear(int year, int width = 3)` — HTML table of tables for full year
      - `string Formatyearpage(int year, int width = 3, string? css = null, string? encoding = null)` — complete HTML page
    - Acceptance: `Formatmonth(2026, 6)` produces valid HTML matching Python's structure
    - Commit: `feat(stdlib): implement calendar HTMLCalendar`

11. **Implement module-level convenience functions** — `src/Sharpy.Stdlib/Calendar/CalendarFunctions.cs`
    - Add to `public static partial class CalendarModule`:
      - `bool Isleap(int year)` — `year % 4 == 0 && (year % 100 != 0 || year % 400 == 0)`
      - `int Leapdays(int y1, int y2)` — count of leap years in `[y1, y2)` range (exclusive of y2, matching Python)
      - `int Weekday(int year, int month, int day)` — return 0=Monday through 6=Sunday. Use `new System.DateTime(year, month, day).DayOfWeek` with mapping.
      - `(int, int) Monthrange(int year, int month)` — return `(weekday of first day, number of days)`. Verified: `monthrange(2026, 2)` = `(6, 28)`.
      - `List<List<int>> Monthcalendar(int year, int month)` — delegate to default `TextCalendar().Monthdayscalendar()`
      - `string Month(int year, int month, int w = 2, int l = 1)` — delegate to default `TextCalendar().Formatmonth()`
      - `string CalendarText(int year, int w = 2, int l = 1, int c = 6, int m = 3)` — delegate to `TextCalendar().Formatyear()`. Named `CalendarText` to avoid collision with `Calendar` class; spy stub maps `calendar.calendar()` to this.
      - `void Setfirstweekday(int weekday)` — set module-level default first weekday
      - `void Prmonth(int year, int month, int w = 2, int l = 1)` — print month to stdout
      - `void Prcal(int year, int w = 2, int l = 1, int c = 6, int m = 3)` — print year calendar to stdout
      - `long Timegm(int year, int month, int day, int hour, int minute, int second)` — inverse of `time.gmtime()`, convert UTC time tuple to Unix timestamp. Use `new DateTimeOffset(year, month, day, hour, minute, second, TimeSpan.Zero).ToUnixTimeSeconds()`.
    - Acceptance: all convenience functions compile and match Python behavior
    - Commit: `feat(stdlib): implement calendar module-level functions`

12. **Create calendar project file and spy stub** — `src/Sharpy.Stdlib/modules/Sharpy.Stdlib.Calendar.csproj`, `src/Sharpy.Stdlib/spy/calendar_module.spy`
    - Project file: standard pattern, `<AssemblyName>Sharpy.Stdlib.Calendar</AssemblyName>`
    - Spy stub: define `Calendar`, `TextCalendar`, `HTMLCalendar` classes and all module-level functions + constants
    - Acceptance: project builds successfully
    - Commit: `build(stdlib): add calendar project file and spy stub`

13. **Add calendar tests** — `src/Sharpy.Stdlib.Tests/CalendarTests.cs`
    - Test `isleap`: 2024 → true, 2025 → false, 2000 → true, 1900 → false
    - Test `leapdays`: `leapdays(2000, 2024)` → 6
    - Test `weekday`: `weekday(2026, 5, 28)` → 3 (Thursday)
    - Test `monthrange`:
      - `monthrange(2026, 2)` → `(6, 28)` (Sunday, 28 days)
      - `monthrange(2024, 2)` → `(3, 29)` (Thursday, 29 days — leap year)
      - `monthrange(2026, 1)` → `(3, 31)` (Thursday, 31 days)
    - Test `monthcalendar(2026, 6)`: verify week structure matches Python
    - Test `TextCalendar.Formatmonth`:
      - Default firstweekday (Monday): verify exact text output matches Python
      - Sunday first: verify header and day order
    - Test `HTMLCalendar.Formatmonth`:
      - Verify table structure contains correct CSS classes
      - Verify day numbers are correct
    - Test `Calendar.Itermonthdays`: verify padding zeros for out-of-month days
    - Test `firstweekday` customization
    - Test `Timegm`: `timegm(2026,1,1,0,0,0)` → `1767225600`
    - Test edge cases: February in leap/non-leap years, December (month 12), January (month 1)
    - Acceptance: all tests pass
    - Commit: `test(stdlib): add calendar module tests`

### Phase 4: zoneinfo Module — ITzinfo Interface

**Goal:** Extract the ITzinfo interface from the datetime module to enable both fixed-offset and DST-aware timezone support.

#### Tasks

14. **Extract ITzinfo interface from datetime module** — `src/Sharpy.Stdlib/Datetime/ITzinfo.cs`, modify `Datetime.cs`
    - Create `ITzinfo.cs` in `src/Sharpy.Stdlib/Datetime/`:
      ```csharp
      namespace Sharpy
      {
          public interface ITzinfo
          {
              Timedelta Utcoffset(DateTime? dt = null);
              string Tzname(DateTime? dt = null);
              Timedelta Dst(DateTime? dt = null);
          }
      }
      ```
      Note: `DateTime?` parameter is nullable (default null) so callers can omit it for fixed-offset zones. [CORRECTED: Must use `DateTime?` (nullable annotation) because `<Nullable>enable</Nullable>` and `TreatWarningsAsErrors` are both active — using non-nullable `DateTime` with `null` default would produce CS8625 warning→error.]
    - Update `Timezone` class to implement `ITzinfo`:
      - Add `: ITzinfo` to class declaration
      - Update `Utcoffset()` → `Utcoffset(DateTime? dt = null)` (ignores `dt`, returns fixed offset) [CORRECTED: nullable annotation]
      - Add `Tzname(DateTime? dt = null)` (ignores `dt`, returns `_name`) [CORRECTED: nullable annotation]
      - Add `Dst(DateTime? dt = null)` → returns `new Timedelta()` (zero — no DST for fixed-offset) [CORRECTED: nullable annotation]
    - Update `DateTime` class:
      - Change `_tzinfo` field type from `Timezone?` to `ITzinfo?`
      - Change `Tzinfo` property return type to `ITzinfo?`
      - Change constructor parameter type to `ITzinfo?`
      - Change `Astimezone` parameter type to `ITzinfo`
      - Update `Astimezone` implementation: call `tz.Utcoffset(targetDt)` instead of `tz.Utcoffset()` (pass the datetime being converted)
    - This is a **source-compatible** change: existing code passing `Timezone` still compiles since `Timezone` implements `ITzinfo`
    - Acceptance: all existing datetime tests still pass, `DateTime` accepts `ITzinfo`
    - Commit: `refactor(stdlib): extract ITzinfo interface from datetime Timezone`

### Phase 5: zoneinfo Module — ZoneInfo Class

**Goal:** Implement the ZoneInfo class with DST-aware timezone support.

#### Tasks

15. **Implement ZoneInfo class** — `src/Sharpy.Stdlib/Zoneinfo/__Init__.cs`, `src/Sharpy.Stdlib/Zoneinfo/ZoneInfo.cs`
    - Create `Zoneinfo/` directory under `src/Sharpy.Stdlib/`
    - Add `__Init__.cs` with `[SharpyModule("zoneinfo")]` on `public static partial class ZoneinfoModule`
    - Add `ZoneInfo.cs` with `[SharpyModuleType("zoneinfo", "ZoneInfo")]` sealed class implementing `ITzinfo`:
      - Internal: wraps `System.TimeZoneInfo`
      - Constructor: `ZoneInfo(string key)`:
        - Call `TimeZoneInfo.FindSystemTimeZoneById(key)`
        - Catch `TimeZoneNotFoundException` → throw `ZoneInfoNotFoundError($"'No time zone found with key {key}'")`
      - Properties:
        - `string Key` — the IANA zone ID (e.g., `"America/New_York"`)
      - ITzinfo implementation [CORRECTED: nullable annotations on `DateTime?` parameters]:
        - `Timedelta Utcoffset(DateTime? dt = null)`:
          - If `dt` is null, return base UTC offset
          - Otherwise, convert `dt` to `System.DateTime` and call `_tz.GetUtcOffset(sysDateTime)` → wrap in `Timedelta`
        - `string Tzname(DateTime? dt = null)`:
          - If `dt` is null, return `Key`
          - If in DST, return `_tz.DaylightName`; otherwise return `_tz.StandardName`
        - `Timedelta Dst(DateTime? dt = null)`:
          - If `dt` is null, return zero `Timedelta`
          - If `_tz.IsDaylightSavingTime(sysDateTime)`, return DST adjustment delta; otherwise return zero
      - `override string ToString()` → `Key`
      - `override bool Equals(object? obj)` → compare by `Key`
      - `override int GetHashCode()` → hash of `Key`
    - Acceptance: `ZoneInfo("America/New_York")` creates successfully, returns correct offsets
    - Commit: `feat(stdlib): implement zoneinfo ZoneInfo class`

16. **Implement ZoneInfoNotFoundError and module functions** — `src/Sharpy.Stdlib/Zoneinfo/Errors.cs`, `src/Sharpy.Stdlib/Zoneinfo/ZoneinfoFunctions.cs`
    - `Errors.cs`:
      - `[SharpyModuleType("zoneinfo", "ZoneInfoNotFoundError")]` class extending `KeyError`:
        - Constructor: `ZoneInfoNotFoundError(string message)` — matching Python's exception hierarchy (ZoneInfoNotFoundError inherits from KeyError)
    - `ZoneinfoFunctions.cs` — add to `public static partial class ZoneinfoModule`:
      - `Set<string> AvailableTimezones()`:
        - Call `TimeZoneInfo.GetSystemTimeZones()`
        - Return set of all zone IDs
        - On .NET 6+, these are IANA names by default
    - Acceptance: `available_timezones()` returns a populated set, `ZoneInfoNotFoundError` matches Python hierarchy
    - Commit: `feat(stdlib): implement zoneinfo error and module functions`

17. **Create zoneinfo project file and spy stub** — `src/Sharpy.Stdlib/modules/Sharpy.Stdlib.Zoneinfo.csproj`, `src/Sharpy.Stdlib/spy/zoneinfo_module.spy`
    - Project file: standard pattern, `<AssemblyName>Sharpy.Stdlib.Zoneinfo</AssemblyName>`
    - Spy stub: define `ZoneInfo` class, `ZoneInfoNotFoundError`, and `available_timezones()` function
    - Note: spy stub should also define `from zoneinfo import ZoneInfo` import pattern
    - Acceptance: project builds successfully
    - Commit: `build(stdlib): add zoneinfo project file and spy stub`

18. **Add zoneinfo tests** — `src/Sharpy.Stdlib.Tests/ZoneinfoTests.cs`
    - Test ZoneInfo creation:
      - `ZoneInfo("UTC")` — key is `"UTC"`
      - `ZoneInfo("America/New_York")` — valid IANA name
      - `ZoneInfo("Europe/London")` — another valid zone
      - `ZoneInfo("Invalid/Zone")` → throws `ZoneInfoNotFoundError`
    - Test UTC offset:
      - `ZoneInfo("America/New_York").Utcoffset()` for a winter date → UTC-5 (`Timedelta(hours: -5)`)
      - `ZoneInfo("America/New_York").Utcoffset()` for a summer date → UTC-4 (`Timedelta(hours: -4)`)
      - `ZoneInfo("UTC").Utcoffset()` → zero
    - Test DST:
      - Winter: `Dst()` returns zero
      - Summer: `Dst()` returns 1 hour
    - Test integration with datetime:
      - `DateTime(2026, 6, 15, 12, 0, tzinfo: ZoneInfo("America/New_York"))` — constructs successfully
      - `dt.Astimezone(ZoneInfo("UTC"))` — converts correctly
      - `dt.Astimezone(ZoneInfo("Europe/London"))` — converts correctly
    - Test `available_timezones()`:
      - Returns non-empty set
      - Contains well-known zones: `"UTC"`, `"America/New_York"`, `"Europe/London"`
    - Test backward compatibility:
      - `DateTime` still accepts `Timezone` (fixed-offset) — existing API unchanged
      - `Timezone.Utc` still works
    - Acceptance: all tests pass, including datetime integration
    - Commit: `test(stdlib): add zoneinfo module tests`

### Phase 6: Documentation

**Goal:** Add batch plan doc for reference.

#### Tasks

19. **Add Batch 8 plan to docs** — `docs/stdlib/batch8-plan.md`
    - Save this plan (cleaned up) as the batch plan document in the docs directory
    - Follow the same format as existing batch plans (batch1-plan.md, batch2-plan.md, batch4-plan.md through batch7-plan.md) [CORRECTED: batch3-plan.md does not exist; batch7-plan.md does exist]
    - Acceptance: document exists with correct content
    - Commit: `docs(stdlib): add Batch 8 implementation plan for pprint, calendar, zoneinfo, colorsys`

## Testing Strategy

### New test fixtures needed

- `src/Sharpy.Stdlib.Tests/ColorsysTests.cs` — ~20 tests covering all 6 conversion functions + roundtrips + edge cases
- `src/Sharpy.Stdlib.Tests/PprintTests.cs` — ~25 tests covering formatting, depth, width, compact, circular refs, all supported types
- `src/Sharpy.Stdlib.Tests/CalendarTests.cs` — ~25 tests covering leap year, weekday, monthrange, text/HTML formatting, iteration
- `src/Sharpy.Stdlib.Tests/ZoneinfoTests.cs` — ~20 tests covering ZoneInfo creation, offsets, DST, datetime integration, available_timezones

### Edge cases to cover

**colorsys:**
- Black (0,0,0) — hue and saturation should be 0
- White (1,1,1) — hue and saturation should be 0, value/lightness should be 1
- Pure primary colors — known hue values (red=0, green=1/3, blue=2/3 for HSV)
- Floating point precision — roundtrip tolerance

**pprint:**
- Empty collections — `{}`, `[]`, `set()`
- Deeply nested structures with depth limit
- Very long strings
- Circular reference in list, dict, and mixed structures
- Single-element tuple — `(1,)` vs `(1)` (trailing comma)
- `width=1` (force one item per line)

**calendar:**
- February in leap/non-leap years
- January (month 1) and December (month 12) boundaries
- `firstweekday=6` (Sunday) — verify header and day layout
- Year boundary dates (Jan 1, Dec 31)
- `leapdays` with reversed range — should return 0 or negative (verify Python behavior)

**zoneinfo:**
- DST transition dates (e.g., US spring forward in March, fall back in November)
- Zones without DST (e.g., `America/Phoenix`, `Asia/Kolkata`)
- UTC zone — always zero offset, no DST
- `available_timezones()` cross-platform consistency (different systems may have different zone databases)

### Negative test cases

**colorsys:** No negative tests needed — functions accept any doubles and compute mathematically.

**pprint:**
- Circular references don't cause stack overflow — verified by circular detection
- `depth=0` — all collections show as `...`

**calendar:**
- Invalid month (0, 13) → should throw (verify Python behavior)
- Invalid year (0, negative) → should throw

**zoneinfo:**
- Invalid zone name → `ZoneInfoNotFoundError`
- Empty string zone name → `ZoneInfoNotFoundError`

## Issues to Close

- #758 — colorsys module (closed by Phase 1, Task 1 — complete implementation)
- #745 — pprint module (closed by Phase 2, Task 5 — module-level functions complete)
- #759 — calendar module (closed by Phase 3, Task 11 — module-level functions complete)
- #760 — zoneinfo module (closed by Phase 5, Task 16 — ZoneInfo + module functions complete)

## Verification Summary

**Result:** PASS WITH CORRECTIONS
**Verified on:** 2026-05-29
**Plan file:** `~/.claude/plans/plan-fdc6fe.md`

### Corrections Made

1. **ITzinfo interface nullable annotations** — Changed all `DateTime dt = null` parameters to `DateTime? dt = null` in the ITzinfo interface definition (Task 14), Timezone implementation (Task 14), and ZoneInfo ITzinfo implementation (Task 15). Reason: `<Nullable>enable</Nullable>` and `TreatWarningsAsErrors` are both active in `Directory.Build.props`; passing `null` to a non-nullable reference type parameter produces CS8625 warning which becomes a build error.

2. **Batch plan file reference** — Changed "batch1-plan.md through batch6-plan.md" to "batch1-plan.md, batch2-plan.md, batch4-plan.md through batch7-plan.md". Reason: `batch3-plan.md` does not exist and `batch7-plan.md` does exist in `docs/stdlib/`.

### Warnings

1. **Missing `IllegalMonthError` / `IllegalWeekdayError`** — Python's `calendar` module defines custom error types (`calendar.IllegalMonthError`, `calendar.IllegalWeekdayError`) for invalid inputs. The plan doesn't define these. Consider adding them as `[SharpyModuleType("calendar", "IllegalMonthError")]` classes extending the appropriate base exception, or at minimum throw `ValueError` with matching messages.

2. **`leapdays()` with reversed range returns negative** — Python returns `-6` for `leapdays(2024, 2000)`, not `0`. The plan's edge case section says "verify Python behavior" which is fine, but test assertions should expect negative values for reversed ranges.

3. **Yaml module not fully buildable** — Plan says "33+ stdlib modules (31 original + Toml + Yaml)" but `Sharpy.Stdlib.Yaml.csproj` does not exist (though the source directory does). The count is technically 32 buildable modules + Yaml source. Minor — doesn't affect this plan.

4. **`Astimezone` DST-awareness** — Task 14 correctly updates `Astimezone` to pass `dt` to `tz.Utcoffset(targetDt)`, but the current implementation converts to UTC using `_tzinfo.Utcoffset()` (no args). When `_tzinfo` is a `ZoneInfo` (DST-aware), the UTC conversion step also needs the `dt` parameter: `_tzinfo.Utcoffset(this)` instead of `_tzinfo.Utcoffset()`. The plan should explicitly mention updating both the source-timezone offset lookup and the target-timezone offset lookup in `Astimezone`.

### Verified Claims (all confirmed)

- **Python behavior**: All colorsys conversions, calendar functions (weekday, monthrange, leapdays, isleap, timegm) verified against Python 3 ✓
- **YIQ coefficients**: `0.30*r + 0.59*g + 0.11*b` confirmed against CPython ✓
- **HLS parameter order**: `(h, l, s)` confirmed — plan matches Python convention ✓
- **Datetime module structure**: `Timezone` is a concrete class with `_offset` field, `Utcoffset()` takes no params, `Astimezone(Timezone tz)` — all confirmed ✓
- **`KeyError` exists** in `src/Sharpy.Core/KeyError.cs` as `public class KeyError : Exception` ✓
- **`ZoneInfoNotFoundError` inherits from `KeyError`** in Python — confirmed ✓
- **Module patterns**: `[SharpyModule]`/`[SharpyModuleType]` attributes, `__Init__.cs` + implementation files, per-module `.csproj` — all confirmed ✓
- **None of the four modules exist yet** — confirmed ✓
- **GitHub issues match roadmap**: #745 (pprint), #758 (colorsys), #759 (calendar), #760 (zoneinfo) all listed in Tier 2 ✓
- **Roadmap Batch 8** correctly lists these four modules ✓
- **`timegm(2026,1,1,0,0,0)` = `1767225600`** — confirmed ✓
- **Directory.Build.props**: multi-target `net10.0;netstandard2.1`, C# 9.0 for netstandard2.1 — confirmed ✓
- **Hashlib csproj pattern**: correct reference for new module csproj structure ✓
- **DayOfWeek mapping**: `((int)dow + 6) % 7` correctly maps Sunday=0 to Python Monday=0 convention — matches existing `Date.Weekday()` implementation ✓

### Unchecked Claims

- **GitHub issues #745, #758, #759, #760 exist and are open** — not verified (would require `gh` API call); issue numbers match the roadmap file
