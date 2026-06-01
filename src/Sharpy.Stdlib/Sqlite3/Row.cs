using System;
using System.Collections.Generic;

namespace Sharpy
{
    /// <summary>Represents a row returned from a query when using <see cref="Sqlite3.Row"/> as the row factory.</summary>
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

        /// <summary>Get a column value by integer index. Supports negative indexing.</summary>
        /// <param name="index">The zero-based column index. Negative values count from the end.</param>
        /// <returns>The column value, or null if the value is SQL NULL.</returns>
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

        /// <summary>Get a column value by column name (case-insensitive).</summary>
        /// <param name="columnName">The column name.</param>
        /// <returns>The column value, or null if the value is SQL NULL.</returns>
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

        /// <summary>Return a list of column names.</summary>
        /// <returns>A list of column name strings.</returns>
        public List<string> Keys()
        {
            return new List<string>(_columnNames);
        }

        /// <summary>Gets the number of columns in the row.</summary>
        public int Count
        {
            get { return _values.Length; }
        }

        /// <summary>Return a string representation of the row.</summary>
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
