using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Sharpy
{
    /// <summary>
    /// CSV file reading and writing, similar to Python's csv module.
    /// </summary>
    public static partial class Csv
    {
        /// <summary>Quote all fields.</summary>
        public const int QUOTE_ALL = 1;

        /// <summary>Quote only fields that contain special characters.</summary>
        public const int QUOTE_MINIMAL = 0;

        /// <summary>Do not quote fields.</summary>
        public const int QUOTE_NONE = 3;

        /// <summary>Quote all non-numeric fields.</summary>
        public const int QUOTE_NONNUMERIC = 2;

        /// <summary>
        /// Create a CSV reader from an enumerable of lines.
        /// </summary>
        /// <param name="lines">An enumerable of CSV lines to parse.</param>
        /// <returns>A <see cref="CsvReader"/> that iterates over parsed rows.</returns>
        public static CsvReader Reader(IEnumerable<Str> lines)
        {
            return new CsvReader(lines);
        }

        /// <summary>
        /// Create a CSV writer that writes to a TextWriter.
        /// </summary>
        /// <param name="output">The output writer to write CSV data to.</param>
        /// <returns>A <see cref="CsvWriter"/> for writing CSV rows.</returns>
        public static CsvWriter Writer(System.IO.TextWriter output)
        {
            return new CsvWriter(output);
        }

        /// <summary>
        /// Create a CSV DictReader from an enumerable of lines.
        /// </summary>
        /// <param name="lines">An enumerable of CSV lines to parse.</param>
        /// <param name="fieldnames">Optional field names. If null, the first row is used as field names.</param>
        /// <returns>A <see cref="CsvDictReader"/> that iterates over parsed rows as dictionaries.</returns>
        public static CsvDictReader DictReader(IEnumerable<Str> lines, Sharpy.List<Str>? fieldnames = null)
        {
            return new CsvDictReader(lines, fieldnames);
        }

        /// <summary>
        /// Create a CSV DictWriter that writes to a TextWriter.
        /// </summary>
        /// <param name="output">The output writer to write CSV data to.</param>
        /// <param name="fieldnames">The field names determining column order.</param>
        /// <returns>A <see cref="CsvDictWriter"/> for writing CSV rows as dictionaries.</returns>
        public static CsvDictWriter DictWriter(System.IO.TextWriter output, Sharpy.List<Str> fieldnames)
        {
            return new CsvDictWriter(output, fieldnames);
        }
    }
}
