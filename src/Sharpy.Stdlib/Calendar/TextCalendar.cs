#nullable enable

using System;
using System.Globalization;
using System.Text;
using SCG = System.Collections.Generic;

namespace Sharpy
{
    [SharpyModuleType("calendar", "TextCalendar")]
    public class TextCalendar : Calendar
    {
        public TextCalendar(int firstweekday = 0) : base(firstweekday)
        {
        }

        public string Formatmonth(int year, int month, int w = 2, int l = 1)
        {
            var sb = new StringBuilder();

            string monthName = CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(month);
            string header = $"{monthName} {year}";
            int totalWidth = 7 * (w + 1) - 1;
            sb.AppendLine(header.PadLeft((totalWidth + header.Length) / 2).PadRight(totalWidth));

            AppendDayHeader(sb, w);
            sb.AppendLine();

            var weeks = MonthdayscalendarRaw(year, month);
            for (int wi = 0; wi < weeks.Count; wi++)
            {
                var week = weeks[wi];
                for (int i = 0; i < week.Count; i++)
                {
                    if (i > 0)
                        sb.Append(' ');
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

        public void Prmonth(int year, int month, int w = 2, int l = 1)
        {
            Console.Write(Formatmonth(year, month, w, l));
        }

        public string Formatyear(int year, int w = 2, int l = 1, int c = 6, int m = 3)
        {
            var sb = new StringBuilder();

            string yearHeader = year.ToString();
            int colWidth = 7 * (w + 1) - 1;
            int totalWidth = m * colWidth + (m - 1) * c;
            sb.AppendLine(yearHeader.PadLeft((totalWidth + yearHeader.Length) / 2).PadRight(totalWidth));
            sb.AppendLine();

            for (int monthStart = 1; monthStart <= 12; monthStart += m)
            {
                var headers = new SCG.List<string>();
                for (int i = 0; i < m && monthStart + i <= 12; i++)
                {
                    string name = CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(monthStart + i);
                    headers.Add(name.PadLeft((colWidth + name.Length) / 2).PadRight(colWidth));
                }
                sb.AppendLine(string.Join(new string(' ', c), headers));

                var dayHeaders = new SCG.List<string>();
                for (int i = 0; i < m && monthStart + i <= 12; i++)
                {
                    var dsb = new StringBuilder();
                    AppendDayHeader(dsb, w);
                    dayHeaders.Add(dsb.ToString().PadRight(colWidth));
                }
                sb.AppendLine(string.Join(new string(' ', c), dayHeaders));

                var allWeeks = new SCG.List<SCG.List<SCG.List<int>>>();
                int maxWeeks = 0;
                for (int i = 0; i < m && monthStart + i <= 12; i++)
                {
                    var weeks = MonthdayscalendarRaw(year, monthStart + i);
                    allWeeks.Add(weeks);
                    if (weeks.Count > maxWeeks)
                        maxWeeks = weeks.Count;
                }

                for (int weekIdx = 0; weekIdx < maxWeeks; weekIdx++)
                {
                    var rowParts = new SCG.List<string>();
                    for (int mi = 0; mi < allWeeks.Count; mi++)
                    {
                        if (weekIdx < allWeeks[mi].Count)
                        {
                            var weekSb = new StringBuilder();
                            var week = allWeeks[mi][weekIdx];
                            for (int d = 0; d < week.Count; d++)
                            {
                                if (d > 0)
                                    weekSb.Append(' ');
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

        public void Pryear(int year, int w = 2, int l = 1, int c = 6, int m = 3)
        {
            Console.Write(Formatyear(year, w, l, c, m));
        }

        private void AppendDayHeader(StringBuilder sb, int w)
        {
            string[] dayNames = { "Mo", "Tu", "We", "Th", "Fr", "Sa", "Su" };
            for (int i = 0; i < 7; i++)
            {
                if (i > 0)
                    sb.Append(' ');
                int idx = (Firstweekday + i) % 7;
                string name = dayNames[idx];
                if (w > 2)
                    name = name.PadLeft(w);
                else
                    name = name.Substring(0, Math.Min(w, name.Length)).PadLeft(w);
                sb.Append(name);
            }
        }
    }
}
