using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sharpy
{
    /// <summary>
    /// Lower-level HTTP connection. Equivalent to Python's <c>http.client.HTTPConnection</c>.
    /// </summary>
    [SharpyModuleType("http", "HTTPConnection")]
    public class HTTPConnection : IDisposable
    {
        private readonly string _host;
        private readonly int _port;
        private readonly bool _useTls;
        private readonly double? _timeout;
        private HttpClient? _client;
        private HttpResponseMessage? _lastResponse;
        private bool _disposed;

        public HTTPConnection(string host, int? port = null, double? timeout = null)
            : this(host, port ?? 80, timeout, false)
        {
        }

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

        public string Host => _host;
        public int Port => _port;

        public void Request(string method, string url, string? body = null, Dict<string, string>? headers = null)
        {
            EnsureNotDisposed();

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
                case "GET":
                    httpMethod = HttpMethod.Get;
                    break;
                case "POST":
                    httpMethod = HttpMethod.Post;
                    break;
                case "PUT":
                    httpMethod = HttpMethod.Put;
                    break;
                case "DELETE":
                    httpMethod = HttpMethod.Delete;
                    break;
                case "HEAD":
                    httpMethod = HttpMethod.Head;
                    break;
                case "OPTIONS":
                    httpMethod = HttpMethod.Options;
                    break;
#if NET10_0_OR_GREATER
                case "PATCH":
                    httpMethod = HttpMethod.Patch;
                    break;
#else
                case "PATCH": httpMethod = new HttpMethod("PATCH"); break;
#endif
                default:
                    httpMethod = new HttpMethod(method);
                    break;
            }

            using var request = new HttpRequestMessage(httpMethod, fullUrl);

            if (body != null)
            {
                request.Content = new StringContent(body, Encoding.UTF8);
            }

            if (headers is not null)
            {
                foreach (var pair in headers.Items())
                {
                    if (!request.Headers.TryAddWithoutValidation(pair.Item1, pair.Item2))
                    {
                        request.Content?.Headers.Remove(pair.Item1);
                        request.Content?.Headers.TryAddWithoutValidation(pair.Item1, pair.Item2);
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

                _lastResponse?.Dispose();
                _lastResponse = null;

                try
                {
                    _lastResponse = GetOrCreateClient()
                        .SendAsync(request, HttpCompletionOption.ResponseContentRead, token)
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
                        throw new HTTPException("request timed out", ex);
                    }
                    throw new HTTPException("request was canceled: " + ex.Message, ex);
                }
                catch (UriFormatException ex)
                {
                    throw new InvalidURL(ex.Message);
                }
            }
            finally
            {
                cts?.Dispose();
            }
        }

        public HTTPResponse Getresponse()
        {
            if (_lastResponse == null)
            {
                throw new NotConnected("No response available");
            }
            var response = new HTTPResponse(_lastResponse);
            _lastResponse = null;
            return response;
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
                _lastResponse?.Dispose();
                _lastResponse = null;
                _client?.Dispose();
                _client = null;
            }
        }

        private HttpClient GetOrCreateClient()
        {
            if (_client == null)
            {
                _client = new HttpClient
                {
                    Timeout = System.Threading.Timeout.InfiniteTimeSpan,
                };
            }
            return _client;
        }

        private void EnsureNotDisposed()
        {
            if (_disposed)
            {
                throw new HTTPException("connection is closed");
            }
        }
    }

    /// <summary>
    /// HTTPS connection. Equivalent to Python's <c>http.client.HTTPSConnection</c>.
    /// </summary>
    [SharpyModuleType("http", "HTTPSConnection")]
    public sealed class HTTPSConnection : HTTPConnection
    {
        public HTTPSConnection(string host, int? port = null, double? timeout = null)
            : base(host, port ?? 443, timeout, true)
        {
        }
    }
}
