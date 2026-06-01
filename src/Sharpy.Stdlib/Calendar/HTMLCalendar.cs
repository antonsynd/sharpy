#nullable enable

using System;
using System.Globalization;
using System.Text;

namespace Sharpy
{
    [SharpyModuleType("calendar", "HTMLCalendar")]
    public class HTMLCalendar : Calendar
    {
        private static readonly string[] CssClasses = { "mon", "tue", "wed", "thu", "fri", "sat", "sun" };

        public HTMLCalendar(int firstweekday = 0) : base(firstweekday)
        {
        }

        public string Formatmonth(int year, int month, bool withyear = true)
        {
            var sb = new StringBuilder();

            sb.AppendLine("<table border=\"0\" cellpadding=\"0\" cellspacing=\"0\" class=\"month\">");

            string monthName = CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(month);
            string header = withyear ? $"{monthName} {year}" : monthName;
            sb.AppendLine($"<tr><th colspan=\"7\" class=\"month\">{header}</th></tr>");

            sb.Append("<tr>");
            for (int i = 0; i < 7; i++)
            {
                int idx = (Firstweekday + i) % 7;
                string dayName = CultureInfo.InvariantCulture.DateTimeFormat.GetAbbreviatedDayName(Calendar.IsoDayToDotNet(idx));
                sb.Append($"<th class=\"{CssClasses[idx]}\">{dayName}</th>");
            }
            sb.AppendLine("</tr>");

            var weeks = MonthdayscalendarRaw(year, month);
            for (int wi = 0; wi < weeks.Count; wi++)
            {
                var week = weeks[wi];
                sb.Append("<tr>");
                for (int i = 0; i < week.Count; i++)
                {
                    int idx = (Firstweekday + i) % 7;
                    if (week[i] == 0)
                        sb.Append("<td class=\"noday\">&nbsp;</td>");
                    else
                        sb.Append($"<td class=\"{CssClasses[idx]}\">{week[i]}</td>");
                }
                sb.AppendLine("</tr>");
            }

            sb.AppendLine("</table>");
            return sb.ToString();
        }

        public string Formatyear(int year, int width = 3)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"<table border=\"0\" cellpadding=\"0\" cellspacing=\"0\" class=\"year\">");
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

        public string Formatyearpage(int year, int width = 3, string? css = null, string? encoding = null)
        {
            string enc = encoding ?? "utf-8";
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine($"<html><head><meta charset=\"{enc}\">");
            if (css != null)
                sb.AppendLine($"<link rel=\"stylesheet\" type=\"text/css\" href=\"{css}\">");
            sb.AppendLine($"<title>Calendar for {year}</title></head><body>");
            sb.Append(Formatyear(year, width));
            sb.AppendLine("</body></html>");
            return sb.ToString();
        }
    }
}
