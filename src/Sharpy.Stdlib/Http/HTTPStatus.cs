using System;
using System.Collections.Generic;

namespace Sharpy
{
    /// <summary>
    /// HTTP status codes with phrase descriptions.
    /// Equivalent to Python's <c>http.HTTPStatus</c>.
    /// </summary>
    [SharpyModuleType("http", "HTTPStatus")]
    public sealed class HTTPStatus : IEquatable<HTTPStatus>
    {
        private static readonly Dictionary<int, HTTPStatus> _valueMap = new Dictionary<int, HTTPStatus>();

        public int Value { get; }
        public string Name { get; }
        public string Phrase { get; }

        private HTTPStatus(int value, string name, string phrase)
        {
            Value = value;
            Name = name;
            Phrase = phrase;
            _valueMap[value] = this;
        }

        public static HTTPStatus FromValue(int value)
        {
            if (_valueMap.TryGetValue(value, out var status))
            {
                return status;
            }
            throw new ValueError("" + value + " is not a valid HTTPStatus");
        }

        public static implicit operator int(HTTPStatus status) => status.Value;

        public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);

        public override bool Equals(object? obj) => obj is HTTPStatus other && Value == other.Value;
        public bool Equals(HTTPStatus? other) => other is not null && Value == other.Value;
        public override int GetHashCode() => Value;

        // 1xx Informational
        public static readonly HTTPStatus CONTINUE = new HTTPStatus(100, "CONTINUE", "Continue");
        public static readonly HTTPStatus SWITCHING_PROTOCOLS = new HTTPStatus(101, "SWITCHING_PROTOCOLS", "Switching Protocols");
        public static readonly HTTPStatus PROCESSING = new HTTPStatus(102, "PROCESSING", "Processing");
        public static readonly HTTPStatus EARLY_HINTS = new HTTPStatus(103, "EARLY_HINTS", "Early Hints");

        // 2xx Success
        public static readonly HTTPStatus OK = new HTTPStatus(200, "OK", "OK");
        public static readonly HTTPStatus CREATED = new HTTPStatus(201, "CREATED", "Created");
        public static readonly HTTPStatus ACCEPTED = new HTTPStatus(202, "ACCEPTED", "Accepted");
        public static readonly HTTPStatus NON_AUTHORITATIVE_INFORMATION = new HTTPStatus(203, "NON_AUTHORITATIVE_INFORMATION", "Non-Authoritative Information");
        public static readonly HTTPStatus NO_CONTENT = new HTTPStatus(204, "NO_CONTENT", "No Content");
        public static readonly HTTPStatus RESET_CONTENT = new HTTPStatus(205, "RESET_CONTENT", "Reset Content");
        public static readonly HTTPStatus PARTIAL_CONTENT = new HTTPStatus(206, "PARTIAL_CONTENT", "Partial Content");
        public static readonly HTTPStatus MULTI_STATUS = new HTTPStatus(207, "MULTI_STATUS", "Multi-Status");
        public static readonly HTTPStatus ALREADY_REPORTED = new HTTPStatus(208, "ALREADY_REPORTED", "Already Reported");
        public static readonly HTTPStatus IM_USED = new HTTPStatus(226, "IM_USED", "IM Used");

        // 3xx Redirection
        public static readonly HTTPStatus MULTIPLE_CHOICES = new HTTPStatus(300, "MULTIPLE_CHOICES", "Multiple Choices");
        public static readonly HTTPStatus MOVED_PERMANENTLY = new HTTPStatus(301, "MOVED_PERMANENTLY", "Moved Permanently");
        public static readonly HTTPStatus FOUND = new HTTPStatus(302, "FOUND", "Found");
        public static readonly HTTPStatus SEE_OTHER = new HTTPStatus(303, "SEE_OTHER", "See Other");
        public static readonly HTTPStatus NOT_MODIFIED = new HTTPStatus(304, "NOT_MODIFIED", "Not Modified");
        public static readonly HTTPStatus USE_PROXY = new HTTPStatus(305, "USE_PROXY", "Use Proxy");
        public static readonly HTTPStatus TEMPORARY_REDIRECT = new HTTPStatus(307, "TEMPORARY_REDIRECT", "Temporary Redirect");
        public static readonly HTTPStatus PERMANENT_REDIRECT = new HTTPStatus(308, "PERMANENT_REDIRECT", "Permanent Redirect");

