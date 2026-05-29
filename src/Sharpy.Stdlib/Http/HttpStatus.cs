namespace Sharpy
{
    /// <summary>
    /// HTTP status codes, equivalent to Python's <c>http.HTTPStatus</c>.
    /// Each member has a numeric value equal to the HTTP status code.
    /// </summary>
    [SharpyModuleType("http")]
    public enum HTTPStatus
    {
        // 1xx Informational
        /// <summary>100 Continue</summary>
        CONTINUE = 100,
        /// <summary>101 Switching Protocols</summary>
        SWITCHING_PROTOCOLS = 101,
        /// <summary>102 Processing</summary>
        PROCESSING = 102,
        /// <summary>103 Early Hints</summary>
        EARLY_HINTS = 103,

        // 2xx Success
        /// <summary>200 OK</summary>
        OK = 200,
        /// <summary>201 Created</summary>
        CREATED = 201,
        /// <summary>202 Accepted</summary>
        ACCEPTED = 202,
        /// <summary>203 Non-Authoritative Information</summary>
        NON_AUTHORITATIVE_INFORMATION = 203,
        /// <summary>204 No Content</summary>
        NO_CONTENT = 204,
        /// <summary>205 Reset Content</summary>
        RESET_CONTENT = 205,
        /// <summary>206 Partial Content</summary>
        PARTIAL_CONTENT = 206,
        /// <summary>207 Multi-Status</summary>
        MULTI_STATUS = 207,
        /// <summary>208 Already Reported</summary>
        ALREADY_REPORTED = 208,
        /// <summary>226 IM Used</summary>
        IM_USED = 226,

        // 3xx Redirection
        /// <summary>300 Multiple Choices</summary>
        MULTIPLE_CHOICES = 300,
        /// <summary>301 Moved Permanently</summary>
        MOVED_PERMANENTLY = 301,
        /// <summary>302 Found</summary>
        FOUND = 302,
        /// <summary>303 See Other</summary>
        SEE_OTHER = 303,
        /// <summary>304 Not Modified</summary>
        NOT_MODIFIED = 304,
        /// <summary>305 Use Proxy</summary>
        USE_PROXY = 305,
        /// <summary>307 Temporary Redirect</summary>
        TEMPORARY_REDIRECT = 307,
        /// <summary>308 Permanent Redirect</summary>
        PERMANENT_REDIRECT = 308,

        // 4xx Client Error
        /// <summary>400 Bad Request</summary>
        BAD_REQUEST = 400,
        /// <summary>401 Unauthorized</summary>
        UNAUTHORIZED = 401,
        /// <summary>402 Payment Required</summary>
        PAYMENT_REQUIRED = 402,
        /// <summary>403 Forbidden</summary>
        FORBIDDEN = 403,
        /// <summary>404 Not Found</summary>
        NOT_FOUND = 404,
        /// <summary>405 Method Not Allowed</summary>
        METHOD_NOT_ALLOWED = 405,
        /// <summary>406 Not Acceptable</summary>
        NOT_ACCEPTABLE = 406,
        /// <summary>407 Proxy Authentication Required</summary>
        PROXY_AUTHENTICATION_REQUIRED = 407,
        /// <summary>408 Request Timeout</summary>
        REQUEST_TIMEOUT = 408,
        /// <summary>409 Conflict</summary>
        CONFLICT = 409,
        /// <summary>410 Gone</summary>
        GONE = 410,
        /// <summary>411 Length Required</summary>
        LENGTH_REQUIRED = 411,
        /// <summary>412 Precondition Failed</summary>
        PRECONDITION_FAILED = 412,
        /// <summary>413 Content Too Large</summary>
        CONTENT_TOO_LARGE = 413,
        /// <summary>414 URI Too Long</summary>
        URI_TOO_LONG = 414,
        /// <summary>415 Unsupported Media Type</summary>
        UNSUPPORTED_MEDIA_TYPE = 415,
        /// <summary>416 Range Not Satisfiable</summary>
        RANGE_NOT_SATISFIABLE = 416,
        /// <summary>417 Expectation Failed</summary>
        EXPECTATION_FAILED = 417,
        /// <summary>418 I'm a Teapot</summary>
        IM_A_TEAPOT = 418,
        /// <summary>421 Misdirected Request</summary>
        MISDIRECTED_REQUEST = 421,
        /// <summary>422 Unprocessable Content</summary>
        UNPROCESSABLE_CONTENT = 422,
        /// <summary>423 Locked</summary>
        LOCKED = 423,
        /// <summary>424 Failed Dependency</summary>
        FAILED_DEPENDENCY = 424,
        /// <summary>425 Too Early</summary>
        TOO_EARLY = 425,
        /// <summary>426 Upgrade Required</summary>
        UPGRADE_REQUIRED = 426,
        /// <summary>428 Precondition Required</summary>
        PRECONDITION_REQUIRED = 428,
        /// <summary>429 Too Many Requests</summary>
        TOO_MANY_REQUESTS = 429,
        /// <summary>431 Request Header Fields Too Large</summary>
        REQUEST_HEADER_FIELDS_TOO_LARGE = 431,
        /// <summary>451 Unavailable For Legal Reasons</summary>
        UNAVAILABLE_FOR_LEGAL_REASONS = 451,

        // 5xx Server Error
        /// <summary>500 Internal Server Error</summary>
        INTERNAL_SERVER_ERROR = 500,
        /// <summary>501 Not Implemented</summary>
        NOT_IMPLEMENTED = 501,
        /// <summary>502 Bad Gateway</summary>
        BAD_GATEWAY = 502,
        /// <summary>503 Service Unavailable</summary>
        SERVICE_UNAVAILABLE = 503,
        /// <summary>504 Gateway Timeout</summary>
        GATEWAY_TIMEOUT = 504,
        /// <summary>505 HTTP Version Not Supported</summary>
        HTTP_VERSION_NOT_SUPPORTED = 505,
        /// <summary>506 Variant Also Negotiates</summary>
        VARIANT_ALSO_NEGOTIATES = 506,
        /// <summary>507 Insufficient Storage</summary>
        INSUFFICIENT_STORAGE = 507,
        /// <summary>508 Loop Detected</summary>
        LOOP_DETECTED = 508,
        /// <summary>510 Not Extended</summary>
        NOT_EXTENDED = 510,
        /// <summary>511 Network Authentication Required</summary>
        NETWORK_AUTHENTICATION_REQUIRED = 511,
    }
}
