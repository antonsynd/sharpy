using System;

namespace Sharpy
{
    /// <summary>
    /// Python-compatible json module.
    /// Provides dumps/loads for string serialization and dump/load for file I/O.
    /// </summary>
    public static partial class Json
    {
        /// <summary>
        /// Serialize obj to a JSON formatted string.
        /// </summary>
        /// <param name="obj">The object to serialize.</param>
        /// <returns>A JSON string representation of <paramref name="obj"/>.</returns>
        /// <example>
        /// <code>
        /// json.dumps({"key": "value"})    # '{"key": "value"}'
        /// json.dumps([1, 2, 3])           # '[1, 2, 3]'
        /// </code>
        /// </example>
        public static string Dumps(object? obj)
        {
            return JsonSerializer.Serialize(obj);
        }

        /// <summary>
        /// Serialize obj to a JSON formatted string with formatting options.
        /// </summary>
        /// <param name="obj">The object to serialize.</param>
        /// <param name="indent">Number of spaces for indentation. Use -1 for compact output.</param>
        /// <param name="sortKeys">Whether to sort dictionary keys.</param>
        /// <param name="ensureAscii">Whether to escape non-ASCII characters.</param>
        public static string Dumps(object? obj, int indent = -1, bool sortKeys = false, bool ensureAscii = true)
        {
            return JsonSerializer.Serialize(obj, indent, sortKeys, ensureAscii);
        }

        /// <summary>
        /// Deserialize a JSON string to a Python-like object.
        /// Returns Dict&lt;string, object?&gt; for objects, List&lt;object?&gt; for arrays,
        /// string, int/long/double, bool, or null.
        /// </summary>
        /// <param name="s">The JSON string to deserialize.</param>
        /// <returns>The deserialized object.</returns>
        /// <example>
        /// <code>
        /// json.loads('{"a": 1}')    # {"a": 1}
        /// json.loads('[1, 2]')      # [1, 2]
        /// </code>
        /// </example>
        public static object? Loads(string s)
        {
            return JsonParser.Parse(s);
        }

        /// <summary>
        /// Serialize obj as a JSON formatted stream to a file.
        /// </summary>
        /// <param name="obj">The object to serialize.</param>
        /// <param name="fp">The file to write to.</param>
        /// <example>
        /// <code>
        /// f = open("data.json", "w")
        /// json.dump({"key": "value"}, f)
        /// f.close()
        /// </code>
        /// </example>
        public static void Dump(object? obj, TextFile fp)
        {
            if (fp == null)
            {
                throw new TypeError("expected TextFile, got NoneType");
            }

            string json = Dumps(obj);
            fp.Write(json);
        }

        /// <summary>
        /// Serialize obj as a JSON formatted stream to a file with formatting options.
        /// </summary>
        public static void Dump(object? obj, TextFile fp, int indent = -1, bool sortKeys = false, bool ensureAscii = true)
        {
            if (fp == null)
            {
                throw new TypeError("expected TextFile, got NoneType");
            }

            string json = Dumps(obj, indent, sortKeys, ensureAscii);
            fp.Write(json);
        }

        /// <summary>
        /// Deserialize a JSON document read from a file.
        /// </summary>
        public static object? Load(TextFile fp)
        {
            if (fp == null)
            {
                throw new TypeError("expected TextFile, got NoneType");
            }

            string content = fp.Read();
            return Loads(content);
        }
    }
}
