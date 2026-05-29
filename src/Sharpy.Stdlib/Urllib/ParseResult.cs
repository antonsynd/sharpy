using System;

namespace Sharpy
{
    /// <summary>
    /// Result of urlparse(). A named tuple containing scheme, netloc, path, params, query, fragment.
    /// Mirrors Python's urllib.parse.ParseResult.
    /// </summary>
    [SharpyModuleType("urllib")]
    public sealed class ParseResult
    {
        /// <summary>URL scheme (e.g., "https").</summary>
        public string Scheme { get; }

        /// <summary>Network location (e.g., "example.com:8080").</summary>
        public string Netloc { get; }

        /// <summary>Hierarchical path (e.g., "/path/to/resource").</summary>
        public string Path { get; }

        /// <summary>Parameters for the last path segment.</summary>
        public string Params { get; }

        /// <summary>Query string (e.g., "q=1&amp;b=2").</summary>
        public string Query { get; }

        /// <summary>Fragment identifier (e.g., "section1").</summary>
        public string Fragment { get; }

        /// <summary>
        /// Creates a new ParseResult.
        /// </summary>
        public ParseResult(string scheme, string netloc, string path, string @params, string query, string fragment)
        {
            Scheme = scheme ?? "";
            Netloc = netloc ?? "";
            Path = path ?? "";
            Params = @params ?? "";
            Query = query ?? "";
            Fragment = fragment ?? "";
        }

        /// <summary>
        /// Reconstruct the URL from its components.
        /// </summary>
        public string Geturl()
        {
            return UrllibModule.Urlunparse(this);
        }

        /// <summary>
        /// The hostname portion of netloc, lowercased.
        /// </summary>
        public string? Hostname
        {
            get
            {
                if (string.IsNullOrEmpty(Netloc))
                {
                    return null;
                }

                string host = Netloc;
                int atIndex = host.IndexOf('@');
                if (atIndex >= 0)
                {
                    host = host.Substring(atIndex + 1);
                }

                int colonIndex = host.LastIndexOf(':');
                if (colonIndex >= 0)
                {
                    host = host.Substring(0, colonIndex);
                }

                return host.Length > 0 ? host.ToLowerInvariant() : null;
            }
        }

        /// <summary>
        /// The port portion of netloc as an integer, or null if not present.
        /// </summary>
        public int? Port
        {
            get
            {
                if (string.IsNullOrEmpty(Netloc))
                {
                    return null;
                }

                string host = Netloc;
                int atIndex = host.IndexOf('@');
                if (atIndex >= 0)
                {
                    host = host.Substring(atIndex + 1);
                }

                int colonIndex = host.LastIndexOf(':');
                if (colonIndex >= 0)
                {
                    string portStr = host.Substring(colonIndex + 1);
                    if (int.TryParse(portStr, out int port))
                    {
                        return port;
                    }
                }

                return null;
            }
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return "ParseResult(scheme='" + Scheme + "', netloc='" + Netloc + "', path='" + Path +
                   "', params='" + Params + "', query='" + Query + "', fragment='" + Fragment + "')";
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            if (obj is ParseResult other)
            {
                return Scheme == other.Scheme && Netloc == other.Netloc && Path == other.Path &&
                       Params == other.Params && Query == other.Query && Fragment == other.Fragment;
            }

            return false;
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            int hash = 17;
            hash = hash * 31 + (Scheme?.GetHashCode() ?? 0);
            hash = hash * 31 + (Netloc?.GetHashCode() ?? 0);
            hash = hash * 31 + (Path?.GetHashCode() ?? 0);
            hash = hash * 31 + (Params?.GetHashCode() ?? 0);
            hash = hash * 31 + (Query?.GetHashCode() ?? 0);
            hash = hash * 31 + (Fragment?.GetHashCode() ?? 0);
            return hash;
        }
    }
}
