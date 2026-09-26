using System;
using System.Globalization;

namespace Sharpy
{
    /// <summary>
    /// python's <c>strftime</c> as it runs on macOS (CPython 3.12 over the BSD libc): every directive's
    /// text is computed from the value and its tzinfo, and literal text is copied verbatim — never a
    /// .NET custom format string, never a post-replace. Two passes, as CPython has: python's own pass
    /// replaces <c>%z</c>, <c>%:z</c>, <c>%Z</c> and <c>%f</c>; the platform pass then formats the rest
    /// in the C locale. Shared by the datetime and time packaging assemblies (each compiles it in).
    /// </summary>
    internal static class StrftimeFormat
    {
        private static readonly string[] DayNames = { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };
        private static readonly string[] MonthNames = { "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December" };

        /// <summary>
        /// Format <paramref name="dt"/> as <c>strftime(format)</c>. <paramref name="utcOffset"/> and
        /// <paramref name="tzname"/> are the tzinfo's <c>utcoffset()</c>/<c>tzname()</c>; both null for a
        /// naive value, whose <c>%z</c>/<c>%:z</c>/<c>%Z</c> are empty.
        /// </summary>
        internal static string Strftime(System.DateTime dt, string format, TimeSpan? utcOffset = null, string? tzname = null)
        {
            // python's pass (CPython wrap_strftime): the result is a platform format.
            var platform = new System.Text.StringBuilder(format.Length);
            int i = 0;
            while (i < format.Length)
            {
                char c = format[i];
                if (c != '%' || i + 1 >= format.Length || format[i + 1] == '\0')
                {
                    platform.Append(c);
                    i++;
                    continue;
                }
                char next = format[i + 1];
                if (next == 'z')
                {
                    AppendUtcOffset(platform, utcOffset, "");
                    i += 2;
                }
                else if (next == ':' && i + 2 < format.Length && format[i + 2] == 'z')
                {
                    AppendUtcOffset(platform, utcOffset, ":");
                    i += 3;
                }
                else if (next == 'Z')
                {
                    platform.Append((tzname ?? "").Replace("%", "%%"));
                    i += 2;
                }
                else if (next == 'f')
                {
                    platform.Append(Microsecond(dt).ToString("D6", CultureInfo.InvariantCulture));
                    i += 2;
                }
                else
                {
                    platform.Append(c).Append(next);
                    i += 2;
                }
            }
            return FormatPlatform(dt, platform.ToString(), null, "");
        }

        /// <summary>
        /// The platform pass (BSD libc <c>strftime</c>, C locale): <c>%</c>, then at most one padding flag
        /// (<c>-</c> none, <c>_</c> spaces, <c>0</c> zeros) and at most one <c>E</c>/<c>O</c> modifier in
        /// either order, then a conversion; an unknown conversion prints itself (<c>%Q</c> is <c>Q</c>).
        /// A conversion cannot span the end, a NUL or a non-ASCII character: the last character read is
        /// printed instead (<c>%</c> at the end is <c>%</c>). <paramref name="zoneOffset"/>/
        /// <paramref name="zoneName"/> are the struct's zone (<c>time.strftime</c>); a datetime's struct has
        /// none, so its platform <c>%z</c>/<c>%Z</c> are empty.
        /// </summary>
        internal static string FormatPlatform(System.DateTime dt, string format, TimeSpan? zoneOffset, string zoneName)
        {
            var sb = new System.Text.StringBuilder(format.Length * 2);
            AppendPlatform(sb, dt, format, zoneOffset, zoneName);
            return sb.ToString();
        }

