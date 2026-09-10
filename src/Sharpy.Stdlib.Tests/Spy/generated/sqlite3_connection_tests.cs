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
using static Sharpy.Stdlib.Tests.Spy.Sqlite3.Sqlite3ConnectionTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Sqlite3
    {
        [global::Sharpy.SharpyModule("sqlite3.sqlite3_connection_tests")]
        public static partial class Sqlite3ConnectionTests
        {
            internal static bool _EqInt(object value, long expected)
            {
#line (31, 5) - (31, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                return @operator.Eq(value, expected);
#line hidden
            }

            internal static global::Sharpy.Sqlite3Connection _Conn()
            {
#line (35, 5) - (35, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (36, 5) - (36, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                conn.RowFactory = sqlite3.Row;
#line (37, 5) - (37, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                return conn;
#line hidden
            }
        }
    }

    public static partial class Sqlite3
    {
        public partial class Sqlite3ConnectionTestsTests
        {
            [Xunit.FactAttribute]
            public void TestConnectMemoryDatabaseSucceeds()
            {
#line (44, 5) - (44, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (45, 5) - (45, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                Xunit.Assert.NotNull(conn);
#line (46, 5) - (46, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                conn.Close();
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestExecuteCreateTableSucceeds()
            {
#line (53, 5) - (53, 19) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                var conn = _Conn();
#line (54, 5) - (54, 80) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                var cursor = conn.Execute("CREATE TABLE t (id INTEGER PRIMARY KEY, name TEXT)");
#line (55, 5) - (55, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                Xunit.Assert.NotNull(cursor);
#line (56, 5) - (56, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                conn.Close();
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestExecuteInsertAndSelectReturnsData()
            {
#line (61, 5) - (61, 19) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                var conn = _Conn();
#line (62, 5) - (62, 71) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                conn.Execute("CREATE TABLE t (id INTEGER PRIMARY KEY, name TEXT)");
#line (63, 5) - (63, 54) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                conn.Execute("INSERT INTO t VALUES (1, 'Alice')");
#line (64, 5) - (64, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                conn.Commit();
#line (66, 5) - (66, 52) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                var cursor = conn.Execute("SELECT id, name FROM t");
#line (67, 5) - (72, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                switch (cursor.Fetchone())
#line hidden
                {
                    case global::Sharpy.Sqlite3Row r:
#line (69, 13) - (69, 37) 24 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                        Xunit.Assert.True(_EqInt(r[0], 1));
#line (70, 13) - (70, 47) 24 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                        Xunit.Assert.True(@operator.Eq(r[1], "Alice"));
#line hidden
                        break;
                    default:
#line (72, 13) - (72, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }

#line (73, 5) - (73, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                conn.Close();
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestExecuteWithParametersBindsCorrectly()
            {
#line (78, 5) - (78, 19) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                var conn = _Conn();
#line (79, 5) - (79, 59) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                conn.Execute("CREATE TABLE t (id INTEGER, name TEXT)");
#line (80, 5) - (80, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                Sharpy.List<object> @params = new Sharpy.List<object>()
#line hidden
                {
                    1,
                    "Bob"
                };
#line (81, 5) - (81, 56) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                conn.Execute("INSERT INTO t VALUES (?, ?)", @params);
#line (82, 5) - (82, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                conn.Commit();
#line (84, 5) - (84, 66) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                var cursor = conn.Execute("SELECT name FROM t WHERE id = ?", new Sharpy.List<int>() { 1 });
#line (85, 5) - (89, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                switch (cursor.Fetchone())
#line hidden
                {
                    case global::Sharpy.Sqlite3Row r:
#line (87, 13) - (87, 45) 24 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                        Xunit.Assert.True(@operator.Eq(r[0], "Bob"));
#line hidden
                        break;
                    default:
#line (89, 13) - (89, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }

#line (90, 5) - (90, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                conn.Close();
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestCommitPersistsData()
            {
#line (97, 5) - (97, 19) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                var conn = _Conn();
#line (98, 5) - (98, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                conn.Execute("CREATE TABLE t (val INTEGER)");
#line (99, 5) - (99, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                conn.Execute("INSERT INTO t VALUES (42)");
#line (100, 5) - (100, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                conn.Commit();
#line (102, 5) - (102, 47) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                var cursor = conn.Execute("SELECT val FROM t");
#line (103, 5) - (107, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                switch (cursor.Fetchone())
#line hidden
                {
                    case global::Sharpy.Sqlite3Row r:
#line (105, 13) - (105, 38) 24 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                        Xunit.Assert.True(_EqInt(r[0], 42));
#line hidden
                        break;
                    default:
#line (107, 13) - (107, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }

#line (108, 5) - (108, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                conn.Close();
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestRollbackDiscardsUncommittedData()
            {
#line (113, 5) - (113, 19) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                var conn = _Conn();
#line (114, 5) - (114, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                conn.Execute("CREATE TABLE t (val INTEGER)");
#line (115, 5) - (115, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                conn.Commit();
#line (117, 5) - (117, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                conn.Execute("INSERT INTO t VALUES (99)");
#line (118, 5) - (118, 20) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                conn.Rollback();
#line (120, 5) - (120, 52) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                var cursor = conn.Execute("SELECT COUNT(*) FROM t");
#line (121, 5) - (125, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                switch (cursor.Fetchone())
#line hidden
                {
                    case global::Sharpy.Sqlite3Row r:
#line (123, 13) - (123, 37) 24 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                        Xunit.Assert.True(_EqInt(r[0], 0));
#line hidden
                        break;
                    default:
#line (125, 13) - (125, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }

#line (126, 5) - (126, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                conn.Close();
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestCommitWithNoTransactionDoesNotThrow()
            {
#line (131, 5) - (131, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (132, 5) - (132, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                conn.Commit();
#line (133, 5) - (133, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                conn.Close();
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestRollbackWithNoTransactionDoesNotThrow()
            {
#line (138, 5) - (138, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (139, 5) - (139, 20) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                conn.Rollback();
#line (140, 5) - (140, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                conn.Close();
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestCloseThenExecuteThrowsProgrammingError()
            {
#line (147, 5) - (147, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (148, 5) - (148, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                conn.Close();
#line (149, 5) - (150, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                bool __raised_0 = false;
#line hidden
                try
                {
#line (150, 9) - (150, 33) 20 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                    conn.Execute("SELECT 1");
#line hidden
                }
                catch (Sqlite3ProgrammingError)
                {
                    __raised_0 = true;
                }

                if (!__raised_0)
                    throw new global::Sharpy.AssertionError("Expected Sqlite3ProgrammingError to be raised, but no exception was raised");
            }

            [Xunit.FactAttribute]
            public void TestCloseThenCursorThrowsProgrammingError()
            {
#line (155, 5) - (155, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (156, 5) - (156, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                conn.Close();
#line (157, 5) - (158, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                bool __raised_1 = false;
#line hidden
                try
                {
#line (158, 9) - (158, 22) 20 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                    conn.Cursor();
#line hidden
                }
                catch (Sqlite3ProgrammingError)
                {
                    __raised_1 = true;
                }

                if (!__raised_1)
                    throw new global::Sharpy.AssertionError("Expected Sqlite3ProgrammingError to be raised, but no exception was raised");
            }

            [Xunit.FactAttribute]
            public void TestCloseThenCommitThrowsProgrammingError()
            {
#line (163, 5) - (163, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (164, 5) - (164, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                conn.Close();
#line (165, 5) - (166, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                bool __raised_2 = false;
#line hidden
                try
                {
#line (166, 9) - (166, 22) 20 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                    conn.Commit();
#line hidden
                }
                catch (Sqlite3ProgrammingError)
                {
                    __raised_2 = true;
                }

                if (!__raised_2)
                    throw new global::Sharpy.AssertionError("Expected Sqlite3ProgrammingError to be raised, but no exception was raised");
            }

            [Xunit.FactAttribute]
            public void TestCloseThenRollbackThrowsProgrammingError()
            {
#line (171, 5) - (171, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (172, 5) - (172, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                conn.Close();
#line (173, 5) - (174, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                bool __raised_3 = false;
#line hidden
                try
                {
#line (174, 9) - (174, 24) 20 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                    conn.Rollback();
#line hidden
                }
                catch (Sqlite3ProgrammingError)
                {
                    __raised_3 = true;
                }

                if (!__raised_3)
                    throw new global::Sharpy.AssertionError("Expected Sqlite3ProgrammingError to be raised, but no exception was raised");
            }

            [Xunit.FactAttribute]
            public void TestCloseCalledTwiceDoesNotThrow()
            {
#line (179, 5) - (179, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (180, 5) - (180, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                conn.Close();
#line (181, 5) - (181, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                conn.Close();
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestExecutemanyInsertsMultipleRows()
            {
#line (188, 5) - (188, 19) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                var conn = _Conn();
#line (189, 5) - (189, 59) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                conn.Execute("CREATE TABLE t (id INTEGER, name TEXT)");
#line (191, 5) - (195, 7) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                Sharpy.List<Sharpy.List<object>> paramSets = new Sharpy.List<Sharpy.List<object>>()
#line hidden
                {
                    new Sharpy.List<object>()
                    {
                        1,
                        "Alice"
                    },
                    new Sharpy.List<object>()
                    {
                        2,
                        "Bob"
                    },
                    new Sharpy.List<object>()
                    {
                        3,
                        "Charlie"
                    }
                };
#line (196, 5) - (196, 64) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                conn.Executemany("INSERT INTO t VALUES (?, ?)", paramSets);
#line (197, 5) - (197, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                conn.Commit();
#line (199, 5) - (199, 52) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                var cursor = conn.Execute("SELECT COUNT(*) FROM t");
#line (200, 5) - (204, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                switch (cursor.Fetchone())
#line hidden
                {
                    case global::Sharpy.Sqlite3Row r:
#line (202, 13) - (202, 37) 24 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                        Xunit.Assert.True(_EqInt(r[0], 3));
#line hidden
                        break;
                    default:
#line (204, 13) - (204, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }

#line (205, 5) - (205, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                conn.Close();
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestExecutemanyReturnsCorrectRowcount()
            {
#line (210, 5) - (210, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (211, 5) - (211, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                conn.Execute("CREATE TABLE t (val INTEGER)");
#line (213, 5) - (216, 6) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                var paramSets = new Sharpy.List<Sharpy.List<int>>()
#line hidden
                {
                    new Sharpy.List<int>()
                    {
                        1
                    },
                    new Sharpy.List<int>()
                    {
                        2
                    }
                };
#line (217, 5) - (217, 70) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                var cursor = conn.Executemany("INSERT INTO t VALUES (?)", paramSets);
#line (218, 5) - (218, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                Xunit.Assert.Equal(2, cursor.Rowcount);
#line (219, 5) - (219, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                conn.Close();
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestExecutescriptExecutesMultipleStatements()
            {
#line (226, 5) - (226, 19) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                var conn = _Conn();
#line (228, 5) - (228, 140) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                conn.Executescript("CREATE TABLE t1 (id INTEGER); CREATE TABLE t2 (id INTEGER); INSERT INTO t1 VALUES (1); INSERT INTO t2 VALUES (2);");
#line (230, 5) - (230, 48) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                var cursor1 = conn.Execute("SELECT id FROM t1");
#line (231, 5) - (235, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                switch (cursor1.Fetchone())
#line hidden
                {
                    case global::Sharpy.Sqlite3Row r1:
#line (233, 13) - (233, 38) 24 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                        Xunit.Assert.True(_EqInt(r1[0], 1));
#line hidden
                        break;
                    default:
#line (235, 13) - (235, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }

#line (237, 5) - (237, 48) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                var cursor2 = conn.Execute("SELECT id FROM t2");
#line (238, 5) - (242, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                switch (cursor2.Fetchone())
#line hidden
                {
                    case global::Sharpy.Sqlite3Row r2:
#line (240, 13) - (240, 38) 24 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                        Xunit.Assert.True(_EqInt(r2[0], 2));
#line hidden
                        break;
                    default:
#line (242, 13) - (242, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }

#line (243, 5) - (243, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                conn.Close();
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestExecutescriptCommitsPendingTransaction()
            {
#line (248, 5) - (248, 19) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                var conn = _Conn();
#line (249, 5) - (249, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                conn.Execute("CREATE TABLE t (val INTEGER)");
#line (250, 5) - (250, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                conn.Execute("INSERT INTO t VALUES (10)");
#line (251, 5) - (251, 56) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                conn.Executescript("CREATE TABLE t2 (val INTEGER)");
#line (253, 5) - (253, 47) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                var cursor = conn.Execute("SELECT val FROM t");
#line (254, 5) - (258, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                switch (cursor.Fetchone())
#line hidden
                {
                    case global::Sharpy.Sqlite3Row r:
#line (256, 13) - (256, 38) 24 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                        Xunit.Assert.True(_EqInt(r[0], 10));
#line hidden
                        break;
                    default:
#line (258, 13) - (258, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }

#line (259, 5) - (259, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                conn.Close();
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestRowFactoryDefaultIsNone()
            {
#line (266, 5) - (266, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (267, 5) - (267, 37) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                Xunit.Assert.Null(conn.RowFactory);
#line (268, 5) - (268, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                conn.Close();
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestRowFactoryCanBeSet()
            {
#line (273, 5) - (273, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (274, 5) - (274, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                conn.RowFactory = sqlite3.Row;
#line (275, 5) - (275, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                Xunit.Assert.NotNull(conn.RowFactory);
#line (276, 5) - (276, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                conn.Close();
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestExecuteInvalidSqlThrowsOperationalError()
            {
#line (283, 5) - (283, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (284, 5) - (285, 38) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                bool __raised_4 = false;
#line hidden
                try
                {
#line (285, 9) - (285, 38) 20 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                    conn.Execute("NOT VALID SQL");
#line hidden
                }
                catch (Sqlite3OperationalError)
                {
                    __raised_4 = true;
                }

                if (!__raised_4)
                    throw new global::Sharpy.AssertionError("Expected Sqlite3OperationalError to be raised, but no exception was raised");
#line (286, 5) - (286, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                conn.Close();
#line hidden
            }
        }
    }
}
#line default
