using System.Collections;
using System.Collections.Generic;

namespace Sharpy
{
    /// <summary>
    /// Bridges between YamlDotNet's representation of parsed/serializable data and
    /// Sharpy's Python-like collection types.
    /// </summary>
    internal static class YamlConverter
    {
        /// <summary>
        /// Convert a value produced by YamlDotNet's deserializer into Sharpy types.
        /// Mappings become <see cref="Dict{K,V}"/>, sequences become <see cref="List{T}"/>,
        /// and scalars (string, int, long, double, bool, null) pass through unchanged.
        /// </summary>
        public static object? ToSharpy(object? value)
        {
            switch (value)
            {
                case null:
                    return null;

                case string:
                    return value;

                case IDictionary mapping:
                {
                    var result = new Dict<string, object?>();
                    foreach (DictionaryEntry entry in mapping)
                    {
                        string key = ScalarToString(entry.Key);
                        result[key] = ToSharpy(entry.Value);
                    }
                    return result;
                }

                case IEnumerable sequence:
                {
                    var result = new List<object?>();
                    foreach (object? item in sequence)
                    {
                        result.Append(ToSharpy(item));
                    }
                    return result;
                }

                default:
                    return value;
            }
        }

        /// <summary>
        /// Convert a Sharpy value into a structure that YamlDotNet can serialize.
        /// <see cref="Dict{K,V}"/> becomes <c>Dictionary&lt;object, object?&gt;</c>,
        /// <see cref="List{T}"/>/<see cref="Set{T}"/>/tuples become <c>List&lt;object?&gt;</c>,
        /// and scalars pass through unchanged.
        /// </summary>
        public static object? ToYamlDotNet(object? value)
        {
            if (value is null)
            {
                return null;
            }

            if (value is string)
            {
                return value;
            }

            System.Type type = value.GetType();
            if (type.IsGenericType)
            {
                System.Type definition = type.GetGenericTypeDefinition();

                if (definition == typeof(Dict<,>) || definition == typeof(FrozenDict<,>))
                {
                    var result = new Dictionary<object, object?>();
                    foreach (object? entry in (IEnumerable)value)
                    {
                        System.Type entryType = entry!.GetType();
                        object? key = entryType.GetProperty("Key")!.GetValue(entry);
                        object? entryValue = entryType.GetProperty("Value")!.GetValue(entry);
                        result[ToYamlDotNet(key)!] = ToYamlDotNet(entryValue);
                    }
                    return result;
                }

                if (definition == typeof(List<>) || definition == typeof(Set<>))
                {
                    return EnumerableToList((IEnumerable)value);
                }
            }

            // Tuples (ValueTuple / Tuple) implement ITuple — YAML has no tuple type, emit a sequence.
            if (value is System.Runtime.CompilerServices.ITuple tuple)
            {
                var result = new List<object?>();
                for (int i = 0; i < tuple.Length; i++)
                {
                    result.Add(ToYamlDotNet(tuple[i]));
                }
                return result;
            }

            // Fall back to plain .NET collections so nested .NET data still serializes.
            if (value is IDictionary netDict)
            {
                var result = new Dictionary<object, object?>();
                foreach (DictionaryEntry entry in netDict)
                {
                    result[ToYamlDotNet(entry.Key)!] = ToYamlDotNet(entry.Value);
                }
                return result;
            }

            if (value is IEnumerable netEnumerable)
            {
                return EnumerableToList(netEnumerable);
            }

            return value;
        }

        private static List<object?> EnumerableToList(IEnumerable source)
        {
            var result = new List<object?>();
            foreach (object? item in source)
            {
                result.Add(ToYamlDotNet(item));
            }
            return result;
        }

        private static string ScalarToString(object? key)
        {
            if (key is null)
            {
                return "null";
            }

            if (key is string s)
            {
                return s;
            }

            return System.Convert.ToString(key, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
        }
    }
}
