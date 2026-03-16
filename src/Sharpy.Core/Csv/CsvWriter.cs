using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Sharpy
{
    /// <summary>
    /// Writes CSV data to a TextWriter.
    /// </summary>
    [SharpyModuleType("csv")]
    public sealed class CsvWriter
    {
        private readonly TextWriter _output;

        internal CsvWriter(TextWriter output)
        {
            _output = output ?? throw new TypeError("'NoneType' object is not valid as output");
        }

        /// <summary>
        /// Write a single row of fields to the CSV output.
        /// </summary>
        public void Writerow(IEnumerable<string> row)
        {
            if (row == null)
            {
                throw new TypeError("'NoneType' is not iterable");
            }

            var sb = new StringBuilder();
            bool first = true;
            foreach (string field in row)
            {
                if (!first)
                {
                    sb.Append(',');
                }

                first = false;
                sb.Append(QuoteField(field ?? ""));
            }

            _output.WriteLine(sb.ToString());
        }

        /// <summary>
        /// Write multiple rows of fields to the CSV output.
        /// </summary>
        public void Writerows(IEnumerable<IEnumerable<string>> rows)
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

        private static string QuoteField(string field)
        {
            bool needsQuoting = false;
            foreach (char c in field)
            {
                if (c == ',' || c == '\"' || c == '\n' || c == '\r')
                {
                    needsQuoting = true;
                    break;
                }
            }

            if (!needsQuoting)
            {
                return field;
            }

            var sb = new StringBuilder();
            sb.Append('\"');
            foreach (char c in field)
            {
                if (c == '\"')
                {
                    sb.Append("\"\"");
                }
                else
                {
                    sb.Append(c);
                }
            }

            sb.Append('\"');
            return sb.ToString();
        }
    }
}
