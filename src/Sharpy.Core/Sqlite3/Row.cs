using System;
using System.Collections.Generic;

namespace Sharpy
{
    [SharpyModuleType("sqlite3", "Row")]
    public class Sqlite3Row : ISized
    {
        private readonly object?[] _values;
        private readonly string[] _columnNames;
        private readonly System.Collections.Generic.Dictionary<string, int> _nameIndex;

        internal Sqlite3Row(object?[] values, string[] columnNames)
        {
            _values = values;
            _columnNames = columnNames;
            _nameIndex = new System.Collections.Generic.Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < columnNames.Length; i++)
            {
                _nameIndex[columnNames[i]] = i;
            }
        }

        public object? this[int index]
        {
            get
            {
                if (index < 0)
                {
                    index = _values.Length + index;
                }

                if (index < 0 || index >= _values.Length)
                {
                    throw new IndexError("tuple index out of range");
                }

                return _values[index];
            }
        }

        public object? this[string columnName]
        {
            get
            {
                if (!_nameIndex.TryGetValue(columnName, out int index))
                {
                    throw new IndexError("No item with that key");
                }

                return _values[index];
            }
        }

        public System.Collections.Generic.List<string> Keys()
        {
            return new System.Collections.Generic.List<string>(_columnNames);
        }

        public int Count
        {
            get { return _values.Length; }
        }

        public override string ToString()
        {
            var parts = new System.Text.StringBuilder("<sqlite3.Row");
            for (int i = 0; i < _columnNames.Length; i++)
            {
                parts.Append(i == 0 ? " " : ", ");
                parts.Append(_columnNames[i]);
                parts.Append("=");
                if (_values[i] == null)
                {
                    parts.Append("None");
                }
                else if (_values[i] is string s)
                {
                    parts.Append("'");
                    parts.Append(s);
                    parts.Append("'");
                }
                else
                {
                    parts.Append(_values[i]);
                }
            }

            parts.Append(">");
            return parts.ToString();
        }
    }
}
