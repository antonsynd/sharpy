// Portions of this file are a C# port of CPython's Modules/_datetimemodule.c (CPython 3.15):
// the entry points date_fromisoformat and datetime_fromisoformat (the length check, the multi-byte
// separator skip, the 24:00 rollover and its message), _sanitize_isoformat_str (the separator-surrogate
// replacement), _find_isoformat_datetime_separator, parse_isoformat_date, parse_isoformat_time,
// parse_hh_mm_ss_ff and iso_to_ymd, and the helpers parse_digits and days_in_month (ymd_to_ord's
// result is reproduced with System.DateTime).
// Copyright (c) 2001 Python Software Foundation; All Rights Reserved
// Licensed under the PSF License Version 2; see THIRD-PARTY-NOTICES.txt at the repository root.
// Changes: translated from C to C#; parses a byte array holding the string's UTF-8 encoding (a position
// past the end reads as NUL, standing in for the C terminator), raises Sharpy's ValueError, and builds
// Sharpy's date, datetime and timezone objects.

using System;
using System.Globalization;

namespace Sharpy
{
    /// <summary>
    /// <c>date.fromisoformat</c> and <c>datetime.fromisoformat</c> as CPython 3.15 reads them
    /// (<c>Modules/_datetimemodule.c</c>: <c>parse_isoformat_date</c>, <c>parse_isoformat_time</c>,
    /// <c>parse_hh_mm_ss_ff</c>, <c>_find_isoformat_datetime_separator</c>), ported step for step so the same
    /// strings are accepted and refused: calendar and ISO week dates, extended and basic forms, any one-character
    /// date/time separator, <c>HH[:MM[:SS[.f…]]]</c> with a comma or dot fraction, <c>Z</c> or a
    /// <c>±HH[:MM[:SS[.f…]]]</c> offset kept as a <see cref="Timezone"/>, and <c>24:00</c> as the next midnight.
    /// Nothing reads the clock, the host time zone or the culture. Like the C code, it reads the string's UTF-8 bytes
    /// (lengths and positions are byte counts, a multi-byte separator is skipped whole), and a position past the end
    /// reads as NUL, as the C code reads its terminator.
    /// </summary>
    internal static class DatetimeIso
    {
        private static readonly int[] FractionCorrection = { 100000, 10000, 1000, 100, 10 };
        private static readonly int[] DaysInMonthTable = { 0, 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 };

        internal static Date ParseDate(string text)
        {
            var bytes = Utf8(text, separatorSurrogateTo: -1);
            int length = bytes?.Length ?? 0;
            if (bytes is not null && (length == 7 || length == 8 || length == 10)
                && ParseDatePart(bytes, length, out int year, out int month, out int day) == 0)
            {
                return new Date(year, month, day);
            }
            throw Invalid(text);
        }

        internal static DateTime ParseDateTime(string text)
        {
            if (text.Length < 7)
                throw Invalid(text);
            var s = Utf8(text, separatorSurrogateTo: 'T');
            if (s is null)
                throw Invalid(text);
            int separator = FindSeparator(s);
            if (separator < 0)
                throw Invalid(text);

            int rv = ParseDatePart(s, separator, out int year, out int month, out int day);
            int hour = 0, minute = 0, second = 0, microsecond = 0, tzSeconds = 0, tzMicroseconds = 0;
            if (rv == 0 && s.Length > separator)
            {
                int start = separator + 1;
                if (s[separator] >= 0x80)
                {
                    while (start < s.Length && (s[start] & 0xC0) == 0x80)
                        start++;
                }
                rv = ParseTime(s, start, out hour, out minute, out second, out microsecond, out tzSeconds, out tzMicroseconds);
            }
            if (rv < 0)
                throw Invalid(text);

            ITzinfo? tzinfo = null;
            if (rv == 1)
            {
                tzinfo = tzSeconds == 0 && tzMicroseconds == 0
                    ? Timezone.Utc
                    : new Timezone(new Timedelta(seconds: tzSeconds, microseconds: tzMicroseconds));
            }

            if (hour == 24 && month <= 12)
            {
                int inMonth = DaysInMonth(year, month);
                if (day <= inMonth)
                {
                    if (minute != 0 || second != 0 || microsecond != 0)
                        throw new ValueError("minute, second, and microsecond must be 0 when hour is 24");
                    hour = 0;
                    day += 1;
                    if (day > inMonth)
                    {
                        day = 1;
                        month += 1;
                        if (month > 12)
                        {
                            month = 1;
                            year += 1;
                        }
                    }
                }
            }
            return new DateTime(year, month, day, hour, minute, second, microsecond, tzinfo);
        }

