using System;

namespace Sharpy
{
    [SharpyModuleType("json")]
    public class JSONEncoder
    {
        private readonly int _indent;
        private readonly bool _sortKeys;
        private readonly bool _ensureAscii;
        private readonly (string, string)? _separators;

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

        public virtual object? Default(object obj)
        {
            throw new TypeError(
                "Object of type " + obj.GetType().Name + " is not JSON serializable");
        }

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
