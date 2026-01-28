using System;
namespace Sharpy.Datetime
{
    /// <summary>
    /// Represents a date (year, month, day).
    /// </summary>
    public class Date
    {
        private readonly System.DateTime _date;

        public Date(int year, int month, int day)
        {
            _date = new System.DateTime(year, month, day);
        }

        internal Date(System.DateTime dateTime)
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
    public class DateTime
    {
        private readonly System.DateTime _dateTime;

        public DateTime(int year, int month, int day, int hour = 0, int minute = 0, int second = 0, int microsecond = 0)
        {
            // Create base DateTime and add microseconds as ticks (10 ticks = 1 microsecond)
            _dateTime = new System.DateTime(year, month, day, hour, minute, second).AddTicks(microsecond * 10L);
        }

        internal DateTime(System.DateTime dateTime)
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

        public Date DateComponent => new Date(_dateTime);
        public Time TimeComponent => new Time(_dateTime.TimeOfDay);

        public override string ToString()
        {
            return _dateTime.ToString("yyyy-MM-dd HH:mm:ss.ffffff");
        }

        /// <summary>
        /// Return the current local datetime.
        /// </summary>
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

        public int Days => _timeSpan.Days;
        /// <summary>
        /// Gets the seconds component (0-59) of the time interval, not the total number of seconds.
        /// This matches the behavior of Python's <c>timedelta.seconds</c> property.
        /// For the total number of seconds, use <see cref="TotalSeconds"/>.
        /// </summary>
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
        public static Type DateType => typeof(Date);
        public static Type TimeType => typeof(Time);
        public static Type DateTimeType => typeof(DateTime);
        public static Type TimedeltaType => typeof(Timedelta);
    }
}
