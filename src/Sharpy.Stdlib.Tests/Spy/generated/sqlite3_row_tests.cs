// Generated from src/Sharpy.Stdlib.Tests/Spy — do not edit directly.
// To regenerate: bash build_tools/regenerate_spy_tests.sh
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Sharpy.Stdlib.Tests.Spy;
using static global::Sharpy.Unittest;
using @operator = global::Sharpy.Operator;
using sqlite3 = global::Sharpy.Sqlite3;
using Xunit;
using static Sharpy.Stdlib.Tests.Spy.Sqlite3.Sqlite3RowTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Sqlite3
    {
        [global::Sharpy.SharpyModule("sqlite3.sqlite3_row_tests")]
        public static partial class Sqlite3RowTests
        {
            internal static bool _EqInt(object value, long expected)
            {
#line (33, 5) - (33, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                return @operator.Eq(value, expected);
            }

            internal static global::Sharpy.Sqlite3Row _MakeRow()
            {
#line (37, 5) - (37, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (38, 5) - (38, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.RowFactory = sqlite3.Row;
#line (39, 5) - (39, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.Execute("CREATE TABLE test_row (id INTEGER, name TEXT, score REAL)");
#line (40, 5) - (40, 66) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.Execute("INSERT INTO test_row VALUES (1, 'Alice', 9.5)");
#line (41, 5) - (41, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.Commit();
#line (42, 5) - (42, 66) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var cursor = conn.Execute("SELECT id, name, score FROM test_row");
#line (43, 5) - (54, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                switch (cursor.Fetchone())
                {
                    case global::Sharpy.Sqlite3Row r:
#line (45, 13) - (45, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                        conn.Close();
#line (46, 13) - (46, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                        return r;
                    default:
#line (48, 13) - (48, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                        conn.Close();
#line (49, 13) - (49, 55) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                        throw new global::Sharpy.ValueError("expected a Sqlite3Row");
                }
            }
        }
    }

    public static partial class Sqlite3
    {
        public partial class Sqlite3RowTestsTests
        {
            [Xunit.FactAttribute]
            public void TestIndexAccessFirstElement()
            {
#line (56, 5) - (56, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var row = _MakeRow();
#line (57, 5) - (57, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                Xunit.Assert.True(_EqInt(row[0], 1));
            }

            [Xunit.FactAttribute]
            public void TestIndexAccessSecondElement()
            {
#line (62, 5) - (62, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var row = _MakeRow();
#line (63, 5) - (63, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                Xunit.Assert.True(@operator.Eq(row[1], "Alice"));
            }

            [Xunit.FactAttribute]
            public void TestIndexAccessThirdElement()
            {
#line (68, 5) - (68, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var row = _MakeRow();
#line (69, 5) - (69, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                Xunit.Assert.True(@operator.Eq(row[2], 9.5d));
            }

            [Xunit.FactAttribute]
            public void TestNegativeIndexLastElement()
            {
#line (76, 5) - (76, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var row = _MakeRow();
#line (77, 5) - (77, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                Xunit.Assert.True(@operator.Eq(row[-1], 9.5d));
            }

            [Xunit.FactAttribute]
            public void TestNegativeIndexFirstElement()
            {
#line (82, 5) - (82, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var row = _MakeRow();
#line (83, 5) - (83, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                Xunit.Assert.True(_EqInt(row[-3], 1));
            }

            [Xunit.FactAttribute]
            public void TestNegativeIndexSecondFromEnd()
            {
#line (88, 5) - (88, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var row = _MakeRow();
#line (89, 5) - (89, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                Xunit.Assert.True(@operator.Eq(row[-2], "Alice"));
            }

            [Xunit.FactAttribute]
            public void TestIndexTooLargeThrowsIndexError()
            {
#line (96, 5) - (96, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var row = _MakeRow();
#line (97, 5) - (101, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                Xunit.Assert.Throws<IndexError>((global::System.Action)(() =>
                {
#line (98, 9) - (98, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                    var _ = row[10];
                }));
            }

            [Xunit.FactAttribute]
            public void TestIndexTooNegativeThrowsIndexError()
            {
#line (103, 5) - (103, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var row = _MakeRow();
#line (104, 5) - (110, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                Xunit.Assert.Throws<IndexError>((global::System.Action)(() =>
                {
#line (105, 9) - (105, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                    var _ = row[-10];
                }));
            }

            [Xunit.FactAttribute]
            public void TestColumnNameAccessValidName()
            {
#line (112, 5) - (112, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var row = _MakeRow();
#line (113, 5) - (113, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                Xunit.Assert.True(@operator.Eq(row["name"], "Alice"));
            }

            [Xunit.FactAttribute]
            public void TestColumnNameAccessCaseInsensitive()
            {
#line (118, 5) - (118, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var row = _MakeRow();
#line (119, 5) - (119, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                Xunit.Assert.True(@operator.Eq(row["NAME"], "Alice"));
#line (120, 5) - (120, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                Xunit.Assert.True(@operator.Eq(row["Name"], "Alice"));
#line (121, 5) - (121, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                Xunit.Assert.True(@operator.Eq(row["nAmE"], "Alice"));
            }

            [Xunit.FactAttribute]
            public void TestColumnNameAccessAllColumns()
            {
#line (126, 5) - (126, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var row = _MakeRow();
#line (127, 5) - (127, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                Xunit.Assert.True(_EqInt(row["id"], 1));
#line (128, 5) - (128, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                Xunit.Assert.True(@operator.Eq(row["name"], "Alice"));
#line (129, 5) - (129, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                Xunit.Assert.True(@operator.Eq(row["score"], 9.5d));
            }

            [Xunit.FactAttribute]
            public void TestColumnNameAccessInvalidNameThrowsIndexError()
            {
#line (134, 5) - (134, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var row = _MakeRow();
#line (135, 5) - (141, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                Xunit.Assert.Throws<IndexError>((global::System.Action)(() =>
                {
#line (136, 9) - (136, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                    var _ = row["nonexistent"];
                }));
            }

            [Xunit.FactAttribute]
            public void TestKeysReturnsColumnNames()
            {
#line (143, 5) - (143, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var row = _MakeRow();
#line (144, 5) - (144, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var keys = row.Keys();
#line (145, 5) - (145, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(keys));
#line (146, 5) - (146, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                Xunit.Assert.Contains("id", keys);
#line (147, 5) - (147, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                Xunit.Assert.Contains("name", keys);
#line (148, 5) - (148, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                Xunit.Assert.Contains("score", keys);
            }

            [Xunit.FactAttribute]
            public void TestCountReturnsNumberOfColumns()
            {
#line (155, 5) - (155, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var row = _MakeRow();
#line (156, 5) - (156, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(row));
            }

            [Xunit.FactAttribute]
            public void TestToStringContainsColumnNamesAndValues()
            {
#line (163, 5) - (163, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var row = _MakeRow();
#line (164, 5) - (164, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                string s = global::Sharpy.Builtins.Str(row);
#line (165, 5) - (165, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                Xunit.Assert.StartsWith("<sqlite3.Row", s);
#line (166, 5) - (166, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                Xunit.Assert.EndsWith(">", s);
#line (167, 5) - (167, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                Xunit.Assert.Contains("id=1", s);
#line (168, 5) - (168, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                Xunit.Assert.Contains("name='Alice'", s);
#line (169, 5) - (169, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                Xunit.Assert.Contains("score=", s);
            }

            [Xunit.FactAttribute]
            public void TestToStringNullValueShowsNone()
            {
#line (174, 5) - (174, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (175, 5) - (175, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.RowFactory = sqlite3.Row;
#line (176, 5) - (176, 63) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.Execute("CREATE TABLE t_null (id INTEGER, val TEXT)");
#line (177, 5) - (177, 56) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.Execute("INSERT INTO t_null VALUES (1, NULL)");
#line (178, 5) - (178, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.Commit();
#line (179, 5) - (179, 56) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var cursor = conn.Execute("SELECT id, val FROM t_null");
#line (180, 5) - (186, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                switch (cursor.Fetchone())
                {
                    case global::Sharpy.Sqlite3Row row:
#line (182, 13) - (182, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                        string s = global::Sharpy.Builtins.Str(row);
#line (183, 13) - (183, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                        Xunit.Assert.Contains("val=None", s);
                        break;
                    default:
#line (185, 13) - (185, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }

#line (186, 5) - (186, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestToStringStringValueIsQuoted()
            {
#line (191, 5) - (191, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var row = _MakeRow();
#line (192, 5) - (192, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                string s = global::Sharpy.Builtins.Str(row);
#line (193, 5) - (193, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                Xunit.Assert.Contains("name='Alice'", s);
            }

            [Xunit.FactAttribute]
            public void TestRowFactoryConnectSetRowFactoryReturnsRowInstances()
            {
#line (200, 5) - (200, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (201, 5) - (201, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.RowFactory = sqlite3.Row;
#line (202, 5) - (202, 62) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.Execute("CREATE TABLE t_rf (id INTEGER, name TEXT)");
#line (203, 5) - (203, 56) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.Execute("INSERT INTO t_rf VALUES (1, 'test')");
#line (204, 5) - (204, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.Commit();
#line (205, 5) - (205, 55) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var cursor = conn.Execute("SELECT id, name FROM t_rf");
#line (206, 5) - (212, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                switch (cursor.Fetchone())
                {
                    case global::Sharpy.Sqlite3Row row:
#line (208, 13) - (208, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                        Xunit.Assert.True(_EqInt(row["id"], 1));
#line (209, 13) - (209, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                        Xunit.Assert.True(@operator.Eq(row["name"], "test"));
                        break;
                    default:
#line (211, 13) - (211, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }

#line (212, 5) - (212, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestRowFactoryFetchallReturnsRowInstances()
            {
#line (217, 5) - (217, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (218, 5) - (218, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.RowFactory = sqlite3.Row;
#line (219, 5) - (219, 63) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.Execute("CREATE TABLE t_rf2 (id INTEGER, name TEXT)");
#line (220, 5) - (220, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.Execute("INSERT INTO t_rf2 VALUES (1, 'Alice')");
#line (221, 5) - (221, 56) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.Execute("INSERT INTO t_rf2 VALUES (2, 'Bob')");
#line (222, 5) - (222, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.Commit();
#line (223, 5) - (223, 68) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var cursor = conn.Execute("SELECT id, name FROM t_rf2 ORDER BY id");
#line (224, 5) - (224, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var rows = cursor.Fetchall();
#line (225, 5) - (225, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(rows));
#line (227, 5) - (233, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                switch (rows[0])
                {
                    case global::Sharpy.Sqlite3Row row1:
#line (229, 13) - (229, 55) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                        Xunit.Assert.True(@operator.Eq(row1["name"], "Alice"));
                        break;
                    default:
#line (231, 13) - (231, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }

#line (233, 5) - (238, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                switch (rows[1])
                {
                    case global::Sharpy.Sqlite3Row row2:
#line (235, 13) - (235, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                        Xunit.Assert.True(@operator.Eq(row2["name"], "Bob"));
                        break;
                    default:
#line (237, 13) - (237, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }

#line (238, 5) - (238, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestRowFactoryIteratorReturnsRowInstances()
            {
#line (243, 5) - (243, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (244, 5) - (244, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.RowFactory = sqlite3.Row;
#line (245, 5) - (245, 63) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.Execute("CREATE TABLE t_rf3 (id INTEGER, name TEXT)");
#line (246, 5) - (246, 57) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.Execute("INSERT INTO t_rf3 VALUES (1, 'test')");
#line (247, 5) - (247, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.Commit();
#line (248, 5) - (248, 56) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var cursor = conn.Execute("SELECT id, name FROM t_rf3");
#line (249, 5) - (255, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                foreach (var __loopVar_0 in cursor)
                {
                    var row = __loopVar_0;
#line (250, 9) - (255, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                    switch (row)
                    {
                        case global::Sharpy.Sqlite3Row r:
#line (252, 17) - (252, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                            Xunit.Assert.True(_EqInt(r["id"], 1));
                            break;
                        default:
#line (254, 17) - (254, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                            Xunit.Assert.True(false);
                            break;
                    }
                }

#line (255, 5) - (255, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestSingleColumnRowIndexAndNameAccess()
            {
#line (262, 5) - (262, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (263, 5) - (263, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.RowFactory = sqlite3.Row;
#line (264, 5) - (264, 56) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.Execute("CREATE TABLE t_single (val INTEGER)");
#line (265, 5) - (265, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.Execute("INSERT INTO t_single VALUES (42)");
#line (266, 5) - (266, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.Commit();
#line (267, 5) - (267, 54) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var cursor = conn.Execute("SELECT val FROM t_single");
#line (268, 5) - (277, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                switch (cursor.Fetchone())
                {
                    case global::Sharpy.Sqlite3Row row:
#line (270, 13) - (270, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                        Xunit.Assert.True(_EqInt(row[0], 42));
#line (271, 13) - (271, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                        Xunit.Assert.True(_EqInt(row[-1], 42));
#line (272, 13) - (272, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                        Xunit.Assert.True(_EqInt(row["val"], 42));
#line (273, 13) - (273, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                        Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(row));
#line (274, 13) - (274, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                        Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(row.Keys()));
                        break;
                    default:
#line (276, 13) - (276, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }

#line (277, 5) - (277, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.Close();
            }
        }
    }
}
