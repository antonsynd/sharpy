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
using static Sharpy.Stdlib.Tests.Spy.CSV.CsvDictTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class CSV
    {
        [global::Sharpy.SharpyModule("csv.csv_dict_tests")]
        public static partial class CsvDictTests
        {
        }
    }

    public static partial class CSV
    {
        public partial class CsvDictTestsTests
        {
            [Xunit.FactAttribute]
            public void TestDictReaderAutoDetectsFieldnamesFromFirstRow()
            {
#line (12, 5) - (12, 55) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                var reader = csv.DictReader(new Sharpy.List<string>() { "name,age", "Alice,30" });
#line (13, 5) - (13, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                Sharpy.List<Sharpy.Dict<string, string>> rows = new Sharpy.List<Sharpy.Dict<string, string>>()
                {
                };
#line (14, 5) - (16, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                foreach (var __loopVar_0 in reader)
                {
                    var row = __loopVar_0;
#line (15, 9) - (15, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                    rows.Append(row);
                }

#line (16, 5) - (16, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                Xunit.Assert.True(reader.Fieldnames.IsSome);
#line (17, 5) - (17, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                Xunit.Assert.Equal("name", reader.Fieldnames.Unwrap()[0]);
#line (18, 5) - (18, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                Xunit.Assert.Equal("age", reader.Fieldnames.Unwrap()[1]);
            }

            [Xunit.FactAttribute]
            public void TestDictReaderAutoDetectRowHasCorrectValues()
            {
#line (22, 5) - (22, 65) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                var reader = csv.DictReader(new Sharpy.List<string>() { "name,age", "Alice,30", "Bob,25" });
#line (23, 5) - (23, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                Sharpy.List<Sharpy.Dict<string, string>> rows = new Sharpy.List<Sharpy.Dict<string, string>>()
                {
                };
#line (24, 5) - (26, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                foreach (var __loopVar_1 in reader)
                {
                    var row = __loopVar_1;
#line (25, 9) - (25, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                    rows.Append(row);
                }

#line (26, 5) - (26, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(rows));
#line (27, 5) - (27, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                Xunit.Assert.Equal("Alice", rows[0]["name"]);
#line (28, 5) - (28, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                Xunit.Assert.Equal("30", rows[0]["age"]);
#line (29, 5) - (29, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                Xunit.Assert.Equal("Bob", rows[1]["name"]);
#line (30, 5) - (30, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                Xunit.Assert.Equal("25", rows[1]["age"]);
            }

            [Xunit.FactAttribute]
            public void TestDictReaderExplicitFieldnamesFirstRowIsData()
            {
#line (35, 5) - (35, 70) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                var reader = csv.DictReader(new Sharpy.List<string>() { "Alice,30", "Bob,25" }, new Sharpy.List<string>() { "name", "age" });
#line (36, 5) - (36, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                Sharpy.List<Sharpy.Dict<string, string>> rows = new Sharpy.List<Sharpy.Dict<string, string>>()
                {
                };
#line (37, 5) - (39, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                foreach (var __loopVar_2 in reader)
                {
                    var row = __loopVar_2;
#line (38, 9) - (38, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                    rows.Append(row);
                }

#line (39, 5) - (39, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(rows));
#line (40, 5) - (40, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                Xunit.Assert.Equal("Alice", rows[0]["name"]);
#line (41, 5) - (41, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                Xunit.Assert.Equal("30", rows[0]["age"]);
            }

            [Xunit.FactAttribute]
            public void TestDictReaderExplicitFieldnamesAccessibleBeforeIteration()
            {
#line (45, 5) - (45, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                var reader = csv.DictReader(new Sharpy.List<string>() { "1,2" }, new Sharpy.List<string>() { "x", "y" });
#line (47, 5) - (47, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                Xunit.Assert.True(reader.Fieldnames.IsSome);
#line (48, 5) - (48, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                Xunit.Assert.Equal("x", reader.Fieldnames.Unwrap()[0]);
#line (49, 5) - (49, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                Xunit.Assert.Equal("y", reader.Fieldnames.Unwrap()[1]);
            }

            [Xunit.FactAttribute]
            public void TestDictReaderMissingFieldProducesEmptyString()
            {
#line (54, 5) - (54, 60) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                var reader = csv.DictReader(new Sharpy.List<string>() { "name,age,city", "Alice,30" });
#line (55, 5) - (55, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                Sharpy.List<Sharpy.Dict<string, string>> rows = new Sharpy.List<Sharpy.Dict<string, string>>()
                {
                };
#line (56, 5) - (58, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                foreach (var __loopVar_3 in reader)
                {
                    var row = __loopVar_3;
#line (57, 9) - (57, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                    rows.Append(row);
                }

#line (58, 5) - (58, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(rows));
#line (59, 5) - (59, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                Xunit.Assert.Equal("Alice", rows[0]["name"]);
#line (60, 5) - (60, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                Xunit.Assert.Equal("30", rows[0]["age"]);
#line (61, 5) - (61, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                Xunit.Assert.Equal("", rows[0]["city"]);
            }

            [Xunit.FactAttribute]
            public void TestDictReaderExtraFieldsAreDropped()
            {
#line (66, 5) - (66, 67) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                var reader = csv.DictReader(new Sharpy.List<string>() { "name,age", "Alice,30,extra_value" });
#line (67, 5) - (67, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                Sharpy.List<Sharpy.Dict<string, string>> rows = new Sharpy.List<Sharpy.Dict<string, string>>()
                {
                };
#line (68, 5) - (70, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                foreach (var __loopVar_4 in reader)
                {
                    var row = __loopVar_4;
#line (69, 9) - (69, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                    rows.Append(row);
                }

#line (70, 5) - (70, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(rows));
#line (71, 5) - (71, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(rows[0]));
#line (72, 5) - (72, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                Xunit.Assert.True(rows[0].ContainsKey("name"));
#line (73, 5) - (73, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                Xunit.Assert.True(rows[0].ContainsKey("age"));
            }

            [Xunit.FactAttribute]
            public void TestDictReaderQuotedFieldsParsedCorrectly()
            {
#line (77, 5) - (77, 70) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                var reader = csv.DictReader(new Sharpy.List<string>() { "name,desc", "Alice,\"hello, world\"" });
#line (78, 5) - (78, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                Sharpy.List<Sharpy.Dict<string, string>> rows = new Sharpy.List<Sharpy.Dict<string, string>>()
                {
                };
#line (79, 5) - (81, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                foreach (var __loopVar_5 in reader)
                {
                    var row = __loopVar_5;
#line (80, 9) - (80, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                    rows.Append(row);
                }

#line (81, 5) - (81, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                Xunit.Assert.Equal("hello, world", rows[0]["desc"]);
            }

            [Xunit.FactAttribute]
            public void TestDictReaderSingleColumnWorks()
            {
#line (85, 5) - (85, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                var reader = csv.DictReader(new Sharpy.List<string>() { "value", "42", "99" });
#line (86, 5) - (86, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                Sharpy.List<Sharpy.Dict<string, string>> rows = new Sharpy.List<Sharpy.Dict<string, string>>()
                {
                };
#line (87, 5) - (89, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                foreach (var __loopVar_6 in reader)
                {
                    var row = __loopVar_6;
#line (88, 9) - (88, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                    rows.Append(row);
                }

#line (89, 5) - (89, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(rows));
#line (90, 5) - (90, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                Xunit.Assert.Equal("42", rows[0]["value"]);
#line (91, 5) - (91, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                Xunit.Assert.Equal("99", rows[1]["value"]);
            }

            [Xunit.FactAttribute]
            public void TestDictWriterFieldnamesPropertyReturnsFieldnames()
            {
#line (97, 5) - (97, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                var sw = new global::Sharpy.StringIO();
#line (98, 5) - (98, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                var writer = csv.DictWriter(sw, new Sharpy.List<string>() { "name", "age" });
#line (99, 5) - (99, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                Xunit.Assert.Equal("name", writer.Fieldnames[0]);
#line (100, 5) - (100, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                Xunit.Assert.Equal("age", writer.Fieldnames[1]);
            }

            [Xunit.FactAttribute]
            public void TestDictWriterWriteheaderWritesFieldnames()
            {
#line (104, 5) - (104, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                var sw = new global::Sharpy.StringIO();
#line (105, 5) - (105, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                var writer = csv.DictWriter(sw, new Sharpy.List<string>() { "name", "age", "city" });
#line (106, 5) - (106, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                writer.Writeheader();
#line (107, 5) - (107, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                Xunit.Assert.Equal("name,age,city\n", sw.Getvalue());
            }

            [Xunit.FactAttribute]
            public void TestDictWriterWriterowWritesValuesInFieldOrder()
            {
#line (111, 5) - (111, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                var sw = new global::Sharpy.StringIO();
#line (112, 5) - (112, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                var writer = csv.DictWriter(sw, new Sharpy.List<string>() { "name", "age" });
#line (113, 5) - (113, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                Sharpy.Dict<string, string> row = new Sharpy.Dict<string, string>()
                {
                    {
                        "name",
                        "Alice"
                    },
                    {
                        "age",
                        "30"
                    }
                };
#line (114, 5) - (114, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                writer.Writerow(row);
#line (115, 5) - (115, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                Xunit.Assert.Equal("Alice,30\n", sw.Getvalue());
            }

            [Xunit.FactAttribute]
            public void TestDictWriterWriterowMissingKeyWritesEmptyString()
            {
#line (119, 5) - (119, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                var sw = new global::Sharpy.StringIO();
#line (120, 5) - (120, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                var writer = csv.DictWriter(sw, new Sharpy.List<string>() { "name", "age", "city" });
#line (122, 5) - (122, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                Sharpy.Dict<string, string> row = new Sharpy.Dict<string, string>()
                {
                    {
                        "name",
                        "Alice"
                    },
                    {
                        "age",
                        "30"
                    }
                };
#line (123, 5) - (123, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                writer.Writerow(row);
#line (124, 5) - (124, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                Xunit.Assert.Equal("Alice,30,\n", sw.Getvalue());
            }

            [Xunit.FactAttribute]
            public void TestDictWriterWriterowFieldWithCommaIsQuoted()
            {
#line (128, 5) - (128, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                var sw = new global::Sharpy.StringIO();
#line (129, 5) - (129, 54) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                var writer = csv.DictWriter(sw, new Sharpy.List<string>() { "name", "address" });
#line (130, 5) - (130, 84) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                Sharpy.Dict<string, string> row = new Sharpy.Dict<string, string>()
                {
                    {
                        "name",
                        "Alice"
                    },
                    {
                        "address",
                        "123 Main St, Springfield"
                    }
                };
#line (131, 5) - (131, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                writer.Writerow(row);
#line (132, 5) - (132, 60) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                Xunit.Assert.Contains("\"123 Main St, Springfield\"", sw.Getvalue());
            }

            [Xunit.FactAttribute]
            public void TestDictWriterWriterowsWritesMultipleRows()
            {
#line (136, 5) - (136, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                var sw = new global::Sharpy.StringIO();
#line (137, 5) - (137, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                var writer = csv.DictWriter(sw, new Sharpy.List<string>() { "name", "age" });
#line (138, 5) - (138, 97) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                Sharpy.List<Sharpy.Dict<string, string>> rows = new Sharpy.List<Sharpy.Dict<string, string>>()
                {
                    new Sharpy.Dict<string, string>()
                    {
                        {
                            "name",
                            "Alice"
                        },
                        {
                            "age",
                            "30"
                        }
                    },
                    new Sharpy.Dict<string, string>()
                    {
                        {
                            "name",
                            "Bob"
                        },
                        {
                            "age",
                            "25"
                        }
                    }
                };
#line (139, 5) - (139, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                writer.Writerows(rows);
#line (140, 5) - (140, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                Xunit.Assert.Equal("Alice,30\nBob,25\n", sw.Getvalue());
            }

            [Xunit.FactAttribute]
            public void TestDictWriterWriteheaderThenRowsProducesFullCsv()
            {
#line (144, 5) - (144, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                var sw = new global::Sharpy.StringIO();
#line (145, 5) - (145, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                var writer = csv.DictWriter(sw, new Sharpy.List<string>() { "name", "score" });
#line (146, 5) - (146, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                writer.Writeheader();
#line (147, 5) - (147, 61) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                Sharpy.Dict<string, string> row = new Sharpy.Dict<string, string>()
                {
                    {
                        "name",
                        "Alice"
                    },
                    {
                        "score",
                        "100"
                    }
                };
#line (148, 5) - (148, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                writer.Writerow(row);
#line (149, 5) - (149, 55) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                Xunit.Assert.Equal("name,score\nAlice,100\n", sw.Getvalue());
            }

            [Xunit.FactAttribute]
            public void TestDictWriterRoundTripDictWriterThenDictReader()
            {
#line (153, 5) - (153, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                var sw = new global::Sharpy.StringIO();
#line (154, 5) - (154, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                var writer = csv.DictWriter(sw, new Sharpy.List<string>() { "name", "age" });
#line (155, 5) - (155, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                writer.Writeheader();
#line (156, 5) - (156, 59) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                Sharpy.Dict<string, string> row1 = new Sharpy.Dict<string, string>()
                {
                    {
                        "name",
                        "Alice"
                    },
                    {
                        "age",
                        "30"
                    }
                };
#line (157, 5) - (157, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                writer.Writerow(row1);
#line (158, 5) - (158, 80) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                Sharpy.List<string> lines = new Sharpy.List<string>(sw.Getvalue().Split("\n").Where((string ln) => ln.Length > 0).Select((string ln) => ln));
#line (159, 5) - (159, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                var reader = csv.DictReader(lines);
#line (160, 5) - (160, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                Sharpy.List<Sharpy.Dict<string, string>> rows = new Sharpy.List<Sharpy.Dict<string, string>>()
                {
                };
#line (161, 5) - (163, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                foreach (var __loopVar_7 in reader)
                {
                    var r = __loopVar_7;
#line (162, 9) - (162, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                    rows.Append(r);
                }

#line (163, 5) - (163, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(rows));
#line (164, 5) - (164, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                Xunit.Assert.Equal("Alice", rows[0]["name"]);
#line (165, 5) - (165, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                Xunit.Assert.Equal("30", rows[0]["age"]);
            }
        }
    }
}
