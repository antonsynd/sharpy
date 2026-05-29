using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;

namespace Sharpy
{
    /// <summary>
    /// Represents an HTTP response from a low-level HTTP connection.
    /// Equivalent to Python's <c>http.client.HTTPResponse</c>.
    /// </summary>
    [SharpyModuleType("http")]
    public sealed class HTTPResponse : IDisposable
    {
        private readonly HttpResponseMessage _httpResponse;
        private byte[]? _body;
        private Dict<string, string>? _headers;
        private bool _disposed;

        internal HTTPResponse(HttpResponseMessage httpResponse)
        {
            _httpResponse = httpResponse ?? throw new ArgumentNullException(nameof(httpResponse));
        }

        /// <summary>The HTTP status code (e.g. 200, 404).</summary>
        public int status => (int)_httpResponse.StatusCode;

        /// <summary>The HTTP reason phrase (e.g. "OK", "Not Found").</summary>
        public string reason => _httpResponse.ReasonPhrase ?? string.Empty;

        /// <summary>The HTTP version string.</summary>
        public string version
        {
            get
            {
                var v = _httpResponse.Version;
                if (v.Major == 1 && v.Minor == 1) return "HTTP/1.1";
                if (v.Major == 2 && v.Minor == 0) return "HTTP/2";
                return "HTTP/" + v.Major + "." + v.Minor;
            }
        }

        /// <summary>
        /// Read and return the response body as bytes.
        /// Equivalent to Python's <c>HTTPResponse.read()</c>.
        /// </summary>
        public byte[] read()
        {
            if (_body == null)
            {
                _body = _httpResponse.Content == null
                    ? Array.Empty<byte>()
                    : _httpResponse.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
            }
            return _body;
        }

        /// <summary>
        /// Read a specific number of bytes from the response body.
        /// If <paramref name="amt"/> is larger than the remaining body, returns whatever is available.
        /// </summary>
        public byte[] read(int amt)
        {
            var all = read();
            if (amt >= all.Length) return all;
            var result = new byte[amt];
            Array.Copy(all, 0, result, 0, amt);
            return result;
        }

        /// <summary>
        /// Get a response header value by name. Returns empty string if not found.
        /// Equivalent to Python's <c>HTTPResponse.getheader(name, default)</c>.
        /// </summary>
        public string getheader(string name, string default_ = "")
        {
            if (_httpResponse.Headers != null)
            {
                IEnumerable<string>? values;
                if (_httpResponse.Headers.TryGetValues(name, out values))
                {
                    return string.Join(", ", values);
                }
            }
            if (_httpResponse.Content != null && _httpResponse.Content.Headers != null)
            {
                IEnumerable<string>? values;
                if (_httpResponse.Content.Headers.TryGetValues(name, out values))
                {
                    return string.Join(", ", values);
                }
            }
            return default_;
        }

        /// <summary>
        /// Get all response headers as a dictionary.
        /// Equivalent to Python's <c>HTTPResponse.getheaders()</c>.
        /// </summary>
        public Dict<string, string> getheaders()
        {
            if (_headers == null)
            {
                _headers = new Dict<string, string>();
                if (_httpResponse.Headers != null)
                {
                    foreach (var header in _httpResponse.Headers)
                    {
                        _headers[header.Key] = string.Join(", ", header.Value);
                    }
                }
                if (_httpResponse.Content != null && _httpResponse.Content.Headers != null)
                {
                    foreach (var header in _httpResponse.Content.Headers)
                    {
                        _headers[header.Key] = string.Join(", ", header.Value);
                    }
                }
            }
            return _headers;
        }

        /// <summary>Releases the underlying response resources.</summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _httpResponse.Dispose();
            }
        }
    }
}
