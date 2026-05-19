using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace Sharpy
{
    /// <summary>
    /// Represents an HTTP response. Wraps <see cref="System.Net.Http.HttpResponseMessage"/>.
    /// Equivalent to Python's <c>requests.Response</c>.
    /// </summary>
    [SharpyModuleType("requests")]
    public sealed class Response : IDisposable
    {
        private readonly HttpResponseMessage _httpResponse;
        private readonly bool _isStreaming;
        private readonly HttpClient? _ownedClient;
        private readonly HttpClientHandler? _ownedHandler;
        private byte[]? _content;
        private string? _text;
        private Dict<string, string>? _headers;
        private string? _encoding;
        private Stream? _stream;
        private bool _streamConsumed;
        private bool _disposed;

        /// <summary>
        /// Create a Response wrapping the given <see cref="HttpResponseMessage"/>.
        /// Internal — callers obtain Response objects via the requests module functions.
        /// </summary>
        internal Response(HttpResponseMessage httpResponse)
            : this(httpResponse, isStreaming: false, ownedClient: null, ownedHandler: null)
        {
        }

        /// <summary>
        /// Create a Response wrapping the given <see cref="HttpResponseMessage"/>, optionally
        /// taking ownership of a per-request <see cref="HttpClient"/>/<see cref="HttpClientHandler"/>
        /// pair (used when the request needs configuration that the shared default client cannot
        /// provide, e.g. disabled redirects or disabled TLS verification).
        /// </summary>
        internal Response(
            HttpResponseMessage httpResponse,
            bool isStreaming,
            HttpClient? ownedClient,
            HttpClientHandler? ownedHandler)
        {
            _httpResponse = httpResponse ?? throw new ArgumentNullException(nameof(httpResponse));
            _isStreaming = isStreaming;
            _ownedClient = ownedClient;
            _ownedHandler = ownedHandler;
        }

        /// <summary>The HTTP status code returned by the server (e.g., 200, 404).</summary>
        public int StatusCode => (int)_httpResponse.StatusCode;

        /// <summary>
        /// True if the status code is in the 2xx range, false otherwise.
        /// Equivalent to Python's <c>response.ok</c>.
        /// </summary>
        public bool Ok => StatusCode >= 200 && StatusCode < 300;

        /// <summary>The URL of the response (i.e., the final URL after redirects).</summary>
        public string Url
        {
            get
            {
                var uri = _httpResponse.RequestMessage?.RequestUri;
                return uri == null ? string.Empty : uri.ToString();
            }
        }

        /// <summary>The raw response body as a byte array. Read once and cached.</summary>
        public byte[] Content
        {
            get
            {
                if (_content == null)
                {
                    _content = _httpResponse.Content == null
                        ? Array.Empty<byte>()
                        : _httpResponse.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                }
                return _content;
            }
        }

        /// <summary>The response body decoded as a string using the response encoding. Lazily computed.</summary>
        public string Text
        {
            get
            {
                if (_text == null)
                {
                    var bytes = Content;
                    var encodingName = Encoding;
                    System.Text.Encoding encoder;
                    try
                    {
                        encoder = System.Text.Encoding.GetEncoding(encodingName);
                    }
                    catch (ArgumentException)
                    {
                        encoder = System.Text.Encoding.UTF8;
                    }
                    _text = encoder.GetString(bytes, 0, bytes.Length);
                }
                return _text;
            }
        }

        /// <summary>
        /// The encoding of the response body, as advertised by the <c>Content-Type</c> charset
        /// parameter. Defaults to <c>"utf-8"</c> if not specified.
        /// </summary>
        public string Encoding
        {
            get
            {
                if (_encoding == null)
                {
                    _encoding = ExtractEncoding();
                }
                return _encoding;
            }
        }

        /// <summary>
        /// The response headers (both regular and content headers). Header values are joined
        /// with <c>", "</c> if there are multiple values for the same header.
        /// </summary>
        public Dict<string, string> Headers
        {
            get
            {
                if (_headers == null)
                {
                    _headers = BuildHeaders();
                }
                return _headers;
            }
        }

        /// <summary>
        /// Parse the response body as JSON and return the resulting object.
        /// Equivalent to Python's <c>response.json()</c>.
        /// </summary>
        /// <returns>The parsed JSON object (Dict, List, string, int, double, bool, or null).</returns>
        public object? Json()
        {
            return JsonParser.Parse(Text);
        }

        /// <summary>
        /// Returns <c>Ok(this)</c> for 2xx status codes, or <c>Err(HTTPError)</c> for 4xx/5xx
        /// (and any other non-2xx) status codes.
        /// Equivalent to Python's <c>response.raise_for_status()</c>, but uses a tagged
        /// <see cref="Result{T, E}"/> instead of raising.
        /// </summary>
        public Result<Response, RequestException> RaiseForStatus()
        {
            if (StatusCode >= 200 && StatusCode < 300)
            {
                return Result<Response, RequestException>.Ok(this);
            }

            string kind;
            if (StatusCode >= 400 && StatusCode < 500)
            {
                kind = "Client Error";
            }
            else if (StatusCode >= 500 && StatusCode < 600)
            {
                kind = "Server Error";
            }
            else
            {
                kind = "Error";
            }

            var message = StatusCode + " " + kind + " for url: " + Url;
            return Result<Response, RequestException>.Err(new HTTPError(message, this));
        }

        /// <summary>Returns a string representation of this response (e.g., <c>&lt;Response [200]&gt;</c>).</summary>
        public override string ToString()
        {
            return "<Response [" + StatusCode + "]>";
        }

        /// <summary>
        /// Iterate over the response body in chunks of the given size (default 1024 bytes).
        /// The response must have been created with <c>stream=true</c> and the body must not
        /// have been fully read (via <see cref="Content"/> or <see cref="Text"/>).
        /// Equivalent to Python's <c>response.iter_content(chunk_size)</c>.
        /// </summary>
        public IEnumerable<byte[]> IterContent(int chunkSize = 1024)
        {
            if (chunkSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(chunkSize), "chunk_size must be > 0");
            }
            return IterContentImpl(chunkSize);
        }

        private IEnumerable<byte[]> IterContentImpl(int chunkSize)
        {
            EnsureStreamable();
            var stream = AcquireStream();
            var buffer = new byte[chunkSize];
            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                var copy = new byte[read];
                Buffer.BlockCopy(buffer, 0, copy, 0, read);
                yield return copy;
            }
        }

        /// <summary>
        /// Iterate over the response body line by line, using the response encoding to decode
        /// bytes. The response must have been created with <c>stream=true</c> and the body
        /// must not have been fully read.
        /// Equivalent to Python's <c>response.iter_lines()</c>.
        /// </summary>
        public IEnumerable<string> IterLines()
        {
            return IterLinesImpl();
        }

        private IEnumerable<string> IterLinesImpl()
        {
            EnsureStreamable();
            var stream = AcquireStream();
            System.Text.Encoding encoder;
            try
            {
                encoder = System.Text.Encoding.GetEncoding(Encoding);
            }
            catch (ArgumentException)
            {
                encoder = System.Text.Encoding.UTF8;
            }
            using var reader = new StreamReader(stream, encoder, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                yield return line;
            }
        }

        private void EnsureStreamable()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(Response));
            }
            if (!_isStreaming)
            {
                throw new InvalidOperationException(
                    "Response was not opened in streaming mode; pass stream=true to enable iter_content/iter_lines.");
            }
            if (_content != null)
            {
                throw new InvalidOperationException(
                    "Cannot iterate response: body has already been read via .content or .text.");
            }
            if (_streamConsumed)
            {
                throw new InvalidOperationException(
                    "Response stream has already been consumed.");
            }
        }

        private Stream AcquireStream()
        {
            if (_stream == null)
            {
                _stream = _httpResponse.Content == null
                    ? Stream.Null
                    : _httpResponse.Content.ReadAsStreamAsync().GetAwaiter().GetResult();
            }
            _streamConsumed = true;
            return _stream;
        }

        /// <summary>Releases the underlying <see cref="HttpResponseMessage"/>.</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            try
            { _stream?.Dispose(); }
            catch { /* swallow */ }
            _httpResponse.Dispose();
            _ownedClient?.Dispose();
            _ownedHandler?.Dispose();
        }

        private string ExtractEncoding()
        {
            MediaTypeHeaderValue? contentType = null;
            if (_httpResponse.Content != null && _httpResponse.Content.Headers != null)
            {
                contentType = _httpResponse.Content.Headers.ContentType;
            }

            if (contentType != null && !string.IsNullOrEmpty(contentType.CharSet))
            {
                var charset = contentType.CharSet!;
                // Strip surrounding quotes if present (some servers emit charset="utf-8")
                if (charset.Length >= 2 && charset[0] == '"' && charset[charset.Length - 1] == '"')
                {
                    charset = charset.Substring(1, charset.Length - 2);
                }
                return charset;
            }

            return "utf-8";
        }

        private Dict<string, string> BuildHeaders()
        {
            var result = new Dict<string, string>();

            if (_httpResponse.Headers != null)
            {
                foreach (var header in _httpResponse.Headers)
                {
                    result[header.Key] = string.Join(", ", header.Value);
                }
            }

            if (_httpResponse.Content != null && _httpResponse.Content.Headers != null)
            {
                foreach (var header in _httpResponse.Content.Headers)
                {
                    result[header.Key] = string.Join(", ", header.Value);
                }
            }

            return result;
        }
    }
}
