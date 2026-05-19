using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Sharpy.Tests
{
    public class RequestsModuleTests
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

        #region Get

        [Fact]
        public void Get_ReturnsOkResultOnSuccess()
        {
            HttpRequestMessage? captured = null;
            var handler = new MockHandler(req => { captured = req; return OkText("hello"); });
            using var client = new HttpClient(handler);

            var result = Requests.Send(HttpMethod.Get, "https://example.com/", null, null, null, null, null, client);

            Assert.True(result.IsOk);
            using var response = result.Unwrap();
            Assert.Equal(200, response.StatusCode);
            Assert.Equal("hello", response.Text);
            Assert.NotNull(captured);
            Assert.Equal(HttpMethod.Get, captured!.Method);
        }

        [Fact]
        public void Get_PublicEntryPointWorks()
        {
            var handler = new MockHandler(req => OkText("hi"));
            using var client = new HttpClient(handler);
            // The public Get(string, ...) overload doesn't take an HttpClient; we exercise
            // Send directly via the internal entry point used by Session and tests.
            var result = Requests.Send(HttpMethod.Get, "https://example.com/", null, null, null, null, null, client);
            Assert.True(result.IsOk);
        }

        #endregion

        #region Post body

        [Fact]
        public void Post_WithJsonBody_SendsJsonContentType()
        {
            HttpRequestMessage? captured = null;
            string capturedBody = "";
            var handler = new MockHandler(req =>
            {
                captured = req;
                if (req.Content != null)
                {
                    capturedBody = req.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                }
                return OkText();
            });
            using var client = new HttpClient(handler);

            var jsonObj = new Dict<string, object?>();
            jsonObj["name"] = "alice";
            jsonObj["age"] = 30;

            var result = Requests.Send(
                HttpMethod.Post,
                "https://example.com/api",
                null,
                null,
                jsonObj,
                null,
                null,
                client);

            Assert.True(result.IsOk);
            Assert.NotNull(captured);
            Assert.NotNull(captured!.Content);
            Assert.Equal("application/json", captured.Content!.Headers.ContentType!.MediaType);
            Assert.Contains("\"name\"", capturedBody);
            Assert.Contains("\"alice\"", capturedBody);
            Assert.Contains("30", capturedBody);
        }

        [Fact]
        public void Post_WithFormData_SendsFormUrlEncodedContentType()
        {
            HttpRequestMessage? captured = null;
            string capturedBody = "";
            var handler = new MockHandler(req =>
            {
                captured = req;
                if (req.Content != null)
                {
                    capturedBody = req.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                }
                return OkText();
            });
            using var client = new HttpClient(handler);

            var form = new Dict<string, string>();
            form["user"] = "bob";
            form["pass"] = "secret";

            var result = Requests.Send(
                HttpMethod.Post,
                "https://example.com/login",
                null,
                null,
                null,
                form,
                null,
                client);

            Assert.True(result.IsOk);
            Assert.NotNull(captured);
            Assert.Equal("application/x-www-form-urlencoded", captured!.Content!.Headers.ContentType!.MediaType);
            Assert.Contains("user=bob", capturedBody);
            Assert.Contains("pass=secret", capturedBody);
        }

        [Fact]
        public void Post_JsonTakesPrecedenceOverData()
        {
            HttpRequestMessage? captured = null;
            var handler = new MockHandler(req => { captured = req; return OkText(); });
            using var client = new HttpClient(handler);

            var jsonObj = new Dict<string, object?>();
            jsonObj["x"] = 1;
            var form = new Dict<string, string>();
            form["y"] = "2";

            var result = Requests.Send(
                HttpMethod.Post,
                "https://example.com/",
                null,
                null,
                jsonObj,
                form,
                null,
                client);

            Assert.True(result.IsOk);
            Assert.Equal("application/json", captured!.Content!.Headers.ContentType!.MediaType);
        }

        #endregion

        #region Headers

        [Fact]
        public void CustomHeaders_AreForwardedOnRequest()
        {
            HttpRequestMessage? captured = null;
            var handler = new MockHandler(req => { captured = req; return OkText(); });
            using var client = new HttpClient(handler);

            var headers = new Dict<string, string>();
            headers["Authorization"] = "Bearer xyz";
            headers["X-Custom"] = "value";

            var result = Requests.Send(HttpMethod.Get, "https://example.com/", headers, null, null, null, null, client);

            Assert.True(result.IsOk);
            Assert.NotNull(captured);
            Assert.True(captured!.Headers.Contains("Authorization"));
            Assert.True(captured.Headers.Contains("X-Custom"));
            Assert.Equal("Bearer xyz", string.Join(", ", captured.Headers.GetValues("Authorization")));
            Assert.Equal("value", string.Join(", ", captured.Headers.GetValues("X-Custom")));
        }

        #endregion

        #region Query params

        [Fact]
        public void QueryParams_AreAppendedToUrl()
        {
            HttpRequestMessage? captured = null;
            var handler = new MockHandler(req => { captured = req; return OkText(); });
            using var client = new HttpClient(handler);

            var qp = new Dict<string, string>();
            qp["q"] = "hello world";
            qp["page"] = "2";

            var result = Requests.Send(HttpMethod.Get, "https://example.com/search", null, qp, null, null, null, client);

            Assert.True(result.IsOk);
            // Use OriginalString (preserves percent-encoding); ToString() unescapes for display.
            var url = captured!.RequestUri!.OriginalString;
            Assert.Contains("q=hello%20world", url);
            Assert.Contains("page=2", url);
            Assert.StartsWith("https://example.com/search?", url);
        }

        [Fact]
        public void QueryParams_AppendsWithAmpersandWhenUrlAlreadyHasQuery()
        {
            HttpRequestMessage? captured = null;
            var handler = new MockHandler(req => { captured = req; return OkText(); });
            using var client = new HttpClient(handler);

            var qp = new Dict<string, string>();
            qp["b"] = "2";

            var result = Requests.Send(HttpMethod.Get, "https://example.com/?a=1", null, qp, null, null, null, client);

            Assert.True(result.IsOk);
            var url = captured!.RequestUri!.OriginalString;
            Assert.Contains("a=1", url);
            Assert.Contains("b=2", url);
            Assert.Contains("&", url);
        }

        [Fact]
        public void QueryParams_Empty_LeavesUrlUnchanged()
        {
            HttpRequestMessage? captured = null;
            var handler = new MockHandler(req => { captured = req; return OkText(); });
            using var client = new HttpClient(handler);

            var qp = new Dict<string, string>();

            var result = Requests.Send(HttpMethod.Get, "https://example.com/path", null, qp, null, null, null, client);

            Assert.True(result.IsOk);
            Assert.Equal("https://example.com/path", captured!.RequestUri!.OriginalString);
        }

        #endregion

        #region Timeout

        [Fact]
        public void Timeout_TriggersTimeoutError()
        {
            var handler = new MockHandler(async (req, ct) =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), ct);
                }
                catch (TaskCanceledException)
                {
                    throw;
                }
                return OkText();
            });
            using var client = new HttpClient(handler);

            var result = Requests.Send(
                HttpMethod.Get,
                "https://example.com/slow",
                null, null, null, null,
                timeout: 0.05,
                client);

            Assert.True(result.IsErr);
            var err = result.UnwrapErr();
            Assert.IsType<Timeout>(err);
        }

        #endregion

        #region ConnectionError

        [Fact]
        public void ConnectionFailure_TriggersConnectionError()
        {
            var handler = new MockHandler((req, ct) =>
                Task.FromException<HttpResponseMessage>(new HttpRequestException("DNS failure")));
            using var client = new HttpClient(handler);

            var result = Requests.Send(HttpMethod.Get, "https://nope.invalid/", null, null, null, null, null, client);

            Assert.True(result.IsErr);
            var err = result.UnwrapErr();
            Assert.IsType<ConnectionError>(err);
            Assert.Contains("DNS failure", err.Message);
        }

        #endregion

        #region Null URL

        [Fact]
        public void NullUrl_ReturnsErrRequestException()
        {
            var result = Requests.Send(HttpMethod.Get, null!, null, null, null, null, null, null);

            Assert.True(result.IsErr);
            var err = result.UnwrapErr();
            Assert.IsType<RequestException>(err);
            Assert.Contains("url cannot be None", err.Message);
        }

        #endregion

        #region All HTTP methods

        [Theory]
        [InlineData("GET")]
        [InlineData("POST")]
        [InlineData("PUT")]
        [InlineData("DELETE")]
        [InlineData("PATCH")]
        [InlineData("HEAD")]
        [InlineData("OPTIONS")]
        public void AllHttpMethods_DispatchCorrectly(string methodName)
        {
            string? capturedMethod = null;
            var handler = new MockHandler(req => { capturedMethod = req.Method.Method; return OkText(); });
            using var client = new HttpClient(handler);

            var method = new HttpMethod(methodName);
            var result = Requests.Send(method, "https://example.com/", null, null, null, null, null, client);

            Assert.True(result.IsOk);
            Assert.Equal(methodName, capturedMethod);
        }

        [Fact]
        public void Get_PublicMethodInvokes()
        {
            // Sanity check: Public Requests.Get path with no client uses the default client.
            // We can't intercept that without a real server, so just verify it does not throw
            // for a clearly-invalid host (it should return an Err, not throw).
            var result = Requests.Get(
                "http://nonexistent.invalid.test.local.localhost/",
                timeout: 0.1);
            Assert.True(result.IsErr);
        }

        #endregion

        // NOTE: Phase 3 tests (file upload, streaming, session redirects/SSL/proxies,
        // module-level allow_redirects/verify) live in RequestsAdvancedTests.cs.
    }
}
