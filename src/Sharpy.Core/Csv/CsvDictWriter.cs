using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Sharpy
{
    /// <summary>
    /// Writes CSV data from dictionaries keyed by field names,
    /// similar to Python's <c>csv.DictWriter</c>.
    /// </summary>
    [SharpyModuleType("csv")]
    public sealed class CsvDictWriter
    {
        private readonly TextWriter _output;
        private readonly Sharpy.List<Str> _fieldnames;

        /// <summary>
        /// The field names that determine column order in the output.
        /// </summary>
        public Sharpy.List<Str> Fieldnames
        {
            get { return _fieldnames; }
        }

        /// <summary>
        /// Create a new DictWriter.
        /// </summary>
        /// <param name="output">The output writer to write CSV data to.</param>
        /// <param name="fieldnames">The field names determining column order.</param>
        internal CsvDictWriter(TextWriter output, Sharpy.List<Str> fieldnames)
        {
            _output = output ?? throw new TypeError("'NoneType' object is not valid as output");
            _fieldnames = fieldnames ?? throw new TypeError("fieldnames must not be None");
        }

        /// <summary>
        /// Write the field names as a header row.
        /// </summary>
        public void Writeheader()
        {
            var sb = new StringBuilder();
            bool first = true;
            foreach (Str name in _fieldnames)
            {
                if (!first)
                {
                    sb.Append(',');
                }

                first = false;
                sb.Append(CsvWriter.QuoteField((string)name));
            }

            _output.WriteLine(sb.ToString());
        }

        /// <summary>
        /// Write a single row from a dictionary. Values are written in field name order.
        /// Missing keys produce empty strings.
        /// </summary>
        /// <param name="row">A dictionary mapping field names to values.</param>
        public void Writerow(Dict<Str, Str> row)
        {
            if (row is null)
            {
                throw new TypeError("'NoneType' is not iterable");
            }

            var sb = new StringBuilder();
            bool first = true;
            foreach (Str name in _fieldnames)
            {
                if (!first)
                {
                    sb.Append(',');
                }

                first = false;
                string value = "";
                if (row.ContainsKey(name))
                {
                    value = (string)row[name];
                }

                sb.Append(CsvWriter.QuoteField(value));
            }

            _output.WriteLine(sb.ToString());
        }

        /// <summary>
        /// Write multiple rows from dictionaries.
        /// </summary>
        /// <param name="rows">An enumerable of dictionaries to write.</param>
        public void Writerows(IEnumerable<Dict<Str, Str>> rows)
        {
            if (rows == null)
            {
                throw new TypeError("'NoneType' is not iterable");
            }

            foreach (var row in rows)
            {
                Writerow(row);
            }
        }
    }
}
