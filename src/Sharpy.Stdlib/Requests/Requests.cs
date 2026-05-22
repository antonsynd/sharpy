using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sharpy
{
    /// <summary>
    /// Python-compatible requests module. Provides simple HTTP request functions.
    /// </summary>
    public static partial class Requests
    {
        private static readonly HttpClient _defaultClient = CreateDefaultClient();

        private static HttpClient CreateDefaultClient()
        {
            var client = new HttpClient();
            // No global timeout; per-request timeouts handled via CancellationTokenSource.
            client.Timeout = System.Threading.Timeout.InfiniteTimeSpan;
            return client;
        }

        /// <summary>Send a GET request.</summary>
        public static Result<Response, RequestException> Get(
            string url,
            Dict<string, string>? headers = null,
            Dict<string, string>? params_ = null,
            object? json = null,
            Dict<string, string>? data = null,
            double? timeout = null,
            (string, string)? auth = null,
            Dict<string, string>? files = null,
            bool stream = false,
            bool? allow_redirects = null,
            bool verify = true)
        {
            return Send(HttpMethod.Get, url, headers, params_, json, data, timeout, null, auth, files, stream, allow_redirects, verify);
        }

        /// <summary>Send a POST request.</summary>
        public static Result<Response, RequestException> Post(
            string url,
            Dict<string, string>? headers = null,
            Dict<string, string>? params_ = null,
            object? json = null,
            Dict<string, string>? data = null,
            double? timeout = null,
            (string, string)? auth = null,
            Dict<string, string>? files = null,
            bool stream = false,
            bool? allow_redirects = null,
            bool verify = true)
        {
            return Send(HttpMethod.Post, url, headers, params_, json, data, timeout, null, auth, files, stream, allow_redirects, verify);
        }

        /// <summary>Send a PUT request.</summary>
        public static Result<Response, RequestException> Put(
            string url,
            Dict<string, string>? headers = null,
            Dict<string, string>? params_ = null,
            object? json = null,
            Dict<string, string>? data = null,
            double? timeout = null,
            (string, string)? auth = null,
            Dict<string, string>? files = null,
            bool stream = false,
            bool? allow_redirects = null,
            bool verify = true)
        {
            return Send(HttpMethod.Put, url, headers, params_, json, data, timeout, null, auth, files, stream, allow_redirects, verify);
        }

        /// <summary>Send a DELETE request.</summary>
        public static Result<Response, RequestException> Delete(
            string url,
            Dict<string, string>? headers = null,
            Dict<string, string>? params_ = null,
            object? json = null,
            Dict<string, string>? data = null,
            double? timeout = null,
            (string, string)? auth = null,
            Dict<string, string>? files = null,
            bool stream = false,
            bool? allow_redirects = null,
            bool verify = true)
        {
            return Send(HttpMethod.Delete, url, headers, params_, json, data, timeout, null, auth, files, stream, allow_redirects, verify);
        }

        /// <summary>Send a PATCH request.</summary>
        public static Result<Response, RequestException> Patch(
            string url,
            Dict<string, string>? headers = null,
            Dict<string, string>? params_ = null,
            object? json = null,
            Dict<string, string>? data = null,
            double? timeout = null,
            (string, string)? auth = null,
            Dict<string, string>? files = null,
            bool stream = false,
            bool? allow_redirects = null,
            bool verify = true)
        {
#if NET10_0_OR_GREATER
            var patchMethod = HttpMethod.Patch;
#else
            var patchMethod = new HttpMethod("PATCH");
#endif
            return Send(patchMethod, url, headers, params_, json, data, timeout, null, auth, files, stream, allow_redirects, verify);
        }

        /// <summary>Send a HEAD request.</summary>
        public static Result<Response, RequestException> Head(
            string url,
            Dict<string, string>? headers = null,
            Dict<string, string>? params_ = null,
            object? json = null,
            Dict<string, string>? data = null,
            double? timeout = null,
            (string, string)? auth = null,
            Dict<string, string>? files = null,
            bool stream = false,
            bool? allow_redirects = null,
            bool verify = true)
        {
            return Send(HttpMethod.Head, url, headers, params_, json, data, timeout, null, auth, files, stream, allow_redirects, verify);
        }

        /// <summary>Send an OPTIONS request.</summary>
        public static Result<Response, RequestException> Options(
            string url,
            Dict<string, string>? headers = null,
            Dict<string, string>? params_ = null,
            object? json = null,
            Dict<string, string>? data = null,
            double? timeout = null,
            (string, string)? auth = null,
            Dict<string, string>? files = null,
            bool stream = false,
            bool? allow_redirects = null,
            bool verify = true)
        {
            return Send(HttpMethod.Options, url, headers, params_, json, data, timeout, null, auth, files, stream, allow_redirects, verify);
        }

        /// <summary>
        /// Internal helper that builds and dispatches an HTTP request. Used by both module-level
        /// functions and the Session class. The <paramref name="client"/> parameter lets Session
        /// inject its own configured HttpClient.
        /// </summary>
        /// <remarks>
        /// When <paramref name="allow_redirects"/> is explicitly <c>false</c> or
        /// <paramref name="verify"/> is <c>false</c> and no caller-supplied <paramref name="client"/>
        /// is provided, a one-off <see cref="HttpClient"/>/<see cref="HttpClientHandler"/> pair
        /// is constructed (because these settings live on the handler, not the request). This
        /// is a known performance trade-off: configure these knobs on a <see cref="Session"/>
        /// to amortize the cost across multiple requests.
        /// </remarks>
        internal static Result<Response, RequestException> Send(
            HttpMethod method,
            string url,
            Dict<string, string>? headers,
            Dict<string, string>? params_,
            object? json,
            Dict<string, string>? data,
            double? timeout,
            HttpClient? client,
            (string, string)? auth = null,
            Dict<string, string>? files = null,
            bool stream = false,
            bool? allow_redirects = null,
            bool verify = true)
        {
            if (url == null)
            {
                return Result<Response, RequestException>.Err(
                    new RequestException("url cannot be None"));
            }

            // Determine if we need a one-off client because the shared default cannot honor
            // these per-request configuration knobs (which live on the HttpClientHandler).
            bool needsCustomHandler = client == null
                && ((allow_redirects.HasValue && !allow_redirects.Value) || !verify);

            HttpClient effectiveClient;
            HttpClientHandler? ownedHandler = null;
            HttpClient? ownedClient = null;
            if (client != null)
            {
                effectiveClient = client;
            }
            else if (needsCustomHandler)
            {
                ownedHandler = new HttpClientHandler();
                if (allow_redirects.HasValue && !allow_redirects.Value)
                {
                    try
                    { ownedHandler.AllowAutoRedirect = false; }
                    catch (PlatformNotSupportedException) { }
                }
                if (!verify)
                {
                    try
                    {
#if NET10_0_OR_GREATER
                        ownedHandler.ServerCertificateCustomValidationCallback =
                            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
#else
                        ownedHandler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
#endif
                    }
                    catch (PlatformNotSupportedException) { }
                    catch (NotImplementedException) { }
                }
                ownedClient = new HttpClient(ownedHandler)
                {
                    Timeout = System.Threading.Timeout.InfiniteTimeSpan,
                };
                effectiveClient = ownedClient;
            }
            else
            {
                effectiveClient = _defaultClient;
            }

            string finalUrl;
            try
            {
                finalUrl = AppendQueryParams(url, params_);
            }
            catch (Exception ex)
            {
                ownedClient?.Dispose();
                ownedHandler?.Dispose();
                return Result<Response, RequestException>.Err(
                    new RequestException("invalid URL or query parameters: " + ex.Message, ex));
            }

            using var request = new HttpRequestMessage(method, finalUrl);
            {
                // Build the body. Precedence: files > json > data.
                // When files is provided, fall back to multipart/form-data with optional data fields.
                if (files is not null)
                {
                    var multipart = new MultipartFormDataContent();
                    if (data is not null)
                    {
                        foreach (var (key, value) in data.Items())
                        {
                            multipart.Add(new StringContent(value ?? string.Empty, Encoding.UTF8), key);
                        }
                    }
                    foreach (var (fieldName, filePath) in files.Items())
                    {
                        byte[] fileBytes;
                        try
                        {
                            fileBytes = System.IO.File.ReadAllBytes(filePath);
                        }
                        catch (Exception ex)
                        {
                            multipart.Dispose();
                            ownedClient?.Dispose();
                            ownedHandler?.Dispose();
                            return Result<Response, RequestException>.Err(
                                new RequestException("failed to read file '" + filePath + "': " + ex.Message, ex));
                        }
                        var fileContent = new ByteArrayContent(fileBytes);
                        var fileName = System.IO.Path.GetFileName(filePath);
                        multipart.Add(fileContent, fieldName, fileName);
                    }
                    request.Content = multipart;
                }
                else if (json != null)
                {
                    var jsonText = JsonSerializer.Serialize(json);
                    request.Content = new StringContent(jsonText, Encoding.UTF8, "application/json");
                }
                else if (data is not null)
                {
                    var formValues = new List<KeyValuePair<string, string>>();
                    foreach (var (key, value) in data.Items())
                    {
                        formValues.Add(new KeyValuePair<string, string>(key, value));
                    }
                    request.Content = new FormUrlEncodedContent(formValues);
                }

                // Apply headers — try the request headers first, fall back to content headers
                // (e.g., Content-Type overrides on a JSON/form body).
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

                // Apply Basic auth last so the explicit `auth` parameter wins over any
                // Authorization header set via the headers dict.
                if (auth.HasValue)
                {
                    var (username, password) = auth.Value;
                    var credentials = Convert.ToBase64String(
                        Encoding.UTF8.GetBytes((username ?? string.Empty) + ":" + (password ?? string.Empty)));
                    request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
                }

                CancellationTokenSource? cts = null;
                try
                {
                    CancellationToken token = CancellationToken.None;
                    if (timeout.HasValue)
                    {
                        cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeout.Value));
                        token = cts.Token;
                    }

                    var completion = stream
                        ? HttpCompletionOption.ResponseHeadersRead
                        : HttpCompletionOption.ResponseContentRead;

                    HttpResponseMessage httpResponse;
                    try
                    {
                        httpResponse = effectiveClient.SendAsync(request, completion, token).GetAwaiter().GetResult();
                    }
                    catch (HttpRequestException ex)
                    {
                        ownedClient?.Dispose();
                        ownedHandler?.Dispose();
                        return Result<Response, RequestException>.Err(
                            new ConnectionError("connection failed: " + ex.Message, ex));
                    }
                    catch (TaskCanceledException ex)
                    {
                        if (cts != null && cts.IsCancellationRequested)
                        {
                            ownedClient?.Dispose();
                            ownedHandler?.Dispose();
                            return Result<Response, RequestException>.Err(
                                new Timeout("request timed out after " + timeout!.Value + "s", ex));
                        }
                        ownedClient?.Dispose();
                        ownedHandler?.Dispose();
                        return Result<Response, RequestException>.Err(
                            new RequestException("request was canceled: " + ex.Message, ex));
                    }
                    catch (OperationCanceledException ex)
                    {
                        if (cts != null && cts.IsCancellationRequested)
                        {
                            ownedClient?.Dispose();
                            ownedHandler?.Dispose();
                            return Result<Response, RequestException>.Err(
                                new Timeout("request timed out after " + timeout!.Value + "s", ex));
                        }
                        ownedClient?.Dispose();
                        ownedHandler?.Dispose();
                        return Result<Response, RequestException>.Err(
                            new RequestException("request was canceled: " + ex.Message, ex));
                    }
                    catch (Exception ex)
                    {
                        ownedClient?.Dispose();
                        ownedHandler?.Dispose();
                        return Result<Response, RequestException>.Err(
                            new RequestException("request failed: " + ex.Message, ex));
                    }

                    // Response now owns ownedClient/ownedHandler (if any) and disposes them.
                    return Result<Response, RequestException>.Ok(
                        new Response(httpResponse, stream, ownedClient, ownedHandler));
                }
                finally
                {
                    cts?.Dispose();
                }
            }
        }

        private static string AppendQueryParams(string url, Dict<string, string>? params_)
        {
            if (params_ is null || params_.Count == 0)
            {
                return url;
            }

            var sb = new StringBuilder(url);
            // If url already has a query string, append with '&'; otherwise start with '?'.
            int queryStart = url.IndexOf('?');
            bool hasExisting = queryStart >= 0 && queryStart < url.Length - 1;
            bool first = !hasExisting;

            if (queryStart < 0)
            {
                sb.Append('?');
            }
            else if (!hasExisting)
            {
                // url ends with '?': no separator needed for first param.
                first = true;
            }

            foreach (var (key, value) in params_.Items())
            {
                if (first)
                {
                    first = false;
                }
                else
                {
                    sb.Append('&');
                }
                sb.Append(Uri.EscapeDataString(key));
                sb.Append('=');
                sb.Append(Uri.EscapeDataString(value));
            }

            return sb.ToString();
        }
    }
}
