using System;
using System.IO;
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
                var options = new TomlSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                T model = TomlSerializer.Deserialize<T>(s, options)!;
                return Result<T, TOMLDecodeError>.Ok(model);
            }
            catch (TomlException ex)
            {
                return Result<T, TOMLDecodeError>.Err(new TOMLDecodeError(ex.Message, s, 0));
            }
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
