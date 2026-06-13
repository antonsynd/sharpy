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
using static Sharpy.Stdlib.Tests.Spy.CSV.CsvReaderWriterTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class CSV
    {
        [global::Sharpy.SharpyModule("csv.csv_reader_writer_tests")]
        public static partial class CsvReaderWriterTests
        {
        }
    }

    public static partial class CSV
    {
        public partial class CsvReaderWriterTestsTests
        {
            [Xunit.FactAttribute]
            public void TestReaderEmptyLinesReturnsNoRows()
            {
#line (12, 5) - (12, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Sharpy.List<string> empty = new Sharpy.List<string>()
                {
                };
#line (13, 5) - (13, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                var reader = csv.Reader(empty);
#line (14, 5) - (14, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Sharpy.List<Sharpy.List<string>> rows = new Sharpy.List<Sharpy.List<string>>()
                {
                };
#line (15, 5) - (17, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                foreach (var __loopVar_0 in reader)
                {
                    var row = __loopVar_0;
#line (16, 9) - (16, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                    rows.Append(row);
                }

#line (17, 5) - (17, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(rows));
            }

            [Xunit.FactAttribute]
            public void TestReaderSingleFieldReturnsSingleElementRow()
            {
#line (21, 5) - (21, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                var reader = csv.Reader(new Sharpy.List<string>() { "hello" });
#line (22, 5) - (22, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Sharpy.List<Sharpy.List<string>> rows = new Sharpy.List<Sharpy.List<string>>()
                {
                };
#line (23, 5) - (25, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                foreach (var __loopVar_1 in reader)
                {
                    var row = __loopVar_1;
#line (24, 9) - (24, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                    rows.Append(row);
                }

#line (25, 5) - (25, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(rows));
#line (26, 5) - (26, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(rows[0]));
#line (27, 5) - (27, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Xunit.Assert.Equal("hello", rows[0][0]);
            }

            [Xunit.FactAttribute]
            public void TestReaderEmptyFieldsParsesMiddleEmptyField()
            {
#line (32, 5) - (32, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                var reader = csv.Reader(new Sharpy.List<string>() { "a,,b" });
#line (33, 5) - (33, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Sharpy.List<Sharpy.List<string>> rows = new Sharpy.List<Sharpy.List<string>>()
                {
                };
#line (34, 5) - (36, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                foreach (var __loopVar_2 in reader)
                {
                    var row = __loopVar_2;
#line (35, 9) - (35, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                    rows.Append(row);
                }

#line (36, 5) - (36, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(rows[0]));
#line (37, 5) - (37, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Xunit.Assert.Equal("a", rows[0][0]);
#line (38, 5) - (38, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Xunit.Assert.Equal("", rows[0][1]);
#line (39, 5) - (39, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Xunit.Assert.Equal("b", rows[0][2]);
            }

            [Xunit.FactAttribute]
            public void TestReaderTrailingCommaProducesEmptyLastField()
            {
#line (44, 5) - (44, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                var reader = csv.Reader(new Sharpy.List<string>() { "a,b," });
#line (45, 5) - (45, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Sharpy.List<Sharpy.List<string>> rows = new Sharpy.List<Sharpy.List<string>>()
                {
                };
#line (46, 5) - (48, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                foreach (var __loopVar_3 in reader)
                {
                    var row = __loopVar_3;
#line (47, 9) - (47, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                    rows.Append(row);
                }

#line (48, 5) - (48, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(rows[0]));
#line (49, 5) - (49, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Xunit.Assert.Equal("", rows[0][2]);
            }

            [Xunit.FactAttribute]
            public void TestReaderLeadingCommaProducesEmptyFirstField()
            {
#line (54, 5) - (54, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                var reader = csv.Reader(new Sharpy.List<string>() { ",a,b" });
#line (55, 5) - (55, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Sharpy.List<Sharpy.List<string>> rows = new Sharpy.List<Sharpy.List<string>>()
                {
                };
#line (56, 5) - (58, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                foreach (var __loopVar_4 in reader)
                {
                    var row = __loopVar_4;
#line (57, 9) - (57, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                    rows.Append(row);
                }

#line (58, 5) - (58, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(rows[0]));
#line (59, 5) - (59, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Xunit.Assert.Equal("", rows[0][0]);
#line (60, 5) - (60, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Xunit.Assert.Equal("a", rows[0][1]);
            }

            [Xunit.FactAttribute]
            public void TestReaderAllEmptySingleRowWithOneEmptyField()
            {
#line (65, 5) - (65, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                var reader = csv.Reader(new Sharpy.List<string>() { "" });
#line (66, 5) - (66, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Sharpy.List<Sharpy.List<string>> rows = new Sharpy.List<Sharpy.List<string>>()
                {
                };
#line (67, 5) - (69, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                foreach (var __loopVar_5 in reader)
                {
                    var row = __loopVar_5;
#line (68, 9) - (68, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                    rows.Append(row);
                }

#line (69, 5) - (69, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(rows));
#line (70, 5) - (70, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(rows[0]));
#line (71, 5) - (71, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Xunit.Assert.Equal("", rows[0][0]);
            }

            [Xunit.FactAttribute]
            public void TestReaderQuotedFieldContainingCommaIsOneField()
            {
#line (75, 5) - (75, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                var reader = csv.Reader(new Sharpy.List<string>() { "\"a,b\",c" });
#line (76, 5) - (76, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Sharpy.List<Sharpy.List<string>> rows = new Sharpy.List<Sharpy.List<string>>()
                {
                };
#line (77, 5) - (79, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                foreach (var __loopVar_6 in reader)
                {
                    var row = __loopVar_6;
#line (78, 9) - (78, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                    rows.Append(row);
                }

#line (79, 5) - (79, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(rows[0]));
#line (80, 5) - (80, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Xunit.Assert.Equal("a,b", rows[0][0]);
#line (81, 5) - (81, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Xunit.Assert.Equal("c", rows[0][1]);
            }

            [Xunit.FactAttribute]
            public void TestReaderQuotedFieldWithDoubleQuoteUnescapesQuote()
            {
#line (86, 5) - (86, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                var reader = csv.Reader(new Sharpy.List<string>() { "\"he said \"\"hi\"\"\"" });
#line (87, 5) - (87, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Sharpy.List<Sharpy.List<string>> rows = new Sharpy.List<Sharpy.List<string>>()
                {
                };
#line (88, 5) - (90, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                foreach (var __loopVar_7 in reader)
                {
                    var row = __loopVar_7;
#line (89, 9) - (89, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                    rows.Append(row);
                }

#line (90, 5) - (90, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Xunit.Assert.Equal("he said \"hi\"", rows[0][0]);
            }

            [Xunit.FactAttribute]
            public void TestReaderLineNumStartsAtZero()
            {
#line (94, 5) - (94, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                var reader = csv.Reader(new Sharpy.List<string>() { "a,b", "c,d" });
#line (95, 5) - (95, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Xunit.Assert.Equal(0, reader.LineNum);
            }

            [Xunit.FactAttribute]
            public void TestReaderLineNumIncrementsDuringIteration()
            {
#line (99, 5) - (99, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                var reader = csv.Reader(new Sharpy.List<string>() { "a,b", "c,d", "e,f" });
#line (100, 5) - (100, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Sharpy.List<int> lineNums = new Sharpy.List<int>()
                {
                };
#line (101, 5) - (103, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                foreach (var __loopVar_8 in reader)
                {
                    var row = __loopVar_8;
#line (102, 9) - (102, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                    lineNums.Append(reader.LineNum);
                }

#line (103, 5) - (103, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 1, 2, 3 }, lineNums);
            }

            [Xunit.FactAttribute]
            public void TestWriterEmptyRowWritesNewlineOnly()
            {
#line (109, 5) - (109, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                var sw = new global::Sharpy.StringIO();
#line (110, 5) - (110, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                var writer = csv.Writer(sw);
#line (111, 5) - (111, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Sharpy.List<string> empty = new Sharpy.List<string>()
                {
                };
#line (112, 5) - (112, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                writer.Writerow(empty);
#line (113, 5) - (113, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Xunit.Assert.Equal("\n", sw.Getvalue());
            }

            [Xunit.FactAttribute]
            public void TestWriterFieldWithQuoteEscapesQuote()
            {
#line (117, 5) - (117, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                var sw = new global::Sharpy.StringIO();
#line (118, 5) - (118, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                var writer = csv.Writer(sw);
#line (119, 5) - (119, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                writer.Writerow(new Sharpy.List<string>() { "say \"hello\"" });
#line (121, 5) - (121, 55) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Xunit.Assert.Equal("\"say \"\"hello\"\"\"\n", sw.Getvalue());
            }

            [Xunit.FactAttribute]
            public void TestWriterFieldWithNewlineQuotesField()
            {
#line (125, 5) - (125, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                var sw = new global::Sharpy.StringIO();
#line (126, 5) - (126, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                var writer = csv.Writer(sw);
#line (127, 5) - (127, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                writer.Writerow(new Sharpy.List<string>() { "line1\nline2" });
#line (128, 5) - (128, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                string output = sw.Getvalue();
#line (129, 5) - (129, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Xunit.Assert.Equal("\"", global::Sharpy.StringHelpers.GetItem(output, 0));
#line (130, 5) - (130, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Xunit.Assert.Contains("line1\nline2", output);
            }

            [Xunit.FactAttribute]
            public void TestWriterPlainFieldNotQuoted()
            {
#line (134, 5) - (134, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                var sw = new global::Sharpy.StringIO();
#line (135, 5) - (135, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                var writer = csv.Writer(sw);
#line (136, 5) - (136, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                writer.Writerow(new Sharpy.List<string>() { "simple" });
#line (137, 5) - (137, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Xunit.Assert.Equal("simple\n", sw.Getvalue());
            }

            [Xunit.FactAttribute]
            public void TestWriterWriterowsEmptyListWritesNothing()
            {
#line (141, 5) - (141, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                var sw = new global::Sharpy.StringIO();
#line (142, 5) - (142, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                var writer = csv.Writer(sw);
#line (143, 5) - (143, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Sharpy.List<Sharpy.List<string>> empty = new Sharpy.List<Sharpy.List<string>>()
                {
                };
#line (144, 5) - (144, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                writer.Writerows(empty);
#line (145, 5) - (145, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Xunit.Assert.Equal("", sw.Getvalue());
            }

            [Xunit.FactAttribute]
            public void TestWriterRoundTripWriteAndReadBack()
            {
#line (150, 5) - (150, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                var sw = new global::Sharpy.StringIO();
#line (151, 5) - (151, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                var writer = csv.Writer(sw);
#line (152, 5) - (152, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                writer.Writerow(new Sharpy.List<string>() { "name", "city" });
#line (153, 5) - (153, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                writer.Writerow(new Sharpy.List<string>() { "Alice", "New York" });
#line (154, 5) - (154, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                writer.Writerow(new Sharpy.List<string>() { "Bob", "San Francisco, CA" });
#line (155, 5) - (155, 80) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Sharpy.List<string> lines = new Sharpy.List<string>(sw.Getvalue().Split("\n").Where((string ln) => ln.Length > 0).Select((string ln) => ln));
#line (156, 5) - (156, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                var reader = csv.Reader(lines);
#line (157, 5) - (157, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Sharpy.List<Sharpy.List<string>> rows = new Sharpy.List<Sharpy.List<string>>()
                {
                };
#line (158, 5) - (160, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                foreach (var __loopVar_9 in reader)
                {
                    var row = __loopVar_9;
#line (159, 9) - (159, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                    rows.Append(row);
                }

#line (160, 5) - (160, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(rows));
#line (161, 5) - (161, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Xunit.Assert.Equal("name", rows[0][0]);
#line (162, 5) - (162, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Xunit.Assert.Equal("city", rows[0][1]);
#line (163, 5) - (163, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Xunit.Assert.Equal("Alice", rows[1][0]);
#line (164, 5) - (164, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Xunit.Assert.Equal("New York", rows[1][1]);
#line (165, 5) - (165, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Xunit.Assert.Equal("Bob", rows[2][0]);
#line (166, 5) - (166, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Xunit.Assert.Equal("San Francisco, CA", rows[2][1]);
            }
        }
    }
}
