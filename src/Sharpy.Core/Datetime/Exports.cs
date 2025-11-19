namespace Sharpy.Datetime;

/// <summary>
/// Represents a date (year, month, day).
/// </summary>
public class DateObject
{
    private readonly System.DateTime _date;

    public DateObject(int year, int month, int day)
    {
        _date = new System.DateTime(year, month, day);
    }

    internal DateObject(System.DateTime dateTime)
    {
        _date = dateTime.Date;
    }

    public int Year => _date.Year;
    public int Month => _date.Month;
    public int Day => _date.Day;

    public override string ToString()
    {
        return _date.ToString("yyyy-MM-dd");
    }

    /// <summary>
    /// Return the current local date.
    /// </summary>
    public static DateObject Today()
    {
        return new DateObject(System.DateTime.Today);
    }
}

/// <summary>
/// Represents a time (hour, minute, second, microsecond).
/// </summary>
public class TimeObject
{
    private readonly TimeSpan _time;

    public TimeObject(int hour = 0, int minute = 0, int second = 0, int microsecond = 0)
    {
        // Convert microseconds to ticks (10 ticks = 1 microsecond)
        long ticks = new TimeSpan(0, hour, minute, second).Ticks + (microsecond * 10L);
        _time = new TimeSpan(ticks);
    }

    internal TimeObject(TimeSpan timeSpan)
    {
        _time = timeSpan;
    }

    public int Hour => _time.Hours;
    public int Minute => _time.Minutes;
    public int Second => _time.Seconds;
    public int Microsecond => (int)((_time.Ticks % TimeSpan.TicksPerSecond) / 10);

    public override string ToString()
    {
        return $"{Hour:D2}:{Minute:D2}:{Second:D2}.{Microsecond:D6}";
    }
}

/// <summary>
/// A combination of a date and a time.
/// </summary>
public class DateTimeObject
{
    private readonly System.DateTime _dateTime;

    public DateTimeObject(int year, int month, int day, int hour = 0, int minute = 0, int second = 0, int microsecond = 0)
    {
        // Create base DateTime and add microseconds as ticks (10 ticks = 1 microsecond)
        _dateTime = new System.DateTime(year, month, day, hour, minute, second).AddTicks(microsecond * 10L);
    }

    internal DateTimeObject(System.DateTime dateTime)
    {
        _dateTime = dateTime;
    }

    public int Year => _dateTime.Year;
    public int Month => _dateTime.Month;
    public int Day => _dateTime.Day;
    public int Hour => _dateTime.Hour;
    public int Minute => _dateTime.Minute;
    public int Second => _dateTime.Second;
    public int Microsecond => (int)((_dateTime.Ticks % TimeSpan.TicksPerSecond) / 10);

    public DateObject DateComponent => new DateObject(_dateTime);
    public TimeObject TimeComponent => new TimeObject(_dateTime.TimeOfDay);

    public override string ToString()
    {
        return _dateTime.ToString("yyyy-MM-dd HH:mm:ss.ffffff");
    }

    /// <summary>
    /// Return the current local datetime.
    /// </summary>
    public static DateTimeObject Now()
    {
        return new DateTimeObject(System.DateTime.Now);
    }

    /// <summary>
    /// Return the current UTC datetime.
    /// </summary>
    public static DateTimeObject Utcnow()
    {
        return new DateTimeObject(System.DateTime.UtcNow);
    }

    /// <summary>
    /// Combine a date and a time to create a datetime.
    /// </summary>
    public static DateTimeObject Combine(DateObject date, TimeObject time)
    {
        return new DateTimeObject(date.Year, date.Month, date.Day, time.Hour, time.Minute, time.Second, time.Microsecond);
    }
}

/// <summary>
/// Represents the difference between two dates or times.
/// </summary>
public class TimedeltaObject
{
    private readonly TimeSpan _timeSpan;

    public TimedeltaObject(int days = 0, int seconds = 0, int microseconds = 0, int milliseconds = 0, int minutes = 0, int hours = 0, int weeks = 0)
    {
        // Build TimeSpan from ticks for proper microsecond precision
        long ticks = 0;
        ticks += (days + weeks * 7L) * TimeSpan.TicksPerDay;
        ticks += hours * TimeSpan.TicksPerHour;
        ticks += minutes * TimeSpan.TicksPerMinute;
        ticks += seconds * TimeSpan.TicksPerSecond;
        ticks += milliseconds * TimeSpan.TicksPerMillisecond;
        ticks += microseconds * 10L; // 10 ticks = 1 microsecond
        
        _timeSpan = new TimeSpan(ticks);
    }

    internal TimedeltaObject(TimeSpan timeSpan)
    {
        _timeSpan = timeSpan;
    }

    public int Days => _timeSpan.Days;
    public int Seconds => _timeSpan.Seconds;
    public int Microseconds => (int)((_timeSpan.Ticks % TimeSpan.TicksPerSecond) / 10);
    public double TotalSeconds => _timeSpan.TotalSeconds;

    public override string ToString()
    {
        return _timeSpan.ToString();
    }
}

/// <summary>
/// Module exports for datetime functionality.
/// </summary>
public static class Exports
{
    // Re-export the classes for convenience
    public static Type DateType => typeof(DateObject);
    public static Type TimeType => typeof(TimeObject);
    public static Type DateTimeType => typeof(DateTimeObject);
    public static Type TimedeltaType => typeof(TimedeltaObject);
}
