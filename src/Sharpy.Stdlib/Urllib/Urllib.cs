using System;
using System.Text;
using SysList = System.Collections.Generic.List<string>;
using SysByteList = System.Collections.Generic.List<byte>;
using SysHashSet = System.Collections.Generic.HashSet<string>;
using SysCharHashSet = System.Collections.Generic.HashSet<char>;
using SysKvp = System.Collections.Generic.KeyValuePair<string, object?>;
using SysPairList = System.Collections.Generic.List<(string, string)>;

namespace Sharpy
{
    /// <summary>
    /// Python-compatible urllib module.
    /// Provides URL parsing, encoding, and manipulation matching Python's urllib.parse API.
    /// </summary>
    public static partial class UrllibModule
    {
        private static readonly SysHashSet _usesNetloc = new SysHashSet(StringComparer.OrdinalIgnoreCase)
        {
            "ftp", "http", "gopher", "nntp", "telnet", "imap", "wais", "mms",
            "mssx", "svn", "svn+ssh", "sftp", "nfs", "git", "git+ssh", "ws", "wss",
            "thismessage", "tcp", "unknown", "userinfo", "https", "shttp",
            "snews", "prospero", "rtsp", "rtsps", "rtspu", "sip", "sips",
            "ventrilo", "vnc", "ssh"
        };

        /// <summary>
        /// Parse a URL into 6 components: scheme, netloc, path, params, query, fragment.
        /// </summary>
        /// <param name="url">The URL string to parse.</param>
        /// <param name="scheme">Default scheme to use if URL has none.</param>
        /// <param name="allowFragments">Whether to allow fragments (default: true).</param>
        /// <returns>A <see cref="ParseResult"/> with the 6 components.</returns>
        public static ParseResult Urlparse(string url, string scheme = "", bool allowFragments = true)
        {
            if (url == null)
            {
                throw new TypeError("expected str, got NoneType");
            }

            SplitResult split = Urlsplit(url, scheme, allowFragments);

            string path = split.Path;
            string @params = "";

            // Extract params (text after last ';' in path)
            int semiIndex = path.LastIndexOf(';');
            if (semiIndex >= 0)
            {
                @params = path.Substring(semiIndex + 1);
                path = path.Substring(0, semiIndex);
            }

            return new ParseResult(split.Scheme, split.Netloc, path, @params, split.Query, split.Fragment);
        }

        /// <summary>
        /// Parse a URL into 5 components: scheme, netloc, path, query, fragment.
        /// Similar to urlparse but does not split params from the path.
        /// </summary>
        /// <param name="url">The URL string to parse.</param>
        /// <param name="scheme">Default scheme to use if URL has none.</param>
        /// <param name="allowFragments">Whether to allow fragments (default: true).</param>
        /// <returns>A <see cref="SplitResult"/> with the 5 components.</returns>
        public static SplitResult Urlsplit(string url, string scheme = "", bool allowFragments = true)
        {
            if (url == null)
            {
                throw new TypeError("expected str, got NoneType");
            }

            string netloc = "";
            string query = "";
            string fragment = "";
            string path;

            // Extract scheme
            int colonIndex = url.IndexOf(':');
            if (colonIndex > 0)
            {
                string potentialScheme = url.Substring(0, colonIndex);
                bool validScheme = true;
                for (int i = 0; i < potentialScheme.Length; i++)
                {
                    char c = potentialScheme[i];
                    if (i == 0)
                    {
                        if (!char.IsLetter(c))
                        {
                            validScheme = false;
                            break;
                        }
                    }
                    else
                    {
                        if (!char.IsLetterOrDigit(c) && c != '+' && c != '-' && c != '.')
                        {
                            validScheme = false;
                            break;
                        }
                    }
                }

                if (validScheme)
                {
                    scheme = potentialScheme.ToLowerInvariant();
                    url = url.Substring(colonIndex + 1);
                }
            }

            // Extract netloc
            if (url.StartsWith("//", StringComparison.Ordinal))
            {
                url = url.Substring(2);
                int delimIndex = FindNetlocEnd(url);
                netloc = url.Substring(0, delimIndex);
                url = url.Substring(delimIndex);
            }

            // Extract fragment
            if (allowFragments)
            {
                int hashIndex = url.IndexOf('#');
                if (hashIndex >= 0)
                {
                    fragment = url.Substring(hashIndex + 1);
                    url = url.Substring(0, hashIndex);
                }
            }

            // Extract query
            int qIndex = url.IndexOf('?');
            if (qIndex >= 0)
            {
                query = url.Substring(qIndex + 1);
                url = url.Substring(0, qIndex);
            }

            path = url;

            return new SplitResult(scheme, netloc, path, query, fragment);
        }

