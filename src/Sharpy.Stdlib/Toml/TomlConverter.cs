using System;
using System.Collections.Generic;
using Tomlyn;
using Tomlyn.Model;

namespace Sharpy
{
    /// <summary>
    /// Converts between Tomlyn TOML model types and Sharpy runtime types.
    /// </summary>
    internal static class TomlConverter
    {
        /// <summary>
        /// Convert a Tomlyn <see cref="TomlTable"/> to a Sharpy Dict.
        /// </summary>
        internal static Dict<string, object?> ToSharpy(TomlTable table)
        {
            var dict = new Dict<string, object?>();
            foreach (var kv in table)
            {
                dict[kv.Key] = ConvertValue(kv.Value);
            }
            return dict;
        }

        private static object? ConvertValue(object? value)
        {
            if (value == null)
            {
                return null;
            }

            if (value is TomlTable nestedTable)
            {
                return ToSharpy(nestedTable);
            }

            if (value is TomlTableArray tableArray)
            {
                var list = new List<object?>();
                foreach (TomlTable item in tableArray)
                {
                    list.Append(ToSharpy(item));
                }
                return list;
            }

            if (value is TomlArray array)
            {
                var list = new List<object?>();
                foreach (var item in array)
                {
                    list.Append(ConvertValue(item));
                }
                return list;
            }

            if (value is TomlDateTime tomlDt)
            {
                return ConvertDateTime(tomlDt);
            }

            // string, long, double, bool pass through
            return value;
        }

        private static object ConvertDateTime(TomlDateTime tomlDt)
        {
            switch (tomlDt.Kind)
            {
                case TomlDateTimeKind.OffsetDateTimeByZ:
                case TomlDateTimeKind.OffsetDateTimeByNumber:
                    {
                        var dto = tomlDt.DateTime;
                        var offset = dto.Offset;
                        var tz = new Timezone(new Timedelta(hours: offset.Hours, minutes: offset.Minutes));
                        return new Sharpy.DateTime(dto.DateTime, tz);
                    }
                case TomlDateTimeKind.LocalDateTime:
                    return new Sharpy.DateTime(tomlDt.DateTime.DateTime);
                case TomlDateTimeKind.LocalDate:
                    return new Date(tomlDt.DateTime.DateTime);
                case TomlDateTimeKind.LocalTime:
                    return new Time(tomlDt.DateTime.DateTime.TimeOfDay);
                default:
                    {
                        var dto = tomlDt.DateTime;
                        var offset = dto.Offset;
                        var tz = new Timezone(new Timedelta(hours: offset.Hours, minutes: offset.Minutes));
                        return new Sharpy.DateTime(dto.DateTime, tz);
                    }
            }
        }

        /// <summary>
        /// Convert a Sharpy Dict to a Tomlyn <see cref="TomlTable"/> for serialization.
        /// </summary>
        internal static TomlTable ToTomlyn(object? obj)
        {
            if (obj is Dict<string, object?> dict)
            {
                return DictToTable(dict);
            }

            throw new TypeError("toml.dumps() requires a dict, not " + (obj == null ? "NoneType" : obj.GetType().Name));
        }

        private static TomlTable DictToTable(Dict<string, object?> dict)
        {
            var table = new TomlTable();
            foreach (string key in dict)
            {
                table[key] = ConvertToTomlyn(dict[key])!;
            }
            return table;
        }

        private static object? ConvertToTomlyn(object? value)
        {
            if (value == null)
            {
                return null;
            }

            if (value is Dict<string, object?> dict)
            {
                return DictToTable(dict);
            }

            if (value is List<object?> list)
            {
                var arr = new TomlArray();
                foreach (var item in list)
                {
                    arr.Add(ConvertToTomlyn(item));
                }
                return arr;
            }

            if (value is int intVal)
            {
                return (long)intVal;
            }

            if (value is float floatVal)
            {
                return (double)floatVal;
            }

            if (value is Sharpy.DateTime sharpyDt)
            {
                var sdt = sharpyDt.InternalDateTime;
                var tzinfo = sharpyDt.Tzinfo;
                if (tzinfo != null)
                {
                    var td = tzinfo.Utcoffset();
                    var offset = td.InternalTimeSpan;
                    var dto = new System.DateTimeOffset(sdt, offset);
                    return new TomlDateTime(dto, 0, offset == System.TimeSpan.Zero ? TomlDateTimeKind.OffsetDateTimeByZ : TomlDateTimeKind.OffsetDateTimeByNumber);
                }
                return new TomlDateTime(new System.DateTimeOffset(sdt), 0, TomlDateTimeKind.LocalDateTime);
            }

            if (value is Date sharpyDate)
            {
                var sdt = sharpyDate.InternalDate;
                return new TomlDateTime(new System.DateTimeOffset(sdt), 0, TomlDateTimeKind.LocalDate);
            }

            if (value is Time sharpyTime)
            {
                var dtForTime = new System.DateTimeOffset(new System.DateTime(1, 1, 1, sharpyTime.Hour, sharpyTime.Minute, sharpyTime.Second).AddTicks(sharpyTime.Microsecond * 10L), System.TimeSpan.Zero);
                return new TomlDateTime(dtForTime, 0, TomlDateTimeKind.LocalTime);
            }

            // string, long, double, bool pass through to Tomlyn
            return value;
        }
    }
}
