using System;
using System.Globalization;
using System.Text;

namespace Sharpy
{
    /// <summary>
    /// A calendar that produces HTML table output, similar to Python's calendar.HTMLCalendar.
    /// </summary>
    [SharpyModuleType("calendar", "HTMLCalendar")]
    public class HTMLCalendar
    {
        /// <summary>The first day of the week (0=Monday, 6=Sunday).</summary>
        public int Firstweekday { get; set; }

        /// <summary>Create an HTMLCalendar with the given first day of the week.</summary>
        public HTMLCalendar(int firstweekday = 0)
        {
            Firstweekday = firstweekday;
        }

        /// <summary>
        /// Return a formatted month as an HTML table.
        /// </summary>
        public string Formatmonth(int year, int month, bool withyear = true)
        {
            var sb = new StringBuilder();
            string[] cssClasses = { "mon", "tue", "wed", "thu", "fri", "sat", "sun" };

            sb.AppendLine("<table border=\"0\" cellpadding=\"0\" cellspacing=\"0\" class=\"month\">");

            // Header
            string monthName = CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(month);
            string header = withyear ? $"{monthName} {year}" : monthName;
            sb.AppendLine($"<tr><th colspan=\"7\" class=\"month\">{header}</th></tr>");

            // Day name header row
            sb.Append("<tr>");
            for (int i = 0; i < 7; i++)
            {
                int idx = (Firstweekday + i) % 7;
                string dayName = CultureInfo.InvariantCulture.DateTimeFormat.GetAbbreviatedDayName(IsoDayToDotNet(idx));
                sb.Append($"<th class=\"{cssClasses[idx]}\">{dayName}</th>");
            }
            sb.AppendLine("</tr>");

            // Weeks
            var weeks = CalendarModule.Monthcalendar(year, month, Firstweekday);
            foreach (var week in weeks)
            {
                sb.Append("<tr>");
                for (int i = 0; i < week.Count; i++)
                {
                    int idx = (Firstweekday + i) % 7;
                    string cls = cssClasses[idx];
                    if (week[i] == 0)
                        sb.Append($"<td class=\"noday\">&nbsp;</td>");
                    else
                        sb.Append($"<td class=\"{cls}\">{week[i]}</td>");
                }
                sb.AppendLine("</tr>");
            }

            sb.AppendLine("</table>");
            return sb.ToString();
        }

        /// <summary>
        /// Return a formatted year as an HTML table of tables.
        /// </summary>
        public string Formatyear(int year, int width = 3)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<table border=\"0\" cellpadding=\"0\" cellspacing=\"0\" class=\"year\">");
            sb.AppendLine($"<tr><th colspan=\"{width}\" class=\"year\">{year}</th></tr>");

            for (int monthStart = 1; monthStart <= 12; monthStart += width)
            {
                sb.Append("<tr>");
                for (int i = 0; i < width && monthStart + i <= 12; i++)
                {
                    sb.Append("<td>");
                    sb.Append(Formatmonth(year, monthStart + i, false));
                    sb.Append("</td>");
                }
                sb.AppendLine("</tr>");
            }

            sb.AppendLine("</table>");
            return sb.ToString();
        }

        private static DayOfWeek IsoDayToDotNet(int isoDay)
        {
            // 0=Monday -> DayOfWeek.Monday, ... 6=Sunday -> DayOfWeek.Sunday
            return isoDay == 6 ? DayOfWeek.Sunday : (DayOfWeek)(isoDay + 1);
        }
    }
}
