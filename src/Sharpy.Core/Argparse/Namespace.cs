using System;
using System.Text;

namespace Sharpy
{
    /// <summary>
    /// Simple namespace object returned by ArgumentParser.ParseArgs().
    /// Stores parsed argument values by name.
    /// </summary>
    public class Namespace
    {
        private readonly Dict<string, object?> _values = new Dict<string, object?>();

        internal void Set(string name, object? value)
        {
            _values[name] = value;
        }

        /// <summary>
        /// Get an argument value by name.
        /// </summary>
        public object? Get(string name)
        {
            if (!_values.ContainsKey(name))
            {
                throw new AttributeError("'Namespace' object has no attribute '" + name + "'");
            }
            return _values[name];
        }

        /// <summary>
        /// Get a typed argument value by name.
        /// </summary>
        public T Get<T>(string name)
        {
            object? val = Get(name);
            if (val == null)
            {
                return default!;
            }
            return (T)val;
        }

        /// <summary>
        /// Check if the namespace contains a given name.
        /// </summary>
        public bool Contains(string name)
        {
            return _values.ContainsKey(name);
        }

        /// <summary>
        /// Get or set a value by name.
        /// </summary>
        public object? this[string name]
        {
            get => Get(name);
            set => _values[name] = value;
        }

        public override string ToString()
        {
            var sb = new StringBuilder("Namespace(");
            bool first = true;
            foreach (var key in _values.Keys())
            {
                if (!first)
                    sb.Append(", ");
                first = false;
                sb.Append(key);
                sb.Append('=');
                object? val = _values[key];
                if (val is string s)
                {
                    sb.Append('\'');
                    sb.Append(s);
                    sb.Append('\'');
                }
                else if (val is bool b)
                {
                    sb.Append(b ? "True" : "False");
                }
                else if (val == null)
                {
                    sb.Append("None");
                }
                else
                {
                    sb.Append(val);
                }
            }
            sb.Append(')');
            return sb.ToString();
        }
    }
}
