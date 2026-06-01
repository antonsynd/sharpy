#nullable enable

namespace Sharpy
{
    public static partial class CalendarModule
    {
        public static readonly int MONDAY = 0;
        public static readonly int TUESDAY = 1;
        public static readonly int WEDNESDAY = 2;
        public static readonly int THURSDAY = 3;
        public static readonly int FRIDAY = 4;
        public static readonly int SATURDAY = 5;
        public static readonly int SUNDAY = 6;

        public static readonly List<string> DayName = new List<string>(new[]
        {
            "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"
        });

        public static readonly List<string> DayAbbr = new List<string>(new[]
        {
            "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"
        });

        public static readonly List<string> MonthName = new List<string>(new[]
        {
            "", "January", "February", "March", "April", "May", "June",
            "July", "August", "September", "October", "November", "December"
        });

        public static readonly List<string> MonthAbbr = new List<string>(new[]
        {
            "", "Jan", "Feb", "Mar", "Apr", "May", "Jun",
            "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"
        });

        private static int _moduleFirstweekday;
    }
}
