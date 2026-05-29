using System;
using System.Globalization;
using System.Text;
using SysList = System.Collections.Generic.List<System.Collections.Generic.List<int>>;
using SysListInt = System.Collections.Generic.List<int>;
using SysListStr = System.Collections.Generic.List<string>;
using SysListWeeks = System.Collections.Generic.List<System.Collections.Generic.List<System.Collections.Generic.List<int>>>;

namespace Sharpy
{
    /// <summary>
    /// A calendar that produces plain text output, similar to Python's calendar.TextCalendar.
    /// </summary>
    [SharpyModuleType("calendar", "TextCalendar")]
    public class TextCalendar
    {
        /// <summary>The first day of the week (0=Monday, 6=Sunday).</summary>
        public int Firstweekday { get; set; }

        /// <summary>Create a TextCalendar with the given first day of the week.</summary>
        public TextCalendar(int firstweekday = 0)
        {
            Firstweekday = firstweekday;
        }

        /// <summary>
        /// Return a formatted month as a multi-line string.
        /// </summary>
        public string Formatmonth(int year, int month, int w = 2, int l = 1)
        {
            var sb = new StringBuilder();

            // Header: month name and year
            string monthName = CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(month);
            string header = $"{monthName} {year}";
            int totalWidth = 7 * (w + 1) - 1;
            sb.AppendLine(header.PadLeft((totalWidth + header.Length) / 2).PadRight(totalWidth));

            // Day name header
            AppendDayHeader(sb, w);
            sb.AppendLine();

            // Weeks
            var weeks = CalendarModule.Monthcalendar(year, month, Firstweekday);
            foreach (var week in weeks)
            {
                for (int i = 0; i < week.Count; i++)
                {
                    if (i > 0) sb.Append(' ');
                    if (week[i] == 0)
                        sb.Append(new string(' ', w));
                    else
                        sb.Append(week[i].ToString().PadLeft(w));
                }
                sb.AppendLine();
                for (int i = 1; i < l; i++)
                    sb.AppendLine();
            }

            return sb.ToString();
        }

        /// <summary>
        /// Return a formatted year calendar as a multi-line string.
        /// </summary>
        public string Formatyear(int year, int w = 2, int l = 1, int c = 6, int m = 3)
        {
            var sb = new StringBuilder();

            // Year header
            string yearHeader = year.ToString();
            int colWidth = 7 * (w + 1) - 1;
            int totalWidth = m * colWidth + (m - 1) * c;
            sb.AppendLine(yearHeader.PadLeft((totalWidth + yearHeader.Length) / 2).PadRight(totalWidth));
            sb.AppendLine();

            for (int monthStart = 1; monthStart <= 12; monthStart += m)
            {
                // Month name headers
                var headers = new SysListStr();
                for (int i = 0; i < m && monthStart + i <= 12; i++)
                {
                    string name = CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(monthStart + i);
                    headers.Add(name.PadLeft((colWidth + name.Length) / 2).PadRight(colWidth));
                }
                sb.AppendLine(string.Join(new string(' ', c), headers));

                // Day name headers
                var dayHeaders = new SysListStr();
                for (int i = 0; i < m && monthStart + i <= 12; i++)
                {
                    var dsb = new StringBuilder();
                    AppendDayHeader(dsb, w);
                    dayHeaders.Add(dsb.ToString().PadRight(colWidth));
                }
                sb.AppendLine(string.Join(new string(' ', c), dayHeaders));

                // Get weeks for each month in this row
                var allWeeks = new SysListWeeks();
                int maxWeeks = 0;
                for (int i = 0; i < m && monthStart + i <= 12; i++)
                {
                    var weeks = CalendarModule.Monthcalendar(year, monthStart + i, Firstweekday);
                    allWeeks.Add(weeks);
                    if (weeks.Count > maxWeeks) maxWeeks = weeks.Count;
                }

                // Print weeks side by side
                for (int weekIdx = 0; weekIdx < maxWeeks; weekIdx++)
                {
                    var rowParts = new SysListStr();
                    for (int mi = 0; mi < allWeeks.Count; mi++)
                    {
                        if (weekIdx < allWeeks[mi].Count)
                        {
                            var weekSb = new StringBuilder();
                            var week = allWeeks[mi][weekIdx];
                            for (int d = 0; d < week.Count; d++)
                            {
                                if (d > 0) weekSb.Append(' ');
                                if (week[d] == 0)
                                    weekSb.Append(new string(' ', w));
                                else
                                    weekSb.Append(week[d].ToString().PadLeft(w));
                            }
                            rowParts.Add(weekSb.ToString().PadRight(colWidth));
                        }
                        else
                        {
                            rowParts.Add(new string(' ', colWidth));
                        }
                    }
                    sb.AppendLine(string.Join(new string(' ', c), rowParts));
                    for (int i = 1; i < l; i++)
                        sb.AppendLine();
                }
            }

            return sb.ToString();
        }

        private void AppendDayHeader(StringBuilder sb, int w)
        {
            string[] dayNames = { "Mo", "Tu", "We", "Th", "Fr", "Sa", "Su" };
            for (int i = 0; i < 7; i++)
            {
                if (i > 0) sb.Append(' ');
                int idx = (Firstweekday + i) % 7;
                string name = dayNames[idx];
                if (w > 2)
                    name = name.PadLeft(w);
                else
                    name = name.Substring(0, System.Math.Min(w, name.Length)).PadLeft(w);
                sb.Append(name);
            }
        }
    }
}
