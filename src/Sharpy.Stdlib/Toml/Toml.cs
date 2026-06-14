using System;
using System.IO;
using System.Reflection;
using Tomlyn;
using Tomlyn.Model;

namespace Sharpy
{
    /// <summary>Provides TOML parsing and serialization helpers.</summary>
    public static partial class Toml
    {
        /// <summary>Parse a TOML string into a dictionary.</summary>
        public static Dict<string, object?> Loads(string s)
        {
            if (s == null)
            {
                throw new TypeError("expected str, not NoneType");
            }

            try
            {
                var table = TomlSerializer.Deserialize<TomlTable>(s)!;
                return TomlConverter.ToSharpy(table);
            }
            catch (TomlException ex)
            {
                throw new TOMLDecodeError(ex.Message, s, 0);
            }
        }

        /// <summary>Serialize an object to a TOML string.</summary>
        public static string Dumps(object? obj)
        {
            return Dumps(obj, false);
        }

        /// <summary>Serialize an object to a TOML string, optionally sorting keys.</summary>
        public static string Dumps(object? obj, bool sortKeys = false)
        {
            var table = TomlConverter.ToTomlyn(obj);

            if (sortKeys)
            {
                SortTable(table);
            }

            try
            {
                return TomlSerializer.Serialize(table);
            }
            catch (TomlException ex)
            {
                throw new TOMLDecodeError(ex.Message, "", 0);
            }
        }

        /// <summary>Parse TOML content from an open text file.</summary>
        public static Dict<string, object?> Load(TextFile fp)
        {
            if (fp == null)
            {
                throw new TypeError("expected TextFile, got NoneType");
            }

            string content = fp.Read();
            return Loads(content);
        }

        /// <summary>Write an object's TOML representation to a text file.</summary>
        public static void Dump(object? obj, TextFile fp)
        {
            Dump(obj, fp, false);
        }

        /// <summary>Write an object's TOML representation to a text file, optionally sorting keys.</summary>
        public static void Dump(object? obj, TextFile fp, bool sortKeys = false)
        {
            if (fp == null)
            {
                throw new TypeError("expected TextFile, got NoneType");
            }

            string toml = Dumps(obj, sortKeys);
            fp.Write(toml);
        }

        /// <summary>Parse a TOML file from a path.</summary>
        public static Dict<string, object?> LoadFile(string path)
        {
            if (path == null)
            {
                throw new TypeError("expected str, not NoneType");
            }

            try
            {
                string content = File.ReadAllText(path);
                return Loads(content);
            }
            catch (System.IO.FileNotFoundException)
            {
                throw new FileNotFoundError("No such file or directory: '" + path + "'");
            }
            catch (System.IO.DirectoryNotFoundException)
            {
                throw new FileNotFoundError("No such file or directory: '" + path + "'");
            }
        }

        /// <summary>Write an object's TOML representation to a file path.</summary>
        public static void DumpFile(object? obj, string path)
        {
            DumpFile(obj, path, false);
        }

        /// <summary>Write an object's TOML representation to a file path, optionally sorting keys.</summary>
        public static void DumpFile(object? obj, string path, bool sortKeys = false)
        {
            if (path == null)
            {
                throw new TypeError("expected str, not NoneType");
            }

            string toml = Dumps(obj, sortKeys);
            File.WriteAllText(path, toml);
        }

