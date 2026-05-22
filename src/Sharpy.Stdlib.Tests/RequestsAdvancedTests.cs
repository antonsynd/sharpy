using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Sharpy.Tests
{
    /// <summary>
    /// Phase 3 requests tests: file upload (multipart), response streaming
    /// (iter_content / iter_lines), session-level redirects/SSL/proxy
    /// configuration. Uses the same MockHandler pattern as
    /// <see cref="RequestsModuleTests"/>.
    /// </summary>
    public class RequestsAdvancedTests
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

        private static string WriteTempFile(string contents)
        {
            var path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "sharpy_advtest_" + Guid.NewGuid().ToString("N") + ".txt");
            File.WriteAllText(path, contents);
            return path;
        }

        private static void SafeDelete(string path)
        {
            try
            { File.Delete(path); }
            catch { /* swallow */ }
        }

        #region File Upload (multipart/form-data)

        [Fact]
        public void Files_Upload_ProducesMultipartFormDataContent()
        {
            HttpRequestMessage? captured = null;
            var handler = new MockHandler(req => { captured = req; return OkText(); });
            using var client = new HttpClient(handler);

            var path = WriteTempFile("hello-from-disk");
            try
            {
                var files = new Dict<string, string>();
                files["upload"] = path;

                var result = Requests.Send(
                    HttpMethod.Post,
                    "https://example.com/upload",
                    headers: null,
                    params_: null,
                    json: null,
                    data: null,
                    timeout: null,
                    client: client,
                    auth: null,
                    files: files);

                Assert.True(result.IsOk);
                Assert.NotNull(captured);
                Assert.NotNull(captured!.Content);
                Assert.IsType<MultipartFormDataContent>(captured.Content);
                Assert.Equal("multipart/form-data", captured.Content!.Headers.ContentType!.MediaType);
            }
            finally
            {
                SafeDelete(path);
            }
        }

        [Fact]
        public void Files_Upload_IncludesFileContentsAndFilename()
        {
            string capturedBody = "";
            var handler = new MockHandler(req =>
            {
                if (req.Content != null)
                {
                    capturedBody = req.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                }
                return OkText();
            });
            using var client = new HttpClient(handler);

            var path = WriteTempFile("multipart-payload-XYZ");
            var fileName = System.IO.Path.GetFileName(path);
            try
            {
                var files = new Dict<string, string>();
                files["attachment"] = path;

                var result = Requests.Send(
                    HttpMethod.Post,
                    "https://example.com/upload",
                    null, null, null, null, null,
                    client, null,
                    files: files);

                Assert.True(result.IsOk);
                // The multipart body should contain the file contents verbatim.
                Assert.Contains("multipart-payload-XYZ", capturedBody);
                // It should include a filename= attribute referencing the actual filename.
                Assert.Contains("filename=", capturedBody);
                Assert.Contains(fileName, capturedBody);
                // And the field name we configured (quoting varies by .NET version).
                Assert.Contains("name=", capturedBody);
                Assert.Contains("attachment", capturedBody);
            }
            finally
            {
                SafeDelete(path);
            }
        }

        [Fact]
        public void Files_CombinedWithData_IncludesBothPartsInMultipart()
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

            var path = WriteTempFile("FILE-BYTES");
            try
            {
                var files = new Dict<string, string>();
                files["doc"] = path;
                var form = new Dict<string, string>();
                form["title"] = "important-report";
                form["author"] = "alice";

                var result = Requests.Send(
                    HttpMethod.Post,
                    "https://example.com/upload",
                    null, null, null, form, null,
                    client, null,
                    files: files);

                Assert.True(result.IsOk);
                Assert.IsType<MultipartFormDataContent>(captured!.Content);
                // File content
                Assert.Contains("FILE-BYTES", capturedBody);
                // Data fields are included as additional multipart parts (quoting varies).
                Assert.Contains("title", capturedBody);
                Assert.Contains("important-report", capturedBody);
                Assert.Contains("author", capturedBody);
                Assert.Contains("alice", capturedBody);
                // Also the file part.
                Assert.Contains("doc", capturedBody);
            }
            finally
            {
                SafeDelete(path);
            }
        }

        [Fact]
        public void Files_MultipleFiles_AllPartsPresent()
        {
            string capturedBody = "";
            var handler = new MockHandler(req =>
            {
                if (req.Content != null)
                {
                    capturedBody = req.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                }
                return OkText();
            });
            using var client = new HttpClient(handler);

            var p1 = WriteTempFile("PAYLOAD-ONE");
            var p2 = WriteTempFile("PAYLOAD-TWO");
            try
            {
                var files = new Dict<string, string>();
                files["first"] = p1;
                files["second"] = p2;

                var result = Requests.Send(
                    HttpMethod.Post,
                    "https://example.com/upload",
                    null, null, null, null, null,
                    client, null,
                    files: files);

                Assert.True(result.IsOk);
                Assert.Contains("PAYLOAD-ONE", capturedBody);
                Assert.Contains("PAYLOAD-TWO", capturedBody);
                // Field names appear (quoting may vary across .NET versions).
                Assert.Contains("first", capturedBody);
                Assert.Contains("second", capturedBody);
            }
            finally
            {
                SafeDelete(p1);
                SafeDelete(p2);
            }
        }

        [Fact]
        public void Files_TakePrecedenceOverJson()
        {
            HttpRequestMessage? captured = null;
            var handler = new MockHandler(req => { captured = req; return OkText(); });
            using var client = new HttpClient(handler);

            var path = WriteTempFile("file-wins");
            try
            {
                var files = new Dict<string, string>();
                files["f"] = path;
                var jsonObj = new Dict<string, object?>();
                jsonObj["should_be_ignored"] = true;

                var result = Requests.Send(
                    HttpMethod.Post,
                    "https://example.com/",
                    null, null, jsonObj, null, null,
                    client, null,
                    files: files);

                Assert.True(result.IsOk);
                Assert.IsType<MultipartFormDataContent>(captured!.Content);
                Assert.Equal("multipart/form-data", captured.Content!.Headers.ContentType!.MediaType);
            }
            finally
            {
                SafeDelete(path);
            }
        }

        [Fact]
        public void Files_MissingFile_ReturnsErrWithPathInMessage()
        {
            var handler = new MockHandler(req => OkText());
            using var client = new HttpClient(handler);

            var missing = "/this/path/definitely/does/not/exist/sharpy_test_missing.bin";
            var files = new Dict<string, string>();
            files["f"] = missing;

            var result = Requests.Send(
                HttpMethod.Post,
                "https://example.com/upload",
                null, null, null, null, null,
                client, null,
                files: files);

            Assert.True(result.IsErr);
            var err = result.UnwrapErr();
            Assert.IsType<RequestException>(err);
            Assert.Contains(missing, err.Message);
        }

        #endregion

        #region Streaming (iter_content / iter_lines)

        [Fact]
        public void IterContent_YieldsByteArrayChunks_WithCorrectTotalSize()
        {
            var bodyBytes = Encoding.UTF8.GetBytes("0123456789ABCDEFGHIJ"); // 20 bytes
            var handler = new MockHandler(req =>
            {
                var msg = new HttpResponseMessage(HttpStatusCode.OK);
                msg.Content = new ByteArrayContent(bodyBytes);
                return msg;
            });
            using var client = new HttpClient(handler);

            var result = Requests.Send(
                HttpMethod.Get,
                "https://example.com/stream",
                null, null, null, null, null,
                client, null, null,
                stream: true);
            Assert.True(result.IsOk);
            using var response = result.Unwrap();

            var chunks = new System.Collections.Generic.List<byte[]>();
            foreach (var c in response.IterContent(chunkSize: 4))
            {
                chunks.Add(c);
            }

            Assert.NotEmpty(chunks);
            foreach (var chunk in chunks)
            {
                Assert.IsType<byte[]>(chunk);
                Assert.True(chunk.Length <= 4);
                Assert.True(chunk.Length > 0);
            }

            // Concatenating chunks must reconstruct the original payload.
            var concatenated = chunks.SelectMany(c => c).ToArray();
            Assert.Equal(bodyBytes, concatenated);
            Assert.Equal(bodyBytes.Length, concatenated.Length);
        }

        [Fact]
        public void IterContent_ChunkSizeOne_YieldsByteByByte()
        {
            var bodyBytes = new byte[] { 0x10, 0x20, 0x30, 0x40 };
            var handler = new MockHandler(req =>
            {
                var msg = new HttpResponseMessage(HttpStatusCode.OK);
                msg.Content = new ByteArrayContent(bodyBytes);
                return msg;
            });
            using var client = new HttpClient(handler);

            var result = Requests.Send(
                HttpMethod.Get, "https://example.com/", null, null, null, null, null,
                client, null, null, stream: true);
            using var response = result.Unwrap();

            var collected = new System.Collections.Generic.List<byte>();
            foreach (var chunk in response.IterContent(chunkSize: 1))
            {
                Assert.Single(chunk);
                collected.Add(chunk[0]);
            }
            Assert.Equal(bodyBytes, collected.ToArray());
        }

        [Fact]
        public void IterContent_LargeChunkSize_YieldsSingleChunk()
        {
            var bodyBytes = Encoding.UTF8.GetBytes("small");
            var handler = new MockHandler(req =>
            {
                var msg = new HttpResponseMessage(HttpStatusCode.OK);
                msg.Content = new ByteArrayContent(bodyBytes);
                return msg;
            });
            using var client = new HttpClient(handler);

            var result = Requests.Send(
                HttpMethod.Get, "https://example.com/", null, null, null, null, null,
                client, null, null, stream: true);
            using var response = result.Unwrap();

            var chunks = response.IterContent(chunkSize: 4096).ToList();
            Assert.Single(chunks);
            Assert.Equal(bodyBytes, chunks[0]);
        }

        [Fact]
        public void IterContent_EmptyBody_YieldsNoChunks()
        {
            var handler = new MockHandler(req =>
            {
                var msg = new HttpResponseMessage(HttpStatusCode.OK);
                msg.Content = new ByteArrayContent(Array.Empty<byte>());
                return msg;
            });
            using var client = new HttpClient(handler);

            var result = Requests.Send(
                HttpMethod.Get, "https://example.com/", null, null, null, null, null,
                client, null, null, stream: true);
            using var response = result.Unwrap();

            var chunks = response.IterContent(chunkSize: 16).ToList();
            Assert.Empty(chunks);
        }

        [Fact]
        public void IterLines_SplitsOnNewlines()
        {
            var body = "alpha\nbeta\ngamma";
            var handler = new MockHandler(req =>
            {
                var msg = new HttpResponseMessage(HttpStatusCode.OK);
                msg.Content = new StringContent(body, Encoding.UTF8, "text/plain");
                return msg;
            });
            using var client = new HttpClient(handler);

            var result = Requests.Send(
                HttpMethod.Get, "https://example.com/", null, null, null, null, null,
                client, null, null, stream: true);
            using var response = result.Unwrap();

            var lines = response.IterLines().ToList();
            Assert.Equal(new[] { "alpha", "beta", "gamma" }, lines);
        }

        [Fact]
        public void IterLines_HandlesCrLfLineEndings()
        {
            var body = "line-a\r\nline-b\r\nline-c";
            var handler = new MockHandler(req =>
            {
                var msg = new HttpResponseMessage(HttpStatusCode.OK);
                msg.Content = new StringContent(body, Encoding.UTF8, "text/plain");
                return msg;
            });
            using var client = new HttpClient(handler);

            var result = Requests.Send(
                HttpMethod.Get, "https://example.com/", null, null, null, null, null,
                client, null, null, stream: true);
            using var response = result.Unwrap();

            var lines = response.IterLines().ToList();
            // StreamReader.ReadLine strips both \r\n and \n line terminators.
            Assert.Equal(new[] { "line-a", "line-b", "line-c" }, lines);
        }

        [Fact]
        public void IterLines_EmptyBody_YieldsNoLines()
        {
            var handler = new MockHandler(req =>
            {
                var msg = new HttpResponseMessage(HttpStatusCode.OK);
                msg.Content = new ByteArrayContent(Array.Empty<byte>());
                return msg;
            });
            using var client = new HttpClient(handler);

            var result = Requests.Send(
                HttpMethod.Get, "https://example.com/", null, null, null, null, null,
                client, null, null, stream: true);
            using var response = result.Unwrap();

            Assert.Empty(response.IterLines().ToList());
        }

        [Fact]
        public void IterContent_NonStreamingResponse_Throws()
        {
            var handler = new MockHandler(req => OkText("body-data"));
            using var client = new HttpClient(handler);

            var result = Requests.Send(
                HttpMethod.Get, "https://example.com/", null, null, null, null, null, client);
            using var response = result.Unwrap();

            Assert.Throws<InvalidOperationException>(() =>
            {
                foreach (var _ in response.IterContent(8))
                { }
            });
        }

        [Fact]
        public void IterLines_NonStreamingResponse_Throws()
        {
            var handler = new MockHandler(req => OkText("body\ndata"));
            using var client = new HttpClient(handler);

            var result = Requests.Send(
                HttpMethod.Get, "https://example.com/", null, null, null, null, null, client);
            using var response = result.Unwrap();

            Assert.Throws<InvalidOperationException>(() =>
            {
                foreach (var _ in response.IterLines())
                { }
            });
        }

        [Fact]
        public void IterContent_AfterContentRead_Throws()
        {
            var handler = new MockHandler(req =>
            {
                var msg = new HttpResponseMessage(HttpStatusCode.OK);
                msg.Content = new StringContent("payload", Encoding.UTF8, "text/plain");
                return msg;
            });
            using var client = new HttpClient(handler);

            var result = Requests.Send(
                HttpMethod.Get, "https://example.com/", null, null, null, null, null,
                client, null, null, stream: true);
            using var response = result.Unwrap();

            // Force-read .Content first.
            _ = response.Content;

            Assert.Throws<InvalidOperationException>(() =>
            {
                foreach (var _ in response.IterContent(4))
                { }
            });
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-100)]
        public void IterContent_NonPositiveChunkSize_Throws(int chunkSize)
        {
            var handler = new MockHandler(req =>
            {
                var msg = new HttpResponseMessage(HttpStatusCode.OK);
                msg.Content = new ByteArrayContent(new byte[] { 1, 2, 3 });
                return msg;
            });
            using var client = new HttpClient(handler);

            var result = Requests.Send(
                HttpMethod.Get, "https://example.com/", null, null, null, null, null,
                client, null, null, stream: true);
            using var response = result.Unwrap();

            Assert.Throws<ArgumentOutOfRangeException>(() => response.IterContent(chunkSize));
        }

        #endregion

        #region Module-level allow_redirects / verify

        [Fact]
        public void ModuleLevel_AllowRedirects_False_StillIssuesRequest()
        {
            // With no caller-supplied client, allow_redirects=false triggers the
            // one-off HttpClient/HttpClientHandler path. We can't reach a real
            // server, so we just verify the call returns an Err (connection
            // failure or timeout) rather than throwing.
            var result = Requests.Get(
                "http://nonexistent.invalid.test.local.localhost/",
                timeout: 0.1,
                allow_redirects: false);
            Assert.True(result.IsErr);
        }

        [Fact]
        public void ModuleLevel_Verify_False_StillIssuesRequest()
        {
            // verify=false also triggers the one-off client path.
            var result = Requests.Get(
                "http://nonexistent.invalid.test.local.localhost/",
                timeout: 0.1,
                verify: false);
            Assert.True(result.IsErr);
        }

        [Fact]
        public void ModuleLevel_AllowRedirectsAndVerify_BothFalse_StillIssuesRequest()
        {
            var result = Requests.Get(
                "http://nonexistent.invalid.test.local.localhost/",
                timeout: 0.1,
                allow_redirects: false,
                verify: false);
            Assert.True(result.IsErr);
        }

        #endregion

        #region Session Redirects

        [Fact]
        public void Session_AllowRedirects_DefaultsToTrue()
        {
            using var session = new Session();
            Assert.True(session.AllowRedirects);
        }

        [Fact]
        public void Session_MaxRedirects_DefaultsTo30()
        {
            using var session = new Session();
            Assert.Equal(30, session.MaxRedirects);
        }

        [Fact]
        public void Session_AllowRedirects_CanBeDisabled()
        {
            using var session = new Session();
            session.AllowRedirects = false;
            Assert.False(session.AllowRedirects);
            // Re-enabling restores the previous value.
            session.AllowRedirects = true;
            Assert.True(session.AllowRedirects);
        }

        [Fact]
        public void Session_MaxRedirects_CanBeOverridden()
        {
            using var session = new Session();
            session.MaxRedirects = 7;
            Assert.Equal(7, session.MaxRedirects);
            session.MaxRedirects = 100;
            Assert.Equal(100, session.MaxRedirects);
        }

        [Fact]
        public void Session_MaxRedirects_RejectsZeroOrNegative()
        {
            using var session = new Session();
            Assert.Throws<ArgumentOutOfRangeException>(() => session.MaxRedirects = 0);
            Assert.Throws<ArgumentOutOfRangeException>(() => session.MaxRedirects = -1);
            Assert.Throws<ArgumentOutOfRangeException>(() => session.MaxRedirects = -100);
            // Value should be unchanged.
            Assert.Equal(30, session.MaxRedirects);
        }

        #endregion

        #region Session SSL Verification

        [Fact]
        public void Session_Verify_DefaultsToTrue()
        {
            using var session = new Session();
            Assert.True(session.Verify);
        }

        [Fact]
        public void Session_Verify_CanBeDisabled()
        {
            using var session = new Session();
            session.Verify = false;
            Assert.False(session.Verify);
        }

        [Fact]
        public void Session_Verify_RoundTrips()
        {
            using var session = new Session();
            session.Verify = false;
            Assert.False(session.Verify);
            session.Verify = true;
            Assert.True(session.Verify);
        }

        #endregion

        #region Session Proxies

        [Fact]
        public void Session_Proxies_DefaultIsNull()
        {
            using var session = new Session();
            Assert.Null(session.Proxies);
        }

        [Fact]
        public void Session_Proxies_CanBeSetToDict()
        {
            using var session = new Session();
            var proxies = new Dict<string, string>();
            proxies["http"] = "http://proxy.example.com:8080";
            proxies["https"] = "http://secure-proxy.example.com:8443";

            session.Proxies = proxies;

            Assert.NotNull(session.Proxies);
            Assert.Equal(2, session.Proxies!.Count);
            Assert.Equal("http://proxy.example.com:8080", session.Proxies["http"]);
            Assert.Equal("http://secure-proxy.example.com:8443", session.Proxies["https"]);
        }

        [Fact]
        public void Session_Proxies_CanBeClearedBackToNull()
        {
            using var session = new Session();
            var proxies = new Dict<string, string>();
            proxies["http"] = "http://proxy.example.com:8080";
            session.Proxies = proxies;
            Assert.NotNull(session.Proxies);

            session.Proxies = null;
            Assert.Null(session.Proxies);
        }

        [Fact]
        public void Session_Proxies_EmptyDictIsAllowed()
        {
            using var session = new Session();
            session.Proxies = new Dict<string, string>();
            Assert.NotNull(session.Proxies);
            Assert.Empty((System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<string, string>>)session.Proxies!);
        }

        #endregion
    }
}
