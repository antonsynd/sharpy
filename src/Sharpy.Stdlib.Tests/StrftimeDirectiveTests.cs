using Xunit;

namespace Sharpy.Stdlib.Tests
{
    /// <summary>
    /// python3 3.12's <c>strftime</c> on macOS for every directive, one row per format:
    /// (format, naive <c>datetime(2021, 1, 3, 13, 4, 5, 60)</c>, the same aware in
    /// <c>timezone.utc</c>, <c>date(2021, 1, 3)</c>, <c>time(13, 4, 5, 60)</c>). The documented set,
    /// <c>%:z</c> (3.12), the platform directives python passes through (e C D T R r n t F h k l g, +, v),
    /// an unknown directive (<c>%Q</c> prints <c>Q</c>), padding flags and E/O modifiers, a trailing
    /// <c>%</c>, and literal <c>j</c>/<c>w</c>/<c>Z</c> text next to <c>%j</c>/<c>%w</c>/<c>%Z</c>.
    /// Every value is independent of the host timezone (python3 under TZ=America/New_York and
    /// TZ=Asia/Tokyo agree); <c>%s</c>, the one host-local directive, is left out.
    /// </summary>
    internal static class StrftimePythonTable
    {
        internal static readonly (string Format, string Naive, string Aware, string Date, string Time)[] Rows =
        {
        ("%a", "Sun", "Sun", "Sun", "Mon"),
        ("%A", "Sunday", "Sunday", "Sunday", "Monday"),
        ("%w", "0", "0", "0", "1"),
        ("%d", "03", "03", "03", "01"),
        ("%b", "Jan", "Jan", "Jan", "Jan"),
        ("%B", "January", "January", "January", "January"),
        ("%m", "01", "01", "01", "01"),
        ("%y", "21", "21", "21", "00"),
        ("%Y", "2021", "2021", "2021", "1900"),
        ("%H", "13", "13", "00", "13"),
        ("%I", "01", "01", "12", "01"),
        ("%p", "PM", "PM", "AM", "PM"),
        ("%M", "04", "04", "00", "04"),
        ("%S", "05", "05", "00", "05"),
        ("%f", "000060", "000060", "000000", "000060"),
        ("%z", "", "+0000", "", ""),
        ("%Z", "", "UTC", "", ""),
        ("%j", "003", "003", "003", "001"),
        ("%U", "01", "01", "01", "00"),
        ("%W", "00", "00", "00", "01"),
        ("%c", "Sun Jan  3 13:04:05 2021", "Sun Jan  3 13:04:05 2021", "Sun Jan  3 00:00:00 2021", "Mon Jan  1 13:04:05 1900"),
        ("%x", "01/03/21", "01/03/21", "01/03/21", "01/01/00"),
        ("%X", "13:04:05", "13:04:05", "00:00:00", "13:04:05"),
        ("%%", "%", "%", "%", "%"),
        ("%G", "2020", "2020", "2020", "1900"),
        ("%u", "7", "7", "7", "1"),
        ("%V", "53", "53", "53", "01"),
        ("%e", " 3", " 3", " 3", " 1"),
        ("%C", "20", "20", "20", "19"),
        ("%D", "01/03/21", "01/03/21", "01/03/21", "01/01/00"),
        ("%T", "13:04:05", "13:04:05", "00:00:00", "13:04:05"),
        ("%R", "13:04", "13:04", "00:00", "13:04"),
        ("%r", "01:04:05 PM", "01:04:05 PM", "12:00:00 AM", "01:04:05 PM"),
        ("%n", "\n", "\n", "\n", "\n"),
        ("%t", "\t", "\t", "\t", "\t"),
        ("%F", "2021-01-03", "2021-01-03", "2021-01-03", "1900-01-01"),
        ("%h", "Jan", "Jan", "Jan", "Jan"),
        ("%k", "13", "13", " 0", "13"),
        ("%l", " 1", " 1", "12", " 1"),
        ("%g", "20", "20", "20", "00"),
        ("%:z", "", "+00:00", "", ""),
        ("%j now", "003 now", "003 now", "003 now", "001 now"),
        ("Zulu %Z just", "Zulu  just", "Zulu UTC just", "Zulu  just", "Zulu  just"),
        ("%w jam", "0 jam", "0 jam", "0 jam", "1 jam"),
        ("day %j of jan", "day 003 of jan", "day 003 of jan", "day 003 of jan", "day 001 of jan"),
        ("%Q", "Q", "Q", "Q", "Q"),
        ("%-d/%-m %_H %0e", "3/1 13 03", "3/1 13 03", "3/1  0 03", "1/1 13 01"),
        ("%Ey %OH %EEd", "21 13 Ed", "21 13 Ed", "21 00 Ed", "00 13 Ed"),
        ("x%", "x%", "x%", "x%", "x%"),
        ("%+", "Sun Jan  3 13:04:05  2021", "Sun Jan  3 13:04:05  2021", "Sun Jan  3 00:00:00  2021", "Mon Jan  1 13:04:05  1900"),
        ("%v", " 3-Jan-2021", " 3-Jan-2021", " 3-Jan-2021", " 1-Jan-1900"),
        };
    }

