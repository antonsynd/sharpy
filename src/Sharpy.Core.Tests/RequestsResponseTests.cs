using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using FluentAssertions;
using Xunit;

namespace Sharpy.Tests
{
    public class RequestsResponseTests
    {
        private static HttpResponseMessage MakeResponse(
            HttpStatusCode statusCode,
            string? body = null,
            string? contentType = null,
            string? requestUrl = null)
        {
            var msg = new HttpResponseMessage(statusCode);
            if (body != null)
            {
                msg.Content = new ByteArrayContent(Encoding.UTF8.GetBytes(body));
                if (contentType != null)
                {
                    msg.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
                }
            }
            else
            {
                msg.Content = new ByteArrayContent(Array.Empty<byte>());
            }
            if (requestUrl != null)
            {
                msg.RequestMessage = new HttpRequestMessage(HttpMethod.Get, requestUrl);
            }
            return msg;
        }

        #region StatusCode

        [Theory]
        [InlineData(HttpStatusCode.OK, 200)]
        [InlineData(HttpStatusCode.Created, 201)]
        [InlineData(HttpStatusCode.NoContent, 204)]
        [InlineData(HttpStatusCode.MovedPermanently, 301)]
        [InlineData(HttpStatusCode.BadRequest, 400)]
        [InlineData(HttpStatusCode.NotFound, 404)]
        [InlineData(HttpStatusCode.InternalServerError, 500)]
        public void StatusCode_ReturnsCorrectInt(HttpStatusCode code, int expected)
        {
            using var httpMsg = MakeResponse(code);
            using var response = new Response(httpMsg);
            Assert.Equal(expected, response.StatusCode);
        }

        #endregion

        #region Ok

        [Theory]
        [InlineData(HttpStatusCode.OK)]
        [InlineData(HttpStatusCode.Created)]
        [InlineData(HttpStatusCode.NoContent)]
        public void Ok_TrueFor2xxStatusCodes(HttpStatusCode code)
        {
            using var httpMsg = MakeResponse(code);
            using var response = new Response(httpMsg);
            Assert.True(response.Ok);
        }

        [Theory]
        [InlineData(HttpStatusCode.MovedPermanently)]
        [InlineData(HttpStatusCode.BadRequest)]
        [InlineData(HttpStatusCode.NotFound)]
        [InlineData(HttpStatusCode.InternalServerError)]
        public void Ok_FalseForNon2xxStatusCodes(HttpStatusCode code)
        {
            using var httpMsg = MakeResponse(code);
            using var response = new Response(httpMsg);
            Assert.False(response.Ok);
        }

        #endregion

        #region Text

        [Fact]
        public void Text_ReturnsDecodedBody()
        {
            using var httpMsg = MakeResponse(HttpStatusCode.OK, "hello world", "text/plain; charset=utf-8");
            using var response = new Response(httpMsg);
            Assert.Equal("hello world", response.Text);
        }

        [Fact]
        public void Text_EmptyBody_ReturnsEmptyString()
        {
            using var httpMsg = MakeResponse(HttpStatusCode.OK);
            using var response = new Response(httpMsg);
            Assert.Equal("", response.Text);
        }

        [Fact]
        public void Text_DecodesNonUtf8Encoding()
        {
            // Latin1-encoded bytes for "café"
            var msg = new HttpResponseMessage(HttpStatusCode.OK);
            msg.Content = new ByteArrayContent(new byte[] { 0x63, 0x61, 0x66, 0xE9 });
            msg.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("text/plain; charset=iso-8859-1");
            using var response = new Response(msg);
            Assert.Equal("café", response.Text);
        }

        [Fact]
        public void Text_IsCached()
        {
            using var httpMsg = MakeResponse(HttpStatusCode.OK, "abc", "text/plain; charset=utf-8");
            using var response = new Response(httpMsg);
            var first = response.Text;
            var second = response.Text;
            Assert.Same(first, second);
        }

        #endregion

        #region Content

        [Fact]
        public void Content_ReturnsRawBytes()
        {
            using var httpMsg = MakeResponse(HttpStatusCode.OK, "abc");
            using var response = new Response(httpMsg);
            Assert.Equal(new byte[] { 0x61, 0x62, 0x63 }, response.Content);
        }

        [Fact]
        public void Content_EmptyBody_ReturnsEmptyArray()
        {
            using var httpMsg = MakeResponse(HttpStatusCode.OK);
            using var response = new Response(httpMsg);
            Assert.Empty(response.Content);
        }

        [Fact]
        public void Content_IsCached()
        {
            using var httpMsg = MakeResponse(HttpStatusCode.OK, "abc");
            using var response = new Response(httpMsg);
            var first = response.Content;
            var second = response.Content;
            Assert.Same(first, second);
        }

        #endregion

        #region Url

        [Fact]
        public void Url_ReturnsRequestUri()
        {
            using var httpMsg = MakeResponse(HttpStatusCode.OK, requestUrl: "https://example.com/path");
            using var response = new Response(httpMsg);
            Assert.Equal("https://example.com/path", response.Url);
        }