        private static void SortTable(TomlTable table)
        {
            var keys = new System.Collections.Generic.List<string>(table.Keys);
            keys.Sort(StringComparer.Ordinal);

            var sorted = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, object?>>();
            foreach (var key in keys)
            {
                sorted.Add(new System.Collections.Generic.KeyValuePair<string, object?>(key, table[key]!));
            }

            table.Clear();
            foreach (var kv in sorted)
            {
                table[kv.Key] = kv.Value!;
                if (kv.Value is TomlTable nested)
                {
                    SortTable(nested);
                }
            }
        }

#if NET10_0_OR_GREATER
        /// <summary>Parse a TOML string into a typed model.</summary>
        public static Result<T, TOMLDecodeError> Loads<T>(string s) where T : class, new()
        {
            if (s == null)
            {
                throw new TypeError("expected str, not NoneType");
            }

            try
            {
                var table = TomlSerializer.Deserialize<TomlTable>(s)!;
                T instance = new T();
                BindTable(instance, table);
                return Result<T, TOMLDecodeError>.Ok(instance);
            }
            catch (TomlException ex)
            {
                return Result<T, TOMLDecodeError>.Err(new TOMLDecodeError(ex.Message, s, 0));
            }
            catch (InvalidOperationException ex)
            {
                return Result<T, TOMLDecodeError>.Err(new TOMLDecodeError(ex.Message, s, 0));
            }
        }

        private static void BindTable(object target, TomlTable table)
        {
            var type = target.GetType();
            foreach (var kv in table)
            {
                string pascalName = SnakeToPascalCase(kv.Key);

                var field = type.GetField(pascalName, BindingFlags.Public | BindingFlags.Instance)
                    ?? type.GetField(pascalName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (field != null)
                {
                    object? converted = ConvertToFieldType(kv.Value, field.FieldType);
                    field.SetValue(target, converted);
                    continue;
                }

                var prop = type.GetProperty(pascalName, BindingFlags.Public | BindingFlags.Instance)
                    ?? type.GetProperty(pascalName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (prop != null && prop.CanWrite)
                {
                    object? converted = ConvertToFieldType(kv.Value, prop.PropertyType);
                    prop.SetValue(target, converted);
                }

                // Skip keys that don't match any field or property
            }
        }

        private static object? ConvertToFieldType(object? value, Type targetType)
        {
            if (value == null)
            {
                return null;
            }

            if (value is TomlTable nestedTable)
            {
                object nested = Activator.CreateInstance(targetType)!;
                BindTable(nested, nestedTable);
                return nested;
            }

            if (value is TomlTableArray tableArray)
            {
                Type elementType = typeof(object);
                if (targetType.IsGenericType)
                {
                    elementType = targetType.GetGenericArguments()[0];
                }

                var sharpyListType = typeof(List<>).MakeGenericType(elementType);
                bool useSharpy = targetType.IsAssignableFrom(sharpyListType);
                var listType = useSharpy
                    ? sharpyListType
                    : typeof(System.Collections.Generic.List<>).MakeGenericType(elementType);
                var list = (System.Collections.IList)Activator.CreateInstance(listType)!;
                foreach (TomlTable item in tableArray)
                {
                    object element = Activator.CreateInstance(elementType)!;
                    BindTable(element, item);
                    list.Add(element);
                }
                return list;
            }

            if (value is TomlDateTime)
            {
                return TomlConverter.ConvertValue(value);
            }

            // Handle numeric conversions: TOML integers are long, but fields may be int
            if (value is long longVal && targetType == typeof(int))
            {
                return (int)longVal;
            }

            if (targetType.IsAssignableFrom(value.GetType()))
            {
                return value;
            }

            // Fallback for other numeric conversions
            try
            {
                return Convert.ChangeType(value, targetType);
            }
            catch (Exception)
            {
                throw new InvalidOperationException(
                    $"Cannot convert TOML value of type '{value.GetType().Name}' to field type '{targetType.Name}'");
            }
        }

        private static string SnakeToPascalCase(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return name;
            }

            var sb = new System.Text.StringBuilder(name.Length);
            bool capitalizeNext = true;
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (c == '_')
                {
                    capitalizeNext = true;
                    continue;
                }

                if (capitalizeNext)
                {
                    sb.Append(char.ToUpperInvariant(c));
                    capitalizeNext = false;
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        /// <summary>Parse TOML content from a text file into a typed model.</summary>
        public static Result<T, TOMLDecodeError> Load<T>(TextFile fp) where T : class, new()
        {
            if (fp == null)
            {
                throw new TypeError("expected TextFile, got NoneType");
            }

            string content = fp.Read();
            return Loads<T>(content);
        }
#endif
    }
}
