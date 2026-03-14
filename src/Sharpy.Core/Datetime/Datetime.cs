using System;
namespace Sharpy
{
    /// <summary>
    /// Represents a date (year, month, day).
    /// </summary>
    public class Date
    {
        private readonly System.DateTime _date;

        /// <summary>Create a date from year, month, and day.</summary>
        public Date(int year, int month, int day)
        {
            _date = new System.DateTime(year, month, day);
        }

        internal Date(System.DateTime dateTime)
        {
            _date = dateTime.Date;
        }

        /// <summary>The year component.</summary>
        public int Year => _date.Year;
        /// <summary>The month component (1-12).</summary>
        public int Month => _date.Month;
        /// <summary>The day component (1-31).</summary>
        public int Day => _date.Day;

        /// <summary>Return the ISO 8601 string representation (yyyy-MM-dd).</summary>
        public override string ToString()
        {
            return _date.ToString("yyyy-MM-dd");
        }

        /// <summary>
        /// Return the current local date.
        /// </summary>
        /// <returns>A <see cref="Date"/> representing today.</returns>
        /// <example>
        /// <code>
        /// d = date.today()
        /// print(d)    # "2024-01-15"
        /// </code>
        /// </example>
        public static Date Today()
        {
            return new Date(System.DateTime.Today);
        }
    }

    /// <summary>
    /// Represents a time (hour, minute, second, microsecond).
    /// </summary>
    public class Time
    {
        private readonly TimeSpan _time;

        /// <summary>Create a time from hour, minute, second, and microsecond.</summary>
        public Time(int hour = 0, int minute = 0, int second = 0, int microsecond = 0)
        {
            // Convert microseconds to ticks (10 ticks = 1 microsecond)
            long ticks = new TimeSpan(0, hour, minute, second).Ticks + (microsecond * 10L);
            _time = new TimeSpan(ticks);
        }

        internal Time(TimeSpan timeSpan)
        {
            _time = timeSpan;
        }

        /// <summary>The hour component (0-23).</summary>
        public int Hour => _time.Hours;
        /// <summary>The minute component (0-59).</summary>
        public int Minute => _time.Minutes;
        /// <summary>The second component (0-59).</summary>
        public int Second => _time.Seconds;
        /// <summary>The microsecond component (0-999999).</summary>
        public int Microsecond => (int)((_time.Ticks % TimeSpan.TicksPerSecond) / 10);

        /// <summary>Return the string representation (HH:mm:ss.ffffff).</summary>
        public override string ToString()
        {
            return $"{Hour:D2}:{Minute:D2}:{Second:D2}.{Microsecond:D6}";
        }
    }

    /// <summary>
    /// A combination of a date and a time.
    /// </summary>
    public class DateTime
    {
        private readonly System.DateTime _dateTime;

        /// <summary>Create a datetime from year, month, day, and optional time components.</summary>
        public DateTime(int year, int month, int day, int hour = 0, int minute = 0, int second = 0, int microsecond = 0)
        {
            // Create base DateTime and add microseconds as ticks (10 ticks = 1 microsecond)
            _dateTime = new System.DateTime(year, month, day, hour, minute, second).AddTicks(microsecond * 10L);
        }

        internal DateTime(System.DateTime dateTime)
        {
            _dateTime = dateTime;
        }

        /// <summary>The year component.</summary>
        public int Year => _dateTime.Year;
        /// <summary>The month component (1-12).</summary>
        public int Month => _dateTime.Month;
        /// <summary>The day component (1-31).</summary>
        public int Day => _dateTime.Day;
        /// <summary>The hour component (0-23).</summary>
        public int Hour => _dateTime.Hour;
        /// <summary>The minute component (0-59).</summary>
        public int Minute => _dateTime.Minute;
        /// <summary>The second component (0-59).</summary>
        public int Second => _dateTime.Second;
        /// <summary>The microsecond component (0-999999).</summary>
        public int Microsecond => (int)((_dateTime.Ticks % TimeSpan.TicksPerSecond) / 10);

