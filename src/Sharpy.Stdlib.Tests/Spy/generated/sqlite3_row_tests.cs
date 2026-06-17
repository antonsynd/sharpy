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
#line (31, 5) - (31, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                return @operator.Eq(value, expected);
            }

            internal static global::Sharpy.Sqlite3Row _MakeRow()
            {
#line (35, 5) - (35, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (36, 5) - (36, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.RowFactory = sqlite3.Row;
#line (37, 5) - (37, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.Execute("CREATE TABLE test_row (id INTEGER, name TEXT, score REAL)");
#line (38, 5) - (38, 66) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.Execute("INSERT INTO test_row VALUES (1, 'Alice', 9.5)");
#line (39, 5) - (39, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.Commit();
#line (40, 5) - (40, 66) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var cursor = conn.Execute("SELECT id, name, score FROM test_row");
#line (41, 5) - (52, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                switch (cursor.Fetchone())
                {
                    case global::Sharpy.Sqlite3Row r:
#line (43, 13) - (43, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                        conn.Close();
#line (44, 13) - (44, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                        return r;
                    default:
#line (46, 13) - (46, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                        conn.Close();
#line (47, 13) - (47, 55) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
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
#line (54, 5) - (54, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var row = _MakeRow();
#line (55, 5) - (55, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                Xunit.Assert.True(_EqInt(row[0], 1));
            }

            [Xunit.FactAttribute]
            public void TestIndexAccessSecondElement()
            {
#line (60, 5) - (60, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var row = _MakeRow();
#line (61, 5) - (61, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                Xunit.Assert.True(@operator.Eq(row[1], "Alice"));
            }

            [Xunit.FactAttribute]
            public void TestIndexAccessThirdElement()
            {
#line (66, 5) - (66, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var row = _MakeRow();
#line (67, 5) - (67, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                Xunit.Assert.True(@operator.Eq(row[2], 9.5d));
            }

            [Xunit.FactAttribute]
            public void TestNegativeIndexLastElement()
            {
#line (74, 5) - (74, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var row = _MakeRow();
#line (75, 5) - (75, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                Xunit.Assert.True(@operator.Eq(row[-1], 9.5d));
            }

            [Xunit.FactAttribute]
            public void TestNegativeIndexFirstElement()
            {
#line (80, 5) - (80, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var row = _MakeRow();
#line (81, 5) - (81, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                Xunit.Assert.True(_EqInt(row[-3], 1));
            }

            [Xunit.FactAttribute]
            public void TestNegativeIndexSecondFromEnd()
            {
#line (86, 5) - (86, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var row = _MakeRow();
#line (87, 5) - (87, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                Xunit.Assert.True(@operator.Eq(row[-2], "Alice"));
            }

            [Xunit.FactAttribute]
            public void TestIndexTooLargeThrowsIndexError()
            {
#line (94, 5) - (94, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var row = _MakeRow();
#line (95, 5) - (99, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                Xunit.Assert.Throws<IndexError>((global::System.Action)(() =>
                {
#line (96, 9) - (96, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                    var _ = row[10];
                }));
            }

            [Xunit.FactAttribute]
            public void TestIndexTooNegativeThrowsIndexError()
            {
#line (101, 5) - (101, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var row = _MakeRow();
#line (102, 5) - (108, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                Xunit.Assert.Throws<IndexError>((global::System.Action)(() =>
                {
#line (103, 9) - (103, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                    var _ = row[-10];
                }));
            }

            [Xunit.FactAttribute]
            public void TestColumnNameAccessValidName()
            {
#line (110, 5) - (110, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var row = _MakeRow();
#line (111, 5) - (111, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                Xunit.Assert.True(@operator.Eq(row["name"], "Alice"));
            }

            [Xunit.FactAttribute]
            public void TestColumnNameAccessCaseInsensitive()
            {
#line (116, 5) - (116, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var row = _MakeRow();
#line (117, 5) - (117, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                Xunit.Assert.True(@operator.Eq(row["NAME"], "Alice"));
#line (118, 5) - (118, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                Xunit.Assert.True(@operator.Eq(row["Name"], "Alice"));
#line (119, 5) - (119, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                Xunit.Assert.True(@operator.Eq(row["nAmE"], "Alice"));
            }

            [Xunit.FactAttribute]
            public void TestColumnNameAccessAllColumns()
            {
#line (124, 5) - (124, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var row = _MakeRow();
#line (125, 5) - (125, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                Xunit.Assert.True(_EqInt(row["id"], 1));
#line (126, 5) - (126, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                Xunit.Assert.True(@operator.Eq(row["name"], "Alice"));
#line (127, 5) - (127, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                Xunit.Assert.True(@operator.Eq(row["score"], 9.5d));
            }

            [Xunit.FactAttribute]
            public void TestColumnNameAccessInvalidNameThrowsIndexError()
            {
#line (132, 5) - (132, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var row = _MakeRow();
#line (133, 5) - (139, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                Xunit.Assert.Throws<IndexError>((global::System.Action)(() =>
                {
#line (134, 9) - (134, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                    var _ = row["nonexistent"];
                }));
            }

            [Xunit.FactAttribute]
            public void TestKeysReturnsColumnNames()
            {
#line (141, 5) - (141, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var row = _MakeRow();
#line (142, 5) - (142, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var keys = row.Keys();
#line (143, 5) - (143, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(keys));
#line (144, 5) - (144, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                Xunit.Assert.Contains("id", keys);
#line (145, 5) - (145, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                Xunit.Assert.Contains("name", keys);
#line (146, 5) - (146, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                Xunit.Assert.Contains("score", keys);
            }

            [Xunit.FactAttribute]
            public void TestCountReturnsNumberOfColumns()
            {
#line (153, 5) - (153, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var row = _MakeRow();
#line (154, 5) - (154, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(row));
            }

            [Xunit.FactAttribute]
            public void TestToStringContainsColumnNamesAndValues()
            {
#line (161, 5) - (161, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var row = _MakeRow();
#line (162, 5) - (162, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                string s = global::Sharpy.Builtins.Str(row);
#line (163, 5) - (163, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                Xunit.Assert.StartsWith("<sqlite3.Row", s);
#line (164, 5) - (164, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                Xunit.Assert.EndsWith(">", s);
#line (165, 5) - (165, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                Xunit.Assert.Contains("id=1", s);
#line (166, 5) - (166, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                Xunit.Assert.Contains("name='Alice'", s);
#line (167, 5) - (167, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                Xunit.Assert.Contains("score=", s);
            }

            [Xunit.FactAttribute]
            public void TestToStringNullValueShowsNone()
            {
#line (172, 5) - (172, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (173, 5) - (173, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.RowFactory = sqlite3.Row;
#line (174, 5) - (174, 63) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.Execute("CREATE TABLE t_null (id INTEGER, val TEXT)");
#line (175, 5) - (175, 56) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.Execute("INSERT INTO t_null VALUES (1, NULL)");
#line (176, 5) - (176, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.Commit();
#line (177, 5) - (177, 56) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var cursor = conn.Execute("SELECT id, val FROM t_null");
#line (178, 5) - (184, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                switch (cursor.Fetchone())
                {
                    case global::Sharpy.Sqlite3Row row:
#line (180, 13) - (180, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                        string s = global::Sharpy.Builtins.Str(row);
#line (181, 13) - (181, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                        Xunit.Assert.Contains("val=None", s);
                        break;
                    default:
#line (183, 13) - (183, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }

#line (184, 5) - (184, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestToStringStringValueIsQuoted()
            {
#line (189, 5) - (189, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var row = _MakeRow();
#line (190, 5) - (190, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                string s = global::Sharpy.Builtins.Str(row);
#line (191, 5) - (191, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                Xunit.Assert.Contains("name='Alice'", s);
            }

            [Xunit.FactAttribute]
            public void TestRowFactoryConnectSetRowFactoryReturnsRowInstances()
            {
#line (198, 5) - (198, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (199, 5) - (199, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.RowFactory = sqlite3.Row;
#line (200, 5) - (200, 62) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.Execute("CREATE TABLE t_rf (id INTEGER, name TEXT)");
#line (201, 5) - (201, 56) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.Execute("INSERT INTO t_rf VALUES (1, 'test')");
#line (202, 5) - (202, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.Commit();
#line (203, 5) - (203, 55) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var cursor = conn.Execute("SELECT id, name FROM t_rf");
#line (204, 5) - (210, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                switch (cursor.Fetchone())
                {
                    case global::Sharpy.Sqlite3Row row:
#line (206, 13) - (206, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                        Xunit.Assert.True(_EqInt(row["id"], 1));
#line (207, 13) - (207, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                        Xunit.Assert.True(@operator.Eq(row["name"], "test"));
                        break;
                    default:
#line (209, 13) - (209, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }

#line (210, 5) - (210, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestRowFactoryFetchallReturnsRowInstances()
            {
#line (215, 5) - (215, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (216, 5) - (216, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.RowFactory = sqlite3.Row;
#line (217, 5) - (217, 63) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.Execute("CREATE TABLE t_rf2 (id INTEGER, name TEXT)");
#line (218, 5) - (218, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.Execute("INSERT INTO t_rf2 VALUES (1, 'Alice')");
#line (219, 5) - (219, 56) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.Execute("INSERT INTO t_rf2 VALUES (2, 'Bob')");
#line (220, 5) - (220, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.Commit();
#line (221, 5) - (221, 68) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var cursor = conn.Execute("SELECT id, name FROM t_rf2 ORDER BY id");
#line (222, 5) - (222, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var rows = cursor.Fetchall();
#line (223, 5) - (223, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(rows));
#line (225, 5) - (231, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                switch (rows[0])
                {
                    case global::Sharpy.Sqlite3Row row1:
#line (227, 13) - (227, 55) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                        Xunit.Assert.True(@operator.Eq(row1["name"], "Alice"));
                        break;
                    default:
#line (229, 13) - (229, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }

#line (231, 5) - (236, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                switch (rows[1])
                {
                    case global::Sharpy.Sqlite3Row row2:
#line (233, 13) - (233, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                        Xunit.Assert.True(@operator.Eq(row2["name"], "Bob"));
                        break;
                    default:
#line (235, 13) - (235, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }

#line (236, 5) - (236, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestRowFactoryIteratorReturnsRowInstances()
            {
#line (241, 5) - (241, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (242, 5) - (242, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.RowFactory = sqlite3.Row;
#line (243, 5) - (243, 63) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.Execute("CREATE TABLE t_rf3 (id INTEGER, name TEXT)");
#line (244, 5) - (244, 57) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.Execute("INSERT INTO t_rf3 VALUES (1, 'test')");
#line (245, 5) - (245, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.Commit();
#line (246, 5) - (246, 56) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var cursor = conn.Execute("SELECT id, name FROM t_rf3");
#line (247, 5) - (253, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                foreach (var __loopVar_0 in cursor)
                {
                    var row = __loopVar_0;
#line (248, 9) - (253, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                    switch (row)
                    {
                        case global::Sharpy.Sqlite3Row r:
#line (250, 17) - (250, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                            Xunit.Assert.True(_EqInt(r["id"], 1));
                            break;
                        default:
#line (252, 17) - (252, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                            Xunit.Assert.True(false);
                            break;
                    }
                }

#line (253, 5) - (253, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestSingleColumnRowIndexAndNameAccess()
            {
#line (260, 5) - (260, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (261, 5) - (261, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.RowFactory = sqlite3.Row;
#line (262, 5) - (262, 56) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.Execute("CREATE TABLE t_single (val INTEGER)");
#line (263, 5) - (263, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.Execute("INSERT INTO t_single VALUES (42)");
#line (264, 5) - (264, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.Commit();
#line (265, 5) - (265, 54) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var cursor = conn.Execute("SELECT val FROM t_single");
#line (266, 5) - (275, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                switch (cursor.Fetchone())
                {
                    case global::Sharpy.Sqlite3Row row:
#line (268, 13) - (268, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                        Xunit.Assert.True(_EqInt(row[0], 42));
#line (269, 13) - (269, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                        Xunit.Assert.True(_EqInt(row[-1], 42));
#line (270, 13) - (270, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                        Xunit.Assert.True(_EqInt(row["val"], 42));
#line (271, 13) - (271, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                        Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(row));
#line (272, 13) - (272, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                        Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(row.Keys()));
                        break;
                    default:
#line (274, 13) - (274, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }

#line (275, 5) - (275, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.Close();
            }
        }
    }
}