        private static ValueError Invalid(string text) => new ValueError("Invalid isoformat string: " + Builtins.Repr(text));

        /// <summary>
        /// The UTF-8 bytes the C code parses, or null where it cannot encode the text (a lone surrogate). For
        /// <c>datetime</c> a surrogate at a separator position (7, 8 or 10) is first replaced, as CPython's
        /// <c>_sanitize_isoformat_str</c> does.
        /// </summary>
        private static byte[]? Utf8(string text, int separatorSurrogateTo)
        {
            char[]? chars = null;
            for (int i = 0; i < text.Length; i++)
            {
                if (!char.IsSurrogate(text[i]))
                    continue;
                if (i + 1 < text.Length && char.IsHighSurrogate(text[i]) && char.IsLowSurrogate(text[i + 1]))
                {
                    i++;
                    continue;
                }
                if (separatorSurrogateTo >= 0 && (i == 7 || i == 8 || i == 10))
                {
                    chars ??= text.ToCharArray();
                    chars[i] = (char)separatorSurrogateTo;
                    continue;
                }
                return null;
            }
            return chars is null ? System.Text.Encoding.UTF8.GetBytes(text) : System.Text.Encoding.UTF8.GetBytes(chars);
        }

        private static char At(byte[] s, int i) => i >= 0 && i < s.Length ? (char)s[i] : '\0';

        private static bool IsDigit(char c) => c >= '0' && c <= '9';

        /// <summary>Read <paramref name="count"/> ASCII digits at <paramref name="p"/>; the next position, or -1.</summary>
        private static int ParseDigits(byte[] s, int p, int count, out int value)
        {
            value = 0;
            for (int i = 0; i < count; i++)
            {
                char c = At(s, p + i);
                if (!IsDigit(c))
                    return -1;
                value = value * 10 + (c - '0');
            }
            return p + count;
        }

        /// <summary>CPython's <c>parse_isoformat_date</c>: 0 on success, negative on a malformed date.</summary>
        private static int ParseDatePart(byte[] s, int length, out int year, out int month, out int day)
        {
            month = day = 0;
            int p = ParseDigits(s, 0, 4, out year);
            if (p < 0)
                return -1;
            bool separator = At(s, p) == '-';
            if (separator)
                p++;

            if (At(s, p) == 'W')
            {
                p = ParseDigits(s, p + 1, 2, out int week);
                if (p < 0)
                    return -3;
                int isoDay = 1;
                if (p < length)
                {
                    if (separator && At(s, p++) != '-')
                        return -2;
                    p = ParseDigits(s, p, 1, out isoDay);
                    if (p < 0)
                        return -4;
                }
                return IsoToYmd(year, week, isoDay, out year, out month, out day);
            }

            p = ParseDigits(s, p, 2, out month);
            if (p < 0)
                return -1;
            if (separator && At(s, p++) != '-')
                return -2;
            p = ParseDigits(s, p, 2, out day);
            return p < 0 ? -1 : 0;
        }

        /// <summary>CPython's <c>iso_to_ymd</c>; a result past 9999-12-31 is left for the constructor to refuse.</summary>
        private static int IsoToYmd(int isoYear, int week, int isoDay, out int year, out int month, out int day)
        {
            year = month = day = 0;
            if (isoYear < DatetimeArgs.MinYear || isoYear > DatetimeArgs.MaxYear)
                return -4;
            if (week <= 0 || week >= 53)
            {
                bool outOfRange = true;
                if (week == 53)
                {
                    long firstWeekday = Ordinal(isoYear, 1, 1) % 7;
                    if (firstWeekday == 4 || (firstWeekday == 3 && System.DateTime.IsLeapYear(isoYear)))
                        outOfRange = false;
                }
                if (outOfRange)
                    return -2;
            }
            if (isoDay <= 0 || isoDay >= 8)
                return -3;

            long first = Ordinal(isoYear, 1, 1);
            long firstWeekdayFromMonday = (first + 6) % 7;
            long week1Monday = first - firstWeekdayFromMonday;
            if (firstWeekdayFromMonday > 3)
                week1Monday += 7;
            long ordinal = week1Monday + (week - 1) * 7 + isoDay - 1;
            if (ordinal > DatetimeArgs.MaxOrdinal)
            {
                year = DatetimeArgs.MaxYear + 1;
                month = 1;
                day = 1;
                return 0;
            }
            var date = new System.DateTime((ordinal - 1) * TimeSpan.TicksPerDay);
            year = date.Year;
            month = date.Month;
            day = date.Day;
            return 0;
        }

