#nullable enable

using System;

namespace Sharpy
{
    public static partial class Zoneinfo
    {
        public static Set<string> AvailableTimezones()
        {
            var result = new Set<string>();
            foreach (var tz in TimeZoneInfo.GetSystemTimeZones())
            {
                result.Add(tz.Id);
            }
            return result;
        }
    }
}
