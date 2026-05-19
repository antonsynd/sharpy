using System;
using System.Net;
using System.Net.Http;
using System.Text;

namespace Sharpy
{
    /// <summary>
    /// A session for sending multiple HTTP requests with shared configuration
    /// (default headers, cookies, authentication). Equivalent to Python's
    /// <c>requests.Session</c>.
    /// </summary>
    /// <remarks>
    /// Wraps a single <see cref="HttpClientHandler"/>/<see cref="HttpClient"/> pair
    /// so connections and a <see cref="CookieContainer"/> are reused across requests.
    /// Always dispose with <c>using</c> (or call <see cref="Dispose"/>) to release the
    /// underlying client.
    /// </remarks>
    [SharpyModuleType("requests")]
    public sealed class Session : IDisposable
    {
        private readonly HttpClientHandler _handler;
        private readonly HttpClient _client;
        private Dict<string, string> _headers;
        private Dict<string, string> _cookies;
        private Dict<string, string>? _proxies;
        private bool _allowRedirects = true;
        private int _maxRedirects = 30;
        private bool _verify = true;
        private bool _disposed;

        /// <summary>Create a new HTTP session.</summary>
        public Session()
        {
            _handler = new HttpClientHandler();
            try
            {
                _handler.UseCookies = true;
                _handler.CookieContainer = new CookieContainer();
            }
            catch (PlatformNotSupportedException)
            {
                // Some platforms (e.g., Blazor WebAssembly) don't expose these.
                // Cookies will then only flow through the user-managed Cookies dict.
            }
            _client = new HttpClient(_handler)
            {
                Timeout = System.Threading.Timeout.InfiniteTimeSpan,
            };
            _headers = new Dict<string, string>();
            _cookies = new Dict<string, string>();
        }

        /// <summary>
        /// Persistent default headers sent with every request from this session.
        /// Per-request headers override these on conflict.
        /// </summary>
        public Dict<string, string> Headers
        {
            get => _headers;
            set => _headers = value ?? new Dict<string, string>();
        }

        /// <summary>
        /// User-managed cookies sent with every request from this session.
        /// Equivalent to Python's <c>session.cookies</c> for simple cases.
        /// </summary>
        /// <remarks>
        /// These are merged into the <c>Cookie</c> header on each outgoing request.
        /// The underlying <see cref="CookieContainer"/> additionally manages cookies
        /// received from server responses automatically.
        /// </remarks>
        public Dict<string, string> Cookies
        {
            get => _cookies;
            set => _cookies = value ?? new Dict<string, string>();
        }

        /// <summary>
        /// Default Basic auth credentials (username, password) sent with every
        /// request from this session. Per-request <c>auth</c> takes precedence,
        /// and an explicit <c>Authorization</c> header (per-request or session-level)
        /// also suppresses this default.
        /// </summary>
        public (string, string)? Auth { get; set; }

        /// <summary>
        /// Whether the session should follow HTTP redirects (3xx responses).
        /// Defaults to <c>true</c>. Setting this configures the underlying
        /// <see cref="HttpClientHandler.AllowAutoRedirect"/>.
        /// </summary>
        public bool AllowRedirects
        {
            get => _allowRedirects;
            set
            {
                _allowRedirects = value;
                try
                { _handler.AllowAutoRedirect = value; }
                catch (PlatformNotSupportedException) { }
                catch (InvalidOperationException) { }
            }
        }

