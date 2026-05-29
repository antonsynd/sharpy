using System;
using System.Globalization;
using System.Text;
using SysList = System.Collections.Generic.List<System.Collections.Generic.List<int>>;
using SysListInt = System.Collections.Generic.List<int>;

namespace Sharpy
{
    public static partial class CalendarModule
    {
        /// <summary>
        /// Return True if year is a leap year, False otherwise.
        /// </summary>
        public static bool Isleap(int year)
        {
            return System.DateTime.IsLeapYear(year);
        }

        /// <summary>
        /// Return the number of leap years in the range [y1, y2).
        /// </summary>
        public static int Leapdays(int y1, int y2)
        {
            int count = 0;
            for (int y = y1; y < y2; y++)
            {
                if (System.DateTime.IsLeapYear(y))
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// Return the day of the week (0=Monday, 6=Sunday) for year, month, day.
        /// </summary>
        public static int Weekday(int year, int month, int day)
        {
            var dt = new System.DateTime(year, month, day);
            return ((int)dt.DayOfWeek + 6) % 7;
        }

        /// <summary>
        /// Return a tuple (weekday of first day, number of days) for the given month/year.
        /// The weekday is 0=Monday through 6=Sunday.
        /// </summary>
        public static (int, int) Monthrange(int year, int month)
        {
            var firstDay = new System.DateTime(year, month, 1);
            int weekday = ((int)firstDay.DayOfWeek + 6) % 7;
            int daysInMonth = System.DateTime.DaysInMonth(year, month);
            return (weekday, daysInMonth);
        }

        /// <summary>
        /// Return a matrix (list of lists) representing a month's calendar.
        /// Each row represents a week; days outside the month are set to 0.
        /// </summary>
        public static SysList Monthcalendar(int year, int month, int firstweekday = 0)
        {
            int daysInMonth = System.DateTime.DaysInMonth(year, month);
            var firstDay = new System.DateTime(year, month, 1);
            int firstDayWeekday = ((int)firstDay.DayOfWeek + 6) % 7;

            // Offset from firstweekday
            int offset = (firstDayWeekday - firstweekday + 7) % 7;

            var weeks = new SysList();
            var week = new SysListInt();

            // Fill leading zeros
            for (int i = 0; i < offset; i++)
            {
                week.Add(0);
            }

            for (int day = 1; day <= daysInMonth; day++)
            {
                week.Add(day);
                if (week.Count == 7)
                {
                    weeks.Add(week);
                    week = new SysListInt();
                }
            }

            // Fill trailing zeros
            if (week.Count > 0)
            {
                while (week.Count < 7)
                {
                    week.Add(0);
                }
                weeks.Add(week);
            }

            return weeks;
        }

        /// <summary>
        /// Return a month's calendar as a multi-line string.
        /// </summary>
        public static string Month(int year, int month, int w = 2, int l = 1)
        {
            var cal = new TextCalendar(0);
            return cal.Formatmonth(year, month, w, l);
        }

        /// <summary>
        /// Return a 3-column calendar for an entire year as a multi-line string.
        /// </summary>
        public static string Calendar(int year, int w = 2, int l = 1, int c = 6, int m = 3)
        {
            var cal = new TextCalendar(0);
            return cal.Formatyear(year, w, l, c, m);
        }
    }
}
