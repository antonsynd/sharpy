namespace Sharpy
{
    /// <summary>
    /// Represents a time value as a named tuple of components, similar to Python's
    /// <c>time.struct_time</c>.
    /// </summary>
    /// <remarks>
    /// Provides fields <c>tm_year</c>, <c>tm_mon</c>, <c>tm_mday</c>, <c>tm_hour</c>,
    /// <c>tm_min</c>, <c>tm_sec</c>, <c>tm_wday</c>, <c>tm_yday</c>, and <c>tm_isdst</c>.
    /// </remarks>
    [SharpyModuleType("time")]
    public sealed class StructTime
    {
        /// <summary>Year (e.g. 2024).</summary>
        public int TmYear { get; }

        /// <summary>Month (1–12).</summary>
        public int TmMon { get; }

        /// <summary>Day of the month (1–31).</summary>
        public int TmMday { get; }

        /// <summary>Hour (0–23).</summary>
        public int TmHour { get; }

        /// <summary>Minute (0–59).</summary>
        public int TmMin { get; }

        /// <summary>Second (0–61; 60 and 61 are for leap seconds).</summary>
        public int TmSec { get; }

        /// <summary>Day of the week (0 = Monday, 6 = Sunday). Matches Python convention.</summary>
        public int TmWday { get; }

        /// <summary>Day of the year (1–366).</summary>
        public int TmYday { get; }

        /// <summary>
        /// Daylight saving time flag: 1 if DST is in effect, 0 if not, -1 if unknown.
        /// </summary>
        public int TmIsdst { get; }

        /// <summary>
        /// Creates a new <see cref="StructTime"/> with the specified components.
        /// </summary>
        public StructTime(int tmYear, int tmMon, int tmMday, int tmHour, int tmMin,
            int tmSec, int tmWday, int tmYday, int tmIsdst)
        {
            TmYear = tmYear;
            TmMon = tmMon;
            TmMday = tmMday;
            TmHour = tmHour;
            TmMin = tmMin;
            TmSec = tmSec;
            TmWday = tmWday;
            TmYday = tmYday;
            TmIsdst = tmIsdst;
        }

        /// <summary>
        /// Creates a <see cref="StructTime"/> from a <see cref="System.DateTime"/> value.
        /// </summary>
        /// <param name="dt">The DateTime to convert.</param>
        /// <returns>A StructTime representing the same point in time.</returns>
        internal static StructTime FromDateTime(System.DateTime dt)
        {
            // Python tm_wday: Monday=0, Sunday=6
            // .NET DayOfWeek: Sunday=0, Saturday=6
            int wday = dt.DayOfWeek == System.DayOfWeek.Sunday ? 6 : (int)dt.DayOfWeek - 1;

            int isdst = dt.Kind == System.DateTimeKind.Utc
                ? 0
                : (System.TimeZoneInfo.Local.IsDaylightSavingTime(dt) ? 1 : 0);

            return new StructTime(
                dt.Year,
                dt.Month,
                dt.Day,
                dt.Hour,
                dt.Minute,
                dt.Second,
                wday,
                dt.DayOfYear,
                isdst
            );
        }

        /// <summary>
        /// Returns a string representation matching Python's <c>time.struct_time</c> format.
        /// </summary>
        public override string ToString()
        {
            return $"time.struct_time(tm_year={TmYear}, tm_mon={TmMon}, tm_mday={TmMday}, " +
                   $"tm_hour={TmHour}, tm_min={TmMin}, tm_sec={TmSec}, " +
                   $"tm_wday={TmWday}, tm_yday={TmYday}, tm_isdst={TmIsdst})";
        }
    }
}
