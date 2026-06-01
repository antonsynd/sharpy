using System;
using System.Collections.Generic;
using System.Net.Http;

namespace Sharpy
{
    /// <summary>
    /// HTTP response from a low-level HTTP connection.
    /// Equivalent to Python's <c>http.client.HTTPResponse</c>.
    /// </summary>
    [SharpyModuleType("http", "HTTPResponse")]
    public sealed class HTTPResponse : IDisposable
    {
        private readonly HttpResponseMessage _response;
        private Bytes? _body;
        private bool _disposed;

        internal HTTPResponse(HttpResponseMessage response)
        {
            _response = response ?? throw new ArgumentNullException(nameof(response));
        }

        public int Status => (int)_response.StatusCode;

        public string Reason => _response.ReasonPhrase ?? "";

        public int Version
        {
            get
            {
                var v = _response.Version;
                return v.Major * 10 + v.Minor;
            }
        }

        public Bytes Read()
        {
            if (_body == null)
            {
                byte[] raw = _response.Content == null
                    ? Array.Empty<byte>()
                    : _response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                _body = new Bytes(raw);
            }
            return _body.Value;
        }

        public Bytes Read(int amt)
        {
            var all = Read();
            if (amt >= all.Length)
                return all;
            return all.Slice(0, amt, null);
        }

        public string? Getheader(string name, string? default_ = null)
        {
            IEnumerable<string>? values;
            if (_response.Headers.TryGetValues(name, out values))
            {
                return string.Join(", ", values);
            }
            if (_response.Content?.Headers.TryGetValues(name, out values) == true)
            {
                return string.Join(", ", values);
            }
            return default_;
        }

        public List<(string, string)> Getheaders()
        {
            var result = new List<(string, string)>();
            foreach (var header in _response.Headers)
            {
                result.Append((header.Key, string.Join(", ", header.Value)));
            }
            if (_response.Content != null)
            {
                foreach (var header in _response.Content.Headers)
                {
                    result.Append((header.Key, string.Join(", ", header.Value)));
                }
            }
            return result;
        }

        public void Close()
        {
            Dispose();
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _response.Dispose();
            }
        }
    }
}
