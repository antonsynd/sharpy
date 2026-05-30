using System;

namespace Sharpy
{
    /// <summary>
    /// Result of <see cref="UrllibModule.Urlparse"/>. Contains the six components of a
    /// parsed URL: scheme, netloc, path, params, query, and fragment.
    /// </summary>
    [SharpyModuleType("urllib")]
    public sealed class ParseResult : IEquatable<ParseResult>
    {
        /// <summary>URL scheme specifier (e.g. "https").</summary>
        public string Scheme { get; }

        /// <summary>Network location part (e.g. "user:pass@host:8080").</summary>
        public string Netloc { get; }

        /// <summary>Hierarchical path (e.g. "/index.html").</summary>
        public string Path { get; }

        /// <summary>Parameters for the last path element (text after semicolon).</summary>
        public string Params { get; }

        /// <summary>Query component (text after '?').</summary>
        public string Query { get; }

        /// <summary>Fragment identifier (text after '#').</summary>
        public string Fragment { get; }

        /// <summary>Lowercase hostname extracted from <see cref="Netloc"/>, or null.</summary>
        public string? Hostname
        {
            get
            {
                if (string.IsNullOrEmpty(Netloc))
                {
                    return null;
                }

                string host = Netloc;

                // Strip userinfo
                int atIdx = host.LastIndexOf('@');
                if (atIdx >= 0)
                {
                    host = host.Substring(atIdx + 1);
                }

                // Handle IPv6 brackets before port stripping
                if (host.StartsWith("["))
                {
                    int closeBracket = host.IndexOf(']');
                    if (closeBracket >= 0)
                    {
                        host = host.Substring(1, closeBracket - 1);
                    }
                }
                else
                {
                    // Strip port (only for non-IPv6)
                    int colonIdx = host.LastIndexOf(':');
                    if (colonIdx >= 0)
                    {
                        host = host.Substring(0, colonIdx);
                    }
                }

                return host.Length > 0 ? host.ToLowerInvariant() : null;
            }
        }

        /// <summary>Port number extracted from <see cref="Netloc"/>, or null.</summary>
        public int? Port
        {
            get
            {
                if (string.IsNullOrEmpty(Netloc))
                {
                    return null;
                }

                string host = Netloc;

                // Strip userinfo
                int atIdx = host.LastIndexOf('@');
                if (atIdx >= 0)
                {
                    host = host.Substring(atIdx + 1);
                }

                // For IPv6, port is after the closing bracket
                if (host.StartsWith("["))
                {
                    int closeBracket = host.IndexOf(']');
                    if (closeBracket >= 0 && closeBracket + 1 < host.Length && host[closeBracket + 1] == ':')
                    {
                        string portStr = host.Substring(closeBracket + 2);
                        if (int.TryParse(portStr, out int port) && port >= 0 && port <= 65535)
                        {
                            return port;
                        }
                    }

                    return null;
                }

                int colonIdx = host.LastIndexOf(':');
                if (colonIdx >= 0)
                {
                    string portStr = host.Substring(colonIdx + 1);
                    if (int.TryParse(portStr, out int port) && port >= 0 && port <= 65535)
                    {
                        return port;
                    }
                }

                return null;
            }
        }

        /// <summary>Username extracted from <see cref="Netloc"/>, or null.</summary>
        public string? Username
        {
            get
            {
                if (string.IsNullOrEmpty(Netloc))
                {
                    return null;
                }

                int atIdx = Netloc.LastIndexOf('@');
                if (atIdx < 0)
                {
                    return null;
                }

                string userinfo = Netloc.Substring(0, atIdx);
                int colonIdx = userinfo.IndexOf(':');
                return colonIdx >= 0 ? userinfo.Substring(0, colonIdx) : userinfo;
            }
        }

        /// <summary>Password extracted from <see cref="Netloc"/>, or null.</summary>
        public string? Password
        {
            get
            {
                if (string.IsNullOrEmpty(Netloc))
                {
                    return null;
                }

                int atIdx = Netloc.LastIndexOf('@');
                if (atIdx < 0)
                {
                    return null;
                }

                string userinfo = Netloc.Substring(0, atIdx);
                int colonIdx = userinfo.IndexOf(':');
                return colonIdx >= 0 ? userinfo.Substring(colonIdx + 1) : null;
            }
        }

        /// <summary>Create a ParseResult from the six URL components.</summary>
        public ParseResult(string scheme, string netloc, string path, string @params, string query, string fragment)
        {
            Scheme = scheme;
            Netloc = netloc;
            Path = path;
            Params = @params;
            Query = query;
            Fragment = fragment;
        }

        /// <summary>Reassemble the URL from its components.</summary>
        public string Geturl()
        {
            return UrllibModule.Urlunparse(this);
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return "ParseResult(scheme='" + Scheme + "', netloc='" + Netloc +
                   "', path='" + Path + "', params='" + Params +
                   "', query='" + Query + "', fragment='" + Fragment + "')";
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return obj is ParseResult other && Equals(other);
        }

        /// <inheritdoc />
        public bool Equals(ParseResult? other)
        {
            if (other is null)
            {
                return false;
            }

            return Scheme == other.Scheme &&
                   Netloc == other.Netloc &&
                   Path == other.Path &&
                   Params == other.Params &&
                   Query == other.Query &&
                   Fragment == other.Fragment;
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
#if NET10_0_OR_GREATER
            return HashCode.Combine(Scheme, Netloc, Path, Params, Query, Fragment);
#else
            unchecked
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
#endif
        }
    }
}