        [Fact]
        public void Url_NoRequestMessage_ReturnsEmptyString()
        {
            using var httpMsg = MakeResponse(HttpStatusCode.OK);
            using var response = new Response(httpMsg);
            Assert.Equal("", response.Url);
        }

        #endregion

        #region Headers

        [Fact]
        public void Headers_ReturnsCombinedResponseAndContentHeaders()
        {
            var msg = new HttpResponseMessage(HttpStatusCode.OK);
            msg.Content = new StringContent("body", Encoding.UTF8, "application/json");
            msg.Headers.TryAddWithoutValidation("X-Custom", "value");
            using var response = new Response(msg);

            var headers = response.Headers;
            Assert.True(headers.ContainsKey("X-Custom"));
            Assert.Equal("value", headers["X-Custom"]);
            Assert.True(headers.ContainsKey("Content-Type"));
        }

        [Fact]
        public void Headers_MultiValueJoinedWithCommaSpace()
        {
            var msg = new HttpResponseMessage(HttpStatusCode.OK);
            msg.Content = new ByteArrayContent(Array.Empty<byte>());
            msg.Headers.TryAddWithoutValidation("X-Multi", "a");
            msg.Headers.TryAddWithoutValidation("X-Multi", "b");
            using var response = new Response(msg);

            Assert.Equal("a, b", response.Headers["X-Multi"]);
        }

        [Fact]
        public void Headers_IsCached()
        {
            using var httpMsg = MakeResponse(HttpStatusCode.OK, "x", "text/plain");
            using var response = new Response(httpMsg);
            var first = response.Headers;
            var second = response.Headers;
            Assert.Same(first, second);
        }

        #endregion

        #region Encoding

        [Fact]
        public void Encoding_ExtractsCharsetFromContentType()
        {
            using var httpMsg = MakeResponse(HttpStatusCode.OK, "x", "text/html; charset=iso-8859-1");
            using var response = new Response(httpMsg);
            Assert.Equal("iso-8859-1", response.Encoding);
        }

        [Fact]
        public void Encoding_NoCharset_DefaultsToUtf8()
        {
            using var httpMsg = MakeResponse(HttpStatusCode.OK, "x", "text/plain");
            using var response = new Response(httpMsg);
            Assert.Equal("utf-8", response.Encoding);
        }

        [Fact]
        public void Encoding_NoContentType_DefaultsToUtf8()
        {
            using var httpMsg = MakeResponse(HttpStatusCode.OK);
            using var response = new Response(httpMsg);
            Assert.Equal("utf-8", response.Encoding);
        }

        #endregion

        #region Json

        [Fact]
        public void Json_ParsesJsonBody()
        {
            using var httpMsg = MakeResponse(
                HttpStatusCode.OK,
                "{\"name\":\"alice\",\"age\":30}",
                "application/json; charset=utf-8");
            using var response = new Response(httpMsg);

            var parsed = response.Json() as Dict<string, object?>;
            Assert.NotNull(parsed);
            Assert.Equal("alice", parsed!["name"]);
            Assert.Equal(30, parsed["age"]);
        }

        [Fact]
        public void Json_ParsesJsonArray()
        {
            using var httpMsg = MakeResponse(
                HttpStatusCode.OK,
                "[1, 2, 3]",
                "application/json; charset=utf-8");
            using var response = new Response(httpMsg);

            var parsed = response.Json() as List<object?>;
            Assert.NotNull(parsed);
            Assert.Equal(3, ((System.Collections.Generic.ICollection<object?>)parsed!).Count);
        }

        [Fact]
        public void Json_EmptyBody_ThrowsJsonDecodeError()
        {
            using var httpMsg = MakeResponse(HttpStatusCode.OK);
            using var response = new Response(httpMsg);
            Assert.Throws<JSONDecodeError>(() => response.Json());
        }

        [Fact]
        public void Json_InvalidJson_ThrowsJsonDecodeError()
        {
            using var httpMsg = MakeResponse(HttpStatusCode.OK, "not json", "text/plain");
            using var response = new Response(httpMsg);
            Assert.Throws<JSONDecodeError>(() => response.Json());
        }

        #endregion

        #region ToString

        [Fact]
        public void ToString_ReturnsBracketedStatus()
        {
            using var httpMsg = MakeResponse(HttpStatusCode.OK);
            using var response = new Response(httpMsg);
            Assert.Equal("<Response [200]>", response.ToString());
        }

        [Theory]
        [InlineData(HttpStatusCode.NotFound, "<Response [404]>")]
        [InlineData(HttpStatusCode.InternalServerError, "<Response [500]>")]
        public void ToString_FormatsVariousStatusCodes(HttpStatusCode code, string expected)
        {
            using var httpMsg = MakeResponse(code);
            using var response = new Response(httpMsg);
            Assert.Equal(expected, response.ToString());
        }

        #endregion

        #region Dispose

        [Fact]
        public void Dispose_DoesNotThrow()
        {
            var httpMsg = MakeResponse(HttpStatusCode.OK);
            var response = new Response(httpMsg);
            response.Dispose();
            // calling twice should be safe (HttpResponseMessage.Dispose is idempotent)
            response.Dispose();
        }

        [Fact]
        public void Constructor_NullHttpResponse_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new Response(null!));
        }

        #endregion
    }
}
