// Generated from src/Sharpy.Stdlib/spy/csv_module.spy — do not edit directly.
// To regenerate: sharpyc emit csharp src/Sharpy.Stdlib/spy/csv_module.spy -t library -n Sharpy
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace Sharpy.CsvModule
{
    /// <summary>
    /// CSV file reading and writing.
    /// </summary>
    public static partial class CsvModuleModule
    {
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int QUOTE_ALL__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte QUOTE_ALL__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void QUOTE_ALL__init()
        {
            QUOTE_ALL__field = 1;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void QUOTE_ALL__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref QUOTE_ALL__state, "QUOTE_ALL", QUOTE_ALL__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref int QUOTE_ALL
        {
            get
            {
                if (!__Swept.Done)
                    QUOTE_ALL__slow();
                return ref QUOTE_ALL__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void QUOTE_ALL__ensure()
        {
            if (!__Swept.Done)
                QUOTE_ALL__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void QUOTE_ALL__storeSlow(int value) => global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref QUOTE_ALL__state, ref QUOTE_ALL__field, value);
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int QUOTE_ALL__set(int value)
        {
            if (__Swept.Done)
                QUOTE_ALL__field = value;
            else
                QUOTE_ALL__storeSlow(value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<int>? QUOTE_ALL__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref QUOTE_ALL__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<int>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref int QUOTE_ALL__out(out global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            spill = QUOTE_ALL__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref QUOTE_ALL__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R QUOTE_ALL__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            if (spill != null)
                QUOTE_ALL__set(spill.Value!);
            return result;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int QUOTE_MINIMAL__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte QUOTE_MINIMAL__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void QUOTE_MINIMAL__init()
        {
            QUOTE_MINIMAL__field = 0;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void QUOTE_MINIMAL__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref QUOTE_MINIMAL__state, "QUOTE_MINIMAL", QUOTE_MINIMAL__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref int QUOTE_MINIMAL
        {
            get
            {
                if (!__Swept.Done)
                    QUOTE_MINIMAL__slow();
                return ref QUOTE_MINIMAL__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void QUOTE_MINIMAL__ensure()
        {
            if (!__Swept.Done)
                QUOTE_MINIMAL__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void QUOTE_MINIMAL__storeSlow(int value) => global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref QUOTE_MINIMAL__state, ref QUOTE_MINIMAL__field, value);
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int QUOTE_MINIMAL__set(int value)
        {
            if (__Swept.Done)
                QUOTE_MINIMAL__field = value;
            else
                QUOTE_MINIMAL__storeSlow(value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<int>? QUOTE_MINIMAL__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref QUOTE_MINIMAL__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<int>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref int QUOTE_MINIMAL__out(out global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            spill = QUOTE_MINIMAL__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref QUOTE_MINIMAL__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R QUOTE_MINIMAL__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            if (spill != null)
                QUOTE_MINIMAL__set(spill.Value!);
            return result;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int QUOTE_NONE__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte QUOTE_NONE__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void QUOTE_NONE__init()
        {
            QUOTE_NONE__field = 3;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void QUOTE_NONE__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref QUOTE_NONE__state, "QUOTE_NONE", QUOTE_NONE__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref int QUOTE_NONE
        {
            get
            {
                if (!__Swept.Done)
                    QUOTE_NONE__slow();
                return ref QUOTE_NONE__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void QUOTE_NONE__ensure()
        {
            if (!__Swept.Done)
                QUOTE_NONE__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void QUOTE_NONE__storeSlow(int value) => global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref QUOTE_NONE__state, ref QUOTE_NONE__field, value);
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int QUOTE_NONE__set(int value)
        {
            if (__Swept.Done)
                QUOTE_NONE__field = value;
            else
                QUOTE_NONE__storeSlow(value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<int>? QUOTE_NONE__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref QUOTE_NONE__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<int>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref int QUOTE_NONE__out(out global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            spill = QUOTE_NONE__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref QUOTE_NONE__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R QUOTE_NONE__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            if (spill != null)
                QUOTE_NONE__set(spill.Value!);
            return result;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int QUOTE_NONNUMERIC__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte QUOTE_NONNUMERIC__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void QUOTE_NONNUMERIC__init()
        {
            QUOTE_NONNUMERIC__field = 2;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void QUOTE_NONNUMERIC__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref QUOTE_NONNUMERIC__state, "QUOTE_NONNUMERIC", QUOTE_NONNUMERIC__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref int QUOTE_NONNUMERIC
        {
            get
            {
                if (!__Swept.Done)
                    QUOTE_NONNUMERIC__slow();
                return ref QUOTE_NONNUMERIC__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void QUOTE_NONNUMERIC__ensure()
        {
            if (!__Swept.Done)
                QUOTE_NONNUMERIC__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void QUOTE_NONNUMERIC__storeSlow(int value) => global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref QUOTE_NONNUMERIC__state, ref QUOTE_NONNUMERIC__field, value);
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int QUOTE_NONNUMERIC__set(int value)
        {
            if (__Swept.Done)
                QUOTE_NONNUMERIC__field = value;
            else
                QUOTE_NONNUMERIC__storeSlow(value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<int>? QUOTE_NONNUMERIC__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref QUOTE_NONNUMERIC__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<int>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref int QUOTE_NONNUMERIC__out(out global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            spill = QUOTE_NONNUMERIC__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref QUOTE_NONNUMERIC__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R QUOTE_NONNUMERIC__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            if (spill != null)
                QUOTE_NONNUMERIC__set(spill.Value!);
            return result;
        }

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
                var c_1 = __loopVar_1;
                if (c_1 == "\"")
                {
                    sb.Append("\"\"");
                }
                else
                {
                    sb.Append(c_1);
                }
            }

            sb.Append("\"");
            return sb.ToString();
        }

        /// <summary>
        /// Create a CSV reader from a list of lines.
        /// </summary>
        public static global::Sharpy.CsvModule.CsvReader Reader(Sharpy.List<string> lines)
        {
            return new global::Sharpy.CsvModule.CsvReader(lines);
        }

        /// <summary>
        /// Create a CSV writer that writes to a TextWriter.
        /// </summary>
        public static global::Sharpy.CsvModule.CsvWriter Writer(global::System.IO.TextWriter output)
        {
            return new global::Sharpy.CsvModule.CsvWriter(output);
        }

        /// <summary>
        /// Create a CSV DictReader from a list of lines.
        /// </summary>
        public static global::Sharpy.CsvModule.CsvDictReader DictReader(Sharpy.List<string> lines, Sharpy.List<string>? fieldnames = null)
        {
            return new global::Sharpy.CsvModule.CsvDictReader(lines, fieldnames);
        }

        /// <summary>
        /// Create a CSV DictWriter that writes to a TextWriter.
        /// </summary>
        public static global::Sharpy.CsvModule.CsvDictWriter DictWriter(global::System.IO.TextWriter output, Sharpy.List<string> fieldnames)
        {
            return new global::Sharpy.CsvModule.CsvDictWriter(output, fieldnames);
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static global::Sharpy.StaticInitHost __host;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void __Sweep()
        {
            global::Sharpy.StaticInit.Run(ref QUOTE_ALL__state, "QUOTE_ALL", QUOTE_ALL__init);
            global::Sharpy.StaticInit.Run(ref QUOTE_MINIMAL__state, "QUOTE_MINIMAL", QUOTE_MINIMAL__init);
            global::Sharpy.StaticInit.Run(ref QUOTE_NONE__state, "QUOTE_NONE", QUOTE_NONE__init);
            global::Sharpy.StaticInit.Run(ref QUOTE_NONNUMERIC__state, "QUOTE_NONNUMERIC", QUOTE_NONNUMERIC__init);
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static class __Swept
        {
            internal static readonly bool Done;
            static __Swept()
            {
                Done = global::Sharpy.StaticInit.TryLoad(ref __host, __Sweep);
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        [global::Sharpy.SharpyLoadEntry(true)]
        public static void __Load()
        {
            if (!__Swept.Done)
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
        }
    }

    /// <summary>
    /// Reads CSV data from a list of lines, parsing each line into a list of fields.
    /// </summary>
    [global::Sharpy.SharpyModuleType("csv", "CsvReader")]
    public class CsvReader : System.Collections.Generic.IEnumerable<Sharpy.List<string>>
    {
        protected Sharpy.List<string> _Lines;
        protected int _LineNum;
        public int LineNum
        {
            get
            {
                return this._LineNum;
            }
        }

        public System.Collections.Generic.IEnumerator<Sharpy.List<string>> GetEnumerator()
        {
            foreach (var __loopVar_2 in this._Lines)
            {
                var line = __loopVar_2;
                this._LineNum = this._LineNum + 1;
                yield return global::Sharpy.CsvModule.CsvModuleModule._ParseLine(line);
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        public CsvReader(Sharpy.List<string> lines)
        {
            if (lines is null)
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
    [global::Sharpy.SharpyModuleType("csv", "CsvWriter")]
    public class CsvWriter
    {
        protected global::System.IO.TextWriter _Output;
        /// <summary>
        /// Write a single row of fields to the CSV output.
        /// </summary>
        public void Writerow(Sharpy.List<string> row)
        {
            if (row is null)
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
                sb.Append(global::Sharpy.CsvModule.CsvModuleModule._QuoteField(field));
            }

            this._Output.WriteLine(sb.ToString());
        }

        /// <summary>
        /// Write multiple rows of fields to the CSV output.
        /// </summary>
        public void Writerows(Sharpy.List<Sharpy.List<string>> rows)
        {
            if (rows is null)
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
            if (output is null)
            {
                throw new global::Sharpy.TypeError("'NoneType' object is not valid as output");
            }

            this._Output = output;
        }
    }

    /// <summary>
    /// Reads CSV data and maps each row to a dictionary keyed by field names.
    /// </summary>
    [global::Sharpy.SharpyModuleType("csv", "CsvDictReader")]
    public class CsvDictReader : System.Collections.Generic.IEnumerable<Sharpy.Dict<string, string>>
    {
        protected Sharpy.List<string> _Lines;
        protected Sharpy.List<string>? _Fieldnames;
        public Sharpy.List<string>? Fieldnames
        {
            get
            {
                return this._Fieldnames;
            }
        }

        public System.Collections.Generic.IEnumerator<Sharpy.Dict<string, string>> GetEnumerator()
        {
            bool isFirstRow = this._Fieldnames is null;
            foreach (var __loopVar_5 in this._Lines)
            {
                var line = __loopVar_5;
                Sharpy.List<string> fields = global::Sharpy.CsvModule.CsvModuleModule._ParseLine(line);
                if (isFirstRow)
                {
                    this._Fieldnames = fields;
                    isFirstRow = false;
                    continue;
                }

                Sharpy.List<string>? header = this._Fieldnames;
                if (header is not null)
                {
                    Sharpy.List<string> names = header!;
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
        public CsvDictReader(Sharpy.List<string> lines, Sharpy.List<string>? fieldnames = null)
        {
            if (lines is null)
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
    [global::Sharpy.SharpyModuleType("csv", "CsvDictWriter")]
    public class CsvDictWriter
    {
        protected global::System.IO.TextWriter _Output;
        protected Sharpy.List<string> _Fieldnames;
        public Sharpy.List<string> Fieldnames
        {
            get
            {
                return this._Fieldnames;
            }
        }

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
                sb.Append(global::Sharpy.CsvModule.CsvModuleModule._QuoteField(name));
            }

            this._Output.WriteLine(sb.ToString());
        }

        /// <summary>
        /// Write a single row from a dictionary in field name order.
        /// </summary>
        public void Writerow(Sharpy.Dict<string, string> row)
        {
            if (row is null)
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

                sb.Append(global::Sharpy.CsvModule.CsvModuleModule._QuoteField(value));
            }

            this._Output.WriteLine(sb.ToString());
        }

        /// <summary>
        /// Write multiple rows from dictionaries.
        /// </summary>
        public void Writerows(Sharpy.List<Sharpy.Dict<string, string>> rows)
        {
            if (rows is null)
            {
                throw new global::Sharpy.TypeError("'NoneType' is not iterable");
            }

            foreach (var __loopVar_8 in rows)
            {
                var row = __loopVar_8;
                this.Writerow(row);
            }
        }

        public CsvDictWriter(global::System.IO.TextWriter output, Sharpy.List<string> fieldnames)
        {
            if (output is null)
            {
                throw new global::Sharpy.TypeError("'NoneType' object is not valid as output");
            }

            if (fieldnames is null)
            {
                throw new global::Sharpy.TypeError("fieldnames must not be None");
            }

            this._Output = output;
            this._Fieldnames = fieldnames;
        }
    }
}
