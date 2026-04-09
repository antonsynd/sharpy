#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type
#pragma warning disable CS8603 // Possible null reference return
#pragma warning disable CS1591 // Missing XML comment
using System;
using System.Globalization;
namespace Sharpy
{
    /// <summary>
    /// Represents a date (year, month, day).
    /// </summary>
    public class Date : IEquatable<Date>, IComparable<Date>
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

        internal System.DateTime InternalDate => _date;

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

        /// <summary>Return the current local date.</summary>
        public static Date Today()
        {
            return new Date(System.DateTime.Today);
        }

        /// <summary>Return the day of the week (0=Monday through 6=Sunday).</summary>
        public int Weekday()
        {
            return ((int)_date.DayOfWeek + 6) % 7;
        }

        /// <summary>Return the ISO day of the week (1=Monday through 7=Sunday).</summary>
        public int Isoweekday()
        {
            return Weekday() + 1;
        }

        /// <summary>Return the ISO 8601 formatted string.</summary>
        public string Isoformat()
        {
            return _date.ToString("yyyy-MM-dd");
        }

        /// <summary>Return a new Date with replaced components.</summary>
        public Date Replace(int? year = null, int? month = null, int? day = null)
        {
            return new Date(year ?? Year, month ?? Month, day ?? Day);
        }

        /// <summary>Return the proleptic Gregorian ordinal of the date.</summary>
        public int Toordinal()
        {
            return (int)(_date.Ticks / TimeSpan.TicksPerDay) + 1;
        }

        /// <summary>Create a Date from a proleptic Gregorian ordinal.</summary>
        public static Date Fromordinal(int ordinal)
        {
            return new Date(new System.DateTime((ordinal - 1) * TimeSpan.TicksPerDay));
        }

        /// <summary>Parse a date from ISO 8601 format string.</summary>
        public static Date Fromisoformat(string date_string)
        {
            var dt = System.DateTime.ParseExact(date_string, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            return new Date(dt);
        }

        /// <summary>Format the date using Python strftime format codes.</summary>
        public string Strftime(string format)
        {
            return DatetimeFormatHelper.Strftime(_date, format);
        }

        // --- Arithmetic ---

        public static Date operator +(Date date, Timedelta delta)
        {
            return new Date(date._date.AddTicks(delta.InternalTimeSpan.Ticks).Date);
        }

        public static Date operator -(Date date, Timedelta delta)
        {
            return new Date(date._date.AddTicks(-delta.InternalTimeSpan.Ticks).Date);
        }

        public static Timedelta operator -(Date left, Date right)
        {
            return new Timedelta(left._date.Subtract(right._date));
        }

        // --- Comparison ---

        public int CompareTo(Date other)
        {
            if (other == null)
                return 1;
            return _date.CompareTo(other._date);
        }

        public bool Equals(Date other)
        {
            if (other is null)
                return false;
            return _date == other._date;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as Date);
        }

        public override int GetHashCode()
        {
            return _date.GetHashCode();
        }

        public static bool operator ==(Date left, Date right)
        {
            if (left is null)
                return right is null;
            return left.Equals(right);
        }

        public static bool operator !=(Date left, Date right)
        {
            return !(left == right);
        }

        public static bool operator <(Date left, Date right)
        {
            return left.CompareTo(right) < 0;
        }

        public static bool operator >(Date left, Date right)
        {
            return left.CompareTo(right) > 0;
        }

        public static bool operator <=(Date left, Date right)
        {
            return left.CompareTo(right) <= 0;
        }

