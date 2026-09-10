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
#line (30, 5) - (30, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                return @operator.Eq(value, expected);
#line hidden
            }

            internal static global::Sharpy.Sqlite3Connection _Conn()
            {
#line (34, 5) - (34, 40) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                return sqlite3.Connect(":memory:");
#line hidden
            }

            internal static global::Sharpy.Sqlite3Connection _PopulatedConn()
            {
#line (38, 5) - (38, 19) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _Conn();
#line (39, 5) - (39, 83) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("CREATE TABLE t (id INTEGER PRIMARY KEY, name TEXT, score REAL)");
#line (40, 5) - (40, 59) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("INSERT INTO t VALUES (1, 'Alice', 9.5)");
#line (41, 5) - (41, 57) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("INSERT INTO t VALUES (2, 'Bob', 8.0)");
#line (42, 5) - (42, 61) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("INSERT INTO t VALUES (3, 'Charlie', 7.5)");
#line (43, 5) - (43, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Commit();
#line (44, 5) - (44, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                return conn;
#line hidden
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
#line (51, 5) - (51, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _PopulatedConn();
#line (52, 5) - (52, 65) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Execute("SELECT id, name FROM t WHERE id = 1");
#line (53, 5) - (58, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                switch ((object[])cursor.Fetchone())
#line hidden
                {
                    case object[] r:
#line (55, 13) - (55, 37) 24 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        Xunit.Assert.True(_EqInt(global::Sharpy.ArrayHelpers.GetItem(r, 0), 1));
#line (56, 13) - (56, 47) 24 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        Xunit.Assert.True(@operator.Eq(global::Sharpy.ArrayHelpers.GetItem(r, 1), "Alice"));
#line hidden
                        break;
                    default:
#line (58, 13) - (58, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }

#line (59, 5) - (59, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestFetchoneReturnsNoneWhenNoMoreRows()
            {
#line (64, 5) - (64, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _PopulatedConn();
#line (65, 5) - (65, 59) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Execute("SELECT id FROM t WHERE id = 1");
#line (66, 5) - (66, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                cursor.Fetchone();
#line (67, 5) - (67, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var result = cursor.Fetchone();
#line (68, 5) - (68, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Null(result);
#line (69, 5) - (69, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestFetchoneNoResultsReturnsNone()
            {
#line (74, 5) - (74, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _PopulatedConn();
#line (75, 5) - (75, 61) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Execute("SELECT id FROM t WHERE id = 999");
#line (76, 5) - (76, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var result = cursor.Fetchone();
#line (77, 5) - (77, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Null(result);
#line (78, 5) - (78, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestFetchmanyReturnsRequestedNumberOfRows()
            {
#line (85, 5) - (85, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _PopulatedConn();
#line (86, 5) - (86, 58) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Execute("SELECT id FROM t ORDER BY id");
#line (87, 5) - (87, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var rows = cursor.Fetchmany(2);
#line (88, 5) - (88, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(rows));
#line (89, 5) - (89, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestFetchmanyDefaultUsesArraysize()
            {
#line (94, 5) - (94, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _PopulatedConn();
#line (95, 5) - (95, 58) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Execute("SELECT id FROM t ORDER BY id");
#line (96, 5) - (96, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                cursor.Arraysize = 2;
#line (97, 5) - (97, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var rows = cursor.Fetchmany();
#line (98, 5) - (98, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(rows));
#line (99, 5) - (99, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestFetchmanyReturnsFewerWhenNotEnoughRows()
            {
#line (104, 5) - (104, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _PopulatedConn();
#line (105, 5) - (105, 58) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Execute("SELECT id FROM t ORDER BY id");
#line (106, 5) - (106, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var rows = cursor.Fetchmany(10);
#line (107, 5) - (107, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(rows));
#line (108, 5) - (108, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestFetchmanyNoReaderReturnsEmptyList()
            {
#line (113, 5) - (113, 19) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _Conn();
#line (114, 5) - (114, 48) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("CREATE TABLE t (id INTEGER)");
#line (115, 5) - (115, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Cursor();
#line (116, 5) - (116, 47) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                cursor.Execute("INSERT INTO t VALUES (1)");
#line (117, 5) - (117, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var rows = cursor.Fetchmany(5);
#line (118, 5) - (118, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(rows));
#line (119, 5) - (119, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestFetchallReturnsAllRows()
            {
#line (126, 5) - (126, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _PopulatedConn();
#line (127, 5) - (127, 58) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Execute("SELECT id FROM t ORDER BY id");
#line (128, 5) - (128, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var rows = cursor.Fetchall();
#line (129, 5) - (129, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(rows));
#line (131, 5) - (135, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                switch ((object[])rows[0])
#line hidden
                {
                    case object[] first:
#line (133, 13) - (133, 41) 24 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        Xunit.Assert.True(_EqInt(global::Sharpy.ArrayHelpers.GetItem(first, 0), 1));
#line hidden
                        break;
                    default:
#line (135, 13) - (135, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }

#line (137, 5) - (141, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                switch ((object[])rows[2])
#line hidden
                {
                    case object[] last:
#line (139, 13) - (139, 40) 24 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        Xunit.Assert.True(_EqInt(global::Sharpy.ArrayHelpers.GetItem(last, 0), 3));
#line hidden
                        break;
                    default:
#line (141, 13) - (141, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }

#line (142, 5) - (142, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestFetchallNoResultsReturnsEmptyList()
            {
#line (147, 5) - (147, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _PopulatedConn();
#line (148, 5) - (148, 61) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Execute("SELECT id FROM t WHERE id = 999");
#line (149, 5) - (149, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var rows = cursor.Fetchall();
#line (150, 5) - (150, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(rows));
#line (151, 5) - (151, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestFetchallAfterPartialFetchReturnsRemaining()
            {
#line (156, 5) - (156, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _PopulatedConn();
#line (157, 5) - (157, 58) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Execute("SELECT id FROM t ORDER BY id");
#line (158, 5) - (158, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                cursor.Fetchone();
#line (159, 5) - (159, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var remaining = cursor.Fetchall();
#line (160, 5) - (160, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(remaining));
#line (161, 5) - (161, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestRowcountAfterInsertReturnsAffectedCount()
            {
#line (168, 5) - (168, 19) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _Conn();
#line (169, 5) - (169, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("CREATE TABLE t (val INTEGER)");
#line (170, 5) - (170, 54) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Execute("INSERT INTO t VALUES (1)");
#line (171, 5) - (171, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Equal(1, cursor.Rowcount);
#line (172, 5) - (172, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestRowcountAfterUpdateReturnsAffectedCount()
            {
#line (177, 5) - (177, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _PopulatedConn();
#line (178, 5) - (178, 73) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Execute("UPDATE t SET score = 10.0 WHERE score < 9.0");
#line (179, 5) - (179, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Equal(2, cursor.Rowcount);
#line (180, 5) - (180, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestRowcountAfterDeleteReturnsAffectedCount()
            {
#line (185, 5) - (185, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _PopulatedConn();
#line (186, 5) - (186, 56) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Execute("DELETE FROM t WHERE id = 1");
#line (187, 5) - (187, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Equal(1, cursor.Rowcount);
#line (188, 5) - (188, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestRowcountAfterSelectIsMinusOne()
            {
#line (193, 5) - (193, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _PopulatedConn();
#line (194, 5) - (194, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Execute("SELECT id FROM t");
#line (195, 5) - (195, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Equal(-1, cursor.Rowcount);
#line (196, 5) - (196, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestRowcountInitialValueIsMinusOne()
            {
#line (201, 5) - (201, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (202, 5) - (202, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Cursor();
#line (203, 5) - (203, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Equal(-1, cursor.Rowcount);
#line (204, 5) - (204, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestExecuteParameterizedInsertBindsValues()
            {
#line (211, 5) - (211, 19) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _Conn();
#line (212, 5) - (212, 63) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("CREATE TABLE t (a TEXT, b INTEGER, c REAL)");
#line (213, 5) - (213, 48) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Sharpy.List<object> @params = new Sharpy.List<object>()
#line hidden
                {
                    "hello",
                    42,
                    3.14d
                };
#line (214, 5) - (214, 59) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("INSERT INTO t VALUES (?, ?, ?)", @params);
#line (215, 5) - (215, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Commit();
#line (217, 5) - (217, 51) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Execute("SELECT a, b, c FROM t");
#line (218, 5) - (224, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                switch ((object[])cursor.Fetchone())
#line hidden
                {
                    case object[] r:
#line (220, 13) - (220, 47) 24 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        Xunit.Assert.True(@operator.Eq(global::Sharpy.ArrayHelpers.GetItem(r, 0), "hello"));
#line (221, 13) - (221, 38) 24 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        Xunit.Assert.True(_EqInt(global::Sharpy.ArrayHelpers.GetItem(r, 1), 42));
#line (222, 13) - (222, 44) 24 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        Xunit.Assert.True(@operator.Eq(global::Sharpy.ArrayHelpers.GetItem(r, 2), 3.14d));
#line hidden
                        break;
                    default:
#line (224, 13) - (224, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }

#line (225, 5) - (225, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestExecuteParameterizedSelectFiltersCorrectly()
            {
#line (230, 5) - (230, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _PopulatedConn();
#line (231, 5) - (231, 66) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Execute("SELECT name FROM t WHERE id = ?", new Sharpy.List<int>() { 2 });
#line (232, 5) - (236, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                switch ((object[])cursor.Fetchone())
#line hidden
                {
                    case object[] r:
#line (234, 13) - (234, 45) 24 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        Xunit.Assert.True(@operator.Eq(global::Sharpy.ArrayHelpers.GetItem(r, 0), "Bob"));
#line hidden
                        break;
                    default:
#line (236, 13) - (236, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }

#line (237, 5) - (237, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestExecuteNullParameterInsertsNull()
            {
#line (242, 5) - (242, 19) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _Conn();
#line (243, 5) - (243, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("CREATE TABLE t (val TEXT)");
#line (244, 5) - (244, 40) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Sharpy.List<object> nullParams = new Sharpy.List<object>()
#line hidden
                {
                    null
                };
#line (245, 5) - (245, 58) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("INSERT INTO t VALUES (?)", nullParams);
#line (246, 5) - (246, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Commit();
#line (248, 5) - (248, 47) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Execute("SELECT val FROM t");
#line (249, 5) - (253, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                switch ((object[])cursor.Fetchone())
#line hidden
                {
                    case object[] r:
#line (251, 13) - (251, 33) 24 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        Xunit.Assert.Null(global::Sharpy.ArrayHelpers.GetItem(r, 0));
#line hidden
                        break;
                    default:
#line (253, 13) - (253, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }

#line (254, 5) - (254, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDescriptionAfterSelectContainsColumnInfo()
            {
#line (261, 5) - (261, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _PopulatedConn();
#line (262, 5) - (262, 59) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Execute("SELECT id, name, score FROM t");
#line (263, 5) - (263, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.NotNull(cursor.Description);
#line (264, 5) - (264, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(cursor.Description));
#line (265, 5) - (265, 56) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.True(@operator.Eq(cursor.Description[0][0], (string?)"id"));
#line (266, 5) - (266, 58) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.True(@operator.Eq(cursor.Description[1][0], (string?)"name"));
#line (267, 5) - (267, 59) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.True(@operator.Eq(cursor.Description[2][0], (string?)"score"));
#line (268, 5) - (268, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDescriptionAfterInsertIsNone()
            {
#line (273, 5) - (273, 19) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _Conn();
#line (274, 5) - (274, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("CREATE TABLE t (val INTEGER)");
#line (275, 5) - (275, 54) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Execute("INSERT INTO t VALUES (1)");
#line (276, 5) - (276, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Null(cursor.Description);
#line (277, 5) - (277, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDescriptionSevenElementTuples()
            {
#line (282, 5) - (282, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _PopulatedConn();
#line (283, 5) - (283, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Execute("SELECT id FROM t");
#line (284, 5) - (284, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.NotNull(cursor.Description);
#line (285, 5) - (285, 44) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Equal(7, global::Sharpy.Builtins.Len(cursor.Description[0]));
#line (286, 5) - (286, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLastrowidAfterInsertReturnsRowId()
            {
#line (293, 5) - (293, 19) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _Conn();
#line (294, 5) - (294, 70) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("CREATE TABLE t (id INTEGER PRIMARY KEY, val TEXT)");
#line (295, 5) - (295, 65) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Execute("INSERT INTO t (val) VALUES ('test')");
#line (296, 5) - (296, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.True(cursor.Lastrowid > 0);
#line (297, 5) - (297, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLastrowidAfterMultipleInsertsReturnsLastId()
            {
#line (302, 5) - (302, 19) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _Conn();
#line (303, 5) - (303, 70) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("CREATE TABLE t (id INTEGER PRIMARY KEY, val TEXT)");
#line (304, 5) - (304, 57) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("INSERT INTO t (val) VALUES ('first')");
#line (305, 5) - (305, 67) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Execute("INSERT INTO t (val) VALUES ('second')");
#line (306, 5) - (306, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Equal(2, cursor.Lastrowid);
#line (307, 5) - (307, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLastrowidInitialValueIsMinusOne()
            {
#line (312, 5) - (312, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (313, 5) - (313, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Cursor();
#line (314, 5) - (314, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Equal(-1, cursor.Lastrowid);
#line (315, 5) - (315, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestExecutemanyInsertsAllParameterSets()
            {
#line (322, 5) - (322, 19) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _Conn();
#line (323, 5) - (323, 47) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("CREATE TABLE t (name TEXT)");
#line (325, 5) - (329, 6) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var paramSets = new Sharpy.List<Sharpy.List<string>>()
#line hidden
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
#line (330, 5) - (330, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Cursor();
#line (331, 5) - (331, 63) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                cursor.Executemany("INSERT INTO t VALUES (?)", paramSets);
#line (332, 5) - (332, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Equal(3, cursor.Rowcount);
#line (333, 5) - (333, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Commit();
#line (335, 5) - (335, 59) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var selectCursor = conn.Execute("SELECT COUNT(*) FROM t");
#line (336, 5) - (340, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                switch ((object[])selectCursor.Fetchone())
#line hidden
                {
                    case object[] r:
#line (338, 13) - (338, 37) 24 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        Xunit.Assert.True(_EqInt(global::Sharpy.ArrayHelpers.GetItem(r, 0), 3));
#line hidden
                        break;
                    default:
#line (340, 13) - (340, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }

#line (341, 5) - (341, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestExecutemanyEmptySequenceRowcountIsZero()
            {
#line (346, 5) - (346, 19) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _Conn();
#line (347, 5) - (347, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("CREATE TABLE t (val INTEGER)");
#line (348, 5) - (348, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Cursor();
#line (349, 5) - (349, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Sharpy.List<Sharpy.List<int>> empty = new Sharpy.List<Sharpy.List<int>>()
#line hidden
                {
                };
#line (350, 5) - (350, 58) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                cursor.Executemany("INSERT INTO t VALUES (?)", empty);
#line (351, 5) - (351, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Equal(0, cursor.Rowcount);
#line (352, 5) - (352, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestForeachIteratesAllRows()
            {
#line (359, 5) - (359, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _PopulatedConn();
#line (360, 5) - (360, 58) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Execute("SELECT id FROM t ORDER BY id");
#line (362, 5) - (362, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Sharpy.List<object> ids = new Sharpy.List<object>()
#line hidden
                {
                };
#line (363, 5) - (368, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                foreach (var __loopVar_0 in cursor)
#line hidden
                {
                    var row = __loopVar_0;
#line (364, 9) - (368, 30) 20 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                    switch ((object[])row)
#line hidden
                    {
                        case object[] r:
#line (366, 17) - (366, 33) 28 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                            ids.Append(global::Sharpy.ArrayHelpers.GetItem(r, 0));
#line hidden
                            break;
                        default:
#line (368, 17) - (368, 30) 28 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                            Xunit.Assert.True(false);
#line hidden
                            break;
                    }
                }

#line (370, 5) - (370, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(ids));
#line (371, 5) - (371, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.True(_EqInt(ids.GetItemUnchecked(0), 1));
#line (372, 5) - (372, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.True(_EqInt(ids.GetItemUnchecked(1), 2));
#line (373, 5) - (373, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.True(_EqInt(ids.GetItemUnchecked(2), 3));
#line (374, 5) - (374, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestTypeMappingIntegerReturnsLong()
            {
#line (381, 5) - (381, 19) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _Conn();
#line (382, 5) - (382, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("CREATE TABLE t (val INTEGER)");
#line (383, 5) - (383, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("INSERT INTO t VALUES (42)");
#line (384, 5) - (384, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Commit();
#line (386, 5) - (386, 47) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Execute("SELECT val FROM t");
#line (387, 5) - (391, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                switch ((object[])cursor.Fetchone())
#line hidden
                {
                    case object[] r:
#line (389, 13) - (389, 38) 24 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        Xunit.Assert.True(_EqInt(global::Sharpy.ArrayHelpers.GetItem(r, 0), 42));
#line hidden
                        break;
                    default:
#line (391, 13) - (391, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }

#line (392, 5) - (392, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestTypeMappingTextReturnsString()
            {
#line (397, 5) - (397, 19) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _Conn();
#line (398, 5) - (398, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("CREATE TABLE t (val TEXT)");
#line (399, 5) - (399, 51) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("INSERT INTO t VALUES ('hello')");
#line (400, 5) - (400, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Commit();
#line (402, 5) - (402, 47) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Execute("SELECT val FROM t");
#line (403, 5) - (407, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                switch ((object[])cursor.Fetchone())
#line hidden
                {
                    case object[] r:
#line (405, 13) - (405, 47) 24 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        Xunit.Assert.True(@operator.Eq(global::Sharpy.ArrayHelpers.GetItem(r, 0), "hello"));
#line hidden
                        break;
                    default:
#line (407, 13) - (407, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }

#line (408, 5) - (408, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestTypeMappingRealReturnsDouble()
            {
#line (413, 5) - (413, 19) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _Conn();
#line (414, 5) - (414, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("CREATE TABLE t (val REAL)");
#line (415, 5) - (415, 48) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("INSERT INTO t VALUES (3.14)");
#line (416, 5) - (416, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Commit();
#line (418, 5) - (418, 47) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Execute("SELECT val FROM t");
#line (419, 5) - (423, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                switch ((object[])cursor.Fetchone())
#line hidden
                {
                    case object[] r:
#line (421, 13) - (421, 44) 24 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        Xunit.Assert.True(@operator.Eq(global::Sharpy.ArrayHelpers.GetItem(r, 0), 3.14d));
#line hidden
                        break;
                    default:
#line (423, 13) - (423, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }

#line (424, 5) - (424, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestTypeMappingBlobReturnsBytes()
            {
#line (429, 5) - (429, 19) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _Conn();
#line (430, 5) - (430, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("CREATE TABLE t (val BLOB)");
#line (431, 5) - (431, 40) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                global::Sharpy.Bytes blobData = new Sharpy.Bytes(new byte[] { 1, 2, 3 });
#line (432, 5) - (432, 58) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("INSERT INTO t VALUES (?)", new Sharpy.List<global::Sharpy.Bytes>() { blobData });
#line (433, 5) - (433, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Commit();
#line (435, 5) - (435, 47) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Execute("SELECT val FROM t");
#line (436, 5) - (446, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                switch ((object[])cursor.Fetchone())
#line hidden
                {
                    case object[] r:
#line (438, 13) - (444, 34) 24 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        switch (global::Sharpy.ArrayHelpers.GetItem(r, 0))
#line hidden
                        {
                            case global::Sharpy.Bytes result:
#line (440, 21) - (440, 43) 32 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                                Xunit.Assert.Equal(1, result[0]);
#line (441, 21) - (441, 43) 32 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                                Xunit.Assert.Equal(2, result[1]);
#line (442, 21) - (442, 43) 32 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                                Xunit.Assert.Equal(3, result[2]);
#line hidden
                                break;
                            default:
#line (444, 21) - (444, 34) 32 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                                Xunit.Assert.True(false);
#line hidden
                                break;
                        }

                        break;
                    default:
#line (446, 13) - (446, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }

#line (447, 5) - (447, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestExecuteBadSqlThrowsOperationalError()
            {
#line (454, 5) - (454, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (455, 5) - (456, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                bool __raised_1 = false;
#line hidden
                try
                {
#line (456, 9) - (456, 46) 20 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                    conn.Execute("INVALID SQL STATEMENT");
#line hidden
                }
                catch (Sqlite3OperationalError)
                {
                    __raised_1 = true;
                }

                if (!__raised_1)
                    throw new global::Sharpy.AssertionError("Expected Sqlite3OperationalError to be raised, but no exception was raised");
#line (457, 5) - (457, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestExecuteConstraintViolationThrowsIntegrityError()
            {
#line (462, 5) - (462, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (463, 5) - (463, 60) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("CREATE TABLE t (id INTEGER PRIMARY KEY)");
#line (464, 5) - (464, 45) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("INSERT INTO t VALUES (1)");
#line (465, 5) - (466, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                bool __raised_2 = false;
#line hidden
                try
                {
#line (466, 9) - (466, 49) 20 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                    conn.Execute("INSERT INTO t VALUES (1)");
#line hidden
                }
                catch (Sqlite3IntegrityError)
                {
                    __raised_2 = true;
                }

                if (!__raised_2)
                    throw new global::Sharpy.AssertionError("Expected Sqlite3IntegrityError to be raised, but no exception was raised");
#line (467, 5) - (467, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestExecuteUniqueConstraintViolationThrowsIntegrityError()
            {
#line (472, 5) - (472, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (473, 5) - (473, 54) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("CREATE TABLE t (name TEXT UNIQUE)");
#line (474, 5) - (474, 51) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("INSERT INTO t VALUES ('Alice')");
#line (475, 5) - (476, 55) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                bool __raised_3 = false;
#line hidden
                try
                {
#line (476, 9) - (476, 55) 20 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                    conn.Execute("INSERT INTO t VALUES ('Alice')");
#line hidden
                }
                catch (Sqlite3IntegrityError)
                {
                    __raised_3 = true;
                }

                if (!__raised_3)
                    throw new global::Sharpy.AssertionError("Expected Sqlite3IntegrityError to be raised, but no exception was raised");
#line (477, 5) - (477, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestExecuteNotNullViolationThrowsIntegrityError()
            {
#line (482, 5) - (482, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (483, 5) - (483, 56) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Execute("CREATE TABLE t (name TEXT NOT NULL)");
#line (484, 5) - (485, 52) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                bool __raised_4 = false;
#line hidden
                try
                {
#line (485, 9) - (485, 52) 20 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                    conn.Execute("INSERT INTO t VALUES (NULL)");
#line hidden
                }
                catch (Sqlite3IntegrityError)
                {
                    __raised_4 = true;
                }

                if (!__raised_4)
                    throw new global::Sharpy.AssertionError("Expected Sqlite3IntegrityError to be raised, but no exception was raised");
#line (486, 5) - (486, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestCursorCloseThenExecuteThrowsProgrammingError()
            {
#line (493, 5) - (493, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (494, 5) - (494, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Cursor();
#line (495, 5) - (495, 19) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                cursor.Close();
#line (496, 5) - (497, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                bool __raised_5 = false;
#line hidden
                try
                {
#line (497, 9) - (497, 35) 20 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                    cursor.Execute("SELECT 1");
#line hidden
                }
                catch (Sqlite3ProgrammingError)
                {
                    __raised_5 = true;
                }

                if (!__raised_5)
                    throw new global::Sharpy.AssertionError("Expected Sqlite3ProgrammingError to be raised, but no exception was raised");
#line (498, 5) - (498, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestCursorCloseThenFetchoneThrowsProgrammingError()
            {
#line (503, 5) - (503, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (504, 5) - (504, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Cursor();
#line (505, 5) - (505, 19) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                cursor.Close();
#line (506, 5) - (507, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                bool __raised_6 = false;
#line hidden
                try
                {
#line (507, 9) - (507, 26) 20 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                    cursor.Fetchone();
#line hidden
                }
                catch (Sqlite3ProgrammingError)
                {
                    __raised_6 = true;
                }

                if (!__raised_6)
                    throw new global::Sharpy.AssertionError("Expected Sqlite3ProgrammingError to be raised, but no exception was raised");
#line (508, 5) - (508, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestCursorCloseThenFetchallThrowsProgrammingError()
            {
#line (513, 5) - (513, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (514, 5) - (514, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Cursor();
#line (515, 5) - (515, 19) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                cursor.Close();
#line (516, 5) - (517, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                bool __raised_7 = false;
#line hidden
                try
                {
#line (517, 9) - (517, 26) 20 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                    cursor.Fetchall();
#line hidden
                }
                catch (Sqlite3ProgrammingError)
                {
                    __raised_7 = true;
                }

                if (!__raised_7)
                    throw new global::Sharpy.AssertionError("Expected Sqlite3ProgrammingError to be raised, but no exception was raised");
#line (518, 5) - (518, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestCursorCloseThenFetchmanyThrowsProgrammingError()
            {
#line (523, 5) - (523, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (524, 5) - (524, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Cursor();
#line (525, 5) - (525, 19) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                cursor.Close();
#line (526, 5) - (527, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                bool __raised_8 = false;
#line hidden
                try
                {
#line (527, 9) - (527, 28) 20 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                    cursor.Fetchmany(5);
#line hidden
                }
                catch (Sqlite3ProgrammingError)
                {
                    __raised_8 = true;
                }

                if (!__raised_8)
                    throw new global::Sharpy.AssertionError("Expected Sqlite3ProgrammingError to be raised, but no exception was raised");
#line (528, 5) - (528, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestArraysizeDefaultIsOne()
            {
#line (535, 5) - (535, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (536, 5) - (536, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Cursor();
#line (537, 5) - (537, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Equal(1, cursor.Arraysize);
#line (538, 5) - (538, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestArraysizeCanBeSet()
            {
#line (543, 5) - (543, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (544, 5) - (544, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Cursor();
#line (545, 5) - (545, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                cursor.Arraysize = 10;
#line (546, 5) - (546, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Equal(10, cursor.Arraysize);
#line (547, 5) - (547, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestExecutescriptBadSqlThrowsOperationalError()
            {
#line (554, 5) - (554, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (555, 5) - (555, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cursor = conn.Cursor();
#line (556, 5) - (557, 51) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                bool __raised_9 = false;
#line hidden
                try
                {
#line (557, 9) - (557, 51) 20 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                    cursor.Executescript("INVALID; SQL; HERE");
#line hidden
                }
                catch (Sqlite3OperationalError)
                {
                    __raised_9 = true;
                }

                if (!__raised_9)
                    throw new global::Sharpy.AssertionError("Expected Sqlite3OperationalError to be raised, but no exception was raised");
#line (558, 5) - (558, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestMultipleCursorsOnSameConnectionWorkIndependently()
            {
#line (565, 5) - (565, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var conn = _PopulatedConn();
#line (567, 5) - (567, 56) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cur1 = conn.Execute("SELECT id FROM t ORDER BY id");
#line (568, 5) - (568, 60) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var cur2 = conn.Execute("SELECT name FROM t ORDER BY name");
#line (570, 5) - (574, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                switch ((object[])cur1.Fetchone())
#line hidden
                {
                    case object[] row1:
#line (572, 13) - (572, 40) 24 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        Xunit.Assert.True(_EqInt(global::Sharpy.ArrayHelpers.GetItem(row1, 0), 1));
#line hidden
                        break;
                    default:
#line (574, 13) - (574, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }

#line (576, 5) - (580, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                switch ((object[])cur2.Fetchone())
#line hidden
                {
                    case object[] row2:
#line (578, 13) - (578, 50) 24 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        Xunit.Assert.True(@operator.Eq(global::Sharpy.ArrayHelpers.GetItem(row2, 0), "Alice"));
#line hidden
                        break;
                    default:
#line (580, 13) - (580, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }

#line (582, 5) - (582, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var remaining1 = cur1.Fetchall();
#line (583, 5) - (583, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(remaining1));
#line (585, 5) - (585, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                var remaining2 = cur2.Fetchall();
#line (586, 5) - (586, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(remaining2));
#line (587, 5) - (587, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_cursor_tests.spy"
                conn.Close();
#line hidden
            }
        }
    }
}
#line default