    /// <summary>
    /// python's <c>strftime</c> (<see cref="StrftimeFormat"/>) against python3 3.12 on macOS, in
    /// process: the table on each of <c>date</c>/<c>time</c>/<c>datetime</c>, the BSD platform rules
    /// (flags, modifiers, unknown conversions, a conversion cut by the end/NUL/non-ASCII), the aware
    /// <c>%z</c>/<c>%:z</c>/<c>%Z</c> for every offset shape, calendar edges (ISO weeks, years below
    /// 1000) and the 12-hour clock. <c>time.strftime</c> runs the same platform pass.
    /// </summary>
    public class StrftimeDirectiveTests
    {
        [Fact]
        public void PythonTable_OnEveryValueKind_MatchesPython()
        {
            var naive = new Sharpy.DateTime(2021, 1, 3, 13, 4, 5, 60);
            var aware = new Sharpy.DateTime(2021, 1, 3, 13, 4, 5, 60, Timezone.Utc);
            var date = new Date(2021, 1, 3);
            var time = new Time(13, 4, 5, 60);
            var mismatches = new System.Collections.Generic.List<string>();
            foreach (var row in StrftimePythonTable.Rows)
            {
                Check(row.Format, "naive", row.Naive, naive.Strftime(row.Format));
                Check(row.Format, "aware", row.Aware, aware.Strftime(row.Format));
                Check(row.Format, "date", row.Date, date.Strftime(row.Format));
                Check(row.Format, "time", row.Time, time.Strftime(row.Format));
            }
            Assert.True(mismatches.Count == 0, string.Join("\n", mismatches));

            void Check(string format, string kind, string python, string actual)
            {
                if (actual != python)
                    mismatches.Add($"{kind} {Repr(format)}: python {Repr(python)}, got {Repr(actual)}");
            }
        }

