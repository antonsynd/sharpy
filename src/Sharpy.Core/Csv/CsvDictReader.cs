using System.Collections;
using System.Collections.Generic;

namespace Sharpy
{
    /// <summary>
    /// Reads CSV data and maps each row to a dictionary keyed by field names,
    /// similar to Python's <c>csv.DictReader</c>.
    /// </summary>
    [SharpyModuleType("csv")]
    public sealed class CsvDictReader : IEnumerable<Dict<Str, Str>>
    {
        private readonly IEnumerable<Str> _lines;
        private Sharpy.List<Str>? _fieldnames;

        /// <summary>
        /// The field names used as dictionary keys. If not provided in the constructor,
        /// this is populated from the first row of the CSV data after iteration begins.
        /// </summary>
        public Sharpy.List<Str>? Fieldnames
        {
            get { return _fieldnames; }
        }

        /// <summary>
        /// Create a new DictReader from an enumerable of lines.
        /// </summary>
        /// <param name="lines">The lines to parse as CSV.</param>
        /// <param name="fieldnames">Optional field names. If null, the first row is used.</param>
        internal CsvDictReader(IEnumerable<Str> lines, Sharpy.List<Str>? fieldnames = null)
        {
            _lines = lines ?? throw new TypeError("'NoneType' is not iterable");
            _fieldnames = fieldnames;
        }

        /// <summary>
        /// Returns an enumerator that parses each line into a dictionary mapping
        /// field names to values.
        /// </summary>
        public IEnumerator<Dict<Str, Str>> GetEnumerator()
        {
            // Intentional: _fieldnames is set once from the first row and persisted across
            // subsequent iterations, matching Python's csv.DictReader behavior.
            bool isFirstRow = _fieldnames == null;

            foreach (Str line in _lines)
            {
                var fields = CsvReader.ParseLine((string)line);

                if (isFirstRow)
                {
                    _fieldnames = fields;
                    isFirstRow = false;
                    continue;
                }

                // After the isFirstRow branch, _fieldnames is guaranteed non-null.
                var fieldnames = _fieldnames!;
                var dict = new Dict<Str, Str>();

                int fieldnameCount = ((System.Collections.Generic.ICollection<Str>)fieldnames).Count;
                int fieldCount = ((System.Collections.Generic.ICollection<Str>)fields).Count;

                for (int i = 0; i < fieldnameCount; i++)
                {
                    Str key = fieldnames[i];
                    Str value = i < fieldCount ? fields[i] : (Str)"";
                    dict[key] = value;
                }

                yield return dict;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