        private static long Ordinal(int year, int month, int day)
            => new System.DateTime(year, month, day).Ticks / TimeSpan.TicksPerDay + 1;

        private static int DaysInMonth(int year, int month)
        {
            if (month == 2 && year % 4 == 0 && (year % 100 != 0 || year % 400 == 0))
                return 29;
            return month >= 0 && month <= 12 ? DaysInMonthTable[month] : 0;
        }

        /// <summary>CPython's <c>_find_isoformat_datetime_separator</c>: where the date part ends, or -1.</summary>
        private static int FindSeparator(byte[] s)
        {
            int length = s.Length;
            if (length == 7)
                return 7;
            if (At(s, 4) == '-')
            {
                if (At(s, 5) != 'W')
                    return 10;
                if (length < 8)
                    return -1;
                if (length > 8 && At(s, 8) == '-')
                {
                    if (length == 9)
                        return -1;
                    if (length > 10 && IsDigit(At(s, 10)))
                        return 8;
                    return 10;
                }
                return 8;
            }
            if (At(s, 4) == 'W')
            {
                int index = 7;
                while (index < length && IsDigit((char)s[index]))
                    index++;
                if (index < 9)
                    return index;
                return index % 2 == 0 ? 7 : 8;
            }
            return 8;
        }

        /// <summary>CPython's <c>parse_hh_mm_ss_ff</c>: 0 at the end, 1 with text left, negative on error.</summary>
        private static int ParseHhMmSsFf(byte[] s, int p, int end, out int hour, out int minute, out int second, out int microsecond)
        {
            hour = minute = second = microsecond = 0;
            bool hasSeparator = true;
            for (int i = 0; i < 3; i++)
            {
                p = ParseDigits(s, p, 2, out int value);
                if (p < 0)
                    return -3;
                if (i == 0)
                    hour = value;
                else if (i == 1)
                    minute = value;
                else
                    second = value;

                char c = At(s, p++);
                if (i == 0)
                    hasSeparator = c == ':';
                if (p >= end)
                {
                    // 3.15 refuses a decimal mark with no digits after it (12:30:45. and 12:30:45.Z alike).
                    if (c == '.' || c == ',')
                        return -3;
                    return c != '\0' ? 1 : 0;
                }
                if (hasSeparator && c == ':')
                {
                    if (i == 2)
                        return -4;
                    continue;
                }
                if (c == '.' || c == ',')
                {
                    if (i < 2)
                        return -3;
                    break;
                }
                if (!hasSeparator)
                {
                    p--;
                    continue;
                }
                return -4;
            }

            int remaining = end - p;
            int toParse = remaining >= 6 ? 6 : remaining;
            if (toParse <= 0)
                return -3;
            p = ParseDigits(s, p, toParse, out microsecond);
            if (p < 0)
                return -3;
            if (toParse < 6)
                microsecond *= FractionCorrection[toParse - 1];
            while (IsDigit(At(s, p)))
                p++;
            return At(s, p) != '\0' ? 1 : 0;
        }

        /// <summary>CPython's <c>parse_isoformat_time</c>: 0 naive, 1 with an offset, negative when malformed.</summary>
        private static int ParseTime(byte[] s, int start, out int hour, out int minute, out int second, out int microsecond,
            out int tzSeconds, out int tzMicroseconds)
        {
            tzSeconds = tzMicroseconds = 0;
            int end = s.Length;
            int tzPos = start;
            do
            {
                char c = At(s, tzPos);
                if (c == 'Z' || c == '+' || c == '-')
                    break;
            }
            while (++tzPos < end);

            int rv = ParseHhMmSsFf(s, start, tzPos, out hour, out minute, out second, out microsecond);
            if (rv < 0)
                return rv;
            if (tzPos == end)
                return rv == 1 ? -5 : 0;

            if (At(s, tzPos) == 'Z')
                return At(s, tzPos + 1) != '\0' ? -5 : 1;

            int sign = At(s, tzPos) == '-' ? -1 : 1;
            rv = ParseHhMmSsFf(s, tzPos + 1, end, out int tzHour, out int tzMinute, out int tzSecond, out int tzMicrosecond);
            DatetimeArgs.CheckTime(tzHour, tzMinute, tzSecond, tzMicrosecond);
            tzSeconds = sign * (tzHour * 3600 + tzMinute * 60 + tzSecond);
            tzMicroseconds = sign * tzMicrosecond;
            return rv != 0 ? -5 : 1;
        }
    }
}