        // datetime(2021, 1, 3, 9, 4, 5, 60): the platform pass's grammar, measured cell by cell.
        [Theory]
        [InlineData("%-d", "3")]
        [InlineData("%_d", " 3")]
        [InlineData("%0e", "03")]
        [InlineData("%-j", "3")]
        [InlineData("%_j", "  3")]
        [InlineData("%0k", "09")]
        [InlineData("%-I", "9")]
        [InlineData("%_V", "53")]
        [InlineData("%-U", "1")]
        [InlineData("%_u", "7")]
        [InlineData("%-y", "21")]
        [InlineData("%-C", "20")]
        [InlineData("%-Y", "2021")]
        [InlineData("%E-d", "3")]
        [InlineData("%-Ed", "3")]
        [InlineData("%--d", "-d")]
        [InlineData("%-0d", "0d")]
        [InlineData("%-E-d", "-d")]
        [InlineData("%EEd", "Ed")]
        [InlineData("%EOd", "Od")]
        [InlineData("%Ex", "01/03/21")]
        [InlineData("%OS", "05")]
        [InlineData("%-p", "AM")]
        [InlineData("%-f", "f")]
        [InlineData("%Ef", "f")]
        [InlineData("%-z", "")]
        [InlineData("%EZ", "")]
        [InlineData("%E:z", ":z")]
        [InlineData("%-:z", ":z")]
        [InlineData("%:", ":")]
        [InlineData("%::z", "::z")]
        [InlineData("%:Z", ":Z")]
        [InlineData("%%z", "%z")]
        [InlineData("%-%Y", "%Y")]
        [InlineData("%E%", "%")]
        [InlineData("%%%", "%%")]
        [InlineData("%", "%")]
        [InlineData("%-", "-")]
        [InlineData("%E", "E")]
        [InlineData("%-E", "E")]
        [InlineData("%E-", "-")]
        [InlineData("%\u00e9x", "%\u00e9x")]
        [InlineData("%-\u00e9", "-\u00e9")]
        [InlineData("%E\u00e9", "E\u00e9")]
        [InlineData("a\u00e9%Y", "a\u00e92021")]
        [InlineData("%\0x", "%\0x")]
        [InlineData("%-\0", "-\0")]
        [InlineData("x\0%Y", "x\02021")]
        [InlineData("%\u007f", "\u007f")]
        [InlineData("%10d", "10d")]
        [InlineData("%#", "#")]
        [InlineData("%^a", "^a")]
        [InlineData("%P", "P")]
        [InlineData("%i", "i")]
        [InlineData("%+", "Sun Jan  3 09:04:05  2021")]
        [InlineData("%-c", "Sun Jan  3 09:04:05 2021")]
        [InlineData("%-D", "01/03/21")]
        [InlineData("%v", " 3-Jan-2021")]
        [InlineData("%L", "L")]
        public void PlatformRules_MatchPython(string format, string python)
            => Assert.Equal(python, new Sharpy.DateTime(2021, 1, 3, 9, 4, 5, 60).Strftime(format));

        // datetime(2021, 1, 3, 9, 4, 5, 60, tzinfo=timezone(timedelta(hours, minutes, seconds, microseconds)[, name])).
        [Theory]
        [InlineData(5, 30, 0, 0, null, "[+0530|+05:30|UTC+05:30||Sun Jan  3 09:04:05  2021]")]
        [InlineData(-8, 0, 0, 0, null, "[-0800|-08:00|UTC-08:00||Sun Jan  3 09:04:05  2021]")]
        [InlineData(1, 0, 7, 3, null, "[+010007.000003|+01:00:07.000003|UTC+01:00:07.000003||Sun Jan  3 09:04:05  2021]")]
        [InlineData(-1, -30, 0, 0, "Foo", "[-0130|-01:30|Foo||Sun Jan  3 09:04:05  2021]")]
        [InlineData(0, 0, 0, 0, "", "[+0000|+00:00|||Sun Jan  3 09:04:05  2021]")]
        [InlineData(0, 0, 0, 0, "a%b", "[+0000|+00:00|a%b||Sun Jan  3 09:04:05  2021]")]
        [InlineData(0, 0, 0, 0, null, "[+0000|+00:00|UTC||Sun Jan  3 09:04:05  2021]")]
        public void AwareOffsetAndName_MatchPython(int hours, int minutes, int seconds, int microseconds, string? name, string python)
        {
            var offset = new Timedelta(hours: hours, minutes: minutes, seconds: seconds, microseconds: microseconds);
            var tz = name is null ? new Timezone(offset) : new Timezone(offset, name);
            Assert.Equal(python, new Sharpy.DateTime(2021, 1, 3, 9, 4, 5, 60, tz).Strftime("[%z|%:z|%Z|%-z|%+]"));
        }

