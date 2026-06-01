#nullable enable

using System;

namespace Sharpy
{
    public static partial class CalendarModule
    {
        public static bool Isleap(int year)
        {
            return System.DateTime.IsLeapYear(year);
        }

        public static int Leapdays(int y1, int y2)
        {
            int count = 0;
            int step = y1 <= y2 ? 1 : -1;
            for (int y = y1; y != y2; y += step)
            {
                if (System.DateTime.IsLeapYear(y))
                    count += step;
            }
            return count;
        }

        public static int Weekday(int year, int month, int day)
        {
            var dt = new System.DateTime(year, month, day);
            return ((int)dt.DayOfWeek + 6) % 7;
        }

        public static (int, int) Monthrange(int year, int month)
        {
            var firstDay = new System.DateTime(year, month, 1);
            int weekday = ((int)firstDay.DayOfWeek + 6) % 7;
            int daysInMonth = System.DateTime.DaysInMonth(year, month);
            return (weekday, daysInMonth);
        }

        public static List<List<int>> Monthcalendar(int year, int month)
        {
            var cal = new Calendar(_moduleFirstweekday);
            return cal.Monthdayscalendar(year, month);
        }

        public static string Month(int year, int month, int w = 2, int l = 1)
        {
            var cal = new TextCalendar(_moduleFirstweekday);
            return cal.Formatmonth(year, month, w, l);
        }

        public static string CalendarText(int year, int w = 2, int l = 1, int c = 6, int m = 3)
        {
            var cal = new TextCalendar(_moduleFirstweekday);
            return cal.Formatyear(year, w, l, c, m);
        }

        public static void Setfirstweekday(int weekday)
        {
            if (weekday < 0 || weekday > 6)
                throw new ValueError($"bad weekday number {weekday}; must be 0 (Monday) to 6 (Sunday)");
            _moduleFirstweekday = weekday;
        }

        public static void Prmonth(int year, int month, int w = 2, int l = 1)
        {
            Console.Write(Month(year, month, w, l));
        }

        public static void Prcal(int year, int w = 2, int l = 1, int c = 6, int m = 3)
        {
            Console.Write(CalendarText(year, w, l, c, m));
        }

        public static long Timegm(int year, int month, int day, int hour, int minute, int second)
        {
            return new DateTimeOffset(year, month, day, hour, minute, second, TimeSpan.Zero).ToUnixTimeSeconds();
        }
    }
}