        private static void AppendPlatform(System.Text.StringBuilder sb, System.DateTime dt, string format, TimeSpan? zoneOffset, string zoneName)
        {
            int i = 0;
            while (i < format.Length)
            {
                char c = format[i++];
                if (c != '%')
                {
                    sb.Append(c);
                    continue;
                }
                char last = '%';
                char pad = '\0';
                bool modified = false;
                while (true)
                {
                    if (i >= format.Length || format[i] == '\0' || format[i] > '\u007f')
                    {
                        sb.Append(last);
                        break;
                    }
                    char k = format[i++];
                    if ((k == '-' || k == '_' || k == '0') && pad == '\0')
                    {
                        pad = k;
                        last = k;
                        continue;
                    }
                    if ((k == 'E' || k == 'O') && !modified)
                    {
                        modified = true;
                        last = k;
                        continue;
                    }
                    AppendConversion(sb, dt, k, pad, zoneOffset, zoneName);
                    break;
                }
            }
        }

        private static void AppendConversion(System.Text.StringBuilder sb, System.DateTime dt, char k, char pad, TimeSpan? zoneOffset, string zoneName)
        {
            int dow = (int)dt.DayOfWeek; // Sunday = 0
            int hour12 = dt.Hour % 12 == 0 ? 12 : dt.Hour % 12;
            switch (k)
            {
                case 'A':
                    sb.Append(DayNames[dow]);
                    break;
                case 'a':
                    sb.Append(DayNames[dow], 0, 3);
                    break;
                case 'B':
                    sb.Append(MonthNames[dt.Month - 1]);
                    break;
                case 'b':
                case 'h':
                    sb.Append(MonthNames[dt.Month - 1], 0, 3);
                    break;
                case 'C':
                    AppendFixed(sb, dt.Year / 100, 2);
                    break;
                case 'c':
                    AppendPlatform(sb, dt, "%a %b %e %H:%M:%S %Y", zoneOffset, zoneName);
                    break;
                case 'D':
                case 'x':
                    AppendPlatform(sb, dt, "%m/%d/%y", zoneOffset, zoneName);
                    break;
                case 'd':
                    AppendPadded(sb, dt.Day, 2, '0', pad);
                    break;
                case 'e':
                    AppendPadded(sb, dt.Day, 2, ' ', pad);
                    break;
                case 'F':
                    AppendPlatform(sb, dt, "%Y-%m-%d", zoneOffset, zoneName);
                    break;
                case 'G':
                    AppendYear(sb, IsoYearWeek(dt, out _));
                    break;
                case 'g':
                    AppendFixed(sb, IsoYearWeek(dt, out _) % 100, 2);
                    break;
                case 'H':
                    AppendPadded(sb, dt.Hour, 2, '0', pad);
                    break;
                case 'I':
                    AppendPadded(sb, hour12, 2, '0', pad);
                    break;
                case 'j':
                    AppendPadded(sb, dt.DayOfYear, 3, '0', pad);
                    break;
                case 'k':
                    AppendPadded(sb, dt.Hour, 2, ' ', pad);
                    break;
                case 'l':
                    AppendPadded(sb, hour12, 2, ' ', pad);
                    break;
                case 'M':
                    AppendPadded(sb, dt.Minute, 2, '0', pad);
                    break;
                case 'm':
                    AppendPadded(sb, dt.Month, 2, '0', pad);
                    break;
                case 'n':
                    sb.Append('\n');
                    break;
                case 'p':
                    sb.Append(dt.Hour < 12 ? "AM" : "PM");
                    break;
                case 'R':
                    AppendPlatform(sb, dt, "%H:%M", zoneOffset, zoneName);
                    break;
                case 'r':
                    AppendPlatform(sb, dt, "%I:%M:%S %p", zoneOffset, zoneName);
                    break;
                case 'S':
                    AppendPadded(sb, dt.Second, 2, '0', pad);
                    break;
                case 's':
                    // mktime: the wall time read as host-local time, as the platform does.
                    var local = System.DateTime.SpecifyKind(dt, DateTimeKind.Local);
                    sb.Append(new DateTimeOffset(local).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture));
                    break;
                case 'T':
                case 'X':
                    AppendPlatform(sb, dt, "%H:%M:%S", zoneOffset, zoneName);
                    break;
                case 't':
                    sb.Append('\t');
                    break;
                case 'U':
                    AppendPadded(sb, (dt.DayOfYear - 1 + 7 - dow) / 7, 2, '0', pad);
                    break;
                case 'u':
                    sb.Append(dow == 0 ? 7 : dow);
                    break;
                case 'V':
                    IsoYearWeek(dt, out int week);
                    AppendPadded(sb, week, 2, '0', pad);
                    break;
                case 'v':
                    AppendPlatform(sb, dt, "%e-%b-%Y", zoneOffset, zoneName);
                    break;
                case 'W':
                    AppendPadded(sb, (dt.DayOfYear - 1 + 7 - (dow + 6) % 7) / 7, 2, '0', pad);
                    break;
                case 'w':
                    sb.Append(dow);
                    break;
                case 'Y':
                    AppendYear(sb, dt.Year);
                    break;
                case 'y':
                    AppendFixed(sb, dt.Year % 100, 2);
                    break;
                case 'Z':
                    sb.Append(zoneName);
                    break;
                case 'z':
                    if (zoneOffset is TimeSpan offset)
                        AppendUtcOffset(sb, offset, "");
                    break;
                case '+':
                    AppendPlatform(sb, dt, "%a %b %e %H:%M:%S %Z %Y", zoneOffset, zoneName);
                    break;
                default:
                    sb.Append(k);
                    break;
            }
        }