        /// <summary>
        /// Maximum number of automatic redirects to follow. Defaults to 30.
        /// Setting this configures <see cref="HttpClientHandler.MaxAutomaticRedirections"/>.
        /// </summary>
        public int MaxRedirects
        {
            get => _maxRedirects;
            set
            {
                if (value < 1)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "max_redirects must be >= 1");
                }
                _maxRedirects = value;
                try
                { _handler.MaxAutomaticRedirections = value; }
                catch (PlatformNotSupportedException) { }
                catch (InvalidOperationException) { }
            }
        }

        /// <summary>
        /// Proxy configuration. Maps URI scheme (e.g., <c>"http"</c>, <c>"https"</c>) to a
        /// proxy URL. Setting this configures <see cref="HttpClientHandler.Proxy"/> with the
        /// HTTPS proxy if present, otherwise the HTTP proxy.
        /// Equivalent to Python's <c>session.proxies</c>.
        /// </summary>
        public Dict<string, string>? Proxies
        {
            get => _proxies;
            set
            {
                _proxies = value;
                ApplyProxies();
            }
        }

        /// <summary>
        /// Whether to verify TLS server certificates. Defaults to <c>true</c>.
        /// Setting this to <c>false</c> disables certificate validation (DANGEROUS — only
        /// use for testing).
        /// </summary>
        public bool Verify
        {
            get => _verify;
            set
            {
                _verify = value;
                try
                {
                    if (value)
                    {
                        _handler.ServerCertificateCustomValidationCallback = null;
                    }
                    else
                    {
#if NET10_0_OR_GREATER
                        _handler.ServerCertificateCustomValidationCallback =
                            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
#else
                        _handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
#endif
                    }
                }
                catch (PlatformNotSupportedException) { }
                catch (NotImplementedException) { }
                catch (InvalidOperationException) { }
            }
        }

        private void ApplyProxies()
        {
            if (_proxies == null || _proxies.Count == 0)
            {
                try
                {
                    _handler.UseProxy = false;
                    _handler.Proxy = null;
                }
                catch (PlatformNotSupportedException) { }
                catch (InvalidOperationException) { }
                return;
            }

            string? proxyUrl = null;
            foreach (var (scheme, value) in _proxies.Items())
            {
                if (string.Equals(scheme, "https", StringComparison.OrdinalIgnoreCase))
                {
                    proxyUrl = value;
                    break;
                }
                if (proxyUrl == null && string.Equals(scheme, "http", StringComparison.OrdinalIgnoreCase))
                {
                    proxyUrl = value;
                }
            }

            if (proxyUrl != null)
            {
                try
                {
                    _handler.UseProxy = true;
                    _handler.Proxy = new WebProxy(proxyUrl);
                }
                catch (PlatformNotSupportedException) { }
                catch (InvalidOperationException) { }
                catch (UriFormatException) { }
            }
        }

        /// <summary>Send a GET request using this session.</summary>
        public Result<Response, RequestException> Get(
            string url,
            Dict<string, string>? headers = null,
            Dict<string, string>? params_ = null,
            object? json = null,
            Dict<string, string>? data = null,
            double? timeout = null,
            (string, string)? auth = null,
            Dict<string, string>? files = null,
            bool stream = false)
        {
            return Dispatch(HttpMethod.Get, url, headers, params_, json, data, timeout, auth, files, stream);
        }

        /// <summary>Send a POST request using this session.</summary>
        public Result<Response, RequestException> Post(
            string url,
            Dict<string, string>? headers = null,
            Dict<string, string>? params_ = null,
            object? json = null,
            Dict<string, string>? data = null,
            double? timeout = null,
            (string, string)? auth = null,
            Dict<string, string>? files = null,
            bool stream = false)
        {
            return Dispatch(HttpMethod.Post, url, headers, params_, json, data, timeout, auth, files, stream);
        }

        /// <summary>Send a PUT request using this session.</summary>
        public Result<Response, RequestException> Put(
            string url,
            Dict<string, string>? headers = null,
            Dict<string, string>? params_ = null,
            object? json = null,
            Dict<string, string>? data = null,
            double? timeout = null,
            (string, string)? auth = null,
            Dict<string, string>? files = null,
            bool stream = false)
        {
            return Dispatch(HttpMethod.Put, url, headers, params_, json, data, timeout, auth, files, stream);
        }

        /// <summary>Send a DELETE request using this session.</summary>
        public Result<Response, RequestException> Delete(
            string url,
            Dict<string, string>? headers = null,
            Dict<string, string>? params_ = null,
            object? json = null,
            Dict<string, string>? data = null,
            double? timeout = null,
            (string, string)? auth = null,
            Dict<string, string>? files = null,
            bool stream = false)
        {
            return Dispatch(HttpMethod.Delete, url, headers, params_, json, data, timeout, auth, files, stream);
        }

        /// <summary>Send a PATCH request using this session.</summary>
        public Result<Response, RequestException> Patch(
            string url,
            Dict<string, string>? headers = null,
            Dict<string, string>? params_ = null,
            object? json = null,
            Dict<string, string>? data = null,
            double? timeout = null,
            (string, string)? auth = null,
            Dict<string, string>? files = null,
            bool stream = false)
        {
#if NET10_0_OR_GREATER
            var patchMethod = HttpMethod.Patch;
#else
            var patchMethod = new HttpMethod("PATCH");
#endif
            return Dispatch(patchMethod, url, headers, params_, json, data, timeout, auth, files, stream);
        }

        /// <summary>Send a HEAD request using this session.</summary>
        public Result<Response, RequestException> Head(
            string url,
            Dict<string, string>? headers = null,
            Dict<string, string>? params_ = null,
            object? json = null,
            Dict<string, string>? data = null,
            double? timeout = null,
            (string, string)? auth = null,
            Dict<string, string>? files = null,
            bool stream = false)
        {
            return Dispatch(HttpMethod.Head, url, headers, params_, json, data, timeout, auth, files, stream);
        }

        /// <summary>Send an OPTIONS request using this session.</summary>
        public Result<Response, RequestException> Options(
            string url,
            Dict<string, string>? headers = null,
            Dict<string, string>? params_ = null,
            object? json = null,
            Dict<string, string>? data = null,
            double? timeout = null,
            (string, string)? auth = null,
            Dict<string, string>? files = null,
            bool stream = false)
        {
            return Dispatch(HttpMethod.Options, url, headers, params_, json, data, timeout, auth, files, stream);
        }

        /// <summary>Releases the underlying <see cref="HttpClient"/> and handler.</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            _client.Dispose();
            _handler.Dispose();
        }

        private Result<Response, RequestException> Dispatch(
            HttpMethod method,
            string url,
            Dict<string, string>? requestHeaders,
            Dict<string, string>? params_,
            object? json,
            Dict<string, string>? data,
            double? timeout,
            (string, string)? requestAuth,
            Dict<string, string>? files,
            bool stream)
        {
            if (_disposed)
            {
                return Result<Response, RequestException>.Err(
                    new RequestException("Session has been disposed"));
            }

            var mergedHeaders = MergeHeaders(requestHeaders);
            var effectiveAuth = ResolveAuth(requestAuth, mergedHeaders);
            return Requests.Send(
                method, url, mergedHeaders, params_, json, data, timeout,
                _client, effectiveAuth, files, stream,
                allow_redirects: null, verify: true);
        }

        /// <summary>
        /// Merge session headers with per-request headers (per-request wins on conflict)
        /// and fold in any user-managed cookies as a <c>Cookie</c> header if none is set.
        /// </summary>
        private Dict<string, string> MergeHeaders(Dict<string, string>? requestHeaders)
        {
            var merged = new Dict<string, string>();
            foreach (var (key, value) in _headers.Items())
            {
                merged[key] = value;
            }
            if (requestHeaders is not null)
            {
                foreach (var (key, value) in requestHeaders.Items())
                {
                    merged[key] = value;
                }
            }

            if (_cookies.Count > 0 && !HasHeaderIgnoreCase(merged, "Cookie"))
            {
                var sb = new StringBuilder();
                bool first = true;
                foreach (var (name, value) in _cookies.Items())
                {
                    if (!first)
                    {
                        sb.Append("; ");
                    }
                    sb.Append(name);
                    sb.Append('=');
                    sb.Append(value);
                    first = false;
                }
                merged["Cookie"] = sb.ToString();
            }

            return merged;
        }

        /// <summary>
        /// Per-request auth always wins. If absent, fall back to session-level Auth only
        /// when no Authorization header is already present in the merged headers.
        /// </summary>
        private (string, string)? ResolveAuth(
            (string, string)? requestAuth,
            Dict<string, string> mergedHeaders)
        {
            if (requestAuth.HasValue)
            {
                return requestAuth;
            }
            if (Auth.HasValue && !HasHeaderIgnoreCase(mergedHeaders, "Authorization"))
            {
                return Auth;
            }
            return null;
        }

        private static bool HasHeaderIgnoreCase(Dict<string, string> headers, string name)
        {
            foreach (var (key, _) in headers.Items())
            {
                if (string.Equals(key, name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
