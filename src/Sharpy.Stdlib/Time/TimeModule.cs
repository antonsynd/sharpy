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

            System.Threading.Thread.Sleep((int)ms);
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
            var local = TimeZoneInfo.Local;
            var zoneName = local.IsDaylightSavingTime(now) ? local.DaylightName : local.StandardName;
            return StrftimeFormat.FormatPlatform(now, format, local.GetUtcOffset(now), zoneName);
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
        /// Convert a Unix timestamp (seconds since the epoch) to a <see cref="StructTime"/>
        /// in UTC (similar to Python's <c>time.gmtime(secs)</c>).
        /// </summary>
        /// <param name="seconds">Seconds since the Unix epoch (1970-01-01T00:00:00Z).</param>
        /// <returns>A <see cref="StructTime"/> representing the specified UTC time.</returns>
        /// <example>
        /// <code>
        /// t = time.gmtime(0)
        /// print(t.tm_year)    # 1970
        /// </code>
        /// </example>
        public static StructTime Gmtime(double seconds)
        {
            var epoch = new System.DateTime(1970, 1, 1, 0, 0, 0, System.DateTimeKind.Utc);
            var dt = epoch.AddSeconds(seconds);
            return StructTime.FromDateTime(dt);
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
        /// Convert a Unix timestamp (seconds since the epoch) to a <see cref="StructTime"/>
        /// in local time (similar to Python's <c>time.localtime(secs)</c>).
        /// </summary>
        /// <param name="seconds">Seconds since the Unix epoch (1970-01-01T00:00:00Z).</param>
        /// <returns>A <see cref="StructTime"/> representing the specified local time.</returns>
        /// <example>
        /// <code>
        /// t = time.localtime(86400)
        /// print(t.tm_mday)    # depends on local timezone
        /// </code>
        /// </example>
        public static StructTime Localtime(double seconds)
        {
            var epoch = new System.DateTime(1970, 1, 1, 0, 0, 0, System.DateTimeKind.Utc);
            var utcDt = epoch.AddSeconds(seconds);
            var localDt = TimeZoneInfo.ConvertTimeFromUtc(utcDt, TimeZoneInfo.Local);
            return StructTime.FromDateTime(localDt);
        }
    }
}
