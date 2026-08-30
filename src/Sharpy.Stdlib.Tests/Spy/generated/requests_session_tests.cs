// Generated from src/Sharpy.Stdlib.Tests/Spy — do not edit directly.
// To regenerate: bash build_tools/regenerate_spy_tests.sh
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Sharpy.Stdlib.Tests.Spy;
using requests = global::Sharpy.Requests;
using Xunit;
using static Sharpy.Stdlib.Tests.Spy.Requests.RequestsSessionTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Requests
    {
        [global::Sharpy.SharpyModule("requests.requests_session_tests")]
        public static partial class RequestsSessionTests
        {
        }
    }

    public static partial class Requests
    {
        public partial class RequestsSessionTestsTests
        {
            [Xunit.FactAttribute]
            public void TestSessionHeadersDefaultsToEmptyDict()
            {
#line (30, 5) - (30, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                global::Sharpy.Session s = new global::Sharpy.Session();
#line (31, 5) - (31, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                Xunit.Assert.NotNull(s.Headers);
#line (32, 5) - (32, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(s.Headers));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSessionHeadersCanBeSetAndPersist()
            {
#line (36, 5) - (36, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                global::Sharpy.Session s = new global::Sharpy.Session();
#line (37, 5) - (37, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                Sharpy.Dict<string, string> headers = new Sharpy.Dict<string, string>()
#line hidden
                {
                };
#line (38, 5) - (38, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                headers["User-Agent"] = "sharpy/1.0";
#line (39, 5) - (39, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                headers["X-Custom"] = "value";
#line (40, 5) - (40, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                s.Headers = headers;
#line (41, 5) - (41, 52) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                Xunit.Assert.Equal("sharpy/1.0", s.Headers["User-Agent"]);
#line (42, 5) - (42, 45) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                Xunit.Assert.Equal("value", s.Headers["X-Custom"]);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSessionHeadersDirectMutationViaIndexerPersists()
            {
#line (46, 5) - (46, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                global::Sharpy.Session s = new global::Sharpy.Session();
#line (47, 5) - (47, 45) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                s.Headers["Accept"] = "application/json";
#line (48, 5) - (48, 54) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                Xunit.Assert.Equal("application/json", s.Headers["Accept"]);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSessionCookiesDefaultsToEmptyDict()
            {
#line (54, 5) - (54, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                global::Sharpy.Session s = new global::Sharpy.Session();
#line (55, 5) - (55, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                Xunit.Assert.NotNull(s.Cookies);
#line (56, 5) - (56, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(s.Cookies));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSessionCookiesCanBeSetAndPersist()
            {
#line (60, 5) - (60, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                global::Sharpy.Session s = new global::Sharpy.Session();
#line (61, 5) - (61, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                Sharpy.Dict<string, string> cookies = new Sharpy.Dict<string, string>()
#line hidden
                {
                };
#line (62, 5) - (62, 37) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                cookies["session_id"] = "abc123";
#line (63, 5) - (63, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                cookies["theme"] = "dark";
#line (64, 5) - (64, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                s.Cookies = cookies;
#line (65, 5) - (65, 48) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                Xunit.Assert.Equal("abc123", s.Cookies["session_id"]);
#line (66, 5) - (66, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                Xunit.Assert.Equal("dark", s.Cookies["theme"]);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSessionAuthDefaultsToNull()
            {
#line (72, 5) - (72, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                global::Sharpy.Session s = new global::Sharpy.Session();
#line (73, 5) - (73, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                Xunit.Assert.Null(s.Auth);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSessionAuthCanBeSetAndPersist()
            {
#line (77, 5) - (77, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                global::Sharpy.Session s = new global::Sharpy.Session();
#line (78, 5) - (78, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                s.Auth = ("alice", "secret");
#line (79, 5) - (79, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                Xunit.Assert.NotNull(s.Auth);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSessionAuthCanBeClearedToNull()
            {
#line (83, 5) - (83, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                global::Sharpy.Session s = new global::Sharpy.Session();
#line (84, 5) - (84, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                s.Auth = ("alice", "secret");
#line (85, 5) - (85, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                s.Auth = null;
#line (86, 5) - (86, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                Xunit.Assert.Null(s.Auth);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSessionAllowRedirectsDefaultsToTrue()
            {
#line (92, 5) - (92, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                global::Sharpy.Session s = new global::Sharpy.Session();
#line (93, 5) - (93, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                Xunit.Assert.True(s.AllowRedirects);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSessionAllowRedirectsCanBeSetToFalse()
            {
#line (97, 5) - (97, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                global::Sharpy.Session s = new global::Sharpy.Session();
#line (98, 5) - (98, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                s.AllowRedirects = false;
#line (99, 5) - (99, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                Xunit.Assert.False(s.AllowRedirects);
#line (100, 5) - (100, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                s.AllowRedirects = true;
#line (101, 5) - (101, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                Xunit.Assert.True(s.AllowRedirects);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSessionMaxRedirectsDefaultsTo30()
            {
#line (107, 5) - (107, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                global::Sharpy.Session s = new global::Sharpy.Session();
#line (108, 5) - (108, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                Xunit.Assert.Equal(30, s.MaxRedirects);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSessionMaxRedirectsCanBeChanged()
            {
#line (112, 5) - (112, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                global::Sharpy.Session s = new global::Sharpy.Session();
#line (113, 5) - (113, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                s.MaxRedirects = 10;
#line (114, 5) - (114, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                Xunit.Assert.Equal(10, s.MaxRedirects);
#line (115, 5) - (115, 23) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                s.MaxRedirects = 1;
#line (116, 5) - (116, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                Xunit.Assert.Equal(1, s.MaxRedirects);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSessionMaxRedirectsCanBeOverridden()
            {
#line (120, 5) - (120, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                global::Sharpy.Session s = new global::Sharpy.Session();
#line (121, 5) - (121, 23) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                s.MaxRedirects = 7;
#line (122, 5) - (122, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                Xunit.Assert.Equal(7, s.MaxRedirects);
#line (123, 5) - (123, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                s.MaxRedirects = 100;
#line (124, 5) - (124, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                Xunit.Assert.Equal(100, s.MaxRedirects);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSessionVerifyDefaultsToTrue()
            {
#line (130, 5) - (130, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                global::Sharpy.Session s = new global::Sharpy.Session();
#line (131, 5) - (131, 21) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                Xunit.Assert.True(s.Verify);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSessionVerifyCanBeSetToFalse()
            {
#line (135, 5) - (135, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                global::Sharpy.Session s = new global::Sharpy.Session();
#line (136, 5) - (136, 21) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                s.Verify = false;
#line (137, 5) - (137, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                Xunit.Assert.False(s.Verify);
#line (138, 5) - (138, 20) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                s.Verify = true;
#line (139, 5) - (139, 21) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                Xunit.Assert.True(s.Verify);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSessionVerifyRoundTrips()
            {
#line (143, 5) - (143, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                global::Sharpy.Session s = new global::Sharpy.Session();
#line (144, 5) - (144, 21) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                s.Verify = false;
#line (145, 5) - (145, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                Xunit.Assert.False(s.Verify);
#line (146, 5) - (146, 20) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                s.Verify = true;
#line (147, 5) - (147, 21) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                Xunit.Assert.True(s.Verify);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSessionProxiesDefaultIsNull()
            {
#line (153, 5) - (153, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                global::Sharpy.Session s = new global::Sharpy.Session();
#line (154, 5) - (154, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                Xunit.Assert.Null(s.Proxies);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSessionProxiesCanBeSetToDict()
            {
#line (158, 5) - (158, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                global::Sharpy.Session s = new global::Sharpy.Session();
#line (159, 5) - (159, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                Sharpy.Dict<string, string> proxies = new Sharpy.Dict<string, string>()
#line hidden
                {
                };
#line (160, 5) - (160, 54) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                proxies["http"] = "http://proxy.example.com:8080";
#line (161, 5) - (161, 62) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                proxies["https"] = "http://secure-proxy.example.com:8443";
#line (162, 5) - (162, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                s.Proxies = proxies;
#line (163, 5) - (163, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                Xunit.Assert.NotNull(s.Proxies);
#line (164, 5) - (164, 65) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                Xunit.Assert.Equal("http://proxy.example.com:8080", s.Proxies!["http"]);
#line (165, 5) - (165, 73) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                Xunit.Assert.Equal("http://secure-proxy.example.com:8443", s.Proxies!["https"]);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSessionProxiesCanBeClearedBackToNull()
            {
#line (169, 5) - (169, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                global::Sharpy.Session s = new global::Sharpy.Session();
#line (170, 5) - (170, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                Sharpy.Dict<string, string> proxies = new Sharpy.Dict<string, string>()
#line hidden
                {
                };
#line (171, 5) - (171, 54) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                proxies["http"] = "http://proxy.example.com:8080";
#line (172, 5) - (172, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                s.Proxies = proxies;
#line (173, 5) - (173, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                Xunit.Assert.NotNull(s.Proxies);
#line (174, 5) - (174, 21) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                s.Proxies = null;
#line (175, 5) - (175, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                Xunit.Assert.Null(s.Proxies);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSessionProxiesEmptyDictIsAllowed()
            {
#line (179, 5) - (179, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                global::Sharpy.Session s = new global::Sharpy.Session();
#line (180, 5) - (180, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                Sharpy.Dict<string, string> empty = new Sharpy.Dict<string, string>()
#line hidden
                {
                };
#line (181, 5) - (181, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                s.Proxies = empty;
#line (182, 5) - (182, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                Xunit.Assert.NotNull(s.Proxies);
#line (183, 5) - (183, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/requests/requests_session_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(s.Proxies!));
#line hidden
            }
        }
    }
}
#line default