        /// <summary>
        /// Combine the components of a ParseResult back into a URL string.
        /// </summary>
        public static string Urlunparse(ParseResult components)
        {
            if (components == null)
            {
                throw new TypeError("expected ParseResult, got NoneType");
            }

            string path = components.Path;
            if (!string.IsNullOrEmpty(components.Params))
            {
                path = path + ";" + components.Params;
            }

            return Urlunsplit(new SplitResult(components.Scheme, components.Netloc, path, components.Query, components.Fragment));
        }

        /// <summary>
        /// Combine the components of a SplitResult back into a URL string.
        /// </summary>
        public static string Urlunsplit(SplitResult components)
        {
            if (components == null)
            {
                throw new TypeError("expected SplitResult, got NoneType");
            }

            StringBuilder sb = new StringBuilder();

            if (!string.IsNullOrEmpty(components.Scheme))
            {
                sb.Append(components.Scheme);
                sb.Append(':');
            }

            if (!string.IsNullOrEmpty(components.Netloc))
            {
                sb.Append("//");
                sb.Append(components.Netloc);
            }
            else if (!string.IsNullOrEmpty(components.Scheme) &&
                     _usesNetloc.Contains(components.Scheme) &&
                     !components.Path.StartsWith("//", StringComparison.Ordinal))
            {
                sb.Append("//");
            }

            sb.Append(components.Path);

            if (!string.IsNullOrEmpty(components.Query))
            {
                sb.Append('?');
                sb.Append(components.Query);
            }

            if (!string.IsNullOrEmpty(components.Fragment))
            {
                sb.Append('#');
                sb.Append(components.Fragment);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Combine a base URL with a relative URL to form an absolute URL.
        /// </summary>
        /// <param name="baseUrl">The base URL.</param>
        /// <param name="url">The relative URL to resolve.</param>
        /// <param name="allowFragments">Whether to allow fragments (default: true).</param>
        /// <returns>The joined absolute URL.</returns>
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

            SplitResult bsplit = Urlsplit(baseUrl, "", allowFragments);
            SplitResult rsplit = Urlsplit(url, bsplit.Scheme, allowFragments);

            // If url has a different scheme, return it as-is
            if (rsplit.Scheme != bsplit.Scheme || !_usesNetloc.Contains(rsplit.Scheme))
            {
                return url;
            }

            // If url has netloc, use it directly (only inherit scheme)
            if (!string.IsNullOrEmpty(rsplit.Netloc))
            {
                string rpath = RemoveDotSegments(rsplit.Path);
                return Urlunsplit(new SplitResult(bsplit.Scheme, rsplit.Netloc, rpath, rsplit.Query, rsplit.Fragment));
            }

            string netloc = bsplit.Netloc;

            if (string.IsNullOrEmpty(rsplit.Path))
            {
                string path = bsplit.Path;
                string query = !string.IsNullOrEmpty(rsplit.Query) ? rsplit.Query : bsplit.Query;
                return Urlunsplit(new SplitResult(bsplit.Scheme, netloc, path, query, rsplit.Fragment));
            }

            if (rsplit.Path.StartsWith("/", StringComparison.Ordinal))
            {
                string resolvedPath = RemoveDotSegments(rsplit.Path);
                return Urlunsplit(new SplitResult(bsplit.Scheme, netloc, resolvedPath, rsplit.Query, rsplit.Fragment));
            }

            // Merge paths
            string basePath = bsplit.Path;
            int lastSlash = basePath.LastIndexOf('/');
            string merged;
            if (lastSlash >= 0)
            {
                merged = basePath.Substring(0, lastSlash + 1) + rsplit.Path;
            }
            else
            {
                merged = rsplit.Path;
            }

            merged = RemoveDotSegments(merged);
            return Urlunsplit(new SplitResult(bsplit.Scheme, netloc, merged, rsplit.Query, rsplit.Fragment));
        }

        /// <summary>
        /// Parse a query string into a dictionary mapping parameter names to lists of values.
        /// </summary>
        /// <param name="qs">The query string to parse.</param>
        /// <param name="separator">The separator character (default: '&amp;').</param>
        /// <returns>A Dict mapping each key to a List of values.</returns>
        public static Dict<string, List<string>> ParseQs(string qs, string separator = "&")
        {
            if (qs == null)
            {
                throw new TypeError("expected str, got NoneType");
            }

            Dict<string, List<string>> result = new Dict<string, List<string>>();
            SysPairList pairs = ParseQslInternal(qs, separator);

            foreach ((string key, string value) in pairs)
            {
                if (!result.ContainsKey(key))
                {
                    result[key] = new List<string>();
                }

                result[key].Add(value);
            }

            return result;
        }

        /// <summary>
        /// Parse a query string into a list of (name, value) pairs.
        /// </summary>
        /// <param name="qs">The query string to parse.</param>
        /// <param name="separator">The separator character (default: '&amp;').</param>
        /// <returns>A List of (key, value) tuples.</returns>
        public static List<(string, string)> ParseQsl(string qs, string separator = "&")
        {
            if (qs == null)
            {
                throw new TypeError("expected str, got NoneType");
            }

            SysPairList internal_result = ParseQslInternal(qs, separator);
            List<(string, string)> result = new List<(string, string)>();
            foreach ((string key, string value) in internal_result)
            {
                result.Add((key, value));
            }

            return result;
        }

        /// <summary>
        /// Encode a dictionary or list of pairs as a URL query string.
        /// </summary>
        /// <param name="query">A Dict&lt;string, string&gt; to encode.</param>
        /// <param name="doseq">If true, values that are lists are encoded as separate key=value pairs.</param>
        /// <returns>The URL-encoded query string.</returns>
        public static string Urlencode(Dict<string, object?> query, bool doseq = false)
        {
            if (query == null)
            {
                throw new TypeError("expected dict, got NoneType");
            }

            SysPairList pairs = new SysPairList();

            foreach (string key in query.Keys())
            {
                object? val = query[key];
                if (doseq && val is System.Collections.IEnumerable enumerable && !(val is string))
                {
                    foreach (object? item in enumerable)
                    {
                        pairs.Add((key, item?.ToString() ?? ""));
                    }
                }
                else
                {
                    pairs.Add((key, val?.ToString() ?? ""));
                }
            }

            return UrlencodePairs(pairs);
        }

        /// <summary>
        /// Encode a list of (key, value) pairs as a URL query string.
        /// </summary>
        /// <param name="query">A list of (key, value) tuples.</param>
        /// <returns>The URL-encoded query string.</returns>
        public static string Urlencode(List<(string, string)> query)
        {
            if (query == null)
            {
                throw new TypeError("expected list, got NoneType");
            }

            SysPairList pairs = new SysPairList();
            foreach ((string key, string value) in query)
            {
                pairs.Add((key, value));
            }

            return UrlencodePairs(pairs);
        }

        /// <summary>
        /// Percent-encode a string, replacing special characters with %xx escapes.
        /// Similar to Python's urllib.parse.quote().
        /// </summary>
        /// <param name="s">The string to encode.</param>
        /// <param name="safe">Characters that should not be encoded (default: "/").</param>
        /// <returns>The percent-encoded string.</returns>
        public static string Quote(string s, string safe = "/")
        {
            if (s == null)
            {
                throw new TypeError("expected str, got NoneType");
            }

            StringBuilder sb = new StringBuilder();
            SysCharHashSet safeChars = new SysCharHashSet();
            if (safe != null)
            {
                foreach (char c in safe)
                {
                    safeChars.Add(c);
                }
            }

            byte[] bytes = Encoding.UTF8.GetBytes(s);
            foreach (byte b in bytes)
            {
                char c = (char)b;
                if (IsUnreserved(c) || safeChars.Contains(c))
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
        /// Percent-encode a string, similar to Quote but also replaces spaces with '+'.
        /// Similar to Python's urllib.parse.quote_plus().
        /// </summary>
        /// <param name="s">The string to encode.</param>
        /// <param name="safe">Characters that should not be encoded (default: "").</param>
        /// <returns>The percent-encoded string with '+' for spaces.</returns>
        public static string QuotePlus(string s, string safe = "")
        {
            if (s == null)
            {
                throw new TypeError("expected str, got NoneType");
            }

            string quoted = Quote(s, safe + " ");
            return quoted.Replace(" ", "+");
        }

        /// <summary>
        /// Decode a percent-encoded string.
        /// Similar to Python's urllib.parse.unquote().
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

            SysByteList bytes = new SysByteList();
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '%' && i + 2 < s.Length && IsHexChar(s[i + 1]) && IsHexChar(s[i + 2]))
                {
                    int hi = HexVal(s[i + 1]);
                    int lo = HexVal(s[i + 2]);
                    bytes.Add((byte)((hi << 4) | lo));
                    i += 2;
                }
                else
                {
                    // For non-percent chars, encode them back to UTF-8 bytes
                    byte[] charBytes = Encoding.UTF8.GetBytes(new char[] { s[i] });
                    for (int j = 0; j < charBytes.Length; j++)
                    {
                        bytes.Add(charBytes[j]);
                    }
                }
            }

            return Encoding.UTF8.GetString(bytes.ToArray());
        }

