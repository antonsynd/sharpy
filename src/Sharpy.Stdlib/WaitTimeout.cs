using System;

namespace Sharpy
{
    /// <summary>
    /// The one conversion from a python timeout in seconds to a .NET wait, shared by every Stdlib
    /// parameter that takes seconds — <c>threading</c>, <c>subprocess</c>, <c>time.sleep</c>,
    /// <c>http</c> and <c>requests</c> (#2263). What a negative or zero timeout means is each
    /// caller's own python verdict: a <c>Lock</c> refuses it, a <c>Semaphore</c> or an <c>Event</c>
    /// stops at once, <c>Thread.join</c> treats it as zero. The callers keep that; this class owns
    /// the two verdicts they share, so no caller hands .NET a value it reads as "wait forever" or
    /// refuses with <c>ArgumentOutOfRangeException</c>. Shared by the threading, subprocess, time,
    /// http and requests packaging assemblies (each compiles it in).
    /// </summary>
    internal static class WaitTimeout
    {
        // python converts a timeout to int64 nanoseconds before it uses it; a value outside that
        // range (about ±9223372036.85 s, just past threading.TIMEOUT_MAX) is OverflowError.
        private const double NanosecondLimit = 9223372036854775808.0;

        /// <summary>
        /// Raises python's error for a timeout python cannot convert: NaN is <see cref="ValueError"/>,
        /// a magnitude past int64 nanoseconds is <see cref="OverflowError"/>.
        /// </summary>
        internal static void CheckRepresentable(double seconds)
        {
            if (double.IsNaN(seconds))
            {
                throw new ValueError("Invalid value NaN (not a number)");
            }
            if (!(Math.Abs(seconds) * 1e9 < NanosecondLimit))
            {
                throw new OverflowError("timestamp out of range for platform time_t");
            }
        }

        /// <summary>
        /// The milliseconds to pass to a .NET wait. A value at or below zero is 0 (do not wait); a
        /// positive value rounds up, as python rounds a timeout, so it never becomes a poll; a value
        /// past <c>int.MaxValue</c> ms (about 24.8 days) saturates there — the deviation row
        /// <c>stdlib-timeout-wait-saturates</c> (R-FH). Never -1, which .NET reads as "wait forever".
        /// </summary>
        internal static int ToMilliseconds(double seconds)
        {
            if (!(seconds > 0))
            {
                return 0;
            }
            double ms = Math.Ceiling(seconds * 1000.0);
            return ms >= int.MaxValue ? int.MaxValue : (int)ms;
        }
    }
}
