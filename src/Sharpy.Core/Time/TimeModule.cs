using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;

namespace Sharpy
{
    /// <summary>
    /// Time access and conversions, similar to Python's <c>time</c> module.
    /// </summary>
    public static partial class TimeModule
    {
        private static readonly Dictionary<string, string> _formatMap = new Dictionary<string, string>
        {
            { "%Y", "yyyy" },
            { "%m", "MM" },
            { "%d", "dd" },
            { "%H", "HH" },
            { "%M", "mm" },
            { "%S", "ss" },
            { "%A", "dddd" },
            { "%B", "MMMM" },
            { "%a", "ddd" },
            { "%b", "MMM" },
            { "%p", "tt" },
            { "%I", "hh" },
        };

        /// <summary>
        /// Return the time in seconds since the epoch (1970-01-01T00:00:00Z) as a
        /// floating point number.
        /// </summary>
        /// <returns>Seconds since the Unix epoch.</returns>
        /// <example>
        /// <code>
        /// t = time.time()    # e.g. 1700000000.123
        /// </code>
        /// </example>
        public static double Time()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
        }

        /// <summary>
        /// Return the time in nanoseconds since the epoch (1970-01-01T00:00:00Z).
        /// </summary>
        /// <remarks>
        /// On netstandard2.x, precision is limited to milliseconds. The value is
        /// derived from <see cref="DateTimeOffset.ToUnixTimeMilliseconds"/> multiplied
        /// by 1,000,000.
        /// </remarks>
        /// <returns>Nanoseconds since the Unix epoch (millisecond precision).</returns>
        public static long TimeNs()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1_000_000L;
        }

        /// <summary>
        /// Suspend execution of the calling thread for the given number of seconds.
        /// </summary>
        /// <param name="secs">Number of seconds to sleep. Fractional values are accepted.</param>
        /// <example>
        /// <code>
        /// time.sleep(0.5)    # sleep for 500 milliseconds
        /// </code>
        /// </example>
        public static void Sleep(double secs)
        {
            if (secs < 0)
            {
                throw new ValueError("sleep length must be non-negative");
            }

            // Clamp to avoid int overflow (int.MaxValue ms ≈ 24.8 days)
            long ms = (long)(secs * 1000);
            if (ms > int.MaxValue)
            {
                ms = int.MaxValue;
            }

            Thread.Sleep((int)ms);
        }

        /// <summary>
        /// Return the value (in fractional seconds) of a performance counter,
        /// i.e. a clock with the highest available resolution to measure a short
        /// duration.
        /// </summary>
        /// <returns>A monotonic time value in seconds.</returns>
        public static double PerfCounter()
        {
            return (double)Stopwatch.GetTimestamp() / Stopwatch.Frequency;
        }

        /// <summary>
        /// Return the value (in nanoseconds) of a performance counter.
        /// </summary>
        /// <returns>A monotonic time value in nanoseconds.</returns>
        public static long PerfCounterNs()
        {
            // Use floating-point intermediate to avoid long overflow on
            // high-resolution counters (direct multiply overflows after ~922s).
            return (long)((double)Stopwatch.GetTimestamp() / Stopwatch.Frequency * 1_000_000_000.0);
        }

        /// <summary>
        /// Return the value (in fractional seconds) of a monotonic clock,
        /// i.e. a clock that cannot go backwards.
        /// </summary>
        /// <returns>A monotonic time value in seconds.</returns>
        public static double Monotonic()
        {
            return PerfCounter();
        }

        /// <summary>
        /// Return the value (in nanoseconds) of a monotonic clock.
        /// </summary>
        /// <returns>A monotonic time value in nanoseconds.</returns>
        public static long MonotonicNs()
        {
            return PerfCounterNs();
        }

        /// <summary>
        /// Convert a time value to a string according to a format specification.
        /// Uses the current local time.
        /// </summary>
        /// <param name="format">A format string using Python-style format codes
        /// (e.g. <c>%Y-%m-%d %H:%M:%S</c>).</param>
        /// <returns>The formatted time string.</returns>
        /// <example>
        /// <code>
        /// time.strftime("%Y-%m-%d")    # e.g. "2024-01-15"
        /// </code>
        /// </example>
        public static string Strftime(string format)
        {
            var now = System.DateTime.Now;
            return now.ToString(ConvertFormat(format, now), CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Convert current UTC time to a <see cref="StructTime"/> (similar to Python's
        /// <c>time.gmtime()</c>).
        /// </summary>
        /// <returns>A <see cref="StructTime"/> representing the current UTC time.</returns>
        /// <example>
        /// <code>
        /// t = time.gmtime()
        /// print(t.tm_year)    # e.g. 2024
        /// </code>
        /// </example>
        public static StructTime Gmtime()
        {
            return StructTime.FromDateTime(System.DateTime.UtcNow);
        }

        /// <summary>
        /// Convert current local time to a <see cref="StructTime"/> (similar to Python's
        /// <c>time.localtime()</c>).
        /// </summary>
        /// <returns>A <see cref="StructTime"/> representing the current local time.</returns>
        /// <example>
        /// <code>
        /// t = time.localtime()
        /// print(t.tm_hour)    # current local hour
        /// </code>
        /// </example>
        public static StructTime Localtime()
        {
            return StructTime.FromDateTime(System.DateTime.Now);
        }

        /// <summary>
        /// Convert a Python strftime format string to a .NET DateTime format string.
        /// Codes that have no .NET equivalent (<c>%j</c>, <c>%w</c>, <c>%Z</c>) are
        /// evaluated against <paramref name="dt"/> and embedded as literals.
        /// </summary>
        internal static string ConvertFormat(string format, System.DateTime? dt = null)
        {
            var result = new System.Text.StringBuilder(format.Length * 2);
            int i = 0;

            while (i < format.Length)
            {
                if (format[i] == '%' && i + 1 < format.Length)
                {
                    char code = format[i + 1];
                    string twoChar = format.Substring(i, 2);

                    if (twoChar == "%%")
                    {
                        result.Append('%');
                        i += 2;
                        continue;
                    }

                    if (_formatMap.TryGetValue(twoChar, out string? mapped))
                    {
                        result.Append(mapped);
                        i += 2;
                        continue;
                    }

                    if (code == 'j')
                    {
                        // Day of year (001-366) — no .NET format specifier
                        int dayOfYear = (dt ?? System.DateTime.Now).DayOfYear;
                        result.Append(dayOfYear.ToString("D3", CultureInfo.InvariantCulture));
                        i += 2;
                        continue;
                    }

                    if (code == 'w')
                    {
                        // Day of week as integer (0=Sunday, 6=Saturday)
                        int dow = (int)(dt ?? System.DateTime.Now).DayOfWeek;
                        result.Append(dow.ToString(CultureInfo.InvariantCulture));
                        i += 2;
                        continue;
                    }

                    if (code == 'Z')
                    {
                        // Timezone name
                        result.Append(TimeZoneInfo.Local.StandardName);
                        i += 2;
                        continue;
                    }

                    if (code == 'f')
                    {
                        // Microseconds (000000-999999). .NET DateTime only has
                        // millisecond precision, so we pad with zeros.
                        int ms = (dt ?? System.DateTime.Now).Millisecond;
                        result.Append((ms * 1000).ToString("D6", CultureInfo.InvariantCulture));
                        i += 2;
                        continue;
                    }

                    // Unknown format code - pass through as-is
                    result.Append(twoChar);
                    i += 2;
                }
                else
                {
                    // Escape characters that .NET would interpret as format specifiers
                    char c = format[i];
                    if (char.IsLetter(c))
                    {
                        result.Append('\\');
                    }
                    result.Append(c);
                    i++;
                }
            }

            return result.ToString();
        }
    }
}
