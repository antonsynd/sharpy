#nullable enable

using System;
using System.Collections.Generic;
using SCG = System.Collections.Generic;

namespace Sharpy
{
    [SharpyModuleType("calendar", "Calendar")]
    public class Calendar
    {
        public int Firstweekday { get; set; }

        public Calendar(int firstweekday = 0)
        {
            Firstweekday = firstweekday;
        }

        public IEnumerable<int> Itermonthdays(int year, int month)
        {
            int daysInMonth = System.DateTime.DaysInMonth(year, month);
            var firstDay = new System.DateTime(year, month, 1);
            int firstDayWeekday = ((int)firstDay.DayOfWeek + 6) % 7;
            int offset = (firstDayWeekday - Firstweekday + 7) % 7;

            for (int i = 0; i < offset; i++)
                yield return 0;
            for (int day = 1; day <= daysInMonth; day++)
                yield return day;
            int trailing = (7 - ((offset + daysInMonth) % 7)) % 7;
            for (int i = 0; i < trailing; i++)
                yield return 0;
        }

        public IEnumerable<(int day, int weekday)> Itermonthdays2(int year, int month)
        {
            int dayIdx = 0;
            foreach (int day in Itermonthdays(year, month))
            {
                int weekday = (Firstweekday + dayIdx) % 7;
                yield return (day, weekday);
                dayIdx++;
            }
        }

        public List<List<int>> Monthdayscalendar(int year, int month)
        {
            var result = new List<List<int>>();
            var week = new SCG.List<int>();

            foreach (int day in Itermonthdays(year, month))
            {
                week.Add(day);
                if (week.Count == 7)
                {
                    result.Append(new List<int>(week));
                    week = new SCG.List<int>();
                }
            }

            return result;
        }

        internal SCG.List<SCG.List<int>> MonthdayscalendarRaw(int year, int month)
        {
            var result = new SCG.List<SCG.List<int>>();
            var week = new SCG.List<int>();

            foreach (int day in Itermonthdays(year, month))
            {
                week.Add(day);
                if (week.Count == 7)
                {
                    result.Add(week);
                    week = new SCG.List<int>();
                }
            }

            return result;
        }

        public List<List<(int day, int weekday)>> Monthdays2calendar(int year, int month)
        {
            var result = new List<List<(int day, int weekday)>>();
            var week = new SCG.List<(int day, int weekday)>();

            foreach (var pair in Itermonthdays2(year, month))
            {
                week.Add(pair);
                if (week.Count == 7)
                {
                    result.Append(new List<(int day, int weekday)>(week));
                    week = new SCG.List<(int day, int weekday)>();
                }
            }

            return result;
        }

        internal static DayOfWeek IsoDayToDotNet(int isoDay)
        {
            return isoDay == 6 ? DayOfWeek.Sunday : (DayOfWeek)(isoDay + 1);
        }
    }
}
