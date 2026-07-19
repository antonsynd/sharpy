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
#line (12, 5) - (12, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Sharpy.List<string> empty = new Sharpy.List<string>()
#line hidden
                {
                };
#line (13, 5) - (13, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                var reader = csv.Reader(empty);
#line (14, 5) - (14, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Sharpy.List<Sharpy.List<string>> rows = new Sharpy.List<Sharpy.List<string>>()
#line hidden
                {
                };
#line (15, 5) - (17, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                foreach (var __loopVar_0 in reader)
#line hidden
                {
                    var row = __loopVar_0;
#line (16, 9) - (16, 25) 20 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                    rows.Append(row);
#line hidden
                }

#line (17, 5) - (17, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(rows));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestReaderSingleFieldReturnsSingleElementRow()
            {
#line (21, 5) - (21, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                var reader = csv.Reader(new Sharpy.List<string>() { "hello" });
#line (22, 5) - (22, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Sharpy.List<Sharpy.List<string>> rows = new Sharpy.List<Sharpy.List<string>>()
#line hidden
                {
                };
#line (23, 5) - (25, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                foreach (var __loopVar_1 in reader)
#line hidden
                {
                    var row = __loopVar_1;
#line (24, 9) - (24, 25) 20 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                    rows.Append(row);
#line hidden
                }

#line (25, 5) - (25, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(rows));
#line (26, 5) - (26, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(rows.GetItemUnchecked(0)));
#line (27, 5) - (27, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Xunit.Assert.Equal("hello", rows.GetItemUnchecked(0)[0]);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestReaderEmptyFieldsParsesMiddleEmptyField()
            {
#line (32, 5) - (32, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                var reader = csv.Reader(new Sharpy.List<string>() { "a,,b" });
#line (33, 5) - (33, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Sharpy.List<Sharpy.List<string>> rows = new Sharpy.List<Sharpy.List<string>>()
#line hidden
                {
                };
#line (34, 5) - (36, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                foreach (var __loopVar_2 in reader)
#line hidden
                {
                    var row = __loopVar_2;
#line (35, 9) - (35, 25) 20 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                    rows.Append(row);
#line hidden
                }

#line (36, 5) - (36, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(rows.GetItemUnchecked(0)));
#line (37, 5) - (37, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Xunit.Assert.Equal("a", rows.GetItemUnchecked(0)[0]);
#line (38, 5) - (38, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Xunit.Assert.Equal("", rows.GetItemUnchecked(0)[1]);
#line (39, 5) - (39, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Xunit.Assert.Equal("b", rows.GetItemUnchecked(0)[2]);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestReaderTrailingCommaProducesEmptyLastField()
            {
#line (44, 5) - (44, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                var reader = csv.Reader(new Sharpy.List<string>() { "a,b," });
#line (45, 5) - (45, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Sharpy.List<Sharpy.List<string>> rows = new Sharpy.List<Sharpy.List<string>>()
#line hidden
                {
                };
#line (46, 5) - (48, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                foreach (var __loopVar_3 in reader)
#line hidden
                {
                    var row = __loopVar_3;
#line (47, 9) - (47, 25) 20 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                    rows.Append(row);
#line hidden
                }

#line (48, 5) - (48, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(rows.GetItemUnchecked(0)));
#line (49, 5) - (49, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Xunit.Assert.Equal("", rows.GetItemUnchecked(0)[2]);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestReaderLeadingCommaProducesEmptyFirstField()
            {
#line (54, 5) - (54, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                var reader = csv.Reader(new Sharpy.List<string>() { ",a,b" });
#line (55, 5) - (55, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Sharpy.List<Sharpy.List<string>> rows = new Sharpy.List<Sharpy.List<string>>()
#line hidden
                {
                };
#line (56, 5) - (58, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                foreach (var __loopVar_4 in reader)
#line hidden
                {
                    var row = __loopVar_4;
#line (57, 9) - (57, 25) 20 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                    rows.Append(row);
#line hidden
                }

#line (58, 5) - (58, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(rows.GetItemUnchecked(0)));
#line (59, 5) - (59, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Xunit.Assert.Equal("", rows.GetItemUnchecked(0)[0]);
#line (60, 5) - (60, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Xunit.Assert.Equal("a", rows.GetItemUnchecked(0)[1]);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestReaderAllEmptySingleRowWithOneEmptyField()
            {
#line (65, 5) - (65, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                var reader = csv.Reader(new Sharpy.List<string>() { "" });
#line (66, 5) - (66, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Sharpy.List<Sharpy.List<string>> rows = new Sharpy.List<Sharpy.List<string>>()
#line hidden
                {
                };
#line (67, 5) - (69, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                foreach (var __loopVar_5 in reader)
#line hidden
                {
                    var row = __loopVar_5;
#line (68, 9) - (68, 25) 20 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                    rows.Append(row);
#line hidden
                }

#line (69, 5) - (69, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(rows));
#line (70, 5) - (70, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(rows.GetItemUnchecked(0)));
#line (71, 5) - (71, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Xunit.Assert.Equal("", rows.GetItemUnchecked(0)[0]);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestReaderQuotedFieldContainingCommaIsOneField()
            {
#line (75, 5) - (75, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                var reader = csv.Reader(new Sharpy.List<string>() { "\"a,b\",c" });
#line (76, 5) - (76, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Sharpy.List<Sharpy.List<string>> rows = new Sharpy.List<Sharpy.List<string>>()
#line hidden
                {
                };
#line (77, 5) - (79, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                foreach (var __loopVar_6 in reader)
#line hidden
                {
                    var row = __loopVar_6;
#line (78, 9) - (78, 25) 20 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                    rows.Append(row);
#line hidden
                }

#line (79, 5) - (79, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(rows.GetItemUnchecked(0)));
#line (80, 5) - (80, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Xunit.Assert.Equal("a,b", rows.GetItemUnchecked(0)[0]);
#line (81, 5) - (81, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Xunit.Assert.Equal("c", rows.GetItemUnchecked(0)[1]);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestReaderQuotedFieldWithDoubleQuoteUnescapesQuote()
            {
#line (86, 5) - (86, 52) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                var reader = csv.Reader(new Sharpy.List<string>() { "\"he said \"\"hi\"\"\"" });
#line (87, 5) - (87, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Sharpy.List<Sharpy.List<string>> rows = new Sharpy.List<Sharpy.List<string>>()
#line hidden
                {
                };
#line (88, 5) - (90, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                foreach (var __loopVar_7 in reader)
#line hidden
                {
                    var row = __loopVar_7;
#line (89, 9) - (89, 25) 20 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                    rows.Append(row);
#line hidden
                }

#line (90, 5) - (90, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Xunit.Assert.Equal("he said \"hi\"", rows.GetItemUnchecked(0)[0]);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestReaderLineNumStartsAtZero()
            {
#line (94, 5) - (94, 40) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                var reader = csv.Reader(new Sharpy.List<string>() { "a,b", "c,d" });
#line (95, 5) - (95, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Xunit.Assert.Equal(0, reader.LineNum);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestReaderLineNumIncrementsDuringIteration()
            {
#line (99, 5) - (99, 47) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                var reader = csv.Reader(new Sharpy.List<string>() { "a,b", "c,d", "e,f" });
#line (100, 5) - (100, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Sharpy.List<int> lineNums = new Sharpy.List<int>()
#line hidden
                {
                };
#line (101, 5) - (103, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                foreach (var __loopVar_8 in reader)
#line hidden
                {
                    var row = __loopVar_8;
#line (102, 9) - (102, 42) 20 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                    lineNums.Append(reader.LineNum);
#line hidden
                }

#line (103, 5) - (103, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 1, 2, 3 }, lineNums);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestWriterEmptyRowWritesNewlineOnly()
            {
#line (109, 5) - (109, 23) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                var sw = new global::Sharpy.StringIO();
#line (110, 5) - (110, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                var writer = csv.Writer(sw);
#line (111, 5) - (111, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Sharpy.List<string> empty = new Sharpy.List<string>()
#line hidden
                {
                };
#line (112, 5) - (112, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                writer.Writerow(empty);
#line (113, 5) - (113, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Xunit.Assert.Equal("\n", sw.Getvalue());
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestWriterFieldWithQuoteEscapesQuote()
            {
#line (117, 5) - (117, 23) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                var sw = new global::Sharpy.StringIO();
#line (118, 5) - (118, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                var writer = csv.Writer(sw);
#line (119, 5) - (119, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                writer.Writerow(new Sharpy.List<string>() { "say \"hello\"" });
#line (121, 5) - (121, 55) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Xunit.Assert.Equal("\"say \"\"hello\"\"\"\n", sw.Getvalue());
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestWriterFieldWithNewlineQuotesField()
            {
#line (125, 5) - (125, 23) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                var sw = new global::Sharpy.StringIO();
#line (126, 5) - (126, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                var writer = csv.Writer(sw);
#line (127, 5) - (127, 38) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                writer.Writerow(new Sharpy.List<string>() { "line1\nline2" });
#line (128, 5) - (128, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                string output = sw.Getvalue();
#line (129, 5) - (129, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Xunit.Assert.Equal("\"", global::Sharpy.StringHelpers.GetItem(output, 0));
#line (130, 5) - (130, 37) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Xunit.Assert.Contains("line1\nline2", output);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestWriterPlainFieldNotQuoted()
            {
#line (134, 5) - (134, 23) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                var sw = new global::Sharpy.StringIO();
#line (135, 5) - (135, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                var writer = csv.Writer(sw);
#line (136, 5) - (136, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                writer.Writerow(new Sharpy.List<string>() { "simple" });
#line (137, 5) - (137, 40) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Xunit.Assert.Equal("simple\n", sw.Getvalue());
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestWriterWriterowsEmptyListWritesNothing()
            {
#line (141, 5) - (141, 23) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                var sw = new global::Sharpy.StringIO();
#line (142, 5) - (142, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                var writer = csv.Writer(sw);
#line (143, 5) - (143, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Sharpy.List<Sharpy.List<string>> empty = new Sharpy.List<Sharpy.List<string>>()
#line hidden
                {
                };
#line (144, 5) - (144, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                writer.Writerows(empty);
#line (145, 5) - (145, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Xunit.Assert.Equal("", sw.Getvalue());
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestWriterRoundTripWriteAndReadBack()
            {
#line (150, 5) - (150, 23) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                var sw = new global::Sharpy.StringIO();
#line (151, 5) - (151, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                var writer = csv.Writer(sw);
#line (152, 5) - (152, 38) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                writer.Writerow(new Sharpy.List<string>() { "name", "city" });
#line (153, 5) - (153, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                writer.Writerow(new Sharpy.List<string>() { "Alice", "New York" });
#line (154, 5) - (154, 50) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                writer.Writerow(new Sharpy.List<string>() { "Bob", "San Francisco, CA" });
#line hidden
                Sharpy.List<string> __src_10 = global::Sharpy.StringExtensions.Split(sw.Getvalue(), "\n");
                var __comp_9 = new Sharpy.List<string>(((global::Sharpy.ISized)__src_10).Count);
                foreach (var __loopVar_11 in __src_10)
                {
                    var ln = __loopVar_11;
                    if (ln.Length > 0)
                    {
                        __comp_9.Add(ln);
                    }
                }

#line (155, 5) - (155, 80) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Sharpy.List<string> lines = __comp_9;
#line (156, 5) - (156, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                var reader = csv.Reader(lines);
#line (157, 5) - (157, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Sharpy.List<Sharpy.List<string>> rows = new Sharpy.List<Sharpy.List<string>>()
#line hidden
                {
                };
#line (158, 5) - (160, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                foreach (var __loopVar_12 in reader)
#line hidden
                {
                    var row = __loopVar_12;
#line (159, 9) - (159, 25) 20 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                    rows.Append(row);
#line hidden
                }

#line (160, 5) - (160, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(rows));
#line (161, 5) - (161, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Xunit.Assert.Equal("name", rows.GetItemUnchecked(0)[0]);
#line (162, 5) - (162, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Xunit.Assert.Equal("city", rows.GetItemUnchecked(0)[1]);
#line (163, 5) - (163, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Xunit.Assert.Equal("Alice", rows.GetItemUnchecked(1)[0]);
#line (164, 5) - (164, 37) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Xunit.Assert.Equal("New York", rows.GetItemUnchecked(1)[1]);
#line (165, 5) - (165, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Xunit.Assert.Equal("Bob", rows.GetItemUnchecked(2)[0]);
#line (166, 5) - (166, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_reader_writer_tests.spy"
                Xunit.Assert.Equal("San Francisco, CA", rows.GetItemUnchecked(2)[1]);
#line hidden
            }
        }
    }
}
#line default
