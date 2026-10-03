// Generated from src/Sharpy.Stdlib.Tests/Spy — do not edit directly.
// To regenerate: bash build_tools/regenerate_spy_tests.sh
#nullable enable

#pragma warning disable xUnit2009, xUnit2017

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Sharpy.Stdlib.Tests.Spy;
using csv = global::Sharpy.CsvModule.CsvModuleModule;
using Xunit;

namespace Sharpy.Stdlib.Tests.Spy.CSV.CsvDictTests
{
    [global::Sharpy.SharpyModule("csv.csv_dict_tests")]
    public static partial class CsvDictTestsModule
    {
    }

    public partial class CsvDictTestsModuleTests
    {
        [Xunit.FactAttribute]
        public void TestDictReaderAutoDetectsFieldnamesFromFirstRow()
        {
#line (12, 5) - (12, 55) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            var reader = csv.DictReader(new Sharpy.List<string>() { "name,age", "Alice,30" });
#line (13, 5) - (13, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            Sharpy.List<Sharpy.Dict<string, string>> rows = new Sharpy.List<Sharpy.Dict<string, string>>()
#line hidden
            {
            };
#line (14, 5) - (15, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            foreach (var __loopVar_0 in reader)
#line hidden
            {
                var row = __loopVar_0;
#line (15, 9) - (15, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                rows.Append(row);
#line hidden
            }

#line (16, 5) - (16, 49) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            Sharpy.List<string>? names = reader.Fieldnames;
#line (17, 5) - (17, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            Xunit.Assert.NotNull(names);
#line (18, 5) - (18, 31) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            Xunit.Assert.Equal("name", names![0]);
#line (19, 5) - (19, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            Xunit.Assert.Equal("age", names![1]);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDictReaderAutoDetectRowHasCorrectValues()
        {
#line (23, 5) - (23, 65) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            var reader = csv.DictReader(new Sharpy.List<string>() { "name,age", "Alice,30", "Bob,25" });
#line (24, 5) - (24, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            Sharpy.List<Sharpy.Dict<string, string>> rows = new Sharpy.List<Sharpy.Dict<string, string>>()
#line hidden
            {
            };
#line (25, 5) - (26, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            foreach (var __loopVar_1 in reader)
#line hidden
            {
                var row = __loopVar_1;
#line (26, 9) - (26, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                rows.Append(row);
#line hidden
            }

#line (27, 5) - (27, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(rows));
#line (28, 5) - (28, 39) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            Xunit.Assert.Equal("Alice", rows.GetItemUnchecked(0)["name"]);
#line (29, 5) - (29, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            Xunit.Assert.Equal("30", rows.GetItemUnchecked(0)["age"]);
#line (30, 5) - (30, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            Xunit.Assert.Equal("Bob", rows.GetItemUnchecked(1)["name"]);
#line (31, 5) - (31, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            Xunit.Assert.Equal("25", rows.GetItemUnchecked(1)["age"]);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDictReaderExplicitFieldnamesFirstRowIsData()
        {
#line (36, 5) - (36, 70) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            var reader = csv.DictReader(new Sharpy.List<string>() { "Alice,30", "Bob,25" }, new Sharpy.List<string>() { "name", "age" });
#line (37, 5) - (37, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            Sharpy.List<Sharpy.Dict<string, string>> rows = new Sharpy.List<Sharpy.Dict<string, string>>()
#line hidden
            {
            };
#line (38, 5) - (39, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            foreach (var __loopVar_2 in reader)
#line hidden
            {
                var row = __loopVar_2;
#line (39, 9) - (39, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                rows.Append(row);
#line hidden
            }

#line (40, 5) - (40, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(rows));
#line (41, 5) - (41, 39) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            Xunit.Assert.Equal("Alice", rows.GetItemUnchecked(0)["name"]);
#line (42, 5) - (42, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            Xunit.Assert.Equal("30", rows.GetItemUnchecked(0)["age"]);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDictReaderExplicitFieldnamesAccessibleBeforeIteration()
        {
#line (46, 5) - (46, 61) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            var reader = csv.DictReader(new Sharpy.List<string>() { "1,2" }, fieldnames: new Sharpy.List<string>() { "x", "y" });
#line (48, 5) - (48, 49) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            Sharpy.List<string>? names = reader.Fieldnames;
#line (49, 5) - (49, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            Xunit.Assert.NotNull(names);
#line (50, 5) - (50, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            Xunit.Assert.Equal("x", names![0]);
#line (51, 5) - (51, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            Xunit.Assert.Equal("y", names![1]);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDictReaderMissingFieldProducesEmptyString()
        {
#line (56, 5) - (56, 60) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            var reader = csv.DictReader(new Sharpy.List<string>() { "name,age,city", "Alice,30" });
#line (57, 5) - (57, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            Sharpy.List<Sharpy.Dict<string, string>> rows = new Sharpy.List<Sharpy.Dict<string, string>>()
#line hidden
            {
            };
#line (58, 5) - (59, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            foreach (var __loopVar_3 in reader)
#line hidden
            {
                var row = __loopVar_3;
#line (59, 9) - (59, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                rows.Append(row);
#line hidden
            }

#line (60, 5) - (60, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(rows));
#line (61, 5) - (61, 39) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            Xunit.Assert.Equal("Alice", rows.GetItemUnchecked(0)["name"]);
#line (62, 5) - (62, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            Xunit.Assert.Equal("30", rows.GetItemUnchecked(0)["age"]);
#line (63, 5) - (63, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            Xunit.Assert.Equal("", rows.GetItemUnchecked(0)["city"]);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDictReaderExtraFieldsAreDropped()
        {
#line (68, 5) - (68, 67) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            var reader = csv.DictReader(new Sharpy.List<string>() { "name,age", "Alice,30,extra_value" });
#line (69, 5) - (69, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            Sharpy.List<Sharpy.Dict<string, string>> rows = new Sharpy.List<Sharpy.Dict<string, string>>()
#line hidden
            {
            };
#line (70, 5) - (71, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            foreach (var __loopVar_4 in reader)
#line hidden
            {
                var row = __loopVar_4;
#line (71, 9) - (71, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                rows.Append(row);
#line hidden
            }

#line (72, 5) - (72, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(rows));
#line (73, 5) - (73, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(rows.GetItemUnchecked(0)));
#line (74, 5) - (74, 41) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            Xunit.Assert.True(rows.GetItemUnchecked(0).ContainsKey("name"));
#line (75, 5) - (75, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            Xunit.Assert.True(rows.GetItemUnchecked(0).ContainsKey("age"));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDictReaderQuotedFieldsParsedCorrectly()
        {
#line (79, 5) - (79, 70) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            var reader = csv.DictReader(new Sharpy.List<string>() { "name,desc", "Alice,\"hello, world\"" });
#line (80, 5) - (80, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            Sharpy.List<Sharpy.Dict<string, string>> rows = new Sharpy.List<Sharpy.Dict<string, string>>()
#line hidden
            {
            };
#line (81, 5) - (82, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            foreach (var __loopVar_5 in reader)
#line hidden
            {
                var row = __loopVar_5;
#line (82, 9) - (82, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                rows.Append(row);
#line hidden
            }

#line (83, 5) - (83, 46) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            Xunit.Assert.Equal("hello, world", rows.GetItemUnchecked(0)["desc"]);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDictReaderSingleColumnWorks()
        {
#line (87, 5) - (87, 52) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            var reader = csv.DictReader(new Sharpy.List<string>() { "value", "42", "99" });
#line (88, 5) - (88, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            Sharpy.List<Sharpy.Dict<string, string>> rows = new Sharpy.List<Sharpy.Dict<string, string>>()
#line hidden
            {
            };
#line (89, 5) - (90, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            foreach (var __loopVar_6 in reader)
#line hidden
            {
                var row = __loopVar_6;
#line (90, 9) - (90, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                rows.Append(row);
#line hidden
            }

#line (91, 5) - (91, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(rows));
#line (92, 5) - (92, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            Xunit.Assert.Equal("42", rows.GetItemUnchecked(0)["value"]);
#line (93, 5) - (93, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            Xunit.Assert.Equal("99", rows.GetItemUnchecked(1)["value"]);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDictWriterFieldnamesPropertyReturnsFieldnames()
        {
#line (99, 5) - (99, 23) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            var sw = new global::Sharpy.StringIO();
#line (100, 5) - (100, 50) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            var writer = csv.DictWriter(sw, new Sharpy.List<string>() { "name", "age" });
#line (101, 5) - (101, 43) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            Xunit.Assert.Equal("name", writer.Fieldnames[0]);
#line (102, 5) - (102, 42) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            Xunit.Assert.Equal("age", writer.Fieldnames[1]);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDictWriterWriteheaderWritesFieldnames()
        {
#line (106, 5) - (106, 23) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            var sw = new global::Sharpy.StringIO();
#line (107, 5) - (107, 58) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            var writer = csv.DictWriter(sw, new Sharpy.List<string>() { "name", "age", "city" });
#line (108, 5) - (108, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            writer.Writeheader();
#line (109, 5) - (109, 47) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            Xunit.Assert.Equal("name,age,city\n", sw.Getvalue());
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDictWriterWriterowWritesValuesInFieldOrder()
        {
#line (113, 5) - (113, 23) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            var sw = new global::Sharpy.StringIO();
#line (114, 5) - (114, 50) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            var writer = csv.DictWriter(sw, new Sharpy.List<string>() { "name", "age" });
#line (115, 5) - (115, 58) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            Sharpy.Dict<string, string> row = new Sharpy.Dict<string, string>()
#line hidden
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
#line (116, 5) - (116, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            writer.Writerow(row);
#line (117, 5) - (117, 42) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            Xunit.Assert.Equal("Alice,30\n", sw.Getvalue());
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDictWriterWriterowMissingKeyWritesEmptyString()
        {
#line (121, 5) - (121, 23) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            var sw = new global::Sharpy.StringIO();
#line (122, 5) - (122, 58) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            var writer = csv.DictWriter(sw, new Sharpy.List<string>() { "name", "age", "city" });
#line (124, 5) - (124, 58) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            Sharpy.Dict<string, string> row = new Sharpy.Dict<string, string>()
#line hidden
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
#line (125, 5) - (125, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            writer.Writerow(row);
#line (126, 5) - (126, 43) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            Xunit.Assert.Equal("Alice,30,\n", sw.Getvalue());
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDictWriterWriterowFieldWithCommaIsQuoted()
        {
#line (130, 5) - (130, 23) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            var sw = new global::Sharpy.StringIO();
#line (131, 5) - (131, 54) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            var writer = csv.DictWriter(sw, new Sharpy.List<string>() { "name", "address" });
#line (132, 5) - (132, 84) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            Sharpy.Dict<string, string> row = new Sharpy.Dict<string, string>()
#line hidden
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
#line (133, 5) - (133, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            writer.Writerow(row);
#line (134, 5) - (134, 60) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            Xunit.Assert.True(sw.Getvalue().Contains("\"123 Main St, Springfield\""));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDictWriterWriterowsWritesMultipleRows()
        {
#line (138, 5) - (138, 23) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            var sw = new global::Sharpy.StringIO();
#line (139, 5) - (139, 50) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            var writer = csv.DictWriter(sw, new Sharpy.List<string>() { "name", "age" });
#line (140, 5) - (140, 97) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            Sharpy.List<Sharpy.Dict<string, string>> rows = new Sharpy.List<Sharpy.Dict<string, string>>()
#line hidden
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
#line (141, 5) - (141, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            writer.Writerows(rows);
#line (142, 5) - (142, 50) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            Xunit.Assert.Equal("Alice,30\nBob,25\n", sw.Getvalue());
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDictWriterWriteheaderThenRowsProducesFullCsv()
        {
#line (146, 5) - (146, 23) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            var sw = new global::Sharpy.StringIO();
#line (147, 5) - (147, 52) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            var writer = csv.DictWriter(sw, new Sharpy.List<string>() { "name", "score" });
#line (148, 5) - (148, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            writer.Writeheader();
#line (149, 5) - (149, 61) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            Sharpy.Dict<string, string> row = new Sharpy.Dict<string, string>()
#line hidden
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
#line (150, 5) - (150, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            writer.Writerow(row);
#line (151, 5) - (151, 55) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            Xunit.Assert.Equal("name,score\nAlice,100\n", sw.Getvalue());
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDictWriterRoundTripDictWriterThenDictReader()
        {
#line (155, 5) - (155, 23) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            var sw = new global::Sharpy.StringIO();
#line (156, 5) - (156, 50) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            var writer = csv.DictWriter(sw, new Sharpy.List<string>() { "name", "age" });
#line (157, 5) - (157, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            writer.Writeheader();
#line (158, 5) - (158, 59) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            Sharpy.Dict<string, string> row1 = new Sharpy.Dict<string, string>()
#line hidden
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
#line (159, 5) - (159, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            writer.Writerow(row1);
#line hidden
            Sharpy.List<string> __src_8 = global::Sharpy.StringExtensions.Split(sw.Getvalue(), "\n");
            var __comp_7 = new Sharpy.List<string>(((global::Sharpy.ISized)__src_8).Count);
            foreach (var __loopVar_9 in __src_8)
            {
                var ln = __loopVar_9;
                if (ln.Length > 0)
                {
                    __comp_7.Add(ln);
                }
            }

#line (160, 5) - (160, 80) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            Sharpy.List<string> lines = __comp_7;
#line (161, 5) - (161, 36) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            var reader = csv.DictReader(lines);
#line (162, 5) - (162, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            Sharpy.List<Sharpy.Dict<string, string>> rows = new Sharpy.List<Sharpy.Dict<string, string>>()
#line hidden
            {
            };
#line (163, 5) - (164, 23) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            foreach (var __loopVar_10 in reader)
#line hidden
            {
                var r = __loopVar_10;
#line (164, 9) - (164, 23) 16 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
                rows.Append(r);
#line hidden
            }

#line (165, 5) - (165, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(rows));
#line (166, 5) - (166, 39) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            Xunit.Assert.Equal("Alice", rows.GetItemUnchecked(0)["name"]);
#line (167, 5) - (167, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/csv/csv_dict_tests.spy"
            Xunit.Assert.Equal("30", rows.GetItemUnchecked(0)["age"]);
#line hidden
        }
    }
}
#line default