        public static bool operator >=(Date left, Date right)
        {
            return left.CompareTo(right) >= 0;
        }
    }

    /// <summary>
    /// Represents a time (hour, minute, second, microsecond).
    /// </summary>
    public class Time : IEquatable<Time>, IComparable<Time>
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

        /// <summary>Return the ISO 8601 formatted string.</summary>
        public string Isoformat()
        {
            if (Microsecond != 0)
                return $"{Hour:D2}:{Minute:D2}:{Second:D2}.{Microsecond:D6}";
            return $"{Hour:D2}:{Minute:D2}:{Second:D2}";
        }

        /// <summary>Format the time using Python strftime format codes.</summary>
        public string Strftime(string format)
        {
            var dt = new System.DateTime(1900, 1, 1, Hour, Minute, Second).AddTicks(Microsecond * 10L);
            return DatetimeFormatHelper.Strftime(dt, format);
        }

        // --- Comparison ---

        public int CompareTo(Time other)
        {
            if (other == null)
                return 1;
            return _time.CompareTo(other._time);
        }

        public bool Equals(Time other)
        {
            if (other is null)
                return false;
            return _time == other._time;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as Time);
        }

        public override int GetHashCode()
        {
            return _time.GetHashCode();
        }

        public static bool operator ==(Time left, Time right)
        {
            if (left is null)
                return right is null;
            return left.Equals(right);
        }

        public static bool operator !=(Time left, Time right)
        {
            return !(left == right);
        }

        public static bool operator <(Time left, Time right)
        {
            return left.CompareTo(right) < 0;
        }

        public static bool operator >(Time left, Time right)
        {
            return left.CompareTo(right) > 0;
        }

        public static bool operator <=(Time left, Time right)
        {
            return left.CompareTo(right) <= 0;
        }

        public static bool operator >=(Time left, Time right)
        {
            return left.CompareTo(right) >= 0;
        }
    }

    /// <summary>
    /// A combination of a date and a time.
    /// </summary>
    public class DateTime : IEquatable<DateTime>, IComparable<DateTime>
    {
        private readonly System.DateTime _dateTime;
        private readonly Timezone _tzinfo;

        /// <summary>Create a datetime from year, month, day, and optional time components.</summary>
        public DateTime(int year, int month, int day, int hour = 0, int minute = 0, int second = 0, int microsecond = 0, Timezone tzinfo = null)
        {
            _dateTime = new System.DateTime(year, month, day, hour, minute, second).AddTicks(microsecond * 10L);
            _tzinfo = tzinfo;
        }

        internal DateTime(System.DateTime dateTime, Timezone tzinfo = null)
        {
            _dateTime = dateTime;
            _tzinfo = tzinfo;
        }

        internal System.DateTime InternalDateTime => _dateTime;

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
        /// <summary>The timezone info, or null if naive.</summary>
        public Timezone Tzinfo => _tzinfo;

        /// <summary>The date component of this datetime.</summary>
        public Date DateComponent => new Date(_dateTime);
        /// <summary>The time component of this datetime.</summary>
        public Time TimeComponent => new Time(_dateTime.TimeOfDay);

        /// <summary>Return the string representation.</summary>
        public override string ToString()
        {
            var result = _dateTime.ToString("yyyy-MM-dd HH:mm:ss.ffffff");
            if (_tzinfo != null)
            {
                var offset = _tzinfo.Utcoffset();
                var sign = offset.InternalTimeSpan.Ticks >= 0 ? "+" : "-";
                var absOffset = offset.InternalTimeSpan.Duration();
                result += $"{sign}{absOffset.Hours:D2}:{absOffset.Minutes:D2}";
            }
            return result;
        }

        /// <summary>Return the current local datetime.</summary>
        public static DateTime Now()
        {
            return new DateTime(System.DateTime.Now);
        }

        /// <summary>Return the current UTC datetime.</summary>
        public static DateTime Utcnow()
        {
            return new DateTime(System.DateTime.UtcNow);
        }

        /// <summary>Combine a date and a time to create a datetime.</summary>
        public static DateTime Combine(Date date, Time time)
        {
            return new DateTime(date.Year, date.Month, date.Day, time.Hour, time.Minute, time.Second, time.Microsecond);
        }

        // --- Utility methods ---

        /// <summary>Return the day of the week (0=Monday through 6=Sunday).</summary>
        public int Weekday()
        {
            return ((int)_dateTime.DayOfWeek + 6) % 7;
        }

        /// <summary>Return the ISO day of the week (1=Monday through 7=Sunday).</summary>
        public int Isoweekday()
        {
            return Weekday() + 1;
        }

        /// <summary>Return the ISO 8601 formatted string.</summary>
        public string Isoformat(string sep = null)
        {
            string s = sep ?? "T";
            var result = _dateTime.ToString("yyyy-MM-dd") + s + _dateTime.ToString("HH:mm:ss");
            if (Microsecond != 0)
            {
                result += "." + Microsecond.ToString("D6");
            }
            if (_tzinfo != null)
            {
                var offset = _tzinfo.Utcoffset();
                var sign = offset.InternalTimeSpan.Ticks >= 0 ? "+" : "-";
                var absOffset = offset.InternalTimeSpan.Duration();
                result += $"{sign}{absOffset.Hours:D2}:{absOffset.Minutes:D2}";
            }
            return result;
        }

        /// <summary>Return a new DateTime with replaced components.</summary>
        public DateTime Replace(int? year = null, int? month = null, int? day = null, int? hour = null, int? minute = null, int? second = null)
        {
            return new DateTime(
                year ?? Year, month ?? Month, day ?? Day,
                hour ?? Hour, minute ?? Minute, second ?? Second,
                Microsecond, _tzinfo);
        }

        /// <summary>Return the Unix timestamp as a double.</summary>
        public double Timestamp()
        {
            var epoch = new System.DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            System.DateTime utcTime;
            if (_tzinfo != null)
            {
                utcTime = _dateTime.AddTicks(-_tzinfo.Utcoffset().InternalTimeSpan.Ticks);
            }
            else
            {
                utcTime = System.DateTime.SpecifyKind(_dateTime, DateTimeKind.Local).ToUniversalTime();
            }
            return (utcTime - epoch).TotalSeconds;
        }

        /// <summary>Parse a datetime from ISO 8601 format string.</summary>
        public static DateTime Fromisoformat(string date_string)
        {
            var dt = System.DateTime.Parse(date_string, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            return new DateTime(dt);
        }

        /// <summary>Format the datetime using Python strftime format codes.</summary>
        public string Strftime(string format)
        {
            return DatetimeFormatHelper.Strftime(_dateTime, format);
        }

        /// <summary>Parse a datetime from a string using Python strftime format codes.</summary>
        public static DateTime Strptime(string date_string, string format)
        {
            var dotnetFormat = DatetimeFormatHelper.TranslateFormat(format);
            var dt = System.DateTime.ParseExact(date_string, dotnetFormat, CultureInfo.InvariantCulture);
            return new DateTime(dt);
        }

        /// <summary>Convert to a different timezone.</summary>
        public DateTime Astimezone(Timezone tz)
        {
            System.DateTime utcTime;
            if (_tzinfo != null)
            {
                utcTime = _dateTime.AddTicks(-_tzinfo.Utcoffset().InternalTimeSpan.Ticks);
            }
            else
            {
                utcTime = System.DateTime.SpecifyKind(_dateTime, DateTimeKind.Local).ToUniversalTime();
            }
            var targetTime = utcTime.AddTicks(tz.Utcoffset().InternalTimeSpan.Ticks);
            return new DateTime(targetTime, tz);
        }

        // --- Arithmetic ---

        public static DateTime operator +(DateTime dt, Timedelta delta)
        {
            return new DateTime(dt._dateTime.AddTicks(delta.InternalTimeSpan.Ticks), dt._tzinfo);
        }

        public static DateTime operator -(DateTime dt, Timedelta delta)
        {
            return new DateTime(dt._dateTime.AddTicks(-delta.InternalTimeSpan.Ticks), dt._tzinfo);
        }

        public static Timedelta operator -(DateTime left, DateTime right)
        {
            return new Timedelta(left._dateTime.Subtract(right._dateTime));
        }

        // --- Comparison ---

        public int CompareTo(DateTime other)
        {
            if (other == null)
                return 1;
            return _dateTime.CompareTo(other._dateTime);
        }

        public bool Equals(DateTime other)
        {
            if (other is null)
                return false;
            return _dateTime == other._dateTime;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as DateTime);
        }

        public override int GetHashCode()
        {
            return _dateTime.GetHashCode();
        }

        public static bool operator ==(DateTime left, DateTime right)
        {
            if (left is null)
                return right is null;
            return left.Equals(right);
        }

        public static bool operator !=(DateTime left, DateTime right)
        {
            return !(left == right);
        }

        public static bool operator <(DateTime left, DateTime right)
        {
            return left.CompareTo(right) < 0;
        }

        public static bool operator >(DateTime left, DateTime right)
        {
            return left.CompareTo(right) > 0;
        }

        public static bool operator <=(DateTime left, DateTime right)
        {
            return left.CompareTo(right) <= 0;
        }

        public static bool operator >=(DateTime left, DateTime right)
        {
            return left.CompareTo(right) >= 0;
        }
    }

    /// <summary>
    /// Represents the difference between two dates or times.
    /// </summary>
    public class Timedelta : IEquatable<Timedelta>, IComparable<Timedelta>
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

        internal TimeSpan InternalTimeSpan => _timeSpan;

        /// <summary>The days component of the time interval.</summary>
        public int Days => _timeSpan.Days;
        /// <summary>
        /// Gets the remaining seconds after extracting days (0-86399).
        /// This matches Python's <c>timedelta.seconds</c> property.
        /// For the total number of seconds, use <see cref="TotalSeconds"/>.
        /// </summary>
        public int Seconds => (int)((_timeSpan.Ticks % TimeSpan.TicksPerDay) / TimeSpan.TicksPerSecond);
        /// <summary>The microseconds component of the time interval.</summary>
        public int Microseconds => (int)((_timeSpan.Ticks % TimeSpan.TicksPerSecond) / 10);
        /// <summary>The total number of seconds represented by this timedelta.</summary>
        public double TotalSeconds => _timeSpan.TotalSeconds;

        /// <summary>Return the string representation.</summary>
        public override string ToString()
        {
            return _timeSpan.ToString();
        }

        /// <summary>Return the absolute value of the timedelta.</summary>
        public Timedelta Abs()
        {
            return new Timedelta(_timeSpan.Duration());
        }

        // --- Arithmetic ---

        public static Timedelta operator +(Timedelta left, Timedelta right)
        {
            return new Timedelta(left._timeSpan + right._timeSpan);
        }

        public static Timedelta operator -(Timedelta left, Timedelta right)
        {
            return new Timedelta(left._timeSpan - right._timeSpan);
        }

        public static Timedelta operator -(Timedelta td)
        {
            return new Timedelta(-td._timeSpan);
        }

        public static Timedelta operator *(Timedelta td, int n)
        {
            return new Timedelta(new TimeSpan(td._timeSpan.Ticks * n));
        }

        public static Timedelta operator *(int n, Timedelta td)
        {
            return new Timedelta(new TimeSpan(n * td._timeSpan.Ticks));
        }

        public static Timedelta operator /(Timedelta td, int n)
        {
            return new Timedelta(new TimeSpan(td._timeSpan.Ticks / n));
        }

        // --- Comparison ---

        public int CompareTo(Timedelta other)
        {
            if (other == null)
                return 1;
            return _timeSpan.CompareTo(other._timeSpan);
        }

        public bool Equals(Timedelta other)
        {
            if (other is null)
                return false;
            return _timeSpan == other._timeSpan;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as Timedelta);
        }

        public override int GetHashCode()
        {
            return _timeSpan.GetHashCode();
        }

        public static bool operator ==(Timedelta left, Timedelta right)
        {
            if (left is null)
                return right is null;
            return left.Equals(right);
        }

        public static bool operator !=(Timedelta left, Timedelta right)
        {
            return !(left == right);
        }

        public static bool operator <(Timedelta left, Timedelta right)
        {
            return left.CompareTo(right) < 0;
        }

        public static bool operator >(Timedelta left, Timedelta right)
        {
            return left.CompareTo(right) > 0;
        }

        public static bool operator <=(Timedelta left, Timedelta right)
        {
            return left.CompareTo(right) <= 0;
        }

        public static bool operator >=(Timedelta left, Timedelta right)
        {
            return left.CompareTo(right) >= 0;
        }
    }

    /// <summary>
    /// Represents a fixed-offset timezone.
    /// </summary>
    public class Timezone
    {
        private readonly Timedelta _offset;
        private readonly string _name;

        /// <summary>The UTC timezone.</summary>
        public static readonly Timezone Utc = new Timezone(new Timedelta(), "UTC");

        /// <summary>Create a timezone with the given UTC offset and optional name.</summary>
        public Timezone(Timedelta offset, string name = null)
        {
            _offset = offset;
            _name = name ?? "";
        }

        /// <summary>Return the UTC offset.</summary>
        public Timedelta Utcoffset()
        {
            return _offset;
        }

        /// <summary>Return the timezone name.</summary>
        public string Tzname()
        {
            return _name;
        }

        /// <summary>Return the string representation.</summary>
        public override string ToString()
        {
            if (_name != "")
                return _name;
            var sign = _offset.InternalTimeSpan.Ticks >= 0 ? "+" : "-";
            var abs = _offset.InternalTimeSpan.Duration();
            return $"UTC{sign}{abs.Hours:D2}:{abs.Minutes:D2}";
        }
    }

    /// <summary>
    /// Helper for translating Python strftime/strptime format codes to .NET format strings.
    /// </summary>
    internal static class DatetimeFormatHelper
    {
        /// <summary>Translate a Python strftime format string to a .NET format string.</summary>
        internal static string TranslateFormat(string pythonFormat)
        {
            var result = new System.Text.StringBuilder();
            for (int i = 0; i < pythonFormat.Length; i++)
            {
                if (pythonFormat[i] == '%' && i + 1 < pythonFormat.Length)
                {
                    char code = pythonFormat[i + 1];
                    i++;
                    switch (code)
                    {
                        case 'Y':
                            result.Append("yyyy");
                            break;
                        case 'm':
                            result.Append("MM");
                            break;
                        case 'd':
                            result.Append("dd");
                            break;
                        case 'H':
                            result.Append("HH");
                            break;
                        case 'I':
                            result.Append("hh");
                            break;
                        case 'M':
                            result.Append("mm");
                            break;
                        case 'S':
                            result.Append("ss");
                            break;
                        case 'f':
                            result.Append("ffffff");
                            break;
                        case 'A':
                            result.Append("dddd");
                            break;
                        case 'a':
                            result.Append("ddd");
                            break;
                        case 'B':
                            result.Append("MMMM");
                            break;
                        case 'b':
                            result.Append("MMM");
                            break;
                        case 'p':
                            result.Append("tt");
                            break;
                        case '%':
                            result.Append("'%'");
                            break;
                        case 'j':
                            // Placeholder replaced in Strftime
                            result.Append("\\j");
                            break;
                        case 'w':
                            // Placeholder replaced in Strftime
                            result.Append("\\w");
                            break;
                        case 'Z':
                            // Placeholder replaced in Strftime
                            result.Append("\\Z");
                            break;
                        default:
                            result.Append('%');
                            result.Append(code);
                            break;
                    }
                }
                else
                {
                    // Escape non-format characters so .NET doesn't interpret them
                    char c = pythonFormat[i];
                    if (char.IsLetter(c))
                    {
                        result.Append('\\');
                    }
                    result.Append(c);
                }
            }
            return result.ToString();
        }

        /// <summary>Format a System.DateTime using Python strftime format codes.</summary>
        internal static string Strftime(System.DateTime dt, string pythonFormat)
        {
            // Handle %j, %w, %Z with pre-processing
            bool hasSpecial = pythonFormat.Contains("%j") || pythonFormat.Contains("%w") || pythonFormat.Contains("%Z");

            var dotnetFormat = TranslateFormat(pythonFormat);

            var result = dt.ToString(dotnetFormat, CultureInfo.InvariantCulture);

            if (hasSpecial)
            {
                // Replace placeholders for special codes
                if (result.Contains("j"))
                {
                    int dayOfYear = dt.DayOfYear;
                    result = result.Replace("j", dayOfYear.ToString("D3"));
                }
                if (result.Contains("w"))
                {
                    int dayOfWeek = (int)dt.DayOfWeek; // Sunday=0
                    result = result.Replace("w", dayOfWeek.ToString());
                }
                if (result.Contains("Z"))
                {
                    result = result.Replace("Z", "");
                }
            }

            return result;
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
        /// <summary>The Timezone type.</summary>
        public static Type TimezoneType => typeof(Timezone);
    }
}