        /// <summary>
        /// Decode a percent-encoded string, also converting '+' back to spaces.
        /// Similar to Python's urllib.parse.unquote_plus().
        /// </summary>
        /// <param name="s">The string to decode.</param>
        /// <returns>The decoded string.</returns>
        public static string UnquotePlus(string s)
        {
            if (s == null)
            {
                throw new TypeError("expected str, got NoneType");
            }

            return Unquote(s.Replace("+", " "));
        }

        // ==================== Private Helpers ====================

        private static int FindNetlocEnd(string url)
        {
            int end = url.Length;
            for (int i = 0; i < url.Length; i++)
            {
                char c = url[i];
                if (c == '/' || c == '?' || c == '#')
                {
                    end = i;
                    break;
                }
            }

            return end;
        }

        private static string RemoveDotSegments(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return path;
            }

            SysList output = new SysList();
            string[] segments = path.Split('/');

            for (int i = 0; i < segments.Length; i++)
            {
                string seg = segments[i];
                if (seg == ".")
                {
                    if (i == segments.Length - 1)
                    {
                        output.Add("");
                    }
                }
                else if (seg == "..")
                {
                    if (output.Count > 0)
                    {
                        output.RemoveAt(output.Count - 1);
                    }

                    if (i == segments.Length - 1)
                    {
                        output.Add("");
                    }
                }
                else
                {
                    output.Add(seg);
                }
            }