        /// <summary>
        /// python's utcoffset text: <c>±HH&lt;sep&gt;MM</c>, then <c>&lt;sep&gt;SS</c> when there are seconds
        /// and <c>.ffffff</c> when there are microseconds; nothing for a naive value.
        /// </summary>
        internal static void AppendUtcOffset(System.Text.StringBuilder sb, TimeSpan? utcOffset, string sep)
        {
            if (utcOffset is not TimeSpan offset)
                return;
            sb.Append(offset < TimeSpan.Zero ? '-' : '+');
            var abs = offset.Duration();
            sb.Append(((int)abs.TotalHours).ToString("D2", CultureInfo.InvariantCulture))
                .Append(sep).Append(abs.Minutes.ToString("D2", CultureInfo.InvariantCulture));
            long microseconds = abs.Ticks % TimeSpan.TicksPerSecond / 10;
            if (abs.Seconds != 0 || microseconds != 0)
                sb.Append(sep).Append(abs.Seconds.ToString("D2", CultureInfo.InvariantCulture));
            if (microseconds != 0)
                sb.Append('.').Append(microseconds.ToString("D6", CultureInfo.InvariantCulture));
        }

        private static int Microsecond(System.DateTime dt) => (int)(dt.Ticks % TimeSpan.TicksPerSecond / 10);

        // The ISO 8601 year and week: those of the Thursday of dt's Monday-first week.
        private static int IsoYearWeek(System.DateTime dt, out int week)
        {
            int isoDow = dt.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)dt.DayOfWeek;
            var thursday = dt.Date.AddDays(4 - isoDow);
            week = (thursday.DayOfYear - 1) / 7 + 1;
            return thursday.Year;
        }

        // A year as the platform prints it: its century and its two-digit year, each zero-padded to 2.
        private static void AppendYear(System.Text.StringBuilder sb, int year)
        {
            AppendFixed(sb, year / 100, 2);
            AppendFixed(sb, year % 100, 2);
        }

        private static void AppendFixed(System.Text.StringBuilder sb, int value, int width)
            => sb.Append(value.ToString(CultureInfo.InvariantCulture).PadLeft(width, '0'));

        // A numeric field honouring the padding flag: '-' none, '_' spaces, '0' zeros, else its default.
        private static void AppendPadded(System.Text.StringBuilder sb, int value, int width, char defaultPad, char flag)
        {
            var digits = value.ToString(CultureInfo.InvariantCulture);
            if (flag == '-')
                sb.Append(digits);
            else
                sb.Append(digits.PadLeft(width, flag == '_' ? ' ' : flag == '0' ? '0' : defaultPad));
        }
    }
}