        [Theory]
        [InlineData(1, 1, 4, "0001|00|01|0001|01|01|00|01|004|4|4|Thu Jan  4 00:00:00 0001|0001-01-04|01/04/01| 4-Jan-0001")]
        [InlineData(5, 1, 4, "0005|00|05|0005|05|01|01|01|004|2|2|Tue Jan  4 00:00:00 0005|0005-01-04|01/04/05| 4-Jan-0005")]
        [InlineData(999, 1, 4, "0999|09|99|0999|99|01|00|00|004|5|5|Fri Jan  4 00:00:00 0999|0999-01-04|01/04/99| 4-Jan-0999")]
        [InlineData(9999, 12, 31, "9999|99|99|9999|99|52|52|52|365|5|5|Fri Dec 31 00:00:00 9999|9999-12-31|12/31/99|31-Dec-9999")]
        [InlineData(2020, 12, 31, "2020|20|20|2020|20|53|52|52|366|4|4|Thu Dec 31 00:00:00 2020|2020-12-31|12/31/20|31-Dec-2020")]
        [InlineData(2024, 12, 30, "2024|20|24|2025|25|01|52|53|365|1|1|Mon Dec 30 00:00:00 2024|2024-12-30|12/30/24|30-Dec-2024")]
        [InlineData(2021, 1, 1, "2021|20|21|2020|20|53|00|00|001|5|5|Fri Jan  1 00:00:00 2021|2021-01-01|01/01/21| 1-Jan-2021")]
        [InlineData(2021, 1, 4, "2021|20|21|2021|21|01|01|01|004|1|1|Mon Jan  4 00:00:00 2021|2021-01-04|01/04/21| 4-Jan-2021")]
        [InlineData(2016, 1, 3, "2016|20|16|2015|15|53|01|00|003|7|0|Sun Jan  3 00:00:00 2016|2016-01-03|01/03/16| 3-Jan-2016")]
        public void CalendarFields_MatchPython(int year, int month, int day, string python)
            => Assert.Equal(python, new Date(year, month, day).Strftime("%Y|%C|%y|%G|%g|%V|%U|%W|%j|%u|%w|%c|%F|%x|%v"));

        [Theory]
        [InlineData(0, 0, "00|12| 0|12|AM|12:00:00 AM|12|12|00")]
        [InlineData(11, 59, "11|11|11|11|AM|11:59:00 AM|11|11|11")]
        [InlineData(12, 0, "12|12|12|12|PM|12:00:00 PM|12|12|12")]
        [InlineData(23, 5, "23|11|23|11|PM|11:05:00 PM|11|11|23")]
        public void TwelveHourClock_MatchPython(int hour, int minute, string python)
            => Assert.Equal(python, new Time(hour, minute).Strftime("%H|%I|%k|%l|%p|%r|%-I|%_l|%0k"));

        /// <summary>
        /// <c>isoformat()</c> and <c>str(timezone)</c> spell an offset as <c>%:z</c> does (seconds and
        /// microseconds when present); an unnamed zero offset is <c>UTC</c>. python3 3.12.
        /// </summary>
        [Fact]
        public void IsoformatAndTimezoneStr_ShareTheOffsetSpelling()
        {
            var seconds = new Timezone(new Timedelta(hours: 1, seconds: 7, microseconds: 3));
            Assert.Equal("2021-01-03T09:04:05.000060+01:00:07.000003", new Sharpy.DateTime(2021, 1, 3, 9, 4, 5, 60, seconds).Isoformat());
            Assert.Equal("2021-01-03T09:04:05-08:00", new Sharpy.DateTime(2021, 1, 3, 9, 4, 5, 0, new Timezone(new Timedelta(hours: -8))).Isoformat());
            Assert.Equal("UTC", new Timezone(new Timedelta()).ToString());
            Assert.Equal("UTC-01:30", new Timezone(new Timedelta(hours: -1, minutes: -30)).ToString());
            Assert.Equal("UTC+01:00:07.000003", seconds.ToString());
        }

