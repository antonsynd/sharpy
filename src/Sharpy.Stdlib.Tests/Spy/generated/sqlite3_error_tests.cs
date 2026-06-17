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
#line (37, 5) - (37, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_error_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (38, 5) - (38, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_error_tests.spy"
                bool caught = false;
#line (39, 5) - (43, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_error_tests.spy"
                try
                {
#line (40, 9) - (40, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_error_tests.spy"
                    conn.Execute("NOT VALID SQL");
                }
                catch (global::Sharpy.Sqlite3Error)
                {
#line (42, 9) - (42, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_error_tests.spy"
                    caught = true;
                }

#line (43, 5) - (43, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_error_tests.spy"
                Xunit.Assert.True(caught);
#line (44, 5) - (44, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_error_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestIntegrityErrorCaughtBySqlite3DatabaseError()
            {
#line (49, 5) - (49, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_error_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (50, 5) - (50, 60) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_error_tests.spy"
                conn.Execute("CREATE TABLE t (id INTEGER PRIMARY KEY)");
#line (51, 5) - (51, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_error_tests.spy"
                conn.Execute("INSERT INTO t VALUES (1)");
#line (52, 5) - (52, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_error_tests.spy"
                bool caught = false;
#line (53, 5) - (57, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_error_tests.spy"
                try
                {
#line (54, 9) - (54, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_error_tests.spy"
                    conn.Execute("INSERT INTO t VALUES (1)");
                }
                catch (global::Sharpy.Sqlite3DatabaseError)
                {
#line (56, 9) - (56, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_error_tests.spy"
                    caught = true;
                }

#line (57, 5) - (57, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_error_tests.spy"
                Xunit.Assert.True(caught);
#line (58, 5) - (58, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_error_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestOperationalErrorIsExactlyOperationalViaAssertRaises()
            {
#line (63, 5) - (63, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_error_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (64, 5) - (66, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_error_tests.spy"
                Xunit.Assert.Throws<Sqlite3OperationalError>((global::System.Action)(() =>
                {
#line (65, 9) - (65, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_error_tests.spy"
                    conn.Execute("NOT VALID SQL");
                }));
#line (66, 5) - (66, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_error_tests.spy"
                conn.Close();
            }

            [Xunit.FactAttribute]
            public void TestIntegrityErrorIsExactlyIntegrityViaAssertRaises()
            {
#line (71, 5) - (71, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_error_tests.spy"
                var conn = sqlite3.Connect(":memory:");
#line (72, 5) - (72, 60) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_error_tests.spy"
                conn.Execute("CREATE TABLE t (id INTEGER PRIMARY KEY)");
#line (73, 5) - (73, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_error_tests.spy"
                conn.Execute("INSERT INTO t VALUES (1)");
#line (74, 5) - (76, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_error_tests.spy"
                Xunit.Assert.Throws<Sqlite3IntegrityError>((global::System.Action)(() =>
                {
#line (75, 9) - (75, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_error_tests.spy"
                    conn.Execute("INSERT INTO t VALUES (1)");
                }));
#line (76, 5) - (76, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/sqlite3/sqlite3_error_tests.spy"
                conn.Close();
            }
        }
    }
}
