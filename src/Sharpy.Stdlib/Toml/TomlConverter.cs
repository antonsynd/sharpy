using System;
using System.Collections.Generic;
using Tomlyn;
using Tomlyn.Model;

namespace Sharpy
{
    internal static class TomlConverter
    {
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
                    return tomlDt.DateTime;
                case TomlDateTimeKind.LocalDateTime:
                    return tomlDt.DateTime.DateTime;
                case TomlDateTimeKind.LocalDate:
                    return tomlDt.DateTime.DateTime.Date;
                case TomlDateTimeKind.LocalTime:
                    return tomlDt.DateTime.DateTime.TimeOfDay;
                default:
                    return tomlDt.DateTime;
            }
        }

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

            if (value is System.DateTimeOffset dto)
            {
                return new TomlDateTime(dto, 0, dto.Offset == System.TimeSpan.Zero ? TomlDateTimeKind.OffsetDateTimeByZ : TomlDateTimeKind.OffsetDateTimeByNumber);
            }

            if (value is System.DateTime dt)
            {
                return new TomlDateTime(new System.DateTimeOffset(dt), 0, TomlDateTimeKind.LocalDateTime);
            }

            if (value is System.TimeSpan ts)
            {
                var dtForTime = new System.DateTimeOffset(new System.DateTime(1, 1, 1).Add(ts), System.TimeSpan.Zero);
                return new TomlDateTime(dtForTime, 0, TomlDateTimeKind.LocalTime);
            }

            // string, long, double, bool pass through to Tomlyn
            return value;
        }
    }
}
