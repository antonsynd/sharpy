using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sharpy
{
    /// <summary>
    /// Lower-level HTTP connection class. Matches Python's <c>http.client.HTTPConnection</c>.
    /// Backed by <see cref="System.Net.Http.HttpClient"/>.
    /// </summary>
    [SharpyModuleType("http")]
    public class HTTPConnection : IDisposable
    {
        private readonly string _host;
        private readonly int _port;
        private readonly bool _useTls;
        private readonly double? _timeout;
        private HttpClient? _client;
        private HttpClientHandler? _handler;
        private HttpResponseMessage? _lastResponse;
        private bool _disposed;

        /// <summary>
        /// Create a new HTTP connection (unencrypted).
        /// </summary>
        /// <param name="host">The hostname to connect to.</param>
        /// <param name="port">The port number (default 80).</param>
        /// <param name="timeout">Optional timeout in seconds for requests.</param>
        public HTTPConnection(string host, int port = 80, double? timeout = null)
            : this(host, port, timeout, useTls: false)
        {
        }

        /// <summary>Internal constructor shared by HTTPConnection and HTTPSConnection.</summary>
        protected HTTPConnection(string host, int port, double? timeout, bool useTls)
        {
            if (string.IsNullOrEmpty(host))
            {
                throw new InvalidURL("host cannot be empty");
            }
            _host = host;
            _port = port;
            _timeout = timeout;
            _useTls = useTls;
        }

        /// <summary>The host this connection targets.</summary>
        public string host => _host;

        /// <summary>The port this connection targets.</summary>
        public int port => _port;

        /// <summary>The timeout in seconds, or null if no timeout.</summary>
        public double? timeout => _timeout;

        /// <summary>
        /// Send a request to the server.
        /// Equivalent to Python's <c>HTTPConnection.request(method, url, body, headers)</c>.
        /// </summary>
        /// <param name="method">HTTP method (e.g. "GET", "POST").</param>
        /// <param name="url">The path/resource on the server (e.g. "/path").</param>
        /// <param name="body">Optional request body as string.</param>
        /// <param name="headers">Optional request headers.</param>
        public void request(string method, string url, string? body = null, Dict<string, string>? headers = null)
        {
            EnsureClient();

            var scheme = _useTls ? "https" : "http";
            string fullUrl;
            if (_port == (_useTls ? 443 : 80))
            {
                fullUrl = scheme + "://" + _host + url;
            }
            else
            {
                fullUrl = scheme + "://" + _host + ":" + _port + url;
            }

            HttpMethod httpMethod;
            switch (method.ToUpperInvariant())
            {
                case "GET": httpMethod = HttpMethod.Get; break;
                case "POST": httpMethod = HttpMethod.Post; break;
                case "PUT": httpMethod = HttpMethod.Put; break;
                case "DELETE": httpMethod = HttpMethod.Delete; break;
                case "HEAD": httpMethod = HttpMethod.Head; break;
                case "OPTIONS": httpMethod = HttpMethod.Options; break;
#if NET10_0_OR_GREATER
                case "PATCH": httpMethod = HttpMethod.Patch; break;
#else
                case "PATCH": httpMethod = new HttpMethod("PATCH"); break;
#endif
                default: httpMethod = new HttpMethod(method); break;
            }

            using var request = new HttpRequestMessage(httpMethod, fullUrl);

            if (body != null)
            {
                request.Content = new StringContent(body, Encoding.UTF8);
            }

            if (headers is not null)
            {
                foreach (var (key, value) in headers.Items())
                {
                    if (!request.Headers.TryAddWithoutValidation(key, value))
                    {
                        if (request.Content != null)
                        {
                            request.Content.Headers.Remove(key);
                            request.Content.Headers.TryAddWithoutValidation(key, value);
                        }
                    }
                }
            }

            CancellationTokenSource? cts = null;
            try
            {
                CancellationToken token = CancellationToken.None;
                if (_timeout.HasValue)
                {
                    cts = new CancellationTokenSource(TimeSpan.FromSeconds(_timeout.Value));
                    token = cts.Token;
                }

                // Dispose previous response
                _lastResponse?.Dispose();
                _lastResponse = null;

                try
                {
                    _lastResponse = _client!.SendAsync(request, HttpCompletionOption.ResponseContentRead, token)
                        .GetAwaiter().GetResult();
                }
                catch (HttpRequestException ex)
                {
                    throw new HTTPException("connection failed: " + ex.Message, ex);
                }
                catch (TaskCanceledException ex)
                {
                    if (cts != null && cts.IsCancellationRequested)
                    {
                        throw new HTTPException("request timed out after " + _timeout!.Value + "s", ex);
                    }
                    throw new HTTPException("request was canceled: " + ex.Message, ex);
                }
                catch (OperationCanceledException ex)
                {
                    if (cts != null && cts.IsCancellationRequested)
                    {
                        throw new HTTPException("request timed out after " + _timeout!.Value + "s", ex);
                    }
                    throw new HTTPException("request was canceled: " + ex.Message, ex);
                }
            }
            finally
            {
                cts?.Dispose();
            }
        }

        /// <summary>
        /// Get the response from the last request.
        /// Equivalent to Python's <c>HTTPConnection.getresponse()</c>.
        /// </summary>
        /// <returns>An <see cref="HTTPResponse"/> wrapping the response.</returns>
        public HTTPResponse getresponse()
        {
            if (_lastResponse == null)
            {
                throw new HTTPException("no request has been sent — call request() first");
            }

            var response = new HTTPResponse(_lastResponse);
            _lastResponse = null; // Ownership transferred to HTTPResponse
            return response;
        }

        /// <summary>
        /// Close the connection and release resources.
        /// Equivalent to Python's <c>HTTPConnection.close()</c>.
        /// </summary>
        public void close()
        {
            Dispose();
        }

        /// <summary>Releases managed resources.</summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _lastResponse?.Dispose();
                _lastResponse = null;
                _client?.Dispose();
                _client = null;
                _handler?.Dispose();
                _handler = null;
            }
        }

        private void EnsureClient()
        {
            if (_disposed)
            {
                throw new HTTPException("connection is closed");
            }
            if (_client == null)
            {
                _handler = new HttpClientHandler();
                if (!_useTls)
                {
                    // For plain HTTP, no special TLS config needed
                }
                _client = new HttpClient(_handler)
                {
                    Timeout = System.Threading.Timeout.InfiniteTimeSpan,
                };
            }
        }
    }

    /// <summary>
    /// HTTPS connection class. Matches Python's <c>http.client.HTTPSConnection</c>.
    /// Same as <see cref="HTTPConnection"/> but uses TLS.
    /// </summary>
    [SharpyModuleType("http")]
    public class HTTPSConnection : HTTPConnection
    {
        /// <summary>
        /// Create a new HTTPS connection (TLS-encrypted).
        /// </summary>
        /// <param name="host">The hostname to connect to.</param>
        /// <param name="port">The port number (default 443).</param>
        /// <param name="timeout">Optional timeout in seconds for requests.</param>
        public HTTPSConnection(string host, int port = 443, double? timeout = null)
            : base(host, port, timeout, useTls: true)
        {
        }
    }
}
