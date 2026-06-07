// Generated from src/Sharpy.Stdlib/spy/csv_module.spy — do not edit directly.
// To regenerate: sharpyc emit csharp src/Sharpy.Stdlib/spy/csv_module.spy -t library -n Sharpy
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace Sharpy
{
    /// <summary>
    /// CSV file reading and writing.
    /// </summary>
    public static partial class CsvModule
    {
        public static int QUOTE_ALL = 1;
        public static int QUOTE_MINIMAL = 0;
        public static int QUOTE_NONE = 3;
        public static int QUOTE_NONNUMERIC = 2;
        /// <summary>
        /// Parse a single CSV line into a list of fields.
        /// </summary>
        internal static Sharpy.List<string> _ParseLine(string line)
        {
            Sharpy.List<string> fields = new Sharpy.List<string>()
            {
            };
            global::System.Text.StringBuilder field = new global::System.Text.StringBuilder();
            bool inQuotes = false;
            int i = 0;
            while (i < line.Length)
            {
                string c = global::Sharpy.StringHelpers.GetItem(line, i);
                if (inQuotes)
                {
                    if (c == "\"")
                    {
                        if (i + 1 < line.Length && global::Sharpy.StringHelpers.GetItem(line, i + 1) == "\"")
                        {
                            field.Append("\"");
                            i = i + 2;
                        }
                        else
                        {
                            inQuotes = false;
                            i = i + 1;
                        }
                    }
                    else
                    {
                        field.Append(c);
                        i = i + 1;
                    }
                }
                else
                {
                    if (c == "\"")
                    {
                        inQuotes = true;
                        i = i + 1;
                    }
                    else if (c == ",")
                    {
                        fields.Append(field.ToString());
                        field.Clear();
                        i = i + 1;
                    }
                    else
                    {
                        field.Append(c);
                        i = i + 1;
                    }
                }
            }

            fields.Append(field.ToString());
            return fields;
        }

        /// <summary>
        /// Quote a field if it contains special characters.
        /// </summary>
        internal static string _QuoteField(string field)
        {
            bool needsQuoting = false;
            foreach (var __loopVar_0 in global::Sharpy.StringHelpers.Iterate(field))
            {
                var c = __loopVar_0;
                if (c == "," || c == "\"" || c == "\n" || c == "\r")
                {
                    needsQuoting = true;
                    break;
                }
            }

            if (!needsQuoting)
            {
                return field;
            }

            global::System.Text.StringBuilder sb = new global::System.Text.StringBuilder();
            sb.Append("\"");
            foreach (var __loopVar_1 in global::Sharpy.StringHelpers.Iterate(field))
            {
                var c = __loopVar_1;
                if (c == "\"")
                {
                    sb.Append("\"\"");
                }
                else
                {
                    sb.Append(c);
                }
            }

            sb.Append("\"");
            return sb.ToString();
        }

        /// <summary>
        /// Reads CSV data from a list of lines, parsing each line into a list of fields.
        /// </summary>
        public class CsvReader : System.Collections.Generic.IEnumerable<Sharpy.List<string>>
        {
            protected Sharpy.List<string> _Lines;
            protected int _LineNum;
            public System.Collections.Generic.IEnumerator<Sharpy.List<string>> GetEnumerator()
            {
                foreach (var __loopVar_2 in this._Lines)
                {
                    var line = __loopVar_2;
                    this._LineNum = this._LineNum + 1;
                    yield return _ParseLine(line);
                }
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
            public int LineNum
            {
                get
                {
                    return this._LineNum;
                }
            }

            public CsvReader(Sharpy.List<string> lines)
            {
                if (lines == null)
                {
                    throw new global::Sharpy.TypeError("'NoneType' is not iterable");
                }

                this._Lines = lines;
                this._LineNum = 0;
            }
        }

        /// <summary>
        /// Writes CSV data to a TextWriter.
        /// </summary>
        public class CsvWriter
        {
            protected global::System.IO.TextWriter _Output;
            /// <summary>
            /// Write a single row of fields to the CSV output.
            /// </summary>
            public void Writerow(Sharpy.List<string> row)
            {
                if (row == null)
                {
                    throw new global::Sharpy.TypeError("'NoneType' is not iterable");
                }

                global::System.Text.StringBuilder sb = new global::System.Text.StringBuilder();
                bool first = true;
                foreach (var __loopVar_3 in row)
                {
                    var field = __loopVar_3;
                    if (!first)
                    {
                        sb.Append(",");
                    }

                    first = false;
                    sb.Append(_QuoteField(field));
                }

                this._Output.WriteLine(sb.ToString());
            }

            /// <summary>
            /// Write multiple rows of fields to the CSV output.
            /// </summary>
            public void Writerows(Sharpy.List<Sharpy.List<string>> rows)
            {
                if (rows == null)
                {
                    throw new global::Sharpy.TypeError("'NoneType' is not iterable");
                }

                foreach (var __loopVar_4 in rows)
                {
                    var row = __loopVar_4;
                    this.Writerow(row);
                }
            }

            public CsvWriter(global::System.IO.TextWriter output)
            {
                if (output == null)
                {
                    throw new global::Sharpy.TypeError("'NoneType' object is not valid as output");
                }

                this._Output = output;
            }
        }

        /// <summary>
        /// Reads CSV data and maps each row to a dictionary keyed by field names.
        /// </summary>
        public class CsvDictReader : System.Collections.Generic.IEnumerable<Sharpy.Dict<string, string>>
        {
            protected Sharpy.List<string> _Lines;
            protected Optional<Sharpy.List<string>> _Fieldnames;
            public System.Collections.Generic.IEnumerator<Sharpy.Dict<string, string>> GetEnumerator()
            {
                bool isFirstRow = this._Fieldnames.IsNone;
                foreach (var __loopVar_5 in this._Lines)
                {
                    var line = __loopVar_5;
                    Sharpy.List<string> fields = _ParseLine(line);
                    if (isFirstRow)
                    {
                        this._Fieldnames = fields;
                        isFirstRow = false;
                        continue;
                    }

                    if (this._Fieldnames.IsSome)
                    {
                        Sharpy.List<string> names = this._Fieldnames.Unwrap();
                        Sharpy.Dict<string, string> d = new Sharpy.Dict<string, string>()
                        {
                        };
                        int i = 0;
                        while (i < global::Sharpy.Builtins.Len(names))
                        {
                            string key = names[i];
                            string value = "";
                            if (i < global::Sharpy.Builtins.Len(fields))
                            {
                                value = fields[i];
                            }

                            d[key] = value;
                            i = i + 1;
                        }

                        yield return d;
                    }
                }
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
            public Optional<Sharpy.List<string>> Fieldnames
            {
                get
                {
                    return this._Fieldnames;
                }
            }

            public CsvDictReader(Sharpy.List<string> lines, Optional<Sharpy.List<string>> fieldnames = default)
            {
                if (lines == null)
                {
                    throw new global::Sharpy.TypeError("'NoneType' is not iterable");
                }

                this._Lines = lines;
                this._Fieldnames = fieldnames;
            }
        }

        /// <summary>
        /// Writes CSV data from dictionaries keyed by field names.
        /// </summary>
        public class CsvDictWriter
        {
            protected global::System.IO.TextWriter _Output;
            protected Sharpy.List<string> _Fieldnames;
            /// <summary>
            /// Write the field names as a header row.
            /// </summary>
            public void Writeheader()
            {
                global::System.Text.StringBuilder sb = new global::System.Text.StringBuilder();
                bool first = true;
                foreach (var __loopVar_6 in this._Fieldnames)
                {
                    var name = __loopVar_6;
                    if (!first)
                    {
                        sb.Append(",");
                    }

                    first = false;
                    sb.Append(_QuoteField(name));
                }

                this._Output.WriteLine(sb.ToString());
            }

            /// <summary>
            /// Write a single row from a dictionary in field name order.
            /// </summary>
            public void Writerow(Sharpy.Dict<string, string> row)
            {
                if (row == null)
                {
                    throw new global::Sharpy.TypeError("'NoneType' is not iterable");
                }

                global::System.Text.StringBuilder sb = new global::System.Text.StringBuilder();
                bool first = true;
                foreach (var __loopVar_7 in this._Fieldnames)
                {
                    var name = __loopVar_7;
                    if (!first)
                    {
                        sb.Append(",");
                    }

                    first = false;
                    string value = "";
                    if (row.Contains(name))
                    {
                        value = row[name];
                    }

                    sb.Append(_QuoteField(value));
                }

                this._Output.WriteLine(sb.ToString());
            }

            /// <summary>
            /// Write multiple rows from dictionaries.
            /// </summary>
            public void Writerows(Sharpy.List<Sharpy.Dict<string, string>> rows)
            {
                if (rows == null)
                {
                    throw new global::Sharpy.TypeError("'NoneType' is not iterable");
                }

                foreach (var __loopVar_8 in rows)
                {
                    var row = __loopVar_8;
                    this.Writerow(row);
                }
            }

            public Sharpy.List<string> Fieldnames
            {
                get
                {
                    return this._Fieldnames;
                }
            }

            public CsvDictWriter(global::System.IO.TextWriter output, Sharpy.List<string> fieldnames)
            {
                if (output == null)
                {
                    throw new global::Sharpy.TypeError("'NoneType' object is not valid as output");
                }

                if (fieldnames == null)
                {
                    throw new global::Sharpy.TypeError("fieldnames must not be None");
                }

                this._Output = output;
                this._Fieldnames = fieldnames;
            }
        }

        /// <summary>
        /// Create a CSV reader from a list of lines.
        /// </summary>
        public static CsvReader Reader(Sharpy.List<string> lines)
        {
            return new CsvReader(lines);
        }

        /// <summary>
        /// Create a CSV writer that writes to a TextWriter.
        /// </summary>
        public static CsvWriter Writer(global::System.IO.TextWriter output)
        {
            return new CsvWriter(output);
        }

        /// <summary>
        /// Create a CSV DictReader from a list of lines.
        /// </summary>
        public static CsvDictReader DictReader(Sharpy.List<string> lines, Optional<Sharpy.List<string>> fieldnames = default)
        {
            return new CsvDictReader(lines, fieldnames);
        }

        /// <summary>
        /// Create a CSV DictWriter that writes to a TextWriter.
        /// </summary>
        public static CsvDictWriter DictWriter(global::System.IO.TextWriter output, Sharpy.List<string> fieldnames)
        {
            return new CsvDictWriter(output, fieldnames);
        }
    }
}
