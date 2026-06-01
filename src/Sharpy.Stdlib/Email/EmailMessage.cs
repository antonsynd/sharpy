using System;
using System.Collections.Generic;

namespace Sharpy
{
    /// <summary>
    /// Email message with headers and body.
    /// Equivalent to Python's <c>email.message.EmailMessage</c>.
    /// </summary>
    [SharpyModuleType("email", "EmailMessage")]
    public sealed partial class EmailMessage
    {
        private readonly System.Collections.Generic.List<(string name, string value)> _headers =
            new System.Collections.Generic.List<(string, string)>();
        private string? _body;
        private readonly System.Collections.Generic.List<Attachment> _attachments =
            new System.Collections.Generic.List<Attachment>();
        private string _contentType = "text/plain";
        private string? _boundary;
        private bool _isMultipart;

        public EmailMessage() { }

        // Dict-like header access

        public string? GetItem(string name)
        {
            for (int i = 0; i < _headers.Count; i++)
            {
                if (string.Equals(_headers[i].name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return _headers[i].value;
                }
            }
            return null;
        }

        public void SetItem(string name, string value)
        {
            for (int i = 0; i < _headers.Count; i++)
            {
                if (string.Equals(_headers[i].name, name, StringComparison.OrdinalIgnoreCase))
                {
                    _headers[i] = (name, value);
                    return;
                }
            }
            _headers.Add((name, value));
        }

        public void DelItem(string name)
        {
            _headers.RemoveAll(h => string.Equals(h.name, name, StringComparison.OrdinalIgnoreCase));
        }

        public bool Contains(string name)
        {
            for (int i = 0; i < _headers.Count; i++)
            {
                if (string.Equals(_headers[i].name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        public List<string> Keys()
        {
            var result = new List<string>();
            for (int i = 0; i < _headers.Count; i++)
            {
                result.Append(_headers[i].name);
            }
            return result;
        }

        public List<string> Values()
        {
            var result = new List<string>();
            for (int i = 0; i < _headers.Count; i++)
            {
                result.Append(_headers[i].value);
            }
            return result;
        }

        public List<(string, string)> Items()
        {
            var result = new List<(string, string)>();
            for (int i = 0; i < _headers.Count; i++)
            {
                result.Append((_headers[i].name, _headers[i].value));
            }
            return result;
        }

        public List<string>? GetAll(string name)
        {
            var result = new System.Collections.Generic.List<string>();
            for (int i = 0; i < _headers.Count; i++)
            {
                if (string.Equals(_headers[i].name, name, StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(_headers[i].value);
                }
            }
            return result.Count > 0 ? new List<string>(result) : null;
        }

        public void AddHeader(string name, string value)
        {
            _headers.Add((name, value));
        }

        public void ReplaceHeader(string name, string value)
        {
            for (int i = 0; i < _headers.Count; i++)
            {
                if (string.Equals(_headers[i].name, name, StringComparison.OrdinalIgnoreCase))
                {
                    _headers[i] = (name, value);
                    return;
                }
            }
            _headers.Add((name, value));
        }
    }

    /// <summary>An email attachment with binary data.</summary>
    public sealed class Attachment
    {
        public Bytes Data { get; }
        public string ContentType { get; }
        public string? Filename { get; }

        internal Attachment(Bytes data, string contentType, string? filename)
        {
            Data = data;
            ContentType = contentType;
            Filename = filename;
        }
    }
}
