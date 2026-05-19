using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Sharpy.Tests
{
    public class RequestsSessionTests
    {
        private class MockHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _sendFunc;

            public MockHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendFunc)
            {
                _sendFunc = sendFunc;
            }

            public MockHandler(Func<HttpRequestMessage, HttpResponseMessage> sendFunc)
                : this((req, ct) => Task.FromResult(sendFunc(req)))
            {
            }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                return _sendFunc(request, cancellationToken);
            }
        }

        private static HttpResponseMessage OkText(string body = "ok")
        {
            var msg = new HttpResponseMessage(HttpStatusCode.OK);
            msg.Content = new StringContent(body, Encoding.UTF8, "text/plain");
            return msg;
        }

        private static HttpResponseMessage MakeResponse(HttpStatusCode statusCode, string? requestUrl = null)
        {
            var msg = new HttpResponseMessage(statusCode);
            msg.Content = new ByteArrayContent(Array.Empty<byte>());
            if (requestUrl != null)
            {
                msg.RequestMessage = new HttpRequestMessage(HttpMethod.Get, requestUrl);
            }
            return msg;
        }

        #region Session.Headers

        [Fact]
        public void Session_Headers_DefaultsToEmptyDict()
        {
            using var session = new Session();
            Assert.NotNull(session.Headers);
            Assert.Empty((System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<string, string>>)session.Headers);
        }

        [Fact]
        public void Session_Headers_CanBeSetAndPersist()
        {
            using var session = new Session();
            var headers = new Dict<string, string>();
            headers["User-Agent"] = "sharpy/1.0";
            headers["X-Custom"] = "value";
            session.Headers = headers;

            Assert.Equal("sharpy/1.0", session.Headers["User-Agent"]);
            Assert.Equal("value", session.Headers["X-Custom"]);
        }

        [Fact]
        public void Session_Headers_SettingToNullCreatesEmptyDict()
        {
            using var session = new Session();
            session.Headers = null!;
            Assert.NotNull(session.Headers);
            Assert.Empty((System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<string, string>>)session.Headers);
        }

        [Fact]
        public void Session_Headers_DirectMutationViaIndexerPersists()
        {
            using var session = new Session();
            session.Headers["Accept"] = "application/json";
            Assert.Equal("application/json", session.Headers["Accept"]);
        }

        #endregion

        #region Session.Cookies

        [Fact]
        public void Session_Cookies_DefaultsToEmptyDict()
        {
            using var session = new Session();
            Assert.NotNull(session.Cookies);
            Assert.Empty((System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<string, string>>)session.Cookies);
        }

        [Fact]
        public void Session_Cookies_CanBeSetAndPersist()
        {
            using var session = new Session();
            var cookies = new Dict<string, string>();
            cookies["session_id"] = "abc123";
            cookies["theme"] = "dark";
            session.Cookies = cookies;

            Assert.Equal("abc123", session.Cookies["session_id"]);
            Assert.Equal("dark", session.Cookies["theme"]);
        }

        [Fact]
        public void Session_Cookies_SettingToNullCreatesEmptyDict()
        {
            using var session = new Session();
            session.Cookies = null!;
            Assert.NotNull(session.Cookies);
            Assert.Empty((System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<string, string>>)session.Cookies);
        }

        #endregion

        #region Session.Auth

        [Fact]
        public void Session_Auth_DefaultsToNull()
        {
            using var session = new Session();
            Assert.Null(session.Auth);
        }

        [Fact]
        public void Session_Auth_CanBeSetAndPersist()
        {
            using var session = new Session();
            session.Auth = ("alice", "secret");
            Assert.NotNull(session.Auth);
            Assert.Equal("alice", session.Auth!.Value.Item1);
            Assert.Equal("secret", session.Auth!.Value.Item2);
        }

        [Fact]
        public void Session_Auth_CanBeClearedToNull()
        {
            using var session = new Session();
            session.Auth = ("alice", "secret");
            session.Auth = null;
            Assert.Null(session.Auth);
        }

        #endregion

        #region Session.Dispose

        [Fact]
        public void Session_Dispose_DoesNotThrow()
        {
            var session = new Session();
            session.Dispose();
        }

        [Fact]
        public void Session_DoubleDispose_DoesNotThrow()
        {
            var session = new Session();
            session.Dispose();
            session.Dispose(); // Second dispose should be a no-op.
        }

        [Fact]
        public void Session_RequestAfterDispose_ReturnsErr()
        {
            var session = new Session();
            session.Dispose();
            var result = session.Get("https://example.com/");
            Assert.True(result.IsErr);
            Assert.IsType<RequestException>(result.UnwrapErr());
            Assert.Contains("disposed", result.UnwrapErr().Message, StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region Session.AllowRedirects

        [Fact]
        public void Session_AllowRedirects_DefaultsToTrue()
        {
            using var session = new Session();
            Assert.True(session.AllowRedirects);
        }

        [Fact]
        public void Session_AllowRedirects_CanBeSetToFalse()
        {
            using var session = new Session();
            session.AllowRedirects = false;
            Assert.False(session.AllowRedirects);
            session.AllowRedirects = true;
            Assert.True(session.AllowRedirects);
        }

        #endregion

        #region Session.MaxRedirects

        [Fact]
        public void Session_MaxRedirects_DefaultsTo30()
        {
            using var session = new Session();
            Assert.Equal(30, session.MaxRedirects);
        }

        [Fact]
        public void Session_MaxRedirects_CanBeChanged()
        {
            using var session = new Session();
            session.MaxRedirects = 10;
            Assert.Equal(10, session.MaxRedirects);
            session.MaxRedirects = 1;
            Assert.Equal(1, session.MaxRedirects);
        }

        [Fact]
        public void Session_MaxRedirects_RejectsZeroAndNegative()
        {
            using var session = new Session();
            Assert.Throws<ArgumentOutOfRangeException>(() => session.MaxRedirects = 0);
            Assert.Throws<ArgumentOutOfRangeException>(() => session.MaxRedirects = -5);
        }

        #endregion

        #region Session.Verify

        [Fact]
        public void Session_Verify_DefaultsToTrue()
        {
            using var session = new Session();
            Assert.True(session.Verify);
        }

        [Fact]
        public void Session_Verify_CanBeSetToFalse()
        {
            using var session = new Session();
            session.Verify = false;
            Assert.False(session.Verify);
            session.Verify = true;
            Assert.True(session.Verify);
        }

        #endregion

        #region RaiseForStatus

        [Theory]
        [InlineData(HttpStatusCode.OK)]
        [InlineData(HttpStatusCode.Created)]
        [InlineData(HttpStatusCode.NoContent)]
        public void RaiseForStatus_2xx_ReturnsOk(HttpStatusCode code)
        {
            using var httpMsg = MakeResponse(code, "https://example.com/");
            using var response = new Response(httpMsg);
            var result = response.RaiseForStatus();
            Assert.True(result.IsOk);
            Assert.Same(response, result.Unwrap());
        }

        [Theory]
        [InlineData(HttpStatusCode.BadRequest, 400, "Client Error")]
        [InlineData(HttpStatusCode.NotFound, 404, "Client Error")]
        [InlineData(HttpStatusCode.Unauthorized, 401, "Client Error")]
        public void RaiseForStatus_4xx_ReturnsErrWithClientError(
            HttpStatusCode code, int expectedStatus, string expectedKind)
        {
            using var httpMsg = MakeResponse(code, "https://example.com/missing");
            using var response = new Response(httpMsg);
            var result = response.RaiseForStatus();
            Assert.True(result.IsErr);
            var err = result.UnwrapErr();
            Assert.IsType<HTTPError>(err);
            Assert.Contains(expectedStatus.ToString(), err.Message);
            Assert.Contains(expectedKind, err.Message);
        }

        [Theory]
        [InlineData(HttpStatusCode.InternalServerError, 500, "Server Error")]
        [InlineData(HttpStatusCode.ServiceUnavailable, 503, "Server Error")]
        [InlineData(HttpStatusCode.BadGateway, 502, "Server Error")]
        public void RaiseForStatus_5xx_ReturnsErrWithServerError(
            HttpStatusCode code, int expectedStatus, string expectedKind)
        {
            using var httpMsg = MakeResponse(code, "https://example.com/down");
            using var response = new Response(httpMsg);
            var result = response.RaiseForStatus();
            Assert.True(result.IsErr);
            var err = result.UnwrapErr();
            Assert.IsType<HTTPError>(err);
            Assert.Contains(expectedStatus.ToString(), err.Message);
            Assert.Contains(expectedKind, err.Message);
        }

        [Fact]
        public void RaiseForStatus_ErrorMessageIncludesUrl()
        {
            using var httpMsg = MakeResponse(HttpStatusCode.NotFound, "https://example.com/missing");
            using var response = new Response(httpMsg);
            var result = response.RaiseForStatus();
            Assert.True(result.IsErr);
            Assert.Contains("https://example.com/missing", result.UnwrapErr().Message);
        }

        [Fact]
        public void RaiseForStatus_HTTPError_HasResponseReference()
        {
            using var httpMsg = MakeResponse(HttpStatusCode.NotFound, "https://example.com/");
            using var response = new Response(httpMsg);
            var result = response.RaiseForStatus();
            Assert.True(result.IsErr);
            var err = (HTTPError)result.UnwrapErr();
            Assert.Same(response, err.Response);
        }

        #endregion

        #region Module-level auth

        [Fact]
        public void Send_WithAuth_AddsBasicAuthorizationHeader()
        {
            HttpRequestMessage? captured = null;
            var handler = new MockHandler(req => { captured = req; return OkText(); });
            using var client = new HttpClient(handler);

            var result = Requests.Send(
                HttpMethod.Get,
                "https://example.com/",
                null, null, null, null, null,
                client,
                auth: ("alice", "secret"));

            Assert.True(result.IsOk);
            Assert.NotNull(captured);
            Assert.NotNull(captured!.Headers.Authorization);
            Assert.Equal("Basic", captured.Headers.Authorization!.Scheme);
            // base64("alice:secret") = "YWxpY2U6c2VjcmV0"
            var expected = Convert.ToBase64String(Encoding.UTF8.GetBytes("alice:secret"));
            Assert.Equal(expected, captured.Headers.Authorization.Parameter);
            Assert.Equal("YWxpY2U6c2VjcmV0", captured.Headers.Authorization.Parameter);
        }

        [Fact]
        public void Send_WithAuth_Base64EncodingMatchesPython()
        {
            // Verified: python3 -c "import base64; print(base64.b64encode(b'user:pass').decode())"
            // => "dXNlcjpwYXNz"
            HttpRequestMessage? captured = null;
            var handler = new MockHandler(req => { captured = req; return OkText(); });
            using var client = new HttpClient(handler);

            var result = Requests.Send(
                HttpMethod.Get,
                "https://example.com/",
                null, null, null, null, null,
                client,
                auth: ("user", "pass"));

            Assert.True(result.IsOk);
            Assert.Equal("dXNlcjpwYXNz", captured!.Headers.Authorization!.Parameter);
        }

        [Fact]
        public void Send_WithAuth_EmptyPassword_StillEncodes()
        {
            HttpRequestMessage? captured = null;
            var handler = new MockHandler(req => { captured = req; return OkText(); });
            using var client = new HttpClient(handler);

            var result = Requests.Send(
                HttpMethod.Get,
                "https://example.com/",
                null, null, null, null, null,
                client,
                auth: ("token", ""));

            Assert.True(result.IsOk);
            // base64("token:") = "dG9rZW46"
            Assert.Equal("dG9rZW46", captured!.Headers.Authorization!.Parameter);
        }

        [Fact]
        public void Send_WithAuth_OverridesAuthorizationHeader()
        {
            HttpRequestMessage? captured = null;
            var handler = new MockHandler(req => { captured = req; return OkText(); });
            using var client = new HttpClient(handler);

            var headers = new Dict<string, string>();
            headers["Authorization"] = "Bearer ignored";

            var result = Requests.Send(
                HttpMethod.Get,
                "https://example.com/",
                headers, null, null, null, null,
                client,
                auth: ("alice", "secret"));

            Assert.True(result.IsOk);
            Assert.Equal("Basic", captured!.Headers.Authorization!.Scheme);
            Assert.Equal(
                Convert.ToBase64String(Encoding.UTF8.GetBytes("alice:secret")),
                captured.Headers.Authorization.Parameter);
        }

        [Fact]
        public void Send_WithoutAuth_NoAuthorizationHeaderAdded()
        {
            HttpRequestMessage? captured = null;
            var handler = new MockHandler(req => { captured = req; return OkText(); });
            using var client = new HttpClient(handler);

            var result = Requests.Send(
                HttpMethod.Get,
                "https://example.com/",
                null, null, null, null, null,
                client);

            Assert.True(result.IsOk);
            Assert.Null(captured!.Headers.Authorization);
        }

        [Fact]
        public void Send_WithAuth_HandlesUnicodeCredentials()
        {
            HttpRequestMessage? captured = null;
            var handler = new MockHandler(req => { captured = req; return OkText(); });
            using var client = new HttpClient(handler);

            var result = Requests.Send(
                HttpMethod.Get,
                "https://example.com/",
                null, null, null, null, null,
                client,
                auth: ("café", "naïve"));

            Assert.True(result.IsOk);
            // UTF-8 encoded "café:naïve"
            var expected = Convert.ToBase64String(Encoding.UTF8.GetBytes("café:naïve"));
            Assert.Equal(expected, captured!.Headers.Authorization!.Parameter);
        }

        #endregion
    }
}
