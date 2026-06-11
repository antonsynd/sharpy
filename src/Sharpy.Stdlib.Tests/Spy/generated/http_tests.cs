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
using http = global::Sharpy.HttpModule;
using Xunit;
using static Sharpy.Stdlib.Tests.Spy.HTTP.HttpTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class HTTP
    {
        [global::Sharpy.SharpyModule("http.http_tests")]
        public static partial class HttpTests
        {
        }
    }

    public static partial class HTTP
    {
        public partial class HttpTestsTests
        {
            [Xunit.FactAttribute]
            public void TestOkHasCorrectValue()
            {
#line (9, 5) - (9, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/http/http_tests.spy"
                Xunit.Assert.Equal(200, global::Sharpy.HTTPStatus.OK.Value);
            }

            [Xunit.FactAttribute]
            public void TestOkHasCorrectPhrase()
            {
#line (13, 5) - (13, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/http/http_tests.spy"
                Xunit.Assert.Equal("OK", global::Sharpy.HTTPStatus.OK.Phrase);
            }

            [Xunit.FactAttribute]
            public void TestNotFoundHasCorrectValue()
            {
#line (17, 5) - (17, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/http/http_tests.spy"
                Xunit.Assert.Equal(404, global::Sharpy.HTTPStatus.NOT_FOUND.Value);
            }

            [Xunit.FactAttribute]
            public void TestNotFoundHasCorrectPhrase()
            {
#line (21, 5) - (21, 60) 1 "src/Sharpy.Stdlib.Tests/Spy/http/http_tests.spy"
                Xunit.Assert.Equal("Not Found", global::Sharpy.HTTPStatus.NOT_FOUND.Phrase);
            }

            [Xunit.FactAttribute]
            public void TestFromValueReturnsCorrectInstance()
            {
#line (25, 5) - (25, 62) 1 "src/Sharpy.Stdlib.Tests/Spy/http/http_tests.spy"
                global::Sharpy.HTTPStatus status = global::Sharpy.HTTPStatus.FromValue(200);
#line (26, 5) - (26, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/http/http_tests.spy"
                Xunit.Assert.Equal(global::Sharpy.HTTPStatus.OK, status);
            }

            [Xunit.FactAttribute]
            public void TestFromValueUnknownCodeThrowsValueError()
            {
#line (30, 5) - (33, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/http/http_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
                {
#line (31, 9) - (31, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/http/http_tests.spy"
                    global::Sharpy.HTTPStatus.FromValue(999);
                }));
            }

            [Xunit.FactAttribute]
            public void TestValuePropertyReturnsInt()
            {
#line (35, 5) - (35, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/http/http_tests.spy"
                int code = global::Sharpy.HTTPStatus.OK.Value;
#line (36, 5) - (36, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/http/http_tests.spy"
                Xunit.Assert.Equal(200, code);
            }

            [Xunit.FactAttribute]
            public void TestToStringReturnsNumericString()
            {
#line (40, 5) - (40, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/http/http_tests.spy"
                Xunit.Assert.Equal("200", global::Sharpy.Builtins.Str(global::Sharpy.HTTPStatus.OK));
            }

            [Xunit.FactAttribute]
            public void TestEqualsSameValueReturnsTrue()
            {
#line (44, 5) - (44, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/http/http_tests.spy"
                global::Sharpy.HTTPStatus a = global::Sharpy.HTTPStatus.OK;
#line (45, 5) - (45, 57) 1 "src/Sharpy.Stdlib.Tests/Spy/http/http_tests.spy"
                global::Sharpy.HTTPStatus b = global::Sharpy.HTTPStatus.FromValue(200);
#line (46, 5) - (46, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/http/http_tests.spy"
                Xunit.Assert.Equal(b, a);
            }

            [Xunit.FactAttribute]
            public void TestGetHashCodeSameValueMatches()
            {
#line (50, 5) - (50, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/http/http_tests.spy"
                global::Sharpy.HTTPStatus a = global::Sharpy.HTTPStatus.OK;
#line (51, 5) - (51, 57) 1 "src/Sharpy.Stdlib.Tests/Spy/http/http_tests.spy"
                global::Sharpy.HTTPStatus b = global::Sharpy.HTTPStatus.FromValue(200);
#line (52, 5) - (52, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/http/http_tests.spy"
                Xunit.Assert.Equal(global::Sharpy.Builtins.Hash(b), global::Sharpy.Builtins.Hash(a));
            }

            [Xunit.TheoryAttribute]
            [Xunit.InlineDataAttribute(100, "Continue")]
            [Xunit.InlineDataAttribute(201, "Created")]
            [Xunit.InlineDataAttribute(301, "Moved Permanently")]
            [Xunit.InlineDataAttribute(403, "Forbidden")]
            [Xunit.InlineDataAttribute(500, "Internal Server Error")]
            [Xunit.InlineDataAttribute(503, "Service Unavailable")]
            public void TestFromValueKnownCodesHaveCorrectPhrase(int code, string expectedPhrase)
            {
#line (64, 5) - (64, 70) 1 "src/Sharpy.Stdlib.Tests/Spy/http/http_tests.spy"
                Xunit.Assert.Equal(expectedPhrase, global::Sharpy.HTTPStatus.FromValue(code).Phrase);
            }

            [Xunit.FactAttribute]
            public void TestHttpConnectionDefaultPortIs80()
            {
#line (70, 5) - (70, 68) 1 "src/Sharpy.Stdlib.Tests/Spy/http/http_tests.spy"
                global::Sharpy.HTTPConnection conn = new global::Sharpy.HTTPConnection("example.com");
#line (71, 5) - (71, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/http/http_tests.spy"
                Xunit.Assert.Equal(80, conn.Port);
#line (72, 5) - (72, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/http/http_tests.spy"
                Xunit.Assert.Equal("example.com", conn.Host);
            }

            [Xunit.FactAttribute]
            public void TestHttpConnectionCustomPort()
            {
#line (76, 5) - (76, 79) 1 "src/Sharpy.Stdlib.Tests/Spy/http/http_tests.spy"
                global::Sharpy.HTTPConnection conn = new global::Sharpy.HTTPConnection("example.com", port: 8080);
#line (77, 5) - (77, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/http/http_tests.spy"
                Xunit.Assert.Equal(8080, conn.Port);
            }

            [Xunit.FactAttribute]
            public void TestHttpsConnectionDefaultPortIs443()
            {
#line (81, 5) - (81, 70) 1 "src/Sharpy.Stdlib.Tests/Spy/http/http_tests.spy"
                global::Sharpy.HTTPSConnection conn = new global::Sharpy.HTTPSConnection("example.com");
#line (82, 5) - (82, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/http/http_tests.spy"
                Xunit.Assert.Equal(443, conn.Port);
            }

            [Xunit.FactAttribute]
            public void TestHttpConnectionEmptyHostThrowsInvalidUrl()
            {
#line (86, 5) - (89, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/http/http_tests.spy"
                Xunit.Assert.Throws<global::Sharpy.InvalidURL>((global::System.Action)(() =>
                {
#line (87, 9) - (87, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/http/http_tests.spy"
                    new global::Sharpy.HTTPConnection("");
                }));
            }

            [Xunit.FactAttribute]
            public void TestGetresponseBeforeRequestThrowsNotConnected()
            {
#line (91, 5) - (91, 68) 1 "src/Sharpy.Stdlib.Tests/Spy/http/http_tests.spy"
                global::Sharpy.HTTPConnection conn = new global::Sharpy.HTTPConnection("example.com");
#line (92, 5) - (100, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/http/http_tests.spy"
                Xunit.Assert.Throws<global::Sharpy.NotConnected>((global::System.Action)(() =>
                {
#line (93, 9) - (93, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/http/http_tests.spy"
                    conn.Getresponse();
                }));
            }

            [Xunit.FactAttribute]
            public void TestHttpExceptionHasMessage()
            {
#line (102, 5) - (102, 57) 1 "src/Sharpy.Stdlib.Tests/Spy/http/http_tests.spy"
                global::Sharpy.HTTPException ex = new global::Sharpy.HTTPException("test");
#line (103, 5) - (103, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/http/http_tests.spy"
                Xunit.Assert.Contains("test", global::Sharpy.Builtins.Str(ex));
            }

            [Xunit.FactAttribute]
            public void TestInvalidUrlHasMessage()
            {
#line (107, 5) - (107, 54) 1 "src/Sharpy.Stdlib.Tests/Spy/http/http_tests.spy"
                global::Sharpy.InvalidURL ex = new global::Sharpy.InvalidURL("bad url");
#line (108, 5) - (108, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/http/http_tests.spy"
                Xunit.Assert.Contains("bad url", global::Sharpy.Builtins.Str(ex));
            }

            [Xunit.FactAttribute]
            public void TestNotConnectedHasMessage()
            {
#line (112, 5) - (112, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/http/http_tests.spy"
                global::Sharpy.NotConnected ex = new global::Sharpy.NotConnected("no conn");
#line (113, 5) - (113, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/http/http_tests.spy"
                Xunit.Assert.Contains("no conn", global::Sharpy.Builtins.Str(ex));
            }

            [Xunit.FactAttribute]
            public void TestHttpPortIs80()
            {
#line (119, 5) - (119, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/http/http_tests.spy"
                Xunit.Assert.Equal(80, http.HTTP_PORT);
            }

            [Xunit.FactAttribute]
            public void TestHttpsPortIs443()
            {
#line (123, 5) - (123, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/http/http_tests.spy"
                Xunit.Assert.Equal(443, http.HTTPS_PORT);
            }
        }
    }
}
