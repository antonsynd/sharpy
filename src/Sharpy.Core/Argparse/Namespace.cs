using System;
using System.Collections.Generic;

namespace Sharpy
{
    /// <summary>
    /// Stores parsed arguments as named values.
    /// </summary>
    public sealed class Namespace
    {
        private readonly Dict<string, object?> _values = new Dict<string, object?>();

        internal Namespace()
        {
        }

        internal void Set(string name, object? value)
        {
            _values[name] = value;
        }

        /// <summary>
        /// Get a parsed argument value by name.
        /// </summary>
        public object? this[string name]
        {
            get
            {
                if (!_values.ContainsKey(name))
                {
                    throw new AttributeError("'Namespace' object has no attribute '" + name + "'");
                }

                return _values[name];
            }
        }

        /// <summary>
        /// Get a parsed argument value with typed conversion.
        /// </summary>
        public T Get<T>(string name)
        {
            object? val = this[name];
            if (val == null)
            {
                throw new TypeError("argument '" + name + "' is None");
            }

            if (val is T typed)
            {
                return typed;
            }

            throw new TypeError(
                "argument '" + name + "' is type " + val.GetType().Name + ", expected " + typeof(T).Name);
        }

        /// <summary>
        /// Check if a named argument exists.
        /// </summary>
        public bool Contains(string name)
        {
            return _values.ContainsKey(name);
        }

        public override string ToString()
        {
            var parts = new System.Collections.Generic.List<string>();
            foreach (string key in _values.Keys())
            {
                object? val = _values[key];
                string valueStr;
                if (val == null)
                {
                    valueStr = "None";
                }
                else if (val is string s)
                {
                    valueStr = "'" + s + "'";
                }
                else if (val is bool b)
                {
                    valueStr = b ? "True" : "False";
                }
                else
                {
                    valueStr = val.ToString() ?? "None";
                }

                parts.Add(key + "=" + valueStr);
            }

            return "Namespace(" + string.Join(", ", parts) + ")";
        }
    }
}
