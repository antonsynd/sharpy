using System;

namespace Sharpy
{
    public static partial class Json
    {
        /// <summary>
        /// Serialize obj to a JSON formatted string.
        /// </summary>
        public static string Dumps(object? obj)
        {
            return JsonSerializer.Serialize(obj);
        }

        /// <summary>
        /// Serialize obj to a JSON formatted string with options.
        /// </summary>
        public static string Dumps(object? obj, int? indent = null, bool sortKeys = false, bool ensureAscii = false)
        {
            return JsonSerializer.Serialize(obj, indent, sortKeys, ensureAscii);
        }

        /// <summary>
        /// Deserialize a JSON string to a Python-like object.
        /// </summary>
        public static object? Loads(string s)
        {
            if (s == null)
                throw new TypeError("the JSON object must be str, not None");
            return JsonParser.Parse(s);
        }

        /// <summary>
        /// Serialize obj as a JSON string and write it to the file.
        /// </summary>
        public static void Dump(object? obj, TextFile file)
        {
            file.Write(Dumps(obj));
        }

        /// <summary>
        /// Serialize obj as a JSON formatted string and write it to the file.
        /// </summary>
        public static void Dump(object? obj, TextFile file, int? indent = null, bool sortKeys = false, bool ensureAscii = false)
        {
            file.Write(Dumps(obj, indent, sortKeys, ensureAscii));
        }

        /// <summary>
        /// Read the file and deserialize its JSON content.
        /// </summary>
        public static object? Load(TextFile file)
        {
            return Loads(file.Read());
        }
    }
}
