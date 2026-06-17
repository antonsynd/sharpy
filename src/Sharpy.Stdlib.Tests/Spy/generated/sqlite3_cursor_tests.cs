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
using static Sharpy.Stdlib.Tests.Spy.Sqlite3.Sqlite3CursorTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Sqlite3
    {
        [global::Sharpy.SharpyModule("sqlite3.sqlite3_cursor_tests")]
        public static partial class Sqlite3CursorTests
        {
            internal static bool _EqInt(object value, long expected)
            {
#line (31, 5) - (31, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                return @operator.Eq(value, expected);
            }

            internal static global::Sharpy.Sqlite3Connection _Conn()
            {
#line (35, 5) - (35, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (36, 5) - (36, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.RowFactory = sqlite3.Row;
#line (37, 5) - (37, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                return conn;
            }

            internal static global::Sharpy.Sqlite3Connection _PopulatedConn()
            {
#line (41, 5) - (41, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _Conn();
#line (42, 5) - (42, 83) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("CREATE TABLE t (id INTEGER PRIMARY KEY, name TEXT, score REAL)");
#line (43, 5) - (43, 59) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("INSERT INTO t VALUES (1, 'Alice', 9.5)");
#line (44, 5) - (44, 57) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("INSERT INTO t VALUES (2, 'Bob', 8.0)");
#line (45, 5) - (45, 61) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("INSERT INTO t VALUES (3, 'Charlie', 7.5)");
#line (46, 5) - (46, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Commit();
#line (47, 5) - (47, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                return conn;
            }
        }
    }

    public static partial class Sqlite3
    {
        public partial class Sqlite3CursorTestsTests
        {
            [Xunit.FactAttribute]
            public void TestFetchoneReturnsSingleRow()
            {
#line (54, 5) - (54, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _PopulatedConn();
#line (55, 5) - (55, 65) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Execute("SELECT id, name FROM t WHERE id = 1");
#line (56, 5) - (62, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                switch (cursor.Fetchone())
                {
                    case global::Sharpy.Sqlite3Row r:
#line (58, 13) - (58, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        Xunit.Assert.True(_EqInt(r[0], 1));
#line (59, 13) - (59, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        Xunit.Assert.True(@operator.Eq(r[1], "Alice"));
                        break;
                    default:
#line (61, 13) - (61, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }

#line (62, 5) - (62, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestFetchoneReturnsNoneWhenNoMoreRows()
            {
#line (67, 5) - (67, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _PopulatedConn();
#line (68, 5) - (68, 59) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Execute("SELECT id FROM t WHERE id = 1");
#line (69, 5) - (69, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                cursor.Fetchone();
#line (70, 5) - (70, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var result = cursor.Fetchone();
#line (71, 5) - (71, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Null(result);
#line (72, 5) - (72, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestFetchoneNoResultsReturnsNone()
            {
#line (77, 5) - (77, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _PopulatedConn();
#line (78, 5) - (78, 61) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Execute("SELECT id FROM t WHERE id = 999");
#line (79, 5) - (79, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var result = cursor.Fetchone();
#line (80, 5) - (80, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Null(result);
#line (81, 5) - (81, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestFetchmanyReturnsRequestedNumberOfRows()
            {
#line (88, 5) - (88, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _PopulatedConn();
#line (89, 5) - (89, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Execute("SELECT id FROM t ORDER BY id");
#line (90, 5) - (90, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var rows = cursor.Fetchmany(2);
#line (91, 5) - (91, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(rows));
#line (92, 5) - (92, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestFetchmanyDefaultUsesArraysize()
            {
#line (97, 5) - (97, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _PopulatedConn();
#line (98, 5) - (98, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Execute("SELECT id FROM t ORDER BY id");
#line (99, 5) - (99, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                cursor.Arraysize = 2;
#line (100, 5) - (100, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var rows = cursor.Fetchmany();
#line (101, 5) - (101, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(rows));
#line (102, 5) - (102, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestFetchmanyReturnsFewerWhenNotEnoughRows()
            {
#line (107, 5) - (107, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _PopulatedConn();
#line (108, 5) - (108, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Execute("SELECT id FROM t ORDER BY id");
#line (109, 5) - (109, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var rows = cursor.Fetchmany(10);
#line (110, 5) - (110, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(rows));
#line (111, 5) - (111, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestFetchmanyNoReaderReturnsEmptyList()
            {
#line (116, 5) - (116, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _Conn();
#line (117, 5) - (117, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("CREATE TABLE t (id INTEGER)");
#line (118, 5) - (118, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Cursor();
#line (119, 5) - (119, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                cursor.Execute("INSERT INTO t VALUES (1)");
#line (120, 5) - (120, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var rows = cursor.Fetchmany(5);
#line (121, 5) - (121, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(rows));
#line (122, 5) - (122, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestFetchallReturnsAllRows()
            {
#line (129, 5) - (129, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _PopulatedConn();
#line (130, 5) - (130, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Execute("SELECT id FROM t ORDER BY id");
#line (131, 5) - (131, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var rows = cursor.Fetchall();
#line (132, 5) - (132, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(rows));
#line (134, 5) - (140, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                switch (rows[0])
                {
                    case global::Sharpy.Sqlite3Row first:
#line (136, 13) - (136, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        Xunit.Assert.True(_EqInt(first[0], 1));
                        break;
                    default:
#line (138, 13) - (138, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }

#line (140, 5) - (145, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                switch (rows[2])
                {
                    case global::Sharpy.Sqlite3Row last:
#line (142, 13) - (142, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        Xunit.Assert.True(_EqInt(last[0], 3));
                        break;
                    default:
#line (144, 13) - (144, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }

#line (145, 5) - (145, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestFetchallNoResultsReturnsEmptyList()
            {
#line (150, 5) - (150, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _PopulatedConn();
#line (151, 5) - (151, 61) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Execute("SELECT id FROM t WHERE id = 999");
#line (152, 5) - (152, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var rows = cursor.Fetchall();
#line (153, 5) - (153, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(rows));
#line (154, 5) - (154, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestFetchallAfterPartialFetchReturnsRemaining()
            {
#line (159, 5) - (159, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _PopulatedConn();
#line (160, 5) - (160, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Execute("SELECT id FROM t ORDER BY id");
#line (161, 5) - (161, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                cursor.Fetchone();
#line (162, 5) - (162, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var remaining = cursor.Fetchall();
#line (163, 5) - (163, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(remaining));
#line (164, 5) - (164, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestRowcountAfterInsertReturnsAffectedCount()
            {
#line (171, 5) - (171, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _Conn();
#line (172, 5) - (172, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("CREATE TABLE t (val INTEGER)");
#line (173, 5) - (173, 54) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Execute("INSERT INTO t VALUES (1)");
#line (174, 5) - (174, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Equal(1, cursor.Rowcount);
#line (175, 5) - (175, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestRowcountAfterUpdateReturnsAffectedCount()
            {
#line (180, 5) - (180, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _PopulatedConn();
#line (181, 5) - (181, 73) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Execute("UPDATE t SET score = 10.0 WHERE score < 9.0");
#line (182, 5) - (182, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Equal(2, cursor.Rowcount);
#line (183, 5) - (183, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestRowcountAfterDeleteReturnsAffectedCount()
            {
#line (188, 5) - (188, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _PopulatedConn();
#line (189, 5) - (189, 56) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Execute("DELETE FROM t WHERE id = 1");
#line (190, 5) - (190, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Equal(1, cursor.Rowcount);
#line (191, 5) - (191, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestRowcountAfterSelectIsMinusOne()
            {
#line (196, 5) - (196, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _PopulatedConn();
#line (197, 5) - (197, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Execute("SELECT id FROM t");
#line (198, 5) - (198, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Equal(-1, cursor.Rowcount);
#line (199, 5) - (199, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestRowcountInitialValueIsMinusOne()
            {
#line (204, 5) - (204, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (205, 5) - (205, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Cursor();
#line (206, 5) - (206, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Equal(-1, cursor.Rowcount);
#line (207, 5) - (207, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestExecuteParameterizedInsertBindsValues()
            {
#line (214, 5) - (214, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _Conn();
#line (215, 5) - (215, 63) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("CREATE TABLE t (a TEXT, b INTEGER, c REAL)");
#line (216, 5) - (216, 72) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("INSERT INTO t VALUES (?, ?, ?)", new Sharpy.List<object>() { "hello", 42, 3.14d });
#line (217, 5) - (217, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Commit();
#line (219, 5) - (219, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Execute("SELECT a, b, c FROM t");
#line (220, 5) - (227, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                switch (cursor.Fetchone())
                {
                    case global::Sharpy.Sqlite3Row r:
#line (222, 13) - (222, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        Xunit.Assert.True(@operator.Eq(r[0], "hello"));
#line (223, 13) - (223, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        Xunit.Assert.True(_EqInt(r[1], 42));
#line (224, 13) - (224, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        Xunit.Assert.True(@operator.Eq(r[2], 3.14d));
                        break;
                    default:
#line (226, 13) - (226, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }

#line (227, 5) - (227, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestExecuteParameterizedSelectFiltersCorrectly()
            {
#line (232, 5) - (232, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _PopulatedConn();
#line (233, 5) - (233, 66) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Execute("SELECT name FROM t WHERE id = ?", new Sharpy.List<int>() { 2 });
#line (234, 5) - (239, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                switch (cursor.Fetchone())
                {
                    case global::Sharpy.Sqlite3Row r:
#line (236, 13) - (236, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        Xunit.Assert.True(@operator.Eq(r[0], "Bob"));
                        break;
                    default:
#line (238, 13) - (238, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }

#line (239, 5) - (239, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestExecuteNullParameterInsertsNull()
            {
#line (244, 5) - (244, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _Conn();
#line (245, 5) - (245, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("CREATE TABLE t (val TEXT)");
#line (246, 5) - (246, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Sharpy.List<object> nullParams = new Sharpy.List<object>()
                {
                    null
                };
#line (247, 5) - (247, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("INSERT INTO t VALUES (?)", nullParams);
#line (248, 5) - (248, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Commit();
#line (250, 5) - (250, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Execute("SELECT val FROM t");
#line (251, 5) - (256, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                switch (cursor.Fetchone())
                {
                    case global::Sharpy.Sqlite3Row r:
#line (253, 13) - (253, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        Xunit.Assert.Null(r[0]);
                        break;
                    default:
#line (255, 13) - (255, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }

#line (256, 5) - (256, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestDescriptionAfterSelectContainsColumnInfo()
            {
#line (263, 5) - (263, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _PopulatedConn();
#line (264, 5) - (264, 59) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Execute("SELECT id, name, score FROM t");
#line (265, 5) - (265, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.NotNull(cursor.Description);
#line (266, 5) - (266, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(cursor.Description));
#line (267, 5) - (267, 56) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.True(@operator.Eq(cursor.Description[0][0], "id"));
#line (268, 5) - (268, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.True(@operator.Eq(cursor.Description[1][0], "name"));
#line (269, 5) - (269, 59) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.True(@operator.Eq(cursor.Description[2][0], "score"));
#line (270, 5) - (270, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestDescriptionAfterInsertIsNone()
            {
#line (275, 5) - (275, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _Conn();
#line (276, 5) - (276, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("CREATE TABLE t (val INTEGER)");
#line (277, 5) - (277, 54) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Execute("INSERT INTO t VALUES (1)");
#line (278, 5) - (278, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Null(cursor.Description);
#line (279, 5) - (279, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestDescriptionSevenElementTuples()
            {
#line (284, 5) - (284, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _PopulatedConn();
#line (285, 5) - (285, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Execute("SELECT id FROM t");
#line (286, 5) - (286, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.NotNull(cursor.Description);
#line (287, 5) - (287, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Equal(7, global::Sharpy.Builtins.Len(cursor.Description[0]));
#line (288, 5) - (288, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestLastrowidAfterInsertReturnsRowId()
            {
#line (295, 5) - (295, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _Conn();
#line (296, 5) - (296, 70) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("CREATE TABLE t (id INTEGER PRIMARY KEY, val TEXT)");
#line (297, 5) - (297, 65) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Execute("INSERT INTO t (val) VALUES ('test')");
#line (298, 5) - (298, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.True(cursor.Lastrowid > 0);
#line (299, 5) - (299, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestLastrowidAfterMultipleInsertsReturnsLastId()
            {
#line (304, 5) - (304, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _Conn();
#line (305, 5) - (305, 70) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("CREATE TABLE t (id INTEGER PRIMARY KEY, val TEXT)");
#line (306, 5) - (306, 57) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("INSERT INTO t (val) VALUES ('first')");
#line (307, 5) - (307, 67) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Execute("INSERT INTO t (val) VALUES ('second')");
#line (308, 5) - (308, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Equal(2, cursor.Lastrowid);
#line (309, 5) - (309, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestLastrowidInitialValueIsMinusOne()
            {
#line (314, 5) - (314, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (315, 5) - (315, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Cursor();
#line (316, 5) - (316, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Equal(-1, cursor.Lastrowid);
#line (317, 5) - (317, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestExecutemanyInsertsAllParameterSets()
            {
#line (324, 5) - (324, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _Conn();
#line (325, 5) - (325, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("CREATE TABLE t (name TEXT)");
#line (327, 5) - (331, 6) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var paramSets = new Sharpy.List<Sharpy.List<string>>()
                {
                    new Sharpy.List<string>()
                    {
                        "Alice"
                    },
                    new Sharpy.List<string>()
                    {
                        "Bob"
                    },
                    new Sharpy.List<string>()
                    {
                        "Charlie"
                    }
                };
#line (332, 5) - (332, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Cursor();
#line (333, 5) - (333, 63) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                cursor.Executemany("INSERT INTO t VALUES (?)", paramSets);
#line (334, 5) - (334, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Equal(3, cursor.Rowcount);
#line (335, 5) - (335, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Commit();
#line (337, 5) - (337, 59) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var selectCursor = conn.Execute("SELECT COUNT(*) FROM t");
#line (338, 5) - (343, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                switch (selectCursor.Fetchone())
                {
                    case global::Sharpy.Sqlite3Row r:
#line (340, 13) - (340, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        Xunit.Assert.True(_EqInt(r[0], 3));
                        break;
                    default:
#line (342, 13) - (342, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }

#line (343, 5) - (343, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestExecutemanyEmptySequenceRowcountIsZero()
            {
#line (348, 5) - (348, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _Conn();
#line (349, 5) - (349, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("CREATE TABLE t (val INTEGER)");
#line (350, 5) - (350, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Cursor();
#line (351, 5) - (351, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Sharpy.List<Sharpy.List<int>> empty = new Sharpy.List<Sharpy.List<int>>()
                {
                };
#line (352, 5) - (352, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                cursor.Executemany("INSERT INTO t VALUES (?)", empty);
#line (353, 5) - (353, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Equal(0, cursor.Rowcount);
#line (354, 5) - (354, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestForeachIteratesAllRows()
            {
#line (361, 5) - (361, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _PopulatedConn();
#line (362, 5) - (362, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Execute("SELECT id FROM t ORDER BY id");
#line (364, 5) - (364, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Sharpy.List<object> ids = new Sharpy.List<object>()
                {
                };
#line (365, 5) - (372, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                foreach (var __loopVar_0 in cursor)
                {
                    var row = __loopVar_0;
#line (366, 9) - (372, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                    switch (row)
                    {
                        case global::Sharpy.Sqlite3Row r:
#line (368, 17) - (368, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                            ids.Append(r[0]);
                            break;
                        default:
#line (370, 17) - (370, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                            Xunit.Assert.True(false);
                            break;
                    }
                }

#line (372, 5) - (372, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(ids));
#line (373, 5) - (373, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.True(_EqInt(ids[0], 1));
#line (374, 5) - (374, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.True(_EqInt(ids[1], 2));
#line (375, 5) - (375, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.True(_EqInt(ids[2], 3));
#line (376, 5) - (376, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestTypeMappingIntegerReturnsLong()
            {
#line (383, 5) - (383, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _Conn();
#line (384, 5) - (384, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("CREATE TABLE t (val INTEGER)");
#line (385, 5) - (385, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("INSERT INTO t VALUES (42)");
#line (386, 5) - (386, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Commit();
#line (388, 5) - (388, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Execute("SELECT val FROM t");
#line (389, 5) - (394, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                switch (cursor.Fetchone())
                {
                    case global::Sharpy.Sqlite3Row r:
#line (391, 13) - (391, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        Xunit.Assert.True(_EqInt(r[0], 42));
                        break;
                    default:
#line (393, 13) - (393, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }

#line (394, 5) - (394, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestTypeMappingTextReturnsString()
            {
#line (399, 5) - (399, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _Conn();
#line (400, 5) - (400, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("CREATE TABLE t (val TEXT)");
#line (401, 5) - (401, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("INSERT INTO t VALUES ('hello')");
#line (402, 5) - (402, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Commit();
#line (404, 5) - (404, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Execute("SELECT val FROM t");
#line (405, 5) - (410, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                switch (cursor.Fetchone())
                {
                    case global::Sharpy.Sqlite3Row r:
#line (407, 13) - (407, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        Xunit.Assert.True(@operator.Eq(r[0], "hello"));
                        break;
                    default:
#line (409, 13) - (409, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }

#line (410, 5) - (410, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestTypeMappingRealReturnsDouble()
            {
#line (415, 5) - (415, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _Conn();
#line (416, 5) - (416, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("CREATE TABLE t (val REAL)");
#line (417, 5) - (417, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("INSERT INTO t VALUES (3.14)");
#line (418, 5) - (418, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Commit();
#line (420, 5) - (420, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Execute("SELECT val FROM t");
#line (421, 5) - (426, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                switch (cursor.Fetchone())
                {
                    case global::Sharpy.Sqlite3Row r:
#line (423, 13) - (423, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        Xunit.Assert.True(@operator.Eq(r[0], 3.14d));
                        break;
                    default:
#line (425, 13) - (425, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }

#line (426, 5) - (426, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestTypeMappingBlobReturnsBytes()
            {
#line (431, 5) - (431, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _Conn();
#line (432, 5) - (432, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("CREATE TABLE t (val BLOB)");
#line (433, 5) - (433, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                global::Sharpy.Bytes blobData = new Sharpy.Bytes(new byte[] { 1, 2, 3 });
#line (434, 5) - (434, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("INSERT INTO t VALUES (?)", new Sharpy.List<global::Sharpy.Bytes>() { blobData });
#line (435, 5) - (435, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Commit();
#line (437, 5) - (437, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Execute("SELECT val FROM t");
#line (438, 5) - (449, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                switch (cursor.Fetchone())
                {
                    case global::Sharpy.Sqlite3Row r:
#line (440, 13) - (447, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        switch (r[0])
                        {
                            case global::Sharpy.Bytes result:
#line (442, 21) - (442, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                                Xunit.Assert.Equal(1, result[0]);
#line (443, 21) - (443, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                                Xunit.Assert.Equal(2, result[1]);
#line (444, 21) - (444, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                                Xunit.Assert.Equal(3, result[2]);
                                break;
                            default:
#line (446, 21) - (446, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                                Xunit.Assert.True(false);
                                break;
                        }

                        break;
                    default:
#line (448, 13) - (448, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }

#line (449, 5) - (449, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestExecuteBadSqlThrowsOperationalError()
            {
#line (456, 5) - (456, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (457, 5) - (459, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Throws<Sqlite3OperationalError>((global::System.Action)(() =>
                {
#line (458, 9) - (458, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                    conn.Execute("INVALID SQL STATEMENT");
                }));
#line (459, 5) - (459, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestExecuteConstraintViolationThrowsIntegrityError()
            {
#line (464, 5) - (464, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (465, 5) - (465, 60) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("CREATE TABLE t (id INTEGER PRIMARY KEY)");
#line (466, 5) - (466, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("INSERT INTO t VALUES (1)");
#line (467, 5) - (469, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Throws<Sqlite3IntegrityError>((global::System.Action)(() =>
                {
#line (468, 9) - (468, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                    conn.Execute("INSERT INTO t VALUES (1)");
                }));
#line (469, 5) - (469, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestExecuteUniqueConstraintViolationThrowsIntegrityError()
            {
#line (474, 5) - (474, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (475, 5) - (475, 54) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("CREATE TABLE t (name TEXT UNIQUE)");
#line (476, 5) - (476, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("INSERT INTO t VALUES ('Alice')");
#line (477, 5) - (479, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Throws<Sqlite3IntegrityError>((global::System.Action)(() =>
                {
#line (478, 9) - (478, 55) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                    conn.Execute("INSERT INTO t VALUES ('Alice')");
                }));
#line (479, 5) - (479, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestExecuteNotNullViolationThrowsIntegrityError()
            {
#line (484, 5) - (484, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (485, 5) - (485, 56) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("CREATE TABLE t (name TEXT NOT NULL)");
#line (486, 5) - (488, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Throws<Sqlite3IntegrityError>((global::System.Action)(() =>
                {
#line (487, 9) - (487, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                    conn.Execute("INSERT INTO t VALUES (NULL)");
                }));
#line (488, 5) - (488, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestCursorCloseThenExecuteThrowsProgrammingError()
            {
#line (495, 5) - (495, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (496, 5) - (496, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Cursor();
#line (497, 5) - (497, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                cursor.Close();
#line (498, 5) - (500, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Throws<Sqlite3ProgrammingError>((global::System.Action)(() =>
                {
#line (499, 9) - (499, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                    cursor.Execute("SELECT 1");
                }));
#line (500, 5) - (500, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestCursorCloseThenFetchoneThrowsProgrammingError()
            {
#line (505, 5) - (505, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (506, 5) - (506, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Cursor();
#line (507, 5) - (507, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                cursor.Close();
#line (508, 5) - (510, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Throws<Sqlite3ProgrammingError>((global::System.Action)(() =>
                {
#line (509, 9) - (509, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                    cursor.Fetchone();
                }));
#line (510, 5) - (510, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestCursorCloseThenFetchallThrowsProgrammingError()
            {
#line (515, 5) - (515, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (516, 5) - (516, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Cursor();
#line (517, 5) - (517, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                cursor.Close();
#line (518, 5) - (520, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Throws<Sqlite3ProgrammingError>((global::System.Action)(() =>
                {
#line (519, 9) - (519, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                    cursor.Fetchall();
                }));
#line (520, 5) - (520, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestCursorCloseThenFetchmanyThrowsProgrammingError()
            {
#line (525, 5) - (525, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (526, 5) - (526, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Cursor();
#line (527, 5) - (527, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                cursor.Close();
#line (528, 5) - (530, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Throws<Sqlite3ProgrammingError>((global::System.Action)(() =>
                {
#line (529, 9) - (529, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                    cursor.Fetchmany(5);
                }));
#line (530, 5) - (530, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestArraysizeDefaultIsOne()
            {
#line (537, 5) - (537, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (538, 5) - (538, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Cursor();
#line (539, 5) - (539, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Equal(1, cursor.Arraysize);
#line (540, 5) - (540, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestArraysizeCanBeSet()
            {
#line (545, 5) - (545, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (546, 5) - (546, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Cursor();
#line (547, 5) - (547, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                cursor.Arraysize = 10;
#line (548, 5) - (548, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Equal(10, cursor.Arraysize);
#line (549, 5) - (549, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestExecutescriptBadSqlThrowsOperationalError()
            {
#line (556, 5) - (556, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (557, 5) - (557, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Cursor();
#line (558, 5) - (560, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Throws<Sqlite3OperationalError>((global::System.Action)(() =>
                {
#line (559, 9) - (559, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                    cursor.Executescript("INVALID; SQL; HERE");
                }));
#line (560, 5) - (560, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestMultipleCursorsOnSameConnectionWorkIndependently()
            {
#line (567, 5) - (567, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _PopulatedConn();
#line (569, 5) - (569, 56) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cur1 = conn.Execute("SELECT id FROM t ORDER BY id");
#line (570, 5) - (570, 60) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cur2 = conn.Execute("SELECT name FROM t ORDER BY name");
#line (572, 5) - (578, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                switch (cur1.Fetchone())
                {
                    case global::Sharpy.Sqlite3Row row1:
#line (574, 13) - (574, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        Xunit.Assert.True(_EqInt(row1[0], 1));
                        break;
                    default:
#line (576, 13) - (576, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }

#line (578, 5) - (584, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                switch (cur2.Fetchone())
                {
                    case global::Sharpy.Sqlite3Row row2:
#line (580, 13) - (580, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        Xunit.Assert.True(@operator.Eq(row2[0], "Alice"));
                        break;
                    default:
#line (582, 13) - (582, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }

#line (584, 5) - (584, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var remaining1 = cur1.Fetchall();
#line (585, 5) - (585, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(remaining1));
#line (587, 5) - (587, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var remaining2 = cur2.Fetchall();
#line (588, 5) - (588, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(remaining2));
#line (589, 5) - (589, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }
        }
    }
}
