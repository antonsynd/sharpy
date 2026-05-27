using System;

namespace Sharpy
{
    /// <summary>Extensible JSON encoder. Subclass this to customize JSON encoding behavior.</summary>
    [SharpyModuleType("json")]
    public class JSONEncoder
    {
        private readonly int _indent;
        private readonly bool _sortKeys;
        private readonly bool _ensureAscii;
        private readonly (string, string)? _separators;

        /// <summary>Create a new JSON encoder with the specified formatting options.</summary>
        /// <param name="indent">Number of spaces for indentation. Use -1 for compact output.</param>
        /// <param name="sortKeys">Whether to sort dictionary keys in the output.</param>
        /// <param name="ensureAscii">Whether to escape non-ASCII characters.</param>
        /// <param name="separators">A tuple of (itemSeparator, keySeparator) overriding the defaults.</param>
        public JSONEncoder(
            int indent = -1,
            bool sortKeys = false,
            bool ensureAscii = true,
            (string, string)? separators = null)
        {
            _indent = indent;
            _sortKeys = sortKeys;
            _ensureAscii = ensureAscii;
            _separators = separators;
        }

        /// <summary>Called for objects that cannot otherwise be serialized. Override to provide custom serialization.</summary>
        /// <param name="obj">The object that is not JSON serializable.</param>
        /// <returns>A JSON-serializable replacement object.</returns>
        public virtual object? Default(object obj)
        {
            throw new TypeError(
                "Object of type " + obj.GetType().Name + " is not JSON serializable");
        }

        /// <summary>Serialize an object to a JSON formatted string.</summary>
        /// <param name="obj">The object to serialize.</param>
        /// <returns>A JSON string representation of the object.</returns>
        public string Encode(object? obj)
        {
            string? itemSep = null;
            string? keySep = null;

            if (_separators.HasValue)
            {
                itemSep = _separators.Value.Item1;
                keySep = _separators.Value.Item2;
            }

            return JsonSerializer.Serialize(
                obj, _indent, _sortKeys, _ensureAscii, itemSep, keySep,
                defaultFunc: DefaultCallback);
        }

        private object? DefaultCallback(object value)
        {
            return Default(value);
        }
    }
}
