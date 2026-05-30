using System;
using System.Collections.Generic;
using System.Text;

namespace Sharpy
{
    /// <summary>
    /// Implements Python's <c>urllib.parse</c> module for URL parsing, construction,
    /// quoting, and query-string handling.
    /// </summary>
    public static partial class UrllibModule
    {
        // Schemes that use a network location (//netloc/path).
        private static readonly HashSet<string> UsesNetloc = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "", "ftp", "http", "gopher", "nntp", "telnet", "imap", "wais",
            "file", "mms", "https", "shttp", "snews", "prospero", "rtsp",
            "rtsps", "rtspu", "rsync", "svn", "svn+ssh", "sftp", "nfs",
            "git", "git+ssh", "ws", "wss"
        };

        // Schemes that support the params component (;params).
        private static readonly HashSet<string> UsesParams = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "", "ftp", "hdl", "prospero", "http", "imap", "https", "shttp",
            "rtsp", "rtsps", "rtspu", "sip", "sips", "mms", "sftp", "tel"
        };

        // Characters that are never percent-encoded (unreserved per RFC 3986).
        private const string AlwaysUnreserved = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_.-~";

        // ─── URL Parsing ─────────────────────────────────────────────────────

        /// <summary>
        /// Parse a URL into six components: (scheme, netloc, path, params, query, fragment).
        /// </summary>
        /// <param name="url">URL string to parse.</param>
        /// <param name="scheme">Default scheme if none is present in the URL.</param>
        /// <param name="allowFragments">Whether to recognize fragment identifiers.</param>
        /// <returns>A <see cref="ParseResult"/> with the six components.</returns>
        public static ParseResult Urlparse(string url, string scheme = "", bool allowFragments = true)
        {
            if (url == null)
            {
                throw new TypeError("expected str, got NoneType");
            }

            SplitResult split = Urlsplit(url, scheme, allowFragments);

            string path = split.Path;
            string @params = "";

            // Extract params from path (only for schemes that use params)
            if (UsesParams.Contains(split.Scheme))
            {
                int semiIdx = path.LastIndexOf(';');
                if (semiIdx >= 0)
                {
                    @params = path.Substring(semiIdx + 1);
                    path = path.Substring(0, semiIdx);
                }
            }

            return new ParseResult(split.Scheme, split.Netloc, path, @params, split.Query, split.Fragment);
        }

        /// <summary>
        /// Parse a URL into five components: (scheme, netloc, path, query, fragment).
        /// Similar to <see cref="Urlparse"/> but does not split params from the path.
        /// </summary>
        /// <param name="url">URL string to parse.</param>
        /// <param name="scheme">Default scheme if none is present in the URL.</param>
        /// <param name="allowFragments">Whether to recognize fragment identifiers.</param>
        /// <returns>A <see cref="SplitResult"/> with the five components.</returns>
        public static SplitResult Urlsplit(string url, string scheme = "", bool allowFragments = true)
        {
            if (url == null)
            {
                throw new TypeError("expected str, got NoneType");
            }

            string netloc = "";
            string path = "";
            string query = "";
            string fragment = "";

            string rest = url;

            // Extract scheme
            int colonIdx = rest.IndexOf(':');
            if (colonIdx > 0)
            {
                string potentialScheme = rest.Substring(0, colonIdx);
                bool validScheme = true;
                for (int i = 0; i < potentialScheme.Length; i++)
                {
                    char c = potentialScheme[i];
                    if (i == 0)
                    {
                        if (!((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')))
                        {
                            validScheme = false;
                            break;
                        }
                    }
                    else
                    {
                        if (!((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') ||
                              (c >= '0' && c <= '9') || c == '+' || c == '-' || c == '.'))
                        {
                            validScheme = false;
                            break;
                        }
                    }
                }

                if (validScheme)
                {
                    scheme = potentialScheme.ToLowerInvariant();
                    rest = rest.Substring(colonIdx + 1);
                }
            }

            // Extract netloc (if scheme uses it)
            if (UsesNetloc.Contains(scheme) && rest.Length >= 2 && rest[0] == '/' && rest[1] == '/')
            {
                rest = rest.Substring(2);
                int pathStart = rest.IndexOf('/');
                int queryStart = rest.IndexOf('?');
                int fragStart = rest.IndexOf('#');

                // Find earliest delimiter
                int delimIdx = rest.Length;
                if (pathStart >= 0 && pathStart < delimIdx)
                {
                    delimIdx = pathStart;
                }

                if (queryStart >= 0 && queryStart < delimIdx)
                {
                    delimIdx = queryStart;
                }

                if (fragStart >= 0 && fragStart < delimIdx)
                {
                    delimIdx = fragStart;
                }

                netloc = rest.Substring(0, delimIdx);
                rest = rest.Substring(delimIdx);
            }

            // Extract fragment
            if (allowFragments)
            {
                int fragIdx = rest.IndexOf('#');
                if (fragIdx >= 0)
                {
                    fragment = rest.Substring(fragIdx + 1);
                    rest = rest.Substring(0, fragIdx);
                }
            }

            // Extract query
            int qIdx = rest.IndexOf('?');
            if (qIdx >= 0)
            {
                query = rest.Substring(qIdx + 1);
                rest = rest.Substring(0, qIdx);
            }

            path = rest;

            return new SplitResult(scheme, netloc, path, query, fragment);
        }

        // ─── URL Construction ────────────────────────────────────────────────

        /// <summary>
        /// Combine the six components of a <see cref="ParseResult"/> into a URL string.
        /// </summary>
        public static string Urlunparse(ParseResult components)
        {
            if (components == null)
            {
                throw new TypeError("expected ParseResult, got NoneType");
            }

            string path = components.Path;

            // Reattach params to path
            if (!string.IsNullOrEmpty(components.Params))
            {
                path = path + ";" + components.Params;
            }

            return Urlunsplit(new SplitResult(
                components.Scheme,
                components.Netloc,
                path,
                components.Query,
                components.Fragment));
        }

        /// <summary>
        /// Combine the five components of a <see cref="SplitResult"/> into a URL string.
        /// </summary>
        public static string Urlunsplit(SplitResult components)
        {
            if (components == null)
            {
                throw new TypeError("expected SplitResult, got NoneType");
            }

            var sb = new StringBuilder();
            string scheme = components.Scheme;
            string netloc = components.Netloc;
            string path = components.Path;
            string query = components.Query;
            string fragment = components.Fragment;

            if (!string.IsNullOrEmpty(scheme))
            {
                sb.Append(scheme);
                sb.Append(':');
            }

            if (!string.IsNullOrEmpty(netloc) || UsesNetloc.Contains(scheme))
            {
                if (!string.IsNullOrEmpty(netloc) || !string.IsNullOrEmpty(path))
                {
                    sb.Append("//");
                }

                sb.Append(netloc);

                // Ensure path starts with / when netloc is present
                if (!string.IsNullOrEmpty(path) && !path.StartsWith("/"))
                {
                    sb.Append('/');
                }
            }

            sb.Append(path);

            if (!string.IsNullOrEmpty(query))
            {
                sb.Append('?');
                sb.Append(query);
            }

            if (!string.IsNullOrEmpty(fragment))
            {
                sb.Append('#');
                sb.Append(fragment);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Construct a full URL by combining a base URL with a relative URL.
        /// </summary>
        /// <param name="baseUrl">The base URL.</param>
        /// <param name="url">The URL to join (may be relative).</param>
        /// <param name="allowFragments">Whether to recognize fragment identifiers.</param>
        /// <returns>The combined URL.</returns>
        public static string Urljoin(string baseUrl, string url, bool allowFragments = true)
        {
            if (baseUrl == null)
            {
                throw new TypeError("expected str, got NoneType");
            }

            if (url == null)
            {
                throw new TypeError("expected str, got NoneType");
            }

            if (string.IsNullOrEmpty(url))
            {
                return baseUrl;
            }

            SplitResult bSplit = Urlsplit(baseUrl, "", allowFragments);
            SplitResult rSplit = Urlsplit(url, bSplit.Scheme, allowFragments);

            // If the relative URL has a different scheme, return it as-is
            if (!string.Equals(rSplit.Scheme, bSplit.Scheme, StringComparison.OrdinalIgnoreCase))
            {
                return url;
            }

            if (UsesNetloc.Contains(rSplit.Scheme))
            {
                // If relative URL has netloc, use it directly
                if (!string.IsNullOrEmpty(rSplit.Netloc))
                {
                    string resolvedPath = RemoveDotSegments(rSplit.Path);
                    return Urlunsplit(new SplitResult(
                        rSplit.Scheme, rSplit.Netloc, resolvedPath, rSplit.Query, rSplit.Fragment));
                }

                // Use base netloc
                string netloc = bSplit.Netloc;

                if (string.IsNullOrEmpty(rSplit.Path) && string.IsNullOrEmpty(rSplit.Query))
                {
                    // Use base path and query, only override fragment
                    string basePath = bSplit.Path;
                    string baseQuery = bSplit.Query;
                    return Urlunsplit(new SplitResult(
                        bSplit.Scheme, netloc, basePath, baseQuery, rSplit.Fragment));
                }

                string path;
                if (rSplit.Path.StartsWith("/"))
                {
                    path = RemoveDotSegments(rSplit.Path);
                }
                else
                {
                    // Merge: use base path directory + relative path
                    string basePath = bSplit.Path;
                    int lastSlash = basePath.LastIndexOf('/');
                    string mergedPath;
                    if (!string.IsNullOrEmpty(bSplit.Netloc) && string.IsNullOrEmpty(basePath))
                    {
                        mergedPath = "/" + rSplit.Path;
                    }
                    else if (lastSlash >= 0)
                    {
                        mergedPath = basePath.Substring(0, lastSlash + 1) + rSplit.Path;
                    }
                    else
                    {
                        mergedPath = rSplit.Path;
                    }

                    path = RemoveDotSegments(mergedPath);
                }

                return Urlunsplit(new SplitResult(
                    bSplit.Scheme, netloc, path, rSplit.Query, rSplit.Fragment));
            }

            // For schemes that don't use netloc, resolve as-is
            return Urlunsplit(rSplit);
        }

        // ─── Query String Handling ───────────────────────────────────────────

        /// <summary>
        /// Parse a query string and return a dictionary of lists.
        /// Keys that appear multiple times have all values aggregated.
        /// </summary>
        /// <param name="qs">The query string to parse.</param>
        /// <param name="separator">The separator between key-value pairs.</param>
        /// <returns>A dictionary mapping keys to lists of values.</returns>
        public static Sharpy.Dict<string, Sharpy.List<string>> ParseQs(string qs, string separator = "&")
        {
            if (qs == null)
            {
                throw new TypeError("expected str, got NoneType");
            }

            var result = new Sharpy.Dict<string, Sharpy.List<string>>();
            Sharpy.List<System.ValueTuple<string, string>> pairs = ParseQsl(qs, separator);

            foreach (var pair in pairs)
            {
                string key = pair.Item1;
                string value = pair.Item2;

                if (result.ContainsKey(key))
                {
                    result[key].Append(value);
                }
                else
                {
                    var list = new Sharpy.List<string>();
                    list.Append(value);
                    result[key] = list;
                }
            }

            return result;
        }

        /// <summary>
        /// Parse a query string and return a list of (key, value) tuples.
        /// </summary>
        /// <param name="qs">The query string to parse.</param>
        /// <param name="separator">The separator between key-value pairs.</param>
        /// <returns>A list of (key, value) tuples.</returns>
        public static Sharpy.List<System.ValueTuple<string, string>> ParseQsl(string qs, string separator = "&")
        {
            if (qs == null)
            {
                throw new TypeError("expected str, got NoneType");
            }

            var result = new Sharpy.List<System.ValueTuple<string, string>>();

            if (string.IsNullOrEmpty(qs))
            {
                return result;
            }

            string[] pairs = qs.Split(new string[] { separator }, StringSplitOptions.None);
            foreach (string pair in pairs)
            {
                if (string.IsNullOrEmpty(pair))
                {
                    continue;
                }

                int eqIdx = pair.IndexOf('=');
                string key;
                string value;

                if (eqIdx >= 0)
                {
                    key = Unquote(pair.Substring(0, eqIdx).Replace('+', ' '));
                    value = Unquote(pair.Substring(eqIdx + 1).Replace('+', ' '));
                }
                else
                {
                    key = Unquote(pair.Replace('+', ' '));
                    value = "";
                }

                result.Append(new System.ValueTuple<string, string>(key, value));
            }

            return result;
        }

        /// <summary>
        /// Encode a dictionary of query parameters into a query string.
        /// </summary>
        /// <param name="query">Dictionary of key-value pairs.</param>
        /// <param name="doseq">If true, sequence values are encoded as separate key=value pairs.</param>
        /// <returns>A URL-encoded query string.</returns>
        public static string Urlencode(Sharpy.Dict<string, object?> query, bool doseq = false)
        {
            if (query == null)
            {
                throw new TypeError("expected dict, got NoneType");
            }

            var parts = new System.Collections.Generic.List<string>();

            foreach (string key in query.Keys())
            {
                object? val = query[key];

                if (doseq && val is Sharpy.List<string> listVal)
                {
                    foreach (string item in listVal)
                    {
                        parts.Add(QuotePlus(key) + "=" + QuotePlus(item));
                    }
                }
                else
                {
                    string valStr = val?.ToString() ?? "";
                    parts.Add(QuotePlus(key) + "=" + QuotePlus(valStr));
                }
            }

            return string.Join("&", parts);
        }

        /// <summary>
        /// Encode a list of (key, value) tuples into a query string.
        /// </summary>
        /// <param name="query">List of (key, value) tuples.</param>
        /// <returns>A URL-encoded query string.</returns>
        public static string Urlencode(Sharpy.List<System.ValueTuple<string, string>> query)
        {
            if (query == null)
            {
                throw new TypeError("expected list, got NoneType");
            }

            var parts = new System.Collections.Generic.List<string>();
            foreach (var pair in query)
            {
                parts.Add(QuotePlus(pair.Item1) + "=" + QuotePlus(pair.Item2));
            }

            return string.Join("&", parts);
        }

        // ─── Percent-Encoding ────────────────────────────────────────────────

        /// <summary>
        /// Percent-encode a string. Characters in <paramref name="safe"/> are not encoded.
        /// By default, <c>/</c> is considered safe.
        /// </summary>
        /// <param name="s">The string to encode.</param>
        /// <param name="safe">Characters that should not be encoded.</param>
        /// <returns>The percent-encoded string.</returns>
        public static string Quote(string s, string safe = "/")
        {
            if (s == null)
            {
                throw new TypeError("expected str, got NoneType");
            }

            var sb = new StringBuilder(s.Length * 2);
            byte[] bytes = Encoding.UTF8.GetBytes(s);

            foreach (byte b in bytes)
            {
                char c = (char)b;
                if (AlwaysUnreserved.IndexOf(c) >= 0 || (safe != null && safe.IndexOf(c) >= 0))
                {
                    sb.Append(c);
                }
                else
                {
                    sb.Append('%');
                    sb.Append(b.ToString("X2"));
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Like <see cref="Quote"/> but also replaces spaces with <c>+</c> signs.
        /// By default, no characters are considered safe.
        /// </summary>
        /// <param name="s">The string to encode.</param>
        /// <param name="safe">Characters that should not be encoded.</param>
        /// <returns>The percent-encoded string with spaces as +.</returns>
        public static string QuotePlus(string s, string safe = "")
        {
            if (s == null)
            {
                throw new TypeError("expected str, got NoneType");
            }

            // quote_plus replaces spaces with '+' and then percent-encodes the rest
            string quoted = Quote(s, " " + safe);
            return quoted.Replace(" ", "+");
        }

        /// <summary>
        /// Decode a percent-encoded string.
        /// </summary>
        /// <param name="s">The string to decode.</param>
        /// <returns>The decoded string.</returns>
        public static string Unquote(string s)
        {
            if (s == null)
            {
                throw new TypeError("expected str, got NoneType");
            }

            if (s.IndexOf('%') < 0)
            {
                return s;
            }

            var bytes = new System.Collections.Generic.List<byte>();
            int i = 0;
            while (i < s.Length)
            {
                if (s[i] == '%' && i + 2 < s.Length && IsHexDigit(s[i + 1]) && IsHexDigit(s[i + 2]))
                {
                    int hi = HexVal(s[i + 1]);
                    int lo = HexVal(s[i + 2]);
                    bytes.Add((byte)((hi << 4) | lo));
                    i += 3;
                }
                else
                {
                    // Encode non-percent characters as UTF-8
                    byte[] charBytes = Encoding.UTF8.GetBytes(new char[] { s[i] });
                    bytes.AddRange(charBytes);
                    i++;
                }
            }

            return Encoding.UTF8.GetString(bytes.ToArray());
        }

        /// <summary>
        /// Like <see cref="Unquote"/> but also replaces <c>+</c> signs with spaces.
        /// </summary>
        /// <param name="s">The string to decode.</param>
        /// <returns>The decoded string.</returns>
        public static string UnquotePlus(string s)
        {
            if (s == null)
            {
                throw new TypeError("expected str, got NoneType");
            }

            return Unquote(s.Replace('+', ' '));
        }

        // ─── Internal Helpers ────────────────────────────────────────────────

        /// <summary>
        /// Remove dot segments from a path per RFC 3986 section 5.2.4.
        /// </summary>
        private static string RemoveDotSegments(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return path;
            }

            var output = new System.Collections.Generic.List<string>();
            string input = path;

            while (input.Length > 0)
            {
                // A: If the input buffer begins with a prefix of "../" or "./"
                if (input.StartsWith("../"))
                {
                    input = input.Substring(3);
                }
                else if (input.StartsWith("./"))
                {
                    input = input.Substring(2);
                }
                // B: If the input buffer begins with a prefix of "/./" or "/."
                else if (input.StartsWith("/./"))
                {
                    input = "/" + input.Substring(3);
                }
                else if (input == "/.")
                {
                    input = "/";
                }
                // C: If the input buffer begins with a prefix of "/../" or "/.."
                else if (input.StartsWith("/../"))
                {
                    input = "/" + input.Substring(4);
                    if (output.Count > 0)
                    {
                        output.RemoveAt(output.Count - 1);
                    }
                }
                else if (input == "/..")
                {
                    input = "/";
                    if (output.Count > 0)
                    {
                        output.RemoveAt(output.Count - 1);
                    }
                }
                // D: If the input buffer consists only of "." or ".."
                else if (input == "." || input == "..")
                {
                    input = "";
                }
                // E: Move first path segment to output
                else
                {
                    int segEnd;
                    if (input[0] == '/')
                    {
                        segEnd = input.IndexOf('/', 1);
                        if (segEnd < 0)
                        {
                            segEnd = input.Length;
                        }
                    }
                    else
                    {
                        segEnd = input.IndexOf('/');
                        if (segEnd < 0)
                        {
                            segEnd = input.Length;
                        }
                    }

                    output.Add(input.Substring(0, segEnd));
                    input = input.Substring(segEnd);
                }
            }

            return string.Concat(output);
        }

        private static bool IsHexDigit(char c)
        {
            return (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
        }

        private static int HexVal(char c)
        {
            if (c >= '0' && c <= '9')
            {
                return c - '0';
            }

            if (c >= 'a' && c <= 'f')
            {
                return c - 'a' + 10;
            }

            if (c >= 'A' && c <= 'F')
            {
                return c - 'A' + 10;
            }

            return 0;
        }
    }
}