        /// <summary>
        /// <c>%s</c> is the platform's <c>mktime</c>: the wall time read as host-local time, tzinfo
        /// ignored — python3 prints 1609682645 for both under TZ=America/New_York.
        /// </summary>
        [Fact]
        public void EpochSeconds_ReadsTheWallTimeAsHostLocal()
        {
            var wall = new System.DateTime(2021, 1, 3, 9, 4, 5, System.DateTimeKind.Local);
            var mktime = new System.DateTimeOffset(wall).ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
            Assert.Equal(mktime, new Sharpy.DateTime(2021, 1, 3, 9, 4, 5, 60).Strftime("%s"));
            Assert.Equal(mktime, new Sharpy.DateTime(2021, 1, 3, 9, 4, 5, 60, Timezone.Utc).Strftime("%s"));
        }

        /// <summary>
        /// <c>time.strftime</c> is the platform pass over the local time: <c>%f</c> is unknown there
        /// (python3: <c>f</c>), literal text survives, and <c>%z</c>/<c>%Z</c> are the host zone's.
        /// </summary>
        [Fact]
        public void TimeModuleStrftime_RunsThePlatformPass()
        {
            Assert.Equal("f|Q|%|\n|now x%", TimeModule.Strftime("%f|%Q|%%|%n|now x%"));
            Assert.Matches(@"^\d{3} jam [0-6]$", TimeModule.Strftime("%j jam %w"));
            Assert.Matches(@"^[+-]\d{4}$", TimeModule.Strftime("%z"));
            var local = System.TimeZoneInfo.Local;
            Assert.Contains(TimeModule.Strftime("%Z"), new[] { local.StandardName, local.DaylightName });
        }
    }

    [CollectionDefinition("HostTimeZone", DisableParallelization = true)]
    public sealed class HostTimeZoneCollection
    {
    }

    /// <summary>
    /// The table does not depend on the host timezone: formatted under two TZ settings (a swapped
    /// <c>TZ</c> and a cleared <see cref="System.TimeZoneInfo"/> cache, in a collection that runs
    /// alone), every cell is python's in both. Positive control: the host offset and <c>%s</c> do move.
    /// </summary>
    [Collection("HostTimeZone")]
    public class StrftimeHostTimeZoneTests
    {
        [Fact]
        public void PythonTable_IsTheSameUnderEveryHostTimeZone()
        {
            var saved = System.Environment.GetEnvironmentVariable("TZ");
            try
            {
                var tokyo = FormatAll("Asia/Tokyo", out var tokyoOffset, out var tokyoEpoch);
                var losAngeles = FormatAll("America/Los_Angeles", out var laOffset, out var laEpoch);
                Assert.NotEqual(tokyoOffset, laOffset);
                Assert.NotEqual(tokyoEpoch, laEpoch);
                var python = new System.Collections.Generic.List<string>();
                foreach (var row in StrftimePythonTable.Rows)
                    python.AddRange(new[] { row.Naive, row.Aware, row.Date, row.Time });
                Assert.Equal(python, tokyo);
                Assert.Equal(python, losAngeles);
            }
            finally
            {
                System.Environment.SetEnvironmentVariable("TZ", saved);
                System.TimeZoneInfo.ClearCachedData();
            }
        }

        private static System.Collections.Generic.List<string> FormatAll(string tz, out System.TimeSpan hostOffset, out string epoch)
        {
            System.Environment.SetEnvironmentVariable("TZ", tz);
            System.TimeZoneInfo.ClearCachedData();
            hostOffset = System.TimeZoneInfo.Local.GetUtcOffset(new System.DateTime(2021, 1, 3));
            var naive = new Sharpy.DateTime(2021, 1, 3, 13, 4, 5, 60);
            epoch = naive.Strftime("%s");
            var aware = new Sharpy.DateTime(2021, 1, 3, 13, 4, 5, 60, Timezone.Utc);
            var date = new Date(2021, 1, 3);
            var time = new Time(13, 4, 5, 60);
            var cells = new System.Collections.Generic.List<string>();
            foreach (var row in StrftimePythonTable.Rows)
                cells.AddRange(new[] { naive.Strftime(row.Format), aware.Strftime(row.Format), date.Strftime(row.Format), time.Strftime(row.Format) });
            return cells;
        }
    }
}
