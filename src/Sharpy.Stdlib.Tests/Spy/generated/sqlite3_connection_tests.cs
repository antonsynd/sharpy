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
#line (67, 5) - (73, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
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
#line (80, 5) - (80, 60) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                conn.Execute("INSERT INTO t VALUES (?, ?)", new Sharpy.List<object>() { 1, "Bob" });
#line (81, 5) - (81, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                conn.Commit();
#line (83, 5) - (83, 66) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                var cursor = conn.Execute("SELECT name FROM t WHERE id = ?", new Sharpy.List<int>() { 1 });
#line (84, 5) - (89, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                switch (cursor.Fetchone())
#line hidden
                {
                    case global::Sharpy.Sqlite3Row r:
#line (86, 13) - (86, 45) 24 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                        Xunit.Assert.True(@operator.Eq(r[0], "Bob"));
#line hidden
                        break;
                    default:
#line (88, 13) - (88, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }

#line (89, 5) - (89, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                conn.Close();
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestCommitPersistsData()
            {
#line (96, 5) - (96, 19) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                var conn = _Conn();
#line (97, 5) - (97, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                conn.Execute("CREATE TABLE t (val INTEGER)");
#line (98, 5) - (98, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                conn.Execute("INSERT INTO t VALUES (42)");
#line (99, 5) - (99, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                conn.Commit();
#line (101, 5) - (101, 47) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                var cursor = conn.Execute("SELECT val FROM t");
#line (102, 5) - (107, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                switch (cursor.Fetchone())
#line hidden
                {
                    case global::Sharpy.Sqlite3Row r:
#line (104, 13) - (104, 38) 24 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                        Xunit.Assert.True(_EqInt(r[0], 42));
#line hidden
                        break;
                    default:
#line (106, 13) - (106, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }

#line (107, 5) - (107, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                conn.Close();
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestRollbackDiscardsUncommittedData()
            {
#line (112, 5) - (112, 19) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                var conn = _Conn();
#line (113, 5) - (113, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                conn.Execute("CREATE TABLE t (val INTEGER)");
#line (114, 5) - (114, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                conn.Commit();
#line (116, 5) - (116, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                conn.Execute("INSERT INTO t VALUES (99)");
#line (117, 5) - (117, 20) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                conn.Rollback();
#line (119, 5) - (119, 52) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                var cursor = conn.Execute("SELECT COUNT(*) FROM t");
#line (120, 5) - (125, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                switch (cursor.Fetchone())
#line hidden
                {
                    case global::Sharpy.Sqlite3Row r:
#line (122, 13) - (122, 37) 24 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                        Xunit.Assert.True(_EqInt(r[0], 0));
#line hidden
                        break;
                    default:
#line (124, 13) - (124, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }

#line (125, 5) - (125, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                conn.Close();
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestCommitWithNoTransactionDoesNotThrow()
            {
#line (130, 5) - (130, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (131, 5) - (131, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                conn.Commit();
#line (132, 5) - (132, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                conn.Close();
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestRollbackWithNoTransactionDoesNotThrow()
            {
#line (137, 5) - (137, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (138, 5) - (138, 20) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                conn.Rollback();
#line (139, 5) - (139, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                conn.Close();
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestCloseThenExecuteThrowsProgrammingError()
            {
#line (146, 5) - (146, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (147, 5) - (147, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                conn.Close();
#line (148, 5) - (152, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                bool __raised_0 = false;
#line hidden
                try
                {
#line (149, 9) - (149, 33) 20 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
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
#line (154, 5) - (154, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (155, 5) - (155, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                conn.Close();
#line (156, 5) - (160, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                bool __raised_1 = false;
#line hidden
                try
                {
#line (157, 9) - (157, 22) 20 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
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
#line (162, 5) - (162, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (163, 5) - (163, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                conn.Close();
#line (164, 5) - (168, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                bool __raised_2 = false;
#line hidden
                try
                {
#line (165, 9) - (165, 22) 20 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
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
#line (170, 5) - (170, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (171, 5) - (171, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                conn.Close();
#line (172, 5) - (176, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                bool __raised_3 = false;
#line hidden
                try
                {
#line (173, 9) - (173, 24) 20 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
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
#line (178, 5) - (178, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (179, 5) - (179, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                conn.Close();
#line (180, 5) - (180, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                conn.Close();
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestExecutemanyInsertsMultipleRows()
            {
#line (187, 5) - (187, 19) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                var conn = _Conn();
#line (188, 5) - (188, 59) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                conn.Execute("CREATE TABLE t (id INTEGER, name TEXT)");
#line (190, 5) - (194, 6) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                var paramSets = new Sharpy.List<Sharpy.List<object>>()
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
#line (195, 5) - (195, 64) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                conn.Executemany("INSERT INTO t VALUES (?, ?)", paramSets);
#line (196, 5) - (196, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                conn.Commit();
#line (198, 5) - (198, 52) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                var cursor = conn.Execute("SELECT COUNT(*) FROM t");
#line (199, 5) - (204, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                switch (cursor.Fetchone())
#line hidden
                {
                    case global::Sharpy.Sqlite3Row r:
#line (201, 13) - (201, 37) 24 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                        Xunit.Assert.True(_EqInt(r[0], 3));
#line hidden
                        break;
                    default:
#line (203, 13) - (203, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }

#line (204, 5) - (204, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                conn.Close();
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestExecutemanyReturnsCorrectRowcount()
            {
#line (209, 5) - (209, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (210, 5) - (210, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                conn.Execute("CREATE TABLE t (val INTEGER)");
#line (212, 5) - (215, 6) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
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
#line (216, 5) - (216, 70) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                var cursor = conn.Executemany("INSERT INTO t VALUES (?)", paramSets);
#line (217, 5) - (217, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                Xunit.Assert.Equal(2, cursor.Rowcount);
#line (218, 5) - (218, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                conn.Close();
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestExecutescriptExecutesMultipleStatements()
            {
#line (225, 5) - (225, 19) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                var conn = _Conn();
#line (227, 5) - (227, 140) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                conn.Executescript("CREATE TABLE t1 (id INTEGER); CREATE TABLE t2 (id INTEGER); INSERT INTO t1 VALUES (1); INSERT INTO t2 VALUES (2);");
#line (229, 5) - (229, 48) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                var cursor1 = conn.Execute("SELECT id FROM t1");
#line (230, 5) - (236, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                switch (cursor1.Fetchone())
#line hidden
                {
                    case global::Sharpy.Sqlite3Row r1:
#line (232, 13) - (232, 38) 24 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                        Xunit.Assert.True(_EqInt(r1[0], 1));
#line hidden
                        break;
                    default:
#line (234, 13) - (234, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }

#line (236, 5) - (236, 48) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                var cursor2 = conn.Execute("SELECT id FROM t2");
#line (237, 5) - (242, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                switch (cursor2.Fetchone())
#line hidden
                {
                    case global::Sharpy.Sqlite3Row r2:
#line (239, 13) - (239, 38) 24 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                        Xunit.Assert.True(_EqInt(r2[0], 2));
#line hidden
                        break;
                    default:
#line (241, 13) - (241, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }

#line (242, 5) - (242, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                conn.Close();
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestExecutescriptCommitsPendingTransaction()
            {
#line (247, 5) - (247, 19) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                var conn = _Conn();
#line (248, 5) - (248, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                conn.Execute("CREATE TABLE t (val INTEGER)");
#line (249, 5) - (249, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                conn.Execute("INSERT INTO t VALUES (10)");
#line (250, 5) - (250, 56) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                conn.Executescript("CREATE TABLE t2 (val INTEGER)");
#line (252, 5) - (252, 47) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                var cursor = conn.Execute("SELECT val FROM t");
#line (253, 5) - (258, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                switch (cursor.Fetchone())
#line hidden
                {
                    case global::Sharpy.Sqlite3Row r:
#line (255, 13) - (255, 38) 24 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                        Xunit.Assert.True(_EqInt(r[0], 10));
#line hidden
                        break;
                    default:
#line (257, 13) - (257, 26) 24 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                        Xunit.Assert.True(false);
#line hidden
                        break;
                }

#line (258, 5) - (258, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                conn.Close();
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestRowFactoryDefaultIsNone()
            {
#line (265, 5) - (265, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (266, 5) - (266, 37) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                Xunit.Assert.Null(conn.RowFactory);
#line (267, 5) - (267, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                conn.Close();
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestRowFactoryCanBeSet()
            {
#line (272, 5) - (272, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (273, 5) - (273, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                conn.RowFactory = sqlite3.Row;
#line (274, 5) - (274, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                Xunit.Assert.NotNull(conn.RowFactory);
#line (275, 5) - (275, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                conn.Close();
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestExecuteInvalidSqlThrowsOperationalError()
            {
#line (282, 5) - (282, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (283, 5) - (285, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                bool __raised_4 = false;
#line hidden
                try
                {
#line (284, 9) - (284, 38) 20 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                    conn.Execute("NOT VALID SQL");
#line hidden
                }
                catch (Sqlite3OperationalError)
                {
                    __raised_4 = true;
                }

                if (!__raised_4)
                    throw new global::Sharpy.AssertionError("Expected Sqlite3OperationalError to be raised, but no exception was raised");
#line (285, 5) - (285, 17) 16 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_connection_tests.spy"
                conn.Close();
#line hidden
            }
        }
    }
}
#line default
