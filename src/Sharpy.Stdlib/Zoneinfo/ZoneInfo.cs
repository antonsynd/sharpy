#nullable enable

using System;

namespace Sharpy
{
    [SharpyModuleType("zoneinfo", "ZoneInfo")]
    public sealed class ZoneInfo : ITzinfo, IEquatable<ZoneInfo>
    {
        private readonly TimeZoneInfo _tz;

        public ZoneInfo(string key)
        {
            try
            {
                _tz = TimeZoneInfo.FindSystemTimeZoneById(key);
            }
            catch (TimeZoneNotFoundException)
            {
                throw new ZoneInfoNotFoundError($"'No time zone found with key {key}'");
            }
        }

        public string Key => _tz.Id;

        public Timedelta Utcoffset(DateTime? dt = null)
        {
            TimeSpan offset;
            if (dt == null)
            {
                offset = _tz.BaseUtcOffset;
            }
            else
            {
                offset = _tz.GetUtcOffset(dt.InternalDateTime);
            }
            return TimespanToTimedelta(offset);
        }

        public string Tzname(DateTime? dt = null)
        {
            if (dt == null)
                return Key;
            return _tz.IsDaylightSavingTime(dt.InternalDateTime) ? _tz.DaylightName : _tz.StandardName;
        }

        public Timedelta Dst(DateTime? dt = null)
        {
            if (dt == null)
                return new Timedelta();
            if (_tz.IsDaylightSavingTime(dt.InternalDateTime))
            {
                var baseOffset = _tz.BaseUtcOffset;
                var actualOffset = _tz.GetUtcOffset(dt.InternalDateTime);
                return TimespanToTimedelta(actualOffset - baseOffset);
            }
            return new Timedelta();
        }

        public override string ToString() => Key;

        public bool Equals(ZoneInfo? other)
        {
            if (other is null) return false;
            return Key == other.Key;
        }

        public override bool Equals(object? obj) => Equals(obj as ZoneInfo);

        public override int GetHashCode() => Key.GetHashCode();

        private static Timedelta TimespanToTimedelta(TimeSpan ts)
        {
            return new Timedelta(days: ts.Days, hours: ts.Hours, minutes: ts.Minutes, seconds: ts.Seconds);
        }
    }
}