        // 4xx Client Error
        public static readonly HTTPStatus BAD_REQUEST = new HTTPStatus(400, "BAD_REQUEST", "Bad Request");
        public static readonly HTTPStatus UNAUTHORIZED = new HTTPStatus(401, "UNAUTHORIZED", "Unauthorized");
        public static readonly HTTPStatus PAYMENT_REQUIRED = new HTTPStatus(402, "PAYMENT_REQUIRED", "Payment Required");
        public static readonly HTTPStatus FORBIDDEN = new HTTPStatus(403, "FORBIDDEN", "Forbidden");
        public static readonly HTTPStatus NOT_FOUND = new HTTPStatus(404, "NOT_FOUND", "Not Found");
        public static readonly HTTPStatus METHOD_NOT_ALLOWED = new HTTPStatus(405, "METHOD_NOT_ALLOWED", "Method Not Allowed");
        public static readonly HTTPStatus NOT_ACCEPTABLE = new HTTPStatus(406, "NOT_ACCEPTABLE", "Not Acceptable");
        public static readonly HTTPStatus PROXY_AUTHENTICATION_REQUIRED = new HTTPStatus(407, "PROXY_AUTHENTICATION_REQUIRED", "Proxy Authentication Required");
        public static readonly HTTPStatus REQUEST_TIMEOUT = new HTTPStatus(408, "REQUEST_TIMEOUT", "Request Timeout");
        public static readonly HTTPStatus CONFLICT = new HTTPStatus(409, "CONFLICT", "Conflict");
        public static readonly HTTPStatus GONE = new HTTPStatus(410, "GONE", "Gone");
        public static readonly HTTPStatus LENGTH_REQUIRED = new HTTPStatus(411, "LENGTH_REQUIRED", "Length Required");
        public static readonly HTTPStatus PRECONDITION_FAILED = new HTTPStatus(412, "PRECONDITION_FAILED", "Precondition Failed");
        public static readonly HTTPStatus CONTENT_TOO_LARGE = new HTTPStatus(413, "CONTENT_TOO_LARGE", "Content Too Large");
        public static readonly HTTPStatus URI_TOO_LONG = new HTTPStatus(414, "URI_TOO_LONG", "URI Too Long");
        public static readonly HTTPStatus UNSUPPORTED_MEDIA_TYPE = new HTTPStatus(415, "UNSUPPORTED_MEDIA_TYPE", "Unsupported Media Type");
        public static readonly HTTPStatus RANGE_NOT_SATISFIABLE = new HTTPStatus(416, "RANGE_NOT_SATISFIABLE", "Range Not Satisfiable");
        public static readonly HTTPStatus EXPECTATION_FAILED = new HTTPStatus(417, "EXPECTATION_FAILED", "Expectation Failed");
        public static readonly HTTPStatus IM_A_TEAPOT = new HTTPStatus(418, "IM_A_TEAPOT", "I'm a Teapot");
        public static readonly HTTPStatus MISDIRECTED_REQUEST = new HTTPStatus(421, "MISDIRECTED_REQUEST", "Misdirected Request");
        public static readonly HTTPStatus UNPROCESSABLE_CONTENT = new HTTPStatus(422, "UNPROCESSABLE_CONTENT", "Unprocessable Content");
        public static readonly HTTPStatus LOCKED = new HTTPStatus(423, "LOCKED", "Locked");
        public static readonly HTTPStatus FAILED_DEPENDENCY = new HTTPStatus(424, "FAILED_DEPENDENCY", "Failed Dependency");
        public static readonly HTTPStatus TOO_EARLY = new HTTPStatus(425, "TOO_EARLY", "Too Early");
        public static readonly HTTPStatus UPGRADE_REQUIRED = new HTTPStatus(426, "UPGRADE_REQUIRED", "Upgrade Required");
        public static readonly HTTPStatus PRECONDITION_REQUIRED = new HTTPStatus(428, "PRECONDITION_REQUIRED", "Precondition Required");
        public static readonly HTTPStatus TOO_MANY_REQUESTS = new HTTPStatus(429, "TOO_MANY_REQUESTS", "Too Many Requests");
        public static readonly HTTPStatus REQUEST_HEADER_FIELDS_TOO_LARGE = new HTTPStatus(431, "REQUEST_HEADER_FIELDS_TOO_LARGE", "Request Header Fields Too Large");
        public static readonly HTTPStatus UNAVAILABLE_FOR_LEGAL_REASONS = new HTTPStatus(451, "UNAVAILABLE_FOR_LEGAL_REASONS", "Unavailable For Legal Reasons");

        // 5xx Server Error
        public static readonly HTTPStatus INTERNAL_SERVER_ERROR = new HTTPStatus(500, "INTERNAL_SERVER_ERROR", "Internal Server Error");
        public static readonly HTTPStatus NOT_IMPLEMENTED = new HTTPStatus(501, "NOT_IMPLEMENTED", "Not Implemented");
        public static readonly HTTPStatus BAD_GATEWAY = new HTTPStatus(502, "BAD_GATEWAY", "Bad Gateway");
        public static readonly HTTPStatus SERVICE_UNAVAILABLE = new HTTPStatus(503, "SERVICE_UNAVAILABLE", "Service Unavailable");
        public static readonly HTTPStatus GATEWAY_TIMEOUT = new HTTPStatus(504, "GATEWAY_TIMEOUT", "Gateway Timeout");
        public static readonly HTTPStatus HTTP_VERSION_NOT_SUPPORTED = new HTTPStatus(505, "HTTP_VERSION_NOT_SUPPORTED", "HTTP Version Not Supported");
        public static readonly HTTPStatus VARIANT_ALSO_NEGOTIATES = new HTTPStatus(506, "VARIANT_ALSO_NEGOTIATES", "Variant Also Negotiates");
        public static readonly HTTPStatus INSUFFICIENT_STORAGE = new HTTPStatus(507, "INSUFFICIENT_STORAGE", "Insufficient Storage");
        public static readonly HTTPStatus LOOP_DETECTED = new HTTPStatus(508, "LOOP_DETECTED", "Loop Detected");
        public static readonly HTTPStatus NOT_EXTENDED = new HTTPStatus(510, "NOT_EXTENDED", "Not Extended");
        public static readonly HTTPStatus NETWORK_AUTHENTICATION_REQUIRED = new HTTPStatus(511, "NETWORK_AUTHENTICATION_REQUIRED", "Network Authentication Required");
    }
}
