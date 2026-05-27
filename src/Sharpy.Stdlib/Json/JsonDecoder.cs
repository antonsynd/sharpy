using System;

namespace Sharpy
{
    /// <summary>Simple JSON decoder. Subclass this to customize JSON decoding behavior.</summary>
    [SharpyModuleType("json")]
    public class JSONDecoder
    {
        private readonly Func<Dict<string, object?>, object?>? _objectHook;

        /// <summary>Create a new JSON decoder.</summary>
        /// <param name="objectHook">Optional callback invoked for every decoded JSON object (dict).</param>
        public JSONDecoder(Func<Dict<string, object?>, object?>? objectHook = null)
        {
            _objectHook = objectHook;
        }

        /// <summary>Deserialize a JSON string to a Python-like object.</summary>
        /// <param name="s">The JSON string to deserialize.</param>
        /// <returns>The deserialized object.</returns>
        public virtual object? Decode(string s)
        {
            return JsonParser.Parse(s, _objectHook);
        }

        /// <summary>Decode a JSON document from a string, starting at the given index.</summary>
        /// <param name="s">The JSON string to decode.</param>
        /// <param name="idx">The index in the string at which to begin decoding.</param>
        /// <returns>A tuple of the decoded object and the index where the document ended.</returns>
        public virtual (object?, int) RawDecode(string s, int idx = 0)
        {
            var result = JsonParser.Parse(s.Substring(idx));
            return (result, s.Length);
        }
    }
}
