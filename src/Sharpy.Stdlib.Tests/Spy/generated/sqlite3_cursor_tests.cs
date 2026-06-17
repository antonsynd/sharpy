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
#line (30, 5) - (30, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                return @operator.Eq(value, expected);
            }

            internal static global::Sharpy.Sqlite3Connection _Conn()
            {
#line (34, 5) - (34, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                return sqlite3.Connect(":memory:");
            }

            internal static global::Sharpy.Sqlite3Connection _PopulatedConn()
            {
#line (38, 5) - (38, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _Conn();
#line (39, 5) - (39, 83) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("CREATE TABLE t (id INTEGER PRIMARY KEY, name TEXT, score REAL)");
#line (40, 5) - (40, 59) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("INSERT INTO t VALUES (1, 'Alice', 9.5)");
#line (41, 5) - (41, 57) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("INSERT INTO t VALUES (2, 'Bob', 8.0)");
#line (42, 5) - (42, 61) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("INSERT INTO t VALUES (3, 'Charlie', 7.5)");
#line (43, 5) - (43, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Commit();
#line (44, 5) - (44, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
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
#line (51, 5) - (51, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _PopulatedConn();
#line (52, 5) - (52, 65) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Execute("SELECT id, name FROM t WHERE id = 1");
#line (53, 5) - (59, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                switch ((object[])cursor.Fetchone())
                {
                    case object[] r:
#line (55, 13) - (55, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        Xunit.Assert.True(_EqInt(global::Sharpy.ArrayHelpers.GetItem(r, 0), 1));
#line (56, 13) - (56, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        Xunit.Assert.True(@operator.Eq(global::Sharpy.ArrayHelpers.GetItem(r, 1), "Alice"));
                        break;
                    default:
#line (58, 13) - (58, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }

#line (59, 5) - (59, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestFetchoneReturnsNoneWhenNoMoreRows()
            {
#line (64, 5) - (64, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _PopulatedConn();
#line (65, 5) - (65, 59) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Execute("SELECT id FROM t WHERE id = 1");
#line (66, 5) - (66, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                cursor.Fetchone();
#line (67, 5) - (67, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var result = cursor.Fetchone();
#line (68, 5) - (68, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Null(result);
#line (69, 5) - (69, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestFetchoneNoResultsReturnsNone()
            {
#line (74, 5) - (74, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _PopulatedConn();
#line (75, 5) - (75, 61) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Execute("SELECT id FROM t WHERE id = 999");
#line (76, 5) - (76, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var result = cursor.Fetchone();
#line (77, 5) - (77, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Null(result);
#line (78, 5) - (78, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestFetchmanyReturnsRequestedNumberOfRows()
            {
#line (85, 5) - (85, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _PopulatedConn();
#line (86, 5) - (86, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Execute("SELECT id FROM t ORDER BY id");
#line (87, 5) - (87, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var rows = cursor.Fetchmany(2);
#line (88, 5) - (88, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(rows));
#line (89, 5) - (89, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestFetchmanyDefaultUsesArraysize()
            {
#line (94, 5) - (94, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _PopulatedConn();
#line (95, 5) - (95, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Execute("SELECT id FROM t ORDER BY id");
#line (96, 5) - (96, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                cursor.Arraysize = 2;
#line (97, 5) - (97, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var rows = cursor.Fetchmany();
#line (98, 5) - (98, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(rows));
#line (99, 5) - (99, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestFetchmanyReturnsFewerWhenNotEnoughRows()
            {
#line (104, 5) - (104, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _PopulatedConn();
#line (105, 5) - (105, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Execute("SELECT id FROM t ORDER BY id");
#line (106, 5) - (106, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var rows = cursor.Fetchmany(10);
#line (107, 5) - (107, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(rows));
#line (108, 5) - (108, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestFetchmanyNoReaderReturnsEmptyList()
            {
#line (113, 5) - (113, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _Conn();
#line (114, 5) - (114, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("CREATE TABLE t (id INTEGER)");
#line (115, 5) - (115, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Cursor();
#line (116, 5) - (116, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                cursor.Execute("INSERT INTO t VALUES (1)");
#line (117, 5) - (117, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var rows = cursor.Fetchmany(5);
#line (118, 5) - (118, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(rows));
#line (119, 5) - (119, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestFetchallReturnsAllRows()
            {
#line (126, 5) - (126, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _PopulatedConn();
#line (127, 5) - (127, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Execute("SELECT id FROM t ORDER BY id");
#line (128, 5) - (128, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var rows = cursor.Fetchall();
#line (129, 5) - (129, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(rows));
#line (131, 5) - (137, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                switch ((object[])rows[0])
                {
                    case object[] first:
#line (133, 13) - (133, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        Xunit.Assert.True(_EqInt(global::Sharpy.ArrayHelpers.GetItem(first, 0), 1));
                        break;
                    default:
#line (135, 13) - (135, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }

#line (137, 5) - (142, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                switch ((object[])rows[2])
                {
                    case object[] last:
#line (139, 13) - (139, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        Xunit.Assert.True(_EqInt(global::Sharpy.ArrayHelpers.GetItem(last, 0), 3));
                        break;
                    default:
#line (141, 13) - (141, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }

#line (142, 5) - (142, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestFetchallNoResultsReturnsEmptyList()
            {
#line (147, 5) - (147, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _PopulatedConn();
#line (148, 5) - (148, 61) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Execute("SELECT id FROM t WHERE id = 999");
#line (149, 5) - (149, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var rows = cursor.Fetchall();
#line (150, 5) - (150, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(rows));
#line (151, 5) - (151, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestFetchallAfterPartialFetchReturnsRemaining()
            {
#line (156, 5) - (156, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _PopulatedConn();
#line (157, 5) - (157, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Execute("SELECT id FROM t ORDER BY id");
#line (158, 5) - (158, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                cursor.Fetchone();
#line (159, 5) - (159, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var remaining = cursor.Fetchall();
#line (160, 5) - (160, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(remaining));
#line (161, 5) - (161, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestRowcountAfterInsertReturnsAffectedCount()
            {
#line (168, 5) - (168, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _Conn();
#line (169, 5) - (169, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("CREATE TABLE t (val INTEGER)");
#line (170, 5) - (170, 54) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Execute("INSERT INTO t VALUES (1)");
#line (171, 5) - (171, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Equal(1, cursor.Rowcount);
#line (172, 5) - (172, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestRowcountAfterUpdateReturnsAffectedCount()
            {
#line (177, 5) - (177, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _PopulatedConn();
#line (178, 5) - (178, 73) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Execute("UPDATE t SET score = 10.0 WHERE score < 9.0");
#line (179, 5) - (179, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Equal(2, cursor.Rowcount);
#line (180, 5) - (180, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestRowcountAfterDeleteReturnsAffectedCount()
            {
#line (185, 5) - (185, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _PopulatedConn();
#line (186, 5) - (186, 56) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Execute("DELETE FROM t WHERE id = 1");
#line (187, 5) - (187, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Equal(1, cursor.Rowcount);
#line (188, 5) - (188, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestRowcountAfterSelectIsMinusOne()
            {
#line (193, 5) - (193, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _PopulatedConn();
#line (194, 5) - (194, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Execute("SELECT id FROM t");
#line (195, 5) - (195, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Equal(-1, cursor.Rowcount);
#line (196, 5) - (196, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestRowcountInitialValueIsMinusOne()
            {
#line (201, 5) - (201, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (202, 5) - (202, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Cursor();
#line (203, 5) - (203, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Equal(-1, cursor.Rowcount);
#line (204, 5) - (204, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestExecuteParameterizedInsertBindsValues()
            {
#line (211, 5) - (211, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _Conn();
#line (212, 5) - (212, 63) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("CREATE TABLE t (a TEXT, b INTEGER, c REAL)");
#line (213, 5) - (213, 72) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("INSERT INTO t VALUES (?, ?, ?)", new Sharpy.List<object>() { "hello", 42, 3.14d });
#line (214, 5) - (214, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Commit();
#line (216, 5) - (216, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Execute("SELECT a, b, c FROM t");
#line (217, 5) - (224, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                switch ((object[])cursor.Fetchone())
                {
                    case object[] r:
#line (219, 13) - (219, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        Xunit.Assert.True(@operator.Eq(global::Sharpy.ArrayHelpers.GetItem(r, 0), "hello"));
#line (220, 13) - (220, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        Xunit.Assert.True(_EqInt(global::Sharpy.ArrayHelpers.GetItem(r, 1), 42));
#line (221, 13) - (221, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        Xunit.Assert.True(@operator.Eq(global::Sharpy.ArrayHelpers.GetItem(r, 2), 3.14d));
                        break;
                    default:
#line (223, 13) - (223, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }

#line (224, 5) - (224, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestExecuteParameterizedSelectFiltersCorrectly()
            {
#line (229, 5) - (229, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _PopulatedConn();
#line (230, 5) - (230, 66) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Execute("SELECT name FROM t WHERE id = ?", new Sharpy.List<int>() { 2 });
#line (231, 5) - (236, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                switch ((object[])cursor.Fetchone())
                {
                    case object[] r:
#line (233, 13) - (233, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        Xunit.Assert.True(@operator.Eq(global::Sharpy.ArrayHelpers.GetItem(r, 0), "Bob"));
                        break;
                    default:
#line (235, 13) - (235, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }

#line (236, 5) - (236, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestExecuteNullParameterInsertsNull()
            {
#line (241, 5) - (241, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _Conn();
#line (242, 5) - (242, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("CREATE TABLE t (val TEXT)");
#line (243, 5) - (243, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Sharpy.List<object> nullParams = new Sharpy.List<object>()
                {
                    null
                };
#line (244, 5) - (244, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("INSERT INTO t VALUES (?)", nullParams);
#line (245, 5) - (245, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Commit();
#line (247, 5) - (247, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Execute("SELECT val FROM t");
#line (248, 5) - (253, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                switch ((object[])cursor.Fetchone())
                {
                    case object[] r:
#line (250, 13) - (250, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        Xunit.Assert.Null(global::Sharpy.ArrayHelpers.GetItem(r, 0));
                        break;
                    default:
#line (252, 13) - (252, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }

#line (253, 5) - (253, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestDescriptionAfterSelectContainsColumnInfo()
            {
#line (260, 5) - (260, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _PopulatedConn();
#line (261, 5) - (261, 59) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Execute("SELECT id, name, score FROM t");
#line (262, 5) - (262, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.NotNull(cursor.Description);
#line (263, 5) - (263, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(cursor.Description));
#line (264, 5) - (264, 56) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.True(@operator.Eq(cursor.Description[0][0], "id"));
#line (265, 5) - (265, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.True(@operator.Eq(cursor.Description[1][0], "name"));
#line (266, 5) - (266, 59) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.True(@operator.Eq(cursor.Description[2][0], "score"));
#line (267, 5) - (267, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestDescriptionAfterInsertIsNone()
            {
#line (272, 5) - (272, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _Conn();
#line (273, 5) - (273, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("CREATE TABLE t (val INTEGER)");
#line (274, 5) - (274, 54) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Execute("INSERT INTO t VALUES (1)");
#line (275, 5) - (275, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Null(cursor.Description);
#line (276, 5) - (276, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestDescriptionSevenElementTuples()
            {
#line (281, 5) - (281, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _PopulatedConn();
#line (282, 5) - (282, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Execute("SELECT id FROM t");
#line (283, 5) - (283, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.NotNull(cursor.Description);
#line (284, 5) - (284, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Equal(7, global::Sharpy.Builtins.Len(cursor.Description[0]));
#line (285, 5) - (285, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestLastrowidAfterInsertReturnsRowId()
            {
#line (292, 5) - (292, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _Conn();
#line (293, 5) - (293, 70) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("CREATE TABLE t (id INTEGER PRIMARY KEY, val TEXT)");
#line (294, 5) - (294, 65) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Execute("INSERT INTO t (val) VALUES ('test')");
#line (295, 5) - (295, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.True(cursor.Lastrowid > 0);
#line (296, 5) - (296, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestLastrowidAfterMultipleInsertsReturnsLastId()
            {
#line (301, 5) - (301, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _Conn();
#line (302, 5) - (302, 70) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("CREATE TABLE t (id INTEGER PRIMARY KEY, val TEXT)");
#line (303, 5) - (303, 57) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("INSERT INTO t (val) VALUES ('first')");
#line (304, 5) - (304, 67) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Execute("INSERT INTO t (val) VALUES ('second')");
#line (305, 5) - (305, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Equal(2, cursor.Lastrowid);
#line (306, 5) - (306, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestLastrowidInitialValueIsMinusOne()
            {
#line (311, 5) - (311, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (312, 5) - (312, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Cursor();
#line (313, 5) - (313, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Equal(-1, cursor.Lastrowid);
#line (314, 5) - (314, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestExecutemanyInsertsAllParameterSets()
            {
#line (321, 5) - (321, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _Conn();
#line (322, 5) - (322, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("CREATE TABLE t (name TEXT)");
#line (324, 5) - (328, 6) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
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
#line (329, 5) - (329, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Cursor();
#line (330, 5) - (330, 63) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                cursor.Executemany("INSERT INTO t VALUES (?)", paramSets);
#line (331, 5) - (331, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Equal(3, cursor.Rowcount);
#line (332, 5) - (332, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Commit();
#line (334, 5) - (334, 59) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var selectCursor = conn.Execute("SELECT COUNT(*) FROM t");
#line (335, 5) - (340, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                switch ((object[])selectCursor.Fetchone())
                {
                    case object[] r:
#line (337, 13) - (337, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        Xunit.Assert.True(_EqInt(global::Sharpy.ArrayHelpers.GetItem(r, 0), 3));
                        break;
                    default:
#line (339, 13) - (339, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }

#line (340, 5) - (340, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestExecutemanyEmptySequenceRowcountIsZero()
            {
#line (345, 5) - (345, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _Conn();
#line (346, 5) - (346, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("CREATE TABLE t (val INTEGER)");
#line (347, 5) - (347, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Cursor();
#line (348, 5) - (348, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Sharpy.List<Sharpy.List<int>> empty = new Sharpy.List<Sharpy.List<int>>()
                {
                };
#line (349, 5) - (349, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                cursor.Executemany("INSERT INTO t VALUES (?)", empty);
#line (350, 5) - (350, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Equal(0, cursor.Rowcount);
#line (351, 5) - (351, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestForeachIteratesAllRows()
            {
#line (358, 5) - (358, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _PopulatedConn();
#line (359, 5) - (359, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Execute("SELECT id FROM t ORDER BY id");
#line (361, 5) - (361, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Sharpy.List<object> ids = new Sharpy.List<object>()
                {
                };
#line (362, 5) - (369, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                foreach (var __loopVar_0 in cursor)
                {
                    var row = __loopVar_0;
#line (363, 9) - (369, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                    switch ((object[])row)
                    {
                        case object[] r:
#line (365, 17) - (365, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                            ids.Append(global::Sharpy.ArrayHelpers.GetItem(r, 0));
                            break;
                        default:
#line (367, 17) - (367, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                            Xunit.Assert.True(false);
                            break;
                    }
                }

#line (369, 5) - (369, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(ids));
#line (370, 5) - (370, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.True(_EqInt(ids[0], 1));
#line (371, 5) - (371, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.True(_EqInt(ids[1], 2));
#line (372, 5) - (372, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.True(_EqInt(ids[2], 3));
#line (373, 5) - (373, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestTypeMappingIntegerReturnsLong()
            {
#line (380, 5) - (380, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _Conn();
#line (381, 5) - (381, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("CREATE TABLE t (val INTEGER)");
#line (382, 5) - (382, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("INSERT INTO t VALUES (42)");
#line (383, 5) - (383, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Commit();
#line (385, 5) - (385, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Execute("SELECT val FROM t");
#line (386, 5) - (391, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                switch ((object[])cursor.Fetchone())
                {
                    case object[] r:
#line (388, 13) - (388, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        Xunit.Assert.True(_EqInt(global::Sharpy.ArrayHelpers.GetItem(r, 0), 42));
                        break;
                    default:
#line (390, 13) - (390, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }

#line (391, 5) - (391, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestTypeMappingTextReturnsString()
            {
#line (396, 5) - (396, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _Conn();
#line (397, 5) - (397, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("CREATE TABLE t (val TEXT)");
#line (398, 5) - (398, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("INSERT INTO t VALUES ('hello')");
#line (399, 5) - (399, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Commit();
#line (401, 5) - (401, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Execute("SELECT val FROM t");
#line (402, 5) - (407, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                switch ((object[])cursor.Fetchone())
                {
                    case object[] r:
#line (404, 13) - (404, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        Xunit.Assert.True(@operator.Eq(global::Sharpy.ArrayHelpers.GetItem(r, 0), "hello"));
                        break;
                    default:
#line (406, 13) - (406, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }

#line (407, 5) - (407, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestTypeMappingRealReturnsDouble()
            {
#line (412, 5) - (412, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _Conn();
#line (413, 5) - (413, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("CREATE TABLE t (val REAL)");
#line (414, 5) - (414, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("INSERT INTO t VALUES (3.14)");
#line (415, 5) - (415, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Commit();
#line (417, 5) - (417, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Execute("SELECT val FROM t");
#line (418, 5) - (423, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                switch ((object[])cursor.Fetchone())
                {
                    case object[] r:
#line (420, 13) - (420, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        Xunit.Assert.True(@operator.Eq(global::Sharpy.ArrayHelpers.GetItem(r, 0), 3.14d));
                        break;
                    default:
#line (422, 13) - (422, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }

#line (423, 5) - (423, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestTypeMappingBlobReturnsBytes()
            {
#line (428, 5) - (428, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _Conn();
#line (429, 5) - (429, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("CREATE TABLE t (val BLOB)");
#line (430, 5) - (430, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                global::Sharpy.Bytes blobData = new Sharpy.Bytes(new byte[] { 1, 2, 3 });
#line (431, 5) - (431, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("INSERT INTO t VALUES (?)", new Sharpy.List<global::Sharpy.Bytes>() { blobData });
#line (432, 5) - (432, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Commit();
#line (434, 5) - (434, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Execute("SELECT val FROM t");
#line (435, 5) - (446, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                switch ((object[])cursor.Fetchone())
                {
                    case object[] r:
#line (437, 13) - (444, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        switch (global::Sharpy.ArrayHelpers.GetItem(r, 0))
                        {
                            case global::Sharpy.Bytes result:
#line (439, 21) - (439, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                                Xunit.Assert.Equal(1, result[0]);
#line (440, 21) - (440, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                                Xunit.Assert.Equal(2, result[1]);
#line (441, 21) - (441, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                                Xunit.Assert.Equal(3, result[2]);
                                break;
                            default:
#line (443, 21) - (443, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                                Xunit.Assert.True(false);
                                break;
                        }

                        break;
                    default:
#line (445, 13) - (445, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }

#line (446, 5) - (446, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestExecuteBadSqlThrowsOperationalError()
            {
#line (453, 5) - (453, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (454, 5) - (456, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Throws<Sqlite3OperationalError>((global::System.Action)(() =>
                {
#line (455, 9) - (455, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                    conn.Execute("INVALID SQL STATEMENT");
                }));
#line (456, 5) - (456, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestExecuteConstraintViolationThrowsIntegrityError()
            {
#line (461, 5) - (461, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (462, 5) - (462, 60) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("CREATE TABLE t (id INTEGER PRIMARY KEY)");
#line (463, 5) - (463, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("INSERT INTO t VALUES (1)");
#line (464, 5) - (466, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Throws<Sqlite3IntegrityError>((global::System.Action)(() =>
                {
#line (465, 9) - (465, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                    conn.Execute("INSERT INTO t VALUES (1)");
                }));
#line (466, 5) - (466, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestExecuteUniqueConstraintViolationThrowsIntegrityError()
            {
#line (471, 5) - (471, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (472, 5) - (472, 54) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("CREATE TABLE t (name TEXT UNIQUE)");
#line (473, 5) - (473, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("INSERT INTO t VALUES ('Alice')");
#line (474, 5) - (476, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Throws<Sqlite3IntegrityError>((global::System.Action)(() =>
                {
#line (475, 9) - (475, 55) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                    conn.Execute("INSERT INTO t VALUES ('Alice')");
                }));
#line (476, 5) - (476, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestExecuteNotNullViolationThrowsIntegrityError()
            {
#line (481, 5) - (481, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (482, 5) - (482, 56) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("CREATE TABLE t (name TEXT NOT NULL)");
#line (483, 5) - (485, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Throws<Sqlite3IntegrityError>((global::System.Action)(() =>
                {
#line (484, 9) - (484, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                    conn.Execute("INSERT INTO t VALUES (NULL)");
                }));
#line (485, 5) - (485, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestCursorCloseThenExecuteThrowsProgrammingError()
            {
#line (492, 5) - (492, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (493, 5) - (493, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Cursor();
#line (494, 5) - (494, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                cursor.Close();
#line (495, 5) - (497, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Throws<Sqlite3ProgrammingError>((global::System.Action)(() =>
                {
#line (496, 9) - (496, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                    cursor.Execute("SELECT 1");
                }));
#line (497, 5) - (497, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestCursorCloseThenFetchoneThrowsProgrammingError()
            {
#line (502, 5) - (502, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (503, 5) - (503, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Cursor();
#line (504, 5) - (504, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                cursor.Close();
#line (505, 5) - (507, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Throws<Sqlite3ProgrammingError>((global::System.Action)(() =>
                {
#line (506, 9) - (506, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                    cursor.Fetchone();
                }));
#line (507, 5) - (507, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestCursorCloseThenFetchallThrowsProgrammingError()
            {
#line (512, 5) - (512, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (513, 5) - (513, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Cursor();
#line (514, 5) - (514, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                cursor.Close();
#line (515, 5) - (517, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Throws<Sqlite3ProgrammingError>((global::System.Action)(() =>
                {
#line (516, 9) - (516, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                    cursor.Fetchall();
                }));
#line (517, 5) - (517, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestCursorCloseThenFetchmanyThrowsProgrammingError()
            {
#line (522, 5) - (522, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (523, 5) - (523, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Cursor();
#line (524, 5) - (524, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                cursor.Close();
#line (525, 5) - (527, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Throws<Sqlite3ProgrammingError>((global::System.Action)(() =>
                {
#line (526, 9) - (526, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                    cursor.Fetchmany(5);
                }));
#line (527, 5) - (527, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestArraysizeDefaultIsOne()
            {
#line (534, 5) - (534, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (535, 5) - (535, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Cursor();
#line (536, 5) - (536, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Equal(1, cursor.Arraysize);
#line (537, 5) - (537, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestArraysizeCanBeSet()
            {
#line (542, 5) - (542, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (543, 5) - (543, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Cursor();
#line (544, 5) - (544, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                cursor.Arraysize = 10;
#line (545, 5) - (545, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Equal(10, cursor.Arraysize);
#line (546, 5) - (546, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestExecutescriptBadSqlThrowsOperationalError()
            {
#line (553, 5) - (553, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (554, 5) - (554, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Cursor();
#line (555, 5) - (557, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Throws<Sqlite3OperationalError>((global::System.Action)(() =>
                {
#line (556, 9) - (556, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                    cursor.Executescript("INVALID; SQL; HERE");
                }));
#line (557, 5) - (557, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestMultipleCursorsOnSameConnectionWorkIndependently()
            {
#line (564, 5) - (564, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _PopulatedConn();
#line (566, 5) - (566, 56) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cur1 = conn.Execute("SELECT id FROM t ORDER BY id");
#line (567, 5) - (567, 60) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cur2 = conn.Execute("SELECT name FROM t ORDER BY name");
#line (569, 5) - (575, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                switch ((object[])cur1.Fetchone())
                {
                    case object[] row1:
#line (571, 13) - (571, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        Xunit.Assert.True(_EqInt(global::Sharpy.ArrayHelpers.GetItem(row1, 0), 1));
                        break;
                    default:
#line (573, 13) - (573, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }

#line (575, 5) - (581, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                switch ((object[])cur2.Fetchone())
                {
                    case object[] row2:
#line (577, 13) - (577, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        Xunit.Assert.True(@operator.Eq(global::Sharpy.ArrayHelpers.GetItem(row2, 0), "Alice"));
                        break;
                    default:
#line (579, 13) - (579, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }

#line (581, 5) - (581, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var remaining1 = cur1.Fetchall();
#line (582, 5) - (582, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(remaining1));
#line (584, 5) - (584, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var remaining2 = cur2.Fetchall();
#line (585, 5) - (585, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(remaining2));
#line (586, 5) - (586, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
            }
        }
    }
}
