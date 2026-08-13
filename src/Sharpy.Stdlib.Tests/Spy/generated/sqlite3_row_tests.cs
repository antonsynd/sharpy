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
#line (33, 5) - (33, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                return @operator.Eq(value, expected);
#line hidden
            }

            internal static int _RowKeyCount(global::Sharpy.Sqlite3Row r)
            {
#line (41, 5) - (41, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                return global::Sharpy.Builtins.Len(r.Keys());
#line hidden
            }

            internal static global::Sharpy.Sqlite3Row _MakeRow()
            {
#line (45, 5) - (45, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (46, 5) - (46, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.RowFactory = sqlite3.Row;
#line (47, 5) - (47, 78) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.Execute("CREATE TABLE test_row (id INTEGER, name TEXT, score REAL)");
#line (48, 5) - (48, 66) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.Execute("INSERT INTO test_row VALUES (1, 'Alice', 9.5)");
#line (49, 5) - (49, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.Commit();
#line (50, 5) - (50, 66) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var cursor = conn.Execute("SELECT id, name, score FROM test_row");
#line (51, 5) - (62, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                switch (cursor.Fetchone())
#line hidden
                {
                    case global::Sharpy.Sqlite3Row r:
#line (53, 13) - (53, 25) 24 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                        conn.Close();
#line (54, 13) - (54, 22) 24 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                        return r;
#line hidden
                    default:
#line (56, 13) - (56, 25) 24 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                        conn.Close();
#line (57, 13) - (57, 55) 24 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                        throw new global::Sharpy.ValueError("expected a Sqlite3Row");
#line hidden
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
#line (64, 5) - (64, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var row = _MakeRow();
#line (65, 5) - (65, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                Xunit.Assert.True(_EqInt(row[0], 1));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestIndexAccessSecondElement()
            {
#line (70, 5) - (70, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var row = _MakeRow();
#line (71, 5) - (71, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                Xunit.Assert.True(@operator.Eq(row[1], "Alice"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestIndexAccessThirdElement()
            {
#line (76, 5) - (76, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var row = _MakeRow();
#line (77, 5) - (77, 37) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                Xunit.Assert.True(@operator.Eq(row[2], 9.5d));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestNegativeIndexLastElement()
            {
#line (84, 5) - (84, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var row = _MakeRow();
#line (85, 5) - (85, 38) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                Xunit.Assert.True(@operator.Eq(row[-1], 9.5d));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestNegativeIndexFirstElement()
            {
#line (90, 5) - (90, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var row = _MakeRow();
#line (91, 5) - (91, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                Xunit.Assert.True(_EqInt(row[-3], 1));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestNegativeIndexSecondFromEnd()
            {
#line (96, 5) - (96, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var row = _MakeRow();
#line (97, 5) - (97, 42) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                Xunit.Assert.True(@operator.Eq(row[-2], "Alice"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestIndexTooLargeThrowsIndexError()
            {
#line (104, 5) - (104, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var row = _MakeRow();
#line (105, 5) - (109, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                bool __raised_0 = false;
#line hidden
                try
                {
#line (106, 9) - (106, 20) 20 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                    var _ = row[10];
#line hidden
                }
                catch (IndexError)
                {
                    __raised_0 = true;
                }

                if (!__raised_0)
                    throw new global::Sharpy.AssertionError("Expected IndexError to be raised, but no exception was raised");
            }

            [Xunit.FactAttribute]
            public void TestIndexTooNegativeThrowsIndexError()
            {
#line (111, 5) - (111, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var row = _MakeRow();
#line (112, 5) - (118, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                bool __raised_1 = false;
#line hidden
                try
                {
#line (113, 9) - (113, 21) 20 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                    var _ = row[-10];
#line hidden
                }
                catch (IndexError)
                {
                    __raised_1 = true;
                }

                if (!__raised_1)
                    throw new global::Sharpy.AssertionError("Expected IndexError to be raised, but no exception was raised");
            }

            [Xunit.FactAttribute]
            public void TestColumnNameAccessValidName()
            {
#line (120, 5) - (120, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var row = _MakeRow();
#line (121, 5) - (121, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                Xunit.Assert.True(@operator.Eq(row["name"], "Alice"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestColumnNameAccessCaseInsensitive()
            {
#line (126, 5) - (126, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var row = _MakeRow();
#line (127, 5) - (127, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                Xunit.Assert.True(@operator.Eq(row["NAME"], "Alice"));
#line (128, 5) - (128, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                Xunit.Assert.True(@operator.Eq(row["Name"], "Alice"));
#line (129, 5) - (129, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                Xunit.Assert.True(@operator.Eq(row["nAmE"], "Alice"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestColumnNameAccessAllColumns()
            {
#line (134, 5) - (134, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var row = _MakeRow();
#line (135, 5) - (135, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                Xunit.Assert.True(_EqInt(row["id"], 1));
#line (136, 5) - (136, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                Xunit.Assert.True(@operator.Eq(row["name"], "Alice"));
#line (137, 5) - (137, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                Xunit.Assert.True(@operator.Eq(row["score"], 9.5d));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestColumnNameAccessInvalidNameThrowsIndexError()
            {
#line (142, 5) - (142, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var row = _MakeRow();
#line (143, 5) - (149, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                bool __raised_2 = false;
#line hidden
                try
                {
#line (144, 9) - (144, 31) 20 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                    var _ = row["nonexistent"];
#line hidden
                }
                catch (IndexError)
                {
                    __raised_2 = true;
                }

                if (!__raised_2)
                    throw new global::Sharpy.AssertionError("Expected IndexError to be raised, but no exception was raised");
            }

            [Xunit.FactAttribute]
            public void TestKeysReturnsColumnNames()
            {
#line (151, 5) - (151, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var row = _MakeRow();
#line (152, 5) - (152, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var keys = row.Keys();
#line (153, 5) - (153, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(keys));
#line (154, 5) - (154, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                Xunit.Assert.Contains("id", keys);
#line (155, 5) - (155, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                Xunit.Assert.Contains("name", keys);
#line (156, 5) - (156, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                Xunit.Assert.Contains("score", keys);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestCountReturnsNumberOfColumns()
            {
#line (163, 5) - (163, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var row = _MakeRow();
#line (164, 5) - (164, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(row));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestToStringContainsColumnNamesAndValues()
            {
#line (171, 5) - (171, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var row = _MakeRow();
#line (172, 5) - (172, 23) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                string s = global::Sharpy.Builtins.Str(row);
#line (173, 5) - (173, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                Xunit.Assert.StartsWith("<sqlite3.Row", s);
#line (174, 5) - (174, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                Xunit.Assert.EndsWith(">", s);
#line (175, 5) - (175, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                Xunit.Assert.Contains("id=1", s);
#line (176, 5) - (176, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                Xunit.Assert.Contains("name='Alice'", s);
#line (177, 5) - (177, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                Xunit.Assert.Contains("score=", s);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestToStringNullValueShowsNone()
            {
#line (182, 5) - (182, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (183, 5) - (183, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.RowFactory = sqlite3.Row;
#line (184, 5) - (184, 63) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.Execute("CREATE TABLE t_null (id INTEGER, val TEXT)");
#line (185, 5) - (185, 56) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.Execute("INSERT INTO t_null VALUES (1, NULL)");
#line (186, 5) - (186, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.Commit();
#line (187, 5) - (187, 56) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var cursor = conn.Execute("SELECT id, val FROM t_null");
#line (188, 5) - (194, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                switch (cursor.Fetchone())
#line hidden
                {
                    case global::Sharpy.Sqlite3Row row:
#line (190, 13) - (190, 31) 24 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                        string s = global::Sharpy.Builtins.Str(row);
#line (191, 13) - (191, 36) 24 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                        Xunit.Assert.Contains("val=None", s);
#line hidden
                        break;
                    default:
#line (193, 13) - (193, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }

#line (194, 5) - (194, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.Close();
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestToStringStringValueIsQuoted()
            {
#line (199, 5) - (199, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var row = _MakeRow();
#line (200, 5) - (200, 23) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                string s = global::Sharpy.Builtins.Str(row);
#line (201, 5) - (201, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                Xunit.Assert.Contains("name='Alice'", s);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestRowFactoryConnectSetRowFactoryReturnsRowInstances()
            {
#line (208, 5) - (208, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (209, 5) - (209, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.RowFactory = sqlite3.Row;
#line (210, 5) - (210, 62) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.Execute("CREATE TABLE t_rf (id INTEGER, name TEXT)");
#line (211, 5) - (211, 56) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.Execute("INSERT INTO t_rf VALUES (1, 'test')");
#line (212, 5) - (212, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.Commit();
#line (213, 5) - (213, 55) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var cursor = conn.Execute("SELECT id, name FROM t_rf");
#line (214, 5) - (220, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                switch (cursor.Fetchone())
#line hidden
                {
                    case global::Sharpy.Sqlite3Row row:
#line (216, 13) - (216, 42) 24 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                        Xunit.Assert.True(_EqInt(row["id"], 1));
#line (217, 13) - (217, 53) 24 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                        Xunit.Assert.True(@operator.Eq(row["name"], "test"));
#line hidden
                        break;
                    default:
#line (219, 13) - (219, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }

#line (220, 5) - (220, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.Close();
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestRowFactoryFetchallReturnsRowInstances()
            {
#line (225, 5) - (225, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (226, 5) - (226, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.RowFactory = sqlite3.Row;
#line (227, 5) - (227, 63) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.Execute("CREATE TABLE t_rf2 (id INTEGER, name TEXT)");
#line (228, 5) - (228, 58) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.Execute("INSERT INTO t_rf2 VALUES (1, 'Alice')");
#line (229, 5) - (229, 56) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.Execute("INSERT INTO t_rf2 VALUES (2, 'Bob')");
#line (230, 5) - (230, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.Commit();
#line (231, 5) - (231, 68) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var cursor = conn.Execute("SELECT id, name FROM t_rf2 ORDER BY id");
#line (232, 5) - (232, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var rows = cursor.Fetchall();
#line (233, 5) - (233, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(rows));
#line (235, 5) - (241, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                switch (rows[0])
#line hidden
                {
                    case global::Sharpy.Sqlite3Row row1:
#line (237, 13) - (237, 55) 24 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                        Xunit.Assert.True(@operator.Eq(row1["name"], "Alice"));
#line hidden
                        break;
                    default:
#line (239, 13) - (239, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }

#line (241, 5) - (246, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                switch (rows[1])
#line hidden
                {
                    case global::Sharpy.Sqlite3Row row2:
#line (243, 13) - (243, 53) 24 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                        Xunit.Assert.True(@operator.Eq(row2["name"], "Bob"));
#line hidden
                        break;
                    default:
#line (245, 13) - (245, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }

#line (246, 5) - (246, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.Close();
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestRowFactoryIteratorReturnsRowInstances()
            {
#line (251, 5) - (251, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (252, 5) - (252, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.RowFactory = sqlite3.Row;
#line (253, 5) - (253, 63) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.Execute("CREATE TABLE t_rf3 (id INTEGER, name TEXT)");
#line (254, 5) - (254, 57) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.Execute("INSERT INTO t_rf3 VALUES (1, 'test')");
#line (255, 5) - (255, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.Commit();
#line (256, 5) - (256, 56) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var cursor = conn.Execute("SELECT id, name FROM t_rf3");
#line (257, 5) - (263, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                foreach (var __loopVar_3 in cursor)
#line hidden
                {
                    var row = __loopVar_3;
#line (258, 9) - (263, 1) 20 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                    switch (row)
#line hidden
                    {
                        case global::Sharpy.Sqlite3Row r:
#line (260, 17) - (260, 44) 28 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                            Xunit.Assert.True(_EqInt(r["id"], 1));
#line hidden
                            break;
                        default:
#line (262, 17) - (262, 30) 28 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                            Xunit.Assert.True(false);
#line hidden
                            break;
                    }
                }

#line (263, 5) - (263, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.Close();
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestRowTypeAnnotationResolvesAndMemberAccessWorks()
            {
#line (274, 5) - (274, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var row = _MakeRow();
#line (275, 5) - (275, 37) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                Xunit.Assert.Equal(3, _RowKeyCount(row));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSingleColumnRowIndexAndNameAccess()
            {
#line (282, 5) - (282, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (283, 5) - (283, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.RowFactory = sqlite3.Row;
#line (284, 5) - (284, 56) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.Execute("CREATE TABLE t_single (val INTEGER)");
#line (285, 5) - (285, 53) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.Execute("INSERT INTO t_single VALUES (42)");
#line (286, 5) - (286, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.Commit();
#line (287, 5) - (287, 54) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                var cursor = conn.Execute("SELECT val FROM t_single");
#line (288, 5) - (297, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                switch (cursor.Fetchone())
#line hidden
                {
                    case global::Sharpy.Sqlite3Row row:
#line (290, 13) - (290, 40) 24 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                        Xunit.Assert.True(_EqInt(row[0], 42));
#line (291, 13) - (291, 41) 24 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                        Xunit.Assert.True(_EqInt(row[-1], 42));
#line (292, 13) - (292, 44) 24 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                        Xunit.Assert.True(_EqInt(row["val"], 42));
#line (293, 13) - (293, 34) 24 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                        Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(row));
#line (294, 13) - (294, 41) 24 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                        Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(row.Keys()));
#line hidden
                        break;
                    default:
#line (296, 13) - (296, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }

#line (297, 5) - (297, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_row_tests.spy"
                conn.Close();
#line hidden
            }
        }
    }
}
#line default
