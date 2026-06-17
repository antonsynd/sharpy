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
using sqlite3 = global::Sharpy.Sqlite3;
using Xunit;
using static Sharpy.Stdlib.Tests.Spy.Sqlite3.Sqlite3ErrorTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Sqlite3
    {
        [global::Sharpy.SharpyModule("sqlite3.sqlite3_error_tests")]
        public static partial class Sqlite3ErrorTests
        {
        }
    }

    public static partial class Sqlite3
    {
        public partial class Sqlite3ErrorTestsTests
        {
            [Xunit.FactAttribute]
            public void TestOperationalErrorCaughtBySqlite3Error()
            {
#line (36, 5) - (36, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_error_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (37, 5) - (37, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_error_tests.spy"
                bool caught = false;
#line (38, 5) - (42, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_error_tests.spy"
                try
                {
#line (39, 9) - (39, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_error_tests.spy"
                    conn.Execute("NOT VALID SQL");
                }
                catch (global::Sharpy.Sqlite3Error)
                {
#line (41, 9) - (41, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_error_tests.spy"
                    caught = true;
                }

#line (42, 5) - (42, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_error_tests.spy"
                Xunit.Assert.True(caught);
#line (43, 5) - (43, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_error_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestIntegrityErrorCaughtBySqlite3DatabaseError()
            {
#line (48, 5) - (48, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_error_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (49, 5) - (49, 60) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_error_tests.spy"
                conn.Execute("CREATE TABLE t (id INTEGER PRIMARY KEY)");
#line (50, 5) - (50, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_error_tests.spy"
                conn.Execute("INSERT INTO t VALUES (1)");
#line (51, 5) - (51, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_error_tests.spy"
                bool caught = false;
#line (52, 5) - (56, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_error_tests.spy"
                try
                {
#line (53, 9) - (53, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_error_tests.spy"
                    conn.Execute("INSERT INTO t VALUES (1)");
                }
                catch (global::Sharpy.Sqlite3DatabaseError)
                {
#line (55, 9) - (55, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_error_tests.spy"
                    caught = true;
                }

#line (56, 5) - (56, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_error_tests.spy"
                Xunit.Assert.True(caught);
#line (57, 5) - (57, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_error_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestOperationalErrorIsExactlyOperationalViaAssertRaises()
            {
#line (62, 5) - (62, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_error_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (63, 5) - (65, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_error_tests.spy"
                Xunit.Assert.Throws<global::Sharpy.Sqlite3OperationalError>((global::System.Action)(() =>
                {
#line (64, 9) - (64, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_error_tests.spy"
                    conn.Execute("NOT VALID SQL");
                }));
#line (65, 5) - (65, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_error_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestIntegrityErrorIsExactlyIntegrityViaAssertRaises()
            {
#line (70, 5) - (70, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_error_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (71, 5) - (71, 60) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_error_tests.spy"
                conn.Execute("CREATE TABLE t (id INTEGER PRIMARY KEY)");
#line (72, 5) - (72, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_error_tests.spy"
                conn.Execute("INSERT INTO t VALUES (1)");
#line (73, 5) - (75, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_error_tests.spy"
                Xunit.Assert.Throws<global::Sharpy.Sqlite3IntegrityError>((global::System.Action)(() =>
                {
#line (74, 9) - (74, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_error_tests.spy"
                    conn.Execute("INSERT INTO t VALUES (1)");
                }));
#line (75, 5) - (75, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_error_tests.spy"
                conn.Close();
            }
        }
    }
}