            string result = string.Join("/", output);

            // Preserve leading slash
            if (path.StartsWith("/", StringComparison.Ordinal) && !result.StartsWith("/", StringComparison.Ordinal))
            {
                result = "/" + result;
            }

            return result;
        }

        private static SysPairList ParseQslInternal(string qs, string separator)
        {
            SysPairList result = new SysPairList();

            if (string.IsNullOrEmpty(qs))
            {
                return result;
            }

            // Remove leading '?' if present
            if (qs.Length > 0 && qs[0] == '?')
            {
                qs = qs.Substring(1);
            }

            string[] pairs = qs.Split(new string[] { separator }, StringSplitOptions.None);
            foreach (string pair in pairs)
            {
                if (string.IsNullOrEmpty(pair))
                {
                    continue;
                }

                int eqIndex = pair.IndexOf('=');
                string key;
                string value;
                if (eqIndex >= 0)
                {
                    key = UnquotePlus(pair.Substring(0, eqIndex));
                    value = UnquotePlus(pair.Substring(eqIndex + 1));
                }
                else
                {
                    key = UnquotePlus(pair);
                    value = "";
                }

                result.Add((key, value));
            }

            return result;
        }

        private static string UrlencodePairs(SysPairList pairs)
        {
            StringBuilder sb = new StringBuilder();
            bool first = true;

            foreach ((string key, string value) in pairs)
            {
                if (!first)
                {
                    sb.Append('&');
                }

                first = false;
                sb.Append(QuotePlus(key));
                sb.Append('=');
                sb.Append(QuotePlus(value));
            }

            return sb.ToString();
        }

        private static bool IsUnreserved(char c)
        {
            return (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') ||
                   (c >= '0' && c <= '9') || c == '-' || c == '_' || c == '.' || c == '~';
        }

        private static bool IsHexChar(char c)
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

            return c - 'A' + 10;
        }
    }
}
