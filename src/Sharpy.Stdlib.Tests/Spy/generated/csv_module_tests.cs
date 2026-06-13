// Generated from src/Sharpy.Stdlib.Tests/Spy — do not edit directly.
// To regenerate: bash build_tools/regenerate_spy_tests.sh
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Sharpy.Stdlib.Tests.Spy;
using csv = global::Sharpy.CsvModule;
using Xunit;
using static Sharpy.Stdlib.Tests.Spy.CSV.CsvModuleTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class CSV
    {
        [global::Sharpy.SharpyModule("csv.csv_module_tests")]
        public static partial class CsvModuleTests
        {
        }
    }

    public static partial class CSV
    {
        public partial class CsvModuleTestsTests
        {
            [Xunit.FactAttribute]
            public void TestReaderSimpleLine()
            {
#line (8, 5) - (8, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_module_tests.spy"
                var reader = csv.Reader(new Sharpy.List<string>() { "a,b,c" });
#line (9, 5) - (9, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_module_tests.spy"
                Sharpy.List<Sharpy.List<string>> rows = new Sharpy.List<Sharpy.List<string>>()
                {
                };
#line (10, 5) - (12, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_module_tests.spy"
                foreach (var __loopVar_0 in reader)
                {
                    var row = __loopVar_0;
#line (11, 9) - (11, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_module_tests.spy"
                    rows.Append(row);
                }

#line (12, 5) - (12, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_module_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(rows));
#line (13, 5) - (13, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_module_tests.spy"
                Xunit.Assert.Equal("a", rows[0][0]);
#line (14, 5) - (14, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_module_tests.spy"
                Xunit.Assert.Equal("b", rows[0][1]);
#line (15, 5) - (15, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_module_tests.spy"
                Xunit.Assert.Equal("c", rows[0][2]);
            }

            [Xunit.FactAttribute]
            public void TestReaderQuotedFieldWithComma()
            {
#line (19, 5) - (19, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_module_tests.spy"
                var reader = csv.Reader(new Sharpy.List<string>() { "a,\"hello, world\",c" });
#line (20, 5) - (20, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_module_tests.spy"
                Sharpy.List<Sharpy.List<string>> rows = new Sharpy.List<Sharpy.List<string>>()
                {
                };
#line (21, 5) - (23, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_module_tests.spy"
                foreach (var __loopVar_1 in reader)
                {
                    var row = __loopVar_1;
#line (22, 9) - (22, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_module_tests.spy"
                    rows.Append(row);
                }

#line (23, 5) - (23, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_module_tests.spy"
                Xunit.Assert.Equal("a", rows[0][0]);
#line (24, 5) - (24, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_module_tests.spy"
                Xunit.Assert.Equal("hello, world", rows[0][1]);
#line (25, 5) - (25, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_module_tests.spy"
                Xunit.Assert.Equal("c", rows[0][2]);
            }

            [Xunit.FactAttribute]
            public void TestReaderEscapedQuote()
            {
#line (30, 5) - (30, 55) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_module_tests.spy"
                var reader = csv.Reader(new Sharpy.List<string>() { "a,\"say \"\"hello\"\"\",c" });
#line (31, 5) - (31, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_module_tests.spy"
                Sharpy.List<Sharpy.List<string>> rows = new Sharpy.List<Sharpy.List<string>>()
                {
                };
#line (32, 5) - (34, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_module_tests.spy"
                foreach (var __loopVar_2 in reader)
                {
                    var row = __loopVar_2;
#line (33, 9) - (33, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_module_tests.spy"
                    rows.Append(row);
                }

#line (34, 5) - (34, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_module_tests.spy"
                Xunit.Assert.Equal("say \"hello\"", rows[0][1]);
            }

            [Xunit.FactAttribute]
            public void TestReaderMultipleRows()
            {
#line (38, 5) - (38, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_module_tests.spy"
                var reader = csv.Reader(new Sharpy.List<string>() { "a,b", "c,d", "e,f" });
#line (39, 5) - (39, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_module_tests.spy"
                int count = 0;
#line (40, 5) - (42, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_module_tests.spy"
                foreach (var __loopVar_3 in reader)
                {
                    var row = __loopVar_3;
#line (41, 9) - (41, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_module_tests.spy"
                    count = count + 1;
                }

#line (42, 5) - (42, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_module_tests.spy"
                Xunit.Assert.Equal(3, count);
            }

            [Xunit.FactAttribute]
            public void TestWriterSimpleRow()
            {
#line (48, 5) - (48, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_module_tests.spy"
                var sw = new global::Sharpy.StringIO();
#line (49, 5) - (49, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_module_tests.spy"
                var writer = csv.Writer(sw);
#line (50, 5) - (50, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_module_tests.spy"
                writer.Writerow(new Sharpy.List<string>() { "a", "b", "c" });
#line (51, 5) - (51, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_module_tests.spy"
                Xunit.Assert.Equal("a,b,c\n", sw.Getvalue());
            }

            [Xunit.FactAttribute]
            public void TestWriterQuotesFieldWithComma()
            {
#line (55, 5) - (55, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_module_tests.spy"
                var sw = new global::Sharpy.StringIO();
#line (56, 5) - (56, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_module_tests.spy"
                var writer = csv.Writer(sw);
#line (57, 5) - (57, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_module_tests.spy"
                writer.Writerow(new Sharpy.List<string>() { "hello, world", "test" });
#line (58, 5) - (58, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_module_tests.spy"
                Xunit.Assert.Contains("\"hello, world\"", sw.Getvalue());
            }

            [Xunit.FactAttribute]
            public void TestWriterWriterowsMultipleRows()
            {
#line (62, 5) - (62, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_module_tests.spy"
                var sw = new global::Sharpy.StringIO();
#line (63, 5) - (63, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_module_tests.spy"
                var writer = csv.Writer(sw);
#line (64, 5) - (64, 54) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_module_tests.spy"
                Sharpy.List<Sharpy.List<string>> rows = new Sharpy.List<Sharpy.List<string>>()
                {
                    new Sharpy.List<string>()
                    {
                        "a",
                        "b"
                    },
                    new Sharpy.List<string>()
                    {
                        "c",
                        "d"
                    }
                };
#line (65, 5) - (65, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_module_tests.spy"
                writer.Writerows(rows);
#line (66, 5) - (66, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_module_tests.spy"
                Xunit.Assert.Equal("a,b\nc,d\n", sw.Getvalue());
            }

            [Xunit.FactAttribute]
            public void TestConstantsHaveCorrectValues()
            {
#line (72, 5) - (72, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_module_tests.spy"
                Xunit.Assert.Equal(1, csv.QUOTE_ALL);
#line (73, 5) - (73, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_module_tests.spy"
                Xunit.Assert.Equal(0, csv.QUOTE_MINIMAL);
#line (74, 5) - (74, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_module_tests.spy"
                Xunit.Assert.Equal(3, csv.QUOTE_NONE);
#line (75, 5) - (75, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_module_tests.spy"
                Xunit.Assert.Equal(2, csv.QUOTE_NONNUMERIC);
            }

            [Xunit.FactAttribute]
            public void TestReaderLineNumTracksLinesRead()
            {
#line (81, 5) - (81, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_module_tests.spy"
                var reader = csv.Reader(new Sharpy.List<string>() { "a,b", "c,d", "e,f" });
#line (82, 5) - (82, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_module_tests.spy"
                Xunit.Assert.Equal(0, reader.LineNum);
#line (83, 5) - (83, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_module_tests.spy"
                int count = 0;
#line (84, 5) - (87, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_module_tests.spy"
                foreach (var __loopVar_4 in reader)
                {
                    var row = __loopVar_4;
#line (85, 9) - (85, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_module_tests.spy"
                    count = count + 1;
#line (86, 9) - (86, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_module_tests.spy"
                    Xunit.Assert.Equal(count, reader.LineNum);
                }

#line (87, 5) - (87, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_module_tests.spy"
                Xunit.Assert.Equal(3, reader.LineNum);
            }
        }
    }
}
