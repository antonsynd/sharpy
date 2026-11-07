// Generated from src/Sharpy.Stdlib.Tests/Spy — do not edit directly.
// To regenerate: bash build_tools/regenerate_spy_tests.sh
#nullable enable

#pragma warning disable xUnit2009, xUnit2017

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Sharpy.Stdlib.Tests.Spy;
using requests = global::Sharpy.Requests;
using Xunit;

namespace Sharpy.Stdlib.Tests.Spy.Requests.RequestsSessionTests
{
    [global::Sharpy.SharpyModule("requests.requests_session_tests")]
    public static partial class RequestsSessionTestsModule
    {
    }

    public partial class RequestsSessionTestsModuleTests
    {
        [Xunit.FactAttribute]
        public void TestSessionHeadersDefaultsToEmptyDict()
        {
#line (27, 5) - (27, 46) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            global::Sharpy.Session s = new global::Sharpy.Session();
#line (28, 5) - (28, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            Xunit.Assert.NotNull(s.Headers);
#line (29, 5) - (29, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(s.Headers));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSessionHeadersCanBeSetAndPersist()
        {
#line (33, 5) - (33, 46) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            global::Sharpy.Session s = new global::Sharpy.Session();
#line (34, 5) - (34, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            Sharpy.Dict<string, string> headers = new Sharpy.Dict<string, string>()
#line hidden
            {
            };
#line (35, 5) - (35, 41) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            headers["User-Agent"] = "sharpy/1.0";
#line (36, 5) - (36, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            headers["X-Custom"] = "value";
#line (37, 5) - (37, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            s.Headers = headers;
#line (38, 5) - (38, 52) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            Xunit.Assert.Equal("sharpy/1.0", s.Headers["User-Agent"]);
#line (39, 5) - (39, 45) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            Xunit.Assert.Equal("value", s.Headers["X-Custom"]);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSessionHeadersDirectMutationViaIndexerPersists()
        {
#line (43, 5) - (43, 46) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            global::Sharpy.Session s = new global::Sharpy.Session();
#line (44, 5) - (44, 45) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            s.Headers["Accept"] = "application/json";
#line (45, 5) - (45, 54) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            Xunit.Assert.Equal("application/json", s.Headers["Accept"]);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSessionCookiesDefaultsToEmptyDict()
        {
#line (51, 5) - (51, 46) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            global::Sharpy.Session s = new global::Sharpy.Session();
#line (52, 5) - (52, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            Xunit.Assert.NotNull(s.Cookies);
#line (53, 5) - (53, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(s.Cookies));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSessionCookiesCanBeSetAndPersist()
        {
#line (57, 5) - (57, 46) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            global::Sharpy.Session s = new global::Sharpy.Session();
#line (58, 5) - (58, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            Sharpy.Dict<string, string> cookies = new Sharpy.Dict<string, string>()
#line hidden
            {
            };
#line (59, 5) - (59, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            cookies["session_id"] = "abc123";
#line (60, 5) - (60, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            cookies["theme"] = "dark";
#line (61, 5) - (61, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            s.Cookies = cookies;
#line (62, 5) - (62, 48) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            Xunit.Assert.Equal("abc123", s.Cookies["session_id"]);
#line (63, 5) - (63, 41) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            Xunit.Assert.Equal("dark", s.Cookies["theme"]);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSessionAuthDefaultsToNull()
        {
#line (69, 5) - (69, 46) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            global::Sharpy.Session s = new global::Sharpy.Session();
#line (70, 5) - (70, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            Xunit.Assert.Null(s.Auth);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSessionAuthCanBeSetAndPersist()
        {
#line (74, 5) - (74, 46) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            global::Sharpy.Session s = new global::Sharpy.Session();
#line (75, 5) - (75, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            s.Auth = ("alice", "secret");
#line (76, 5) - (76, 31) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            Xunit.Assert.NotNull(s.Auth);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSessionAuthCanBeClearedToNull()
        {
#line (80, 5) - (80, 46) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            global::Sharpy.Session s = new global::Sharpy.Session();
#line (81, 5) - (81, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            s.Auth = ("alice", "secret");
#line (82, 5) - (82, 18) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            s.Auth = null;
#line (83, 5) - (83, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            Xunit.Assert.Null(s.Auth);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSessionAllowRedirectsDefaultsToTrue()
        {
#line (89, 5) - (89, 46) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            global::Sharpy.Session s = new global::Sharpy.Session();
#line (90, 5) - (90, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            Xunit.Assert.True(s.AllowRedirects);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSessionAllowRedirectsCanBeSetToFalse()
        {
#line (94, 5) - (94, 46) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            global::Sharpy.Session s = new global::Sharpy.Session();
#line (95, 5) - (95, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            s.AllowRedirects = false;
#line (96, 5) - (96, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            Xunit.Assert.False(s.AllowRedirects);
#line (97, 5) - (97, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            s.AllowRedirects = true;
#line (98, 5) - (98, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            Xunit.Assert.True(s.AllowRedirects);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSessionMaxRedirectsDefaultsTo30()
        {
#line (104, 5) - (104, 46) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            global::Sharpy.Session s = new global::Sharpy.Session();
#line (105, 5) - (105, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            Xunit.Assert.Equal(30, s.MaxRedirects);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSessionMaxRedirectsCanBeChanged()
        {
#line (109, 5) - (109, 46) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            global::Sharpy.Session s = new global::Sharpy.Session();
#line (110, 5) - (110, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            s.MaxRedirects = 10;
#line (111, 5) - (111, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            Xunit.Assert.Equal(10, s.MaxRedirects);
#line (112, 5) - (112, 23) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            s.MaxRedirects = 1;
#line (113, 5) - (113, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            Xunit.Assert.Equal(1, s.MaxRedirects);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSessionMaxRedirectsRejectsZeroAndNegative()
        {
#line (118, 5) - (118, 46) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            global::Sharpy.Session s = new global::Sharpy.Session();
#line (119, 5) - (120, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            bool __raised_0 = false;
#line hidden
            try
            {
#line (120, 9) - (120, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                s.MaxRedirects = 0;
#line hidden
            }
            catch (ValueError)
            {
                __raised_0 = true;
            }

            if (!__raised_0)
                throw new global::Sharpy.AssertionError("Expected ValueError to be raised, but no exception was raised");
#line (121, 5) - (122, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            bool __raised_1 = false;
#line hidden
            try
            {
#line (122, 9) - (122, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                s.MaxRedirects = -1;
#line hidden
            }
            catch (ValueError)
            {
                __raised_1 = true;
            }

            if (!__raised_1)
                throw new global::Sharpy.AssertionError("Expected ValueError to be raised, but no exception was raised");
#line (123, 5) - (123, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            Xunit.Assert.Equal(30, s.MaxRedirects);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSessionMaxRedirectsCanBeOverridden()
        {
#line (127, 5) - (127, 46) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            global::Sharpy.Session s = new global::Sharpy.Session();
#line (128, 5) - (128, 23) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            s.MaxRedirects = 7;
#line (129, 5) - (129, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            Xunit.Assert.Equal(7, s.MaxRedirects);
#line (130, 5) - (130, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            s.MaxRedirects = 100;
#line (131, 5) - (131, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            Xunit.Assert.Equal(100, s.MaxRedirects);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSessionVerifyDefaultsToTrue()
        {
#line (137, 5) - (137, 46) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            global::Sharpy.Session s = new global::Sharpy.Session();
#line (138, 5) - (138, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            Xunit.Assert.True(s.Verify);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSessionVerifyCanBeSetToFalse()
        {
#line (142, 5) - (142, 46) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            global::Sharpy.Session s = new global::Sharpy.Session();
#line (143, 5) - (143, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            s.Verify = false;
#line (144, 5) - (144, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            Xunit.Assert.False(s.Verify);
#line (145, 5) - (145, 20) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            s.Verify = true;
#line (146, 5) - (146, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            Xunit.Assert.True(s.Verify);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSessionVerifyRoundTrips()
        {
#line (150, 5) - (150, 46) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            global::Sharpy.Session s = new global::Sharpy.Session();
#line (151, 5) - (151, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            s.Verify = false;
#line (152, 5) - (152, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            Xunit.Assert.False(s.Verify);
#line (153, 5) - (153, 20) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            s.Verify = true;
#line (154, 5) - (154, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            Xunit.Assert.True(s.Verify);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSessionProxiesDefaultIsNull()
        {
#line (160, 5) - (160, 46) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            global::Sharpy.Session s = new global::Sharpy.Session();
#line (161, 5) - (161, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            Xunit.Assert.Null(s.Proxies);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSessionProxiesCanBeSetToDict()
        {
#line (165, 5) - (165, 46) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            global::Sharpy.Session s = new global::Sharpy.Session();
#line (166, 5) - (166, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            Sharpy.Dict<string, string> proxies = new Sharpy.Dict<string, string>()
#line hidden
            {
            };
#line (167, 5) - (167, 54) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            proxies["http"] = "http://proxy.example.com:8080";
#line (168, 5) - (168, 62) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            proxies["https"] = "http://secure-proxy.example.com:8443";
#line (169, 5) - (169, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            s.Proxies = proxies;
#line (170, 5) - (170, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            Xunit.Assert.NotNull(s.Proxies);
#line (171, 5) - (171, 65) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            Xunit.Assert.Equal("http://proxy.example.com:8080", s.Proxies!["http"]);
#line (172, 5) - (172, 73) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            Xunit.Assert.Equal("http://secure-proxy.example.com:8443", s.Proxies!["https"]);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSessionProxiesCanBeClearedBackToNull()
        {
#line (176, 5) - (176, 46) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            global::Sharpy.Session s = new global::Sharpy.Session();
#line (177, 5) - (177, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            Sharpy.Dict<string, string> proxies = new Sharpy.Dict<string, string>()
#line hidden
            {
            };
#line (178, 5) - (178, 54) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            proxies["http"] = "http://proxy.example.com:8080";
#line (179, 5) - (179, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            s.Proxies = proxies;
#line (180, 5) - (180, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            Xunit.Assert.NotNull(s.Proxies);
#line (181, 5) - (181, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            s.Proxies = null;
#line (182, 5) - (182, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            Xunit.Assert.Null(s.Proxies);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSessionProxiesEmptyDictIsAllowed()
        {
#line (186, 5) - (186, 46) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            global::Sharpy.Session s = new global::Sharpy.Session();
#line (187, 5) - (187, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            Sharpy.Dict<string, string> empty = new Sharpy.Dict<string, string>()
#line hidden
            {
            };
#line (188, 5) - (188, 22) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            s.Proxies = empty;
#line (189, 5) - (189, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            Xunit.Assert.NotNull(s.Proxies);
#line (190, 5) - (190, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
            Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(s.Proxies!));
#line hidden
        }
    }
}
#line default
