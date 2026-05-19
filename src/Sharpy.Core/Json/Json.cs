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
        /// <param name="separators">A tuple of <c>(itemSeparator, keySeparator)</c> overriding the
        /// defaults. When <c>null</c>, defaults to <c>(", ", ": ")</c> in compact mode and
        /// <c>(",", ": ")</c>-style behavior in pretty mode (newlines drive item separation).</param>
        /// <param name="default">Optional callback invoked for any value that is not natively
        /// JSON-serializable. The callback should return a JSON-serializable replacement value, or
        /// raise a <c>TypeError</c> for unsupported types. Returning the original object will
        /// raise a <c>TypeError</c> to avoid infinite recursion. Named <c>default</c> for Python
        /// compatibility (kwarg <c>default=</c>).</param>
        /// <returns>A JSON string representation of <paramref name="obj"/>.</returns>
        public static string Dumps(
            object? obj,
            int indent = -1,
            bool sortKeys = false,
            bool ensureAscii = true,
            (string, string)? separators = null,
            Func<object, object?>? @default = null)
        {
            string? itemSep = null;
            string? keySep = null;

            if (separators.HasValue)
            {
                itemSep = separators.Value.Item1;
                keySep = separators.Value.Item2;
            }

            return JsonSerializer.Serialize(obj, indent, sortKeys, ensureAscii, itemSep, keySep, @default);
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
        /// <param name="obj">The object to serialize.</param>
        /// <param name="fp">The file to write to.</param>
        /// <param name="indent">Number of spaces for indentation. Use -1 for compact output.</param>
        /// <param name="sortKeys">Whether to sort dictionary keys.</param>
        /// <param name="ensureAscii">Whether to escape non-ASCII characters.</param>
        /// <param name="separators">A tuple of <c>(itemSeparator, keySeparator)</c> overriding the
        /// defaults. See <see cref="Dumps(object?, int, bool, bool, ValueTuple{string, string}?, Func{object, object?}?)"/>.</param>
        /// <param name="default">Optional callback invoked for any value that is not natively
        /// JSON-serializable. See <see cref="Dumps(object?, int, bool, bool, ValueTuple{string, string}?, Func{object, object?}?)"/>.</param>
        public static void Dump(
            object? obj,
            TextFile fp,
            int indent = -1,
            bool sortKeys = false,
            bool ensureAscii = true,
            (string, string)? separators = null,
            Func<object, object?>? @default = null)
        {
            if (fp == null)
            {
                throw new TypeError("expected TextFile, got NoneType");
            }

            string json = Dumps(obj, indent, sortKeys, ensureAscii, separators, @default);
            fp.Write(json);
        }

        /// <summary>
        /// Deserialize a JSON document read from a file.
        /// </summary>
        /// <param name="fp">The file to read from.</param>
        /// <returns>The deserialized object.</returns>
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
