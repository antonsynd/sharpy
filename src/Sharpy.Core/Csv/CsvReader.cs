using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Sharpy
{
    /// <summary>
    /// Reads CSV data from an enumerable of lines, parsing each line into a list of fields.
    /// Handles quoted fields, escaped quotes, and commas within quoted fields.
    /// </summary>
    [SharpyModuleType("csv")]
    public sealed class CsvReader : IEnumerable<List<string>>
    {
        private readonly IEnumerable<string> _lines;

        /// <summary>
        /// Create a new CsvReader from an enumerable of lines.
        /// </summary>
        /// <param name="lines">The lines to parse as CSV.</param>
        internal CsvReader(IEnumerable<string> lines)
        {
            _lines = lines ?? throw new TypeError("'NoneType' is not iterable");
        }

        /// <summary>
        /// Returns an enumerator that parses each line into a list of string fields.
        /// </summary>
        public IEnumerator<List<string>> GetEnumerator()
        {
            foreach (string line in _lines)
            {
                yield return ParseLine(line);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        private static List<string> ParseLine(string line)
        {
            var fields = new System.Collections.Generic.List<string>();
            var field = new StringBuilder();
            bool inQuotes = false;
            int i = 0;

            while (i < line.Length)
            {
                char c = line[i];

                if (inQuotes)
                {
                    if (c == '"')
                    {
                        // Check for escaped quote ("")
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            field.Append('"');
                            i += 2;
                        }
                        else
                        {
                            // End of quoted field
                            inQuotes = false;
                            i++;
                        }
                    }
                    else
                    {
                        field.Append(c);
                        i++;
                    }
                }
                else
                {
                    if (c == '"')
                    {
                        inQuotes = true;
                        i++;
                    }
                    else if (c == ',')
                    {
                        fields.Add(field.ToString());
                        field.Clear();
                        i++;
                    }
                    else
                    {
                        field.Append(c);
                        i++;
                    }
                }
            }

            // Add last field
            fields.Add(field.ToString());

            return new List<string>(fields);
        }
    }
}