        /// <summary>The date component of this datetime.</summary>
        public Date DateComponent => new Date(_dateTime);
        /// <summary>The time component of this datetime.</summary>
        public Time TimeComponent => new Time(_dateTime.TimeOfDay);

        /// <summary>Return the string representation (yyyy-MM-dd HH:mm:ss.ffffff).</summary>
        public override string ToString()
        {
            return _dateTime.ToString("yyyy-MM-dd HH:mm:ss.ffffff");
        }

        /// <summary>
        /// Return the current local datetime.
        /// </summary>
        /// <returns>A <see cref="DateTime"/> representing the current local date and time.</returns>
        /// <example>
        /// <code>
        /// dt = datetime.now()
        /// print(dt.year, dt.month, dt.day)
        /// </code>
        /// </example>
        public static DateTime Now()
        {
            return new DateTime(System.DateTime.Now);
        }

        /// <summary>
        /// Return the current UTC datetime.
        /// </summary>
        public static DateTime Utcnow()
        {
            return new DateTime(System.DateTime.UtcNow);
        }

        /// <summary>
        /// Combine a date and a time to create a datetime.
        /// </summary>
        /// <param name="date">The date component.</param>
        /// <param name="time">The time component.</param>
        /// <returns>A new <see cref="DateTime"/> combining the date and time.</returns>
        /// <example>
        /// <code>
        /// d = date(2024, 1, 15)
        /// t = time(14, 30)
        /// dt = datetime.combine(d, t)    # 2024-01-15 14:30:00.000000
        /// </code>
        /// </example>
        public static DateTime Combine(Date date, Time time)
        {
            return new DateTime(date.Year, date.Month, date.Day, time.Hour, time.Minute, time.Second, time.Microsecond);
        }
    }

    /// <summary>
    /// Represents the difference between two dates or times.
    /// </summary>
    public class Timedelta
    {
        private readonly TimeSpan _timeSpan;

        /// <summary>Create a timedelta from the given time components.</summary>
        public Timedelta(int days = 0, int seconds = 0, int microseconds = 0, int milliseconds = 0, int minutes = 0, int hours = 0, int weeks = 0)
        {
            // Build TimeSpan from ticks for proper microsecond precision
            long ticks = 0;
            checked
            {
                ticks += (days + weeks * 7L) * TimeSpan.TicksPerDay;
                ticks += hours * TimeSpan.TicksPerHour;
                ticks += minutes * TimeSpan.TicksPerMinute;
                ticks += seconds * TimeSpan.TicksPerSecond;
                ticks += milliseconds * TimeSpan.TicksPerMillisecond;
                ticks += microseconds * 10L; // 10 ticks = 1 microsecond
            }

            _timeSpan = new TimeSpan(ticks);
        }

        internal Timedelta(TimeSpan timeSpan)
        {
            _timeSpan = timeSpan;
        }

        /// <summary>The days component of the time interval.</summary>
        public int Days => _timeSpan.Days;
        /// <summary>
        /// Gets the seconds component (0-59) of the time interval, not the total number of seconds.
        /// This matches the behavior of Python's <c>timedelta.seconds</c> property.
        /// For the total number of seconds, use <see cref="TotalSeconds"/>.
        /// </summary>
        public int Seconds => _timeSpan.Seconds;
        /// <summary>The microseconds component of the time interval.</summary>
        public int Microseconds => (int)((_timeSpan.Ticks % TimeSpan.TicksPerSecond) / 10);
        /// <summary>The total number of seconds represented by this timedelta.</summary>
        public double TotalSeconds => _timeSpan.TotalSeconds;

        /// <summary>Return the string representation.</summary>
        public override string ToString()
        {
            return _timeSpan.ToString();
        }
    }

    /// <summary>
    /// Module exports for datetime functionality.
    /// </summary>
    public static partial class Datetime
    {
        /// <summary>The Date type.</summary>
        public static Type DateType => typeof(Date);
        /// <summary>The Time type.</summary>
        public static Type TimeType => typeof(Time);
        /// <summary>The DateTime type.</summary>
        public static Type DateTimeType => typeof(DateTime);
        /// <summary>The Timedelta type.</summary>
        public static Type TimedeltaType => typeof(Timedelta);
    }
}
